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
    private readonly WorkItemService _aufgabeService;
    private readonly ProjectService _projektService;
    private readonly Guid _projektId;

    /// <summary>RecoveryServiceIntegrationTests.</summary>
    public RecoveryServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _projektService = new ProjectService(_db, NullLogger<ProjectService>.Instance);

        var project = _projektService.CreateAsync("Recovery-Projekt", null).GetAwaiter().GetResult();
        _projektId = project.Id;
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>ScanForRecoveryCandidates_FindetAufgaben_MitAltemHeartbeatUndInArbeit.</summary>.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_FindetAufgaben_MitAltemHeartbeatUndInArbeit()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        SetzeAltenHeartbeat(workItem.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var kandidaten = await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken);

        kandidaten.Should().Contain(workItem.Id);
    }

    /// <summary><summary>ScanForRecoveryCandidates_IgnoriertAufgaben_MitFrischemHeartbeat.</summary>.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_IgnoriertAufgaben_MitFrischemHeartbeat()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        SetzeAltenHeartbeat(workItem.Id, minutesAlt: 1);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var kandidaten = await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken);

        kandidaten.Should().NotContain(workItem.Id);
    }

    /// <summary><summary>ScanForRecoveryCandidates_FindetAufgaben_MitAltemHeartbeatUndWartend.</summary>.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_FindetAufgaben_MitAltemHeartbeatUndWartend()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Waiting);
        SetzeAltenHeartbeat(workItem.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var kandidaten = await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken);

        kandidaten.Should().Contain(workItem.Id);
    }

    /// <summary><summary>ScanForRecoveryCandidates_IgnoriertLaufendeAufgaben.</summary>.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_IgnoriertLaufendeAufgaben()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        SetzeAltenHeartbeat(workItem.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(isRunning: true);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        var kandidaten = await sut.ScanForRecoveryCandidatesAsync(TestContext.Current.CancellationToken);

        kandidaten.Should().NotContain(workItem.Id);
    }

    /// <summary><summary>RecoverManuellAsync_Setzt_StatusAufGestartet.</summary>.</summary>
    [Fact]
    public async Task RecoverManuellAsync_Setzt_StatusAufGestartet()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        SetzeAltenHeartbeat(workItem.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new WorkItemRecoveryService(_db, running, NullLogger<WorkItemRecoveryService>.Instance);

        await sut.RecoverManuellAsync(workItem.Id, TestContext.Current.CancellationToken);

        var geladen = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        geladen!.Status.Should().Be(WorkItemStatus.Started);
    }

    private async Task<WorkItem> ErstelleAufgabeAsync(WorkItemStatus status)
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projektId,
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

    private void SetzeAltenHeartbeat(Guid workItemId, int minutesAlt)
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
