# Ablauf – LocalDirectoryPlugin (WorkspaceMode, Kopierpfad, Guardrails)

**Modul:** `LocalDirectoryPlugin`, `ArbeitsverzeichnisResolver`, `ArbeitsverzeichnisSettingsService`, `EntwicklungsprozessService`, `GitOrchestrationService`  
**Letzte Aktualisierung:** 2026-05-12

---

## Kontext

`LocalDirectoryPlugin` implementiert `IGitPlugin` für lokale Verzeichnisse ohne Remote-Provider.

- `WorkspaceMode.InSourceDirectory`: arbeitet direkt im Quellverzeichnis.
- `WorkspaceMode.SeparateWorkingDirectory`: erstellt eine separate Arbeitskopie.

Der Integrationspfad ist:
1. `EntwicklungsprozessService` erzeugt den angefragten `lokalerKlonPfad` aus Resolver-Ergebnis.
2. `IGitPlugin.CloneRepositoryAsync(...)` wird aufgerufen.
3. `LocalDirectoryPlugin` löst intern den effektiven Workspace auf.
4. Folgeaktionen (`CreateBranch`, `Commit`, `Reset`) nutzen Mapping/Pointer-Datei auf den effektiven Workspace.

---

## Ablauf 1: WorkspaceMode-Verhalten (InSourceDirectory vs SeparateWorkingDirectory)

```mermaid
sequenceDiagram
    actor Benutzer
    participant Einst as EinstellungenBase/PluginSettingsService
    participant Proc as EntwicklungsprozessService
    participant Plugin as LocalDirectoryPlugin
    participant Git as git CLI

    Benutzer->>Einst: Speichert LocalDirectoryPlugin.* Settings
    Benutzer->>Proc: ProzessStartenAsync(..., repositoryUrl)
    Proc->>Plugin: CloneRepositoryAsync(repositoryUrl, lokalerKlonPfad)
    Plugin->>Plugin: ResolveWorkspaceMode() (Default: SeparateWorkingDirectory)
    Plugin->>Plugin: ResolveSourcePath(repositoryUrl || SourceDirectory)

    alt WorkspaceMode == InSourceDirectory
        Plugin->>Git: git rev-parse --is-inside-work-tree (source)
        Plugin->>Git: git status --porcelain (source, wenn Repo)
        alt Kein Git-Repo
            Plugin->>Plugin: ConfirmGitInitInSourceDirectory == true?
            Plugin->>Git: git init (source)
        end
        Plugin->>Plugin: WriteWorkspacePointer(lokalerKlonPfad -> source) falls verschieden
        Plugin->>Plugin: TrackWorkspace(lokalerKlonPfad, source)
    else WorkspaceMode == SeparateWorkingDirectory
        Plugin->>Plugin: ResolveWorkingPath(WorkingDirectory || lokalerKlonPfad)
        Plugin->>Plugin: EnsureTargetIsSafeForCopy(target != source, target leer)
        Plugin->>Plugin: CopyDirectoryWithGuardrails(source -> target)
        Plugin->>Git: git rev-parse --is-inside-work-tree (target)
        Plugin->>Git: git init (target, wenn kein Repo)
        Plugin->>Git: git status --porcelain (target)
        Plugin->>Plugin: TrackWorkspace(lokalerKlonPfad, target)
    end

    Proc-->>Benutzer: Prozessstart erfolgreich / Exception
```

---

## Ablauf 2: Kopierpfad in SeparateWorkingDirectory inkl. Guardrails

