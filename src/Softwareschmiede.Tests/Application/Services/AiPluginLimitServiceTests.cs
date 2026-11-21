using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="AiPluginLimitService"/> (Issue 151).</summary>
public sealed class AiPluginLimitServiceTests : IDisposable
{
    private const string TestPrefix = "Softwareschmiede.TestKi";

    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly WorkItemRunDataChangedNotifier _notifier;
    private readonly AiPluginLimitService _sut;
    private readonly Guid _projectId = new Guid("55555555-5555-5555-5555-555555555555");

    /// <summary>AiPluginLimitServiceTests.</summary>
    public AiPluginLimitServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _notifier = new WorkItemRunDataChangedNotifier();
        _sut = new AiPluginLimitService(
            _db,
            new AppSettingService(_db, NullLogger<AppSettingService>.Instance),
            _notifier,
            NullLogger<AiPluginLimitService>.Instance);

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Limit-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>ProcessRateLimitAsync persistiert den Reset-Zeitpunkt unter dem Prefix-Schlüssel.</summary>
    [Fact]
    public async Task ProcessRateLimitAsync_ShouldPersistSessionLimitKey()
    {
        var workItem = await CreateRunningWorkItemAsync("Ausloeser", TestPrefix);
        var resetUtc = DateTimeOffset.UtcNow.AddHours(2);

        await _sut.ProcessRateLimitAsync(workItem.Id, resetUtc, TestContext.Current.CancellationToken);

        var setting = await _db.AppSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Key == AiPluginLimitService.SessionLimitKeyPrefix + TestPrefix, cancellationToken: TestContext.Current.CancellationToken);
        setting.Should().NotBeNull();
        setting!.Value.Should().NotBeNullOrEmpty();
        DateTimeOffset.Parse(setting.Value!, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal)
            .Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5));
    }

    /// <summary>ProcessRateLimitAsync pausiert alle aktiv laufenden Aufgaben desselben Prefix, lässt fremde Prefixe unberührt und feuert den Notifier je Aufgabe.</summary>
    [Fact]
    public async Task ProcessRateLimitAsync_ShouldPauseSamePrefixTasks_AndNotify()
    {
        var a1 = await CreateRunningWorkItemAsync("Peer A", TestPrefix);
        var a2 = await CreateRunningWorkItemAsync("Peer B", TestPrefix);
        var foreign = await CreateRunningWorkItemAsync("Fremdes Plugin", "Softwareschmiede.AnderesKi");
        var notStarted = await _workItemService.CreateAsync(_projectId, "Nicht gestartet", null, ct: TestContext.Current.CancellationToken);
        var newTracked = await _db.WorkItems.FindAsync(new object?[] { notStarted.Id }, TestContext.Current.CancellationToken);
        newTracked!.AiPluginPrefix = TestPrefix;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notified = new List<Guid>();
        _notifier.RunDataChanged += id => notified.Add(id);

        var resetUtc = DateTimeOffset.UtcNow.AddHours(3);
        await _sut.ProcessRateLimitAsync(a1.Id, resetUtc, TestContext.Current.CancellationToken);

        var loaded = await _db.WorkItems.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        loaded.Single(a => a.Id == a1.Id).PausedUntilUtc.Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5));
        loaded.Single(a => a.Id == a2.Id).PausedUntilUtc.Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5));
        loaded.Single(a => a.Id == foreign.Id).PausedUntilUtc.Should().BeNull("ein fremder Plugin-Prefix darf nicht pausiert werden");
        loaded.Single(a => a.Id == notStarted.Id).PausedUntilUtc.Should().BeNull("nur aktiv laufende Aufgaben werden pausiert");

        notified.Should().BeEquivalentTo(new[] { a1.Id, a2.Id });

        // Jede pausierte Aufgabe erhält einen SystemMeldung-Protokolleintrag.
        var systemMessages = await _db.LogEntries
            .Where(e => e.Type == LogEntryType.SystemMessage && e.Content.Contains("Session-Limit"))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        systemMessages.Should().HaveCount(2);
    }

    /// <summary>Ein bereits abgelaufener Reset-Zeitpunkt wird persistiert, löst aber keine Pause aus.</summary>
    [Fact]
    public async Task ProcessRateLimitAsync_ShouldNotPause_WhenResetExpired()
    {
        var workItem = await CreateRunningWorkItemAsync("Ausloeser", TestPrefix);

        await _sut.ProcessRateLimitAsync(workItem.Id, DateTimeOffset.UtcNow.AddMinutes(-5), TestContext.Current.CancellationToken);

        (await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken))!.PausedUntilUtc.Should().BeNull();
        (await _db.AppSettings.SingleOrDefaultAsync(e => e.Key == AiPluginLimitService.SessionLimitKeyPrefix + TestPrefix, cancellationToken: TestContext.Current.CancellationToken))
            .Should().NotBeNull("der Wert wird auch bei abgelaufenem Limit persistiert");
    }

    /// <summary>GetActiveSessionLimitsAsync liefert nur zukünftige Limits und ignoriert abgelaufene Werte.</summary>
    [Fact]
    public async Task GetActiveSessionLimitAsync_ShouldReturnOnlyFutureLimit()
    {
        var workItem = await CreateRunningWorkItemAsync("Ausloeser", TestPrefix);
        await _sut.ProcessRateLimitAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        // Abgelaufenes Limit für ein zweites Prefix direkt seeden.
        var appSettings = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        await appSettings.SetSettingAsync(AiPluginLimitService.SessionLimitKeyPrefix + "Softwareschmiede.Abgelaufen", DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O"), TestContext.Current.CancellationToken);

        var limit = await _sut.GetActiveSessionLimitsAsync([TestPrefix, "Softwareschmiede.Abgelaufen", "Softwareschmiede.OhneLimit"], TestContext.Current.CancellationToken);

        limit.Should().ContainKey(TestPrefix);
        limit.Should().NotContainKey("Softwareschmiede.Abgelaufen");
        limit.Should().NotContainKey("Softwareschmiede.OhneLimit");
    }

    /// <summary>Eine auslösende Aufgabe ohne KiPluginPrefix führt zu keiner Aktion: kein AppEinstellung-Eintrag, keine Pause, kein Notify.</summary>
    [Fact]
    public async Task ProcessRateLimitAsync_ShouldDoNothing_WhenTriggerWithoutPrefix()
    {
        var peer = await CreateRunningWorkItemAsync("Peer", TestPrefix);
        var workItemWithoutPrefix = await _workItemService.CreateAsync(_projectId, "Ohne Prefix", null, ct: TestContext.Current.CancellationToken);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItemWithoutPrefix.Id }, TestContext.Current.CancellationToken);
        tracked!.Status = WorkItemStatus.Started;
        tracked.ExecutionStatus = WorkItemExecutionStatus.Active;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notified = new List<Guid>();
        _notifier.RunDataChanged += id => notified.Add(id);

        await _sut.ProcessRateLimitAsync(workItemWithoutPrefix.Id, DateTimeOffset.UtcNow.AddHours(2), TestContext.Current.CancellationToken);

        (await _db.AppSettings.ToListAsync(cancellationToken: TestContext.Current.CancellationToken))
            .Should().BeEmpty("ohne KiPluginPrefix darf kein Session-Limit persistiert werden");
        (await _db.WorkItems.FindAsync(new object?[] { peer.Id }, TestContext.Current.CancellationToken))!.PausedUntilUtc
            .Should().BeNull("keine Aufgabe darf pausiert werden");
        notified.Should().BeEmpty();
        (await _db.LogEntries
                .Where(e => e.Type == LogEntryType.SystemMessage)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken))
            .Should().BeEmpty();
    }

    /// <summary>Eine beendete Aufgabe mit demselben Prefix bleibt unverändert (kein PausedUntilUtc, kein Notify).</summary>
    [Fact]
    public async Task ProcessRateLimitAsync_ShouldNotPause_WhenWorkItemCompleted()
    {
        var trigger = await CreateRunningWorkItemAsync("Ausloeser", TestPrefix);
        var completed = await _workItemService.CreateAsync(_projectId, "Beendete Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var completedTracked = await _db.WorkItems.FindAsync(new object?[] { completed.Id }, TestContext.Current.CancellationToken);
        completedTracked!.Status = WorkItemStatus.Completed;
        completedTracked.ExecutionStatus = WorkItemExecutionStatus.Completed;
        completedTracked.AiPluginPrefix = TestPrefix;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notified = new List<Guid>();
        _notifier.RunDataChanged += id => notified.Add(id);

        await _sut.ProcessRateLimitAsync(trigger.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        (await _db.WorkItems.FindAsync(new object?[] { completed.Id }, TestContext.Current.CancellationToken))!.PausedUntilUtc
            .Should().BeNull("eine beendete Aufgabe darf nicht pausiert werden");
        notified.Should().NotContain(completed.Id);
    }

    /// <summary>Eine manuell gesetzte, später endende Pause wird durch einen früheren Session-Limit-Reset nicht verkürzt; eine kürzere bestehende Pause wird dagegen auf den Reset-Zeitpunkt verlängert.</summary>
    [Fact]
    public async Task ProcessRateLimitAsync_ShouldKeepLongerPause_AndExtendShorterPause()
    {
        var trigger = await CreateRunningWorkItemAsync("Ausloeser", TestPrefix);
        var longerPaused = await CreateRunningWorkItemAsync("Manuell länger pausiert", TestPrefix);
        var manualUntil = DateTimeOffset.UtcNow.AddHours(5);
        longerPaused.PausedUntilUtc = manualUntil;
        var shorterPaused = await CreateRunningWorkItemAsync("Kürzer pausiert", TestPrefix);
        shorterPaused.PausedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resetUtc = DateTimeOffset.UtcNow.AddHours(2);
        await _sut.ProcessRateLimitAsync(trigger.Id, resetUtc, TestContext.Current.CancellationToken);

        var loaded = await _db.WorkItems.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        loaded.Single(a => a.Id == longerPaused.Id).PausedUntilUtc
            .Should().BeCloseTo(manualUntil, TimeSpan.FromSeconds(5),
                "eine längere manuelle Pause darf durch den früheren Limit-Reset nicht verkürzt werden");
        loaded.Single(a => a.Id == shorterPaused.Id).PausedUntilUtc
            .Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5),
                "eine kürzere bestehende Pause wird auf den späteren Limit-Reset verlängert");
    }

    private async Task<WorkItem> CreateRunningWorkItemAsync(string title, string aiPluginPrefix)
    {
        var workItem = await _workItemService.CreateAsync(_projectId, title, null);
        var tracked = await _db.WorkItems.FindAsync(workItem.Id);
        tracked!.Status = WorkItemStatus.Started;
        tracked.ExecutionStatus = WorkItemExecutionStatus.Active;
        tracked.ActiveRunId = Guid.NewGuid().ToString("N");
        tracked.AiPluginPrefix = aiPluginPrefix;
        await _db.SaveChangesAsync();
        return tracked;
    }
}
