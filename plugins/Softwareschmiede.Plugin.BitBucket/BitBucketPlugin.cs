using Microsoft.Extensions.Logging;
using System.Text.Json;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using System.Reflection;

namespace Softwareschmiede.Infrastructure.Plugins;

public sealed class BitbucketPlugin : GitPluginBase<BitbucketPlugin>
{
    private const string BitbucketUserKey = "Softwareschmiede.Bitbucket.Username";
    private const string BitbucketAppPasswordKey = "Softwareschmiede.Bitbucket.AppPassword";
    private const string BitbucketWorkspaceKey = "Softwareschmiede.Bitbucket.Workspace";

    private const string RepositoryUrlKey = "RepositoryUrl";
    private const string RepositoryNameKey = "RepositoryName";

    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<BitbucketPlugin> _logger;

    public override string PluginName => "Bitbucket";
    public override string PluginPrefix => "Softwareschmiede.Bitbucket";
    public override PluginType PluginType => PluginType.SourceCodeManagement;

    public BitbucketPlugin(ICliRunner cliRunner, ICredentialStore credentialStore, ILogger<BitbucketPlugin> logger)
        : base(cliRunner)
    {
        _cliRunner = cliRunner;
        _credentialStore = credentialStore;
        _logger = logger;
    }

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
        ])

    ];

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

            var netrcContent = $@"machine bitbucket.org
