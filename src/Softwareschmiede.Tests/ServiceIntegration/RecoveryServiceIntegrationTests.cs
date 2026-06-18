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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProjektService _projektService;
    private readonly Guid _projektId;

    public RecoveryServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);

        var projekt = _projektService.CreateAsync("Recovery-Projekt", null).GetAwaiter().GetResult();
        _projektId = projekt.Id;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ScanForRecoveryCandidates_FindetAufgaben_MitAltemHeartbeatUndInArbeit()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        SetzeAltenHeartbeat(aufgabe.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var kandidaten = await sut.ScanForRecoveryCandidatesAsync();

        kandidaten.Should().Contain(aufgabe.Id);
    }

    [Fact]
    public async Task ScanForRecoveryCandidates_IgnoriertAufgaben_MitFrischemHeartbeat()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        SetzeAltenHeartbeat(aufgabe.Id, minutesAlt: 1);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var kandidaten = await sut.ScanForRecoveryCandidatesAsync();

        kandidaten.Should().NotContain(aufgabe.Id);
    }

    [Fact]
    public async Task ScanForRecoveryCandidates_FindetAufgaben_MitAltemHeartbeatUndWartend()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Wartend);
        SetzeAltenHeartbeat(aufgabe.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var kandidaten = await sut.ScanForRecoveryCandidatesAsync();

        kandidaten.Should().Contain(aufgabe.Id);
    }

    [Fact]
    public async Task ScanForRecoveryCandidates_IgnoriertLaufendeAufgaben()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        SetzeAltenHeartbeat(aufgabe.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(isRunning: true);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        var kandidaten = await sut.ScanForRecoveryCandidatesAsync();

        kandidaten.Should().NotContain(aufgabe.Id);
    }

    [Fact]
    public async Task RecoverManuellAsync_Setzt_StatusAufGestartet()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        SetzeAltenHeartbeat(aufgabe.Id, minutesAlt: 10);

        var running = new E2EFakeRunningStatusSource(false);
        var sut = new AufgabeRecoveryService(_db, running, NullLogger<AufgabeRecoveryService>.Instance);

        await sut.RecoverManuellAsync(aufgabe.Id);

        var geladen = await _db.Aufgaben.FindAsync(aufgabe.Id);
        geladen!.Status.Should().Be(AufgabeStatus.Gestartet);
    }

    private async Task<Aufgabe> ErstelleAufgabeAsync(AufgabeStatus status)
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Recovery-Aufgabe",
            Status = status,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync();
        return aufgabe;
    }

    private void SetzeAltenHeartbeat(Guid aufgabeId, int minutesAlt)
    {
        var aufgabe = _db.Aufgaben.Find(aufgabeId)!;
        aufgabe.LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-minutesAlt);
        _db.SaveChanges();
        _db.Entry(aufgabe).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
    }
}

/// <summary>Fake für IRunningAutomationStatusSource zum Testen (E2E-Variante).</summary>
internal sealed class E2EFakeRunningStatusSource : IRunningAutomationStatusSource
{
    private readonly bool _isRunning;

    public E2EFakeRunningStatusSource(bool isRunning) => _isRunning = isRunning;

    public event Action<int, int>? RunningCountChanged;

    public bool IsRunning(Guid aufgabeId) => _isRunning;

    public int GetRunningCount() => _isRunning ? 1 : 0;
}
