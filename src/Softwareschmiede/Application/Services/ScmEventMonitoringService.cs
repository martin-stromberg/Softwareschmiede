using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Enums;
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

    /// <summary>Zykluslokaler Zustand einer Provider-Runde; wird explizit an die Quellenhelfer
    /// weitergereicht statt als Seiteneffekt über ein Instanzfeld gesetzt.</summary>
    private sealed class CycleState
    {
        internal bool HadFailure;
    }

    /// <inheritdoc/>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        await startup.WaitAsync(ct);
        using var scope = scopes.CreateScope();
        var einstellungen = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        // Laufzeit-Schalter (Einstellungen → Automatisierung → Aufgaben ausführen): Bei deaktivierter
        // Überwachung wird weder beobachtet/enumeriert noch automatisch zugestellt. Bereits in
        // der Inbox vorhandene Ereignisse und alle Checkpoints bleiben unverändert bestehen.
        if (!await einstellungen.GetScmMonitoringEnabledAsync(ct))
        {
            logger.LogDebug("SCM-Ereignisüberwachung ist deaktiviert; Durchlauf wird übersprungen.");
            return;
        }
        var triggerKeyword = await einstellungen.GetScmTriggerKeywordAsync(ct);
        var kontoModus = await einstellungen.GetScmKontoModusAsync(ct);
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var tasks = await db.Aufgaben.AsNoTracking().Include(x => x.GitRepository).Include(x => x.IssueReferenz)
            .Include(x => x.PullRequests).Where(x => x.GitRepository != null && x.GitRepository.Aktiv).ToListAsync(ct);
        var repositories = await db.GitRepositories.AsNoTracking().Where(x => x.Aktiv).ToListAsync(ct);
        var plugins = scope.ServiceProvider.GetRequiredService<IPluginManager>().GetSourceCodeManagementPlugins();
        var pendingInboxCommits = new List<(string Key, DateTimeOffset Now, string Diagnostic)>();
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
            var localRepositories = new Dictionary<ScmEventRepository, List<GitRepository>>();
            var complete = true;
            var cycle = new CycleState();
            // Im SeparatesKonto ordnet die Inbox über Repository-Node-IDs zu, es wird nichts geraten.
            // Ein für das Konto unsichtbares Repository (404/403) liefert ihm auch keine Notifications
            // und wird daher nur ausgelassen, statt die Überwachung aller Repositories anzuhalten.
            var inboxMode = kontoModus == ScmKontoModus.SeparatesKonto && plugin is IScmNotificationSource;
            // Resolve the entire local reference set before assigning any incoming event.
            // Raw HTTPS/SSH addresses and plugin display names are not repository identities.
            var aliasGroups = linked.Where(x => ScmPluginMatcher.MatchesPlugin(plugin, x.Repository))
                .GroupBy(x => x.RepositoryId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => (Repository: x.First().Repository, Linked: x.ToList()), StringComparer.OrdinalIgnoreCase);
            foreach (var repository in repositories.Where(x => ScmPluginMatcher.MatchesPlugin(plugin, x)
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
                    if (!(inboxMode && IsInaccessible(checkpoint.ErrorCode)))
                        complete = false;
                    continue;
                }
                try
                {
                    var repository = await source.GetEventRepositoryAsync(new ScmRepositoryContext(aliasGroup.Repository.Id, plugin.PluginPrefix, alias, kontoModus), ct);
                    resolved.AddRange(aliasGroup.Linked.Select(x => ((Guid?)x.TaskId, new ScmEventReference(repository, x.Kind, x.Number))));
                    if (!localRepositories.TryGetValue(repository, out var candidates))
                        localRepositories[repository] = candidates = [];
                    if (candidates.All(x => x.Id != aliasGroup.Repository.Id))
                        candidates.Add(aliasGroup.Repository);
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
                    var isolated = inboxMode && ex is ScmProviderException failure && IsInaccessible(failure.FailureKind.ToString());
                    if (!isolated)
                        complete = false;
                    // Repository-Auflösungen werden auch bei „Permanent“ (z. B. 404 nach entzogenem oder
                    // noch fehlendem Zugriff) per Backoff erneut versucht, statt dauerhaft gesperrt zu bleiben.
                    if (!isolated)
                        cycle.HadFailure = true;
                    await ScmCheckpointSupport.RecordFailureAsync(scopes, _time, resolutionKey, ex, ct, isolated
                            ? " Das Repository ist für das separate Konto nicht erreichbar (z. B. privates Repository ohne Zugriff oder fehlende Token-Berechtigung)."
                              + " Es wird ausgelassen; die übrigen Repositories werden weiter überwacht."
                            : " Bis zur vollständigen Repositoryzuordnung werden keine neuen Anfragen dieses Providers eingelesen.",
                        retryPermanent: true);
                }
            }
            // An unresolved alias could name any of the resolved repositories. Do not guess an assignment.
            if (!complete) continue;
            if (kontoModus == ScmKontoModus.SeparatesKonto && plugin is IScmNotificationSource notificationSource)
            {
                // Im SeparatesKonto ersetzt die Inbox des separaten Kontos die repository-weite
                // Enumeration; verknüpfte Referenzen aus Aufgaben werden nicht als Quellen übernommen.
                resolved.Clear();
                foreach (var host in repositoriesForEnumeration.Select(x => x.Host).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    using var inboxScope = scopes.CreateScope();
                    await EnumerateNotificationsAsync(notificationSource, plugin, host,
                        inboxScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>(),
                        resolved, repositoriesForEnumeration, pendingInboxCommits, cycle, ct);
                }
            }
            else
            {
                var enumerated = new HashSet<string>(StringComparer.Ordinal);
                foreach (var repository in repositoriesForEnumeration)
                {
                    if (!await ScmCheckpointSupport.EnumerateRepositoryAsync(
                            scopes, _time, options.Value.PollInterval, source, repository,
                            "enumeration", enumerated,
                            (discovered, _) =>
                            {
                                resolved.AddRange(discovered.Select(x => ((Guid?)null, x)));
                                return Task.CompletedTask;
                            }, ct))
                        cycle.HadFailure = true;
                }
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
                        var aufgabeId = taskIds.Length == 1 ? taskIds[0] : (Guid?)null;
                        var unassigned = taskIds.Length == 0;
                        if (unassigned && localRepositories.TryGetValue(reference.Repository, out var candidates)
                            && batch.Observations.FirstOrDefault(x => ScmEventValidationService.GetRejection(x, triggerKeyword) is null) is { } trigger)
                        {
                            var resolution = await batchScope.ServiceProvider.GetRequiredService<ScmEventTaskResolver>()
                                .ResolveAsync(reference, source, trigger, candidates, ct);
                            aufgabeId = resolution.AufgabeId;
                            unassigned = resolution.Kind == ScmTaskResolutionKind.Unassigned;
                        }
                        await batchScope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(
                            aufgabeId, sourceKey, batch, unassigned, triggerKeyword, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        cycle.HadFailure = true;
                        await ScmCheckpointSupport.RecordFailureAsync(scopes, _time, sourceKey, ex, ct);
                    }
                }
            }
            // Der Inbox-Watermark wird erst committet, wenn Inbox und alle abgeleiteten Quellen
            // des Zyklus fehlerfrei verarbeitet wurden; sonst bleibt er stehen (idempotent).
            if (!cycle.HadFailure)
                foreach (var (commitKey, commitNow, diagnostic) in pendingInboxCommits)
                {
                    using var commitScope = scopes.CreateScope();
                    var commitDb = commitScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                    var commitCheckpoint = await commitDb.ScmObservationCheckpoints.FindAsync([commitKey], ct);
                    if (commitCheckpoint is null)
                    {
                        commitCheckpoint = new ScmObservationCheckpoint { SourceKey = commitKey };
                        commitDb.Add(commitCheckpoint);
                    }
                    commitCheckpoint.WatermarkUtc = commitNow.ToUnixTimeMilliseconds();
                    commitCheckpoint.Failures = 0;
                    commitCheckpoint.NextAttemptUtc = null;
                    commitCheckpoint.ErrorCode = null;
                    commitCheckpoint.Diagnostic = diagnostic;
                    await commitDb.SaveChangesAsync(ct);
                    scope.ServiceProvider.GetRequiredService<ScmEventChangeNotifier>().Notify();
                }
            pendingInboxCommits.Clear();
        }
        var nowMilliseconds = _time.GetUtcNow().ToUnixTimeMilliseconds();
        var upperSequence = await db.ScmIncomingEvents.MaxAsync(x => (long?)x.Sequence, ct) ?? 0;
        long afterSequence = 0;
        while (afterSequence < upperSequence)
        {
            // Abgebrochene Ereignisse sind endgültig: auch noch ausstehende Hostaktionen werden nicht mehr gesendet.
            var batch = await db.ScmIncomingEvents.AsNoTracking().Where(x => x.Sequence > afterSequence && x.Sequence <= upperSequence).Where(x =>
                x.Status != "Abgebrochen" &&
                ((x.Status != "Verarbeitet" && (x.NextAttemptUtc == null || x.NextAttemptUtc <= nowMilliseconds)) ||
                db.ScmOutgoingActions.Any(action => action.EventId == x.Id && action.Status != "Succeeded"
                    && (action.NextAttemptUtc == null || action.NextAttemptUtc <= nowMilliseconds))))
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

    /// <summary>Liest die Notifications-Inbox eines Hosts und leitet Referenzen auf registrierte
    /// Repositories ab. Der Watermark wird erst nach vollständiger Verarbeitung des Zyklus committet;
    /// verworfene Fremd-Notifications werden als nicht-alarmierende Diagnose sichtbar.</summary>
    private async Task EnumerateNotificationsAsync(IScmNotificationSource source, IGitPlugin plugin, string host,
        SoftwareschmiededDbContext db,
        List<(Guid? TaskId, ScmEventReference Reference)> resolved, IReadOnlyList<ScmEventRepository> repositories,
        List<(string Key, DateTimeOffset Now, string Diagnostic)> pendingInboxCommits,
        CycleState cycle, CancellationToken ct)
    {
        var inboxKey = string.Join('/', new[] { "notifications", plugin.PluginPrefix.ToLowerInvariant(), host.ToLowerInvariant() }.Select(Uri.EscapeDataString));
        var checkpoint = await db.ScmObservationCheckpoints.FindAsync([inboxKey], ct);
        var now = _time.GetUtcNow();
        if (checkpoint?.NextAttemptUtc > now.ToUnixTimeMilliseconds()) return;
        try
        {
            var sinceUtc = checkpoint?.WatermarkUtc is long watermark
                ? DateTimeOffset.FromUnixTimeMilliseconds(watermark) - options.Value.PollInterval * 2
                : (DateTimeOffset?)null;
            var notifications = await source.EnumerateNotificationsAsync(host, sinceUtc, ct);
            // Node-IDs sind instanzlokal: eine Notification darf nur Repositories desselben Hosts
            // matchen — sonst zeigt die abgeleitete Referenz auf ein fremdes Repository.
            var byRepository = repositories
                .Where(x => x.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.RepositoryId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var discarded = 0;
            var mentions = notifications.Where(x => x.Reason == ScmNotification.MentionReason).ToList();
            foreach (var notification in mentions)
            {
                if (!byRepository.TryGetValue(notification.RepositoryNodeId, out var repository))
                {
                    discarded++;
                    continue;
                }
                if (seen.Add($"{notification.RepositoryNodeId}/{notification.Kind}/{notification.Number}"))
                    resolved.Add((null, new ScmEventReference(repository, notification.Kind, notification.Number)));
            }
            // Der Watermark darf erst nach dem Verarbeiten aller abgeleiteten Quellen stehen;
            // das Commit erfolgt daher erst am Ende des Plugin-Durchlaufs (siehe RunOnceAsync).
            pendingInboxCommits.Add((inboxKey, now,
                $"{ScmEventStore.InboxDiscardDiagnosticPrefix} {mentions.Count - discarded} verarbeitet, {discarded} verworfen (Repository nicht registriert)."));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            cycle.HadFailure = true;
            await ScmCheckpointSupport.RecordFailureAsync(scopes, _time, inboxKey, ex, ct);
        }
    }

    /// <summary>Kennzeichnet Fehlerarten, bei denen das Repository für das Konto schlicht nicht
    /// sichtbar ist (GitHub meldet fehlenden Zugriff auf private Repositories als 404).</summary>
    /// <param name="errorCode">Der gespeicherte bzw. aktuelle Fehlercode (Name von <see cref="ScmProviderFailureKind"/>).</param>
    /// <returns><see langword="true"/>, wenn das Repository für das Konto nicht erreichbar ist.</returns>
    private static bool IsInaccessible(string? errorCode)
        => errorCode is nameof(ScmProviderFailureKind.Permanent) or nameof(ScmProviderFailureKind.Permission);
}
