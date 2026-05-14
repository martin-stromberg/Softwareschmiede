using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Abstractions;

/// <summary>Gemeinsame Basis für Git-Plugins. Öffentlich zugänglich für externe Plugin-Assemblies.</summary>
public abstract class GitPluginBase<TPlugin> : IGitPlugin
{
    private readonly ICliRunner _cliRunner;

    protected GitPluginBase(ICliRunner cliRunner)
    {
        _cliRunner = cliRunner;
    }

    protected ICliRunner CliRunner => _cliRunner;

    public abstract string PluginName { get; }
    public abstract string PluginPrefix { get; }
    public abstract PluginType PluginType { get; }
    public abstract IReadOnlyList<PluginSettingGroup> GetSettingGroups();
    public virtual IReadOnlyList<PluginSettingField> GetRepositoryLinkFields() => [];
    public abstract Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default);
    public abstract Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default);
    public abstract Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default);
    public abstract Task PullAsync(string localPath, CancellationToken ct = default);
    public abstract Task<PullRequest> CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct = default);
    public abstract Task<bool> CheckHealthAsync(CancellationToken ct = default);
    public abstract Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default);
    public abstract Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default);
    public virtual Task<GitActionCapabilities> GetGitActionCapabilitiesAsync(string? localPath = null, CancellationToken ct = default)
        => Task.FromResult(new GitActionCapabilities(
            RepositoryKind.RemoteGit,
            IsWorkingDirectoryCopy: false,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: false));
    public virtual Task MergeToSourceAsync(string localPath, CancellationToken ct = default)
        => throw new NotSupportedException($"'{nameof(MergeToSourceAsync)}' wird von Plugin '{PluginPrefix}' nicht unterstützt.");

    /// <inheritdoc/>
    public virtual async Task CreateBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        var result = await RunGitAsync(["checkout", "-b", branchName], localPath, ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git checkout -b fehlgeschlagen: {result.StdErr}");
        }
    }

    /// <inheritdoc/>
    public virtual async Task CommitAsync(string localPath, string message, CancellationToken ct = default)
    {
        await AddAllAsync(localPath, ct);

        var result = await RunGitAsync(["commit", "-m", message], localPath, ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git commit fehlgeschlagen: {result.StdErr}");
        }
    }

    /// <inheritdoc/>
    public virtual async Task ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct = default)
    {
        var args = new List<string> { "reset", $"--{resetType}" };
        if (!string.IsNullOrWhiteSpace(targetRef))
        {
            args.Add(targetRef);
        }

        var result = await RunGitAsync(args, localPath, ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git reset fehlgeschlagen: {result.StdErr}");
        }
    }

    /// <inheritdoc/>
    public virtual async Task CheckoutRemoteBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        var result = await RunGitAsync(["checkout", "-b", branchName, "--track", $"origin/{branchName}"], localPath, ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git checkout (remote branch) fehlgeschlagen: {result.StdErr}");
        }
    }

    /// <summary>Führt <c>git add .</c> aus.</summary>
    protected async Task AddAllAsync(string localPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(["add", "."], localPath, ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git add fehlgeschlagen: {result.StdErr}");
        }
    }

    /// <summary>Stellt sicher, dass das Verzeichnis ein Git-Repository ist.</summary>
    protected async Task EnsureGitRepositoryAsync(string localPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(["rev-parse", "--is-inside-work-tree"], localPath, ct);
        if (!result.IsSuccess || !string.Equals(result.StdOut.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Pfad '{localPath}' ist kein Git-Repository.");
        }
    }

    /// <summary>Führt einen Git-Befehl aus.</summary>
    protected Task<CliResult> RunGitAsync(
        IEnumerable<string> args,
        string? workingDirectory,
        CancellationToken ct = default,
        IDictionary<string, string>? environmentVariables = null)
    {
        return _cliRunner.RunAsync("git", args, workingDirectory, environmentVariables, ct);
    }
}
