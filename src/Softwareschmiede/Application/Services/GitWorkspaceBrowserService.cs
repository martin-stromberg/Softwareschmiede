using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using System.Text;

namespace Softwareschmiede.Application.Services;

/// <summary>Lädt Git-Status, Baum und Dateivorschauen aus dem lokalen Arbeitsverzeichnis.</summary>
public sealed class GitWorkspaceBrowserService : IGitWorkspaceBrowserService
{
    private const long MaxInlineBytes = 1_048_576;
    private const int BinaryProbeBytes = 8_192;
    private static readonly HashSet<string> CodeExtensions =
    [
        ".cs", ".cshtml", ".razor",
        ".ts", ".tsx", ".js", ".jsx",
        ".json", ".yml", ".yaml",
        ".sql", ".ps1", ".sh",
        ".xml", ".xaml", ".css", ".scss", ".html"
    ];

    private readonly ICliRunner _cliRunner;
    private readonly ILogger<GitWorkspaceBrowserService> _logger;

    public GitWorkspaceBrowserService(ICliRunner cliRunner, ILogger<GitWorkspaceBrowserService> logger)
    {
        _cliRunner = cliRunner;
        _logger = logger;
    }

    public async Task<WorkspaceSnapshot> LoadSnapshotAsync(string repositoryPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        _logger.LogInformation("Workspace-Snapshot für {RepositoryPath} laden.", repositoryPath);

        if (!Directory.Exists(repositoryPath))
        {
            return WorkspaceSnapshot.FromError($"Repositorypfad '{repositoryPath}' existiert nicht.");
        }

        var repoCheck = await _cliRunner.RunAsync("git", ["rev-parse", "--is-inside-work-tree"], repositoryPath, null, ct);
        if (!repoCheck.IsSuccess || !string.Equals(repoCheck.StdOut.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspaceSnapshot.FromError($"Pfad '{repositoryPath}' ist kein Git-Repository.");
        }

        var commitCount = await ReadCommitCountAsync(repositoryPath, ct);
        var entries = await ReadStatusEntriesAsync(repositoryPath, ct);

        var snapshot = BuildSnapshot(repositoryPath, commitCount, entries);
        _logger.LogInformation(
            "Workspace-Snapshot geladen für {RepositoryPath}: {CommitCount} Commits, {ChangedCount} geänderte Dateien.",
            repositoryPath,
            snapshot.CommitCount,
            snapshot.ChangedFileCount);

        return snapshot;
    }

    public async Task<FilePreview> LoadPreviewAsync(string repositoryPath, WorkspaceFileNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        if (node.IsDirectory)
        {
            return new FilePreview(node.RelativePath, node.SourceRelativePath, node.IsDeleted, false, false, null, null, "Verzeichnisse können nicht direkt in der Vorschau angezeigt werden.");
        }

        var relativePath = NormalizeRelativePath(node.RelativePath);
        var sourceRelativePath = string.IsNullOrWhiteSpace(node.SourceRelativePath)
            ? null
            : NormalizeRelativePath(node.SourceRelativePath);
        var workingTreePath = CombinePath(repositoryPath, relativePath);

        if (node.IsDeleted)
        {
            var originalContent = await ReadHeadContentAsync(repositoryPath, sourceRelativePath ?? relativePath, ct);
            return new FilePreview(relativePath, sourceRelativePath, true, false, false, null, originalContent, null);
        }

        if (!File.Exists(workingTreePath))
        {
            var originalContent = await ReadHeadContentAsync(repositoryPath, sourceRelativePath ?? relativePath, ct);
            return new FilePreview(relativePath, sourceRelativePath, false, false, false, null, originalContent, "Die aktuelle Datei existiert nicht mehr.");
        }

        var fileInfo = new FileInfo(workingTreePath);
        if (fileInfo.Length > MaxInlineBytes)
        {
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                false,
                false,
                true,
                null,
                null,
                $"Datei ist für die Inline-Vorschau zu groß ({FormatBytes(fileInfo.Length)}).");
        }

        var fileBytes = await File.ReadAllBytesAsync(workingTreePath, ct);
        if (IsBinary(fileBytes))
        {
            return new FilePreview(relativePath, sourceRelativePath, false, true, false, null, null, "Binärdatei – Vorschau nicht verfügbar.");
        }

        var currentContent = DecodeText(fileBytes);
        var original = sourceRelativePath is not null || node.Status is not null
            ? await ReadHeadContentAsync(repositoryPath, sourceRelativePath ?? relativePath, ct)
            : null;

        return new FilePreview(relativePath, sourceRelativePath, false, false, false, currentContent, original, null);
    }

