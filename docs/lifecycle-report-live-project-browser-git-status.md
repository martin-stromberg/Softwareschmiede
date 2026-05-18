# Lifecycle Report: Live Project Browser mit Git Status

## Geplant
- Requirements:
  - `docs/requirements/live-project-browser-git-status-requirements-analysis.md`
  - `docs/requirements/live-project-browser-git-status-complete-requirements.md`
- Architektur:
  - `docs/architecture/live-project-browser-git-status-architecture-blueprint.md`
  - `docs/architecture/live-project-browser-git-status-entity-relationship-model.md`
- Review:
  - `docs/improvements/live-project-browser-git-status-architecture-review.md`

## Implementiert
- Query-Parameter-Toggle `?view=task|tree`
- Live-Repository-Explorer in der Aufgabenseite
- Rekursiver Tree/List-Wechsel und Virtualisierung
- Git-Status-Parsing inkl. `C`, `T`, `U`, Rename/Copy und Deleted
- Preview-Fallbacks für große, binäre und ungültige Pfade
- Service-/ValueObject-Schicht für Workspace-Snapshot und Preview

## Tests
- Ergänzte Tests für Workspace-Browser, Preview-Fallbacks, Status-Mapping und View-Toggle
- Feature-Tests: 61/61 grün

## Dokumentiert
- Business-Doku: `docs/business/features/F021-live-project-browser-git-status.md`
- API-Doku: `docs/api/live-project-browser-git-status.md`
- Flow-Doku: `docs/flows/live-project-browser-git-status-flow.md`
- Aktualisierte Indizes und Übersichten in `README.md`, `docs/user-guide.md`, `docs/api/README.md`, `docs/flows/README.md`, `docs/business/features.md`

## Hinweise
- Im Gesamttestlauf bleiben zwei bekannte, fachfremde `GitHubCopilotPlugin`-Fehler bestehen.
