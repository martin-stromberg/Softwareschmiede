# API-Dokumentation – Softwareschmiede

Technische Dokumentation der öffentlichen Schnittstellen und internen API-Contracts.

## API-Status (HTTP)

Die Softwareschmiede stellt öffentliche HTTP-Endpunkte für den Diff-Bereich bereit.
Für das Feature **„Benachrichtigungssystem für abgeschlossene KI-Aufgaben“** wurden **keine neuen öffentlichen REST-Endpunkte** eingeführt.
Auch für das Feature **„KI-Arbeitsprotokoll als Markdown“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Auch für das Feature **„KI-Protokoll Auto-Scroll“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Auch für das Feature **„favicon-hammer-pick-svg“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Auch für das Feature **„Erkennung geänderter Planungsdokumente + Agentendefinitions-Compliance“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Auch für den Sollzustand **„Agentenpaket/Agent optional beim Aufgabenstart“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Auch für das Feature **„Branch-Commit-Anzeige im Dateibaum + Commit-Diff-Preview“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Auch für das Feature **„Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Details: [http-endpoints.md](./http-endpoints.md) und [diff.md](./diff.md)

## Dokumentierte API-Bereiche

| Dokument | Kurzbeschreibung |
|---|---|
| [http-endpoints.md](./http-endpoints.md) | Übersicht aller aktuell verfügbaren öffentlichen HTTP-Endpunkte inkl. Auth- und Content-Type-Konventionen sowie Feature-Hinweis zum Benachrichtigungssystem (keine neuen REST-Endpunkte, interne Service-/UI-Integration). |
| [diff.md](./diff.md) | Vollständige REST-Dokumentation für `DiffController` (`/api/diff`) mit Request-/Response-Beispielen, Servicevertrags-Referenzen und testabgeglichenen Verhaltensdetails. |
| [branch-commit-diff-preview.md](./branch-commit-diff-preview.md) | Feature-spezifischer API-/Contract-Überblick für Branch-Commit-Knoten im Dateibaum und Commit-Vorschau inkl. klarer Abgrenzung „keine neuen REST-Endpunkte“, bestehender Diff-API-Bezüge, Testabdeckung und bekannten Grenzen. |
| [diff-viewer.md](./diff-viewer.md) | Technischer UI-/Routen-Contract für den Diff Viewer mit Dual-Mode (Embedded/Standalone), Zustandsverantwortung (`AufgabeDetail`/`DiffPreviewPanel`/`DiffViewer`), FR-4-Fallback-Entscheidungen, Parameterwechsel-Stabilität und kompatibler Wrapper-Route `/diff/{DiffResultId:guid}`. |
| [aufgabe-recovery.md](./aufgabe-recovery.md) | Interner Service-/UI-Contract für die manuelle Wiederherstellung festhängender Aufgaben (Eligibility, Statuswechsel, Audit, Concurrency-Schutz). |
| [issue-branch-pr-linking.md](./issue-branch-pr-linking.md) | Interner Contract für Issue-Auswahl, issuebezogene Branch-Erzeugung und PR-Closing-Direktive (`Closes #<Issue>`). |
| [live-project-browser-git-status.md](./live-project-browser-git-status.md) | Technischer Contract für den lokalen Repository-Browser auf der Aufgabenseite inkl. Snapshot, Tree-/Listenansicht, Branch-Commit-Knoten mit lazy Commit-Dateibaum, Commit-/Working-Tree-Vorschau, getrennter Klassifikation von Code-/Planungsänderungen sowie Testbezug und Workflow-Auswirkung in `AufgabeDetail`. |
| [local-directory-plugin.md](./local-directory-plugin.md) | Technischer Contract der `IGitPlugin`-Implementierung `LocalDirectoryPlugin` inkl. Workspace-Modi, `git-init`-Fallback, Pull ohne Merge, Push-/Delete-Sync sowie Capability-Flags für die Aktionsmatrix (Push/Pull/PR ausblenden, Merge einblenden bei Arbeitskopie). |
| [plugin-default-selection.md](./plugin-default-selection.md) | Interner API-Contract für **Standardplugin** je **Pluginart**, **KI-Plugin-Auswahl** sowie die projektspezifische/aufgabenbezogene `IGitPlugin`-Auflösung (vor Default) inkl. Fallback bei fehlender Repo-Verknüpfung. |
| [ki-plugin-spezifische-agenten-discovery-auswahl.md](./ki-plugin-spezifische-agenten-discovery-auswahl.md) | Technischer Contract für Issue 58: plugin-spezifische Agenten-Discovery/Auswahl, Persistenz von `KiPluginPrefix` sowie Pflicht-/Optional-Regeln (KI-Plugin Pflicht, Agentenpaket/Agent optional). |
| [aufgaben-startvalidierung.md](./aufgaben-startvalidierung.md) | Technischer UI-/Service-Contract für die Startvalidierung in `AufgabeDetail` inkl. Pflichtfeldlogik, optionaler Agentenselektion und Fehler-/Hinweispfaden beim Aufgabenstart. |
| [plugin-interfaces.md](./plugin-interfaces.md) | Schnittstellenreferenz für `IPlugin`, `IGitPlugin`, `IKiPlugin`, Plugin-Discovery und provider-spezifische Agentenpaket-Compliance (`.github` bei Copilot, `.claude/commands` bei Claude CLI) inkl. Deploy-/Fallback-/Health-Details. |
| [repository-startskript-freier-port.md](./repository-startskript-freier-port.md) | Interner Contract für repositorybezogene Startskripte mit freier Portreservierung, Persistenz (`RepositoryStartKonfiguration`) und Ausführung beim Prozessstart. |
| [start-ps1-visual-studio-freier-http-port.md](./start-ps1-visual-studio-freier-http-port.md) | Skriptvertrag für `start.ps1`: parameterloser Aufruf, autonome Mehrprojekt-Portzuweisung, Exit-Codes und VS-kompatibler Host-Fallback auf `localhost`. |
| [workdir-configuration.md](./workdir-configuration.md) | Interner Contract für Arbeitsverzeichnis-Auflösung und Laufzeit-**Fallback** beim Klonpfad. |
| [favicon-hammer-pick-svg.md](./favicon-hammer-pick-svg.md) | App-level Contract für die SVG-Favicon-Integration (`icon`/`shortcut icon`/`mask-icon`) inkl. Static-Asset-Auswirkung und Bestätigung „keine neuen HTTP-Endpunkte“. |
| [ki-protokoll-auto-scroll.md](./ki-protokoll-auto-scroll.md) | Technischer UI-/Interop-Contract für Auto-Scroll im KI-Protokoll (`AufgabeDetail`, `log-scroll.js`): Initial-Scroll beim Einblenden, konditionales Follow-Scroll nur bei Endposition und Positionsbeibehaltung bei manuellem Hochscrollen. |

