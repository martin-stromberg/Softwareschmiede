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
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
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
            It.Is<IEnumerable<string>>(a => a.Contains("clone") && a.Any(x => x.Contains("https://oauth2:token@github.com/test/repo", StringComparison.Ordinal)) && a.Contains("/target/path")),
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
        var result = await _sut.CreatePullRequestAsync("owner/repo", "feature/branch", "My PR", "Body");

        // Assert
        result.Nummer.Should().Be(42);
        result.Titel.Should().Be("My PR");
        result.Url.Should().Be(prUrl);
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

    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginPrefix.Should().Be("Softwareschmiede.GitHub");
        _sut.GetSettingGroups().Should().ContainSingle();
        _sut.GetSettingGroups().Single().Fields.Should().ContainSingle(f => f.Key == "Token" && f.IsRequired);
    }

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
            .ReturnsAsync(new CliResult(0, "https://github.com/owner/repo.git", string.Empty));
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

        await _sut.PushBranchAsync("/repo", "feature/a");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => a.Contains("remote") && a.Contains("set-url") && a.Contains("origin") && a.Any(x => x.Contains("oauth2:token123@", StringComparison.Ordinal))),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "push", "--set-upstream", "origin", "feature/a")),
            "/repo",
            It.Is<IDictionary<string, string>?>(env => env != null && env.ContainsKey("GIT_TERMINAL_PROMPT")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushBranchAsync_ShouldNotSetRemoteUrl_WhenTokenIsMissing()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns((string?)null);
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
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "config", "remote.origin.url")),
            It.IsAny<string?>(),
            null,
            It.IsAny<CancellationToken>()), Times.Never);
    }

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
            .ReturnsAsync(new CliResult(0, "https://oauth2:token@github.com/owner/repo.git", string.Empty));
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
            .ReturnsAsync(new CliResult(0, "https://oauth2:token@github.com/owner/repo.git", string.Empty));
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
                "/repo",
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        await _sut.PullAsync("/repo");

        _cliRunnerMock.Verify(c => c.RunAsync(
            "git",
            It.Is<IEnumerable<string>>(a => SequenceEqual(a, "pull")),
            "/repo",
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PullAsync_ShouldThrow_WhenGitPullFails()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns((string?)null);
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

        await _sut.ResetAsync("/repo", "hard", "HEAD~1");

        _cliRunnerMock.VerifyAll();
    }

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

        await _sut.ResetAsync("/repo", "soft", null);

        _cliRunnerMock.VerifyAll();
    }

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

        var result = (await _sut.GetRemoteBranchesAsync("https://github.com/owner/repo.git")).ToList();

        result.Should().Equal("feature/a", "feature/z", "main");
    }

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

        var result = await _sut.GetRemoteBranchesAsync("repo");

        result.Should().BeEmpty();
    }

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

        var result = await _sut.GetDefaultBranchAsync("https://github.com/owner/repo.git");

        result.Should().Be("master");
    }

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

        var result = await _sut.GetDefaultBranchAsync("repo");

        result.Should().Be("main");
    }

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

    private static bool SequenceEqual(IEnumerable<string> actual, params string[] expected)
        => actual.SequenceEqual(expected);
}
