# Entity-Relationship-Modell – AufgabeDetail UI Register-Navigation

> **Dokument-Typ:** Konzeptionelles ERM (Domäne + UI-ViewState)  
> **Status:** Aktualisiert auf Requirements/Architektur v1.2.0  
> **Persistenzleitlinie:** ✅ **Keine neue Persistenz und keine DB-Migration erforderlich** (NFR-6)

---

## 1. Referenzen

- Requirements: [`../requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md`](../requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md)
- Architektur-Blueprint: [`./aufgabe-detail-ui-register-navigation-architecture-blueprint.md`](./aufgabe-detail-ui-register-navigation-architecture-blueprint.md)
- Architektur-Review: [`../improvements/aufgabe-detail-ui-register-navigation-architecture-review.md`](../improvements/aufgabe-detail-ui-register-navigation-architecture-review.md)
- Planungsübersicht: [`../planning-overview-aufgabe-detail-ui-register-navigation.md`](../planning-overview-aufgabe-detail-ui-register-navigation.md)

---

## 2. ERM-Diagramm (fachlich relevante Domäne + UI-Zustand)

```mermaid
erDiagram
    Aufgabe ||--o{ Protokolleintrag : hat
    Aufgabe ||--o| WorkspaceSnapshot : hat_aktuellen
    Aufgabe ||--|| AufgabeDetailViewState : wird_angezeigt_in

    AufgabeDetailViewState ||--|| RegisterNavigationState : enthaelt
    AufgabeDetailViewState ||--|| RegisterAufgabeState : enthaelt
    AufgabeDetailViewState ||--|| RegisterAusfuehrungState : enthaelt
    AufgabeDetailViewState ||--|| RegisterProjektverzeichnisState : enthaelt
    AufgabeDetailViewState ||--|| GlobalInfoBoxState : enthaelt
    AufgabeDetailViewState ||--o| QueryRegisterState : initialisiert_aus
    AufgabeDetailViewState ||--o{ EmptyOrErrorState : nutzt

    RegisterAusfuehrungState ||--o{ Protokolleintrag : visualisiert
    RegisterAusfuehrungState ||--|| KiAnfrageDialogState : steuert
    RegisterProjektverzeichnisState ||--o| WorkspaceSnapshot : visualisiert
    RegisterProjektverzeichnisState ||--|| GitDialogState : steuert

    Aufgabe {
        Guid Id PK
        string Titel
        string Anforderungsbeschreibung
        DateTimeOffset Anlagezeitpunkt
        string Status
        DateTimeOffset LetzteAusfuehrung
        string LetzterAusfuehrungsstatus
    }

    Protokolleintrag {
        Guid Id PK
        Guid AufgabeId FK
        DateTimeOffset Zeitstempel
        string Typ
        string Nachricht
    }

    WorkspaceSnapshot {
        Guid AufgabeId PK_FK
        string RootPfad
        int CommitCount
        int ChangedFileCount
        DateTimeOffset ErfasstAm
    }

    AufgabeDetailViewState {
        Guid AufgabeId PK_FK
        string ActiveRegister
        bool ExactlyOneRegisterVisible
        bool IsLoading
    }

    QueryRegisterState {
        string RegisterQueryValue
        string ViewQueryValue
        bool IsValidRegisterValue
    }

    RegisterNavigationState {
        string ActiveRegister
        string AllowedRegisters
    }

    RegisterAufgabeState {
        bool ShowBeschreibung
        bool ShowKennzahlen
        bool AktionenEnabled
    }

    RegisterAusfuehrungState {
        bool ShowProtokoll
        bool ShowKiAnfrage
        bool AktionenEnabled
    }

    RegisterProjektverzeichnisState {
        bool ShowExplorer
        bool ShowGitAktionen
        bool AktionenEnabled
    }

    GlobalInfoBoxState {
        int CommitCount
        int ChangedFileCount
        bool VisibleAcrossAllRegisters
    }

    GitDialogState {
        bool ShowCommitDialog
        bool ShowPushDialog
        bool ShowPullDialog
        bool ShowPullRequestDialog
    }

    KiAnfrageDialogState {
        string PromptDraft
        bool IsSending
    }

    EmptyOrErrorState {
        string RegisterName
        string Cause
        string CtaLabel
    }
```

