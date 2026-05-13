using FluentAssertions;
using Moq;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Abstractions;

public sealed class GitPluginBaseTests
{
    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    private sealed class TestGitPlugin(ICliRunner cliRunner) : GitPluginBase<TestGitPlugin>(cliRunner)
    {
        public override string PluginName => "Test";
        public override string PluginPrefix => "Test";
        public override PluginType PluginType => PluginType.SourceCodeManagement;
        public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public override Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default) => Task.FromResult<IEnumerable<Issue>>([]);
        public override Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default) => Task.CompletedTask;
        public override Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default) => Task.CompletedTask;
        public override Task PullAsync(string localPath, CancellationToken ct = default) => Task.CompletedTask;
        public override Task<PullRequest> CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct = default) => Task.FromResult(new PullRequest(1, "t", "u", "b"));
        public override Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public override Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default) => Task.FromResult<IEnumerable<string>>([]);
        public override Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default) => Task.FromResult("main");
        public Task EnsureRepoAsync(string localPath, CancellationToken ct = default) => EnsureGitRepositoryAsync(localPath, ct);
    }
}
