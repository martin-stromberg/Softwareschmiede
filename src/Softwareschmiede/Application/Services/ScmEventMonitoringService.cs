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

    /// <summary>Eine über eine lokale Aufgabe verknüpfte SCM-Referenz samt auflösendem
    /// Projekt-Repository und Zweck-Markierung (<see cref="WorkItem.Purpose"/>) der Aufgabe.</summary>
    private sealed record LinkedReference(Guid TaskId, GitRepository Repository,
        ScmReferenceKind Kind, int Number, string RepositoryId, string? Purpose);

    /// <summary>Eine für den aktuellen Provider-Durchlauf aufgelöste SCM-Referenz: entweder
    /// über eine Aufgabe verknüpft (TaskId und Zweck-Markierung gesetzt) oder repository-weit
    /// entdeckt bzw. aus der Inbox abgeleitet (TaskId und Purpose = <see langword="null"/>;
    /// Notification trägt den Inbox-Eintrag samt auslösendem Reason).</summary>
    private sealed record ResolvedReference(Guid? TaskId, ScmEventReference Reference, string? Purpose,
        ScmNotification? Notification = null);

    /// <inheritdoc/>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        await startup.WaitAsync(ct);
        using var scope = scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        // Laufzeit-Schalter (Einstellungen → Automatisierung → Aufgaben ausführen): Bei deaktivierter
        // Überwachung wird weder beobachtet/enumeriert noch automatisch zugestellt. Bereits in
        // der Inbox vorhandene Ereignisse und alle Checkpoints bleiben unverändert bestehen.
        if (!await settings.GetScmMonitoringEnabledAsync(ct))
        {
            logger.LogDebug("SCM-Ereignisüberwachung ist deaktiviert; Durchlauf wird übersprungen.");
            return;
        }
        var triggerKeyword = await settings.GetScmTriggerKeywordAsync(ct);
        var delayKeyword = await settings.GetScmDelayKeywordAsync(ct);
        var accountMode = await settings.GetScmAccountModeAsync(ct);
        var issuePreparationEnabled = await settings.GetScmIssuePreparationEnabledAsync(ct);
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        // Zweck-markierte Aufbereitungs-Aufgaben ziehen die Issue-Ereignisse ihrer eigenen
        // Issue-Nummer auf sich (Folgekommentare laufen in derselben Aufgabe/Sitzung); sie
        // liefern weiterhin keine PR-Referenzen und keinen PR-Kontext (Isolationsentscheidung).
        var tasks = await db.WorkItems.AsNoTracking().Include(x => x.GitRepository).Include(x => x.IssueReference)
            .Include(x => x.PullRequests).Where(x => x.GitRepository != null && x.GitRepository.Active
                && (x.Purpose == null || x.Purpose == ScmIssuePreparationService.Purpose)).ToListAsync(ct);
        var repositories = await db.GitRepositories.AsNoTracking().Where(x => x.Active).ToListAsync(ct);
        var plugins = scope.ServiceProvider.GetRequiredService<IPluginManager>().GetSourceCodeManagementPlugins();
        var pendingInboxCommits = new List<(string Key, DateTimeOffset Now, string Diagnostic)>();
        var linked = new List<LinkedReference>();
        foreach (var task in tasks)
        {
            var repository = task.GitRepository!;
            if (task.IssueReference?.IssueNumber is > 0)
                linked.Add(new(task.Id, repository, ScmReferenceKind.Issue, task.IssueReference.IssueNumber.Value, repository.RepositoryUrl, task.Purpose));
            if (task.Purpose is null)
                foreach (var reference in task.PullRequests)
                    linked.Add(new(task.Id, repository, ScmReferenceKind.PullRequest, reference.PullRequestNumber, reference.RepositoryId, task.Purpose));
        }
        foreach (var plugin in plugins)
        {
            if (plugin is not IScmEventSource source)
                continue;
            var resolved = new List<ResolvedReference>();
            var repositoriesForEnumeration = new List<ScmEventRepository>();
            var localRepositories = new Dictionary<ScmEventRepository, List<GitRepository>>();
            var complete = true;
            var cycle = new CycleState();
            // Im SeparatesKonto ordnet die Inbox über Repository-Node-IDs zu, es wird nichts geraten.
            // Ein für das Konto unsichtbares Repository (404/403) liefert ihm auch keine Notifications
            // und wird daher nur ausgelassen, statt die Überwachung aller Repositories anzuhalten.
            var inboxMode = accountMode == ScmAccountMode.SeparateAccount && plugin is IScmNotificationSource;
            // Die gesamte lokale Referenzmenge auflösen, bevor irgendein eingehendes Ereignis zugeordnet
            // wird. Rohe HTTPS/SSH-Adressen und Plugin-Anzeigenamen sind keine Repository-Identitäten.
            // Die Gruppierung läuft über den kanonischen Alias-Schlüssel der Quelle: HTTPS-/SSH-URL,
            // owner/name- und .git-Formen desselben Remote laufen auf eine Gruppe, einen Checkpoint
            // und eine Auflösung zusammen; der Gruppenwert sammelt alle beteiligten lokalen
            // Repositories und verknüpften Referenzen.
            var aliasGroups = BuildAliasGroups(plugin, source, linked, repositories);
            // Verwaiste Auflösungs-Checkpoints (Alt-Alias-Keys, entfernte Repositories) entfernen,
            // bevor die aktuellen Gruppen aufgelöst werden.
            await ScmCheckpointSupport.PruneResolutionCheckpointsAsync(scopes,
                $"repository/{plugin.PluginPrefix.ToLowerInvariant()}/", aliasGroups.Keys, ct);
            foreach (var (alias, aliasGroup) in aliasGroups)
            {
                using var resolutionScope = scopes.CreateScope();
                var resolutionDb = resolutionScope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
                var resolutionKey = ScmCheckpointSupport.ResolutionKey(
                    $"repository/{plugin.PluginPrefix.ToLowerInvariant()}/", alias);
                var checkpoint = await resolutionDb.ScmObservationCheckpoints.FindAsync([resolutionKey], ct);
                if (checkpoint?.NextAttemptUtc > _time.GetUtcNow().ToUnixTimeMilliseconds())
                {
                    if (!(inboxMode && IsInaccessible(checkpoint.ErrorCode)))
                        complete = false;
                    continue;
                }
                try
                {
                    var repository = await source.GetEventRepositoryAsync(new ScmRepositoryContext(aliasGroup.Repositories[0].Id, plugin.PluginPrefix, alias, accountMode), ct);
                    resolved.AddRange(aliasGroup.Linked.Select(x => new ResolvedReference(x.TaskId, new ScmEventReference(repository, x.Kind, x.Number), x.Purpose)));
                    if (!localRepositories.TryGetValue(repository, out var candidates))
                        localRepositories[repository] = candidates = [];
                    foreach (var local in aliasGroup.Repositories)
                        if (candidates.All(x => x.Id != local.Id))
                            candidates.Add(local);
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
            // Ein unaufgelöster Alias könnte jedes der aufgelösten Repositories meinen. Keine Zuordnung raten.
            if (!complete) continue;
            if (accountMode == ScmAccountMode.SeparateAccount && plugin is IScmNotificationSource notificationSource)
            {
                // Im SeparatesKonto ersetzt die Inbox des separaten Kontos die repository-weite
                // Enumeration; verknüpfte Referenzen aus Aufgaben werden nicht als Quellen übernommen.
                resolved.Clear();
                foreach (var host in repositoriesForEnumeration.Select(x => x.Host).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    using var inboxScope = scopes.CreateScope();
                    await EnumerateNotificationsAsync(notificationSource, plugin, host,
                        inboxScope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>(),
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
                                resolved.AddRange(discovered.Select(x => new ResolvedReference(null, x, null)));
                                return Task.CompletedTask;
                            }, ct))
                        cycle.HadFailure = true;
                }
            }
            foreach (var group in resolved.GroupBy(x => (Provider: x.Reference.Repository.ProviderPrefix.ToLowerInvariant(),
                Host: x.Reference.Repository.Host.ToLowerInvariant(), x.Reference.Repository.RepositoryId, x.Reference.Kind, x.Reference.Number)))
            {
                var reference = group.First().Reference;
                // Eine assign-abgeleitete Referenz validiert ihren Description-Batch über den
                // Zuweisender-Kreis statt über den Texttrigger (siehe GetAssignRejection).
                var assignNotification = group.Any(x => x.Notification?.Reason == ScmNotification.AssignReason);
                var kinds = reference.Kind == ScmReferenceKind.Issue
                    ? new[] { ScmContentKind.Description, ScmContentKind.IssueComment }
                    : new[] { ScmContentKind.Description, ScmContentKind.IssueComment, ScmContentKind.ReviewComment, ScmContentKind.PullRequestReview };
                // Verknüpfte Kandidaten der Referenzgruppe samt Zweck-Markierung; dieselbe
                // Aufgabe kann über mehrere verknüpfte Referenzen in der Gruppe stehen.
                var linkedCandidates = group.Where(x => x.TaskId is not null)
                    .Select(x => (Id: x.TaskId!.Value, x.Purpose)).Distinct().ToList();
                var regularIds = linkedCandidates.Where(x => x.Purpose is null).Select(x => x.Id).ToList();
                // Purpose-Token-Vergleich wie im gespiegelten Resolver und in der
                // Bestandssuche: künftige Fremdzwecke zählen nicht als Aufbereitungskandidat.
                var purposeIds = linkedCandidates.Where(x => x.Purpose == ScmIssuePreparationService.Purpose).Select(x => x.Id).ToList();
                foreach (var kind in kinds)
                {
                    using var batchScope = scopes.CreateScope();
                    var batchDb = batchScope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
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
                        if (assignNotification && kind == ScmContentKind.Description)
                        {
                            // Zuweisender und Ereignisidentität der jüngsten wirksamen
                            // Viewer-Zuweisung; ohne Plugin-Fähigkeit bleibt die Zuweisung
                            // unbestimmt und wird weiter unten abgelehnt. Die Dekoration trägt
                            // die Zuweisungsidentität in den ContentKey — eine erneute
                            // Zuweisung bei unverändertem Text erzeugt ein neues Ereignis.
                            // Der Plugin-Vorfilter entfernt Beschreibungen nicht mehr — eine
                            // eigene, marker-tragende Beschreibung kommt hier als Beobachtung
                            // an und wird ausschließlich über den Zuweisender-Kreis geprüft.
                            var assignment = source is IScmAssignmentSource assignments
                                ? await assignments.GetLatestViewerAssignmentAsync(reference, ct)
                                : null;
                            batch = batch with
                            {
                                Observations = batch.Observations.Select(x => x with
                                {
                                    RemoteNodeId = x.RemoteNodeId + "/assign-" + (assignment?.EventNodeId ?? "unbestimmt"),
                                    Trigger = new ScmEventTrigger(ScmNotification.AssignReason, assignment?.AssignerId,
                                    assignment?.AssignerLogin, assignment?.EventNodeId, assignment?.OccurredAt)
                                }).ToArray()
                            };
                        }
                        // Zweistufige Auswahl wie im Resolver: genau eine reguläre Aufgabe
                        // gewinnt (auch gegenüber einem Altbestand-Paar aus regulärer und
                        // zweck-markierter Aufgabe); ohne regulären Treffer tritt genau eine
                        // zweck-markierte Aufgabe ein. Mehrfachtreffer je Stufe bleiben
                        // mehrdeutig (AmbiguousAssignmentDiagnostic, kein Resolver-Aufruf).
                        var workItemId = regularIds.Count == 1 ? regularIds[0]
                            : regularIds.Count == 0 && purposeIds.Count == 1 ? purposeIds[0]
                            : (Guid?)null;
                        var unassigned = regularIds.Count == 0 && purposeIds.Count == 0;
                        if (unassigned && localRepositories.TryGetValue(reference.Repository, out var candidates)
                            && batch.Observations.FirstOrDefault(x => ScmEventValidationService.GetRejection(x, triggerKeyword) is null) is { } trigger)
                        {
                            var resolution = await batchScope.ServiceProvider.GetRequiredService<ScmEventTaskResolver>()
                                .ResolveAsync(reference, source, trigger, candidates, ct);
                            workItemId = resolution.WorkItemId;
                            unassigned = resolution.Kind == ScmTaskResolutionKind.Unassigned;
                        }
                        // „Issue erstellt"-Erkennung läuft vor der Inbox-Aufnahme; der
                        // Auswertungs-Checkpoint je Issue dedupliziert Erst- und
                        // Folgebeobachtungen. Nur der repository-weite Enumerationskanal
                        // (kein Inbox-Modus) meldet neue Issues.
                        if (!inboxMode && issuePreparationEnabled && reference.Kind == ScmReferenceKind.Issue
                            && kind == ScmContentKind.Description && batch.Observations.Count > 0
                            && localRepositories.TryGetValue(reference.Repository, out var preparationRepositories))
                        {
                            await batchScope.ServiceProvider.GetRequiredService<ScmIssuePreparationService>()
                                .DetectAndEnqueueAsync(source, reference, batch, preparationRepositories, ct);
                        }
                        await batchScope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(
                            workItemId, sourceKey, batch, unassigned, triggerKeyword, delayKeyword,
                            assignNotification && kind == ScmContentKind.Description
                                ? x => ScmEventValidationService.GetRejection(x, triggerKeyword)
                                : null,
                            ct);
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
                    var commitDb = commitScope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
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
        // Die Startprüfung liegt im Orchestrator: Der Poll-Zyklus und der ereignisgesteuerte
        // Auslöser (ScmStartPruefungsService bei freiwerdenden Plätzen oder gesetzten Pausen)
        // führen exakt dieselbe priorisierte Auswahl fälliger Ereignisse aus; der Poll bleibt
        // als langsamere Sicherheitsnetz-Wiederholung bestehen.
        await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchDueEventsAsync(ct);
    }

    /// <summary>Zweistufige Alias-Gruppenbildung eines Provider-Durchlaufs: verknüpfte Referenzen
    /// und aktive Projekt-Repositories desselben Plugins laufen je kanonischem Alias-Schlüssel der
    /// Quelle (<see cref="ScmRepositoryAliasKey.CanonicalOrRaw"/>) in eine Gruppe zusammen. Der
    /// Gruppenwert sammelt dedupliziert alle beteiligten lokalen Repositories sowie die
    /// verknüpften Referenzen.</summary>
    /// <param name="plugin">Das Plugin der aktuellen Runde (Zuordnung über
    /// <see cref="ScmPluginMatcher.MatchesPlugin"/>).</param>
    /// <param name="source">Die Ereignisquelle des Plugins (Alias-Normalisierung).</param>
    /// <param name="linked">Alle verknüpften Referenzen des Laufes.</param>
    /// <param name="repositories">Alle aktiven Projekt-Repositories.</param>
    private Dictionary<string, (List<GitRepository> Repositories, List<LinkedReference> Linked)>
        BuildAliasGroups(IGitPlugin plugin, IScmEventSource source,
            List<LinkedReference> linked, List<GitRepository> repositories)
    {
        var aliasGroups = new Dictionary<string, (List<GitRepository> Repositories,
            List<LinkedReference> Linked)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in linked.Where(x => ScmPluginMatcher.MatchesPlugin(plugin, x.Repository)))
        {
            var key = ScmRepositoryAliasKey.CanonicalOrRaw(source, entry.RepositoryId, logger);
            if (!aliasGroups.TryGetValue(key, out var group))
                aliasGroups[key] = group = ([], []);
            group.Linked.Add(entry);
            if (group.Repositories.All(x => x.Id != entry.Repository.Id))
                group.Repositories.Add(entry.Repository);
        }
        foreach (var repository in repositories.Where(x => ScmPluginMatcher.MatchesPlugin(plugin, x)
            && !string.IsNullOrWhiteSpace(x.RepositoryUrl)))
        {
            var key = ScmRepositoryAliasKey.CanonicalOrRaw(source, repository.RepositoryUrl, logger);
            if (!aliasGroups.TryGetValue(key, out var group))
                aliasGroups[key] = group = ([], []);
            if (group.Repositories.All(x => x.Id != repository.Id))
                group.Repositories.Add(repository);
        }
        return aliasGroups;
    }

    /// <summary>Liest die Notifications-Inbox eines Hosts und leitet Referenzen auf registrierte
    /// Repositories ab. Der Watermark wird erst nach vollständiger Verarbeitung des Zyklus committet;
    /// verworfene Fremd-Notifications werden als nicht-alarmierende Diagnose sichtbar.</summary>
    private async Task EnumerateNotificationsAsync(IScmNotificationSource source, IGitPlugin plugin, string host,
        SoftwareschmiedeDbContext db,
        List<ResolvedReference> resolved, IReadOnlyList<ScmEventRepository> repositories,
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
            // Erwähnung und Assignee-Zuweisung lösen die Beobachtung aus; bei gleichzeitigem
            // Vorliegen gewinnt die Zuweisung als stärkeres Delegationssignal die Deduplizierung.
            var relevant = notifications
                .Where(x => x.Reason is ScmNotification.MentionReason or ScmNotification.AssignReason)
                .OrderByDescending(x => x.Reason == ScmNotification.AssignReason).ToList();
            foreach (var notification in relevant)
            {
                if (!byRepository.TryGetValue(notification.RepositoryNodeId, out var repository))
                {
                    discarded++;
                    continue;
                }
                if (seen.Add($"{notification.RepositoryNodeId}/{notification.Kind}/{notification.Number}"))
                    resolved.Add(new(null, new ScmEventReference(repository, notification.Kind, notification.Number), null, notification));
            }
            // Der Watermark darf erst nach dem Verarbeiten aller abgeleiteten Quellen stehen;
            // das Commit erfolgt daher erst am Ende des Plugin-Durchlaufs (siehe RunOnceAsync).
            pendingInboxCommits.Add((inboxKey, now,
                $"{ScmEventStore.InboxDiscardDiagnosticPrefix} {relevant.Count - discarded} verarbeitet, {discarded} verworfen (Repository nicht registriert)."));
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
