# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `ScmIncomingEventStatus.StartSlotFreeing` (`IReadOnlyCollection<string>`) — angelegt mit den sieben freigebenden Stati `{Aufgenommen, Verzögert, Wartend, ZustellungUngewiss, VerarbeitungUngeklärt, Verarbeitet, Abgebrochen}` inkl. XML-Doku (`src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs`)
- [x] `ScmEventExecutionLimitService.GetOccupancyAsync` — Pending-Abfrage auf `!ScmIncomingEventStatus.StartSlotFreeing.Contains(x.Status)` umgestellt; String-Literale ersetzt; XML-Doku von Record und Methode an die In-Flight-Regel angepasst
- [x] `ScmEventOrchestrator.UndeliverableReason` — private Hilfsfunktion, prüft beendet/archiviert und fehlende `GitRepositoryId` (Signatur nimmt `WorkItem`, siehe Hinweise)
- [x] `ScmEventOrchestrator.MarkUndeliverableAsync` — setzt `VerarbeitungUngeklärt` + `Reason` + `NextAttemptUtc`, speichert (Wert `long.MaxValue` statt Plan-`null`, siehe Hinweise)
- [x] `DispatchCoreAsync`: Guard für `VerarbeitungUngeklärt`/`ZustellungUngewiss` ohne `DeliveryAttemptId` nach dem `Verarbeitet`-Check (nach Hostaktionen-Flush, vor Reconcile)
- [x] `DispatchCoreAsync`: `task is null` → `MarkUndeliverableAsync` (Konstante `RemovedTaskUndeliverableReason`); `UndeliverableReason` vor `BlockReason` an beiden `blocked`-Stellen (vor und nach `ReloadAsync`)
- [x] `DispatchPreparationAsync`: gleiche Ersetzung an beiden `task`-/`blocked`-Stellen
- [x] `OpenFromQueueAsync`: `SingleOrDefaultAsync` + `UndeliverableReason` vor `BlockReason` (entfernte Aufgabe wird klärbar statt `SingleAsync`-Ausnahme)
- [x] `ScmEventExecutionLimitServiceTests`: Theory in In-Flight (`GetStartBlockReasonAsync_ReadySessionInFlightEvent_CountsAsOccupied`, inkl. unbekanntem Status) und freigebend (`GetStartBlockReasonAsync_ReadySessionNotYetDeliveredEvent_FreesSlot`) gesplittet; `AddEventAsync` um `nextAttemptUtc` erweitert; `GetStartBlockReasonAsync_ReadySessionWaitingWithMaxAttempt_FreesSlot` neu
- [x] `ScmIncomingEventStatusTests`: `StartSlotFreeing_ShouldContainExactlyTheNonOccupyingStatuses`, `StartSlotFreeing_ShouldExcludeOccupyingStatuses` (inkl. unbekannt), `StartSlotFreeing_WartendIsDecoupledFromNonBlocking`
- [x] `ScmEventDispatchTests_Limit`: `Dispatch_EndedOrArchivedTask_MarkedUndeliverable` (Theory, inkl. Idempotenz), `Dispatch_RemovedTask_MarkedUndeliverable`, `Dispatch_TaskWithoutRepository_MarkedUndeliverable`, `Dispatch_ReadySessionWaitingEvent_SlotFreedForNextStart`, `Dispatch_ReadySessionUnclarifiedEvent_SlotFreedForNextStart` (decken das geplante `Dispatch_TaskReadyWithUndeliverableOnlyEvent_SlotFreed` ab)
- [x] `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_ReusedBlockedTask_StaysWaitingWithoutStart`: Completed/Archived → `VerarbeitungUngeklärt`, New+paused → `Wartend`
- [x] `ScmEventOrchestratorTests.BlockedTask_IsNeverActivated`: Erwartung differenziert (während Testlauf gefundener weiterer Altbestand)
- [x] `E2E_ScmEvents`: Wartestati für beendete/archivierte Aufgaben auf `VerarbeitungUngeklärt` aktualisiert

## Offene Aufgaben

Keine.

## Hinweise

- **Abweichung 1 (bewusst):** `UndeliverableReason` nimmt `WorkItem` statt `WorkItem?` — der `task is null`-Fall läuft über die Konstante `RemovedTaskUndeliverableReason` an den Ladestellen; so bleibt der Compiler-Null-Nachweis für `BlockReason` erhalten.
- **Abweichung 2 (bewusst):** Die im Plan genannte Prüfung `GitRepository is null` (Navigation) entfällt — `UndeliverableReason` prüft nur das Skalar `GitRepositoryId`, damit die Nach-`ReloadAsync`-Aufrufstellen kein falsch-positives Ergebnis aus einer zurückgesetzten Navigation riskieren. Der Restfall (dangling FK, `GitRepositoryId` gesetzt, Zeile fehlt) läuft weiter über den generischen `catch` in `Wartend` mit `long.MaxValue` — platzmäßig unproblematisch.
- **Abweichung 3 (bewusst):** `MarkUndeliverableAsync` setzt `NextAttemptUtc = long.MaxValue` statt `null` — `DispatchDueEventsAsync` würde das Ereignis sonst jeden Poll-Zyklus erneut auswählen (nur early-return-Rauschen). `long.MaxValue` ist die vorhandene Konvention für „kein automatischer Neuversand"; `RetryAsync` setzt explizit zurück.
- **Ergänzung über Plan hinaus:** `RetryAsync` setzt `VerarbeitungUngeklärt`/`ZustellungUngewiss` ohne `DeliveryAttemptId` auf `Aufgenommen` zurück — sonst wäre „Erneut prüfen" für unzustellbar markierte Ereignisse ein No-Op (Wiederaufnahme-Pfad aus Akzeptanzszenario 5).
- **Ergänzung über Plan hinaus:** `Monitor_GlobalLimitOne_ContinuationWinsSlotOverSequence` zeigte im Testlauf die neue, gewollte Platzfreigabe (Ready-Sitzung + `Wartend` auf Stabilisierungsfrist → Platz frei): Erwartung auf das neue Semantikbild angepasst (Priorität gewinnt den ersten Start, reguläres Ereignis darf ebenfalls starten).
- **Ergänzung über Plan hinaus:** Die ereignisgesteuerte Startprüfung am Dispatch-Ende wird jetzt auch bei `VerarbeitungUngeklärt`/`ZustellungUngewiss` ausgelöst — beide Stati geben den Platz frei, wartende Ereignisse sollen nicht erst auf den Poll warten.
