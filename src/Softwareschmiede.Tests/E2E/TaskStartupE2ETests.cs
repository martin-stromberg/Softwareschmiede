using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.E2E;

/// <summary>E2E-Test: Aufgabe starten → Arbeitsverzeichnis einrichten → Branch erstellen.</summary>
public sealed class TaskStartupE2ETests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;

    public TaskStartupE2ETests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task StartenAsync_Setzt_Status_AufArbeitsverzeichnisEingerichtet()
    {
        var projekt = await _projektService.CreateAsync("Startup-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Startup-Aufgabe", null);

        aufgabe.Status.Should().Be(AufgabeStatus.Neu);

        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/test-branch", "/tmp/repo");

        var geladen = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geladen!.Status.Should().Be(AufgabeStatus.ArbeitsverzeichnisEingerichtet);
        geladen.BranchName.Should().Be("feature/test-branch");
        geladen.LokalerKlonPfad.Should().Be("/tmp/repo");
    }

    [Fact]
    public async Task StatusUebergang_ArbeitsverzeichnisEingerichtet_NachGestartet_IstErlaubt()
    {
        var projekt = await _projektService.CreateAsync("Uebergang-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Uebergang-Aufgabe", null);

        await _aufgabeService.StartenAsync(aufgabe.Id, "main", "/tmp/repo");
        await _aufgabeService.SetStatusAsync(aufgabe.Id, AufgabeStatus.Gestartet);

        var geladen = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geladen!.Status.Should().Be(AufgabeStatus.Gestartet);
    }

    [Fact]
    public async Task StatusUebergang_Gestartet_NachInArbeit_IstErlaubt()
    {
        var projekt = await _projektService.CreateAsync("InArbeit-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "InArbeit-Aufgabe", null);

        await _aufgabeService.StartenAsync(aufgabe.Id, "main", "/tmp/repo");
        await _aufgabeService.SetStatusAsync(aufgabe.Id, AufgabeStatus.Gestartet);
        await _aufgabeService.SetStatusAsync(aufgabe.Id, AufgabeStatus.InArbeit);

        var geladen = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geladen!.Status.Should().Be(AufgabeStatus.InArbeit);
    }
}
