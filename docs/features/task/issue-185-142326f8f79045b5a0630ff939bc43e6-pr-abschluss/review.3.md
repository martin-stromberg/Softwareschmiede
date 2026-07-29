# Plan-Review - PR-Abschluss

Status: `Offene Aufgaben vorhanden`

## Kurzfazit

Die aktuelle Implementierung setzt den Kern des Plans weitgehend um. Persistenzmodell, EF-Migration, Contract-Erweiterungen, GitHub-Status-/Workflow-/Abschlussfunktionen, PR-Persistenz nach Erstellung, Monitoring-Service, DI-Registrierung und der neue `PR`-Bereich in der Aufgaben-Detailansicht sind vorhanden.

Die Befunde aus `review-code.3.md` sind erledigt: `AutoMerge` wird nach `gh pr merge --auto` nur noch dann als gemergter Abschluss behandelt, wenn die anschliessende Statusabfrage `PullRequestStatus.Merged` liefert, und `WorkflowRunConclusion.Skipped` wird in Pre- und Post-Merge-Phasen konsistent als neutral erfolgreicher Abschluss bewertet.

Der Plan ist trotzdem noch nicht vollstaendig umgesetzt, weil einzelne im Plan explizit genannte Test- und UI-/Fallback-Details nicht vollstaendig nachgewiesen beziehungsweise umgesetzt sind und der vollstaendige Testlauf weiterhin nur als Timeout dokumentiert ist.

## Gepruefte Umsetzung

| Planbereich | Bewertung | Nachweis |
|-------------|-----------|----------|
| Domain- und Persistenzmodell | Umgesetzt | `PullRequestReferenz`, `PullRequestWorkflowRun`, `Aufgabe.PullRequests`, DbSets, Enum-Konvertierungen, Indizes, Cascade Delete und Migration sind vorhanden. |
| Plugin-Contract | Umgesetzt | `PullRequest` wurde erweitert; Status-, Workflow-Run- und Completion-Value-Objects sowie Default-Methoden in `IGitPlugin`/`GitPluginBase` sind vorhanden. |
| GitHub-Plugin | Groesstenteils umgesetzt | `CreatePullRequestAsync` reichert PR-Metadaten per Statusabfrage an; `GetPullRequestStatusAsync`, `GetPullRequestWorkflowRunsAsync` und `CompletePullRequestAsync` sind implementiert; GitHub-Settings fuer Auto-Abschluss sind vorhanden. |
| Application-Services | Umgesetzt | `PullRequestReferenzService` speichert PRs, laedt PRs je Aufgabe, upsertet Workflow-Runs und persistiert Fehler-/Unsicherheitszustaende; `GitOrchestrationService` speichert nach erfolgreicher PR-Erstellung. |
| Monitoring und Auto-Abschluss | Groesstenteils umgesetzt | `PullRequestMonitoringService` laedt faellige PRs, aktualisiert Status/Runs, versucht Auto-Abschluss nach erfolgreichen Pre-Merge-Runs und behandelt retryfaehige Fehler, Approval-/AutoMerge-Wartezustaende sowie Post-Merge-Unsicherheit. |
| Aufgaben-UI | Teilweise umgesetzt | `PR`-Tab, Command, Collection, Ladepfad, Leerzustand, Status-/Merge-/Monitoring-Anzeige, `LastError` und Workflow-Run-Liste sind vorhanden; eigene Lade- und Abruffehlerzustaende fuer den PR-Bereich fehlen. |
| DI und Konfiguration | Umgesetzt | `PullRequestReferenzService` und `PullRequestMonitoringService` sind in `App.xaml.cs` registriert; GitHub-Plugin-Settings sind ergaenzt. |
| Tests | Teilweise umgesetzt | Fokussierte Persistenz-, Monitoring- und GitHubPlugin-Tests sind vorhanden und laufen erfolgreich; ViewModel-/View-Tests sowie einzelne Persistenz- und Fehlerpfadtests aus dem Plan fehlen weiterhin. |