## Feature-Fokus: DiffViewer-Integration (2026-05-23)

- Aktualisierter technischer Contract: [diff-viewer.md](./diff-viewer.md)
- Fokus auf Dual-Mode-Nutzung (`Embedded`/`Standalone`) ohne Divergenz in der Kernlogik.
- Klare Zustandsverantwortung zwischen `AufgabeDetail` (Kontext/Selektion), `DiffPreviewPanel` (FR-4-Fallback-Entscheidungen) und `DiffViewer` (Laden/Rendern).
- Stabilitätsmaßnahmen bei Parameterwechseln (`OnParametersSetAsync`, Cancellation, `loadingVersion`-Guard).
- Route-Kompatibilität über Wrapper-Page auf `/diff/{DiffResultId:guid}`.

## Feature-Fokus: Korrekte Diff-Anzeige für geänderte Dateien (2026-05-24)

- `AufgabeDetail` nutzt für die eingebettete Vorschau eine dateispezifische Diff-Auflösung statt einer globalen Latest-ID.
- Neue Service-Query: `GetLatestDiffResultIdForFileAsync(aufgabeId, relativePath)` mit Pfadnormalisierung (`\`/`/`, `./`, Case-Insensitivity).
- Fallbackpfad: Wenn für `RelativePath` kein Diff gefunden wird, erfolgt ein zweiter Lookup über `SourceRelativePath` (falls sinnvoll).
- Die Route `/diff/{DiffResultId:guid}` bleibt unverändert und verwendet weiterhin die zuletzt bekannte Diff-ID für den Standalone-Deep-Link.

## Feature-Fokus: Branch-Commit-Anzeige im Dateibaum + Commit-Diff-Preview (2026-05-27)

- Keine neuen öffentlichen REST-Endpunkte; Umsetzung über bestehende interne Service-Contracts.
- `WorkspaceSnapshot.BranchCommits` liefert Commit-Knoten, `LoadCommitFilesAsync` lädt Commit-Dateien lazy, `LoadCommitPreviewAsync` liefert Commit-inhaltliche Vorschau.
- Für Commit-Dateien wird kein `DiffResultId` aufgelöst; für Working-Tree-Dateien bleibt der dateispezifische Diff-Lookup aktiv.
- Details: [branch-commit-diff-preview.md](./branch-commit-diff-preview.md), [live-project-browser-git-status.md](./live-project-browser-git-status.md), [diff-viewer.md](./diff-viewer.md)

## Feature-Fokus: App-Favicon `favicon-hammer-pick.svg`

- Technischer Contract: [favicon-hammer-pick-svg.md](./favicon-hammer-pick-svg.md)
- App.razor referenziert das Favicon bewusst als `icon`, `shortcut icon` und `mask-icon`.
- Die Änderung betrifft statische Assets (`wwwroot`) und Head-Markup; öffentliche HTTP-Endpunkte bleiben unverändert.

## Feature-Fokus: Benachrichtigungssystem für abgeschlossene KI-Aufgaben

- Keine neuen öffentlichen REST-Endpunkte.
- Interne Service-/UI-Integration über Abschlussereignisse, Hub-Verteilung und UI-Verarbeitung.
- Terminologie für dieses Feature: **BenachrichtigungsModus**, **Toast**, **Hinweiston**, **Audit**.

## Feature-Fokus: KI-Arbeitsprotokoll als Markdown (2026-05-24)

- Keine neuen öffentlichen REST-Endpunkte.
- Formatvertrag in der internen Pipeline:
  - Datumszeile als `# yyyy-MM-dd`
  - Schritttrennung als `## Schritt n` mit Leerzeile zwischen Schritten
  - Fallback bei leerer Antwort (`Keine Ausgabe vorhanden.`) und Streaming-Fallback (`Warte auf Ausgabe...`)
- Sichere Webdarstellung:
  - Markdig-Pipeline mit `DisableHtml()`
  - Sanitizing entfernt `on*`-Eventattribute und neutralisiert unsichere `href/src`-Schemes (`javascript:`, `data:`, `vbscript:`)
  - Fallback auf HTML-encoded `<pre>` bei Render-/Sanitizing-Fehlern
- Technische Vertiefung: [KI-Arbeitsprotokoll-Flow](../flows/ki-arbeitsprotokoll-rendering-flow.md)
- Fachliche Einordnung: [F005 – Aufgabenprotokoll](../business/features/F005-aufgabenprotokoll.md)

## Feature-Fokus: KI-Protokoll Auto-Scroll (2026-05-25)

- Technischer Contract: [ki-protokoll-auto-scroll.md](./ki-protokoll-auto-scroll.md)
- Beim Einblenden des Protokolls erfolgt ein Initial-Scroll ans Ende (`_streamingInitialScrollPending`, `_historyInitialScrollPending`).
- Bei neuem Inhalt erfolgt Auto-Scroll nur, wenn vor dem Append eine Endposition erkannt wurde (`TryReadAtEndStateAsync`, `IsAtEnd`, `ScrollEndThresholdPx = 16`).
- Bei manuellem Hochscrollen bleibt die Position stabil; `ApplyPendingScrollAsync` verarbeitet das Update dann ohne `scrollToEnd`.
- Implementierungsbezug: `AufgabeDetail` (`OnAfterRenderAsync`, `Capture*ScrollStateBeforeUpdateAsync`, `Register*ContentUpdate`) und `wwwroot/js/log-scroll.js`.

## Feature-Fokus: Standardplugin je Pluginart & KI-Plugin-Auswahl

- Technischer Contract: [plugin-default-selection.md](./plugin-default-selection.md)
- Fachliche Einordnung: [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](../business/features/F014-standardplugin-ki-plugin-auswahl.md)
- Ablaufdarstellung: [plugin-default-selection-flow.md](../flows/plugin-default-selection-flow.md)

## Feature-Fokus: Issue 58 – KI-Plugin-spezifische Agenten-Discovery/Auswahl

- Technischer Contract: [ki-plugin-spezifische-agenten-discovery-auswahl.md](./ki-plugin-spezifische-agenten-discovery-auswahl.md)
- Startvalidierung (Pflicht/Optional): [aufgaben-startvalidierung.md](./aufgaben-startvalidierung.md)
- Ablaufdarstellung: [ki-plugin-spezifische-agenten-discovery-auswahl-flow.md](../flows/ki-plugin-spezifische-agenten-discovery-auswahl-flow.md)
- Fachliche Einordnung: [F026 – KI-Plugin-spezifische Agenten-Discovery und -Auswahl](../business/features/F026-ki-plugin-spezifische-agenten-discovery-auswahl.md)
- Planungsartefakte:
  - [Requirements](../requirements/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-requirements-analysis.md)
  - [Architektur-Blueprint](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-blueprint.md)
  - [ERM](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-entity-relationship-model.md)
  - [Architecture-Review](../improvements/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-review.md)

## Feature-Fokus: Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung) (2026-05-28)

- Keine neuen öffentlichen REST-Endpunkte; die Umsetzung erfolgt im internen `IKiPlugin`-/CLI-Contract.
- Claude-spezifische Agenten-Discovery und Deploy laufen über `.claude/commands` bzw. `.claude`.
- Prompt-Ausführung nutzt First-Run `-n` und Follow-up `-r`; bei `session not found` erfolgt ein transparenter Fallback auf First-Run.
- Große Prompts werden über einen stdin-Wrapper (`powershell`/`sh`) statt Inline-Argument übergeben.
- Technische Referenz: [plugin-interfaces.md](./plugin-interfaces.md), [http-endpoints.md](./http-endpoints.md)
- Verknüpfte Planungs-/Testartefakte:
  - [Requirements](../requirements/claude-cli-integration-requirements-analysis.md)
  - [Architektur-Blueprint](../architecture/claude-cli-integration-architecture-blueprint.md)
  - [ERM](../architecture/claude-cli-integration-entity-relationship-model.md)
  - [Architecture-Review](../improvements/claude-cli-integration-architecture-review.md)
  - [Planungsübersicht](../planning-overview-claude-cli-integration.md)
  - [Testplan](../tests/testplan-claude-cli-integration.md)
  - [Testlücken](../tests/testluecken-claude-cli-integration.md)

## Feature-Fokus: Lokales Verzeichnis Plugin

- Technischer Contract: [local-directory-plugin.md](./local-directory-plugin.md)
- Schnittstellen-Referenz: [plugin-interfaces.md](./plugin-interfaces.md)
- Ablaufdarstellung: [local-directory-plugin-flow.md](../flows/local-directory-plugin-flow.md)

## Feature-Fokus: Live Project Browser mit Git-Status

- Technischer Contract: [live-project-browser-git-status.md](./live-project-browser-git-status.md)
- Ablaufdarstellung: [live-project-browser-git-status-flow.md](../flows/live-project-browser-git-status-flow.md)
- Fachliche Einordnung: [F021 – Live Project Browser mit Git-Status](../business/features/F021-live-project-browser-git-status.md)

## Feature-Fokus: Changed Artifact Detection & Agentendefinitions-Compliance

- **Ziel:** Sichtbarkeit geänderter Planungsdokumente und reproduzierbare Agentenpaket-Ausführung ohne Contract-Brüche.
- **Verhalten:** `WorkspaceSnapshot` trennt Änderungen in `CodeFiles` und `PlanningDocuments` inkl. Fallback-Erkennung für Slash-/Dot-Varianten der `docs/*`-Pfade.
- **Betroffene Komponenten:** `GitWorkspaceBrowserService`, `WorkspaceSnapshot`, `AufgabeDetail`, `GitHubCopilotPlugin`, `ClaudeCliPlugin`, `AgentPackageReader`.
- **Compliance-Regeln:** Kompatibilität ist provider-spezifisch (`GitHubCopilotPlugin`: `.github`, `ClaudeCliPlugin`: `.claude/commands`); fehlende Paketpfade oder fehlende Pflichtordner werden kontrolliert behandelt (leere Agentenliste, `false` bei Kompatibilität, Deploy-Skip statt Hard-Fail).
- **Testbezug:** `GitWorkspaceBrowserServiceTests`, `AufgabeDetailWorkspacePreviewBunitTests`, `GitHubCopilotPluginTests`, `ClaudeCliPluginTests`, `AgentPackageReaderTests`.
- **Workflow-Auswirkung:** Die Aufgabenansicht bleibt auch bei reinen Doku-Änderungen aktiv nutzbar; Agentenpaket-Prüfung und Deploy-Verhalten reduzieren Laufzeitabbrüche im Entwicklungsworkflow.
- Details: [live-project-browser-git-status.md](./live-project-browser-git-status.md), [plugin-interfaces.md](./plugin-interfaces.md), [planning-overview-changed-artifact-detection.md](../planning-overview-changed-artifact-detection.md)

## Feature-Fokus: Manuelle Aufgaben-Recovery

- Technischer Contract: [aufgabe-recovery.md](./aufgabe-recovery.md)
- Ablaufdarstellung: [aufgabe-recovery-flow.md](../flows/aufgabe-recovery-flow.md)
- Fachliche Einordnung: [F016 – Fehlerbehandlung & Recovery](../business/features/F016-fehlerbehandlung-und-recovery.md)

## Feature-Fokus: Issue-, Branch- und PR-Verknüpfung

- Technischer Contract: [issue-branch-pr-linking.md](./issue-branch-pr-linking.md)
- Ablaufdarstellung: [issue-branch-pr-linking-flow.md](../flows/issue-branch-pr-linking-flow.md)
- Fachliche Einordnung: [F019 – Issue-, Branch- und PR-Verknüpfung](../business/features/F019-issue-branch-pr-verknuepfung.md)

## Feature-Fokus: Repository-Startskript mit freier Portzuweisung

- Technischer Contract: [repository-startskript-freier-port.md](./repository-startskript-freier-port.md)
- Skriptvertrag für VS-Debug: [start-ps1-visual-studio-freier-http-port.md](./start-ps1-visual-studio-freier-http-port.md)
- Ablaufdarstellung: [repository-startskript-freier-port-flow.md](../flows/repository-startskript-freier-port-flow.md)
- Fachliche Einordnung: [F020 – Repository-Startskript mit freier Portzuweisung](../business/features/F020-repository-startskript-freier-port.md)

## Verknüpfte Dokumentation

- Flows-Index: [docs/flows/README.md](../flows/README.md)
- Feature-Index: [docs/business/features.md](../business/features.md)
- Plugin-Prinzip (Business): [F010 – Plugin-Prinzip für Integrationen](../business/features/F010-plugin-prinzip-integrationen.md)
