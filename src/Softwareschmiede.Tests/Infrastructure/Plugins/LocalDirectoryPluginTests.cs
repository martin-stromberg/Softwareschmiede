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
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
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
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
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
                sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "clone", sourceDir, targetDir })),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
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

        cli.VerifyAll();
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldUseRequestedTargetDirectory_WhenWorkingDirectorySettingExists()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var requestedTarget = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "readme.txt"), "base");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "clone", sourceDir, requestedTarget })),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                requestedTarget,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                requestedTarget,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkingDirectory")).Returns(@"C:\configured-should-be-ignored");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourceDir, requestedTarget);

        cli.VerifyAll();
    }

    [Fact]
    public void GetSettingGroups_ShouldNotExposeWorkingDirectoryField()
    {
        var sut = new LocalDirectoryPlugin(
            new Mock<ICliRunner>().Object,
            new Mock<ICredentialStore>().Object,
            NullLogger<LocalDirectoryPlugin>.Instance);

        var allFieldKeys = sut.GetSettingGroups()
            .SelectMany(group => group.Fields)
            .Select(field => field.Key)
            .ToList();

        allFieldKeys.Should().NotContain("WorkingDirectory");
    }

    [Fact]
    public void GetRepositoryLinkFields_ShouldRequireSourceDirectory()
    {
        var sut = new LocalDirectoryPlugin(
            new Mock<ICliRunner>().Object,
            new Mock<ICredentialStore>().Object,
            NullLogger<LocalDirectoryPlugin>.Instance);

        var fields = sut.GetRepositoryLinkFields();

        fields.Should().ContainSingle(field =>
            field.Key == "SourceDirectory"
            && field.IsRequired
            && field.FieldType == PluginSettingFieldType.Text);
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
    public async Task CloneRepositoryAsync_ShouldInitAndClone_WhenSeparateModeHasNoGitAndInitIsConfirmed()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(targetDir, recursive: true);

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.SetupSequence(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "init" })),
                sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Initialized empty Git repository", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "clone", sourceDir, targetDir })),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
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
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory")).Returns("true");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourceDir, targetDir);

        cli.VerifyAll();
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldCopyWithoutClone_WhenSeparateModeHasNoGitAndInitNotConfirmed()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(targetDir, recursive: true);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "copy.txt"), "copied");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "init" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Initialized empty Git repository", string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory")).Returns("false");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourceDir, targetDir);

        File.Exists(Path.Combine(targetDir, "copy.txt")).Should().BeTrue();
        cli.VerifyAll();
    }

    [Fact]
    public async Task PushBranchAsync_ShouldSynchronizeFilesAndDeleteGitDeletedEntries_InSeparateMode()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var workspaceDir = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "keep.txt"), "source-old");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "removed.txt"), "to-be-removed");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "old-name.txt"), "old-name");
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, "keep.txt"), "workspace-new");
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, "new-name.txt"), "new-name");
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, ".softwareschmiede-local-source"), sourceDir);

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, " D removed.txt\nR  old-name.txt -> new-name.txt\n", string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.PushBranchAsync(workspaceDir, "feature/local-sync");

        (await File.ReadAllTextAsync(Path.Combine(sourceDir, "keep.txt"))).Should().Be("workspace-new");
        File.Exists(Path.Combine(sourceDir, "new-name.txt")).Should().BeTrue();
        File.Exists(Path.Combine(sourceDir, "removed.txt")).Should().BeFalse();
        File.Exists(Path.Combine(sourceDir, "old-name.txt")).Should().BeFalse();
        cli.VerifyAll();
    }

    [Fact]
    public async Task PullAsync_ShouldSynchronizeSourceToWorkspace_WithoutMerge()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var workspaceDir = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "source.txt"), "source-version");
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, "source.txt"), "workspace-version");
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, ".softwareschmiede-local-source"), sourceDir);

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.PullAsync(workspaceDir);

        (await File.ReadAllTextAsync(Path.Combine(workspaceDir, "source.txt"))).Should().Be("source-version");
        cli.VerifyAll();
    }

    [Fact]
    public async Task PushBranchAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.PushBranchAsync(@"C:\temp\workspace", "feature/noop");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*PushBranchAsync*");
    }

    [Fact]
    public async Task PullAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.PullAsync(@"C:\temp\workspace");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*PullAsync*");
    }

    [Fact]
    public async Task PullAsync_ShouldThrowInvalidOperationException_WhenWorkspaceContainsChanges()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var workspaceDir = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, ".softwareschmiede-local-source"), sourceDir);

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, " M changed.txt", string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.PullAsync(workspaceDir);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*uncommitted changes*");
        cli.VerifyAll();
    }

    [Fact]
    public async Task PushBranchAsync_ShouldThrowInvalidOperationException_WhenWorkspaceEqualsSource()
    {
        var workspaceDir = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, ".softwareschmiede-local-source"), workspaceDir);

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.PushBranchAsync(workspaceDir, "feature/sync");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unterschiedliche Pfade*");
        cli.VerifyAll();
    }

    [Fact]
    public async Task PushBranchAsync_ShouldThrowInvalidOperationException_WhenGitStatusForDeleteSyncFails()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var workspaceDir = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, ".softwareschmiede-local-source"), sourceDir);
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, "keep.txt"), "workspace-new");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fatal"));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.PushBranchAsync(workspaceDir, "feature/sync");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git status für Delete-Sync fehlgeschlagen*");
        cli.VerifyAll();
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldFallbackToSeparateMode_WhenWorkspaceModeIsInvalid()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(targetDir, recursive: true);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "copy.txt"), "copied");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "init" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Initialized empty Git repository", string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("invalid-value");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory")).Returns("false");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourceDir, targetDir);

        File.Exists(Path.Combine(targetDir, "copy.txt")).Should().BeTrue();
        cli.VerifyAll();
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldThrow_WhenNoSourceDirectoryIsProvided()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.SourceDirectory")).Returns((string?)null);
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(string.Empty, @"C:\temp\target");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*kein Quellverzeichnis*");
    }

    [Fact]
    public async Task PullAsync_ShouldUseConfiguredSourceDirectory_WhenPointerAndRemoteAreMissing()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var workspaceDir = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "source.txt"), "source-version");
        await File.WriteAllTextAsync(Path.Combine(workspaceDir, "source.txt"), "workspace-version");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "config", "--get", "remote.origin.url" })),
                workspaceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "unset"));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.SourceDirectory")).Returns(sourceDir);
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.PullAsync(workspaceDir);

        (await File.ReadAllTextAsync(Path.Combine(workspaceDir, "source.txt"))).Should().Be("source-version");
        cli.VerifyAll();
    }

    [Fact]
    public async Task UnsupportedRemoteMethods_ShouldThrowNotSupportedException()
    {
        var sut = new LocalDirectoryPlugin(
            new Mock<ICliRunner>().Object,
            new Mock<ICredentialStore>().Object,
            NullLogger<LocalDirectoryPlugin>.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(() => sut.CreatePullRequestAsync("repo", "branch", "title", "body"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.GetRemoteBranchesAsync("repo"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.GetDefaultBranchAsync("repo"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.CheckoutRemoteBranchAsync("/repo", "feature/a"));
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.GetIssuesAsync("repo"));
    }
}
