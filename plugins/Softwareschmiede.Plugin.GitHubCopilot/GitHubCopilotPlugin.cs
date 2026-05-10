using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>
/// GitHub Copilot Plugin – nutzt das <c>copilot</c>-CLI für KI-gestützte Entwicklung.
/// Der Prozess läuft im Repository-Verzeichnis, sodass der Agent Dateien direkt anlegen und ändern kann.
/// Die Agent-Datei wird als Kontext-Präambel an den Prompt angehängt.
/// </summary>
public sealed class GitHubCopilotPlugin : IKiPlugin
{
    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<GitHubCopilotPlugin> _logger;

    /// <inheritdoc/>
    public string PluginName => "GitHub Copilot";

    /// <inheritdoc/>
    public string PluginPrefix => "Softwareschmiede.GitHubCopilot";

    /// <inheritdoc/>
    public PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <inheritdoc/>
    public IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Authentifizierung",
        [
            new PluginSettingField(
                Key: "Token",
                Label: "GitHub Token",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "ghp_...",
                Description: "GitHub Personal Access Token. Wird als GH_TOKEN-Umgebungsvariable an das copilot-CLI übergeben.",
                IsRequired: false)
        ])
    ];

    /// <summary>Erstellt eine neue Instanz des <see cref="GitHubCopilotPlugin"/>.</summary>
    public GitHubCopilotPlugin(
        ICliRunner cliRunner,
        ICredentialStore credentialStore,
        ILogger<GitHubCopilotPlugin> logger)
    {
        _cliRunner = cliRunner;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    private IDictionary<string, string> GetGhEnvironment()
    {
        var token = _credentialStore.GetCredential("Softwareschmiede.GitHub.Token");
        var env = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
        {
            env["GH_TOKEN"] = token;
        }
        return env;
    }

    /// <inheritdoc/>
    public Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default)
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

    /// <inheritdoc/>
    public Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default)
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
                "Agentenpaket {PackagePath} ist nicht kompatibel mit GitHub Copilot: Kein '.github'-Ordner gefunden.",
                agentPackagePath);
        }

        return Task.FromResult(compatible);
    }

    /// <inheritdoc/>
    public async Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default)
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

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StartDevelopmentAsync(
        string prompt,
        AgentInfo agent,
        string localRepoPath,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starte KI-Entwicklung mit Agent {AgentName} in {RepoPath}.", agent.Name, localRepoPath);

        var promptFile = Path.Combine(localRepoPath, $"{Guid.NewGuid()}.copilot-task.md");
        await File.WriteAllTextAsync(promptFile, prompt, ct);

        var args = BuildCopilotArgs(promptFile, agent, model);
        var env = GetGhEnvironment();

        _logger.LogInformation("Rufe copilot CLI mit Agent {AgentName} auf.", agent.Name);

        await foreach (var line in _cliRunner.StreamAsync("copilot", args, localRepoPath, env, ct))
        {
            yield return line;
        }
    }

    /// <summary>
    /// Baut die Argument-Liste für den <c>copilot</c>-CLI-Aufruf zusammen.
    /// <para>
    /// Der Prompt wird als Dateireferenz übergeben (<c>@pfad</c>), damit Zeilenumbrüche
    /// und beliebige Länge kein Problem für den Konsolenaufruf darstellen.
    /// Für den nicht-interaktiven Skript-Modus sind folgende Flags erforderlich:
    /// <list type="bullet">
    ///   <item><c>--allow-all-tools</c> – alle Tool-Aufrufe ohne Bestätigung (Pflicht im Skript-Modus, sonst Exit-Code 1)</item>
    ///   <item><c>--allow-all-paths</c> – Dateizugriff auf beliebige Pfade</item>
    ///   <item><c>--no-ask-user</c> – deaktiviert Rückfragen, Agent arbeitet autonom</item>
    ///   <item><c>--silent</c> – unterdrückt Statistik-Ausgaben, liefert nur die Agenten-Antwort</item>
    /// </list>
    /// </para>
    /// </summary>
    private static IEnumerable<string> BuildCopilotArgs(string promptFilePath, AgentInfo agent, string? model)
    {
        // @<pfad> lässt copilot den Prompt aus der Datei lesen
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

        // Kein --model-Flag → GitHub wählt automatisch das passende Modell
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

    /// <inheritdoc/>
    public async Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Fuhre Tests in {RepoPath} aus.", localRepoPath);

        var result = await _cliRunner.RunAsync(
            "dotnet",
            ["test", "--verbosity", "normal", "--logger", "trx"],
            localRepoPath,
            null,
            ct);

        var ergebnisse = ParseTestOutput(result.StdOut + result.StdErr);
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

    /// <inheritdoc/>
    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Prufe GitHub-Copilot-Plugin-Health.");
        var result = await _cliRunner.RunAsync("copilot", ["--version"], null, null, ct);
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
