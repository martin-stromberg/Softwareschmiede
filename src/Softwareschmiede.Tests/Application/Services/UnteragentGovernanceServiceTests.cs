using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Exceptions;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den UnteragentGovernanceService.</summary>
public sealed class UnteragentGovernanceServiceTests : IDisposable
{
    private readonly UnteragentGovernanceService _sut;
    private readonly string _testRoot;
    private readonly string _agentDirectory;

    /// <summary>UnteragentGovernanceServiceTests.</summary>
    public UnteragentGovernanceServiceTests()
    {
        _sut = new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance);
        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "UnteragentGovernance", Guid.NewGuid().ToString("N"));
        _agentDirectory = Path.Combine(_testRoot, "tasks", "task_001");
        Directory.CreateDirectory(_agentDirectory);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private UnteragentSpezifikation ErstelleUnteragent() => new()
    {
        Id = Guid.NewGuid(),
        AutonomAufgabeId = Guid.NewGuid(),
        AgentId = "agent-001",
        TaskId = "task_001",
        AgentScope = "feature-backend",
        AgentPrompt = "Implementiere das Backend-Feature.",
        AgentDirectory = _agentDirectory,
        AgentBranch = "feature-unteragent-001",
        AgentClone = Path.Combine(_testRoot, "clones", "repo_feature_001"),
        ErzeugungsDatum = DateTimeOffset.UtcNow,
        Status = UnteragentStatus.Erzeugt
    };

    /// <summary>VerifiziereBerechtigung erlaubt Zugriff auf den eigenen Arbeitsbereich (tasks/task_XXX/).</summary>
    [Fact]
    public void VerifiziereBerechtigung_ErlaubtZugriffAufEigenenBereich()
    {
        var unteragent = ErstelleUnteragent();
        var zielPfad = Path.Combine(_agentDirectory, "task_report.md");

        var erlaubt = _sut.VerifiziereBerechtigung(unteragent, UnteragentAktion.ArbeitsverzeichnisErstellen, zielPfad);

        erlaubt.Should().BeTrue();
    }

    /// <summary>VerifiziereBerechtigung verbietet Änderungen außerhalb des eigenen Arbeitsbereichs (z. B. clones/).</summary>
    [Fact]
    public void VerifiziereBerechtigung_VerbietetAenderungenAusserhalbArbeitsbereich()
    {
        var unteragent = ErstelleUnteragent();
        var zielPfad = Path.Combine(_testRoot, "clones", "repo_main", "irgendeine-datei.txt");

        var erlaubt = _sut.VerifiziereBerechtigung(unteragent, UnteragentAktion.ArbeitsverzeichnisErstellen, zielPfad);

        erlaubt.Should().BeFalse();
    }

    /// <summary>VerifiziereBerechtigung verbietet die Erstellung von Pull Requests durch Unteragenten unabhängig vom Zielpfad.</summary>
    [Fact]
    public void VerifiziereBerechtigung_VerbietetPullRequestErstellung()
    {
        var unteragent = ErstelleUnteragent();

        var erlaubt = _sut.VerifiziereBerechtigung(unteragent, UnteragentAktion.PullRequestErstellen, _agentDirectory);

        erlaubt.Should().BeFalse();
    }

    /// <summary>VerifiziereBerechtigung verbietet die Modifikation von Skills durch Unteragenten unabhängig vom Zielpfad.</summary>
    [Fact]
    public void VerifiziereBerechtigung_VerbietetSkillModifikation()
    {
        var unteragent = ErstelleUnteragent();

        var erlaubt = _sut.VerifiziereBerechtigung(unteragent, UnteragentAktion.SkillModifizieren, _agentDirectory);

        erlaubt.Should().BeFalse();
    }

    /// <summary>ValidiereFehlerBedingungAsync erkennt eine Tokenlimit-Verletzung anhand von task_state.json und wirft eine UnteragentAbbruchException.</summary>
    [Fact]
    public async Task ValidiereFehlerBedingungAsync_ErkenntTokenLimitVerletzung()
    {
        var unteragent = ErstelleUnteragent();
        var statePfad = Path.Combine(_agentDirectory, "task_state.json");
        await File.WriteAllTextAsync(statePfad, JsonSerializer.Serialize(new
        {
            tokens_used = 12000,
            token_limit = 10000,
            started_utc = DateTimeOffset.UtcNow,
            runtime_limit_minutes = 480
        }));

        var akt = () => _sut.ValidiereFehlerBedingungAsync(unteragent);

        await akt.Should().ThrowAsync<UnteragentAbbruchException>();
    }

    /// <summary>ValidiereFehlerBedingungAsync erkennt eine Laufzeitlimit-Verletzung anhand von task_state.json und wirft eine UnteragentAbbruchException.</summary>
    [Fact]
    public async Task ValidiereFehlerBedingungAsync_ErkenntLaufzeitLimitVerletzung()
    {
        var unteragent = ErstelleUnteragent();
        var statePfad = Path.Combine(_agentDirectory, "task_state.json");
        await File.WriteAllTextAsync(statePfad, JsonSerializer.Serialize(new
        {
            tokens_used = 100,
            token_limit = 10000,
            started_utc = DateTimeOffset.UtcNow.AddHours(-2),
            runtime_limit_minutes = 60
        }));

        var akt = () => _sut.ValidiereFehlerBedingungAsync(unteragent);

        await akt.Should().ThrowAsync<UnteragentAbbruchException>();
    }

    /// <summary>ValidiereFehlerBedingungAsync wirft keine Ausnahme, wenn keine Abbruchbedingung vorliegt.</summary>
    [Fact]
    public async Task ValidiereFehlerBedingungAsync_WirftKeineAusnahme_OhneAbbruchbedingung()
    {
        var unteragent = ErstelleUnteragent();
        var statePfad = Path.Combine(_agentDirectory, "task_state.json");
        await File.WriteAllTextAsync(statePfad, JsonSerializer.Serialize(new
        {
            tokens_used = 100,
            token_limit = 10000,
            started_utc = DateTimeOffset.UtcNow,
            runtime_limit_minutes = 480
        }));

        var akt = () => _sut.ValidiereFehlerBedingungAsync(unteragent);

        await akt.Should().NotThrowAsync();
    }

    /// <summary>ValidiereFehlerBedingungAsync wirft keine Ausnahme, wenn task_state.json (noch) nicht existiert.</summary>
    [Fact]
    public async Task ValidiereFehlerBedingungAsync_WirftKeineAusnahme_OhneTaskStateJson()
    {
        var unteragent = ErstelleUnteragent();

        var akt = () => _sut.ValidiereFehlerBedingungAsync(unteragent);

        await akt.Should().NotThrowAsync();
    }

    /// <summary>VerifiziereBerechtigung wirft eine ArgumentNullException, wenn kein Unteragent übergeben wird.</summary>
    [Fact]
    public void VerifiziereBerechtigung_WirftBeiNullUnteragent()
    {
        var akt = () => _sut.VerifiziereBerechtigung(null!, UnteragentAktion.ArbeitsverzeichnisErstellen, _agentDirectory);

        akt.Should().Throw<ArgumentNullException>();
    }

    /// <summary>VerifiziereBerechtigung wirft eine ArgumentException, wenn der Zielpfad leer ist.</summary>
    [Fact]
    public void VerifiziereBerechtigung_WirftBeiLeeremZielPfad()
    {
        var unteragent = ErstelleUnteragent();

        var akt = () => _sut.VerifiziereBerechtigung(unteragent, UnteragentAktion.ArbeitsverzeichnisErstellen, string.Empty);

        akt.Should().Throw<ArgumentException>();
    }
}
