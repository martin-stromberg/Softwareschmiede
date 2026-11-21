# Logik

## `ScmEventExecutionLimitService` — Kernstelle der Anforderung

Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`

Primärkonstruktor-Abhängigkeiten (Z. 47–52): `SoftwareschmiedeDbContext db`,
`AppSettingService settings`, `ICliExecutionLimitSource cli`, `TimeProvider time`,
`ScmStartSlotReservationPool reservationPool`. Registrierung: scoped
(`ScmTestServices` Z. 69; produktiv analog), Pool als Singleton.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckAndReserveStartSlotAsync(pluginPrefix, ct)` | public | Atomare Zulassung + Platzbelegung unter `reservationPool.AdmissionGate` (Z. 81–104); liest `EffectiveOccupancyAsync`, reserviert bei freiem Platz. Wird aus `ScmEventOrchestrator` (Z. 245 regulärer Dispatch, Z. 705 Issue-Aufbereitung) aufgerufen. |
| `GetStartBlockReasonAsync(pluginPrefix, ct)` | public | Reine Prüfung ohne Belegung (Z. 117–131); aktuell ohne Produktiv-Aufrufer (nur Tests). |
| `GetStartBlockReason(prefix, global, plugin, occupancy)` | internal static | Reine Auswertung: globales Limit zuerst, dann Plugin-Limit (Z. 139–147); liefert den benutzerlesbaren Wartegrund. |
| `GetLimitsAsync(pluginPrefix, ct)` | private | Liest `scm.execution.maxparallel` + `plugins.maxparallel.<prefix>` in einem `GetSettingsAsync`-Aufruf (Z. 150–161). |
| `EffectiveOccupancyAsync(ct)` | private | `GetOccupancyAsync` + offene Reservierungen (Z. 166–180); muss unter `AdmissionGate` laufen. |
| **`GetOccupancyAsync(ct)`** | private | **Zielmethode der Anforderung** (Z. 189–214): `cli.GetRunningWorkItemIds()` → Aufgabenprojektion `{ Id, AiPluginPrefix, PausedUntilUtc }` → Pausenfilter (`IsCurrentlyPaused`) → Plugin-Attribution über `cli.GetScmPromptSession(id)` (`IsAlive && PluginPrefix` sonst `AiPluginPrefix`) → `Global = active.Count + (runningIds.Count - workItems.Count)`. Keine Session-/Pending-Prüfung heute. |
| `ParseLimit(value)` | private static | Ganzzahl ≥ 1, sonst unbegrenzt (Z. 182–183). |

Konstanten: `GlobalLimitKey` (Z. 55), `PluginLimitKeyPrefix` (Z. 58), `ReservationLifetime` = 5 min (Z. 62).

Wichtig: Die Pending-Abfrage für die neue Freigaberegel läuft über `db.ScmIncomingEvents` — die
Entity ist bereits im selben DbContext verfügbar; die Aufgabenprojektion wird aktuell gebündelt
über `runningIds.Contains(x.Id)` geladen (kein N+1 für Aufgaben).

