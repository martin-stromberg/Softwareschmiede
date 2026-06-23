using Microsoft.Extensions.Logging;
using System.Text.Json;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>SCM-Plugin für Bitbucket Cloud und Bitbucket Server/Data Center (Self-Hosted).</summary>
public sealed class BitbucketPlugin : GitPluginBase<BitbucketPlugin>
{
    private const string BitbucketUserKey = "Softwareschmiede.Bitbucket.Username";
    private const string BitbucketAppPasswordKey = "Softwareschmiede.Bitbucket.AppPassword";
    private const string BitbucketWorkspaceKey = "Softwareschmiede.Bitbucket.Workspace";
    private const string BitbucketHostingModeKey = "Softwareschmiede.Bitbucket.HostingMode";
    private const string BitbucketSelfHostedUrlKey = "Softwareschmiede.Bitbucket.SelfHostedUrl";

    private const string RepositoryUrlKey = "RepositoryUrl";
    private const string RepositoryNameKey = "RepositoryName";

    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<BitbucketPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "Bitbucket";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.Bitbucket";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.SourceCodeManagement;

    /// <inheritdoc cref="BitbucketPlugin"/>
    public BitbucketPlugin(ICliRunner cliRunner, ICredentialStore credentialStore, ILogger<BitbucketPlugin> logger)
        : base(cliRunner)
    {
        _cliRunner = cliRunner;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Authentifizierung",
        [
            new PluginSettingField(
                Key: "Username",
                Label: "Bitbucket Username",
                FieldType: PluginSettingFieldType.Text,
                Placeholder: "martin@example.com",
                Description: "Bitbucket Login-Name oder E-Mail.",
                IsRequired: true),

            new PluginSettingField(
                Key: "AppPassword",
                Label: "App Password (Token)",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "xxxx-xxxx-xxxx",
                Description: "Bitbucket App Password (Token) mit Rechten: repository:read, repository:write, pullrequest.",
                IsRequired: true),

            new PluginSettingField(
                Key: "Workspace",
                Label: "Bitbucket Workspace",
                FieldType: PluginSettingFieldType.Text,
                Placeholder: "martin-stromberg",
                Description: "Workspace-Name aus der Repository-URL.",
                IsRequired: true)
        ]),
        new PluginSettingGroup("Jira",
        [
            new PluginSettingField(
                Key: "JiraUrl",
                Label: "Jira Base URL",
                FieldType: PluginSettingFieldType.Url,
                Placeholder: "https://softwareschmiede.atlassian.net",
                Description: "Basis-URL deiner Jira Cloud Instanz.",
                IsRequired: true),

            new PluginSettingField(
                Key: "JiraProjectKey",
                Label: "Jira Project Key",
                FieldType: PluginSettingFieldType.Text,
                Placeholder: "MYAPP",
                Description: "Projekt-Key des verknüpften Jira-Projekts.",
                IsRequired: true),

            new PluginSettingField(
                Key: "JiraEmail",
                Label: "Jira Login E-Mail",
                FieldType: PluginSettingFieldType.Text,
                Placeholder: "martin@example.com",
                Description: "E-Mail für Jira API Login.",
                IsRequired: true),

            new PluginSettingField(
                Key: "JiraApiToken",
                Label: "Jira API Token",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "xxxx-xxxx",
                Description: "Jira API Token (nicht Bitbucket App Password!).",
                IsRequired: true)
        ]),
        new PluginSettingGroup("BitBucket-Hosting",
        [
            new PluginSettingField(
                Key: "HostingMode",
                Label: "Hosting-Modus",
                FieldType: PluginSettingFieldType.Enum,
                Placeholder: "Cloud",
                Description: "Cloud nutzt api.bitbucket.org, Self-Hosted eine eigene URL.",
                IsRequired: true,
                EnumOptions: ["Cloud", "SelfHosted"]),

            new PluginSettingField(
                Key: "SelfHostedUrl",
                Label: "BitBucket URL (Self-Hosted)",
                FieldType: PluginSettingFieldType.Url,
                Placeholder: "https://bitbucket.example.com",
                Description: "Nur erforderlich wenn Hosting-Modus auf Self-Hosted gesetzt. Basis-URL ohne Pfad.",
                IsRequired: false)
        ])

    ];

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingField> GetRepositoryLinkFields() =>
    [
        new PluginSettingField(
            Key: RepositoryUrlKey,
            Label: "Repository-URL",
            FieldType: PluginSettingFieldType.Url,
            Placeholder: "https://bitbucket.org/workspace/repo",
            Description: "Vollständige URL des Bitbucket-Repositories.",
            IsRequired: true),

        new PluginSettingField(
            Key: RepositoryNameKey,
            Label: "Repository-Name",
            FieldType: PluginSettingFieldType.Text,
            Placeholder: "workspace/repo",
            Description: "Repository-ID für API-Aufrufe.",
            IsRequired: true)
    ];

    private IDictionary<string, string> GetGitEnvironment()
    {
        var user = _credentialStore.GetCredential(BitbucketUserKey);
        var appPassword = _credentialStore.GetCredential(BitbucketAppPasswordKey);

        var env = new Dictionary<string, string>
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GIT_SSH_COMMAND"] = "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null"
        };

        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(appPassword))
        {
            var netrcPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows() ? "_netrc" : ".netrc");

            string host;
            var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";
            if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
            {
                var selfHostedUrl = _credentialStore.GetCredential(BitbucketSelfHostedUrlKey);
                if (string.IsNullOrWhiteSpace(selfHostedUrl))
                    throw new InvalidOperationException("Self-Hosted URL ist nicht konfiguriert. Bitte die BitBucket-Einstellungen überprüfen.");
                host = new Uri(selfHostedUrl).Host;
            }
            else
            {
                host = "bitbucket.org";
            }

            UpdateNetrcEntry(netrcPath, host, user, appPassword);
        }

        return env;
    }

    private string[] GetGitHttpAuthArgs()
    {
        var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";
        if (!hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
            return [];

        var user = _credentialStore.GetCredential(BitbucketUserKey) ?? string.Empty;
        var appPassword = _credentialStore.GetCredential(BitbucketAppPasswordKey) ?? string.Empty;
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user}:{appPassword}"));

        // credential.helper="" deaktiviert GCM/Keychain, damit der Basic-Auth-Header greift.
        return ["-c", "credential.helper=", "-c", $"http.extraheader=Authorization: Basic {encoded}"];
    }

    private string[] GetJiraCurlAuthArgs()
    {
        var email = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraEmail") ?? string.Empty;
        var token = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraApiToken") ?? string.Empty;
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{email}:{token}"));
        return ["-H", $"Authorization: Basic {encoded}"];
    }

    private string[] GetCurlAuthArgs()
    {
        var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";
        var token = _credentialStore.GetCredential(BitbucketAppPasswordKey) ?? string.Empty;

        if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
            return ["-H", $"Authorization: Bearer {token}"];

        var user = _credentialStore.GetCredential(BitbucketUserKey) ?? string.Empty;
        return ["-u", $"{user}:{token}"];
    }

    private static string BuildAuthenticatedCloneUrl(string repositoryUrl, string user, string appPassword)
    {
        var uri = new Uri(repositoryUrl);
        var builder = new UriBuilder(uri)
        {
            UserName = Uri.EscapeDataString(user),
            Password = Uri.EscapeDataString(appPassword)
        };
        return builder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Wandelt eine Bitbucket-Server-URL (Browser oder API) in eine Git-HTTP-Clone-URL um.
    /// Browser:  https://host/projects/KEY/repos/SLUG/browse  → https://host/scm/KEY/SLUG.git
    /// API:      https://host/rest/api/1.0/projects/KEY/repos/SLUG → https://host/scm/KEY/SLUG.git
    /// SCM:      https://host/scm/KEY/SLUG.git → unverändert
    /// </summary>
    internal static string ResolveGitCloneUrl(string repositoryUrl)
    {
        var uri = new Uri(repositoryUrl);
        var path = uri.AbsolutePath.TrimEnd('/');
        var baseUrl = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}";

        // Bereits eine SCM-URL
        if (path.StartsWith("/scm/", StringComparison.OrdinalIgnoreCase))
            return repositoryUrl.TrimEnd('/').EndsWith(".git") ? repositoryUrl : repositoryUrl.TrimEnd('/') + ".git";

        // Browser-URL: /projects/KEY/repos/SLUG[/...]
        // API-URL:     /rest/api/1.0/projects/KEY/repos/SLUG[/...]
        var patterns = new[]
        {
            @"/projects/([^/]+)/repos/([^/]+)",
            @"/rest/api/\d+\.\d+/projects/([^/]+)/repos/([^/]+)"
        };

        foreach (var pattern in patterns)
        {
            var m = System.Text.RegularExpressions.Regex.Match(path, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
                return $"{baseUrl}/scm/{m.Groups[1].Value}/{m.Groups[2].Value}.git";
        }

        // Fallback: URL unverändert zurückgeben
        return repositoryUrl;
    }

    /// <inheritdoc/>
    public override async Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Klone Bitbucket-Repository {Url} nach {TargetPath}.", repositoryUrl, targetPath);

        var user = _credentialStore.GetCredential(BitbucketUserKey);
        var appPassword = _credentialStore.GetCredential(BitbucketAppPasswordKey);

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(appPassword))
            throw new InvalidOperationException("Bitbucket-Authentifizierung fehlt.");

        var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";

        // Browser-URL in Git-SCM-Clone-URL umwandeln, dann Credentials einbetten.
        var resolvedUrl = hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase)
            ? ResolveGitCloneUrl(repositoryUrl)
            : repositoryUrl;

        var cloneUrl = BuildAuthenticatedCloneUrl(resolvedUrl, user, appPassword);

        var result = await _cliRunner.RunAsync(
            "git",
            ["clone", cloneUrl, targetPath],
            null,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"git clone fehlgeschlagen: {result.StdErr}");
    }

    /// <inheritdoc/>
    public override async Task PullAsync(string localPath, CancellationToken ct = default)
    {
        var result = await _cliRunner.RunAsync(
            "git",
            [..GetGitHttpAuthArgs(), "pull"],
            localPath,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"git pull fehlgeschlagen: {result.StdErr}");
    }

    /// <inheritdoc/>
    public override async Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        var result = await _cliRunner.RunAsync(
            "git",
            [..GetGitHttpAuthArgs(), "push", "--set-upstream", "origin", branchName],
            localPath,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"git push fehlgeschlagen: {result.StdErr}");
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default)
    {
        var jiraProject = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraProjectKey");
        var jiraUrl = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraUrl");

        if (string.IsNullOrWhiteSpace(jiraUrl))
            return [];

        var jql = $"project={jiraProject} ORDER BY created DESC";
        var apiUrl = $"{jiraUrl.TrimEnd('/')}/rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&fields=key&fields=summary&fields=description&fields=labels&fields=status&maxResults=100";

        var result = await _cliRunner.RunAsync(
            "curl",
            ["-s", ..GetJiraCurlAuthArgs(), "-H", "Accept: application/json", apiUrl],
            null,
            null,
            ct);

        if (!result.IsSuccess)
            return [];

        _logger.LogDebug("Jira Issues Response: {Response}", result.StdOut);

        using var doc = JsonDocument.Parse(result.StdOut);
        if (doc.RootElement.TryGetProperty("errorMessages", out var errorMessages) &&
            errorMessages.ValueKind == JsonValueKind.Array &&
            errorMessages.GetArrayLength() > 0)
        {
            var msg = errorMessages.EnumerateArray().First().GetString() ?? "Unbekannter Fehler";
            _logger.LogWarning("Jira API-Fehler beim Laden der Issues: {Error}", msg);
            return [];
        }

        return ParseJiraIssues(doc.RootElement);
    }

    private static IEnumerable<Issue> ParseJiraIssues(JsonElement root)
    {
        var list = new List<Issue>();

        if (!root.TryGetProperty("issues", out var issues))
            return list;

        foreach (var el in issues.EnumerateArray())
        {
            var key = el.TryGetProperty("key", out var keyEl) ? keyEl.GetString() ?? "" : "";
            var issueUrl = el.TryGetProperty("self", out var selfEl) ? selfEl.GetString() : null;

            if (!el.TryGetProperty("fields", out var fields))
                continue;

            var summary = fields.TryGetProperty("summary", out var summaryEl) ? summaryEl.GetString() ?? "" : "";
            var description = fields.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.Object
                ? RenderAdf(desc)
                : null;

            var labels = fields.TryGetProperty("labels", out var lbl)
                ? lbl.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                : new List<string>();

            list.Add(new Issue(
                Nummer: 0,
                Titel: $"{key}: {summary}",
                Body: description,
                Labels: labels,
                Milestone: null,
                IssueUrl: issueUrl
            ));
        }

        return list;
    }

    private static string RenderAdf(JsonElement node)
    {
        var sb = new System.Text.StringBuilder();
        RenderAdfNode(node, sb);
        return sb.ToString().Trim();
    }

    private static void RenderAdfNode(JsonElement node, System.Text.StringBuilder sb)
    {
        if (!node.TryGetProperty("type", out var typeEl))
            return;

        var type = typeEl.GetString() ?? "";

        switch (type)
        {
            case "text":
                if (node.TryGetProperty("text", out var text))
                    sb.Append(text.GetString());
                break;

            case "hardBreak":
                sb.AppendLine();
                break;

            case "paragraph":
                RenderAdfChildren(node, sb);
                sb.AppendLine();
                break;

            case "bulletList":
            case "orderedList":
                RenderAdfChildren(node, sb);
                break;

            case "listItem":
                sb.Append("- ");
                RenderAdfChildren(node, sb);
                break;

            case "heading":
                RenderAdfChildren(node, sb);
                sb.AppendLine();
                break;

            case "codeBlock":
                RenderAdfChildren(node, sb);
                sb.AppendLine();
                break;

            default:
                RenderAdfChildren(node, sb);
                break;
        }
    }

    private static void RenderAdfChildren(JsonElement node, System.Text.StringBuilder sb)
    {
        if (!node.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return;
        foreach (var child in content.EnumerateArray())
            RenderAdfNode(child, sb);
    }

    /// <inheritdoc/>
    public override async Task<PullRequest> CreatePullRequestAsync(
        string repositoryId,
        string branchName,
        string title,
        string body,
        CancellationToken ct = default)
    {
        var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";
        string apiUrl;
        if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
        {
            var parts = repositoryId.Split('/', 2);
            var projectKey = parts.Length > 0 ? parts[0] : repositoryId;
            var repoSlug = parts.Length > 1 ? parts[1] : repositoryId;
            apiUrl = $"{GetBitbucketApiBaseUrl()}/rest/api/1.0/projects/{projectKey}/repos/{repoSlug}/pull-requests";
        }
        else
        {
            apiUrl = $"{GetBitbucketApiBaseUrl()}/2.0/repositories/{repositoryId}/pullrequests";
        }

        var repositoryUrl = BuildRepositoryCloneUrl(repositoryId, hostingMode);
        var defaultBranch = await GetDefaultBranchAsync(repositoryUrl, ct);

        var payload = JsonSerializer.Serialize(new
        {
            title,
            description = body,
            source = new { branch = new { name = branchName } },
            destination = new { branch = new { name = defaultBranch } }
        });

        var result = await _cliRunner.RunAsync(
            "curl",
            ["-s", ..GetCurlAuthArgs(), "-H", "Content-Type: application/json", "-d", payload, apiUrl],
            null,
            null,
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"PR-Erstellung fehlgeschlagen: {result.StdErr}");

        using var doc = JsonDocument.Parse(result.StdOut);
        var id = doc.RootElement.GetProperty("id").GetInt32();
        var links = doc.RootElement.GetProperty("links");
        string link;
        if (links.TryGetProperty("html", out var htmlLink))
        {
            link = htmlLink.GetProperty("href").GetString() ?? "";
        }
        else if (links.TryGetProperty("self", out var selfLinks) && selfLinks.ValueKind == JsonValueKind.Array)
        {
            var first = selfLinks.EnumerateArray().FirstOrDefault();
            link = first.ValueKind != JsonValueKind.Undefined
                ? first.GetProperty("href").GetString() ?? ""
                : "";
        }
        else
        {
            link = "";
        }

        return new PullRequest(id, title, link, branchName);
    }

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        var jiraUrl = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraUrl");

        var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";
        string bbApiUrl;
        if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
        {
            var selfHostedUrl = _credentialStore.GetCredential(BitbucketSelfHostedUrlKey);
            if (string.IsNullOrWhiteSpace(selfHostedUrl))
                throw new InvalidOperationException("Self-Hosted URL ist nicht konfiguriert.");
            bbApiUrl = $"{GetBitbucketApiBaseUrl()}/rest/api/1.0/user";
        }
        else
        {
            bbApiUrl = $"{GetBitbucketApiBaseUrl()}/2.0/user";
        }

        var bb = await _cliRunner.RunAsync("curl", ["-s", ..GetCurlAuthArgs(), bbApiUrl], null, null, ct);
        var bbOk = bb.IsSuccess && !HasBitbucketApiError(bb.StdOut);

        if (string.IsNullOrWhiteSpace(jiraUrl))
            return bbOk;

        var jira = await _cliRunner.RunAsync("curl", ["-s", ..GetJiraCurlAuthArgs(), $"{jiraUrl.TrimEnd('/')}/rest/api/3/myself"], null, null, ct);
        return bbOk && jira.IsSuccess && !HasBitbucketApiError(jira.StdOut);
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default)
    {
        var result = await _cliRunner.RunAsync(
            "git",
            ["ls-remote", "--heads", repositoryUrl],
            null,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
            return [];

        return result.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split('\t').Last().Replace("refs/heads/", ""))
            .OrderBy(x => x)
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<AvailableRepository>> GetAvailableRepositoriesAsync(CancellationToken ct = default)
    {
        var workspace = _credentialStore.GetCredential(BitbucketWorkspaceKey);

        if (string.IsNullOrWhiteSpace(workspace))
            return [];

        var apiUrl = $"{GetBitbucketApiBaseUrl()}{GetBitbucketRepositoriesPath(workspace)}";

        var result = await _cliRunner.RunAsync(
            "curl",
            ["-s", ..GetCurlAuthArgs(), apiUrl],
            null,
            null,
            ct);

        if (!result.IsSuccess)
            return [];

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);

            if (HasBitbucketErrors(doc.RootElement, out var errorMsg))
            {
                _logger.LogWarning("Bitbucket API-Fehler beim Laden der Repositories: {Error}", errorMsg);
                return [];
            }

            if (!doc.RootElement.TryGetProperty("values", out var values))
                return [];

            return values.EnumerateArray()
                .Select(e =>
                {
                    var name = e.GetProperty("name").GetString() ?? string.Empty;

                    string nameWithOwner;
                    if (e.TryGetProperty("full_name", out var fullName))
                    {
                        nameWithOwner = fullName.GetString() ?? string.Empty;
                    }
                    else
                    {
                        var projectKey = e.TryGetProperty("project", out var proj) && proj.TryGetProperty("key", out var key)
                            ? key.GetString() ?? string.Empty
                            : string.Empty;
                        var slug = e.TryGetProperty("slug", out var slugEl) ? slugEl.GetString() ?? name : name;
                        nameWithOwner = string.IsNullOrEmpty(projectKey) ? slug : $"{projectKey}/{slug}";
                    }

                    string url;
                    var repoLinks = e.GetProperty("links");
                    if (repoLinks.TryGetProperty("html", out var htmlEl))
                    {
                        url = htmlEl.GetProperty("href").GetString() ?? string.Empty;
                    }
                    else if (repoLinks.TryGetProperty("self", out var selfEl) && selfEl.ValueKind == JsonValueKind.Array)
                    {
                        var firstSelf = selfEl.EnumerateArray().FirstOrDefault();
                        url = firstSelf.ValueKind != JsonValueKind.Undefined
                            ? firstSelf.GetProperty("href").GetString() ?? string.Empty
                            : string.Empty;
                    }
                    else
                    {
                        url = string.Empty;
                    }

                    DateTime updatedAt =
                        e.TryGetProperty("updated_on", out var updated)
                            ? updated.GetDateTime()
                            : e.TryGetProperty("created_on", out var created)
                                ? created.GetDateTime()
                                : DateTime.MinValue;

                    return new AvailableRepository(
                        Name: name,
                        UpdatedAt: updatedAt,
                        NameWithOwner: nameWithOwner,
                        Url: url);
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public override async Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default)
    {
        var result = await _cliRunner.RunAsync(
            "git",
            ["ls-remote", "--symref", repositoryUrl, "HEAD"],
            null,
            GetGitEnvironment(),
            ct);

        if (result.IsSuccess)
        {
            var line = (result.StdOut.Split('\n').FirstOrDefault() ?? "").TrimEnd('\r', '\n');
            if (line.StartsWith("ref: refs/heads/"))
                return line.Replace("ref: refs/heads/", "").Split('\t')[0].TrimEnd('\r', '\n');
        }

        return "main";
    }

    private static void UpdateNetrcEntry(string netrcPath, string host, string user, string appPassword)
    {
        var existingContent = File.Exists(netrcPath) ? File.ReadAllText(netrcPath) : string.Empty;

        var newEntry = $"machine {host}{Environment.NewLine}login {user}{Environment.NewLine}password {appPassword}";

        var lines = existingContent.Split('\n');
        var result = new List<string>();
        var i = 0;
        var replaced = false;

        while (i < lines.Length)
        {
            var trimmed = lines[i].TrimEnd('\r');
            if (trimmed.Equals($"machine {host}", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(newEntry);
                replaced = true;
                i++;
                while (i < lines.Length && !lines[i].TrimEnd('\r').StartsWith("machine ", StringComparison.OrdinalIgnoreCase))
                    i++;
            }
            else
            {
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
                i++;
            }
        }

        if (!replaced)
            result.Add(newEntry);

        File.WriteAllText(netrcPath, string.Join(Environment.NewLine, result));
    }

    private string BuildRepositoryCloneUrl(string repositoryId, string hostingMode)
    {
        if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = GetBitbucketApiBaseUrl();
            var parts = repositoryId.Split('/', 2);
            var projectKey = parts.Length > 0 ? parts[0] : repositoryId;
            var repoSlug = parts.Length > 1 ? parts[1] : repositoryId;
            return $"{baseUrl}/scm/{projectKey}/{repoSlug}.git";
        }

        return $"https://bitbucket.org/{repositoryId}.git";
    }

    /// <summary>Gibt die API-Basis-URL zurück — Cloud oder Self-Hosted je nach Konfiguration.</summary>
    internal string GetBitbucketApiBaseUrl()
    {
        var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";

        if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
        {
            var selfHostedUrl = _credentialStore.GetCredential(BitbucketSelfHostedUrlKey);
            if (string.IsNullOrWhiteSpace(selfHostedUrl))
                throw new InvalidOperationException("Self-Hosted URL ist nicht konfiguriert.");
            return selfHostedUrl.TrimEnd('/');
        }

        return "https://api.bitbucket.org";
    }

    /// <summary>Gibt den API-Pfad für Repositories zurück — Cloud oder Self-Hosted je nach Konfiguration.</summary>
    internal string GetBitbucketRepositoriesPath(string workspace)
    {
        var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";

        if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
            return $"/rest/api/1.0/projects/{workspace}/repos";

        return $"/2.0/repositories/{workspace}?pagelen=100";
    }

    private static bool HasBitbucketErrors(JsonElement root, out string message)
    {
        if (root.TryGetProperty("errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array &&
            errors.GetArrayLength() > 0)
        {
            var first = errors.EnumerateArray().First();
            message = first.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Unbekannter Fehler"
                : "Unbekannter Fehler";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static bool HasBitbucketApiError(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return HasBitbucketErrors(doc.RootElement, out _);
        }
        catch
        {
            return false;
        }
    }
}
