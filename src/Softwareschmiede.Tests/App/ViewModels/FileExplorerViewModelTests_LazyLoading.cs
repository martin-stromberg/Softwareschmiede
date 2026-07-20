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
    public async Task LadeKinderAsync_LaedtKinderUndSetztChildrenLoaded()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 1, ChildrenLoaded = false };
        node.Children.Add(new WorkspaceFileNode { IsPlaceholder = true });
        var kinder = new List<WorkspaceFileNode> { new() { Name = "Program.cs", RelativePath = Path.Combine("src", "Program.cs"), Depth = 2 } };
        gitMock.Setup(g => g.LoadSubtreeAsync(RepositoryPath, "src", 2, It.IsAny<CancellationToken>())).ReturnsAsync(kinder);

        await sut.LadeKinderAsync(node);

        node.ChildrenLoaded.Should().BeTrue();
        node.Children.Should().ContainSingle(n => n.Name == "Program.cs");
    }

    /// <summary>Ist ChildrenLoaded bereits true, erfolgt kein Service-Aufruf.</summary>
    [Fact]
    public async Task LadeKinderAsync_BereitsGeladen_LaedtNichtErneut()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 1, ChildrenLoaded = true };

        await sut.LadeKinderAsync(node);

        gitMock.Verify(g => g.LoadSubtreeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Für einen Datei-Knoten erfolgt kein Service-Aufruf.</summary>
    [Fact]
    public async Task LadeKinderAsync_KeinVerzeichnis_TutNichts()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt", IsDirectory = false };

        await sut.LadeKinderAsync(node);

        gitMock.Verify(g => g.LoadSubtreeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Wirft der Service beim Nachladen, wird der Fehler geloggt und ChildrenLoaded bleibt false (erneutes Laden möglich).</summary>
    [Fact]
    public async Task LadeKinderAsync_Fehler_LaesstChildrenLoadedFalse()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 1, ChildrenLoaded = false };
        node.Children.Add(new WorkspaceFileNode { IsPlaceholder = true });
        gitMock.Setup(g => g.LoadSubtreeAsync(RepositoryPath, "src", 2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await sut.LadeKinderAsync(node);

        node.ChildrenLoaded.Should().BeFalse();
        node.Children.Should().ContainSingle(n => n.IsPlaceholder);
    }

    /// <summary>Beim Zuklappen werden Groß-Enkel-Knoten (Depth > node.Depth + 1) entfernt; die betroffenen Kinder erhalten wieder ChildrenLoaded = false und einen Platzhalter.</summary>
    [Fact]
    public void BeraeumeKnoten_EntferntGrossEnkel()
    {
        var (sut, _, _) = CreateSut();

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 0, ChildrenLoaded = true };
        var kind = new WorkspaceFileNode { Name = "nested", RelativePath = Path.Combine("src", "nested"), IsDirectory = true, Depth = 1, ChildrenLoaded = true };
        kind.Children.Add(new WorkspaceFileNode { Name = "Deep.cs", RelativePath = Path.Combine("src", "nested", "Deep.cs"), Depth = 2 });
        node.Children.Add(kind);

        sut.BeraeumeKnoten(node);

        kind.ChildrenLoaded.Should().BeFalse();
        kind.Children.Should().ContainSingle(n => n.IsPlaceholder);
    }

    /// <summary>Nach dem Zuklappen bleiben der Knoten selbst und seine direkten Kinder erhalten - pro Verzeichnis ist stets genau eine Ebene mehr geladen als sichtbar.</summary>
    [Fact]
    public void BeraeumeKnoten_BehaeltDirekteKinderUndPlatzhalterInvariante()
    {
        var (sut, _, _) = CreateSut();

        var node = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true, Depth = 0, ChildrenLoaded = true };
        var kind = new WorkspaceFileNode { Name = "nested", RelativePath = Path.Combine("src", "nested"), IsDirectory = true, Depth = 1, ChildrenLoaded = true };
        kind.Children.Add(new WorkspaceFileNode { Name = "Deep.cs", RelativePath = Path.Combine("src", "nested", "Deep.cs"), Depth = 2 });
        node.Children.Add(kind);

        sut.BeraeumeKnoten(node);

        node.Depth.Should().Be(0);
        node.Children.Should().ContainSingle(n => n.Name == "nested");
        node.Children.Single().Depth.Should().Be(1);
    }

    /// <summary>Die Auswahl eines Platzhalterknotens lädt keine Vorschau.</summary>
    [Fact]
    public async Task Platzhalterknoten_WirdNichtAlsAuswahlBehandelt()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var placeholder = new WorkspaceFileNode { IsPlaceholder = true };

        sut.AusgewaehlterKnoten = placeholder;

        sut.DateiInhalt.Should().BeNull();
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