## Erledigte Befunde aus `review-code.3.md`

- `AutoMerge` ist nicht mehr faelschlich terminal: `GitHubPlugin.CompletePullRequestAsync` ruft nach erfolgreichem `gh pr merge --auto` den PR-Status ab und gibt `WaitingForMerge(...)` zurueck, wenn der PR noch nicht `Merged` ist.
- Das Monitoring persistiert nicht gemergte AutoMerge-Ergebnisse als nicht-terminalen Wartezustand `Approved` mit Hinweistext und setzt eine neue `NextCheckUtc`, statt `Completed` zu setzen.
- `WorkflowRunConclusion.Skipped` wird ueber `IsSuccessfulConclusion(...)` gemeinsam mit `Success` als erfolgreicher Abschluss bewertet.
- `AllSucceeded(...)` und `AnyFailed(...)` verwenden dieselbe Erfolgsdefinition; dadurch werden geskipptete Runs in Pre-Merge- und Post-Merge-Phasen konsistent behandelt.
- Neue fokussierte Tests decken AutoMerge-ohne-Merge, geskipptete Pre-Merge-Runs und geskipptete Post-Merge-Runs ab.

## Offene Aufgaben

- ViewModel-/View-Tests aus dem Plan fehlen weiterhin: PR-Tab verfuegbar, PRs und Workflow-Runs werden geladen und angezeigt, Leer-/Fehlerzustaende und Aktualisierung nach PR-Erstellung sind nicht als dedizierte PR-UI-Tests nachgewiesen.
- Persistenztests sind noch nicht vollstaendig: mehrere PRs pro Aufgabe und Cascade Delete von Aufgabe zu PRs beziehungsweise PRs zu Workflow-Runs sind im Plan gefordert, aber nicht durch dedizierte Tests abgedeckt.
- GitHubPlugin-Testabdeckung ist verbessert, aber nicht vollstaendig gemaess Plan: Sanitizing der neuen PR-Fehlerpfade und alle Merge-/Bypass-Fehlerpfade sind nicht vollstaendig als fokussierte Tests sichtbar.
- Monitoring-Testabdeckung ist verbessert, aber nicht vollstaendig gemaess Plan: Pre-Merge-Fehlschlag, Auto-Abschluss nur bei deaktivierter Einstellung, Blockierung bei Berechtigungs-/Bypass-Fehlern und Post-Merge-Fehlerphase sind nicht vollstaendig abgedeckt.
- Die PR-UI hat keinen eigenen Ladezustand und keinen eigenen Abruffehlerzustand fuer das Laden der PR-Liste. Fehler werden global ueber `FehlerMeldung` beziehungsweise pro PR ueber `LastError` angezeigt, decken aber nicht den im Plan genannten PR-spezifischen Lade-/Fehlerzustand ab.
- Post-Merge-Fallback ueber Zielbranch-Runs mit zeitlicher und SHA-Zuordnung ist nicht umgesetzt. Die aktuelle Loesung macht fehlende sichere Zuordnung als `PostMergeUncertain` sichtbar, ersetzt aber nicht den im Plan beschriebenen Fallback.
- Ein vollstaendiger `dotnet test`-Lauf ist weiterhin nicht erfolgreich abgeschlossen nachgewiesen. `test-results.md` dokumentiert einen Timeout; aktuell wurde nur der relevante fokussierte Testfilter erneut erfolgreich ausgefuehrt.

## Pruefung

- `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~PullRequestMonitoringServiceTests|FullyQualifiedName~PullRequestReferenzServiceTests|FullyQualifiedName~GitHubPluginTests" --no-build`
- Ergebnis: 61 bestanden, 0 fehlgeschlagen, 0 uebersprungen.

## Todo

Schritt 7 ist in `todo.md` bereits als erledigt markiert und bleibt erledigt.
