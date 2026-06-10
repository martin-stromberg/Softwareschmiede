using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private const string ConversationNotFoundMarker = "no conversation found with session id";
    public const string RateLimitSuggestionMarker = "[[SOFTWARESCHMIEDE_RATE_LIMIT]]";
    private static readonly Regex RateLimitResetTextRegex = new(@"resets\s+(?<time>[^\(\r\n]+)\s*\((?<timezone>[^\)]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        var (instruction, includeContext) = CliKiPluginBase.UnwrapPromptContextMarker(prompt);
        var promptFile = BuildTaskFilePath(localRepoPath, Guid.NewGuid());
        await File.WriteAllTextAsync(promptFile, instruction, ct);

        var cliPrompt = BuildCliPrompt(localRepoPath, promptFile, includeContext);

        var env = GetClaudeEnvironment();
        var taskId = ResolveTaskId(localRepoPath);
        var isFollowUp = IsTaskStarted(taskId);
        var useStdIn = Encoding.UTF8.GetByteCount(cliPrompt) > PromptInlineLimitBytes;
        var execution = CreateExecutionRequest(cliPrompt, agent, model, taskId, isFollowUp, useStdIn);

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
                sessionNotFound |= IsSessionNotFoundLine(line);
                yield return NormalizeOutputLine(line);
            }

            if (sessionNotFound)
            {
                _logger.LogWarning("Claude-Session {TaskId} nicht gefunden. Starte neuen Erstlauf.", taskId);
                MarkTaskAsNotStarted(taskId);
                var fallbackExecution = CreateExecutionRequest(cliPrompt, agent, model, taskId, isFollowUp: false, useStdIn);
                _logger.LogDebug("{Command} {Args}", fallbackExecution.Command, BuildDebugArgs(fallbackExecution));

                var fallbackLines = new List<string>();
                await foreach (var line in _cliRunner.StreamAsync(fallbackExecution.Command, fallbackExecution.Args, localRepoPath, env, ct))
                {
                    fallbackLines.Add(line);
                    yield return NormalizeOutputLine(line);
                }

                var fallbackSessionId = ResolveSessionIdFromOutput(fallbackLines, taskId);
                MarkTaskAsStarted(localRepoPath, fallbackSessionId, previousTaskId: taskId);
                yield break;
            }

            var followUpSessionId = ResolveSessionIdFromOutput(followUpLines, taskId);
            MarkTaskAsStarted(localRepoPath, followUpSessionId, previousTaskId: taskId);
            yield break;
        }

        var firstRunLines = new List<string>();
        await foreach (var line in _cliRunner.StreamAsync(execution.Command, execution.Args, localRepoPath, env, ct))
        {
            firstRunLines.Add(line);
            yield return NormalizeOutputLine(line);
        }

        var firstRunSessionId = ResolveSessionIdFromOutput(firstRunLines, taskId);
        MarkTaskAsStarted(localRepoPath, firstRunSessionId);
    }

    private CliExecutionRequest CreateExecutionRequest(
        string cliPrompt,
        AgentInfo agent,
        string? model,
        Guid taskId,
        bool isFollowUp,
        bool useStdIn)
    {
        var baseArgs = BuildClaudeArgs(cliPrompt, agent, model, taskId, isFollowUp, includePromptArgument: !useStdIn).ToList();
        if (!useStdIn)
        {
            return new CliExecutionRequest("claude", baseArgs, ContainsInlinePrompt: true);
        }

        if (OperatingSystem.IsWindows())
        {
            var escapedPrompt = EscapePowerShellString(cliPrompt);
            var escapedArgs = baseArgs.Select(arg => $"'{EscapePowerShellString(arg)}'").ToList();
            var script = $"$prompt = '{escapedPrompt}'; echo $prompt | claude {string.Join(" ", escapedArgs)}";
            return new CliExecutionRequest("powershell", ["-NoProfile", "-NonInteractive", "-Command", script], ContainsInlinePrompt: false);
        }

        var escapedPromptUnix = EscapeBashString(cliPrompt);
        var escapedUnixArgs = baseArgs.Select(arg => $"'{EscapeBashString(arg)}'").ToList();
        var unixScript = $"printf '%s' '{escapedPromptUnix}' | claude {string.Join(" ", escapedUnixArgs)}";
        return new CliExecutionRequest("sh", ["-c", unixScript], ContainsInlinePrompt: false);
    }

    private static IEnumerable<string> BuildClaudeArgs(
        string cliPrompt,
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
        args.Add("--verbose");
        args.AddRange(["--output-format", "stream-json"]);

        if (!string.IsNullOrWhiteSpace(agent.Name))
        {
            args.AddRange(["--agent", agent.Name]);
        }

        args.AddRange(["--model", NormalizeModel(model)]);

        if (includePromptArgument)
        {
            args.Add($"\"{cliPrompt}\"");
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

    private void MarkTaskAsStarted(string localRepoPath, Guid taskId, Guid? previousTaskId = null)
    {
        lock (_sessionLock)
        {
            if (previousTaskId is Guid oldTaskId && oldTaskId != taskId)
            {
                _startedTaskIds.Remove(oldTaskId);
            }

            _startedTaskIds.Add(taskId);
            _repoTaskIds[Path.GetFullPath(localRepoPath)] = taskId;
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

    private static bool IsSessionNotFoundLine(string line)
        => line.Contains(SessionNotFoundMarker, StringComparison.OrdinalIgnoreCase)
            || line.Contains(ConversationNotFoundMarker, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOutputLine(string line)
    {
        try
        {
            if (!TryExtractJsonRoot(line, out var root))
            {
                return line;
            }

            var type = TryGetString(root, "type");
            if (string.IsNullOrWhiteSpace(type))
            {
                return line;
            }

            if (string.Equals(type, "system", StringComparison.OrdinalIgnoreCase))
            {
                var subtype = TryGetString(root, "subtype");

                if (string.Equals(subtype, "init", StringComparison.OrdinalIgnoreCase))
                {
                    var model = TryGetString(root, "model") ?? "unbekannt";
                    var sessionId = TryGetString(root, "session_id") ?? "unbekannt";
                    var cwd = TryGetString(root, "cwd") ?? "unbekannt";
                    return $"[Claude] Initialisiert (Model={model}, Session={sessionId}, Cwd={cwd})";
                }

                if (string.Equals(subtype, "task_started", StringComparison.OrdinalIgnoreCase))
                {
                    var taskId = TryGetString(root, "task_id") ?? "unknown";
                    var description = TryGetString(root, "description") ?? "";
                    var taskType = TryGetString(root, "task_type") ?? "unknown";
                    description = WrapDescriptionAt80Chars(description);
                    return $"[Claude] Task gestartet ({taskId}, {taskType}): {description}";
                }

                if (string.Equals(subtype, "task_progress", StringComparison.OrdinalIgnoreCase))
                {
                    var taskId = TryGetString(root, "task_id") ?? "unknown";
                    var toolName = TryGetString(root, "last_tool_name") ?? "unknown";
                    var description = TryGetString(root, "description") ?? "";
                    description = WrapDescriptionAt80Chars(description);
                    var durationMs = TryGetInt(root, "duration_ms") ?? 0;
                    return $"[Claude] Task {taskId} läuft... (Tool: {toolName}, Dauer: {durationMs}ms) {description}";
                }

                if (string.Equals(subtype, "task_notification", StringComparison.OrdinalIgnoreCase))
                {
                    var taskId = TryGetString(root, "task_id") ?? "unknown";
                    var status = TryGetString(root, "status") ?? "unknown";
                    var summary = TryGetString(root, "summary") ?? "";
                    summary = WrapDescriptionAt80Chars(summary);
                    return $"[Claude] Task {taskId} {status}: {summary}";
                }
            }

            if (string.Equals(type, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                var message = root.TryGetProperty("message", out var messageNode) ? ExtractAssistantMessage(messageNode) : null;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return $"[Claude] {message}";
                }

                return "[Claude] Assistant-Ereignis empfangen.";
            }

            if (string.Equals(type, "user", StringComparison.OrdinalIgnoreCase))
            {
                // Versuche zuerst tool_use_result zu finden
                if (root.TryGetProperty("tool_use_result", out var toolUseResultNode))
                {
                    try
                    {
                        var commandName = TryGetString(toolUseResultNode, "commandName") ?? "unknown";
                        var success = TryGetBool(toolUseResultNode, "success") ?? false;
                        return $"[Claude] Tool '{commandName}' {(success ? "erfolgreich" : "fehlgeschlagen")}.";
                    }
                    catch (Exception)
                    {
                        // Falls tool_use_result nicht wie erwartet strukturiert ist
                    }
                }

                // Versuche auch in der message Property nach tool_use_result zu suchen (nested)
                if (root.TryGetProperty("message", out var messageNode) && messageNode.ValueKind == JsonValueKind.Object)
                {
                    try
                    {
                        if (messageNode.TryGetProperty("content", out var contentNode) && contentNode.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var contentEntry in contentNode.EnumerateArray())
                            {
                                if (contentEntry.TryGetProperty("type", out var typeNode) && typeNode.GetString() == "tool_result")
                                    {
                                        var isError = TryGetBool(contentEntry, "is_error") ?? false;
                                        var content = TryGetString(contentEntry, "content") ?? "";
                                        content = WrapDescriptionAt80Chars(content);
                                        return $"[Claude] Tool-Ergebnis {(isError ? "Fehler" : "OK")}: {content}";
                                    }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Falls message nicht wie erwartet ist, einfach generic machen
                    }
                }

                return "[Claude] Nutzerereignis empfangen.";
            }

            if (string.Equals(type, "rate_limit_event", StringComparison.OrdinalIgnoreCase))
            {
                var suggestedTime = TryResolveRateLimitResetTime(root);
                if (suggestedTime is not null)
                {
                    return BuildRateLimitSuggestionLine(suggestedTime.Value);
                }

                return "[Claude] Rate-Limit erreicht.";
            }

            if (string.Equals(type, "result", StringComparison.OrdinalIgnoreCase)
                && (TryGetInt(root, "api_error_status") == 429 || ContainsRateLimitText(TryGetString(root, "result"))))
            {
                var suggestedTime = TryResolveRateLimitResetTime(root);
                if (suggestedTime is not null)
                {
                    return BuildRateLimitSuggestionLine(suggestedTime.Value);
                }

                return "[Claude] Rate-Limit erreicht.";
            }

            return line;
        }
        catch (Exception ex)
        {
            return $"[Claude] JSON-Parsing-Fehler: {ex.Message} | Original: {line}";
        }
    }

    private static string BuildRateLimitSuggestionLine(DateTimeOffset resetUtc)
        => $"{RateLimitSuggestionMarker};resetUtc={resetUtc:O};prompt=Mach nun bitte weiter.";

    private static bool ContainsRateLimitText(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains("hit your limit", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? TryResolveRateLimitResetTime(JsonElement root)
    {
        try
        {
            var unixSeconds = TryGetLong(root, "resetsAt")
                ?? TryGetLong(root, "resets_at")
                ?? TryGetNestedLong(root, "rate_limit_info", "resetsAt")
                ?? TryGetNestedLong(root, "rate_limit_info", "resets_at");

            if (unixSeconds.HasValue)
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value);
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }

            var resultText = TryGetString(root, "result");
            if (TryExtractRateLimitTimeFromText(resultText, out var parsedFromText))
            {
                return parsedFromText;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryExtractRateLimitTimeFromText(string? text, out DateTimeOffset resetUtc)
    {
        resetUtc = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = RateLimitResetTextRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        var timeRaw = match.Groups["time"].Value.Trim();
        var timezoneRaw = match.Groups["timezone"].Value.Trim();

        if (!DateTime.TryParse(
                timeRaw,
                CultureInfo.GetCultureInfo("en-US"),
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedTime))
        {
            return false;
        }

        var nowLocal = DateTime.Now;
        var localCandidate = new DateTime(
            nowLocal.Year,
            nowLocal.Month,
            nowLocal.Day,
            parsedTime.Hour,
            parsedTime.Minute,
            parsedTime.Second,
            DateTimeKind.Unspecified);

        if (localCandidate <= nowLocal.AddMinutes(-1))
        {
            localCandidate = localCandidate.AddDays(1);
        }

        var timezoneInfo = TryResolveTimezone(timezoneRaw);
        if (timezoneInfo is null)
        {
            return false;
        }

        var offset = timezoneInfo.GetUtcOffset(localCandidate);
        resetUtc = new DateTimeOffset(localCandidate, offset).ToUniversalTime();
        return true;
    }

    private static TimeZoneInfo? TryResolveTimezone(string timezoneName)
    {
        if (string.IsNullOrWhiteSpace(timezoneName))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneName);
        }
        catch (TimeZoneNotFoundException)
        {
            if (timezoneName.Equals("Europe/Berlin", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    return null;
                }
            }

            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static string? ExtractAssistantMessage(JsonElement messageNode)
    {
        try
        {
            if (!messageNode.TryGetProperty("content", out var contentNode) || contentNode.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var contentEntry in contentNode.EnumerateArray())
            {
                try
                {
                    var entryType = TryGetString(contentEntry, "type");
                    if (string.Equals(entryType, "text", StringComparison.OrdinalIgnoreCase))
                    {
                        var text = TryGetString(contentEntry, "text");
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text.Trim();
                        }
                    }

                    if (string.Equals(entryType, "thinking", StringComparison.OrdinalIgnoreCase))
                    {
                        var thinking = TryGetString(contentEntry, "thinking");
                        if (!string.IsNullOrWhiteSpace(thinking))
                        {
                            return "[thinking] " + thinking.Trim();
                        }
                    }
                }
                catch (Exception)
                {
                    // Fehler beim Parsen dieses Eintrags ignorieren und nächsten versuchen
                    continue;
                }
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string WrapDescriptionAt80Chars(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        var sb = new StringBuilder();
        var charCount = 0;

        foreach (var ch in description)
        {
            if (charCount >= 80)
            {
                sb.Append('\n');
                charCount = 0;
            }

            sb.Append(ch);
            charCount++;
        }

        return sb.ToString();
    }

    private static bool TryExtractJsonRoot(string input, out JsonElement root)
    {
        root = default;
        var trimmed = input.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        try
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Null => null,
                // Für andere Typen wie Object, Array etc., versuchen sie zu stringifizieren
                _ => property.ToString()
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool? TryGetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return bool.TryParse(property.ToString(), out var parsed) ? parsed : null;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var asInt))
        {
            return asInt;
        }

        return int.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? TryGetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var asLong))
        {
            return asLong;
        }

        return long.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? TryGetNestedLong(JsonElement element, string parentPropertyName, string propertyName)
    {
        if (!element.TryGetProperty(parentPropertyName, out var parentElement) || parentElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetLong(parentElement, propertyName);
    }

    private static Guid ResolveSessionIdFromOutput(IEnumerable<string> lines, Guid fallback)
    {
        foreach (var line in lines.Reverse())
        {
            if (TryExtractSessionId(line, out var parsedSessionId))
            {
                return parsedSessionId;
            }
        }

        return fallback;
    }

    private static bool TryExtractSessionId(string line, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('{'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (!document.RootElement.TryGetProperty("session_id", out var sessionIdElement))
            {
                return false;
            }

            var value = sessionIdElement.GetString();
            return Guid.TryParse(value, out sessionId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string BuildCliPrompt(string localRepoPath, string taskFilePath, bool includeContext)
        => base.BuildCliPrompt(localRepoPath, taskFilePath, includeContext);

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