---

## 3. Tabellarische Übersicht (Entitäten, Schlüssel, Beziehungen, Kardinalitäten)

| Entität | Typ | Schlüssel | Wesentliche Attribute | Beziehungen / Kardinalitäten |
|---|---|---|---|---|
| `Aufgabe` | persistent (Domäne) | `Id` (PK) | Titel, Anforderungsbeschreibung, Anlagezeitpunkt, Status, LetzteAusführung, LetzterAusführungsstatus | 1:n zu `Protokolleintrag`, 1:0..1 zu `WorkspaceSnapshot`, 1:1 zu `AufgabeDetailViewState` |
| `Protokolleintrag` | persistent (Domäne) | `Id` (PK), `AufgabeId` (FK) | Zeitstempel, Typ, Nachricht | n:1 zu `Aufgabe`; wird im Register Ausführung dargestellt |
| `WorkspaceSnapshot` | persistent-nahes Read-Model | `AufgabeId` (PK/FK) | RootPfad, CommitCount, ChangedFileCount, ErfasstAm | 0..1:1 zu `Aufgabe`; Datenquelle für Projektverzeichnis + globale Infoboxen |
| `AufgabeDetailViewState` | laufzeit (UI-Aggregat) | `AufgabeId` (konzeptionell) | ActiveRegister, ExactlyOneRegisterVisible, IsLoading | 1:1 zu `Aufgabe`; enthält alle registerbezogenen Zustände |
| `QueryRegisterState` | laufzeit | – | RegisterQueryValue, ViewQueryValue, IsValidRegisterValue | 0..1:1 zu `AufgabeDetailViewState`; steuert Query-Init/Fallback |
| `RegisterNavigationState` | laufzeit | – | ActiveRegister, AllowedRegisters (`Aufgabe`,`Ausführung`,`Projektverzeichnis`) | 1:1 Teil von `AufgabeDetailViewState` |
| `RegisterAufgabeState` | laufzeit | – | ShowBeschreibung, ShowKennzahlen, AktionenEnabled | 1:1 Teil von `AufgabeDetailViewState`; deckt FR-2/FR-2.1 ab |
| `RegisterAusfuehrungState` | laufzeit | – | ShowProtokoll, ShowKiAnfrage, AktionenEnabled | 1:1 Teil von `AufgabeDetailViewState`; 1:n Sichtbezug auf `Protokolleintrag`; 1:1 zu `KiAnfrageDialogState` |
| `RegisterProjektverzeichnisState` | laufzeit | – | ShowExplorer, ShowGitAktionen, AktionenEnabled | 1:1 Teil von `AufgabeDetailViewState`; 1:0..1 Sichtbezug auf `WorkspaceSnapshot`; 1:1 zu `GitDialogState` |
| `GlobalInfoBoxState` | laufzeit | – | CommitCount, ChangedFileCount, VisibleAcrossAllRegisters | 1:1 Teil von `AufgabeDetailViewState`; in allen Registern sichtbar (FR-5) |
| `GitDialogState` | laufzeit | – | ShowCommitDialog, ShowPushDialog, ShowPullDialog, ShowPullRequestDialog | 1:1 zu `RegisterProjektverzeichnisState`; nur dort sichtbar/aktiv |
| `KiAnfrageDialogState` | laufzeit | – | PromptDraft, IsSending | 1:1 zu `RegisterAusfuehrungState` |
| `EmptyOrErrorState` | laufzeit | – | RegisterName, Cause, CtaLabel | 0..n Teil von `AufgabeDetailViewState`; für Empty-/Error-Zustände |

