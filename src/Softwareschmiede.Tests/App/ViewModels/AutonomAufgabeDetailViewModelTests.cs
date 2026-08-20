using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für den AutonomAufgabeDetailViewModel.</summary>
public sealed class AutonomAufgabeDetailViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektleiterAgentService _projektleiterAgentService;
    private readonly SessionManagementService _sessionManagementService;
    private readonly AutonomAufgabeDetailViewModel _sut;
    private readonly string _testRoot;
    private readonly Aufgabe _aufgabe;
    private readonly AutonomAufgabeKonfiguration _konfiguration;

    /// <summary>AutonomAufgabeDetailViewModelTests.</summary>
    public AutonomAufgabeDetailViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "DetailVm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        var cliRunnerMock = new Mock<ICliRunner>();
        var governanceService = new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance);
        _projektleiterAgentService = new ProjektleiterAgentService(_db, cliRunnerMock.Object, governanceService, NullLogger<ProjektleiterAgentService>.Instance);
        _sessionManagementService = new SessionManagementService(_db, NullLogger<SessionManagementService>.Instance);

        var projektId = Guid.NewGuid();
        _db.Projekte.Add(new Projekt { Id = projektId, Name = "Testprojekt", ErstellungsDatum = DateTimeOffset.UtcNow, Status = ProjektStatus.Aktiv });
        _aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = "Testaufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.AutonomAufgabe,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(_aufgabe);

        _konfiguration = new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = _aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = Path.Combine(_testRoot, "permissions.json"),
            TokenBudget = 500000,
            LaufzeitLimitMinuten = 480,
            PersistenzModus = PersistenzModus.Standard,
            ArbeitsverzeichnisPfad = _testRoot
        };
        _db.AutonomAufgabeKonfigurationen.Add(_konfiguration);
        _db.SaveChanges();

        _sut = new AutonomAufgabeDetailViewModel(_projektleiterAgentService, _sessionManagementService, NullLogger<AutonomAufgabeDetailViewModel>.Instance);
        _sut.Initialize(_aufgabe, _konfiguration);
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

    /// <summary>LaedePlanAsync lädt plan.md aus dem Arbeitsverzeichnis.</summary>
    [Fact]
    public async Task LaedePlanAsync_LaedesDateiausArbeitsverzeichnis()
    {
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "plan.md"), "# Plan\n\nTeilaufgabe 1: Backend");

        await _sut.LaedePlanAsync();

        _sut.PlanContent.Should().Contain("Teilaufgabe 1: Backend");
    }

    /// <summary>StarteAgentAsync ruft ProjektleiterAgentService.StarteAgentAsync auf und aktualisiert den Ausführungsstatus.</summary>
    [Fact]
    public async Task StarteAgentAsync_CallsProjektleiterAgentService()
    {
        await _sut.StarteAgentAsync();

        _sut.ErrorMessage.Should().BeNull();
        var aktualisiert = await _db.Aufgaben.FindAsync(_aufgabe.Id);
        aktualisiert!.ProjektleiterAgentId.Should().NotBeNullOrWhiteSpace();
        aktualisiert.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);
    }

    /// <summary>AktualisierePlanAsync speichert Änderungen an plan.md im Arbeitsverzeichnis.</summary>
    [Fact]
    public async Task AktualisierePlanAsync_SpeichertAenderungen()
    {
        await _sut.AktualisierePlanAsync("# Plan\n\nAktualisierter Inhalt.");

        _sut.PlanContent.Should().Be("# Plan\n\nAktualisierter Inhalt.");
        var gespeichert = await File.ReadAllTextAsync(Path.Combine(_testRoot, "plan.md"));
        gespeichert.Should().Be("# Plan\n\nAktualisierter Inhalt.");
    }
}
