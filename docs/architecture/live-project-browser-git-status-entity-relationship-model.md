# ERM – Live Project Browser mit Git-Status

> **Dokument-Typ:** Entity-Relationship-Model  
> **Status:** ✅ Validiert  
> **Version:** 1.1.0  
> **Datum:** 2026-05-18

---

## 1. Referenzen

- [Requirements Analysis](../requirements/live-project-browser-git-status-requirements-analysis.md)
- [Architecture Blueprint](./live-project-browser-git-status-architecture-blueprint.md)
- [Architecture Review](../improvements/live-project-browser-git-status-architecture-review.md)

---

## 2. Persistenzrelevanz

**Ergebnis:** Für den Live Project Browser ist **keine Erweiterung des persistenten Datenmodells erforderlich**.

Begründung:
1. Alle angezeigten Informationen werden aus dem lokalen Repositoryzustand zur Laufzeit abgeleitet.
2. Baum, Status und Diff sind temporäre UI- bzw. Service-Sichten.
3. Commit-Anzahl und Änderungsanzahl sind keine dauerhaften Geschäftsobjekte.
4. Eine zusätzliche Historisierung würde den Scope unnötig erweitern.

---

## 3. Logisches Laufzeitmodell

```mermaid
erDiagram
    AUFGABE ||--|| REPOSITORY_CONTEXT : provides
    REPOSITORY_CONTEXT ||--|| WORKSPACE_SNAPSHOT : derives
    WORKSPACE_SNAPSHOT ||--o{ FILE_STATUS_ENTRY : contains
    FILE_STATUS_ENTRY ||--|| FILE_PREVIEW : may_create
    FILE_STATUS_ENTRY ||--|| DIFF_VIEW_STATE : may_create
    WORKSPACE_SNAPSHOT ||--|| TASK_METRICS : produces

    AUFGABE {
        string AufgabeId PK
        string BranchName
        string LokalerKlonPfad
        string Status
    }

    REPOSITORY_CONTEXT {
        string ContextId PK
        string ProjektId
        string RepositoryKind
        string RepositoryPath
    }

    WORKSPACE_SNAPSHOT {
        string SnapshotId PK
        datetime CapturedAt
        int CommitCount
        int DirtyFileCount
    }

    FILE_STATUS_ENTRY {
        string EntryId PK
        string SnapshotId FK
        string RelativePath
        string StatusCode
        string DisplayGroup
        bool IsDirectory
    }

    FILE_PREVIEW {
        string EntryId PK_FK
        bool IsText
        bool IsBinary
        int FileSizeBytes
        string PreviewMode
    }

    DIFF_VIEW_STATE {
        string EntryId PK_FK
        string OriginalRevision
        string ModifiedRevision
        bool ShowWhitespace
    }

    TASK_METRICS {
        string SnapshotId PK_FK
        int CommitCount
        int DirtyFileCount
        string RefreshSource
    }
```

---

## 4. Tabellarische Übersicht

| Entität | Typ | Wichtige Attribute | Beziehung |
|---|---|---|---|
| `AUFGABE` | persistent | `BranchName`, `LokalerKlonPfad`, `Status` | bestehende Domäne |
| `REPOSITORY_CONTEXT` | logisch | `RepositoryPath`, `RepositoryKind` | 1:1 aus `AUFGABE` abgeleitet |
| `WORKSPACE_SNAPSHOT` | logisch | `CommitCount`, `DirtyFileCount`, `CapturedAt` | 1:1 mit Kontext |
| `FILE_STATUS_ENTRY` | logisch | `RelativePath`, `StatusCode`, `DisplayGroup` | 1:n pro Snapshot |
| `FILE_PREVIEW` | logisch | `IsText`, `IsBinary`, `PreviewMode` | optional pro Dateieintrag |
| `DIFF_VIEW_STATE` | logisch | `OriginalRevision`, `ModifiedRevision`, `ShowWhitespace` | optional pro Dateieintrag |
| `TASK_METRICS` | logisch | `CommitCount`, `DirtyFileCount`, `RefreshSource` | 1:1 pro Snapshot |

---

## 5. Konsequenz für Persistenz und Migration

- **Neue Tabellen:** keine
- **Neue Spalten:** keine
- **Migration notwendig:** nein
- **Rollback auf DB-Ebene:** nicht erforderlich

Die einzige relevante Zustandsänderung liegt im lokalen Repository und in der UI.

---

## 6. Risiken und Gegenmaßnahmen

| Risiko | Wirkung | Gegenmaßnahme |
|---|---|---|
| Repositoryzustand ändert sich zwischen Laden und Klick | Preview/Diff kann kurz veralten | Manuelles Refresh und erneute Snapshot-Ermittlung |
| Große Repositories | Verzögerte Baum-/Statusanzeige | Begrenzte Inline-Vorschau, lazy Laden von Dateiinhalten |
| Binärdateien | Nicht lesbare Vorschau | Binary-Hinweis und Download statt Inline-Rendering |
| Staged/unstaged Mischfälle | Falsche Statuszuordnung | Statuscodes direkt aus `git status --porcelain` ableiten |

---

## 7. Modellierungsentscheidungen

1. Keine neue persistente Domäne für Git-Status.
2. Ein Snapshot-Modell für Ladezeitpunkt und Refresh-Kohärenz.
3. Status und Vorschau klar trennen.
4. Diff-State nur als UI-konkrete Laufzeitinformation behandeln.

---

## 8. Entscheidung

Für den Live Project Browser wird das **persistente ERM nicht erweitert**.  
Verbindlich ist ein **nicht-persistentes Laufzeitmodell** zur Implementierung und Testabsicherung.

---

## 9. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-18 | planning-entity-relationship-modeler | ERM als nicht-persistentes Laufzeitmodell für Live Project Browser definiert |
| 1.1.0 | 2026-05-18 | documentation-orchestrator | Laufzeitmodell mit Snapshot, Dateivorschau und Diff-/Preview-Zustand final abgeglichen |
