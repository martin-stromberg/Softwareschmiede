using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests für das GitHubPlugin.</summary>
public sealed class GitHubPluginTests
{
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly FakeGitHubEventApi _api;
    private readonly GitHubPlugin _sut;

    /// <summary>GitHubPluginTests.</summary>
    public GitHubPluginTests()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _api = new FakeGitHubEventApi();
        _sut = new GitHubPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            new Mock<ILogger<GitHubPlugin>>().Object,
            _api,
            null,
            CreateMissingNetrcPath());
    }

    /// <summary>Liefert einen garantiert nicht existierenden netrc-Pfad, damit die
    /// Konstruktor-Bereinigung im Test nicht die echte %USERPROFILE%\_netrc anfasst.</summary>
    internal static string CreateMissingNetrcPath()
        => Path.Combine(Path.GetTempPath(), $"sws-test-{Guid.NewGuid():N}", "_netrc");

    /// <summary>GetIssuesAsync parsed die REST-Antwort (inkl. html_url und PR-Filter) korrekt.</summary>
    [Fact]
    public async Task GetIssuesAsync_ShouldReturnParsedIssues_WhenCliSucceeds()
    {
        // Arrange
        const string json = """
            [
                {
                    "number": 1,
                    "title": "Bug Fix",
                    "body": "Ein Fehler wurde gefunden.",
                    "labels": [{"name": "bug"}, {"name": "urgent"}],
                    "milestone": {"title": "v1.0"},
                    "html_url": "https://github.com/owner/repo/issues/1"
                },
                {
                    "number": 3,
                    "title": "Pull Request",
                    "pull_request": {"url": "https://api.github.com/repos/owner/repo/pulls/3"},
                    "html_url": "https://github.com/owner/repo/pull/3"
                },
                {
                    "number": 2,
                    "title": "Feature Request",
                    "body": null,
                    "labels": [],
                    "milestone": null,
                    "html_url": "https://github.com/owner/repo/issues/2"
                }
            ]
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("test-token");
        _api.AddJson("issues?state=open", json);

        // Act
        var result = (await _sut.GetIssuesAsync("owner/repo", TestContext.Current.CancellationToken)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Number.Should().Be(1);
        result[0].Title.Should().Be("Bug Fix");
        result[0].Labels.Should().BeEquivalentTo(new[] { "bug", "urgent" });
        result[0].Milestone.Should().Be("v1.0");
        result[1].Number.Should().Be(2);
        result[1].Body.Should().BeNull();
    }

    /// <summary>GetIssuesAsync gibt leere Liste zurück wenn der API-Aufruf fehlschlägt.</summary>
    [Fact]
    public async Task GetIssuesAsync_ShouldReturnEmptyList_WhenCliFails()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);
        _api.AddStatus("issues?state=open", 401);

        // Act
        var result = await _sut.GetIssuesAsync("owner/repo", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>GetAlertsAsync lädt GitHub-Code-Scanning-Alerts und mappt die relevanten Felder.</summary>
    [Fact]
    public async Task GetAlertsAsync_ShouldReturnMappedCodeScanningAlerts_WhenCliSucceeds()
    {
        const string json = """
            [
              {
                "number": 7,
                "state": "open",
                "html_url": "https://github.com/owner/repo/security/code-scanning/7",
                "rule": {
                  "id": "cs/sql-injection",
                  "name": "SQL injection",
                  "description": "User input reaches SQL query.",
                  "security_severity_level": "high",
                  "severity": "warning"
                },
                "tool": { "name": "CodeQL" },
                "most_recent_instance": {
                  "location": { "path": "src/Login.cs", "start_line": 42 },
                  "message": { "text": "Unsanitized input" }
                }
              }
            ]
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("code-scanning/alerts?state=open", json);

        var result = (await _sut.GetAlertsAsync("https://github.com/owner/repo.git", TestContext.Current.CancellationToken)).ToList();

        result.Should().ContainSingle();
        result[0].SourceKey.Should().Be("github:code-scanning:owner/repo:7");
        result[0].Title.Should().Be("SQL injection");
        result[0].Description.Should().Be("Unsanitized input");
        result[0].Severity.Should().Be("high");
        result[0].ToolName.Should().Be("CodeQL");
        result[0].FilePath.Should().Be("src/Login.cs");
        result[0].StartLine.Should().Be(42);
    }

    /// <summary>GetAlertsAsync verarbeitet paginierte REST-Antworten aus mehreren Seiten.</summary>
    [Fact]
    public async Task GetAlertsAsync_ShouldReturnAlertsFromAllPaginatedPages()
    {
        const string page1Json = """
            [
              {
                "number": 1,
                "state": "open",
                "rule": { "id": "rule/one", "name": "Rule one" },
                "tool": { "name": "CodeQL" }
              }
            ]
            """;
        const string page2Json = """
            [
              {
                "number": 2,
                "state": "open",
                "rule": { "id": "rule/two", "name": "Rule two" },
                "tool": { "name": "CodeQL" }
              }
            ]
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("code-scanning/alerts?state=open", page1Json,
            new() { ["Link"] = "<https://api.github.com/repos/owner/repo/code-scanning/alerts?state=open&per_page=100&page=2>; rel=\"next\"" });
        _api.AddJson("page=2", page2Json);

        var result = (await _sut.GetAlertsAsync("owner/repo", TestContext.Current.CancellationToken)).ToList();

        result.Select(a => a.AlertNumber).Should().Equal(1, 2);
        result.Select(a => a.SourceKey).Should().Equal(
            "github:code-scanning:owner/repo:1",
            "github:code-scanning:owner/repo:2");
    }

    /// <summary>GetAlertsAsync behandelt fehlendes Code Scanning als leere Liste.</summary>
    [Fact]
    public async Task GetAlertsAsync_ShouldReturnEmpty_WhenCodeScanningIsNotAvailable()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddStatus("code-scanning/alerts", 404);

        var result = await _sut.GetAlertsAsync("owner/repo", TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    /// <summary>GetAlertsAsync gibt bei fehlenden Rechten eine leere Liste zurück und maskiert Tokens in Logs.</summary>
    [Fact]
    public async Task GetAlertsAsync_ShouldReturnEmpty_WhenPermissionsAreMissing()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("secret-token");
        _api.AddStatus("code-scanning/alerts", 403,
            new() { ["X-RateLimit-Remaining"] = "5000" });

        var result = await _sut.GetAlertsAsync("owner/repo", TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    /// <summary>GetAlertsAsync propagiert Cancellation statt daraus eine leere Liste zu machen.</summary>
    [Fact]
    public async Task GetAlertsAsync_ShouldPropagateCancellation_WhenCliIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddFailure("code-scanning/alerts", new OperationCanceledException(cts.Token));

        var act = () => _sut.GetAlertsAsync("owner/repo", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>CreateIssueAsync ruft gh issue create auf und mappt die URL auf eine Issue-Referenz.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldReturnIssue_WhenCliSucceeds()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "issue", "create", "--repo", "owner/repo", "--title", "Titel", "--body", "Body")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo/issues/17", string.Empty));

        var result = await _sut.CreateIssueAsync("owner/repo", new IssueCreateRequest("Titel", "Body"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Issue!.Number.Should().Be(17);
        result.Issue.Title.Should().Be("Titel");
        result.Issue.Body.Should().Be("Body");
        result.Issue.IssueUrl.Should().Be("https://github.com/owner/repo/issues/17");
    }

    /// <summary>CreateIssueAsync validiert fehlende Repository-ID ohne CLI-Aufruf.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldReturnFailed_WhenRepositoryIdIsMissing()
    {
        var result = await _sut.CreateIssueAsync(" ", new IssueCreateRequest("Titel", "Body"), TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueCreateResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Repository-ID");
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>CreateIssueAsync validiert leere Titel ohne CLI-Aufruf.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldReturnFailed_WhenTitleIsMissing()
    {
        var result = await _sut.CreateIssueAsync("owner/repo", new IssueCreateRequest(" ", "Body"), TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueCreateResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Titel");
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>CreateIssueAsync liefert bei CLI-Fehler ein Fehlerergebnis ohne Issue.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldReturnFailed_WhenCliFails()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(a => a.Contains("create")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "permission denied"));

        var result = await _sut.CreateIssueAsync("owner/repo", new IssueCreateRequest("Titel", "Body"), TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueCreateResultStatus.Failed);
        result.Issue.Should().BeNull();
        result.ErrorMessage.Should().Contain("permission denied");
    }

    /// <summary>CreateIssueAsync propagiert Cancellation statt daraus einen Providerfehler zu machen.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldPropagateCancellation_WhenCliIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(a => a.Contains("create")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => _sut.CreateIssueAsync("owner/repo", new IssueCreateRequest("Titel", "Body"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>GetOpenPullRequestsAsync normalisiert gespeicherte GitHub-URLs zu owner/repo.</summary>
    [Fact]
    public async Task GetOpenPullRequestsAsync_ShouldNormalizeRepositoryUrl()
    {
        const string json = """
            [
              {
                "number": 42,
                "title": "Bump package from 1.0.0 to 1.0.1",
                "body": "Dependabot updates the package dependency.",
                "html_url": "https://github.com/owner/repo/pull/42",
                "state": "open",
                "node_id": "PR_kwDOExample",
                "head": {
                  "ref": "dependabot/nuget/package-1.0.1",
                  "sha": "abc123",
                  "repo": {
                    "full_name": "owner/repo",
                    "clone_url": "https://github.com/owner/repo.git"
                  }
                },
                "base": {
                  "ref": "main"
                }
              }
            ]
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("pulls?state=open", json);

        var result = (await _sut.GetOpenPullRequestsAsync("https://github.com/owner/repo.git", TestContext.Current.CancellationToken)).ToList();

        result.Should().ContainSingle();
        result[0].RepositoryId.Should().Be("owner/repo");
        result[0].Number.Should().Be(42);
        result[0].BranchName.Should().Be("dependabot/nuget/package-1.0.1");
        result[0].TargetBranch.Should().Be("main");
        result[0].Body.Should().Be("Dependabot updates the package dependency.");
    }

    /// <summary>GetOpenPullRequestsAsync gibt bei unvollstaendiger Repository-ID leer zurueck.</summary>
    [Fact]
    public async Task GetOpenPullRequestsAsync_ShouldReturnEmpty_WhenRepositoryIdIsIncomplete()
    {
        var result = await _sut.GetOpenPullRequestsAsync("repo", TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>GetIssueTemplatesAsync lädt Markdown-Templates aus .github/ISSUE_TEMPLATE.</summary>
    [Fact]
    public async Task GetIssueTemplatesAsync_ShouldReturnTemplates_WhenRepositoryContainsTemplates()
    {
        var listJson = """
            [
              {"type":"file","name":"bug.md","path":".github/ISSUE_TEMPLATE/bug.md"},
              {"type":"dir","name":"ignored","path":".github/ISSUE_TEMPLATE/ignored"}
            ]
            """;
        var contentJson = $$"""
            {"content":"{{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Template Body"))}}"}
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("contents/.github/ISSUE_TEMPLATE", listJson);
        _api.AddJson("contents/.github/ISSUE_TEMPLATE/bug.md", contentJson);

        var result = await _sut.GetIssueTemplatesAsync("owner/repo", TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueTemplateLoadResultStatus.Success);
        result.Templates.Should().ContainSingle()
            .Which.Should().Be(new IssueTemplate("bug.md", "Template Body"));
    }

    /// <summary>GetIssueTemplatesAsync kodiert Template-Dateinamen mit Sonderzeichen im Endpunkt.</summary>
    [Fact]
    public async Task GetIssueTemplatesAsync_ShouldEscapeTemplatePath_InEndpoint()
    {
        var listJson = """
            [
              {"type":"file","name":"bug#fix%.md","path":".github/ISSUE_TEMPLATE/bug#fix%.md"}
            ]
            """;
        var contentJson = $$"""
            {"content":"{{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Body"))}}"}
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("contents/.github/ISSUE_TEMPLATE", listJson);
        _api.AddJson("contents/.github/ISSUE_TEMPLATE/bug%23fix%25.md", contentJson);

        var result = await _sut.GetIssueTemplatesAsync("owner/repo", TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueTemplateLoadResultStatus.Success);
        result.Templates.Should().ContainSingle();
        _api.Requests.Should().Contain(r => r.Endpoint.Contains("bug%23fix%25.md"));
    }

    /// <summary>GetIssueTemplatesAsync normalisiert gespeicherte GitHub-URLs zu owner/repo.</summary>
    [Fact]
    public async Task GetIssueTemplatesAsync_ShouldNormalizeRepositoryUrl()
    {
        var listJson = """
            [
              {"type":"file","name":"bug.md","path":".github/ISSUE_TEMPLATE/bug.md"}
            ]
            """;
        var contentJson = $$"""
            {"content":"{{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Template Body"))}}"}
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("contents/.github/ISSUE_TEMPLATE", listJson);
        _api.AddJson("contents/.github/ISSUE_TEMPLATE/bug.md", contentJson);

        var result = await _sut.GetIssueTemplatesAsync("https://github.com/owner/repo.git", TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueTemplateLoadResultStatus.Success);
        result.Templates.Should().ContainSingle();
        _api.Requests.Select(r => r.Endpoint).Should().OnlyContain(path => path.Contains("repos/owner/repo/contents/", StringComparison.Ordinal));
    }

    /// <summary>GetIssueTemplatesAsync blendet GitHub Issue Forms und config.yml aus.</summary>
    [Fact]
    public async Task GetIssueTemplatesAsync_ShouldIgnoreYamlIssueFormsAndConfig()
    {
        var listJson = """
            [
              {"type":"file","name":"bug.md","path":".github/ISSUE_TEMPLATE/bug.md"},
              {"type":"file","name":"feature.yml","path":".github/ISSUE_TEMPLATE/feature.yml"},
              {"type":"file","name":"config.yml","path":".github/ISSUE_TEMPLATE/config.yml"},
              {"type":"file","name":"task.yaml","path":".github/ISSUE_TEMPLATE/task.yaml"}
            ]
            """;
        var contentJson = $$"""
            {"content":"{{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Markdown Template"))}}"}
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("contents/.github/ISSUE_TEMPLATE", listJson);
        _api.AddJson("contents/.github/ISSUE_TEMPLATE/bug.md", contentJson);

        var result = await _sut.GetIssueTemplatesAsync("owner/repo", TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueTemplateLoadResultStatus.Success);
        result.Templates.Should().ContainSingle()
            .Which.Should().Be(new IssueTemplate("bug.md", "Markdown Template"));
        _api.Requests.Select(r => r.Endpoint)
            .Where(path => path.Contains("/contents/.github/ISSUE_TEMPLATE/", StringComparison.Ordinal))
            .Should().ContainSingle()
            .Which.Should().EndWith("/contents/.github/ISSUE_TEMPLATE/bug.md");
    }

    /// <summary>GetIssueTemplatesAsync behandelt fehlende Template-Verzeichnisse als leere Template-Liste.</summary>
    [Fact]
    public async Task GetIssueTemplatesAsync_ShouldReturnEmpty_WhenTemplateDirectoryDoesNotExist()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddStatus("contents/.github/ISSUE_TEMPLATE", 404);

        var result = await _sut.GetIssueTemplatesAsync("owner/repo", TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueTemplateLoadResultStatus.Success);
        result.Templates.Should().BeEmpty();
    }

    /// <summary>GetIssueTemplatesAsync unterscheidet echte Providerfehler von fehlenden Templates.</summary>
    [Fact]
    public async Task GetIssueTemplatesAsync_ShouldReturnFailed_WhenTemplateApiFailsWithProviderError()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddStatus("contents/.github/ISSUE_TEMPLATE", 403);

        var result = await _sut.GetIssueTemplatesAsync("owner/repo", TestContext.Current.CancellationToken);

        result.Status.Should().Be(IssueTemplateLoadResultStatus.Failed);
        result.Templates.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("HTTP 403");
    }

    /// <summary>CloneRepositoryAsync ruft git clone mit korrekten Argumenten auf.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        // Act
        await _sut.CloneRepositoryAsync("https://github.com/test/repo", "/target/path", TestContext.Current.CancellationToken);

        // Assert
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Contains("https://github.com/test/repo") && a.Contains("/target/path")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && env.ContainsKey("GH_TOKEN") && env["GH_TOKEN"] == "token"
                && HasGitHubExtraHeader(env, "token", "http.https://github.com/.extraheader") && !env.ContainsKey("NETRC") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CloneRepositoryAsync bettet den Token nicht mehr in die Clone-URL ein.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ShouldNotEmbedToken_InCloneUrl()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("secret-token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        // Act
        await _sut.CloneRepositoryAsync("https://github.com/test/repo", "/target/path", TestContext.Current.CancellationToken);

        // Assert
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone")
                && a.All(x => !x.Contains("oauth2:", StringComparison.Ordinal))
                && a.All(x => !x.Contains("secret-token", StringComparison.Ordinal))),
            null,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CloneRepositoryAsync bricht bei HTTPS ohne Token kontrolliert vor dem Clone ab.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ShouldFailEarly_WhenHttpsTokenIsMissing()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);

        // Act
        var act = () => _sut.CloneRepositoryAsync("https://github.com/owner/private-repo.git", "/target");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GitHub-Token fehlt*");
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>CloneRepositoryAsync wirft Exception wenn git clone fehlschlägt.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ShouldThrowInvalidOperationException_WhenCliFails()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("test-token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git", It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(128, string.Empty, "repository not found"));

        // Act
        var act = () => _sut.CloneRepositoryAsync("https://github.com/invalid/repo", "/target");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git clone fehlgeschlagen*");
    }

    /// <summary>CloneRepositoryAsync mappt Prompt/Auth-Fehler auf verständliche Auth-Meldung.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ShouldMapAuthenticationErrors_ToHelpfulMessage()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("bad-token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git", It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(128, string.Empty, "fatal: could not read Username for 'https://github.com': terminal prompts disabled"));

        // Act
        var act = () => _sut.CloneRepositoryAsync("https://github.com/owner/private-repo.git", "/target");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Authentifizierung fehlgeschlagen*")
            .WithMessage("*Token prüfen/neu setzen*");
    }

    /// <summary>CloneRepositoryAsync gibt bei Fehlern niemals den Klartext-Token zurück.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ShouldSanitizeToken_InThrownExceptionMessage()
    {
        // Arrange
        const string token = "my-very-secret-token";
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns(token);
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git", It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, $"fatal: remote: https://oauth2:{token}@github.com denied"));

        // Act
        Func<Task> act = () => _sut.CloneRepositoryAsync("https://github.com/owner/private-repo.git", "/target");

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().NotContain(token);
        exception.Which.Message.Should().Contain("oauth2:***@");
    }

    /// <summary>CreateBranchAsync ruft git checkout -b mit korrektem Branch-Namen auf.</summary>
    [Fact]
    public async Task CreateBranchAsync_ShouldCallGitCheckoutMinusB_WhenCalled()
    {
        // Arrange
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("checkout") && a.Contains("-b")),
                "/local/path",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        // Act
        await _sut.CreateBranchAsync("/local/path", "feature/my-branch", ct: TestContext.Current.CancellationToken);

        // Assert
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("-b") && a.Contains("feature/my-branch")),
            "/local/path",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CreatePullRequestAsync parsed die JSON-Antwort korrekt.</summary>
    [Fact]
    public async Task CreatePullRequestAsync_ShouldReturnParsedPullRequest_WhenCliSucceeds()
    {
        // Arrange
        const string prUrl = "https://github.com/owner/repo/pull/42";
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, prUrl, string.Empty));

        // Act
        var result = await _sut.CreatePullRequestAsync("owner/repo", "feature/branch", null, title: "My PR", body: "Body", ct: TestContext.Current.CancellationToken);

        // Assert
        result.Number.Should().Be(42);
        result.Title.Should().Be("My PR");
        result.Url.Should().Be(prUrl);
        result.BranchName.Should().Be("feature/branch");
    }

    /// <summary>CreatePullRequestAsync liefert eine verständliche Meldung, wenn kein Commit-Unterschied zum Zielbranch existiert.</summary>
    [Fact]
    public async Task CreatePullRequestAsync_ShouldExplainNoCommitsBetweenFailure()
    {
        // Arrange
        const string ghError = "pull request create failed: GraphQL: Head sha can't be blank. Base sha can't be blank, No commits between main and feature/branch";
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, ghError));

        // Act
        var act = () => _sut.CreatePullRequestAsync("owner/repo", "feature/branch", null, title: "My PR", body: "Body");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*enthält keine Commits gegenüber dem Zielbranch*");
    }

    /// <summary>CheckHealthAsync gibt true zurück wenn GET /user erfolgreich ist.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue_WhenGhAuthStatusSucceeds()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("user", """{"login":"owner","node_id":"U_1"}""");

        // Act
        var result = await _sut.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        _api.Requests.Should().ContainSingle()
            .Which.Should().Match<GitHubEventApiRequest>(r => r.Method == "GET" && r.Endpoint == "user" && r.Host == "github.com");
    }

    /// <summary>CheckHealthAsync gibt false zurück wenn GET /user fehlschlägt.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnFalse_WhenGhAuthStatusFails()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);
        _api.AddStatus("user", 401);

        // Act
        var result = await _sut.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>PluginMetadata_ShouldExposeExpectedValues.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginPrefix.Should().Be("Softwareschmiede.GitHub");
        var settingGroups = _sut.GetSettingGroups();
        settingGroups.Should().HaveCount(2);
        settingGroups.Single(g => g.GroupName == "Authentifizierung").Fields.Should()
            .ContainSingle(f => f.Key == "Token" && f.IsRequired);
        settingGroups.Single(g => g.GroupName == "Pull Requests").Fields.Select(f => f.Key).Should().Contain(
            "AutoCompletePullRequests",
            "PullRequestCompletionStrategy",
            "PullRequestMergeMethod",
            "AllowProtectedBranchBypass");
        _sut.GetRepositoryLinkFields().Should().ContainSingle(f => f.Key == "RepositoryUrl" && f.IsRequired);
        _sut.GetRepositoryLinkFields().Should().ContainSingle(f => f.Key == "RepositoryName" && f.IsRequired);
    }

    /// <summary>GetPullRequestWorkflowRunsAsync fragt bekannte SHAs gezielt per head_sha-Filter ab.</summary>
    [Fact]
    public async Task GetPullRequestWorkflowRunsAsync_ShouldScopeRunListByCommitSha()
    {
        const string headSha = "head123";
        const string mergeSha = "merge456";
        const string preMergeJson = """
            {
              "total_count": 1,
              "workflow_runs": [
                {
                  "id": 1,
                  "name": "Build",
                  "status": "completed",
                  "conclusion": "success",
                  "html_url": "https://github.com/owner/repo/actions/runs/1",
                  "head_sha": "head123",
                  "head_branch": "feature/pr",
                  "created_at": "2026-07-29T10:00:00Z",
                  "updated_at": "2026-07-29T10:05:00Z"
                }
              ]
            }
            """;
        const string postMergeJson = """
            {
              "total_count": 1,
              "workflow_runs": [
                {
                  "id": 2,
                  "name": "Deploy",
                  "status": "in_progress",
                  "conclusion": null,
                  "html_url": "https://github.com/owner/repo/actions/runs/2",
                  "head_sha": "merge456",
                  "head_branch": "main",
                  "created_at": "2026-07-29T10:06:00Z",
                  "updated_at": "2026-07-29T10:07:00Z"
                }
              ]
            }
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("actions/runs?head_sha=head123", preMergeJson);
        _api.AddJson("actions/runs?head_sha=merge456", postMergeJson);

        var result = await _sut.GetPullRequestWorkflowRunsAsync("owner/repo", 42, headSha, mergeSha, TestContext.Current.CancellationToken);

        result.Should().HaveCount(2);
        result.Should().Contain(r => r.ProviderRunId == "1" && r.Name == "Build" && !r.IsPostMerge);
        result.Should().Contain(r => r.ProviderRunId == "2" && r.IsPostMerge);
        _api.Requests.Should().HaveCount(2);
        _api.Requests[0].Endpoint.Should().Contain("head_sha=head123");
        _api.Requests[1].Endpoint.Should().Contain("head_sha=merge456");
        _api.Requests.Should().OnlyContain(r => r.Method == "GET" && r.Endpoint.Contains("repos/owner/repo/actions/runs"));
    }

    /// <summary>GetPullRequestWorkflowRunsAsync kombiniert Workflow-Name und GitHub-Anzeigetitel.</summary>
    [Fact]
    public async Task GetPullRequestWorkflowRunsAsync_ShouldCombineWorkflowNameAndDisplayTitle()
    {
        const string headSha = "head123";
        const string runsJson = """
            {
              "total_count": 1,
              "workflow_runs": [
                {
                  "id": 233,
                  "name": "Tests",
                  "display_title": "Missing Translation for Statement Draft Validation Results",
                  "status": "in_progress",
                  "conclusion": null,
                  "html_url": "https://github.com/owner/repo/actions/runs/233",
                  "head_sha": "head123",
                  "head_branch": "feature/pr",
                  "created_at": "2026-07-30T10:00:00Z",
                  "updated_at": "2026-07-30T10:01:00Z"
                }
              ]
            }
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("actions/runs?head_sha=head123", runsJson);

        var result = await _sut.GetPullRequestWorkflowRunsAsync("owner/repo", 42, headSha, ct: TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Tests: Missing Translation for Statement Draft Validation Results");
        result[0].Status.Should().Be(WorkflowRunStatus.InProgress);
    }

    /// <summary>GetPullRequestWorkflowRunsAsync unterscheidet gleichlautende Run-Titel ueber den Workflow-Namen.</summary>
    [Fact]
    public async Task GetPullRequestWorkflowRunsAsync_ShouldDistinguishSameDisplayTitleByWorkflowName()
    {
        const string mergeSha = "merge123";
        const string runsJson = """
            {
              "total_count": 2,
              "workflow_runs": [
                {
                  "id": 301,
                  "name": "Tests",
                  "display_title": "fix: Implement proper translation for validation messages and budget",
                  "status": "completed",
                  "conclusion": "success",
                  "html_url": "https://github.com/owner/repo/actions/runs/301",
                  "head_sha": "merge123",
                  "head_branch": "main",
                  "created_at": "2026-07-30T10:00:00Z",
                  "updated_at": "2026-07-30T10:01:00Z"
                },
                {
                  "id": 302,
                  "name": "Release",
                  "display_title": "fix: Implement proper translation for validation messages and budget",
                  "status": "completed",
                  "conclusion": "success",
                  "html_url": "https://github.com/owner/repo/actions/runs/302",
                  "head_sha": "merge123",
                  "head_branch": "main",
                  "created_at": "2026-07-30T10:02:00Z",
                  "updated_at": "2026-07-30T10:03:00Z"
                }
              ]
            }
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("actions/runs?head_sha=merge123", runsJson);

        var result = await _sut.GetPullRequestWorkflowRunsAsync("owner/repo", 42, mergeCommitSha: mergeSha, ct: TestContext.Current.CancellationToken);

        result.Select(r => r.Name).Should().BeEquivalentTo(
        [
            "Tests: fix: Implement proper translation for validation messages and budget",
            "Release: fix: Implement proper translation for validation messages and budget"
        ]);
    }

    /// <summary>ApprovalOnly liefert ein erfolgreiches, aber nicht gemergtes Abschlussresultat.</summary>
    [Fact]
    public async Task CompletePullRequestAsync_ShouldReturnNonMergedResult_WhenApprovalOnlySucceeds()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pr", "review", "42", "--repo", "owner/repo", "--approve")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "approved", string.Empty));

        var result = await _sut.CompletePullRequestAsync("owner/repo", 42, new PullRequestCompletionOptions(PullRequestCompletionStrategy.ApprovalOnly), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.PullRequestMerged.Should().BeFalse();
        result.MergeCommitSha.Should().BeNull();
        _cliRunnerMock.Verify(c => c.RunAsync(
            "gh",
            It.Is<IEnumerable<string>>(a => a.Contains("merge")),
            null,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>AutoMerge gilt erst nach echtem Merged-Status als gemergter Abschluss.</summary>
    [Fact]
    public async Task CompletePullRequestAsync_ShouldReturnNonMergedResult_WhenAutoMergeDoesNotMergeImmediately()
    {
        const string openStatusJson = """
            {
                "data": {
                    "repository": {
                        "pullRequest": {
                            "id": "PR_kw",
                            "number": 42,
                            "title": "PR",
                            "url": "https://github.com/owner/repo/pull/42",
                            "state": "OPEN",
                            "headRefName": "feature/pr",
                            "baseRefName": "main",
                            "headRefOid": "head",
                            "mergeCommit": null,
                            "mergeStateStatus": "CLEAN"
                        }
                    }
                }
            }
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pr", "merge", "42", "--repo", "owner/repo", "--squash", "--auto")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Auto-merge enabled", string.Empty));
        _api.AddJson("graphql", openStatusJson);

        var result = await _sut.CompletePullRequestAsync("owner/repo", 42, new PullRequestCompletionOptions(PullRequestCompletionStrategy.AutoMerge), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.PullRequestMerged.Should().BeFalse();
        result.MergeCommitSha.Should().BeNull();
        result.Message.Should().Be("Auto-merge enabled");
    }

    /// <summary>PushBranchAsync entfernt einen bereits eingebetteten Token aus der Remote-URL vor dem Push (Normalisierung), statt einen neuen einzubetten.</summary>
    [Fact]
    public async Task PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://oauth2:token123@github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("set-url") && a.Contains("origin")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push") && a.Contains("--set-upstream") && a.Contains("feature/a")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("remote") && a.Contains("set-url") && a.Contains("origin") && a.Any(x => x == "https://github.com/owner/repo.git")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "push", "--set-upstream", "origin", "feature/a")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env.ContainsKey("GIT_TERMINAL_PROMPT") && env.ContainsKey("GH_TOKEN") && env["GH_TOKEN"] == "token123"
                && HasGitHubExtraHeader(env, "token123", "http.https://github.com/.extraheader") && !env.ContainsKey("NETRC") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>PushBranchAsync bettet keinen Token in die Remote-URL ein, wenn diese bereits unauthentifiziert ist.</summary>
    [Fact]
    public async Task PushBranchAsync_ShouldNotEmbedToken_InRemoteUrl()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("set-url")),
            It.IsAny<string?>(),
            null,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>PushBranchAsync liest die Remote-URL auch dann, wenn kein Token verfügbar ist (Normalisierung ist token-unabhängig), setzt sie aber nicht neu, wenn kein Token eingebettet ist.</summary>
    [Fact]
    public async Task PushBranchAsync_ShouldNotSetRemoteUrl_WhenTokenIsMissing()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns((string?)null);
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("set-url")),
            It.IsAny<string?>(),
            null,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>PushBranchAsync_ShouldThrow_WhenPushFails.</summary>
    [Fact]
    public async Task PushBranchAsync_ShouldThrow_WhenPushFails()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "error"));

        var act = () => _sut.PushBranchAsync("/repo", "feature/a");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*git push fehlgeschlagen*");
    }

    /// <summary>PullAsync normalisiert eine eingebettete Remote-URL auf die unauthentifizierte Form.</summary>
    [Fact]
    public async Task PullAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("abc");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://oauth2:abc@github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("set-url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PullAsync("/repo", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Last() == "https://github.com/owner/repo.git"),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>PullAsync entfernt einen bereits eingebetteten Token aus der Remote-URL vor dem Pull (Normalisierung), statt einen neuen einzubetten.</summary>
    [Fact]
    public async Task PullAsync_ShouldRunGitPull_WithConfiguredCredentials()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PullAsync("/repo", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env.ContainsKey("GH_TOKEN") && env["GH_TOKEN"] == "token"
                && HasGitHubExtraHeader(env, "token", "http.https://github.com/.extraheader") && !env.ContainsKey("NETRC") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>PullAsync benötigt keinen im Voraus in der Remote-URL eingebetteten Token; die Authentifizierung erfolgt ausschließlich über GH_TOKEN.</summary>
    [Fact]
    public async Task PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PullAsync("/repo", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("set-url")),
            It.IsAny<string?>(),
            null,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>PullAsync_ShouldThrow_WhenGitPullFails.</summary>
    [Fact]
    public async Task PullAsync_ShouldThrow_WhenGitPullFails()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns((string?)null);
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fail"));

        var act = () => _sut.PullAsync("/repo");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*git pull fehlgeschlagen*");
    }

    /// <summary>SanitizeSensitiveOutput maskiert einen im git-push-Fehler enthaltenen Token, sodass er nie im Klartext auftaucht.</summary>
    [Fact]
    public async Task SanitizeSensitiveOutput_ShouldMaskToken_InPushErrorMessages()
    {
        const string token = "push-secret-token";
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns(token);
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, $"fatal: unable to access 'https://oauth2:{token}@github.com/owner/repo.git/': push failed"));

        Func<Task> act = () => _sut.PushBranchAsync("/repo", "feature/a");

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().NotContain(token);
        exception.Which.Message.Should().Contain("oauth2:***@");
    }

    /// <summary>SanitizeSensitiveOutput maskiert einen im git-pull-Fehler enthaltenen Token, sodass er nie im Klartext auftaucht.</summary>
    [Fact]
    public async Task SanitizeSensitiveOutput_ShouldMaskToken_InPullErrorMessages()
    {
        const string token = "pull-secret-token";
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns(token);
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, $"fatal: unable to access 'https://oauth2:{token}@github.com/owner/repo.git/': pull failed"));

        Func<Task> act = () => _sut.PullAsync("/repo");

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().NotContain(token);
        exception.Which.Message.Should().Contain("oauth2:***@");
    }

    /// <summary>ResetAsync_ShouldCallGitResetWithTargetRef_WhenProvided.</summary>
    [Fact]
    public async Task ResetAsync_ShouldCallGitResetWithTargetRef_WhenProvided()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "reset", "--hard", "HEAD~1")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.ResetAsync("/repo", "hard", "HEAD~1", TestContext.Current.CancellationToken);

        _cliRunnerMock.VerifyAll();
    }

    /// <summary>ResetAsync_ShouldCallGitResetWithoutTargetRef_WhenNotProvided.</summary>
    [Fact]
    public async Task ResetAsync_ShouldCallGitResetWithoutTargetRef_WhenNotProvided()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "reset", "--soft")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.ResetAsync("/repo", "soft", null, TestContext.Current.CancellationToken);

        _cliRunnerMock.VerifyAll();
    }

    /// <summary>GetRemoteBranchesAsync_ShouldParseAndSortBranches.</summary>
    [Fact]
    public async Task GetRemoteBranchesAsync_ShouldParseAndSortBranches()
    {
        const string output = """
            hash1	refs/heads/feature/z
            hash2	refs/heads/main
            hash3	refs/heads/feature/a
            """;
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "ls-remote", "--heads", "https://github.com/owner/repo.git")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, output, string.Empty));

        var result = (await _sut.GetRemoteBranchesAsync("https://github.com/owner/repo.git", TestContext.Current.CancellationToken)).ToList();

        result.Should().Equal("feature/a", "feature/z", "main");
    }

    /// <summary>GetRemoteBranchesAsync_ShouldReturnEmpty_WhenCliFails.</summary>
    [Fact]
    public async Task GetRemoteBranchesAsync_ShouldReturnEmpty_WhenCliFails()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("ls-remote")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "error"));

        var result = await _sut.GetRemoteBranchesAsync("repo", TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    /// <summary>GetDefaultBranchAsync_ShouldReturnParsedBranch_WhenSymRefCanBeParsed.</summary>
    [Fact]
    public async Task GetDefaultBranchAsync_ShouldReturnParsedBranch_WhenSymRefCanBeParsed()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "ls-remote", "--symref", "https://github.com/owner/repo.git", "HEAD")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "ref: refs/heads/master\tHEAD", string.Empty));

        var result = await _sut.GetDefaultBranchAsync("https://github.com/owner/repo.git", TestContext.Current.CancellationToken);

        result.Should().Be("master");
    }

    /// <summary>GetDefaultBranchAsync_ShouldFallbackToMain_WhenSymRefFails.</summary>
    [Fact]
    public async Task GetDefaultBranchAsync_ShouldFallbackToMain_WhenSymRefFails()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("--symref")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "error"));

        var result = await _sut.GetDefaultBranchAsync("repo", TestContext.Current.CancellationToken);

        result.Should().Be("main");
    }

    /// <summary>CheckoutRemoteBranchAsync_ShouldThrow_WhenCheckoutFails.</summary>
    [Fact]
    public async Task CheckoutRemoteBranchAsync_ShouldThrow_WhenCheckoutFails()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "checkout", "-b", "feature/a", "--track", "origin/feature/a")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fail"));

        var act = () => _sut.CheckoutRemoteBranchAsync("/repo", "feature/a");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git checkout (remote branch) fehlgeschlagen*");
    }

    /// <summary>GetAgentEnvironmentAsync stellt dem Agenten dieselbe gh-/git-Anmeldung wie dem
    /// Host bereit: GH_TOKEN, an den Repository-Host gebundener extraheader und deaktiviertes Credential-Prompt.</summary>
    [Fact]
    public async Task GetAgentEnvironmentAsync_WithToken_ProvidesGhAndGitAuth()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("agent-token");

        var environment = await _sut.GetAgentEnvironmentAsync(Softwareschmiede.Domain.Enums.ScmKontoModus.Benutzerkonto, "https://github.com/owner/repo", TestContext.Current.CancellationToken);

        environment["GH_TOKEN"].Should().Be("agent-token");
        environment["GIT_TERMINAL_PROMPT"].Should().Be("0");
        environment["GIT_CONFIG_COUNT"].Should().Be("1");
        environment["GIT_CONFIG_KEY_0"].Should().Be("http.https://github.com/.extraheader");
        var expected = "AUTHORIZATION: basic " + Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("x-access-token:agent-token"));
        environment["GIT_CONFIG_VALUE_0"].Should().Be(expected);
        environment.Should().NotContainKey("GIT_SSH_COMMAND");
    }

    /// <summary>Ein GHE-Host in der Repository-URL bindet den extraheader-Key an diesen Host.</summary>
    [Fact]
    public async Task GetAgentEnvironmentAsync_GheHost_BindetExtraHeaderAnGheHost()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("agent-token");

        var environment = await _sut.GetAgentEnvironmentAsync(Softwareschmiede.Domain.Enums.ScmKontoModus.Benutzerkonto, "https://ghe.example.com/owner/repo.git", TestContext.Current.CancellationToken);

        environment["GH_TOKEN"].Should().Be("agent-token");
        environment["GIT_CONFIG_COUNT"].Should().Be("1");
        environment["GIT_CONFIG_KEY_0"].Should().Be("http.https://ghe.example.com/.extraheader");
    }

    /// <summary>Ein Nicht-Default-Port der Repository-URL wird in den extraheader-Key aufgenommen;
    /// der Default-Port 443 nicht.</summary>
    [Theory]
    [InlineData("https://ghe.example.com:8443/owner/repo.git", "http.https://ghe.example.com:8443/.extraheader")]
    [InlineData("https://ghe.example.com:443/owner/repo.git", "http.https://ghe.example.com/.extraheader")]
    public async Task GetAgentEnvironmentAsync_NonDefaultPort_NimmtPortInKeyAuf(string repositoryUrl, string expectedKey)
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("agent-token");

        var environment = await _sut.GetAgentEnvironmentAsync(Softwareschmiede.Domain.Enums.ScmKontoModus.Benutzerkonto, repositoryUrl, TestContext.Current.CancellationToken);

        environment["GIT_CONFIG_COUNT"].Should().Be("1");
        environment["GIT_CONFIG_KEY_0"].Should().Be(expectedKey);
    }

    /// <summary>Userinfo/Credentials in der Repository-URL dürfen nicht in den extraheader-Key
    /// einfließen — der Key enthält ausschließlich den Host.</summary>
    [Fact]
    public async Task GetAgentEnvironmentAsync_UserinfoInUrl_KeyEnthaeltNurHost()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("agent-token");

        var environment = await _sut.GetAgentEnvironmentAsync(Softwareschmiede.Domain.Enums.ScmKontoModus.Benutzerkonto, "https://oauth2:geheim@ghe.example.com/o/r.git", TestContext.Current.CancellationToken);

        environment["GIT_CONFIG_COUNT"].Should().Be("1");
        environment["GIT_CONFIG_KEY_0"].Should().Be("http.https://ghe.example.com/.extraheader");
        environment["GIT_CONFIG_KEY_0"].Should().NotContain("oauth2").And.NotContain("geheim");
    }

    /// <summary>Eine SSH-/SCP-gespeicherte Repository-URL bindet den extraheader-Key nach der
    /// HTTPS-Normalisierung an denselben Host wie die HTTPS-Form.</summary>
    [Theory]
    [InlineData("git@ghe.example.com:o/r.git")]
    [InlineData("ssh://git@ghe.example.com/o/r.git")]
    public async Task GetAgentEnvironmentAsync_SshForm_BindetKeyAnHttpsHost(string repositoryUrl)
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("agent-token");

        var environment = await _sut.GetAgentEnvironmentAsync(Softwareschmiede.Domain.Enums.ScmKontoModus.Benutzerkonto, repositoryUrl, TestContext.Current.CancellationToken);

        environment["GIT_CONFIG_COUNT"].Should().Be("1");
        environment["GIT_CONFIG_KEY_0"].Should().Be("http.https://ghe.example.com/.extraheader");
    }

    /// <summary>Lässt sich aus der Repository-URL kein HTTPS-Host bestimmen, entsteht fail-closed
    /// kein extraheader-Eintrag — GH_TOKEN und GIT_TERMINAL_PROMPT bleiben erhalten.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("keine-url")]
    [InlineData("ssh://git@host:2222/x")]
    public async Task GetAgentEnvironmentAsync_OhneBestimmbarenHost_KeinExtraHeader(string? repositoryUrl)
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("agent-token");

        var environment = await _sut.GetAgentEnvironmentAsync(Softwareschmiede.Domain.Enums.ScmKontoModus.Benutzerkonto, repositoryUrl, TestContext.Current.CancellationToken);

        environment["GIT_TERMINAL_PROMPT"].Should().Be("0");
        environment["GH_TOKEN"].Should().Be("agent-token");
        environment.Should().NotContainKey("GIT_CONFIG_COUNT");
        environment.Should().NotContainKey("GIT_CONFIG_KEY_0");
        environment.Should().NotContainKey("GIT_CONFIG_VALUE_0");
    }

    /// <summary>Ohne gespeicherten Token bleibt nur das deaktivierte Credential-Prompt —
    /// ein fehlender Token blockiert den CLI-Start nicht.</summary>
    [Fact]
    public async Task GetAgentEnvironmentAsync_WithoutToken_OnlyDisablesGitPrompt()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);

        var environment = await _sut.GetAgentEnvironmentAsync(Softwareschmiede.Domain.Enums.ScmKontoModus.Benutzerkonto, "https://github.com/owner/repo", TestContext.Current.CancellationToken);

        environment.Should().ContainKey("GIT_TERMINAL_PROMPT").WhoseValue.Should().Be("0");
        environment.Should().NotContainKey("GH_TOKEN");
        environment.Should().NotContainKey("GIT_CONFIG_VALUE_0");
        environment.Should().NotContainKey("GIT_SSH_COMMAND");
    }

    /// <summary>Im SeparatesKonto liefert GetAgentEnvironmentAsync das AgentToken statt des Benutzer-Tokens.</summary>
    [Fact]
    public async Task GetAgentEnvironmentAsync_SeparatesKonto_UsesAgentToken()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("user-token");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.AgentToken")).Returns("bot-token");

        var environment = await _sut.GetAgentEnvironmentAsync(Softwareschmiede.Domain.Enums.ScmKontoModus.SeparatesKonto, "https://github.com/owner/repo", TestContext.Current.CancellationToken);

        environment["GH_TOKEN"].Should().Be("bot-token");
        var expected = "AUTHORIZATION: basic " + Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("x-access-token:bot-token"));
        environment["GIT_CONFIG_VALUE_0"].Should().Be(expected);
        environment.Should().NotContainKey("GIT_SSH_COMMAND");
    }

    /// <summary>GetSettingGroups enthält das optionale AgentToken-Secret in der Authentifizierung.</summary>
    [Fact]
    public void GetSettingGroups_ContainsOptionalAgentTokenField()
    {
        var auth = _sut.GetSettingGroups().Single(g => g.GroupName == "Authentifizierung");
        var field = auth.Fields.Should().ContainSingle(f => f.Key == "AgentToken").Which;
        field.FieldType.Should().Be(PluginSettingFieldType.Secret);
        field.IsRequired.Should().BeFalse();
        field.Description.Should().Contain("notifications").And.Contain("repo");
    }

    /// <summary>SanitizeSensitiveOutput maskiert auch das AgentToken in Fehlertexten.</summary>
    [Fact]
    public async Task SanitizeSensitiveOutput_ShouldMaskAgentToken_InErrorMessages()
    {
        const string agentToken = "bot-secret-token";
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("user-token");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.AgentToken")).Returns(agentToken);
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(128, string.Empty, $"fatal: Authentication failed for '{agentToken}'"));

        Func<Task> act = () => _sut.CloneRepositoryAsync("https://github.com/owner/repo.git", "/target");

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().NotContain(agentToken);
    }

    /// <summary>Im SeparatesKonto schlägt jeder geroutete Aufruf ohne AgentToken als Authentication-Fehler fehl.</summary>
    [Fact]
    public async Task SeparatesKonto_WithoutAgentToken_ThrowsAuthenticationBeforeAnyCall()
    {
        var repository = new ScmEventRepository("Softwareschmiede.GitHub", "github.com", "R_1", "owner", "repo",
            "U_1", "BOT", "scm-bot", true, Softwareschmiede.Domain.Enums.ScmKontoModus.SeparatesKonto);

        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => _sut.ReadEventsAsync(new(new(repository, ScmReferenceKind.Issue, 1), ScmContentKind.Description,
                null, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken));

        error.FailureKind.Should().Be(ScmProviderFailureKind.Authentication);
        error.Message.Should().Contain("Einstellungen → Plugins → GitHub");
        _api.Requests.Should().BeEmpty();
    }

    /// <summary>GetPullRequestStatusAsync stellt die GraphQL-Abfrage mit der vollständigen
    /// Feldliste (inkl. mergeStateStatus) und mappt das Ergebnis über ParsePullRequestStatus.</summary>
    [Fact]
    public async Task GetPullRequestStatusAsync_UsesGraphqlQuery()
    {
        const string statusJson = """
            {
                "data": {
                    "repository": {
                        "pullRequest": {
                            "id": "PR_kw",
                            "number": 42,
                            "title": "PR",
                            "url": "https://github.com/owner/repo/pull/42",
                            "state": "OPEN",
                            "headRefName": "feature/pr",
                            "baseRefName": "main",
                            "headRefOid": "head",
                            "mergeCommit": null,
                            "mergeStateStatus": "CLEAN"
                        }
                    }
                }
            }
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("graphql", statusJson);

        var result = await _sut.GetPullRequestStatusAsync("owner/repo", 42, TestContext.Current.CancellationToken);

        var request = _api.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be("POST");
        request.Endpoint.Should().Be("graphql");
        request.JsonBody.Should().Contain("pullRequest(number:$number)")
            .And.Contain("headRefName").And.Contain("baseRefName").And.Contain("headRefOid")
            .And.Contain("mergeCommit{oid}").And.Contain("mergeStateStatus")
            .And.Contain("\"owner\":\"owner\"").And.Contain("\"name\":\"repo\"").And.Contain("\"number\":42");
        result.Status.Should().Be(PullRequestStatus.Open);
        result.MergeStatus.Should().Be(PullRequestMergeStatus.Mergeable);
        result.Number.Should().Be(42);
        result.ProviderPullRequestId.Should().Be("PR_kw");
        result.SourceBranch.Should().Be("feature/pr");
        result.TargetBranch.Should().Be("main");
        result.HeadSha.Should().Be("head");
        result.MergeCommitSha.Should().BeNull();
        result.Url.Should().Be("https://github.com/owner/repo/pull/42");
    }

    /// <summary>Ein API-Fehler des Status-Reads wird in eine InvalidOperationException gewrappt.</summary>
    [Fact]
    public async Task GetPullRequestStatusAsync_FailureThrowsInvalidOperation()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddStatus("graphql", 500);

        var act = () => _sut.GetPullRequestStatusAsync("owner/repo", 42);

        var error = await act.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Message.Should().Contain("Pull-Request-Status konnte nicht geladen werden");
    }

    /// <summary>GetAvailableRepositoriesAsync ruft user/repos?affiliation=owner ab und mappt
    /// full_name/html_url/updated_at (Fallback created_at) korrekt.</summary>
    [Fact]
    public async Task GetAvailableRepositoriesAsync_MapsRestFields()
    {
        const string json = """
            [
              {
                "name": "repo",
                "full_name": "owner/repo",
                "html_url": "https://github.com/owner/repo",
                "created_at": "2026-09-01T08:00:00Z",
                "updated_at": "2026-10-01T10:00:00Z"
              },
              {
                "name": "older",
                "full_name": "owner/older",
                "html_url": "https://github.com/owner/older",
                "created_at": "2026-08-01T08:00:00Z"
              }
            ]
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("user/repos?affiliation=owner", json);

        var result = (await _sut.GetAvailableRepositoriesAsync(TestContext.Current.CancellationToken)).ToList();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("repo");
        result[0].NameWithOwner.Should().Be("owner/repo");
        result[0].Url.Should().Be("https://github.com/owner/repo");
        result[0].UpdatedAt.Should().Be(new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc));
        result[1].UpdatedAt.Should().Be(new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
        var request = _api.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be("GET");
        request.Endpoint.Should().Be("user/repos?affiliation=owner&per_page=100");
    }

    /// <summary>Ein API-Fehler beim Repository-Listing liefert eine leere Liste statt einer Exception.</summary>
    [Fact]
    public async Task GetAvailableRepositoriesAsync_FailureReturnsEmptyList()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddStatus("user/repos?affiliation=owner", 403);

        var result = await _sut.GetAvailableRepositoriesAsync(TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    /// <summary>GetPullRequestStatusAsync fragt isDraft ab und mappt Draft-PRs korrekt.</summary>
    [Fact]
    public async Task GetPullRequestStatusAsync_ParsesIsDraft()
    {
        const string statusJson = """
            {
                "data": {
                    "repository": {
                        "pullRequest": {
                            "id": "PR_kw",
                            "number": 42,
                            "title": "PR",
                            "url": "https://github.com/owner/repo/pull/42",
                            "state": "OPEN",
                            "headRefName": "feature/pr",
                            "baseRefName": "main",
                            "headRefOid": "head",
                            "mergeCommit": null,
                            "mergeStateStatus": "DRAFT",
                            "isDraft": true
                        }
                    }
                }
            }
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("graphql", statusJson);

        var result = await _sut.GetPullRequestStatusAsync("owner/repo", 42, TestContext.Current.CancellationToken);

        var request = _api.Requests.Should().ContainSingle().Which;
        request.JsonBody.Should().Contain("isDraft");
        result.IsDraft.Should().BeTrue();
        result.MergeStatus.Should().Be(PullRequestMergeStatus.Blocked);
    }

    /// <summary>Fehlendes isDraft-Feld in der Antwort wird als Nicht-Entwurf gelesen.</summary>
    [Fact]
    public async Task GetPullRequestStatusAsync_MissingIsDraft_DefaultsFalse()
    {
        const string statusJson = """
            {
                "data": {
                    "repository": {
                        "pullRequest": {
                            "id": "PR_kw",
                            "number": 42,
                            "title": "PR",
                            "url": "https://github.com/owner/repo/pull/42",
                            "state": "OPEN",
                            "headRefName": "feature/pr",
                            "baseRefName": "main",
                            "headRefOid": "head",
                            "mergeCommit": null,
                            "mergeStateStatus": "CLEAN"
                        }
                    }
                }
            }
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _api.AddJson("graphql", statusJson);

        var result = await _sut.GetPullRequestStatusAsync("owner/repo", 42, TestContext.Current.CancellationToken);

        result.IsDraft.Should().BeFalse();
    }

    /// <summary>MarkPullRequestReadyAsync führt „gh pr ready &lt;nummer&gt; --repo &lt;repo&gt;“ aus.</summary>
    [Fact]
    public async Task MarkPullRequestReadyAsync_RunsGhPrReady()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pr", "ready", "42", "--repo", "owner/repo")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "marked ready", string.Empty));

        var result = await _sut.MarkPullRequestReadyAsync("owner/repo", 42, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _cliRunnerMock.Verify(c => c.RunAsync(
            "gh",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pr", "ready", "42", "--repo", "owner/repo")),
            null,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein gh-Fehler bei der Draft-Aktivierung liefert ein nicht erfolgreiches Ergebnis statt einer Exception.</summary>
    [Fact]
    public async Task MarkPullRequestReadyAsync_CliFailure_ReturnsFailed()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pr", "ready", "42", "--repo", "owner/repo")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a draft"));

        var result = await _sut.MarkPullRequestReadyAsync("owner/repo", 42, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not a draft");
    }

    /// <summary>Ein Berechtigungsfehler bei der Draft-Aktivierung wird als blockiert markiert.</summary>
    [Fact]
    public async Task MarkPullRequestReadyAsync_PermissionError_ReturnsBlocked()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pr", "ready", "42", "--repo", "owner/repo")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "Resource not accessible by integration (requires protected branch bypass)"));

        var result = await _sut.MarkPullRequestReadyAsync("owner/repo", 42, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Blocked.Should().BeTrue();
    }

    /// <summary>Ohne Repository-ID schlägt die Draft-Aktivierung direkt fehl.</summary>
    [Fact]
    public async Task MarkPullRequestReadyAsync_WithoutRepositoryId_ReturnsFailed()
    {
        var result = await _sut.MarkPullRequestReadyAsync(" ", 42, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Repository-ID fehlt");
        _cliRunnerMock.Verify(c => c.RunAsync(
            "gh",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ohne gespeicherten Token schlägt der Klon einer SSH-URL — nach der automatischen
    /// HTTPS-Konvertierung — mit dem bestehenden Token-Fail-Fast fehl, bevor git gestartet wird.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_SshUrlOhneToken_WirftInvalidOperationException()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);

        var act = () => _sut.CloneRepositoryAsync("git@github.com:owner/repo.git", "/target/path");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GitHub-Token fehlt*");
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>GetDefaultBranchAsync nutzt die git-Umgebung mit http.extraheader-Auth,
    /// damit ls-remote --symref gegen private Repositories authentifiziert bleibt.</summary>
    [Fact]
    public async Task GetDefaultBranchAsync_Env_EnthaeltGitAuth()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("--symref")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "ref: refs/heads/main\tHEAD", string.Empty));

        var result = await _sut.GetDefaultBranchAsync("https://github.com/owner/repo.git", TestContext.Current.CancellationToken);

        result.Should().Be("main");
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "ls-remote", "--symref", "https://github.com/owner/repo.git", "HEAD")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token123"
                && HasGitHubExtraHeader(env, "token123", "http.https://github.com/.extraheader") && !env.ContainsKey("NETRC") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Der PR-Checkout übergibt die git-Umgebung mit http.extraheader-Auth an
    /// git fetch/checkout — seit dem Wegfall der _netrc-Persistenz nötig für private Repositories.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_UebergibtGitEnvironment()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "owner/repo",
            TargetRepositoryUrl: "https://github.com/owner/repo.git",
            SourceRepositoryId: "owner/repo",
            SourceRepositoryUrl: null,
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec, TestContext.Current.CancellationToken);

        // Vorab-Hygiene der betreuten Arbeitskopie: Remote-URL lesen und
        // plugin-geschriebenes core.sshCommand prüfen.
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "fetch", "--no-tags", "origin", "refs/heads/feature/pr")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token123"
                && HasGitHubExtraHeader(env, "token123", "http.https://github.com/.extraheader") && !env.ContainsKey("NETRC") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "checkout", "-B", "feature/pr", "FETCH_HEAD")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && HasGitHubExtraHeader(env, "token123", "http.https://github.com/.extraheader") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine SCP-SSH-Repository-URL (git@host:…) wird beim Klon in die HTTPS-Form
    /// umgewandelt; die Umgebung trägt GH_TOKEN + extraheader und kein GIT_SSH_COMMAND.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_SshUrl_WirdZuHttpsGeklont()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.CloneRepositoryAsync("git@github.com:owner/repo.git", "/target/path", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Contains("https://github.com/owner/repo.git") && a.Contains("/target/path")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token"
                && HasGitHubExtraHeader(env, "token", "http.https://github.com/.extraheader") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Any(x => x.Contains("git@", StringComparison.Ordinal) || x.Contains("ssh://", StringComparison.Ordinal))),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Eine ssh://-Repository-URL wird beim Klon in die HTTPS-Form umgewandelt.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_SshSchemeUrl_WirdZuHttpsGeklont()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.CloneRepositoryAsync("ssh://git@github.com/owner/repo.git", "/target/path", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Contains("https://github.com/owner/repo.git") && a.Contains("/target/path")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Nicht konvertierbare Repository-URLs (anderes Schema, SSH mit Fremd-Port) werden
    /// vor jedem CLI-Aufruf mit verständlicher Meldung abgelehnt.</summary>
    [Theory]
    [InlineData("http://github.com/owner/repo")]
    [InlineData("git://github.com/owner/repo")]
    [InlineData("ssh://git@github.com:2222/owner/repo.git")]
    public async Task CloneRepositoryAsync_NichtHttpsSchema_WirdAbgelehnt(string repositoryUrl)
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");

        var act = () => _sut.CloneRepositoryAsync(repositoryUrl, "/target/path");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nur HTTPS-Repository-URLs*");
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Nach einem erfolgreichen Klon wird kein core.sshCommand in die Arbeitskopie
    /// geschrieben; user.name, user.email und die Remote-URL werden weiterhin konfiguriert.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_KonfiguriertKeinSshCommand()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.CloneRepositoryAsync("https://github.com/owner/repo.git", "/target/path", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Any(x => x.Contains("core.sshCommand", StringComparison.Ordinal))),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "user.name", "Softwareschmiede Bot")),
            "/target/path",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "user.email", "bot@softwareschmiede.local")),
            "/target/path",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("remote") && a.Contains("set-url") && a.Contains("origin") && a.Contains("https://github.com/owner/repo.git")),
            "/target/path",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine noch im SCP-SSH-Format vorliegende remote.origin.url wird vor dem Push
    /// auf die HTTPS-Form umgeschrieben.</summary>
    [Fact]
    public async Task PushBranchAsync_SshRemoteUrl_WirdZuHttpsNormalisiert()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        var calls = new List<string>();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "git@github.com:owner/repo.git", string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        var setUrlIndex = calls.FindIndex(c => c == "remote set-url origin https://github.com/owner/repo.git");
        var pushIndex = calls.FindIndex(c => c == "push --set-upstream origin feature/a");
        setUrlIndex.Should().BeGreaterThanOrEqualTo(0);
        pushIndex.Should().BeGreaterThan(setUrlIndex);
    }

    /// <summary>Eine noch im SCP-SSH-Format vorliegende remote.origin.url wird vor dem Pull
    /// auf die HTTPS-Form umgeschrieben.</summary>
    [Fact]
    public async Task PullAsync_SshRemoteUrl_WirdZuHttpsNormalisiert()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        var calls = new List<string>();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "git@github.com:owner/repo.git", string.Empty));

        await _sut.PullAsync("/repo", TestContext.Current.CancellationToken);

        var setUrlIndex = calls.FindIndex(c => c == "remote set-url origin https://github.com/owner/repo.git");
        var pullIndex = calls.FindIndex(c => c == "pull");
        setUrlIndex.Should().BeGreaterThanOrEqualTo(0);
        pullIndex.Should().BeGreaterThan(setUrlIndex);
    }

    /// <summary>Ein von einer Altversion geschriebenes core.sshCommand mit StrictHostKeyChecking=no
    /// wird vor dem Push per git config --unset entfernt — das Unset muss vor dem Remote-Aufruf liegen,
    /// damit die unsichere Option während des Push nicht mehr aktiv ist.</summary>
    [Fact]
    public async Task PushBranchAsync_LegacySshCommand_WirdEntfernt()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        var calls = new List<string>();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--unset", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        var unsetIndex = calls.FindIndex(c => c == "config --unset core.sshCommand");
        var pushIndex = calls.FindIndex(c => c == "push --set-upstream origin feature/a");
        unsetIndex.Should().BeGreaterThanOrEqualTo(0);
        pushIndex.Should().BeGreaterThan(unsetIndex);
    }

    /// <summary>Dasselbe gilt für den Pull: das unsichere core.sshCommand wird per --unset entfernt,
    /// bevor git pull gegen origin läuft.</summary>
    [Fact]
    public async Task PullAsync_LegacySshCommand_WirdEntfernt()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        var calls = new List<string>();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null", string.Empty));

        await _sut.PullAsync("/repo", TestContext.Current.CancellationToken);

        var unsetIndex = calls.FindIndex(c => c == "config --unset core.sshCommand");
        var pullIndex = calls.FindIndex(c => c == "pull");
        unsetIndex.Should().BeGreaterThanOrEqualTo(0);
        pullIndex.Should().BeGreaterThan(unsetIndex);
    }

    /// <summary>Ein vom Anwender gesetztes eigenes core.sshCommand, das zwar die Option
    /// StrictHostKeyChecking=no enthält, aber nicht exakt der Plugin-Signatur entspricht
    /// (hier: zusätzliches -i-Flag), bleibt unverändert erhalten — nur der nachweisbar
    /// plugin-geschriebene Wert wird entfernt.</summary>
    [Fact]
    public async Task PushBranchAsync_EigenesSshCommandMitOption_BleibtErhalten()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "ssh -o StrictHostKeyChecking=no -i /key", string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("--unset")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Never);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("push")),
            "/repo",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein fremd gesetztes core.sshCommand ohne die Plugin-Signatur
    /// (StrictHostKeyChecking=no) bleibt unverändert erhalten.</summary>
    [Fact]
    public async Task PushBranchAsync_FremdesSshCommand_BleibtErhalten()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "ssh -i /key", string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("--unset")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Never);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("push")),
            "/repo",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ist kein core.sshCommand gesetzt (git config --get schlägt fehl), wird kein
    /// --unset ausgeführt und der Pull läuft normal weiter.</summary>
    [Fact]
    public async Task PullAsync_OhneSshCommand_KeinUnset()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PullAsync("/repo", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("--unset")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Never);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
            "/repo",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Schlägt das Lesen von core.sshCommand fehl (z. B. kein git-Repository),
    /// wird nur gewarnt — der Pull läuft in seinen normalen Pfad weiter.</summary>
    [Fact]
    public async Task PullAsync_SshCommandReadFehler_NurWarnung()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(128, string.Empty, "fatal: not in a git directory"));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(128, string.Empty, "fatal: not in a git directory"));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PullAsync("/repo", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("--unset")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Never);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
            "/repo",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine SSH-SourceRepositoryUrl eines Fork-PRs wird vor dem Fetch in die
    /// HTTPS-Form umgewandelt.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_SshForkUrl_WirdZuHttpsGefetcht()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "owner/repo",
            TargetRepositoryUrl: "https://github.com/owner/repo.git",
            SourceRepositoryId: "fork/repo",
            SourceRepositoryUrl: "git@github.com:fork/repo.git",
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec, TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "fetch", "--no-tags", "https://github.com/fork/repo.git", "refs/heads/feature/pr")),
            "/repo",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>GetRemoteBranchesAsync fragt SCP-SSH-Repository-URLs über die konvertierte
    /// HTTPS-Remote-URL ab.</summary>
    [Fact]
    public async Task GetRemoteBranchesAsync_SshUrl_NutztHttpsRemote()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("ls-remote")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "hash1\trefs/heads/main", string.Empty));

        var result = await _sut.GetRemoteBranchesAsync("git@github.com:owner/repo.git", TestContext.Current.CancellationToken);

        result.Should().Equal("main");
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "ls-remote", "--heads", "https://github.com/owner/repo.git")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>GetDefaultBranchAsync fragt SCP-SSH-Repository-URLs über die konvertierte
    /// HTTPS-Remote-URL ab.</summary>
    [Fact]
    public async Task GetDefaultBranchAsync_SshUrl_NutztHttpsRemote()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("ls-remote")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "ref: refs/heads/main\tHEAD", string.Empty));

        var result = await _sut.GetDefaultBranchAsync("ssh://git@github.com/owner/repo.git", TestContext.Current.CancellationToken);

        result.Should().Be("main");
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "ls-remote", "--symref", "https://github.com/owner/repo.git", "HEAD")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Beim Same-Repo-PR-Checkout (Fetch gegen origin) wird eine noch im SCP-SSH-Format
    /// vorliegende remote.origin.url vor dem Fetch auf HTTPS normalisiert.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_SshOriginUrl_WirdVorFetchNormalisiert()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        var calls = new List<string>();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "git@github.com:owner/repo.git", string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "owner/repo",
            TargetRepositoryUrl: "https://github.com/owner/repo.git",
            SourceRepositoryId: "owner/repo",
            SourceRepositoryUrl: null,
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec, TestContext.Current.CancellationToken);

        var setUrlIndex = calls.FindIndex(c => c == "remote set-url origin https://github.com/owner/repo.git");
        var fetchIndex = calls.FindIndex(c => c == "fetch --no-tags origin refs/heads/feature/pr");
        setUrlIndex.Should().BeGreaterThanOrEqualTo(0);
        fetchIndex.Should().BeGreaterThan(setUrlIndex);
    }

    /// <summary>Beim PR-Checkout wird ein von einer Altversion geschriebenes unsicheres
    /// core.sshCommand vor dem Fetch per git config --unset entfernt.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_LegacySshCommand_WirdEntfernt()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        var calls = new List<string>();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => calls.Add(string.Join(' ', args)))
            .ReturnsAsync(new CliResult(0, "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null", string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "owner/repo",
            TargetRepositoryUrl: "https://github.com/owner/repo.git",
            SourceRepositoryId: "owner/repo",
            SourceRepositoryUrl: null,
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec, TestContext.Current.CancellationToken);

        var unsetIndex = calls.FindIndex(c => c == "config --unset core.sshCommand");
        var fetchIndex = calls.FindIndex(c => c == "fetch --no-tags origin refs/heads/feature/pr");
        unsetIndex.Should().BeGreaterThanOrEqualTo(0);
        fetchIndex.Should().BeGreaterThan(unsetIndex);
    }

    /// <summary>Eine null-CheckoutSpec wird mit ArgumentNullException abgelehnt, bevor irgendein
    /// git-Aufruf auf der Arbeitskopie stattfindet.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_NullSpec_WirftArgumentNullException()
    {
        var act = () => _sut.CheckoutPullRequestSourceAsync("/repo", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Eine remote.origin.url, die sich nicht in die HTTPS-Form umwandeln lässt
    /// (hier: ssh:// mit Fremd-Port), bleibt unverändert — kein set-url, der Push läuft
    /// fail-closed ohne extraheader-Eintrag weiter.</summary>
    [Theory]
    [InlineData("ssh://git@github.com:2222/owner/repo.git")]
    [InlineData("ssh://user:pass@github.com/owner/repo.git")]
    public async Task PushBranchAsync_NichtKonvertierbareRemoteUrl_KeinSetUrl(string remoteUrl)
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, remoteUrl, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("set-url")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Never);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "push", "--set-upstream", "origin", "feature/a")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env.ContainsKey("GH_TOKEN")
                && !env.ContainsKey("GIT_CONFIG_COUNT") && !env.ContainsKey("GIT_CONFIG_KEY_0") && !env.ContainsKey("GIT_CONFIG_VALUE_0")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein Klon gegen eine GitHub-Enterprise-URL bindet den extraheader-Key an den
    /// GHE-Host statt an github.com.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_GheUrl_BindetExtraHeaderAnGheHost()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.CloneRepositoryAsync("https://ghe.example.com/owner/repo.git", "/target/path", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Contains("https://ghe.example.com/owner/repo.git")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token"
                && HasGitHubExtraHeader(env, "token", "http.https://ghe.example.com/.extraheader") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine GHE-remote.origin.url bindet den extraheader-Key des Push an den
    /// Origin-Host statt an github.com.</summary>
    [Fact]
    public async Task PushBranchAsync_GheRemote_BindetExtraHeaderAnOriginHost()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://ghe.example.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "push", "--set-upstream", "origin", "feature/a")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token123"
                && HasGitHubExtraHeader(env, "token123", "http.https://ghe.example.com/.extraheader") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ist remote.origin.url nicht lesbar, läuft der Push fail-closed ohne
    /// extraheader-Eintrag weiter — die Operation bricht nicht ab.</summary>
    [Fact]
    public async Task PushBranchAsync_RemoteUrlNichtLesbar_KeinExtraHeader()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(128, string.Empty, "fatal: not in a git directory"));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "push", "--set-upstream", "origin", "feature/a")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token123"
                && env["GIT_TERMINAL_PROMPT"] == "0"
                && !env.ContainsKey("GIT_CONFIG_COUNT") && !env.ContainsKey("GIT_CONFIG_KEY_0") && !env.ContainsKey("GIT_CONFIG_VALUE_0")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine GHE-remote.origin.url bindet den extraheader-Key des Pull an den
    /// Origin-Host statt an github.com.</summary>
    [Fact]
    public async Task PullAsync_GheRemote_BindetExtraHeaderAnOriginHost()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://ghe.example.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PullAsync("/repo", TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token123"
                && HasGitHubExtraHeader(env, "token123", "http.https://ghe.example.com/.extraheader") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Im Fork-Fall bindet der extraheader-Key an den Host der kontaktierten
    /// SourceRepositoryUrl, nicht an den Origin-Host.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_Fork_BindetExtraHeaderAnSourceHost()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://ghe.example.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "owner/repo",
            TargetRepositoryUrl: "https://ghe.example.com/owner/repo.git",
            SourceRepositoryId: "fork/repo",
            SourceRepositoryUrl: "https://ghe-fork.example.com/fork/repo.git",
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec, TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "fetch", "--no-tags", "https://ghe-fork.example.com/fork/repo.git", "refs/heads/feature/pr")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token123"
                && HasGitHubExtraHeader(env, "token123", "http.https://ghe-fork.example.com/.extraheader") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Im Same-Repository-Fall bindet der extraheader-Key an den Origin-Host der
    /// Arbeitskopie — hier eine GHE-remote.origin.url.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_SameRepo_BindetExtraHeaderAnOriginHost()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://ghe.example.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "--get", "core.sshCommand")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "owner/repo",
            TargetRepositoryUrl: "https://ghe.example.com/owner/repo.git",
            SourceRepositoryId: "owner/repo",
            SourceRepositoryUrl: null,
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec, TestContext.Current.CancellationToken);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "fetch", "--no-tags", "origin", "refs/heads/feature/pr")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env["GH_TOKEN"] == "token123"
                && HasGitHubExtraHeader(env, "token123", "http.https://ghe.example.com/.extraheader") && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Prüft die drei GIT_CONFIG_*-Einträge des http.extraheader gegen den erwarteten Token und Key.</summary>
    private static bool HasGitHubExtraHeader(IDictionary<string, string> env, string token, string expectedKey)
    {
        var expected = "AUTHORIZATION: basic " + Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("x-access-token:" + token));
        return env.TryGetValue("GIT_CONFIG_COUNT", out var count) && count == "1"
            && env.TryGetValue("GIT_CONFIG_KEY_0", out var key) && key == expectedKey
            && env.TryGetValue("GIT_CONFIG_VALUE_0", out var value) && value == expected;
    }

    private static bool SequenceEqual(IEnumerable<string> actual, params string[] expected)
        => actual.SequenceEqual(expected);
}
