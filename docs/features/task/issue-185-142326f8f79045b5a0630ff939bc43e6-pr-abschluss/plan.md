# Umsetzungsplan - PR-Abschluss

## Zielbild

Pull Requests, die aus einer Aufgabe heraus erstellt werden, werden dauerhaft an der Aufgabe gespeichert, im neuen Aufgabenbereich `PR` angezeigt und durch einen Hintergrundprozess aktualisiert. Fuer GitHub werden PR-Status, relevante Workflow-/Action-Status, automatische Abschlussversuche und Post-Merge-Actions abgebildet.

Der initiale Scope bleibt bewusst GitHub. Die Daten- und Contract-Erweiterungen werden providerneutral genug benannt, damit spaetere Provider nicht die Aufgaben-UI neu aufbrechen muessen.

## Arbeitsannahmen

1. "Bestaetigt" wird fuer die erste Umsetzung als "Pull Request abschliessen" modelliert. Die Standardstrategie ist `Merge`; `ApprovalOnly` und `AutoMerge` koennen als konfigurierte Strategien vorbereitet werden, muessen aber GitHub-API-Fehler sichtbar melden, falls Rechte oder Branch-Protection-Regeln den Weg blockieren.
2. Relevante Pre-Merge-Actions sind initial die GitHub-Runs/Checks, die dem PR-Head-SHA zugeordnet werden koennen. Required-Checks aus Branch Protection werden als spaetere Praezisierung vorbereitet, aber nicht zwingend fuer den ersten funktionsfaehigen Stand vorausgesetzt.
3. Mehrere Pull Requests pro Aufgabe werden unterstuetzt und gleichwertig angezeigt.
4. Post-Merge-Actions werden ueber den Merge-Commit-SHA beziehungsweise, falls GitHub diesen nicht liefert, ueber Zielbranch-Runs mit passender zeitlicher und SHA-Zuordnung verfolgt. Unsichere Zuordnungen werden als Fehler-/Hinweiszustand gespeichert statt still geraten.
5. Bypass bedeutet nicht normales Self-Approval. Die Implementierung versucht nur konfigurierte Abschlusswege und macht fehlende Berechtigungen oder Schutzregeln als blockierten Status sichtbar.

## Umsetzungsschritte

### 1. Domain- und Persistenzmodell ergaenzen

- Neue Entities im Domain-Projekt anlegen:
  - `PullRequestReferenz` fuer die Aufgabe-gebundene PR-Referenz.
  - `PullRequestWorkflowRun` fuer die zugeordneten GitHub-Actions-/Workflow-Runs.
- Enums ergaenzen, jeweils mit XML-Dokumentation:
  - `PullRequestProvider` mit initial `GitHub`.
  - `PullRequestStatus`, z. B. `Unknown`, `Open`, `Closed`, `Merged`.
  - `PullRequestMergeStatus`, z. B. `Unknown`, `Mergeable`, `Blocked`, `Conflicting`, `Merged`.
  - `PullRequestMonitoringPhase`, z. B. `Created`, `PreMergeRunning`, `PreMergeSucceeded`, `Completing`, `Completed`, `PostMergeRunning`, `PostMergeSucceeded`, `PostMergeFailed`, `Blocked`, `Failed`.
  - `WorkflowRunStatus` und `WorkflowRunConclusion` passend zu GitHub-Statuswerten.
- `Aufgabe` um eine Collection `PullRequests` erweitern.
- `SoftwareschmiededDbContext` um `DbSet<PullRequestReferenz>` und `DbSet<PullRequestWorkflowRun>` erweitern.
- EF-Konfiguration:
  - Cascade Delete von Aufgabe zu PRs und von PRs zu Workflow-Runs.
  - String-Konvertierungen fuer Enums.
  - Indizes fuer `AufgabeId`, `Provider/RepositoryId/PullRequestNumber`, `MonitoringPhase/LastCheckedUtc` und `ProviderRunId`.
- EF-Migration erstellen und Snapshot aktualisieren.

### 2. Plugin-Contract fuer PR-Status und Abschluss erweitern

- `PullRequest`-Value-Object um fuer Persistenz und Monitoring relevante Felder erweitern oder ein neues Rueckgabeobjekt fuer erstellte PRs einfuehren:
  - Provider, RepositoryId, Nummer, ProviderPullRequestId optional, URL, Titel, SourceBranch, TargetBranch, HeadSha.
- Neue Contract-Records anlegen:
  - `PullRequestStatusInfo`.
  - `PullRequestWorkflowRunInfo`.
  - `PullRequestCompletionOptions`.
  - `PullRequestCompletionResult`.
