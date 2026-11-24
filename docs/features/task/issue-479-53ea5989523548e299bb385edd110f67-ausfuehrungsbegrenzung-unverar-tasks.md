# Tasks: Ausführungsbegrenzung — hängende Ereignisse halten Startplatz belegt (Issue #479)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `ScmIncomingEventStatus.StartSlotFreeing`-Menge ergänzen (7 freigebende Stati) | Offen | — |
| 2 | Logik | `ScmEventExecutionLimitService.GetOccupancyAsync`: Pending-Abfrage auf `!StartSlotFreeing` umstellen + XML-Doku | Offen | — |
| 3 | Logik | `ScmEventOrchestrator`: `UndeliverableReason` + `MarkUndeliverableAsync` hinzufügen | Offen | — |
| 4 | Logik | `ScmEventOrchestrator.DispatchCoreAsync`: Guard für manuell klärbare Stati ohne `DeliveryAttemptId`; `task is null`/`blocked`-Stellen auf `UndeliverableReason` umstellen | Offen | — |
| 5 | Logik | `ScmEventOrchestrator.DispatchPreparationAsync`: beide `task`-/`blocked`-Stellen auf `UndeliverableReason` umstellen | Offen | — |
| 6 | Logik | `ScmEventOrchestrator.OpenFromQueueAsync`: `Include(GitRepository)` + `UndeliverableReason` vor `BlockReason` | Offen | — |
| 7 | Tests | `ScmEventExecutionLimitServiceTests`: Pending-Theory auf In-Flight splitten; neue Theory für freigebende Stati; `NextAttemptUtc`-Fall | Offen | — |
| 8 | Tests | `ScmIncomingEventStatusTests`: `StartSlotFreeing`-Mengen- und Klassifikationstests | Offen | — |
| 9 | Tests | `ScmEventDispatchTests_Limit`: `Dispatch_EndedOrArchivedTask_StaysBlocked` → `VerarbeitungUngeklärt`; neue Tests (fehlende Repository-Zuordnung, entfernte Aufgabe, Ready + nur klärbares Ereignis → Platz frei) | Offen | — |
| 10 | Tests | `ScmEventMonitoringServiceTests_AutoStartDelivery`: `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` auf neuen Endzustand anpassen | Offen | — |
| 11 | Dokumentation | Hilfe-Doku (`docs/help/plugins/github-ereignisse/business-rules.md` o. ä.) zur geänderten Platzbelegung aktualisieren | Offen | — |
| 12 | Dokumentation | `docs/RELEASE_NOTES.md` ergänzen | Offen | — |
