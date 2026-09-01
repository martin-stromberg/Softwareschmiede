using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text.Json;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>
/// GitHub Plugin – nutzt gh CLI und git CLI für alle GitHub-Operationen.
/// Der GitHub-Token wird als GH_TOKEN Umgebungsvariable übergeben, niemals als CLI-Argument.
/// </summary>
public sealed class GitHubPlugin : GitPluginBase<GitHubPlugin>
{
    private const string GitHubTokenCredentialKey = "Softwareschmiede.GitHub.Token";
    private const string RepositoryUrlKey = "RepositoryUrl";
    private const string RepositoryNameKey = "RepositoryName";
    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<GitHubPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "GitHub";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.GitHub";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.SourceCodeManagement;

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Authentifizierung",
        [
            new PluginSettingField(
                Key: "Token",
                Label: "Personal Access Token",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "ghp_...",
                Description: "GitHub Personal Access Token mit den Berechtigungen repo, read:org und Zugriff auf Code-Scanning-Alerts. Token erstellen: https://github.com/settings/tokens/new",
                IsRequired: true)
        ]),
        new PluginSettingGroup("Pull Requests",
        [
            new PluginSettingField(
                Key: "AutoCompletePullRequests",
                Label: "Automatischer PR-Abschluss",
                FieldType: PluginSettingFieldType.Boolean,
                Description: "Pull Requests nach erfolgreichen zugeordneten Actions automatisch abschliessen.",
                DefaultValue: "false"),
            new PluginSettingField(
                Key: "PullRequestCompletionStrategy",
                Label: "Abschlussstrategie",
                FieldType: PluginSettingFieldType.Enum,
                Description: "Strategie fuer den automatischen PR-Abschluss.",
                EnumOptions: Enum.GetNames<PullRequestCompletionStrategy>(),
                DefaultValue: PullRequestCompletionStrategy.Merge.ToString()),
            new PluginSettingField(
                Key: "PullRequestMergeMethod",
                Label: "Merge-Methode",
                FieldType: PluginSettingFieldType.Enum,
                Description: "Merge-Methode fuer direkte PR-Merges.",
                EnumOptions: Enum.GetNames<PullRequestMergeMethod>(),
                DefaultValue: PullRequestMergeMethod.Squash.ToString()),
            new PluginSettingField(
                Key: "AllowProtectedBranchBypass",
                Label: "Protected-Branch-Bypass erlauben",
                FieldType: PluginSettingFieldType.Boolean,
                Description: "Erlaubt Abschlussversuche mit administrativem Bypass, falls GitHub und Token das unterstuetzen.",
                DefaultValue: "false")
        ])
    ];

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingField> GetRepositoryLinkFields() =>
    [
        new PluginSettingField(
            Key: RepositoryUrlKey,
            Label: "Repository-URL",
            FieldType: PluginSettingFieldType.Url,
            Placeholder: "https://github.com/owner/repo",
            Description: "Vollständige URL des GitHub-Repositories.",
            IsRequired: true),
        new PluginSettingField(
            Key: RepositoryNameKey,
            Label: "Repository-Name",
            FieldType: PluginSettingFieldType.Text,
            Placeholder: "owner/repo",
            Description: "Repository-ID für API-Aufrufe und Pull-Requests.",
            IsRequired: true)
    ];

    /// <summary>Erstellt eine neue Instanz des <see cref="GitHubPlugin"/>.</summary>
    public GitHubPlugin(ICliRunner cliRunner, ICredentialStore credentialStore, ILogger<GitHubPlugin> logger)
        : base(cliRunner)
    {
        _cliRunner = cliRunner;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    private IDictionary<string, string> GetGhEnvironment()
    {
        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);
        var env = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
        {
            env["GH_TOKEN"] = token;
        }
        return env;
    }

    private IDictionary<string, string> GetGitEnvironment(string? token = null)
    {
        token ??= _credentialStore.GetCredential(GitHubTokenCredentialKey);
        var env = new Dictionary<string, string>();

        // Disable terminal prompts completely
        env["GIT_TERMINAL_PROMPT"] = "0";

        // Disable SSH host key checking to prevent /dev/tty access
        env["GIT_SSH_COMMAND"] = "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null";

        // Tell git to use .netrc for credentials
        // GIT_CREDENTIAL_HELPER=store will use ~/.git-credentials if it exists
        // But .netrc is more universal, so we make sure curl uses it too
        if (!string.IsNullOrEmpty(token))
        {
            // For curl (which git uses under the hood for HTTPS)
            env["NETRC"] = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows() ? "_netrc" : ".netrc");
        }

        return env;
    }

    private static bool IsHttpsRepositoryUrl(string repositoryUrl)
        => Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri)
           && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static string BuildAuthenticatedCloneUrl(string repositoryUrl, string token)
    {
        var repositoryUri = new Uri(repositoryUrl, UriKind.Absolute);
        var uriBuilder = new UriBuilder(repositoryUri)
        {
            UserName = "oauth2",
            Password = token
        };

        return uriBuilder.Uri.AbsoluteUri;
    }

    private static bool IsAuthenticationFailure(string error)
    {
        var normalizedError = error.ToLowerInvariant();
        return normalizedError.Contains("terminal prompts disabled", StringComparison.Ordinal)
               || normalizedError.Contains("could not read username", StringComparison.Ordinal)
               || normalizedError.Contains("authentication failed", StringComparison.Ordinal)
               || normalizedError.Contains("invalid username or password", StringComparison.Ordinal)
               || normalizedError.Contains("support for password authentication was removed", StringComparison.Ordinal)
               || normalizedError.Contains("403", StringComparison.Ordinal)
               || normalizedError.Contains("access denied", StringComparison.Ordinal)
               || normalizedError.Contains("insufficient", StringComparison.Ordinal);
    }

    private static string SanitizeSensitiveOutput(string? message, string? token)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unbekannter Fehler";
        }

        var sanitizedMessage = message;

        if (!string.IsNullOrWhiteSpace(token))
        {
            sanitizedMessage = sanitizedMessage.Replace(token, "***", StringComparison.Ordinal);
        }

        sanitizedMessage = Regex.Replace(
            sanitizedMessage,
            "oauth2:[^@\\s]+@",
            "oauth2:***@",
            RegexOptions.IgnoreCase);

        return sanitizedMessage.Trim();
    }

    private static bool IsNoCommitsBetweenFailure(string error)
    {
        var normalizedError = error.ToLowerInvariant();
        return normalizedError.Contains("no commits between", StringComparison.Ordinal)
               || (normalizedError.Contains("head sha", StringComparison.Ordinal)
                   && normalizedError.Contains("blank", StringComparison.Ordinal)
                   && normalizedError.Contains("base sha", StringComparison.Ordinal));
    }

    private async Task EnsureRemoteCredentialsAsync(string localPath, CancellationToken ct = default)
    {
        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Kein GitHub-Token verfügbar für Remote-Konfiguration.");
            return;
        }

        // Get current remote URL
        var getUrlResult = await _cliRunner.RunAsync(
            "git",
            ["config", "remote.origin.url"],
            localPath,
            null,
            ct);

        if (!getUrlResult.IsSuccess)
        {
            _logger.LogWarning("Konnte remote.origin.url nicht abrufen: {Error}", getUrlResult.StdErr);
            return;
        }

        var currentUrl = getUrlResult.StdOut.Trim();

        // If URL doesn't have credentials yet, add them
        if (!currentUrl.Contains("@"))
        {
            // Check if it's an HTTPS URL
            if (currentUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var authUrl = currentUrl.Replace(
                    "https://",
                    $"https://oauth2:{Uri.EscapeDataString(token)}@",
                    StringComparison.OrdinalIgnoreCase);

                var setUrlResult = await _cliRunner.RunAsync(
                    "git",
                    ["remote", "set-url", "origin", authUrl],
                    localPath,
                    null,
                    ct);

                if (!setUrlResult.IsSuccess)
                {
                    _logger.LogWarning("Konnte remote.origin.url mit Token nicht aktualisieren: {Error}", setUrlResult.StdErr);
                }
                else
                {
                    _logger.LogInformation("Remote origin URL mit Token aktualisiert.");
                }
            }
        }
    }

    private async Task ConfigureGitCredentialsAsync(string localPath, string repositoryUrl, CancellationToken ct = default)
    {
        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Kein GitHub-Token verfügbar für Git-Konfiguration.");
            return;
        }

        // Configure user name and email for commits
        var userNameResult = await _cliRunner.RunAsync(
            "git",
            ["config", "user.name", "Softwareschmiede Bot"],
            localPath,
            null,
            ct);

        if (!userNameResult.IsSuccess)
        {
            _logger.LogWarning("Git user.name Konfiguration fehlgeschlagen: {Error}", userNameResult.StdErr);
        }

        var userEmailResult = await _cliRunner.RunAsync(
            "git",
            ["config", "user.email", "bot@softwareschmiede.local"],
            localPath,
            null,
            ct);

        if (!userEmailResult.IsSuccess)
        {
            _logger.LogWarning("Git user.email Konfiguration fehlgeschlagen: {Error}", userEmailResult.StdErr);
        }

        // Create .netrc file for backup credential storage
        var netrcPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            OperatingSystem.IsWindows() ? "_netrc" : ".netrc");

        var netrcContent = $@"machine github.com
