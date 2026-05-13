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
    private const string WorkingDirectoryKey = "WorkingDirectory";
    private const string ConfirmGitInitKey = "ConfirmGitInitInSourceDirectory";
    private const string CopyTimeoutSecondsKey = "CopyTimeoutSeconds";
    private const string CopyMaxFilesKey = "CopyMaxFiles";
    private const string CopyMaxMegabytesKey = "CopyMaxMegabytes";
    private const int DefaultCopyTimeoutSeconds = 600;
    private const int DefaultCopyMaxFiles = 100_000;
    private const long DefaultCopyMaxMegabytes = 10 * 1024;
    private const string WorkspacePointerFileName = ".softwareschmiede-local-workspace";

    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<LocalDirectoryPlugin> _logger;
    private readonly ConcurrentDictionary<string, string> _workspaceMappings = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override string PluginName => "Local Directory";

    /// <inheritdoc/>
    public override string PluginPrefix => "LocalDirectoryPlugin";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.SourceCodeManagement;

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Workspace",
        [
            new PluginSettingField(
                Key: WorkspaceModeKey,
                Label: "Workspace-Modus",
                FieldType: PluginSettingFieldType.Enum,
                Description: "InSourceDirectory arbeitet direkt im Quellpfad. SeparateWorkingDirectory erstellt eine Arbeitskopie.",
                IsRequired: true,
                EnumOptions: [WorkspaceMode.InSourceDirectory.ToString(), WorkspaceMode.SeparateWorkingDirectory.ToString()]),
            new PluginSettingField(
                Key: SourceDirectoryKey,
                Label: "SourceDirectory (optional)",
                FieldType: PluginSettingFieldType.Text,
                Description: "Optionaler Fallback-Quellpfad, wenn beim Start kein Pfad übergeben wird.",
                IsRequired: false),
            new PluginSettingField(
                Key: WorkingDirectoryKey,
                Label: "WorkingDirectory (optional)",
                FieldType: PluginSettingFieldType.Text,
                Description: "Optionales Zielverzeichnis für SeparateWorkingDirectory.",
                IsRequired: false),
            new PluginSettingField(
                Key: ConfirmGitInitKey,
                Label: "git init im Quellverzeichnis explizit bestätigen",
                FieldType: PluginSettingFieldType.Boolean,
                Description: "Pflicht für InSourceDirectory wenn noch kein .git vorhanden ist.",
                IsRequired: false),
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
        ])
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
            : ResolveAndNormalizePath(ResolveWorkingPath(targetPath));

        if (mode == WorkspaceMode.InSourceDirectory)
        {
            await ValidateWorkspaceIsCleanAsync(sourcePath, ct);
            await EnsureInitializedInSourceDirectoryAsync(sourcePath, ct);
            WriteWorkspacePointer(targetPath, sourcePath);
            TrackWorkspace(targetPath, sourcePath);
            return;
        }

        EnsureTargetIsSafeForCopy(workingPath, sourcePath);
        await CopyDirectoryWithGuardrailsAsync(sourcePath, workingPath, ct);
        await EnsureGitRepositoryAsync(workingPath, ct);
        await ValidateWorkspaceIsCleanAsync(workingPath, ct);
        TrackWorkspace(targetPath, workingPath);
    }

    /// <inheritdoc/>
    public override async Task CreateBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        var workspacePath = ResolveWorkspacePath(localPath);
        await EnsureGitRepositoryAsync(workspacePath, ct);
        await base.CreateBranchAsync(workspacePath, branchName, ct);
    }

    /// <inheritdoc/>
    public override Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default)
        => throw BuildNotSupported(nameof(PushBranchAsync));

    /// <inheritdoc/>
    public override Task PullAsync(string localPath, CancellationToken ct = default)
        => throw BuildNotSupported(nameof(PullAsync));

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

    private string ResolveWorkingPath(string targetPath)
    {
        var configured = _credentialStore.GetCredential($"{PluginPrefix}.{WorkingDirectoryKey}");
        return string.IsNullOrWhiteSpace(configured) ? targetPath : configured.Trim();
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

        if (!string.IsNullOrWhiteSpace(status.StdOut))
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
                    Directory.CreateDirectory(Path.Combine(destinationPath, relative));
                    directories.Push(directory);
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    token.ThrowIfCancellationRequested();
                    EnsureNotReparsePoint(file);

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

                    var relative = Path.GetRelativePath(sourcePath, file);
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
