using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den GitWorkspaceBrowserService.</summary>
public sealed class GitWorkspaceBrowserServiceTests : IDisposable
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

    /// <summary>Ermittelt einen rekursiven Baum und behält Git-Sonderstatus korrekt bei.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldBuildRecursiveTree_AndPreserveSpecialStatuses()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(
            repositoryPath,
            """
             M src/Changed.cs
            A  src/NewFile.cs
            D  src/Deleted.cs
            R  src/OldName.cs -> src/NewName.cs
            C  src/Source.cs -> src/Copy.cs
            T  src/TypeChange.cs
            UU src/Conflict.cs
            ?? docs/requirements/new-feature.md
            ?? docs/architecture/target-state.md
            R  docs/improvements/legacy-plan.md -> archive/legacy-plan.md
            ?? ./docs/improvements/edge-case.md
            ?? docs/Untracked.md
            !! obj/Ignored.dll
            """);

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);

        snapshot.HasError.Should().BeFalse();
        snapshot.CommitCount.Should().Be(42);
        snapshot.ChangedFileCount.Should().Be(12);
        snapshot.CodeFiles.Should().HaveCount(7);
        snapshot.PlanningDocuments.Should().HaveCount(4);
        snapshot.PlanningDocuments.Should().ContainSingle(node => node.RelativePath == Path.Combine("docs", "requirements", "new-feature.md"));
        snapshot.PlanningDocuments.Should().ContainSingle(node => node.RelativePath == Path.Combine("docs", "architecture", "target-state.md"));
        snapshot.PlanningDocuments.Should().ContainSingle(node => node.RelativePath == Path.Combine("archive", "legacy-plan.md"));
        snapshot.PlanningDocuments.Should().ContainSingle(node => node.RelativePath == Path.Combine(".", "docs", "improvements", "edge-case.md"));

        FindNode(snapshot.RootNodes, "src").Should().NotBeNull();
        FindNode(snapshot.RootNodes, Path.Combine("src", "NewName.cs"))!.Status!.BadgeText.Should().Be("R");
        FindNode(snapshot.RootNodes, Path.Combine("src", "TypeChange.cs"))!.Status!.BadgeText.Should().Be("T");
        FindNode(snapshot.RootNodes, Path.Combine("src", "Conflict.cs"))!.Status!.IsConflict.Should().BeTrue();
        FindNode(snapshot.RootNodes, Path.Combine("docs", "Untracked.md"))!.Status!.IsUntracked.Should().BeTrue();
        FindNode(snapshot.RootNodes, Path.Combine("docs", "requirements", "new-feature.md"))!.Status!.IsUntracked.Should().BeTrue();
        FindNode(snapshot.RootNodes, Path.Combine(".", "docs", "improvements", "edge-case.md"))!.Status!.IsUntracked.Should().BeTrue();
        FindNode(snapshot.RootNodes, Path.Combine("src", "Deleted.cs"))!.IsDeleted.Should().BeTrue();
    }

    /// <summary>Prüft, dass ein fehlender Repository-Pfad als Fehler-Snapshot zurückkommt.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldReturnError_WhenRepositoryPathDoesNotExist()
    {
        var missingPath = Path.Combine(CreateTempDirectory(), "missing");
        var service = CreateService(missingPath, string.Empty);

        var snapshot = await service.LoadSnapshotAsync(missingPath);

        snapshot.HasError.Should().BeTrue();
        snapshot.ErrorMessage.Should().Contain("existiert nicht");
    }

    /// <summary>Prüft, dass ein nicht-gitfähiger Ordner als Fehler-Snapshot zurückkommt.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldReturnError_WhenPathIsNotAGitRepository()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(repositoryPath, string.Empty, repoCheckStdOut: "false");

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);

        snapshot.HasError.Should().BeTrue();
        snapshot.ErrorMessage.Should().Contain("kein Git-Repository");
    }

    /// <summary>Prüft, dass ein leerer Status-Output zu einem leeren Snapshot führt.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldReturnEmptySnapshot_WhenNoChangesExist()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(repositoryPath, string.Empty);

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);

        snapshot.HasError.Should().BeFalse();
        snapshot.ChangedFileCount.Should().Be(0);
        snapshot.RootNodes.Should().BeEmpty();
        snapshot.FlatFiles.Should().BeEmpty();
        snapshot.CodeFiles.Should().BeEmpty();
        snapshot.PlanningDocuments.Should().BeEmpty();
    }

    /// <summary>Erkennt Planungsdokumente über Fallback für Slash- und Dot-Varianten.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldDetectPlanningDocumentsViaFallback_WhenOnlySlashAndDotVariantsExist()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(
            repositoryPath,
            """
            ?? /docs/architecture/target-state.md
            ?? ././docs/requirements/new-feature.md
            ?? /docs/notes/outside.md
            """);

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);
        var expectedArchitecturePath = $"{Path.DirectorySeparatorChar}docs{Path.DirectorySeparatorChar}architecture{Path.DirectorySeparatorChar}target-state.md";
        var expectedRequirementsPath = $".{Path.DirectorySeparatorChar}.{Path.DirectorySeparatorChar}docs{Path.DirectorySeparatorChar}requirements{Path.DirectorySeparatorChar}new-feature.md";
        var excludedNotesSuffix = $"{Path.DirectorySeparatorChar}docs{Path.DirectorySeparatorChar}notes{Path.DirectorySeparatorChar}outside.md";

        snapshot.PlanningDocuments.Should().HaveCount(2);
        snapshot.PlanningDocuments.Should().ContainSingle(node => node.RelativePath == expectedArchitecturePath);
        snapshot.PlanningDocuments.Should().ContainSingle(node => node.RelativePath == expectedRequirementsPath);
        snapshot.PlanningDocuments.Should().NotContain(node => node.RelativePath.EndsWith(excludedNotesSuffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Erkennt Planungsdokumente im Fallback über SourceRelativePath bei Rename/Copies.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldUseSourceRelativePath_ForPlanningDocumentFallbackDetection()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(
            repositoryPath,
            "R  /docs/improvements/legacy-plan.md -> src/legacy-plan.txt");

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);
        var expectedSourcePath = $"{Path.DirectorySeparatorChar}docs{Path.DirectorySeparatorChar}improvements{Path.DirectorySeparatorChar}legacy-plan.md";

        snapshot.PlanningDocuments.Should().ContainSingle();
        snapshot.PlanningDocuments[0].RelativePath.Should().Be(Path.Combine("src", "legacy-plan.txt"));
        snapshot.PlanningDocuments[0].SourceRelativePath.Should().Be(expectedSourcePath);
    }

    /// <summary>Klassifiziert Markdown außerhalb erlaubter docs-Bereiche auch im Fallback nicht als Planung.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldNotClassifyMarkdownOutsideAllowedDocsFolders_AsPlanningDocumentInFallback()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(
            repositoryPath,
            """
            ?? /docs/notes/planning.md
            ?? /docs/requirements/accepted.md
            """);

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);
        var expectedAcceptedSuffix = $"{Path.DirectorySeparatorChar}docs{Path.DirectorySeparatorChar}requirements{Path.DirectorySeparatorChar}accepted.md";
        var excludedNotesSuffix = $"{Path.DirectorySeparatorChar}docs{Path.DirectorySeparatorChar}notes{Path.DirectorySeparatorChar}planning.md";

        snapshot.PlanningDocuments.Should().ContainSingle(node => node.RelativePath.EndsWith(expectedAcceptedSuffix, StringComparison.OrdinalIgnoreCase));
        snapshot.PlanningDocuments.Should().NotContain(node => node.RelativePath.EndsWith(excludedNotesSuffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Prüft, dass ein fehlschlagendes git status als Ausnahme durchgereicht wird.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldThrow_WhenGitStatusFails()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(repositoryPath, string.Empty, statusSuccess: false, statusStdErr: "kaputt");

        var act = () => service.LoadSnapshotAsync(repositoryPath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*git status fehlgeschlagen*");
    }

    /// <summary>Setzt CommitCount auf 0 wenn der rev-list-Aufruf fehlschlägt.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldReturnCommitCountZero_WhenRevListFails()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(repositoryPath, "?? src/file.cs", commitCountSuccess: false, commitCountStdErr: "fatal");

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);

        snapshot.CommitCount.Should().Be(0);
    }

    /// <summary>Liest gelöschte Dateien aus HEAD und verweigert Inline-Vorschau für Binärdateien.</summary>
    [Fact]
    public async Task LoadPreviewAsync_ShouldUseHeadForDeletedFiles_AndDetectBinaryFiles()
    {
        var repositoryPath = CreateTempDirectory();
        var deletedService = CreateService(repositoryPath, string.Empty, headContent: "old content");
        var deletedNode = new WorkspaceFileNode
        {
            Name = "deleted.cs",
            RelativePath = Path.Combine("src", "deleted.cs"),
            IsDirectory = false,
            IsDeleted = true,
            SourceRelativePath = Path.Combine("src", "deleted.cs"),
            Status = new WorkspaceFileStatus('D', ' '),
        };

        var deletedPreview = await deletedService.LoadPreviewAsync(repositoryPath, deletedNode);

        deletedPreview.IsDeleted.Should().BeTrue();
        deletedPreview.OriginalContent.Should().Be("old content");
        deletedPreview.CurrentContent.Should().BeNull();
        deletedPreview.IsBinary.Should().BeFalse();

        var binaryPath = Path.Combine(repositoryPath, "binary.dat");
        await File.WriteAllBytesAsync(binaryPath, [1, 2, 0, 4, 5]);

        var binaryService = CreateService(repositoryPath, string.Empty);
        var binaryNode = new WorkspaceFileNode
        {
            Name = "binary.dat",
            RelativePath = "binary.dat",
            IsDirectory = false,
            Status = new WorkspaceFileStatus(' ', 'M'),
        };

        var binaryPreview = await binaryService.LoadPreviewAsync(repositoryPath, binaryNode);

        binaryPreview.IsBinary.Should().BeTrue();
        binaryPreview.Hint.Should().Contain("Binärdatei");
        binaryPreview.CurrentContent.Should().BeNull();
    }

    /// <summary>Lädt bei fehlender Working-Tree-Datei den HEAD-Inhalt und liefert einen Hinweistext.</summary>
    [Fact]
    public async Task LoadPreviewAsync_ShouldFallbackToHeadAndHint_WhenWorkingTreeFileIsMissing()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(
            repositoryPath,
            string.Empty,
            headContentByPath: new Dictionary<string, CliResult>
            {
                ["docs/requirements/missing.md"] = new(0, "head-content", string.Empty),
            });

        var preview = await service.LoadPreviewAsync(repositoryPath, new WorkspaceFileNode
        {
            Name = "missing.md",
            RelativePath = Path.Combine("docs", "requirements", "missing.md"),
            IsDirectory = false,
            Status = new WorkspaceFileStatus('M', ' '),
        });

        preview.CurrentContent.Should().BeNull();
        preview.OriginalContent.Should().Be("head-content");
        preview.Hint.Should().Contain("existiert nicht mehr");
    }

    /// <summary>Lädt bei regulären Textdateien den HEAD-Inhalt als OriginalContent.</summary>
    [Fact]
    public async Task LoadPreviewAsync_ShouldPopulateOriginalContent_ForRegularTextFileWhenHeadExists()
    {
        var repositoryPath = CreateTempDirectory();
        var relativePath = Path.Combine("src", "changed.cs");
        var filePath = Path.Combine(repositoryPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "current-content");

        var service = CreateService(
            repositoryPath,
            string.Empty,
            headContentByPath: new Dictionary<string, CliResult>
            {
                ["src/changed.cs"] = new(0, "original-content", string.Empty),
            });

        var preview = await service.LoadPreviewAsync(repositoryPath, new WorkspaceFileNode
        {
            Name = "changed.cs",
            RelativePath = relativePath,
            IsDirectory = false,
            Status = new WorkspaceFileStatus('M', ' '),
        });

        preview.CurrentContent.Should().Be("current-content");
        preview.OriginalContent.Should().Be("original-content");
    }

    /// <summary>Setzt OriginalContent auf null wenn git show für HEAD-Inhalt fehlschlägt.</summary>
    [Fact]
    public async Task LoadPreviewAsync_ShouldSetOriginalContentNull_WhenGitShowFails()
    {
        var repositoryPath = CreateTempDirectory();
        var relativePath = Path.Combine("src", "changed.cs");
        var filePath = Path.Combine(repositoryPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "current-content");

        var service = CreateService(
            repositoryPath,
            string.Empty,
            headContentByPath: new Dictionary<string, CliResult>
            {
                ["src/changed.cs"] = new(1, string.Empty, "fatal"),
            });

        var preview = await service.LoadPreviewAsync(repositoryPath, new WorkspaceFileNode
        {
            Name = "changed.cs",
            RelativePath = relativePath,
            IsDirectory = false,
            Status = new WorkspaceFileStatus('M', ' '),
        });

        preview.CurrentContent.Should().Be("current-content");
        preview.OriginalContent.Should().BeNull();
    }

    /// <summary>Prüft, dass Verzeichnisse keine Dateivorschau auslösen.</summary>
    [Fact]
    public async Task LoadPreviewAsync_ShouldReturnDirectoryHint_WhenNodeIsDirectory()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(repositoryPath, string.Empty);

        var preview = await service.LoadPreviewAsync(repositoryPath, new WorkspaceFileNode
        {
            Name = "src",
            RelativePath = "src",
            IsDirectory = true,
        });

        preview.Hint.Should().Contain("Verzeichnisse");
        preview.CurrentContent.Should().BeNull();
        preview.OriginalContent.Should().BeNull();
    }

    /// <summary>Prüft die Größen-Schutzschranke für Inline-Vorschauen.</summary>
    [Fact]
    public async Task LoadPreviewAsync_ShouldReturnTooBigHint_WhenFileExceedsInlineLimit()
    {
        var repositoryPath = CreateTempDirectory();
        var largeFilePath = Path.Combine(repositoryPath, "large.txt");
        await File.WriteAllTextAsync(largeFilePath, new string('a', 1_048_577));
        var service = CreateService(repositoryPath, string.Empty);

        var preview = await service.LoadPreviewAsync(repositoryPath, new WorkspaceFileNode
        {
            Name = "large.txt",
            RelativePath = "large.txt",
            IsDirectory = false,
        });

        preview.IsTooBig.Should().BeTrue();
        preview.CurrentContent.Should().BeNull();
        preview.Hint.Should().Contain("zu groß");
    }

    /// <summary>Prüft den Schutz gegen Pfad-Traversal außerhalb des Repository-Roots.</summary>
    [Fact]
    public async Task LoadPreviewAsync_ShouldRejectPathTraversalOutsideRepository()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(repositoryPath, string.Empty);

        var act = () => service.LoadPreviewAsync(repositoryPath, new WorkspaceFileNode
        {
            Name = "outside.txt",
            RelativePath = Path.Combine("..", "..", "outside.txt"),
            IsDirectory = false,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*außerhalb des Repository-Roots*");
    }

    /// <summary>Ignoriert zu kurze oder inkonsistente Statuszeilen ohne Ausnahme.</summary>
    [Fact]
    public async Task LoadSnapshotAsync_ShouldIgnoreTooShortStatusLines_WithoutThrowing()
    {
        var repositoryPath = CreateTempDirectory();
        var service = CreateService(
            repositoryPath,
            """
            ?
            A
             

            ?? src/valid.cs
            """);

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);

        snapshot.ChangedFileCount.Should().Be(1);
        snapshot.FlatFiles.Should().ContainSingle(node => node.RelativePath == Path.Combine("src", "valid.cs"));
    }

    private GitWorkspaceBrowserService CreateService(
        string repositoryPath,
        string statusOutput,
        string? headContent = null,
        string repoCheckStdOut = "true",
        bool repoCheckSuccess = true,
        bool statusSuccess = true,
        string? statusStdErr = null,
        bool commitCountSuccess = true,
        string commitCountStdOut = "42",
        string? commitCountStdErr = null,
        IReadOnlyDictionary<string, CliResult>? headContentByPath = null)
    {
        var cliRunner = new Mock<ICliRunner>(MockBehavior.Strict);
        var headResults = new Dictionary<string, CliResult>(StringComparer.OrdinalIgnoreCase);
        if (headContentByPath is not null)
        {
            foreach (var pair in headContentByPath)
            {
                headResults[pair.Key] = pair.Value;
            }
        }

        if (headContent is not null && !headResults.ContainsKey("src/deleted.cs"))
        {
            headResults["src/deleted.cs"] = new CliResult(0, headContent, string.Empty);
        }

        cliRunner
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })), repositoryPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(repoCheckSuccess ? 0 : 1, repoCheckStdOut, string.Empty));
        cliRunner
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-list", "--count", "HEAD" })), repositoryPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(commitCountSuccess ? 0 : 1, commitCountStdOut, commitCountStdErr ?? string.Empty));
        cliRunner
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain=v1", "--untracked-files=all" })), repositoryPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(statusSuccess ? 0 : 1, statusOutput, statusStdErr ?? string.Empty));

        if (headResults.Count > 0)
        {
            cliRunner
                .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(args => IsGitShowArgument(args)), repositoryPath, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string command, IEnumerable<string> args, string? workingDirectory, IDictionary<string, string>? environmentVariables, CancellationToken cancellationToken) =>
                {
                    var gitPath = args.Skip(1).First()["HEAD:".Length..];
                    return headResults.TryGetValue(gitPath, out var result)
                        ? result
                        : new CliResult(1, string.Empty, "not found");
                });
        }

        return new GitWorkspaceBrowserService(cliRunner.Object, NullLogger<GitWorkspaceBrowserService>.Instance);
    }

    private static bool IsGitShowArgument(IEnumerable<string> args)
    {
        var argumentList = args.ToList();
        return argumentList.Count == 2
               && string.Equals(argumentList[0], "show", StringComparison.Ordinal)
               && argumentList[1].StartsWith("HEAD:", StringComparison.Ordinal);
    }

    private string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"softwareschmiede-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }

    private static WorkspaceFileNode? FindNode(IEnumerable<WorkspaceFileNode> nodes, string relativePath)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            var child = FindNode(node.Children, relativePath);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }
}
