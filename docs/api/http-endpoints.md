# HTTP-Endpunkte – Softwareschmiede

## Übersicht

Die Anwendung stellt über `app.MapControllers()` öffentliche REST-Endpunkte für Diff-Funktionen bereit.
Die detaillierte Endpunktbeschreibung mit vollständigen Request-/Response-Beispielen liegt in [diff.md](./diff.md).

## Feature-Hinweis: Benachrichtigungssystem für abgeschlossene KI-Aufgaben

Für das Feature **„Benachrichtigungssystem für abgeschlossene KI-Aufgaben“** wurden **keine neuen öffentlichen REST-Endpunkte** eingeführt.
Die Integration erfolgt intern über Service-/UI-Komponenten (Abschlussereignis, Hub-Verteilung, UI-Verarbeitung).
Im UI-Kontext steuert der **BenachrichtigungsModus** die Ausgabe via **Toast** und optionalem **Hinweiston**; relevante Vorgänge werden im **Audit** erfasst.

## Feature-Hinweis: KI-Arbeitsprotokoll als Markdown

Für das Feature **„KI-Arbeitsprotokoll als strukturiertes Markdown“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung erfolgt in bestehenden Service-/UI-Komponenten (`EntwicklungsprozessService`, `AufgabeDetail`) über interne Render- und Sicherheitslogik.

- **Strukturregeln:** Datumszeile `# yyyy-MM-dd`, Schrittblöcke `## Schritt n`, Leerzeile zwischen Schritten, Fallback-Schritt bei leerer KI-Antwort.
- **Webdarstellung:** Markdown wird in HTML gerendert (Heading-/Listen-/Link-/Code-Support).
- **Sicherheit:** Raw-HTML ist in der Pipeline deaktiviert; nachgelagertes Sanitizing entfernt `on*`-Attribute und neutralisiert unsichere URI-Schemes in `href/src`.
- **Robustheit:** Bei Render-/Sanitizing-Fehlern greift ein HTML-encodiertes `<pre>`-Fallback.
- **Details:** [Flow: KI-Arbeitsprotokoll – Persistierung, Rendering und Fallback](../flows/ki-arbeitsprotokoll-rendering-flow.md)

## Feature-Hinweis: KI-Protokoll Auto-Scroll

Für das Feature **„KI-Protokoll Auto-Scroll“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung erfolgt als interner UI-/Interop-Contract in `AufgabeDetail` und `wwwroot/js/log-scroll.js`.

- **Initiales Scrollen:** Beim Einblenden des Streaming-/Historie-Containers wird einmalig ans Ende gescrollt.
- **Konditionales Follow-Scroll:** Bei neuem Inhalt erfolgt Auto-Scroll nur, wenn vor dem Update eine Endposition erkannt wurde.
- **Positionsbeibehaltung:** Bei manuellem Hochscrollen unterbleibt `scrollToEnd`, die aktuelle Position bleibt erhalten.
- **Technischer Contract:** [ki-protokoll-auto-scroll.md](./ki-protokoll-auto-scroll.md)

## Feature-Hinweis: App-Favicon `favicon-hammer-pick-svg`

Für das Feature **„favicon-hammer-pick-svg“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung erfolgt auf App-Ebene über zusätzliche `<link>`-Einträge in `App.razor` und ein neues statisches Asset in `wwwroot`.
Details: [favicon-hammer-pick-svg.md](./favicon-hammer-pick-svg.md)

## Feature-Hinweis: Changed Artifact Detection & Agentendefinitions-Compliance

Für das Feature **„Erkennung geänderter Planungsdokumente zusätzlich zu Codedateien und Sicherstellung der Agentendefinitions-Compliance“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung betrifft bestehende Service-/Plugin-Komponenten (`GitWorkspaceBrowserService`, `WorkspaceSnapshot`, `AufgabeDetail`, `GitHubCopilotPlugin`, `ClaudeCliPlugin`, `AgentPackageReader`) und deren interne Vertragslogik.

