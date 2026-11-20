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
    public async Task Default_LoadsRootnodeOverWorkingTree()
    {
        var (sut, gitMock, _) = CreateSut();
        var nodes = new List<WorkspaceFileNode> { new() { Name = "a.txt", RelativePath = "a.txt" } };
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(nodes);

        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        sut.CurrentMode.Should().Be(FileBrowserViewMode.Default);
        sut.RootNodes.Should().ContainSingle(node => node.Name == "a.txt");
        gitMock.Verify(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Die Auswahl eines Datei-Knotens im Standardmodus lädt die Vorschau und setzt DateiInhalt auf CurrentContent.</summary>
    [Fact]
    public async Task FileSelection_Default_SetsFileContentOutPreview()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };
        var preview = new FilePreview("a.txt", null, false, false, false, "inhalt", null, null);
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>())).ReturnsAsync(preview);

        sut.SelectedNode = node;
        await WaitForAsync(() => sut.FileContent == "inhalt");

        sut.FileContent.Should().Be("inhalt");
        gitMock.Verify(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein schneller Auswahlwechsel bricht den Ladevorgang der vorherigen Auswahl über deren CancellationToken ab, sodass er den neueren Inhalt nicht überschreiben kann.</summary>
    [Fact]
    public async Task FileSelection_SwitchFast_CancelsTokenOfPreviousLoadprocessFrom()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

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

        sut.SelectedNode = nodeA;
        await WaitForAsync(() => capturedTokenForA is not null);

        sut.SelectedNode = nodeB;
        await WaitForAsync(() => sut.FileContent == "neu-inhalt");

        capturedTokenForA!.Value.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>InitialisierenAsync bricht einen noch laufenden DateiLadenAsync-Vorgang ab, statt ihn den gerade geleerten Zustand mit veraltetem Inhalt überschreiben zu lassen.</summary>
    [Fact]
    public async Task InitializeAsync_CancelsRunningFileLoadprocessFrom()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };
        CancellationToken? capturedToken = null;
        var previewCompletionSource = new TaskCompletionSource<FilePreview>();
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>()))
            .Returns((string _, WorkspaceFileNode _, CancellationToken ct) =>
            {
                capturedToken = ct;
                return previewCompletionSource.Task;
            });

        sut.SelectedNode = node;
        await WaitForAsync(() => capturedToken is not null);

        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        capturedToken!.Value.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>AktualisierenAsync bricht einen noch laufenden DateiLadenAsync-Vorgang ab, statt ihn den gerade geleerten Zustand mit veraltetem Inhalt überschreiben zu lassen.</summary>
    [Fact]
    public async Task RefreshCommand_CancelsRunningFileLoadprocessFrom()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "a.txt", RelativePath = "a.txt" };
        CancellationToken? capturedToken = null;
        var previewCompletionSource = new TaskCompletionSource<FilePreview>();
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>()))
            .Returns((string _, WorkspaceFileNode _, CancellationToken ct) =>
            {
                capturedToken = ct;
                return previewCompletionSource.Task;
            });

        sut.SelectedNode = node;
        await WaitForAsync(() => capturedToken is not null);

        await ((AsyncRelayCommand)sut.RefreshCommand).ExecuteAsync();

        capturedToken!.Value.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>Schlägt das Laden der Dateivorschau fehl, zeigt DateiInhalt einen Hinweistext statt des veralteten Inhalts der vorherigen Datei, und DiffZeilen wird geleert.</summary>
    [Fact]
    public async Task LoadFileAsync_LoadErrorAt_ShowsNoticeAndClearsDiffLines()
    {
        var (sut, gitMock, diffMock) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var diffNode = new WorkspaceFileNode { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" };
        var preview = new FilePreview("a.cs", null, false, false, false, "neu", "alt", null);
        gitMock.Setup(g => g.LoadCommitPreviewAsync(RepositoryPath, diffNode, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        var diffLines = new List<TextDiffLine> { new("neu", DiffLineStatus.Modified, 1, 1, []) };
        diffMock.Setup(d => d.BuildDiff("alt", "neu")).Returns(new FileTextDiff(diffLines));
        sut.SelectedNode = diffNode;
        await WaitForAsync(() => sut.DiffLines.Count > 0);

        var failingNode = new WorkspaceFileNode { Name = "b.txt", RelativePath = "b.txt" };
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, failingNode, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        sut.SelectedNode = failingNode;
        await WaitForAsync(() => sut.FileContent == "Datei konnte nicht geladen werden.");

        sut.FileContent.Should().Be("Datei konnte nicht geladen werden.");
        sut.DiffLines.Should().BeEmpty();
    }

    /// <summary>Die Auswahl eines Verzeichnis-Knotens leert den zuvor angezeigten Dateiinhalt und die Diff-Zeilen, statt die veraltete Anzeige stehen zu lassen.</summary>
    [Fact]
    public async Task FileSelection_Directory_ClearsPreviousFileContentAndDiffLines()
    {
        var (sut, gitMock, diffMock) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var fileNode = new WorkspaceFileNode { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" };
        var preview = new FilePreview("a.cs", null, false, false, false, "neu", "alt", null);
        gitMock.Setup(g => g.LoadCommitPreviewAsync(RepositoryPath, fileNode, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        var diffLines = new List<TextDiffLine> { new("neu", DiffLineStatus.Modified, 1, 1, []) };
        diffMock.Setup(d => d.BuildDiff("alt", "neu")).Returns(new FileTextDiff(diffLines));

        sut.SelectedNode = fileNode;
        await WaitForAsync(() => sut.DiffLines.Count > 0);

        var directoryNode = new WorkspaceFileNode { Name = "src", RelativePath = "src", IsDirectory = true };
        sut.SelectedNode = directoryNode;

        sut.FileContent.Should().BeNull();
        sut.DiffLines.Should().BeEmpty();
    }

    /// <summary>Bei IsBinary/IsTooBig wird der Hinweistext statt des Inhalts angezeigt.</summary>
    [Theory]
    [InlineData(true, false, "Binärdatei – Vorschau nicht verfügbar.")]
    [InlineData(false, true, "Datei ist für die Inline-Vorschau zu groß (2 MB).")]
    public async Task FileSelection_BinaryOrToLarge_ShowsNotice(bool isBinary, bool isTooBig, string hint)
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "datei.dat", RelativePath = "datei.dat" };
        var preview = new FilePreview("datei.dat", null, false, isBinary, isTooBig, null, null, hint);
        gitMock.Setup(g => g.LoadPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>())).ReturnsAsync(preview);

        sut.SelectedNode = node;
        await WaitForAsync(() => sut.FileContent == hint);

        sut.FileContent.Should().Be(hint);
    }

    /// <summary>Der Wechsel in den Vergleichsmodus lädt den Snapshot und füllt CommitGruppen.</summary>
    [Fact]
    public async Task ComparisonCommand_LoadsCommitsOutSnapshot()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var commits = new List<BranchCommit> { new() { Sha = "abc123", ShortSha = "abc123", Subject = "Testcommit" } };
        var snapshot = new WorkspaceSnapshot { RepositoryPath = RepositoryPath, BranchCommits = commits };
        gitMock.Setup(g => g.LoadSnapshotAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);

        await ((AsyncRelayCommand)sut.CompareCommand).ExecuteAsync();

        sut.CurrentMode.Should().Be(FileBrowserViewMode.Compare);
        sut.CommitGroup.Should().ContainSingle(commit => commit.Sha == "abc123");
    }

    /// <summary>Das Aufklappen eines Commits lädt dessen geänderte Dateien und setzt ChildrenLoaded.</summary>
    [Fact]
    public async Task ExpandCommit_LoadsChangedFiles()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var commit = new BranchCommit { Sha = "abc123", ShortSha = "abc123", Subject = "Testcommit" };
        var files = new List<WorkspaceFileNode> { new() { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" } };
        gitMock.Setup(g => g.LoadCommitFilesAsync(RepositoryPath, "abc123", It.IsAny<CancellationToken>())).ReturnsAsync(files);

        await sut.ExpandCommitAsync(commit, TestContext.Current.CancellationToken);

        commit.Files.Should().ContainSingle(file => file.Name == "a.cs");
        commit.ChildrenLoaded.Should().BeTrue();
    }

    /// <summary>Das Aufklappen eines Commits ändert CommitGruppen nicht (kein Remove/Insert-Umweg mehr) und benachrichtigt stattdessen über die Files-Collection selbst.</summary>
    [Fact]
    public async Task ExpandCommit_ChangesCommitGroupsNotAndNotifiedOverFiles()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var commits = new List<BranchCommit> { new() { Sha = "abc123", ShortSha = "abc123", Subject = "Testcommit" } };
        gitMock.Setup(g => g.LoadSnapshotAsync(RepositoryPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSnapshot { RepositoryPath = RepositoryPath, BranchCommits = commits });
        await ((AsyncRelayCommand)sut.CompareCommand).ExecuteAsync();

        var commit = sut.CommitGroup.Should().ContainSingle().Subject;
        var files = new List<WorkspaceFileNode> { new() { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" } };
        gitMock.Setup(g => g.LoadCommitFilesAsync(RepositoryPath, "abc123", It.IsAny<CancellationToken>())).ReturnsAsync(files);

        var commitGroupsChanged = false;
        sut.CommitGroup.CollectionChanged += (_, _) => commitGroupsChanged = true;
        var filesChanged = false;
        commit.Files.CollectionChanged += (_, _) => filesChanged = true;

        await sut.ExpandCommitAsync(commit, TestContext.Current.CancellationToken);

        commitGroupsChanged.Should().BeFalse();
        filesChanged.Should().BeTrue();
        commit.Files.Should().ContainSingle(file => file.Name == "a.cs");
    }

    /// <summary>Die Auswahl einer Commit-Datei im Vergleichsmodus lädt die Commit-Vorschau und erzeugt Diff-Zeilen über ITextDiffService.</summary>
    [Fact]
    public async Task FileSelection_Comparison_CreatesDiffLines()
    {
        var (sut, gitMock, diffMock) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        var node = new WorkspaceFileNode { Name = "a.cs", RelativePath = "a.cs", CommitSha = "abc123" };
        var preview = new FilePreview("a.cs", null, false, false, false, "neu", "alt", null);
        gitMock.Setup(g => g.LoadCommitPreviewAsync(RepositoryPath, node, It.IsAny<CancellationToken>())).ReturnsAsync(preview);

        var diffLines = new List<TextDiffLine> { new("neu", DiffLineStatus.Modified, 1, 1, []) };
        diffMock.Setup(d => d.BuildDiff("alt", "neu")).Returns(new FileTextDiff(diffLines));

        sut.SelectedNode = node;
        await WaitForAsync(() => sut.DiffLines.Count > 0);

        sut.DiffLines.Should().ContainSingle();
        diffMock.Verify(d => d.BuildDiff("alt", "neu"), Times.Once);
    }

    /// <summary>AktualisierenCommand lädt je nach aktuellem Modus den Arbeitsbaum bzw. den Snapshot erneut.</summary>
    [Fact]
    public async Task RefreshCommand_LoadsCurrentModeNew()
    {
        var (sut, gitMock, _) = CreateSut();
        gitMock.Setup(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await sut.InitializeAsync(RepositoryPath, TestContext.Current.CancellationToken);

        await ((AsyncRelayCommand)sut.RefreshCommand).ExecuteAsync();
        gitMock.Verify(g => g.LoadWorkingTreeAsync(RepositoryPath, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        gitMock.Setup(g => g.LoadSnapshotAsync(RepositoryPath, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceSnapshot { RepositoryPath = RepositoryPath });
        await ((AsyncRelayCommand)sut.CompareCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.RefreshCommand).ExecuteAsync();

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
