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
    private readonly BitbucketPlugin _sut;

    /// <summary>Initialisiert Testinstanz mit Mocks.</summary>
    public BitbucketPluginTests()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new BitbucketPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            new Mock<ILogger<BitbucketPlugin>>().Object);
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

    /// <summary>ResolveGitCloneUrl wandelt Browser-URL korrekt um.</summary>
    [Theory]
    [InlineData(
        "https://bitbucket.vectron.de/projects/ERP/repos/udr-aufbereitung/browse",
        "https://bitbucket.vectron.de/scm/ERP/udr-aufbereitung.git")]
    /// <summary>"https://bitbucket.vectron.de/projects/ERP/repos/udr-aufbereitung",.</summary>
    [InlineData(
        "https://bitbucket.vectron.de/projects/ERP/repos/udr-aufbereitung",
        "https://bitbucket.vectron.de/scm/ERP/udr-aufbereitung.git")]
    /// <summary>"https://bitbucket.example.com:7990/projects/MY/repos/myrepo/browse",.</summary>
    [InlineData(
        "https://bitbucket.example.com:7990/projects/MY/repos/myrepo/browse",
        "https://bitbucket.example.com:7990/scm/MY/myrepo.git")]
    /// <summary>"https://bitbucket.example.com/rest/api/1.0/projects/KEY/repos/slug",.</summary>
    [InlineData(
        "https://bitbucket.example.com/rest/api/1.0/projects/KEY/repos/slug",
        "https://bitbucket.example.com/scm/KEY/slug.git")]
    /// <summary>"https://bitbucket.example.com/scm/KEY/slug.git",.</summary>
    [InlineData(
        "https://bitbucket.example.com/scm/KEY/slug.git",
        "https://bitbucket.example.com/scm/KEY/slug.git")]
    /// <summary>"https://bitbucket.example.com/scm/KEY/slug",.</summary>
    [InlineData(
        "https://bitbucket.example.com/scm/KEY/slug",
        "https://bitbucket.example.com/scm/KEY/slug.git")]
    /// <summary>ResolveGitCloneUrl_ShouldReturnCorrectCloneUrl.</summary>
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
}
