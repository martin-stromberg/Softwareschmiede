using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="CliUpdateSafetyService"/>.</summary>
public sealed class CliUpdateSafetyServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db = TestDbContextFactory.Create();
    private readonly WorkItemService _workItemService;
    private readonly AiPluginLimitService _kiPluginLimitService;
    private readonly Guid _projectId = new("33333333-3333-3333-3333-333333333333");

    /// <summary>Initialisiert die Testdatenbank.</summary>
    public CliUpdateSafetyServiceTests()
    {
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _kiPluginLimitService = new AiPluginLimitService(
            _db,
            new AppSettingService(_db, NullLogger<AppSettingService>.Instance),
            new WorkItemRunDataChangedNotifier(),
            NullLogger<AiPluginLimitService>.Instance);
        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = _projectId,
            Name = "Projekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Eine Aufgabe mit aktiver RunId, LaufStatus Laeuft und frischem Heartbeat blockiert das Update.</summary>
    [Fact]
    public async Task CheckAsync_ShouldTreatTaskWithFreshHeartbeatAsRisky()
    {
        var running = await CreateActiveTaskAsync("Laeuft", WorkItemRunStatus.Running);
        var sut = new CliUpdateSafetyService(_workItemService, _kiPluginLimitService, NullLogger<CliUpdateSafetyService>.Instance);

        var result = await sut.CheckAsync(TestContext.Current.CancellationToken);

        result.RequiresConfirmation.Should().BeTrue();
        result.RiskyTaskCount.Should().Be(1);
        result.RiskyTasks.Should().Contain(t => t.Contains(running.Title, StringComparison.Ordinal));
    }

    /// <summary>
    /// Eine Aufgabe mit aktiver RunId und LaufStatus Laeuft, aber veraltetem (stale) Heartbeat blockiert das
    /// Update NICHT mehr. Regressionstest für den vom Anwender gemeldeten Fehler: Der bisherige, rein
    /// LaufStatus-basierte Filter hätte diese Aufgabe fälschlich als riskant eingestuft, obwohl der CLI-Prozess
    /// tatsächlich verlorengegangen ist (Crash, verlorenes Exited-Event, App-Neustart) und das Aufgabenpanel die
    /// Aufgabe längst als "✓ Bereit" anzeigt.
    /// </summary>
    [Fact]
    public async Task CheckAsync_ShouldNotTreatTaskWithStaleHeartbeatAsRisky()
    {
        var stale = await CreateActiveTaskAsync(
            "Stale",
            WorkItemRunStatus.Running,
            DateTimeOffset.UtcNow.AddMinutes(-(WorkItemRecoveryService.HeartbeatTimeoutMinutes + 1)));
        var sut = new CliUpdateSafetyService(_workItemService, _kiPluginLimitService, NullLogger<CliUpdateSafetyService>.Instance);

        var result = await sut.CheckAsync(TestContext.Current.CancellationToken);

        result.RequiresConfirmation.Should().BeFalse();
        result.RiskyTaskCount.Should().Be(0);
        result.RiskyTasks.Should().NotContain(t => t.Contains(stale.Title, StringComparison.Ordinal));
    }

    /// <summary>
    /// Eine Aufgabe mit zukünftigem, persistiertem Session-Limit ihres KiPluginPrefix wird NICHT
    /// als riskant eingestuft — die Heartbeat-Toleranz entfällt komplett (Issue 151).
    /// </summary>
    [Fact]
    public async Task CheckAsync_ShouldNotTreatTaskWithFutureSessionLimitAsRisky()
    {
        var limited = await CreateActiveTaskAsync("Limitiert", WorkItemRunStatus.Running, aiPluginPrefix: "Test.Plugin");
        await _kiPluginLimitService.ProcessRateLimitAsync(limited.Id, DateTimeOffset.UtcNow.AddHours(2), TestContext.Current.CancellationToken);
        var sut = new CliUpdateSafetyService(_workItemService, _kiPluginLimitService, NullLogger<CliUpdateSafetyService>.Instance);

        var result = await sut.CheckAsync(TestContext.Current.CancellationToken);

        result.RequiresConfirmation.Should().BeFalse();
        result.RiskyTaskCount.Should().Be(0);
        result.RiskyTasks.Should().NotContain(t => t.Contains(limited.Title, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ein abgelaufenes Session-Limit ändert nichts am bisherigen Verhalten: Die Aufgabe bleibt
    /// bei frischem Heartbeat riskant.
    /// </summary>
    [Fact]
    public async Task CheckAsync_ShouldTreatTaskWithExpiredSessionLimitAsRisky()
    {
        var expired = await CreateActiveTaskAsync("Abgelaufen", WorkItemRunStatus.Running, aiPluginPrefix: "Test.Plugin");
        await _kiPluginLimitService.ProcessRateLimitAsync(expired.Id, DateTimeOffset.UtcNow.AddMinutes(-5), TestContext.Current.CancellationToken);
        var sut = new CliUpdateSafetyService(_workItemService, _kiPluginLimitService, NullLogger<CliUpdateSafetyService>.Instance);

        var result = await sut.CheckAsync(TestContext.Current.CancellationToken);

        result.RequiresConfirmation.Should().BeTrue();
        result.RiskyTasks.Should().Contain(t => t.Contains(expired.Title, StringComparison.Ordinal));
    }

    private async Task<Softwareschmiede.Domain.Entities.WorkItem> CreateActiveTaskAsync(
        string title,
        WorkItemRunStatus? runStatus,
        DateTimeOffset? lastHeartbeatUtc = null,
        string? aiPluginPrefix = null)
    {
        var workItem = await _workItemService.CreateAsync(_projectId, title, null);
        await _workItemService.StartAsync(workItem.Id, "feature/test", "C:/repo");
        await _workItemService.SetActiveRunAsync(workItem.Id, Guid.NewGuid().ToString("N"));
        var tracked = await _db.WorkItems.FindAsync(workItem.Id);
        tracked!.RunStatus = runStatus;
        if (lastHeartbeatUtc.HasValue)
        {
            tracked.LastHeartbeatUtc = lastHeartbeatUtc.Value;
        }
        if (aiPluginPrefix is not null)
        {
            tracked.AiPluginPrefix = aiPluginPrefix;
        }
        await _db.SaveChangesAsync();
        return tracked;
    }
}
