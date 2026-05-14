# Planungsübersicht – Separates Arbeitsverzeichnis mit Source-Copy und Git-Bootstrap (v3.0.0)

> **Dokument-Typ:** Planungsoverview (planning-orchestrator)
> **Status:** ✅ Vollständiger Planungsdurchlauf abgeschlossen / konsolidiert
> **Version:** 3.0.0
> **Datum:** 2026-05-13

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
| Anforderungen | [requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md](requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md) | v3.0.0 |
| Architektur | [architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md) | v3.0.0 |
| ERM | [architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md](architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md) | v3.0.0 |
| Review | [improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md](improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md) | v3.0.0 |

## 3. Konsolidierte Entscheidungen

1. **Source bleibt unberührt**
   Das Quellverzeichnis wird nicht initialisiert und nicht fachlich verändert.
2. **Copy vor Git-Bootstrap**
   Die Arbeitskopie wird per Dateikopie erzeugt; danach folgt `git init` im Arbeitsverzeichnis.
3. **Keine Git-Init-Option im separaten Modus**
   Die Einstellung ist dort nicht benutzbar, sondern ein festes Systemverhalten.
4. **Legacy-Modus bleibt getrennt**
   `InSourceDirectory` wird weiterhin separat behandelt.

## 4. Risiken aus dem Review

### Blocker
1. Copy-Scope muss verbindlich begrenzt werden.
2. Zielverzeichniszustand vor Copy/Init muss fail-fast geprüft werden.
3. Git-Init darf in keiner separaten UI-Ansicht konfigurierbar bleiben.

### Major
4. Verhalten bei bereits existierendem Git-Repository im Source-Baum muss eindeutig definiert sein.
5. Fehlerklassen für Copy und Bootstrap müssen normiert werden.

## 5. Umsetzbare Reihenfolge

1. Copy-/Exclude-Regeln festziehen.
2. Zielverzeichnis-Guardrails spezifizieren.
3. UI-Settings-Projection für das separate Arbeitsverzeichnis konsolidieren.
4. Danach Implementierung und Tests auf Basis der Blueprint-/ERM-Vorgaben starten.

## 6. Freigabeempfehlung

- **Planungsstand 3.0.0 ist konsistent.**
- Implementierungsfreigabe erst nach Klärung der Blocker aus dem Review.

---

*Konsolidiert durch planning-orchestrator (Requirements → Architecture → ERM → Review).*
