using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Unit-Tests für PromptZeitVersandService.</summary>
public sealed class PromptZeitVersandServiceTests : IDisposable
{
    private readonly KiAusfuehrungsService _kiService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly PromptZeitVersandService _sut;

    /// <summary>PromptZeitVersandServiceTests.</summary>
    public PromptZeitVersandServiceTests()
    {
        _kiService = TestKiAusfuehrungsServiceFactory.Create();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _sut = new PromptZeitVersandService(_kiService, _timeProvider, NullLogger<PromptZeitVersandService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _kiService.Dispose();

    /// <summary>
    /// Registriert für eine neue Aufgabe eine gültige <see cref="PseudoConsoleSession"/> beim
    /// <see cref="KiAusfuehrungsService"/>. Nutzt bewusst den klassischen <c>StartCliAsync</c>-Pfad mit einem
    /// lange laufenden Ping-Kommando (statt ConPTY mit einer per Tastatureingabe simulierten Shell): Die äußere
    /// ConPTY-<c>cmd.exe</c>-Shell dieser Sandbox kann sich (siehe CLAUDE.md, ConPTY-Isolationsproblem)
    /// unvorhersehbar innerhalb weniger Sekunden selbst beenden, was das Handle aus der internen
    /// Verwaltung entfernt und Tests, die eine über mehrere <c>FakeTimeProvider.Advance</c>-Aufrufe hinweg
    /// gültige Session voraussetzen, nichtdeterministisch scheitern lässt. Der Ping-Prozess bleibt dagegen
    /// zuverlässig für die Testdauer aktiv; die <see cref="PseudoConsoleSession"/> wird manuell an das Handle
    /// angehängt, damit <see cref="KiAusfuehrungsService.GetTerminalSession"/> sie zurückgibt.
    /// </summary>
    /// <returns>Die ID der Aufgabe, für die eine Session registriert wurde.</returns>
    private Task<Guid> StartCliSessionAsync()
    {
        var aufgabeId = Guid.NewGuid();
        RegisterSession(aufgabeId);
        return Task.FromResult(aufgabeId);
    }

    /// <summary>Registriert eine Pseudo-Console-Session für die übergebene Aufgaben-ID (mit vorgegebener ID, damit die Aufgabe zusätzlich in einer Test-DB seeded werden kann).</summary>
    private void RegisterSession(Guid aufgabeId)
        => RegisterSession(aufgabeId, TestPseudoConsoleSessionFactory.Create(new MemoryStream(), new MemoryStream()));

    /// <summary>Registriert eine beliebige <see cref="ITerminalSession"/> für die übergebene Aufgaben-ID.</summary>
    private void RegisterSession(Guid aufgabeId, ITerminalSession session)
    {
        var handle = new CliProcessHandle(aufgabeId, System.Diagnostics.Process.GetCurrentProcess())
        {
            Session = session
        };

        var handlesField = typeof(KiAusfuehrungsService).GetField("_handles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_kiService)!;
        handles[aufgabeId] = handle;
    }

    /// <summary>Löst das <see cref="KiAusfuehrungsService.CliProcessStatusChanged"/>-Event testseitig über das Backing-Field aus.</summary>
    private void RaiseCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)
    {
        var eventField = typeof(KiAusfuehrungsService).GetField("CliProcessStatusChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((Action<Guid, CliProcessStatus>?)eventField.GetValue(_kiService))?.Invoke(aufgabeId, status);
    }

    /// <summary>Wartet aktiv, bis die Bedingung erfüllt ist (für Fire-and-forget-Abläufe ohne Event-Signal).</summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> bedingung, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (await bedingung())
                return;
            await Task.Delay(50);
        }
    }

    /// <summary>Erstellt einen ServiceProvider mit InMemory-DbContext und seedet eine Aufgabe mit dem übergebenen PausiertBisUtc-Wert.</summary>
    private async Task<ServiceProvider> CreatePausedTaskProviderAsync(DateTimeOffset? pausiertBisUtc, Guid aufgabeId)
    {
        // Der Datenbankname muss AUSSERHALB des Options-Lambdas erzeugt werden: AddDbContext führt
        // die Options-Action bei jeder Context-Auflösung erneut aus — ein Guid.NewGuid() im Lambda
        // ergäbe pro Scope eine andere (leere) InMemory-Datenbank.
        var databaseName = Guid.NewGuid().ToString();
        var provider = new ServiceCollection()
            .AddDbContext<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>(options =>
                options.UseInMemoryDatabase(databaseName))
            .AddScoped<TodoService>()
            .AddScoped<AufgabeService>()
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<TodoService>>(NullLogger<TodoService>.Instance)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<AufgabeService>>(NullLogger<AufgabeService>.Instance)
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
        var projektId = Guid.NewGuid();
        db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = projektId,
            Name = "Prompt-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = Softwareschmiede.Domain.Enums.ProjektStatus.Aktiv
        });
        db.Aufgaben.Add(new Softwareschmiede.Domain.Entities.Aufgabe
        {
            Id = aufgabeId,
            ProjektId = projektId,
            Titel = "Pausierte Aufgabe",
            Status = Softwareschmiede.Domain.Enums.AufgabeStatus.Gestartet,
            PausiertBisUtc = pausiertBisUtc,
            ErstellungsDatum = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return provider;
    }

    /// <summary>
    /// Erstellt einen ServiceProvider mit InMemory-DbContext, <see cref="ZeitgesteuerterPromptStore"/>,
    /// <see cref="AufgabeService"/> und <see cref="ProtokollService"/> und seedet eine Aufgabe
    /// mit dem übergebenen PausiertBisUtc-Wert (Muster <see cref="CreatePausedTaskProviderAsync"/>).
    /// </summary>
    /// <param name="pausiertBisUtc">Der zu seedende PausiertBisUtc-Wert der Aufgabe.</param>
    /// <param name="aufgabeId">Die ID der zu seedenden Aufgabe.</param>
    /// <param name="mitProtokollService">Bei <c>false</c> wird <see cref="ProtokollService"/> bewusst nicht registriert (Fehlerpfad-Protokollierung).</param>
    private async Task<ServiceProvider> CreateStoreProviderAsync(DateTimeOffset? pausiertBisUtc, Guid aufgabeId, bool mitProtokollService = true)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection()
            .AddDbContext<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>(options =>
                options.UseInMemoryDatabase(databaseName))
            .AddScoped<TodoService>()
            .AddScoped<AufgabeService>()
            .AddScoped<ZeitgesteuerterPromptStore>()
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<TodoService>>(NullLogger<TodoService>.Instance)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<AufgabeService>>(NullLogger<AufgabeService>.Instance)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<ZeitgesteuerterPromptStore>>(NullLogger<ZeitgesteuerterPromptStore>.Instance);

        if (mitProtokollService)
        {
            services
                .AddScoped<ProtokollService>()
                .AddSingleton<Microsoft.Extensions.Logging.ILogger<ProtokollService>>(NullLogger<ProtokollService>.Instance);
        }

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
        var projektId = Guid.NewGuid();
        db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = projektId,
            Name = "Prompt-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = Softwareschmiede.Domain.Enums.ProjektStatus.Aktiv
        });
        db.Aufgaben.Add(new Softwareschmiede.Domain.Entities.Aufgabe
        {
            Id = aufgabeId,
            ProjektId = projektId,
            Titel = "Testaufgabe",
            Status = Softwareschmiede.Domain.Enums.AufgabeStatus.Gestartet,
            PausiertBisUtc = pausiertBisUtc,
            ErstellungsDatum = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return provider;
    }

    /// <summary>Liest die persistierten ZeitgesteuerterPrompt-Datensätze der Aufgabe über einen eigenen Scope.</summary>
    private static async Task<List<Softwareschmiede.Domain.Entities.ZeitgesteuerterPrompt>> LeseDatensaetzeAsync(ServiceProvider provider, Guid aufgabeId)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
        return await db.ZeitgesteuertePrompts.Where(z => z.AufgabeId == aufgabeId).ToListAsync();
    }

    /// <summary>Liest die Protokolleinträge der Aufgabe über einen eigenen Scope.</summary>
    private static async Task<List<Softwareschmiede.Domain.Entities.Protokolleintrag>> LeseProtokollAsync(ServiceProvider provider, Guid aufgabeId)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
        return await db.Protokolleintraege.Where(p => p.AufgabeId == aufgabeId).ToListAsync();
    }

    /// <summary>Liegt die Zielzeit in der Vergangenheit, wird der Prompt sofort versendet und es bleibt kein Eintrag in der Warteschlange.</summary>
    [Fact]
    public async Task SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort()
    {
        var aufgabeId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(-1));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "eine Zielzeit in der Vergangenheit muss sofort versendet werden");
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull("ein sofort versendeter Prompt darf keinen Eintrag in der Warteschlange hinterlassen");
    }

    /// <summary>Liegt die Zielzeit in der Zukunft, wird der Prompt gepuffert und der Status ist über GetScheduledPromptStatus abrufbar.</summary>
    [Fact]
    public async Task SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt()
    {
        var aufgabeId = Guid.NewGuid();
        var targetTime = _timeProvider.GetUtcNow().AddMinutes(5);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", targetTime);

        var status = _sut.GetScheduledPromptStatus(aufgabeId);
        status.Should().NotBeNull();
        status!.PromptText.Should().Be("Testprompt");
        status.TargetTime.Should().Be(targetTime);
    }

    /// <summary>Erreicht der FakeTimeProvider die Zielzeit, wird der Prompt automatisch versendet und PromptSent gefeuert.</summary>
    [Fact]
    public async Task Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch()
    {
        var aufgabeId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "der Timer muss beim Erreichen der Zielzeit automatisch feuern");
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull("nach dem Versand darf kein Eintrag mehr in der Warteschlange sein");
    }

    /// <summary>CancelScheduledPromptAsync entfernt den geplanten Prompt; auch nach Ablauf der Zielzeit erfolgt kein Versand mehr.</summary>
    [Fact]
    public async Task CancelScheduledPromptAsync_EntferntGeplantenPrompt()
    {
        var aufgabeId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        await _sut.CancelScheduledPromptAsync(aufgabeId);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromMilliseconds(500)));
        finished.Should().NotBe(versendet.Task, "ein stornierter Prompt darf auch nach Ablauf der Zielzeit nicht versendet werden");
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull();
    }

    /// <summary>Ein zweiter geplanter Prompt für dieselbe Aufgabe ersetzt den ersten und storniert dessen Timer.</summary>
    [Fact]
    public async Task SchedulePromptAsync_ZweiterPromptFuerSelbeAufgabe_ErsetztErsten()
    {
        var aufgabeId = Guid.NewGuid();
        var ersteZielzeit = _timeProvider.GetUtcNow().AddMinutes(5);
        var zweiteZielzeit = _timeProvider.GetUtcNow().AddMinutes(10);

        await _sut.SchedulePromptAsync(aufgabeId, "ErsterPrompt", ersteZielzeit);
        await _sut.SchedulePromptAsync(aufgabeId, "ZweiterPrompt", zweiteZielzeit);

        _sut.GetScheduledPromptStatus(aufgabeId)!.PromptText.Should().Be("ZweiterPrompt");

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _sut.GetScheduledPromptStatus(aufgabeId).Should().NotBeNull(
            "der erste (ersetzte) Timer darf zu seiner ursprünglichen Zielzeit nicht mehr feuern");
        _sut.GetScheduledPromptStatus(aufgabeId)!.PromptText.Should().Be(
            "ZweiterPrompt",
            "der Eintrag muss weiterhin der zweite, ersetzende Prompt sein");

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        _sut.GetScheduledPromptStatus(aufgabeId).Should().NotBeNull(
            "ohne zustellfähige Session bleibt der Plan des zweiten Prompts im Zustand „wartet auf Session“ bestehen");
    }

    /// <summary>Ist zur Fälligkeit keine aktive CLI-Session vorhanden, bleibt Plan und Datensatz bestehen und der Eintrag wartet auf die nächste Session.</summary>
    [Fact]
    public async Task Timer_OhneSession_BehaeltPlanUndWartetAufSession()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var versendet = false;
        sut.PromptSent += _ => versendet = true;

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        var act = () => _timeProvider.Advance(TimeSpan.FromMinutes(5));
        act.Should().NotThrow();

        await Task.Delay(TimeSpan.FromMilliseconds(500));

        versendet.Should().BeFalse("ohne aktive CLI-Session darf kein PromptSent-Event gefeuert werden");
        var status = sut.GetScheduledPromptStatus(aufgabeId);
        status.Should().NotBeNull("der Plan muss im Zustand „wartet auf Session“ erhalten bleiben");
        status!.WartetAufSession.Should().BeTrue("der Status muss den Wartezustand an die Oberfläche melden");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().HaveCount(1, "der persistierte Datensatz darf nicht verworfen werden");
    }

    /// <summary>Ist die Aufgabe zum Planungszeitpunkt bereits pausiert, wird die Zielzeit auf das Pausenende verschoben statt sofort zu senden (Issue 151).</summary>
    [Fact]
    public async Task SchedulePromptAsync_AktivePause_VerschiebtZielzeitAufPausenende()
    {
        var aufgabeId = Guid.NewGuid();
        var pausiertBis = _timeProvider.GetUtcNow().AddHours(2);
        await using var provider = await CreatePausedTaskProviderAsync(pausiertBis, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        var status = sut.GetScheduledPromptStatus(aufgabeId);
        status.Should().NotBeNull("der Prompt muss gepuffert statt sofort versendet werden");
        // BeCloseTo statt Be: PausiertBisUtc läuft durch den Unix-Millisekunden-Converter und
        // verliert dabei Sub-Millisekunden-Genauigkeit.
        status!.TargetTime.Should().BeCloseTo(pausiertBis, TimeSpan.FromSeconds(1), "die Zielzeit wird auf das Pausenende verschoben");
    }

    /// <summary>Wird die Pause NACH dem Planen gesetzt, plant der Timer-Callback den Versand auf das Pausenende um und versendet erst danach (Issue 151).</summary>
    [Fact]
    public async Task Timer_BeiNachPlanungGesetzterPause_VerschiebtVersandAufPausenende()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreatePausedTaskProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(aufgabeId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        // Pause erst nach dem Planen setzen — der Timer-Callback muss sie beim Feuern erkennen.
        var pausiertBis = _timeProvider.GetUtcNow().AddHours(1);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
            (await db.Aufgaben.FindAsync(aufgabeId))!.PausiertBisUtc = pausiertBis;
            await db.SaveChangesAsync();
        }

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        versendet.Task.IsCompleted.Should().BeFalse("während einer aktiven Pause darf der Prompt nicht versendet werden");
        var status = sut.GetScheduledPromptStatus(aufgabeId);
        status.Should().NotBeNull("der Prompt muss auf das Pausenende umgeplant werden");
        // BeCloseTo statt Be: PausiertBisUtc läuft durch den Unix-Millisekunden-Converter und
        // verliert dabei Sub-Millisekunden-Genauigkeit.
        status!.TargetTime.Should().BeCloseTo(pausiertBis, TimeSpan.FromSeconds(1),
            "die gemeldete Zielzeit muss dem Pausenende entsprechen, nicht dem verstrichenen ursprünglichen Zeitpunkt");

        _timeProvider.Advance(TimeSpan.FromHours(1));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "nach dem Pausenende muss der gepufferte Prompt versendet werden");
        sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull();
    }

    /// <summary>Planen mit zukünftiger Zielzeit legt genau einen ZeitgesteuerterPrompt-Datensatz an.</summary>
    [Fact]
    public async Task SchedulePromptAsync_SchreibtDatensatz()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var zielzeit = _timeProvider.GetUtcNow().AddMinutes(5);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", zielzeit);

        var datensaetze = await LeseDatensaetzeAsync(provider, aufgabeId);
        datensaetze.Should().HaveCount(1);
        datensaetze[0].PromptText.Should().Be("Testprompt");
        datensaetze[0].AufgabeId.Should().Be(aufgabeId);
        // BeCloseTo statt Be: ZielZeitpunktUtc läuft durch den Unix-Millisekunden-Converter und
        // verliert dabei Sub-Millisekunden-Genauigkeit.
        datensaetze[0].ZielZeitpunktUtc.Should().BeCloseTo(zielzeit, TimeSpan.FromSeconds(1));
    }

    /// <summary>Eine zweite Planung derselben Aufgabe ersetzt den Datensatz (Upsert) — es bleibt genau ein Datensatz mit neuem Text und neuer Zielzeit.</summary>
    [Fact]
    public async Task SchedulePromptAsync_ErneutesPlanen_ErsetztDatensatz()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var zweiteZielzeit = _timeProvider.GetUtcNow().AddMinutes(10);

        await sut.SchedulePromptAsync(aufgabeId, "ErsterPrompt", _timeProvider.GetUtcNow().AddMinutes(5));
        await sut.SchedulePromptAsync(aufgabeId, "ZweiterPrompt", zweiteZielzeit);

        var datensaetze = await LeseDatensaetzeAsync(provider, aufgabeId);
        datensaetze.Should().HaveCount(1, "die Ersetzungsregel erlaubt maximal einen Plan pro Aufgabe");
        datensaetze[0].PromptText.Should().Be("ZweiterPrompt");
        datensaetze[0].ZielZeitpunktUtc.Should().BeCloseTo(zweiteZielzeit, TimeSpan.FromSeconds(1));
    }

    /// <summary>Nach erfolgreichem Versand sind Datensatz und In-Memory-Eintrag entfernt.</summary>
    [Fact]
    public async Task Versand_LoeschtDatensatz()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(aufgabeId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().HaveCount(1);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task);
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty("der Datensatz muss nach erfolgreichem Versand gelöscht sein");
        sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull();
    }

    /// <summary>Storno entfernt den persistierten Datensatz und den In-Memory-Eintrag.</summary>
    [Fact]
    public async Task CancelScheduledPromptAsync_LoeschtDatensatz()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().HaveCount(1);

        await sut.CancelScheduledPromptAsync(aufgabeId);

        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty();
        sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull();
    }

    /// <summary>Schlägt WritePromptAsync mit einem sonstigen Fehler fehl, bleiben Plan und Datensatz bestehen; der nächste CLI-Start stellt zu.</summary>
    [Fact]
    public async Task Sendefehler_BehaeltPlanUndWartetAufSession()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        var fehlerhafteSession = new Mock<ITerminalSession>();
        fehlerhafteSession
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sendefehler"));
        RegisterSession(aufgabeId, fehlerhafteSession.Object);

        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        versendet.Task.IsCompleted.Should().BeFalse("ein Sendefehler darf kein PromptSent-Event auslösen");
        sut.GetScheduledPromptStatus(aufgabeId).Should().NotBeNull("der Plan muss im Zustand „wartet auf Session“ erhalten bleiben");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().HaveCount(1);

        // Nächster Session-Start mit funktionierender Session → Nachzustellung.
        RegisterSession(aufgabeId);
        RaiseCliProcessStatusChanged(aufgabeId, CliProcessStatus.Gestartet);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "beim nächsten Session-Start muss der wartende Plan zugestellt werden");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty();
    }

    /// <summary>Ein CLI-Start (CliProcessStatusChanged Gestartet) stellt einen wartenden/fälligen Plan nach und löscht den Datensatz.</summary>
    [Fact]
    public async Task CliGestartet_MitWartendemPlan_ZustelltNach()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        // Ohne Session fällig werden lassen → Eintrag wartet auf Session.
        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        sut.GetScheduledPromptStatus(aufgabeId).Should().NotBeNull();

        RegisterSession(aufgabeId);
        RaiseCliProcessStatusChanged(aufgabeId, CliProcessStatus.Gestartet);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "der Session-Start muss den wartenden Plan zustellen");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty();
        sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull();
    }

    /// <summary>Die Nachzustellung über einen Session-Start schreibt immer einen SystemMeldung-Protokolleintrag — auch mit PromptSent-Abonnenten.</summary>
    [Fact]
    public async Task Nachzustellung_SchreibtSystemMeldung()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        RegisterSession(aufgabeId);
        RaiseCliProcessStatusChanged(aufgabeId, CliProcessStatus.Gestartet);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task);
        await WaitUntilAsync(async () => (await LeseProtokollAsync(provider, aufgabeId)).Count > 0);
        (await LeseProtokollAsync(provider, aufgabeId))
            .Should().ContainSingle(p => p.Typ == ProtokollTyp.SystemMeldung,
                "die Nachzustellung muss im Aufgabenprotokoll dokumentiert werden");
    }

    /// <summary>Ein Timer-Versand ohne PromptSent-Abonnenten (keine geöffnete Detailansicht) schreibt einen SystemMeldung-Eintrag.</summary>
    [Fact]
    public async Task Versand_OhneAbonnent_SchreibtSystemMeldung()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(aufgabeId);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        await WaitUntilAsync(async () => (await LeseProtokollAsync(provider, aufgabeId)).Count > 0);
        (await LeseProtokollAsync(provider, aufgabeId))
            .Should().ContainSingle(p => p.Typ == ProtokollTyp.SystemMeldung,
                "der Hintergrundversand ohne geöffnete Detailansicht muss protokolliert werden");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty();
    }

    /// <summary>Ein Timer-Versand mit PromptSent-Abonnenten (geöffnete Detailansicht) schreibt keinen Protokolleintrag.</summary>
    [Fact]
    public async Task Versand_MitAbonnent_KeinProtokollEintrag()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(aufgabeId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        (await LeseProtokollAsync(provider, aufgabeId)).Should().BeEmpty(
            "bei beobachtetem Versand (Detailansicht offen) darf kein Protokolleintrag entstehen");
    }

    /// <summary>Schlägt das Schreiben des Protokolleintrags fehl, bleibt der erfolgreiche Versand gültig: Datensatz gelöscht, PromptSent gefeuert.</summary>
    [Fact]
    public async Task ProtokollEintrag_BeiGeloeschterAufgabe_BrichtVersandNicht()
    {
        var aufgabeId = Guid.NewGuid();
        // Ohne registrierten ProtokollService schlägt die Protokoll-Auflösung fehl — der
        // Versandpfad darf dadurch nicht brechen (gleicher Schutzpfad wie eine gelöschte Aufgabe).
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId, mitProtokollService: false);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(aufgabeId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "ein fehlschlagender Protokolleintrag darf den Versand nicht brechen");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty();
        sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull();
    }

    /// <summary>Die Pause-Verschiebung im Timer-Callback aktualisiert ZielZeitpunktUtc des persistierten Datensatzes auf das Pausenende.</summary>
    [Fact]
    public async Task Pause_Verschiebung_AktualisiertDatensatz()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var umgeplant = new TaskCompletionSource<Guid>();
        sut.ScheduledPromptChanged += id => umgeplant.TrySetResult(id);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        var pausiertBis = _timeProvider.GetUtcNow().AddHours(1);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
            (await db.Aufgaben.FindAsync(aufgabeId))!.PausiertBisUtc = pausiertBis;
            await db.SaveChangesAsync();
        }

        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var finished = await Task.WhenAny(umgeplant.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(umgeplant.Task, "die Pause-Umplanung muss ScheduledPromptChanged auslösen");
        await WaitUntilAsync(async () => (await LeseDatensaetzeAsync(provider, aufgabeId))
            .Any(z => z.ZielZeitpunktUtc > _timeProvider.GetUtcNow().AddMinutes(30)));
        var datensaetze = await LeseDatensaetzeAsync(provider, aufgabeId);
        datensaetze.Should().HaveCount(1);
        // BeCloseTo statt Be: ZielZeitpunktUtc läuft durch den Unix-Millisekunden-Converter.
        datensaetze[0].ZielZeitpunktUtc.Should().BeCloseTo(pausiertBis, TimeSpan.FromSeconds(1),
            "die Pausenverschiebung muss den persistierten Zielzeitpunkt auf das Pausenende setzen");
    }

    /// <summary>RestoreScheduledPromptsAsync armiert für einen zukünftigen Datensatz den Timer — der Status ist gemeldet und der Versand löst fällig aus.</summary>
    [Fact]
    public async Task Restore_ZukuenftigerPlan_ArmiertTimer()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var zielzeit = _timeProvider.GetUtcNow().AddMinutes(5);
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ZeitgesteuerterPromptStore>();
            await store.UpsertAsync(aufgabeId, "PersistierterPrompt", zielzeit);
        }

        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(aufgabeId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.RestoreScheduledPromptsAsync();

        var status = sut.GetScheduledPromptStatus(aufgabeId);
        status.Should().NotBeNull("der persistierte Plan muss wiederhergestellt werden");
        status!.PromptText.Should().Be("PersistierterPrompt");

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "der wiederhergestellte Timer muss zur Zielzeit versenden");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty();
    }

    /// <summary>Ein überfälliger Datensatz wird beim Re-Arm nicht sofort versendet, sondern wartet auf die nächste Session.</summary>
    [Fact]
    public async Task Restore_UeberfaelligerPlan_WartetAufSession()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ZeitgesteuerterPromptStore>();
            await store.UpsertAsync(aufgabeId, "UeberfaelligerPrompt", _timeProvider.GetUtcNow().AddMinutes(-5));
        }

        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.RestoreScheduledPromptsAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        versendet.Task.IsCompleted.Should().BeFalse("ohne Session darf der überfällige Plan nicht sofort versendet werden");
        sut.GetScheduledPromptStatus(aufgabeId).Should().NotBeNull("der überfällige Plan muss gemeldet bleiben");

        RegisterSession(aufgabeId);
        RaiseCliProcessStatusChanged(aufgabeId, CliProcessStatus.Gestartet);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "beim nächsten Session-Start muss der überfällige Plan zugestellt werden");
    }

    /// <summary>Ein Datensatz ohne existierende Aufgabe wird beim Re-Arm defensiv gelöscht.</summary>
    [Fact]
    public async Task Restore_VerwaisterDatensatz_WirdGeloescht()
    {
        var aufgabeId = Guid.NewGuid();
        var verwaisteAufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ZeitgesteuerterPromptStore>();
            await store.UpsertAsync(verwaisteAufgabeId, "VerwaisterPrompt", _timeProvider.GetUtcNow().AddMinutes(5));
        }

        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        await sut.RestoreScheduledPromptsAsync();

        (await LeseDatensaetzeAsync(provider, verwaisteAufgabeId)).Should().BeEmpty(
            "der verwaiste Datensatz muss beim Re-Arm entfernt werden");
        sut.GetScheduledPromptStatus(verwaisteAufgabeId).Should().BeNull();
    }

    /// <summary>Simulierter App-Neustart: Ein neuer Service gegen dieselbe Datenbank stellt den Plan beim nächsten Session-Start zu.</summary>
    [Fact]
    public async Task Restore_NachNeustart_LiefertBeiSessionStartZu()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var ersterService = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            scopeFactory);
        await ersterService.SchedulePromptAsync(aufgabeId, "NeustartPrompt", _timeProvider.GetUtcNow().AddMinutes(5));
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().HaveCount(1);

        // Neuer Service = neuer In-Memory-Zustand gegen dieselbe Datenbank (simulierter App-Neustart);
        // die Zielzeit ist beim „Neustart“ bereits verstrichen.
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var zweiterService = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            scopeFactory);
        var versendet = new TaskCompletionSource<Guid>();
        zweiterService.PromptSent += id => versendet.TrySetResult(id);

        await zweiterService.RestoreScheduledPromptsAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        versendet.Task.IsCompleted.Should().BeFalse("ohne Session darf nach dem Neustart nicht versendet werden");
        zweiterService.GetScheduledPromptStatus(aufgabeId).Should().NotBeNull(
            "der persistierte Plan muss nach dem simulierten Neustart wiederhergestellt sein");

        // „App-Stop" der ersten Instanz: Ihr CliProcessStatusChanged-Abo am gemeinsamen
        // KiAusfuehrungsService lösen, damit sie beim Session-Start nicht ebenfalls zustellt.
        ersterService.Dispose();

        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterSession(aufgabeId, sessionMock.Object);
        RaiseCliProcessStatusChanged(aufgabeId, CliProcessStatus.Gestartet);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "der Plan muss nach dem Neustart beim ersten Session-Start zugestellt werden");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty();
        sessionMock.Verify(
            s => s.WritePromptAsync("NeustartPrompt", It.IsAny<CancellationToken>()),
            Times.Once,
            "die Zustellung darf nicht durch beide Service-Instanzen doppelt erfolgen");
    }

    /// <summary>Schlägt der Sofortversand fehl, wird der neue Plan per Upsert persistiert — ein älterer Datensatz derselben Aufgabe wird dabei ersetzt.</summary>
    [Fact]
    public async Task SofortVersand_BeiSendefehler_ErsetztPersistiertenDatensatz()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        // Bestehender persistierter Plan (alter Text/Zielzeit), der ersetzt werden soll.
        await sut.SchedulePromptAsync(aufgabeId, "AlterPlan", _timeProvider.GetUtcNow().AddMinutes(10));
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().HaveCount(1);

        // Vergangene Zielzeit → Sofortversand; ohne Session schlägt er fehl und der Plan
        // geht in „wartet auf Session" über — mit dem NEUEN Plan, nicht dem alten Datensatz.
        var neueZielzeit = _timeProvider.GetUtcNow().AddMinutes(-1);
        await sut.SchedulePromptAsync(aufgabeId, "NeuerPlan", neueZielzeit);

        var datensaetze = await LeseDatensaetzeAsync(provider, aufgabeId);
        datensaetze.Should().HaveCount(1);
        datensaetze[0].PromptText.Should().Be(
            "NeuerPlan",
            "der fehlgeschlagene Sofortversand muss den neuen Plan persistieren, damit ein Neustart nicht den alten Datensatz wiederherstellt");
        // BeCloseTo statt Be: ZielZeitpunktUtc läuft durch den Unix-Millisekunden-Converter.
        datensaetze[0].ZielZeitpunktUtc.Should().BeCloseTo(neueZielzeit, TimeSpan.FromSeconds(1));
    }

    /// <summary>Wird während eines in-flight-Versands ein neuer Plan persistiert, darf der alte Versand den neuen Datensatz nicht mitlöschen (Lost-Update-Schutz).</summary>
    [Fact]
    public async Task Versand_LoeschtNichtZwischenzeitlichNeuGeplantenDatensatz()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        // Session, deren Schreibvorgang sich kontrollieren lässt — simuliert den in-flight-Versand.
        var schreibBegonnen = new TaskCompletionSource();
        var schreibBlockade = new TaskCompletionSource();
        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                schreibBegonnen.TrySetResult();
                return schreibBlockade.Task;
            });
        RegisterSession(aufgabeId, sessionMock.Object);

        await sut.SchedulePromptAsync(aufgabeId, "AlterPlan", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var angekommen = await Task.WhenAny(schreibBegonnen.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        angekommen.Should().Be(schreibBegonnen.Task, "der Timer-Versand muss in-flight sein, bevor neu geplant wird");

        // Während der Versand an der Session hängt: neuen Plan derselben Aufgabe planen und persistieren.
        var neueZielzeit = _timeProvider.GetUtcNow().AddMinutes(10);
        await sut.SchedulePromptAsync(aufgabeId, "NeuerPlan", neueZielzeit);

        schreibBlockade.SetResult();
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        var datensaetze = await LeseDatensaetzeAsync(provider, aufgabeId);
        datensaetze.Should().HaveCount(1);
        datensaetze[0].PromptText.Should().Be(
            "NeuerPlan",
            "ein während des Versands neu persistierter Plan darf vom alten Versand nicht mitgelöscht werden");
        datensaetze[0].ZielZeitpunktUtc.Should().BeCloseTo(neueZielzeit, TimeSpan.FromSeconds(1));
        sut.GetScheduledPromptStatus(aufgabeId)!.PromptText.Should().Be("NeuerPlan");
    }

    /// <summary>Schlägt ein in-flight-Versand fehl, nachdem zwischenzeitlich ein neuer Plan eingetragen wurde, darf die Wartestellungs-Markierung den neuen Eintrag nicht überschreiben — sonst sendet der verwaiste neue Timer den alten Text zur neuen Zielzeit.</summary>
    [Fact]
    public async Task Sendefehler_UeberschreibtNichtZwischenzeitlichNeuGeplantenEintrag()
    {
        var aufgabeId = Guid.NewGuid();

        var schreibBegonnen = new TaskCompletionSource();
        var schreibBlockade = new TaskCompletionSource();
        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                schreibBegonnen.TrySetResult();
                await schreibBlockade.Task;
                throw new InvalidOperationException("Sendefehler");
            });
        RegisterSession(aufgabeId, sessionMock.Object);

        await _sut.SchedulePromptAsync(aufgabeId, "AlterPlan", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var angekommen = await Task.WhenAny(schreibBegonnen.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        angekommen.Should().Be(schreibBegonnen.Task, "der Timer-Versand muss in-flight sein, bevor neu geplant wird");

        // Während der Versand hängt: neuen Plan mit eigenem Timer eintragen.
        var neueZielzeit = _timeProvider.GetUtcNow().AddMinutes(10);
        await _sut.SchedulePromptAsync(aufgabeId, "NeuerPlan", neueZielzeit);

        // Jetzt fehlschlagen lassen: Die Wartestellungs-Markierung des alten Versands darf den
        // neuen Eintrag nicht zurückschreiben (Guard wie bei der Pausen-Umplanung).
        schreibBlockade.SetResult();
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        _sut.GetScheduledPromptStatus(aufgabeId)!.PromptText.Should().Be(
            "NeuerPlan",
            "der zwischenzeitlich geplante Eintrag darf vom fehlgeschlagenen Alt-Versand nicht überschrieben werden");

        // Session wieder funktionsfähig: Der neue Timer muss zur neuen Zielzeit den NEUEN Text senden.
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        sessionMock.Verify(
            s => s.WritePromptAsync("NeuerPlan", It.IsAny<CancellationToken>()),
            Times.Once,
            "der neu armierte Timer darf nicht verwaist auf den veralteten Wartestellungs-Eintrag treffen");
        sessionMock.Verify(
            s => s.WritePromptAsync("AlterPlan", It.IsAny<CancellationToken>()),
            Times.Once,
            "der alte Plan wird nur einmal (in-flight, fehlschlagend) versendet");
    }

    /// <summary>Schlägt die Wiederherstellung eines einzelnen Datensatzes fehl, werden die übrigen Pläne trotzdem re-armiert (Fehler pro Datensatz isoliert).</summary>
    [Fact]
    public async Task Restore_FehlerhafterDatensatz_BlockiertWeitereRestoresNicht()
    {
        var aufgabeId = Guid.NewGuid();
        var defekteAufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
            var projektId = (await db.Aufgaben.FindAsync(aufgabeId))!.ProjektId;
            db.Aufgaben.Add(new Softwareschmiede.Domain.Entities.Aufgabe
            {
                Id = defekteAufgabeId,
                ProjektId = projektId,
                Titel = "Defekte Aufgabe",
                Status = Softwareschmiede.Domain.Enums.AufgabeStatus.Gestartet,
                ErstellungsDatum = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            var store = scope.ServiceProvider.GetRequiredService<ZeitgesteuerterPromptStore>();
            // Defekter Plan zuerst (Insertionsreihenfolge): Die Zielzeit liegt über dem maximalen
            // Timer-Intervall (~49,7 Tage), sodass das Armiieren des Timers beim Re-Arm wirft.
            await store.UpsertAsync(defekteAufgabeId, "DefekterPlan", _timeProvider.GetUtcNow().AddYears(1));
            await store.UpsertAsync(aufgabeId, "GuterPlan", _timeProvider.GetUtcNow().AddMinutes(5));
        }

        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        var act = () => sut.RestoreScheduledPromptsAsync();

        await act.Should().NotThrowAsync();
        var status = sut.GetScheduledPromptStatus(aufgabeId);
        status.Should().NotBeNull(
            "ein fehlschlagender Datensatz darf die Wiederherstellung der übrigen Pläne nicht blockieren");
        status!.PromptText.Should().Be("GuterPlan");
        sut.GetScheduledPromptStatus(defekteAufgabeId).Should().BeNull(
            "der nicht armierte defekte Plan darf keinen untoten Warteschlangeneintrag hinterlassen");
    }

    /// <summary>Schlägt ein in-flight-Sofortversand fehl, nachdem zwischenzeitlich ein neuer Plan persistiert wurde, darf der Fehlerpfad des alten Versands den neuen Datensatz nicht mit dem alten Plan überschreiben — sonst überlebt eine Leiche, die nach einem Neustart zugestellt wird.</summary>
    [Fact]
    public async Task SofortVersand_Sendefehler_UeberschreibtNichtZwischenzeitlichNeuGeplantenDatensatz()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        // Session, deren Schreibvorgang blockiert und danach fehlschlägt — in-flight-Sofortversand.
        var schreibBegonnen = new TaskCompletionSource();
        var schreibBlockade = new TaskCompletionSource();
        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                schreibBegonnen.TrySetResult();
                await schreibBlockade.Task;
                throw new InvalidOperationException("Sendefehler");
            });
        RegisterSession(aufgabeId, sessionMock.Object);

        // Zielzeit in der Vergangenheit → Sofortversand, der in WritePromptAsync hängt.
        var sofortPlanung = sut.SchedulePromptAsync(aufgabeId, "AlterPlan", _timeProvider.GetUtcNow().AddMinutes(-1));
        var angekommen = await Task.WhenAny(schreibBegonnen.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        angekommen.Should().Be(schreibBegonnen.Task, "der Sofortversand muss in-flight sein, bevor neu geplant wird");

        // Während der Versand hängt: neuen Plan derselben Aufgabe planen und persistieren.
        var neueZielzeit = _timeProvider.GetUtcNow().AddMinutes(10);
        await sut.SchedulePromptAsync(aufgabeId, "NeuerPlan", neueZielzeit);

        // Jetzt fehlschlagen lassen: Der Upsert des Alt-Versands darf den neuen Datensatz
        // nicht überschreiben (konditionaler Schreibzugriff analog zum In-Memory-Guard).
        schreibBlockade.SetResult();
        await sofortPlanung;

        var datensaetze = await LeseDatensaetzeAsync(provider, aufgabeId);
        datensaetze.Should().HaveCount(1);
        datensaetze[0].PromptText.Should().Be(
            "NeuerPlan",
            "der fehlgeschlagene Alt-Versand darf den zwischenzeitlich persistierten neuen Datensatz nicht überschreiben");
        datensaetze[0].ZielZeitpunktUtc.Should().BeCloseTo(neueZielzeit, TimeSpan.FromSeconds(1));
        sut.GetScheduledPromptStatus(aufgabeId)!.PromptText.Should().Be("NeuerPlan");
    }

    /// <summary>Ist der persistierte Datensatz vom In-Memory-Stand abgedriftet (z. B. weil ein früherer Store-Schreibzugriff fehlschlug und nur geloggt wurde), muss er nach erfolgreichem Versand trotzdem gelöscht werden — sonst wird der bereits zugestellte Prompt nach einem Neustart ein zweites Mal zugestellt.</summary>
    [Fact]
    public async Task Versand_LoeschtAbgedriftetenDatensatz_WennKeinPlanRegistriert()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(aufgabeId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        // Store-Drift simulieren: Der Datensatz entspricht nicht mehr dem geplanten Stand
        // (so sähe er aus, wenn ein früherer Upsert/UpdateZielZeitpunkt fehlschlug).
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
            (await db.ZeitgesteuertePrompts.SingleAsync()).PromptText = "AbgedrifteterText";
            await db.SaveChangesAsync();
        }

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task);
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().BeEmpty(
            "ein vom versendeten Plan abweichender Datensatz ohne lebenden In-Memory-Plan ist eine Leiche und darf nicht überleben");
        sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull();
    }

    /// <summary>Dispose muss armierte Timer entschärfen und die Warteschlange leeren — eine disposed Instanz (simulierter App-Stop) darf keine Prompts mehr versenden; der persistierte Datensatz bleibt für den Restore beim nächsten Start bestehen.</summary>
    [Fact]
    public async Task Dispose_EntschaerftArmierteTimer()
    {
        var aufgabeId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, aufgabeId);
        var sut = new PromptZeitVersandService(
            _kiService,
            _timeProvider,
            NullLogger<PromptZeitVersandService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterSession(aufgabeId, sessionMock.Object);

        await sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().HaveCount(1);

        sut.Dispose();
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        sessionMock.Verify(
            s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "eine disposed Instanz darf keine armierten Timer mehr feuern");
        sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull(
            "die Warteschlange muss beim Dispose geleert werden");
        (await LeseDatensaetzeAsync(provider, aufgabeId)).Should().HaveCount(1,
            "der persistierte Datensatz bleibt für den Restore beim nächsten Start bewusst bestehen");
    }
}