## `ScmPromptSessionAdapter` / `IScmPromptSessionAdapter`-Implementierung

Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `IsAlive` | public | `_isCurrent() && Session.Failure is null && Session.ExitCode is null && !Session.Process.HasExited` (Z. 129–136). |
| `ReadyStatus` | public | `Exited` wenn `!IsAlive`, sonst interner `_ready`-Stand unter `_sync` (Z. 140). |
| `IsBusy` | public | `ReadyStatus == Busy` (Z. 138). |
| `WaitForInputReadyAsync(ct)` | public | Wartet auf echtes Promptsignal über `_changed`-TCS (max. 2 s Slice), nie auf I/O-Ruhe (Z. 145–155). |
| `CheckExchangeAccessAsync(...)` | public | Kanal-/Startvertragsprüfung inkl. Dateitool-Probe; `MarkInputStarted` beim Probesenden (Z. 158–234, 237–244). |
| `ConfirmReady(runId)` | public | Manuelle Ready-Bestätigung; nicht bei `Busy`/`TrustPrompt`/unbekanntem Format (Z. 247–264). |
| `MarkInputStarted()` | public | Verwirft Bereitschaft → `Busy` (Z. 267–275); aufgerufen aus `SendAccessProbeAsync`, `WritePromptAsync` und extern über `ITerminalSession.InputStarted` (`AiExecutionService` Z. 379 → `ScmCoordinatedTerminalSession` Z. 24/31). |
| `WritePromptAsync(prompt, ct)` | public | Schreibt nur bei `IsAlive && ReadyStatus == Ready` und `Supported`-Kanal; `MarkInputStarted` vor dem Schreiben (Z. 278–288). |
| `SetReady(ready)` | private | Einziges Statussignal: `_ready`-Wechsel stößt einen `_changed`-TCS-Wechsel an (Z. 375–382) — **kein öffentliches Ereignis**. |
| `RefreshScreen()` | private | Bildschirmpuffer-Auswertung: Trust-Dialoge → `TrustPrompt`; Simulator `SCM_READY_V1`/`SCM_BUSY_V1`; Codex `›`, Claude `❯`, Devin `❭` → `Ready`; Busy-Marker (`esc to interrupt` u. a.) → `Busy`; sonst `Unknown` (Z. 334–372). |
| `OnExited`/`OnFailed` | private | → `Exited` (Z. 385–386). |

## `CliProcessManager` — Kandidat für den Auslöser

Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs`

Ctor (Z. 62–81): `AiExecutionService`, `IServiceScopeFactory`, `ILogger<CliProcessManager>`,
`WorkItemRunDataChangedNotifier` — **kein** `ScmStartCheckTrigger` bisher. DI: Singleton
(`App.xaml.cs` Z. 327).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnRuntimeStatusChanged(workItemId, status)` | private | **Ankoppelpunkt:** bei `Inactive` No-Op, sonst `EnsureRunDataAsync` fire-and-forget (Z. 318–324). Wird für jeden `CliRuntimeStatus`-Wechsel (Running ↔ WaitingForInput) aufgerufen. |
| `SubscribeRuntimeStatus(workItemId)` | private | Idempotente Subscription auf `ITerminalSession.RuntimeStatusChanged` unter `_lifecycleLock` (Z. 250–288). |
| `EnsureRunDataAsync(workItemId)` | private | Selbstheilung: Heartbeat + Subscription + `WorkItemService.EnsureActiveRunAsync` mit Live-Substatus (Z. 357–386). |
| `OnCliProcessStatusChanged(...)` | private | Started → Heartbeat/ActiveRun/Subscription; Stopped/Error → Stop/Abmeldung (Z. 177–206). |

## `ScmStartCheckTrigger` / `ScmStartCheckService` — bestehender Auslösemechanismus

Datei: `src/Softwareschmiede/Application/Services/ScmStartCheckService.cs`

`ScmStartCheckTrigger` (Z. 17–24): `event Action? Requested`; `RequestStartCheck()` = `Requested?.Invoke()`.
Doku-Kommentar (Z. 8–16) nennt aktuell nur die Pausen-Setzpfade und `CancelAsync`.

