# Orchestrator: Dispatch-Pipeline und Wartegründe

Zurück: [../inventory.md](../inventory.md)

## `ScmEventOrchestrator`

Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Aufrufer | Zweck |
|---------|----------|-------|
| `DispatchAsync(eventId, explicitPluginPrefix?, ct)` (Z. 21-26) | Monitoring-Poll, „Status erneut prüfen" | Lease + `DispatchCoreAsync`. |
| `OpenFromQueueAsync(eventId, select, ct)` (Z. 29-70) | UI „Öffnen / CLI auswählen" | Lease + Auswahl; setzt `item.NextAttemptUtc = null` (Z. 58) vor Dispatch. |
| `RetryAsync` (Z. 235-244) | UI „Erneut prüfen" | `NextAttemptUtc`/`ErrorCode` löschen (auch Outbox-Actions + Checkpoints) + Dispatch. |
| `ConfirmReadyAsync` (Z. 247-259) | UI „Eingabebereitschaft bestätigen" | Run-gebundene Einmal-Bestätigung; **setzt `NextAttemptUtc` nicht zurück** → bei `long.MaxValue` endet der nachfolgende Dispatch im Return Z. 114. |
| `ResendAsync` (Z. 262-276) | UI „Neu senden…" | Nur `ZustellungUngewiss`/`VerarbeitungUngeklärt`; setzt u. a. `NextAttemptUtc = null`. |

### `DispatchCoreAsync` — relevante Schritte (Z. 72-223)

- Z. 114: `item.NextAttemptUtc > now` → Return (kein Wait-Update, kein weiterer Schritt).
- Z. 139-153: Kein lebender Adapter → CLI-Auflösung + `CheckHealthAsync` + Start (`ProzessStartenUndCliStartenAsync` / `CliNeustartenAsync`); `InvalidOperationException` → Catch Z. 209-214 → `NextAttemptUtc = long.MaxValue`.
- Z. 163-168: `item.CliRunId` persistiert; `WaitForInputReadyAsync != Ready` → `WaitWithCommentAsync`: `IsBusy` → „CLI beschäftigt; wartet auf Eingabebereitschaft.", sonst → **„Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen."**
- Z. 171-173: `CheckExchangeAccessAsync != Supported` → `WaitWithCommentAsync` „Antwortkanal für diese CLI nicht verfügbar: " + `access.Reason`.
- Z. 174-199: `exchange.PrepareAsync` → Status `ZustellungVorbereitet` + `DeliveryAttemptId` → `adapter.WritePromptAsync` → `Zugestellt` / `ZustellungUngewiss`.
- Catch Z. 209-214: `InvalidOperationException | IOException | UnauthorizedAccessException` → `NextAttemptUtc = long.MaxValue` + `WaitWithCommentAsync(ex.Message)`.

`WaitAsync`/`WaitWithCommentAsync` (Z. 385-399) setzen nur `Status="Wartend"` + `Reason` — `NextAttemptUtc` bleibt unverändert (meist `null` → Auto-Dispatch durch Poll `ScmEventMonitoringService.cs:121-124`, Filter: `Status != "Verarbeitet" && (NextAttemptUtc == null || <= now)`).

## UI: `ScmEventQueueViewModel` / `ScmEventQueueItem`

Datei: `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`

- `CanConfirmReady` (Z. 295): `!IsBlocked && AttemptId is null && CliRunId is not null && Reason?.Contains("Eingabebereitschaft unbekannt")` — ein anderer Wartegrund-Text (z. B. Trust-Dialog oder Kanalprobe) deaktiviert die Aktion automatisch.
- `OpenEventCommand`/`RetryCommand`/`CheckStatusCommand`/`ResendCommand` (Z. 54-59): Sichtbarkeitslogik über `AttemptId`/`Status`; `RetryCommand` für jedes selektierte Ereignis.
- `OpenAsync` (Z. 219-246): lebende Session → `BestaetigenDialog` „An laufende CLI übergeben…", sonst Plugin-Auswahl.

## Statusmodell `ScmIncomingEvent.Status` (Auszug)

`Aufgenommen` → `Wartend` (+ `Reason`) → `ZustellungVorbereitet` → `Zugestellt` → `InBearbeitung` → `Verarbeitet`; Fehler: `ZustellungUngewiss`, `VerarbeitungUngeklärt`. `DeliveryAttemptId != null` → nur `ReconcileAttemptAsync` (kein zweiter Prompt).