    private async Task<int> ReadCommitCountAsync(string repositoryPath, CancellationToken ct)
    {
        var result = await _cliRunner.RunAsync("git", ["rev-list", "--count", "HEAD"], repositoryPath, null, ct);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Commit-Anzahl für {RepositoryPath} konnte nicht geladen werden: {Error}", repositoryPath, result.StdErr);
            return 0;
        }

        return int.TryParse(result.StdOut.Trim(), out var count) ? count : 0;
    }

    private async Task<IReadOnlyList<WorkspaceStatusEntry>> ReadStatusEntriesAsync(string repositoryPath, CancellationToken ct)
    {
        var result = await _cliRunner.RunAsync(
            "git",
            ["status", "--porcelain=v1", "--untracked-files=all"],
            repositoryPath,
            null,
            ct);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git status fehlgeschlagen: {result.StdErr}");
        }

        var entries = new List<WorkspaceStatusEntry>();
        var lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 3)
            {
                continue;
            }

            if (line.StartsWith("!!", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("??", StringComparison.Ordinal))
            {
                var path = NormalizeRelativePath(line[3..]);
                entries.Add(new WorkspaceStatusEntry(path, null, new WorkspaceFileStatus('?', '?')));
                continue;
            }

            var status = WorkspaceFileStatus.Parse(line[..2]);
            var payload = line[3..];
            string relativePath;
            string? sourcePath = null;

            if (status.IsRenameOrCopy && payload.Contains(" -> ", StringComparison.Ordinal))
            {
                var split = payload.Split(" -> ", 2, StringSplitOptions.None);
                sourcePath = NormalizeRelativePath(split[0]);
                relativePath = NormalizeRelativePath(split[1]);
            }
            else
            {
                relativePath = NormalizeRelativePath(payload);
            }

            entries.Add(new WorkspaceStatusEntry(relativePath, sourcePath, status));
        }

        return entries;
    }

    private WorkspaceSnapshot BuildSnapshot(string repositoryPath, int commitCount, IReadOnlyList<WorkspaceStatusEntry> entries)
    {
        var fileNodes = new List<WorkspaceFileNode>();
        var rootNodes = new List<WorkspaceFileNode>();
        var pathMap = new Dictionary<string, WorkspaceFileNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var node = CreateFileNode(entry);
            fileNodes.Add(node);
            InsertNode(rootNodes, pathMap, node);
            IncrementAncestorCounts(pathMap, node.RelativePath);
        }

        SortNodes(rootNodes);

        var orderedFlatFiles = fileNodes.OrderBy(node => node.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        var planningDocuments = orderedFlatFiles
            .Where(IsPlanningDocumentNode)
            .ToList();
        if (planningDocuments.Count == 0)
        {
            planningDocuments = orderedFlatFiles
                .Where(node => IsPlanningDocumentPathFallback(node.RelativePath) || IsPlanningDocumentPathFallback(node.SourceRelativePath))
                .ToList();
        }
        var codeFiles = orderedFlatFiles
            .Where(node => !IsPlanningDocumentNode(node) && IsCodeFileNode(node))
            .ToList();

        return new WorkspaceSnapshot
        {
            RepositoryPath = repositoryPath,
            CommitCount = commitCount,
            ChangedFileCount = fileNodes.Count,
            RootNodes = rootNodes,
            FlatFiles = orderedFlatFiles,
            CodeFiles = codeFiles,
            PlanningDocuments = planningDocuments,
        };
    }

    private static WorkspaceFileNode CreateFileNode(WorkspaceStatusEntry entry)
    {
        var name = Path.GetFileName(entry.RelativePath);
        return new WorkspaceFileNode
        {
            Name = string.IsNullOrEmpty(name) ? entry.RelativePath : name,
            RelativePath = entry.RelativePath,
            IsDirectory = false,
            IsDeleted = entry.Status.IsDeleted,
            SourceRelativePath = entry.SourceRelativePath,
            Status = entry.Status,
        };
    }

    private static void InsertNode(List<WorkspaceFileNode> rootNodes, IDictionary<string, WorkspaceFileNode> pathMap, WorkspaceFileNode fileNode)
    {
        var segments = fileNode.RelativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            rootNodes.Add(fileNode);
            return;
        }

        var parentNodes = rootNodes;
        var currentPath = string.Empty;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            currentPath = string.IsNullOrEmpty(currentPath)
                ? segments[index]
                : Path.Combine(currentPath, segments[index]);

            if (!pathMap.TryGetValue(currentPath, out var directoryNode))
            {
                directoryNode = new WorkspaceFileNode
                {
                    Name = segments[index],
                    RelativePath = currentPath,
                    IsDirectory = true,
                    ChildrenLoaded = true,
                };
                pathMap[currentPath] = directoryNode;
                parentNodes.Add(directoryNode);
            }

            parentNodes = directoryNode.Children;
        }

        parentNodes.Add(fileNode);
    }

    private static void IncrementAncestorCounts(IReadOnlyDictionary<string, WorkspaceFileNode> pathMap, string relativePath)
    {
        var parent = Path.GetDirectoryName(relativePath);
        while (!string.IsNullOrWhiteSpace(parent))
        {
            if (pathMap.TryGetValue(parent, out var node))
            {
                node.ChangedFileCount++;
            }

            parent = Path.GetDirectoryName(parent);
        }
    }

    private static void SortNodes(List<WorkspaceFileNode> nodes)
    {
        nodes.Sort((left, right) =>
        {
            if (left.IsDirectory != right.IsDirectory)
            {
                return left.IsDirectory ? -1 : 1;
            }

            var deleteCompare = left.IsDeleted.CompareTo(right.IsDeleted);
            if (deleteCompare != 0)
            {
                return deleteCompare;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        foreach (var directory in nodes.Where(node => node.IsDirectory))
        {
            SortNodes(directory.Children);
        }
    }

    private async Task<string?> ReadHeadContentAsync(string repositoryPath, string relativePath, CancellationToken ct)
    {
        var gitPath = ToGitPath(relativePath);
        var result = await _cliRunner.RunAsync("git", ["show", $"HEAD:{gitPath}"], repositoryPath, null, ct);
        if (!result.IsSuccess)
        {
            return null;
        }

        return result.StdOut;
    }

    private static bool IsBinary(byte[] bytes)
    {
        var probeLength = Math.Min(bytes.Length, BinaryProbeBytes);
        for (var index = 0; index < probeLength; index++)
        {
            if (bytes[index] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string DecodeText(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    private static bool IsCodeFilePath(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return !string.IsNullOrWhiteSpace(extension) && CodeExtensions.Contains(extension);
    }

    private static bool IsCodeFileNode(WorkspaceFileNode node)
    {
        return IsCodeFilePath(node.RelativePath) || (!string.IsNullOrWhiteSpace(node.SourceRelativePath) && IsCodeFilePath(node.SourceRelativePath));
    }

    private static bool IsPlanningDocumentNode(WorkspaceFileNode node)
    {
        return IsPlanningDocumentPath(node.RelativePath) || IsPlanningDocumentPath(node.SourceRelativePath);
    }

    private static bool IsPlanningDocumentPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = NormalizeRelativePath(relativePath);
        if (normalizedPath.StartsWith($".{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath[2..];
        }

        if (!normalizedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedPath.StartsWith(Path.Combine("docs", "requirements"), StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith(Path.Combine("docs", "architecture"), StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith(Path.Combine("docs", "improvements"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlanningDocumentPathFallback(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = relativePath.Trim().Replace('\\', '/').TrimStart('.', '/');
        if (!normalizedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedPath.StartsWith("docs/requirements/", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith("docs/architecture/", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith("docs/improvements/", StringComparison.OrdinalIgnoreCase);
    }

    private static string CombinePath(string rootPath, string relativePath)
    {
        var candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Pfad '{relativePath}' liegt außerhalb des Repository-Roots.");
        }

        return candidate;
    }

    private static string ToGitPath(string relativePath) => NormalizeRelativePath(relativePath).Replace(Path.DirectorySeparatorChar, '/');

    private static string FormatBytes(long byteCount)
    {
        if (byteCount < 1024)
        {
            return $"{byteCount} B";
        }

        var size = byteCount / 1024d / 1024d;
        return $"{size:0.##} MB";
    }

    private sealed record WorkspaceStatusEntry(string RelativePath, string? SourceRelativePath, WorkspaceFileStatus Status);
}
