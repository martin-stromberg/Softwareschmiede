# Tasks: Durchgängiger automatischer Start und Zustellung automatisch angelegter Aufgaben (Schritt 4)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests | `Monitor_CreatedTaskWithDefaultCli_StartsAndDelivers` — Anlage → Auto-Start → `Zugestellt` mit globalem CLI-Default | Offen | — |
| 2 | Tests | `Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers` — Anlage trotz fehlender CLI, Wartend-Text, kein Duplikat, `RetryAsync` nach Konfiguration → `Zugestellt` | Offen | — |
| 3 | Tests | `Monitor_CreatedTaskWithoutCli_QueueSelectionDelivers` — `OpenFromQueueAsync`-CLI-Auswahl → `Zugestellt` | Offen | — |
| 4 | Tests | `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` (Theory: beendet/archiviert/pausiert) — keine Auto-Starts für blockierte Aufgaben | Offen | — |
| 5 | E2E | E7-Block in `E2E_ScmEvents.cs` erweitern: Wartend „Keine Standard-CLI" → `ScmEventOpen` + `ChoosePlugin` → `Verarbeitet`; Aufgabe gestartet (Klon/Branch/Prefix) | Offen | — |
| 6 | E2E | Hilfsmethoden `SelectEventByTitle` + `WaitEventByTaskAsync` in `E2E_ScmEvents.cs` ergänzen | Offen | — |
| 7 | Entscheidung | „Anlage erfolgt auch bei fehlender Standard-CLI" dokumentiert (plan.md Designentscheidungen → business-rules.md) | Offen | — |
| 8 | Doku | `docs/help/plugins/github-ereignisse/` aktualisieren (Auto-Start/Zustellung angelegter Aufgaben, Entscheidung) | Offen | — |
| 9 | Doku | `changes.log`-Eintrag | Offen | — |
| 10 | Doku | `README.md` prüfen/aktualisieren | Offen | — |
