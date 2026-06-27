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
    /// <summary>RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskIsInArbeitAndNotRunning.</summary>
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
    /// <summary>RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskInWartendAndNotRunning.</summary>
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
    [InlineData(AufgabeStatus.Gestartet, true)]
    [InlineData(AufgabeStatus.Wartend, true)]
    [InlineData(AufgabeStatus.Neu, false)]
    [InlineData(AufgabeStatus.Archiviert, false)]
    [InlineData(AufgabeStatus.Beendet, false)]
    /// <summary>IstRecoveryStatus_ShouldMatchAllowedStates.</summary>
    public void IstRecoveryStatus_ShouldMatchAllowedStates(AufgabeStatus status, bool expected)
    {
        AufgabeRecoveryService.IstRecoveryStatus(status).Should().Be(expected);
    }

    /// <summary><summary>RecoverManuellAsync_ShouldThrow_WhenTaskIsStillRunning.</summary>.</summary>
    [Fact]
    /// <summary>RecoverManuellAsync_ShouldThrow_WhenTaskIsStillRunning.</summary>
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
    /// <summary>RecoverManuellAsync_ShouldThrow_WhenStatusIsNotRecoverable.</summary>
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
    /// <summary>RecoverManuellAsync_ShouldThrow_WhenRunningCheckFails.</summary>
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
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        // Aufgabe Neu (soll nie erkannt werden)
        var aufgabeNeu = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Neue Aufgabe",
            Status = AufgabeStatus.Neu,
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        _db.Aufgaben.AddRange(aufgabeAlt, aufgabeWartend, aufgabeFrisch, aufgabeNeu);
        await _db.SaveChangesAsync();

        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        // Act
        var kandidaten = (await sut.ScanForRecoveryCandidatesAsync()).ToList();

        // Assert
        kandidaten.Should().Contain(aufgabeAlt.Id);
        kandidaten.Should().Contain(aufgabeWartend.Id);
        kandidaten.Should().NotContain(aufgabeFrisch.Id);
        kandidaten.Should().NotContain(aufgabeNeu.Id);
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
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync();
        return aufgabe;
    }

    private sealed class FakeRunningAutomationStatusSource(bool isRunning) : IRunningAutomationStatusSource
    {
        public event Action<int, int>? RunningCountChanged;
        /// <summary>GetRunningCount.</summary>
        public int GetRunningCount() => isRunning ? 1 : 0;
        /// <summary>IsRunning.</summary>
        public bool IsRunning(Guid aufgabeId) => isRunning;
    }

    private sealed class ThrowingRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        public event Action<int, int>? RunningCountChanged;
        /// <summary>GetRunningCount.</summary>
        public int GetRunningCount() => 0;
        /// <summary>IsRunning.</summary>
        public bool IsRunning(Guid aufgabeId) => throw new TimeoutException("simulated");
    }
}
