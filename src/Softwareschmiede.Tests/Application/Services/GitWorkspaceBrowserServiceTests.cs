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
            ?? docs/Untracked.md
            !! obj/Ignored.dll
            """);

        var snapshot = await service.LoadSnapshotAsync(repositoryPath);

        snapshot.HasError.Should().BeFalse();
        snapshot.CommitCount.Should().Be(42);
        snapshot.ChangedFileCount.Should().Be(8);

        FindNode(snapshot.RootNodes, "src").Should().NotBeNull();
        FindNode(snapshot.RootNodes, Path.Combine("src", "NewName.cs"))!.Status!.BadgeText.Should().Be("R");
        FindNode(snapshot.RootNodes, Path.Combine("src", "TypeChange.cs"))!.Status!.BadgeText.Should().Be("T");
        FindNode(snapshot.RootNodes, Path.Combine("src", "Conflict.cs"))!.Status!.IsConflict.Should().BeTrue();
        FindNode(snapshot.RootNodes, Path.Combine("docs", "Untracked.md"))!.Status!.IsUntracked.Should().BeTrue();
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

    private GitWorkspaceBrowserService CreateService(
        string repositoryPath,
        string statusOutput,
        string? headContent = null,
        string repoCheckStdOut = "true",
        bool repoCheckSuccess = true,
        bool statusSuccess = true,
        string? statusStdErr = null)
    {
        var cliRunner = new Mock<ICliRunner>(MockBehavior.Strict);

        cliRunner
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })), repositoryPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(repoCheckSuccess ? 0 : 1, repoCheckStdOut, string.Empty));
        cliRunner
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-list", "--count", "HEAD" })), repositoryPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "42", string.Empty));
        cliRunner
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain=v1", "--untracked-files=all" })), repositoryPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(statusSuccess ? 0 : 1, statusOutput, statusStdErr ?? string.Empty));

        if (headContent is not null)
        {
            cliRunner
                .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "show", "HEAD:src/deleted.cs" })), repositoryPath, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CliResult(0, headContent, string.Empty));
        }

        return new GitWorkspaceBrowserService(cliRunner.Object, NullLogger<GitWorkspaceBrowserService>.Instance);
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