- `IGitPlugin` um Default-Methoden erweitern, die `NotSupported`-Ergebnisse liefern:
  - `GetPullRequestStatusAsync(...)`.
  - `GetPullRequestWorkflowRunsAsync(...)`.
  - `CompletePullRequestAsync(...)`.
- Dadurch bleiben andere Plugins buildbar, waehrend GitHub die Funktionen konkret implementiert.

### 3. GitHub-Plugin implementieren

- `GitHubPlugin.CreatePullRequestAsync` so erweitern, dass alle verfuegbaren PR-Metadaten geliefert werden. Wo `gh pr create` nicht genug Daten liefert, direkt danach `gh pr view` abfragen.
- Statusabfrage ueber `gh pr view <number> --repo <owner/repo> --json ...` implementieren.
- Workflow-/Action-Abfrage ueber `gh run list` und, falls noetig, ergaenzende `gh api`-Aufrufe implementieren. Zuordnung primaer ueber `headSha`.
- Abschlussoperation implementieren:
  - `Merge`: `gh pr merge` mit konfigurierter Merge-Methode.
  - `AutoMerge`: GitHub-CLI/API-Weg nutzen, sofern verfuegbar; fehlende Voraussetzungen als blockiert melden.
  - `ApprovalOnly`: `gh pr review --approve`; Self-Approval-/Protection-Fehler als blockiert melden.
- Token-Sanitizing fuer alle neuen CLI-Fehlerpfade wiederverwenden.
- GitHub-Plugin-Einstellungen erweitern:
  - `AutoCompletePullRequests` Boolean, Standard `false`.
  - `PullRequestCompletionStrategy` Enum, Standard `Merge`.
  - `PullRequestMergeMethod` Enum, Standard `Squash` oder bestehende Projektkonvention, falls vorhanden.
  - `AllowProtectedBranchBypass` Boolean, Standard `false`, nur als expliziter Abschlussmodus mit klarer Fehlermeldung bei fehlender Berechtigung.

### 4. Application-Services einfuehren

- `PullRequestReferenzService` anlegen:
  - PR nach erfolgreicher Erstellung speichern.
  - PRs inklusive Workflow-Runs je Aufgabe laden.
  - Statusdaten und Workflow-Runs upserten.
  - Fehler- und Blockierungszustaende persistieren.
- `GitOrchestrationService.PullRequestErstellenAsync` nach erfolgreichem `CreatePullRequestAsync` an den neuen Service anbinden und die PR-Referenz speichern.
- `AufgabeService.GetDetailAsync` entweder um Include fuer PRs erweitern oder die PRs konsequent ueber den dedizierten Service laden. Bevorzugt: dedizierter Service im ViewModel, um `AufgabeService` klein zu halten.
- Protokolleintraege fuer PR-Erstellung, Auto-Abschluss, blockierte Abschluesse und Monitoring-Fehler ergaenzen.

### 5. Monitoring und Auto-Abschluss

- `PullRequestMonitoringService` implementieren:
  - faellige PRs anhand `MonitoringPhase` und `LastCheckedUtc` laden,
  - PR-Status und Workflow-Runs beim passenden Git-Plugin abrufen,
  - Monitoring-Phase deterministisch aktualisieren,
  - bei erfolgreichen Pre-Merge-Actions und aktivierter GitHub-Einstellung den Abschluss versuchen,
  - nach Merge Post-Merge-Runs beobachten,
  - API-/CLI-/Berechtigungsfehler in `LastError` und Phase `Blocked` oder `Failed` sichtbar speichern.
- Als Hosted Service oder timerbasierter Singleton registrieren. Bei Singleton-Variante DbContext-Zugriffe nur ueber `IServiceScopeFactory`.
- Polling-Intervall als konservativen Default setzen und spaeter konfigurierbar machen, falls die bestehende Einstellungsarchitektur dafuer genutzt werden soll.
- Monitoring-Logik so kapseln, dass sie ohne echten Timer und ohne echte GitHub-Aufrufe unit-testbar ist.

### 6. Aufgaben-UI erweitern

- `DetailAnsicht` um `Pr` oder `PullRequests` erweitern.
- `TaskDetailViewModel` ergaenzen:
  - `PullRequestViewCommand`,
  - `IsPullRequestViewSelected`,
  - ObservableCollection fuer PR-Anzeigen,
  - abgeleitete Properties fuer Leer-, Lade- und Fehlerzustand,
  - Ladepfad fuer PRs beim Oeffnen der Aufgabe und nach `PR erstellen`.
