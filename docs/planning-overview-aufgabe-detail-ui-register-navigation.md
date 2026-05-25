# Planungsübersicht – AufgabeDetail UI Register-Navigation

> **Dokument-Typ:** Planning Overview  
> **Status:** Konsolidiert (Implementierung weiterhin in Arbeit)  
> **Version:** 1.2.0

---

## 1. Primäre Anforderungsquelle

Ausgangsanforderung: UI-Neuordnung der Seite `AufgabeDetail` in drei Register (**Aufgabe**, **Ausführung**, **Projektverzeichnis**) mit:

- global sichtbaren Infoboxen für **Commits** und **geänderte Dateien**,
- robust sichtbaren Git-Dialogen (Commit/Push/Pull/Pull Request) im Register **Projektverzeichnis**,
- Entfernung der bisherigen **Ansichts-Box**.

---

## 2. Orchestrierter Ablauf (vollständig durchgeführt)

1. **Requirements Analysis**  
   `docs/requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md`
2. **Architecture Blueprint**  
   `docs/architecture/aufgabe-detail-ui-register-navigation-architecture-blueprint.md`
3. **Entity-Relationship Model**  
   `docs/architecture/aufgabe-detail-ui-register-navigation-entity-relationship-model.md`
4. **Architecture Review**  
   `docs/improvements/aufgabe-detail-ui-register-navigation-architecture-review.md`
5. **Konsolidierung/Verlinkung**  
   dieses Dokument als zentrale Übersicht

---

## 3. Erzeugte/aktualisierte Planungsdokumente

- Anforderungen: [requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md](requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md)
- Architektur: [architecture/aufgabe-detail-ui-register-navigation-architecture-blueprint.md](architecture/aufgabe-detail-ui-register-navigation-architecture-blueprint.md)
- ERM: [architecture/aufgabe-detail-ui-register-navigation-entity-relationship-model.md](architecture/aufgabe-detail-ui-register-navigation-entity-relationship-model.md)
- Review: [improvements/aufgabe-detail-ui-register-navigation-architecture-review.md](improvements/aufgabe-detail-ui-register-navigation-architecture-review.md)

Alle Artefakte sind gegenseitig verlinkt und auf denselben Feature-Scope abgestimmt.

---

## 4. Konsolidiertes Ergebnis

- Zielarchitektur: klares 3-Register-Modell mit exklusiver Sichtbarkeit
- Sichtbarkeitsproblem der Git-Dialoge: im Zielbild eindeutig im Register Projektverzeichnis verankert
- Globale Infoboxen: registerübergreifend stabil sichtbar
- Legacy-Bestandteil: „Ansicht“-Box inklusive Alt-State als zu entfernen festgelegt
- Persistenz: keine Datenbankmigration erforderlich
- Qualitätsstatus: fachliche Endfreigabe erst nach Nachweis der Gates G1–G8

---

## 5. Umsetzungsschwerpunkte

- `AufgabeDetail.razor` / `AufgabeDetail.razor.cs`: Registerzustand, Renderpfade, Dialoghost
- Einheitliche Aktionsmatrix pro Register
- Deterministische Dialog-Lifecycle-Regeln bei Registerwechsel
- Pflichttests für Exklusivität, Dialogsichtbarkeit, globale Infoboxen, Legacy-Entfernung, Empty-States

---

## 6. Offene Risiken und Annahmen

### Risiken
- Unvollständiger Dialog-Lifecycle kann zu versteckten Dialogzuständen führen
- Unpräzise Definition zusätzlicher Kennzahlen aus FR-2 kann die Abnahme erschweren
- Fehlende Operationalisierung von NFR-2 (Latenz) verhindert objektive Qualitätsmessung

### Annahmen
- Bestehende Task-/KI-/Git-Services liefern weiterhin kompatible Zustände
- Die notwendigen Daten für zusätzliche Kennzahlen sind verfügbar oder ableitbar
- Bestehende Tests werden auf das neue Registermodell angepasst

---

## 7. Freigabestatus

**Freigabe mit Auflagen** gemäß Architektur-Review v1.2.0.  
Die Implementierung darf fortgeführt werden; produktive Endfreigabe erst nach Schließen der Review-Blocker und grünem Nachweis der Gates G1–G8.

---

## 8. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-24 | planning-orchestrator | Erstfassung |
| 1.1.0 | 2026-05-25 | planning-orchestrator | Vollständige Konsolidierung von Requirements, Architektur, ERM und Review inkl. Risiko-/Freigabestatus |
| 1.2.0 | 2026-05-25 | planning-orchestrator | Re-Orchestrierung auf Basis der Aufgabenquelle „Implementierung fortsetzen“, Artefakte auf v1.2.0 konsolidiert und Freigabestatus präzisiert |
