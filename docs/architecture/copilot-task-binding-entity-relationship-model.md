# Daten- und Zustandsmodell: `.copilot-task.md` / `.gitignore`-Synchronisation

## 1. Modellfokus
Für dieses Feature werden keine neuen persistenten DB-Entitäten eingeführt.  
Das Modell beschreibt daher die **Dateiobjekte und Laufzeitzustände** der Implementierung.

## 2. Laufzeitmodell (Mermaid)
```mermaid
classDiagram
    class StartDevelopmentContext {
        +string LocalRepoPath
        +string Prompt
        +AgentInfo Agent
        +string? Model
    }

    class TaskFile {
        +string Path = ".copilot-task.md"
        +string Content
    }

    class GitIgnoreFile {
        +string Path = ".gitignore"
        +string[] Lines
    }

    class GitIgnoreRule {
        +string RawValue = "/.copilot-task.md"
        +string NormalizedValue = "copilot-task.md"
    }

    class CopilotCommand {
        +string Executable = "copilot"
        +string[] Args
    }

    StartDevelopmentContext --> TaskFile : writes
    StartDevelopmentContext --> GitIgnoreFile : reads/writes
    GitIgnoreFile --> GitIgnoreRule : contains(0..n)
    StartDevelopmentContext --> CopilotCommand : builds and executes
```

## 3. Zustandsübergänge
```mermaid
stateDiagram-v2
    [*] --> RepoValidated
    RepoValidated --> TaskFileWritten
    TaskFileWritten --> GitIgnoreSynced
    GitIgnoreSynced --> CopilotStarted
    CopilotStarted --> Streaming
    Streaming --> Completed

    RepoValidated --> Failed : Directory missing
    TaskFileWritten --> Failed : IO error
    GitIgnoreSynced --> Failed : IO error after retries
```

## 4. Normalisierungsmodell für Regeln
Die Regeläquivalenz basiert auf:
1. `Trim()`
2. `\` → `/`
3. führende `/` entfernen
4. führende `.` entfernen
5. erneute Entfernung führender `/`

Damit werden `/.copilot-task.md` und `.copilot-task.md` als gleichwertig behandelt.

## 5. Mapping zur Implementierung
| Modellbaustein | Code |
|---------------|------|
| `TaskFile` | `StartDevelopmentAsync` (`File.WriteAllTextAsync`) |
| `GitIgnoreFile` + `GitIgnoreRule` | `EnsureGitIgnoreRuleAsync`, `IsEquivalentGitIgnoreRule`, `NormalizeGitIgnoreRule` |
| `CopilotCommand` | `BuildCopilotArgs` + `_cliRunner.StreamAsync("copilot", ...)` |

## 6. Traceability
- Anforderungen: `../requirements/copilot-task-binding-requirements-analysis.md`
- Architektur: `./copilot-task-binding-architecture-blueprint.md`
- Review: `../improvements/copilot-task-binding-architecture-review.md`


