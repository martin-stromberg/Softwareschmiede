using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests der <see cref="RunningAutomationsCount"/>: fachliche Klassifikation über die
/// gemeinsame <see cref="AiExecutionStatusClassification"/> — nur der Zustand
/// <see cref="AiExecutionState.Running"/> zählt als „laufend".
/// </summary>
public sealed class LaufendeAutomatisierungenZaehlungTests
{
    private static RunningAutomationsCount ErstelleZaehlung(
        List<WorkItem> workItems,
        IRunningAutomationStatusSource statusSource,
        out ServiceProvider provider)
        => ErstelleZaehlung(workItems, statusSource, out provider, out _, out _);

    private static RunningAutomationsCount ErstelleZaehlung(
        List<WorkItem> workItems,
        IRunningAutomationStatusSource statusSource,
        out ServiceProvider provider,
        out PromptScheduleDispatchService promptScheduleDispatchService)
        => ErstelleZaehlung(workItems, statusSource, out provider, out promptScheduleDispatchService, out _);

    private static RunningAutomationsCount ErstelleZaehlung(
        List<WorkItem> workItems,
        IRunningAutomationStatusSource statusSource,
        out ServiceProvider provider,
        out PromptScheduleDispatchService promptScheduleDispatchService,
        out SoftwareschmiedeDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IActiveWorkItemsService>(_ => new FakeAktiveAufgabenService(workItems));
        services.AddSingleton(db = TestDbContextFactory.Create());
        provider = services.BuildServiceProvider();

        var aiService = TestKiAusfuehrungsServiceFactory.Create();
        promptScheduleDispatchService = new PromptScheduleDispatchService(
            aiService,
            TimeProvider.System,
            NullLogger<PromptScheduleDispatchService>.Instance);

        return new RunningAutomationsCount(
            provider.GetRequiredService<IServiceScopeFactory>(),
            statusSource,
            promptScheduleDispatchService);
    }