`ScmStartCheckService` (BackgroundService, Singleton + HostedService, `App.xaml.cs` Z. 325–326):

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync(ct)` | public | Eine Startprüfung: respektiert `GetScmMonitoringEnabledAsync`, ruft `ScmEventOrchestrator.DispatchDueEventsAsync` in neuem Scope (Z. 95–107). |
| `ExecuteAsync(stoppingToken)` | protected | Wartet `ScmEventStartupGate`, dann `_signal`-Loop mit 250-ms-Sammelphase (`DefaultCollectionPeriod`, Z. 39) und Koaleszierung (Z. 110–139). |
| `OnRequested()` | private | `_signal.Release()`; `SemaphoreFullException`/`ObjectDisposedException` werden geschluckt (Z. 150–158). |
| `OnRunningCountChanged(prev, cur)` | private | Nur `cur < prev` löst aus (Z. 160–167) — deckt Prozess-Enden ab, **nicht** den Übergang in den Wartezustand. |

Abonnierte Events: `ScmStartCheckTrigger.Requested`, `IRunningAutomationStatusSource.RunningCountChanged`.

## `AiExecutionService` — Quelle der Belegungsdaten

Datei: `src/Softwareschmiede/Application/Services/AiExecutionService.cs`
(implementiert `ICliExecutionLimitSource`, `IRunningAutomationStatusSource`; Singleton)

| Methode / Event | Sichtbarkeit | Kurzbeschreibung |
|-----------------|-------------|------------------|
| `GetRunningWorkItemIds()` | public | Aufgaben-IDs mit `!HasExited`-Handle (Z. 135–143). |
| `GetScmPromptSession(workItemId)` | public | `handle.ScmPromptSession` oder `null` (Z. 480–481) — liefert den Adapter je Aufgabe für die Belegungsbewertung. |
| `GetTerminalSession(workItemId)` | public | `ITerminalSession` des Handles (Z. 472–477). |
| `IsRunning` / `GetRunningCount` / `GetLiveRunStatus` | public | Laufstatus (Z. 88–158). |
| `RunningCountChanged` | event | Feuert in `RaiseRunningCountChanged` (Z. 774–785) bei Handle-Registrierung/Entfernung. |
| `CliProcessStatusChanged` / `CliRunActive` | events | Started/Stopped/Error bzw. „laufend bestätigt". |
| Adapter-Anlage | — | `handle.ScmPromptSession = new ScmPromptSessionAdapter(...)` beim Session-Start (Z. 377–379) inkl. `session.InputStarted = adapter.MarkInputStarted`. |

## `ScmEventOrchestrator` — Dispatch und Pending-Muster

Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
(scoped; Ctor nimmt u. a. `ScmEventExecutionLimitService` und optional `ScmStartCheckTrigger?` = null, Z. 15–22)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchDueEventsAsync(ct)` | public | Batch-Auswahl fälliger Ereignisse + `ScmDispatchPriority.OrderForDispatch`, je Ereignis eigener Scope → `DispatchAsync` (Z. 34–66). Aufgerufen von `ScmEventMonitoringService` (Poll) und `ScmStartCheckService`. |
| `DispatchAsync(eventId, explicitPluginPrefix, ct)` | public | Aufgabensperre (`ScmTaskCoordinator`) → `DispatchCoreAsync` (Z. 69–74). |
| `DispatchCoreAsync(...)` | private | Zustellungspfad: Pending-Vorgänger-Prüfung (Z. 182: `x.WorkItemId == item.WorkItemId && x.Sequence < item.Sequence && Status ∉ Endstatus` → `Wartend`), `BlockReason` (beendet/pausiert), Plugin-Auflösung, `CheckAndReserveStartSlotAsync` (Z. 245), CLI-Start, `WaitForInputReadyAsync` → `Ready` gefordert, Kanalprobe, `WritePromptAsync` → `Zugestellt`/`ZustellungUngewiss` (Z. 120–330). |
| `ReconcileAttemptAsync(...)` | private | Abgleich eines begonnenen Versuchs über den Antwortkanal; setzt `InBearbeitung`/`Verarbeitet`/`VerarbeitungUngeklärt` (ab Z. 485; Status-Zuweisungen Z. 499–505, 537, 621–666). **Kein** `RequestStartCheck()` am Endstatus. |
| `CancelAsync(eventId, ct)` | public | Atomarer `ExecuteUpdate` → `Abgebrochen`, Pausen-Rückstufung bei `Verzögert`, dann `notifier.Notify()` + `startCheckTrigger?.RequestStartCheck()` (Z. 422–454). |
| `ResendAsync` / `MutateAndDispatchAsync` | public/private | Neuversand unter Lease (Z. 402–483). |
| `BlockReason(task)` | private | `"Aufgabe beendet/archiviert"` bzw. `"Aufgabe pausiert"` via `IsCurrentlyPaused` (Z. 998–1000). |
| `WaitAsync` / `WaitWithCommentAsync` | private | `Status = "Wartend"` + Reason (+ optionaler Remote-Kommentar) (Z. 1002–1016). |

