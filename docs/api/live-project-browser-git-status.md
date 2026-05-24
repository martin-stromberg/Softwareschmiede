# Live Project Browser mit Git-Status – Technischer Contract

## Zweck

Der Live Project Browser stellt den lokalen Repository-Zustand der Aufgabenseite zur Laufzeit bereit.
Er liest ausschließlich aus dem lokalen Klon und speichert keinen eigenen Browserzustand.

## Implementierung und Contract

- Contract: `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`
- Implementierung: `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`
- Laufzeitmodelle:
  - `WorkspaceSnapshot`
  - `WorkspaceFileNode`
  - `WorkspaceFileStatus`
  - `FilePreview`
  - `WorkspaceNodeRow`

## Unterstützte Operationen

| Methode | Zweck | Rückgabe |
|---|---|---|
| `LoadSnapshotAsync(repositoryPath)` | Lädt Commit-Zahl, Statusdaten und Baum-/Listenmodell eines lokalen Repositories. | `WorkspaceSnapshot` |
| `LoadPreviewAsync(repositoryPath, node)` | Lädt die Vorschau für die selektierte Datei oder den zugehörigen Hinweis. | `FilePreview` |

## Snapshot-Regeln

- `repositoryPath` muss existieren und ein Git-Repository sein.
- Commit-Zahl kommt aus `git rev-list --count HEAD`.
- Statusdaten kommen aus `git status --porcelain=v1 --untracked-files=all`.
- Ignorierte Einträge (`!!`) werden gefiltert.
- Untracked-Dateien (`??`) werden als eigener Status modelliert.
- Rename/Copy-Einträge werden aus dem `old -> new`-Pfad aufgelöst.
- Der Service erzeugt sowohl `RootNodes` als auch `FlatFiles`.
- Geänderte Planungsdokumente werden zusätzlich zu Codedateien explizit klassifiziert:
  - `PlanningDocuments`: Markdown-Dateien unter `docs/requirements/`, `docs/architecture/`, `docs/improvements/`
  - `CodeFiles`: bekannte Quellcode-/Konfigurationsdateitypen aus dem Workspace
- Wenn initial keine `PlanningDocuments` erkannt werden, greift eine robuste Fallback-Prüfung auf normalisierte Slash-/Dot-Varianten der drei docs-Pfade.

## Feature-Fokus: Changed Artifact Detection & Agentendefinitions-Compliance

- **Ziel:** Keine „blinden Flecken“ bei geänderten Planungsdokumenten im Live Project Browser.
- **Verhalten:** Auch bei ausschließlich geänderten Planungsdokumenten bleibt die Workspace-Ansicht konsistent nutzbar (kein irreführender „Keine Änderungen“-Pfad).
- **Betroffene Komponenten:**
  - `GitWorkspaceBrowserService` (Klassifikation + Fallback)
  - `WorkspaceSnapshot` (`CodeFiles`, `PlanningDocuments`)
  - `AufgabeDetail` (UI-Weiterverarbeitung der getrennten Artefaktlisten)
- **Compliance-Regeln (angrenzend):**
  - Planungsdokumente sind ausschließlich `*.md` unter `docs/requirements/`, `docs/architecture/`, `docs/improvements/`.
  - Fallback-Prüfung normalisiert Slash-/Dot-Varianten (`/docs/...`, `./docs/...`) nur für diese drei freigegebenen Pfade.
  - Agentenpakete werden pluginseitig auf kompatible Struktur geprüft (`.github`-Ordner, robuste Fehlerpfade bei nicht lesbaren/nicht vorhandenen Paketdateien), damit Orchestrierungsabläufe reproduzierbar bleiben.
- **Testbezug:**
  - `GitWorkspaceBrowserServiceTests` (Klassifikation, Fallback, SourceRelativePath bei Rename/Copy)
  - `AufgabeDetailWorkspacePreviewBunitTests` (Darstellung von Planning-only und gemischten Änderungen im UI-Workflow)
- **Workflow-Auswirkung:**
  - Die Aufgabenansicht zeigt geänderte Planungsdokumente als normale Artefakte im Workspace-Explorer.
  - Folgeaktionen wie Dateiselektion/Vorschau bleiben verfügbar, auch wenn keine klassischen Codedateien geändert wurden.

## Vorschau-Regeln

- Verzeichnisse liefern nur einen Hinweis.
- Gelöschte Dateien werden aus `HEAD:path` gelesen.
- Fehlende Arbeitskopien werden defensiv mit Hinweis behandelt.
- Dateien > 1 MB werden nicht inline angezeigt.
- Binärdateien werden per Null-Byte-Heuristik erkannt.
- Pfad-Traversal außerhalb des Repository-Roots wird blockiert.

## Fachliche Grenzen

- Keine Schreiboperationen.
- Keine Server-seitige Paginierung.
- Keine Merge-/Commit-/Push-/Pull-Operationen.

## Verwandte Dokumentation

- [F021 – Live Project Browser mit Git-Status](../business/features/F021-live-project-browser-git-status.md)
- [Ablauf – Live Project Browser mit Git-Status](../flows/live-project-browser-git-status-flow.md)
- [Requirements Analysis](../requirements/live-project-browser-git-status-requirements-analysis.md)
- [Architecture Blueprint](../architecture/live-project-browser-git-status-architecture-blueprint.md)