```mermaid
flowchart TD
    A([CloneRepositoryAsync im SeparateWorkingDirectory]) --> B[Resolve source + destination]
    B --> C{destination == source?}
    C -- Ja --> C1[InvalidOperationException\n"anderes Zielverzeichnis erforderlich"]:::error
    C -- Nein --> D{destination existiert und nicht leer?}
    D -- Ja --> D1[InvalidOperationException\n"Zielverzeichnis ist nicht leer"]:::error
    D -- Nein --> E[Directory.CreateDirectory(destination)]
    E --> F[Traverse Verzeichnisbaum]
    F --> G{Reparse Point/Symlink?}
    G -- Ja --> G1[InvalidOperationException\n"Symlink/Reparse-Point ist nicht erlaubt"]:::error
    G -- Nein --> H[Datei zählen + Bytes summieren]
    H --> I{CopyMaxFiles überschritten?}
    I -- Ja --> I1[InvalidOperationException\nCopy-Guardrail Dateien]:::error
    I -- Nein --> J{CopyMaxMegabytes überschritten?}
    J -- Ja --> J1[InvalidOperationException\nCopy-Guardrail MB]:::error
    J -- Nein --> K[File copy (overwrite:false)]
    K --> F
    F --> L[git init falls nötig]
    L --> M[git status --porcelain leer?]
    M -- Nein --> M1[InvalidOperationException\nuncommitted changes]:::error
    M -- Ja --> N([Workspace bereit])

    C1 --> X[Bei Fehler nach Zielerstellung:\nDirectory.Delete(destination, recursive:true)]
    D1 --> X
    G1 --> X
    I1 --> X
    J1 --> X
    M1 --> X

    classDef error fill:#ffcccc,stroke:#cc0000,color:#333
```

---

## Fehler- und Guardrail-Matrix

| Fall | Quelle | Verhalten |
|---|---|---|
| `WorkspaceMode` ungültig im Credential Store | `ResolveWorkspaceMode` | Warn-Log, Fallback auf `SeparateWorkingDirectory` |
| Kein Quellverzeichnis übergeben/konfiguriert | `ResolveSourcePath` | `InvalidOperationException` |
| Quellverzeichnis existiert nicht | `EnsureDirectoryExists` | `DirectoryNotFoundException` |
| `InSourceDirectory` ohne `ConfirmGitInitInSourceDirectory=true` und ohne `.git` | `EnsureInitializedInSourceDirectoryAsync` | `InvalidOperationException` |
| Dirty Workspace (`git status --porcelain` nicht leer) | `ValidateWorkspaceIsCleanAsync` | `InvalidOperationException` |
| Copy-Guardrails verletzt (`CopyMaxFiles`, `CopyMaxMegabytes`, Timeout/Cancellation) | `CopyDirectoryWithGuardrailsAsync` | Abbruch + Aufräumen des Zielverzeichnisses |
| Remote-Operationen (`Push`, `Pull`, `CreatePullRequest`, `GetIssues`, …) | `BuildNotSupported` | `NotSupportedException` |

---

## Integrationspfad nach Clone (Folgeoperationen)

```mermaid
sequenceDiagram
    participant UI as AufgabeDetail
    participant GitSvc as GitOrchestrationService
    participant Plugin as LocalDirectoryPlugin
    participant FS as WorkspacePointerFile

    UI->>GitSvc: CommitAsync/ResetAsync(aufgabeId, ...)
    GitSvc->>Plugin: CommitAsync/ResetAsync(aufgabe.LokalerKlonPfad, ...)
    Plugin->>Plugin: ResolveWorkspacePath(lokalerKlonPfad)
    alt Mapping im Speicher vorhanden
        Plugin->>Plugin: nutze gemappten Pfad
    else Pointer-Datei vorhanden
        Plugin->>FS: lese .softwareschmiede-local-workspace
        Plugin->>Plugin: mappe auf effektiven Workspace
    else
        Plugin->>Plugin: nutze lokalen Pfad unverändert
    end
    Plugin->>Plugin: EnsureGitRepositoryAsync(...)
    Plugin-->>GitSvc: Operation ausgeführt / Exception
```

---

## Verwandte Dokumentation

- [workdir-resolution-flow.md](./workdir-resolution-flow.md)
- [development-process-flow.md](./development-process-flow.md)
- [plugin-settings-service-flow.md](./plugin-settings-service-flow.md)
