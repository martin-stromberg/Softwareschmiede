using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
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
    private const string CopilotTaskFileSuffix = ".copilot-task.md";
    private const string GitIgnoreFileName = ".gitignore";
    private const string CopilotTaskGitIgnoreRule = "*.copilot-task.md";
    private const int GitIgnoreWriteRetryCount = 3;
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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
        string? model,
        string? executionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        string? promptFile = null;
        string normalizedExecutionId;

        try
        {
            normalizedExecutionId = NormalizeAndValidateExecutionId(executionId);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(
                ex,
                "KI-Entwicklungsschritt fehlgeschlagen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, DurationMs: {DurationMs}.",
                "validate-id",
                "failed",
                executionId,
                totalStopwatch.ElapsedMilliseconds);
            throw;
        }

        _logger.LogInformation(
            "Starte KI-Entwicklung mit Agent {AgentName} in {RepoPath}. ExecutionId: {ExecutionId}.",
            agent.Name,
            localRepoPath,
            normalizedExecutionId);

        if (!Directory.Exists(localRepoPath))
        {
            throw new DirectoryNotFoundException($"Repository-Verzeichnis nicht gefunden: {localRepoPath}");
        }

        try
        {
            var validateStepDuration = totalStopwatch.ElapsedMilliseconds;
            _logger.LogInformation(
                "KI-Entwicklungsschritt abgeschlossen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, DurationMs: {DurationMs}.",
                "validate-id",
                "success",
                normalizedExecutionId,
                validateStepDuration);

            promptFile = Path.Combine(localRepoPath, $"{normalizedExecutionId}{CopilotTaskFileSuffix}");
            try
            {
                await File.WriteAllTextAsync(promptFile, prompt, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "KI-Entwicklungsschritt fehlgeschlagen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, TaskFilePath: {TaskFilePath}, DurationMs: {DurationMs}.",
                    "write-task-file",
                    "failed",
                    normalizedExecutionId,
                    promptFile,
                    totalStopwatch.ElapsedMilliseconds);
                throw;
            }

            _logger.LogInformation(
                "KI-Entwicklungsschritt abgeschlossen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, TaskFilePath: {TaskFilePath}, DurationMs: {DurationMs}.",
                "write-task-file",
                "success",
                normalizedExecutionId,
                promptFile,
                totalStopwatch.ElapsedMilliseconds);

            var gitignorePath = Path.Combine(localRepoPath, GitIgnoreFileName);
            bool ignoreRuleChanged;
            try
            {
                ignoreRuleChanged = await EnsureGitIgnoreRuleAsync(gitignorePath, CopilotTaskGitIgnoreRule, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "KI-Entwicklungsschritt fehlgeschlagen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, Rule: {Rule}, DurationMs: {DurationMs}.",
                    "sync-gitignore",
                    "failed",
                    normalizedExecutionId,
                    CopilotTaskGitIgnoreRule,
                    totalStopwatch.ElapsedMilliseconds);
                throw;
            }

            _logger.LogInformation(
                "KI-Entwicklungsschritt abgeschlossen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, Rule: {Rule}, Status: {Status}, DurationMs: {DurationMs}.",
                "sync-gitignore",
                "success",
                normalizedExecutionId,
                CopilotTaskGitIgnoreRule,
                ignoreRuleChanged ? "updated" : "already-synced",
                totalStopwatch.ElapsedMilliseconds);

            var args = BuildCopilotArgs(promptFile, agent, model);
            var env = GetGhEnvironment();
            _logger.LogInformation(
                "KI-Entwicklungsschritt gestartet. Step: {Step}, ExecutionId: {ExecutionId}, AgentName: {AgentName}, TaskFilePath: {TaskFilePath}, DurationMs: {DurationMs}.",
                "invoke-cli",
                normalizedExecutionId,
                agent.Name,
                promptFile,
                totalStopwatch.ElapsedMilliseconds);

            await using var cliEnumerator = _cliRunner.StreamAsync("copilot", args, localRepoPath, env, ct)
                .GetAsyncEnumerator(ct);
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await cliEnumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "KI-Entwicklungsschritt fehlgeschlagen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, DurationMs: {DurationMs}.",
                        "invoke-cli",
                        "failed",
                        normalizedExecutionId,
                        totalStopwatch.ElapsedMilliseconds);
                    throw;
                }

                if (!hasNext)
                {
                    break;
                }

                yield return cliEnumerator.Current;
            }

            _logger.LogInformation(
                "KI-Entwicklungsschritt abgeschlossen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, DurationMs: {DurationMs}.",
                "invoke-cli",
                "success",
                normalizedExecutionId,
                totalStopwatch.ElapsedMilliseconds);
        }
        finally
        {
            await CleanupTaskFileAsync(promptFile, normalizedExecutionId, ct);
            _logger.LogInformation(
                "KI-Entwicklung abgeschlossen. ExecutionId: {ExecutionId}, DurationMs: {DurationMs}.",
                normalizedExecutionId,
                totalStopwatch.ElapsedMilliseconds);
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

    private static async Task<bool> EnsureGitIgnoreRuleAsync(
        string gitIgnorePath,
        string requiredRule,
        CancellationToken ct)
    {
        var normalizedRequiredRule = NormalizeGitIgnoreRule(requiredRule);

        for (var attempt = 1; attempt <= GitIgnoreWriteRetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(
                    gitIgnorePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                using var reader = new StreamReader(
                    stream,
                    Utf8NoBom,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true);
                var content = await reader.ReadToEndAsync(ct);
                var normalizedContent = content.Replace("\r\n", "\n");
                var lines = normalizedContent.Length == 0
                    ? []
                    : normalizedContent.Split('\n');

                var filteredLines = lines
                    .Select(line => line.TrimEnd('\r'))
                    .Where(line => !ShouldConsolidateCopilotTaskRule(line))
                    .ToList();

                var hasRule = filteredLines.Any(line => IsEquivalentGitIgnoreRule(line, normalizedRequiredRule));
                if (!hasRule)
                {
                    filteredLines.Add(requiredRule);
                }

                while (filteredLines.Count > 0 && string.IsNullOrEmpty(filteredLines[^1]))
                {
                    filteredLines.RemoveAt(filteredLines.Count - 1);
                }

                var updatedContent = string.Join('\n', filteredLines);
                if (updatedContent.Length > 0 && !updatedContent.EndsWith('\n'))
                {
                    updatedContent += '\n';
                }

                if (string.Equals(updatedContent, normalizedContent, StringComparison.Ordinal))
                {
                    return false;
                }

                stream.Position = 0;
                stream.SetLength(0);
                await using var writer = new StreamWriter(
                    stream,
                    Utf8NoBom,
                    bufferSize: 1024,
                    leaveOpen: true);
                await writer.WriteAsync(updatedContent.AsMemory(), ct);
                await writer.FlushAsync(ct);

                return true;
            }
            catch (IOException) when (attempt < GitIgnoreWriteRetryCount)
            {
                await Task.Delay(50 * attempt, ct);
            }
        }

        throw new IOException($"Konnte .gitignore nicht aktualisieren: {gitIgnorePath}");
    }

    private static bool IsEquivalentGitIgnoreRule(string line, string normalizedRequiredRule)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith('#'))
        {
            return false;
        }

        return string.Equals(
            NormalizeGitIgnoreRule(trimmed),
            normalizedRequiredRule,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeGitIgnoreRule(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        normalized = normalized.TrimStart('/');
        normalized = normalized.TrimStart('.');
        normalized = normalized.TrimStart('/');
        return normalized;
    }

    private static bool ShouldConsolidateCopilotTaskRule(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith('#'))
        {
            return false;
        }

        var normalized = NormalizeGitIgnoreRule(trimmed);
        return string.Equals(normalized, CopilotTaskGitIgnoreRule, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAndValidateExecutionId(string? executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId))
        {
            return Guid.NewGuid().ToString("N");
        }

        var trimmed = executionId.Trim();
        if (!Guid.TryParse(trimmed, out var guid))
        {
            throw new ArgumentException(
                $"Invalid executionId format. Expected a GUID in .NET format (N, D, B or P), received: '{executionId}'.",
                nameof(executionId));
        }

        return guid.ToString("N");
    }

    private async Task CleanupTaskFileAsync(string? taskFilePath, string executionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(taskFilePath))
        {
            _logger.LogInformation(
                "KI-Entwicklungsschritt abgeschlossen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, DurationMs: {DurationMs}.",
                "cleanup",
                "skipped",
                executionId,
                0);
            return;
        }

        var cleanupStopwatch = Stopwatch.StartNew();
        try
        {
            if (!File.Exists(taskFilePath))
            {
                _logger.LogInformation(
                    "KI-Entwicklungsschritt abgeschlossen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, TaskFilePath: {TaskFilePath}, DurationMs: {DurationMs}.",
                    "cleanup",
                    "already-absent",
                    executionId,
                    taskFilePath,
                    cleanupStopwatch.ElapsedMilliseconds);
                return;
            }

            await Task.Run(() => File.Delete(taskFilePath), ct);
            _logger.LogInformation(
                "KI-Entwicklungsschritt abgeschlossen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, TaskFilePath: {TaskFilePath}, DurationMs: {DurationMs}.",
                "cleanup",
                "success",
                executionId,
                taskFilePath,
                cleanupStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "KI-Entwicklungsschritt abgeschlossen. Step: {Step}, Result: {Result}, ExecutionId: {ExecutionId}, TaskFilePath: {TaskFilePath}, DurationMs: {DurationMs}. Cleanup-Fehler ist non-blocking.",
                "cleanup",
                "warning",
                executionId,
                taskFilePath,
                cleanupStopwatch.ElapsedMilliseconds);
        }
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
