# Tasks: Durchgängiger automatischer Start und Zustellung automatisch angelegter Aufgaben (Schritt 4)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests | `Monitor_CreatedTaskWithDefaultCli_StartsAndDelivers` — Anlage → Auto-Start → `Zugestellt` mit globalem CLI-Default | Erledigt | `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_CreatedTaskWithDefaultCli_StartsAndDelivers` |
| 2 | Tests | `Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers` — Anlage trotz fehlender CLI, Wartend-Text, kein Duplikat, `RetryAsync` nach Konfiguration → `Zugestellt` | Erledigt | `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers` |
| 3 | Tests | `Monitor_CreatedTaskWithoutCli_QueueSelectionDelivers` — `OpenFromQueueAsync`-CLI-Auswahl → `Zugestellt` | Erledigt | `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_CreatedTaskWithoutCli_QueueSelectionDelivers` |
| 4 | Tests | `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` (Theory: beendet/archiviert/pausiert) — keine Auto-Starts für blockierte Aufgaben | Erledigt | `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` |
| 5 | E2E | E7-Block in `E2E_ScmEvents.cs` erweitern: Wartend „Keine Standard-CLI" → `ScmEventOpen` + `ChoosePlugin` → `Verarbeitet`; Aufgabe gestartet (Klon/Branch/Prefix) | Erledigt | `ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` (E8-Abschnitt) |
| 6 | E2E | Hilfsmethoden `SelectEventByTitle` + `WaitEventByTaskAsync` in `E2E_ScmEvents.cs` ergänzen | Erledigt | `ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` (E8-Abschnitt) |
| 7 | Entscheidung | „Anlage erfolgt auch bei fehlender Standard-CLI" dokumentiert (plan.md Designentscheidungen → business-rules.md) | Erledigt | `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers` |
| 8 | Doku | `docs/help/plugins/github-ereignisse/` aktualisieren (Auto-Start/Zustellung angelegter Aufgaben, Entscheidung) | Erledigt | Kein direkter Test (Dokumentation) |
| 9 | Doku | `changes.log`-Eintrag | Erledigt | Kein direkter Test (Dokumentation) |
| 10 | Doku | `README.md` prüfen/aktualisieren | Erledigt | Kein direkter Test (Dokumentation) |
