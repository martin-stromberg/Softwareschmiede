# Architektur-Review – Pull-Request-Repository-ID entfernen

> **Dokument-Typ:** Architektur-Review  
> **Projekt:** Softwareschmiede  
> **Scope:** Bewertung des vereinfachten PR-Flows ohne manuelle Repository-ID-Eingabe  
> **Datum:** 2026-05-11

---

## 1. Executive Summary

Die Änderung ist fachlich sinnvoll und reduziert Fehlbedienung. Der Hauptnutzen liegt in einer klareren UI und einer eindeutigen Repository-Quelle. Kritisch ist nur die Frage, wie bei mehreren Repositories pro Projekt deterministisch entschieden wird.

**Gesamtbewertung:** ✅ **Freigabe mit kleiner Auflage**

---

## 2. Bewertungsmatrix

| Bereich | Bewertung | Kurzbegründung |
|---|---|---|
| Systemarchitektur | Gut | Die Repository-Ermittlung bleibt zentral im Service. |
| UI/UX | Sehr gut | Redundantes Feld entfällt. |
| Datenmodell | Sehr gut | Keine Schemaänderung nötig. |
| Testbarkeit | Gut | Standard- und Fehlerpfad sind gut testbar. |

---

## 3. Findings

| ID | Priorität | Bereich | Finding | Risiko |
|---|---|---|---|---|
| F-01 | Medium | Konsistenz | Manuelle Repository-ID kann von der tatsächlichen Projektzuordnung abweichen. | Falsches Repository im PR |
| F-02 | Medium | Domäne | Mehrere Repositories pro Projekt brauchen eine klare Auswahlregel. | Nicht-deterministisches Verhalten |
| F-03 | Low | Fehlerbehandlung | Fehlende Repository-Zuordnung muss früh und klar gemeldet werden. | Schlechte UX im Ausnahmefall |

---

## 4. Verbesserungsvorschläge

### M-01 – Repository-Override aus dem UI entfernen
Das PR-Formular soll nur Titel und Beschreibung anbieten.

### M-02 – Repository-Ermittlung im Service kapseln
Die Auswahl des Repositorys bleibt ausschließlich in `GitOrchestrationService`.

### M-03 – Fehlerpfad absichern
Wenn kein Repository vorhanden ist, erfolgt ein klarer Abbruch mit verständlicher Meldung.

### M-04 – Tests ergänzen
Tests für den PR-Standardpfad und den fehlenden-Repository-Fall hinzufügen.

---

## 5. Freigabeempfehlung

Die Änderung kann umgesetzt werden, sobald die Auswahlregel für mehrere Repositories dokumentiert ist. Für den aktuellen Ein-Repository-Standardfall ist die Lösung unkritisch.

