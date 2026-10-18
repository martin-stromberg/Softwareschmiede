using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Ein gemeinsamer, dauerhafter Dispatch für Polling und Queue-Bedienung.</summary>
public sealed class ScmEventOrchestrator(
    SoftwareschmiededDbContext db, ScmEventStore store, ScmEventOutbox outbox,
    IPluginManager plugins, PluginSelectionService selection, EntwicklungsprozessService development,
    AufgabeService tasks, KiAusfuehrungsService cli, ScmEventExchangeProtocol exchange,
    ScmEventChangeNotifier notifier, IServiceScopeFactory scopes, TimeProvider time, IOptions<ScmEventOptions> options,
    AppEinstellungService einstellungen)
{
    /// <summary>Versucht Zustellung oder rekonstruiert ausschließlich den schon begonnenen Versuch.</summary>
    public async Task DispatchAsync(Guid eventId, string? explicitPluginPrefix = null, CancellationToken ct = default)
    {
        var taskId = await db.ScmIncomingEvents.Where(x => x.Id == eventId).Select(x => (Guid?)x.AufgabeId).SingleOrDefaultAsync(ct);
        if (taskId is null) return;
        await ScmTaskCoordinator.Shared.RunAsync(taskId.Value, () => DispatchCoreAsync(eventId, taskId.Value, explicitPluginPrefix, ct), ct);
    }

    /// <summary>Hält Queue-Auswahl und Aktivierung unter derselben Aufgabenlease wie die spätere Zustellung.</summary>
    public async Task OpenFromQueueAsync(Guid eventId, Func<Aufgabe, IScmPromptSessionAdapter?, CancellationToken, Task<string?>> select,
        CancellationToken ct = default)
    {
        var taskId = await db.ScmIncomingEvents.Where(x => x.Id == eventId).Select(x => x.AufgabeId).SingleAsync(ct);
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
                if (item.Status is "Verarbeitet" or "Abgebrochen") return;
                if (item.DeliveryAttemptId is not null)
                {
                    await DispatchCoreAsync(eventId, taskId, null, ct, owner);
                    return;
                }
                var task = await db.Aufgaben.SingleAsync(x => x.Id == taskId, ct);
                if (BlockReason(task) is { } blocked) { await WaitAsync(item, blocked, ct); return; }
                if (task.Status == AufgabeStatus.Neu) await tasks.SetStatusAsync(taskId, AufgabeStatus.Gestartet, ct);
                var session = cli.GetScmPromptSession(taskId);
                var prefix = await select(task, session?.IsAlive == true ? session : null, ct);
                if (prefix is null) return;
                // A confirmed queue selection is an explicit retry, unlike a host-outbox poll.
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
        try
        {
            db.ChangeTracker.Clear();
            var item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId, ct);
            // Ein Abbruch ist endgültig: keine Hostaktionen, kein Abgleich und keine Zustellung mehr.
            if (item.Status == "Abgebrochen") return;
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
                var receipt = await outbox.EnsureAsync(item, "receipt", new(observation.Reference,
                    ScmEventStore.ActionId(item.Id, "receipt").ToString("D"), ScmResponseKind.Reaction,
                    Reaction: "eyes", ContentKind: observation.ContentKind, RemoteCommentId: observation.RemoteCommentId, RemoteNodeId: observation.RemoteNodeId), ct);
                await outbox.ExecuteAsync(receipt, responder, ct);
            }
            foreach (var action in await db.ScmOutgoingActions.Where(x => x.EventId == item.Id && x.Purpose == "waiting" && x.Status != "Succeeded").ToListAsync(ct))
                await outbox.ExecuteAsync(action, responder, ct);
            if (item.Status == "Verarbeitet") return;
            if (item.DeliveryAttemptId is not null)
            {
                try { await ReconcileAttemptAsync(item, observation, responder, ct); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    item.Status = "VerarbeitungUngeklärt";
                    item.Reason = "Antwortkanal ist nicht mehr verfügbar oder enthält keine gültige Meldung. Status erneut prüfen; kein automatischer Neuversand.";
                    await db.SaveChangesAsync(ct);
                }
                return;
            }
            // A due receipt/waiting response can select an otherwise delayed event. Its
            // independent retry must not authorize another health check, start or prompt.
            if (item.NextAttemptUtc > time.GetUtcNow().ToUnixTimeMilliseconds()) return;
            if (await db.ScmIncomingEvents.AnyAsync(x => x.AufgabeId == item.AufgabeId && x.Sequence < item.Sequence && x.Status != "Verarbeitet" && x.Status != "Abgebrochen", ct))
            {
                await WaitWithCommentAsync(item, observation, responder, "Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden.", ct);
                return;
            }
            var task = await db.Aufgaben.Include(x => x.GitRepository).SingleOrDefaultAsync(x => x.Id == item.AufgabeId, ct);
            if (task is null) { await WaitAsync(item, "Zugehörige Aufgabe wurde entfernt.", ct); return; }
            var blocked = BlockReason(task);
            if (blocked is not null) { await WaitWithCommentAsync(item, observation, responder, blocked, ct); return; }
            try
            {
                var currentRepository = await source.GetEventRepositoryAsync(new ScmRepositoryContext(task.GitRepositoryId!.Value,
                    plugin.PluginPrefix, $"https://{observation.Reference.Repository.Host}/{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}"), ct);
                if (currentRepository.RepositoryId != observation.Reference.Repository.RepositoryId || !currentRepository.CanWrite)
                { await WaitWithCommentAsync(item, observation, responder, "Schreibberechtigung für den Ereignisursprung fehlt. Prüfen Sie unter Einstellungen → Plugins → GitHub → Authentifizierung den Personal Access Token: Er benötigt Zugriff auf " + observation.Reference.Repository.Owner + "/" + observation.Reference.Repository.Name + " sowie Lese- und Schreibrechte für Issues und Pull Requests. Speichern Sie Änderungen und klicken Sie anschließend beim Ereignis auf Erneut prüfen.", ct); return; }
                // Der Trigger wurde bereits bei der Aufnahme geprüft; ein seither geändertes
                // Keyword entwertet ein angenommenes Ereignis nicht nachträglich.
                var revalidation = ScmEventValidationService.GetRejection(
                    observation with { Reference = observation.Reference with { Repository = currentRepository } },
                    await einstellungen.GetScmTriggerKeywordAsync(ct));
                if (revalidation is not null && revalidation != "Kein Trigger")
                { await WaitAsync(item, "Angemeldeter Benutzer ist nicht mehr der bestätigte Repository-Owner.", ct); return; }
                if (task.Status == AufgabeStatus.Neu) await tasks.SetStatusAsync(task.Id, AufgabeStatus.Gestartet, ct);
                var adapter = cli.GetScmPromptSession(task.Id);
                if (adapter?.IsAlive == true && explicitPluginPrefix is not null &&
                    !string.Equals(adapter.PluginPrefix, explicitPluginPrefix, StringComparison.OrdinalIgnoreCase))
                { await WaitAsync(item, "Inzwischen läuft eine andere CLI. Bitte die laufende Sitzung bestätigen oder die CLI über die Aufgabe regulär wechseln.", ct); return; }
                if (adapter is null && cli.GetTerminalSession(task.Id) is not null)
                { await WaitWithCommentAsync(item, observation, responder, "Antwortkanal für diese CLI nicht verfügbar: Sitzung ohne geprüften Startvertrag.", ct); return; }
                if (adapter is null || !adapter.IsAlive)
                {
                    var prefix = explicitPluginPrefix ?? await selection.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(task.KiPluginPrefix, task.ProjektId, ct);
                    if (string.IsNullOrWhiteSpace(prefix))
                    { await WaitWithCommentAsync(item, observation, responder, "Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen.", ct); return; }
                    var kiPlugin = await selection.ResolveDevelopmentAutomationPluginStrictAsync(prefix, ct);
                    if (!await kiPlugin.CheckHealthAsync(ct)) throw new InvalidOperationException("Die ausgewählte CLI ist nicht startbereit.");
                    var parameters = prefix.Contains("KiSimulator", StringComparison.OrdinalIgnoreCase) ? "--scm-exchange" : null;
                    if (string.IsNullOrWhiteSpace(task.LokalerKlonPfad))
                        await development.ProzessStartenUndCliStartenAsync(task.Id, task.GitRepository!.RepositoryUrl,
                            task.BasisBranchName, prefix, ct, strictPluginSelection: true, optionalParameters: parameters);
                    else
                        await development.CliNeustartenAsync(task.Id, prefix, parameters, ct, strictPluginSelection: true);
                    adapter = cli.GetScmPromptSession(task.Id);
                }
                if (adapter is null || !adapter.IsAlive)
                { await WaitWithCommentAsync(item, observation, responder, "CLI-Sitzung ist nicht verfügbar.", ct); return; }
                // Die lebende Sitzung hat Vorrang. Ein geänderter Dialogwert beendet oder ersetzt sie nicht.
                await db.Entry(task).ReloadAsync(ct);
                blocked = BlockReason(task);
                if (blocked is not null) { await WaitWithCommentAsync(item, observation, responder, blocked, ct); return; }
                if (task.Status == AufgabeStatus.Neu) await tasks.SetStatusAsync(task.Id, AufgabeStatus.Gestartet, ct);
                // A denied channel must still identify the actual run that was checked, so the
                // user can distinguish it from the replacement run after a regular restart.
                item.CliRunId = adapter.CliRunId.ToString("D");
                var ready = await adapter.WaitForInputReadyAsync(ct);
                if (ready != ScmPromptReadyStatus.Ready)
                {
                    var reason = ready == ScmPromptReadyStatus.TrustPrompt
                        ? "Die CLI wartet auf die Bestätigung der Vertrauenswürdigkeit dieses Ordners. Bitte die Abfrage im Terminal der laufenden CLI-Sitzung beantworten; die Zustellung wird danach automatisch fortgesetzt."
                        : adapter.IsBusy ? "CLI beschäftigt; wartet auf Eingabebereitschaft." : "Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen.";
                    await WaitWithCommentAsync(item, observation, responder, reason, ct);
                    return;
                }
                if (adapter.IsBusy) { await WaitWithCommentAsync(item, observation, responder, "CLI beschäftigt; wartet auf Eingabebereitschaft.", ct); return; }
                var access = await adapter.CheckExchangeAccessAsync(Path.Combine(adapter.WorkingDirectory, ScmEventExchangeProtocol.DirectoryName), ct);
                if (access.Status != ScmExchangeAccessStatus.Supported)
                { await WaitWithCommentAsync(item, observation, responder, "Antwortkanal für diese CLI nicht verfügbar: " + access.Reason, ct); return; }
                var attemptId = Guid.NewGuid();
                // Konfigurierte Prompt-Anhänge werden erst hier — bei der tatsächlichen Zustellung —
                // aufgelöst und nur in den Prompt eingefügt: Der gespeicherte Ereignistext, die
                // Trigger-Gegenprüfung, Dedup/BodyHash und request.json bleiben unverändert.
                var promptSuffix = ScmPromptAppendix.Resolve(observation, await einstellungen.GetScmPromptAppendicesAsync(ct));
                var attempt = await exchange.PrepareAsync(new(item.Id, attemptId, adapter.CliRunId, task.Id, observation.Body,
                    observation.ContentKind.ToString(), $"{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}", observation.Url), adapter.WorkingDirectory, ct, access.RestrictedWriteSids, promptSuffix);
                if (!ReferenceEquals(adapter, cli.GetScmPromptSession(task.Id)) || !adapter.IsAlive)
                { await WaitAsync(item, "CLI-Sitzung hat sich geändert. Erneut prüfen.", ct); return; }
                // A cancellation between preparation and write wins over the tracked delivery state.
                if (await IsCancelledAsync(item.Id, ct)) return;
                item.Status = "ZustellungVorbereitet";
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
                    item.Status = "Zugestellt";
                }
                catch (Exception)
                {
                    if (await IsCancelledAsync(item.Id, CancellationToken.None)) return;
                    item.Status = "ZustellungUngewiss";
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
                // Configuration, workspace and start failures require a deliberate retry after correction.
                item.NextAttemptUtc = long.MaxValue;
                await WaitWithCommentAsync(item, observation, responder, ex.Message, ct);
            }
        }
        finally
        {
            renewalCancellation.Cancel();
            try { await renewal; } catch (OperationCanceledException) { }
            if (existingOwner is null) await store.ReleaseTaskAsync(taskId, owner, CancellationToken.None);
            notifier.Notify();
        }
    }

    /// <summary>Öffnen aktiviert ausschließlich eine neue, unpausierte Aufgabe.</summary>
    public async Task ActivateForOpenAsync(Guid eventId, CancellationToken ct = default)
    {
        var item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId, ct);
        var task = await db.Aufgaben.SingleAsync(x => x.Id == item.AufgabeId, ct);
        if (BlockReason(task) is null && task.Status == AufgabeStatus.Neu)
            await tasks.SetStatusAsync(task.Id, AufgabeStatus.Gestartet, ct);
    }

    /// <summary>Hebt gezielte Fehlersperren auf; ein bestehender Versuch wird nur rekonstruiert.</summary>
    public Task RetryAsync(Guid eventId, CancellationToken ct = default)
        => MutateAndDispatchAsync(eventId, async (item, token) =>
    {
        if (item.Status == "Abgebrochen") return false;
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
        if (item.Status is "Verarbeitet" or "Abgebrochen") return Task.FromResult(false);
        var session = cli.GetScmPromptSession(item.AufgabeId);
        if (session is null || !session.IsAlive || session.CliRunId.ToString("D") != displayedRunId)
            throw new InvalidOperationException("CLI-Sitzung hat sich geändert. Bitte die neue Sitzung ausdrücklich bestätigen.");
        // The live adapter owns this one-input confirmation and invalidates it at every input.
        // Persisting only a run id would authorize later, unrelated input phases in that same run.
        if (!session.ConfirmReady(session.CliRunId))
            throw new InvalidOperationException("CLI ist beschäftigt oder kann noch nicht bestätigt werden. Bitte erneut prüfen.");
        // A deliberate confirmation is an explicit retry, so an earlier hard stop must not
        // swallow it like the other queue actions already prevent.
        item.NextAttemptUtc = null;
        item.ReadyConfirmedRunId = null;
        return Task.FromResult(true);
    }, ct);

    /// <summary>Expliziter Neuversand nach sichtbarer Warnung; veraltete UI-Bestätigungen sind wirkungslos.</summary>
    public Task ResendAsync(Guid eventId, Guid displayedAttemptId, CancellationToken ct = default)
        => MutateAndDispatchAsync(eventId, (item, _) =>
    {
        if (item.DeliveryAttemptId != displayedAttemptId || item.Status is not ("ZustellungUngewiss" or "VerarbeitungUngeklärt"))
            return Task.FromResult(false);
        item.Status = "Aufgenommen";
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
        var changed = await db.ScmIncomingEvents
            .Where(x => x.Id == eventId && x.Status != "Verarbeitet" && x.Status != "Abgebrochen")
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Status, "Abgebrochen")
                .SetProperty(p => p.Reason, "Vom Anwender abgebrochen. Eine eventuell laufende CLI-Verarbeitung wird dadurch nicht beendet; es folgen keine weiteren Zustell- oder Prüfversuche.")
                .SetProperty(p => p.NextAttemptUtc, (long?)null), ct);
        if (changed != 0) notifier.Notify();
        return changed != 0;
    }

    private async Task MutateAndDispatchAsync(Guid eventId, Func<ScmIncomingEvent, CancellationToken, Task<bool>> mutation, CancellationToken ct)
    {
        var taskId = await db.ScmIncomingEvents.Where(x => x.Id == eventId).Select(x => x.AufgabeId).SingleAsync(ct);
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
        var request = new ScmExchangeRequest(item.Id, item.DeliveryAttemptId!.Value, runId, item.AufgabeId, observation.Body,
            observation.ContentKind.ToString(), $"{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}", observation.Url);
        var attempt = new ScmExchangeAttempt(request, item.ExchangePath)
        {
            RestrictedWriteSids = JsonSerializer.Deserialize<string[]>(item.ExchangeRestrictedWriteSidsJson ?? "[]") ?? []
        };
        var messages = await exchange.ReadAsync(attempt, ct);
        // A user cancellation ends all reconciliation; no further remote action is created.
        if (await IsCancelledAsync(item.Id, ct)) return;
        if (messages.Diagnostic is not null)
        {
            item.Status = "VerarbeitungUngeklärt";
            item.Reason = messages.Diagnostic;
        }
        if (messages.Accepted is not null)
        {
            item.AcceptedUtc ??= time.GetUtcNow().ToUnixTimeMilliseconds();
            item.Status = "InBearbeitung";
            item.Reason = null;
            foreach (var action in messages.Actions)
            {
                if (await IsCancelledAsync(item.Id, ct)) return;
                var responseKind = action.Kind switch { "comment" => ScmResponseKind.Comment, "reaction" => ScmResponseKind.Reaction, _ => ScmResponseKind.PullRequest };
                var response = new ScmResponseRequest(observation.Reference, action.ActionId.ToString("D"), responseKind,
                    action.Body, action.Reaction, observation.ContentKind, observation.RemoteCommentId, observation.RemoteNodeId, action.Title, action.Head, action.Base);
                var existing = await db.ScmOutgoingActions.FindAsync([action.ActionId], ct);
                if (existing is not null && (existing.EventId != item.Id || existing.AttemptId != item.DeliveryAttemptId || existing.RequestJson != JsonSerializer.Serialize(response, ScmEventStore.JsonOptions)))
                {
                    item.Status = "VerarbeitungUngeklärt";
                    item.Reason = "Aktionskennung wurde für einen anderen Inhalt oder Versuch verwendet.";
                    break;
                }
                var outgoing = await outbox.EnsureAsync(item, "ai:" + action.ActionId.ToString("D"), response, ct);
                await outbox.ExecuteAsync(outgoing, responder, ct);
                await exchange.WriteResultAsync(attempt, new(1, item.Id, request.AttemptId, runId, action.ActionId,
                    outgoing.Status == "Succeeded", outgoing.RemoteId, outgoing.Error), ct);
                if (outgoing.Status != "Succeeded")
                {
                    item.Status = "VerarbeitungUngeklärt";
                    item.Reason = outgoing.Error;
                }
            }
            if (messages.Complete is { } complete && item.Status == "InBearbeitung")
            {
                var actions = await db.ScmOutgoingActions.Where(x => x.EventId == item.Id && x.AttemptId == item.DeliveryAttemptId).ToListAsync(ct);
                var completeSet = complete.ActionIds.ToHashSet();
                if ((complete.NoRemoteAction && actions.Count == 0 && messages.Actions.Count == 0)
                    || (!complete.NoRemoteAction && actions.Count == completeSet.Count && actions.All(x => completeSet.Contains(x.Id) && x.Status == "Succeeded")))
                {
                    item.Status = "Verarbeitet";
                    item.Reason = null;
                }
            }
        }
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        if (item.Status == "ZustellungVorbereitet")
        {
            item.Status = "ZustellungUngewiss";
            item.Reason = "Unterbrochener Zustellversuch; keine automatische Wiederholung.";
        }
        if (item.Status is "Zugestellt" or "InBearbeitung")
        {
            var timeout = item.AcceptedUtc is null ? options.Value.AcceptanceTimeout : options.Value.CompletionTimeout;
            var began = item.AcceptedUtc ?? item.DeliveredUtc ?? now;
            var session = cli.GetScmPromptSession(item.AufgabeId);
            if (now - began > timeout.TotalMilliseconds || session is null || !session.IsAlive || session.CliRunId != runId)
            {
                item.Status = "VerarbeitungUngeklärt";
                item.Reason = item.AcceptedUtc is null ? "Annahme fehlt oder CLI-Sitzung beendet. Kein automatischer Neuversand." : "Korrelierter Abschluss fehlt oder CLI-Sitzung beendet.";
            }
        }
        if (await IsCancelledAsync(item.Id, ct)) return;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Liest den persistierten Status unabhängig vom verfolgten Datensatz, damit ein während eines
    /// Versuchs eintreffender Abbruch gegenüber den getrackten Änderungen gewinnt.</summary>
    private async Task<bool> IsCancelledAsync(Guid eventId, CancellationToken ct)
        => await db.ScmIncomingEvents.Where(x => x.Id == eventId).Select(x => x.Status).SingleOrDefaultAsync(ct) == "Abgebrochen";

    private static string? BlockReason(Aufgabe task)
        => task.Status is AufgabeStatus.Beendet or AufgabeStatus.Archiviert ? "Aufgabe beendet/archiviert; Start gesperrt."
            : task.PausiertBisUtc > DateTimeOffset.UtcNow ? "Aufgabe pausiert; Start gesperrt." : null;

    private async Task WaitAsync(ScmIncomingEvent item, string reason, CancellationToken ct)
    {
        item.Status = "Wartend";
        item.Reason = reason;
        await db.SaveChangesAsync(ct);
    }

    private async Task WaitWithCommentAsync(ScmIncomingEvent item, ScmEventObservation observation, IScmEventResponder responder, string reason, CancellationToken ct)
    {
        await WaitAsync(item, reason, ct);
        var action = await outbox.EnsureAsync(item, "waiting", new(observation.Reference,
            ScmEventStore.ActionId(item.Id, "waiting").ToString("D"), ScmResponseKind.Comment,
            Body: $"Die Anfrage wurde empfangen und wartet auf die Verarbeitung in der Anwendung.\n<!-- softwareschmiede:event:{item.Id:D}:waiting -->"), ct);
        await outbox.ExecuteAsync(action, responder, ct);
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
