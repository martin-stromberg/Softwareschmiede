using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Ueberwacht gespeicherte Pull Requests und fuehrt optional automatische Abschluesse aus.</summary>
public sealed class PullRequestMonitoringService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(2);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PullRequestMonitoringService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Erstellt eine neue Instanz des <see cref="PullRequestMonitoringService"/>.</summary>
    public PullRequestMonitoringService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<PullRequestMonitoringService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pull-Request-Monitoring ist fehlgeschlagen.");
            }

            await Task.Delay(PollingInterval, _timeProvider, stoppingToken);
        }
    }

    /// <summary>Fuehrt einen einzelnen Monitoring-Durchlauf aus.</summary>
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var references = scope.ServiceProvider.GetRequiredService<PullRequestReferenzService>();
        var due = await references.GetDueForMonitoringAsync(_timeProvider.GetUtcNow(), 20, ct);

        foreach (var pullRequest in due)
        {
            await MonitorAsync(scope.ServiceProvider, pullRequest, references, ct);
        }
    }

    private async Task MonitorAsync(
        IServiceProvider services,
        PullRequestReferenz pullRequest,
        PullRequestReferenzService references,
        CancellationToken ct)
    {
        if (pullRequest.Provider != PullRequestProvider.GitHub)
        {
            await references.SetProblemAsync(pullRequest, PullRequestMonitoringPhase.Failed, $"Provider '{pullRequest.Provider}' wird nicht unterstuetzt.", ct);
            return;
        }

        var plugin = ResolveProviderPlugin(services, pullRequest.Provider);
        var protokoll = services.GetRequiredService<ProtokollService>();

        try
        {
            var status = await plugin.GetPullRequestStatusAsync(pullRequest.RepositoryId, pullRequest.PullRequestNumber, ct);
            var workflowRuns = await plugin.GetPullRequestWorkflowRunsAsync(
                pullRequest.RepositoryId,
                pullRequest.PullRequestNumber,
                status.HeadSha ?? pullRequest.HeadSha,
                status.MergeCommitSha ?? pullRequest.MergeCommitSha,
                ct);

            var (phase, uncertainty) = DeterminePhase(status, workflowRuns);
            await references.UpdateFromProviderAsync(pullRequest, status, workflowRuns, phase, ct);
            if (uncertainty is not null)
            {
                await references.SetProviderUncertaintyAsync(pullRequest, phase, uncertainty, ct);
            }

            if (phase == PullRequestMonitoringPhase.PreMergeSucceeded && IsAutoCompleteEnabled(services, plugin))
            {
                await TryCompleteAsync(services, references, protokoll, plugin, pullRequest, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (NotSupportedException ex)
        {
            await references.SetProblemAsync(pullRequest, PullRequestMonitoringPhase.Failed, ex.Message, ct);
        }
        catch (Exception ex)
        {
            await references.SetRetryableErrorAsync(pullRequest, ex.Message, ct);
        }
    }

    private async Task TryCompleteAsync(
        IServiceProvider services,
        PullRequestReferenzService references,
        ProtokollService protokoll,
        IGitPlugin plugin,
        PullRequestReferenz pullRequest,
        CancellationToken ct)
    {
        pullRequest.MonitoringPhase = PullRequestMonitoringPhase.Completing;
        var options = ResolveCompletionOptions(services, plugin);
        var result = await plugin.CompletePullRequestAsync(
            pullRequest.RepositoryId,
            pullRequest.PullRequestNumber,
            options,
            ct);

        if (result.Success)
        {
            pullRequest.MergeCommitSha = result.MergeCommitSha ?? pullRequest.MergeCommitSha;
            if (result.PullRequestMerged)
            {
                await protokoll.AddEintragAsync(
                    pullRequest.AufgabeId,
                    ProtokollTyp.GitAktion,
                    $"Pull Request #{pullRequest.PullRequestNumber} automatisch abgeschlossen.",
                    ct: ct);
                await references.SetPhaseAsync(pullRequest, PullRequestMonitoringPhase.Completed, ct);
            }
            else
            {
                var protokollMessage = options.Strategy == PullRequestCompletionStrategy.ApprovalOnly
                    ? $"Pull Request #{pullRequest.PullRequestNumber} automatisch genehmigt; Merge steht noch aus."
                    : $"Auto-Merge fuer Pull Request #{pullRequest.PullRequestNumber} aktiviert; Merge steht noch aus.";
                var uncertainty = options.Strategy == PullRequestCompletionStrategy.ApprovalOnly
                    ? "Pull Request wurde genehmigt, ist aber noch offen und wird weiter ueberwacht."
                    : "Auto-Merge wurde aktiviert, der Pull Request ist aber noch offen und wird weiter ueberwacht.";
                await protokoll.AddEintragAsync(
                    pullRequest.AufgabeId,
                    ProtokollTyp.GitAktion,
                    protokollMessage,
                    ct: ct);
                await references.SetProviderUncertaintyAsync(
                    pullRequest,
                    PullRequestMonitoringPhase.Approved,
                    uncertainty,
                    ct);
            }

            return;
        }

        var phase = result.Blocked ? PullRequestMonitoringPhase.Blocked : PullRequestMonitoringPhase.Failed;
        var message = result.Message ?? "Pull Request konnte nicht automatisch abgeschlossen werden.";
        await protokoll.AddEintragAsync(
            pullRequest.AufgabeId,
            ProtokollTyp.GitAktion,
            $"Pull Request #{pullRequest.PullRequestNumber} nicht automatisch abgeschlossen: {message}",
            ct: ct);
        await references.SetProblemAsync(pullRequest, phase, message, ct);
    }

    private static (PullRequestMonitoringPhase Phase, string? Uncertainty) DeterminePhase(
        PullRequestStatusInfo status,
        IReadOnlyList<PullRequestWorkflowRunInfo> workflowRuns)
    {
        if (status.Status == PullRequestStatus.Merged)
        {
            if (string.IsNullOrWhiteSpace(status.MergeCommitSha))
            {
                return (
                    PullRequestMonitoringPhase.PostMergeUncertain,
                    "Pull Request ist gemergt, aber GitHub liefert noch keine Merge-Commit-SHA. Post-Merge-Runs koennen nicht sicher zugeordnet werden.");
            }

            var postMergeRuns = workflowRuns.Where(r => r.IsPostMerge).ToList();
            if (postMergeRuns.Count == 0)
            {
                return (
                    PullRequestMonitoringPhase.PostMergeUncertain,
                    "Pull Request ist gemergt, aber es wurden noch keine Post-Merge-Runs zur Merge-Commit-SHA gefunden.");
            }

            return (AllSucceeded(postMergeRuns)
                ? PullRequestMonitoringPhase.PostMergeSucceeded
                : AnyFailed(postMergeRuns)
                    ? PullRequestMonitoringPhase.PostMergeFailed
                    : PullRequestMonitoringPhase.PostMergeRunning, null);
        }

        if (status.MergeStatus is PullRequestMergeStatus.Blocked or PullRequestMergeStatus.Conflicting)
        {
            return (PullRequestMonitoringPhase.Blocked, null);
        }

        var preMergeRuns = workflowRuns.Where(r => !r.IsPostMerge).ToList();
        if (preMergeRuns.Count == 0 || preMergeRuns.Any(r => r.Status != WorkflowRunStatus.Completed))
        {
            return (PullRequestMonitoringPhase.PreMergeRunning, null);
        }

        return (AllSucceeded(preMergeRuns)
            ? PullRequestMonitoringPhase.PreMergeSucceeded
            : PullRequestMonitoringPhase.Failed, null);
    }

    private static bool AllSucceeded(IEnumerable<PullRequestWorkflowRunInfo> runs)
        => runs.All(r => r.Status == WorkflowRunStatus.Completed && IsSuccessfulConclusion(r.Conclusion));

    private static bool AnyFailed(IEnumerable<PullRequestWorkflowRunInfo> runs)
        => runs.Any(r => r.Status == WorkflowRunStatus.Completed && !IsSuccessfulConclusion(r.Conclusion));

    private static bool IsSuccessfulConclusion(WorkflowRunConclusion conclusion)
        => conclusion is WorkflowRunConclusion.Success or WorkflowRunConclusion.Skipped;

    private static IGitPlugin ResolveProviderPlugin(IServiceProvider services, PullRequestProvider provider)
    {
        var pluginManager = services.GetRequiredService<IPluginManager>();
        var expectedPrefix = provider == PullRequestProvider.GitHub ? "Softwareschmiede.GitHub" : provider.ToString();
        return pluginManager.GetSourceCodeManagementPlugins()
                   .FirstOrDefault(p => string.Equals(p.PluginPrefix, expectedPrefix, StringComparison.OrdinalIgnoreCase))
               ?? pluginManager.GetDefaultSourceCodeManagementPlugin();
    }

    private static bool IsAutoCompleteEnabled(IServiceProvider services, IGitPlugin plugin)
    {
        var settings = services.GetRequiredService<PluginSettingsService>();
        var field = plugin.GetSettingGroups().SelectMany(g => g.Fields).FirstOrDefault(f => f.Key == "AutoCompletePullRequests");
        var value = field is null ? null : settings.GetValue(plugin, field);
        return bool.TryParse(value ?? field?.DefaultValue, out var enabled) && enabled;
    }

    private static PullRequestCompletionOptions ResolveCompletionOptions(IServiceProvider services, IGitPlugin plugin)
    {
        var settings = services.GetRequiredService<PluginSettingsService>();
        string? Get(string key)
        {
            var field = plugin.GetSettingGroups().SelectMany(g => g.Fields).FirstOrDefault(f => f.Key == key);
            return field is null ? null : settings.GetValue(plugin, field) ?? field.DefaultValue;
        }

        Enum.TryParse<PullRequestCompletionStrategy>(Get("PullRequestCompletionStrategy"), ignoreCase: true, out var strategy);
        Enum.TryParse<PullRequestMergeMethod>(Get("PullRequestMergeMethod"), ignoreCase: true, out var method);
        bool.TryParse(Get("AllowProtectedBranchBypass"), out var allowBypass);
        return new PullRequestCompletionOptions(
            strategy == default ? PullRequestCompletionStrategy.Merge : strategy,
            method == default ? PullRequestMergeMethod.Squash : method,
            allowBypass);
    }
}
