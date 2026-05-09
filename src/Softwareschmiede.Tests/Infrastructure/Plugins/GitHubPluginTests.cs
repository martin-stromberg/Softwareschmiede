using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests für das GitHubPlugin.</summary>
public sealed class GitHubPluginTests
{
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly GitHubPlugin _sut;

    public GitHubPluginTests()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new GitHubPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            new Mock<ILogger<GitHubPlugin>>().Object);
    }

    /// <summary>GetIssuesAsync parsed JSON-Antwort korrekt und gibt Issues zurück.</summary>
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
                    "url": "https://github.com/owner/repo/issues/1"
                },
                {
                    "number": 2,
                    "title": "Feature Request",
                    "body": null,
                    "labels": [],
                    "milestone": null,
                    "url": "https://github.com/owner/repo/issues/2"
                }
            ]
            """;
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("test-token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, json, string.Empty));

        // Act
        var result = (await _sut.GetIssuesAsync("owner/repo")).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Nummer.Should().Be(1);
        result[0].Titel.Should().Be("Bug Fix");
        result[0].Labels.Should().BeEquivalentTo(new[] { "bug", "urgent" });
        result[0].Milestone.Should().Be("v1.0");
        result[1].Nummer.Should().Be(2);
        result[1].Body.Should().BeNull();
    }

    /// <summary>GetIssuesAsync gibt leere Liste zurück wenn CLI fehlschlägt.</summary>
    [Fact]
    public async Task GetIssuesAsync_ShouldReturnEmptyList_WhenCliFails()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);
        _cliRunnerMock.Setup(c => c.RunAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "authentication required"));

        // Act
        var result = await _sut.GetIssuesAsync("owner/repo");

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>CloneRepositoryAsync ruft git clone mit korrekten Argumenten auf.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => a.Contains("clone")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        // Act
        await _sut.CloneRepositoryAsync("https://github.com/test/repo", "/target/path");

        // Assert
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Contains("https://github.com/test/repo") && a.Contains("/target/path")),
            null,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CloneRepositoryAsync wirft Exception wenn git clone fehlschlägt.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ShouldThrowInvalidOperationException_WhenCliFails()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);
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
        await _sut.CreateBranchAsync("/local/path", "feature/my-branch");

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
        const string json = """{"number": 42, "title": "My PR", "url": "https://github.com/owner/repo/pull/42"}""";
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, json, string.Empty));

        // Act
        var result = await _sut.CreatePullRequestAsync("owner/repo", "feature/branch", "My PR", "Body");

        // Assert
        result.Nummer.Should().Be(42);
        result.Titel.Should().Be("My PR");
        result.Url.Should().Be("https://github.com/owner/repo/pull/42");
        result.BranchName.Should().Be("feature/branch");
    }

    /// <summary>CheckHealthAsync gibt true zurück wenn gh auth status erfolgreich ist.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue_WhenGhAuthStatusSucceeds()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh", It.IsAny<IEnumerable<string>>(), null,
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Logged in", string.Empty));

        // Act
        var result = await _sut.CheckHealthAsync();

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>CheckHealthAsync gibt false zurück wenn gh auth status fehlschlägt.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnFalse_WhenGhAuthStatusFails()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);
        _cliRunnerMock.Setup(c => c.RunAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not logged in"));

        // Act
        var result = await _sut.CheckHealthAsync();

        // Assert
        result.Should().BeFalse();
    }
}
