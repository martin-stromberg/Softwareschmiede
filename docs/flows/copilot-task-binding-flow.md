# Ablauf: GUID-präfixierte `.copilot-task`-Datei und `.gitignore`-Konsolidierung

**Modul:** `GitHubCopilotPlugin`  
**Methode:** `StartDevelopmentAsync`  
**Letzte Aktualisierung:** 2026-05-10

## Sequenzdiagramm
```mermaid
sequenceDiagram
    participant Caller
    participant Plugin as GitHubCopilotPlugin
    participant FS as Dateisystem
    participant CLI as copilot CLI

    Caller->>Plugin: StartDevelopmentAsync(prompt, agent, repoPath, model, executionId)
    Plugin->>Plugin: NormalizeAndValidateExecutionId(executionId)
    alt executionId ungültig
        Plugin-->>Caller: ArgumentException
    else executionId gültig/erzeugt
        Plugin->>FS: Schreibe {executionId}.copilot-task.md
        Plugin->>FS: EnsureGitIgnoreRuleAsync(.gitignore, "*.copilot-task.md")
        FS-->>Plugin: updated | already-synced
        Plugin->>Plugin: BuildCopilotArgs(@taskFile, agent, model|auto)
        Plugin->>CLI: StreamAsync("copilot", args, repoPath, env)
        CLI-->>Plugin: Stream-Chunks
        Plugin-->>Caller: yield return Chunks
    end
    Plugin->>FS: CleanupTaskFileAsync(taskFile) (finally)
```

## Entscheidungslogik `.gitignore`
```mermaid
flowchart TD
    A[.gitignore lesen/erstellen] --> B[Legacy-Regeln filtern]
    B --> C{`*.copilot-task.md` vorhanden?}
    C -- Ja --> D[Keine inhaltliche Änderung]
    C -- Nein --> E[Regel `*.copilot-task.md` anhängen]
    E --> F[Datei speichern]
    D --> G[Weiter mit CLI-Start]
    F --> G
```

## Wichtige Punkte
- Prompt-Dateiname ist je Lauf eindeutig: `{executionId}.copilot-task.md`.
- Bei fehlender `executionId` wird eine GUID erzeugt.
- `.gitignore` wird robust und idempotent konsolidiert.
- Es gibt keinen test-spezifischen Kurz-Overload mehr; alle Caller nutzen die kanonische Signatur mit `executionId`-Parameter.
- Cleanup der Task-Datei wird immer ausgeführt; Fehler sind non-blocking.
