using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für GitWorkspaceBrowserService.LoadWorkingTreeAsync (Standardmodus-Baumaufzählung).</summary>
public sealed class GitWorkspaceBrowserServiceWorkingTreeTests : IDisposable
{
    private readonly List<string> _tempDirectories = [];

    /// <summary>Löscht alle temporären Testverzeichnisse.</summary>
    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>Listet Dateien und verschachtelte Verzeichnisse des Arbeitsbaums vollständig auf.</summary>
    [Fact]
    public async Task LoadWorkingTreeAsync_ListetDateienUndVerzeichnisse()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "nested"));
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "readme.md"), "root file");
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "src", "Program.cs"), "code");
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "src", "nested", "Deep.cs"), "deep code");

        var service = CreateService();

        var nodes = await service.LoadWorkingTreeAsync(repositoryPath);

        nodes.Should().Contain(node => !node.IsDirectory && node.Name == "readme.md");
        var srcNode = nodes.Should().ContainSingle(node => node.IsDirectory && node.Name == "src").Subject;
        srcNode.Children.Should().Contain(node => !node.IsDirectory && node.Name == "Program.cs");
        var nestedNode = srcNode.Children.Should().ContainSingle(node => node.IsDirectory && node.Name == "nested").Subject;
        nestedNode.Children.Should().ContainSingle(node => !node.IsDirectory && node.Name == "Deep.cs");
    }

    /// <summary>Das .git-Verzeichnis wird von der Baumaufzählung ausgeschlossen.</summary>
    [Fact]
    public async Task LoadWorkingTreeAsync_SchliesstGitVerzeichnisAus()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, ".git"));
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, ".git", "config"), "git-internals");
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "readme.md"), "root file");

        var service = CreateService();

        var nodes = await service.LoadWorkingTreeAsync(repositoryPath);

        nodes.Should().NotContain(node => node.Name == ".git");
        nodes.Should().ContainSingle(node => node.Name == "readme.md");
    }

    /// <summary>Ein nicht existierender Pfad liefert eine leere Liste ohne Ausnahme.</summary>
    [Fact]
    public async Task LoadWorkingTreeAsync_NichtExistierenderPfad_LiefertLeereListe()
    {
        var missingPath = Path.Combine(CreateTempDirectory(), "missing");
        var service = CreateService();

        var nodes = await service.LoadWorkingTreeAsync(missingPath);

        nodes.Should().BeEmpty();
    }

    private static GitWorkspaceBrowserService CreateService()
        => new(new Mock<ICliRunner>().Object, NullLogger<GitWorkspaceBrowserService>.Instance);

    private string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"softwareschmiede-worktree-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }
}
