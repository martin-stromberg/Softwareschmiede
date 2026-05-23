using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

public sealed class AufgabeRecoveryServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Guid _projektId = Guid.NewGuid();

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

    [Fact]
    public async Task RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskIsStuckAndNotRunning()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.KiAktiv);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        await sut.RecoverManuellAsync(aufgabe.Id);

        var loaded = await _db.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.Status.Should().Be(AufgabeStatus.InBearbeitung);
        _db.Protokolleintraege.Count(e => e.AufgabeId == aufgabe.Id && e.Typ == ProtokollTyp.StatusUebergang).Should().Be(1);
        _db.Protokolleintraege.Single(e => e.AufgabeId == aufgabe.Id).Inhalt.Should().Contain("Manuelle Wiederherstellung");
    }

    [Fact]
    public async Task RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskInTestsLaufenAndNotRunning()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.TestsLaufen);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        await sut.RecoverManuellAsync(aufgabe.Id);

        var loaded = await _db.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.Status.Should().Be(AufgabeStatus.InBearbeitung);
        loaded.RecoveryVersion.Should().Be(1);
        _db.Protokolleintraege.Count(e => e.AufgabeId == aufgabe.Id && e.Typ == ProtokollTyp.StatusUebergang).Should().Be(1);
    }

    [Theory]
    [InlineData(AufgabeStatus.KiAktiv, true)]
    [InlineData(AufgabeStatus.TestsLaufen, true)]
    [InlineData(AufgabeStatus.Offen, false)]
    [InlineData(AufgabeStatus.InBearbeitung, false)]
    [InlineData(AufgabeStatus.Abgeschlossen, false)]
    public void IstRecoveryStatus_ShouldMatchAllowedStates(AufgabeStatus status, bool expected)
    {
        AufgabeRecoveryService.IstRecoveryStatus(status).Should().Be(expected);
    }

    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenTaskIsStillRunning()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.KiAktiv);
        var running = new FakeRunningAutomationStatusSource(true);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung nicht möglich, Verarbeitung läuft noch.");
    }

    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenStatusIsNotRecoverable()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Offen);
        var running = new FakeRunningAutomationStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung für aktuellen Status nicht verfügbar.");
    }

    [Fact]
    public async Task RecoverManuellAsync_ShouldThrow_WhenRunningCheckFails()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.KiAktiv);
        var running = new ThrowingRunningAutomationStatusSource();
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Prüfung der Laufzeit war nicht möglich.");
    }

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
        public int GetRunningCount() => isRunning ? 1 : 0;
        public bool IsRunning(Guid aufgabeId) => isRunning;
    }

    private sealed class ThrowingRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        public event Action<int, int>? RunningCountChanged;
        public int GetRunningCount() => 0;
        public bool IsRunning(Guid aufgabeId) => throw new TimeoutException("simulated");
    }
}
