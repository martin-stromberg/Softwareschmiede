using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den ProjektleiterAgentService.</summary>
public sealed class ProjektleiterAgentServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly KiAusfuehrungsService _kiAusfuehrungsService;
    private readonly Mock<IKiPlugin> _kiPluginMock;
    private readonly ProjektleiterAgentService _sut;
    private readonly string _testRoot;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>ProjektleiterAgentServiceTests.</summary>
    public ProjektleiterAgentServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _cliRunnerMock = new Mock<ICliRunner>();
        _cliRunnerMock
            .Setup(r => r.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.Contains("clone")),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) =>
            {
                Directory.CreateDirectory(args.Last());
            })
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock
            .Setup(r => r.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.Contains("branch")),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupPassthroughResolveEffectiveRepositoryPath();

        var governanceService = new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance);
        var gitProvisioningService = new UnteragentGitProvisioningService(_cliRunnerMock.Object, gitPluginMock.Object, NullLogger<UnteragentGitProvisioningService>.Instance);
        _kiAusfuehrungsService = TestKiAusfuehrungsServiceFactory.Create();
        (_kiPluginMock, var pluginSelectionService) = ProjektleiterAgentServiceTestDatenFactory.ErstellePluginSelectionServiceMitKiPlugin(_db);
        _sut = new ProjektleiterAgentService(_db, governanceService, gitProvisioningService, _kiAusfuehrungsService, pluginSelectionService, new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance), Options.Create(new AutonomAufgabenOptions()), NullLogger<ProjektleiterAgentService>.Instance);

        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "ProjektleiterAgent", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(Path.Combine(_testRoot, "clones", "repo_main"));

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
        _kiAusfuehrungsService.Dispose();
        _db.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    /// <summary>StarteAgentAsync startet den Projektleiter-Agenten mit dem Initialprompt und setzt den Ausführungsstatus.</summary>
    [Fact]
    public async Task StarteAgentAsync_StartetAgentMitInitialprompt()
    {
        var (aufgabe, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);

        var agentId = await _sut.StarteAgentAsync(konfiguration);

        agentId.Should().NotBeNullOrWhiteSpace();
        var aktualisiert = await _db.Aufgaben.FindAsync(aufgabe.Id);
        aktualisiert!.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);

        var konfigurationAktualisiert = await _db.AutonomAufgabeKonfigurationen.FindAsync(konfiguration.Id);
        konfigurationAktualisiert!.ProjektleiterAgentId.Should().Be(agentId);

        var skillPfad = Path.Combine(_testRoot, "skills", "skill_projektleiter_v1.md");
        File.Exists(skillPfad).Should().BeTrue();
        var skillInhalt = await File.ReadAllTextAsync(skillPfad);
        skillInhalt.Should().Contain(konfiguration.InitialPrompt);
    }

    /// <summary>SteuereUnteragentAsync erzeugt Arbeitsverzeichnis, Klon und persistiert die UnteragentSpezifikation.</summary>
    [Fact]
    public async Task SteuereUnteragentAsync_ErzeugtUnteragentSpezifikation()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);

        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id);

        await _sut.SteuereUnteragentAsync(unteragent);

        Directory.Exists(unteragent.VerzeichnisPfad).Should().BeTrue();
        Directory.Exists(unteragent.ClonePfad).Should().BeTrue();
        unteragent.Status.Should().Be(UnteragentStatus.Erzeugt);

        var persistiert = await _db.UnteragentSpezifikationen.FindAsync(unteragent.Id);
        persistiert.Should().NotBeNull();
        persistiert!.Branch.Should().Be("feature-unteragent-001");
    }

    /// <summary>IntegriereErgebnisseAsync aktualisiert plan.md und progress.md mit den Ergebnissen des Unteragenten.</summary>
    [Fact]
    public async Task IntegriereErgebnisseAsync_AktualisieertPlanMdUndProgressMd()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);

        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id, "002");
        unteragent.ErzeugungsDatum = DateTimeOffset.UtcNow;
        unteragent.Status = UnteragentStatus.Erzeugt;

        Directory.CreateDirectory(unteragent.VerzeichnisPfad);
        await File.WriteAllTextAsync(Path.Combine(unteragent.VerzeichnisPfad, "task_report.md"), "Backend-Feature erfolgreich implementiert.");

        _db.UnteragentSpezifikationen.Add(unteragent);
        await _db.SaveChangesAsync();

        await _sut.IntegriereErgebnisseAsync(konfiguration, unteragent);

        var planInhalt = await File.ReadAllTextAsync(Path.Combine(_testRoot, "plan.md"));
        planInhalt.Should().Contain("task_002");

        var progressInhalt = await File.ReadAllTextAsync(Path.Combine(_testRoot, "progress.md"));
        progressInhalt.Should().Contain("Backend-Feature erfolgreich implementiert.");

        var persistiert = await _db.UnteragentSpezifikationen.FindAsync(unteragent.Id);
        persistiert!.Status.Should().Be(UnteragentStatus.Abgeschlossen);
        persistiert.AbschlussDatum.Should().NotBeNull();
    }

    /// <summary>StarteAgentAsync wirft eine InvalidOperationException, wenn das Feature-Flag AutonomAufgabenOptions.Enabled deaktiviert ist (Guard-Klausel, Issue 205).</summary>
    [Fact]
    public async Task WhenEnabledFlagIsFalse_StarteAgentAsync_ShouldThrow()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        var sut = AutonomAufgabenInitialisierungsServiceTestFactory.CreateProjektleiterAgentService(
            _db, _kiAusfuehrungsService, new AutonomAufgabenOptions { Enabled = false });

        var akt = () => sut.StarteAgentAsync(konfiguration);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>StarteAgentAsync startet den Agenten normal, wenn das Feature-Flag AutonomAufgabenOptions.Enabled aktiviert ist (Baseline-Test gegen Regression der Guard-Klausel, Issue 205).</summary>
    [Fact]
    public async Task WhenEnabledFlagIsTrue_StarteAgentAsync_ShouldSucceed()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        var sut = AutonomAufgabenInitialisierungsServiceTestFactory.CreateProjektleiterAgentService(
            _db, _kiAusfuehrungsService, new AutonomAufgabenOptions { Enabled = true });

        var agentId = await sut.StarteAgentAsync(konfiguration);

        agentId.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>StarteAgentAsync wirft eine InvalidOperationException, wenn der DB-persistierte Laufzeit-Schalter
    /// (AppEinstellungService.AutonomAufgabenEnabledKey, GUI-Einstellung) auf false steht, selbst wenn der
    /// appsettings.json-Deployment-Default AutonomAufgabenOptions.Enabled true ist (Issue 205, Verdrahtung
    /// Settings-Schalter -> Guard-Klausel).</summary>
    [Fact]
    public async Task WhenDbValueIsFalse_StarteAgentAsync_ShouldThrow_EvenIfOptionsEnabledIsTrue()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        var appEinstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        await appEinstellungService.SetBoolSettingAsync(AppEinstellungService.AutonomAufgabenEnabledKey, false);
        var sut = AutonomAufgabenInitialisierungsServiceTestFactory.CreateProjektleiterAgentService(
            _db, _kiAusfuehrungsService, new AutonomAufgabenOptions { Enabled = true });

        var akt = () => sut.StarteAgentAsync(konfiguration);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>StarteAgentAsync startet den Agenten normal, wenn der DB-persistierte Laufzeit-Schalter auf true
    /// steht, selbst wenn der appsettings.json-Deployment-Default AutonomAufgabenOptions.Enabled false ist
    /// (Issue 205, Verdrahtung Settings-Schalter -> Guard-Klausel).</summary>
    [Fact]
    public async Task WhenDbValueIsTrue_StarteAgentAsync_ShouldSucceed_EvenIfOptionsEnabledIsFalse()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        var appEinstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        await appEinstellungService.SetBoolSettingAsync(AppEinstellungService.AutonomAufgabenEnabledKey, true);
        var sut = AutonomAufgabenInitialisierungsServiceTestFactory.CreateProjektleiterAgentService(
            _db, _kiAusfuehrungsService, new AutonomAufgabenOptions { Enabled = false });

        var agentId = await sut.StarteAgentAsync(konfiguration);

        agentId.Should().NotBeNullOrWhiteSpace();
    }
}
