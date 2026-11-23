using System.Text.Json;
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

/// <summary>Ein gemeinsamer, dauerhafter Dispatch für Polling und Queue-Bedienung.</summary>
public sealed class ScmEventOrchestrator(
    SoftwareschmiedeDbContext db, ScmEventStore store, ScmEventOutbox outbox,
    IPluginManager plugins, PluginSelectionService selection, DevelopmentProcessService development,
    WorkItemService tasks, AiExecutionService cli, ScmEventExchangeProtocol exchange,
    ScmEventChangeNotifier notifier, IServiceScopeFactory scopes, TimeProvider time, IOptions<ScmEventOptions> options,
    AppSettingService settings, PullRequestReferenceService pullRequestReferences,
    LogService log, ScmEventExecutionLimitService executionLimits,
    ILogger<ScmEventOrchestrator> logger, ScmStartCheckTrigger? startCheckTrigger = null)
{
    /// <summary>Fest verdrahtete Stabilisierungsfrist vor jeder Prompt-Zustellung: Erst wenn die
    /// laufende CLI-Sitzung mindestens so lange ununterbrochen eingabebereit
    /// (<see cref="ScmPromptReadyStatus.Ready"/>) ist, wird geschrieben. Vorher endet der
    /// Zustellversuch ehrlich wartend mit verschobenem <see cref="ScmIncomingEvent.NextAttemptUtc"/>.</summary>
    internal static readonly TimeSpan DeliveryStabilityPeriod = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Startprüfung: Dispatcht alle fälligen, nicht abgeschlossenen Ereignisse in der priorisierten
    /// Reihenfolge aus <see cref="ScmDispatchPriority"/> (Fortsetzungsereignisse → fällige
    /// Verzögerungen zeitaufsteigend → Eingangsreihenfolge). Wird vom Poll-Zyklus des
    /// <see cref="ScmEventMonitoringService"/> und vom ereignisgesteuerten
    /// <see cref="ScmStartCheckService"/> gleichermaßen aufgerufen und ist beliebig oft
    /// wiederholbar: Fälligkeit, Sperren und die atomare Platzvergabe entscheidet
    /// <see cref="DispatchAsync"/> je Ereignis erneut unter der Aufgabensperre.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task DispatchDueEventsAsync(CancellationToken ct = default)
    {
        var nowMilliseconds = time.GetUtcNow().ToUnixTimeMilliseconds();
        var upperSequence = await db.ScmIncomingEvents.MaxAsync(x => (long?)x.Sequence, ct) ?? 0;
        long afterSequence = 0;
        while (afterSequence < upperSequence)
        {
            // Abgebrochene Ereignisse sind endgültig: auch noch ausstehende Hostaktionen werden nicht mehr gesendet.
            var batch = await db.ScmIncomingEvents.AsNoTracking().Where(x => x.Sequence > afterSequence && x.Sequence <= upperSequence).Where(x =>
                x.Status != ScmIncomingEventStatus.Abgebrochen &&
                ((x.Status != ScmIncomingEventStatus.Verarbeitet && (x.NextAttemptUtc == null || x.NextAttemptUtc <= nowMilliseconds)) ||
                db.ScmOutgoingActions.Any(action => action.EventId == x.Id && action.Status != "Succeeded"
                    && (action.NextAttemptUtc == null || action.NextAttemptUtc <= nowMilliseconds))))
                .OrderBy(x => x.Sequence).Take(100)
                .Select(x => new ScmDispatchPriority.EventSelection(x.Id, x.WorkItemId, x.Sequence, x.Status, x.NextAttemptUtc, x.Purpose))
                .ToListAsync(ct);
            if (batch.Count == 0) break;
            afterSequence = batch[^1].Sequence;
            // Priorisierter Zugriff auf die begrenzten Ausführungsplätze: Fortsetzungen vor
            // fälligen Verzögerungen (zeitaufsteigend) vor der Eingangsreihenfolge.
            var batchWorkItemIds = batch.Select(x => x.WorkItemId).Distinct().ToList();
            var workItemStates = await db.WorkItems.AsNoTracking()
                .Where(x => batchWorkItemIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Status, x.ExecutionStatus, x.PausedUntilUtc })
                .ToDictionaryAsync(x => x.Id,
                    x => new ScmDispatchPriority.WorkItemState(x.Status, x.ExecutionStatus, x.PausedUntilUtc), ct);
            foreach (var item in ScmDispatchPriority.OrderForDispatch(batch, workItemStates))
            {
                using var dispatchScope = scopes.CreateScope();
                await dispatchScope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchAsync(item.Id, ct: ct);
            }
        }
    }

    /// <summary>Versucht Zustellung oder rekonstruiert ausschließlich den schon begonnenen Versuch.</summary>
    public async Task DispatchAsync(Guid eventId, string? explicitPluginPrefix = null, CancellationToken ct = default)
    {
        var taskId = await db.ScmIncomingEvents.Where(x => x.Id == eventId).Select(x => (Guid?)x.WorkItemId).SingleOrDefaultAsync(ct);
        if (taskId is null) return;
        await ScmTaskCoordinator.Shared.RunAsync(taskId.Value, () => DispatchCoreAsync(eventId, taskId.Value, explicitPluginPrefix, ct), ct);
    }

    /// <summary>Hält Queue-Auswahl und Aktivierung unter derselben Aufgabenlease wie die spätere Zustellung.</summary>
    public async Task OpenFromQueueAsync(Guid eventId, Func<WorkItem, IScmPromptSessionAdapter?, CancellationToken, Task<string?>> select,
        CancellationToken ct = default)
    {
        var taskId = await db.ScmIncomingEvents.Where(x => x.Id == eventId).Select(x => x.WorkItemId).SingleAsync(ct);
        await ScmTaskCoordinator.Shared.RunAsync(taskId, async () =>
        {
            var owner = Guid.NewGuid().ToString("N");
            if (!await store.ClaimTaskAsync(taskId, owner, ct))
                throw new InvalidOperationException("Dieses Ereignis wird bereits in einer anderen Sitzung bearbeitet. Bitte erneut prüfen.");
            using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var renewal = RenewLeaseAsync(taskId, owner, renewalCancellation);
            ct = renewalCancellation.Token;
            try
            {
                db.ChangeTracker.Clear();
                var item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId, ct);
                if (item.Status is ScmIncomingEventStatus.Verarbeitet or ScmIncomingEventStatus.Abgebrochen) return;
                if (item.DeliveryAttemptId is not null)
                {
                    await DispatchCoreAsync(eventId, taskId, null, ct, owner);
                    return;
                }
                var task = await db.WorkItems.SingleAsync(x => x.Id == taskId, ct);
                if (BlockReason(task) is { } blocked) { await WaitAsync(item, blocked, ct); return; }
                if (task.Status == WorkItemStatus.New) await tasks.SetStatusAsync(taskId, WorkItemStatus.Started, ct);
                var session = cli.GetScmPromptSession(taskId);
                var prefix = await select(task, session?.IsAlive == true ? session : null, ct);
                if (prefix is null) return;
                // Eine bestätigte Queue-Auswahl ist ein expliziter Wiederholungsversuch, anders als ein Host-Outbox-Poll.
                item.NextAttemptUtc = null;
                await db.SaveChangesAsync(ct);
                await DispatchCoreAsync(eventId, taskId, prefix, ct, owner);
            }
            finally
            {
                renewalCancellation.Cancel();
                try { await renewal; } catch (OperationCanceledException) { }
                await store.ReleaseTaskAsync(taskId, owner, CancellationToken.None);
                notifier.Notify();
            }
        }, ct);
    }

    private async Task DispatchCoreAsync(Guid eventId, Guid taskId, string? explicitPluginPrefix, CancellationToken ct, string? existingOwner = null)
    {
        var owner = existingOwner ?? Guid.NewGuid().ToString("N");
        if (existingOwner is null && !await store.ClaimTaskAsync(taskId, owner, ct)) return;
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var renewal = RenewLeaseAsync(taskId, owner, renewalCancellation);
        ct = renewalCancellation.Token;
        ScmIncomingEvent? item = null;
        try
        {
            db.ChangeTracker.Clear();
            item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId, ct);
            // Ein Abbruch ist endgültig: keine Hostaktionen, kein Abgleich und keine Zustellung mehr.
            if (item.Status == ScmIncomingEventStatus.Abgebrochen) return;
            // Interner Aufbereitungsauftrag: dedizierter Ausführungspfad; der generische
            // Zustellpfad würde den rohen Issue-Body als Prompt zustellen.
            if (item.Purpose == ScmIssuePreparationService.Purpose)
            {
                await DispatchPreparationAsync(item, explicitPluginPrefix, ct);
                return;
            }
            var observation = ScmEventStore.Observation(item);
            var plugin = plugins.GetSourceCodeManagementPlugins().FirstOrDefault(x => x.PluginPrefix == observation.Reference.Repository.ProviderPrefix);
            if (plugin is not IScmEventSource source || plugin is not IScmEventResponder responder)
            {
                await WaitAsync(item, "SCM-Anbieter ist nicht verfügbar.", ct);
                return;
            }
            // Hostaktionen werden unabhängig vom Zustellstatus nachgeholt, ohne einen zweiten Prompt.
            if (observation.ContentKind != ScmContentKind.Description)
            {
                var receipt = await EnsureHostActionAsync(item, "receipt", new(observation.Reference,
                    ScmEventStore.ActionId(item.Id, "receipt").ToString("D"), ScmResponseKind.Reaction,
                    Reaction: "eyes", ContentKind: observation.ContentKind, RemoteCommentId: observation.RemoteCommentId, RemoteNodeId: observation.RemoteNodeId), ct);
                if (receipt is null) return;
                await outbox.ExecuteAsync(receipt, responder, ct);
            }
            // Hostaktionen aller Zwecke werden unabhängig vom Zustellstatus nachgeholt;
            // versuchsgebundene "ai:"-Aktionen bleiben dem Exchange-Abgleich vorbehalten.
            foreach (var action in await db.ScmOutgoingActions.Where(x => x.EventId == item.Id
                && !x.Purpose.StartsWith("ai:") && x.Status != "Succeeded").ToListAsync(ct))
                await outbox.ExecuteAsync(action, responder, ct);
            if (item.Status == ScmIncomingEventStatus.Verarbeitet) return;
            if (item.DeliveryAttemptId is not null)
            {
                try { await ReconcileAttemptAsync(item, observation, responder, ct); }
                catch (ScmActionIdCollisionException ex)
                {
                    item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                    item.Reason = ex.Message;
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                    item.Reason = "Antwortkanal ist nicht mehr verfügbar oder enthält keine gültige Meldung. Status erneut prüfen; kein automatischer Neuversand.";
                    await db.SaveChangesAsync(ct);
                }
                return;
            }
            // Eine fällige Eingangs-/Warte-Antwort kann ein sonst verzögertes Ereignis auswählen. Ihr
            // eigenständiger Retry darf keinen weiteren Health-Check, Start oder Prompt autorisieren.
            if (item.NextAttemptUtc > time.GetUtcNow().ToUnixTimeMilliseconds()) return;
            if (await db.ScmIncomingEvents.AnyAsync(x => x.WorkItemId == item.WorkItemId && x.Sequence < item.Sequence && !ScmIncomingEventStatus.NonBlocking.Contains(x.Status), ct))
            {
                await WaitWithCommentAsync(item, observation, responder, "Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden.", ct);
                return;
            }
            var task = await db.WorkItems.Include(x => x.GitRepository).SingleOrDefaultAsync(x => x.Id == item.WorkItemId, ct);
            if (task is null) { await WaitAsync(item, "Zugehörige Aufgabe wurde entfernt.", ct); return; }
            var blocked = BlockReason(task);
            if (blocked is not null) { await WaitWithCommentAsync(item, observation, responder, blocked, ct); return; }
            try
            {
                var currentRepository = await source.GetEventRepositoryAsync(new ScmRepositoryContext(task.GitRepositoryId!.Value,
                    plugin.PluginPrefix, $"https://{observation.Reference.Repository.Host}/{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}",
                    observation.Reference.Repository.Account), ct);
                if (currentRepository.RepositoryId != observation.Reference.Repository.RepositoryId || !currentRepository.CanWrite)
                { await WaitWithCommentAsync(item, observation, responder, "Schreibberechtigung für den Ereignisursprung fehlt. Prüfen Sie unter Einstellungen → Plugins → GitHub → Authentifizierung den Personal Access Token: Er benötigt Zugriff auf " + observation.Reference.Repository.Owner + "/" + observation.Reference.Repository.Name + " sowie Lese- und Schreibrechte für Issues und Pull Requests. Speichern Sie Änderungen und klicken Sie anschließend beim Ereignis auf Erneut prüfen.", ct); return; }
                // Der Trigger wurde bereits bei der Aufnahme geprüft; ein seither geändertes
                // Keyword entwertet ein angenommenes Ereignis nicht nachträglich.
                var revalidation = ScmEventValidationService.GetRejection(
                    observation with { Reference = observation.Reference with { Repository = currentRepository } },
                    await settings.GetScmTriggerKeywordAsync(ct));
                // Eine gespeicherte Beobachtung mit Kontoreaktion erreicht die Inbox produktiv
                // nie (Eingangsablehnung); Altbestand oder Eingriffe werden sicherheitshalber
                // nicht zugestellt, sondern zur Klärung vorgelegt.
                if (revalidation == ScmEventValidationService.AlreadyReactedRejection)
                { await WaitAsync(item, "Auf diese Textversion wurde bereits mit dem Ereignis-Konto reagiert; die Verarbeitung erfolgt vermutlich durch eine zweite Installation.", ct); return; }
                if (revalidation is not null && revalidation != "Kein Trigger" && revalidation != ScmEventValidationService.NoMentionRejection)
                { await WaitAsync(item, "Angemeldeter Benutzer ist nicht mehr der bestätigte Repository-Owner.", ct); return; }
                if (task.Status == WorkItemStatus.New) await tasks.SetStatusAsync(task.Id, WorkItemStatus.Started, ct);
                var adapter = cli.GetScmPromptSession(task.Id);
                if (adapter?.IsAlive == true && explicitPluginPrefix is not null &&
                    !string.Equals(adapter.PluginPrefix, explicitPluginPrefix, StringComparison.OrdinalIgnoreCase))
                { await WaitAsync(item, "Inzwischen läuft eine andere CLI. Bitte die laufende Sitzung bestätigen oder die CLI über die Aufgabe regulär wechseln.", ct); return; }
                if (adapter is null && cli.GetTerminalSession(task.Id) is not null)
                { await WaitWithCommentAsync(item, observation, responder, "Antwortkanal für diese CLI nicht verfügbar: Sitzung ohne geprüften Startvertrag.", ct); return; }
                if (adapter is null || !adapter.IsAlive)
                {
                    var prefix = explicitPluginPrefix;
                    if (prefix is null)
                    {
                        var resolution = await selection.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(task.AiPluginPrefix, task.ProjectId, ct);
                        prefix = resolution.PluginPrefix;
                        if (prefix is null && resolution.IgnoredPluginPrefixes.Count > 0)
                        {
                            var reason = "Die gespeicherte Standard-CLI-Zuordnung ("
                                + string.Join(", ", resolution.IgnoredPluginPrefixes.Select(x => $"'{x}'"))
                                + ") ist nicht mehr installiert oder aktiviert, und es ist kein ausführbares Standard-Plugin konfiguriert. Plugin in den Einstellungen aktivieren oder Ereignis öffnen und CLI auswählen.";
                            // Protokolleintrag nur bei geändertem Wartegrund, damit der Poll-Zyklus
                            // nicht bei jedem Versuch denselben Eintrag erzeugt.
                            if (!string.Equals(item.Reason, reason, StringComparison.Ordinal))
                                await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage, reason, ct: ct);
                            await WaitWithCommentAsync(item, observation, responder, reason, ct);
                            return;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(prefix))
                    { await WaitWithCommentAsync(item, observation, responder, "Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen.", ct); return; }
                    // Zulassungskontrolle unmittelbar vor dem CLI-Start: Erreichtes globales oder
                    // Plugin-Limit lässt das Ereignis ohne neue Ausführung ehrlich warten. Prüfung
                    // und Platzbelegung sind eine atomare Operation; die Reservierung deckt das
                    // gesamte Startfenster (Plugin-Auflösung, Health-Check, Prozessstart) ab und
                    // wird nach der Handle-Registrierung von der echten Laufzählung abgelöst —
                    // so kann kein nebenläufiger Dispatch zwischen Prüfung und Start rutschen.
                    var admission = await executionLimits.CheckAndReserveStartSlotAsync(prefix, ct);
                    if (admission.WaitReason is not null)
                    {
                        if (!string.Equals(item.Reason, admission.WaitReason, StringComparison.Ordinal))
                            await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage, admission.WaitReason, ct: ct);
                        await WaitAsync(item, admission.WaitReason, ct);
                        return;
                    }
                    using (admission.Reservation)
                    {
                        var aiPlugin = await selection.ResolveDevelopmentAutomationPluginStrictAsync(prefix, ct);
                        if (!await aiPlugin.CheckHealthAsync(ct)) throw new InvalidOperationException("Die ausgewählte CLI ist nicht startbereit.");
                        var parameters = prefix.Contains("KiSimulator", StringComparison.OrdinalIgnoreCase) ? "--scm-exchange" : null;
                        if (string.IsNullOrWhiteSpace(task.LocalClonePath))
                            await development.StartProcessAndCliAsync(task.Id, task.GitRepository!.RepositoryUrl,
                                task.BaseBranchName, prefix, ct, strictPluginSelection: true, optionalParameters: parameters);
                        else
                            await development.RestartCliAsync(task.Id, prefix, parameters, ct, strictPluginSelection: true);
                    }
                    adapter = cli.GetScmPromptSession(task.Id);
                }
                if (adapter is null || !adapter.IsAlive)
                { await WaitWithCommentAsync(item, observation, responder, "CLI-Sitzung ist nicht verfügbar.", ct); return; }
                // Die lebende Sitzung hat Vorrang. Ein geänderter Dialogwert beendet oder ersetzt sie nicht.
                await db.Entry(task).ReloadAsync(ct);
                blocked = BlockReason(task);
                if (blocked is not null) { await WaitWithCommentAsync(item, observation, responder, blocked, ct); return; }
                if (task.Status == WorkItemStatus.New) await tasks.SetStatusAsync(task.Id, WorkItemStatus.Started, ct);
                // Ein verweigerter Kanal muss den tatsächlich geprüften Lauf trotzdem identifizieren,
                // damit der Anwender ihn vom Ersatzlauf nach einem regulären Neustart unterscheiden kann.
                item.CliRunId = adapter.CliRunId.ToString("D");
                var ready = await adapter.WaitForInputReadyAsync(ct);
                if (ready != ScmPromptReadyStatus.Ready)
                {
                    // Einmaliger Schnappschuss: geprüfte Bedingung und gemeldeter Grund stammen
                    // aus demselben Erkennungsstand.
                    var recognition = adapter.CliRecognition;
                    var reason = ready == ScmPromptReadyStatus.TrustPrompt
                        ? "Die CLI wartet auf die Bestätigung der Vertrauenswürdigkeit dieses Ordners. Bitte die Abfrage im Terminal der laufenden CLI-Sitzung beantworten; die Zustellung wird danach automatisch fortgesetzt."
                        : adapter.IsBusy ? "CLI beschäftigt; wartet auf Eingabebereitschaft."
                        : recognition.State != ScmCliRecognitionState.Supported
                        ? ScmCliWaitReason.Describe(recognition)
                        : ScmCliWaitReason.UnknownReady;
                    await WaitWithCommentAsync(item, observation, responder, reason, ct);
                    return;
                }
                if (adapter.IsBusy) { await WaitWithCommentAsync(item, observation, responder, "CLI beschäftigt; wartet auf Eingabebereitschaft.", ct); return; }
                // Stabilisierungsfrist: Erst anhaltend eingabebereite CLIs bekommen einen Prompt;
                // ein zwischenzeitlicher Statuswechsel hat ReadySinceUtc neu gestempelt.
                if (TryGetPendingStabilityDeadlineUtc(adapter, out var stabilityAttemptUtc))
                {
                    item.NextAttemptUtc = stabilityAttemptUtc;
                    await WaitWithCommentAsync(item, observation, responder,
                        "Die CLI ist erst seit Kurzem eingabebereit; die Zustellung wird nach einer kurzen Stabilisierungsphase erneut versucht.", ct);
                    return;
                }
                var access = await adapter.CheckExchangeAccessAsync(Path.Combine(adapter.WorkingDirectory, ScmEventExchangeProtocol.DirectoryName), ct, task.Title);
                if (access.Status != ScmExchangeAccessStatus.Supported)
                { await WaitWithCommentAsync(item, observation, responder, "Antwortkanal für diese CLI nicht verfügbar: " + access.Reason, ct); return; }
                var attemptId = Guid.NewGuid();
                // Konfigurierte Prompt-Anhänge werden erst hier — bei der tatsächlichen Zustellung —
                // aufgelöst und nur in den Prompt eingefügt: Der gespeicherte Ereignistext, die
                // Trigger-Gegenprüfung, Dedup/BodyHash und request.json bleiben unverändert.
                var promptSuffix = ScmPromptAppendix.Resolve(observation, await settings.GetScmPromptAppendicesAsync(ct));
                var attempt = await exchange.PrepareAsync(new(item.Id, attemptId, adapter.CliRunId, task.Id, observation.Body,
                    observation.ContentKind.ToString(), $"{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}", observation.Url), adapter.WorkingDirectory, ct, access.RestrictedWriteSids, promptSuffix);
                if (!ReferenceEquals(adapter, cli.GetScmPromptSession(task.Id)) || !adapter.IsAlive)
                { await WaitAsync(item, "CLI-Sitzung hat sich geändert. Erneut prüfen.", ct); return; }
                // Ein Abbruch zwischen Vorbereitung und Schreiben geht dem getrackten Zustellungszustand vor.
                if (await IsCancelledAsync(item.Id, ct)) return;
                item.Status = ScmIncomingEventStatus.ZustellungVorbereitet;
                item.DeliveryAttemptId = attemptId;
                item.CliRunId = adapter.CliRunId.ToString("D");
                item.ExchangePath = attempt.DirectoryPath;
                item.ExchangeRestrictedWriteSidsJson = JsonSerializer.Serialize(access.RestrictedWriteSids);
                item.DeliveredUtc = time.GetUtcNow().ToUnixTimeMilliseconds();
                item.Attempts++;
                item.Reason = null;
                item.NextAttemptUtc = null;
                await db.SaveChangesAsync(ct);
                try
                {
                    if (await IsCancelledAsync(item.Id, ct)) return;
                    await adapter.WritePromptAsync(attempt.Prompt, ct);
                    if (await IsCancelledAsync(item.Id, CancellationToken.None)) return;
                    item.Status = ScmIncomingEventStatus.Zugestellt;
                }
                catch (Exception)
                {
                    if (await IsCancelledAsync(item.Id, CancellationToken.None)) return;
                    item.Status = ScmIncomingEventStatus.ZustellungUngewiss;
                    item.Reason = "Der Schreibversuch wurde begonnen, konnte aber nicht sicher bestätigt werden. Keine automatische Wiederholung.";
                }
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (ScmProviderException ex)
            {
                item.ErrorCode = ex.FailureKind.ToString();
                item.NextAttemptUtc = ex.FailureKind is ScmProviderFailureKind.Authentication or ScmProviderFailureKind.Permission
                    ? long.MaxValue : ScmEventStore.RetryAt(++item.Attempts, time.GetUtcNow().ToUnixTimeMilliseconds(), ex.RetryAfterUtc);
                await WaitAsync(item, ex.Message, ct);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                // Konfigurations-, Arbeitsbereichs- und Startfehler erfordern einen bewussten Wiederholungsversuch nach Korrektur.
                item.NextAttemptUtc = long.MaxValue;
                await WaitWithCommentAsync(item, observation, responder, ex.Message, ct);
            }
        }
        finally
        {
            renewalCancellation.Cancel();
            try { await renewal; } catch (OperationCanceledException) { }
            if (existingOwner is null) await store.ReleaseTaskAsync(taskId, owner, CancellationToken.None);
            // Randfall „Sitzung bereits am verifizierten Prompt, letztes offenes Ereignis erreicht
            // den Endstatus": Erst jetzt wird die Platzfreigabe der Ausführungsbegrenzung wirksam —
            // die angeforderte Startprüfung füllt den Platz ereignisgesteuert statt erst beim Poll.
            if (item?.Status is "Verarbeitet" or "Abgebrochen")
                startCheckTrigger?.RequestStartCheck();
            notifier.Notify();
        }
    }

    /// <summary>Öffnen aktiviert ausschließlich eine neue, unpausierte Aufgabe.</summary>
    public async Task ActivateForOpenAsync(Guid eventId, CancellationToken ct = default)
    {
        var item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId, ct);
        var task = await db.WorkItems.SingleAsync(x => x.Id == item.WorkItemId, ct);
        if (BlockReason(task) is null && task.Status == WorkItemStatus.New)
            await tasks.SetStatusAsync(task.Id, WorkItemStatus.Started, ct);
    }

    /// <summary>Hebt gezielte Fehlersperren auf; ein bestehender Versuch wird nur rekonstruiert.</summary>
    public Task RetryAsync(Guid eventId, CancellationToken ct = default)
        => MutateAndDispatchAsync(eventId, async (item, token) =>
    {
        if (item.Status == ScmIncomingEventStatus.Abgebrochen) return false;
        item.NextAttemptUtc = null;
        item.ErrorCode = null;
        await db.ScmOutgoingActions.Where(x => x.EventId == eventId && x.Status != "Succeeded")
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.NextAttemptUtc, (long?)null), token);
        await db.ScmObservationCheckpoints.ExecuteUpdateAsync(x => x.SetProperty(p => p.NextAttemptUtc, (long?)null), token);
        return true;
    }, ct);

    /// <summary>Bestätigung gilt nur für die vom Nutzer tatsächlich angezeigte lebende Sitzung.</summary>
    public Task ConfirmReadyAsync(Guid eventId, string displayedRunId, CancellationToken ct = default)
        => MutateAndDispatchAsync(eventId, (item, _) =>
    {
        if (item.Status is ScmIncomingEventStatus.Verarbeitet or ScmIncomingEventStatus.Abgebrochen) return Task.FromResult(false);
        var session = cli.GetScmPromptSession(item.WorkItemId);
        if (session is null || !session.IsAlive || session.CliRunId.ToString("D") != displayedRunId)
            throw new InvalidOperationException("CLI-Sitzung hat sich geändert. Bitte die neue Sitzung ausdrücklich bestätigen.");
        // Der Live-Adapter besitzt diese Ein-Eingabe-Bestätigung und invalidiert sie bei jeder Eingabe.
        // Nur eine Run-ID zu persistieren würde spätere, fremde Eingabephasen desselben Laufs autorisieren.
        if (!session.ConfirmReady(session.CliRunId))
        {
            var recognition = session.CliRecognition;
            throw new InvalidOperationException(recognition.State == ScmCliRecognitionState.Unknown
                ? ScmCliWaitReason.Describe(recognition)
                : "CLI ist beschäftigt oder kann noch nicht bestätigt werden. Bitte erneut prüfen.");
        }
        // Eine bewusste Bestätigung ist ein expliziter Wiederholungsversuch; ein früherer harter Stopp
        // darf sie daher nicht verschlucken, wie es die übrigen Queue-Aktionen bereits verhindern.
        item.NextAttemptUtc = null;
        item.ReadyConfirmedRunId = null;
        return Task.FromResult(true);
    }, ct);

    /// <summary>Expliziter Neuversand nach sichtbarer Warnung; veraltete UI-Bestätigungen sind wirkungslos.</summary>
    public Task ResendAsync(Guid eventId, Guid displayedAttemptId, CancellationToken ct = default)
        => MutateAndDispatchAsync(eventId, (item, _) =>
    {
        if (item.DeliveryAttemptId != displayedAttemptId || item.Status is not (ScmIncomingEventStatus.ZustellungUngewiss or ScmIncomingEventStatus.VerarbeitungUngeklärt))
            return Task.FromResult(false);
        item.Status = ScmIncomingEventStatus.Aufgenommen;
        item.DeliveryAttemptId = null;
        item.ExchangePath = null;
        item.ExchangeRestrictedWriteSidsJson = null;
        item.AcceptedUtc = null;
        item.DeliveredUtc = null;
        item.ReadyConfirmedRunId = null;
        item.NextAttemptUtc = null;
        return Task.FromResult(true);
    }, ct);

    /// <summary>Endgültiger Benutzerabbruch eines nicht abgeschlossenen Ereignisses. Der atomare Übergang gilt
    /// auch ohne Aufgabenlease, damit ein laufender Versuch ihn an den Prüfpunkten des Dispatchers respektiert.
    /// Eine bereits zugestellte CLI-Verarbeitung wird nicht beendet; es folgen keinerlei weitere Zustell-,
    /// Prüf- oder Remote-Schritte mehr. Gibt zurück, ob der Übergang ausgeführt wurde.</summary>
    public async Task<bool> CancelAsync(Guid eventId, CancellationToken ct = default)
    {
        var prior = await db.ScmIncomingEvents.Where(x => x.Id == eventId)
            .Select(x => new { x.Status, x.NextAttemptUtc, x.WorkItemId }).SingleOrDefaultAsync(ct);
        var changed = await db.ScmIncomingEvents
            .Where(x => x.Id == eventId && x.Status != ScmIncomingEventStatus.Verarbeitet && x.Status != ScmIncomingEventStatus.Abgebrochen)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, ScmIncomingEventStatus.Abgebrochen)
                .SetProperty(p => p.Reason, "Vom Anwender abgebrochen. Eine eventuell laufende CLI-Verarbeitung wird dadurch nicht beendet; es folgen keine weiteren Zustell- oder Prüfversuche.")
                .SetProperty(p => p.NextAttemptUtc, (long?)null), ct);
        // Eine von der Verzögerungsdirektive gesetzte Aufgabenpause wird auf das nächste
        // verbleibende Direktiven-Ziel zurückgestuft bzw. aufgehoben, wenn sie noch unverändert
        // auf dem Zielzeitpunkt des abgebrochenen Ereignisses steht; fremd gesetzte Pausen bleiben.
        if (changed != 0 && prior?.Status == ScmIncomingEventStatus.Verzögert && prior.NextAttemptUtc is long deferredUntilMs)
        {
            var deferredUntil = DateTimeOffset.FromUnixTimeMilliseconds(deferredUntilMs);
            var nextMs = await db.ScmIncomingEvents
                .Where(x => x.WorkItemId == prior.WorkItemId && x.Status == ScmIncomingEventStatus.Verzögert && x.NextAttemptUtc != null)
                .MaxAsync(x => x.NextAttemptUtc, ct);
            var nextUntil = nextMs is long ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : (DateTimeOffset?)null;
            await db.WorkItems.Where(a => a.Id == prior.WorkItemId && a.PausedUntilUtc == deferredUntil)
                .ExecuteUpdateAsync(a => a.SetProperty(p => p.PausedUntilUtc, nextUntil), ct);
        }
        if (changed != 0)
        {
            notifier.Notify();
            // Ein Abbruch kann einen Ausführungsplatz freigeben (zurückgestufte Pause) oder
            // spätere Ereignisse derselben Aufgabe entsperren — die Startprüfung soll das
            // nicht erst beim nächsten Poll sehen.
            startCheckTrigger?.RequestStartCheck();
        }
        return changed != 0;
    }

    private async Task MutateAndDispatchAsync(Guid eventId, Func<ScmIncomingEvent, CancellationToken, Task<bool>> mutation, CancellationToken ct)
    {
        var taskId = await db.ScmIncomingEvents.Where(x => x.Id == eventId).Select(x => x.WorkItemId).SingleAsync(ct);
        await ScmTaskCoordinator.Shared.RunAsync(taskId, async () =>
        {
            var owner = Guid.NewGuid().ToString("N");
            if (!await store.ClaimTaskAsync(taskId, owner, ct))
                throw new InvalidOperationException("Das Ereignis wird bereits bearbeitet. Bitte erneut prüfen.");
            using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var renewal = RenewLeaseAsync(taskId, owner, renewalCancellation);
            var token = renewalCancellation.Token;
            try
            {
                db.ChangeTracker.Clear();
                var item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId, token);
                if (!await mutation(item, token)) return;
                await db.SaveChangesAsync(token);
                await DispatchCoreAsync(eventId, taskId, null, token, owner);
            }
            finally
            {
                renewalCancellation.Cancel();
                try { await renewal; } catch (OperationCanceledException) { }
                await store.ReleaseTaskAsync(taskId, owner, CancellationToken.None);
                notifier.Notify();
            }
        }, ct);
    }

    private async Task ReconcileAttemptAsync(ScmIncomingEvent item, ScmEventObservation observation, IScmEventResponder responder, CancellationToken ct)
    {
        if (item.ExchangePath is null || !Guid.TryParse(item.CliRunId, out var runId)) return;
        var request = new ScmExchangeRequest(item.Id, item.DeliveryAttemptId!.Value, runId, item.WorkItemId, observation.Body,
            observation.ContentKind.ToString(), $"{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}", observation.Url);
        var attempt = new ScmExchangeAttempt(request, item.ExchangePath)
        {
            RestrictedWriteSids = JsonSerializer.Deserialize<string[]>(item.ExchangeRestrictedWriteSidsJson ?? "[]") ?? []
        };
        var messages = await exchange.ReadAsync(attempt, ct);
        // Ein Benutzerabbruch beendet jegliche Abstimmung; es wird keine weitere Remote-Aktion erzeugt.
        if (await IsCancelledAsync(item.Id, ct)) return;
        if (messages.Diagnostic is not null)
        {
            item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
            item.Reason = messages.Diagnostic;
        }
        if (messages.Accepted is not null)
        {
            item.AcceptedUtc ??= time.GetUtcNow().ToUnixTimeMilliseconds();
            item.Status = ScmIncomingEventStatus.InBearbeitung;
            item.Reason = null;
            foreach (var action in messages.Actions)
            {
                if (await IsCancelledAsync(item.Id, ct)) return;
                var responseKind = action.Kind switch { "comment" => ScmResponseKind.Comment, "reaction" => ScmResponseKind.Reaction, "issueBodyUpdate" => ScmResponseKind.IssueBodyUpdate, _ => ScmResponseKind.PullRequest };
                var response = new ScmResponseRequest(observation.Reference, action.ActionId.ToString("D"), responseKind,
                    action.Body, action.Reaction, observation.ContentKind, observation.RemoteCommentId, observation.RemoteNodeId, action.Title, action.Head, action.Base);
                var existing = await db.ScmOutgoingActions.FindAsync([action.ActionId], ct);
                if (existing is not null && (existing.EventId != item.Id || existing.AttemptId != item.DeliveryAttemptId || existing.RequestJson != JsonSerializer.Serialize(response, ScmEventStore.JsonOptions)))
                {
                    item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                    item.Reason = "Aktionskennung wurde für einen anderen Inhalt oder Versuch verwendet.";
                    break;
                }
                var outgoing = await outbox.EnsureAsync(item, "ai:" + action.ActionId.ToString("D"), response, ct);
                await outbox.ExecuteAsync(outgoing, responder, ct);
                await exchange.WriteResultAsync(attempt, new(1, item.Id, request.AttemptId, runId, action.ActionId,
                    outgoing.Status == "Succeeded", outgoing.RemoteId, outgoing.Error), ct);
                if (outgoing.Status != "Succeeded")
                {
                    item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                    item.Reason = outgoing.Error;
                }
            }
            if (messages.Complete is { } complete && item.Status == ScmIncomingEventStatus.InBearbeitung)
            {
                var actions = await db.ScmOutgoingActions.Where(x => x.EventId == item.Id && x.AttemptId == item.DeliveryAttemptId).ToListAsync(ct);
                var completeSet = complete.ActionIds.ToHashSet();
                if ((complete.NoRemoteAction && actions.Count == 0 && messages.Actions.Count == 0)
                    || (!complete.NoRemoteAction && actions.Count == completeSet.Count && actions.All(x => completeSet.Contains(x.Id) && x.Status == "Succeeded")))
                {
                    item.Status = ScmIncomingEventStatus.Verarbeitet;
                    item.Reason = null;
                    // Nachweis-Bilder des Versuchs werden als eigener, versuchsübergreifender
                    // Outbox-Schritt (AttemptId bleibt null) eingesammelt und hochgeladen —
                    // sie verändern die geprüfte Abschlussmenge nie.
                    if (await IsCancelledAsync(item.Id, ct)) return;
                    var collected = await exchange.CollectAttachmentsAsync(attempt, complete, ct);
                    // Auch wenn alle deklarierten/abgelegten Dateien übersprungen wurden, erhält
                    // der Versuch eine transparente Kommentaraktion mit den Übersprungen-Hinweisen:
                    // ohne sie bliebe eine versuchte, vollständig verworfene Lieferung auf GitHub
                    // und in der Queue unsichtbar. Nur wenn weder Dateien noch Gründe vorliegen
                    // (leere Ablage ohne Deklaration) wird keine Aktion erzeugt.
                    if (collected.Attachments.Count > 0 || collected.Skipped.Count > 0)
                    {
                        // Die Übersprungen-Liste ist mengenmäßig unbegrenzt und wird wie der
                        // Protokolleintrag gekappt, damit der Kommentar das Remote-Limit nie sprengt.
                        const int maximumSkippedLines = 8;
                        var skippedNote = collected.Skipped.Count == 0 ? string.Empty
                            : "\n\n" + string.Join("\n", collected.Skipped.Take(maximumSkippedLines).Select(x => $"*Übersprungen: {x}*"))
                                + (collected.Skipped.Count > maximumSkippedLines
                                    ? $"\n*… und {collected.Skipped.Count - maximumSkippedLines} weitere übersprungene Dateien.*"
                                    : string.Empty);
                        var response = new ScmResponseRequest(observation.Reference,
                            ScmEventStore.ActionId(item.Id, "attachments").ToString("D"), ScmResponseKind.Comment,
                            Body: (collected.Attachments.Count > 0
                                ? "Screenshot-Nachweise des abgeschlossenen Laufs."
                                : "Die Screenshot-Nachweise des abgeschlossenen Laufs konnten nicht hochgeladen werden.") + skippedNote,
                            Attachments: collected.Attachments.Select(x => new ScmResponseAttachment(x.FullPath, x.Name, x.ContentType, x.Caption)).ToArray());
                        var outgoing = await EnsureHostActionAsync(item, "attachments", response, ct);
                        if (outgoing is null) return;
                        await outbox.ExecuteAsync(outgoing, responder, ct);
                    }
                    if (collected.Skipped.Count > 0)
                        await log.AddEntryAsync(item.WorkItemId, LogEntryType.GitAction,
                            "Screenshot-Nachweise übersprungen: " + string.Join("; ", collected.Skipped.Take(8))
                                + (collected.Skipped.Count > 8 ? "; …" : string.Empty), ct: ct);
                }
            }
        }
        // Ein im Abschluss gemeldeter, vom Agenten selbst angelegter Pull Request wird unabhängig
        // vom einzelnen Abschlussversuch mit der Aufgabe verknüpft: Die Abschlussüberwachung kann
        // die Aufgabe nach seinem Merge/Schluss beenden. Die Verknüpfung hängt nicht am sauberen
        // Abschlusspfad — sie erfolgt auch ohne Annahmemeldung und bei ungeklärtem Ereignisstatus;
        // ein verspätet lesbares complete.json holt der laufende Kanalabgleich nach.
        if (messages.Complete is { PullRequestNumber: > 0 } withPullRequest && !await IsCancelledAsync(item.Id, ct))
            await LinkReportedPullRequestAsync(item, observation, withPullRequest.PullRequestNumber.Value, ct);
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        if (item.Status == ScmIncomingEventStatus.ZustellungVorbereitet)
        {
            item.Status = ScmIncomingEventStatus.ZustellungUngewiss;
            item.Reason = "Unterbrochener Zustellversuch; keine automatische Wiederholung.";
        }
        if (item.Status is ScmIncomingEventStatus.Zugestellt or ScmIncomingEventStatus.InBearbeitung)
        {
            var timeout = item.AcceptedUtc is null ? options.Value.AcceptanceTimeout : options.Value.CompletionTimeout;
            var began = item.AcceptedUtc ?? item.DeliveredUtc ?? now;
            var session = cli.GetScmPromptSession(item.WorkItemId);
            if (now - began > timeout.TotalMilliseconds || session is null || !session.IsAlive || session.CliRunId != runId)
            {
                item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                item.Reason = item.AcceptedUtc is null ? "Annahme fehlt oder CLI-Sitzung beendet. Kein automatischer Neuversand." : "Korrelierter Abschluss fehlt oder CLI-Sitzung beendet.";
            }
        }
        if (await IsCancelledAsync(item.Id, ct)) return;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Dedizierter Ausführungspfad für interne Aufbereitungsaufträge
    /// (<see cref="ScmIssuePreparationService.Purpose"/>): Spiegelt den generischen Zustellpfad,
    /// liefert aber den festen Analyse-Prompt statt des Ereignistexts, erzeugt nie einen
    /// Kommentar am Issue und wertet das Analyse-Ergebnis über den eigenen Abgleich aus.
    /// Die Ausführung zählt über die gemeinsame Reservierung in die #410-Begrenzung.</summary>
    private async Task DispatchPreparationAsync(ScmIncomingEvent item, string? explicitPluginPrefix, CancellationToken ct)
    {
        var observation = ScmEventStore.Observation(item);
        var plugin = plugins.GetSourceCodeManagementPlugins().FirstOrDefault(x => x.PluginPrefix == observation.Reference.Repository.ProviderPrefix);
        if (plugin is not IScmEventSource source || plugin is not IScmEventResponder responder)
        {
            await WaitAsync(item, "SCM-Anbieter ist nicht verfügbar.", ct);
            return;
        }
        // Hostaktionen (außer versuchsgebundene "ai:"-Aktionen) werden unabhängig vom
        // Zustellstatus nachgeholt, ohne einen zweiten Prompt.
        foreach (var action in await db.ScmOutgoingActions.Where(x => x.EventId == item.Id
            && !x.Purpose.StartsWith("ai:") && x.Status != "Succeeded").ToListAsync(ct))
            await outbox.ExecuteAsync(action, responder, ct);
        if (item.Status == ScmIncomingEventStatus.Verarbeitet) return;
        if (item.DeliveryAttemptId is not null)
        {
            try { await ReconcilePreparationAttemptAsync(item, observation, source, responder, ct); }
            catch (ScmActionIdCollisionException ex)
            {
                item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                item.Reason = ex.Message;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                item.Reason = "Antwortkanal ist nicht mehr verfügbar oder enthält keine gültige Meldung. Status erneut prüfen; kein automatischer Neuversand.";
                await db.SaveChangesAsync(ct);
            }
            return;
        }
        if (item.NextAttemptUtc > time.GetUtcNow().ToUnixTimeMilliseconds()) return;
        var task = await db.WorkItems.Include(x => x.GitRepository).Include(x => x.IssueReference)
            .SingleOrDefaultAsync(x => x.Id == item.WorkItemId, ct);
        if (task is null) { await WaitAsync(item, "Zugehörige Aufgabe wurde entfernt.", ct); return; }
        var blocked = BlockReason(task);
        if (blocked is not null) { await WaitAsync(item, blocked, ct); return; }
        try
        {
            // Wirksamkeitsprüfung der Schalter zum Ausführungszeitpunkt: Eine deaktivierte
            // Funktion oder ein abgewähltes Repository lässt den Auftrag ehrlich warten,
            // ohne dass er verloren geht oder remote irgendetwas sichtbar wird.
            if (!await settings.GetScmIssuePreparationEnabledAsync(ct))
            { await WaitAsync(item, "Die Issue-Aufbereitung ist in den Einstellungen deaktiviert.", ct); return; }
            if (task.GitRepository?.IssuePreparationEnabled != true)
            { await WaitAsync(item, "Die Issue-Aufbereitung ist für dieses Repository deaktiviert.", ct); return; }
            var currentRepository = await source.GetEventRepositoryAsync(new ScmRepositoryContext(task.GitRepositoryId!.Value,
                plugin.PluginPrefix, $"https://{observation.Reference.Repository.Host}/{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}",
                observation.Reference.Repository.Account), ct);
            if (currentRepository.RepositoryId != observation.Reference.Repository.RepositoryId || !currentRepository.CanWrite)
            { await WaitAsync(item, "Schreibberechtigung für den Ereignisursprung fehlt. Prüfen Sie unter Einstellungen → Plugins → GitHub → Authentifizierung den Personal Access Token und klicken Sie anschließend beim Ereignis auf Erneut prüfen.", ct); return; }
            var issue = await source.ReadIssueAsync(observation.Reference, ct);
            if (issue is null)
            { await WaitAsync(item, "Das Issue konnte nicht gelesen werden. Erneut prüfen.", ct); return; }
            if (issue.IsClosed)
            {
                // Analyse eines bereits geschlossenen Issues wäre unredlich als Erfolg gemeldet;
                // ohne Änderung am Issue wird der Auftrag mit Begründung abgeschlossen.
                item.Status = ScmIncomingEventStatus.Verarbeitet;
                item.Reason = null;
                await db.SaveChangesAsync(ct);
                await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage,
                    $"Issue #{issue.Number} ist bereits geschlossen; die Aufbereitung wurde ohne Änderung abgeschlossen.", ct: ct);
                return;
            }
            if (task.Status == WorkItemStatus.New) await tasks.SetStatusAsync(task.Id, WorkItemStatus.Started, ct);
            var adapter = cli.GetScmPromptSession(task.Id);
            if (adapter?.IsAlive == true && explicitPluginPrefix is not null &&
                !string.Equals(adapter.PluginPrefix, explicitPluginPrefix, StringComparison.OrdinalIgnoreCase))
            { await WaitAsync(item, "Inzwischen läuft eine andere CLI. Bitte die laufende Sitzung bestätigen oder die CLI über die Aufgabe regulär wechseln.", ct); return; }
            if (adapter is null && cli.GetTerminalSession(task.Id) is not null)
            { await WaitAsync(item, "Antwortkanal für diese CLI nicht verfügbar: Sitzung ohne geprüften Startvertrag.", ct); return; }
            if (adapter is null || !adapter.IsAlive)
            {
                var prefix = explicitPluginPrefix;
                if (prefix is null)
                {
                    // Aufbereitungs-Plugin (Einstellung bzw. in der Aufgabe hinterlegtes Prefix),
                    // danach die #411-Kette: Projekt-Default → Global-Default → ki.plugin.default.
                    var resolution = await selection.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(
                        await settings.GetScmIssuePreparationPluginPrefixAsync(ct) ?? task.AiPluginPrefix, task.ProjectId, ct);
                    prefix = resolution.PluginPrefix;
                    if (prefix is null && resolution.IgnoredPluginPrefixes.Count > 0)
                    {
                        var reason = "Die konfigurierte KI-Plugin-Zuordnung der Issue-Aufbereitung ("
                            + string.Join(", ", resolution.IgnoredPluginPrefixes.Select(x => $"'{x}'"))
                            + ") ist nicht mehr installiert oder aktiviert, und es ist kein ausführbares Standard-Plugin konfiguriert. Plugin in den Einstellungen aktivieren oder Ereignis öffnen und CLI auswählen.";
                        if (!string.Equals(item.Reason, reason, StringComparison.Ordinal))
                            await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage, reason, ct: ct);
                        await WaitAsync(item, reason, ct);
                        return;
                    }
                }
                if (string.IsNullOrWhiteSpace(prefix))
                { await WaitAsync(item, "Keine ausführbare CLI für die Issue-Aufbereitung konfiguriert. Ereignis öffnen und CLI auswählen.", ct); return; }
                // Zulassungskontrolle unmittelbar vor dem CLI-Start: Erreichtes globales oder
                // Plugin-Limit lässt den Auftrag ohne neue Ausführung ehrlich warten.
                var admission = await executionLimits.CheckAndReserveStartSlotAsync(prefix, ct);
                if (admission.WaitReason is not null)
                {
                    if (!string.Equals(item.Reason, admission.WaitReason, StringComparison.Ordinal))
                        await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage, admission.WaitReason, ct: ct);
                    await WaitAsync(item, admission.WaitReason, ct);
                    return;
                }
                using (admission.Reservation)
                {
                    var aiPlugin = await selection.ResolveDevelopmentAutomationPluginStrictAsync(prefix, ct);
                    if (!await aiPlugin.CheckHealthAsync(ct)) throw new InvalidOperationException("Die ausgewählte CLI ist nicht startbereit.");
                    var parameters = prefix.Contains("KiSimulator", StringComparison.OrdinalIgnoreCase) ? "--scm-exchange" : null;
                    if (string.IsNullOrWhiteSpace(task.LocalClonePath))
                        await development.StartProcessAndCliAsync(task.Id, task.GitRepository!.RepositoryUrl,
                            task.BaseBranchName, prefix, ct, strictPluginSelection: true, optionalParameters: parameters);
                    else
                        await development.RestartCliAsync(task.Id, prefix, parameters, ct, strictPluginSelection: true);
                }
                adapter = cli.GetScmPromptSession(task.Id);
            }
            if (adapter is null || !adapter.IsAlive)
            { await WaitAsync(item, "CLI-Sitzung ist nicht verfügbar.", ct); return; }
            await db.Entry(task).ReloadAsync(ct);
            blocked = BlockReason(task);
            if (blocked is not null) { await WaitAsync(item, blocked, ct); return; }
            if (task.Status == WorkItemStatus.New) await tasks.SetStatusAsync(task.Id, WorkItemStatus.Started, ct);
            item.CliRunId = adapter.CliRunId.ToString("D");
            var ready = await adapter.WaitForInputReadyAsync(ct);
            if (ready != ScmPromptReadyStatus.Ready)
            {
                var recognition = adapter.CliRecognition;
                var reason = ready == ScmPromptReadyStatus.TrustPrompt
                    ? "Die CLI wartet auf die Bestätigung der Vertrauenswürdigkeit dieses Ordners. Bitte die Abfrage im Terminal der laufenden CLI-Sitzung beantworten; die Zustellung wird danach automatisch fortgesetzt."
                    : adapter.IsBusy ? "CLI beschäftigt; wartet auf Eingabebereitschaft."
                    : recognition.State != ScmCliRecognitionState.Supported
                    ? ScmCliWaitReason.Describe(recognition)
                    : ScmCliWaitReason.UnknownReady;
                await WaitAsync(item, reason, ct);
                return;
            }
            if (adapter.IsBusy) { await WaitAsync(item, "CLI beschäftigt; wartet auf Eingabebereitschaft.", ct); return; }
            // Stabilisierungsfrist wie im generischen Pfad — ohne Hostkommentar.
            if (TryGetPendingStabilityDeadlineUtc(adapter, out var stabilityAttemptUtc))
            {
                item.NextAttemptUtc = stabilityAttemptUtc;
                await WaitAsync(item, "Die CLI ist erst seit Kurzem eingabebereit; die Aufbereitung wird nach einer kurzen Stabilisierungsphase erneut versucht.", ct);
                return;
            }
            var access = await adapter.CheckExchangeAccessAsync(Path.Combine(adapter.WorkingDirectory, ScmEventExchangeProtocol.DirectoryName), ct, task.Title);
            if (access.Status != ScmExchangeAccessStatus.Supported)
            { await WaitAsync(item, "Antwortkanal für diese CLI nicht verfügbar: " + access.Reason, ct); return; }
            // Letzte Lebendprüfung unmittelbar vor der Zustellung: Plugin-Auflösung,
            // CLI-Start und Kanalprobe können beliebig lange dauern — ein
            // zwischenzeitlich geschlossenes Issue wird nicht mehr analysiert.
            issue = await source.ReadIssueAsync(observation.Reference, ct);
            if (issue is null)
            { await WaitAsync(item, "Das Issue konnte nicht gelesen werden. Erneut prüfen.", ct); return; }
            if (issue.IsClosed)
            {
                item.Status = ScmIncomingEventStatus.Verarbeitet;
                item.Reason = null;
                await db.SaveChangesAsync(ct);
                await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage,
                    $"Issue #{issue.Number} wurde vor der Zustellung geschlossen; die Aufbereitung wurde ohne Änderung abgeschlossen.", ct: ct);
                return;
            }
            var attemptId = Guid.NewGuid();
            // Der Analyse-Prompt wird aus persistierten Erkennungsdaten gebaut und bleibt damit
            // beim Wiederanlauf bitgleich rekonstruierbar (Prüfung gegen request.json).
            var promptBody = ScmIssuePreparationPrompt.Build(task.IssueReference?.Title ?? task.Title, observation.Body);
            var attempt = await exchange.PrepareAsync(new(item.Id, attemptId, adapter.CliRunId, task.Id, promptBody,
                ScmIssuePreparationService.Purpose, $"{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}",
                issue.IssueUrl ?? observation.Url), adapter.WorkingDirectory, ct, access.RestrictedWriteSids);
            if (!ReferenceEquals(adapter, cli.GetScmPromptSession(task.Id)) || !adapter.IsAlive)
            { await WaitAsync(item, "CLI-Sitzung hat sich geändert. Erneut prüfen.", ct); return; }
            if (await IsCancelledAsync(item.Id, ct)) return;
            item.Status = ScmIncomingEventStatus.ZustellungVorbereitet;
            item.DeliveryAttemptId = attemptId;
            item.CliRunId = adapter.CliRunId.ToString("D");
            item.ExchangePath = attempt.DirectoryPath;
            item.ExchangeRestrictedWriteSidsJson = JsonSerializer.Serialize(access.RestrictedWriteSids);
            item.DeliveredUtc = time.GetUtcNow().ToUnixTimeMilliseconds();
            item.Attempts++;
            item.Reason = null;
            item.NextAttemptUtc = null;
            await db.SaveChangesAsync(ct);
            try
            {
                if (await IsCancelledAsync(item.Id, ct)) return;
                await adapter.WritePromptAsync(attempt.Prompt, ct);
                if (await IsCancelledAsync(item.Id, CancellationToken.None)) return;
                item.Status = ScmIncomingEventStatus.Zugestellt;
                // Issue #451: Mit der erfolgreichen Zustellung des Analyse-Prompts ist die
                // Aufbereitungs-Phase abgeschlossen — die Aufgabe wird zur Anweisungsaufgabe.
                // Der Purpose bleibt zur Routing-Isolation unverändert „IssueAufbereitung";
                // auch vor der Typisierung persistierte Aufbereitungsaufgaben (Unspecified)
                // wechseln so mit ihrer ersten Zustellung.
                task.Kind = WorkItemKind.Instruction;
            }
            catch (Exception)
            {
                if (await IsCancelledAsync(item.Id, CancellationToken.None)) return;
                item.Status = ScmIncomingEventStatus.ZustellungUngewiss;
                item.Reason = "Der Schreibversuch wurde begonnen, konnte aber nicht sicher bestätigt werden. Keine automatische Wiederholung.";
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (ScmProviderException ex)
        {
            item.ErrorCode = ex.FailureKind.ToString();
            item.NextAttemptUtc = ex.FailureKind is ScmProviderFailureKind.Authentication or ScmProviderFailureKind.Permission
                ? long.MaxValue : ScmEventStore.RetryAt(++item.Attempts, time.GetUtcNow().ToUnixTimeMilliseconds(), ex.RetryAfterUtc);
            logger.LogWarning(ex, "Issue-Aufbereitung {EventId}: Anbieter-Fehler ({FailureKind}).", item.Id, ex.FailureKind);
            await LogPreparationErrorAsync(item, ex.Message, ct);
            await WaitAsync(item, ex.Message, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // Konfigurations-, Arbeitsverzeichnis- und Startfehler verlangen einen bewussten
            // Neuversand nach Korrektur — kein automatischer Retry, keine Remote-Änderung.
            item.NextAttemptUtc = long.MaxValue;
            logger.LogError(ex, "Issue-Aufbereitung {EventId} fehlgeschlagen.", item.Id);
            await LogPreparationErrorAsync(item, ex.Message, ct);
            await WaitAsync(item, ex.Message, ct);
        }
    }

    /// <summary>Gibt Analyse- und Rückschreibefehler der Aufbereitung als ehrlichen
    /// Protokolleintrag am Vorgang aus (dedupliziert nach Wartegrund, damit der
    /// Poll-Zyklus nicht bei jedem Versuch denselben Eintrag erzeugt). Es wird
    /// bewusst kein Kommentar am Issue erzeugt.</summary>
    private async Task LogPreparationErrorAsync(ScmIncomingEvent item, string reason, CancellationToken ct)
    {
        if (string.Equals(item.Reason, reason, StringComparison.Ordinal)) return;
        await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage,
            "Issue-Aufbereitung fehlgeschlagen: " + reason, ct: ct);
    }

    /// <summary>Fristberechnung der Stabilisierungsphase beider Zustellpfade: Liefert
    /// <see langword="true"/> samt nächstem Versuchszeitpunkt (<c>ReadySinceUtc +
    /// <see cref="DeliveryStabilityPeriod"/></c>), solange die Sitzung kürzer als die Frist
    /// ununterbrochen eingabebereit ist; der pfadspezifische Warteaufruf (mit bzw. ohne
    /// Hostkommentar) bleibt am Ort.</summary>
    /// <param name="adapter">Die eingabebereit gemeldete Prompt-Sitzung.</param>
    /// <param name="nextAttemptUtc">Unix-ms des frühesten nächsten Zustellversuchs.</param>
    private bool TryGetPendingStabilityDeadlineUtc(IScmPromptSessionAdapter adapter, out long nextAttemptUtc)
    {
        nextAttemptUtc = default;
        if (adapter.ReadySinceUtc is not { } readySinceUtc
            || time.GetUtcNow() - readySinceUtc >= DeliveryStabilityPeriod)
            return false;
        nextAttemptUtc = readySinceUtc.Add(DeliveryStabilityPeriod).ToUnixTimeMilliseconds();
        return true;
    }

    /// <summary>Abgleich des Aufbereitungs-Versuchs: Die CLI liefert ihre Analyse als
    /// „issueBodyUpdate"- und „comment"-Aktionen über den Exchange-Kanal; der Host erzeugt daraus
    /// idempotente Outbox-Aktionen (<see cref="ScmResponseKind.IssueBodyUpdate"/> ersetzt nur die
    /// Beschreibung — der Titel bleibt unverändert). Der Kommentar-Schalter wird hier hostseitig
    /// erzwungen: Bei deaktiviertem Schalter wird eine gelieferte Kommentaraktion verworfen und
    /// protokolliert, bei aktivem Schalter ist ein erfolgreicher Kommentar Abschlusskriterium.
    /// Kein Wartehinweis-, Fehler- oder Attachments-Kommentar am Issue.</summary>
    private async Task ReconcilePreparationAttemptAsync(ScmIncomingEvent item, ScmEventObservation observation,
        IScmEventSource source, IScmEventResponder responder, CancellationToken ct)
    {
        if (item.ExchangePath is null || !Guid.TryParse(item.CliRunId, out var runId)) return;
        var task = await db.WorkItems.Include(x => x.IssueReference)
            .SingleOrDefaultAsync(x => x.Id == item.WorkItemId, ct);
        var promptBody = ScmIssuePreparationPrompt.Build(task?.IssueReference?.Title ?? task?.Title, observation.Body);
        var request = new ScmExchangeRequest(item.Id, item.DeliveryAttemptId!.Value, runId, item.WorkItemId, promptBody,
            ScmIssuePreparationService.Purpose, $"{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}", observation.Url);
        var attempt = new ScmExchangeAttempt(request, item.ExchangePath)
        {
            RestrictedWriteSids = JsonSerializer.Deserialize<string[]>(item.ExchangeRestrictedWriteSidsJson ?? "[]") ?? []
        };
        var messages = await exchange.ReadAsync(attempt, ct);
        if (await IsCancelledAsync(item.Id, ct)) return;
        if (messages.Diagnostic is not null)
        {
            item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
            item.Reason = messages.Diagnostic;
        }
        var commentAllowed = await settings.GetScmIssuePreparationCommentEnabledAsync(ct);
        if (messages.Accepted is not null)
        {
            item.AcceptedUtc ??= time.GetUtcNow().ToUnixTimeMilliseconds();
            item.Status = ScmIncomingEventStatus.InBearbeitung;
            item.Reason = null;
            // Zwischenzeitlich geschlossenes Issue: gelieferte Remote-Aktionen werden
            // nicht mehr angewendet — je Aktion ein ehrliches Misserfolgs-Result, der
            // Auftrag endet sauber ohne Remote-Änderung (Analyse durfte zu Ende laufen).
            // Unlesbar = abwarten und beim nächsten Abgleich erneut prüfen.
            if (messages.Actions.Count > 0)
            {
                var live = await source.ReadIssueAsync(observation.Reference, ct);
                if (live is null) { await db.SaveChangesAsync(ct); return; }
                if (live.IsClosed)
                {
                    foreach (var action in messages.Actions)
                        await exchange.WriteResultAsync(attempt, new(1, item.Id, request.AttemptId, runId, action.ActionId,
                            false, null, "Das Issue wurde zwischenzeitlich geschlossen; die Änderung wird nicht remote angewendet."), ct);
                    item.Status = ScmIncomingEventStatus.Verarbeitet;
                    item.Reason = null;
                    await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage,
                        $"Issue #{live.Number} wurde während der Analyse geschlossen; die Aufbereitung wurde ohne Remote-Änderung abgeschlossen.", ct: ct);
                    await db.SaveChangesAsync(ct);
                    return;
                }
            }
            var skippedComments = 0;
            var skippedActionIds = new HashSet<Guid>();
            foreach (var action in messages.Actions)
            {
                if (await IsCancelledAsync(item.Id, ct)) return;
                ScmResponseKind responseKind;
                if (action.Kind == "issueBodyUpdate")
                {
                    responseKind = ScmResponseKind.IssueBodyUpdate;
                }
                else if (action.Kind == "comment")
                {
                    // Der Kommentar-Schalter ist eine Host-Entscheidung: Bei „aus" wird die
                    // gelieferte Dokumentation ehrlich verworfen (Result success=false), damit
                    // das Issue unverändert bleibt und die CLI das Ergebnis sieht.
                    if (!commentAllowed)
                    {
                        skippedComments++;
                        skippedActionIds.Add(action.ActionId);
                        await exchange.WriteResultAsync(attempt, new(1, item.Id, request.AttemptId, runId, action.ActionId,
                            false, null, "Der Dokumentations-Kommentar ist in den Einstellungen deaktiviert."), ct);
                        continue;
                    }
                    responseKind = ScmResponseKind.Comment;
                }
                else
                {
                    item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                    item.Reason = $"Nicht unterstützte Analyse-Aktion '{action.Kind}'.";
                    await exchange.WriteResultAsync(attempt, new(1, item.Id, request.AttemptId, runId, action.ActionId,
                        false, null, "Aktionsart wird für die Issue-Aufbereitung nicht unterstützt."), ct);
                    break;
                }
                var response = new ScmResponseRequest(observation.Reference, action.ActionId.ToString("D"), responseKind,
                    action.Body, action.Reaction, observation.ContentKind, observation.RemoteCommentId, observation.RemoteNodeId, action.Title, action.Head, action.Base);
                var existing = await db.ScmOutgoingActions.FindAsync([action.ActionId], ct);
                if (existing is not null && (existing.EventId != item.Id || existing.AttemptId != item.DeliveryAttemptId || existing.RequestJson != JsonSerializer.Serialize(response, ScmEventStore.JsonOptions)))
                {
                    item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                    item.Reason = "Aktionskennung wurde für einen anderen Inhalt oder Versuch verwendet.";
                    break;
                }
                var outgoing = await outbox.EnsureAsync(item, "ai:" + action.ActionId.ToString("D"), response, ct);
                await outbox.ExecuteAsync(outgoing, responder, ct);
                await exchange.WriteResultAsync(attempt, new(1, item.Id, request.AttemptId, runId, action.ActionId,
                    outgoing.Status == "Succeeded", outgoing.RemoteId, outgoing.Error), ct);
                if (outgoing.Status != "Succeeded")
                {
                    // Fehlgeschlagene Rückschreibung: ehrlich unklar, Log + Protokolleintrag;
                    // kein Fehlerkommentar am Issue.
                    item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                    logger.LogError("Issue-Aufbereitung {EventId}: Aktion {ActionId} fehlgeschlagen: {Error}", item.Id, action.ActionId, outgoing.Error);
                    await LogPreparationErrorAsync(item, outgoing.Error ?? "Unbekannter Fehler.", ct);
                    item.Reason = outgoing.Error;
                }
            }
            if (skippedComments > 0)
                await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage,
                    $"{skippedComments} Analyse-Kommentar(e) verworfen: Der Dokumentations-Kommentar ist in den Einstellungen deaktiviert.", ct: ct);
            if (messages.Complete is { } complete && item.Status == ScmIncomingEventStatus.InBearbeitung)
            {
                var actions = await db.ScmOutgoingActions.Where(x => x.EventId == item.Id && x.AttemptId == item.DeliveryAttemptId).ToListAsync(ct);
                var completeSet = complete.ActionIds.ToHashSet();
                if (complete.NoRemoteAction && actions.Count == 0)
                {
                    item.Status = ScmIncomingEventStatus.Verarbeitet;
                    item.Reason = null;
                    if (!string.IsNullOrWhiteSpace(complete.Report))
                        await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage,
                            "Issue-Aufbereitung abgeschlossen ohne Remote-Änderung: " + complete.Report, ct: ct);
                }
                else if (!complete.NoRemoteAction
                    && completeSet.Except(skippedActionIds).ToHashSet() is { } expected
                    && actions.Count == expected.Count && actions.All(x => expected.Contains(x.Id) && x.Status == "Succeeded")
                    && actions.Any(x => JsonSerializer.Deserialize<ScmResponseRequest>(x.RequestJson, ScmEventStore.JsonOptions)?.Kind == ScmResponseKind.IssueBodyUpdate))
                {
                    // Abschlusskriterien: ≥1 erfolgreiches IssueBodyUpdate; bei aktivem
                    // Kommentar-Schalter zusätzlich ≥1 erfolgreicher Analyse-Kommentar.
                    var commentOk = !commentAllowed || actions.Any(x =>
                        JsonSerializer.Deserialize<ScmResponseRequest>(x.RequestJson, ScmEventStore.JsonOptions)?.Kind == ScmResponseKind.Comment);
                    if (commentOk)
                    {
                        item.Status = ScmIncomingEventStatus.Verarbeitet;
                        item.Reason = null;
                        if (!string.IsNullOrWhiteSpace(complete.Report))
                            await log.AddEntryAsync(item.WorkItemId, LogEntryType.SystemMessage,
                                "Issue-Aufbereitung abgeschlossen: " + complete.Report, ct: ct);
                    }
                    else
                    {
                        item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                        await LogPreparationErrorAsync(item, "Der dokumentierende Analyse-Kommentar wurde nicht remote bestätigt.", ct);
                        item.Reason = "Der dokumentierende Analyse-Kommentar wurde nicht remote bestätigt.";
                    }
                }
                else if (!complete.NoRemoteAction)
                {
                    item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                    item.Reason = "Die optimierte Issue-Beschreibung wurde nicht remote bestätigt. Status erneut prüfen; kein automatischer Neuversand.";
                }
            }
        }
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        if (item.Status == ScmIncomingEventStatus.ZustellungVorbereitet)
        {
            item.Status = ScmIncomingEventStatus.ZustellungUngewiss;
            item.Reason = "Unterbrochener Zustellversuch; keine automatische Wiederholung.";
        }
        if (item.Status is ScmIncomingEventStatus.Zugestellt or ScmIncomingEventStatus.InBearbeitung)
        {
            var timeout = item.AcceptedUtc is null ? options.Value.AcceptanceTimeout : options.Value.CompletionTimeout;
            var began = item.AcceptedUtc ?? item.DeliveredUtc ?? now;
            var session = cli.GetScmPromptSession(item.WorkItemId);
            if (now - began > timeout.TotalMilliseconds || session is null || !session.IsAlive || session.CliRunId != runId)
            {
                item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
                item.Reason = item.AcceptedUtc is null ? "Annahme fehlt oder CLI-Sitzung beendet. Kein automatischer Neuversand." : "Korrelierter Abschluss fehlt oder CLI-Sitzung beendet.";
            }
        }
        if (await IsCancelledAsync(item.Id, ct)) return;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Verknüpft einen vom Agenten im Abschluss gemeldeten Pull Request mit der Aufgabe.
    /// Repository und Provider kommen ausschließlich aus dem gespeicherten, host-validierten Ursprung;
    /// die gemeldete Nummer ist Datenwert. Eine fremd zugeordnete Referenz wird nicht übernommen.
    /// Da der Kanalabgleich für jedes nicht abgeschlossene Ereignis wiederholt läuft, sind die
    /// Verknüpfung und ihr Protokolleintrag je Aufgabe dedupliziert.</summary>
    private async Task LinkReportedPullRequestAsync(ScmIncomingEvent item, ScmEventObservation observation,
        int pullRequestNumber, CancellationToken ct)
    {
        var repository = observation.Reference.Repository;
        var repositoryId = $"{repository.Owner}/{repository.Name}";
        var url = $"https://{repository.Host}/{repository.Owner}/{repository.Name}/pull/{pullRequestNumber}";
        var foreignMessage = $"Der vom Agenten gemeldete Pull Request #{pullRequestNumber} ist bereits einer anderen Aufgabe zugeordnet und wurde nicht verknüpft ({url}).";
        var existing = await pullRequestReferences.FindByKeyAsync(
            repository.ProviderPrefix, repositoryId, pullRequestNumber, ct);
        // Bereits derselben Aufgabe verknüpft: weder erneutes Speichern noch ein weiterer Protokolleintrag.
        if (existing?.WorkItemId == item.WorkItemId)
            return;
        // Fremdzuordnung bleibt unangetastet; der Hinweis wird höchstens einmal protokolliert.
        if (existing is not null)
        {
            if (!await db.LogEntries.AnyAsync(x => x.WorkItemId == item.WorkItemId
                && x.Type == LogEntryType.GitAction && x.Content == foreignMessage, ct))
                await log.AddEntryAsync(item.WorkItemId, LogEntryType.GitAction, foreignMessage, ct: ct);
            return;
        }
        var linked = await pullRequestReferences.SaveReportedAsync(
            item.WorkItemId, repository.ProviderPrefix, repositoryId, pullRequestNumber, url, ct);
        await log.AddEntryAsync(item.WorkItemId, LogEntryType.GitAction,
            linked is not null
                ? $"Pull Request #{pullRequestNumber} wurde vom Agenten gemeldet und mit der Aufgabe verknüpft ({url})."
                : foreignMessage,
            ct: ct);
    }

    /// <summary>Liest den persistierten Status unabhängig vom verfolgten Datensatz, damit ein während eines
    /// Versuchs eintreffender Abbruch gegenüber den getrackten Änderungen gewinnt.</summary>
    private async Task<bool> IsCancelledAsync(Guid eventId, CancellationToken ct)
        => await db.ScmIncomingEvents.Where(x => x.Id == eventId).Select(x => x.Status).SingleOrDefaultAsync(ct) == ScmIncomingEventStatus.Abgebrochen;

    private string? BlockReason(WorkItem task)
        => task.Status is WorkItemStatus.Completed or WorkItemStatus.Archived ? "Aufgabe beendet/archiviert; Start gesperrt."
            : task.PausedUntilUtc.IsCurrentlyPaused(time.GetUtcNow()) ? "Aufgabe pausiert; Start gesperrt." : null;

    private async Task WaitAsync(ScmIncomingEvent item, string reason, CancellationToken ct)
    {
        item.Status = ScmIncomingEventStatus.Wartend;
        item.Reason = reason;
        await db.SaveChangesAsync(ct);
    }

    private async Task WaitWithCommentAsync(ScmIncomingEvent item, ScmEventObservation observation, IScmEventResponder responder, string reason, CancellationToken ct)
    {
        await WaitAsync(item, reason, ct);
        var action = await EnsureHostActionAsync(item, "waiting", new(observation.Reference,
            ScmEventStore.ActionId(item.Id, "waiting").ToString("D"), ScmResponseKind.Comment,
            Body: $"Die Anfrage wurde empfangen und wartet auf die Verarbeitung in der Anwendung.\n<!-- softwareschmiede:event:{item.Id:D}:waiting -->"), ct);
        if (action is not null) await outbox.ExecuteAsync(action, responder, ct);
    }

    /// <summary>Legt eine host-generierte Outbox-Aktion an. Die deterministischen IDs sind dem
    /// Agenten errechenbar; hat er die ID mit einer eigenen Aktion vorweggenommen, wird diese
    /// nicht still als Nachweis wiederverwendet — das Ereignis endet sichtbar als unklar.</summary>
    private async Task<ScmOutgoingAction?> EnsureHostActionAsync(ScmIncomingEvent item, string purpose,
        ScmResponseRequest request, CancellationToken ct)
    {
        try
        {
            return await outbox.EnsureAsync(item, purpose, request, ct);
        }
        catch (ScmActionIdCollisionException ex)
        {
            item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt;
            item.Reason = ex.Message;
            await db.SaveChangesAsync(ct);
            return null;
        }
    }

    private async Task RenewLeaseAsync(Guid taskId, string owner, CancellationTokenSource cancellation)
    {
        var ct = cancellation.Token;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), time);
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = scopes.CreateScope();
            if (await scope.ServiceProvider.GetRequiredService<ScmEventStore>().RenewTaskAsync(taskId, owner, ct) != 1)
            {
                cancellation.Cancel();
                throw new OperationCanceledException("SCM-Aufgabenlease ging verloren.", ct);
            }
        }
    }
}
