using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="KiPluginLimitService"/> (Issue 151).</summary>
public sealed class KiPluginLimitServiceTests : IDisposable
{
    private const string TestPrefix = "Softwareschmiede.TestKi";

    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly AufgabeLaufdatenChangedNotifier _notifier;
    private readonly KiPluginLimitService _sut;
    private readonly Guid _projektId = new Guid("55555555-5555-5555-5555-555555555555");

    /// <summary>KiPluginLimitServiceTests.</summary>
    public KiPluginLimitServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _notifier = new AufgabeLaufdatenChangedNotifier();
        _sut = new KiPluginLimitService(
            _db,
            new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance),
            _notifier,
            NullLogger<KiPluginLimitService>.Instance);

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Limit-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>VerarbeiteRateLimitAsync persistiert den Reset-Zeitpunkt unter dem Prefix-Schlüssel.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldPersistSessionLimitKey()
    {
        var aufgabe = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);
        var resetUtc = DateTimeOffset.UtcNow.AddHours(2);

        await _sut.VerarbeiteRateLimitAsync(aufgabe.Id, resetUtc);

        var einstellung = await _db.AppEinstellungen
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Schluessel == KiPluginLimitService.SessionLimitKeyPrefix + TestPrefix);
        einstellung.Should().NotBeNull();
        einstellung!.Wert.Should().NotBeNullOrEmpty();
        DateTimeOffset.Parse(einstellung.Wert!, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal)
            .Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5));
    }

    /// <summary>VerarbeiteRateLimitAsync pausiert alle aktiv laufenden Aufgaben desselben Prefix, lässt fremde Prefixe unberührt und feuert den Notifier je Aufgabe.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldPauseSamePrefixTasks_AndNotify()
    {
        var a1 = await ErstelleLaufendeAufgabeAsync("Peer A", TestPrefix);
        var a2 = await ErstelleLaufendeAufgabeAsync("Peer B", TestPrefix);
        var fremd = await ErstelleLaufendeAufgabeAsync("Fremdes Plugin", "Softwareschmiede.AnderesKi");
        var nichtGestartet = await _aufgabeService.CreateAsync(_projektId, "Nicht gestartet", null);
        var neuTracked = await _db.Aufgaben.FindAsync(nichtGestartet.Id);
        neuTracked!.KiPluginPrefix = TestPrefix;
        await _db.SaveChangesAsync();

        var notified = new List<Guid>();
        _notifier.LaufdatenChanged += id => notified.Add(id);

        var resetUtc = DateTimeOffset.UtcNow.AddHours(3);
        await _sut.VerarbeiteRateLimitAsync(a1.Id, resetUtc);

        var geladene = await _db.Aufgaben.AsNoTracking().ToListAsync();
        geladene.Single(a => a.Id == a1.Id).PausiertBisUtc.Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5));
        geladene.Single(a => a.Id == a2.Id).PausiertBisUtc.Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5));
        geladene.Single(a => a.Id == fremd.Id).PausiertBisUtc.Should().BeNull("ein fremder Plugin-Prefix darf nicht pausiert werden");
        geladene.Single(a => a.Id == nichtGestartet.Id).PausiertBisUtc.Should().BeNull("nur aktiv laufende Aufgaben werden pausiert");

        notified.Should().BeEquivalentTo(new[] { a1.Id, a2.Id });

        // Jede pausierte Aufgabe erhält einen SystemMeldung-Protokolleintrag.
        var systemMeldungen = await _db.Protokolleintraege
            .Where(e => e.Typ == ProtokollTyp.SystemMeldung && e.Inhalt.Contains("Session-Limit"))
            .ToListAsync();
        systemMeldungen.Should().HaveCount(2);
    }

    /// <summary>Ein bereits abgelaufener Reset-Zeitpunkt wird persistiert, löst aber keine Pause aus.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldNotPause_WhenResetExpired()
    {
        var aufgabe = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);

        await _sut.VerarbeiteRateLimitAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddMinutes(-5));

        (await _db.Aufgaben.FindAsync(aufgabe.Id))!.PausiertBisUtc.Should().BeNull();
        (await _db.AppEinstellungen.SingleOrDefaultAsync(
            e => e.Schluessel == KiPluginLimitService.SessionLimitKeyPrefix + TestPrefix))
            .Should().NotBeNull("der Wert wird auch bei abgelaufenem Limit persistiert");
    }

    /// <summary>GetAktiveSessionLimitsAsync liefert nur zukünftige Limits und ignoriert abgelaufene Werte.</summary>
    [Fact]
    public async Task GetAktiveSessionLimitsAsync_ShouldReturnOnlyFutureLimits()
    {
        var aufgabe = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);
        await _sut.VerarbeiteRateLimitAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(1));

        // Abgelaufenes Limit für ein zweites Prefix direkt seeden.
        var appEinstellungen = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        await appEinstellungen.SetSettingAsync(
            KiPluginLimitService.SessionLimitKeyPrefix + "Softwareschmiede.Abgelaufen",
            DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O"));

        var limits = await _sut.GetAktiveSessionLimitsAsync(
            [TestPrefix, "Softwareschmiede.Abgelaufen", "Softwareschmiede.OhneLimit"]);

        limits.Should().ContainKey(TestPrefix);
        limits.Should().NotContainKey("Softwareschmiede.Abgelaufen");
        limits.Should().NotContainKey("Softwareschmiede.OhneLimit");
    }

    /// <summary>Eine auslösende Aufgabe ohne KiPluginPrefix führt zu keiner Aktion: kein AppEinstellung-Eintrag, keine Pause, kein Notify.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldDoNothing_WhenAusloeserOhnePrefix()
    {
        var peer = await ErstelleLaufendeAufgabeAsync("Peer", TestPrefix);
        var aufgabeOhnePrefix = await _aufgabeService.CreateAsync(_projektId, "Ohne Prefix", null);
        var tracked = await _db.Aufgaben.FindAsync(aufgabeOhnePrefix.Id);
        tracked!.Status = AufgabeStatus.Gestartet;
        tracked.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv;
        await _db.SaveChangesAsync();

        var notified = new List<Guid>();
        _notifier.LaufdatenChanged += id => notified.Add(id);

        await _sut.VerarbeiteRateLimitAsync(aufgabeOhnePrefix.Id, DateTimeOffset.UtcNow.AddHours(2));

        (await _db.AppEinstellungen.ToListAsync())
            .Should().BeEmpty("ohne KiPluginPrefix darf kein Session-Limit persistiert werden");
        (await _db.Aufgaben.FindAsync(peer.Id))!.PausiertBisUtc
            .Should().BeNull("keine Aufgabe darf pausiert werden");
        notified.Should().BeEmpty();
        (await _db.Protokolleintraege
                .Where(e => e.Typ == ProtokollTyp.SystemMeldung)
                .ToListAsync())
            .Should().BeEmpty();
    }

    /// <summary>Eine beendete Aufgabe mit demselben Prefix bleibt unverändert (kein PausiertBisUtc, kein Notify).</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldNotPause_WhenAufgabeBeendet()
    {
        var ausloeser = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);
        var beendet = await _aufgabeService.CreateAsync(_projektId, "Beendete Aufgabe", null);
        var beendetTracked = await _db.Aufgaben.FindAsync(beendet.Id);
        beendetTracked!.Status = AufgabeStatus.Beendet;
        beendetTracked.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Beendet;
        beendetTracked.KiPluginPrefix = TestPrefix;
        await _db.SaveChangesAsync();

        var notified = new List<Guid>();
        _notifier.LaufdatenChanged += id => notified.Add(id);

        await _sut.VerarbeiteRateLimitAsync(ausloeser.Id, DateTimeOffset.UtcNow.AddHours(1));

        (await _db.Aufgaben.FindAsync(beendet.Id))!.PausiertBisUtc
            .Should().BeNull("eine beendete Aufgabe darf nicht pausiert werden");
        notified.Should().NotContain(beendet.Id);
    }

    /// <summary>Eine manuell gesetzte, später endende Pause wird durch einen früheren Session-Limit-Reset nicht verkürzt; eine kürzere bestehende Pause wird dagegen auf den Reset-Zeitpunkt verlängert.</summary>
    [Fact]
    public async Task VerarbeiteRateLimitAsync_ShouldKeepLongerPause_AndExtendShorterPause()
    {
        var ausloeser = await ErstelleLaufendeAufgabeAsync("Ausloeser", TestPrefix);
        var laengerPausiert = await ErstelleLaufendeAufgabeAsync("Manuell länger pausiert", TestPrefix);
        var manuellBis = DateTimeOffset.UtcNow.AddHours(5);
        laengerPausiert.PausiertBisUtc = manuellBis;
        var kuerzerPausiert = await ErstelleLaufendeAufgabeAsync("Kürzer pausiert", TestPrefix);
        kuerzerPausiert.PausiertBisUtc = DateTimeOffset.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync();

        var resetUtc = DateTimeOffset.UtcNow.AddHours(2);
        await _sut.VerarbeiteRateLimitAsync(ausloeser.Id, resetUtc);

        var geladene = await _db.Aufgaben.AsNoTracking().ToListAsync();
        geladene.Single(a => a.Id == laengerPausiert.Id).PausiertBisUtc
            .Should().BeCloseTo(manuellBis, TimeSpan.FromSeconds(5),
                "eine längere manuelle Pause darf durch den früheren Limit-Reset nicht verkürzt werden");
        geladene.Single(a => a.Id == kuerzerPausiert.Id).PausiertBisUtc
            .Should().BeCloseTo(resetUtc, TimeSpan.FromSeconds(5),
                "eine kürzere bestehende Pause wird auf den späteren Limit-Reset verlängert");
    }

    private async Task<Aufgabe> ErstelleLaufendeAufgabeAsync(string titel, string kiPluginPrefix)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, titel, null);
        var tracked = await _db.Aufgaben.FindAsync(aufgabe.Id);
        tracked!.Status = AufgabeStatus.Gestartet;
        tracked.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv;
        tracked.AktiveRunId = Guid.NewGuid().ToString("N");
        tracked.KiPluginPrefix = kiPluginPrefix;
        await _db.SaveChangesAsync();
        return tracked;
    }
}
