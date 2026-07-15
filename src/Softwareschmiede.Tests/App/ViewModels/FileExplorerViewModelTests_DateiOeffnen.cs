using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für DateiMitStandardanwendungOeffnenCommand von FileExplorerViewModel.</summary>
public sealed class FileExplorerViewModelTests_DateiOeffnen
{
    /// <summary>Ohne ausgewählten Knoten ist der Command nicht aktiv.</summary>
    [Fact]
    public void DateiMitStandardanwendungOeffnenCommand_OhneAuswahl_CanExecuteFalse()
    {
        var sut = CreateSut();

        sut.DateiMitStandardanwendungOeffnenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Für eine normale, nicht gelöschte Datei ist der Command aktiv. Die CanExecute-Prüfung hängt nur vom
    /// synchron gesetzten Knoten ab, nicht vom asynchron nachgeladenen Vorschauinhalt.</summary>
    [Fact]
    public void DateiMitStandardanwendungOeffnenCommand_NichtGeloeschteDatei_CanExecuteTrue()
    {
        var sut = CreateSut();
        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };

        sut.AusgewaehlterKnoten = node;

        sut.DateiMitStandardanwendungOeffnenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Für ein Verzeichnis ist der Command nicht aktiv.</summary>
    [Fact]
    public void DateiMitStandardanwendungOeffnenCommand_Verzeichnis_CanExecuteFalse()
    {
        var sut = CreateSut();
        var directoryNode = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true };

        sut.AusgewaehlterKnoten = directoryNode;

        sut.DateiMitStandardanwendungOeffnenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Für eine als gelöscht markierte Datei ist der Command nicht aktiv.</summary>
    [Fact]
    public void DateiMitStandardanwendungOeffnenCommand_GeloeschteDatei_CanExecuteFalse()
    {
        var sut = CreateSut();
        var deletedNode = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt", IsDeleted = true };

        sut.AusgewaehlterKnoten = deletedNode;

        sut.DateiMitStandardanwendungOeffnenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Wechselt die Auswahl von einer gültigen Datei zurück auf keinen Knoten (z. B. nach Aktualisieren), wird der
    /// Command wieder inaktiv.</summary>
    [Fact]
    public void DateiMitStandardanwendungOeffnenCommand_AuswahlAufgehoben_CanExecuteFalse()
    {
        var sut = CreateSut();
        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };
        sut.AusgewaehlterKnoten = node;
        sut.DateiMitStandardanwendungOeffnenCommand.CanExecute(null).Should().BeTrue();

        sut.AusgewaehlterKnoten = null;

        sut.DateiMitStandardanwendungOeffnenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Für einen aus einem historischen Commit stammenden Knoten (Vergleichsmodus, <c>CommitSha</c> gesetzt) ist der
    /// Command nicht aktiv, da die angezeigte Vorschau (historischer Inhalt) nicht zwingend mit der Datei auf der
    /// Festplatte übereinstimmt.</summary>
    [Fact]
    public void DateiMitStandardanwendungOeffnenCommand_CommitKnoten_CanExecuteFalse()
    {
        var sut = CreateSut();
        var commitNode = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt", CommitSha = "abc123" };

        sut.AusgewaehlterKnoten = commitNode;

        sut.DateiMitStandardanwendungOeffnenCommand.CanExecute(null).Should().BeFalse();
    }

    private static FileExplorerViewModel CreateSut()
    {
        var gitMock = new Mock<IGitWorkspaceBrowserService>();
        var diffMock = new Mock<ITextDiffService>();
        return new FileExplorerViewModel(gitMock.Object, diffMock.Object, NullLogger<FileExplorerViewModel>.Instance);
    }
}
