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
    private const int MaxWorkingTreeNodeCount = 100_000;
    private const string GitDirectoryName = ".git";

    private readonly ICliRunner _cliRunner;
    private readonly ILogger<GitWorkspaceBrowserService> _logger;

    /// <summary>Erstellt eine neue Instanz von <see cref="GitWorkspaceBrowserService"/>.</summary>
    public GitWorkspaceBrowserService(ICliRunner cliRunner, ILogger<GitWorkspaceBrowserService> logger)
    {
        _cliRunner = cliRunner;
        _logger = logger;
    }

    /// <inheritdoc/>
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

        var baseReference = await ResolveBaseReferenceAsync(repositoryPath, ct);
        var commitCount = await ReadCommitCountAsync(repositoryPath, baseReference, ct);
        var branchCommits = await ReadBranchCommitsAsync(repositoryPath, baseReference, ct);

        var snapshot = new WorkspaceSnapshot
        {
            RepositoryPath = repositoryPath,
            CommitCount = commitCount,
            BranchCommits = branchCommits,
        };
        _logger.LogInformation(
            "Workspace-Snapshot geladen für {RepositoryPath}: {CommitCount} Branch-Commits, {BranchCommitNodes} Commit-Knoten.",
            repositoryPath,
            snapshot.CommitCount,
            snapshot.BranchCommits.Count);

        return snapshot;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkspaceFileNode>> LoadCommitFilesAsync(string repositoryPath, string commitSha, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);

        var result = await _cliRunner.RunAsync(
            "git",
            ["diff-tree", "--root", "--no-commit-id", "-r", "--name-status", "-z", commitSha],
            repositoryPath,
            null,
            ct);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Commit-Dateien konnten nicht geladen werden: {result.StdErr}");
        }

        return BuildCommitFileTree(commitSha, result.StdOut);
    }

    /// <inheritdoc/>
    public async Task<FilePreview> LoadCommitPreviewAsync(string repositoryPath, WorkspaceFileNode node, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.CommitSha);

        if (node.IsDirectory)
        {
            return new FilePreview(node.RelativePath, node.SourceRelativePath, node.IsDeleted, false, false, null, null, "Verzeichnisse können nicht direkt in der Vorschau angezeigt werden.");
        }

        var relativePath = NormalizeRelativePath(node.RelativePath);
        var sourceRelativePath = string.IsNullOrWhiteSpace(node.SourceRelativePath)
            ? relativePath
            : NormalizeRelativePath(node.SourceRelativePath);
        var gitCurrentPath = ToGitPath(relativePath);
        var gitOriginalPath = ToGitPath(sourceRelativePath);
        var commitSha = node.CommitSha!;

        var currentResult = await _cliRunner.RunAsync("git", ["show", $"{commitSha}:{gitCurrentPath}"], repositoryPath, null, ct);
        var originalResult = await _cliRunner.RunAsync("git", ["show", $"{commitSha}^:{gitOriginalPath}"], repositoryPath, null, ct);

        var currentContent = currentResult.IsSuccess ? currentResult.StdOut : null;
        var originalContent = originalResult.IsSuccess ? originalResult.StdOut : null;

        if (IsBinaryText(currentContent) || IsBinaryText(originalContent))
        {
            return new FilePreview(relativePath, sourceRelativePath, node.IsDeleted, true, false, null, null, "Binärdatei – Vorschau nicht verfügbar.");
        }

        var currentByteCount = currentContent is null ? 0L : Encoding.UTF8.GetByteCount(currentContent);
        var originalByteCount = originalContent is null ? 0L : Encoding.UTF8.GetByteCount(originalContent);
        if (currentByteCount > MaxInlineBytes || originalByteCount > MaxInlineBytes)
        {
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                node.IsDeleted,
                false,
                true,
                null,
                null,
                $"Commit-Datei ist für die Inline-Vorschau zu groß ({FormatBytes(Math.Max(currentByteCount, originalByteCount))}).");
        }

        if (!currentResult.IsSuccess && !originalResult.IsSuccess)
        {
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                node.IsDeleted,
                false,
                false,
                null,
                null,
                $"Commit-Vorschau konnte nicht geladen werden: {currentResult.StdErr}");
        }

        return new FilePreview(
            relativePath,
            sourceRelativePath,
            node.IsDeleted,
            false,
            false,
            currentContent,
            originalContent,
            null);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkspaceFileNode>> LoadWorkingTreeAsync(string repositoryPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        return Task.Run(() =>
        {
            if (!Directory.Exists(repositoryPath))
            {
                _logger.LogWarning("Arbeitsbaum für {RepositoryPath} kann nicht geladen werden: Pfad existiert nicht.", repositoryPath);
                return (IReadOnlyList<WorkspaceFileNode>)new List<WorkspaceFileNode>();
            }

            var rootNodes = new List<WorkspaceFileNode>();
            var nodeCount = 0;
            WalkWorkingTreeDirectory(repositoryPath, repositoryPath, rootNodes, ref nodeCount, ct);

            if (nodeCount >= MaxWorkingTreeNodeCount)
            {
                _logger.LogWarning(
                    "Arbeitsbaum für {RepositoryPath} überschreitet die Knoten-Obergrenze ({MaxNodeCount}); Baum wird gekürzt angezeigt.",
                    repositoryPath,
                    MaxWorkingTreeNodeCount);
            }

            SortNodes(rootNodes);
            return (IReadOnlyList<WorkspaceFileNode>)rootNodes;
        }, ct);
    }

    private void WalkWorkingTreeDirectory(string rootPath, string currentPath, List<WorkspaceFileNode> parentNodes, ref int nodeCount, CancellationToken ct)
    {
        if (nodeCount >= MaxWorkingTreeNodeCount)
        {
            return;
        }

        IEnumerable<string> directories;
        IEnumerable<string> files;
        try
        {
            directories = Directory.EnumerateDirectories(currentPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            files = Directory.EnumerateFiles(currentPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogDebug(ex, "Verzeichnis {DirectoryPath} konnte nicht aufgezählt werden und wird übersprungen.", currentPath);
            return;
        }

        foreach (var directoryPath in directories)
        {
            ct.ThrowIfCancellationRequested();
            if (nodeCount >= MaxWorkingTreeNodeCount)
            {
                return;
            }

            var name = Path.GetFileName(directoryPath);
            if (string.Equals(name, GitDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var directoryNode = new WorkspaceFileNode
            {
                Name = name,
                RelativePath = Path.GetRelativePath(rootPath, directoryPath),
                IsDirectory = true,
                ChildrenLoaded = true,
            };
            nodeCount++;
            parentNodes.Add(directoryNode);
            WalkWorkingTreeDirectory(rootPath, directoryPath, directoryNode.Children, ref nodeCount, ct);
        }

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();
            if (nodeCount >= MaxWorkingTreeNodeCount)
            {
                return;
            }

            parentNodes.Add(new WorkspaceFileNode
            {
                Name = Path.GetFileName(filePath),
                RelativePath = Path.GetRelativePath(rootPath, filePath),
                IsDirectory = false,
            });
            nodeCount++;
        }
    }

    private async Task<int> ReadCommitCountAsync(string repositoryPath, string? baseReference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseReference))
        {
            return 0;
        }

        var result = await _cliRunner.RunAsync("git", ["rev-list", "--count", $"{baseReference}..HEAD"], repositoryPath, null, ct);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Commit-Anzahl für {RepositoryPath} konnte nicht geladen werden: {Error}", repositoryPath, result.StdErr);
            return 0;
        }

        return int.TryParse(result.StdOut.Trim(), out var count) ? count : 0;
    }

    private async Task<IReadOnlyList<BranchCommit>> ReadBranchCommitsAsync(string repositoryPath, string? baseReference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseReference))
        {
            return [];
        }

        var result = await _cliRunner.RunAsync(
            "git",
            ["log", $"--format=%H%x00%h%x00%s", $"{baseReference}..HEAD"],
            repositoryPath,
            null,
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Commit-Liste für {RepositoryPath} konnte nicht geladen werden: {Error}", repositoryPath, result.StdErr);
            return [];
        }

        var commits = new List<BranchCommit>();
        var lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('\0', 3, StringSplitOptions.None);
            if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0]))
            {
                _logger.LogWarning("Ungültige Commit-Zeile verworfen: {Line}", line);
                continue;
            }

            commits.Add(new BranchCommit
            {
                Sha = parts[0],
                ShortSha = string.IsNullOrWhiteSpace(parts[1]) ? parts[0][..Math.Min(7, parts[0].Length)] : parts[1],
                Subject = parts[2],
            });
        }

        return commits;
    }

    private async Task<string?> ResolveBaseReferenceAsync(string repositoryPath, CancellationToken ct)
    {
        var remoteHead = await _cliRunner.RunAsync(
            "git",
            ["symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD"],
            repositoryPath,
            null,
            ct);

        if (remoteHead.IsSuccess)
        {
            var resolved = NormalizeReferenceCandidate(remoteHead.StdOut);
            if (!string.IsNullOrWhiteSpace(resolved) &&
                await ReferenceExistsAsync(repositoryPath, resolved, ct))
            {
                return resolved;
            }
        }

        var fallbackCandidates = new[] { "origin/main", "origin/master", "main", "master" };
        var validFallbacks = new List<string>();
        foreach (var candidate in fallbackCandidates)
        {
            if (await ReferenceExistsAsync(repositoryPath, candidate, ct))
            {
                validFallbacks.Add(candidate);
            }
        }

        if (validFallbacks.Count > 1)
        {
            _logger.LogWarning(
                "Mehrdeutige Basis-Branch-Kandidaten für {RepositoryPath}: {Candidates}. Verwende {Selected}.",
                repositoryPath,
                string.Join(", ", validFallbacks),
                validFallbacks[0]);
        }

        if (validFallbacks.Count > 0)
        {
            return validFallbacks[0];
        }

        _logger.LogWarning("Basis-Branch konnte für {RepositoryPath} nicht ermittelt werden.", repositoryPath);
        return null;
    }

    private static string? NormalizeReferenceCandidate(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return null;
        }

        var firstLine = rawOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        var arrowIndex = firstLine.IndexOf("->", StringComparison.Ordinal);
        if (arrowIndex >= 0 && arrowIndex < firstLine.Length - 2)
        {
            return firstLine[(arrowIndex + 2)..].Trim();
        }

        return firstLine;
    }

    private async Task<bool> ReferenceExistsAsync(string repositoryPath, string reference, CancellationToken ct)
    {
        var verify = await _cliRunner.RunAsync("git", ["rev-parse", "--verify", "--quiet", reference], repositoryPath, null, ct);
        return verify.IsSuccess && !string.IsNullOrWhiteSpace(verify.StdOut);
    }

    private IReadOnlyList<WorkspaceFileNode> BuildCommitFileTree(string commitSha, string diffTreeOutput)
    {
        var files = new List<WorkspaceFileNode>();
        var rootNodes = new List<WorkspaceFileNode>();
        var pathMap = new Dictionary<string, WorkspaceFileNode>(StringComparer.OrdinalIgnoreCase);

        var tokens = diffTreeOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < tokens.Length;)
        {
            var statusToken = tokens[index++].Trim();
            if (string.IsNullOrWhiteSpace(statusToken))
            {
                continue;
            }

            var statusCode = statusToken[0];
            var status = new WorkspaceFileStatus(statusCode, ' ');
            if (index >= tokens.Length)
            {
                continue;
            }

            string relativePath;
            string? sourcePath = null;
            if (statusCode is 'R' or 'C')
            {
                sourcePath = NormalizeRelativePath(tokens[index++]);
                if (index >= tokens.Length)
                {
                    continue;
                }

                relativePath = NormalizeRelativePath(tokens[index++]);
            }
            else
            {
                relativePath = NormalizeRelativePath(tokens[index++]);
            }

            var node = new WorkspaceFileNode
            {
                Name = Path.GetFileName(relativePath),
                RelativePath = relativePath,
                IsDirectory = false,
                IsDeleted = status.IsDeleted,
                SourceRelativePath = sourcePath,
                Status = status,
                CommitSha = commitSha,
            };

            files.Add(node);
            InsertNode(rootNodes, pathMap, node);
        }

        SortNodes(rootNodes);
        return rootNodes;
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

    private static bool IsBinaryText(string? text) => text?.Contains('\0') == true;

    private static string DecodeText(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
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
}
