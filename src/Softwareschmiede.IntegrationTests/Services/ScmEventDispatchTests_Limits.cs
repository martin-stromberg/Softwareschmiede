using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Ausführungsbegrenzung (Issue 410, Schritt 2): globales und Plugin-Parallel-Limit,
/// ehrlicher Wartegrund, Nachrücken bei freiem Platz, priorisierte Polling-Auswahl und
/// Unbegrenztheit von Reconcile/Hostaktionen.</summary>
public sealed class ScmEventDispatchTests_Limits
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

        await fixture.Cli.StopCliAsync(firstTask);
        await fixture.DispatchAsync(secondId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
        Assert.Equal(2, fixture.Sessions.Count);
    }

    /// <summary>Das Plugin-Limit sperrt nur das betroffene Plugin; ein anderes Plugin startet weiter.</summary>
    [Fact]
    public async Task Dispatch_PluginLimitReached_OnlyThatPluginWaits()
    {
        await using var fixture = new ScmDispatchFixture(ki2: ScmKiProfile.Devin());
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(ScmDispatchFixture.KiPrefix), "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        var (thirdTask, thirdId) = await fixture.AddAsync(configured: true, kiPluginPrefix: fixture.Ki2Profile!.Prefix);

        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);

        // Gleiches Plugin: Limit erreicht → Wartend mit Plugin-Grund.
        await fixture.DispatchAsync(secondId);
        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains($"'{ScmDispatchFixture.KiPrefix}'", second.Reason);

        // Anderes Plugin: startet trotzdem (zweite Sitzung), obwohl Plugin A am Limit ist.
        await fixture.DispatchAsync(thirdId);
        Assert.Equal(2, fixture.Sessions.Count);
        Assert.True(fixture.Cli.GetScmPromptSession(thirdTask)?.IsAlive);
        Assert.DoesNotContain("Limit", (await fixture.EventAsync(thirdId)).Reason ?? string.Empty);
    }

    /// <summary>Beide Limits gelten gleichzeitig: Bei freiem Plugin-Platz entscheidet das globale Limit.</summary>
    [Fact]
    public async Task Dispatch_BothLimits_GlobalDecidesWhenReached()
    {
        await using var fixture = new ScmDispatchFixture(ki2: ScmKiProfile.Devin());
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(fixture.Ki2Profile!.Prefix), "5");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true, kiPluginPrefix: fixture.Ki2Profile.Prefix);

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
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.ScmIncomingEvents.Where(x => x.Id == secondId).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, "Zugestellt")
                .SetProperty(p => p.DeliveryAttemptId, attemptId)
                .SetProperty(p => p.CliRunId, Guid.NewGuid().ToString("D"))
                .SetProperty(p => p.ExchangePath, exchangeDir)
                .SetProperty(p => p.ExchangeRestrictedWriteSidsJson, "[]")
                .SetProperty(p => p.Attempts, 1));
        }

        await fixture.DispatchAsync(secondId);

        var second = await fixture.EventAsync(secondId);
        Assert.DoesNotContain("Limit", second.Reason ?? string.Empty);
        Assert.Single(fixture.Sessions);
    }

    /// <summary>Ein Ereignis an einer beendeten/archivierten Aufgabe bleibt permanent gesperrt — auch bei freiem Platz.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Beendet)]
    [InlineData(AufgabeStatus.Archiviert)]
    public async Task Dispatch_EndedOrArchivedTask_StaysBlocked(AufgabeStatus status)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "10");
        var (_, eventId) = await fixture.AddAsync(status, configured: true);

        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Equal("Aufgabe beendet/archiviert; Start gesperrt.", item.Reason);

        // Auch wiederholte Prüfung reaktiviert nichts.
        await fixture.DispatchAsync(eventId);
        Assert.Equal("Aufgabe beendet/archiviert; Start gesperrt.", (await fixture.EventAsync(eventId)).Reason);
        Assert.Empty(fixture.Sessions);
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
        var (delayedFruehTask, delayedFruehId) = await fixture.AddAsync(configured: true);
        var (delayedSpaetTask, delayedSpaetId) = await fixture.AddAsync(configured: true);
        var (continuationTask, _) = await fixture.AddAsync(configured: true);
        var now = DateTimeOffset.UtcNow;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.ScmIncomingEvents.Where(x => x.Id == delayedFruehId).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, "Verzögert")
                .SetProperty(p => p.NextAttemptUtc, (long?)now.AddMinutes(-10).ToUnixTimeMilliseconds()));
            await db.ScmIncomingEvents.Where(x => x.Id == delayedSpaetId).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, "Verzögert")
                .SetProperty(p => p.NextAttemptUtc, (long?)now.AddMinutes(-5).ToUnixTimeMilliseconds()));
            await db.Aufgaben.Where(x => x.Id == continuationTask).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, AufgabeStatus.Gestartet)
                .SetProperty(p => p.AusfuehrungsStatus, AufgabeAusfuehrungsStatus.Beendet));
        }

        await fixture.Monitor.RunOnceAsync();

        var startOrder = fixture.StartSpecs.Select(x => Path.GetFileName(x.WorkingDirectory)).ToList();
        var expectedOrder = new[]
        {
            continuationTask.ToString("N"),
            delayedFruehTask.ToString("N"),
            delayedSpaetTask.ToString("N"),
            regularTask.ToString("N")
        };
        Assert.Equal(expectedOrder, startOrder);
        Assert.Equal(4, fixture.Sessions.Count);
    }

    /// <summary>Bei einem einzigen freien Platz gewinnt das Fortsetzungsereignis den Platz vor dem
    /// regulären Ereignis mit kleinerer Sequence; dieses wartet mit Limit-Grund.</summary>
    [Fact]
    public async Task Monitor_GlobalLimitOne_ContinuationWinsSlotOverSequence()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (regularTask, regularId) = await fixture.AddAsync(configured: true);
        var (continuationTask, _) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.Aufgaben.Where(x => x.Id == continuationTask).ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, AufgabeStatus.Gestartet)
                .SetProperty(p => p.AusfuehrungsStatus, AufgabeAusfuehrungsStatus.Beendet));
        }

        await fixture.Monitor.RunOnceAsync();

        Assert.Single(fixture.Sessions);
        Assert.True(fixture.Sessions.ContainsKey(continuationTask));
        var regular = await fixture.EventAsync(regularId);
        Assert.Equal("Wartend", regular.Status);
        Assert.Contains("globale Limit", regular.Reason);
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
            await orchestrator.OpenFromQueueAsync(secondId,
                (_, _, _) => Task.FromResult<string?>(ScmDispatchFixture.KiPrefix));
        }

        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains("globale Limit", second.Reason);
        Assert.Single(fixture.Sessions);
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
}
