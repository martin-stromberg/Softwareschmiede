using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für FileExplorerViewModel.</summary>
public sealed class FileExplorerViewModelTests
{
    private const string RepositoryPath = "repo";

    /// <summary>InitialisierenAsync lädt im Standardmodus den Arbeitsbaum und füllt Wurzelknoten.</summary>
    [Fact]
    public async Task Standard_LaedtWurzelknotenUeberWorkingTree()
    {
        var (sut, gitMock, _) = CreateSut();
        var nodes = new List<WorkspaceFileNode> { new() { Name = "a.txt", RelativePath = "a.txt" } };
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync(nodes);

        await sut.InitialisierenAsync(RepositoryPath);

        sut.AktuellerModus.Should().Be(DateibrowserAnsichtsmodus.Standard);
        sut.Wurzelknoten.Should().ContainSingle(node => node.Name == "a.txt");
        gitMock.Verify(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Die Auswahl eines Datei-Knotens im Standardmodus lädt die Vorschau und setzt DateiInhalt auf CurrentContent.</summary>
    [Fact]
    public async Task DateiAuswahl_Standard_SetztDateiInhaltAusPreview()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };
        var preview = new FilePreview("a.txt", null, false, false, false, "inhalt", null, null);
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>())).ReturnsAsync(preview);

        sut.AusgewaehlterKnoten = node;
        await WaitForAsync(() => sut.DateiInhalt == "inhalt");

        sut.DateiInhalt.Should().Be("inhalt");
        gitMock.Verify(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein schneller Auswahlwechsel bricht den Ladevorgang der vorherigen Auswahl über deren CancellationToken ab, sodass er den neueren Inhalt nicht überschreiben kann.</summary>
    [Fact]
    public async Task DateiAuswahl_SchnellerWechsel_BrichtTokenDesVorherigenLadevorgangsAb()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var nodeA = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };
        var nodeB = new WorkspaceFileNode { Name = "b.txt", RelativePath = "b.txt" };

        CancellationToken? capturedTokenForA = null;
        var previewACompletionSource = new TaskCompletionSource<FilePreview>();
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, nodeA, It.IsAny<CancellationToken>()))
            .Returns((string _, WorkspaceFileNode _, CancellationToken ct) =>
            {
                capturedTokenForA = ct;
                return previewACompletionSource.Task;
            });
        var previewB = new FilePreview("b.txt", null, false, false, false, "neu-inhalt", null, null);
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, nodeB, It.IsAny<CancellationToken>())).ReturnsAsync(previewB);

        sut.AusgewaehlterKnoten = nodeA;
        await WaitForAsync(() => capturedTokenForA is not null);

        sut.AusgewaehlterKnoten = nodeB;
        await WaitForAsync(() => sut.DateiInhalt == "neu-inhalt");

        capturedTokenForA!.Value.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>InitialisierenAsync bricht einen noch laufenden DateiLadenAsync-Vorgang ab, statt ihn den gerade geleerten Zustand mit veraltetem Inhalt überschreiben zu lassen.</summary>
    [Fact]
    public async Task InitialisierenAsync_BrichtLaufendenDateiLadevorgangAb()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };
        CancellationToken? capturedToken = null;
        var previewCompletionSource = new TaskCompletionSource<FilePreview>();
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>()))
            .Returns((string _, WorkspaceFileNode _, CancellationToken ct) =>
            {
                capturedToken = ct;
                return previewCompletionSource.Task;
            });

        sut.AusgewaehlterKnoten = node;
        await WaitForAsync(() => capturedToken is not null);

        await sut.InitialisierenAsync(RepositoryPath);

        capturedToken!.Value.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>AktualisierenAsync bricht einen noch laufenden DateiLadenAsync-Vorgang ab, statt ihn den gerade geleerten Zustand mit veraltetem Inhalt überschreiben zu lassen.</summary>
    [Fact]
    public async Task AktualisierenCommand_BrichtLaufendenDateiLadevorgangAb()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };
        CancellationToken? capturedToken = null;
        var previewCompletionSource = new TaskCompletionSource<FilePreview>();
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>()))
            .Returns((string _, WorkspaceFileNode _, CancellationToken ct) =>
            {
                capturedToken = ct;
                return previewCompletionSource.Task;
            });

        sut.AusgewaehlterKnoten = node;
        await WaitForAsync(() => capturedToken is not null);

        await ((AsyncRelayCommand)sut.AktualisierenCommand).ExecuteAsync();

        capturedToken!.Value.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>Schlägt das Laden der Dateivorschau fehl, zeigt DateiInhalt einen Hinweistext statt des veralteten Inhalts der vorherigen Datei, und DiffZeilen wird geleert.</summary>
    [Fact]
    public async Task DateiLadenAsync_FehlerBeimLaden_ZeigtHinweisUndLeertDiffZeilen()
    {
        var (sut, gitMock, diffMock) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var diffNode = new WorkspaceFileNode { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" };
        var preview = new FilePreview("a.cs", null, false, false, false, "neu", "alt", null);
        gitMock.Setup(g => g.LoadCommitPreviewAsync(RepositoryPath, diffNode, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        var diffLines = new List<TextDiffLine> { new("neu", DiffLineStatus.Modified, 1, 1, []) };
        diffMock.Setup(d => d.BuildDiff("alt", "neu")).Returns(new FileTextDiff(diffLines));
        sut.AusgewaehlterKnoten = diffNode;
        await WaitForAsync(() => sut.DiffZeilen.Count > 0);

        var failingNode = new WorkspaceFileNode { Name = "b.txt", RelativePath = "b.txt" };
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, failingNode, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        sut.AusgewaehlterKnoten = failingNode;
        await WaitForAsync(() => sut.DateiInhalt == "Datei konnte nicht geladen werden.");

        sut.DateiInhalt.Should().Be("Datei konnte nicht geladen werden.");
        sut.DiffZeilen.Should().BeEmpty();
    }

    /// <summary>Die Auswahl eines Verzeichnis-Knotens leert den zuvor angezeigten Dateiinhalt und die Diff-Zeilen, statt die veraltete Anzeige stehen zu lassen.</summary>
    [Fact]
    public async Task DateiAuswahl_Verzeichnis_LeertVorherigenDateiInhaltUndDiffZeilen()
    {
        var (sut, gitMock, diffMock) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var fileNode = new WorkspaceFileNode { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" };
        var preview = new FilePreview("a.cs", null, false, false, false, "neu", "alt", null);
        gitMock.Setup(g => g.LoadCommitPreviewAsync(RepositoryPath, fileNode, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        var diffLines = new List<TextDiffLine> { new("neu", DiffLineStatus.Modified, 1, 1, []) };
        diffMock.Setup(d => d.BuildDiff("alt", "neu")).Returns(new FileTextDiff(diffLines));

        sut.AusgewaehlterKnoten = fileNode;
        await WaitForAsync(() => sut.DiffZeilen.Count > 0);

        var directoryNode = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true };
        sut.AusgewaehlterKnoten = directoryNode;

        sut.DateiInhalt.Should().BeNull();
        sut.DiffZeilen.Should().BeEmpty();
    }

    /// <summary>Bei IsBinary/IsTooBig wird der Hinweistext statt des Inhalts angezeigt.</summary>
    [Fact]
    public async Task DateiAuswahl_BinaerOderZuGross_ZeigtHinweis()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "bin.dat", RelativePath = "bin.dat" };
        var preview = new FilePreview("bin.dat", null, false, true, false, null, null, "Binärdatei – Vorschau nicht verfügbar.");
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>())).ReturnsAsync(preview);

        sut.AusgewaehlterKnoten = node;
        await WaitForAsync(() => sut.DateiInhalt == preview.Hint);

        sut.DateiInhalt.Should().Be("Binärdatei – Vorschau nicht verfügbar.");
    }

    /// <summary>Der Wechsel in den Vergleichsmodus lädt den Snapshot und füllt CommitGruppen.</summary>
    [Fact]
    public async Task VergleichCommand_LaedtCommitsAusSnapshot()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var commits = new List<BranchCommit> { new() { Sha = "abc123", ShortSha = "abc123", Subject = "Testcommit" } };
        var snapshot = new WorkspaceSnapshot { RepositoryPath = RepositoryPath, BranchCommits = commits };
        gitMock.Setup(g => g.LoadSnapshotAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);

        await ((AsyncRelayCommand)sut.VergleichCommand).ExecuteAsync();

        sut.AktuellerModus.Should().Be(DateibrowserAnsichtsmodus.Vergleich);
        sut.CommitGruppen.Should().ContainSingle(commit => commit.Sha == "abc123");
    }

    /// <summary>Das Aufklappen eines Commits lädt dessen geänderte Dateien und setzt ChildrenLoaded.</summary>
    [Fact]
    public async Task CommitAufklappen_LaedtGeaenderteDateien()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var commit = new BranchCommit { Sha = "abc123", ShortSha = "abc123", Subject = "Testcommit" };
        var files = new List<WorkspaceFileNode> { new() { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" } };
        gitMock.Setup(g => g.LoadCommitFilesAsync(RepositoryPath, "abc123", It.IsAny<CancellationToken>())).ReturnsAsync(files);

        await sut.CommitAufklappenAsync(commit);

        commit.Files.Should().ContainSingle(file => file.Name == "a.cs");
        commit.ChildrenLoaded.Should().BeTrue();
    }

    /// <summary>Das Aufklappen eines Commits ändert CommitGruppen nicht (kein Remove/Insert-Umweg mehr) und benachrichtigt stattdessen über die Files-Collection selbst.</summary>
    [Fact]
    public async Task CommitAufklappen_AendertCommitGruppenNichtUndBenachrichtigtUeberFiles()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var commits = new List<BranchCommit> { new() { Sha = "abc123", ShortSha = "abc123", Subject = "Testcommit" } };
        gitMock.Setup(g => g.LoadSnapshotAsync(RepositoryPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSnapshot { RepositoryPath = RepositoryPath, BranchCommits = commits });
        await ((AsyncRelayCommand)sut.VergleichCommand).ExecuteAsync();

        var commit = sut.CommitGruppen.Should().ContainSingle().Subject;
        var files = new List<WorkspaceFileNode> { new() { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" } };
        gitMock.Setup(g => g.LoadCommitFilesAsync(RepositoryPath, "abc123", It.IsAny<CancellationToken>())).ReturnsAsync(files);

        var commitGruppenChanged = false;
        sut.CommitGruppen.CollectionChanged += (_, _) => commitGruppenChanged = true;
        var filesChanged = false;
        commit.Files.CollectionChanged += (_, _) => filesChanged = true;

        await sut.CommitAufklappenAsync(commit);

        commitGruppenChanged.Should().BeFalse();
        filesChanged.Should().BeTrue();
        commit.Files.Should().ContainSingle(file => file.Name == "a.cs");
    }

    /// <summary>Die Auswahl einer Commit-Datei im Vergleichsmodus lädt die Commit-Vorschau und erzeugt Diff-Zeilen über ITextDiffService.</summary>
    [Fact]
    public async Task DateiAuswahl_Vergleich_ErzeugtDiffZeilen()
    {
        var (sut, gitMock, diffMock) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        var node = new WorkspaceFileNode { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" };
        var preview = new FilePreview("a.cs", null, false, false, false, "neu", "alt", null);
        gitMock.Setup(g => g.LoadCommitPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>())).ReturnsAsync(preview);

        var diffLines = new List<TextDiffLine> { new("neu", DiffLineStatus.Modified, 1, 1, []) };
        diffMock.Setup(d => d.BuildDiff("alt", "neu")).Returns(new FileTextDiff(diffLines));

        sut.AusgewaehlterKnoten = node;
        await WaitForAsync(() => sut.DiffZeilen.Count > 0);

        sut.DiffZeilen.Should().ContainSingle();
        diffMock.Verify(d => d.BuildDiff("alt", "neu"), Times.Once);
    }

    /// <summary>AktualisierenCommand lädt je nach aktuellem Modus den Arbeitsbaum bzw. den Snapshot erneut.</summary>
    [Fact]
    public async Task AktualisierenCommand_LaedtAktuellenModusNeu()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitialisierenAsync(RepositoryPath);

        await ((AsyncRelayCommand)sut.AktualisierenCommand).ExecuteAsync();
        gitMock.Verify(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<CancellationToken>()), Times.Exactly(2));

        gitMock.Setup(g => g.LoadSnapshotAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceSnapshot { RepositoryPath = RepositoryPath });
        await ((AsyncRelayCommand)sut.VergleichCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.AktualisierenCommand).ExecuteAsync();

        gitMock.Verify(g => g.LoadSnapshotAsync(RepositoryPath, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static (FileExplorerViewModel Sut, Mock<IGitWorkspaceBrowserService> GitMock, Mock<ITextDiffService> DiffMock) CreateSut()
    {
        var gitMock = new Mock<IGitWorkspaceBrowserService>();
        var diffMock = new Mock<ITextDiffService>();
        var sut = new FileExplorerViewModel(gitMock.Object, diffMock.Object, NullLogger<FileExplorerViewModel>.Instance);
        return (sut, gitMock, diffMock);
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline && !condition())
            await Task.Delay(20);
    }
}
