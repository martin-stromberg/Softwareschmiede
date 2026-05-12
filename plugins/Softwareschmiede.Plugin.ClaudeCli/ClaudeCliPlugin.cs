using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Claude-CLI Plugin für KI-gestützte Entwicklung.</summary>
public sealed class ClaudeCliPlugin : CliKiPluginBase
{
    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<ClaudeCliPlugin> _logger;

    public override string PluginName => "Claude CLI";
    public override string ProviderDateiPraefix => "claude";
    public override string PluginPrefix => "Softwareschmiede.ClaudeCli";
    public override PluginType PluginType => PluginType.DevelopmentAutomation;

    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Authentifizierung",
        [
            new PluginSettingField(
                Key: "Token",
                Label: "Anthropic API Key",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "sk-ant-...",
                Description: "Anthropic API Key. Wird als ANTHROPIC_API_KEY-Umgebungsvariable an das claude-CLI übergeben.",
                IsRequired: false)
        ])
    ];

    public ClaudeCliPlugin(
        ICliRunner cliRunner,
        ICredentialStore credentialStore,
        ILogger<ClaudeCliPlugin> logger)
    {
        _cliRunner = cliRunner;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    private IDictionary<string, string> GetClaudeEnvironment()
    {
        var token = _credentialStore.GetCredential("Softwareschmiede.ClaudeCli.Token");
        var env = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
        {
            env["ANTHROPIC_API_KEY"] = token;
        }
        return env;
    }

    public override Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Lese Agenten aus Paket {PackagePath}.", agentPackagePath);

        if (!Directory.Exists(agentPackagePath))
        {
            _logger.LogWarning("Agentenpaket-Verzeichnis {Path} nicht gefunden.", agentPackagePath);
            return Task.FromResult<IEnumerable<AgentInfo>>([]);
        }

        var githubDir = Path.Combine(agentPackagePath, ".github");
        var searchRoot = Directory.Exists(githubDir) ? githubDir : agentPackagePath;

        var agents = Directory.GetFiles(searchRoot, "*.agent.md", SearchOption.AllDirectories)
            .Select(f => new AgentInfo(
                Name: Path.GetFileNameWithoutExtension(f).Replace(".agent", string.Empty, StringComparison.OrdinalIgnoreCase),
                Beschreibung: ReadAgentDescription(f),
                DateiPfad: f
            ))
            .ToList();

        _logger.LogInformation("{Count} Agenten im Paket gefunden.", agents.Count);
        return Task.FromResult<IEnumerable<AgentInfo>>(agents);
    }

    public override Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Prufe Kompatibilitat des Agentenpakets {PackagePath}.", agentPackagePath);

        if (!Directory.Exists(agentPackagePath))
        {
            _logger.LogWarning("Agentenpaket-Verzeichnis {Path} nicht gefunden.", agentPackagePath);
            return Task.FromResult(false);
        }

        var githubDir = Path.Combine(agentPackagePath, ".github");
        var compatible = Directory.Exists(githubDir);

        if (!compatible)
        {
            _logger.LogWarning(
                "Agentenpaket {PackagePath} ist nicht kompatibel mit Claude CLI: Kein '.github'-Ordner gefunden.",
                agentPackagePath);
        }

        return Task.FromResult(compatible);
    }

    public override async Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Deploye Agentenpaket {PackagePath} nach {RepoPath}.", agentPackagePath, localRepoPath);

        var githubSourceDir = Path.Combine(agentPackagePath, ".github");
        if (!Directory.Exists(githubSourceDir))
        {
            _logger.LogWarning(
                "Kein '.github'-Ordner im Agentenpaket {PackagePath} gefunden. Deploy wird ubersprungen.",
                agentPackagePath);
            return;
        }

        var githubTargetDir = Path.Combine(localRepoPath, ".github");

        await Task.Run(() =>
        {
            foreach (var sourceFile in Directory.GetFiles(githubSourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(githubSourceDir, sourceFile);
                var targetFile = Path.Combine(githubTargetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(sourceFile, targetFile, overwrite: true);
            }
        }, ct);

        _logger.LogInformation("Agentenpaket '.github'-Ordner erfolgreich nach {TargetDir} deployed.", githubTargetDir);
    }

    public override async IAsyncEnumerable<string> StartDevelopmentAsync(
        string prompt,
        AgentInfo agent,
        string localRepoPath,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starte KI-Entwicklung mit Agent {AgentName} in {RepoPath}.", agent.Name, localRepoPath);

        var promptFile = BuildTaskFilePath(localRepoPath, Guid.NewGuid());
        await File.WriteAllTextAsync(promptFile, prompt, ct);

        var args = BuildClaudeArgs(promptFile, agent, model);
        var env = GetClaudeEnvironment();

        _logger.LogInformation("Rufe claude CLI mit Agent {AgentName} auf.", agent.Name);

        await foreach (var line in _cliRunner.StreamAsync("claude", args, localRepoPath, env, ct))
        {
            yield return line;
        }
    }

    private static IEnumerable<string> BuildClaudeArgs(string promptFilePath, AgentInfo agent, string? model)
    {
        var args = new List<string>
        {
            "--prompt", $"@{promptFilePath}",
            "--allow-all-tools",
            "--allow-all-paths",
            "--no-ask-user",
            "--silent",
        };

        if (!string.IsNullOrWhiteSpace(agent.Name))
        {
            args.AddRange(["--agent", agent.Name]);
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            args.AddRange(["--model", model]);
        }
        else
        {
            args.AddRange(["--model", "auto"]);
        }

        return args;
    }

    public override async Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Fuhre Tests in {RepoPath} aus.", localRepoPath);

        var result = await _cliRunner.RunAsync(
            "dotnet",
            ["test", "--verbosity", "normal", "--logger", "trx"],
            localRepoPath,
            null,
            ct);

        var output = string.IsNullOrEmpty(result.StdOut) || string.IsNullOrEmpty(result.StdErr)
            ? result.StdOut + result.StdErr
            : $"{result.StdOut}{Environment.NewLine}{result.StdErr}";
        var ergebnisse = ParseTestOutput(output);
        var bestanden = result.IsSuccess;

        _logger.LogInformation(
            "Tests abgeschlossen. Bestanden: {Bestanden}, Anzahl Ergebnisse: {Count}.",
            bestanden, ergebnisse.Count);

        return new TestResult(bestanden, ergebnisse);
    }

    private static List<TestErgebnisInfo> ParseTestOutput(string output)
    {
        var results = new List<TestErgebnisInfo>();
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Passed", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new TestErgebnisInfo(trimmed, TestStatus.Bestanden, null, TimeSpan.Zero));
            }
            else if (trimmed.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new TestErgebnisInfo(trimmed, TestStatus.Fehlgeschlagen, trimmed, TimeSpan.Zero));
            }
            else if (trimmed.StartsWith("Skipped", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new TestErgebnisInfo(trimmed, TestStatus.Uebersprungen, null, TimeSpan.Zero));
            }
        }
        return results;
    }

    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Prufe Claude-CLI-Plugin-Health.");
        var result = await _cliRunner.RunAsync("claude", ["--version"], null, null, ct);
        return result.IsSuccess;
    }

    private static string? ReadAgentDescription(string agentFilePath)
    {
        try
        {
            var lines = File.ReadLines(agentFilePath).Take(10).ToList();
            var descLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("description:", StringComparison.OrdinalIgnoreCase));
            if (descLine is not null)
            {
                return descLine.Split(':', 2).ElementAtOrDefault(1)?.Trim().Trim('"');
            }
            return lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("---", StringComparison.Ordinal));
        }
        catch { return null; }
    }
}
