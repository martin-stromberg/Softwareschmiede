# Architektur-Review – AufgabeDetail UI Register-Navigation

> **Dokument-Typ:** Architektur-Review  
> **Status:** Freigabe mit Auflagen (Umsetzung fortführen)  
> **Version:** 1.2.0

---

## 1. Referenzen

- Requirements: [`../requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md`](../requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md)
- Architektur-Blueprint: [`../architecture/aufgabe-detail-ui-register-navigation-architecture-blueprint.md`](../architecture/aufgabe-detail-ui-register-navigation-architecture-blueprint.md)
- ERM: [`../architecture/aufgabe-detail-ui-register-navigation-entity-relationship-model.md`](../architecture/aufgabe-detail-ui-register-navigation-entity-relationship-model.md)
- Übersicht: [`../planning-overview-aufgabe-detail-ui-register-navigation.md`](../planning-overview-aufgabe-detail-ui-register-navigation.md)
- Ausgangsaufgabe: [`../../980a1311-2ba6-455b-95c3-ca4147fd36fd.copilot-task.md`](../../980a1311-2ba6-455b-95c3-ca4147fd36fd.copilot-task.md)

---

## 2. Executive Summary

Die Architektur ist konsistent zu Requirements v1.2.0 und adressiert das zentrale Problembild (3 Register, globale Infoboxen, Git-Dialoge im Projektverzeichnis).  
Der Umsetzungsstand ist weiterhin **begonnen, aber nicht abgeschlossen**. Für die fachliche/produktive Abnahme fehlen noch vollständige Nachweise zu Performance und Accessibility.

**Freigabeempfehlung:** ⚠️ **Freigabe mit Auflagen**  
- Fortführung der Implementierung ist freigegeben  
- Endfreigabe erst nach Erfüllung der Release Gates G1–G8

---

## 3. Priorisierte Findings

### Blocker (vor Endfreigabe)

| ID | Finding | Risiko |
|---|---|---|
| F-B01 | NFR-2 ist nicht vollständig operationalisiert (Messumgebung/-methode nicht final fixiert). | Qualitätsziel nicht objektiv nachweisbar |
| F-B02 | NFR-5/A11y-Prüfumfang (Tastaturpfade, Fokusverhalten) nicht vollständig abnahmeklar. | Bedien-/A11y-Regressionen |

### Major

| ID | Finding | Risiko |
|---|---|---|
| F-M01 | FR-2-Kennzahlen müssen verbindlich als feste Liste inkl. Datenquelle/Fallback umgesetzt werden. | Uneinheitliche Anzeige / Abnahmerisiko |
| F-M02 | Traceability Requirement ↔ Testfall ist noch nicht vollständig dokumentiert. | Lückenhafte Nachweisführung |

### Minor

| ID | Finding | Risiko |
|---|---|---|
| F-m01 | Reifegradhinweise über Artefakte hinweg uneinheitlich formuliert. | Missverständnisse im Team |
| F-m02 | Query-Fallback-Verhalten sollte im Testreport explizit separat ausgewiesen werden. | geringes Diagnose-Risiko |

---

## 4. Maßnahmen (priorisiert)

### P0 (sofort)
1. Zielumgebung und Messmethode für NFR-2 verbindlich festlegen.
2. A11y-Prüfumfang (Keyboard-Matrix, Fokusregeln, ARIA-Checks) verbindlich definieren.

### P1 (vor Endfreigabe)
3. FR-2-Kennzahlenliste finalisieren und mit Fallbackregeln im UI absichern.
4. Traceability-Matrix für FR/NFR zu G1–G8 vervollständigen.
5. Nachweisdokumentation (Testergebnisse + Messprotokolle) aktualisieren.

### P2 (Stabilisierung)
6. Reifegrad- und Freigabetexte in allen Artefakten sprachlich vereinheitlichen.

---

## 5. Test-/Qualitätsauflagen (Release Gates)

- **G1:** Register-Exklusivität für alle Wechselpfade (inkl. Query-Init/Fallback)  
- **G2:** Git-Dialoge nur im Projektverzeichnis sichtbar & interaktiv  
- **G3:** Globale Infoboxen in allen drei Registern sichtbar  
- **G4:** Legacy-„Ansicht“-Box inkl. Alt-State vollständig entfernt  
- **G5:** FR-2-Kennzahlen inkl. Empty-/Error-Pfad getestet  
- **G6:** Performancenachweis gemäß NFR-2 dokumentiert  
- **G7:** Accessibility-Nachweis (ARIA + Tastaturfluss) dokumentiert  
- **G8:** Regressionstests für Startskript/Abschließen/Abbrechen/Git grün

---

## 6. Freigabeentscheidung

**Freigabe mit Auflagen.**  
Die Implementierung darf fortgeführt werden.  
Die **produktive Endfreigabe** erfolgt erst nach Abschluss von P0/P1 und grünem Nachweis aller Gates G1–G8.

---

## 7. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-24 | review-architecture | Erstfassung |
| 1.1.0 | 2026-05-25 | planning-orchestrator | Findings/Maßnahmen auf Requirements/Architektur/ERM v1.1.0 harmonisiert |
| 1.2.0 | 2026-05-25 | planning-orchestrator | Review auf Requirements/Blueprint/ERM v1.2.0 aktualisiert, Status „Implementierung begonnen“ berücksichtigt, Maßnahmen und Gates geschärft |
