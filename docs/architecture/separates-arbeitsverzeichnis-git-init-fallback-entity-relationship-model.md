# Entity-Relationship-Modell – Separates Arbeitsverzeichnis mit git-init-/Copy-Fallback

> **Dokument-Typ:** Entity Relationship Model  
> **Status:** ✅ Umgesetzt  
> **Version:** 1.0.0  
> **Datum:** 2026-05-13

---

## 1. Ziel und Scope

Dieses ERM beschreibt die fachlichen Entitäten und Beziehungen für die Workspace-Vorbereitung bei lokalen Quellen mit Strategien `clone`, `init+clone` und `copy`.

## 2. Entitäten (konzeptionell)

```mermaid
erDiagram
    SOURCE_DIRECTORY {
        string source_id PK
        string path
        bool is_git_repository
        datetime checked_at_utc
    }

    WORKSPACE_TARGET {
        string workspace_id PK
        string requested_path
        string resolved_path
        bool used_workdir_fallback
        string reason_code
    }

    PLUGIN_SETTINGS_SNAPSHOT {
        string settings_id PK
        string workspace_mode
        bool confirm_git_init_in_source
        int copy_timeout_seconds
        int copy_max_files
        int copy_max_megabytes
    }

    PREPARATION_STRATEGY_RUN {
        string strategy_run_id PK
        string strategy_type
        bool used_git_init
        bool used_clone
        bool used_copy_fallback
        string decision_reason
        string status
    }

    TASK_RUN {
        string task_run_id PK
        string task_id
        string status
        datetime started_at_utc
        datetime finished_at_utc
    }

    ERROR_EVENT {
        string error_id PK
        string category
        string reason_code
        string sanitized_message
        datetime occurred_at_utc
    }

    TASK_RUN ||--|| SOURCE_DIRECTORY : uses
    TASK_RUN ||--|| WORKSPACE_TARGET : prepares
    TASK_RUN ||--|| PLUGIN_SETTINGS_SNAPSHOT : applies
    TASK_RUN ||--|| PREPARATION_STRATEGY_RUN : executes
    TASK_RUN ||--o{ ERROR_EVENT : emits
    PREPARATION_STRATEGY_RUN ||--o{ ERROR_EVENT : causes
```

## 3. Beziehungen und Kardinalitäten

- `TASK_RUN` zu `SOURCE_DIRECTORY`: 1:1 (pro Lauf genau eine Quelle)
- `TASK_RUN` zu `WORKSPACE_TARGET`: 1:1 (pro Lauf genau ein Ziel)
- `TASK_RUN` zu `PLUGIN_SETTINGS_SNAPSHOT`: 1:1 (Settings-Snapshot pro Lauf)
- `TASK_RUN` zu `PREPARATION_STRATEGY_RUN`: 1:1 (eine entschiedene Strategie)
- `TASK_RUN` zu `ERROR_EVENT`: 1:n (optional mehrere Fehlerereignisse)

## 4. Zustände / Transitionen der Strategie

```mermaid
stateDiagram-v2
    [*] --> GitCheck
    GitCheck --> Clone: sourceIsGit
    GitCheck --> InitThenClone: !sourceIsGit && gitInitEnabled
    GitCheck --> CopyFallback: !sourceIsGit && !gitInitEnabled
    InitThenClone --> Clone: initSuccess
    InitThenClone --> Failed: initFailed
    Clone --> Prepared: cloneSuccess
    Clone --> Failed: cloneFailed
    CopyFallback --> Prepared: copySuccess
    CopyFallback --> Failed: copyFailed
    Prepared --> [*]
    Failed --> [*]
```

## 5. Konsistenzregeln / Invarianten

1. Bei `workspace_mode != SeparateWorkingDirectory` ist dieses Modell nicht anwendbar.
2. Vor jeder Strategieentscheidung muss `is_git_repository` bestimmt sein.
3. `used_git_init = true` impliziert `confirm_git_init_in_source = true`.
4. `used_copy_fallback = true` impliziert `used_clone = false`.
5. Zielpfad darf nicht identisch mit Quellpfad sein.
6. Bei Fehlerstatus muss mindestens ein `ERROR_EVENT` vorhanden sein.

## 6. Mapping auf bestehende Komponenten

- `SOURCE_DIRECTORY` → LocalDirectoryPlugin (Input `SourceDirectory`)
- `WORKSPACE_TARGET` → ArbeitsverzeichnisResolver + LocalDirectoryPlugin
- `PLUGIN_SETTINGS_SNAPSHOT` → Plugin-/Arbeitsverzeichnis-Settings
- `PREPARATION_STRATEGY_RUN` → Entscheidungslogik in LocalDirectoryPlugin
- `TASK_RUN` → EntwicklungsprozessService
- `ERROR_EVENT` → Logging/Fehlerklassifikation

## 7. Verlinkung

- Anforderungen: [../requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md](../requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md)
- Architektur: [separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md)
- Review: [../improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md](../improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md)
