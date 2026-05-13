# Planungsübersicht – Separates Arbeitsverzeichnis mit git-init-/Copy-Fallback

> **Dokument-Typ:** Planungsoverview (planning-orchestrator)  
> **Status:** ✅ Vollständiger Planungsdurchlauf abgeschlossen  
> **Datum:** 2026-05-13

---

## 1. Durchlaufstatus

Die Orchestrator-Schritte wurden vollständig durchgeführt:
1. Requirements Analysis
2. Architecture Blueprint
3. Entity-Relationship Modeling
4. Architecture Review
5. Konsolidierung

## 2. Artefakte

| Bereich | Datei | Stand |
|---|---|---|
| Anforderungen | [docs/requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md](requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md) | v1.0.0 |
| Architektur | [docs/architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md) | v1.0.0 |
| ERM | [docs/architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md](architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md) | v1.0.0 |
| Review | [docs/improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md](improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md) | v1.0.0 |

## 3. Konsolidierte Architekturentscheidungen

- Vor jedem Clone wird die Git-Basis der lokalen Quelle geprüft.
- Nicht-Git + init aktiv: `git init` im Quellverzeichnis, danach Clone.
- Nicht-Git + init deaktiviert: Copy-Fallback ohne Clone.
- Bereits git-basierte Quellen bleiben rückwärtskompatibel.
- Fehlerpfade und Cleanup sind als verpflichtender Teil der Lösung definiert.

## 4. Hauptrisiken und Auflagen aus dem Review

1. Entscheidungsmatrix und Quellmutationsregeln müssen vor Implementierung finalisiert werden.
2. Concurrency-/Cleanup-Vertrag ist verbindlich zu spezifizieren.
3. Fehlercodes und strukturierte Logs müssen vollständig geplant werden.

## 5. Umsetzungsreihenfolge (empfohlen)

1. Entscheidungsmatrix + Fehlerkatalog finalisieren.
2. Strategielogik im LocalDirectoryPlugin implementieren.
3. Cleanup-/Idempotenzverhalten absichern.
4. Unit- und Integrationstests über alle Zweige.
5. Dokumentationsabgleich in Flow/API-Dokumenten.

---

*Erstellt durch planning-orchestrator (sequenziell: planning-requirements-analysis → planning-architecture-blueprint → planning-entity-relationship-modeler → review-architecture).*

