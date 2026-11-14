using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests für das BitbucketPlugin.</summary>
public sealed class BitbucketPluginTests
{
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly string _netrcPath;
    private readonly BitbucketPlugin _sut;

    /// <summary>Initialisiert Testinstanz mit Mocks.</summary>
    public BitbucketPluginTests()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _netrcPath = Path.Combine(Path.GetTempPath(), $"sws-bb-netrc-{Guid.NewGuid():N}");
        _sut = new BitbucketPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            new Mock<ILogger<BitbucketPlugin>>().Object,
            _netrcPath);
    }

    /// <summary>GetSettingGroups gibt exakt 3 Gruppen zurück.</summary>
    [Fact]
    public void GetSettingGroups_ShouldReturnThreeGroups()
    {
        var groups = _sut.GetSettingGroups();

        groups.Should().HaveCount(3);
    }

    /// <summary>GetSettingGroups gibt Gruppen mit korrekten Namen zurück.</summary>
    [Fact]
    public void GetSettingGroups_ShouldHaveCorrectGroupNames()
    {
        var groups = _sut.GetSettingGroups();

        groups[0].GroupName.Should().Be("Authentifizierung");
        groups[1].GroupName.Should().Be("Jira");
        groups[2].GroupName.Should().Be("BitBucket-Hosting");
    }

    /// <summary>BitBucket-Hosting-Gruppe enthält exakt 2 Felder.</summary>
    [Fact]
    public void GetSettingGroups_BitBucketHostingGroup_ShouldHaveTwoFields()
    {
        var hostingGroup = _sut.GetSettingGroups()[2];

        hostingGroup.Fields.Should().HaveCount(2);
    }

    /// <summary>HostingMode-Feld hat EnumOptions Cloud und SelfHosted.</summary>
    [Fact]
    public void GetSettingGroups_HostingModeField_ShouldHaveEnumOptions()
    {
        var hostingGroup = _sut.GetSettingGroups()[2];
        var hostingModeField = hostingGroup.Fields.Single(f => f.Key == "HostingMode");

        hostingModeField.FieldType.Should().Be(PluginSettingFieldType.Enum);
        hostingModeField.EnumOptions.Should().BeEquivalentTo(new[] { "Cloud", "SelfHosted" });
    }

    /// <summary>SelfHostedUrl-Feld ist optional.</summary>
    [Fact]
    public void GetSettingGroups_SelfHostedUrlField_ShouldBeOptional()
    {
        var hostingGroup = _sut.GetSettingGroups()[2];
        var selfHostedUrlField = hostingGroup.Fields.Single(f => f.Key == "SelfHostedUrl");

        selfHostedUrlField.IsRequired.Should().BeFalse();
        selfHostedUrlField.FieldType.Should().Be(PluginSettingFieldType.Url);
    }

    /// <summary>GetBitbucketApiBaseUrl gibt api.bitbucket.org zurück wenn Modus Cloud ist.</summary>
    [Fact]
    public void GetBitbucketApiBaseUrl_ShouldReturnCloudUrl_WhenHostingModeIsCloud()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("Cloud");

        var result = _sut.GetBitbucketApiBaseUrl();

        result.Should().Be("https://api.bitbucket.org");
    }

    /// <summary>GetBitbucketApiBaseUrl gibt api.bitbucket.org zurück wenn kein Modus gesetzt ist.</summary>
    [Fact]
    public void GetBitbucketApiBaseUrl_ShouldReturnCloudUrl_WhenHostingModeIsNotSet()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns((string?)null);

        var result = _sut.GetBitbucketApiBaseUrl();

        result.Should().Be("https://api.bitbucket.org");
    }

    /// <summary>GetBitbucketApiBaseUrl gibt konfigurierte Self-Hosted-URL zurück.</summary>
    [Fact]
    public void GetBitbucketApiBaseUrl_ShouldReturnSelfHostedUrl_WhenHostingModeIsSelfHosted()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("SelfHosted");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl"))
            .Returns("https://bitbucket.example.com");

        var result = _sut.GetBitbucketApiBaseUrl();

        result.Should().Be("https://bitbucket.example.com");
    }

    /// <summary>GetBitbucketApiBaseUrl entfernt trailing Slash aus Self-Hosted-URL.</summary>
    [Fact]
    public void GetBitbucketApiBaseUrl_ShouldTrimTrailingSlash_WhenSelfHostedUrlHasTrailingSlash()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("SelfHosted");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl"))
            .Returns("https://bitbucket.example.com/");

        var result = _sut.GetBitbucketApiBaseUrl();

        result.Should().Be("https://bitbucket.example.com");
    }

    /// <summary>GetBitbucketApiBaseUrl unterstützt Self-Hosted-URL mit Port.</summary>
    [Fact]
    public void GetBitbucketApiBaseUrl_ShouldSupportPort_WhenSelfHostedUrlIncludesPort()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("SelfHosted");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl"))
            .Returns("https://bitbucket.example.com:7990");

        var result = _sut.GetBitbucketApiBaseUrl();

        result.Should().Be("https://bitbucket.example.com:7990");
    }

    /// <summary>GetBitbucketApiBaseUrl wirft Exception wenn Self-Hosted-URL nicht konfiguriert ist.</summary>
    [Fact]
    public void GetBitbucketApiBaseUrl_ShouldThrow_WhenSelfHostedUrlIsEmpty()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("SelfHosted");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl"))
            .Returns((string?)null);

        var act = () => _sut.GetBitbucketApiBaseUrl();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Self-Hosted URL ist nicht konfiguriert*");
    }

    /// <summary>GetBitbucketRepositoriesPath gibt Cloud-Pfad zurück wenn Modus Cloud ist.</summary>
    [Fact]
    public void GetBitbucketRepositoriesPath_ShouldReturnCloudPath_WhenHostingModeIsCloud()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("Cloud");

        var result = _sut.GetBitbucketRepositoriesPath("my-workspace");

        result.Should().Be("/2.0/repositories/my-workspace?pagelen=100");
    }

    /// <summary>GetBitbucketRepositoriesPath gibt Self-Hosted-Pfad zurück wenn Modus Self-Hosted ist.</summary>
    [Fact]
    public void GetBitbucketRepositoriesPath_ShouldReturnSelfHostedPath_WhenHostingModeIsSelfHosted()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("SelfHosted");

        var result = _sut.GetBitbucketRepositoriesPath("MY-PROJECT");

        result.Should().Be("/rest/api/1.0/projects/MY-PROJECT/repos");
    }

    /// <summary>CheckHealthAsync wirft Exception wenn Self-Hosted-URL fehlt.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldThrow_WhenSelfHostedAndUrlIsEmpty()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("SelfHosted");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl"))
            .Returns((string?)null);

        var act = () => _sut.CheckHealthAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Self-Hosted URL ist nicht konfiguriert*");
    }

    /// <summary>CheckHealthAsync prüft Cloud-API-URL für Cloud-Modus.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldUseCloudApiUrl_WhenHostingModeIsCloud()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("test-value");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("Cloud");

        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "{}", string.Empty));

        await _sut.CheckHealthAsync();

        _cliRunnerMock.Verify(c => c.RunAsync(
            "curl",
            It.Is<IEnumerable<string>>(a => a.Any(x => x.Contains("api.bitbucket.org/2.0/user", StringComparison.Ordinal))),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CheckHealthAsync prüft Self-Hosted-API-URL für Self-Hosted-Modus.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldUseSelfHostedApiUrl_WhenHostingModeIsSelfHosted()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("test-value");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("SelfHosted");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl"))
            .Returns("https://bitbucket.example.com");

        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "{}", string.Empty));

        await _sut.CheckHealthAsync();

        _cliRunnerMock.Verify(c => c.RunAsync(
            "curl",
            It.Is<IEnumerable<string>>(a => a.Any(x => x.Contains("bitbucket.example.com/rest/api/1.0/user", StringComparison.Ordinal))),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CheckHealthAsync überspringt Jira-Check wenn jiraUrl null ist.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldSkipJira_WhenJiraUrlIsNull()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.Username"))
            .Returns("user");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.AppPassword"))
            .Returns("pass");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("Cloud");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraUrl"))
            .Returns((string?)null);

        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "{\"account_id\":\"123\"}", string.Empty));

        var result = await _sut.CheckHealthAsync();

        result.Should().BeTrue();
        _cliRunnerMock.Verify(c => c.RunAsync(
            "curl",
            It.IsAny<IEnumerable<string>>(),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>GetAvailableRepositoriesAsync parst Cloud-JSON-Felder (full_name, links.html.href) korrekt.</summary>
    [Fact]
    public async Task GetAvailableRepositoriesAsync_ShouldParseCloudJson()
    {
        const string json = """
            {
              "values": [
                {
                  "name": "my-repo",
                  "full_name": "myworkspace/my-repo",
                  "updated_on": "2025-01-01T00:00:00Z",
                  "links": {
                    "html": { "href": "https://bitbucket.org/myworkspace/my-repo" }
                  }
                }
              ]
            }
            """;

        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.Workspace"))
            .Returns("myworkspace");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("Cloud");
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("test");

        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, json, string.Empty));

        var repos = (await _sut.GetAvailableRepositoriesAsync()).ToList();

        repos.Should().HaveCount(1);
        repos[0].Name.Should().Be("my-repo");
        repos[0].NameWithOwner.Should().Be("myworkspace/my-repo");
        repos[0].Url.Should().Be("https://bitbucket.org/myworkspace/my-repo");
    }

    /// <summary>GetAvailableRepositoriesAsync parst Self-Hosted-JSON-Felder (slug, project.key, links.self[0].href) korrekt.</summary>
    [Fact]
    public async Task GetAvailableRepositoriesAsync_ShouldParseSelfHostedJson()
    {
        const string json = """
            {
              "values": [
                {
                  "name": "My Repo",
                  "slug": "my-repo",
                  "project": { "key": "MYPROJ" },
                  "links": {
                    "self": [{ "href": "https://bitbucket.example.com/projects/MYPROJ/repos/my-repo/browse" }]
                  }
                }
              ]
            }
            """;

        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.Workspace"))
            .Returns("MYPROJ");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
            .Returns("SelfHosted");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl"))
            .Returns("https://bitbucket.example.com");
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("test");

        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, json, string.Empty));

        var repos = (await _sut.GetAvailableRepositoriesAsync()).ToList();

        repos.Should().HaveCount(1);
        repos[0].Name.Should().Be("My Repo");
        repos[0].NameWithOwner.Should().Be("MYPROJ/my-repo");
        repos[0].Url.Should().Be("https://bitbucket.example.com/projects/MYPROJ/repos/my-repo/browse");
    }

    /// <summary>ParseJiraIssues behandelt null-Beschreibung ohne Exception.</summary>
    [Fact]
    public async Task GetIssuesAsync_ShouldHandleNullDescription()
    {
        const string json = """
            {
              "issues": [
                {
                  "key": "PROJ-1",
                  "fields": {
                    "summary": "Ein Issue ohne Beschreibung",
                    "description": null,
                    "labels": []
                  }
                }
              ]
            }
            """;

        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("test");

        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, json, string.Empty));

        var issues = (await _sut.GetIssuesAsync("any")).ToList();

        issues.Should().HaveCount(1);
        issues[0].Titel.Should().Be("PROJ-1: Ein Issue ohne Beschreibung");
        issues[0].Body.Should().BeNull();
    }

    /// <summary>CreateIssueAsync erstellt Jira-Issue mit Summary, Projekt, Issue-Typ und ADF-Beschreibung.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldPostJiraAdfPayload()
    {
        IEnumerable<string>? capturedArgs = null;
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("credential");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraUrl"))
            .Returns("https://jira.example.test");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraProjectKey"))
            .Returns("PROJ");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraIssueType"))
            .Returns("Bug");

        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) => capturedArgs = args.ToList())
            .ReturnsAsync(new CliResult(0, """{"key":"PROJ-42","self":"https://jira.example.test/rest/api/3/issue/42"}""", string.Empty));

        var result = await _sut.CreateIssueAsync("workspace/repo", new IssueCreateRequest("Titel", "Zeile 1\nZeile 2"));

        result.IsSuccess.Should().BeTrue();
        result.Issue!.Titel.Should().Be("PROJ-42: Titel");
        result.Issue.IssueUrl.Should().Be("https://jira.example.test/browse/PROJ-42");
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Should().Contain("POST");
        capturedArgs.Should().Contain("https://jira.example.test/rest/api/3/issue");
        var payload = capturedArgs.SkipWhile(a => a != "-d").Skip(1).First();
        payload.Should().Contain("\"summary\":\"Titel\"");
        payload.Should().Contain("\"key\":\"PROJ\"");
        payload.Should().Contain("\"name\":\"Bug\"");
        payload.Should().Contain("\"type\":\"doc\"");
        payload.Should().Contain("Zeile 1");
        payload.Should().Contain("Zeile 2");
    }

    /// <summary>CanCreateIssueAsync ist false, wenn Jira-URL oder Projekt-Key fehlen.</summary>
    [Theory]
    [InlineData(null, "PROJ")]
    [InlineData("https://jira.example.test", null)]
    [InlineData(" ", "PROJ")]
    [InlineData("https://jira.example.test", " ")]
    public async Task CanCreateIssueAsync_ShouldReturnFalse_WhenJiraConfigurationIsMissing(string? jiraUrl, string? jiraProject)
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraUrl"))
            .Returns(jiraUrl);
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraProjectKey"))
            .Returns(jiraProject);

        var result = await _sut.CanCreateIssueAsync("workspace/repo");

        result.Should().BeFalse();
    }

    /// <summary>CreateIssueAsync liefert NotSupported ohne Curl-Aufruf, wenn Jira nicht konfiguriert ist.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldReturnNotSupported_WhenJiraConfigurationIsMissing()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraUrl"))
            .Returns((string?)null);
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraProjectKey"))
            .Returns("PROJ");

        var result = await _sut.CreateIssueAsync("workspace/repo", new IssueCreateRequest("Titel", "Body"));

        result.Status.Should().Be(IssueCreateResultStatus.NotSupported);
        result.Issue.Should().BeNull();
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>CreateIssueAsync validiert leere Jira-Summaries ohne Curl-Aufruf.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldReturnFailed_WhenTitleIsMissing()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("credential");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraUrl"))
            .Returns("https://jira.example.test");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraProjectKey"))
            .Returns("PROJ");

        var result = await _sut.CreateIssueAsync("workspace/repo", new IssueCreateRequest(" ", "Body"));

        result.Status.Should().Be(IssueCreateResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Summary");
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>CreateIssueAsync gibt Jira-Validierungsfehler als Fehlerergebnis zurück.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldReturnFailed_WhenJiraReturnsErrors()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("credential");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraUrl"))
            .Returns("https://jira.example.test");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraProjectKey"))
            .Returns("PROJ");
        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, """{"errors":{"summary":"Summary is required"}}""", string.Empty));

        var result = await _sut.CreateIssueAsync("workspace/repo", new IssueCreateRequest("Titel", "Body"));

        result.Status.Should().Be(IssueCreateResultStatus.Failed);
        result.Issue.Should().BeNull();
        result.ErrorMessage.Should().Contain("summary");
    }

    /// <summary>CreateIssueAsync propagiert Cancellation statt eine lokale Referenz vorzutäuschen.</summary>
    [Fact]
    public async Task CreateIssueAsync_ShouldPropagateCancellation_WhenCurlIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns("credential");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraUrl"))
            .Returns("https://jira.example.test");
        _credentialStoreMock
            .Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.JiraProjectKey"))
            .Returns("PROJ");
        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "curl",
                It.IsAny<IEnumerable<string>>(),
                null,
                null,
                cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => _sut.CreateIssueAsync("workspace/repo", new IssueCreateRequest("Titel", "Body"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>ResolveGitCloneUrl wandelt Browser-URL korrekt um.</summary>
    [Theory]
    [InlineData(
        "https://bitbucket.vectron.de/projects/ERP/repos/udr-aufbereitung/browse",
        "https://bitbucket.vectron.de/scm/ERP/udr-aufbereitung.git")]
    [InlineData(
        "https://bitbucket.vectron.de/projects/ERP/repos/udr-aufbereitung",
        "https://bitbucket.vectron.de/scm/ERP/udr-aufbereitung.git")]
    [InlineData(
        "https://bitbucket.example.com:7990/projects/MY/repos/myrepo/browse",
        "https://bitbucket.example.com:7990/scm/MY/myrepo.git")]
    [InlineData(
        "https://bitbucket.example.com/rest/api/1.0/projects/KEY/repos/slug",
        "https://bitbucket.example.com/scm/KEY/slug.git")]
    [InlineData(
        "https://bitbucket.example.com/scm/KEY/slug.git",
        "https://bitbucket.example.com/scm/KEY/slug.git")]
    [InlineData(
        "https://bitbucket.example.com/scm/KEY/slug",
        "https://bitbucket.example.com/scm/KEY/slug.git")]
    public void ResolveGitCloneUrl_ShouldReturnCorrectCloneUrl(string input, string expected)
    {
        BitbucketPlugin.ResolveGitCloneUrl(input).Should().Be(expected);
    }

    /// <summary>GetDefaultBranchAsync entfernt CRLF aus Windows-Ausgabe.</summary>
    [Fact]
    public async Task GetDefaultBranchAsync_ShouldTrimCarriageReturn_WhenOutputHasCRLF()
    {
        _credentialStoreMock
            .Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns((string?)null);

        _cliRunnerMock
            .Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "ref: refs/heads/main\r\tHEAD\r\n", string.Empty));

        var branch = await _sut.GetDefaultBranchAsync("https://bitbucket.org/owner/repo.git");

        branch.Should().Be("main");
    }

    /// <summary>Eine SCP-SSH-Repository-URL (git@host:…) wird beim Klon in die HTTPS-Form
    /// umgewandelt und mit den konfigurierten Credentials authentifiziert; die Umgebung
    /// enthält kein GIT_SSH_COMMAND.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_SshUrl_WirdZuHttpsGeklont()
    {
        SetupCredentials();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.CloneRepositoryAsync("git@bitbucket.org:ws/repo.git", "/target/path");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Contains("https://user:appPassword@bitbucket.org/ws/repo.git") && a.Contains("/target/path")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && env["GIT_TERMINAL_PROMPT"] == "0" && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine ssh://-Repository-URL wird beim Klon in die HTTPS-Form umgewandelt.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_SshSchemeUrl_WirdZuHttpsGeklont()
    {
        SetupCredentials();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.CloneRepositoryAsync("ssh://git@bitbucket.org/ws/repo.git", "/target/path");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Contains("https://user:appPassword@bitbucket.org/ws/repo.git") && a.Contains("/target/path")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Im SelfHosted-Modus wird eine SCP-SSH-URL zunächst zu HTTPS konvertiert und dann
    /// über ResolveGitCloneUrl in die SCM-Clone-URL überführt.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_SelfHostedSshUrl_WirdZuScmHttpsUrlGeklont()
    {
        SetupCredentials();
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode")).Returns("SelfHosted");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl")).Returns("https://bitbucket.example.com");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.CloneRepositoryAsync("git@bitbucket.example.com:projects/KEY/repos/slug", "/target/path");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Contains("https://user:appPassword@bitbucket.example.com/scm/KEY/slug.git") && a.Contains("/target/path")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Nicht konvertierbare Repository-URLs werden vor jedem CLI-Aufruf abgelehnt.</summary>
    [Theory]
    [InlineData("http://bitbucket.org/ws/repo")]
    [InlineData("git://bitbucket.org/ws/repo")]
    public async Task CloneRepositoryAsync_NichtHttpsSchema_WirdAbgelehnt(string repositoryUrl)
    {
        SetupCredentials();

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

    /// <summary>Bitbucket-Server-SSH-URLs mit Nicht-Standard-Port werden nicht geraten,
    /// sondern vor jedem CLI-Aufruf abgelehnt.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_SshUrlMitFremdPort_WirdAbgelehnt()
    {
        SetupCredentials();
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode")).Returns("SelfHosted");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl")).Returns("https://bitbucket.example.com");

        var act = () => _sut.CloneRepositoryAsync("ssh://git@bitbucket.example.com:7999/scm/KEY/slug.git", "/target/path");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nur HTTPS-Repository-URLs*");
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ohne Username/AppPassword greift der bestehende Credential-Fail-Fast
    /// vor jedem CLI-Aufruf.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_OhneCredentials_WirftInvalidOperationException()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);

        var act = () => _sut.CloneRepositoryAsync("git@bitbucket.org:ws/repo.git", "/target/path");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Bitbucket-Authentifizierung fehlt*");
        _cliRunnerMock.Verify(c => c.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Eine noch im SCP-SSH-Format vorliegende remote.origin.url wird vor dem Push
    /// auf die HTTPS-Form mit eingebetteten Credentials umgeschrieben.</summary>
    [Fact]
    public async Task PushBranchAsync_SshRemoteUrl_WirdZuHttpsNormalisiert()
    {
        SetupCredentials();
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
            .ReturnsAsync(new CliResult(0, "git@bitbucket.org:ws/repo.git", string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a");

        var setUrlIndex = calls.FindIndex(c => c == "remote set-url origin https://user:appPassword@bitbucket.org/ws/repo.git");
        var pushIndex = calls.FindIndex(c => c == "push --set-upstream origin feature/a");
        setUrlIndex.Should().BeGreaterThanOrEqualTo(0);
        pushIndex.Should().BeGreaterThan(setUrlIndex);
    }

    /// <summary>Eine noch im SCP-SSH-Format vorliegende remote.origin.url wird vor dem Pull
    /// auf die HTTPS-Form mit eingebetteten Credentials umgeschrieben.</summary>
    [Fact]
    public async Task PullAsync_SshRemoteUrl_WirdZuHttpsNormalisiert()
    {
        SetupCredentials();
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
            .ReturnsAsync(new CliResult(0, "git@bitbucket.org:ws/repo.git", string.Empty));

        await _sut.PullAsync("/repo");

        var setUrlIndex = calls.FindIndex(c => c == "remote set-url origin https://user:appPassword@bitbucket.org/ws/repo.git");
        var pullIndex = calls.FindIndex(c => c == "pull");
        setUrlIndex.Should().BeGreaterThanOrEqualTo(0);
        pullIndex.Should().BeGreaterThan(setUrlIndex);
    }

    /// <summary>Eine bereits HTTPS-förmige remote.origin.url (inkl. eingebetteter Credentials)
    /// bleibt unverändert — kein set-url, der Push läuft normal.</summary>
    [Fact]
    public async Task PushBranchAsync_HttpsRemoteUrl_KeinSetUrl()
    {
        SetupCredentials();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "https://user:appPassword@bitbucket.org/ws/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("set-url")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Never);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("push")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Schlägt das Lesen der remote.origin.url fehl, läuft der Pull in seinem
    /// normalen Pfad weiter (Fehlertoleranz der Normalisierung).</summary>
    [Fact]
    public async Task PullAsync_RemoteUrlReadFehler_LaeuftWeiter()
    {
        SetupCredentials();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(128, string.Empty, "fatal: not in a git directory"));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PullAsync("/repo");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("set-url")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Never);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("pull")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
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

        var result = (await _sut.GetRemoteBranchesAsync("git@bitbucket.org:ws/repo.git")).ToList();

        result.Should().Equal("main");
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "ls-remote", "--heads", "https://bitbucket.org/ws/repo.git")),
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

        var result = await _sut.GetDefaultBranchAsync("ssh://git@bitbucket.org/ws/repo.git");

        result.Should().Be("main");
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "ls-remote", "--symref", "https://bitbucket.org/ws/repo.git", "HEAD")),
            null,
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine SSH-SourceRepositoryUrl eines Fork-PRs wird vor dem Fetch in die
    /// HTTPS-Form umgewandelt.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_SshForkUrl_WirdZuHttpsGefetcht()
    {
        SetupCredentials();
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
            .ReturnsAsync(new CliResult(0, "https://user:appPassword@bitbucket.org/ws/repo.git", string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "ws/repo",
            TargetRepositoryUrl: "https://bitbucket.org/ws/repo.git",
            SourceRepositoryId: "fork/repo",
            SourceRepositoryUrl: "git@bitbucket.org:fork/repo.git",
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "fetch", "--no-tags", "https://bitbucket.org/fork/repo.git", "refs/heads/feature/pr")),
            "/repo",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Der PR-Checkout übergibt erstmals die git-Umgebung (GIT_TERMINAL_PROMPT=0,
    /// kein GIT_SSH_COMMAND) an git fetch/checkout und normalisiert vorab die remote.origin.url.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_UebergibtGitEnvironment()
    {
        SetupCredentials();
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
            .ReturnsAsync(new CliResult(0, "https://user:appPassword@bitbucket.org/ws/repo.git", string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "ws/repo",
            TargetRepositoryUrl: "https://bitbucket.org/ws/repo.git",
            SourceRepositoryId: "ws/repo",
            SourceRepositoryUrl: null,
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec);

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "fetch", "--no-tags", "origin", "refs/heads/feature/pr")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env["GIT_TERMINAL_PROMPT"] == "0" && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "checkout", "-B", "feature/pr", "FETCH_HEAD")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && !env.ContainsKey("GIT_SSH_COMMAND")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Beim Same-Repo-PR-Checkout (Fetch gegen origin) wird eine noch im SCP-SSH-Format
    /// vorliegende remote.origin.url vor dem Fetch auf HTTPS mit Credentials normalisiert.</summary>
    [Fact]
    public async Task CheckoutPullRequestSourceAsync_SshOriginUrl_WirdVorFetchNormalisiert()
    {
        SetupCredentials();
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
            .ReturnsAsync(new CliResult(0, "git@bitbucket.org:ws/repo.git", string.Empty));

        var spec = new PullRequestCheckoutSpec(
            TargetRepositoryId: "ws/repo",
            TargetRepositoryUrl: "https://bitbucket.org/ws/repo.git",
            SourceRepositoryId: "ws/repo",
            SourceRepositoryUrl: null,
            SourceBranch: "feature/pr",
            SourceRef: null,
            HeadSha: null);

        await _sut.CheckoutPullRequestSourceAsync("/repo", spec);

        var setUrlIndex = calls.FindIndex(c => c == "remote set-url origin https://user:appPassword@bitbucket.org/ws/repo.git");
        var fetchIndex = calls.FindIndex(c => c == "fetch --no-tags origin refs/heads/feature/pr");
        setUrlIndex.Should().BeGreaterThanOrEqualTo(0);
        fetchIndex.Should().BeGreaterThan(setUrlIndex);
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

    /// <summary>Ohne Credentials wird eine SSH-remote.origin.url auf die nackte HTTPS-Form
    /// umgeschrieben — die _netrc-Pflege übernimmt die Authentifizierung zur Laufzeit.</summary>
    [Fact]
    public async Task PushBranchAsync_SshRemoteUrlOhneCredentials_WirdZuNacktemHttpsNormalisiert()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);
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
            .ReturnsAsync(new CliResult(0, "git@bitbucket.org:ws/repo.git", string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "remote", "set-url", "origin", "https://bitbucket.org/ws/repo.git")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine remote.origin.url, die sich nicht in die HTTPS-Form umwandeln lässt
    /// (Fremd-Port oder Fremd-Userinfo), bleibt unverändert — kein set-url, der Push läuft weiter.</summary>
    [Theory]
    [InlineData("ssh://git@bitbucket.example.com:7999/scm/KEY/slug.git")]
    [InlineData("ssh://user:pass@bitbucket.org/ws/repo.git")]
    public async Task PushBranchAsync_NichtKonvertierbareRemoteUrl_KeinSetUrl(string remoteUrl)
    {
        SetupCredentials();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, remoteUrl, string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("push")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PushBranchAsync("/repo", "feature/a");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("set-url")),
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

    /// <summary>Die _netrc-Pflege schreibt in den per Konstruktorparameter übergebenen
    /// netrcPath — die echte Benutzer-_netrc bleibt unangetastet.</summary>
    [Fact]
    public async Task NetrcPfad_AusKonstruktorParameter_WirdGenutzt()
    {
        SetupCredentials();
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
            .ReturnsAsync(new CliResult(0, "https://user:appPassword@bitbucket.org/ws/repo.git", string.Empty));
        var realNetrcPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            OperatingSystem.IsWindows() ? "_netrc" : ".netrc");
        var realNetrcBefore = File.Exists(realNetrcPath) ? File.ReadAllText(realNetrcPath) : null;

        await _sut.PullAsync("/repo");

        File.Exists(_netrcPath).Should().BeTrue();
        var content = File.ReadAllText(_netrcPath);
        content.Should().Contain("machine bitbucket.org");
        content.Should().Contain("login user");
        content.Should().Contain("password appPassword");
        var realNetrcAfter = File.Exists(realNetrcPath) ? File.ReadAllText(realNetrcPath) : null;
        realNetrcAfter.Should().Be(realNetrcBefore);
    }

    private void SetupCredentials()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.Username")).Returns("user");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.AppPassword")).Returns("appPassword");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode")).Returns("Cloud");
    }

    private static bool SequenceEqual(IEnumerable<string> actual, params string[] expected)
        => actual.SequenceEqual(expected);
}
