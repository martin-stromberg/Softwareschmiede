# Bestandsaufnahme: Logik

## `ScmEventExecutionLimitService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckAndReserveStartSlotAsync` | public | Atomare Limit-Prüfung + Platzbelegung unter `reservationPool.AdmissionGate` (SemaphoreSlim); Reservierung über `ScmStartSlotReservationPool` (DI-Singleton), Lebensdauer `ReservationLifetime` = 5 min. |
| `GetStartBlockReasonAsync` | public | Reine Limit-Prüfung ohne Platzbelegung (ebenfalls unter `AdmissionGate`). |
| `GetStartBlockReason` | internal static | Reine Auswertung: globales Limit zuerst, dann Plugin-Limit; liefert benutzerlesbaren Wartegrund. |
| `GetLimitsAsync` | private | Liest `scm.execution.maxparallel` + `plugins.maxparallel.<prefix>` aus `AppSettingService`; ungültig/<1 = unbegrenzt. |
| `EffectiveOccupancyAsync` | private | `GetOccupancyAsync` + aktive Reservierungen (verfallene werden entfernt). Muss unter `AdmissionGate` laufen. |
| `ParseLimit` | private static | `int.TryParse` + `>= 1`. |
| `GetOccupancyAsync` | private | Zählt laufende CLI-Handles global + je Plugin. Freigaberegeln: (1) `PausedUntilUtc` in Zukunft → zählt nicht; (2) lebende SCM-Promptsitzung mit `ReadyStatus == Ready` **und** keinem offenen Ereignis → zählt nicht. Pending-Abfrage (Z. 218–223): `Status != "Verarbeitet" && Status != "Abgebrochen"` als String-Literale — jeder andere Status (inkl. `VerarbeitungUngeklärt`, `ZustellungUngewiss`, `Wartend`, `Verzögert`, `Aufgenommen`, unbekannte) blockiert. Plugin-Attribution: lebende Sitzung über `session.PluginPrefix`, sonst `WorkItem.AiPluginPrefix`. Handles ohne Aufgabenzeile zählen global voll. |

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchDueEventsAsync` | public | Batch-Auswahl fälliger Ereignisse: `Status != Abgebrochen && ((Status != Verarbeitet && (NextAttemptUtc == null || <= now)) || offene Outbox-Aktionen)`; priorisiert über `ScmDispatchPriority.OrderForDispatch`; ruft `DispatchAsync` je Ereignis in eigenem Scope. |
| `DispatchAsync` | public | Leased pro Aufgabe (`ScmTaskCoordinator.Shared`) → `DispatchCoreAsync`. |
| `OpenFromQueueAsync` | public | UI-Pfad „aus Queue öffnen": claimt Aufgabenlease, flusht `BlockReason` → `WaitAsync`, ruft `DispatchCoreAsync` mit explizitem Prefix. |
| `DispatchCoreAsync` | private | Generischer Zustellpfad: Hostaktionen flushen → `Verarbeitet`-Abbruch → `DeliveryAttemptId != null` → `ReconcileAttemptAsync` → `NextAttemptUtc`-Fälligkeit → Vorgängerprüfung (`NonBlocking`) → Aufgabe laden (`task is null` → `WaitAsync`) → `BlockReason` → `WaitWithCommentAsync` → Repository/Trigger-Validierung → Sitzung: `adapter is null \|\| !adapter.IsAlive` → Prefix-Auflösung + `CheckAndReserveStartSlotAsync` + CLI-Start → `WaitForInputReadyAsync` → Stabilisierungsfrist → `CheckExchangeAccessAsync` → `exchange.PrepareAsync` → `ZustellungVorbereitet` → `WritePromptAsync` → `Zugestellt` / `ZustellungUngewiss`. |
| `DispatchPreparationAsync` | private | Dedizierter Pfad für `Purpose == ScmIssuePreparationService.Purpose`; spiegelt den generischen Pfad (gleiche Startplatz-Wache `adapter is null \|\| !adapter.IsAlive` → `CheckAndReserveStartSlotAsync`). |
| `ReconcileAttemptAsync` | private | Antwortabgleich eines begonnenen Versuchs; Fehler → `VerarbeitungUngeklärt`. |
| `ActivateForOpenAsync` | public | `BlockReason(task) is null && Status == New` → `Started`. |
| `RetryAsync` / `ConfirmReadyAsync` / `ResendAsync` / `CancelAsync` | public | Mutationspfade: `ResendAsync` setzt `ZustellungUngewiss`/`VerarbeitungUngeklärt` → `Aufgenommen`; `CancelAsync` → `Abgebrochen` via `ExecuteUpdate`. |
| `BlockReason` | private | `Completed`/`Archived` → „Aufgabe beendet/archiviert; Start gesperrt."; `PausedUntilUtc` aktiv → „Aufgabe pausiert; Start gesperrt."; sonst `null`. |
| `WaitAsync` | private | `Status = Wartend`, `Reason`, `SaveChanges` (kein `NextAttemptUtc`-Reset nötig — bleibt wie gesetzt bzw. `null`). |
| `WaitWithCommentAsync` | private | `WaitAsync` + deterministische Host-Wartekommentar-Outbox-Aktion. |
| `IsCancelledAsync` | private | Liest persistierten Status gegen Abbruch. |

`task is null`-Stellen: generischer Pfad Z. 200 (`WaitAsync`), Aufbereitung Z. 687 (`WaitAsync`). `task.GitRepositoryId!.Value` wird in beiden Pfaden ohne Null-Prüfung dereferenziert (generisch Z. 205, Aufbereitung Z. 699) → `InvalidOperationException` → generischer Catch → `NextAttemptUtc = long.MaxValue` + `Wartend`. Post-Reload-`BlockReason`-Prüfungen: Z. 286 (generisch, `WaitWithCommentAsync`), Z. 774 (Aufbereitung, `WaitAsync`).

## `ScmTaskCompletionMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| Abschlussprüfung (Z. 133, 184) | private | `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` → blockierende Ereignisse verhindern Abschluss. Nutzt `NonBlocking` bereits korrekt — unverändert. |

## `ScmEventStore`
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ClaimTaskAsync`/`ReleaseTaskAsync`/`RenewTaskAsync` | public | Aufgabenlease (`ScmTaskLease`) serialisiert Dispatch je Aufgabe. |
| `Observation` / `ActionId` / `RetryAt` | public static | Deserialisierung, deterministische Aktions-IDs, Exponential-Backoff. |

## `ScmStartSlotReservationPool`
Datei: `src/Softwareschmiede/Application/Services/ScmStartSlotReservationPool.cs`

DI-Singleton: `AdmissionGate` (Semaphore), `Reserve`/`ActiveReservations` mit `ExpiryUtcMs`.

## `WorkItemService.CompleteAsync`
Datei: `src/Softwareschmiede/Application/Services/WorkItemService.cs` (Z. 929–957)

Setzt `Status = Completed`, `ExecutionStatus = Completed`, leert `GitWorkspace` (Branch/Klonpfad), `ActiveRunId`, `LastHeartbeatUtc`, `LastCliStartUtc`, `RunStatus` — danach greift `BlockReason` → „beendet/archiviert".
