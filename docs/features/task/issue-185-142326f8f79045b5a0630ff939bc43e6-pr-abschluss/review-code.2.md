# Code-Review - PR-Abschluss

Status: `Befunde vorhanden`

## Befunde

1. `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs:106` / `src/Softwareschmiede/Application/Services/PullRequestReferenzService.cs:91`
   Transiente Provider- oder CLI-Fehler beenden das Monitoring dauerhaft. `MonitorAsync` schreibt jede nicht spezialisierte Exception als Phase `Failed`; `GetDueForMonitoringAsync` schliesst `Failed` anschliessend aus der faelligen Menge aus. Ein einzelner Netzwerkfehler, Rate-Limit, gh-CLI-Problem oder kurzfristiger GitHub-Ausfall sorgt damit dafuer, dass der PR nie wieder aktualisiert wird. Das widerspricht der geforderten Ueberwachung gespeicherter PRs und macht auch Post-Merge-Actions nach einem transienten Fehler unsichtbar. Fehler sollten retrybar bleiben, z. B. mit `LastError` plus `NextCheckUtc`, und nur fachlich terminale Zustaende sollten aus dem Polling fallen.

2. `src/Softwareschmiede/Application/Services/PullRequestReferenzService.cs:91` / `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs:154`
   `Completed` wird nicht als terminaler Zustand behandelt. `DeterminePhase` liefert fuer gemergte PRs ohne zugeordnete Post-Merge-Runs `Completed`, aber `GetDueForMonitoringAsync` filtert `Completed` nicht heraus. Dadurch werden abgeschlossene PRs alle fuenf Minuten weiter gepollt. Falls keine Post-Merge-Runs existieren oder GitHub sie nicht per Merge-SHA liefert, entsteht dauerhafte Hintergrundlast und die Datenbank bleibt in einem Zustand, der zwar fertig klingt, aber nie aus dem Monitoring faellt. Der Zustandsautomat braucht eine klare Trennung zwischen "warte noch auf Post-Merge-Runs" und terminal abgeschlossen.

3. `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs:976` / `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs:128`
   Die Strategie `ApprovalOnly` wird als erfolgreicher PR-Abschluss persistiert, obwohl der Pull Request danach offen bleibt. `CompletePullRequestAsync` gibt nach `gh pr review --approve` ein Success-Ergebnis zurueck; `TryCompleteAsync` setzt daraufhin die Monitoring-Phase auf `Completed`. Beim naechsten Poll kann der offene PR wieder als `PreMergeSucceeded` erkannt werden und bei aktivierter Automatik erneut approven. Das fuehrt je nach GitHub-Antwort zu wiederholten Approval-Versuchen oder einem irrefuehrenden lokalen Abschlussstatus. `ApprovalOnly` sollte entweder als eigener nicht-terminaler Zustand modelliert werden oder nach dem Approval nicht als abgeschlossener Merge-/Post-Merge-Pfad gelten.

4. `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs:941` / `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs:1068`
   Die Workflow-Zuordnung fragt `gh run list` unscoped fuer die letzten 100 Runs ab und filtert danach nur lokal nach `headSha` beziehungsweise `mergeCommitSha`. Bei aktiven Repositories koennen relevante PR- oder Post-Merge-Runs aus der 100er-Liste herausfallen, bevor sie erfasst werden. Der PR-Bereich zeigt dann "keine Actions" oder bleibt auf `PreMergeRunning`/`Completed`, obwohl zugeordnete Runs existieren. Die Abfrage sollte branch-/event-/SHA-nah erfolgen oder bei fehlender Merge-SHA einen expliziten Unsicherheitszustand setzen, wie im Plan beschrieben.

## Testluecken

- Es fehlen Unit-Tests fuer `PullRequestMonitoringService`, insbesondere transiente Fehler, terminale Phasen, `Completed`, `ApprovalOnly` und Auto-Abschluss-Wiederholungen.
- Es fehlen GitHubPlugin-Tests fuer `gh pr view`, `gh run list`, Merge-/Approval-/AutoMerge-Argumente und Sanitizing der neuen Fehlerpfade.
- Es fehlen ViewModel-/UI-Tests fuer PR-Lade-, Leer- und Fehlerzustaende sowie Aktualisierung nach PR-Erstellung.
- Der volle `dotnet test` ist laut Implementierungsbericht nicht abgeschlossen, sondern nach 180 Sekunden in den Timeout gelaufen.

## Gepruefte Hinweise

- Die neuen Services sind in `App.xaml.cs` registriert.
- Der PR-Tab laedt gespeicherte Pull Requests ueber `PullRequestReferenzService`.
- Andere Git-Plugins bleiben durch Default-Methoden im Contract grundsaetzlich buildbar, solange sie von `IGitPlugin` beziehungsweise `GitPluginBase` ausgehen.
