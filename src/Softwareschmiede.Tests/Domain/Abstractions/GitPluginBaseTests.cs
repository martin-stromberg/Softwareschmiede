using FluentAssertions;
using Moq;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Abstractions;

/// <summary>GitPluginBaseTests.</summary>
public sealed class GitPluginBaseTests
{
    /// <summary><summary>CreateBranchAsync_ShouldRunCheckoutMinusB.</summary>.</summary>
    [Fact]
    /// <summary>CreateBranchAsync_ShouldRunCheckoutMinusB.</summary>
    public async Task CreateBranchAsync_ShouldRunCheckoutMinusB()
    {
        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "checkout", "-b", "feature/x" })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        var sut = new TestGitPlugin(cli.Object);

        await sut.CreateBranchAsync("/repo", "feature/x");

        cli.VerifyAll();
    }

    /// <summary><summary>CreateBranchAsync_ShouldThrow_WhenGitCheckoutFails.</summary>.</summary>
    [Fact]
    /// <summary>CreateBranchAsync_ShouldThrow_WhenGitCheckoutFails.</summary>
    public async Task CreateBranchAsync_ShouldThrow_WhenGitCheckoutFails()
    {
        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "checkout", "-b", "feature/x" })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "checkout failed"));
        var sut = new TestGitPlugin(cli.Object);

        var act = () => sut.CreateBranchAsync("/repo", "feature/x");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git checkout -b fehlgeschlagen*");
    }

    /// <summary><summary>CommitAsync_ShouldRunAddAndCommit.</summary>.</summary>
    [Fact]
    /// <summary>CommitAsync_ShouldRunAddAndCommit.</summary>
    public async Task CommitAsync_ShouldRunAddAndCommit()
    {
        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "add", "." })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "commit", "-m", "msg" })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        var sut = new TestGitPlugin(cli.Object);

        await sut.CommitAsync("/repo", "msg");

        cli.VerifyAll();
    }

    /// <summary><summary>CommitAsync_ShouldThrow_WhenGitAddFails.</summary>.</summary>
    [Fact]
    /// <summary>CommitAsync_ShouldThrow_WhenGitAddFails.</summary>
    public async Task CommitAsync_ShouldThrow_WhenGitAddFails()
    {
        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "add", "." })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "add failed"));
        var sut = new TestGitPlugin(cli.Object);

        var act = () => sut.CommitAsync("/repo", "msg");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git add fehlgeschlagen*");
    }

    /// <summary><summary>CommitAsync_ShouldThrow_WhenGitCommitFails.</summary>.</summary>
    [Fact]
    /// <summary>CommitAsync_ShouldThrow_WhenGitCommitFails.</summary>
    public async Task CommitAsync_ShouldThrow_WhenGitCommitFails()
    {
        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "add", "." })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "commit", "-m", "msg" })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "commit failed"));
        var sut = new TestGitPlugin(cli.Object);

        var act = () => sut.CommitAsync("/repo", "msg");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git commit fehlgeschlagen*");
    }

    /// <summary><summary>CheckoutRemoteBranchAsync_ShouldRunTrackOriginBranch_WhenSuccess.</summary>.</summary>
    [Fact]
    /// <summary>CheckoutRemoteBranchAsync_ShouldRunTrackOriginBranch_WhenSuccess.</summary>
    public async Task CheckoutRemoteBranchAsync_ShouldRunTrackOriginBranch_WhenSuccess()
    {
        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "checkout", "-b", "feature/x", "--track", "origin/feature/x" })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        var sut = new TestGitPlugin(cli.Object);

        await sut.CheckoutRemoteBranchAsync("/repo", "feature/x");

        cli.VerifyAll();
    }

    /// <summary><summary>EnsureGitRepositoryAsync_ShouldThrow_WhenRevParseFails.</summary>.</summary>
    [Fact]
    /// <summary>EnsureGitRepositoryAsync_ShouldThrow_WhenRevParseFails.</summary>
    public async Task EnsureGitRepositoryAsync_ShouldThrow_WhenRevParseFails()
    {
        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a repository"));
        var sut = new TestGitPlugin(cli.Object);

        var act = () => sut.EnsureRepoAsync("/repo");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ist kein Git-Repository*");
    }

    /// <summary><summary>ResetAsync_ShouldThrow_WhenGitFails.</summary>.</summary>
    [Fact]
    /// <summary>ResetAsync_ShouldThrow_WhenGitFails.</summary>
    public async Task ResetAsync_ShouldThrow_WhenGitFails()
    {
        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                "/repo",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "failed"));
        var sut = new TestGitPlugin(cli.Object);

        var act = () => sut.ResetAsync("/repo", "hard", "HEAD~1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git reset fehlgeschlagen*");
    }

    /// <summary>Prüft die Default-Capabilities der Basisklasse für Remote-Git-Plugins.</summary>
    [Fact]
    public async Task GetGitActionCapabilitiesAsync_ShouldReturnRemoteDefaults_ByDefault()
    {
        var sut = new TestGitPlugin(new Mock<ICliRunner>().Object);

        var capabilities = await sut.GetGitActionCapabilitiesAsync();

        capabilities.RepositoryKind.Should().Be(RepositoryKind.RemoteGit);
        capabilities.IsWorkingDirectoryCopy.Should().BeFalse();
        capabilities.CanPush.Should().BeTrue();
        capabilities.CanPull.Should().BeTrue();
        capabilities.CanCreatePullRequest.Should().BeTrue();
        capabilities.CanMergeToSource.Should().BeFalse();
    }

    /// <summary>Issue-Anlage ist in der Basisklasse standardmäßig nicht unterstützt.</summary>
    [Fact]
    public async Task IssueCreateCapability_ShouldReturnNotSupported_ByDefault()
    {
        var sut = new TestGitPlugin(new Mock<ICliRunner>().Object);

        var canCreate = await sut.CanCreateIssueAsync("repo");
        var createResult = await sut.CreateIssueAsync("repo", new IssueCreateRequest("Titel", "Body"));
        var templateResult = await sut.GetIssueTemplatesAsync("repo");

        canCreate.Should().BeFalse();
        createResult.Status.Should().Be(IssueCreateResultStatus.NotSupported);
        createResult.IsSuccess.Should().BeFalse();
        createResult.Issue.Should().BeNull();
        templateResult.Status.Should().Be(IssueTemplateLoadResultStatus.NotSupported);
        templateResult.Templates.Should().BeEmpty();
    }

    /// <summary>Prüft, dass MergeToSource standardmäßig als nicht unterstützt markiert ist.</summary>
    [Fact]
    public async Task MergeToSourceAsync_ShouldThrowNotSupportedException_ByDefault()
    {
        var sut = new TestGitPlugin(new Mock<ICliRunner>().Object);

        var act = () => sut.MergeToSourceAsync("/repo");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*MergeToSourceAsync*");
    }

    private sealed class TestGitPlugin(ICliRunner cliRunner) : GitPluginBase<TestGitPlugin>(cliRunner)
    {
        public override string PluginName => "Test";
        public override string PluginPrefix => "Test";
        public override PluginType PluginType => PluginType.SourceCodeManagement;
        /// <summary>IReadOnlyList.</summary>
        public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public override Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default) => Task.FromResult<IEnumerable<Issue>>([]);
        /// <summary>CloneRepositoryAsync.</summary>
        public override Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default) => Task.CompletedTask;
        /// <summary>PushBranchAsync.</summary>
        public override Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default) => Task.CompletedTask;
        /// <summary>PullAsync.</summary>
        public override Task PullAsync(string localPath, CancellationToken ct = default) => Task.CompletedTask;
        /// <summary>Task.</summary>
        public override Task<PullRequest> CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct = default) => Task.FromResult(new PullRequest(1, "t", "u", "b"));
        /// <summary>Task.</summary>
        public override Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public override Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default) => Task.FromResult<IEnumerable<string>>([]);
        /// <summary>Task.</summary>
        public override Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default) => Task.FromResult("main");
        /// <summary>EnsureRepoAsync.</summary>
        public Task EnsureRepoAsync(string localPath, CancellationToken ct = default) => EnsureGitRepositoryAsync(localPath, ct);
    }
}