## Pausen-Setzpfade mit `ScmStartCheckTrigger` (Muster für den neuen Auslöser)

| Klasse / Stelle | Aufruf |
|-----------------|--------|
| `WorkItemService.SetPauseAsync` | `_startCheckTrigger?.RequestStartCheck()` nach `SaveChangesAsync` (Z. 918); optionaler Ctor-Param (Z. 38–57). |
| `ScmEventStore.AcceptBatchAsync` | `startCheckTrigger?.RequestStartCheck()` nach Commit, nur wenn die Verzögerungsdirektive eine Pause setzte (`pauseSet`, Z. 128). |
| `AiPluginLimitService.ProcessRateLimitAsync` | `_startCheckTrigger?.RequestStartCheck()` nach Persistenz der Session-Limit-Pausen (Z. 143). |
| `ScmEventOrchestrator.CancelAsync` | `startCheckTrigger?.RequestStartCheck()` bei `changed != 0` (Z. 451). |

## `ScmEventMonitoringService` — Poll-Sicherheitsnetz

Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

`PeriodicBackgroundService` mit `ScmEventOptions.PollInterval` (Default 60 s,
`ScmEventOptions.cs` Z. 7). `RunOnceAsync` endet mit
`ScmEventOrchestrator.DispatchDueEventsAsync` (Z. 251) — bleibt laut Anforderung als langsamere
Wiederholung bestehen.

## `ScmTaskCompletionMonitoringService` — Pending-Guard-Referenzmuster

Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`

Frühe Rückkehr, sobald ein Ereignis der Aufgabe nicht im Endstatus steht:
`db.ScmIncomingEvents.AnyAsync(x => x.WorkItemId == taskId && x.Status != "Verarbeitet" && x.Status != "Abgebrochen")`
(Z. 84–85, wiederholt unter der Aufgabensperre Z. 133–135). Identisches Muster:
`ScmEventOrchestrator` Z. 182 (ältere Sequence), `ScmEventExchangeService` Z. 27
(`DeliveryAttemptId != null` zusätzlich), `ScmEventOrchestrator.CancelAsync` Z. 427.

## `AiExecutionStatusClassification` — Pausen-Referenz

Datei: `src/Softwareschmiede/Application/Services/AiExecutionStatusClassification.cs`

`IsCurrentlyPaused(this DateTimeOffset?, DateTimeOffset)` = `pausedUntilUtc > now` (Z. 21–22) —
Referenzdefinition, in `GetOccupancyAsync` (Z. 199), `BlockReason` (`ScmEventOrchestrator`
Z. 1000), `WorkItemRecoveryService` Z. 86 und `DevelopmentProcessService` Z. 990 verwendet.

## `ScmDispatchPriority` — unverändert bleibende Priorisierung

Datei: `src/Softwareschmiede/Application/Services/ScmDispatchPriority.cs`

`OrderForDispatch` (Z. 52–60): Klasse 1 Fortsetzungen (`PausedUntilUtc != null` oder
`ExecutionStatus == Completed` auf startbarem Status) → Klasse 2 `Verzögert` nach `NextAttemptUtc`
→ Klasse 3 Sequence.

## `ScmCoordinatedTerminalSession` — Eingabe-Signal

Datei: `src/Softwareschmiede/Infrastructure/Terminal/ScmCoordinatedTerminalSession.cs`

Session-Hülle pro Aufgabe; `InputStarted?.Invoke()` vor jeder `WriteInputAsync`/`WritePromptAsync`
(Z. 21–33) — treibt `MarkInputStarted` → `Busy` auch bei manueller Eingabe.

## `PseudoConsoleSession` — I/O-Ruhe-Heuristik (kein Zählkriterium)

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

`_waitingThreshold` Default 4 s (Z. 125), 1-s-Timer `RefreshRuntimeStatus` (Z. 150, 447–490)
feuert `RuntimeStatusChanged`; `CliRuntimeStatusEvaluator.Determine` (Z. 685–705) liefert
`WaitingForInput` ab Schwellenüberschreitung — laut Anforderung explizit kein
Freigabekriterium, nur Auslösekandidat.
