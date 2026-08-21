using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den SessionManagementService.</summary>
public sealed class SessionManagementServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly SessionManagementService _sut;
    private readonly string _testRoot;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>SessionManagementServiceTests.</summary>
    public SessionManagementServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new SessionManagementService(_db, NullLogger<SessionManagementService>.Instance);
        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "SessionManagement", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private async Task<Aufgabe> ErstelleAutonomeAufgabeAsync()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Autonome Testaufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.AutonomAufgabe,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(aufgabe);

        var konfiguration = new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig.",
            PermissionsJsonPfad = Path.Combine(_testRoot, "permissions.json"),
            TokenBudget = 500000,
            LaufzeitLimitMinuten = 480,
            PersistenzModus = PersistenzModus.Standard,
            ArbeitsverzeichnisPfad = _testRoot
        };
        _db.AutonomAufgabeKonfigurationen.Add(konfiguration);

        var state = new { runtime = new { started_utc = DateTimeOffset.UtcNow, paused_utc = (DateTimeOffset?)null } };
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "state.json"), JsonSerializer.Serialize(state));

        await _db.SaveChangesAsync();
        return aufgabe;
    }

    /// <summary>PauseAufgabeBeiBudgetLimitAsync setzt SessionPauseUtc auf der Aufgabe.</summary>
    [Fact]
    public async Task PauseAufgabeBeiBudgetLimit_SetztSessionPauseUtc()
    {
        var aufgabe = await ErstelleAutonomeAufgabeAsync();

        await _sut.PauseAufgabeBeiBudgetLimitAsync(aufgabe);

        var aktualisiert = await _db.Aufgaben.FindAsync(aufgabe.Id);
        aktualisiert!.SessionPauseUtc.Should().NotBeNull();
        aktualisiert.SessionPauseUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>PauseAufgabeBeiBudgetLimitAsync aktualisiert state.json.runtime.paused_utc.</summary>
    [Fact]
    public async Task PauseAufgabeBeiBudgetLimit_AktualisieertStateJson()
    {
        var aufgabe = await ErstelleAutonomeAufgabeAsync();

        await _sut.PauseAufgabeBeiBudgetLimitAsync(aufgabe);

        var json = await File.ReadAllTextAsync(Path.Combine(_testRoot, "state.json"));
        var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("runtime").GetProperty("paused_utc").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>SetzeFortAsync generiert einen "Weitermachen"-Prompt, der über VorschlagPrompt an den Agenten gesendet wird.</summary>
    [Fact]
    public async Task SetzeFort_SendetWeitermachenPrompt()
    {
        var aufgabe = await ErstelleAutonomeAufgabeAsync();
        await _sut.PauseAufgabeBeiBudgetLimitAsync(aufgabe);

        await _sut.SetzeFortAsync(aufgabe);

        var aktualisiert = await _db.Aufgaben.FindAsync(aufgabe.Id);
        aktualisiert!.SessionPauseUtc.Should().BeNull();
        aktualisiert.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);
        aktualisiert.VorschlagPrompt.Should().NotBeNullOrWhiteSpace();
        aktualisiert.VorschlagPrompt.Should().Contain("Weitermachen");
        aktualisiert.VorschlagAusfuehrenAbUtc.Should().NotBeNull();
    }

    /// <summary>PruefeAusfuehrungAsync erkennt eine Unterbrechung, wenn der letzte Heartbeat älter als das Timeout ist.</summary>
    [Fact]
    public async Task PruefeAusfuehrung_ErkenntUnterbruch()
    {
        var aufgabe = await ErstelleAutonomeAufgabeAsync();
        var entity = await _db.Aufgaben.FindAsync(aufgabe.Id);
        entity!.LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        await _db.SaveChangesAsync();

        var ergebnis = await _sut.PruefeAusfuehrungAsync(aufgabe, TimeSpan.FromMinutes(5));

        ergebnis.Should().BeFalse();
        var aktualisiert = await _db.Aufgaben.FindAsync(aufgabe.Id);
        aktualisiert!.VorschlagPrompt.Should().Contain("unterbrochen");
    }

    /// <summary>PruefeAusfuehrungAsync meldet keine Unterbrechung, wenn der Heartbeat innerhalb des Timeouts liegt.</summary>
    [Fact]
    public async Task PruefeAusfuehrung_KeineUnterbrechung_WennHeartbeatAktuell()
    {
        var aufgabe = await ErstelleAutonomeAufgabeAsync();
        var entity = await _db.Aufgaben.FindAsync(aufgabe.Id);
        entity!.LastHeartbeatUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var ergebnis = await _sut.PruefeAusfuehrungAsync(aufgabe, TimeSpan.FromMinutes(5));

        ergebnis.Should().BeTrue();
    }

    /// <summary>PruefeAusfuehrungAsync meldet früh "kein Unterbruch", wenn bereits eine Session-Pause aktiv ist (SessionPauseUtc gesetzt).</summary>
    [Fact]
    public async Task PruefeAusfuehrung_GibtTrueZurueck_WennSessionPausiertIst()
    {
        var aufgabe = await ErstelleAutonomeAufgabeAsync();
        var entity = await _db.Aufgaben.FindAsync(aufgabe.Id);
        entity!.SessionPauseUtc = DateTimeOffset.UtcNow;
        entity.LastHeartbeatUtc = DateTimeOffset.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();

        var ergebnis = await _sut.PruefeAusfuehrungAsync(aufgabe, TimeSpan.FromMinutes(5));

        ergebnis.Should().BeTrue();
    }

    /// <summary>PruefeAusfuehrungAsync meldet früh "kein Unterbruch", wenn noch kein Heartbeat vorliegt (LastHeartbeatUtc null).</summary>
    [Fact]
    public async Task PruefeAusfuehrung_GibtTrueZurueck_WennNochKeinHeartbeatVorliegt()
    {
        var aufgabe = await ErstelleAutonomeAufgabeAsync();

        var ergebnis = await _sut.PruefeAusfuehrungAsync(aufgabe, TimeSpan.FromMinutes(5));

        ergebnis.Should().BeTrue();
    }

    /// <summary>PauseAufgabeBeiBudgetLimitAsync wirft eine InvalidOperationException, wenn die Aufgabe nicht (mehr) existiert.</summary>
    [Fact]
    public async Task PauseAufgabeBeiBudgetLimit_WirftBeiNichtExistierenderAufgabe()
    {
        var nichtPersistierteAufgabe = new Aufgabe { Id = Guid.NewGuid(), ProjektId = _projektId, Titel = "Unbekannt", ErstellungsDatum = DateTimeOffset.UtcNow };

        var akt = () => _sut.PauseAufgabeBeiBudgetLimitAsync(nichtPersistierteAufgabe);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>SetzeFortAsync wirft eine InvalidOperationException, wenn die Aufgabe nicht (mehr) existiert.</summary>
    [Fact]
    public async Task SetzeFort_WirftBeiNichtExistierenderAufgabe()
    {
        var nichtPersistierteAufgabe = new Aufgabe { Id = Guid.NewGuid(), ProjektId = _projektId, Titel = "Unbekannt", ErstellungsDatum = DateTimeOffset.UtcNow };

        var akt = () => _sut.SetzeFortAsync(nichtPersistierteAufgabe);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>PruefeAusfuehrungAsync wirft eine InvalidOperationException, wenn die Aufgabe nicht (mehr) existiert.</summary>
    [Fact]
    public async Task PruefeAusfuehrung_WirftBeiNichtExistierenderAufgabe()
    {
        var nichtPersistierteAufgabe = new Aufgabe { Id = Guid.NewGuid(), ProjektId = _projektId, Titel = "Unbekannt", ErstellungsDatum = DateTimeOffset.UtcNow };

        var akt = () => _sut.PruefeAusfuehrungAsync(nichtPersistierteAufgabe, TimeSpan.FromMinutes(5));

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }
}