- **Ziel:** Vollständige Erkennung geänderter Artefakte (Code + Planung) und verlässliche Agentenpaket-Kompatibilität.
- **Verhalten:** Keine API-Flächenänderung nach außen; interne Contracts liefern getrennte Listen (`CodeFiles`, `PlanningDocuments`) und robuste Plugin-Fehlerpfade.
- **Compliance-Regeln:** Das Kompatibilitätskriterium ist provider-spezifisch (`GitHubCopilotPlugin`: `.github`, `ClaudeCliPlugin`: `.claude/commands`); fehlende Paketpfade/Pflichtordner führen zu kontrolliertem Verhalten statt Hard-Fail.
- **Testbezug:** `GitWorkspaceBrowserServiceTests`, `AufgabeDetailWorkspacePreviewBunitTests`, `GitHubCopilotPluginTests`, `ClaudeCliPluginTests`, `AgentPackageReaderTests`.
- **Workflow-Auswirkung:** Der Aufgaben-Workflow bleibt auch bei reinen Planungsdokument-Änderungen aktiv, und Agentenbereitstellung wird reproduzierbarer.

## Feature-Hinweis: Issue 58 – KI-Plugin-spezifische Agenten-Discovery/Auswahl

Für das Feature **„Agenten-Discovery und Agenten-Auswahl KI-Plugin-spezifisch“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung betrifft UI-/Service-Vertragslogik (`AufgabeDetail`, `PluginSelectionService`, `EntwicklungsprozessService`, `KiAusfuehrungsService`, `AufgabeService`) inklusive Persistenz von `KiPluginPrefix`.

- **Ziel:** Konsistente, plugin-spezifische Auswahl mit **KI-Plugin als Pflichtfeld** und **optionalem Agentenpaket/Agent**.
- **Verhalten:** Start und Folgeprompt nutzen dieselbe Prefix-Auflösung (explizit → Aufgabe → Default → Fallback).
- **Startvalidierung:** Start/Senden ist nur bei fehlendem KI-Plugin blockiert; ohne Paket/Agent wird mit Standardeinstellungen ausgeführt.
- **Persistenz:** `Aufgabe.KiPluginPrefix` ist nullable und über Migration eingeführt.
- **Testbezug:** `PluginSelectionServiceTests`, `EntwicklungsprozessServiceTests`, `AufgabeDetailFolgePromptTests`, `AufgabeServiceTests`.
- **Details:** [ki-plugin-spezifische-agenten-discovery-auswahl.md](./ki-plugin-spezifische-agenten-discovery-auswahl.md), [aufgaben-startvalidierung.md](./aufgaben-startvalidierung.md)

## Feature-Hinweis: Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)

Für das Feature **„Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung liegt ausschließlich in der internen Plugin-/CLI-Orchestrierung (`ClaudeCliPlugin`, `EntwicklungsprozessService`).

- **Agentenpaket-Contract (Claude):** Discovery nur aus `.claude/commands`; Deploy kopiert `.claude` ins Repository.
- **Session-Verhalten:** First-Run mit `-n <taskId>`, Follow-up mit `-r <taskId> -p`; bei `session not found` erfolgt ein Fallback auf neuen First-Run.
- **Large-Prompt-Verhalten:** Prompts > 8 KB werden per stdin (`powershell`/`sh` Wrapper) statt Inline-Argument an `claude` übergeben.
- **Token-Weitergabe:** Optionales Secret wird als `ANTHROPIC_API_KEY` an den Prozess übergeben.
- **Dokumentations-Fallback (transparent):** Für den Dokumentationslauf wurde bei fehlender `~/.copilot/agents/documentation-orchestrator.agent.md` auf `.github/agents/documentation-orchestrator.agent.md` gewechselt (siehe [documentation-plan.md](../documentation-plan.md)).
- **Details:** [plugin-interfaces.md](./plugin-interfaces.md)
- **Verknüpfte Planungs-/Testartefakte:** [Requirements](../requirements/claude-cli-integration-requirements-analysis.md), [Architektur-Blueprint](../architecture/claude-cli-integration-architecture-blueprint.md), [ERM](../architecture/claude-cli-integration-entity-relationship-model.md), [Architecture-Review](../improvements/claude-cli-integration-architecture-review.md), [Planungsübersicht](../planning-overview-claude-cli-integration.md), [Testplan](../tests/testplan-claude-cli-integration.md), [Testlücken](../tests/testluecken-claude-cli-integration.md)

