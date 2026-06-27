using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>LocalDirectoryPluginTests.</summary>
public sealed class LocalDirectoryPluginTests
{
    /// <summary><summary>CloneRepositoryAsync_ShouldRequireExplicitConfirmation_ForGitInitInSourceDirectory.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldRequireExplicitConfirmation_ForGitInitInSourceDirectory.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldFailHard_WhenWorkspaceIsDirty.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldFailHard_WhenWorkspaceIsDirty.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldAbortCopy_WhenGuardrailFileLimitIsExceeded.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldAbortCopy_WhenGuardrailFileLimitIsExceeded.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldAbortCopy_WhenGuardrailMegabyteLimitIsExceeded.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldAbortCopy_WhenGuardrailMegabyteLimitIsExceeded.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldThrow_WhenSeparateModeUsesSameSourceAndTarget.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldThrow_WhenSeparateModeUsesSameSourceAndTarget.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldThrow_WhenSeparateTargetDirectoryIsNotEmpty.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldThrow_WhenSeparateTargetDirectoryIsNotEmpty.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldUseConfiguredSourceDirectory_WhenRepositoryUrlIsEmpty.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldUseConfiguredSourceDirectory_WhenRepositoryUrlIsEmpty.</summary>
    public async Task CloneRepositoryAsync_ShouldUseConfiguredSourceDirectory_WhenRepositoryUrlIsEmpty()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(targetDir, recursive: true);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "readme.txt"), "base");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.SetupSequence(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "init" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Initialized empty Git repository", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "config", "user.name", "Softwareschmiede" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "config", "user.email", "noreply@softwareschmiede.local" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "add", "." })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "commit", "-m", "Initial workspace snapshot" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "[master (root-commit)] Initial workspace snapshot", string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.SourceDirectory")).Returns(sourceDir);
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(string.Empty, targetDir);

        File.Exists(Path.Combine(targetDir, "readme.txt")).Should().BeTrue();
        cli.VerifyAll();
    }

    /// <summary><summary>CloneRepositoryAsync_ShouldUseRequestedTargetDirectory_WhenWorkingDirectorySettingExists.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldUseRequestedTargetDirectory_WhenWorkingDirectorySettingExists.</summary>
    public async Task CloneRepositoryAsync_ShouldUseRequestedTargetDirectory_WhenWorkingDirectorySettingExists()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var requestedTarget = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(requestedTarget, recursive: true);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "readme.txt"), "base");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.SetupSequence(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                requestedTarget,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "init" })),
                requestedTarget,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Initialized empty Git repository", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "config", "user.name", "Softwareschmiede" })),
                requestedTarget,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "config", "user.email", "noreply@softwareschmiede.local" })),
                requestedTarget,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "add", "." })),
                requestedTarget,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "commit", "-m", "Initial workspace snapshot" })),
                requestedTarget,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "[master (root-commit)] Initial workspace snapshot", string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkingDirectory")).Returns(@"C:\configured-should-be-ignored");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourceDir, requestedTarget);

        File.Exists(Path.Combine(requestedTarget, "readme.txt")).Should().BeTrue();
        cli.VerifyAll();
    }

    /// <summary><summary>GetSettingGroups_ShouldNotExposeWorkingDirectoryField.</summary>.</summary>
    [Fact]
    /// <summary>GetSettingGroups_ShouldNotExposeWorkingDirectoryField.</summary>
    public void GetSettingGroups_ShouldNotExposeWorkingDirectoryField()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(
            new Mock<ICliRunner>().Object,
            store.Object,
            NullLogger<LocalDirectoryPlugin>.Instance);

        var allFieldKeys = sut.GetSettingGroups()
            .SelectMany(group => group.Fields)
            .Select(field => field.Key)
            .ToList();

        allFieldKeys.Should().NotContain("WorkingDirectory");
        allFieldKeys.Should().NotContain("ConfirmGitInitInSourceDirectory");
    }

    /// <summary><summary>GetSettingGroups_ShouldExposeGitInitConfirmation_WhenModeIsInSourceDirectory.</summary>.</summary>
    [Fact]
    /// <summary>GetSettingGroups_ShouldExposeGitInitConfirmation_WhenModeIsInSourceDirectory.</summary>
    public void GetSettingGroups_ShouldExposeGitInitConfirmation_WhenModeIsInSourceDirectory()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(
            new Mock<ICliRunner>().Object,
            store.Object,
            NullLogger<LocalDirectoryPlugin>.Instance);

        var allFieldKeys = sut.GetSettingGroups()
            .SelectMany(group => group.Fields)
            .Select(field => field.Key)
            .ToList();

        allFieldKeys.Should().Contain("ConfirmGitInitInSourceDirectory");
    }

    /// <summary><summary>GetRepositoryLinkFields_ShouldRequireSourceDirectory.</summary>.</summary>
    [Fact]
    /// <summary>GetRepositoryLinkFields_ShouldRequireSourceDirectory.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldThrow_WhenGitInitFails_InSourceDirectory.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldThrow_WhenGitInitFails_InSourceDirectory.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldThrow_WhenGitStatusCheckFails.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldThrow_WhenGitStatusCheckFails.</summary>
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

    /// <summary><summary>PushBranchAsync_ShouldSynchronizeFilesAndDeleteGitDeletedEntries_InSeparateMode.</summary>.</summary>
    [Fact]
    /// <summary>PushBranchAsync_ShouldSynchronizeFilesAndDeleteGitDeletedEntries_InSeparateMode.</summary>
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

    /// <summary><summary>PullAsync_ShouldSynchronizeSourceToWorkspace_WithoutMerge.</summary>.</summary>
    [Fact]
    /// <summary>PullAsync_ShouldSynchronizeSourceToWorkspace_WithoutMerge.</summary>
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

    /// <summary><summary>GetGitActionCapabilitiesAsync_ShouldReturnCopyFlowFlags_WhenWorkspaceModeIsSeparate.</summary>.</summary>
    [Fact]
    /// <summary>GetGitActionCapabilitiesAsync_ShouldReturnCopyFlowFlags_WhenWorkspaceModeIsSeparate.</summary>
    public async Task GetGitActionCapabilitiesAsync_ShouldReturnCopyFlowFlags_WhenWorkspaceModeIsSeparate()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var result = await sut.GetGitActionCapabilitiesAsync();

        result.RepositoryKind.Should().Be(RepositoryKind.LocalDirectory);
        result.IsWorkingDirectoryCopy.Should().BeTrue();
        result.CanPush.Should().BeFalse();
        result.CanPull.Should().BeFalse();
        result.CanCreatePullRequest.Should().BeFalse();
        result.CanMergeToSource.Should().BeTrue();
    }

    /// <summary>Verifiziert die Capability-Matrix im InSourceDirectory-Modus.</summary>
    [Fact]
    public async Task GetGitActionCapabilitiesAsync_ShouldReturnInSourceFlags_WhenWorkspaceModeIsInSourceDirectory()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var result = await sut.GetGitActionCapabilitiesAsync();

        result.RepositoryKind.Should().Be(RepositoryKind.LocalDirectory);
        result.IsWorkingDirectoryCopy.Should().BeFalse();
        result.CanPush.Should().BeTrue();
        result.CanPull.Should().BeTrue();
        result.CanCreatePullRequest.Should().BeTrue();
        result.CanMergeToSource.Should().BeFalse();
    }

    /// <summary>Prüft den Fallback auf SeparateWorkingDirectory bei fehlender Workspace-Mode-Konfiguration.</summary>
    [Fact]
    public async Task GetGitActionCapabilitiesAsync_ShouldFallbackToSeparateFlags_WhenWorkspaceModeIsMissing()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns((string?)null);
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var result = await sut.GetGitActionCapabilitiesAsync();

        result.IsWorkingDirectoryCopy.Should().BeTrue();
        result.CanPush.Should().BeFalse();
        result.CanPull.Should().BeFalse();
        result.CanCreatePullRequest.Should().BeFalse();
        result.CanMergeToSource.Should().BeTrue();
    }

    /// <summary>Prüft den Fallback auf SeparateWorkingDirectory bei ungültigem Workspace-Mode-Wert.</summary>
    [Fact]
    public async Task GetGitActionCapabilitiesAsync_ShouldFallbackToSeparateFlags_WhenWorkspaceModeIsInvalid()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("invalid-value");
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var result = await sut.GetGitActionCapabilitiesAsync();

        result.IsWorkingDirectoryCopy.Should().BeTrue();
        result.CanPush.Should().BeFalse();
        result.CanPull.Should().BeFalse();
        result.CanCreatePullRequest.Should().BeFalse();
        result.CanMergeToSource.Should().BeTrue();
    }

    /// <summary><summary>PushBranchAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory.</summary>.</summary>
    [Fact]
    /// <summary>PushBranchAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory.</summary>
    public async Task PushBranchAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.PushBranchAsync(@"C:\temp\workspace", "feature/noop");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*PushBranchAsync*");
    }

    /// <summary><summary>PullAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory.</summary>.</summary>
    [Fact]
    /// <summary>PullAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory.</summary>
    public async Task PullAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.PullAsync(@"C:\temp\workspace");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*PullAsync*");
    }

    /// <summary><summary>MergeToSourceAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory.</summary>.</summary>
    [Fact]
    /// <summary>MergeToSourceAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory.</summary>
    public async Task MergeToSourceAsync_ShouldThrowNotSupportedException_WhenWorkspaceModeIsInSourceDirectory()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");
        var sut = new LocalDirectoryPlugin(new Mock<ICliRunner>(MockBehavior.Strict).Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.MergeToSourceAsync(@"C:\temp\workspace");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*MergeToSourceAsync*");
    }

    /// <summary>Verifiziert den direkten Merge-Entry-Point für die Dateisynchronisation vom Workspace zur Quelle.</summary>
    [Fact]
    public async Task MergeToSourceAsync_ShouldSynchronizeFilesAndDeleteGitDeletedEntries_InSeparateMode()
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

        await sut.MergeToSourceAsync(workspaceDir);

        (await File.ReadAllTextAsync(Path.Combine(sourceDir, "keep.txt"))).Should().Be("workspace-new");
        File.Exists(Path.Combine(sourceDir, "new-name.txt")).Should().BeTrue();
        File.Exists(Path.Combine(sourceDir, "removed.txt")).Should().BeFalse();
        File.Exists(Path.Combine(sourceDir, "old-name.txt")).Should().BeFalse();
        cli.VerifyAll();
    }

    /// <summary><summary>PullAsync_ShouldThrowInvalidOperationException_WhenWorkspaceContainsChanges.</summary>.</summary>
    [Fact]
    /// <summary>PullAsync_ShouldThrowInvalidOperationException_WhenWorkspaceContainsChanges.</summary>
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

    /// <summary><summary>PushBranchAsync_ShouldThrowInvalidOperationException_WhenWorkspaceEqualsSource.</summary>.</summary>
    [Fact]
    /// <summary>PushBranchAsync_ShouldThrowInvalidOperationException_WhenWorkspaceEqualsSource.</summary>
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

    /// <summary><summary>PushBranchAsync_ShouldThrowInvalidOperationException_WhenGitStatusForDeleteSyncFails.</summary>.</summary>
    [Fact]
    /// <summary>PushBranchAsync_ShouldThrowInvalidOperationException_WhenGitStatusForDeleteSyncFails.</summary>
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

    /// <summary><summary>CloneRepositoryAsync_ShouldFallbackToSeparateMode_WhenWorkspaceModeIsInvalid.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldFallbackToSeparateMode_WhenWorkspaceModeIsInvalid.</summary>
    public async Task CloneRepositoryAsync_ShouldFallbackToSeparateMode_WhenWorkspaceModeIsInvalid()
    {
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var targetDir = Directory.CreateTempSubdirectory().FullName;
        Directory.Delete(targetDir, recursive: true);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "copy.txt"), "copied");

        var cli = new Mock<ICliRunner>(MockBehavior.Strict);
        cli.SetupSequence(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "not a git repository"))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "init" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Initialized empty Git repository", string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "config", "user.name", "Softwareschmiede" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "config", "user.email", "noreply@softwareschmiede.local" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "add", "." })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        cli.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "commit", "-m", "Initial workspace snapshot" })),
                targetDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "[master (root-commit)] Initial workspace snapshot", string.Empty));

        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("invalid-value");
        store.Setup(s => s.GetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory")).Returns("false");
        var sut = new LocalDirectoryPlugin(cli.Object, store.Object, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourceDir, targetDir);

        File.Exists(Path.Combine(targetDir, "copy.txt")).Should().BeTrue();
        cli.VerifyAll();
    }

    /// <summary><summary>CloneRepositoryAsync_ShouldThrow_WhenNoSourceDirectoryIsProvided.</summary>.</summary>
    [Fact]
    /// <summary>CloneRepositoryAsync_ShouldThrow_WhenNoSourceDirectoryIsProvided.</summary>
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

    /// <summary><summary>PullAsync_ShouldUseConfiguredSourceDirectory_WhenPointerAndRemoteAreMissing.</summary>.</summary>
    [Fact]
    /// <summary>PullAsync_ShouldUseConfiguredSourceDirectory_WhenPointerAndRemoteAreMissing.</summary>
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

    /// <summary><summary>UnsupportedRemoteMethods_ShouldThrowNotSupportedException.</summary>.</summary>
    [Fact]
    /// <summary>UnsupportedRemoteMethods_ShouldThrowNotSupportedException.</summary>
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
