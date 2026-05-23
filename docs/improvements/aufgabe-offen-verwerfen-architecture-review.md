# Architektur-Review – Offene Aufgabe verwerfen (`Offen → Archiviert / gelöscht`)

> **Dokument-Typ:** Architecture Review  
> **Status:** ✅ Freigabe  
> **Datum:** 2026-05-23

---

## 1. Executive Summary

Die geplante Lösung ist architektonisch sauber und minimal invasiv.  
Sie ergänzt einen expliziten Kurzschluss-Pfad für `Offen`-Aufgaben, ohne bestehende Guards oder das Datenbankschema zu verändern.

**Gesamtbewertung:** ✅ Freigabe

---

## 2. Konsistenzprüfung Requirements ↔ Blueprint ↔ ERM

| Aspekt | Konform? | Hinweis |
|---|---|---|
| Kein Schema-Change | ✅ | Kein neues Feld, keine Migration, kein neuer Status |
| `VerwerfenAsync` nur für `Offen` | ✅ | Guard ist im Blueprint klar beschrieben |
| Zwei Zielpfade | ✅ | `Archiviert` oder physisches Löschen |
| Bestätigungsdialog | ✅ | Eigener Dialog mit klarer Nutzerentscheidung |
| Weiterleitung nach Erfolg | ✅ | Zur Projektübersicht / Projektdetail zurück |
| Datenmodelländerung erforderlich | ❌ | Nicht erforderlich |

---

## 3. Bewertungsmatrix

| Bereich | Bewertung | Kurzbegründung |
|---|---|---|
| Systemarchitektur | Sehr gut | Additive Erweiterung im bestehenden `AufgabeService` |
| Datenkonsistenz | Sehr gut | Status-Guard verhindert unzulässige Verwerfung |
| UI/UX | Gut | Eigener Button und Bestätigungsdialog trennen die Aktion klar |
| Nachvollziehbarkeit | Gut | Strukturierte Logs reichen für den vorgesehenen Scope |

---

## 4. Findings

Keine Blocker.

---

## 5. Bestätigte Entscheidungen

1. Kein eigener Service für das Feature.
2. Kein neuer `AufgabeStatus`.
3. Kein ERM- oder Migrationsbedarf.
4. `VerwerfenAktion` bleibt Laufzeitparameter.
5. `DeleteAsync` wird für den Löschpfad weiterverwendet.

---

## 6. Verlinkung

- Requirements: [../requirements/aufgabe-offen-verwerfen-requirements-analysis.md](../requirements/aufgabe-offen-verwerfen-requirements-analysis.md)
- Architektur-Blueprint: [../architecture/aufgabe-offen-verwerfen-architecture-blueprint.md](../architecture/aufgabe-offen-verwerfen-architecture-blueprint.md)
- ERM: [../architecture/aufgabe-offen-verwerfen-entity-relationship-model.md](../architecture/aufgabe-offen-verwerfen-entity-relationship-model.md)