- `TaskDetailView.xaml` erweitern:
  - neue Ansicht-Schaltflaeche `PR`,
  - PR-Panel mit Liste aller PRs,
  - Statusanzeige fuer PR, Merge-/Monitoring-Phase und letzte Aktualisierung,
  - untergeordnete Anzeige der Workflow-Runs,
  - Zustaende fuer keine PRs, keine Actions, laufend, erfolgreich, fehlgeschlagen, blockiert.
- UI-Texte knapp halten und bestehende Task-Detail-Optik weiterverwenden.

### 7. DI und Konfiguration verdrahten

- Neue Services in `src/Softwareschmiede.App/App.xaml.cs` registrieren.
- Sicherstellen, dass Plugin-Aufloesung im Monitoring mit dem gespeicherten Provider funktioniert.
- Plugin-Settings UI pruefen, ob Boolean-/Enum-Felder ohne weitere Anpassung korrekt dargestellt werden. Falls nicht, Settings-ViewModel minimal erweitern.

### 8. Tests

- Persistenztests:
  - PRs werden eindeutig Aufgabe und Repository/Nummer zugeordnet.
  - Mehrere PRs pro Aufgabe funktionieren.
  - Workflow-Runs werden upserted und bei Delete kaskadiert entfernt.
- GitHubPlugin-Tests:
  - `gh pr view` JSON wird korrekt gemappt.
  - `gh run list` JSON wird korrekt gemappt.
  - Merge-/Approval-/AutoMerge-Kommandos verwenden erwartete Argumente.
  - Token und geheime Werte werden in Fehlern sanitisiert.
  - NotSupported-Fallbacks anderer Plugins bleiben buildbar.
- Monitoring-Tests:
  - Pre-Merge laufend, erfolgreich und fehlgeschlagen.
  - Auto-Abschluss nur bei aktivierter Einstellung und erfolgreichen Runs.
  - Blockierung bei Bypass-/Berechtigungsfehler.
  - Post-Merge-Runs werden nach Merge-Commit verfolgt.
- ViewModel-/View-Tests:
  - PR-Tab ist verfuegbar.
  - PRs und Workflow-Runs werden geladen und angezeigt.
  - Leer-/Fehlerzustand wird gesetzt.
  - Nach PR-Erstellung wird die gespeicherte Referenz sichtbar.
- Abschliessend `dotnet build` und `dotnet test` ausfuehren.

## Reihenfolge fuer die Implementierung

1. Domain-Entities, DbContext-Konfiguration und Migration.
2. Contract-Records und Default-Methoden in `IGitPlugin`.
3. Persistenzservice fuer PR-Referenzen.
4. GitHubPlugin-Status- und Abschlussmethoden.
5. GitOrchestrationService an Persistenz anbinden.
6. Monitoring-Service mit testbarer Policy.
7. UI/ViewModel fuer den neuen PR-Bereich.
8. DI-Registrierung, Settings und Tests vervollstaendigen.

## Akzeptanzkriterien

- Ein ueber die Ribbon-Action erstellter GitHub-Pull-Request wird persistent mit der Aufgabe verknuepft.
- Die Aufgaben-Detailansicht enthaelt einen Inhaltsbereich `PR`.
- Der PR-Bereich zeigt alle Pull Requests der Aufgabe mit PR-, Merge-/Monitoring- und Action-Status.
- GitHub-Statusabfragen aktualisieren gespeicherte PRs und Workflow-Runs.
- Der automatische PR-Abschluss ist im GitHub-Plugin konfigurierbar und standardmaessig deaktiviert.
- Bei aktiviertem Auto-Abschluss wird ein PR erst nach erfolgreichen zugeordneten Pre-Merge-Actions abgeschlossen.
- Fehlende Berechtigungen, Branch-Protection-/Bypass-Probleme und GitHub-API-Fehler sind in Persistenz, Protokoll und UI nachvollziehbar.
- Nach Merge werden zuordenbare Post-Merge-Actions weiter ueberwacht.
- Andere Git-Plugins bleiben trotz Contract-Erweiterung buildbar.
- Relevante Unit-/Integrationstests sowie Build und Tests laufen erfolgreich.

## Risiken und Folgeaufgaben

- Die exakte fachliche Bedeutung von "bestaetigt" sollte nach dem ersten funktionsfaehigen Stand mit dem Nutzer validiert werden. Der Plan setzt fuer die Umsetzung auf abschliessbaren PR-Merge mit sichtbaren Alternativstrategien.
- Required Checks aus Branch Protection koennen eine spaetere Praezisierung erfordern, wenn "alle Head-SHA-Runs" fachlich zu breit ist.
- GitHub-Bypass haengt von Rollen, Repository-Regeln und Token-Rechten ab. Die erste Umsetzung darf fehlende Rechte sichtbar blockieren, statt Schutzregeln zu umgehen.
