using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
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
    /// angehängt, damit <see cref="KiAusfuehrungsService.GetPseudoConsoleSession"/> sie zurückgibt.
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
    {
        var session = TestPseudoConsoleSessionFactory.Create(new MemoryStream(), new MemoryStream());
        var handle = new CliProcessHandle(aufgabeId, System.Diagnostics.Process.GetCurrentProcess())
        {
            PseudoConsoleSession = session
        };

        var handlesField = typeof(KiAusfuehrungsService).GetField("_handles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_kiService)!;
        handles[aufgabeId] = handle;
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

    /// <summary>CancelScheduledPrompt entfernt den geplanten Prompt; auch nach Ablauf der Zielzeit erfolgt kein Versand mehr.</summary>
    [Fact]
    public async Task CancelScheduledPrompt_EntferntGeplantenPrompt()
    {
        var aufgabeId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        _sut.CancelScheduledPrompt(aufgabeId);
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
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull(
            "der zweite (ersetzende) Timer muss zu seiner Zielzeit feuern und den Eintrag entfernen");
    }

    /// <summary>Ist zur Fälligkeit keine aktive CLI-Session mehr vorhanden, wird der Prompt still verworfen: kein Event, keine Exception, Eintrag wird entfernt.</summary>
    [Fact]
    public async Task Timer_OhneSession_VerwirftPromptStill()
    {
        var aufgabeId = Guid.NewGuid();
        var versendet = false;
        _sut.PromptSent += _ => versendet = true;

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        var act = () => _timeProvider.Advance(TimeSpan.FromMinutes(5));
        act.Should().NotThrow();

        await Task.Delay(TimeSpan.FromMilliseconds(500));

        versendet.Should().BeFalse("ohne aktive CLI-Session darf kein PromptSent-Event gefeuert werden");
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull("der Eintrag muss trotz stillem Verwerfen entfernt werden");
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
}
