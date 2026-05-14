# Architektur-Review – Lokales Verzeichnis Plugin (Kopie-Aktionsmatrix)

> **Dokument-Typ:** Architektur-Review  
> **Status:** ⚠️ Freigabe mit Auflagen (vor Implementierung)  
> **Version:** 1.0.0  
> **Datum:** 2026-05-14

---

## Reviewte Unterlagen

- Requirements: [../requirements/lokales-verzeichnis-plugin-kopie-aktionsmatrix-requirements-analysis.md](../requirements/lokales-verzeichnis-plugin-kopie-aktionsmatrix-requirements-analysis.md)
- Architektur-Blueprint: [../architecture/lokales-verzeichnis-plugin-kopie-aktionsmatrix-architecture-blueprint.md](../architecture/lokales-verzeichnis-plugin-kopie-aktionsmatrix-architecture-blueprint.md)
- ERM: [../architecture/lokales-verzeichnis-plugin-kopie-aktionsmatrix-entity-relationship-model.md](../architecture/lokales-verzeichnis-plugin-kopie-aktionsmatrix-entity-relationship-model.md)
- Planning Overview: [../planning-overview-lokales-verzeichnis-plugin-kopie-aktionsmatrix.md](../planning-overview-lokales-verzeichnis-plugin-kopie-aktionsmatrix.md)

## 1) Executive Summary + Freigabestatus

Die Architektur ist in der Grundrichtung stimmig: zentrale Policy-Entscheidung, capability-basierte UI-Steuerung und klare Trennung von lokalem Kopie-Flow vs. Remote-Git-Flow sind sauber angelegt. Requirements, Blueprint und ERM sind weitgehend konsistent.

Vor Implementierungsstart bestehen jedoch kritische Präzisierungsbedarfe bei:
1. **Flag-Semantik und Vertragsinvarianten** (insb. `RepositoryKind`, `IsWorkingDirectoryCopy`, `CanMergeToSource`),
2. **Merge-Fehlerfällen / Konsistenzgarantien** (Teilzustände, Konflikte, Recovery),
3. **erzwingbarer UI-Konsistenz** (ein Decision Point, keine Nebenlogik).

**Gesamturteil:** ⚠️ **Freigabe mit Auflagen**  
(**1 Blocker**, **3 Major**, **1 Minor** vor Umsetzung einplanen)

## 2) Bewertete Fokusbereiche

| Bereich | Bewertung | Kurzbegründung |
|---|---|---|
| Systemarchitektur (Schichten/Module) | Gut | Verantwortlichkeiten sind klar getrennt; Decision Point ist benannt. |
| Technologieentscheidungen | Gut mit Risiken | Capability-Contract ist sinnvoll, aber Semantik noch nicht hart genug abgesichert. |
| UI/UX | Solide mit Risiko | Zielbild klar, aber Inkonsistenzrisiko ohne technische Durchsetzung/Tests. |
| Qualitätsziele | Teilweise belastbar | NFRs vorhanden, aber Merge-Konsistenz und Nachweisführung noch zu grob. |
| Artefakt-Traceability | Gut | Artefaktkette ist vollständig verlinkt. |

## 3) Priorisierte Findings (Blocker/Major/Minor)

