using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Ausführungsbegrenzung (Issue 410, Schritt 2): globales und Plugin-Parallel-Limit,
/// ehrlicher Wartegrund, Nachrücken bei freiem Platz, priorisierte Polling-Auswahl und
/// Unbegrenztheit von Reconcile/Hostaktionen.</summary>
public sealed class ScmEventDispatchTests_Limit
{
    /// <summary>Bei erreichtem globalem Limit startet kein zweiter CLI-Prozess; das Ereignis
    /// wartet mit ehrlichem Grund und ohne harte Zurückstellung.</summary>
    [Fact]
    public async Task Dispatch_GlobalLimitReached_EventWaitsWithReason()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);
        Assert.Single(fixture.Sessions);

        await fixture.DispatchAsync(secondId);

        var item = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("globale Limit paralleler CLI-Ausführungen", item.Reason);
        Assert.NotEqual(long.MaxValue, item.NextAttemptUtc ?? 0);
        Assert.Single(fixture.Sessions);
    }

    /// <summary>Nach dem Freiwerden eines Platzes startet das wartende Ereignis regulär.</summary>
    [Fact]
    public async Task Dispatch_SlotFreed_WaitingEventStarts()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        await fixture.DispatchAsync(secondId);
        Assert.Equal("Wartend", (await fixture.EventAsync(secondId)).Status);

        await fixture.Cli.StopCliAsync(firstTask, TestContext.Current.CancellationToken);
        await fixture.DispatchAsync(secondId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
        Assert.Equal(2, fixture.Sessions.Count);
    }

    /// <summary>Das Plugin-Limit sperrt nur das betroffene Plugin; ein anderes Plugin startet weiter.</summary>
    [Fact]
    public async Task Dispatch_PluginLimitReached_OnlyThatPluginWaits()
    {
        await using var fixture = new ScmDispatchFixture(ki2: ScmAiProfile.Devin());
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(ScmDispatchFixture.AiPrefix), "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        var (thirdTask, thirdId) = await fixture.AddAsync(configured: true, aiPluginPrefix: fixture.Ki2Profile!.Prefix);

        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);

        // Gleiches Plugin: Limit erreicht → Wartend mit Plugin-Grund.
        await fixture.DispatchAsync(secondId);
        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains($"'{ScmDispatchFixture.AiPrefix}'", second.Reason);

        // Anderes Plugin: startet trotzdem (zweite Sitzung), obwohl Plugin A am Limit ist.
        await fixture.DispatchAsync(thirdId);
        Assert.Equal(2, fixture.Sessions.Count);
        Assert.True(fixture.Cli.GetScmPromptSession(thirdTask)?.IsAlive);
        Assert.DoesNotContain("Limit", (await fixture.EventAsync(thirdId)).Reason ?? string.Empty);
    }

    /// <summary>Beide Limits gelten gleichzeitig: Bei freiem Plugin-Platz entscheidet das globale Limit.</summary>
    [Fact]
    public async Task Dispatch_BothLimit_GlobalDecidesWhenReached()
    {
        await using var fixture = new ScmDispatchFixture(ki2: ScmAiProfile.Devin());
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(fixture.Ki2Profile!.Prefix), "5");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true, aiPluginPrefix: fixture.Ki2Profile.Prefix);

        await fixture.DispatchAsync(firstId);
        await fixture.DispatchAsync(secondId);

        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains("globale Limit", second.Reason);
        Assert.Single(fixture.Sessions);
    }

    /// <summary>Die Reconciliation eines begonnenen Zustellversuchs ist von der Begrenzung
    /// nicht betroffen: Sie läuft auch bei belegtem Platz und startet keinen neuen Prozess.</summary>
    [Fact]
    public async Task Dispatch_ReconcileAttempt_IgnoresLimit()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);

        var (_, secondId) = await fixture.AddAsync(configured: true);
        var attemptId = Guid.NewGuid();
        var exchangeDir = Path.Combine(fixture.Root, "exchange-" + attemptId.ToString("N"));
        Directory.CreateDirectory(exchangeDir);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            await db.ScmIncomingEvents.Where(x => x.Id == secondId).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, "Zugestellt")
                .SetProperty(p => p.DeliveryAttemptId, attemptId)
                .SetProperty(p => p.CliRunId, Guid.NewGuid().ToString("D"))
                .SetProperty(p => p.ExchangePath, exchangeDir)
                .SetProperty(p => p.ExchangeRestrictedWriteSidsJson, "[]")
                .SetProperty(p => p.Attempts, 1), cancellationToken: TestContext.Current.CancellationToken);
        }

        await fixture.DispatchAsync(secondId);

        var second = await fixture.EventAsync(secondId);
        Assert.DoesNotContain("Limit", second.Reason ?? string.Empty);
        Assert.Single(fixture.Sessions);
    }

    /// <summary>Ein Ereignis an einer beendeten/archivierten Aufgabe kann nie zugestellt werden:
    /// Es erhält den terminalen Klärungsstatus statt dauerhaft zu warten — auch bei freiem Platz.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Archived)]
    public async Task Dispatch_EndedOrArchivedTask_MarkedUndeliverable(WorkItemStatus status)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "10");
        var (_, eventId) = await fixture.AddAsync(status, configured: true);

        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("VerarbeitungUngeklärt", item.Status);
        Assert.Equal("Die Aufgabe ist beendet/archiviert; das Ereignis kann nicht mehr zugestellt werden.", item.Reason);

        // Auch wiederholte Prüfung reaktiviert nichts.
        await fixture.DispatchAsync(eventId);
        Assert.Equal("VerarbeitungUngeklärt", (await fixture.EventAsync(eventId)).Status);
        Assert.Empty(fixture.Sessions);
    }

    /// <summary>Ein Ereignis an einer entfernten Aufgabe ist strukturell unzustellbar und
    /// erhält den terminalen Klärungsstatus statt dauerhaft zu warten.</summary>
    [Fact]
    public async Task Dispatch_RemovedTask_MarkedUndeliverable()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().WorkItems
                .Where(x => x.Id == taskId).ExecuteDeleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        }

        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("VerarbeitungUngeklärt", item.Status);
        Assert.Contains("entfernt", item.Reason);
        Assert.Empty(fixture.Sessions);
    }

    /// <summary>Ein Ereignis an einer Aufgabe ohne Repository-Zuordnung kann nie zugestellt
    /// werden und erhält den terminalen Klärungsstatus mit Handlungsanweisung.</summary>
    [Fact]
    public async Task Dispatch_TaskWithoutRepository_MarkedUndeliverable()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().WorkItems
                .Where(x => x.Id == taskId).ExecuteUpdateAsync(x => x.SetProperty(p => p.GitRepositoryId, (Guid?)null),
                    cancellationToken: TestContext.Current.CancellationToken);
        }

        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("VerarbeitungUngeklärt", item.Status);
        Assert.Contains("kein Git-Repository", item.Reason);
        Assert.Empty(fixture.Sessions);
    }

    /// <summary>Ein wartendes Ereignis einer Prompt-bereiten Aufgabe hält den Startplatz nicht
    /// belegt: Die Sitzung wartet ehrlich auf Eingabe, ein zweites Ereignis darf starten.</summary>
    [Fact]
    public async Task Dispatch_ReadySessionWaitingEvent_SlotFreedForNextStart()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);

        // Ereignis in den nicht weiterbeweglichen Wartezustand versetzen (manueller Neuversand
        // nötig); die Sitzung kehrt ans verifizierte Eingabeprompt zurück.
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().ScmIncomingEvents
                .Where(x => x.Id == firstId).ExecuteUpdateAsync(x => x
                    .SetProperty(p => p.Status, "Wartend")
                    .SetProperty(p => p.NextAttemptUtc, (long?)long.MaxValue),
                    cancellationToken: TestContext.Current.CancellationToken);
        }
        fixture.SetPrompt(firstTask, fixture.Prompt);

        await fixture.DispatchAsync(secondId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
        Assert.Equal(2, fixture.Sessions.Count);
    }

    /// <summary>Ein terminal klärbares Ereignis einer Prompt-bereiten Aufgabe hält den
    /// Startplatz ebenfalls nicht belegt.</summary>
    [Fact]
    public async Task Dispatch_ReadySessionUnclarifiedEvent_SlotFreedForNextStart()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);

        await fixture.SetEventStatusAsync(firstId, "VerarbeitungUngeklärt");
        fixture.SetPrompt(firstTask, fixture.Prompt);

        await fixture.DispatchAsync(secondId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
        Assert.Equal(2, fixture.Sessions.Count);
    }

    /// <summary>Der Polling-Lauf priorisiert Fortsetzungen vor fälligen Verzögerungen (zeitaufsteigend)
    /// vor der Eingangsreihenfolge — unabhängig von der Sequence der Ereignisse.</summary>
    [Fact]
    public async Task Monitor_PrioritizedDispatch_ContinuationThenDelayThenSequence()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        // Eingangsreihenfolge absichtlich invers zur Priorität: regulär zuerst, Fortsetzung zuletzt.
        var (regularTask, _) = await fixture.AddAsync(configured: true);
        var (delayedEarlyTask, delayedEarlyId) = await fixture.AddAsync(configured: true);
        var (delayedLateTask, delayedLateId) = await fixture.AddAsync(configured: true);
        var (continuationTask, _) = await fixture.AddAsync(configured: true);
        var now = DateTimeOffset.UtcNow;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            await db.ScmIncomingEvents.Where(x => x.Id == delayedEarlyId).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, "Verzögert")
                .SetProperty(p => p.NextAttemptUtc, (long?)now.AddMinutes(-10).ToUnixTimeMilliseconds()), cancellationToken: TestContext.Current.CancellationToken);
            await db.ScmIncomingEvents.Where(x => x.Id == delayedLateId).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, "Verzögert")
                .SetProperty(p => p.NextAttemptUtc, (long?)now.AddMinutes(-5).ToUnixTimeMilliseconds()), cancellationToken: TestContext.Current.CancellationToken);
            await db.WorkItems.Where(x => x.Id == continuationTask).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, WorkItemStatus.Started)
                .SetProperty(p => p.ExecutionStatus, WorkItemExecutionStatus.Completed), cancellationToken: TestContext.Current.CancellationToken);
        }

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        var startOrder = fixture.StartSpecs.Select(x => Path.GetFileName(x.WorkingDirectory)).ToList();
        var expectedOrder = new[]
        {
            continuationTask.ToString("N"),
            delayedEarlyTask.ToString("N"),
            delayedLateTask.ToString("N"),
            regularTask.ToString("N")
        };
        Assert.Equal(expectedOrder, startOrder);
        Assert.Equal(4, fixture.Sessions.Count);
    }

    /// <summary>Bei einem einzigen freien Platz gewinnt das Fortsetzungsereignis den ersten Start
    /// vor dem regulären Ereignis mit kleinerer Sequence. Da die frisch gestartete Sitzung jedoch
    /// ehrlich am Eingabeprompt wartet (Stabilisierungsfrist vor der Zustellung), ist der Platz
    /// sofort wieder frei — das reguläre Ereignis darf ebenfalls starten.</summary>
    [Fact]
    public async Task Monitor_GlobalLimitOne_ContinuationWinsSlotOverSequence()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (regularTask, _) = await fixture.AddAsync(configured: true);
        var (continuationTask, continuationId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            await db.WorkItems.Where(x => x.Id == continuationTask).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, WorkItemStatus.Started)
                .SetProperty(p => p.ExecutionStatus, WorkItemExecutionStatus.Completed), cancellationToken: TestContext.Current.CancellationToken);
        }

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        // Priorität gewinnt den ersten Start; die prompt-bereite Sitzung mit noch wartendem
        // Ereignis belegt den Platz jedoch nicht — das reguläre Ereignis startet ebenfalls.
        Assert.Equal(2, fixture.Sessions.Count);
        Assert.Equal(continuationTask.ToString("N"), Path.GetFileName(fixture.StartSpecs[0].WorkingDirectory));
        var continuation = await fixture.EventAsync(continuationId);
        Assert.Equal("Wartend", continuation.Status);
        Assert.Contains("Stabilisierungsphase", continuation.Reason);
    }

    /// <summary>Der UI-Pfad „aus der Queue öffnen" läuft durch dieselbe Zulassungskontrolle.</summary>
    [Fact]
    public async Task OpenFromQueue_LimitReached_WaitsWithReason()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        Assert.Single(fixture.Sessions);

        using (var scope = fixture.Services.CreateScope())
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>();
            await orchestrator.OpenFromQueueAsync(secondId, (_, _, _) => Task.FromResult<string?>(ScmDispatchFixture.AiPrefix), TestContext.Current.CancellationToken);
        }

        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains("globale Limit", second.Reason);
        Assert.Single(fixture.Sessions);
    }

    /// <summary>Atomare Zulassung: Ein Dispatch, der den Platz reserviert hat, aber noch mitten
    /// im Startfenster steht (Health-Check, noch kein Prozess-Handle registriert), belegt den
    /// Platz bereits — ein gleichzeitig einlaufender Dispatch einer anderen Aufgabe wartet mit
    /// Limit-Grund statt das Limit transient zu überschreiten.</summary>
    [Fact]
    public async Task Dispatch_ConcurrentStartDuringHealthCheck_GlobalLimitOne_ExactlyOneStarts()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);

        // Der Health-Check öffnet das reale Startfenster zwischen Zulassung und Handle-
        // Registrierung kontrolliert: Der erste Dispatch hält die Platzreservierung,
        // während der zweite die Zulassungskontrolle vollständig passiert.
        var healthCheckEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHealthCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Ai.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _) =>
            {
                healthCheckEntered.TrySetResult();
                await releaseHealthCheck.Task;
                return true;
            });

        var first = Task.Run(() => fixture.DispatchAsync(firstId), TestContext.Current.CancellationToken);
        await healthCheckEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Zweiter Dispatch während des offenen Startfensters: kein Prozess-Handle aktiv.
        await fixture.DispatchAsync(secondId);

        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains("globale Limit", second.Reason);
        Assert.Empty(fixture.Sessions);

        releaseHealthCheck.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Single(fixture.Sessions);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);
        Assert.Equal("Wartend", (await fixture.EventAsync(secondId)).Status);
    }

    /// <summary>Ein fehlschlagender Start gibt die Reservierung wieder frei: Der nächste
    /// Dispatch darf den Platz sofort neu belegen statt dauerhaft zu warten.</summary>
    [Fact]
    public async Task Dispatch_StartFails_ReservationReleasedForNextDispatch()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);

        fixture.Ai.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await fixture.DispatchAsync(firstId);
        var first = await fixture.EventAsync(firstId);
        Assert.Equal("Wartend", first.Status);
        Assert.Contains("nicht startbereit", first.Reason);
        Assert.Empty(fixture.Sessions);

        fixture.Ai.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await fixture.DispatchAsync(secondId);

        Assert.Single(fixture.Sessions);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
    }

    /// <summary>Host-Zustellaktionen (Empfangsquittung/Wartekommentar) laufen unbegrenzt:
    /// Bei belegtem Platz werden sie dennoch ausgeführt.</summary>
    [Fact]
    public async Task Dispatch_HostActions_UnlimitedWhileSlotOccupied()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        var responsesBefore = fixture.Responses.Count;

        await fixture.DispatchAsync(secondId);

        Assert.Equal("Wartend", (await fixture.EventAsync(secondId)).Status);
        // Empfangs-Reaktion der zweiten Beobachtung wurde trotz belegtem Platz ausgeführt.
        Assert.True(fixture.Responses.Count > responsesBefore);
        Assert.Single(fixture.Sessions);
    }

    /// <summary>Eine pausierte Aufgabe gibt ihren globalen Ausführungsplatz frei: Der
    /// laufende Prozess bleibt erhalten, aber das wartende Ereignis startet sofort.</summary>
    [Fact]
    public async Task Dispatch_TaskPaused_SlotFreed_WaitingEventStarts()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        await fixture.DispatchAsync(secondId);
        Assert.Equal("Wartend", (await fixture.EventAsync(secondId)).Status);

        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WorkItemService>()
                .SetPauseAsync(firstTask, DateTimeOffset.UtcNow.AddMinutes(30), TestContext.Current.CancellationToken);
        }
        await fixture.DispatchAsync(secondId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
        Assert.Equal(2, fixture.Sessions.Count);
        Assert.True(fixture.Cli.IsRunning(firstTask));
    }

    /// <summary>Eine pausierte Aufgabe gibt auch den Plugin-Platz frei: Das wartende
    /// Ereignis desselben Plugins startet, obwohl der Prozess weiterläuft.</summary>
    [Fact]
    public async Task Dispatch_TaskPaused_PluginSlotFreed_WaitingEventStarts()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(ScmDispatchFixture.AiPrefix), "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        await fixture.DispatchAsync(secondId);
        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains($"'{ScmDispatchFixture.AiPrefix}'", second.Reason);

        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WorkItemService>()
                .SetPauseAsync(firstTask, DateTimeOffset.UtcNow.AddMinutes(30), TestContext.Current.CancellationToken);
        }
        await fixture.DispatchAsync(secondId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
        Assert.Equal(2, fixture.Sessions.Count);
    }

    /// <summary>Eine abgelaufene, aber noch gesetzte Pausenzeit belegt den Platz weiterhin —
    /// bei weiterlaufendem Prozess wartet das zweite Ereignis mit Limit-Grund.</summary>
    [Fact]
    public async Task Dispatch_TaskPauseExpired_SlotStillOccupied()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            await db.WorkItems.Where(x => x.Id == firstTask)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.PausedUntilUtc, DateTimeOffset.UtcNow.AddMinutes(-5)),
                    cancellationToken: TestContext.Current.CancellationToken);
        }

        await fixture.DispatchAsync(secondId);

        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains("globale Limit", second.Reason);
        Assert.Single(fixture.Sessions);
    }

    /// <summary>Eine Aufgabe am verifizierten Eingabeprompt ohne offenes Ereignis gibt ihren
    /// Platz frei: Der Übergang nach Ready löst die Startprüfung aus und das wartende Ereignis
    /// startet ereignisgesteuert — ohne Poll und ohne dass der Prozess endet.</summary>
    [Fact]
    public async Task Dispatch_TaskReadyAtPrompt_SlotFreed_WaitingEventStarts()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);
        await fixture.DispatchAsync(secondId);
        Assert.Equal("Wartend", (await fixture.EventAsync(secondId)).Status);
        Assert.Single(fixture.Sessions);

        // Das auslösende Ereignis ist fachlich abgeschlossen — erst jetzt kann der
        // Prompt-Nachweis den Platz freigeben (Pending-Guard).
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            await db.ScmIncomingEvents.Where(x => x.Id == firstId)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.Status, "Verarbeitet"),
                    cancellationToken: TestContext.Current.CancellationToken);
        }

        await fixture.StartCheck.StartAsync(CancellationToken.None);
        try
        {
            // Die CLI beendet ihren Turn und zeigt erneut ihren verifizierten Eingabeprompt.
            fixture.SetPrompt(firstTask, "SCM_READY_V1");

            await fixture.WaitForTriggeredDeliveryAsync(async () => fixture.Sessions.Count == 2
                    && (await fixture.EventAsync(secondId)).Status == "Zugestellt",
                "Das wartende Ereignis wurde nach dem Prompt-Nachweis nicht ereignisgesteuert gestartet.");
            Assert.True(fixture.Cli.IsRunning(firstTask));
        }
        finally
        {
            await fixture.StartCheck.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Der Pending-Guard hält den Platz trotz sichtbarem Eingabeprompt belegt: Solange
    /// ein Ereignis der Aufgabe nicht im Endstatus steht, startet auch die ereignisgesteuerte
    /// Startprüfung keinen Folgelauf.</summary>
    [Fact]
    public async Task Dispatch_TaskReadyWithPendingEvent_SlotStaysOccupied()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);
        await fixture.DispatchAsync(secondId);
        Assert.Equal("Wartend", (await fixture.EventAsync(secondId)).Status);
        Assert.Single(fixture.Sessions);

        // Die CLI zeigt ihren verifizierten Eingabeprompt, das zugestellte Ereignis ist aber
        // noch offen — der Platz bleibt belegt.
        fixture.SetPrompt(firstTask, "SCM_READY_V1");
        await fixture.StartCheck.RunOnceAsync(TestContext.Current.CancellationToken);
        await fixture.DispatchAsync(secondId);

        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains("globale Limit", second.Reason);
        Assert.Single(fixture.Sessions);
    }
}