---

## 4. Integritätsregeln und Invarianten

1. **Register-Exklusivität:** `ActiveRegister` ist genau einer aus {`Aufgabe`,`Ausführung`,`Projektverzeichnis`}; gleichzeitig ist nur ein Registerinhalt sichtbar (FR-1.1, NFR-1).
2. **Query-Fallback:** Ungültige Werte aus `register`/`view` setzen deterministisch `ActiveRegister = Aufgabe` (AC-1.3).
3. **Dialog-Kontextbindung:** In `GitDialogState` darf nur im aktiven Register `Projektverzeichnis` ein Dialog geöffnet sein (FR-4.2).
4. **Dialog-Lifecycle:** Beim Wechsel weg von `Projektverzeichnis` werden alle Git-Dialog-Flags auf `false` gesetzt (AC-3.4, Review F-B01).
5. **Globale Sichtbarkeit:** `GlobalInfoBoxState.VisibleAcrossAllRegisters = true`; Werte sind registerunabhängig verfügbar (FR-5).
6. **Legacy-Verbot:** Es existiert keine Entität und kein Zustand für die frühere „Ansicht“-Box (FR-6).

---

## 5. Abgleich mit Requirements und Architektur

| Quelle | Erwartung | ERM-Abbildung | Status |
|---|---|---|---|
| Requirements FR-1..FR-4.2 | 3 Register, exklusive Anzeige, Git-Dialoge im richtigen Kontext | `RegisterNavigationState`, registerspezifische States, `GitDialogState` | ✅ konsistent |
| Requirements FR-5 | Globale Infoboxen immer sichtbar | `GlobalInfoBoxState` außerhalb Registerkontext | ✅ konsistent |
| Requirements FR-6 | Legacy-Ansicht entfernt | Kein Legacy-State modelliert | ✅ konsistent |
| Requirements NFR-6 | Keine Schemaänderung | Nur Laufzeitzustände ergänzt, Persistenz unverändert | ✅ konsistent |
| Architektur-Blueprint §4/§5 | zentraler Registerzustand + Dialog-Lifecycle | `AufgabeDetailViewState.ActiveRegister`, `GitDialogState` + Integritätsregeln | ✅ konsistent |
| Architektur-Review F-B01/F-B02 | deterministische Dialog- und Exklusivitätsregeln | explizite Invarianten in Abschnitt 4 | ✅ adressiert |

---

## 6. Persistenz- und Migrationsauswirkung

✅ **Keine neue Persistenz-/DB-Migration notwendig.**

- Persistente Entitäten (`Aufgabe`, `Protokolleintrag`, `WorkspaceSnapshot`) bleiben unverändert.
- Neue Modellbestandteile sind ausschließlich **UI-ViewState** zur Laufzeit.
- Keine neuen Tabellen, keine Schemaänderungen, keine DB-Migration.

---

## 7. Modellierungsbegründungen (kurz)

1. UI-Refactoring wird über `AufgabeDetailViewState` gekapselt, ohne Domänenmodell aufzubrechen.
2. Register-spezifische Teilzustände halten Aktions- und Sichtbarkeitslogik klar trennbar und testbar.
3. `QueryRegisterState` macht Initialisierung/Fallback aus URL nachvollziehbar.
4. Strikte Kopplung `RegisterProjektverzeichnisState` ↔ `GitDialogState` verhindert unsichtbare/inkonsistente Dialoge.

---

## 8. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-24 | planning-entity-relationship-modeler | Erstfassung |
| 1.1.0 | 2026-05-25 | Copilot | Modell auf Requirements/Architektur v1.1.0 erweitert |
| 1.2.0 | 2026-05-25 | Copilot | Aktualisierung auf Requirements/Architektur v1.2.0; UI-ViewState präzisiert, Integritätsregeln ergänzt, Konsistenzabgleich und Migrationsaussage geschärft |