| ID | Priorität | Finding | Risiko | Empfehlung |
|---|---|---|---|---|
| F-01 | **Blocker** | Merge-Fehlerfälle sind fachlich benannt, aber technisch nicht ausreichend operationalisiert (Atomicität, Rollback/Recovery, Teilzustände). | Inkonsistentes Quellverzeichnis, potenzieller Datenverlust. | Verbindliches Merge-Fehlermodell inkl. Zustandsautomat, idempotenter Retry-Regeln, klarer `status`-Semantik (`Succeeded/Failed/Conflicted`) und Recovery-Strategie definieren. |
| F-02 | **Major** | Flag-Semantik kann falsch interpretiert werden (insb. `IsWorkingDirectoryCopy`, `RepositoryKind=Unknown`, Pflichtsignale). | Falsche Aktionssichtbarkeit, fachlich inkorrekte Bedienoptionen. | Vertragsinvarianten + Contract-Tests festschreiben (kein implizites UI-Defaulting, Unknown nur explizit, harte Validierung). |
| F-03 | **Major** | UI-Konsistenz ist architektonisch gefordert, aber technisch noch nicht abgesichert (keine „single source of truth“-Erzwingung). | Divergierende Buttons zwischen Views/Komponenten. | Decision Point als einzige API für Sichtbarkeit kapseln; statische Regeln/Tests gegen direkte Sonderlogik in Views ergänzen. |
| F-04 | **Major** | Policy-first vs. Capability-Werte im Kopie-Modus ist beschrieben, aber Diagnose-/Reason-Codes nicht verbindlich im Blueprint verankert. | Support-/Debug-Aufwand, schwer nachvollziehbare Entscheidungen. | Standardisierte `reason_code`-Liste + strukturierte Logs ohne sensitive Pfade verpflichtend definieren. |
| F-05 | **Minor** | Performanceziel ist genannt, aber ohne konkreten Mess-/Nachweisplan. | Qualitätsziel schwer prüfbar. | Messpunkte, Testdatensätze und Akzeptanznachweis in Testplan verbindlich ergänzen. |

## 4) Risiken (Schwerpunkt)

- **R-1: Falsche Flag-Semantik**  
  Gegenmaßnahme: Contract-Tests + Invarianten + harte Validierung an Servicegrenze.
- **R-2: UI-Inkonsistenz**  
  Gegenmaßnahme: Zentraler Policy-Service als Pflichtpfad + UI-Integrationsmatrix für alle Views.
- **R-3: Merge-Fehlerfälle**  
  Gegenmaßnahme: Definierter Merge-Zustandsautomat, temporäre Staging-Strategie, Recovery- und Fehlerklassifikation.

## 5) Nachweise / Abgleich gegen Artefakte

| Artefakt | Beobachtung | Ergebnis |
|---|---|---|
| Requirements FR-1/FR-1.1/FR-1.2 | Kopie-Modus: Push/Pull/PR unsichtbar, Merge sichtbar | ✅ Im Blueprint/ERM konsistent modelliert |
| Requirements FR-2/FR-2.1 | Capability-Objekt als zentrale Quelle | ✅ Blueprint-Vertrag vorhanden |
| Requirements FR-3 + NFR-5 | Merge-Ergebnis + Fehlertoleranz gefordert | ⚠️ Ergebnisobjekt vorhanden, Fehler-/Konsistenzmodell noch zu unscharf |
| Requirements NFR-1 | Deterministische UI-Sichtbarkeit | ⚠️ Konzeptuell erfüllt, technische Erzwingung/Testtiefe nachschärfen |
| Blueprint §5/§6/§7 | Policy-first + zentraler Decision Point | ✅ Zielarchitektur stimmig |
| ERM Decision + reason_code | Nachvollziehbarkeit vorgesehen | ⚠️ Standardisierte Reason-Codes/Logging-Vertrag fehlt |

## 6) Konkrete Maßnahmen vor Implementierung

1. Blocker schließen: Merge-Fehlermodell finalisieren (Atomicität, Recovery, Konfliktpfad, Teilzustandsvermeidung).
2. Capability-Vertrag härten: Invarianten schriftlich fixieren + Contract-Tests (inkl. `Unknown`/Copy-Kombinationen).
3. Single Decision Point erzwingen: UI darf Sichtbarkeit nur über Policy-API beziehen.
4. Reason-Code-Katalog definieren (z. B. `LOCAL_COPY_POLICY`, `FLAG_BASED_REMOTE`, `MERGE_DISABLED_CAPABILITY`).
5. Testmatrix komplettieren: Unit + Integration + UI je Matrixzeile inkl. Negativ-/Edge-Cases.
6. Messbarkeit ergänzen: Performance-/Determinismus-Nachweise mit Schwellenwerten als DoD verankern.

## 7) Freigabeempfehlung

**Implementierung starten nach Schließen von F-01 (Blocker) und F-02/F-03 (Major).**  
F-04/F-05 sollten spätestens vor Merge in den Hauptbranch abgeschlossen sein.

## 8) Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-14 | GitHub Copilot Agent | Initiales strukturiertes Architektur-Review für Lokales Verzeichnis Plugin Kopie-Aktionsmatrix |
