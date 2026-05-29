using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;
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

    private readonly Dictionary<string, Guid> _repoTaskIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _startedTaskIds = [];
    private readonly object _sessionLock = new();
    private const int PromptInlineLimitBytes = 8 * 1024;
    private const string SessionNotFoundMarker = "session not found";

    /// <inheritdoc/>
    public override string PluginName => "Claude CLI";
    /// <inheritdoc/>
    public override string ProviderDateiPraefix => "claude";
    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.ClaudeCli";
    /// <inheritdoc/>
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

        var agents = DiscoverAgents(agentPackagePath, Path.Combine(".claude", "commands"));

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

        var compatible = Directory.Exists(Path.Combine(agentPackagePath, ".claude", "commands"));

        if (!compatible)
        {
            _logger.LogWarning(
                "Agentenpaket {PackagePath} ist nicht kompatibel mit Claude CLI: Kein '.claude/commands'-Ordner gefunden.",
                agentPackagePath);
        }

        return Task.FromResult(compatible);
    }

    public override async Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Deploye Agentenpaket {PackagePath} nach {RepoPath}.", agentPackagePath, localRepoPath);

        var claudeSourceDir = Path.Combine(agentPackagePath, ".claude");
        if (!Directory.Exists(claudeSourceDir))
        {
            _logger.LogWarning(
                "Kein '.claude'-Ordner im Agentenpaket {PackagePath} gefunden. Deploy wird ubersprungen.",
                agentPackagePath);
            return;
        }

        var claudeTargetDir = Path.Combine(localRepoPath, ".claude");

        await Task.Run(() =>
        {
            foreach (var sourceFile in Directory.GetFiles(claudeSourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(claudeSourceDir, sourceFile);
                var targetFile = Path.Combine(claudeTargetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(sourceFile, targetFile, overwrite: true);
            }
        }, ct);

        _logger.LogInformation("Agentenpaket '.claude'-Ordner erfolgreich nach {TargetDir} deployed.", claudeTargetDir);
    }

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

        var env = GetClaudeEnvironment();
        var taskId = ResolveTaskId(localRepoPath);
        var isFollowUp = IsTaskStarted(taskId);
        var useStdIn = Encoding.UTF8.GetByteCount(prompt) > PromptInlineLimitBytes;
        var execution = CreateExecutionRequest(prompt, promptFile, agent, model, taskId, isFollowUp, useStdIn);

        _logger.LogInformation(
            "Rufe claude CLI mit Agent {AgentName} auf. Aufgabe: {TaskId}, FollowUp: {IsFollowUp}.",
            agent.Name,
            taskId,
            isFollowUp);
        _logger.LogDebug("{Command} {Args}", execution.Command, BuildDebugArgs(execution));

        if (isFollowUp)
        {
            var followUpLines = new List<string>();
            var sessionNotFound = false;
            await foreach (var line in _cliRunner.StreamAsync(execution.Command, execution.Args, localRepoPath, env, ct))
            {
                followUpLines.Add(line);
                sessionNotFound |= line.Contains(SessionNotFoundMarker, StringComparison.OrdinalIgnoreCase);
            }

            if (sessionNotFound)
            {
                _logger.LogWarning("Claude-Session {TaskId} nicht gefunden. Starte neuen Erstlauf.", taskId);
                MarkTaskAsNotStarted(taskId);
                var fallbackExecution = CreateExecutionRequest(prompt, promptFile, agent, model, taskId, isFollowUp: false, useStdIn);
                _logger.LogDebug("{Command} {Args}", fallbackExecution.Command, BuildDebugArgs(fallbackExecution));

                await foreach (var line in _cliRunner.StreamAsync(fallbackExecution.Command, fallbackExecution.Args, localRepoPath, env, ct))
                {
                    yield return line;
                }

                MarkTaskAsStarted(taskId);
                yield break;
            }

            foreach (var line in followUpLines)
            {
                yield return line;
            }

            yield break;
        }

        await foreach (var line in _cliRunner.StreamAsync(execution.Command, execution.Args, localRepoPath, env, ct))
        {
            yield return line;
        }

        MarkTaskAsStarted(taskId);
    }

    private CliExecutionRequest CreateExecutionRequest(
        string prompt,
        string promptFilePath,
        AgentInfo agent,
        string? model,
        Guid taskId,
        bool isFollowUp,
        bool useStdIn)
    {
        var baseArgs = BuildClaudeArgs(prompt, agent, model, taskId, isFollowUp, includePromptArgument: !useStdIn).ToList();
        if (!useStdIn)
        {
            return new CliExecutionRequest("claude", baseArgs, ContainsInlinePrompt: true);
        }

        if (OperatingSystem.IsWindows())
        {
            var escapedPromptPath = EscapePowerShellString(promptFilePath);
            var escapedArgs = baseArgs.Select(arg => $"'{EscapePowerShellString(arg)}'").ToList();
            var script = $"Get-Content -Raw -LiteralPath '{escapedPromptPath}' | claude {string.Join(" ", escapedArgs)}";
            return new CliExecutionRequest("powershell", ["-NoProfile", "-NonInteractive", "-Command", script], ContainsInlinePrompt: false);
        }

        var escapedPromptPathUnix = EscapeBashString(promptFilePath);
        var escapedUnixArgs = baseArgs.Select(arg => $"'{EscapeBashString(arg)}'").ToList();
        var unixScript = $"cat '{escapedPromptPathUnix}' | claude {string.Join(" ", escapedUnixArgs)}";
        return new CliExecutionRequest("sh", ["-c", unixScript], ContainsInlinePrompt: false);
    }

    private static IEnumerable<string> BuildClaudeArgs(
        string prompt,
        AgentInfo agent,
        string? model,
        Guid taskId,
        bool isFollowUp,
        bool includePromptArgument)
    {
        var args = new List<string>();
        if (isFollowUp)
        {
            args.AddRange(["-r", taskId.ToString(), "-p"]);
        }
        else
        {
            args.AddRange(["-p", "-n", taskId.ToString()]);
        }

        args.Add("--dangerously-skip-permissions");
        args.AddRange(["--output-format", "stream-json"]);

        if (!string.IsNullOrWhiteSpace(agent.Name))
        {
            args.AddRange(["--agent", agent.Name]);
        }

        args.AddRange(["--model", NormalizeModel(model)]);

        if (includePromptArgument)
        {
            args.Add(prompt);
        }

        return args;
    }

    private static string NormalizeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model) || string.Equals(model, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return "sonnet";
        }

        return model;
    }

    private static string BuildDebugArgs(CliExecutionRequest execution)
    {
        if (!execution.ContainsInlinePrompt || execution.Args.Count == 0)
        {
            return string.Join(" ", execution.Args);
        }

        var args = execution.Args.ToList();
        args[^1] = "<prompt-redacted>";
        return string.Join(" ", args);
    }

    private Guid ResolveTaskId(string localRepoPath)
    {
        var contextFiles = Directory.GetFiles(localRepoPath, BuildContextFileMask());
        foreach (var contextFile in contextFiles.OrderByDescending(File.GetLastWriteTimeUtc))
        {
            var fileName = Path.GetFileName(contextFile);
            var suffix = $".{ProviderDateiPraefix}.context.md";
            if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var idCandidate = fileName[..^suffix.Length];
            if (Guid.TryParse(idCandidate, out var taskId))
            {
                return taskId;
            }
        }

        var key = Path.GetFullPath(localRepoPath);
        lock (_sessionLock)
        {
            if (_repoTaskIds.TryGetValue(key, out var existingTaskId))
            {
                return existingTaskId;
            }

            var createdTaskId = Guid.NewGuid();
            _repoTaskIds[key] = createdTaskId;
            return createdTaskId;
        }
    }

    private bool IsTaskStarted(Guid taskId)
    {
        lock (_sessionLock)
        {
            return _startedTaskIds.Contains(taskId);
        }
    }

    private void MarkTaskAsStarted(Guid taskId)
    {
        lock (_sessionLock)
        {
            _startedTaskIds.Add(taskId);
        }
    }

    private void MarkTaskAsNotStarted(Guid taskId)
    {
        lock (_sessionLock)
        {
            _startedTaskIds.Remove(taskId);
        }
    }

    private static string EscapePowerShellString(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static string EscapeBashString(string value) => value.Replace("'", "'\"'\"'", StringComparison.Ordinal);

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

    private sealed record CliExecutionRequest(string Command, IReadOnlyList<string> Args, bool ContainsInlinePrompt);
}
