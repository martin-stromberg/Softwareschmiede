# HTTP-Endpunkte – Softwareschmiede

## Übersicht

Die Anwendung stellt über `app.MapControllers()` öffentliche REST-Endpunkte für Diff-Funktionen bereit.
Die detaillierte Endpunktbeschreibung mit vollständigen Request-/Response-Beispielen liegt in [diff.md](./diff.md).

## Feature-Hinweis: Benachrichtigungssystem für abgeschlossene KI-Aufgaben

Für das Feature **„Benachrichtigungssystem für abgeschlossene KI-Aufgaben“** wurden **keine neuen öffentlichen REST-Endpunkte** eingeführt.
Die Integration erfolgt intern über Service-/UI-Komponenten (Abschlussereignis, Hub-Verteilung, UI-Verarbeitung).
Im UI-Kontext steuert der **BenachrichtigungsModus** die Ausgabe via **Toast** und optionalem **Hinweiston**; relevante Vorgänge werden im **Audit** erfasst.

## Feature-Hinweis: App-Favicon `favicon-hammer-pick-svg`

Für das Feature **„favicon-hammer-pick-svg“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung erfolgt auf App-Ebene über zusätzliche `<link>`-Einträge in `App.razor` und ein neues statisches Asset in `wwwroot`.
Details: [favicon-hammer-pick-svg.md](./favicon-hammer-pick-svg.md)

## Feature-Hinweis: Changed Artifact Detection & Agentendefinitions-Compliance

Für das Feature **„Erkennung geänderter Planungsdokumente zusätzlich zu Codedateien und Sicherstellung der Agentendefinitions-Compliance“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Umsetzung betrifft bestehende Service-/Plugin-Komponenten (`GitWorkspaceBrowserService`, `WorkspaceSnapshot`, `AufgabeDetail`, `GitHubCopilotPlugin`, `ClaudeCliPlugin`, `AgentPackageReader`) und deren interne Vertragslogik.

- **Ziel:** Vollständige Erkennung geänderter Artefakte (Code + Planung) und verlässliche Agentenpaket-Kompatibilität.
- **Verhalten:** Keine API-Flächenänderung nach außen; interne Contracts liefern getrennte Listen (`CodeFiles`, `PlanningDocuments`) und robuste Plugin-Fehlerpfade.
- **Compliance-Regeln:** Für produktive KI-Plugins gilt `.github` als Kompatibilitätskriterium; fehlende Paketpfade/fehlender `.github`-Ordner führen zu kontrolliertem Verhalten statt Hard-Fail.
- **Testbezug:** `GitWorkspaceBrowserServiceTests`, `AufgabeDetailWorkspacePreviewBunitTests`, `GitHubCopilotPluginTests`, `ClaudeCliPluginTests`, `AgentPackageReaderTests`.
- **Workflow-Auswirkung:** Der Aufgaben-Workflow bleibt auch bei reinen Planungsdokument-Änderungen aktiv, und Agentenbereitstellung wird reproduzierbarer.

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
- App-Favicon-Contract: [favicon-hammer-pick-svg.md](./favicon-hammer-pick-svg.md)
- Live Project Browser Contract: [live-project-browser-git-status.md](./live-project-browser-git-status.md)
- Plugin- und Agentenpaket-Contracts: [plugin-interfaces.md](./plugin-interfaces.md)
- API-Index: [README.md](./README.md)
