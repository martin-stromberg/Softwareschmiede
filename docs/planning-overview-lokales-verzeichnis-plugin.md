# Planungsübersicht – LocalDirectoryPlugin

> **Dokument-Typ:** Planungsübersicht (Orchestrator-Ergebnis)  
> **Feature:** LocalDirectoryPlugin – Git-Plugin für lokale Verzeichnisse  
> **Status:** ✅ Planungsphase abgeschlossen (mit priorisierten Follow-ups)  
> **Datum:** 2026-05-12

---

## 1. Ergebnis der Orchestrator-Phasen

Der vollständige Planungsablauf wurde in der vorgesehenen Reihenfolge durchgeführt:

1. Requirements Analysis
2. Architecture Blueprint
3. Entity-Relationship Modeling
4. Architecture Review
5. Konsolidierung und Verlinkung

---

## 2. Artefakte (final, verlinkt)

| Phase | Artefakt | Stand |
|---|---|---|
| Anforderungen | [docs/requirements/lokales-verzeichnis-plugin-requirements-analysis.md](requirements/lokales-verzeichnis-plugin-requirements-analysis.md) | v1.1.0 |
| Architektur | [docs/architecture/lokales-verzeichnis-plugin-architecture-blueprint.md](architecture/lokales-verzeichnis-plugin-architecture-blueprint.md) | v1.1.0 |
| ERM | [docs/architecture/lokales-verzeichnis-plugin-entity-relationship-model.md](architecture/lokales-verzeichnis-plugin-entity-relationship-model.md) | v1.2.0 |
| Review | [docs/improvements/lokales-verzeichnis-plugin-architecture-review.md](improvements/lokales-verzeichnis-plugin-architecture-review.md) | v1.1.0 |

---

## 3. Verbindliche Entscheidungen (Unklarheiten aufgelöst)

| Thema | Entscheidung |
|---|---|
| `git init` im Source-Verzeichnis | Nur mit expliziter Nutzerbestätigung |
| Verzeichniskopie bei großen Projekten | Guardrails mit Timeout/Datei-/Größenlimits |
| Uncommitted Changes | Harter Fehler (kein stilles Überschreiben) |
| Persistenz Source-/WorkingDirectory | Bestehendes Plugin-Settings-Schema im Credential Store |
| `GetIssuesAsync` im LocalDirectoryPlugin | `NotSupportedException` |
| Sichtbarkeit `GitPluginBase<TPlugin>` | `public` |

---

## 4. Konsolidiertes Zielbild

- Neues `LocalDirectoryPlugin` unterstützt lokale Workspaces für:
  - `CloneRepositoryAsync`
  - `CreateBranchAsync`
  - `CommitAsync`
  - `ResetAsync`
- `WorkspaceMode` (`InSourceDirectory`, `SeparateWorkingDirectory`) ist als Enum/Select in Settings verfügbar und wird stabil serialisiert.
- Nicht unterstützte Remote-Funktionen (`Push/Pull/PR/Issues/...`) werden klar und konsistent als `NotSupportedException` behandelt.
- `GitHubPlugin` wird auf gemeinsame Git-Bausteine via `GitPluginBase<TPlugin>` refaktoriert.

---

## 5. Review-Ergebnis und priorisierte Nacharbeiten

**Architektururteil:** Freigabe mit Auflagen.

Priorisierte Maßnahmen:

1. Confirm-Contract (`git init`) als klarer Application-Policy-Contract
2. Security-Hardening für lokale Pfad-/Copy-Operationen
3. Capability-getriebene UI-Gates für NotSupported-Funktionen
4. Verbindliche Testfallmatrix inkl. Guardrail-/Abbruchszenarien

Details: [Architecture Review](improvements/lokales-verzeichnis-plugin-architecture-review.md)

---

## 6. Nächste Umsetzungsschritte

1. Contracts erweitern (`WorkspaceMode`, `PluginSettingFieldType.Enum`, `GitPluginBase<TPlugin>`).
2. `GitHubPlugin` auf Basisklasse refaktorieren (ohne Verhaltensänderung).
3. `LocalDirectoryPlugin` implementieren (Clone/Branch/Commit/Reset + NotSupported Remote).
4. Settings-UI + Serialisierung finalisieren.
5. Unit-/Integrationstests ergänzen und Gates ausführen:
   - `dotnet build Softwareschmiede.slnx`
   - `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
   - `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`

---

*Erstellt durch den planning-orchestrator mit Unteragenten: planning-requirements-analysis · planning-architecture-blueprint · planning-entity-relationship-modeler · review-architecture*
