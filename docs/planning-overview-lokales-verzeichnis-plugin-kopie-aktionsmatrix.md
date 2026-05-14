# Planungsübersicht – Lokales Verzeichnis Plugin Kopie-Aktionsmatrix

> **Dokument-Typ:** Planungsoverview (planning-orchestrator)
> **Status:** ✅ Vollständiger Planungsdurchlauf abgeschlossen / konsolidiert
> **Version:** 1.0.0
> **Datum:** 2026-05-14

---

## 1. Durchlaufstatus

Der sequenzielle Planungsablauf wurde abgeschlossen:

1. Requirements Analysis
2. Architecture Blueprint
3. Entity-Relationship Modeling
4. Architecture Review
5. Orchestrator-Konsolidierung

## 2. Kernartefakte

| Bereich | Datei | Stand |
|---|---|---|
| Anforderungen | [requirements/lokales-verzeichnis-plugin-kopie-aktionsmatrix-requirements-analysis.md](requirements/lokales-verzeichnis-plugin-kopie-aktionsmatrix-requirements-analysis.md) | v1.0.0 |
| Architektur | [architecture/lokales-verzeichnis-plugin-kopie-aktionsmatrix-architecture-blueprint.md](architecture/lokales-verzeichnis-plugin-kopie-aktionsmatrix-architecture-blueprint.md) | v1.0.0 |
| ERM | [architecture/lokales-verzeichnis-plugin-kopie-aktionsmatrix-entity-relationship-model.md](architecture/lokales-verzeichnis-plugin-kopie-aktionsmatrix-entity-relationship-model.md) | v1.0.0 |
| Review | [improvements/lokales-verzeichnis-plugin-kopie-aktionsmatrix-architecture-review.md](improvements/lokales-verzeichnis-plugin-kopie-aktionsmatrix-architecture-review.md) | v1.0.0 |

## 3. Konsolidierte Entscheidungen

1. **Policy-first für Copy-Flow**  
   Bei `RepositoryKind=LocalDirectory` und `IsWorkingDirectoryCopy=true` sind Push/Pull/PR fachlich ausgeblendet.
2. **Merge statt Remote-Aktionen im Copy-Flow**  
   UI zeigt Merge ausschließlich capability-gesteuert (`CanMergeToSource`).
3. **Plugin liefert vollständige Capabilities**  
   Flags `CanPush`, `CanPull`, `CanCreatePullRequest`, `CanMergeToSource`, plus Kontextfelder `RepositoryKind`, `IsWorkingDirectoryCopy`.
4. **Ein zentraler UI-Decision-Point**  
   Alle betroffenen Views nutzen dieselbe Aktionsmatrix-Entscheidung (keine verteilten Sonderlogiken).
5. **Remote-Git-Verhalten bleibt kompatibel**  
   Außerhalb des Copy-Sonderfalls gelten die bestehenden Flag-Regeln unverändert.

## 4. Transparente Annahmen

1. „Merge“ im lokalen Copy-Kontext ist fachlich eine Übernahme von Änderungen ins Quellverzeichnis und kein klassischer Remote-/PR-Flow.
2. `IsWorkingDirectoryCopy` ist ein verlässliches Plugin-Signal und wird nicht in der UI heuristisch abgeleitet.
3. Konflikt- und Fehlerbehandlung für Merge wird vor Umsetzung gemäß Review-Befunden präzisiert.

## 5. Risiken aus dem Review

- **Blocker:** Merge-Fehlermodell (Atomicität/Recovery/Teilzustände) muss vor Implementierungsstart finalisiert werden.
- **Major:** Flag-Semantik und Invarianten müssen vertraglich geschärft und per Contract-Tests abgesichert werden.
- **Major:** Zentraler Decision-Point muss technisch erzwungen werden, um UI-Inkonsistenzen zu vermeiden.
- **Major:** Standardisierte Reason-Codes/Diagnose-Logs fehlen noch.
- **Minor:** Performance-Nachweis für die Aktionsentscheidung ist zu konkretisieren.

## 6. Umsetzbare Reihenfolge

1. Capability-Vertrag + Invarianten finalisieren.
2. Merge-Fehlermodell und Recovery-Strategie spezifizieren.
3. Zentrale Action-Visibility-Policy implementieren.
4. Matrix-basierte Unit-/Integration-/UI-Tests ergänzen.
5. Performance- und Determinismus-Nachweis als DoD prüfen.

## 7. Freigabeempfehlung

- Planungsstand ist konsistent.
- Implementierungsstart nach Schließen des Blockers aus dem Architektur-Review.

---

*Konsolidiert durch planning-orchestrator (Requirements → Architecture → ERM → Review).*
