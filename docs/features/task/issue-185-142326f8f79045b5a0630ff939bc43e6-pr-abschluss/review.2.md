# Plan-Review - PR-Abschluss

Status: `Offene Aufgaben vorhanden`

## Kurzfazit

Die aktuelle Implementierung setzt den Kern des Plans weitgehend um. Persistenzmodell, EF-Migration, Contract-Erweiterungen, GitHub-Status-/Workflow-/Abschlussfunktionen, PR-Persistenz nach Erstellung, Monitoring-Service, DI-Registrierung und der neue `PR`-Bereich in der Aufgaben-Detailansicht sind vorhanden.

Die Befunde aus `review.1.md` und `review-code.1.md` wurden groesstenteils adressiert: retryfaehige Providerfehler bleiben im Polling, `Completed` ist terminal, `ApprovalOnly` wird als nicht gemergter Approval-Zustand modelliert, Post-Merge-Unsicherheit ist sichtbar und `gh run list` wird bei bekannter SHA per `--commit` eingeschraenkt. Der in `test-results.md` dokumentierte fehlschlagende Metadaten-Test ist in der aktuellen Testfassung behoben.

Der Plan ist trotzdem noch nicht vollstaendig umgesetzt, weil mehrere im Plan explizit geforderte Tests und einzelne UI-/Post-Merge-Details fehlen beziehungsweise nicht vollstaendig nachgewiesen sind.

## Gepruefte Umsetzung

| Planbereich | Bewertung | Nachweis |
|-------------|-----------|----------|
| Domain- und Persistenzmodell | Umgesetzt | `PullRequestReferenz`, `PullRequestWorkflowRun`, `Aufgabe.PullRequests`, DbSets, Enum-Konvertierungen, Indizes, Cascade Delete und Migration sind vorhanden. |
| Plugin-Contract | Umgesetzt | `PullRequest` wurde erweitert; Status-, Workflow-Run- und Completion-Value-Objects sowie Default-Methoden in `IGitPlugin`/`GitPluginBase` sind vorhanden. |
| GitHub-Plugin | Groesstenteils umgesetzt | `CreatePullRequestAsync` reichert PR-Metadaten per Statusabfrage an; `GetPullRequestStatusAsync`, `GetPullRequestWorkflowRunsAsync` und `CompletePullRequestAsync` sind implementiert; GitHub-Settings fuer Auto-Abschluss sind vorhanden. |
| Application-Services | Umgesetzt | `PullRequestReferenzService` speichert PRs, laedt PRs je Aufgabe, upsertet Workflow-Runs und persistiert Fehler-/Unsicherheitszustaende; `GitOrchestrationService` speichert nach erfolgreicher PR-Erstellung. |
| Monitoring und Auto-Abschluss | Groesstenteils umgesetzt | `PullRequestMonitoringService` laedt faellige PRs, aktualisiert Status/Runs, versucht Auto-Abschluss nach erfolgreichen Pre-Merge-Runs und behandelt retryfaehige Fehler sowie Approval/Post-Merge-Unsicherheit. |
| Aufgaben-UI | Teilweise umgesetzt | `PR`-Tab, Command, Collection, Ladepfad, Leerzustand, Status-/Merge-/Monitoring-Anzeige, `LastError` und Workflow-Run-Liste sind vorhanden; eigene Lade- und Abruffehlerzustaende fuer den PR-Bereich fehlen. |
| DI und Konfiguration | Umgesetzt | `PullRequestReferenzService` und `PullRequestMonitoringService` sind in `App.xaml.cs` registriert; GitHub-Plugin-Settings sind ergaenzt. |
| Tests | Teilweise umgesetzt | Fokussierte Persistenz-, Monitoring- und GitHubPlugin-Tests sind vorhanden und laufen erfolgreich; ViewModel-/View-Tests sowie einzelne Persistenz- und Abschluss-/Fehlerpfadtests aus dem Plan fehlen. |

## Erledigte Befunde aus den Vorreviews

- Retryfaehige Provider-/CLI-Fehler beenden das Monitoring nicht mehr terminal. `PullRequestMonitoringService` schreibt nicht spezialisierte Exceptions ueber `SetRetryableErrorAsync`; `PullRequestReferenzService` behaelt die Phase und setzt `NextCheckUtc`.
- `Completed` faellt aus der faelligen Monitoring-Menge heraus.
- `ApprovalOnly` wird nicht mehr als gemergter Abschluss behandelt. Das Completion-Result unterscheidet `PullRequestMerged`; der Monitoring-Service setzt bei Approval die Phase `Approved` und ueberwacht weiter.
- Workflow-Runs werden bei bekannter Head- oder Merge-SHA mit `gh run list --commit <sha>` abgefragt.
- Post-Merge-Zuordnung ohne Merge-SHA oder ohne gefundene Runs wird als `PostMergeUncertain` mit `LastError` sichtbar modelliert.
- Der in `test-results.md` dokumentierte Fehler `GitHubPluginTests.PluginMetadata_ShouldExposeExpectedValues` ist behoben; der Test erwartet nun die zusaetzliche Pull-Request-Setting-Gruppe.

## Offene Aufgaben

- ViewModel-/View-Tests aus dem Plan fehlen weiterhin: PR-Tab verfuegbar, PRs und Workflow-Runs werden geladen und angezeigt, Leer-/Fehlerzustaende und Aktualisierung nach PR-Erstellung.
- Persistenztests sind noch nicht vollstaendig: mehrere PRs pro Aufgabe und Cascade Delete von Aufgabe zu PRs beziehungsweise PRs zu Workflow-Runs sind im Plan gefordert, aber nicht durch dedizierte Tests abgedeckt.
- GitHubPlugin-Testabdeckung ist verbessert, aber nicht vollstaendig gemaess Plan: Mapping von `gh pr view`, Merge-/AutoMerge-Argumente und Sanitizing der neuen PR-Fehlerpfade sind nicht vollstaendig als fokussierte Tests sichtbar.
- Monitoring-Testabdeckung ist verbessert, aber nicht vollstaendig gemaess Plan: Pre-Merge fehlgeschlagen, Auto-Abschluss nur bei aktivierter Einstellung, Blockierung bei Berechtigungs-/Bypass-Fehlern und Post-Merge-Erfolgs-/Fehlerphasen sind nicht vollstaendig abgedeckt.
- Die PR-UI hat keinen eigenen Ladezustand und keinen eigenen Abruffehlerzustand fuer das Laden der PR-Liste. Fehler werden global ueber `FehlerMeldung` beziehungsweise pro PR ueber `LastError` angezeigt, decken aber nicht den im Plan genannten PR-spezifischen Lade-/Fehlerzustand ab.
- Post-Merge-Fallback ueber Zielbranch-Runs mit zeitlicher und SHA-Zuordnung ist nicht umgesetzt. Die aktuelle Loesung macht fehlende sichere Zuordnung sichtbar, was den Risikopfad entschärft, ersetzt aber nicht den im Plan beschriebenen Fallback.
- Ein vollstaendiger `dotnet test`-Lauf ist weiterhin nicht erfolgreich abgeschlossen nachgewiesen. `test-results.md` dokumentiert einen Timeout; im Review wurde nur der relevante fokussierte Testfilter erneut erfolgreich ausgefuehrt.

## Pruefung

- `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~PullRequestMonitoringServiceTests|FullyQualifiedName~PullRequestReferenzServiceTests|FullyQualifiedName~GitHubPluginTests" --no-build`
- Ergebnis: 57 bestanden, 0 fehlgeschlagen, 0 uebersprungen.