login {user}
password {appPassword}
";

            File.WriteAllText(netrcPath, netrcContent);
        }

        return env;
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

    public override async Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Klone Bitbucket-Repository {Url} nach {TargetPath}.", repositoryUrl, targetPath);

        var user = _credentialStore.GetCredential(BitbucketUserKey);
        var appPassword = _credentialStore.GetCredential(BitbucketAppPasswordKey);

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(appPassword))
            throw new InvalidOperationException("Bitbucket-Authentifizierung fehlt.");

        var cloneUrl = BuildAuthenticatedCloneUrl(repositoryUrl, user, appPassword);

        var result = await _cliRunner.RunAsync(
            "git",
            ["clone", cloneUrl, targetPath],
            null,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"git clone fehlgeschlagen: {result.StdErr}");
    }

    public override async Task PullAsync(string localPath, CancellationToken ct = default)
    {
        var result = await _cliRunner.RunAsync(
            "git",
            ["pull"],
            localPath,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"git pull fehlgeschlagen: {result.StdErr}");
    }

    public override async Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        var result = await _cliRunner.RunAsync(
            "git",
            ["push", "--set-upstream", "origin", branchName],
            localPath,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"git push fehlgeschlagen: {result.StdErr}");
    }

    public override async Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default)
    {
        var jiraUrl = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraUrl");
        var jiraProject = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraProjectKey");
        var jiraEmail = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraEmail");
        var jiraToken = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraApiToken");

        var jql = $"project={jiraProject} ORDER BY created DESC";
        var apiUrl = $"{jiraUrl}/rest/api/3/search?jql={Uri.EscapeDataString(jql)}";

        var result = await _cliRunner.RunAsync(
            "curl",
            ["-s", "-u", $"{jiraEmail}:{jiraToken}", "-H", "Accept: application/json", apiUrl],
            null,
            null,
            ct);

        if (!result.IsSuccess)
            return [];

        return ParseJiraIssues(result.StdOut);
    }

    private static IEnumerable<Issue> ParseJiraIssues(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<Issue>();

        if (!doc.RootElement.TryGetProperty("issues", out var issues))
            return list;

        foreach (var el in issues.EnumerateArray())
        {
            var key = el.GetProperty("key").GetString() ?? "";
            var fields = el.GetProperty("fields");

            var summary = fields.GetProperty("summary").GetString() ?? "";
            var description = fields.TryGetProperty("description", out var desc)
                ? desc.GetProperty("content").ToString()
                : null;

            var labels = fields.TryGetProperty("labels", out var lbl)
                ? lbl.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                : new List<string>();

            list.Add(new Issue(
                Nummer: 0, // Jira hat keine numerischen IDs
                Titel: $"{key}: {summary}",
                Body: description,
                Labels: labels,
                Milestone: null,
                IssueUrl: null
            ));
        }

        return list;
    }

    public override async Task<PullRequest> CreatePullRequestAsync(
        string repositoryId,
        string branchName,
        string title,
        string body,
        CancellationToken ct = default)
    {
        var user = _credentialStore.GetCredential(BitbucketUserKey);
        var appPassword = _credentialStore.GetCredential(BitbucketAppPasswordKey);

        var apiUrl = $"https://api.bitbucket.org/2.0/repositories/{repositoryId}/pullrequests";

        var payload = JsonSerializer.Serialize(new
        {
            title,
            description = body,
            source = new { branch = new { name = branchName } },
            destination = new { branch = new { name = "main" } }
        });

        var result = await _cliRunner.RunAsync(
            "curl",
            ["-s", "-u", $"{user}:{appPassword}", "-H", "Content-Type: application/json", "-d", payload, apiUrl],
            null,
            null,
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"PR-Erstellung fehlgeschlagen: {result.StdErr}");

        using var doc = JsonDocument.Parse(result.StdOut);
        var id = doc.RootElement.GetProperty("id").GetInt32();
        var link = doc.RootElement.GetProperty("links").GetProperty("html").GetProperty("href").GetString() ?? "";

        return new PullRequest(id, title, link, branchName);
    }

    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        var bbUser = _credentialStore.GetCredential(BitbucketUserKey);
        var bbPass = _credentialStore.GetCredential(BitbucketAppPasswordKey);

        var jiraEmail = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraEmail");
        var jiraToken = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraApiToken");
        var jiraUrl = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.JiraUrl");

        var bb = await _cliRunner.RunAsync("curl", ["-s", "-u", $"{bbUser}:{bbPass}", "https://api.bitbucket.org/2.0/user"], null, null, ct);
        var jira = await _cliRunner.RunAsync("curl", ["-s", "-u", $"{jiraEmail}:{jiraToken}", $"{jiraUrl}/rest/api/3/myself"], null, null, ct);

        return bb.IsSuccess && !bb.StdOut.Contains("error")
            && jira.IsSuccess && !jira.StdOut.Contains("error");
    }


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

    public override async Task<IEnumerable<AvailableRepository>> GetAvailableRepositoriesAsync(CancellationToken ct = default)
    {
        var user = _credentialStore.GetCredential(BitbucketUserKey);
        var appPassword = _credentialStore.GetCredential(BitbucketAppPasswordKey);
        var workspace = _credentialStore.GetCredential(BitbucketWorkspaceKey);

        if (string.IsNullOrWhiteSpace(workspace))
            return [];

        var apiUrl = $"https://api.bitbucket.org/2.0/repositories/{workspace}?pagelen=100";

        var result = await _cliRunner.RunAsync(
            "curl",
            ["-s", "-u", $"{user}:{appPassword}", apiUrl],
            null,
            null,
            ct);

        if (!result.IsSuccess)
            return [];

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);

            if (!doc.RootElement.TryGetProperty("values", out var values))
                return [];

            return values.EnumerateArray()
                .Select(e => new AvailableRepository(
                    Name: e.GetProperty("name").GetString() ?? string.Empty,
                    UpdatedAt:
                        e.TryGetProperty("updated_on", out var updated)
                            ? updated.GetDateTime()
                            : e.TryGetProperty("created_on", out var created)
                                ? created.GetDateTime()
                                : DateTime.MinValue,
                    NameWithOwner: e.GetProperty("full_name").GetString() ?? string.Empty,
                    Url: e.GetProperty("links").GetProperty("html").GetProperty("href").GetString() ?? string.Empty
                ))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

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
            var line = result.StdOut.Split('\n').FirstOrDefault() ?? "";
            if (line.StartsWith("ref: refs/heads/"))
                return line.Replace("ref: refs/heads/", "").Split('\t')[0];
        }

        return "main";
    }
}
