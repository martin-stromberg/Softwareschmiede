# ERM – `start.ps1` (parameterloser Mehrprojekt-Ansatz, freier HTTP-Port)

> **Dokument-Typ:** Entity-Relationship-Model  
> **Status:** Aktualisiert  
> **Version:** 1.1.0  
> **Datum:** 2026-05-14

---

## 1. Referenzen

- [Anforderungsanalyse](../requirements/start-ps1-visual-studio-freier-http-port-requirements-analysis.md)
- [Architektur-Blueprint](./start-ps1-visual-studio-freier-http-port-architecture-blueprint.md)
- [Architecture-Review](../improvements/start-ps1-visual-studio-freier-http-port-architecture-review.md)

---

## 2. Persistenzrelevanz – Bewertung für den neuen Ansatz

**Ergebnis:** Für den parameterlosen, autonomen Mehrprojekt-Ansatz ist **keine Erweiterung des persistenten Datenmodells** erforderlich.

Begründung:
1. Autonome Projekterkennung basiert auf Repository-Scan zur Laufzeit und benötigt keine dauerhafte Entität.
2. Mehrprojektverarbeitung erzeugt temporäre Verarbeitungseinheiten, aber keinen fachlichen Langzeitzustand.
3. Portzuweisung ist bewusst ephemer (TOCTOU-Risiko) und wird nicht als DB-Wahrheit persistiert.
4. Persistente Historisierung von Portvergaben ist out of scope.

---

## 3. ERM-Diagramm (logisches Laufzeitmodell, nicht-persistent)

```mermaid
erDiagram
    START_SCRIPT_RUN ||--o{ WEB_PROJECT_TARGET : processes
    WEB_PROJECT_TARGET ||--|| HTTP_PROFILE_SNAPSHOT : reads
    WEB_PROJECT_TARGET ||--|| PORT_ASSIGNMENT : receives
    WEB_PROJECT_TARGET ||--|| PROJECT_UPDATE_RESULT : produces

    START_SCRIPT_RUN {
        string RunId PK
        datetime StartedAt
        datetime FinishedAt
        int ExitCode
        string TriggerSource
    }

    WEB_PROJECT_TARGET {
        string TargetId PK
        string RunId FK
        string ProjectPath
        string LaunchSettingsPath
        bool HttpProfileExists
        string ProcessingState
    }

    HTTP_PROFILE_SNAPSHOT {
        string TargetId PK_FK
        string PreviousApplicationUrl
        string ResolvedHost
    }

    PORT_ASSIGNMENT {
        string TargetId PK_FK
        int AssignedPort
        bool PortWasFreeAtAssignment
        string AssignmentSource
    }

    PROJECT_UPDATE_RESULT {
        string TargetId PK_FK
        bool JsonWriteSucceeded
        int ProjectExitCode
        string DiagnosticCode
    }
```

---

## 4. Tabellarische Übersicht (nicht-persistent, logisch)

| Entität | Schlüssel | Wichtige Attribute | Beziehungen | Kardinalität |
|---|---|---|---|---|
| `START_SCRIPT_RUN` | `RunId` | `StartedAt`, `FinishedAt`, `ExitCode`, `TriggerSource` | zu `WEB_PROJECT_TARGET` | 1 : 0..n |
| `WEB_PROJECT_TARGET` | `TargetId`, `RunId` | `ProjectPath`, `LaunchSettingsPath`, `HttpProfileExists`, `ProcessingState` | zu `HTTP_PROFILE_SNAPSHOT`, `PORT_ASSIGNMENT`, `PROJECT_UPDATE_RESULT` | jeweils 1 : 1 |
| `HTTP_PROFILE_SNAPSHOT` | `TargetId` | `PreviousApplicationUrl`, `ResolvedHost` | gehört zu `WEB_PROJECT_TARGET` | 1 : 1 |
| `PORT_ASSIGNMENT` | `TargetId` | `AssignedPort`, `PortWasFreeAtAssignment`, `AssignmentSource` | gehört zu `WEB_PROJECT_TARGET` | 1 : 1 |
| `PROJECT_UPDATE_RESULT` | `TargetId` | `JsonWriteSucceeded`, `ProjectExitCode`, `DiagnosticCode` | gehört zu `WEB_PROJECT_TARGET` | 1 : 1 |

Hinweis: Die Schlüssel sind Laufzeit-Korrelationen, keine persistierten DB-Primärschlüssel.

---

## 5. Konsequenz für Persistenz, Schema und Migration

- **Neue Tabellen:** keine  
- **Schemaänderungen:** keine  
- **Migration notwendig:** nein  
- **Rollback auf DB-Ebene:** nicht erforderlich

Datenwirksam ist ausschließlich die kontrollierte Dateiänderung in `launchSettings.json` pro Zielprojekt.

---

## 6. Risiken/Nebenwirkungen auf Datenhaltung

| Risiko | Datenhaltungswirkung | Einordnung / Gegenmaßnahme |
|---|---|---|
| Teilfehlschlag in Mehrprojektlauf | Uneinheitlicher Zustand zwischen Projekten | Pro Projekt atomar schreiben, Diagnosecode je Projekt, aggregierter Gesamtexit |
| Gleichzeitige Skriptläufe | Konkurrenz auf dieselbe `launchSettings.json` | Lock/Retry-Strategie und klarer Schreibkonflikt-Fehler |
| TOCTOU nach Portzuweisung | Persistierte URL zeigt später belegten Port | Akzeptiertes Laufzeitrisiko, erneute Ausführung + Diagnosehinweis |
| Merge-/Lokalkonflikte | Lokale Dateiänderungen in mehreren Projekten | Nur `profiles.http.applicationUrl` ändern |
| Keine Persistenzhistorie | Keine DB-Auditspur | Anforderungen-konforme Diagnostik pro Lauf |

---

## 7. Modellierungsentscheidungen

1. Kein persistentes Port-Audit.
2. Run-zentriertes Laufzeitmodell zur Trennung von Orchestrierung und Zielprojektverarbeitung.
3. 1:1-Nebenobjekte pro Zielprojekt (`Snapshot`, `Assignment`, `Result`) für klare Nachvollziehbarkeit.
4. Dateibasierte Zustandsänderung statt DB-Zustand bleibt architekturkonform.

---

## 8. Entscheidung

Für den parameterlosen Mehrprojekt-Ansatz wird das **persistente ERM nicht erweitert**.  
Verbindlich ist ein **nicht-persistentes Laufzeitmodell** als Grundlage für Implementierung, Tests und Betrieb.

---

## 9. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.1.0 | 2026-05-14 | planning-entity-relationship-modeler | ERM auf parameterlosen Mehrprojekt-Ansatz aktualisiert; Laufzeitmodell und Risiken konkretisiert |
| 1.0.0 | 2026-05-14 | planning-orchestrator | Initiale ERM-Relevanzprüfung |
