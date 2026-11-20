using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="WorkItemRecoveryService"/> mit echter SQLite-Datenbank.</summary>
public sealed class AufgabeRecoveryServiceTests
{
    /// <summary>Prüft, dass RecoveryVersion inkrementiert und ein Audit-Eintrag angelegt wird.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldSetGestartetAndCreateAudit_WhenStatusIsGestartet()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItem = await CreateTaskAsync(db, WorkItemStatus.Started);
        var sut = new WorkItemRecoveryService(db.Context, new AlwaysNotRunningAutomationStatusSource(), NullLogger<WorkItemRecoveryService>.Instance);

        await sut.RecoverManuallyAsync(workItem.Id, TestContext.Current.CancellationToken);

        await using var assertContext = db.CreateNewContext();
        var loaded = await assertContext.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
        loaded.RecoveryVersion.Should().Be(1);
        assertContext.LogEntries
            .Count(p => p.WorkItemId == workItem.Id && p.Type == LogEntryType.StatusTransition && p.Content.Contains("Manuelle Wiederherstellung"))
            .Should().Be(1);
    }

    /// <summary>Prüft, dass Recovery abgelehnt wird, wenn der CLI-Prozess noch läuft.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrowAndKeepStatus_WhenProcessingIsStillRunning()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItem = await CreateTaskAsync(db, WorkItemStatus.Started);
        var sut = new WorkItemRecoveryService(db.Context, new AlwaysRunningAutomationStatusSource(), NullLogger<WorkItemRecoveryService>.Instance);

        var act = () => sut.RecoverManuallyAsync(workItem.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung nicht möglich, Verarbeitung läuft noch.");

        await using var assertContext = db.CreateNewContext();
        var loaded = await assertContext.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
        loaded.RecoveryVersion.Should().Be(0);
        assertContext.LogEntries.Count(p => p.WorkItemId == workItem.Id).Should().Be(0);
    }

    /// <summary>Prüft, dass ein Nebenläufigkeitskonflikt erkannt wird, wenn RecoveryVersion sich während der Recovery ändert.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrowConcurrencyConflict_WhenRecoveryVersionChangedConcurrently()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItem = await CreateTaskAsync(db, WorkItemStatus.Started);
        using var concurrentContext = db.CreateNewContext();
        var runningStatus = new MutatingNotRunningAutomationStatusSource(() =>
        {
            var concurrent = concurrentContext.WorkItems.Single(a => a.Id == workItem.Id);
            concurrent.RecoveryVersion++;
            concurrentContext.SaveChanges();
        });
        var sut = new WorkItemRecoveryService(db.Context, runningStatus, NullLogger<WorkItemRecoveryService>.Instance);

        var act = () => sut.RecoverManuallyAsync(workItem.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Status wurde bereits geändert. Ansicht wurde aktualisiert.");

        await using var assertContext = db.CreateNewContext();
        var loaded = await assertContext.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
        loaded.RecoveryVersion.Should().Be(1);
        assertContext.LogEntries.Count(p => p.WorkItemId == workItem.Id).Should().Be(0);
    }

    /// <summary>Prüft, dass Recovery für ungültige Ausgangszustände abgelehnt wird.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldRejectInvalidState()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItem = await CreateTaskAsync(db, WorkItemStatus.New);
        var sut = new WorkItemRecoveryService(db.Context, new AlwaysNotRunningAutomationStatusSource(), NullLogger<WorkItemRecoveryService>.Instance);

        var act = () => sut.RecoverManuallyAsync(workItem.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung für aktuellen Status nicht verfügbar.");
    }

    /// <summary>Prüft, dass bei parallelen Recovery-Versuchen mit demselben Snapshot exakt einer erfolgreich ist.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldAllowExactlyOneSuccess_WhenTriggeredInParallel()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItem = await CreateTaskAsync(db, WorkItemStatus.Started);

        await using var context1 = db.CreateNewContext();
        await using var context2 = db.CreateNewContext();

        // The barrier ensures both tasks complete their initial entity read (RecoveryVersion=0)
        // before either proceeds to the transaction and update. Without it, one task may finish
        // entirely before the other reads, giving the second task a different starting RecoveryVersion
        // and allowing both to succeed.
        var runningStatus = new BarrieredNotRunningStatusSource(participantCount: 2);
        var recovery1 = new WorkItemRecoveryService(context1, runningStatus, NullLogger<WorkItemRecoveryService>.Instance);
        var recovery2 = new WorkItemRecoveryService(context2, runningStatus, NullLogger<WorkItemRecoveryService>.Instance);

        var task1 = Task.Run(() => ExecuteRecoverSafeAsync(recovery1, workItem.Id));
        var task2 = Task.Run(() => ExecuteRecoverSafeAsync(recovery2, workItem.Id));
        var results = await Task.WhenAll(task1, task2);

        results.Count(r => r).Should().Be(1);

        await using var assertContext = db.CreateNewContext();
        var loaded = await assertContext.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
        assertContext.LogEntries
            .Count(p => p.WorkItemId == workItem.Id && p.Type == LogEntryType.StatusTransition && p.Content.Contains("Manuelle Wiederherstellung"))
            .Should().Be(1);
    }

    private static async Task<bool> ExecuteRecoverSafeAsync(WorkItemRecoveryService service, Guid workItemId)
    {
        try
        {
            await service.RecoverManuallyAsync(workItemId);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class AlwaysNotRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        public int GetRunningCount() => 0;
        public bool IsRunning(Guid workItemId) => false;
        public WorkItemRunStatus? GetLiveRunStatus(Guid workItemId) => null;
    }

    private sealed class AlwaysRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        public int GetRunningCount() => 1;
        public bool IsRunning(Guid workItemId) => true;
        public WorkItemRunStatus? GetLiveRunStatus(Guid workItemId) => WorkItemRunStatus.Running;
    }

    private sealed class MutatingNotRunningAutomationStatusSource(Action beforeReturn) : IRunningAutomationStatusSource
    {
        private int _called;
#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        public int GetRunningCount() => 0;

        public bool IsRunning(Guid workItemId)
        {
            if (Interlocked.Exchange(ref _called, 1) == 0)
            {
                beforeReturn();
            }

            return false;
        }

        public WorkItemRunStatus? GetLiveRunStatus(Guid workItemId) => null;
    }

    // Ensures all participant tasks have completed their initial DB read before any proceeds to
    // the transaction and UPDATE. SignalAndWait blocks synchronously, so Task.Run is required in
    // the calling test to avoid deadlock on a single-threaded context.
    private sealed class BarrieredNotRunningStatusSource(int participantCount) : IRunningAutomationStatusSource
    {
        private readonly Barrier _barrier = new(participantCount);
#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        public int GetRunningCount() => 0;

        public bool IsRunning(Guid workItemId)
        {
            _barrier.SignalAndWait(TimeSpan.FromSeconds(10));
            return false;
        }

        public WorkItemRunStatus? GetLiveRunStatus(Guid workItemId) => null;
    }

    private static async Task<WorkItem> CreateTaskAsync(DatabaseFixture db, WorkItemStatus status)
    {
        var project = new ProjectService(db.Context, NullLogger<ProjectService>.Instance);
        var projektEntity = await project.CreateAsync("Recovery-Parallel", null);
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await workItemService.CreateAsync(projektEntity.Id, "Festhängende Aufgabe", "test");
        await workItemService.SetStatusUncheckedAsync(workItem.Id, status);
        if (status.IsActiveOrWaiting())
        {
            // Aktiven Ausführungsstatus direkt auf der Entity setzen (das frühere
            // AufgabeService.AusfuehrungAktivSetzenAsync wurde entfernt): simuliert den Zustand
            // einer gestarteten Aufgabe ohne zugehörige Laufdaten (AktiveRunId bleibt null).
            workItem.ExecutionStatus = WorkItemExecutionStatus.Active;
            await db.Context.SaveChangesAsync();
        }

        return workItem;
    }
}
