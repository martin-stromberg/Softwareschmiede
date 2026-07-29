# Code-Review - PR-Abschluss

Status: `Befunde vorhanden`

## Befunde

1. `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs:995` / `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs:1021` / `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs:142`
   Die Strategie `AutoMerge` wird nach einem erfolgreichen `gh pr merge --auto` als gemergter Abschluss behandelt, obwohl GitHub damit Auto-Merge nur aktivieren kann und der Pull Request danach weiterhin offen sein kann. `CompletePullRequestAsync` ruft zwar danach `GetPullRequestStatusAsync` auf, ignoriert aber `status.Status` und gibt immer `PullRequestCompletionResult.Completed(...)` mit `PullRequestMerged = true` zurueck. `TryCompleteAsync` setzt die Phase anschliessend auf `Completed`; `GetDueForMonitoringAsync` filtert `Completed` aus dem Polling. Ergebnis: Ein PR mit aktiviertem Auto-Merge kann lokal terminal abgeschlossen wirken, ohne gemergt zu sein, und Post-Merge-Actions werden nie beobachtet. Der AutoMerge-Pfad sollte nur bei `PullRequestStatus.Merged` als gemergt gelten; andernfalls braucht er einen nicht-terminalen Wartezustand.

2. `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs:210` / `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs:215` / `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs:218`
   `WorkflowRunConclusion.Skipped` wird inkonsistent bewertet. `AllSucceeded` akzeptiert nur `Success`, `AnyFailed` ignoriert aber `Skipped`. Bei Pre-Merge-Runs fuehrt ein abgeschlossener, geskippteter Run deshalb zu `Failed`; bei Post-Merge-Runs fuehrt derselbe Zustand zu `PostMergeRunning`, weil weder Erfolg noch Fehler erkannt wird. In GitHub Actions sind `skipped`-Conclusions normale Endzustaende, z. B. bei bedingten Jobs. Dadurch koennen PRs faelschlich blockiert werden oder Post-Merge-Monitoring dauerhaft laufen. Die Statuspolicy sollte explizit festlegen, ob `Skipped` neutral, erfolgreich oder blockierend ist, und beide Phasen gleich behandeln.

## Testluecken

- Die offenen Plan-Review-Punkte bleiben relevant: ViewModel-/View-Tests fuer PR-Tab, Lade-/Leer-/Fehlerzustaende und Aktualisierung nach PR-Erstellung fehlen.
- Persistenztests fuer mehrere PRs pro Aufgabe und Cascade Delete Aufgabe -> PR -> WorkflowRuns fehlen weiterhin.
- GitHubPlugin-Tests decken `ApprovalOnly` und `--commit` ab, aber nicht den fehlerhaften `AutoMerge`-Statuspfad nach `gh pr merge --auto`.
- Monitoring-Tests decken keine `Skipped`-Conclusions und keine AutoMerge-Wartephase ab.
- Ein vollstaendiger `dotnet test`-Lauf ist weiterhin nicht erfolgreich nachgewiesen; dokumentiert sind fokussierte erfolgreiche Testlaeufe und ein frueherer Timeout.

## Gepruefte Hinweise

- Die frueheren Befunde zu retryfaehigen Providerfehlern, `Completed` als terminale Phase, `ApprovalOnly` als nicht gemergter Zustand und SHA-spezifischem `gh run list --commit` sind in der aktuellen Implementierung adressiert.
- `PullRequestReferenzService` und `PullRequestMonitoringService` sind in `App.xaml.cs` registriert.
- Der PR-Bereich wird nach PR-Erstellung ueber `LadenAsync` neu geladen und zeigt gespeicherte Referenzen inklusive Workflow-Runs an.
