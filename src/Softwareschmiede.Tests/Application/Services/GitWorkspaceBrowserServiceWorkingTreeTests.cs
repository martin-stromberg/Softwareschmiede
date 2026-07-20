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

    /// <summary>Listet Dateien und Verzeichnisse innerhalb der Standard-Ladetiefe (maxInitialDepth = 2) auf; tiefere Einträge werden nicht mitgeladen.</summary>
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
        nestedNode.ChildrenLoaded.Should().BeFalse();
        nestedNode.Children.Should().ContainSingle(node => node.IsPlaceholder);
    }

    /// <summary>Bei maxInitialDepth = 2 sind nur Ebene 0 und 1 als echte Knoten geladen; tiefere Einträge fehlen.</summary>
    [Fact]
    public async Task LoadWorkingTreeAsync_LaedtNurMaxInitialDepthEbenen()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "nested"));
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "src", "nested", "Deep.cs"), "deep code");

        var service = CreateService();

        var nodes = await service.LoadWorkingTreeAsync(repositoryPath, maxInitialDepth: 2);

        var srcNode = nodes.Should().ContainSingle(node => node.IsDirectory && node.Name == "src").Subject;
        var nestedNode = srcNode.Children.Should().ContainSingle(node => node.IsDirectory && node.Name == "nested").Subject;
        nestedNode.Children.Should().NotContain(node => node.Name == "Deep.cs");
    }

    /// <summary>Depth wird für die oberste Ebene auf 0 und für deren Kinder auf 1 gesetzt.</summary>
    [Fact]
    public async Task LoadWorkingTreeAsync_SetztDepthKorrekt()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src"));
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "src", "Program.cs"), "code");

        var service = CreateService();

        var nodes = await service.LoadWorkingTreeAsync(repositoryPath, maxInitialDepth: 2);

        var srcNode = nodes.Should().ContainSingle(node => node.IsDirectory && node.Name == "src").Subject;
        srcNode.Depth.Should().Be(0);
        var programNode = srcNode.Children.Should().ContainSingle(node => node.Name == "Program.cs").Subject;
        programNode.Depth.Should().Be(1);
    }

    /// <summary>Ein Verzeichnis auf der Grenztiefe hat ChildrenLoaded == false und genau einen Platzhalter-Kindknoten.</summary>
    [Fact]
    public async Task LoadWorkingTreeAsync_GrenztiefeVerzeichnis_ChildrenLoadedFalseUndPlatzhalter()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "nested"));

        var service = CreateService();

        var nodes = await service.LoadWorkingTreeAsync(repositoryPath, maxInitialDepth: 2);

        var srcNode = nodes.Should().ContainSingle(node => node.IsDirectory && node.Name == "src").Subject;
        var nestedNode = srcNode.Children.Should().ContainSingle(node => node.IsDirectory && node.Name == "nested").Subject;
        nestedNode.ChildrenLoaded.Should().BeFalse();
        nestedNode.Children.Should().ContainSingle();
        nestedNode.Children.Single().IsPlaceholder.Should().BeTrue();
    }

    /// <summary>Ein Verzeichnis oberhalb der Grenztiefe hat echte Kinder und ChildrenLoaded == true, keinen Platzhalter.</summary>
    [Fact]
    public async Task LoadWorkingTreeAsync_ObereEbeneVerzeichnis_ChildrenLoadedTrue()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src"));
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "src", "Program.cs"), "code");

        var service = CreateService();

        var nodes = await service.LoadWorkingTreeAsync(repositoryPath, maxInitialDepth: 2);

        var srcNode = nodes.Should().ContainSingle(node => node.IsDirectory && node.Name == "src").Subject;
        srcNode.ChildrenLoaded.Should().BeTrue();
        srcNode.Children.Should().NotContain(node => node.IsPlaceholder);
        srcNode.Children.Should().ContainSingle(node => node.Name == "Program.cs");
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

    /// <summary>LoadSubtreeAsync gibt genau die unmittelbaren Kinder von parentPath zurück.</summary>
    [Fact]
    public async Task LoadSubtreeAsync_LaedtEineEbeneUnterhalbParent()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "nested"));
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "src", "Program.cs"), "code");

        var service = CreateService();

        var nodes = await service.LoadSubtreeAsync(repositoryPath, "src", 1);

        nodes.Should().Contain(node => !node.IsDirectory && node.Name == "Program.cs");
        nodes.Should().Contain(node => node.IsDirectory && node.Name == "nested");
        nodes.Should().HaveCount(2);
    }

    /// <summary>Alle von LoadSubtreeAsync zurückgegebenen Knoten haben Depth == depth.</summary>
    [Fact]
    public async Task LoadSubtreeAsync_SetztDepthAufUebergebenenWert()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "nested"));
        await File.WriteAllTextAsync(Path.Combine(repositoryPath, "src", "Program.cs"), "code");

        var service = CreateService();

        var nodes = await service.LoadSubtreeAsync(repositoryPath, "src", 3);

        nodes.Should().NotBeEmpty();
        nodes.Should().OnlyContain(node => node.Depth == 3);
    }

    /// <summary>Zurückgegebene Unterverzeichnisse tragen einen Platzhalter und ChildrenLoaded == false.</summary>
    [Fact]
    public async Task LoadSubtreeAsync_UnterverzeichnisMitPlatzhalterUndChildrenLoadedFalse()
    {
        var repositoryPath = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "nested"));

        var service = CreateService();

        var nodes = await service.LoadSubtreeAsync(repositoryPath, "src", 1);

        var nestedNode = nodes.Should().ContainSingle(node => node.IsDirectory && node.Name == "nested").Subject;
        nestedNode.ChildrenLoaded.Should().BeFalse();
        nestedNode.Children.Should().ContainSingle(node => node.IsPlaceholder);
    }

    /// <summary>Ein nicht existierender parentPath liefert eine leere Liste ohne Ausnahme.</summary>
    [Fact]
    public async Task LoadSubtreeAsync_NichtExistierenderPfad_LeereListe()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService();

        var nodes = await service.LoadSubtreeAsync(repositoryPath, "does-not-exist", 1);

        nodes.Should().BeEmpty();
    }

    /// <summary>Prüft den Schutz gegen Pfad-Traversal außerhalb des Repository-Roots.</summary>
    [Fact]
    public async Task LoadSubtreeAsync_PfadAusserhalbRepository_WirftException()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService();

        var act = () => service.LoadSubtreeAsync(repositoryPath, Path.Combine("..", "..", "outside"), 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*außerhalb des Repository-Roots*");
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
