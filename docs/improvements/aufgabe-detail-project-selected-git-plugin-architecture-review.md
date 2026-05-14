# Architektur-Review – AufgabeDetail: projektspezifisches Git-Plugin

> **Dokument-Typ:** Architektur-Review  
> **Status:** Freigabe mit Auflagen  
> **Version:** 1.0.0

---

## 1. Referenzen

- Requirements: [`../requirements/aufgabe-detail-project-selected-git-plugin-requirements-analysis.md`](../requirements/aufgabe-detail-project-selected-git-plugin-requirements-analysis.md)
- Architektur: [`../architecture/aufgabe-detail-project-selected-git-plugin-architecture-blueprint.md`](../architecture/aufgabe-detail-project-selected-git-plugin-architecture-blueprint.md)
- ERM: [`../architecture/aufgabe-detail-project-selected-git-plugin-entity-relationship-model.md`](../architecture/aufgabe-detail-project-selected-git-plugin-entity-relationship-model.md)
- Übersicht: [`../planning-overview-aufgabe-detail-project-selected-git-plugin.md`](../planning-overview-aufgabe-detail-project-selected-git-plugin.md)

---

## 2. Executive Summary

Die geplante Korrektur ist fachlich notwendig: aktuell kann `GitOrchestrationService` das falsche Plugin nutzen (DI-Default statt Aufgabenkontext).  
Die geplante Zentralisierung der Plugin-Auflösung im Service ist korrekt.

**Bewertung:** ✅ Umsetzbar, **mit Auflagen**.

---

## 3. Bewertungsmatrix

| Bereich | Bewertung | Kommentar |
|---|---|---|
| Architekturkonsistenz | Gut | passt zu bestehender PluginSelection |
| Testbarkeit | Gut | zwei klare Pflichttests definierbar |
| Risiko | Mittel | Hauptgefahr: weiter statische Plugin-Nutzung |
| Datenmodell | Sehr gut | keine Schemaänderung nötig |

---

## 4. Findings

| ID | Prio | Finding | Risiko |
|---|---|---|---|
| F-01 | Blocker | Statische Nutzung eines injizierten `_gitPlugin` im Service | falsches Plugin in produktiven Git-Aktionen |
| F-02 | Major | Test prüft nicht ausreichend Laufzeitverhalten gegen konkurrierendes Default-Plugin | praxisferne Absicherung |
| F-03 | Major | Kein dedizierter `LocalDirectoryPlugin`-Auswahltest | lokale Repositories nicht explizit abgesichert |
| F-04 | Minor | Fallback-Regel bei fehlender Repo-Zuordnung nicht eindeutig dokumentiert | uneinheitliches Verhalten |

---

## 5. Maßnahmen

1. Resolver in `GitOrchestrationService` für effektives Plugin pro Aufgabe einführen.
2. Alle relevanten Git-Methoden auf resolvertes Plugin umstellen.
3. Test `AufgabeDetail_ShouldUseProjectSelectedGitPlugin_InInjectedGitOrchestrationService` auf verhaltensnahe Prüfung umbauen.
4. Zweiten Pflichttest für `LocalDirectoryPlugin` ergänzen.
5. Fallback-Regel dokumentieren und mit mindestens einem Test absichern.

---

## 6. Test-/Qualitätsauflagen

### Pflichttest A (Remote/GitHub)
- Setup: selected = `Softwareschmiede.GitHub`, default = `LocalDirectoryPlugin`
- Erwartung: effektive Nutzung GitHub-Plugin

### Pflichttest B (LocalDirectory)
- Setup: selected = `LocalDirectoryPlugin`, default = `Softwareschmiede.GitHub`
- Erwartung: effektive Nutzung LocalDirectory-Plugin

### Weitere Auflagen
- Bestehende Capability-Button-Tests bleiben grün.
- Keine Migration.

---

## 7. Freigabeempfehlung

Freigabe zur Implementierung nach Schließen von **F-01 bis F-03**.  
Merge-Empfehlung erst nach grünem Nachweis beider Pflichttests.

