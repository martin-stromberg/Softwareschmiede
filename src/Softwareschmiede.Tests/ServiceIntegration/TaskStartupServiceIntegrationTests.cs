using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Aufgabe starten → Arbeitsverzeichnis einrichten → Branch erstellen.</summary>
public sealed class TaskStartupServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;

    /// <summary>TaskStartupServiceIntegrationTests.</summary>
    public TaskStartupServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>StartenAsync_Setzt_Status_AufGestartet.</summary>.</summary>
    [Fact]
    /// <summary>StartenAsync_Setzt_Status_AufGestartet.</summary>
    public async Task StartenAsync_Setzt_Status_AufGestartet()
    {
        var projekt = await _projektService.CreateAsync("Startup-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Startup-Aufgabe", null);

        aufgabe.Status.Should().Be(AufgabeStatus.Neu);

        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/test-branch", "/tmp/repo");

        var geladen = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geladen!.Status.Should().Be(AufgabeStatus.Gestartet);
        geladen.BranchName.Should().Be("feature/test-branch");
        geladen.LokalerKlonPfad.Should().Be("/tmp/repo");
    }

    /// <summary><summary>StatusUebergang_Gestartet_NachWartend_IstErlaubt.</summary>.</summary>
    [Fact]
    /// <summary>StatusUebergang_Gestartet_NachWartend_IstErlaubt.</summary>
    public async Task StatusUebergang_Gestartet_NachWartend_IstErlaubt()
    {
        var projekt = await _projektService.CreateAsync("Uebergang-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Uebergang-Aufgabe", null);

        await _aufgabeService.StartenAsync(aufgabe.Id, "main", "/tmp/repo");
        await _aufgabeService.SetStatusAsync(aufgabe.Id, AufgabeStatus.Wartend);

        var geladen = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geladen!.Status.Should().Be(AufgabeStatus.Wartend);
    }

    /// <summary><summary>StatusUebergang_Wartend_NachGestartet_IstErlaubt.</summary>.</summary>
    [Fact]
    /// <summary>StatusUebergang_Wartend_NachGestartet_IstErlaubt.</summary>
    public async Task StatusUebergang_Wartend_NachGestartet_IstErlaubt()
    {
        var projekt = await _projektService.CreateAsync("Resume-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Resume-Aufgabe", null);

        await _aufgabeService.StartenAsync(aufgabe.Id, "main", "/tmp/repo");
        await _aufgabeService.SetStatusAsync(aufgabe.Id, AufgabeStatus.Wartend);
        await _aufgabeService.SetStatusAsync(aufgabe.Id, AufgabeStatus.Gestartet);

        var geladen = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geladen!.Status.Should().Be(AufgabeStatus.Gestartet);
    }
}
