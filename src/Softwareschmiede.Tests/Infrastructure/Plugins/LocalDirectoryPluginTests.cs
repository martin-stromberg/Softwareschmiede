using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

public sealed class LocalDirectoryPluginTests
{
    [Fact]
    public async Task CloneRepositoryAsync_ShouldRequireExplicitConfirmation_ForGitInitInSourceDirectory()
    {
        var source = Directory.CreateTempSubdirectory().FullName;
        var target = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(target, recursive: true);

        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                source,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory")).Returns("false");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(source, target);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht bestätigt*");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldFailHard_WhenWorkspaceIsDirty()
    {
        var source = Directory.CreateTempSubdirectory().FullName;
        var target = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(target, recursive: true);

        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                source,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                source,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, " M demo.txt", string.Empty));
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(source, target);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*uncommitted changes*");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldAbortCopy_WhenGuardrailFileLimitIsExceeded()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(targetDir, recursive: true);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "b.txt"), "b");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.CopyMaxFiles")).Returns("1");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(sourceDir, targetDir);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Copy-Guardrail*Dateien*");
        Directory.Exists(targetDir).Should().BeFalse("partielle Kopien müssen aufgeräumt werden");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldAbortCopy_WhenGuardrailMegabyteLimitIsExceeded()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(targetDir, recursive: true);
        await File.WriteAllBytesAsync(Path.Combine(sourceDir, "huge.bin"), new byte[2 * 1024 * 1024]);

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.CopyMaxMegabytes")).Returns("1");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(sourceDir, targetDir);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Copy-Guardrail*MB*");
        Directory.Exists(targetDir).Should().BeFalse("partielle Kopien müssen aufgeräumt werden");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldThrow_WhenSeparateModeUsesSameSourceAndTarget()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(sourceDir, sourceDir);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*erfordert ein anderes Zielverzeichnis*");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldThrow_WhenSeparateTargetDirectoryIsNotEmpty()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(targetDir, "existing.txt"), "x");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(sourceDir, targetDir);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ist nicht leer*");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldUseConfiguredSourceDirectory_WhenRepositoryUrlIsEmpty()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(targetDir, recursive: true);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "readme.txt"), "base");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.SourceDirectory")).Returns(sourceDir);
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(string.Empty, targetDir);

        File.Exists(Path.Combine(targetDir, "readme.txt")).Should().BeTrue();
        cli.VerifyAll();
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldUseConfiguredWorkingDirectory_WhenSet()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var configuredWorkingDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(configuredWorkingDir, recursive: true);
        var requestedTarget = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "readme.txt"), "base");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                configuredWorkingDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                configuredWorkingDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkingDirectory")).Returns(configuredWorkingDir);
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourceDir, requestedTarget);

        File.Exists(Path.Combine(configuredWorkingDir, "readme.txt")).Should().BeTrue();
        File.Exists(Path.Combine(requestedTarget, "readme.txt")).Should().BeFalse();
        cli.VerifyAll();
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldThrow_WhenGitInitFails_InSourceDirectory()
    {
        var source = Directory.CreateTempSubdirectory().FullName;
        var target = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(target, recursive: true);

        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                source,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "init" })),
                source,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fatal"));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory")).Returns("true");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(source, target);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git init fehlgeschlagen*");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldThrow_WhenGitStatusCheckFails()
    {
        var source = Directory.CreateTempSubdirectory().FullName;
        var target = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(target, recursive: true);

        var cli = new Mock<ICliRunner>();
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                source,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                source,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fatal"));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(source, target);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Workspace-Status konnte nicht geprüft werden*");
    }

    [Fact]
    public async Task RemoteMethods_ShouldThrowNotSupportedException()
    {
        var sut = new LocalDirectoryPlugin(
            new Mock<ICliRunner>().Object,
            new Mock<ICredentialStore>().Object,
            NullLogger<LocalDirectoryPlugin>.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(() => sut.PushBranchAsync("/repo", "feature/a"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.PullAsync("/repo"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.CreatePullRequestAsync("repo", "branch", "title", "body"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.GetRemoteBranchesAsync("repo"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.GetDefaultBranchAsync("repo"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.CheckoutRemoteBranchAsync("/repo", "feature/a"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.GetIssuesAsync("repo"));
    }
}
