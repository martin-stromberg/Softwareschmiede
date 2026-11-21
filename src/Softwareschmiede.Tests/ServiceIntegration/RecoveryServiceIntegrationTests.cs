using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: App startet, erkennt InArbeit/Wartend-Aufgaben, zeigt Recovery-Banner.</summary>
public sealed class RecoveryServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly ProjectService _projectService;
    private readonly Guid _projectId;

    /// <summary>RecoveryServiceIntegrationTests.</summary>
    public RecoveryServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);

        var project = _projectService.CreateAsync("Recovery-Projekt", null).GetAwaiter().GetResult();
        _projectId = project.Id;
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>ScanForRecoveryCandidates_FindetAufgaben_MitAltemHeartbeatUndInArbeit.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_FindsWorkItems_WithOldHeartbeatAndInWork()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        SetOldHeartbeat(workItem.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var candidates = await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken);

        candidates.Should().Contain(workItem.Id);
    }

    /// <summary>ScanForRecoveryCandidates_IgnoriertAufgaben_MitFrischemHeartbeat.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_IgnoresWorkItems_WithFreshHeartbeat()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        SetOldHeartbeat(workItem.Id, minutesAlt: 1);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var candidates = await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken);

        candidates.Should().NotContain(workItem.Id);
    }

    /// <summary>ScanForRecoveryCandidates_FindetAufgaben_MitAltemHeartbeatUndWartend.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_FindsWorkItems_WithOldHeartbeatAndWaiting()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Waiting);
        SetOldHeartbeat(workItem.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var candidates = await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken);

        candidates.Should().Contain(workItem.Id);
    }

    /// <summary>ScanForRecoveryCandidates_IgnoriertLaufendeAufgaben.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_IgnoresRunningWorkItems()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        SetOldHeartbeat(workItem.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(isRunning: true);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var candidates = await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken);

        candidates.Should().NotContain(workItem.Id);
    }

    /// <summary>RecoverManuellAsync_Setzt_StatusAufGestartet.</summary>
    [Fact]
    public async Task RecoverManualAsync_Sets_StatusOnStarted()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        SetOldHeartbeat(workItem.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        await sut.RecoverManuallyAsync(workItem.Id, TestContext.Current.CancellationToken);

        var loaded = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
    }

    private async Task<WorkItem> CreateWorkItemAsync(WorkItemStatus status)
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Recovery-Aufgabe",
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

    private void SetOldHeartbeat(Guid workItemId, int minutesAlt)
    {
        var workItem = _db.WorkItems.Find(workItemId)!;
        workItem.LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-minutesAlt);
        _db.SaveChanges();
        _db.Entry(workItem).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
    }
}

/// <summary>Fake für IRunningAutomationStatusSource zum Testen (E2E-Variante).</summary>
internal sealed class E2EFakeRunningStatusSource : IRunningAutomationStatusSource
{
    private readonly bool _isRunning;

    /// <summary>E2EFakeRunningStatusSource.</summary>
    public E2EFakeRunningStatusSource(bool isRunning) => _isRunning = isRunning;

#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
    public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067

    /// <summary>IsRunning.</summary>
    public bool IsRunning(Guid workItemId) => _isRunning;

    /// <summary>GetLiveLaufStatus.</summary>
    public Softwareschmiede.Domain.Enums.WorkItemRunStatus? GetLiveRunStatus(Guid workItemId)
        => _isRunning ? Softwareschmiede.Domain.Enums.WorkItemRunStatus.Running : null;

    /// <summary>GetRunningCount.</summary>
    public int GetRunningCount() => _isRunning ? 1 : 0;
}