    private static WorkItem ErstelleLaufendeAufgabe() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Laufende Aufgabe",
        Status = WorkItemStatus.Started,
        ExecutionStatus = WorkItemExecutionStatus.Active,
        ActiveRunId = Guid.NewGuid().ToString("N"),
        LastHeartbeatUtc = DateTimeOffset.UtcNow
    };

    /// <summary>Ohne aktive Aufgaben ist die Anzahl 0.</summary>
    [Fact]
    public async Task ZaehleAsync_LiefertNull_BeiLeererKandidatenmenge()
    {
        var sut = ErstelleZaehlung([], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>Eine Aufgabe mit aktivem Lauf-Nachweis zaehlt als laufend.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltAufgabeMitAktivemLauf()
    {
        var sut = ErstelleZaehlung([ErstelleLaufendeAufgabe()], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>Persistierter Substatus WartetAufEingabe zaehlt nicht als laufend.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_WennPersistierterLaufstatusWartetAufEingabe()
    {
        var workItem = ErstelleLaufendeAufgabe();
        workItem.RunStatus = WorkItemRunStatus.WaitingForInput;
        var sut = ErstelleZaehlung([workItem], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>Eine wartende Aufgabe ohne Lauf-Nachweis zaehlt nicht.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiWartendemAufgabenstatus()
    {
        var workItem = ErstelleLaufendeAufgabe();
        workItem.Status = WorkItemStatus.Waiting;
        workItem.LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10); // abgelaufener Heartbeat
        var sut = ErstelleZaehlung([workItem], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>Eine pausierte Aufgabe zaehlt nicht als laufend.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiPausierterAufgabe()
    {
        var workItem = ErstelleLaufendeAufgabe();
        workItem.PausedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        var sut = ErstelleZaehlung([workItem], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>Eine beendete Aufgabe zaehlt nicht.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiBeendeterAusfuehrung()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Beendete Aufgabe",
            Status = WorkItemStatus.Completed,
            ExecutionStatus = WorkItemExecutionStatus.Completed
        };
        var sut = ErstelleZaehlung([workItem], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>Live-Status Laeuft schlaegt auf Laeuft durch und zaehlt.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltLiveStatus_LaeuftUeberPersistenzHinweg()
    {
        // Ohne Herzschlag-Nachweis wäre der persistierte Zustand "Bereit"; der Live-Substatus
        // schlägt auf Laeuft durch und muss fachlich zählen.
        var workItem = ErstelleLaufendeAufgabe();
        var statusSource = new FakeStatusSource();
        statusSource.LiveStatus[workItem.Id] = WorkItemRunStatus.Running;
        var sut = ErstelleZaehlung([workItem], statusSource, out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>Live-Status WartetAufEingabe zaehlt nicht (wartender Prozess blockiert nicht).</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiLiveStatusWartetAufEingabe()
    {
        // Wartender physischer CLI-Prozess: fachlich "Wartet" — blockiert den Countdown nicht.
        var workItem = ErstelleLaufendeAufgabe();
        var statusSource = new FakeStatusSource();
        statusSource.LiveStatus[workItem.Id] = WorkItemRunStatus.WaitingForInput;
        var sut = ErstelleZaehlung([workItem], statusSource, out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// Eine Aufgabe mit zeitgesteuertem Prompt in Wartestellung wird als
    /// <see cref="AiExecutionState.PromptQueued"/> klassifiziert und zaehlt nicht —
    /// auch dann, wenn ihr persistierter Lauf-Beleg ansonsten aktiv wäre.
    /// </summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiPromptInWartestellung()
    {
        var workItem = ErstelleLaufendeAufgabe();
        var sut = ErstelleZaehlung([workItem], new FakeStatusSource(), out var provider, out var promptScheduleDispatchService);
        using var _ = provider;
        using var promptService = promptScheduleDispatchService;

        await promptService.SchedulePromptAsync(workItem.Id, "spaeter senden", DateTimeOffset.UtcNow.AddMinutes(10));
        promptService.GetScheduledPromptStatus(workItem.Id).Should().NotBeNull();

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>In einer gemischten Kandidatenmenge zaehlt nur der Zustand Laeuft.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltGemischteMengeKorrekt()
    {
        var laufend = ErstelleLaufendeAufgabe();
        var waiting = ErstelleLaufendeAufgabe();
        waiting.RunStatus = WorkItemRunStatus.WaitingForInput;
        var pausiert = ErstelleLaufendeAufgabe();
        pausiert.PausedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        var bereit = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Bereite Aufgabe",
            Status = WorkItemStatus.Completed,
            ExecutionStatus = WorkItemExecutionStatus.Completed
        };
        var sut = ErstelleZaehlung([laufend, waiting, pausiert, bereit], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>Ein verzögert ausstehendes SCM-Ereignis zählt als ausstehende
    /// Automatisierung und blockiert damit den Auto-Shutdown-Countdown.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltVerzoegertesScmEreignis()
    {
        var sut = ErstelleZaehlung([], new FakeStatusSource(), out var provider, out var promptService, out var db);
        using var _ = provider;
        using var prompt = promptService;
        db.ScmIncomingEvents.Add(new ScmIncomingEvent
        {
            WorkItemId = Guid.NewGuid(),
            EventKey = "verzoegert/1",
            Status = "Verzögert",
            NextAttemptUtc = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds()
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>Abgeschlossene oder abgebrochene SCM-Ereignisse zählen nicht.</summary>
    [Theory]
    [InlineData("Verarbeitet")]
    [InlineData("Abgebrochen")]
    [InlineData("Wartend")]
    [InlineData("Aufgenommen")]
    public async Task ZaehleAsync_ZaehltNicht_BeiNichtVerzoegertemEreignisstatus(string status)
    {
        var sut = ErstelleZaehlung([], new FakeStatusSource(), out var provider, out var promptService, out var db);
        using var _ = provider;
        using var prompt = promptService;
        db.ScmIncomingEvents.Add(new ScmIncomingEvent
        {
            WorkItemId = Guid.NewGuid(),
            EventKey = $"status/{status}",
            Status = status
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await sut.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    private sealed class FakeAktiveAufgabenService(List<WorkItem> workItems) : IActiveWorkItemsService
    {
        public Task<List<WorkItem>> GetActiveWorkItemsAsync(CancellationToken ct = default)
            => Task.FromResult(workItems);
    }

    private sealed class FakeStatusSource : IRunningAutomationStatusSource
    {
        public Dictionary<Guid, WorkItemRunStatus> LiveStatus { get; } = new();
#pragma warning disable CS0067 // Event ist Teil der Schnittstelle, wird in den Tests nicht ausgelöst.
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        public int GetRunningCount() => LiveStatus.Count;
        public bool IsRunning(Guid workItemId) => LiveStatus.ContainsKey(workItemId);
        public WorkItemRunStatus? GetLiveRunStatus(Guid workItemId)
            => LiveStatus.TryGetValue(workItemId, out var status) ? status : null;
    }
}
