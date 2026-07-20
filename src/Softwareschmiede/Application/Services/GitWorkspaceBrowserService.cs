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
    private const int MaxLazyLoadDepth = 64;
    private const string VerzeichnisVorschauHinweis = "Verzeichnisse können nicht direkt in der Vorschau angezeigt werden.";
    private const string BinaerdateiHinweis = "Binärdatei – Vorschau nicht verfügbar.";

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
            return new FilePreview(
                node.RelativePath,
                node.SourceRelativePath,
                IsDeleted: node.IsDeleted,
                IsBinary: false,
                IsTooBig: false,
                CurrentContent: null,
                OriginalContent: null,
                Hint: VerzeichnisVorschauHinweis);
        }

        var relativePath = NormalizeRelativePath(node.RelativePath);
        var sourceRelativePath = string.IsNullOrWhiteSpace(node.SourceRelativePath)
            ? null
            : NormalizeRelativePath(node.SourceRelativePath);
        var workingTreePath = CombinePath(repositoryPath, relativePath);

        if (node.IsDeleted)
        {
            var originalContent = await ReadHeadContentAsync(repositoryPath, sourceRelativePath ?? relativePath, ct);
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                IsDeleted: true,
                IsBinary: false,
                IsTooBig: false,
                CurrentContent: null,
                OriginalContent: originalContent,
                Hint: null);
        }

        if (!File.Exists(workingTreePath))
        {
            var originalContent = await ReadHeadContentAsync(repositoryPath, sourceRelativePath ?? relativePath, ct);
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                IsDeleted: false,
                IsBinary: false,
                IsTooBig: false,
                CurrentContent: null,
                OriginalContent: originalContent,
                Hint: "Die aktuelle Datei existiert nicht mehr.");
        }

        var fileInfo = new FileInfo(workingTreePath);
        if (fileInfo.Length > MaxInlineBytes)
        {
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                IsDeleted: false,
                IsBinary: false,
                IsTooBig: true,
                CurrentContent: null,
                OriginalContent: null,
                Hint: $"Datei ist für die Inline-Vorschau zu groß ({FormatBytes(fileInfo.Length)}).");
        }

        var fileBytes = await File.ReadAllBytesAsync(workingTreePath, ct);
        if (IsBinary(fileBytes))
        {
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                IsDeleted: false,
                IsBinary: true,
                IsTooBig: false,
                CurrentContent: null,
                OriginalContent: null,
                Hint: BinaerdateiHinweis);
        }

        var currentContent = DecodeText(fileBytes);
        var original = sourceRelativePath is not null || node.Status is not null
            ? await ReadHeadContentAsync(repositoryPath, sourceRelativePath ?? relativePath, ct)
            : null;

        return new FilePreview(
            relativePath,
            sourceRelativePath,
            IsDeleted: false,
            IsBinary: false,
            IsTooBig: false,
            CurrentContent: currentContent,
            OriginalContent: original,
            Hint: null);
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
            return new FilePreview(
                node.RelativePath,
                node.SourceRelativePath,
                IsDeleted: node.IsDeleted,
                IsBinary: false,
                IsTooBig: false,
                CurrentContent: null,
                OriginalContent: null,
                Hint: VerzeichnisVorschauHinweis);
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
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                IsDeleted: node.IsDeleted,
                IsBinary: true,
                IsTooBig: false,
                CurrentContent: null,
                OriginalContent: null,
                Hint: BinaerdateiHinweis);
        }

        var currentByteCount = currentContent is null ? 0L : Encoding.UTF8.GetByteCount(currentContent);
        var originalByteCount = originalContent is null ? 0L : Encoding.UTF8.GetByteCount(originalContent);
        if (currentByteCount > MaxInlineBytes || originalByteCount > MaxInlineBytes)
        {
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                IsDeleted: node.IsDeleted,
                IsBinary: false,
                IsTooBig: true,
                CurrentContent: null,
                OriginalContent: null,
                Hint: $"Commit-Datei ist für die Inline-Vorschau zu groß ({FormatBytes(Math.Max(currentByteCount, originalByteCount))}).");
        }

        if (!currentResult.IsSuccess && !originalResult.IsSuccess)
        {
            return new FilePreview(
                relativePath,
                sourceRelativePath,
                IsDeleted: node.IsDeleted,
                IsBinary: false,
                IsTooBig: false,
                CurrentContent: null,
                OriginalContent: null,
                Hint: $"Commit-Vorschau konnte nicht geladen werden: {currentResult.StdErr}");
        }

        return new FilePreview(
            relativePath,
            sourceRelativePath,
            IsDeleted: node.IsDeleted,
            IsBinary: false,
            IsTooBig: false,
            CurrentContent: currentContent,
            OriginalContent: originalContent,
            Hint: null);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkspaceFileNode>> LoadWorkingTreeAsync(string repositoryPath, int maxInitialDepth = 2, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var effectiveMaxDepth = Math.Min(maxInitialDepth, MaxLazyLoadDepth);
        if (maxInitialDepth > MaxLazyLoadDepth)
        {
            _logger.LogWarning(
                "maxInitialDepth {MaxInitialDepth} überschreitet MaxLazyLoadDepth {MaxLazyLoadDepth} für {RepositoryPath}; wird auf {EffectiveMaxDepth} begrenzt.",
                maxInitialDepth,
                MaxLazyLoadDepth,
                repositoryPath,
                effectiveMaxDepth);
        }

        return WalkDirectoryAsync(repositoryPath, repositoryPath, 0, effectiveMaxDepth, $"Arbeitsbaum für {repositoryPath}", ct);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkspaceFileNode>> LoadSubtreeAsync(string repositoryPath, string parentPath, int depth, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(parentPath);

        if (depth > MaxLazyLoadDepth)
        {
            _logger.LogWarning(
                "Lazy-Load-Tiefe {Depth} überschreitet MaxLazyLoadDepth {MaxLazyLoadDepth} für {ParentPath}; Enumeration wird übersprungen.",
                depth,
                MaxLazyLoadDepth,
                parentPath);
            return Task.FromResult((IReadOnlyList<WorkspaceFileNode>)new List<WorkspaceFileNode>());
        }

        var fullParentPath = CombinePath(repositoryPath, parentPath);
        return WalkDirectoryAsync(repositoryPath, fullParentPath, depth, depth + 1, $"Unterverzeichnis {fullParentPath}", ct);
    }

    private Task<IReadOnlyList<WorkspaceFileNode>> WalkDirectoryAsync(string rootPath, string startPath, int startDepth, int maxDepth, string warnLabel, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(startPath))
            {
                _logger.LogWarning("{WarnLabel} kann nicht geladen werden: Pfad existiert nicht.", warnLabel);
                return (IReadOnlyList<WorkspaceFileNode>)new List<WorkspaceFileNode>();
            }

            var nodes = new List<WorkspaceFileNode>();
            var context = new WorkingTreeWalkContext(rootPath, maxDepth, ct);
            WalkWorkingTreeDirectory(startPath, nodes, startDepth, context);

            if (context.NodeCount >= MaxWorkingTreeNodeCount)
            {
                _logger.LogWarning(
                    "{WarnLabel} überschreitet die Knoten-Obergrenze ({MaxNodeCount}); Ergebnis wird gekürzt angezeigt.",
                    warnLabel,
                    MaxWorkingTreeNodeCount);
            }

            SortNodes(nodes);
            return (IReadOnlyList<WorkspaceFileNode>)nodes;
        }, ct);
    }

    private void WalkWorkingTreeDirectory(string currentPath, IList<WorkspaceFileNode> parentNodes, int currentDepth, WorkingTreeWalkContext context)
    {
        if (context.NodeCount >= MaxWorkingTreeNodeCount)
        {
            return;
        }

        if (!TryEnumerateDirectoryEntries(currentPath, out var directories, out var files))
        {
            return;
        }

        foreach (var directoryPath in directories)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.NodeCount >= MaxWorkingTreeNodeCount)
            {
                return;
            }

            if (string.Equals(Path.GetFileName(directoryPath), GitDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddDirectoryNode(directoryPath, parentNodes, currentDepth, context);
        }

        foreach (var filePath in files)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.NodeCount >= MaxWorkingTreeNodeCount)
            {
                return;
            }

            AddFileNode(filePath, parentNodes, currentDepth, context);
        }
    }

    private bool TryEnumerateDirectoryEntries(string currentPath, out IEnumerable<string> directories, out IEnumerable<string> files)
    {
        try
        {
            directories = Directory.EnumerateDirectories(currentPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            files = Directory.EnumerateFiles(currentPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogDebug(ex, "Verzeichnis {DirectoryPath} konnte nicht aufgezählt werden und wird übersprungen.", currentPath);
            directories = [];
            files = [];
            return false;
        }
    }

    private void AddDirectoryNode(string directoryPath, IList<WorkspaceFileNode> parentNodes, int currentDepth, WorkingTreeWalkContext context)
    {
        var directoryNode = new WorkspaceFileNode
        {
            Name = Path.GetFileName(directoryPath),
            RelativePath = Path.GetRelativePath(context.RootPath, directoryPath),
            IsDirectory = true,
            Depth = currentDepth,
        };
        context.NodeCount++;
        parentNodes.Add(directoryNode);

        if (currentDepth + 1 < context.MaxDepth)
        {
            directoryNode.ChildrenLoaded = true;
            WalkWorkingTreeDirectory(directoryPath, directoryNode.Children, currentDepth + 1, context);
        }
        else
        {
            directoryNode.Children.Add(WorkspaceFileNode.CreatePlaceholder(currentDepth + 1));
        }
    }

    private static void AddFileNode(string filePath, IList<WorkspaceFileNode> parentNodes, int currentDepth, WorkingTreeWalkContext context)
    {
        parentNodes.Add(new WorkspaceFileNode
        {
            Name = Path.GetFileName(filePath),
            RelativePath = Path.GetRelativePath(context.RootPath, filePath),
            IsDirectory = false,
            Depth = currentDepth,
        });
        context.NodeCount++;
    }

    private sealed class WorkingTreeWalkContext(string rootPath, int maxDepth, CancellationToken cancellationToken)
    {
        public string RootPath { get; } = rootPath;

        public int MaxDepth { get; } = maxDepth;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public int NodeCount { get; set; }
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

    private static void InsertNode(IList<WorkspaceFileNode> rootNodes, IDictionary<string, WorkspaceFileNode> pathMap, WorkspaceFileNode fileNode)
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

    private static void SortNodes(IList<WorkspaceFileNode> nodes)
    {
        var sortableNodes = nodes.Where(node => !node.IsPlaceholder).ToList();
        if (sortableNodes.Count <= 1)
        {
            return;
        }

        var sortedNodes = sortableNodes
            .OrderBy(node => node.IsDirectory ? 0 : 1)
            .ThenBy(node => node.IsDeleted)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < sortedNodes.Count; index++)
        {
            nodes[index] = sortedNodes[index];
        }

        foreach (var directory in sortedNodes.Where(node => node.IsDirectory))
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