login oauth2
password {token}
machine api.github.com
login oauth2
password {token}
";

        try
        {
            File.WriteAllText(netrcPath, netrcContent);
            _logger.LogInformation("Git .netrc credentials file erstellt/aktualisiert: {Path}", netrcPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git .netrc file konnte nicht erstellt werden");
        }

        // Embed token directly in remote URL
        if (repositoryUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var authUrl = repositoryUrl.Replace(
                "https://",
                $"https://oauth2:{Uri.EscapeDataString(token)}@",
                StringComparison.OrdinalIgnoreCase);

            var setUrlResult = await _cliRunner.RunAsync(
                "git",
                ["remote", "set-url", "origin", authUrl],
                localPath,
                null,
                ct);

            if (!setUrlResult.IsSuccess)
            {
                _logger.LogWarning("Git remote URL Konfiguration fehlgeschlagen: {Error}", setUrlResult.StdErr);
            }
            else
            {
                _logger.LogInformation("Git remote origin URL aktualisiert mit eingebettetem Token.");
            }
        }

        // Disable strict host key checking for git operations
        var strictHostKeyResult = await _cliRunner.RunAsync(
            "git",
            ["config", "core.sshCommand", "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null"],
            localPath,
            null,
            ct);

        if (!strictHostKeyResult.IsSuccess)
        {
            _logger.LogWarning("Git core.sshCommand Konfiguration fehlgeschlagen: {Error}", strictHostKeyResult.StdErr);
        }
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return [];
        }

        _logger.LogInformation("Rufe Issues für Repository {RepositoryId} ab.", normalizedRepositoryId);

        var result = await _cliRunner.RunAsync(
            "gh",
            ["issue", "list", "--repo", normalizedRepositoryId, "--json", "number,title,body,labels,milestone", "--limit", "100"],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogError("gh issue list fehlgeschlagen für {RepositoryId}: {StdErr}", normalizedRepositoryId, result.StdErr);
            return [];
        }

        return ParseIssues(result.StdOut);
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<ScmAlert>> GetAlertsAsync(string repositoryId, CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return [];
        }

        _logger.LogInformation("Rufe Code-Scanning-Alerts für Repository {RepositoryId} ab.", normalizedRepositoryId);

        var result = await _cliRunner.RunAsync(
            "gh",
            ["api", "--paginate", "--slurp", $"repos/{normalizedRepositoryId}/code-scanning/alerts?state=open&per_page=100"],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr, _credentialStore.GetCredential(GitHubTokenCredentialKey));
            if (IsNotFound(result.StdErr))
            {
                _logger.LogInformation(
                    "Code-Scanning-Alerts für Repository {RepositoryId} nicht verfügbar: {StdErr}",
                    normalizedRepositoryId,
                    sanitizedError);
                return [];
            }

            if (IsAuthenticationFailure(result.StdErr))
            {
                _logger.LogWarning(
                    "Code-Scanning-Alerts für Repository {RepositoryId} konnten wegen fehlender Rechte nicht geladen werden: {StdErr}",
                    normalizedRepositoryId,
                    sanitizedError);
                return [];
            }

            _logger.LogWarning(
                "gh api code-scanning/alerts fehlgeschlagen für {RepositoryId}: {StdErr}",
                normalizedRepositoryId,
                sanitizedError);
            return [];
        }

        return ParseCodeScanningAlerts(result.StdOut, normalizedRepositoryId);
    }

    /// <inheritdoc/>
    public override Task<bool> CanCreateIssueAsync(string repositoryId, CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrWhiteSpace(NormalizeRepositoryId(repositoryId)));

    /// <inheritdoc/>
    public override async Task<IssueCreateResult> CreateIssueAsync(
        string repositoryId,
        IssueCreateRequest request,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return IssueCreateResult.Failed("Repository-ID fehlt.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return IssueCreateResult.Failed("Der Issue-Titel darf nicht leer sein.");
        }

        var result = await _cliRunner.RunAsync(
            "gh",
            [
                "issue", "create",
                "--repo", normalizedRepositoryId,
                "--title", request.Title.Trim(),
                "--body", request.Body ?? string.Empty
            ],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr, _credentialStore.GetCredential(GitHubTokenCredentialKey));
            _logger.LogError("gh issue create fehlgeschlagen für {RepositoryId}: {StdErr}", normalizedRepositoryId, sanitizedError);
            return IssueCreateResult.Failed($"GitHub-Issue konnte nicht erstellt werden: {sanitizedError}");
        }

        var issueUrl = result.StdOut.Trim();
        if (!TryParseIssueNumber(issueUrl, out var issueNumber))
        {
            return IssueCreateResult.Failed($"GitHub-Issue wurde erstellt, die Antwort konnte aber nicht ausgewertet werden: {issueUrl}");
        }

        return IssueCreateResult.Success(new Issue(
            issueNumber,
            request.Title.Trim(),
            request.Body,
            [],
            null,
            issueUrl));
    }

    /// <inheritdoc/>
    public override async Task<IssueTemplateLoadResult> GetIssueTemplatesAsync(string repositoryId, CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return IssueTemplateLoadResult.Success([]);
        }

        var listResult = await _cliRunner.RunAsync(
            "gh",
            ["api", $"repos/{normalizedRepositoryId}/contents/.github/ISSUE_TEMPLATE"],
            null,
            GetGhEnvironment(),
            ct);

        if (!listResult.IsSuccess)
        {
            return IsNotFound(listResult.StdErr)
                ? IssueTemplateLoadResult.Success([])
                : IssueTemplateLoadResult.Failed($"GitHub-Issue-Templates konnten nicht geladen werden: {SanitizeSensitiveOutput(listResult.StdErr, _credentialStore.GetCredential(GitHubTokenCredentialKey))}");
        }

        try
        {
            using var doc = JsonDocument.Parse(listResult.StdOut);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return IssueTemplateLoadResult.Success([]);
            }

            var templates = new List<IssueTemplate>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var path = item.TryGetProperty("path", out var pathEl) ? pathEl.GetString() : null;

                if (!string.Equals(type, "file", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(name)
                    || string.IsNullOrWhiteSpace(path)
                    || !IsSupportedTemplateFile(name))
                {
                    continue;
                }

                var content = await LoadTemplateContentAsync(normalizedRepositoryId, path, ct);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    templates.Add(new IssueTemplate(name, content));
                }
            }

            return IssueTemplateLoadResult.Success(templates);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Auswerten der GitHub-Issue-Templates für {RepositoryId}.", repositoryId);
            return IssueTemplateLoadResult.Failed("GitHub-Issue-Templates konnten nicht ausgewertet werden.");
        }
    }

    private async Task<string?> LoadTemplateContentAsync(string repositoryId, string path, CancellationToken ct)
    {
        var result = await _cliRunner.RunAsync(
            "gh",
            ["api", $"repos/{repositoryId}/contents/{path}"],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("GitHub-Issue-Template {TemplatePath} konnte nicht geladen werden: {StdErr}", path, result.StdErr);
            return null;
        }

        using var doc = JsonDocument.Parse(result.StdOut);
        if (!doc.RootElement.TryGetProperty("content", out var contentEl))
        {
            return null;
        }

        var encoded = contentEl.GetString();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace("\n", string.Empty, StringComparison.Ordinal)));
    }

    private static bool TryParseIssueNumber(string issueUrl, out int issueNumber)
    {
        issueNumber = 0;
        var lastSlashIndex = issueUrl.LastIndexOf('/');
        return lastSlashIndex > 0
               && lastSlashIndex < issueUrl.Length - 1
               && int.TryParse(issueUrl[(lastSlashIndex + 1)..], out issueNumber);
    }

    private static bool IsSupportedTemplateFile(string name)
        => name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeRepositoryId(string repositoryId)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            return null;
        }

        var extracted = TryExtractRepositoryId(repositoryId);
        return extracted ?? repositoryId.Trim().Trim('/');
    }

    private static bool IsNotFound(string error)
        => error.Contains("404", StringComparison.OrdinalIgnoreCase)
           || error.Contains("not found", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ScmAlert> ParseCodeScanningAlerts(string json, string repositoryId)
    {
        using var doc = JsonDocument.Parse(json);
        var alerts = new List<ScmAlert>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return alerts;
        }

        foreach (var element in EnumerateCodeScanningAlertElements(doc.RootElement))
        {
            var number = GetInt32OrDefault(element, "number");
            if (number <= 0)
            {
                continue;
            }

            var rule = TryGetObject(element, "rule");
            var tool = TryGetObject(element, "tool");
            var location = TryGetObject(TryGetObject(element, "most_recent_instance"), "location");
            var message = TryGetObject(element, "most_recent_instance");

            var ruleId = GetStringOrNull(rule, "id");
            var ruleName = GetStringOrNull(rule, "name");
            var description = GetStringOrNull(rule, "description");
            var messageText = GetStringOrNull(TryGetObject(message, "message"), "text");
            var title = FirstNonWhiteSpace(ruleName, ruleId, messageText, $"Code scanning alert #{number}")!;
            var severity = FirstNonWhiteSpace(
                GetStringOrNull(rule, "security_severity_level"),
                GetStringOrNull(rule, "severity"));

            alerts.Add(new ScmAlert(
                AlertNumber: number,
                SourceKey: $"github:code-scanning:{repositoryId}:{number}",
                AlertType: ScmAlertType.CodeScanning,
                Title: title,
                Description: FirstNonWhiteSpace(messageText, description),
                AlertUrl: GetStringOrNull(element, "html_url"),
                Severity: severity,
                State: GetStringOrNull(element, "state"),
                ToolName: GetStringOrNull(tool, "name"),
                RuleId: ruleId,
                RuleName: ruleName,
                FilePath: GetStringOrNull(location, "path"),
                StartLine: GetNullableInt32(location, "start_line")));
        }

        return alerts;
    }

    private static IEnumerable<JsonElement> EnumerateCodeScanningAlertElements(JsonElement root)
    {
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var nestedElement in element.EnumerateArray())
                {
                    if (nestedElement.ValueKind == JsonValueKind.Object)
                    {
                        yield return nestedElement;
                    }
                }

                continue;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                yield return element;
            }
        }
    }

    private static JsonElement? TryGetObject(JsonElement? element, string propertyName)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property;
    }

    private static string? GetStringOrNull(JsonElement? element, string propertyName)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.GetString();
    }

    private static int GetInt32OrDefault(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : 0;

    private static int? GetNullableInt32(JsonElement? element, string propertyName)
        => element is { ValueKind: JsonValueKind.Object } value
           && value.TryGetProperty(propertyName, out var property)
           && property.TryGetInt32(out var intValue)
            ? intValue
            : null;

    private static string? FirstNonWhiteSpace(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static IEnumerable<Issue> ParseIssues(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var issues = new List<Issue>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var number = element.GetProperty("number").GetInt32();
            var title = element.GetProperty("title").GetString() ?? string.Empty;
            var body = element.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;
            var labels = element.TryGetProperty("labels", out var labelsEl)
                ? labelsEl.EnumerateArray()
                    .Select(l => l.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty)
                    .ToList()
                : new List<string>();
            var milestone = element.TryGetProperty("milestone", out var msEl) && msEl.ValueKind == JsonValueKind.Object
                ? msEl.TryGetProperty("title", out var msTitle) ? msTitle.GetString() : null
                : null;
            var url = element.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
            issues.Add(new Issue(number, title, body, labels, milestone, url));
        }
        return issues;
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<PullRequest>> GetOpenPullRequestsAsync(string repositoryId, CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return [];
        }

        normalizedRepositoryId = PullRequestRepositoryId.Normalize(PullRequestProvider.GitHub, normalizedRepositoryId);
        _logger.LogInformation("Rufe offene Pull Requests fuer Repository {RepositoryId} ab.", normalizedRepositoryId);

        var result = await _cliRunner.RunAsync(
            "gh",
            ["api", "--paginate", "--slurp", $"repos/{normalizedRepositoryId}/pulls?state=open&per_page=100"],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr, _credentialStore.GetCredential(GitHubTokenCredentialKey));
            _logger.LogWarning("gh api pulls fehlgeschlagen fuer {RepositoryId}: {StdErr}", normalizedRepositoryId, sanitizedError);
            return [];
        }

        return ParseOpenPullRequests(result.StdOut, normalizedRepositoryId);
    }

    private static IEnumerable<PullRequest> ParseOpenPullRequests(string json, string repositoryId)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<PullRequest>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var page in doc.RootElement.EnumerateArray())
        {
            if (page.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in page.EnumerateArray())
                    AddOpenPullRequest(list, element, repositoryId);
            }
            else
            {
                AddOpenPullRequest(list, page, repositoryId);
            }
        }

        return list;
    }

    private static void AddOpenPullRequest(List<PullRequest> list, JsonElement element, string repositoryId)
    {
        var state = GetStringOrNull(element, "state");
        if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
            return;

        var number = GetInt32OrDefault(element, "number");
        if (number <= 0)
            return;

        var title = GetStringOrNull(element, "title") ?? string.Empty;
        var url = GetStringOrNull(element, "html_url") ?? GetStringOrNull(element, "url") ?? string.Empty;
        var head = element.TryGetProperty("head", out var headElement) && headElement.ValueKind == JsonValueKind.Object
            ? headElement
            : (JsonElement?)null;
        var @base = element.TryGetProperty("base", out var baseElement) && baseElement.ValueKind == JsonValueKind.Object
            ? baseElement
            : (JsonElement?)null;

        var sourceBranch = head is null ? string.Empty : GetStringOrNull(head.Value, "ref") ?? string.Empty;
        var sourceSha = head is null ? null : GetStringOrNull(head.Value, "sha");
        var sourceRepositoryId = repositoryId;
        string? sourceRepositoryUrl = null;
        if (head is not null
            && head.Value.TryGetProperty("repo", out var headRepo)
            && headRepo.ValueKind == JsonValueKind.Object)
        {
            var fullName = GetStringOrNull(headRepo, "full_name");
            if (!string.IsNullOrWhiteSpace(fullName))
                sourceRepositoryId = PullRequestRepositoryId.Normalize(PullRequestProvider.GitHub, fullName);
            sourceRepositoryUrl = GetStringOrNull(headRepo, "clone_url") ?? GetStringOrNull(headRepo, "ssh_url");
        }

        var targetBranch = @base is null ? null : GetStringOrNull(@base.Value, "ref");

        list.Add(new PullRequest(
            number,
            title,
            url,
            sourceBranch,
            PullRequestProvider.GitHub,
            repositoryId,
            GetStringOrNull(element, "node_id") ?? GetStringOrNull(element, "id"),
            targetBranch,
            sourceSha,
            sourceRepositoryId,
            sourceRepositoryUrl,
            $"refs/heads/{sourceBranch}"));
    }

    /// <inheritdoc/>
    public override async Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Klone Repository {Url} nach {TargetPath}.", repositoryUrl, targetPath);

        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);
        var cloneUrl = repositoryUrl;

        if (IsHttpsRepositoryUrl(repositoryUrl))
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "git clone abgebrochen: GitHub-Token fehlt. Bitte in den Plugin-Einstellungen einen gültigen Personal Access Token konfigurieren (Scope: repo, ggf. read:org).");
            }

            cloneUrl = BuildAuthenticatedCloneUrl(repositoryUrl, token);
        }

        // Clone with environment that disables SSH prompts
        var result = await _cliRunner.RunAsync(
            "git",
            ["clone", cloneUrl, targetPath],
            null,
            GetGitEnvironment(token),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr, token);
            if (IsAuthenticationFailure(result.StdErr))
            {
                throw new InvalidOperationException(
                    $"git clone fehlgeschlagen: Authentifizierung fehlgeschlagen. Bitte GitHub-Token prüfen/neu setzen und Scopes (repo, ggf. read:org) verifizieren. Details: {sanitizedError}");
            }

            throw new InvalidOperationException($"git clone fehlgeschlagen: {sanitizedError}");
        }

        // Configure git credentials for the cloned repository
        await ConfigureGitCredentialsAsync(targetPath, repositoryUrl, ct);
    }

    /// <inheritdoc/>
    public override async Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        _logger.LogInformation("Pushe Branch {BranchName} in {LocalPath}.", branchName, localPath);

        // Ensure credentials are configured before pushing
        await EnsureRemoteCredentialsAsync(localPath, ct);

        var result = await _cliRunner.RunAsync(
            "git",
            ["push", "--set-upstream", "origin", branchName],
            localPath,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git push fehlgeschlagen: {result.StdErr}");
        }
    }

    /// <inheritdoc/>
    public override async Task PullAsync(string localPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Führe git pull in {LocalPath} durch.", localPath);

        // Ensure credentials are configured before pulling
        await EnsureRemoteCredentialsAsync(localPath, ct);

        var result = await _cliRunner.RunAsync(
            "git",
            ["pull"],
            localPath,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git pull fehlgeschlagen: {result.StdErr}");
        }
    }

    /// <inheritdoc/>
    public override async Task<PullRequest> CreatePullRequestAsync(
        string repositoryId,
        string branchName,
        string? baseBranch,
        string title,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Erstelle Pull Request für Branch {BranchName} in Repository {RepositoryId}.",
            branchName, repositoryId);

        // Use --fill flag to auto-fill from commits, or provide explicit title/body
        var args = new List<string>
        {
            "pr", "create",
            "--repo", repositoryId,
            "--head", branchName,
            "--title", title,
            "--body", body
        };

        if (!string.IsNullOrEmpty(baseBranch))
        {
            args.AddRange(["--base", baseBranch]);
        }

        var result = await _cliRunner.RunAsync(
            "gh",
            args.ToArray(),
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr, _credentialStore.GetCredential(GitHubTokenCredentialKey));
            _logger.LogError("gh pr create fehlgeschlagen: {StdErr}", sanitizedError);

            if (IsNoCommitsBetweenFailure(result.StdErr))
            {
                throw new InvalidOperationException(
                    $"gh pr create fehlgeschlagen: Der Branch '{branchName}' enthält keine Commits gegenüber dem Zielbranch. Bitte stelle sicher, dass Änderungen committet wurden. Details: {sanitizedError}");
            }

            throw new InvalidOperationException($"gh pr create fehlgeschlagen: {sanitizedError}");
        }

        // Parse the output text: "https://github.com/owner/repo/pull/123"
        var prUrl = result.StdOut.Trim();

        // Extract PR number from URL
        var lastSlashIndex = prUrl.LastIndexOf('/');
        if (lastSlashIndex > 0 && int.TryParse(prUrl[(lastSlashIndex + 1)..], out var prNumber))
        {
            try
            {
                var status = await GetPullRequestStatusAsync(repositoryId, prNumber, ct);
                return new PullRequest(
                    status.Nummer,
                    status.Titel,
                    status.Url,
                    status.SourceBranch,
                    status.Provider,
                    status.RepositoryId,
                    status.ProviderPullRequestId,
                    status.TargetBranch,
                    status.HeadSha);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Pull-Request-Metadaten konnten nach Erstellung nicht nachgeladen werden.");
                return new PullRequest(prNumber, title, prUrl, branchName, PullRequestProvider.GitHub, repositoryId);
            }
        }

        // Fallback: if we can't parse, throw error
        throw new InvalidOperationException($"PR created but could not parse response: {result.StdOut}");
    }

    /// <inheritdoc/>
    public override async Task<PullRequestStatusInfo> GetPullRequestStatusAsync(
        string repositoryId,
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId)
            ?? throw new InvalidOperationException("Repository-ID fehlt.");

        var result = await _cliRunner.RunAsync(
            "gh",
            [
                "pr", "view", pullRequestNumber.ToString(),
                "--repo", normalizedRepositoryId,
                "--json", "id,number,title,url,state,headRefName,baseRefName,headRefOid,mergeCommit,mergeStateStatus"
            ],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr, _credentialStore.GetCredential(GitHubTokenCredentialKey));
            throw new InvalidOperationException($"gh pr view fehlgeschlagen: {sanitizedError}");
        }

        return ParsePullRequestStatus(result.StdOut, normalizedRepositoryId);
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<PullRequestWorkflowRunInfo>> GetPullRequestWorkflowRunsAsync(
        string repositoryId,
        int pullRequestNumber,
        string? headSha = null,
        string? mergeCommitSha = null,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId)
            ?? throw new InvalidOperationException("Repository-ID fehlt.");

        var runs = new List<PullRequestWorkflowRunInfo>();
        if (!string.IsNullOrWhiteSpace(headSha))
        {
            var result = await RunWorkflowListAsync(normalizedRepositoryId, headSha, ct);
            runs.AddRange(ParseWorkflowRuns(result.StdOut, headSha, null));
        }

        if (!string.IsNullOrWhiteSpace(mergeCommitSha)
            && !string.Equals(mergeCommitSha, headSha, StringComparison.OrdinalIgnoreCase))
        {
            var result = await RunWorkflowListAsync(normalizedRepositoryId, mergeCommitSha, ct);
            runs.AddRange(ParseWorkflowRuns(result.StdOut, null, mergeCommitSha));
        }

        if (runs.Count == 0 && string.IsNullOrWhiteSpace(headSha) && string.IsNullOrWhiteSpace(mergeCommitSha))
        {
            var result = await RunWorkflowListAsync(normalizedRepositoryId, null, ct);
            runs.AddRange(ParseWorkflowRuns(result.StdOut, headSha, mergeCommitSha));
        }

        return runs
            .GroupBy(r => r.ProviderRunId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<PullRequestCompletionResult> CompletePullRequestAsync(
        string repositoryId,
        int pullRequestNumber,
        PullRequestCompletionOptions options,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return PullRequestCompletionResult.Failed("Repository-ID fehlt.");
        }

        var args = new List<string>();
        if (options.Strategy == PullRequestCompletionStrategy.ApprovalOnly)
        {
            args.AddRange(["pr", "review", pullRequestNumber.ToString(), "--repo", normalizedRepositoryId, "--approve"]);
        }
        else
        {
            args.AddRange(["pr", "merge", pullRequestNumber.ToString(), "--repo", normalizedRepositoryId]);
            args.Add(options.MergeMethod switch
            {
                PullRequestMergeMethod.Merge => "--merge",
                PullRequestMergeMethod.Rebase => "--rebase",
                _ => "--squash"
            });

            if (options.Strategy == PullRequestCompletionStrategy.AutoMerge)
            {
                args.Add("--auto");
            }

            if (options.AllowProtectedBranchBypass)
            {
                args.Add("--admin");
            }
        }

        var result = await _cliRunner.RunAsync("gh", args.ToArray(), null, GetGhEnvironment(), ct);
        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr, _credentialStore.GetCredential(GitHubTokenCredentialKey));
            return IsAuthenticationFailure(result.StdErr) || IsBranchProtectionFailure(result.StdErr)
                ? PullRequestCompletionResult.BlockedResult(sanitizedError)
                : PullRequestCompletionResult.Failed(sanitizedError);
        }

        if (options.Strategy == PullRequestCompletionStrategy.ApprovalOnly)
        {
            return PullRequestCompletionResult.Approved(result.StdOut.Trim());
        }

        var status = await GetPullRequestStatusAsync(normalizedRepositoryId, pullRequestNumber, ct);
        if (status.Status != PullRequestStatus.Merged)
        {
            return PullRequestCompletionResult.WaitingForMerge(result.StdOut.Trim());
        }

        return PullRequestCompletionResult.Completed(status.MergeCommitSha, result.StdOut.Trim());
    }

    private async Task<CliResult> RunWorkflowListAsync(string repositoryId, string? commitSha, CancellationToken ct)
    {
        var args = new List<string>
        {
            "run", "list",
            "--repo", repositoryId,
            "--json", "databaseId,name,displayTitle,status,conclusion,url,headSha,headBranch,createdAt,updatedAt",
            "--limit", "100"
        };

        if (!string.IsNullOrWhiteSpace(commitSha))
        {
            args.AddRange(["--commit", commitSha]);
        }

        var result = await _cliRunner.RunAsync("gh", args.ToArray(), null, GetGhEnvironment(), ct);
        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr, _credentialStore.GetCredential(GitHubTokenCredentialKey));
            throw new InvalidOperationException($"gh run list fehlgeschlagen: {sanitizedError}");
        }

        return result;
    }

    private static PullRequestStatusInfo ParsePullRequestStatus(string json, string repositoryId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var state = GetStringOrNull(root, "state");
        var mergeStateStatus = GetStringOrNull(root, "mergeStateStatus");
        var mergeCommitSha = root.TryGetProperty("mergeCommit", out var mergeCommit)
                             && mergeCommit.ValueKind == JsonValueKind.Object
            ? GetStringOrNull(mergeCommit, "oid")
            : null;

        var status = string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase)
                     || (string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(mergeCommitSha))
            ? PullRequestStatus.Merged
            : string.Equals(state, "OPEN", StringComparison.OrdinalIgnoreCase)
                ? PullRequestStatus.Open
                : string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase)
                    ? PullRequestStatus.Closed
                    : PullRequestStatus.Unknown;

        return new PullRequestStatusInfo(
            PullRequestProvider.GitHub,
            repositoryId,
            GetInt32OrDefault(root, "number"),
            GetStringOrNull(root, "id"),
            GetStringOrNull(root, "url") ?? string.Empty,
            GetStringOrNull(root, "title") ?? string.Empty,
            GetStringOrNull(root, "headRefName") ?? string.Empty,
            GetStringOrNull(root, "baseRefName") ?? string.Empty,
            GetStringOrNull(root, "headRefOid"),
            mergeCommitSha,
            status,
            MapMergeStatus(mergeStateStatus, status),
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<PullRequestWorkflowRunInfo> ParseWorkflowRuns(
        string json,
        string? headSha,
        string? mergeCommitSha)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var runs = new List<PullRequestWorkflowRunInfo>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var runHeadSha = GetStringOrNull(element, "headSha");
            var isPreMerge = !string.IsNullOrWhiteSpace(headSha)
                             && string.Equals(runHeadSha, headSha, StringComparison.OrdinalIgnoreCase);
            var isPostMerge = !string.IsNullOrWhiteSpace(mergeCommitSha)
                              && string.Equals(runHeadSha, mergeCommitSha, StringComparison.OrdinalIgnoreCase);

            if (!isPreMerge && !isPostMerge)
            {
                continue;
            }

            var providerRunId = element.TryGetProperty("databaseId", out var databaseId)
                ? databaseId.ValueKind == JsonValueKind.Number
                    ? databaseId.GetInt64().ToString()
                    : databaseId.GetString() ?? string.Empty
                : string.Empty;

            runs.Add(new PullRequestWorkflowRunInfo(
                providerRunId,
                ResolveWorkflowRunDisplayName(element),
                GetStringOrNull(element, "url"),
                runHeadSha,
                GetStringOrNull(element, "headBranch"),
                MapWorkflowRunStatus(GetStringOrNull(element, "status")),
                MapWorkflowRunConclusion(GetStringOrNull(element, "conclusion")),
                TryGetDateTimeOffset(element, "createdAt"),
                TryGetDateTimeOffset(element, "updatedAt"),
                isPostMerge));
        }

        return runs;
    }

    private static string ResolveWorkflowRunDisplayName(JsonElement element)
    {
        var name = GetStringOrNull(element, "name");
        var displayTitle = GetStringOrNull(element, "displayTitle");
        if (!string.IsNullOrWhiteSpace(displayTitle))
        {
            return !string.IsNullOrWhiteSpace(name)
                   && !string.Equals(name, displayTitle, StringComparison.OrdinalIgnoreCase)
                ? $"{name}: {displayTitle}"
                : displayTitle;
        }

        return name ?? "Workflow";
    }

    private static PullRequestMergeStatus MapMergeStatus(string? mergeStateStatus, PullRequestStatus status)
    {
        if (status == PullRequestStatus.Merged)
        {
            return PullRequestMergeStatus.Merged;
        }

        return mergeStateStatus?.ToUpperInvariant() switch
        {
            "CLEAN" or "HAS_HOOKS" or "UNSTABLE" => PullRequestMergeStatus.Mergeable,
            "DIRTY" => PullRequestMergeStatus.Conflicting,
            "BLOCKED" or "DRAFT" or "BEHIND" => PullRequestMergeStatus.Blocked,
            _ => PullRequestMergeStatus.Unknown
        };
    }

    private static WorkflowRunStatus MapWorkflowRunStatus(string? status)
        => status?.ToLowerInvariant() switch
        {
            "queued" or "waiting" or "requested" or "pending" => WorkflowRunStatus.Queued,
            "in_progress" => WorkflowRunStatus.InProgress,
            "completed" => WorkflowRunStatus.Completed,
            _ => WorkflowRunStatus.Unknown
        };

    private static WorkflowRunConclusion MapWorkflowRunConclusion(string? conclusion)
        => conclusion?.ToLowerInvariant() switch
        {
            "success" => WorkflowRunConclusion.Success,
            "failure" => WorkflowRunConclusion.Failure,
            "cancelled" => WorkflowRunConclusion.Cancelled,
            "skipped" => WorkflowRunConclusion.Skipped,
            "timed_out" => WorkflowRunConclusion.TimedOut,
            "action_required" => WorkflowRunConclusion.ActionRequired,
            _ => WorkflowRunConclusion.Unknown
        };

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = GetStringOrNull(element, propertyName);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool IsBranchProtectionFailure(string error)
    {
        var normalizedError = error.ToLowerInvariant();
        return normalizedError.Contains("protected branch", StringComparison.Ordinal)
               || normalizedError.Contains("required status check", StringComparison.Ordinal)
               || normalizedError.Contains("review required", StringComparison.Ordinal)
               || normalizedError.Contains("bypass", StringComparison.Ordinal)
               || normalizedError.Contains("cannot approve your own pull request", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Prüfe GitHub-Plugin-Health.");
        var result = await _cliRunner.RunAsync("gh", ["auth", "status"], null, GetGhEnvironment(), ct);
        return result.IsSuccess;
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Rufe Remote-Branches für {RepositoryUrl} ab.", repositoryUrl);

        var result = await _cliRunner.RunAsync(
            "git",
            ["ls-remote", "--heads", repositoryUrl],
            null,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("git ls-remote fehlgeschlagen für {RepositoryUrl}: {StdErr}", repositoryUrl, result.StdErr);
            return [];
        }

        // Ausgabe: "<hash>\trefs/heads/<branchname>"
        return result.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var parts = line.Split('\t', 2);
                return parts.Length == 2 ? parts[1].Replace("refs/heads/", string.Empty).Trim() : null;
            })
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<AvailableRepository>> GetAvailableRepositoriesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Lade verfügbare GitHub-Repositories.");
        var result = await _cliRunner.RunAsync(
            "gh",
            ["repo", "list", "--json", "name,nameWithOwner,url,createdAt,updatedAt,owner", "--limit", "100"],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("gh repo list fehlgeschlagen: {StdErr}", result.StdErr);
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            return doc.RootElement.EnumerateArray()
                .Select(e => new AvailableRepository(
                    e.GetProperty("name").GetString() ?? string.Empty,
                    e.TryGetProperty("updatedAt", out var updatedAt) ? updatedAt.GetDateTime() :
                        e.TryGetProperty("createdAt", out var createdAt) ? createdAt.GetDateTime() : DateTime.MinValue,
                    e.GetProperty("nameWithOwner").GetString() ?? string.Empty,
                    e.GetProperty("url").GetString() ?? string.Empty))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Parsen der GitHub-Repository-Liste.");
            return [];
        }
    }

    /// <inheritdoc/>
    public override async Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Ermittle Standard-Branch für {RepositoryUrl}.", repositoryUrl);

        var result = await _cliRunner.RunAsync(
            "git",
            ["ls-remote", "--symref", repositoryUrl, "HEAD"],
            null,
            GetGhEnvironment(),
            ct);

        if (result.IsSuccess)
        {
            // Erste Zeile: "ref: refs/heads/main\tHEAD"
            var firstLine = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (firstLine.StartsWith("ref: refs/heads/", StringComparison.Ordinal))
            {
                var branch = firstLine.Replace("ref: refs/heads/", string.Empty).Split('\t')[0].Trim();
                if (!string.IsNullOrEmpty(branch))
                {
                    return branch;
                }
            }
        }

        _logger.LogWarning("Standard-Branch konnte nicht ermittelt werden, Fallback auf 'main'.");
        return "main";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Ruft die Verzeichnisstruktur des Standard-Branches rein remote über die GitHub Git-Trees-API ab
    /// (<c>gh api repos/{owner}/{repo}/git/trees/{branch}?recursive=1</c>) — ein lokaler Klon ist dafür nicht
    /// erforderlich. Damit ist eine Unterverzeichnis-Auswahl bereits vor dem Klon möglich (Hauptanwendungsfall
    /// der Arbeitsverzeichnis-Auswahl).
    /// </remarks>
    public override async Task<IEnumerable<RepositoryDirectoryEntry>> GetRepositoryStructureAsync(
        string repositoryUrl,
        int maxDepth = 2,
        CancellationToken ct = default,
        string? branchName = null)
    {
        var result = await GetRepositoryStructureLoadResultAsync(repositoryUrl, maxDepth, ct, branchName).ConfigureAwait(false);
        return result.Status == RepositoryStructureLoadStatus.Success ? result.Entries : [];
    }

    /// <inheritdoc/>
    public override async Task<RepositoryStructureLoadResult> GetRepositoryStructureLoadResultAsync(
        string repositoryUrl,
        int maxDepth = 2,
        CancellationToken ct = default,
        string? branchName = null)
    {
        var repositoryId = TryExtractRepositoryId(repositoryUrl);
        if (repositoryId is null)
        {
            _logger.LogWarning(
                "Verzeichnisstruktur konnte nicht ermittelt werden: Repository-ID konnte nicht aus '{RepositoryUrl}' extrahiert werden.",
                repositoryUrl);
            return RepositoryStructureLoadResult.Failed("Repository-ID konnte nicht aus der URL ermittelt werden.");
        }

        var branch = string.IsNullOrWhiteSpace(branchName) ? await GetDefaultBranchAsync(repositoryUrl, ct) : branchName;

        var result = await _cliRunner.RunAsync(
            "gh",
            ["api", $"repos/{repositoryId}/git/trees/{branch}?recursive=1"],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "gh api git/trees fehlgeschlagen für {RepositoryId} (Branch {Branch}): {StdErr}",
                repositoryId,
                branch,
                result.StdErr);
            return RepositoryStructureLoadResult.Failed(result.StdErr);
        }

        return ParseRepositoryTreeLoadResult(result.StdOut, maxDepth, repositoryId);
    }

    private RepositoryStructureLoadResult ParseRepositoryTreeLoadResult(string json, int maxDepth, string repositoryId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("truncated", out var truncatedEl) &&
                truncatedEl.ValueKind == JsonValueKind.True)
            {
                _logger.LogWarning(
                    "GitHub Git-Trees-API-Antwort für {RepositoryId} ist abgeschnitten (truncated=true) — bei sehr großen Repositories ist die ermittelte Verzeichnisstruktur ggf. unvollständig.",
                    repositoryId);
            }

            if (!doc.RootElement.TryGetProperty("tree", out var treeEl) || treeEl.ValueKind != JsonValueKind.Array)
            {
                return RepositoryStructureLoadResult.Failed("GitHub-Antwort enthält keine gültige tree-Liste.");
            }

            var entries = treeEl.EnumerateArray()
                .Select(entry => new
                {
                    Type = entry.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null,
                    Path = entry.TryGetProperty("path", out var pathEl) ? pathEl.GetString() : null
                })
                .Where(entry => (entry.Type == "tree" || entry.Type == "blob") && !string.IsNullOrEmpty(entry.Path))
                .Where(entry => entry.Path!.Count(c => c == '/') + 1 <= maxDepth)
                .Select(entry => new RepositoryDirectoryEntry(entry.Path!, entry.Type == "tree"))
                .ToList();

            return RepositoryStructureLoadResult.Success(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Parsen der GitHub-Verzeichnisstruktur für {RepositoryId}.", repositoryId);
            return RepositoryStructureLoadResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Extrahiert die Repository-ID (<c>owner/repo</c>) aus einer GitHub-Repository-URL. Unterstützt HTTPS-
    /// (<c>https://github.com/owner/repo(.git)?</c>) und SSH-URLs (<c>git@github.com:owner/repo(.git)?</c>).
    /// Liefert <c>null</c> statt zu werfen, wenn die URL nicht geparst werden kann.
    /// </summary>
    /// <param name="repositoryUrl">Die zu parsende GitHub-Repository-URL (HTTPS oder SSH).</param>
    /// <returns>Die Repository-ID im Format <c>owner/repo</c>, oder <c>null</c> wenn die URL nicht erkannt wurde.</returns>
    private static string? TryExtractRepositoryId(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return null;
        }

        var url = repositoryUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? repositoryUrl[..^4]
            : repositoryUrl;

        if (!url.Contains("://", StringComparison.Ordinal))
        {
            // SCP-/SSH-Format: git@github.com:owner/repo
            var colonIndex = url.IndexOf(':');
            if (colonIndex < 0 || colonIndex >= url.Length - 1)
            {
                return null;
            }

            var repositoryPath = url[(colonIndex + 1)..];
            var slashIndex = repositoryPath.IndexOf('/');
            if (slashIndex <= 0 || slashIndex >= repositoryPath.Length - 1)
            {
                return null;
            }

            return $"{repositoryPath[..slashIndex]}/{repositoryPath[(slashIndex + 1)..]}";
        }

        // HTTPS-Format: https://github.com/owner/repo[.git][/][?query][#fragment]. Uri.AbsolutePath
        // normalisiert Trailing-Slashes weg und ignoriert Query-String/Fragment, statt sie fälschlich
        // in "owner"/"repo" einzumischen (siehe TryExtractRepositoryId in BitbucketPlugin für dasselbe Muster).
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        var owner = segments[0];
        var repo = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];

        return string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo) ? null : $"{owner}/{repo}";
    }
}
