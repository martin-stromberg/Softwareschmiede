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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>AufgabeRecoveryServiceTests.</summary>
    public AufgabeRecoveryServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Recovery Projekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary><summary>RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskIsInArbeitAndNotRunning.</summary>.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskIsInArbeitAndNotRunning()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        await sut.RecoverManuellAsync(aufgabe.Id);

        var loaded = await _db.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.Status.Should().Be(AufgabeStatus.Gestartet);
        _db.Protokolleintraege.Count(e => e.AufgabeId == aufgabe.Id && e.Typ == ProtokollTyp.StatusUebergang).Should().Be(1);
        _db.Protokolleintraege.Single(e => e.AufgabeId == aufgabe.Id).Inhalt.Should().Contain("Manuelle Wiederherstellung");
    }

    /// <summary><summary>RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskInWartendAndNotRunning.</summary>.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskInWartendAndNotRunning()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Wartend);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        await sut.RecoverManuellAsync(aufgabe.Id);

        var loaded = await _db.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.Status.Should().Be(AufgabeStatus.Gestartet);
        loaded.RecoveryVersion.Should().Be(1);
        _db.Protokolleintraege.Count(e => e.AufgabeId == aufgabe.Id && e.Typ == ProtokollTyp.StatusUebergang).Should().Be(1);
    }

    /// <summary><summary>IstRecoveryStatus_ShouldMatchAllowedStates.</summary>.</summary>
    /// <summary><summary>IstRecoveryStatus_ShouldMatchAllowedStates.</summary>.</summary>
    /// <summary><summary>IstRecoveryStatus_ShouldMatchAllowedStates.</summary>.</summary>
    /// <summary><summary>IstRecoveryStatus_ShouldMatchAllowedStates.</summary>.</summary>
    /// <summary><summary>IstRecoveryStatus_ShouldMatchAllowedStates.</summary>.</summary>
    /// <summary><summary>IstRecoveryStatus_ShouldMatchAllowedStates.</summary>.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Gestartet, AufgabeAusfuehrungsStatus.Aktiv, false, true)]
    [InlineData(AufgabeStatus.Wartend, AufgabeAusfuehrungsStatus.Aktiv, false, true)]
    [InlineData(AufgabeStatus.Gestartet, AufgabeAusfuehrungsStatus.Beendet, false, false)]
    [InlineData(AufgabeStatus.Gestartet, AufgabeAusfuehrungsStatus.NichtGestartet, false, false)]
    [InlineData(AufgabeStatus.Neu, AufgabeAusfuehrungsStatus.Aktiv, false, false)]
    [InlineData(AufgabeStatus.Archiviert, AufgabeAusfuehrungsStatus.Aktiv, false, false)]
    [InlineData(AufgabeStatus.Beendet, AufgabeAusfuehrungsStatus.Aktiv, false, false)]
    [InlineData(AufgabeStatus.Gestartet, AufgabeAusfuehrungsStatus.Aktiv, true, false)]
    public void IstRecoveryStatus_ShouldMatchAllowedStates(AufgabeStatus status, AufgabeAusfuehrungsStatus ausfuehrungsStatus, bool istAutonom, bool expected)
    {
        AufgabeRecoveryService.IstRecoveryStatus(status, ausfuehrungsStatus, istAutonom).Should().Be(expected);
    }

    /// <summary><summary>RecoverManuellAsync_ShouldThrow_WhenTaskIsStillRunning.</summary>.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenTaskIsStillRunning()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var running = new FakeRunningAutomationStatusSource(true);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung nicht möglich, Verarbeitung läuft noch.");
    }

    /// <summary><summary>RecoverManuellAsync_ShouldThrow_WhenStatusIsNotRecoverable.</summary>.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenStatusIsNotRecoverable()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Neu);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung für aktuellen Status nicht verfügbar.");
    }

    /// <summary><summary>RecoverManuellAsync_ShouldThrow_WhenRunningCheckFails.</summary>.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenRunningCheckFails()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var running = new ThrowingRunningAutomationStatusSource();
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Prüfung der Laufzeit war nicht möglich.");
    }

    /// <summary>TestRecoveryCandidates: Aufgaben mit Heartbeat > 5 Min und Status InArbeit/Wartend werden erkannt.</summary>
    [Fact]
    public async Task TestRecoveryCandidates()
    {
        // Arrange – Aufgabe InArbeit mit altem Heartbeat
        var aufgabeAlt = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Alte InArbeit Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        // Aufgabe Wartend mit altem Heartbeat
        var aufgabeWartend = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Alte Wartend Aufgabe",
            Status = AufgabeStatus.Wartend,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-6),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        // Aufgabe InArbeit mit frischem Heartbeat (soll nicht erkannt werden)
        var aufgabeFrisch = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Frische InArbeit Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        var aufgabeBeendeteAusfuehrung = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Beendete Ausfuehrung",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Beendet,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        // Aufgabe Neu (soll nie erkannt werden)
        var aufgabeNeu = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Neue Aufgabe",
            Status = AufgabeStatus.Neu,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.NichtGestartet,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        _db.Aufgaben.AddRange(aufgabeAlt, aufgabeWartend, aufgabeFrisch, aufgabeBeendeteAusfuehrung, aufgabeNeu);
        await _db.SaveChangesAsync();

        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        // Act
        var kandidaten = (await sut.ScanForRecoveryCandidatesAsync()).ToList();

        // Assert
        kandidaten.Should().Contain(aufgabeAlt.Id);
        kandidaten.Should().Contain(aufgabeWartend.Id);
        kandidaten.Should().NotContain(aufgabeFrisch.Id);
        kandidaten.Should().NotContain(aufgabeBeendeteAusfuehrung.Id);
        kandidaten.Should().NotContain(aufgabeNeu.Id);
    }

    /// <summary>ScanForRecoveryCandidatesAsync ignoriert Autonome Aufgaben, auch wenn Status/Heartbeat sonst einen Recovery-Kandidaten ergeben würden: Autonome Aufgaben werden vom Projektleiter-Agenten selbst gesteuert, nicht durch die generische Crash-Recovery.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_ShouldExcludeAutonomeAufgaben()
    {
        var aufgabeAutonom = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Autonome Aufgabe mit altem Heartbeat",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(aufgabeAutonom);
        _db.AutonomAufgabeKonfigurationen.Add(new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabeAutonom.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = @"C:\arbeitsverzeichnis\permissions.json",
            ArbeitsverzeichnisPfad = @"C:\arbeitsverzeichnis"
        });
        await _db.SaveChangesAsync();

        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var kandidaten = (await sut.ScanForRecoveryCandidatesAsync()).ToList();

        kandidaten.Should().NotContain(aufgabeAutonom.Id);
    }

    /// <summary>RecoverManuellAsync lehnt eine Autonome Aufgabe ab, selbst wenn Status/Ausführungsstatus sonst einer manuellen Wiederherstellung entsprechen würden.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenAufgabeIstAutonom()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Autonome Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(aufgabe);
        _db.AutonomAufgabeKonfigurationen.Add(new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = @"C:\arbeitsverzeichnis\permissions.json",
            ArbeitsverzeichnisPfad = @"C:\arbeitsverzeichnis"
        });
        await _db.SaveChangesAsync();
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung für aktuellen Status nicht verfügbar.");
    }

    /// <summary>ScanForRecoveryCandidates ignoriert Aufgaben, für die ein Prozess noch läuft.</summary>
    [Fact]
    public async Task ScanForRecoveryCandidates_ShouldExcludeRunningTasks()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Laufende Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync();

        var running = new FakeRunningAutomationStatusSource(true);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var kandidaten = (await sut.ScanForRecoveryCandidatesAsync()).ToList();

        kandidaten.Should().BeEmpty();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private async Task<Aufgabe> ErstelleAufgabeAsync(AufgabeStatus status)
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Recovery Task",
            Status = status,
            AusfuehrungsStatus = status.IstAktivOderWartend()
                ? AufgabeAusfuehrungsStatus.Aktiv
                : AufgabeAusfuehrungsStatus.NichtGestartet,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync();
        return aufgabe;
    }

    private sealed class FakeRunningAutomationStatusSource(bool isRunning) : IRunningAutomationStatusSource
    {
#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        /// <summary>GetRunningCount.</summary>
        public int GetRunningCount() => isRunning ? 1 : 0;
        /// <summary>IsRunning.</summary>
        public bool IsRunning(Guid aufgabeId) => isRunning;
    }

    private sealed class ThrowingRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
#pragma warning disable CS0067 // von IRunningAutomationStatusSource gefordert, in diesem Fake ungenutzt
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        /// <summary>GetRunningCount.</summary>
        public int GetRunningCount() => 0;
        /// <summary>IsRunning.</summary>
        public bool IsRunning(Guid aufgabeId) => throw new TimeoutException("simulated");
    }
}
