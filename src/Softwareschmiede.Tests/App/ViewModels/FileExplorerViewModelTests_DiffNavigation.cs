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
    public async Task NaechsteAenderungCommand_ErsterAufruf_SpringtZuErstemBlock()
    {
        var sut = await CreateSutMitDiffZeilenAsync(
            Zeile(DiffLineStatus.Context),
            Zeile(DiffLineStatus.Added),
            Zeile(DiffLineStatus.Added),
            Zeile(DiffLineStatus.Context),
            Zeile(DiffLineStatus.Removed));

        var fokussierteIndizes = new List<int>();
        sut.DiffZeileFokussiert += fokussierteIndizes.Add;

        sut.NaechsteAenderungCommand.Execute(null);

        fokussierteIndizes.Should().ContainSingle().Which.Should().Be(1);
    }

    /// <summary>Wiederholtes Aufrufen von NaechsteAenderungCommand springt der Reihe nach durch alle Änderungsblöcke und danach zurück zum Anfang (Wrap-Around).</summary>
    [Fact]
    public async Task NaechsteAenderungCommand_MehrfachAufruf_SpringtDurchAlleBloeckeUndWrapt()
    {
        var sut = await CreateSutMitDiffZeilenAsync(
            Zeile(DiffLineStatus.Context),
            Zeile(DiffLineStatus.Added),
            Zeile(DiffLineStatus.Context),
            Zeile(DiffLineStatus.Removed),
            Zeile(DiffLineStatus.Removed));

        var fokussierteIndizes = new List<int>();
        sut.DiffZeileFokussiert += fokussierteIndizes.Add;

        sut.NaechsteAenderungCommand.Execute(null);
        sut.NaechsteAenderungCommand.Execute(null);
        sut.NaechsteAenderungCommand.Execute(null);

        fokussierteIndizes.Should().Equal(1, 3, 1);
    }

    /// <summary>VorherigeAenderungCommand springt beim ersten Aufruf zum letzten Änderungsblock.</summary>
    [Fact]
    public async Task VorherigeAenderungCommand_ErsterAufruf_SpringtZuLetztemBlock()
    {
        var sut = await CreateSutMitDiffZeilenAsync(
            Zeile(DiffLineStatus.Added),
            Zeile(DiffLineStatus.Context),
            Zeile(DiffLineStatus.Modified));

        var fokussierteIndizes = new List<int>();
        sut.DiffZeileFokussiert += fokussierteIndizes.Add;

        sut.VorherigeAenderungCommand.Execute(null);

        fokussierteIndizes.Should().ContainSingle().Which.Should().Be(2);
    }

    /// <summary>Ohne Diff-Zeilen (kein Vergleichsmodus aktiv) wird kein Fokus-Event ausgelöst.</summary>
    [Fact]
    public void NaechsteAenderungCommand_OhneDiffZeilen_LoestKeinEreignisAus()
    {
        var sut = CreateSut();
        var ereignisAusgeloest = false;
        sut.DiffZeileFokussiert += _ => ereignisAusgeloest = true;

        sut.NaechsteAenderungCommand.Execute(null);

        ereignisAusgeloest.Should().BeFalse();
    }

    private static TextDiffLine Zeile(DiffLineStatus status) => new("inhalt", status, 1, 1, []);

    private static FileExplorerViewModel CreateSut()
    {
        var gitMock = new Mock<IGitWorkspaceBrowserService>();
        var diffMock = new Mock<ITextDiffService>();
        return new FileExplorerViewModel(gitMock.Object, diffMock.Object, NullLogger<FileExplorerViewModel>.Instance);
    }

    private static async Task<FileExplorerViewModel> CreateSutMitDiffZeilenAsync(params TextDiffLine[] zeilen)
    {
        var gitMock = new Mock<IGitWorkspaceBrowserService>();
        var diffMock = new Mock<ITextDiffService>();
        var sut = new FileExplorerViewModel(gitMock.Object, diffMock.Object, NullLogger<FileExplorerViewModel>.Instance);

        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" };
        var preview = new FilePreview("a.cs", null, false, false, false, "neu", "alt", null);
        gitMock.Setup(g => g.LoadCommitPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        diffMock.Setup(d => d.BuildDiff("alt", "neu")).Returns(new FileTextDiff(zeilen));

        sut.AusgewaehlterKnoten = node;
        await WaitForAsync(() => sut.DiffZeilen.Count == zeilen.Length);

        return sut;
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline && !condition())
            await Task.Delay(20);
    }
}