## Feature-Hinweis: Branch-Commit-Anzeige im Dateibaum + Commit-Diff-Preview

Für das Feature **„Branch-Commit-Anzeige im Dateibaum + Commit-Diff-Preview“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung erfolgt über interne Contracts in `GitWorkspaceBrowserService`, `CommitTreePresenter` und `AufgabeDetail`.

- **Interne Operationen:** `LoadSnapshotAsync` (inkl. `BranchCommits`), `LoadCommitFilesAsync`, `LoadCommitPreviewAsync`.
- **Öffentlicher API-Bezug:** bestehender Diff-Endpunkt `GET /api/diff/{id}` für dateispezifische Working-Tree-Diffanzeige.
- **Testbezug:** `GitWorkspaceBrowserServiceTests`, `CommitTreePresenterTests`, `AufgabeDetailWorkspacePreviewBunitTests`, `AufgabeServiceTests`.
- **Details:** [branch-commit-diff-preview.md](./branch-commit-diff-preview.md), [live-project-browser-git-status.md](./live-project-browser-git-status.md), [diff-viewer.md](./diff-viewer.md)

## Öffentliche REST-Endpunkte

| Methode | Pfad | Zweck |
|---|---|---|
| `POST` | `/api/diff/generate` | Erzeugt ein neues Diff-Ergebnis aus Source-/Target-Content. |
| `GET` | `/api/diff/{id}` | Lädt ein einzelnes Diff-Ergebnis inkl. Blöcken/Zeilen. |
| `GET` | `/api/diff` | Listet Diff-Ergebnisse einer Aufgabe paginiert. |
| `GET` | `/api/diff/statistics` | Liefert aggregierte Diff-Statistiken pro Aufgabe. |
| `DELETE` | `/api/diff/{id}` | Löscht ein Diff-Ergebnis inkl. Cache-Invalidierung. |
| `POST` | `/api/diff/{id}/invalidate-cache` | Invalidiert den Cache eines bestehenden Diff-Ergebnisses. |

## Authentifizierung

Aktuell ist für diese Endpunkte keine Authentifizierung erzwungen.
Bei vorgeschalteter Auth-Middleware können `401 Unauthorized`-Antworten auftreten (Beispiel-Payloads in [diff.md](./diff.md)).

## Request-/Response-Konventionen

- Content-Type für JSON-Requests: `application/json`
- Responses: `application/json`
- Fehlerformat: `ProblemDetails` (ASP.NET Core Standard)
- Enum-Serialisierung: numerisch (Ausnahme: `statusBreakdown` in Statistiken nutzt Enum-Namen als JSON-Keys)
- Aktuell testabgeglichen: Zeilenänderungen werden primär als `Added`/`Removed` ausgewiesen; `modifiedLines` ist im aktuellen Algorithmus typischerweise `0` (Details in [diff.md](./diff.md#datenmodelle-enums-und-serviceverträge))

## Verknüpfte Dokumentation

- Detaillierte Diff-API: [diff.md](./diff.md)
- Branch-Commit-/Commit-Preview-Contract: [branch-commit-diff-preview.md](./branch-commit-diff-preview.md)
- App-Favicon-Contract: [favicon-hammer-pick-svg.md](./favicon-hammer-pick-svg.md)
- Live Project Browser Contract: [live-project-browser-git-status.md](./live-project-browser-git-status.md)
- Issue-58 Contract: [ki-plugin-spezifische-agenten-discovery-auswahl.md](./ki-plugin-spezifische-agenten-discovery-auswahl.md)
- Startvalidierung beim Aufgabenstart: [aufgaben-startvalidierung.md](./aufgaben-startvalidierung.md)
- Plugin- und Agentenpaket-Contracts: [plugin-interfaces.md](./plugin-interfaces.md)
- KI-Protokoll Auto-Scroll Contract: [ki-protokoll-auto-scroll.md](./ki-protokoll-auto-scroll.md)
- API-Index: [README.md](./README.md)
