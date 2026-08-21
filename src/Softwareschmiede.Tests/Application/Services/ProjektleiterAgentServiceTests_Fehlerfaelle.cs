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

/// <summary>Tests für Fehler- und Validierungspfade des ProjektleiterAgentService.</summary>
public sealed class ProjektleiterAgentServiceTests_Fehlerfaelle : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ICliRunner> _cliRunnerMock = new();
    private readonly ProjektleiterAgentService _sut;
    private readonly string _testRoot;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>ProjektleiterAgentServiceTests_Fehlerfaelle.</summary>
    public ProjektleiterAgentServiceTests_Fehlerfaelle()
    {
        _db = TestDbContextFactory.Create();
        var governanceService = new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance);
        _sut = new ProjektleiterAgentService(_db, _cliRunnerMock.Object, governanceService, NullLogger<ProjektleiterAgentService>.Instance);

        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "ProjektleiterAgentFehler", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(Path.Combine(_testRoot, "clones", "repo_main"));

        _db.Projekte.Add(new Projekt { Id = _projektId, Name = "Testprojekt", ErstellungsDatum = DateTimeOffset.UtcNow, Status = ProjektStatus.Aktiv });
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

    private async Task<AutonomAufgabeKonfiguration> ErstelleKonfigurationAsync()
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

        return konfiguration;
    }

    private UnteragentSpezifikation ErstelleUnteragent(Guid autonomAufgabeId, string suffix = "001") => new()
    {
        Id = Guid.NewGuid(),
        AutonomAufgabeId = autonomAufgabeId,
        AgentId = $"agent-{suffix}",
        TaskId = $"task_{suffix}",
        AgentScope = "feature-backend",
        AgentPrompt = "Implementiere das Backend.",
        AgentDirectory = Path.Combine(_testRoot, "tasks", $"task_{suffix}"),
        AgentBranch = $"feature-unteragent-{suffix}",
        AgentClone = Path.Combine(_testRoot, "clones", $"repo_feature_{suffix}")
    };

    /// <summary>StarteAgentAsync wirft eine InvalidOperationException, wenn die referenzierte Aufgabe nicht existiert.</summary>
    [Fact]
    public async Task StarteAgentAsync_WirftBeiNichtExistierenderAufgabe()
    {
        var konfiguration = new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = Guid.NewGuid(),
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = Path.Combine(_testRoot, "permissions.json"),
            TokenBudget = 500000,
            LaufzeitLimitMinuten = 480,
            PersistenzModus = PersistenzModus.Standard,
            ArbeitsverzeichnisPfad = _testRoot
        };

        var akt = () => _sut.StarteAgentAsync(konfiguration);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>SteuereUnteragentAsync wirft eine InvalidOperationException, wenn die referenzierte AutonomAufgabeKonfiguration nicht existiert.</summary>
    [Fact]
    public async Task SteuereUnteragentAsync_WirftBeiNichtExistierenderKonfiguration()
    {
        var unteragent = ErstelleUnteragent(Guid.NewGuid());

        var akt = () => _sut.SteuereUnteragentAsync(unteragent);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>IntegriereErgebnisseAsync wirft eine InvalidOperationException, wenn die UnteragentSpezifikation nicht persistiert ist.</summary>
    [Fact]
    public async Task IntegriereErgebnisseAsync_WirftBeiNichtPersistierterUnteragentSpezifikation()
    {
        var konfiguration = await ErstelleKonfigurationAsync();
        var unteragent = ErstelleUnteragent(konfiguration.Id);
        Directory.CreateDirectory(unteragent.AgentDirectory);

        var akt = () => _sut.IntegriereErgebnisseAsync(konfiguration, unteragent);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>SteuereUnteragentAsync wirft eine InvalidOperationException, wenn der Git-Klon für den Unteragenten fehlschlägt.</summary>
    [Fact]
    public async Task SteuereUnteragentAsync_WirftBeiFehlgeschlagenemGitKlon()
    {
        _cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("branch")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fatal: Klon fehlgeschlagen"));

        var konfiguration = await ErstelleKonfigurationAsync();
        var unteragent = ErstelleUnteragent(konfiguration.Id);

        var akt = () => _sut.SteuereUnteragentAsync(unteragent);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>SteuereUnteragentAsync wirft eine ArgumentException, wenn AgentScope leer ist.</summary>
    [Fact]
    public async Task SteuereUnteragentAsync_WirftBeiLeeremAgentScope()
    {
        var konfiguration = await ErstelleKonfigurationAsync();
        var unteragent = ErstelleUnteragent(konfiguration.Id);
        unteragent.AgentScope = string.Empty;

        var akt = () => _sut.SteuereUnteragentAsync(unteragent);

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>SteuereUnteragentAsync wirft eine ArgumentException, wenn AgentBranch leer ist.</summary>
    [Fact]
    public async Task SteuereUnteragentAsync_WirftBeiLeeremAgentBranch()
    {
        var konfiguration = await ErstelleKonfigurationAsync();
        var unteragent = ErstelleUnteragent(konfiguration.Id);
        unteragent.AgentBranch = string.Empty;

        var akt = () => _sut.SteuereUnteragentAsync(unteragent);

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>SteuereUnteragentAsync wirft eine ArgumentException, wenn AgentDirectory kein absoluter Pfad ist.</summary>
    [Fact]
    public async Task SteuereUnteragentAsync_WirftBeiRelativemAgentDirectory()
    {
        var konfiguration = await ErstelleKonfigurationAsync();
        var unteragent = ErstelleUnteragent(konfiguration.Id);
        unteragent.AgentDirectory = "tasks/task_001";

        var akt = () => _sut.SteuereUnteragentAsync(unteragent);

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>SteuereUnteragentAsync wirft eine ArgumentException, wenn AgentClone kein absoluter Pfad ist.</summary>
    [Fact]
    public async Task SteuereUnteragentAsync_WirftBeiRelativemAgentClone()
    {
        var konfiguration = await ErstelleKonfigurationAsync();
        var unteragent = ErstelleUnteragent(konfiguration.Id);
        unteragent.AgentClone = "clones/repo_feature_001";

        var akt = () => _sut.SteuereUnteragentAsync(unteragent);

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>IntegriereErgebnisseAsync trägt einen Fallback-Text in progress.md ein, wenn task_report.md im Unteragenten-Verzeichnis fehlt.</summary>
    [Fact]
    public async Task IntegriereErgebnisseAsync_TraegtFallbackEin_WennTaskReportFehlt()
    {
        var konfiguration = await ErstelleKonfigurationAsync();
        var unteragent = ErstelleUnteragent(konfiguration.Id, "003");
        Directory.CreateDirectory(unteragent.AgentDirectory);
        _db.UnteragentSpezifikationen.Add(unteragent);
        await _db.SaveChangesAsync();

        await _sut.IntegriereErgebnisseAsync(konfiguration, unteragent);

        var progressInhalt = await File.ReadAllTextAsync(Path.Combine(_testRoot, "progress.md"));
        progressInhalt.Should().Contain("kein task_report.md gefunden");
    }
}
