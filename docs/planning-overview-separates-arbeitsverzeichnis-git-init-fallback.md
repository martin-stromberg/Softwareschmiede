# Planungsübersicht – Separates Arbeitsverzeichnis mit Git-Workflow (v2.0.0)

> **Dokument-Typ:** Planungsoverview (planning-orchestrator)  
> **Status:** ✅ Vollständiger Planungsdurchlauf abgeschlossen / konsolidiert  
> **Version:** 2.0.0  
> **Datum:** 2026-05-13

---

## 1. Durchlaufstatus

Der sequenzielle Planungsablauf wurde vollständig abgeschlossen:
1. Requirements Analysis
2. Architecture Blueprint
3. Entity-Relationship Modeling
4. Architecture Review
5. Orchestrator-Konsolidierung (dieses Dokument)

## 2. Kernartefakte (konsolidiert auf v2.0.0)

| Bereich | Datei | Stand |
|---|---|---|
| Anforderungen | [requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md](requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md) | v2.0.0 |
| Architektur | [architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md) | v2.0.0 |
| ERM | [architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md](architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md) | v2.0.0 |
| Review | [improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md](improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md) | v2.0.0 |

## 3. Konsolidierte Entscheidungen (2.0.0)

1. **Pull ohne Merge:**  
   Im Modus `SeparateWorkingDirectory` ist Pull ein eigener No-Merge-Flow mit verpflichtender Bestätigung.
2. **Push als Dateisynchronisation:**  
   Push führt **kein `git push`** aus, sondern `WorkingDirectory -> SourceDirectory` als Copy/Overwrite-Sync.
3. **Delete-Sync via Git-Änderungserkennung:**  
   Löschungen werden über Git-Status im Working Directory ermittelt und im Source Directory gespiegelt.
4. **Git im separaten Working Directory:**  
   Fehlt `.git`, wird abhängig von Policy `git init` im Working Directory ausgeführt; lokaler Commit-Flow wird ermöglicht.
5. **Legacy-Schutz:**  
   Bei `WorkingDirectory == SourceDirectory` bleibt der Legacy-Workflow unverändert (regressionsfrei).

## 4. Konsolidierte Risiken aus dem Review

### Blocker (vor Implementierungsfreigabe zu schließen)
1. Fehlender atomarer/fail-safe Konsistenzvertrag für Push (Copy + Delete) bei Teilfehlern.
2. Unklarer Sync-Scope (`.git`, Symlinks, Hidden Files, Rechte, Locks) und fehlende Guardrails.
3. Unvollständige Delete-Sync-Regeln für Git-Statusvarianten (u. a. rename/typechange/staged vs. unstaged).

### Major
4. Nicht spezifiziertes Parallelitäts-/Race-Verhalten bei konkurrierenden Push/Pull.
5. Pull-ohne-Merge fachlich definiert, technisch noch unpräzise (Quelle/Konfliktbehandlung).
6. Legacy-Regressionsziel ohne vollständige Kompatibilitätsmatrix.

### Minor
7. Telemetrie ohne verbindliche KPI-/SLO-Schwellen.
8. `git init`-/Initial-Commit-Policy (Autor/Commit-Message/Audit) noch nicht final.

## 5. Umsetzbare Reihenfolge (aus Review + Blueprint)

### P0 – vor Implementierungsstart
1. Push-Konsistenzmodell verbindlich festlegen (atomar oder expliziter fail-safe Zustand).
2. Sync-Scope-/Security-Regeln als Normtabelle spezifizieren.
3. Delete-Sync-Status-Mapping inkl. Testorakel finalisieren.

### P1 – während Implementierung
4. Locking-Strategie für konkurrierende Operationen definieren und umsetzen.
5. Pull-No-Merge technisch präzisieren (Datenquelle, Divergenzen, Fehlercodes).
6. Legacy-Kompatibilitätsmatrix aufbauen und in Regressionstests überführen.

### P2 – vor Release
7. KPI/SLO und Alerting-Grenzen für Observability finalisieren.
8. Auditierbare `git init`-/Commit-Metadaten standardisieren und testen.

## 6. Freigabeempfehlung

- **Planungsstand 2.0.0 ist konsistent**, aber **noch nicht implementierungsreif ohne P0-Abschluss**.  
- Nach Abschluss der P0-Punkte kann Umsetzung mit den in Blueprint/ERM definierten Services und Invarianten starten.

---

*Konsolidiert durch planning-orchestrator (sequenziell: planning-requirements-analysis → planning-architecture-blueprint → planning-entity-relationship-modeler → review-architecture).*
