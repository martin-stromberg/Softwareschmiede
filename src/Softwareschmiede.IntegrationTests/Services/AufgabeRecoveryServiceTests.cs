using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="AufgabeRecoveryService"/> mit echter SQLite-Datenbank.</summary>
public sealed class AufgabeRecoveryServiceTests
{
    /// <summary>Prüft, dass RecoveryVersion inkrementiert und ein Audit-Eintrag angelegt wird.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldSetGestartetAndCreateAudit_WhenStatusIsGestartet()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabe = await CreateTaskAsync(db, AufgabeStatus.Gestartet);
        var sut = new AufgabeRecoveryService(db.Context, new AlwaysNotRunningAutomationStatusSource(), NullLogger<AufgabeRecoveryService>.Instance);

        await sut.RecoverManuellAsync(aufgabe.Id);

        await using var assertContext = db.CreateNewContext();
        var loaded = await assertContext.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.Status.Should().Be(AufgabeStatus.Gestartet);
        loaded.RecoveryVersion.Should().Be(1);
        assertContext.Protokolleintraege
            .Count(p => p.AufgabeId == aufgabe.Id && p.Typ == ProtokollTyp.StatusUebergang && p.Inhalt.Contains("Manuelle Wiederherstellung"))
            .Should().Be(1);
    }

    /// <summary>Prüft, dass Recovery abgelehnt wird, wenn der CLI-Prozess noch läuft.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrowAndKeepStatus_WhenProcessingIsStillRunning()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabe = await CreateTaskAsync(db, AufgabeStatus.Gestartet);
        var sut = new AufgabeRecoveryService(db.Context, new AlwaysRunningAutomationStatusSource(), NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung nicht möglich, Verarbeitung läuft noch.");

        await using var assertContext = db.CreateNewContext();
        var loaded = await assertContext.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.Status.Should().Be(AufgabeStatus.Gestartet);
        loaded.RecoveryVersion.Should().Be(0);
        assertContext.Protokolleintraege.Count(p => p.AufgabeId == aufgabe.Id).Should().Be(0);
    }

    /// <summary>Prüft, dass ein Nebenläufigkeitskonflikt erkannt wird, wenn RecoveryVersion sich während der Recovery ändert.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldThrowConcurrencyConflict_WhenRecoveryVersionChangedConcurrently()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabe = await CreateTaskAsync(db, AufgabeStatus.Gestartet);
        using var concurrentContext = db.CreateNewContext();
        var runningStatus = new MutatingNotRunningAutomationStatusSource(() =>
        {
            var concurrent = concurrentContext.Aufgaben.Single(a => a.Id == aufgabe.Id);
            concurrent.RecoveryVersion++;
            concurrentContext.SaveChanges();
        });
        var sut = new AufgabeRecoveryService(db.Context, runningStatus, NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Status wurde bereits geändert. Ansicht wurde aktualisiert.");

        await using var assertContext = db.CreateNewContext();
        var loaded = await assertContext.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.Status.Should().Be(AufgabeStatus.Gestartet);
        loaded.RecoveryVersion.Should().Be(1);
        assertContext.Protokolleintraege.Count(p => p.AufgabeId == aufgabe.Id).Should().Be(0);
    }

    /// <summary>Prüft, dass Recovery für ungültige Ausgangszustände abgelehnt wird.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldRejectInvalidState()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabe = await CreateTaskAsync(db, AufgabeStatus.Neu);
        var sut = new AufgabeRecoveryService(db.Context, new AlwaysNotRunningAutomationStatusSource(), NullLogger<AufgabeRecoveryService>.Instance);

        var act = () => sut.RecoverManuellAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wiederherstellung für aktuellen Status nicht verfügbar.");
    }

    /// <summary>Prüft, dass bei parallelen Recovery-Versuchen mit demselben Snapshot exakt einer erfolgreich ist.</summary>
    [Fact]
    public async Task RecoverManuellAsync_ShouldAllowExactlyOneSuccess_WhenTriggeredInParallel()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabe = await CreateTaskAsync(db, AufgabeStatus.Gestartet);

        await using var context1 = db.CreateNewContext();
        await using var context2 = db.CreateNewContext();

        // The barrier ensures both tasks complete their initial entity read (RecoveryVersion=0)
        // before either proceeds to the transaction and update. Without it, one task may finish
        // entirely before the other reads, giving the second task a different starting RecoveryVersion
        // and allowing both to succeed.
        var runningStatus = new BarrieredNotRunningStatusSource(participantCount: 2);
        var recovery1 = new AufgabeRecoveryService(context1, runningStatus, NullLogger<AufgabeRecoveryService>.Instance);
        var recovery2 = new AufgabeRecoveryService(context2, runningStatus, NullLogger<AufgabeRecoveryService>.Instance);

        var task1 = Task.Run(() => ExecuteRecoverSafeAsync(recovery1, aufgabe.Id));
        var task2 = Task.Run(() => ExecuteRecoverSafeAsync(recovery2, aufgabe.Id));
        var results = await Task.WhenAll(task1, task2);

        results.Count(r => r).Should().Be(1);

        await using var assertContext = db.CreateNewContext();
        var loaded = await assertContext.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.Status.Should().Be(AufgabeStatus.Gestartet);
        assertContext.Protokolleintraege
            .Count(p => p.AufgabeId == aufgabe.Id && p.Typ == ProtokollTyp.StatusUebergang && p.Inhalt.Contains("Manuelle Wiederherstellung"))
            .Should().Be(1);
    }

    private static async Task<bool> ExecuteRecoverSafeAsync(AufgabeRecoveryService service, Guid aufgabeId)
    {
        try
        {
            await service.RecoverManuellAsync(aufgabeId);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class AlwaysNotRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        public event Action<int, int>? RunningCountChanged;
        public int GetRunningCount() => 0;
        public bool IsRunning(Guid aufgabeId) => false;
    }

    private sealed class AlwaysRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        public event Action<int, int>? RunningCountChanged;
        public int GetRunningCount() => 1;
        public bool IsRunning(Guid aufgabeId) => true;
    }

    private sealed class MutatingNotRunningAutomationStatusSource(Action beforeReturn) : IRunningAutomationStatusSource
    {
        private int _called;
        public event Action<int, int>? RunningCountChanged;
        public int GetRunningCount() => 0;

        public bool IsRunning(Guid aufgabeId)
        {
            if (Interlocked.Exchange(ref _called, 1) == 0)
            {
                beforeReturn();
            }

            return false;
        }
    }

    // Ensures all participant tasks have completed their initial DB read before any proceeds to
    // the transaction and UPDATE. SignalAndWait blocks synchronously, so Task.Run is required in
    // the calling test to avoid deadlock on a single-threaded context.
    private sealed class BarrieredNotRunningStatusSource(int participantCount) : IRunningAutomationStatusSource
    {
        private readonly Barrier _barrier = new(participantCount);
        public event Action<int, int>? RunningCountChanged;
        public int GetRunningCount() => 0;

        public bool IsRunning(Guid aufgabeId)
        {
            _barrier.SignalAndWait(TimeSpan.FromSeconds(10));
            return false;
        }
    }

    private static async Task<Aufgabe> CreateTaskAsync(DatabaseFixture db, AufgabeStatus status)
    {
        var projekt = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var projektEntity = await projekt.CreateAsync("Recovery-Parallel", null);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var aufgabe = await aufgabeService.CreateAsync(projektEntity.Id, "Festhängende Aufgabe", "test");
        await aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
        return aufgabe;
    }
}
