using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Beobachtet lokal verknüpfte SCM-Referenzen sowie repository-weit alle Referenzen aktiver
/// Projekt-Repositories und persistiert vollständige Quellen.</summary>
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
        var repositories = await db.GitRepositories.AsNoTracking().Where(x => x.Aktiv).ToListAsync(ct);
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
            var resolved = new List<(Guid? TaskId, ScmEventReference Reference)>();
            var repositoriesForEnumeration = new List<ScmEventRepository>();
            var complete = true;
            // Resolve the entire local reference set before assigning any incoming event.
            // Raw HTTPS/SSH addresses and plugin display names are not repository identities.
            var aliasGroups = linked.Where(x => MatchesPlugin(plugin, x.Repository))
                .GroupBy(x => x.RepositoryId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => (Repository: x.First().Repository, Linked: x.ToList()), StringComparer.OrdinalIgnoreCase);
            foreach (var repository in repositories.Where(x => MatchesPlugin(plugin, x)
                && !string.IsNullOrWhiteSpace(x.RepositoryUrl)))
                if (!aliasGroups.ContainsKey(repository.RepositoryUrl))
                    aliasGroups[repository.RepositoryUrl] = (repository, []);
            foreach (var (alias, aliasGroup) in aliasGroups)
            {
                using var resolutionScope = scopes.CreateScope();
                var resolutionDb = resolutionScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                var resolutionKey = $"repository/{plugin.PluginPrefix.ToLowerInvariant()}/{Uri.EscapeDataString(alias.ToLowerInvariant())}";
                var checkpoint = await resolutionDb.ScmObservationCheckpoints.FindAsync([resolutionKey], ct);
                if (checkpoint?.NextAttemptUtc > _time.GetUtcNow().ToUnixTimeMilliseconds())
                {
                    complete = false;
                    continue;
                }
                try
                {
                    var repository = await source.GetEventRepositoryAsync(new ScmRepositoryContext(aliasGroup.Repository.Id, plugin.PluginPrefix, alias), ct);
                    resolved.AddRange(aliasGroup.Linked.Select(x => ((Guid?)x.TaskId, new ScmEventReference(repository, x.Kind, x.Number))));
                    if (checkpoint is not null)
                    {
                        resolutionDb.Remove(checkpoint);
                        await resolutionDb.SaveChangesAsync(ct);
                        resolutionScope.ServiceProvider.GetRequiredService<ScmEventChangeNotifier>().Notify();
                    }
                    repositoriesForEnumeration.Add(repository);
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
            var enumerated = new HashSet<string>(StringComparer.Ordinal);
            foreach (var repository in repositoriesForEnumeration)
            {
                using var enumerationScope = scopes.CreateScope();
                await EnumerateRepositoryAsync(source, repository,
                    enumerationScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>(), enumerated,
                    enumerationScope.ServiceProvider.GetRequiredService<ScmEventChangeNotifier>(), resolved, ct);
            }
            foreach (var group in resolved.GroupBy(x => (Provider: x.Reference.Repository.ProviderPrefix.ToLowerInvariant(),
                Host: x.Reference.Repository.Host.ToLowerInvariant(), x.Reference.Repository.RepositoryId, x.Reference.Kind, x.Reference.Number)))
            {
                var reference = group.First().Reference;
                var kinds = reference.Kind == ScmReferenceKind.Issue
                    ? new[] { ScmContentKind.Description, ScmContentKind.IssueComment }
                    : new[] { ScmContentKind.Description, ScmContentKind.IssueComment, ScmContentKind.ReviewComment, ScmContentKind.PullRequestReview };
                var taskIds = group.Select(x => x.TaskId).OfType<Guid>().Distinct().ToArray();
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
                            && (!x.IdentityResolved || x.Diagnostic == ScmEventStore.AmbiguousAssignmentDiagnostic
                                || x.Diagnostic == ScmEventStore.UnassignedDiagnostic), ct))
                            request = request with { SinceUtc = null };
                        var batch = await source.ReadEventsAsync(request, ct);
                        await batchScope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(
                            taskIds.Length == 1 ? taskIds[0] : null, sourceKey, batch, taskIds.Length == 0, ct);
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

    private static bool MatchesPlugin(IGitPlugin plugin, GitRepository repository)
        => string.Equals(plugin.PluginPrefix, repository.PluginTyp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(plugin.PluginName, repository.PluginTyp, StringComparison.OrdinalIgnoreCase);

    /// <summary>Enumeriert ein kanonisch aufgelöstes Repository einmal pro Lauf und verwaltet dessen
    /// eigenen Watermark-/Backoff-Checkpoint. Enumerationsfehler blockieren die Provider-Runde nicht.</summary>
    private async Task EnumerateRepositoryAsync(IScmEventSource source, ScmEventRepository repository,
        SoftwareschmiededDbContext db, HashSet<string> enumerated, ScmEventChangeNotifier notifier,
        List<(Guid? TaskId, ScmEventReference Reference)> resolved, CancellationToken ct)
    {
        var enumerationKey = string.Join('/', new[] { "enumeration", repository.ProviderPrefix.ToLowerInvariant(),
            repository.Host.ToLowerInvariant(), repository.RepositoryId }.Select(Uri.EscapeDataString));
        if (!enumerated.Add(enumerationKey)) return;
        var checkpoint = await db.ScmObservationCheckpoints.FindAsync([enumerationKey], ct);
        var now = _time.GetUtcNow();
        if (checkpoint?.NextAttemptUtc > now.ToUnixTimeMilliseconds()) return;
        try
        {
            var sinceUtc = checkpoint?.WatermarkUtc is long watermark
                ? DateTimeOffset.FromUnixTimeMilliseconds(watermark) - options.Value.PollInterval * 2
                : (DateTimeOffset?)null;
            var discovered = await source.EnumerateReferencesAsync(repository, sinceUtc, ct);
            resolved.AddRange(discovered.Select(x => ((Guid?)null, x)));
            if (checkpoint is null)
            {
                checkpoint = new ScmObservationCheckpoint { SourceKey = enumerationKey };
                db.Add(checkpoint);
            }
            checkpoint.WatermarkUtc = now.ToUnixTimeMilliseconds();
            checkpoint.Failures = 0;
            checkpoint.NextAttemptUtc = null;
            checkpoint.ErrorCode = null;
            checkpoint.Diagnostic = null;
            await db.SaveChangesAsync(ct);
            notifier.Notify();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            await RecordFailureAsync(enumerationKey, ex, ct);
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
