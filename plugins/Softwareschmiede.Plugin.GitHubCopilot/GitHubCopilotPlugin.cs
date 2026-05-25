using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>
/// GitHub Copilot Plugin – nutzt das <c>copilot</c>-CLI für KI-gestützte Entwicklung.
/// Der Prozess läuft im Repository-Verzeichnis, sodass der Agent Dateien direkt anlegen und ändern kann.
/// Die Agent-Datei wird als Kontext-Präambel an den Prompt angehängt.
/// </summary>
public sealed class GitHubCopilotPlugin : CliKiPluginBase
{
    private const string ExecutablePathSettingKey = "ExecutablePath";

    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<GitHubCopilotPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "GitHub Copilot";

    /// <inheritdoc/>
    public override string ProviderDateiPraefix => "copilot";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.GitHubCopilot";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
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
        ]),
        new PluginSettingGroup("Ausführung",
        [
            new PluginSettingField(
                Key: ExecutablePathSettingKey,
                Label: "Copilot CLI Pfad",
                FieldType: PluginSettingFieldType.Text,
                Placeholder: "C:\\Program Files\\GitHub Copilot\\copilot.exe",
                Description: "Optionaler absoluter Pfad zur copilot-Executable. Erforderlich für IIS, wenn der Application-Pool die PATH-Variable nicht enthält.",
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

    private IDictionary<string, string> GetGhEnvironment(string localRepoPath)
    {
        var token = _credentialStore.GetCredential("Softwareschmiede.GitHub.Token");

        var runtimeRoot = Path.Combine(localRepoPath, ".softwareschmiede", "copilot-runtime");
        var userProfile = Path.Combine(runtimeRoot, "userprofile");
        var localAppData = Path.Combine(runtimeRoot, "localappdata");
        var appData = Path.Combine(runtimeRoot, "appdata");
        var temp = Path.Combine(runtimeRoot, "temp");

        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(userProfile);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(temp);

        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = userProfile,
            ["HOME"] = userProfile,
            ["LOCALAPPDATA"] = localAppData,
            ["APPDATA"] = appData,
            ["TEMP"] = temp,
            ["TMP"] = temp,
        };

        if (!string.IsNullOrEmpty(token))
        {
            env["GH_TOKEN"] = token;
        }

        return env;
    }

    private string GetCopilotCommand()
    {
        var configuredPath = _credentialStore.GetCredential($"{PluginPrefix}.{ExecutablePathSettingKey}");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Trim().Trim('"');
        }

        return "copilot";
    }

    /// <inheritdoc/>
    public override Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Lese Agenten aus Paket {PackagePath}.", agentPackagePath);

        var agents = DiscoverAgents(agentPackagePath, Path.Combine(".github", "agents"));

        _logger.LogInformation("{Count} Agenten im Paket gefunden.", agents.Count);
        return Task.FromResult<IEnumerable<AgentInfo>>(agents);
    }

    /// <inheritdoc/>
    public override Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Prufe Kompatibilitat des Agentenpakets {PackagePath}.", agentPackagePath);

        if (!Directory.Exists(agentPackagePath))
        {
            _logger.LogWarning("Agentenpaket-Verzeichnis {Path} nicht gefunden.", agentPackagePath);
            return Task.FromResult(false);
        }

        var compatible = Directory.Exists(Path.Combine(agentPackagePath, ".github", "agents"));

        if (!compatible)
        {
            _logger.LogWarning(
                "Agentenpaket {PackagePath} ist nicht kompatibel mit GitHub Copilot: Kein '.github/agents'-Ordner gefunden.",
                agentPackagePath);
        }

        return Task.FromResult(compatible);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override async IAsyncEnumerable<string> StartDevelopmentAsync(
        string prompt,
        AgentInfo agent,
        string localRepoPath,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starte KI-Entwicklung mit Agent {AgentName} in {RepoPath}.", agent.Name, localRepoPath);
        
        EnsureGitignoreEntries(localRepoPath);

        var promptFile = BuildTaskFilePath(localRepoPath, Guid.NewGuid());
        await File.WriteAllTextAsync(promptFile, prompt, ct);

        var args = BuildCopilotArgs(promptFile, agent, model);
        var env = GetGhEnvironment(localRepoPath);
        var copilotCommand = GetCopilotCommand();

        _logger.LogInformation("Rufe copilot CLI mit Agent {AgentName} auf.", agent.Name);

        var stream = _cliRunner.StreamAsync(copilotCommand, args, localRepoPath, env, ct);
        await using var enumerator = stream.GetAsyncEnumerator(ct);

        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "Copilot CLI wurde nicht gefunden. Bitte in den Plugin-Einstellungen 'Copilot CLI Pfad' als absoluten Pfad setzen (z.B. C:\\Program Files\\GitHub Copilot\\copilot.exe).",
                    ex);
            }

            if (!hasNext)
            {
                break;
            }

            yield return enumerator.Current;
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
            "--prompt", $"\"@{promptFilePath}\"",
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

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Prufe GitHub-Copilot-Plugin-Health.");
        try
        {
            var result = await _cliRunner.RunAsync(GetCopilotCommand(), ["--version"], null, null, ct);
            if (result is null)
            {
                _logger.LogWarning("Copilot-Healthcheck lieferte kein Ergebnis vom CLI-Runner.");
                return false;
            }

            return result.IsSuccess;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
