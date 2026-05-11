# Architektur-Review – Kontextsteuerung bei Folgeanweisungen

> **Dokument-Typ:** Architektur-Review  
> **Projekt:** Softwareschmiede  
> **Scope:** Requirements, Blueprint und ERM für die Kontextsteuerung bei Folgeanweisungen  
> **Datum:** 2026-05-11

---

## 1. Referenzen

- Requirements: [`../requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md`](../requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md)
- Architektur-Blueprint: [`../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md`](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md)
- ERM: [`../architecture/kontextsteuerung-folgeanweisungen-entity-relationship-model.md`](../architecture/kontextsteuerung-folgeanweisungen-entity-relationship-model.md)

---

## 2. Executive Summary

Die Planungsartefakte sind konsistent und decken die Kernanforderungen ab: Persistenz in `{id}.copilot.context.md`, deterministische Prompt-Reihenfolge, Komprimierung bei Größenlimit und exakt drei UI-Modi.  
Mit den aktuellen Blueprint-/ERM-Ergänzungen (atomisches Schreiben, Hard-Limit-Strategie, `RunId`/`ContextEventId`) ist die Lösung fachlich tragfähig.  
Offen bleiben verbindliche Umsetzungsdetails in Fehlerpfaden und UX-Schutz beim Modus **„Kontext neu beginnen“**.

**Gesamtbewertung:** ⚠️ **Freigabe mit Auflagen (umsetzungsnah)**

---

## 3. Bewertungsmatrix

| Bereich | Bewertung | Kurzbegründung |
|---|---|---|
| Systemarchitektur | Gut | Klare Schichtung und nachvollziehbare Verantwortlichkeiten |
| Datenmodell | Gut | Datei-/Laufzeitmodell ist passend, DB-Migration nicht erforderlich |
| UI/UX | Gut | Drei Modi klar definiert, Verhalten je Modus nachvollziehbar |
| Robustheit | Mittel | Fehlerfälle bei Komprimierung und Dateioperationen müssen geschärft werden |
| Nachvollziehbarkeit | Gut | Audit-Ziel definiert, konkrete Event-Korrelation empfohlen |

---

## 4. Findings

| ID | Priorität | Bereich | Finding | Maßnahme |
|---|---|---|---|---|
| F-01 | Blocker | AK-/Fehlerpfad-Konsistenz | AC-1.1 impliziert immer Nutzer+KI-Antwort. In Abbruchfällen (Hard-Limit/Plugin-Fehler) ist das nicht erfüllbar. | AC-Präzisierung: bei Fehlschlag verpflichtender Systemeintrag mit `RunId` statt KI-Antwort. |
| F-02 | Major | Zuverlässigkeit | Atomisches Schreiben/Recovery sind im Blueprint benannt, aber noch als technische Entscheidung umzusetzen. | Verbindliche Implementierungsregeln (`temp + fsync + replace + .bak`) und Wiederanlaufverhalten festlegen. |
| F-03 | Major | Audit | `RunId`/`ContextEventId` sind modelliert, aber noch nicht als Pflichtfeldausgabe für jeden Lauf spezifiziert. | Korrelationsfelder in Protokollausgabe und Kontexteinträgen verpflichtend machen. |
| F-04 | Medium | UX | Modus „Kontext neu beginnen“ ist potenziell destruktiv. Aktuell nur Hinweis, keine zwingende Schutzinteraktion. | 2-Schritt-Bestätigung + irreversible Warnung vor Senden einführen. |
| F-05 | Low | Performance/Retention | Messung und Aufbewahrung für Komprimierungs-/Backup-Pfade noch nicht operationalisiert. | SLA-Aufteilung (Normal vs. Komprimierung) und Retention-Regeln für Backups definieren. |

---

## 5. Verifikation gegen Akzeptanzkriterien

| Kriterium | Ergebnis | Nachweis |
|---|---|---|
| Speicherung in `{id}.copilot.context.md` | Erfüllt (Plan) | Requirements FR-1, ERM `Kontextdatei` |
| Kontext vor Nutzeranweisung | Erfüllt (Plan) | Requirements FR-2, Blueprint Prompt-Komposition |
| Komprimierung bei zu großer Datei | Erfüllt mit Auflagen | Requirements FR-3, Blueprint Limits, Findings F-02/F-05 |
| Genau drei UI-Optionen | Erfüllt mit UX-Auflage | Requirements FR-4, Blueprint UI/UX, Finding F-04 |

---

## 6. Empfehlung

Die Planung ist tragfähig. Vor Implementierung sind die Auflagen **F-01** bis **F-04** verbindlich als Umsetzungsregeln zu fixieren. Danach ist die Umsetzung freigabefähig.

