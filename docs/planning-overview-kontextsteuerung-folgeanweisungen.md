# Planungsübersicht – Kontextsteuerung bei Folgeanweisungen

> **Dokument-Typ:** Planungsübersicht (Orchestrator-Ergebnis)  
> **Status:** ✅ Planungsphase abgeschlossen  
> **Datum:** 2026-05-11

## 1. Anlass
Für Folgeanweisungen soll ein steuerbarer Kontextfluss eingeführt werden: Verlauf in `{id}.copilot.context.md` persistieren, vor Anweisungen einfügen, bei Größe komprimieren und per UI zwischen drei Kontextmodi wählen.

## 2. Erstellte/aktualisierte Artefakte
| Dokument | Zweck | Link |
|---|---|---|
| Requirements Analysis | Ziele, FR/NFR, Akzeptanzkriterien, Scope | [requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md](requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md) |
| Architektur-Blueprint | Zielarchitektur, Schichten, Ablauf, Qualitätsziele | [architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md](architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) |
| ERM | Datei-/Laufzeitmodell, Entitäten, Beziehungen, Regeln | [architecture/kontextsteuerung-folgeanweisungen-entity-relationship-model.md](architecture/kontextsteuerung-folgeanweisungen-entity-relationship-model.md) |
| Architektur-Review | Review-Befunde, Risiken, Maßnahmen | [improvements/kontextsteuerung-folgeanweisungen-architecture-review.md](improvements/kontextsteuerung-folgeanweisungen-architecture-review.md) |

## 3. Kernentscheidungen
1. Kontext wird pro Aufgabe dateibasiert in `{id}.copilot.context.md` geführt.
2. Prompt-Bildung ist deterministisch pro Modus:
   - **Kontext mitgeben:** `Kontext → Nutzeranweisung`
   - **Kontext ignorieren:** nur `Nutzeranweisung`
   - **Kontext neu beginnen:** Reset, dann nur `Nutzeranweisung`
3. Bei Überschreitung konfigurierter Grenzwerte wird eine KI-Komprimierung vor Prompt-Erstellung ausgelöst.
4. Die UI bietet exakt drei explizite Kontextoptionen ohne zusätzliche Alternative.
5. DB-Schema bleibt unverändert; Modellierung erfolgt auf Datei-/Laufzeitebene.

## 4. Priorisierte Maßnahmen aus dem Review
- **Blocker:** AK-/Fehlerpfad-Konsistenz präzisieren (Systemeintrag statt KI-Antwort bei Abbruchfällen).
- **Major:** Atomisches Schreiben + Recovery für Reset/Komprimierung verbindlich umsetzen.
- **Major:** Audit-Korrelation (`RunId`, `ContextEventId`) als Pflichtausgabe definieren.
- **Medium:** Schutzinteraktion für „Kontext neu beginnen“ (2-Schritt-Bestätigung) ergänzen.

## 5. Traceability
Alle Artefakte sind gegenseitig verlinkt und decken den vollständigen Planungsablauf ab: Anforderungsanalyse → Architektur-Blueprint → ERM → Architektur-Review.

