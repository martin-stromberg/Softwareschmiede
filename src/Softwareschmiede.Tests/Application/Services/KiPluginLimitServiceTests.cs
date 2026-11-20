using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="AiPluginLimitService"/> (Issue 151).</summary>
public sealed class KiPluginLimitServiceTests : IDisposable
{
    private const string TestPrefix = "Softwareschmiede.TestKi";

    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _aufgabeService;
    private readonly WorkItemRunDataChangedNotifier _notifier;
    private readonly AiPluginLimitService _sut;
    private readonly Guid _projektId = new Guid("55555555-5555-5555-5555-555555555555");

    /// <summary>KiPluginLimitServiceTests.</summary>
    public KiPluginLimitServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _notifier = new WorkItemRunDataChangedNotifier();
        _sut = new AiPluginLimitService(
            _db,
            new AppSettingService(_db, NullLogger<AppSettingService>.Instance),
            _notifier,
            NullLogger<AiPluginLimitService>.Instance);

        _db.Projects.Add(new Project
        {
            Id = _projektId,
            Name = "Limit-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>VerarbeiteRateLimitAsync persistiert den Reset-Zeitpunkt unter dem Prefix-Schlüssel.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldPersistSessionLimitKey()
    {
        var workItem = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);
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

    /// <summary>VerarbeiteRateLimitAsync pausiert alle aktiv laufenden Aufgaben desselben Prefix, lässt fremde Prefixe unberührt und feuert den Notifier je Aufgabe.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldPauseSamePrefixTasks_AndNotify()
    {
        var a1 = await ErstelleLaufendeAufgabeAsync("Peer A", TestPrefix);
        var a2 = await ErstelleLaufendeAufgabeAsync("Peer B", TestPrefix);
        var fremd = await ErstelleLaufendeAufgabeAsync("Fremdes Plugin", "Softwareschmiede.AnderesKi");
        var nichtGestartet = await _aufgabeService.CreateAsync(_projektId, "Nicht gestartet", null, ct: TestContext.Current.CancellationToken);
        var neuTracked = await _db.WorkItems.FindAsync(new object?[] { nichtGestartet.Id }, TestContext.Current.CancellationToken);
        neuTracked!.AiPluginPrefix = TestPrefix;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notified = new List<Guid>();
        _notifier.LaufdatenChanged += id => notified.Add(id);

        var resetUtc = DateTimeOffset.UtcNow.AddHours(3);
        await _sut.ProcessRateLimitAsync(a1.Id, resetUtc, TestContext.Current.CancellationToken);

        var geladene = await _db.WorkItems.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        geladene.Single(a => a.Id == a1.Id).PausedUntilUtc.Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5));
        geladene.Single(a => a.Id == a2.Id).PausedUntilUtc.Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5));
        geladene.Single(a => a.Id == fremd.Id).PausedUntilUtc.Should().BeNull("ein fremder Plugin-Prefix darf nicht pausiert werden");
        geladene.Single(a => a.Id == nichtGestartet.Id).PausedUntilUtc.Should().BeNull("nur aktiv laufende Aufgaben werden pausiert");

        notified.Should().BeEquivalentTo(new[] { a1.Id, a2.Id });

        // Jede pausierte Aufgabe erhält einen SystemMeldung-Protokolleintrag.
        var systemMeldungen = await _db.LogEntries
            .Where(e => e.Type == LogEntryType.SystemMessage && e.Content.Contains("Session-Limit"))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        systemMeldungen.Should().HaveCount(2);
    }

    /// <summary>Ein bereits abgelaufener Reset-Zeitpunkt wird persistiert, löst aber keine Pause aus.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldNotPause_WhenResetExpired()
    {
        var workItem = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);

        await _sut.ProcessRateLimitAsync(workItem.Id, DateTimeOffset.UtcNow.AddMinutes(-5), TestContext.Current.CancellationToken);

        (await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken))!.PausedUntilUtc.Should().BeNull();
        (await _db.AppSettings.SingleOrDefaultAsync(e => e.Key == AiPluginLimitService.SessionLimitKeyPrefix + TestPrefix, cancellationToken: TestContext.Current.CancellationToken))
            .Should().NotBeNull("der Wert wird auch bei abgelaufenem Limit persistiert");
    }

    /// <summary>GetAktiveSessionLimitsAsync liefert nur zukünftige Limits und ignoriert abgelaufene Werte.</summary>
    [Fact]
    public async Task GetAktiveSessionLimitsAsync_ShouldReturnOnlyFutureLimits()
    {
        var workItem = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);
        await _sut.ProcessRateLimitAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        // Abgelaufenes Limit für ein zweites Prefix direkt seeden.
        var appEinstellungen = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        await appEinstellungen.SetSettingAsync(AiPluginLimitService.SessionLimitKeyPrefix + "Softwareschmiede.Abgelaufen", DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O"), TestContext.Current.CancellationToken);

        var limits = await _sut.GetAktiveSessionLimitsAsync([TestPrefix, "Softwareschmiede.Abgelaufen", "Softwareschmiede.OhneLimit"], TestContext.Current.CancellationToken);

        limits.Should().ContainKey(TestPrefix);
        limits.Should().NotContainKey("Softwareschmiede.Abgelaufen");
        limits.Should().NotContainKey("Softwareschmiede.OhneLimit");
    }

    /// <summary>Eine auslösende Aufgabe ohne KiPluginPrefix führt zu keiner Aktion: kein AppEinstellung-Eintrag, keine Pause, kein Notify.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldDoNothing_WhenAusloeserOhnePrefix()
    {
        var peer = await ErstelleLaufendeAufgabeAsync("Peer", TestPrefix);
        var aufgabeOhnePrefix = await _aufgabeService.CreateAsync(_projektId, "Ohne Prefix", null, ct: TestContext.Current.CancellationToken);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { aufgabeOhnePrefix.Id }, TestContext.Current.CancellationToken);
        tracked!.Status = WorkItemStatus.Started;
        tracked.ExecutionStatus = WorkItemExecutionStatus.Active;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notified = new List<Guid>();
        _notifier.LaufdatenChanged += id => notified.Add(id);

        await _sut.ProcessRateLimitAsync(aufgabeOhnePrefix.Id, DateTimeOffset.UtcNow.AddHours(2), TestContext.Current.CancellationToken);

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

    /// <summary>Eine beendete Aufgabe mit demselben Prefix bleibt unverändert (kein PausiertBisUtc, kein Notify).</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldNotPause_WhenAufgabeBeendet()
    {
        var trigger = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);
        var beendet = await _aufgabeService.CreateAsync(_projektId, "Beendete Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var beendetTracked = await _db.WorkItems.FindAsync(new object?[] { beendet.Id }, TestContext.Current.CancellationToken);
        beendetTracked!.Status = WorkItemStatus.Completed;
        beendetTracked.ExecutionStatus = WorkItemExecutionStatus.Completed;
        beendetTracked.AiPluginPrefix = TestPrefix;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notified = new List<Guid>();
        _notifier.LaufdatenChanged += id => notified.Add(id);

        await _sut.ProcessRateLimitAsync(trigger.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        (await _db.WorkItems.FindAsync(new object?[] { beendet.Id }, TestContext.Current.CancellationToken))!.PausedUntilUtc
            .Should().BeNull("eine beendete Aufgabe darf nicht pausiert werden");
        notified.Should().NotContain(beendet.Id);
    }

    /// <summary>Eine manuell gesetzte, später endende Pause wird durch einen früheren Session-Limit-Reset nicht verkürzt; eine kürzere bestehende Pause wird dagegen auf den Reset-Zeitpunkt verlängert.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldKeepLongerPause_AndExtendShorterPause()
    {
        var trigger = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);
        var laengerPausiert = await ErstelleLaufendeAufgabeAsync("Manuell länger pausiert", TestPrefix);
        var manuellBis = DateTimeOffset.UtcNow.AddHours(5);
        laengerPausiert.PausedUntilUtc = manuellBis;
        var kuerzerPausiert = await ErstelleLaufendeAufgabeAsync("Kürzer pausiert", TestPrefix);
        kuerzerPausiert.PausedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resetUtc = DateTimeOffset.UtcNow.AddHours(2);
        await _sut.ProcessRateLimitAsync(trigger.Id, resetUtc, TestContext.Current.CancellationToken);

        var geladene = await _db.WorkItems.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        geladene.Single(a => a.Id == laengerPausiert.Id).PausedUntilUtc
            .Should().BeCloseTo(manuellBis, TimeSpan.FromSeconds(5),
                "eine längere manuelle Pause darf durch den früheren Limit-Reset nicht verkürzt werden");
        geladene.Single(a => a.Id == kuerzerPausiert.Id).PausedUntilUtc
            .Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5),
                "eine kürzere bestehende Pause wird auf den späteren Limit-Reset verlängert");
    }

    private async Task<WorkItem> ErstelleLaufendeAufgabeAsync(string titel, string kiPluginPrefix)
    {
        var workItem = await _aufgabeService.CreateAsync(_projektId, titel, null);
        var tracked = await _db.WorkItems.FindAsync(workItem.Id);
        tracked!.Status = WorkItemStatus.Started;
        tracked.ExecutionStatus = WorkItemExecutionStatus.Active;
        tracked.ActiveRunId = Guid.NewGuid().ToString("N");
        tracked.AiPluginPrefix = kiPluginPrefix;
        await _db.SaveChangesAsync();
        return tracked;
    }
}
