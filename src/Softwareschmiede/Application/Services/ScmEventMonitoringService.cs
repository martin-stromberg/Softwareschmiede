using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Beobachtet ausschließlich lokal verknüpfte SCM-Referenzen und persistiert vollständige Quellen.</summary>
public sealed class ScmEventMonitoringService(
    IServiceScopeFactory scopes, TimeProvider time, IOptions<ScmEventOptions> options,
    ScmEventStartupGate startup, ILogger<ScmEventMonitoringService> logger)
    : PeriodicBackgroundService(options.Value.PollInterval, time, logger, "SCM-Ereignisüberwachung fehlgeschlagen.")
{
    private readonly TimeProvider _time = time;
    /// <inheritdoc/>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        await startup.WaitAsync(ct);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var tasks = await db.Aufgaben.AsNoTracking().Include(x => x.GitRepository).Include(x => x.IssueReferenz)
            .Include(x => x.PullRequests).Where(x => x.GitRepository != null && x.GitRepository.Aktiv).ToListAsync(ct);
        var plugins = scope.ServiceProvider.GetRequiredService<IPluginManager>().GetSourceCodeManagementPlugins();
        var linked = new List<(Guid TaskId, GitRepository Repository, ScmReferenceKind Kind, int Number, string RepositoryId)>();
        foreach (var task in tasks)
        {
            var repository = task.GitRepository!;
            if (task.IssueReferenz?.IssueNummer is > 0)
                linked.Add((task.Id, repository, ScmReferenceKind.Issue, task.IssueReferenz.IssueNummer.Value, repository.RepositoryUrl));
            foreach (var reference in task.PullRequests)
                linked.Add((task.Id, repository, ScmReferenceKind.PullRequest, reference.PullRequestNumber, reference.RepositoryId));
        }
        foreach (var plugin in plugins)
        {
            if (plugin is not IScmEventSource source)
                continue;
            var resolved = new List<(Guid TaskId, ScmEventReference Reference)>();
            var complete = true;
            // Resolve the entire local reference set before assigning any incoming event.
            // Raw HTTPS/SSH addresses and plugin display names are not repository identities.
            foreach (var aliases in linked.Where(x =>
                string.Equals(plugin.PluginPrefix, x.Repository.PluginTyp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(plugin.PluginName, x.Repository.PluginTyp, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.RepositoryId, StringComparer.OrdinalIgnoreCase))
            {
                using var resolutionScope = scopes.CreateScope();
                var resolutionDb = resolutionScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                var local = aliases.First();
                var resolutionKey = $"repository/{plugin.PluginPrefix.ToLowerInvariant()}/{Uri.EscapeDataString(aliases.Key.ToLowerInvariant())}";
                var checkpoint = await resolutionDb.ScmObservationCheckpoints.FindAsync([resolutionKey], ct);
                if (checkpoint?.NextAttemptUtc > _time.GetUtcNow().ToUnixTimeMilliseconds())
                {
                    complete = false;
                    continue;
                }
                try
                {
                    var repository = await source.GetEventRepositoryAsync(new ScmRepositoryContext(local.Repository.Id, plugin.PluginPrefix, local.RepositoryId), ct);
                    resolved.AddRange(aliases.Select(x => (x.TaskId, new ScmEventReference(repository, x.Kind, x.Number))));
                    if (checkpoint is not null)
                    {
                        resolutionDb.Remove(checkpoint);
                        await resolutionDb.SaveChangesAsync(ct);
                        resolutionScope.ServiceProvider.GetRequiredService<ScmEventChangeNotifier>().Notify();
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    complete = false;
                    await RecordFailureAsync(resolutionKey, ex, ct,
                        " Bis zur vollständigen Repositoryzuordnung werden keine neuen Anfragen dieses Providers eingelesen.");
                }
            }
            // An unresolved alias could name any of the resolved repositories. Do not guess an assignment.
            if (!complete) continue;
            foreach (var group in resolved.GroupBy(x => (Provider: x.Reference.Repository.ProviderPrefix.ToLowerInvariant(),
                Host: x.Reference.Repository.Host.ToLowerInvariant(), x.Reference.Repository.RepositoryId, x.Reference.Kind, x.Reference.Number)))
            {
                var reference = group.First().Reference;
                var kinds = reference.Kind == ScmReferenceKind.Issue
                    ? new[] { ScmContentKind.Description, ScmContentKind.IssueComment }
                    : new[] { ScmContentKind.Description, ScmContentKind.IssueComment, ScmContentKind.ReviewComment, ScmContentKind.PullRequestReview };
                var taskIds = group.Select(x => x.TaskId).Distinct().ToArray();
                foreach (var kind in kinds)
                {
                    using var batchScope = scopes.CreateScope();
                    var batchDb = batchScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                    var sourceKey = string.Join('/', new[] { group.Key.Provider, group.Key.Host, group.Key.RepositoryId,
                        reference.Kind.ToString(), reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), kind.ToString() }.Select(Uri.EscapeDataString));
                    var checkpoint = await batchDb.ScmObservationCheckpoints.FindAsync([sourceKey], ct);
                    var now = _time.GetUtcNow();
                    if (checkpoint?.NextAttemptUtc > now.ToUnixTimeMilliseconds()) continue;
                    try
                    {
                        var request = new ScmEventReadRequest(reference, kind,
                            checkpoint?.WatermarkUtc is long watermark ? DateTimeOffset.FromUnixTimeMilliseconds(watermark) - options.Value.PollInterval * 2 : null, now);
                        if (await batchDb.ScmContentSnapshots.AnyAsync(x => x.ContentKey.StartsWith(sourceKey + "/")
                            && (!x.IdentityResolved || x.Diagnostic == "Mehrdeutige Zuordnung zu Aufgaben"), ct))
                            request = request with { SinceUtc = null };
                        var batch = await source.ReadEventsAsync(request, ct);
                        await batchScope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(taskIds.Length == 1 ? taskIds[0] : null, sourceKey, batch, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        await RecordFailureAsync(sourceKey, ex, ct);
                    }
                }
            }
        }
        var nowMilliseconds = _time.GetUtcNow().ToUnixTimeMilliseconds();
        var upperSequence = await db.ScmIncomingEvents.MaxAsync(x => (long?)x.Sequence, ct) ?? 0;
        long afterSequence = 0;
        while (afterSequence < upperSequence)
        {
            var batch = await db.ScmIncomingEvents.AsNoTracking().Where(x => x.Sequence > afterSequence && x.Sequence <= upperSequence).Where(x =>
                (x.Status != "Verarbeitet" && (x.NextAttemptUtc == null || x.NextAttemptUtc <= nowMilliseconds)) ||
                db.ScmOutgoingActions.Any(action => action.EventId == x.Id && action.Status != "Succeeded"
                    && (action.NextAttemptUtc == null || action.NextAttemptUtc <= nowMilliseconds)))
                .OrderBy(x => x.Sequence).Select(x => new { x.Id, x.Sequence }).Take(100).ToListAsync(ct);
            if (batch.Count == 0) break;
            afterSequence = batch[^1].Sequence;
            foreach (var item in batch)
            {
                using var dispatchScope = scopes.CreateScope();
                await dispatchScope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchAsync(item.Id, ct: ct);
            }
        }
    }

    private async Task RecordFailureAsync(string sourceKey, Exception error, CancellationToken ct, string diagnosticSuffix = "")
    {
        using var failureScope = scopes.CreateScope();
        var failureDb = failureScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = await failureDb.ScmObservationCheckpoints.FindAsync([sourceKey], ct);
        if (checkpoint is null)
        {
            checkpoint = new ScmObservationCheckpoint { SourceKey = sourceKey };
            failureDb.Add(checkpoint);
        }
        checkpoint.Failures++;
        var failure = error as ScmProviderException;
        checkpoint.ErrorCode = failure?.FailureKind.ToString() ?? "Temporary";
        checkpoint.Diagnostic = (failure?.Message ?? "Quelle konnte nicht vollständig gelesen werden.") + diagnosticSuffix;
        checkpoint.NextAttemptUtc = failure?.FailureKind is ScmProviderFailureKind.Authentication or ScmProviderFailureKind.Permission or ScmProviderFailureKind.Permanent
            ? long.MaxValue : ScmEventStore.RetryAt(checkpoint.Failures, _time.GetUtcNow().ToUnixTimeMilliseconds(), failure?.RetryAfterUtc);
        await failureDb.SaveChangesAsync(ct);
        failureScope.ServiceProvider.GetRequiredService<ScmEventChangeNotifier>().Notify();
    }
}
