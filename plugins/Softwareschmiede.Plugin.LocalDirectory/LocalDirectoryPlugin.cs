using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Git-Plugin für lokale Verzeichnisse ohne Remote-Provider.</summary>
public sealed class LocalDirectoryPlugin : GitPluginBase<LocalDirectoryPlugin>
{
    private const string WorkspaceModeKey = "WorkspaceMode";
    private const string SourceDirectoryKey = "SourceDirectory";
    private const string ConfirmGitInitKey = "ConfirmGitInitInSourceDirectory";
    private const string CopyTimeoutSecondsKey = "CopyTimeoutSeconds";
    private const string CopyMaxFilesKey = "CopyMaxFiles";
    private const string CopyMaxMegabytesKey = "CopyMaxMegabytes";
    private const int DefaultCopyTimeoutSeconds = 600;
    private const int DefaultCopyMaxFiles = 100_000;
    private const long DefaultCopyMaxMegabytes = 10 * 1024;
    private const string WorkspacePointerFileName = ".softwareschmiede-local-workspace";
    private const string SourcePointerFileName = ".softwareschmiede-local-source";
    private const string SourceCopyStrategyName = "SourceCopy";
    private const string InitialWorkspaceCommitMessage = "Initial workspace snapshot";
    private const string BootstrapUserName = "Softwareschmiede";
    private const string BootstrapUserEmail = "noreply@softwareschmiede.local";

    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<LocalDirectoryPlugin> _logger;
    private readonly ConcurrentDictionary<string, string> _workspaceMappings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _workspaceSourceMappings = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override string PluginName => "Local Directory";

    /// <inheritdoc/>
    public override string PluginPrefix => "LocalDirectoryPlugin";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.SourceCodeManagement;

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups()
    {
        var fields = new List<PluginSettingField>
        {
            new(
                Key: WorkspaceModeKey,
                Label: "Workspace-Modus",
                FieldType: PluginSettingFieldType.Enum,
                Description: "InSourceDirectory arbeitet direkt im Quellpfad. SeparateWorkingDirectory erstellt eine Arbeitskopie.",
                IsRequired: true,
                EnumOptions: [WorkspaceMode.InSourceDirectory.ToString(), WorkspaceMode.SeparateWorkingDirectory.ToString()],
                DefaultValue: WorkspaceMode.SeparateWorkingDirectory.ToString()),
            new(
                Key: SourceDirectoryKey,
                Label: "SourceDirectory (optional)",
                FieldType: PluginSettingFieldType.Text,
                Description: "Optionaler Fallback-Quellpfad, wenn beim Start kein Pfad übergeben wird.",
                IsRequired: false)
        };

        if (ResolveWorkspaceMode() == WorkspaceMode.InSourceDirectory)
        {
            fields.Add(new PluginSettingField(
                Key: ConfirmGitInitKey,
                Label: "git init im Quellverzeichnis explizit bestätigen",
                FieldType: PluginSettingFieldType.Boolean,
                Description: "Pflicht für InSourceDirectory wenn noch kein .git vorhanden ist.",
                IsRequired: false));
        }

        fields.AddRange(
        [
            new PluginSettingField(
                Key: CopyTimeoutSecondsKey,
                Label: "Copy Timeout (Sekunden)",
                FieldType: PluginSettingFieldType.Integer,
                Description: "Guardrail für Verzeichniskopien. Default: 600.",
                IsRequired: false),
            new PluginSettingField(
                Key: CopyMaxFilesKey,
                Label: "Copy Max Files",
                FieldType: PluginSettingFieldType.Integer,
                Description: "Maximale Dateianzahl je Kopie. Default: 100000.",
                IsRequired: false),
            new PluginSettingField(
                Key: CopyMaxMegabytesKey,
                Label: "Copy Max Megabytes",
                FieldType: PluginSettingFieldType.Integer,
                Description: "Maximale Datenmenge je Kopie. Default: 10240 MB.",
                IsRequired: false)
        ]);

        return [new PluginSettingGroup("Workspace", fields)];
    }

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingField> GetRepositoryLinkFields() =>
    [
        new PluginSettingField(
            Key: SourceDirectoryKey,
            Label: "Lokales Verzeichnis",
            FieldType: PluginSettingFieldType.Text,
            Placeholder: @"C:\Projekte\MeinRepository",
            Description: "Lokales Quellverzeichnis des Projekts.",
            IsRequired: true)
    ];

