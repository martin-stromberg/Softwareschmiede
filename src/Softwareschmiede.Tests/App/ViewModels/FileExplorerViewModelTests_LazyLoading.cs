using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für das Lazy-Loading des Verzeichnisbaums von FileExplorerViewModel (LadeKinderAsync, BeraeumeKnoten, Platzhalter-Guard).</summary>
public sealed class FileExplorerViewModelTests_LazyLoading
{
    private const string RepositoryPath = "repo";

    /// <summary>Das Aufklappen eines Grenztiefen-Verzeichnisses lädt dessen Kinder nach, ersetzt den Platzhalter durch echte Kinder und setzt ChildrenLoaded.</summary>
    [Fact]
    public async Task LoadChildrenAsync_LoadsChildrenAndSetsChildrenLoaded()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 1, ChildrenLoaded = false };
        node.Children.Add(new WorkspaceFileNode { IsPlaceholder = true });
        var children = new List<WorkspaceFileNode> { new() { Name = "Program.cs", RelativePath = Path.Combine("src", "Program.cs"), Depth = 2 } };
        gitMock.Setup(g => g.LoadSubtreeAsync(RepositoryPath, "src", 2, It.IsAny<CancellationToken>())).ReturnsAsync(children);

        await sut.LoadChildrenAsync(node, TestContext.Current.CancellationToken);

        node.ChildrenLoaded.Should().BeTrue();
        node.Children.Should().ContainSingle(n => n.Name == "Program.cs");
    }

    /// <summary>Ist ChildrenLoaded bereits true, erfolgt kein Service-Aufruf.</summary>
    [Fact]
    public async Task LoadChildrenAsync_ReadyLoaded_LoadsNotAgain()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 1, ChildrenLoaded = true };

        await sut.LoadChildrenAsync(node, TestContext.Current.CancellationToken);

        gitMock.Verify(g => g.LoadSubtreeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Für einen Datei-Knoten erfolgt kein Service-Aufruf.</summary>
    [Fact]
    public async Task LoadChildrenAsync_NoDirectory_DoesNothing()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt", IsDirectory = false };

        await sut.LoadChildrenAsync(node, TestContext.Current.CancellationToken);

        gitMock.Verify(g => g.LoadSubtreeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Wirft der Service beim Nachladen, wird der Fehler geloggt und ChildrenLoaded bleibt false (erneutes Laden möglich).</summary>
    [Fact]
    public async Task LoadChildrenAsync_Error_LetsChildrenLoadedFalse()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 1, ChildrenLoaded = false };
        node.Children.Add(new WorkspaceFileNode { IsPlaceholder = true });
        gitMock.Setup(g => g.LoadSubtreeAsync(RepositoryPath, "src", 2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await sut.LoadChildrenAsync(node, TestContext.Current.CancellationToken);

        node.ChildrenLoaded.Should().BeFalse();
        node.Children.Should().ContainSingle(n => n.IsPlaceholder);
    }

    /// <summary>Beim Zuklappen werden Groß-Enkel-Knoten (Depth > node.Depth + 1) entfernt; die betroffenen Kinder erhalten wieder ChildrenLoaded = false und einen Platzhalter.</summary>
    [Fact]
    public void CleanNode_RemovesLargeGrandchild()
    {
        var (sut, _, _) = CreateSut();

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 0, ChildrenLoaded = true };
        var kind = new WorkspaceFileNode { Name = "nested", RelativePath = Path.Combine("src", "nested"), IsDirectory = true, Depth = 1, ChildrenLoaded = true };
        kind.Children.Add(new WorkspaceFileNode { Name = "Deep.cs", RelativePath = Path.Combine("src", "nested", "Deep.cs"), Depth = 2 });
        node.Children.Add(kind);

        sut.CleanNode(node);

        kind.ChildrenLoaded.Should().BeFalse();
        kind.Children.Should().ContainSingle(n => n.IsPlaceholder);
    }

    /// <summary>Nach dem Zuklappen bleiben der Knoten selbst und seine direkten Kinder erhalten - pro Verzeichnis ist stets genau eine Ebene mehr geladen als sichtbar.</summary>
    [Fact]
    public void CleanNode_KeepsDirectChildrenAndPlaceholderInvariant()
    {
        var (sut, _, _) = CreateSut();

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 0, ChildrenLoaded = true };
        var kind = new WorkspaceFileNode { Name = "nested", RelativePath = Path.Combine("src", "nested"), IsDirectory = true, Depth = 1, ChildrenLoaded = true };
        kind.Children.Add(new WorkspaceFileNode { Name = "Deep.cs", RelativePath = Path.Combine("src", "nested", "Deep.cs"), Depth = 2 });
        node.Children.Add(kind);

        sut.CleanNode(node);

        node.Depth.Should().Be(0);
        node.Children.Should().ContainSingle(n => n.Name == "nested");
        node.Children.Single().Depth.Should().Be(1);
    }

    /// <summary>Die Auswahl eines Platzhalterknotens lädt keine Vorschau.</summary>
    [Fact]
    public async Task Placeholdernode_WillNotAsSelectionHandles()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var placeholder = new WorkspaceFileNode { IsPlaceholder = true };

        sut.SelectedNode = placeholder;

        sut.FileContent.Should().BeNull();
        gitMock.Verify(g => g.LoadPreviewAsync(It.IsAny<string>(), It.IsAny<WorkspaceFileNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (FileExplorerViewModel Sut, Mock<IGitWorkspaceBrowserService> GitMock, Mock<ITextDiffService> DiffMock) CreateSut()
    {
        var gitMock = new Mock<IGitWorkspaceBrowserService>();
        var diffMock = new Mock<ITextDiffService>();
        var sut = new FileExplorerViewModel(gitMock.Object, diffMock.Object, NullLogger<FileExplorerViewModel>.Instance);
        return (sut, gitMock, diffMock);
    }
}
