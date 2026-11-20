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
public sealed class PromptTimeDispatchServiceTests : IDisposable
{
    private readonly AiExecutionService _aiService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly PromptScheduleDispatchService _sut;

    /// <summary>PromptZeitVersandServiceTests.</summary>
    public PromptTimeDispatchServiceTests()
    {
        _aiService = TestAiExecutionServiceFactory.Create();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _sut = new PromptScheduleDispatchService(_aiService, _timeProvider, NullLogger<PromptScheduleDispatchService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _aiService.Dispose();

    /// <summary>
    /// Registriert für eine neue Aufgabe eine gültige <see cref="PseudoConsoleSession"/> beim
    /// <see cref="AiExecutionService"/>. Nutzt bewusst den klassischen <c>StartCliAsync</c>-Pfad mit einem
    /// lange laufenden Ping-Kommando (statt ConPTY mit einer per Tastatureingabe simulierten Shell): Die äußere
    /// ConPTY-<c>cmd.exe</c>-Shell dieser Sandbox kann sich (siehe CLAUDE.md, ConPTY-Isolationsproblem)
    /// unvorhersehbar innerhalb weniger Sekunden selbst beenden, was das Handle aus der internen
    /// Verwaltung entfernt und Tests, die eine über mehrere <c>FakeTimeProvider.Advance</c>-Aufrufe hinweg
    /// gültige Session voraussetzen, nichtdeterministisch scheitern lässt. Der Ping-Prozess bleibt dagegen
    /// zuverlässig für die Testdauer aktiv; die <see cref="PseudoConsoleSession"/> wird manuell an das Handle
    /// angehängt, damit <see cref="AiExecutionService.GetTerminalSession"/> sie zurückgibt.
    /// </summary>
    /// <returns>Die ID der Aufgabe, für die eine Session registriert wurde.</returns>
    private Task<Guid> StartCliSessionAsync()
    {
        var workItemId = Guid.NewGuid();
        RegisterSession(workItemId);
        return Task.FromResult(workItemId);
    }

    /// <summary>Registriert eine Pseudo-Console-Session für die übergebene Aufgaben-ID (mit vorgegebener ID, damit die Aufgabe zusätzlich in einer Test-DB seeded werden kann).</summary>
    private void RegisterSession(Guid workItemId)
        => RegisterSession(workItemId, TestPseudoConsoleSessionFactory.Create(new MemoryStream(), new MemoryStream()));

    /// <summary>Registriert eine beliebige <see cref="ITerminalSession"/> für die übergebene Aufgaben-ID.</summary>
    private void RegisterSession(Guid workItemId, ITerminalSession session)
    {
        var handle = new CliProcessHandle(workItemId, System.Diagnostics.Process.GetCurrentProcess())
        {
            Session = session
        };

        var handlesField = typeof(AiExecutionService).GetField("_handles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_aiService)!;
        handles[workItemId] = handle;
    }

    /// <summary>Löst das <see cref="AiExecutionService.CliProcessStatusChanged"/>-Event testseitig über das Backing-Field aus.</summary>
    private void RaiseCliProcessStatusChanged(Guid workItemId, CliProcessStatus status)
    {
        var eventField = typeof(AiExecutionService).GetField("CliProcessStatusChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((Action<Guid, CliProcessStatus>?)eventField.GetValue(_aiService))?.Invoke(workItemId, status);
    }

    /// <summary>Wartet aktiv, bis die Bedingung erfüllt ist (für Fire-and-forget-Abläufe ohne Event-Signal).</summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(50);
        }
    }

    /// <summary>Erstellt einen ServiceProvider mit InMemory-DbContext und seedet eine Aufgabe mit dem übergebenen PausiertBisUtc-Wert.</summary>
    private async Task<ServiceProvider> CreatePausedTaskProviderAsync(DateTimeOffset? pausedUntilUtc, Guid workItemId)
    {
        // Der Datenbankname muss AUSSERHALB des Options-Lambdas erzeugt werden: AddDbContext führt
        // die Options-Action bei jeder Context-Auflösung erneut aus — ein Guid.NewGuid() im Lambda
        // ergäbe pro Scope eine andere (leere) InMemory-Datenbank.
        var databaseName = Guid.NewGuid().ToString();
        var provider = new ServiceCollection()
            .AddDbContext<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>(options =>
                options.UseInMemoryDatabase(databaseName))
            .AddScoped<TodoService>()
            .AddScoped<WorkItemService>()
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<TodoService>>(NullLogger<TodoService>.Instance)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<WorkItemService>>(NullLogger<WorkItemService>.Instance)
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        var projectId = Guid.NewGuid();
        db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = projectId,
            Name = "Prompt-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = Softwareschmiede.Domain.Enums.ProjectStatus.Active
        });
        db.WorkItems.Add(new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = workItemId,
            ProjectId = projectId,
            Title = "Pausierte Aufgabe",
            Status = Softwareschmiede.Domain.Enums.WorkItemStatus.Started,
            PausedUntilUtc = pausedUntilUtc,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return provider;
    }

    /// <summary>
    /// Erstellt einen ServiceProvider mit InMemory-DbContext, <see cref="ScheduledPromptStore"/>,
    /// <see cref="WorkItemService"/> und <see cref="LogService"/> und seedet eine Aufgabe
    /// mit dem übergebenen PausiertBisUtc-Wert (Muster <see cref="CreatePausedTaskProviderAsync"/>).
    /// </summary>
    /// <param name="pausedUntilUtc">Der zu seedende PausiertBisUtc-Wert der Aufgabe.</param>
    /// <param name="workItemId">Die ID der zu seedenden Aufgabe.</param>
    /// <param name="withLogService">Bei <c>false</c> wird <see cref="LogService"/> bewusst nicht registriert (Fehlerpfad-Protokollierung).</param>
    private async Task<ServiceProvider> CreateStoreProviderAsync(DateTimeOffset? pausedUntilUtc, Guid workItemId, bool withLogService = true)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection()
            .AddDbContext<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>(options =>
                options.UseInMemoryDatabase(databaseName))
            .AddScoped<TodoService>()
            .AddScoped<WorkItemService>()
            .AddScoped<ScheduledPromptStore>()
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<TodoService>>(NullLogger<TodoService>.Instance)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<WorkItemService>>(NullLogger<WorkItemService>.Instance)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<ScheduledPromptStore>>(NullLogger<ScheduledPromptStore>.Instance);

        if (withLogService)
        {
            services
                .AddScoped<LogService>()
                .AddSingleton<Microsoft.Extensions.Logging.ILogger<LogService>>(NullLogger<LogService>.Instance);
        }

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        var projectId = Guid.NewGuid();
        db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = projectId,
            Name = "Prompt-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = Softwareschmiede.Domain.Enums.ProjectStatus.Active
        });
        db.WorkItems.Add(new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = workItemId,
            ProjectId = projectId,
            Title = "Testaufgabe",
            Status = Softwareschmiede.Domain.Enums.WorkItemStatus.Started,
            PausedUntilUtc = pausedUntilUtc,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return provider;
    }

    /// <summary>Liest die persistierten ZeitgesteuerterPrompt-Datensätze der Aufgabe über einen eigenen Scope.</summary>
    private static async Task<List<Softwareschmiede.Domain.Entities.ScheduledPrompt>> ReadRecordsAsync(ServiceProvider provider, Guid workItemId)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        return await db.ScheduledPrompts.Where(z => z.WorkItemId == workItemId).ToListAsync();
    }

    /// <summary>Liest die Protokolleinträge der Aufgabe über einen eigenen Scope.</summary>
    private static async Task<List<Softwareschmiede.Domain.Entities.LogEntry>> ReadLogAsync(ServiceProvider provider, Guid workItemId)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        return await db.LogEntries.Where(p => p.WorkItemId == workItemId).ToListAsync();
    }

    /// <summary>Liegt die Zielzeit in der Vergangenheit, wird der Prompt sofort versendet und es bleibt kein Eintrag in der Warteschlange.</summary>
    [Fact]
    public async Task SchedulePromptAsync_TargettimeInPast_SendsImmediately()
    {
        var workItemId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(-1));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "eine Zielzeit in der Vergangenheit muss sofort versendet werden");
        _sut.GetScheduledPromptStatus(workItemId).Should().BeNull("ein sofort versendeter Prompt darf keinen Eintrag in der Warteschlange hinterlassen");
    }

    /// <summary>Liegt die Zielzeit in der Zukunft, wird der Prompt gepuffert und der Status ist über GetScheduledPromptStatus abrufbar.</summary>
    [Fact]
    public async Task SchedulePromptAsync_TargettimeInFuture_BuffersPrompt()
    {
        var workItemId = Guid.NewGuid();
        var targetTime = _timeProvider.GetUtcNow().AddMinutes(5);

        await _sut.SchedulePromptAsync(workItemId, "Testprompt", targetTime);

        var status = _sut.GetScheduledPromptStatus(workItemId);
        status.Should().NotBeNull();
        status!.PromptText.Should().Be("Testprompt");
        status.TargetTime.Should().Be(targetTime);
    }

    /// <summary>Erreicht der FakeTimeProvider die Zielzeit, wird der Prompt automatisch versendet und PromptSent gefeuert.</summary>
    [Fact]
    public async Task Timer_AtReachTheTargettime_SendsPromptAutomatic()
    {
        var workItemId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "der Timer muss beim Erreichen der Zielzeit automatisch feuern");
        _sut.GetScheduledPromptStatus(workItemId).Should().BeNull("nach dem Versand darf kein Eintrag mehr in der Warteschlange sein");
    }

    /// <summary>CancelScheduledPromptAsync entfernt den geplanten Prompt; auch nach Ablauf der Zielzeit erfolgt kein Versand mehr.</summary>
    [Fact]
    public async Task CancelScheduledPromptAsync_RemovesPlannedPrompt()
    {
        var workItemId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        await _sut.CancelScheduledPromptAsync(workItemId);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken));
        finished.Should().NotBe(versendet.Task, "ein stornierter Prompt darf auch nach Ablauf der Zielzeit nicht versendet werden");
        _sut.GetScheduledPromptStatus(workItemId).Should().BeNull();
    }

    /// <summary>Ein zweiter geplanter Prompt für dieselbe Aufgabe ersetzt den ersten und storniert dessen Timer.</summary>
    [Fact]
    public async Task SchedulePromptAsync_SecondPromptForSameWorkItem_ReplacesFirst()
    {
        var workItemId = Guid.NewGuid();
        var firstTargettime = _timeProvider.GetUtcNow().AddMinutes(5);
        var secondTargettime = _timeProvider.GetUtcNow().AddMinutes(10);

        await _sut.SchedulePromptAsync(workItemId, "ErsterPrompt", firstTargettime);
        await _sut.SchedulePromptAsync(workItemId, "ZweiterPrompt", secondTargettime);

        _sut.GetScheduledPromptStatus(workItemId)!.PromptText.Should().Be("ZweiterPrompt");

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _sut.GetScheduledPromptStatus(workItemId).Should().NotBeNull(
            "der erste (ersetzte) Timer darf zu seiner ursprünglichen Zielzeit nicht mehr feuern");
        _sut.GetScheduledPromptStatus(workItemId)!.PromptText.Should().Be(
            "ZweiterPrompt",
            "der Eintrag muss weiterhin der zweite, ersetzende Prompt sein");

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        _sut.GetScheduledPromptStatus(workItemId).Should().NotBeNull(
            "ohne zustellfähige Session bleibt der Plan des zweiten Prompts im Zustand „wartet auf Session“ bestehen");
    }

    /// <summary>Ist zur Fälligkeit keine aktive CLI-Session vorhanden, bleibt Plan und Datensatz bestehen und der Eintrag wartet auf die nächste Session.</summary>
    [Fact]
    public async Task Timer_WithoutSession_KeepsPlanAndWaitsOnSession()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var versendet = false;
        sut.PromptSent += _ => versendet = true;

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        var act = () => _timeProvider.Advance(TimeSpan.FromMinutes(5));
        act.Should().NotThrow();

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        versendet.Should().BeFalse("ohne aktive CLI-Session darf kein PromptSent-Event gefeuert werden");
        var status = sut.GetScheduledPromptStatus(workItemId);
        status.Should().NotBeNull("der Plan muss im Zustand „wartet auf Session“ erhalten bleiben");
        status!.WaitingForSession.Should().BeTrue("der Status muss den Wartezustand an die Oberfläche melden");
        (await ReadRecordsAsync(provider, workItemId)).Should().HaveCount(1, "der persistierte Datensatz darf nicht verworfen werden");
    }

    /// <summary>Ist die Aufgabe zum Planungszeitpunkt bereits pausiert, wird die Zielzeit auf das Pausenende verschoben statt sofort zu senden (Issue 151).</summary>
    [Fact]
    public async Task SchedulePromptAsync_ActivePause_MovesTargettimeOnPauseend()
    {
        var workItemId = Guid.NewGuid();
        var pausedUntil = _timeProvider.GetUtcNow().AddHours(2);
        await using var provider = await CreatePausedTaskProviderAsync(pausedUntil, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        var status = sut.GetScheduledPromptStatus(workItemId);
        status.Should().NotBeNull("der Prompt muss gepuffert statt sofort versendet werden");
        // BeCloseTo statt Be: PausiertBisUtc läuft durch den Unix-Millisekunden-Converter und
        // verliert dabei Sub-Millisekunden-Genauigkeit.
        status!.TargetTime.Should().BeCloseTo(pausedUntil, TimeSpan.FromSeconds(1), "die Zielzeit wird auf das Pausenende verschoben");
    }

    /// <summary>Wird die Pause NACH dem Planen gesetzt, plant der Timer-Callback den Versand auf das Pausenende um und versendet erst danach (Issue 151).</summary>
    [Fact]
    public async Task Timer_AtAfterPlanSetPause_MovesDispatchOnPauseend()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreatePausedTaskProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(workItemId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        // Pause erst nach dem Planen setzen — der Timer-Callback muss sie beim Feuern erkennen.
        var pausedUntil = _timeProvider.GetUtcNow().AddHours(1);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
            (await db.WorkItems.FindAsync(new object?[] { workItemId }, TestContext.Current.CancellationToken))!.PausedUntilUtc = pausedUntil;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        versendet.Task.IsCompleted.Should().BeFalse("während einer aktiven Pause darf der Prompt nicht versendet werden");
        var status = sut.GetScheduledPromptStatus(workItemId);
        status.Should().NotBeNull("der Prompt muss auf das Pausenende umgeplant werden");
        // BeCloseTo statt Be: PausiertBisUtc läuft durch den Unix-Millisekunden-Converter und
        // verliert dabei Sub-Millisekunden-Genauigkeit.
        status!.TargetTime.Should().BeCloseTo(pausedUntil, TimeSpan.FromSeconds(1),
            "die gemeldete Zielzeit muss dem Pausenende entsprechen, nicht dem verstrichenen ursprünglichen Zeitpunkt");

        _timeProvider.Advance(TimeSpan.FromHours(1));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "nach dem Pausenende muss der gepufferte Prompt versendet werden");
        sut.GetScheduledPromptStatus(workItemId).Should().BeNull();
    }

    /// <summary>Planen mit zukünftiger Zielzeit legt genau einen ZeitgesteuerterPrompt-Datensatz an.</summary>
    [Fact]
    public async Task SchedulePromptAsync_WritesRecord()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var targettime = _timeProvider.GetUtcNow().AddMinutes(5);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", targettime);

        var records = await ReadRecordsAsync(provider, workItemId);
        records.Should().HaveCount(1);
        records[0].PromptText.Should().Be("Testprompt");
        records[0].WorkItemId.Should().Be(workItemId);
        // BeCloseTo statt Be: ZielZeitpunktUtc läuft durch den Unix-Millisekunden-Converter und
        // verliert dabei Sub-Millisekunden-Genauigkeit.
        records[0].TargetTimeUtc.Should().BeCloseTo(targettime, TimeSpan.FromSeconds(1));
    }

    /// <summary>Eine zweite Planung derselben Aufgabe ersetzt den Datensatz (Upsert) — es bleibt genau ein Datensatz mit neuem Text und neuer Zielzeit.</summary>
    [Fact]
    public async Task SchedulePromptAsync_AgainPlan_ReplacesRecord()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var secondTargettime = _timeProvider.GetUtcNow().AddMinutes(10);

        await sut.SchedulePromptAsync(workItemId, "ErsterPrompt", _timeProvider.GetUtcNow().AddMinutes(5));
        await sut.SchedulePromptAsync(workItemId, "ZweiterPrompt", secondTargettime);

        var records = await ReadRecordsAsync(provider, workItemId);
        records.Should().HaveCount(1, "die Ersetzungsregel erlaubt maximal einen Plan pro Aufgabe");
        records[0].PromptText.Should().Be("ZweiterPrompt");
        records[0].TargetTimeUtc.Should().BeCloseTo(secondTargettime, TimeSpan.FromSeconds(1));
    }

    /// <summary>Nach erfolgreichem Versand sind Datensatz und In-Memory-Eintrag entfernt.</summary>
    [Fact]
    public async Task Dispatch_DeletesRecord()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(workItemId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        (await ReadRecordsAsync(provider, workItemId)).Should().HaveCount(1);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task);
        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty("der Datensatz muss nach erfolgreichem Versand gelöscht sein");
        sut.GetScheduledPromptStatus(workItemId).Should().BeNull();
    }

    /// <summary>Storno entfernt den persistierten Datensatz und den In-Memory-Eintrag.</summary>
    [Fact]
    public async Task CancelScheduledPromptAsync_DeletesRecord()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        (await ReadRecordsAsync(provider, workItemId)).Should().HaveCount(1);

        await sut.CancelScheduledPromptAsync(workItemId);

        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty();
        sut.GetScheduledPromptStatus(workItemId).Should().BeNull();
    }

    /// <summary>Schlägt WritePromptAsync mit einem sonstigen Fehler fehl, bleiben Plan und Datensatz bestehen; der nächste CLI-Start stellt zu.</summary>
    [Fact]
    public async Task Senderror_KeepsPlanAndWaitsOnSession()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        var faultySession = new Mock<ITerminalSession>();
        faultySession
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sendefehler"));
        RegisterSession(workItemId, faultySession.Object);

        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        versendet.Task.IsCompleted.Should().BeFalse("ein Sendefehler darf kein PromptSent-Event auslösen");
        sut.GetScheduledPromptStatus(workItemId).Should().NotBeNull("der Plan muss im Zustand „wartet auf Session“ erhalten bleiben");
        (await ReadRecordsAsync(provider, workItemId)).Should().HaveCount(1);

        // Nächster Session-Start mit funktionierender Session → Nachzustellung.
        RegisterSession(workItemId);
        RaiseCliProcessStatusChanged(workItemId, CliProcessStatus.Started);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "beim nächsten Session-Start muss der wartende Plan zugestellt werden");
        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty();
    }

    /// <summary>Ein CLI-Start (CliProcessStatusChanged Gestartet) stellt einen wartenden/fälligen Plan nach und löscht den Datensatz.</summary>
    [Fact]
    public async Task CliStarted_WithWaitingPlan_DeliversAfter()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        // Ohne Session fällig werden lassen → Eintrag wartet auf Session.
        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        sut.GetScheduledPromptStatus(workItemId).Should().NotBeNull();

        RegisterSession(workItemId);
        RaiseCliProcessStatusChanged(workItemId, CliProcessStatus.Started);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "der Session-Start muss den wartenden Plan zustellen");
        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty();
        sut.GetScheduledPromptStatus(workItemId).Should().BeNull();
    }

    /// <summary>Die Nachzustellung über einen Session-Start schreibt immer einen SystemMeldung-Protokolleintrag — auch mit PromptSent-Abonnenten.</summary>
    [Fact]
    public async Task Redelivery_WritesSystemMessage()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        RegisterSession(workItemId);
        RaiseCliProcessStatusChanged(workItemId, CliProcessStatus.Started);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task);
        await WaitUntilAsync(async () => (await ReadLogAsync(provider, workItemId)).Count > 0);
        (await ReadLogAsync(provider, workItemId))
            .Should().ContainSingle(p => p.Type == LogEntryType.SystemMessage,
                "die Nachzustellung muss im Aufgabenprotokoll dokumentiert werden");
    }

    /// <summary>Ein Timer-Versand ohne PromptSent-Abonnenten (keine geöffnete Detailansicht) schreibt einen SystemMeldung-Eintrag.</summary>
    [Fact]
    public async Task Dispatch_WithoutSubscriber_WritesSystemMessage()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(workItemId);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        await WaitUntilAsync(async () => (await ReadLogAsync(provider, workItemId)).Count > 0);
        (await ReadLogAsync(provider, workItemId))
            .Should().ContainSingle(p => p.Type == LogEntryType.SystemMessage,
                "der Hintergrundversand ohne geöffnete Detailansicht muss protokolliert werden");
        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty();
    }

    /// <summary>Ein Timer-Versand mit PromptSent-Abonnenten (geöffnete Detailansicht) schreibt keinen Protokolleintrag.</summary>
    [Fact]
    public async Task Dispatch_WithSubscriber_NoLogEntry()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(workItemId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        (await ReadLogAsync(provider, workItemId)).Should().BeEmpty(
            "bei beobachtetem Versand (Detailansicht offen) darf kein Protokolleintrag entstehen");
    }

    /// <summary>Schlägt das Schreiben des Protokolleintrags fehl, bleibt der erfolgreiche Versand gültig: Datensatz gelöscht, PromptSent gefeuert.</summary>
    [Fact]
    public async Task LogEntry_AtDeletedWorkItem_CancelsDispatchNot()
    {
        var workItemId = Guid.NewGuid();
        // Ohne registrierten ProtokollService schlägt die Protokoll-Auflösung fehl — der
        // Versandpfad darf dadurch nicht brechen (gleicher Schutzpfad wie eine gelöschte Aufgabe).
        await using var provider = await CreateStoreProviderAsync(null, workItemId, withLogService: false);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(workItemId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "ein fehlschlagender Protokolleintrag darf den Versand nicht brechen");
        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty();
        sut.GetScheduledPromptStatus(workItemId).Should().BeNull();
    }

    /// <summary>Die Pause-Verschiebung im Timer-Callback aktualisiert ZielZeitpunktUtc des persistierten Datensatzes auf das Pausenende.</summary>
    [Fact]
    public async Task Pause_Move_UpdatesRecord()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var umgeplant = new TaskCompletionSource<Guid>();
        sut.ScheduledPromptChanged += id => umgeplant.TrySetResult(id);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        var pausedUntil = _timeProvider.GetUtcNow().AddHours(1);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
            (await db.WorkItems.FindAsync(new object?[] { workItemId }, TestContext.Current.CancellationToken))!.PausedUntilUtc = pausedUntil;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var finished = await Task.WhenAny(umgeplant.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(umgeplant.Task, "die Pause-Umplanung muss ScheduledPromptChanged auslösen");
        await WaitUntilAsync(async () => (await ReadRecordsAsync(provider, workItemId))
            .Any(z => z.TargetTimeUtc > _timeProvider.GetUtcNow().AddMinutes(30)));
        var records = await ReadRecordsAsync(provider, workItemId);
        records.Should().HaveCount(1);
        // BeCloseTo statt Be: ZielZeitpunktUtc läuft durch den Unix-Millisekunden-Converter.
        records[0].TargetTimeUtc.Should().BeCloseTo(pausedUntil, TimeSpan.FromSeconds(1),
            "die Pausenverschiebung muss den persistierten Zielzeitpunkt auf das Pausenende setzen");
    }

    /// <summary>RestoreScheduledPromptsAsync armiert für einen zukünftigen Datensatz den Timer — der Status ist gemeldet und der Versand löst fällig aus.</summary>
    [Fact]
    public async Task Restore_FuturePlan_ArmedTimer()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var targettime = _timeProvider.GetUtcNow().AddMinutes(5);
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ScheduledPromptStore>();
            await store.UpsertAsync(workItemId, "PersistierterPrompt", targettime, TestContext.Current.CancellationToken);
        }

        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(workItemId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.RestoreScheduledPromptsAsync();

        var status = sut.GetScheduledPromptStatus(workItemId);
        status.Should().NotBeNull("der persistierte Plan muss wiederhergestellt werden");
        status!.PromptText.Should().Be("PersistierterPrompt");

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "der wiederhergestellte Timer muss zur Zielzeit versenden");
        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty();
    }

    /// <summary>Ein überfälliger Datensatz wird beim Re-Arm nicht sofort versendet, sondern wartet auf die nächste Session.</summary>
    [Fact]
    public async Task Restore_OverduePlan_WaitsOnSession()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ScheduledPromptStore>();
            await store.UpsertAsync(workItemId, "UeberfaelligerPrompt", _timeProvider.GetUtcNow().AddMinutes(-5), TestContext.Current.CancellationToken);
        }

        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.RestoreScheduledPromptsAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        versendet.Task.IsCompleted.Should().BeFalse("ohne Session darf der überfällige Plan nicht sofort versendet werden");
        sut.GetScheduledPromptStatus(workItemId).Should().NotBeNull("der überfällige Plan muss gemeldet bleiben");

        RegisterSession(workItemId);
        RaiseCliProcessStatusChanged(workItemId, CliProcessStatus.Started);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "beim nächsten Session-Start muss der überfällige Plan zugestellt werden");
    }

    /// <summary>Ein Datensatz ohne existierende Aufgabe wird beim Re-Arm defensiv gelöscht.</summary>
    [Fact]
    public async Task Restore_OrphanedRecord_WillDeleted()
    {
        var workItemId = Guid.NewGuid();
        var orphanedWorkItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ScheduledPromptStore>();
            await store.UpsertAsync(orphanedWorkItemId, "VerwaisterPrompt", _timeProvider.GetUtcNow().AddMinutes(5), TestContext.Current.CancellationToken);
        }

        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        await sut.RestoreScheduledPromptsAsync();

        (await ReadRecordsAsync(provider, orphanedWorkItemId)).Should().BeEmpty(
            "der verwaiste Datensatz muss beim Re-Arm entfernt werden");
        sut.GetScheduledPromptStatus(orphanedWorkItemId).Should().BeNull();
    }

    /// <summary>Simulierter App-Neustart: Ein neuer Service gegen dieselbe Datenbank stellt den Plan beim nächsten Session-Start zu.</summary>
    [Fact]
    public async Task Restore_AfterRestart_ReturnsAtSessionStartTo()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var firstService = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            scopeFactory);
        await firstService.SchedulePromptAsync(workItemId, "NeustartPrompt", _timeProvider.GetUtcNow().AddMinutes(5));
        (await ReadRecordsAsync(provider, workItemId)).Should().HaveCount(1);

        // Neuer Service = neuer In-Memory-Zustand gegen dieselbe Datenbank (simulierter App-Neustart);
        // die Zielzeit ist beim „Neustart“ bereits verstrichen.
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var secondService = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            scopeFactory);
        var versendet = new TaskCompletionSource<Guid>();
        secondService.PromptSent += id => versendet.TrySetResult(id);

        await secondService.RestoreScheduledPromptsAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        versendet.Task.IsCompleted.Should().BeFalse("ohne Session darf nach dem Neustart nicht versendet werden");
        secondService.GetScheduledPromptStatus(workItemId).Should().NotBeNull(
            "der persistierte Plan muss nach dem simulierten Neustart wiederhergestellt sein");

        // „App-Stop" der ersten Instanz: Ihr CliProcessStatusChanged-Abo am gemeinsamen
        // KiAusfuehrungsService lösen, damit sie beim Session-Start nicht ebenfalls zustellt.
        firstService.Dispose();

        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterSession(workItemId, sessionMock.Object);
        RaiseCliProcessStatusChanged(workItemId, CliProcessStatus.Started);

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task, "der Plan muss nach dem Neustart beim ersten Session-Start zugestellt werden");
        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty();
        sessionMock.Verify(
            s => s.WritePromptAsync("NeustartPrompt", It.IsAny<CancellationToken>()),
            Times.Once,
            "die Zustellung darf nicht durch beide Service-Instanzen doppelt erfolgen");
    }

    /// <summary>Schlägt der Sofortversand fehl, wird der neue Plan per Upsert persistiert — ein älterer Datensatz derselben Aufgabe wird dabei ersetzt.</summary>
    [Fact]
    public async Task ImmediatelyDispatch_AtSenderror_ReplacesPersistedRecord()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        // Bestehender persistierter Plan (alter Text/Zielzeit), der ersetzt werden soll.
        await sut.SchedulePromptAsync(workItemId, "AlterPlan", _timeProvider.GetUtcNow().AddMinutes(10));
        (await ReadRecordsAsync(provider, workItemId)).Should().HaveCount(1);

        // Vergangene Zielzeit → Sofortversand; ohne Session schlägt er fehl und der Plan
        // geht in „wartet auf Session" über — mit dem NEUEN Plan, nicht dem alten Datensatz.
        var newTargettime = _timeProvider.GetUtcNow().AddMinutes(-1);
        await sut.SchedulePromptAsync(workItemId, "NeuerPlan", newTargettime);

        var records = await ReadRecordsAsync(provider, workItemId);
        records.Should().HaveCount(1);
        records[0].PromptText.Should().Be(
            "NeuerPlan",
            "der fehlgeschlagene Sofortversand muss den neuen Plan persistieren, damit ein Neustart nicht den alten Datensatz wiederherstellt");
        // BeCloseTo statt Be: ZielZeitpunktUtc läuft durch den Unix-Millisekunden-Converter.
        records[0].TargetTimeUtc.Should().BeCloseTo(newTargettime, TimeSpan.FromSeconds(1));
    }

    /// <summary>Wird während eines in-flight-Versands ein neuer Plan persistiert, darf der alte Versand den neuen Datensatz nicht mitlöschen (Lost-Update-Schutz).</summary>
    [Fact]
    public async Task Dispatch_DeletesNotMeanwhileNewPlannedRecord()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        // Session, deren Schreibvorgang sich kontrollieren lässt — simuliert den in-flight-Versand.
        var writeStarted = new TaskCompletionSource();
        var writeBlock = new TaskCompletionSource();
        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                writeStarted.TrySetResult();
                return writeBlock.Task;
            });
        RegisterSession(workItemId, sessionMock.Object);

        await sut.SchedulePromptAsync(workItemId, "AlterPlan", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var angekommen = await Task.WhenAny(writeStarted.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        angekommen.Should().Be(writeStarted.Task, "der Timer-Versand muss in-flight sein, bevor neu geplant wird");

        // Während der Versand an der Session hängt: neuen Plan derselben Aufgabe planen und persistieren.
        var newTargettime = _timeProvider.GetUtcNow().AddMinutes(10);
        await sut.SchedulePromptAsync(workItemId, "NeuerPlan", newTargettime);

        writeBlock.SetResult();
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        var records = await ReadRecordsAsync(provider, workItemId);
        records.Should().HaveCount(1);
        records[0].PromptText.Should().Be(
            "NeuerPlan",
            "ein während des Versands neu persistierter Plan darf vom alten Versand nicht mitgelöscht werden");
        records[0].TargetTimeUtc.Should().BeCloseTo(newTargettime, TimeSpan.FromSeconds(1));
        sut.GetScheduledPromptStatus(workItemId)!.PromptText.Should().Be("NeuerPlan");
    }

    /// <summary>Schlägt ein in-flight-Versand fehl, nachdem zwischenzeitlich ein neuer Plan eingetragen wurde, darf die Wartestellungs-Markierung den neuen Eintrag nicht überschreiben — sonst sendet der verwaiste neue Timer den alten Text zur neuen Zielzeit.</summary>
    [Fact]
    public async Task Senderror_OverwritesNotMeanwhileNewPlannedEntry()
    {
        var workItemId = Guid.NewGuid();

        var writeStarted = new TaskCompletionSource();
        var writeBlock = new TaskCompletionSource();
        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                writeStarted.TrySetResult();
                await writeBlock.Task;
                throw new InvalidOperationException("Sendefehler");
            });
        RegisterSession(workItemId, sessionMock.Object);

        await _sut.SchedulePromptAsync(workItemId, "AlterPlan", _timeProvider.GetUtcNow().AddMinutes(5));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var angekommen = await Task.WhenAny(writeStarted.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        angekommen.Should().Be(writeStarted.Task, "der Timer-Versand muss in-flight sein, bevor neu geplant wird");

        // Während der Versand hängt: neuen Plan mit eigenem Timer eintragen.
        var newTargettime = _timeProvider.GetUtcNow().AddMinutes(10);
        await _sut.SchedulePromptAsync(workItemId, "NeuerPlan", newTargettime);

        // Jetzt fehlschlagen lassen: Die Wartestellungs-Markierung des alten Versands darf den
        // neuen Eintrag nicht zurückschreiben (Guard wie bei der Pausen-Umplanung).
        writeBlock.SetResult();
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        _sut.GetScheduledPromptStatus(workItemId)!.PromptText.Should().Be(
            "NeuerPlan",
            "der zwischenzeitlich geplante Eintrag darf vom fehlgeschlagenen Alt-Versand nicht überschrieben werden");

        // Session wieder funktionsfähig: Der neue Timer muss zur neuen Zielzeit den NEUEN Text senden.
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

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
    public async Task Restore_FaultyRecord_BlocksFurtherRestoresNot()
    {
        var workItemId = Guid.NewGuid();
        var defectWorkItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
            var projectId = (await db.WorkItems.FindAsync(new object?[] { workItemId }, TestContext.Current.CancellationToken))!.ProjectId;
            db.WorkItems.Add(new Softwareschmiede.Domain.Entities.WorkItem
            {
                Id = defectWorkItemId,
                ProjectId = projectId,
                Title = "Defekte Aufgabe",
                Status = Softwareschmiede.Domain.Enums.WorkItemStatus.Started,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var store = scope.ServiceProvider.GetRequiredService<ScheduledPromptStore>();
            // Defekter Plan zuerst (Insertionsreihenfolge): Die Zielzeit liegt über dem maximalen
            // Timer-Intervall (~49,7 Tage), sodass das Armiieren des Timers beim Re-Arm wirft.
            await store.UpsertAsync(defectWorkItemId, "DefekterPlan", _timeProvider.GetUtcNow().AddYears(1), TestContext.Current.CancellationToken);
            await store.UpsertAsync(workItemId, "GuterPlan", _timeProvider.GetUtcNow().AddMinutes(5), TestContext.Current.CancellationToken);
        }

        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        var act = () => sut.RestoreScheduledPromptsAsync();

        await act.Should().NotThrowAsync();
        var status = sut.GetScheduledPromptStatus(workItemId);
        status.Should().NotBeNull(
            "ein fehlschlagender Datensatz darf die Wiederherstellung der übrigen Pläne nicht blockieren");
        status!.PromptText.Should().Be("GuterPlan");
        sut.GetScheduledPromptStatus(defectWorkItemId).Should().BeNull(
            "der nicht armierte defekte Plan darf keinen untoten Warteschlangeneintrag hinterlassen");
    }

    /// <summary>Schlägt ein in-flight-Sofortversand fehl, nachdem zwischenzeitlich ein neuer Plan persistiert wurde, darf der Fehlerpfad des alten Versands den neuen Datensatz nicht mit dem alten Plan überschreiben — sonst überlebt eine Leiche, die nach einem Neustart zugestellt wird.</summary>
    [Fact]
    public async Task ImmediatelyDispatch_Senderror_OverwritesNotMeanwhileNewPlannedRecord()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        // Session, deren Schreibvorgang blockiert und danach fehlschlägt — in-flight-Sofortversand.
        var writeStarted = new TaskCompletionSource();
        var writeBlock = new TaskCompletionSource();
        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                writeStarted.TrySetResult();
                await writeBlock.Task;
                throw new InvalidOperationException("Sendefehler");
            });
        RegisterSession(workItemId, sessionMock.Object);

        // Zielzeit in der Vergangenheit → Sofortversand, der in WritePromptAsync hängt.
        var immediatelyPlan = sut.SchedulePromptAsync(workItemId, "AlterPlan", _timeProvider.GetUtcNow().AddMinutes(-1));
        var angekommen = await Task.WhenAny(writeStarted.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        angekommen.Should().Be(writeStarted.Task, "der Sofortversand muss in-flight sein, bevor neu geplant wird");

        // Während der Versand hängt: neuen Plan derselben Aufgabe planen und persistieren.
        var newTargettime = _timeProvider.GetUtcNow().AddMinutes(10);
        await sut.SchedulePromptAsync(workItemId, "NeuerPlan", newTargettime);

        // Jetzt fehlschlagen lassen: Der Upsert des Alt-Versands darf den neuen Datensatz
        // nicht überschreiben (konditionaler Schreibzugriff analog zum In-Memory-Guard).
        writeBlock.SetResult();
        await immediatelyPlan;

        var records = await ReadRecordsAsync(provider, workItemId);
        records.Should().HaveCount(1);
        records[0].PromptText.Should().Be(
            "NeuerPlan",
            "der fehlgeschlagene Alt-Versand darf den zwischenzeitlich persistierten neuen Datensatz nicht überschreiben");
        records[0].TargetTimeUtc.Should().BeCloseTo(newTargettime, TimeSpan.FromSeconds(1));
        sut.GetScheduledPromptStatus(workItemId)!.PromptText.Should().Be("NeuerPlan");
    }

    /// <summary>Ist der persistierte Datensatz vom In-Memory-Stand abgedriftet (z. B. weil ein früherer Store-Schreibzugriff fehlschlug und nur geloggt wurde), muss er nach erfolgreichem Versand trotzdem gelöscht werden — sonst wird der bereits zugestellte Prompt nach einem Neustart ein zweites Mal zugestellt.</summary>
    [Fact]
    public async Task Dispatch_DeletesDriftedRecord_WhenNoPlanRegistered()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        RegisterSession(workItemId);
        var versendet = new TaskCompletionSource<Guid>();
        sut.PromptSent += id => versendet.TrySetResult(id);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        // Store-Drift simulieren: Der Datensatz entspricht nicht mehr dem geplanten Stand
        // (so sähe er aus, wenn ein früherer Upsert/UpdateZielZeitpunkt fehlschlug).
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
            (await db.ScheduledPrompts.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)).PromptText = "AbgedrifteterText";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(versendet.Task);
        (await ReadRecordsAsync(provider, workItemId)).Should().BeEmpty(
            "ein vom versendeten Plan abweichender Datensatz ohne lebenden In-Memory-Plan ist eine Leiche und darf nicht überleben");
        sut.GetScheduledPromptStatus(workItemId).Should().BeNull();
    }

    /// <summary>Dispose muss armierte Timer entschärfen und die Warteschlange leeren — eine disposed Instanz (simulierter App-Stop) darf keine Prompts mehr versenden; der persistierte Datensatz bleibt für den Restore beim nächsten Start bestehen.</summary>
    [Fact]
    public async Task Dispose_DefusedArmedTimer()
    {
        var workItemId = Guid.NewGuid();
        await using var provider = await CreateStoreProviderAsync(null, workItemId);
        var sut = new PromptScheduleDispatchService(
            _aiService,
            _timeProvider,
            NullLogger<PromptScheduleDispatchService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        var sessionMock = new Mock<ITerminalSession>();
        sessionMock
            .Setup(s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterSession(workItemId, sessionMock.Object);

        await sut.SchedulePromptAsync(workItemId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));
        (await ReadRecordsAsync(provider, workItemId)).Should().HaveCount(1);

        sut.Dispose();
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        sessionMock.Verify(
            s => s.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "eine disposed Instanz darf keine armierten Timer mehr feuern");
        sut.GetScheduledPromptStatus(workItemId).Should().BeNull(
            "die Warteschlange muss beim Dispose geleert werden");
        (await ReadRecordsAsync(provider, workItemId)).Should().HaveCount(1,
            "der persistierte Datensatz bleibt für den Restore beim nächsten Start bewusst bestehen");
    }
}