    /// <summary>Erstellt eine neue Instanz von <see cref="LocalDirectoryPlugin"/>.</summary>
    public LocalDirectoryPlugin(
        ICliRunner cliRunner,
        ICredentialStore credentialStore,
        ILogger<LocalDirectoryPlugin> logger)
        : base(cliRunner)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default)
        => throw BuildNotSupported(nameof(GetIssuesAsync));

    /// <inheritdoc/>
    public override async Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var mode = ResolveWorkspaceMode();
        var sourcePath = ResolveAndNormalizePath(ResolveSourcePath(repositoryUrl));
        EnsureDirectoryExists(sourcePath, "Quellverzeichnis");

        var workingPath = mode == WorkspaceMode.InSourceDirectory
            ? sourcePath
            : ResolveAndNormalizePath(targetPath);

        if (mode == WorkspaceMode.InSourceDirectory)
        {
            await ValidateWorkspaceIsCleanAsync(sourcePath, ct);
            await EnsureInitializedInSourceDirectoryAsync(sourcePath, ct);
            WriteWorkspacePointer(targetPath, sourcePath);
            TrackWorkspace(targetPath, sourcePath);
            TrackSourceDirectory(targetPath, sourcePath, sourcePath);
            return;
        }

        EnsureTargetIsSafeForCopy(workingPath, sourcePath);

        LogPreparationStrategy(SourceCopyStrategyName, "SeparateWorkingDirectory", sourcePath, workingPath);
        await CopyDirectoryWithGuardrailsAsync(sourcePath, workingPath, ct);
        await EnsureInitializedInWorkingDirectoryAsync(workingPath, ct);
        await CreateInitialWorkspaceCommitAsync(workingPath, ct);
        TrackWorkspace(targetPath, workingPath);
        TrackSourceDirectory(targetPath, workingPath, sourcePath);
    }

    /// <inheritdoc/>
    public override async Task CreateBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        var workspacePath = ResolveWorkspacePath(localPath);
        await EnsureGitRepositoryAsync(workspacePath, ct);
        await base.CreateBranchAsync(workspacePath, branchName, ct);
    }

    /// <inheritdoc/>
    public override async Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        if (ResolveWorkspaceMode() != WorkspaceMode.SeparateWorkingDirectory)
        {
            throw BuildNotSupported(nameof(PushBranchAsync));
        }

        await MergeToSourceAsync(localPath, ct);
    }

    /// <inheritdoc/>
    public override async Task PullAsync(string localPath, CancellationToken ct = default)
    {
        if (ResolveWorkspaceMode() != WorkspaceMode.SeparateWorkingDirectory)
        {
            throw BuildNotSupported(nameof(PullAsync));
        }

        var workspacePath = ResolveWorkspacePath(localPath);
        await EnsureGitRepositoryAsync(workspacePath, ct);
        await ValidateWorkspaceIsCleanAsync(workspacePath, ct);
        var sourcePath = await ResolveSourcePathForWorkspaceAsync(workspacePath, ct);
        EnsureDirectoryExists(sourcePath, "Quellverzeichnis");

        _logger.LogInformation(
            "LocalDirectoryPlugin.PullAsync führt im SeparateWorkingDirectory-Modus keinen Merge aus. Es wird eine Dateisynchronisation von Quelle zu Arbeitsverzeichnis ausgeführt.");

        await CopyDirectoryForSyncAsync(sourcePath, workspacePath, overwriteFiles: true, ct);
    }

    /// <inheritdoc/>
    public override Task<PullRequest> CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct = default)
        => throw BuildNotSupported(nameof(CreatePullRequestAsync));

    /// <inheritdoc/>
    public override async Task CommitAsync(string localPath, string message, CancellationToken ct = default)
    {
        var workspacePath = ResolveWorkspacePath(localPath);
        await EnsureGitRepositoryAsync(workspacePath, ct);
        await base.CommitAsync(workspacePath, message, ct);
    }

    /// <inheritdoc/>
    public override async Task ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct = default)
    {
        var workspacePath = ResolveWorkspacePath(localPath);
        await EnsureGitRepositoryAsync(workspacePath, ct);
        await base.ResetAsync(workspacePath, resetType, targetRef, ct);
    }

    /// <inheritdoc/>
    public override Task<bool> CheckHealthAsync(CancellationToken ct = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public override Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default)
        => throw BuildNotSupported(nameof(GetRemoteBranchesAsync));

    /// <inheritdoc/>
    public override Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default)
        => throw BuildNotSupported(nameof(GetDefaultBranchAsync));

    /// <inheritdoc/>
    public override Task CheckoutRemoteBranchAsync(string localPath, string branchName, CancellationToken ct = default)
        => throw BuildNotSupported(nameof(CheckoutRemoteBranchAsync));

    /// <inheritdoc/>
    public override Task<GitActionCapabilities> GetGitActionCapabilitiesAsync(string? localPath = null, CancellationToken ct = default)
    {
        var isWorkingDirectoryCopy = ResolveWorkspaceMode() == WorkspaceMode.SeparateWorkingDirectory;
        return Task.FromResult(new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: isWorkingDirectoryCopy,
            CanPush: !isWorkingDirectoryCopy,
            CanPull: !isWorkingDirectoryCopy,
            CanCreatePullRequest: !isWorkingDirectoryCopy,
            CanMergeToSource: isWorkingDirectoryCopy));
    }

    /// <inheritdoc/>
    public override async Task MergeToSourceAsync(string localPath, CancellationToken ct = default)
    {
        if (ResolveWorkspaceMode() != WorkspaceMode.SeparateWorkingDirectory)
        {
            throw BuildNotSupported(nameof(MergeToSourceAsync));
        }

        var workspacePath = ResolveWorkspacePath(localPath);
        await EnsureGitRepositoryAsync(workspacePath, ct);
        var sourcePath = await ResolveSourcePathForWorkspaceAsync(workspacePath, ct);
        EnsureDirectoryExists(sourcePath, "Quellverzeichnis");

        if (string.Equals(workspacePath, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Merge-Synchronisation erfordert unterschiedliche Pfade für Arbeits- und Quellverzeichnis.");
        }

        await CopyDirectoryForSyncAsync(workspacePath, sourcePath, overwriteFiles: true, ct);
        await ApplyDeletedFilesToSourceAsync(workspacePath, sourcePath, ct);
    }

    /// <inheritdoc/>
    public override Task<IEnumerable<AvailableRepository>> GetAvailableRepositoriesAsync(CancellationToken ct = default)
    {
        var sourceDir = _credentialStore.GetCredential($"{PluginPrefix}.{SourceDirectoryKey}");
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            _logger.LogWarning("LocalDirectoryPlugin: Kein gültiges SourceDirectory konfiguriert.");
            return Task.FromResult(Enumerable.Empty<AvailableRepository>());
        }

        var dirs = Directory.EnumerateDirectories(sourceDir)
            .Select(d => new AvailableRepository(Path.GetFileName(d), File.GetLastAccessTime(d), Path.GetFileName(d), d))
            .OrderByDescending(r => r.UpdatedAt)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IEnumerable<AvailableRepository>>(dirs);
    }

    private NotSupportedException BuildNotSupported(string methodName)
        => new($"LocalDirectoryPlugin unterstützt '{methodName}' nicht, da keine Remote-Provider-Funktionen verfügbar sind.");

    private WorkspaceMode ResolveWorkspaceMode()
    {
        var stored = _credentialStore.GetCredential($"{PluginPrefix}.{WorkspaceModeKey}");
        if (string.IsNullOrWhiteSpace(stored))
        {
            return WorkspaceMode.SeparateWorkingDirectory;
        }

        if (Enum.TryParse<WorkspaceMode>(stored.Trim(), ignoreCase: false, out var parsed))
        {
            return parsed;
        }

        _logger.LogWarning("Ungültiger WorkspaceMode '{WorkspaceMode}' im Store. Fallback auf SeparateWorkingDirectory.", stored);
        return WorkspaceMode.SeparateWorkingDirectory;
    }

    private string ResolveSourcePath(string repositoryUrl)
    {
        if (!string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return repositoryUrl;
        }

        var fallback = _credentialStore.GetCredential($"{PluginPrefix}.{SourceDirectoryKey}");
        if (string.IsNullOrWhiteSpace(fallback))
        {
            throw new InvalidOperationException("Für LocalDirectoryPlugin wurde kein Quellverzeichnis übergeben oder konfiguriert.");
        }

        return fallback;
    }

    private async Task EnsureInitializedInSourceDirectoryAsync(string sourcePath, CancellationToken ct)
    {
        if (await IsGitRepositoryAsync(sourcePath, ct))
        {
            return;
        }

        if (!ResolveConfirmationFlag())
        {
            throw new InvalidOperationException(
                "git init im Quellverzeichnis ist nicht bestätigt. Setzen Sie LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory explizit auf true.");
        }

        var result = await RunGitAsync(["init"], sourcePath, ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git init fehlgeschlagen: {result.StdErr}");
        }
    }

    private bool ResolveConfirmationFlag()
    {
        var raw = _credentialStore.GetCredential($"{PluginPrefix}.{ConfirmGitInitKey}");
        return string.Equals(raw?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsGitRepositoryAsync(string path, CancellationToken ct)
    {
        var result = await RunGitAsync(["rev-parse", "--is-inside-work-tree"], path, ct);
        return result.IsSuccess && string.Equals(result.StdOut.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private void LogPreparationStrategy(string strategy, string reason, string sourcePath, string targetPath)
    {
        _logger.LogInformation(
            "Using local workspace preparation strategy {Strategy} ({Reason}). Source: {SourcePath}, Target: {TargetPath}",
            strategy,
            reason,
            sourcePath,
            targetPath);
    }

    private async Task ValidateWorkspaceIsCleanAsync(string workspacePath, CancellationToken ct)
    {
        if (!await IsGitRepositoryAsync(workspacePath, ct))
        {
            return;
        }

        var status = await RunGitAsync(["status", "--porcelain"], workspacePath, ct);
        if (!status.IsSuccess)
        {
            throw new InvalidOperationException($"Workspace-Status konnte nicht geprüft werden: {status.StdErr}");
        }

        var relevantChanges = status.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length >= 4)
            .Select(line => NormalizeGitRelativePath(line[3..]))
            .Where(path => !ShouldSkipRelativePath(path))
            .ToList();

        if (relevantChanges.Count > 0)
        {
            throw new InvalidOperationException(
                $"Workspace '{workspacePath}' enthält uncommitted changes. Operation wird aus Sicherheitsgründen abgebrochen.");
        }
    }

    private void EnsureTargetIsSafeForCopy(string workingPath, string sourcePath)
    {
        if (string.Equals(workingPath, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SeparateWorkingDirectory erfordert ein anderes Zielverzeichnis als das Quellverzeichnis.");
        }

        if (Directory.Exists(workingPath) && Directory.EnumerateFileSystemEntries(workingPath).Any())
        {
            throw new InvalidOperationException($"Zielverzeichnis '{workingPath}' ist nicht leer. Dirty Workspace wird nicht überschrieben.");
        }
    }

    private async Task CopyDirectoryWithGuardrailsAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        var timeoutSeconds = ResolveIntSetting(CopyTimeoutSecondsKey, DefaultCopyTimeoutSeconds, minimum: 1);
        var maxFiles = ResolveIntSetting(CopyMaxFilesKey, DefaultCopyMaxFiles, minimum: 1);
        var maxMegabytes = ResolveIntSetting(CopyMaxMegabytesKey, (int)DefaultCopyMaxMegabytes, minimum: 1);
        var maxBytes = (long)maxMegabytes * 1024L * 1024L;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var token = linkedCts.Token;

        Directory.CreateDirectory(destinationPath);

        var copiedFiles = 0;
        var copiedBytes = 0L;
        var directories = new Stack<string>();
        directories.Push(sourcePath);

        try
        {
            while (directories.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var current = directories.Pop();

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    EnsureNotReparsePoint(directory);
                    var relative = Path.GetRelativePath(sourcePath, directory);
                    if (ShouldSkipRelativePath(relative))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.Combine(destinationPath, relative));
                    directories.Push(directory);
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    token.ThrowIfCancellationRequested();
                    EnsureNotReparsePoint(file);
                    var relative = Path.GetRelativePath(sourcePath, file);
                    if (ShouldSkipRelativePath(relative))
                    {
                        continue;
                    }

                    var fileInfo = new FileInfo(file);
                    copiedFiles++;
                    copiedBytes += fileInfo.Length;

                    if (copiedFiles > maxFiles)
                    {
                        throw new InvalidOperationException($"Copy-Guardrail verletzt: mehr als {maxFiles} Dateien.");
                    }

                    if (copiedBytes > maxBytes)
                    {
                        throw new InvalidOperationException($"Copy-Guardrail verletzt: mehr als {maxMegabytes} MB.");
                    }

                    var destinationFile = Path.Combine(destinationPath, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                    fileInfo.CopyTo(destinationFile, overwrite: false);
                }
            }
        }
        catch
        {
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, recursive: true);
            }

            throw;
        }
    }

    private async Task EnsureInitializedInWorkingDirectoryAsync(string workingPath, CancellationToken ct)
    {
        if (await IsGitRepositoryAsync(workingPath, ct))
        {
            return;
        }

        var initResult = await RunGitAsync(["init"], workingPath, ct);
        if (!initResult.IsSuccess)
        {
            throw new InvalidOperationException($"git init im Arbeitsverzeichnis fehlgeschlagen: {initResult.StdErr}");
        }
    }

    private async Task CreateInitialWorkspaceCommitAsync(string workingPath, CancellationToken ct)
    {
        if (!await IsGitRepositoryAsync(workingPath, ct))
        {
            throw new InvalidOperationException($"Workspace '{workingPath}' konnte nach git init nicht bestätigt werden.");
        }

        await ConfigureBootstrapIdentityAsync(workingPath, ct);

        var addResult = await RunGitAsync(["add", "."], workingPath, ct);
        if (!addResult.IsSuccess)
        {
            throw new InvalidOperationException($"git add für Initial-Commit fehlgeschlagen: {addResult.StdErr}");
        }

        var commitResult = await RunGitAsync(["commit", "-m", InitialWorkspaceCommitMessage], workingPath, ct);
        if (!commitResult.IsSuccess)
        {
            throw new InvalidOperationException($"Initialer Workspace-Commit fehlgeschlagen: {commitResult.StdErr}");
        }
    }

    private async Task ConfigureBootstrapIdentityAsync(string workingPath, CancellationToken ct)
    {
        var nameResult = await RunGitAsync(["config", "user.name", BootstrapUserName], workingPath, ct);
        if (!nameResult.IsSuccess)
        {
            throw new InvalidOperationException($"git config user.name fehlgeschlagen: {nameResult.StdErr}");
        }

        var emailResult = await RunGitAsync(["config", "user.email", BootstrapUserEmail], workingPath, ct);
        if (!emailResult.IsSuccess)
        {
            throw new InvalidOperationException($"git config user.email fehlgeschlagen: {emailResult.StdErr}");
        }
    }

    private async Task<string> ResolveSourcePathForWorkspaceAsync(string workspacePath, CancellationToken ct)
    {
        var normalizedWorkspace = ResolveAndNormalizePath(workspacePath);
        if (_workspaceSourceMappings.TryGetValue(normalizedWorkspace, out var mapped))
        {
            return mapped;
        }

        var sourcePointerPath = Path.Combine(normalizedWorkspace, SourcePointerFileName);
        if (File.Exists(sourcePointerPath))
        {
            var pointedSource = ResolveAndNormalizePath(File.ReadAllText(sourcePointerPath));
            _workspaceSourceMappings[normalizedWorkspace] = pointedSource;
            return pointedSource;
        }

        var remoteResult = await RunGitAsync(["config", "--get", "remote.origin.url"], normalizedWorkspace, ct);
        if (remoteResult.IsSuccess && !string.IsNullOrWhiteSpace(remoteResult.StdOut))
        {
            var remotePath = ResolveAndNormalizePath(remoteResult.StdOut.Trim());
            if (Directory.Exists(remotePath))
            {
                _workspaceSourceMappings[normalizedWorkspace] = remotePath;
                return remotePath;
            }
        }

        var configuredSource = _credentialStore.GetCredential($"{PluginPrefix}.{SourceDirectoryKey}");
        if (!string.IsNullOrWhiteSpace(configuredSource))
        {
            var normalizedConfiguredSource = ResolveAndNormalizePath(configuredSource);
            _workspaceSourceMappings[normalizedWorkspace] = normalizedConfiguredSource;
            return normalizedConfiguredSource;
        }

        throw new InvalidOperationException(
            $"Quellverzeichnis für Workspace '{normalizedWorkspace}' konnte nicht aufgelöst werden.");
    }

    private async Task CopyDirectoryForSyncAsync(string sourcePath, string destinationPath, bool overwriteFiles, CancellationToken ct)
    {
        foreach (var file in EnumerateFilesForSync(sourcePath))
        {
            ct.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourcePath, file);
            if (ShouldSkipRelativePath(relativePath))
            {
                continue;
            }

            var destinationFile = CombineAndValidatePath(destinationPath, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationFile)
                ?? throw new InvalidOperationException($"Ungültiger Zielpfad '{destinationFile}'.");
            Directory.CreateDirectory(destinationDirectory);
            File.Copy(file, destinationFile, overwrite: overwriteFiles);
        }
    }

    private async Task ApplyDeletedFilesToSourceAsync(string workspacePath, string sourcePath, CancellationToken ct)
    {
        var deletedPaths = await ResolveDeletedPathsAsync(workspacePath, ct);
        foreach (var deletedPath in deletedPaths)
        {
            ct.ThrowIfCancellationRequested();
            if (ShouldSkipRelativePath(deletedPath))
            {
                continue;
            }

            var sourceCandidate = CombineAndValidatePath(sourcePath, deletedPath);
            if (File.Exists(sourceCandidate))
            {
                File.Delete(sourceCandidate);
                continue;
            }

            if (Directory.Exists(sourceCandidate))
            {
                Directory.Delete(sourceCandidate, recursive: true);
            }
        }
    }

    private async Task<IReadOnlyCollection<string>> ResolveDeletedPathsAsync(string workspacePath, CancellationToken ct)
    {
        var statusResult = await RunGitAsync(["status", "--porcelain"], workspacePath, ct);
        if (!statusResult.IsSuccess)
        {
            throw new InvalidOperationException($"git status für Delete-Sync fehlgeschlagen: {statusResult.StdErr}");
        }

        var deletedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = statusResult.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 3)
            {
                continue;
            }

            var status = line[..2];
            var payload = line[3..];

            if (status.IndexOf('R') >= 0 && payload.Contains(" -> ", StringComparison.Ordinal))
            {
                var renameParts = payload.Split(" -> ", 2, StringSplitOptions.None);
                deletedPaths.Add(NormalizeGitRelativePath(renameParts[0]));
            }

            if (status.IndexOf('D') >= 0 || status.IndexOf('T') >= 0)
            {
                var pathPart = payload.Contains(" -> ", StringComparison.Ordinal)
                    ? payload.Split(" -> ", 2, StringSplitOptions.None)[0]
                    : payload;
                deletedPaths.Add(NormalizeGitRelativePath(pathPart));
            }
        }

        return deletedPaths;
    }

    private static IEnumerable<string> EnumerateFilesForSync(string rootPath)
    {
        var directories = new Stack<string>();
        directories.Push(rootPath);

        while (directories.Count > 0)
        {
            var current = directories.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                EnsureNotReparsePoint(directory);
                var relativeDirectory = Path.GetRelativePath(rootPath, directory);
                if (ShouldSkipRelativePath(relativeDirectory))
                {
                    continue;
                }

                directories.Push(directory);
            }

            foreach (var file in Directory.EnumerateFiles(current))
            {
                EnsureNotReparsePoint(file);
                yield return file;
            }
        }
    }

    private static string NormalizeGitRelativePath(string path)
    {
        var unquoted = UnquoteGitPath(path.Trim());
        return unquoted.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string UnquoteGitPath(string path)
    {
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
        {
            return path[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return path;
    }

    private static bool ShouldSkipRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".")
        {
            return true;
        }

        var normalized = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalized.Equals(".git", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith($".git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(SourcePointerFileName, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(WorkspacePointerFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineAndValidatePath(string rootPath, string relativePath)
    {
        var normalizedRoot = ResolveAndNormalizePath(rootPath);
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Pfad '{relativePath}' liegt außerhalb des erlaubten Root-Verzeichnisses.");
        }

        return candidate;
    }

    private static void EnsureNotReparsePoint(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
        {
            throw new InvalidOperationException($"Symlink/Reparse-Point ist nicht erlaubt: {path}");
        }
    }

    private int ResolveIntSetting(string key, int defaultValue, int minimum)
    {
        var raw = _credentialStore.GetCredential($"{PluginPrefix}.{key}");
        if (!int.TryParse(raw, out var parsed) || parsed < minimum)
        {
            return defaultValue;
        }

        return parsed;
    }

    private static string ResolveAndNormalizePath(string path)
        => Path.GetFullPath(path.Trim());

    private static void EnsureDirectoryExists(string path, string label)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"{label} '{path}' wurde nicht gefunden.");
        }
    }

    private void TrackWorkspace(string requestedPath, string resolvedPath)
    {
        var normalizedRequested = ResolveAndNormalizePath(requestedPath);
        var normalizedResolved = ResolveAndNormalizePath(resolvedPath);
        _workspaceMappings[normalizedRequested] = normalizedResolved;
        _workspaceMappings[normalizedResolved] = normalizedResolved;
    }

    private void TrackSourceDirectory(string requestedPath, string workspacePath, string sourcePath)
    {
        var normalizedRequested = ResolveAndNormalizePath(requestedPath);
        var normalizedWorkspace = ResolveAndNormalizePath(workspacePath);
        var normalizedSource = ResolveAndNormalizePath(sourcePath);
        _workspaceSourceMappings[normalizedRequested] = normalizedSource;
        _workspaceSourceMappings[normalizedWorkspace] = normalizedSource;

        if (Directory.Exists(normalizedWorkspace))
        {
            File.WriteAllText(Path.Combine(normalizedWorkspace, SourcePointerFileName), normalizedSource);
        }
    }

    private void WriteWorkspacePointer(string requestedPath, string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return;
        }

        var normalizedRequested = ResolveAndNormalizePath(requestedPath);
        var normalizedResolved = ResolveAndNormalizePath(resolvedPath);
        if (string.Equals(normalizedRequested, normalizedResolved, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(normalizedRequested);
        File.WriteAllText(Path.Combine(normalizedRequested, WorkspacePointerFileName), normalizedResolved);
    }

    private string ResolveWorkspacePath(string localPath)
    {
        var normalized = ResolveAndNormalizePath(localPath);
        if (_workspaceMappings.TryGetValue(normalized, out var mapped))
        {
            return mapped;
        }

        var pointerPath = Path.Combine(normalized, WorkspacePointerFileName);
        if (File.Exists(pointerPath))
        {
            var pointedPath = ResolveAndNormalizePath(File.ReadAllText(pointerPath));
            _workspaceMappings[normalized] = pointedPath;
            return pointedPath;
        }

        return normalized;
    }
}
