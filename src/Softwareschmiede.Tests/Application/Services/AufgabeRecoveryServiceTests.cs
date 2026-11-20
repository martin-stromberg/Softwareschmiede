using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>AufgabeRecoveryServiceTests.</summary>
public sealed class AufgabeRecoveryServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly Guid _projectId = Guid.NewGuid();

    /// <summary>AufgabeRecoveryServiceTests.</summary>
    public AufgabeRecoveryServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Recovery Projekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskIsInArbeitAndNotRunning.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskIsInArbeitAndNotRunning()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        await sut.RecoverManuallyAsync(workItem.Id, TestContext.Current.CancellationToken);

        var loaded = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
        _db.LogEntries.Count(e => e.WorkItemId == workItem.Id && e.Type == LogEntryType.StatusTransition).Should().Be(1);
        _db.LogEntries.Single(e => e.WorkItemId == workItem.Id).Content.Should().Contain("Manuelle Wiederherstellung");
    }

    /// <summary>RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskInWartendAndNotRunning.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskInWartendAndNotRunning()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Waiting);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        await sut.RecoverManuallyAsync(workItem.Id, TestContext.Current.CancellationToken);

        var loaded = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
        loaded.RecoveryVersion.Should().Be(1);
        _db.LogEntries.Count(e => e.WorkItemId == workItem.Id && e.Type == LogEntryType.StatusTransition).Should().Be(1);
    }

    /// <summary>IsRecoveryStatus_ShouldMatchAllowedStates.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Started, WorkItemExecutionStatus.Active, true)]
    [InlineData(WorkItemStatus.Waiting, WorkItemExecutionStatus.Active, true)]
    [InlineData(WorkItemStatus.Started, WorkItemExecutionStatus.Completed, false)]
    [InlineData(WorkItemStatus.Started, WorkItemExecutionStatus.NotStarted, false)]
    [InlineData(WorkItemStatus.New, WorkItemExecutionStatus.Active, false)]
    [InlineData(WorkItemStatus.Archived, WorkItemExecutionStatus.Active, false)]
    [InlineData(WorkItemStatus.Completed, WorkItemExecutionStatus.Active, false)]
    public void IstRecoveryStatus_ShouldMatchAllowedStates(WorkItemStatus status, WorkItemExecutionStatus executionStatus, bool expected)
    {
        WorkItemRecoveryService.IsRecoveryStatus(status, executionStatus).Should().Be(expected);
    }

    /// <summary>RecoverManuellAsync_ShouldThrow_WhenTaskIsStillRunning.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenTaskIsStillRunning()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var running = new FakeRunningAutomationStatusSource(true);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var act = () => sut.RecoverManuallyAsync(workItem.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung nicht möglich, Verarbeitung läuft noch.");
    }

    /// <summary>RecoverManuellAsync_ShouldThrow_WhenStatusIsNotRecoverable.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenStatusIsNotRecoverable()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.New);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var act = () => sut.RecoverManuallyAsync(workItem.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung für aktuellen Status nicht verfügbar.");
    }

    /// <summary>RecoverManuellAsync_ShouldThrow_WhenRunningCheckFails.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenRunningCheckFails()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var running = new ThrowingRunningAutomationStatusSource();
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var act = () => sut.RecoverManuallyAsync(workItem.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Prüfung der Laufzeit war nicht möglich.");
    }

    /// <summary>TestRecoveryCandidates: Aufgaben mit Heartbeat > 5 Min und Status InArbeit/Wartend werden erkannt.</summary>
    [Fact]
    public async Task TestRecoveryCandidates()
    {
        // Arrange – Aufgabe InArbeit mit altem Heartbeat
        var aufgabeAlt = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Alte InArbeit Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Aufgabe Wartend mit altem Heartbeat
        var aufgabeWartend = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Alte Wartend Aufgabe",
            Status = WorkItemStatus.Waiting,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-6),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Aufgabe InArbeit mit frischem Heartbeat (soll nicht erkannt werden)
        var aufgabeFrisch = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Frische InArbeit Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var aufgabeBeendeteAusfuehrung = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Beendete Ausfuehrung",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Completed,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Aufgabe Neu (soll nie erkannt werden)
        var aufgabeNeu = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Neue Aufgabe",
            Status = WorkItemStatus.New,
            ExecutionStatus = WorkItemExecutionStatus.NotStarted,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.WorkItems.AddRange(aufgabeAlt, aufgabeWartend, aufgabeFrisch, aufgabeBeendeteAusfuehrung, aufgabeNeu);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        // Act
        var candidates = (await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken)).ToList();

        // Assert
        candidates.Should().Contain(aufgabeAlt.Id);
        candidates.Should().Contain(aufgabeWartend.Id);
        candidates.Should().NotContain(aufgabeFrisch.Id);
        candidates.Should().NotContain(aufgabeBeendeteAusfuehrung.Id);
        candidates.Should().NotContain(aufgabeNeu.Id);
    }

    /// <summary>ScanForRecoveryCandidates ignoriert Aufgaben, für die ein Prozess noch läuft.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_ShouldExcludeRunningTasks()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Laufende Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var running = new FakeRunningAutomationStatusSource(true);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var candidates = (await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken)).ToList();

        candidates.Should().BeEmpty();
    }

    /// <summary>ScanForRecoveryCandidatesAsync ignoriert pausierte Aufgaben (PausiertBisUtc in der Zukunft), auch bei abgelaufenem Heartbeat (Issue 151).</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_ShouldExcludePausedTasks()
    {
        var pausiert = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Pausierte Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            PausedUntilUtc = DateTimeOffset.UtcNow.AddHours(2),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var abgelaufenePause = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Aufgabe mit abgelaufener Pause",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            PausedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.WorkItems.AddRange(pausiert, abgelaufenePause);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var candidates = (await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken)).ToList();

        candidates.Should().NotContain(pausiert.Id, "eine aktiv pausierte Aufgabe ist kein Recovery-Kandidat");
        candidates.Should().Contain(abgelaufenePause.Id, "eine abgelaufene Pause darf die Recovery-Erkennung nicht blockieren");
    }

    /// <summary>RecoverManuellAsync lehnt eine aktiv pausierte Aufgabe mit InvalidOperationException ab (Issue 151).</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenTaskIsPaused()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        tracked!.PausedUntilUtc = DateTimeOffset.UtcNow.AddHours(1);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var act = () => sut.RecoverManuallyAsync(workItem.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pausiert*");
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private async Task<WorkItem> ErstelleAufgabeAsync(WorkItemStatus status)
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Recovery Task",
            Status = status,
            ExecutionStatus = status.IsActiveOrWaiting()
                ? WorkItemExecutionStatus.Active
                : WorkItemExecutionStatus.NotStarted,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync();
        return workItem;
    }

    private sealed class FakeRunningAutomationStatusSource(bool isRunning) : IRunningAutomationStatusSource
    {
#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        /// <summary>GetRunningCount.</summary>
        public int GetRunningCount() => isRunning ? 1 : 0;
        /// <summary>IsRunning.</summary>
        public bool IsRunning(Guid workItemId) => isRunning;
        /// <summary>GetLiveLaufStatus.</summary>
        public WorkItemRunStatus? GetLiveRunStatus(Guid workItemId) => isRunning ? WorkItemRunStatus.Running : null;
    }

    private sealed class ThrowingRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        /// <summary>GetRunningCount.</summary>
        public int GetRunningCount() => 0;
        /// <summary>IsRunning.</summary>
        public bool IsRunning(Guid workItemId) => throw new TimeoutException("simulated");
        /// <summary>GetLiveLaufStatus.</summary>
        public WorkItemRunStatus? GetLiveRunStatus(Guid workItemId) => throw new TimeoutException("simulated");
    }
}
