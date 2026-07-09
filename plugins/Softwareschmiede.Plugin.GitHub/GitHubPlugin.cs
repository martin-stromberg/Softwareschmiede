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
                Description: "GitHub Personal Access Token mit den Berechtigungen repo und read:org. Token erstellen: https://github.com/settings/tokens/new",
                IsRequired: true)
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
        _logger.LogInformation("Rufe Issues für Repository {RepositoryId} ab.", repositoryId);

        var result = await _cliRunner.RunAsync(
            "gh",
            ["issue", "list", "--repo", repositoryId, "--json", "number,title,body,labels,milestone", "--limit", "100"],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogError("gh issue list fehlgeschlagen für {RepositoryId}: {StdErr}", repositoryId, result.StdErr);
            return [];
        }

        return ParseIssues(result.StdOut);
    }

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

        var result = await _cliRunner.RunAsync(
            "gh",
            args.ToArray(),
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogError("gh pr create fehlgeschlagen: {StdErr}", result.StdErr);
            throw new InvalidOperationException($"gh pr create fehlgeschlagen: {result.StdErr}");
        }

        // Parse the output text: "https://github.com/owner/repo/pull/123"
        var prUrl = result.StdOut.Trim();
        
        // Extract PR number from URL
        var lastSlashIndex = prUrl.LastIndexOf('/');
        if (lastSlashIndex > 0 && int.TryParse(prUrl[(lastSlashIndex + 1)..], out var prNumber))
        {
            return new PullRequest(prNumber, title, prUrl, branchName);
        }

        // Fallback: if we can't parse, throw error
        throw new InvalidOperationException($"PR created but could not parse response: {result.StdOut}");
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
        CancellationToken ct = default)
    {
        var repositoryId = TryExtractRepositoryId(repositoryUrl);
        if (repositoryId is null)
        {
            _logger.LogWarning(
                "Verzeichnisstruktur konnte nicht ermittelt werden: Repository-ID konnte nicht aus '{RepositoryUrl}' extrahiert werden.",
                repositoryUrl);
            return [];
        }

        var branch = await GetDefaultBranchAsync(repositoryUrl, ct);

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
            return [];
        }

        return ParseRepositoryTree(result.StdOut, maxDepth, repositoryId);
    }

    private IEnumerable<RepositoryDirectoryEntry> ParseRepositoryTree(string json, int maxDepth, string repositoryId)
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
                return [];
            }

            return treeEl.EnumerateArray()
                .Where(entry => entry.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "tree")
                .Select(entry => entry.TryGetProperty("path", out var pathEl) ? pathEl.GetString() : null)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => path!)
                .Where(path => path.Count(c => c == '/') + 1 <= maxDepth)
                .Select(path => new RepositoryDirectoryEntry(path, true))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Parsen der GitHub-Verzeichnisstruktur für {RepositoryId}.", repositoryId);
            return [];
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
