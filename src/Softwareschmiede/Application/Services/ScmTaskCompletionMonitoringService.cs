using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Abschlussüberwachung von Aufgaben mit SCM-Referenz: Prüft zyklisch — im selben Intervall-Stil wie
/// <see cref="ScmEventMonitoringService"/> — den Remote-Zustand der verknüpften SCM-Referenzen der
/// durch den Prüfmodus (<see cref="ScmTaskCompletionCheckMode"/>) bestimmten Aufgaben.
/// Ereignisgesteuert werden nur Aufgaben mit mindestens einem Inbox-Ereignis beobachtet, im Modus
/// „AlleAufgaben“ alle noch nicht beendeten Aufgaben; Voraussetzung ist stets eine SCM-Referenz
/// (Issue oder Pull Request). Ist die verknüpfte Referenz geschlossen/gemergt (alle verknüpften
/// Pull Requests; ohne Pull-Request-Verknüpfung das verknüpfte Issue), startet die persistierte
/// Wartezeit (<see cref="WorkItem.ScmCompletionDetectedUtc"/>); nach deren Ablauf wird die laufende
/// KI-Ausführung beendet und die Aufgabe auf den Endstatus <see cref="WorkItemStatus.Completed"/>
/// gesetzt. Aufgaben ohne jede SCM-Referenz bleiben unberührt.
/// </summary>
public sealed class ScmTaskCompletionMonitoringService(
    IServiceScopeFactory scopes, TimeProvider time, IOptions<ScmEventOptions> options,
    ScmEventStartupGate startup, ILogger<ScmTaskCompletionMonitoringService> logger)
    : PeriodicBackgroundService(options.Value.PollInterval, time, logger, "SCM-Abschlussüberwachung fehlgeschlagen.")
{
    private readonly TimeProvider _time = time;

    // Begrenztes Wiederholungsbudget je Aufgabe und Fehlersignatur (#433): Transiente Fehler
    // bekommen exponentielles Backoff statt festem Poll-Takt; nach Erschöpfung wird die
    // automatische Prüfung/Ausführung der Aufgabe einmalig laut ausgesetzt (Aufgabenprotokoll
    // + Anwendungslog) statt stumm minütlich weiterzulaufen. In-memory: Ein App-Neustart
    // ist der dokumentierte Wiederanstoß; aufgabenfremde Einträge werden je Durchlauf
    // abgeräumt.
    private static readonly TimeSpan RetryInitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RetryMaxDelay = TimeSpan.FromMinutes(60);
    private const int RetryMaxAttempts = 10;
    private readonly ConcurrentDictionary<Guid, CompletionRetryState> _retries = new();

    private sealed class CompletionRetryState
    {
        public string? Signature;
        public string? LastDetail;
        public int Failures;
        public DateTimeOffset NextAttemptUtc = DateTimeOffset.MinValue;
        public bool Exhausted;
    }
    /// <inheritdoc/>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        await startup.WaitAsync(ct);
        using var scope = scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        // Laufzeit-Schalter (Einstellungen → Automatisierung → Aufgaben beenden): Bei deaktivierter
        // Abschlussüberwachung wird weder beobachtet noch automatisch abgeschlossen; ein bereits
        // gesetzter Erkennungszeitpunkt bleibt persistiert bestehen.
        if (!await settings.GetScmTaskCompletionEnabledAsync(ct))
        {
            logger.LogDebug("SCM-Abschlussüberwachung ist deaktiviert; Durchlauf wird übersprungen.");
            return;
        }
        var wait = TimeSpan.FromMinutes(await settings.GetScmTaskCompletionWaitMinutesAsync(ct));
        var checkMode = await settings.GetScmTaskCompletionCheckModeAsync(ct);
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        // Ereignisgesteuert = mindestens ein eingegangenes SCM-Ereignis; abgeschlossene Ereignisse
        // entscheiden die Referenzbeobachtung nicht vorzeitig — erst nach deren Abschluss.
        // Kandidaten brauchen immer eine SCM-Referenz (Issue oder Pull Request); Aufgaben ohne
        // Referenz werden nie beobachtet.
        var candidates = checkMode == ScmTaskCompletionCheckMode.AllWorkItems
            ? await db.WorkItems
                .Where(a => a.Status != WorkItemStatus.Completed && a.Status != WorkItemStatus.Archived
                    && (a.IssueReference != null || a.PullRequests.Any()))
                .Select(a => a.Id)
                .ToListAsync(ct)
            : await db.WorkItems
                .Where(a => a.Status != WorkItemStatus.Completed && a.Status != WorkItemStatus.Archived
                    && db.ScmIncomingEvents.Any(e => e.WorkItemId == a.Id)
                    && (a.IssueReference != null || a.PullRequests.Any()))
                .Select(a => a.Id)
                .ToListAsync(ct);
        foreach (var taskId in candidates)
        {
            try
            {
                await CheckTaskAsync(taskId, wait, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "SCM-Abschlussprüfung für Aufgabe {AufgabeId} fehlgeschlagen.", taskId);
            }
        }
        // Budget-Einträge von Aufgaben abräumen, die die Kandidatenmenge verlassen haben
        // (manuell abgeschlossen, archiviert oder durch den Prüfmodus ausgeschlossen).
        var active = candidates.ToHashSet();
        foreach (var key in _retries.Keys)
            if (!active.Contains(key)) _retries.TryRemove(key, out _);
    }

    private async Task CheckTaskAsync(Guid taskId, TimeSpan wait, CancellationToken ct)
    {
        // Budget-Gate zuerst: ausgesetzte oder im Backoff ruhende Aufgaben werden ohne
        // jeglichen Provider-/DB-Zugriff übersprungen.
        var state = _retries.GetOrAdd(taskId, _ => new CompletionRetryState());
        if (state.Exhausted || _time.GetUtcNow() < state.NextAttemptUtc)
            return;
        var failuresBefore = state.Failures;
        try
        {
            await CheckTaskCoreAsync(taskId, wait, state, ct);
            // Ein Durchlauf ohne neuen Fehlschlag heilt das Budget der Aufgabe.
            if (state.Failures == failuresBefore) _retries.TryRemove(taskId, out _);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            await RecordFailureAsync(taskId, state, ex, null, ct);
        }
    }

    private async Task CheckTaskCoreAsync(Guid taskId, TimeSpan wait, CompletionRetryState state, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<SoftwareschmiedeDbContext>();
        // Nur transient offene Ereignisse stellen die Referenzbeobachtung zurück; final oder
        // terminal ungeklärte (freigegebene) Ereignisse dürfen den Abschluss nicht länger aufhalten.
        if (await db.ScmIncomingEvents.AnyAsync(
                x => x.WorkItemId == taskId && !ScmIncomingEventStatus.NonBlocking.Contains(x.Status), ct))
            return;
        var task = await db.WorkItems
            .Include(a => a.GitRepository)
            .Include(a => a.IssueReference)
            .Include(a => a.PullRequests)
            .SingleOrDefaultAsync(a => a.Id == taskId, ct);
        if (task is null || task.Status is WorkItemStatus.Completed or WorkItemStatus.Archived)
            return;

        var now = _time.GetUtcNow();
        var log = services.GetRequiredService<LogService>();
        var closedLabel = await DetermineClosureAsync(services, db, task, ct);
        if (closedLabel is null)
        {
            // Die Referenz wurde wieder geöffnet (oder ist noch offen): Der Countdown wird zurückgesetzt.
            if (task.ScmCompletionDetectedUtc is not null)
            {
                var label = task.ScmCompletionReference ?? "Verknüpfte SCM-Referenz";
                task.ScmCompletionDetectedUtc = null;
                task.ScmCompletionReference = null;
                await db.SaveChangesAsync(ct);
                await log.AddEntryAsync(taskId, LogEntryType.SystemMessage,
                    $"{label} ist wieder offen; der automatische Abschluss wurde zurückgesetzt.", ct: ct);
            }
            return;
        }
        if (task.ScmCompletionDetectedUtc is null)
        {
            task.ScmCompletionDetectedUtc = now;
            task.ScmCompletionReference = closedLabel;
            await db.SaveChangesAsync(ct);
            await log.AddEntryAsync(taskId, LogEntryType.SystemMessage,
                $"{closedLabel} ist geschlossen/gemergt; die Aufgabe wird nach Ablauf der konfigurierten Wartezeit automatisch beendet.",
                ct: ct);
            return;
        }
        if (now - task.ScmCompletionDetectedUtc.Value < wait)
            return;
        // Der Abschluss läuft unter derselben Aufgabensperre wie Dispatch und manuelle Starts.
        await ScmTaskCoordinator.Shared.RunAsync(taskId, async () =>
        {
            db.ChangeTracker.Clear();
            var current = await db.WorkItems.FindAsync([taskId], ct);
            if (current is null || current.Status is WorkItemStatus.Completed or WorkItemStatus.Archived
                || current.ScmCompletionDetectedUtc is null
                || _time.GetUtcNow() - current.ScmCompletionDetectedUtc.Value < wait)
                return;
            // Erneuter Check unter der Aufgabensperre: Der Status kann sich seit Kandidatenwahl
            // verändert haben; es gilt dieselbe Freigabe-Einteilung transient vs. terminal/final.
            if (await db.ScmIncomingEvents.AnyAsync(
                    x => x.WorkItemId == taskId && !ScmIncomingEventStatus.NonBlocking.Contains(x.Status), ct))
                return;
            try
            {
                await services.GetRequiredService<DevelopmentProcessService>().CompleteAsync(taskId, ct);
                await log.AddEntryAsync(taskId, LogEntryType.SystemMessage,
                    $"Aufgabe automatisch beendet: {current.ScmCompletionReference} ist geschlossen/gemergt und die Abschluss-Wartezeit ist abgelaufen.",
                    ct: ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                // Z. B. offene To-Dos oder ein nicht löschbarer Klon blockieren den Abschluss; der
                // Erkennungszeitpunkt bleibt persistiert — der Versuch zählt in dasselbe
                // Budget/Backoff wie die Referenzprüfung. Bei einer Dateisperre wird der
                // haltende Prozess best-effort diagnostiziert und ins Fehlerprotokoll aufgenommen.
                var detail = ex is IOException && state.LastDetail is null
                    && current.LocalClonePath is { Length: > 0 } clone
                    ? LockedFileProcessDiagnostics.DescribeLockingProcesses(clone)
                    : null;
                await RecordFailureAsync(taskId, state, ex, detail, ct);
            }
        }, ct);
    }

    /// <summary>Zählt einen Fehlschlag je (Aufgabe, Fehlersignatur), setzt den exponentiellen
    /// Backoff-Zeitpunkt und versetzt die Aufgabe nach Erschöpfung des Budgets in den
    /// ausgesetzten Zustand — einmal laut (Anwendungslog + Aufgabenprotokoll) statt
    /// minütlich still. Wiederholte identische Fehlschläge landen ab dem zweiten Versuch
    /// nur noch auf Debug-Ebene (Log-Dedupe).</summary>
    private async Task RecordFailureAsync(Guid taskId, CompletionRetryState state, Exception ex,
        string? detail, CancellationToken ct)
    {
        // Signatur bewusst ohne die Prozess-Diagnose: Deren Prozessliste kann sich von
        // Durchlauf zu Durchlauf ändern und würde das Budget sonst endlos zurücksetzen.
        var signature = ex.GetType().Name + ": " + (ex.Message.Length > 200 ? ex.Message[..200] : ex.Message);
        state.LastDetail = detail ?? state.LastDetail;
        if (!string.Equals(state.Signature, signature, StringComparison.Ordinal))
        {
            state.Signature = signature;
            state.Failures = 0;
            state.Exhausted = false;
        }
        state.Failures++;
        var display = signature + (state.LastDetail is null ? "" : " | offen gehalten von: " + state.LastDetail);
        if (state.Failures == 1)
            logger.LogWarning(ex, "SCM-Abschlussprüfung/-abschluss der Aufgabe {AufgabeId} fehlgeschlagen (Versuch {Versuch}/{Max}).", taskId, state.Failures, RetryMaxAttempts);
        else
            logger.LogDebug("SCM-Abschlussprüfung/-abschluss der Aufgabe {AufgabeId} wiederholt fehlgeschlagen ({Versuch}/{Max}): {Signatur}", taskId, state.Failures, RetryMaxAttempts, display);
        var delay = TimeSpan.FromTicks(Math.Min(
            RetryInitialDelay.Ticks << Math.Min(state.Failures - 1, 10), RetryMaxDelay.Ticks));
        state.NextAttemptUtc = _time.GetUtcNow() + delay;
        if (state.Failures < RetryMaxAttempts || state.Exhausted) return;
        state.Exhausted = true;
        logger.LogError("SCM-Abschlussprüfung/-abschluss der Aufgabe {AufgabeId} nach {Versuch} Fehlversuchen ausgesetzt: {Signatur}", taskId, state.Failures, display);
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<LogService>().AddEntryAsync(taskId,
                LogEntryType.SystemMessage,
                $"Automatische SCM-Abschlussüberwachung nach {state.Failures} Fehlversuchen ausgesetzt: {display} "
                + "Ursache beheben und die Aufgabe manuell abschließen oder die Anwendung neu starten; danach wird die Überwachung fortgesetzt.",
                ct: ct);
        }
        catch (Exception logEx)
        {
            logger.LogDebug(logEx, "Protokolleintrag zur ausgesetzten Abschlussüberwachung konnte nicht geschrieben werden.");
        }
    }

    /// <summary>Ermittelt, ob die verknüpfte SCM-Referenz der Aufgabe geschlossen/gemergt ist.
    /// Verknüpfte Pull Requests haben Vorrang und müssen allesamt geschlossen/gemergt sein; ohne
    /// Pull-Request-Verknüpfung gilt der Zustand des verknüpften Issues. Gibt eine Anzeigebezeichnung
    /// der geschlossenen Referenz zurück oder <see langword="null"/>.</summary>
    private async Task<string?> DetermineClosureAsync(
        IServiceProvider services, SoftwareschmiedeDbContext db, WorkItem task, CancellationToken ct)
    {
        var plugins = services.GetRequiredService<IPluginManager>().GetSourceCodeManagementPlugins();
        if (task.PullRequests.Count > 0)
        {
            var changed = false;
            foreach (var reference in task.PullRequests)
            {
                // Gemergt ist final; ein lediglich geschlossener Pull Request kann sich
                // wieder öffnen und wird daher weiter geprüft.
                if (reference.Status == PullRequestStatus.Merged)
                    continue;
                var plugin = ResolvePlugin(plugins, task, reference.Provider);
                if (plugin is null || !PullRequestMonitoringPolicy.CanMonitor(reference.Role, reference.Provider))
                    return null;
                PullRequestStatusInfo status;
                try
                {
                    status = await plugin.GetPullRequestStatusAsync(
                        reference.RepositoryId, reference.PullRequestNumber, ct);
                }
                catch (NotSupportedException)
                {
                    return null;
                }
                reference.Status = status.Status;
                reference.MergeStatus = status.MergeStatus;
                reference.LastCheckedUtc = _time.GetUtcNow();
                if (!string.IsNullOrWhiteSpace(status.Url))
                    reference.Url = status.Url;
                changed = true;
                if (status.Status is not (PullRequestStatus.Closed or PullRequestStatus.Merged))
                    return null;
            }
            if (changed)
                await db.SaveChangesAsync(ct);
            var numbers = task.PullRequests.Select(x => x.PullRequestNumber);
            return "Pull Request " + string.Join(", ", numbers.Select(x => "#" + x));
        }
        if (task.IssueReference?.IssueNumber is > 0 && task.GitRepository is not null)
        {
            var plugin = plugins.FirstOrDefault(x => ScmPluginMatcher.MatchesPlugin(x, task.GitRepository));
            if (plugin is not IScmEventSource source)
                return null;
            // Die Issue-Prüfung läuft immer unter dem Benutzerkonto — unabhängig vom konfigurierten
            // Überwachungsmodus der Ereignisüberwachung.
            var repository = await source.GetEventRepositoryAsync(
                new ScmRepositoryContext(task.GitRepository.Id, plugin.PluginPrefix,
                    task.GitRepository.RepositoryUrl, ScmAccountMode.UserAccount), ct);
            var issue = await source.ReadIssueAsync(
                new ScmEventReference(repository, ScmReferenceKind.Issue, task.IssueReference.IssueNumber.Value), ct);
            return issue?.IsClosed == true
                ? $"Issue #{task.IssueReference.IssueNumber.Value}"
                : null;
        }
        return null;
    }

    /// <summary>Löst das Plugin für eine Referenz bevorzugt über den Provider-Prefix auf und fällt
    /// andernfalls auf das Repository-Plugin der Aufgabe zurück.</summary>
    private static IGitPlugin? ResolvePlugin(IReadOnlyList<IGitPlugin> plugins, WorkItem task, PullRequestProvider provider)
    {
        var expected = PullRequestProviderDescriptor.GetPluginPrefix(provider);
        return plugins.FirstOrDefault(x => string.Equals(x.PluginPrefix, expected, StringComparison.OrdinalIgnoreCase))
            ?? (task.GitRepository is null ? null : plugins.FirstOrDefault(x => ScmPluginMatcher.MatchesPlugin(x, task.GitRepository)));
    }
}
