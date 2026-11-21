using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für die Navigation zwischen Änderungsblöcken im Diff von FileExplorerViewModel.</summary>
public sealed class FileExplorerViewModelTests_DiffNavigation
{
    private const string RepositoryPath = "repo";

    /// <summary>NaechsteAenderungCommand springt zum Start des ersten Änderungsblocks, wenn noch nicht navigiert wurde.</summary>
    [Fact]
    public async Task NextChangeCommand_FirstCall_JumpsToFirstBlock()
    {
        var sut = await CreateSutWithDiffLinesAsync(
            Line(DiffLineStatus.Context),
            Line(DiffLineStatus.Added),
            Line(DiffLineStatus.Added),
            Line(DiffLineStatus.Context),
            Line(DiffLineStatus.Removed));

        var focusedIndexes = new List<int>();
        sut.DiffLineFocused += focusedIndexes.Add;

        sut.NextChangeCommand.Execute(null);

        focusedIndexes.Should().ContainSingle().Which.Should().Be(1);
    }

    /// <summary>Wiederholtes Aufrufen von NaechsteAenderungCommand springt der Reihe nach durch alle Änderungsblöcke und danach zurück zum Anfang (Wrap-Around).</summary>
    [Fact]
    public async Task NextChangeCommand_MultipleCall_JumpsThroughAllBlocksAndWraps()
    {
        var sut = await CreateSutWithDiffLinesAsync(
            Line(DiffLineStatus.Context),
            Line(DiffLineStatus.Added),
            Line(DiffLineStatus.Context),
            Line(DiffLineStatus.Removed),
            Line(DiffLineStatus.Removed));

        var focusedIndexes = new List<int>();
        sut.DiffLineFocused += focusedIndexes.Add;

        sut.NextChangeCommand.Execute(null);
        sut.NextChangeCommand.Execute(null);
        sut.NextChangeCommand.Execute(null);

        focusedIndexes.Should().Equal(1, 3, 1);
    }

    /// <summary>VorherigeAenderungCommand springt beim ersten Aufruf zum letzten Änderungsblock.</summary>
    [Fact]
    public async Task PreviousChangeCommand_FirstCall_JumpsToLastBlock()
    {
        var sut = await CreateSutWithDiffLinesAsync(
            Line(DiffLineStatus.Added),
            Line(DiffLineStatus.Context),
            Line(DiffLineStatus.Modified));

        var focusedIndexes = new List<int>();
        sut.DiffLineFocused += focusedIndexes.Add;

        sut.PreviousChangeCommand.Execute(null);

        focusedIndexes.Should().ContainSingle().Which.Should().Be(2);
    }

    /// <summary>Ohne Diff-Zeilen (kein Vergleichsmodus aktiv) wird kein Fokus-Event ausgelöst.</summary>
    [Fact]
    public void NextChangeCommand_WithoutDiffLines_TriggersNoEventOut()
    {
        var sut = CreateSut();
        var eventTriggered = false;
        sut.DiffLineFocused += _ => eventTriggered = true;

        sut.NextChangeCommand.Execute(null);

        eventTriggered.Should().BeFalse();
    }

    private static TextDiffLine Line(DiffLineStatus status) => new("inhalt", status, 1, 1, []);

    private static FileExplorerViewModel CreateSut()
    {
        var gitMock = new Mock<IGitWorkspaceBrowserService>();
        var diffMock = new Mock<ITextDiffService>();
        return new FileExplorerViewModel(gitMock.Object, diffMock.Object, NullLogger<FileExplorerViewModel>.Instance);
    }

    private static async Task<FileExplorerViewModel> CreateSutWithDiffLinesAsync(params TextDiffLine[] lines)
    {
        var gitMock = new Mock<IGitWorkspaceBrowserService>();
        var diffMock = new Mock<ITextDiffService>();
        var sut = new FileExplorerViewModel(gitMock.Object, diffMock.Object, NullLogger<FileExplorerViewModel>.Instance);

        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" };
        var preview = new FilePreview("a.cs", null, false, false, false, "neu", "alt", null);
        gitMock.Setup(g => g.LoadCommitPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        diffMock.Setup(d => d.BuildDiff("alt", "neu")).Returns(new FileTextDiff(lines));

        sut.SelectedNode = node;
        await WaitForAsync(() => sut.DiffLines.Count == lines.Length);

        return sut;
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline && !condition())
            await Task.Delay(20);
    }
}
