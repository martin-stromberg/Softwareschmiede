using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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

        var governanceService = new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance);
        _sut = new ProjektleiterAgentService(_db, _cliRunnerMock.Object, governanceService, NullLogger<ProjektleiterAgentService>.Instance);

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
        _db.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private async Task<(Aufgabe Aufgabe, AutonomAufgabeKonfiguration Konfiguration)> ErstelleAutonomeAufgabeAsync()
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
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = Path.Combine(_testRoot, "permissions.json"),
            TokenBudget = 500000,
            LaufzeitLimitMinuten = 480,
            PersistenzModus = PersistenzModus.Standard,
            ArbeitsverzeichnisPfad = _testRoot
        };
        _db.AutonomAufgabeKonfigurationen.Add(konfiguration);
        await _db.SaveChangesAsync();

        await File.WriteAllTextAsync(Path.Combine(_testRoot, "plan.md"), "# Plan\n");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "progress.md"), "# Fortschritt\n");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "state.json"), "{\"subagents\":[]}");

        return (aufgabe, konfiguration);
    }

    /// <summary>StarteAgentAsync startet den Projektleiter-Agenten mit dem Initialprompt und setzt den Ausführungsstatus.</summary>
    [Fact]
    public async Task StarteAgentAsync_StartetAgentMitInitialprompt()
    {
        var (aufgabe, konfiguration) = await ErstelleAutonomeAufgabeAsync();

        var agentId = await _sut.StarteAgentAsync(konfiguration);

        agentId.Should().NotBeNullOrWhiteSpace();
        var aktualisiert = await _db.Aufgaben.FindAsync(aufgabe.Id);
        aktualisiert!.ProjektleiterAgentId.Should().Be(agentId);
        aktualisiert.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);

        var skillPfad = Path.Combine(_testRoot, "skills", "skill_projektleiter_v1.md");
        File.Exists(skillPfad).Should().BeTrue();
        var skillInhalt = await File.ReadAllTextAsync(skillPfad);
        skillInhalt.Should().Contain(konfiguration.InitialPrompt);
    }

    /// <summary>SteuereUnteragentAsync erzeugt Arbeitsverzeichnis, Klon und persistiert die UnteragentSpezifikation.</summary>
    [Fact]
    public async Task SteuereUnteragentAsync_ErzeugtUnteragentSpezifikation()
    {
        var (_, konfiguration) = await ErstelleAutonomeAufgabeAsync();

        var unteragent = new UnteragentSpezifikation
        {
            Id = Guid.NewGuid(),
            AutonomAufgabeId = konfiguration.Id,
            AgentId = "agent-001",
            TaskId = "task_001",
            AgentScope = "feature-backend",
            AgentPrompt = "Implementiere das Backend.",
            AgentDirectory = Path.Combine(_testRoot, "tasks", "task_001"),
            AgentBranch = "feature-unteragent-001",
            AgentClone = Path.Combine(_testRoot, "clones", "repo_feature_001")
        };

        await _sut.SteuereUnteragentAsync(unteragent);

        Directory.Exists(unteragent.AgentDirectory).Should().BeTrue();
        Directory.Exists(unteragent.AgentClone).Should().BeTrue();
        unteragent.Status.Should().Be(UnteragentStatus.Erzeugt);

        var persistiert = await _db.UnteragentSpezifikationen.FindAsync(unteragent.Id);
        persistiert.Should().NotBeNull();
        persistiert!.AgentBranch.Should().Be("feature-unteragent-001");
    }

    /// <summary>IntegriereErgebnisseAsync aktualisiert plan.md und progress.md mit den Ergebnissen des Unteragenten.</summary>
    [Fact]
    public async Task IntegriereErgebnisseAsync_AktualisieertPlanMdUndProgressMd()
    {
        var (_, konfiguration) = await ErstelleAutonomeAufgabeAsync();

        var agentDirectory = Path.Combine(_testRoot, "tasks", "task_002");
        Directory.CreateDirectory(agentDirectory);
        await File.WriteAllTextAsync(Path.Combine(agentDirectory, "task_report.md"), "Backend-Feature erfolgreich implementiert.");

        var unteragent = new UnteragentSpezifikation
        {
            Id = Guid.NewGuid(),
            AutonomAufgabeId = konfiguration.Id,
            AgentId = "agent-002",
            TaskId = "task_002",
            AgentScope = "feature-backend",
            AgentPrompt = "Implementiere das Backend.",
            AgentDirectory = agentDirectory,
            AgentBranch = "feature-unteragent-002",
            AgentClone = Path.Combine(_testRoot, "clones", "repo_feature_002"),
            ErzeugungsDatum = DateTimeOffset.UtcNow,
            Status = UnteragentStatus.Erzeugt
        };
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
}
