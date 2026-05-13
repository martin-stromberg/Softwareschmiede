using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.IntegrationTests.Infrastructure.Plugins;

public sealed class LocalDirectoryPluginIntegrationTests : IDisposable
{
    private readonly List<string> _tempPaths = [];

    [Fact]
    public async Task CloneBranchCommitReset_ShouldWork_InSeparateWorkingDirectoryMode()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var targetPath = Path.Combine(Path.GetTempPath(), $"local-plugin-target-{Guid.NewGuid():N}");
        _tempPaths.Add(targetPath);
        await InitializeRepositoryAsync(cliRunner, sourcePath);

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourcePath, targetPath);
        await sut.CreateBranchAsync(targetPath, "feature/integration");
        await File.AppendAllTextAsync(Path.Combine(targetPath, "readme.txt"), "\nchange");
        await sut.CommitAsync(targetPath, "feat: local change");

        var countBeforeReset = await ReadCommitCountAsync(cliRunner, targetPath);
        await sut.ResetAsync(targetPath, "hard", "HEAD~1");
        var countAfterReset = await ReadCommitCountAsync(cliRunner, targetPath);

        Directory.Exists(Path.Combine(targetPath, ".git")).Should().BeTrue();
        countAfterReset.Should().Be(countBeforeReset - 1);
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldRunInSourceDirectory_WhenModeIsInSourceDirectory()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        await File.WriteAllTextAsync(Path.Combine(sourcePath, "readme.txt"), "demo");
        var pointerPath = Path.Combine(Path.GetTempPath(), $"local-plugin-pointer-{Guid.NewGuid():N}");
        _tempPaths.Add(pointerPath);

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "InSourceDirectory");
        credentials.SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourcePath, pointerPath);
        await sut.CreateBranchAsync(pointerPath, "feature/from-source");

        var currentBranch = await ReadCurrentBranchAsync(cliRunner, sourcePath);
        Directory.Exists(Path.Combine(sourcePath, ".git")).Should().BeTrue();
        currentBranch.Should().Be("feature/from-source");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldFail_WhenSourceWorkspaceIsDirty()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var targetPath = Path.Combine(Path.GetTempPath(), $"local-plugin-dirty-{Guid.NewGuid():N}");
        _tempPaths.Add(targetPath);
        await InitializeRepositoryAsync(cliRunner, sourcePath);
        await File.AppendAllTextAsync(Path.Combine(sourcePath, "readme.txt"), "\ndirty");

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "InSourceDirectory");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(sourcePath, targetPath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*uncommitted changes*");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldFail_WhenSeparateTargetEqualsSource()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(sourcePath, sourcePath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*erfordert ein anderes Zielverzeichnis*");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldFail_WhenSeparateTargetIsNotEmpty()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var targetPath = CreateTempDirectory();
        await File.WriteAllTextAsync(Path.Combine(targetPath, "existing.txt"), "x");

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        var act = () => sut.CloneRepositoryAsync(sourcePath, targetPath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ist nicht leer*");
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldUseSourceDirectoryFallback_WhenRepositoryUrlEmpty()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var targetPath = Path.Combine(Path.GetTempPath(), $"local-plugin-fallback-{Guid.NewGuid():N}");
        _tempPaths.Add(targetPath);
        await InitializeRepositoryAsync(cliRunner, sourcePath);

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "SeparateWorkingDirectory");
        credentials.SetCredential("LocalDirectoryPlugin.SourceDirectory", sourcePath);
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(string.Empty, targetPath);

        Directory.Exists(Path.Combine(targetPath, ".git")).Should().BeTrue();
        File.Exists(Path.Combine(targetPath, "readme.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldUseCopyFallback_WhenSeparateModeSourceIsNotGitAndInitNotConfirmed()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var targetPath = Path.Combine(Path.GetTempPath(), $"local-plugin-copy-fallback-{Guid.NewGuid():N}");
        _tempPaths.Add(targetPath);
        await File.WriteAllTextAsync(Path.Combine(sourcePath, "copy-source.txt"), "copy-me");

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "SeparateWorkingDirectory");
        credentials.SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "false");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourcePath, targetPath);

        File.Exists(Path.Combine(targetPath, "copy-source.txt")).Should().BeTrue();
        Directory.Exists(Path.Combine(targetPath, ".git")).Should().BeTrue();
    }

    [Fact]
    public async Task CloneRepositoryAsync_ShouldInitSourceAndClone_WhenSeparateModeSourceIsNotGitAndInitConfirmed()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var targetPath = Path.Combine(Path.GetTempPath(), $"local-plugin-init-clone-{Guid.NewGuid():N}");
        _tempPaths.Add(targetPath);
        await File.WriteAllTextAsync(Path.Combine(sourcePath, "untracked.txt"), "content");

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "SeparateWorkingDirectory");
        credentials.SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourcePath, targetPath);

        Directory.Exists(Path.Combine(sourcePath, ".git")).Should().BeTrue();
        Directory.Exists(Path.Combine(targetPath, ".git")).Should().BeTrue();
    }

    [Fact]
    public async Task PushBranchAsync_ShouldSynchronizeFilesAndDeleteTrackedRemovals_InSeparateMode()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var targetPath = Path.Combine(Path.GetTempPath(), $"local-plugin-push-sync-{Guid.NewGuid():N}");
        _tempPaths.Add(targetPath);
        await InitializeRepositoryAsync(cliRunner, sourcePath);
        await File.WriteAllTextAsync(Path.Combine(sourcePath, "delete-me.txt"), "delete");
        await File.WriteAllTextAsync(Path.Combine(sourcePath, "old-name.txt"), "old");
        await RunGitAsync(cliRunner, sourcePath, "add", ".");
        await RunGitAsync(cliRunner, sourcePath, "commit", "-m", "add files");

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourcePath, targetPath);
        await File.WriteAllTextAsync(Path.Combine(targetPath, "readme.txt"), "updated");
        File.Delete(Path.Combine(targetPath, "delete-me.txt"));
        await RunGitAsync(cliRunner, targetPath, "mv", "old-name.txt", "new-name.txt");

        await sut.PushBranchAsync(targetPath, "feature/sync");

        (await File.ReadAllTextAsync(Path.Combine(sourcePath, "readme.txt"))).Should().Be("updated");
        File.Exists(Path.Combine(sourcePath, "delete-me.txt")).Should().BeFalse();
        File.Exists(Path.Combine(sourcePath, "old-name.txt")).Should().BeFalse();
        File.Exists(Path.Combine(sourcePath, "new-name.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task PullAsync_ShouldRefreshWorkspaceFromSource_WithoutMerge()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var targetPath = Path.Combine(Path.GetTempPath(), $"local-plugin-pull-sync-{Guid.NewGuid():N}");
        _tempPaths.Add(targetPath);
        await InitializeRepositoryAsync(cliRunner, sourcePath);

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "SeparateWorkingDirectory");
        var sut = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        await sut.CloneRepositoryAsync(sourcePath, targetPath);
        await File.WriteAllTextAsync(Path.Combine(sourcePath, "readme.txt"), "source-new");
        await File.WriteAllTextAsync(Path.Combine(sourcePath, "from-source.txt"), "new");

        await sut.PullAsync(targetPath);

        (await File.ReadAllTextAsync(Path.Combine(targetPath, "readme.txt"))).Should().Be("source-new");
        File.Exists(Path.Combine(targetPath, "from-source.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task CreateBranchCommitReset_ShouldWork_AfterPluginRecreation_UsingPointerFile()
    {
        var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
        var sourcePath = CreateTempDirectory();
        var pointerPath = Path.Combine(Path.GetTempPath(), $"local-plugin-pointer-{Guid.NewGuid():N}");
        _tempPaths.Add(pointerPath);
        await InitializeRepositoryAsync(cliRunner, sourcePath);

        var credentials = new InMemoryCredentialStore();
        credentials.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "InSourceDirectory");
        credentials.SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        var firstInstance = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);

        await firstInstance.CloneRepositoryAsync(sourcePath, pointerPath);

        var secondInstance = new LocalDirectoryPlugin(cliRunner, credentials, NullLogger<LocalDirectoryPlugin>.Instance);
        await secondInstance.CreateBranchAsync(pointerPath, "feature/recreated");

        var currentBranch = await ReadCurrentBranchAsync(cliRunner, sourcePath);
        currentBranch.Should().Be("feature/recreated");
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths.Where(p => Directory.Exists(p)))
        {
            DeleteDirectoryForce(path);
        }
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"local-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempPaths.Add(path);
        return path;
    }

    private static async Task InitializeRepositoryAsync(CliRunner cliRunner, string repositoryPath)
    {
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "readme.txt"), "base");
        await RunGitAsync(cliRunner, repositoryPath, "init");
        await RunGitAsync(cliRunner, repositoryPath, "config", "user.name", "Integration Tester");
        await RunGitAsync(cliRunner, repositoryPath, "config", "user.email", "integration@test.local");
        await RunGitAsync(cliRunner, repositoryPath, "add", ".");
        await RunGitAsync(cliRunner, repositoryPath, "commit", "-m", "init");
    }

    private static async Task<int> ReadCommitCountAsync(CliRunner cliRunner, string repositoryPath)
    {
        var result = await RunGitAsync(cliRunner, repositoryPath, "rev-list", "--count", "HEAD");
        return int.Parse(result.StdOut.Trim());
    }

    private static async Task<string> ReadCurrentBranchAsync(CliRunner cliRunner, string repositoryPath)
    {
        var result = await RunGitAsync(cliRunner, repositoryPath, "branch", "--show-current");
        return result.StdOut.Trim();
    }

    private static async Task<Softwareschmiede.Domain.ValueObjects.CliResult> RunGitAsync(
        CliRunner cliRunner,
        string workingDirectory,
        params string[] args)
    {
        var result = await cliRunner.RunAsync("git", args, workingDirectory, null);
        result.IsSuccess.Should().BeTrue($"git {string.Join(' ', args)} sollte erfolgreich sein");
        return result;
    }

    private sealed class InMemoryCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

        public string? GetCredential(string target) => _store.GetValueOrDefault(target);

        public void SetCredential(string target, string value) => _store[target] = value;

        public void DeleteCredential(string target) => _store.Remove(target);
    }

    private static void DeleteDirectoryForce(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(path, recursive: true);
    }
}
