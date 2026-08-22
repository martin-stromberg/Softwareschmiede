# Bestandsaufnahme

## Relevante Komponenten

| Komponente | Pfad | Rolle |
|------------|------|-------|
| `GitWorkspaceBrowserService` | `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs` | Ermittelt Branch-Commits gegen eine Basis-Referenz |
| `IGitWorkspaceBrowserService` | `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs` | Schnittstelle |
| `GitOrchestrationService` | `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs` | Erstellt PR und baut den Body |
| `PullRequestBodyBuilder` | `src/Softwareschmiede/Application/Services/PullRequestBodyBuilder.cs` | Formatiert die Commit-Liste im PR-Body |
| `TaskDetailViewModel` | `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | Ribbon-Action-Handler |
| `GitHubPlugin` | `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` | PR-Erstellung via `gh` |

## Beobachtung

- `GitWorkspaceBrowserService.LoadSnapshotAsync` ermittelt die Basis-Referenz ausschließlich über `refs/remotes/origin/HEAD` bzw. Fallback auf `origin/main`/`origin/master`. Das ist der Hauptbranch, nicht der konfigurierte Zielbranch.
- `GitOrchestrationService.PullRequestErstellenAsync` baut den PR-Body **vor** der Auflösung des `baseBranch`. Der Body enthält daher Commits gegen `origin/HEAD`, auch wenn der PR selbst gegen `staging` erstellt wird.
- Der `baseBranch` (aus `GitRepository.DefaultSourceBranchName`) wird zwar an `gh pr create --base` übergeben, aber nicht an `LoadSnapshotAsync`.

## Root Cause

Die Commit-Liste im PR-Body (und ggf. die visuelle Commit-Darstellung) wird aus einem `WorkspaceSnapshot` berechnet, dessen Basis-Referenz unabhängig vom gewählten Zielbranch stets `origin/HEAD` verwendet. Beim Zielbranch `staging` fehlt die Konfiguration in der Snapshot-Berechnung.
