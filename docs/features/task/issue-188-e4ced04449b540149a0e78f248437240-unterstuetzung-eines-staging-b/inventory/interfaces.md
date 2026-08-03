# Interfaces

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

### Branch-Operationen

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateBranchAsync()` | `localPath: string`, `branchName: string`, `ct: CancellationToken` | `Task` | Legt neuen Branch im lokalen Klon an. Basis-Branch wird **nicht** spezifiziert. |
| `CheckoutRemoteBranchAsync()` | `localPath: string`, `branchName: string`, `ct: CancellationToken` | `Task` | Wechselt zu vorhandenem Remote-Branch (erstellt lokalen Tracking-Branch). |
| `GetRemoteBranchesAsync()` | `repositoryUrl: string`, `ct: CancellationToken` | `Task<IEnumerable<string>>` | Listet alle Remote-Branches auf (ohne "origin/"-Präfix). |
| `GetDefaultBranchAsync()` | `repositoryUrl: string`, `ct: CancellationToken` | `Task<string>` | Ermittelt Standard-Branch des Repositories (z.B. "main" oder "master"). |

### Pull-Request-Operationen

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreatePullRequestAsync()` | `repositoryId: string`, `branchName: string`, `title: string`, `body: string`, `ct: CancellationToken` | `Task<PullRequest>` | Erstellt PR. Ziel-Branch wird **nicht** spezifiziert (verwendet Plugin-Standard). |
| `GetPullRequestStatusAsync()` | `repositoryId: string`, `pullRequestNumber: int`, `ct: CancellationToken` | `Task<PullRequestStatusInfo>` | Ruft aktuellen Status eines PRs ab. |
| `GetPullRequestWorkflowRunsAsync()` | `repositoryId: string`, `pullRequestNumber: int`, `headSha?: string`, `mergeCommitSha?: string`, `ct: CancellationToken` | `Task<IReadOnlyList<PullRequestWorkflowRunInfo>>` | Ruft zugeordnete Workflow-Runs ab. |
| `CompletePullRequestAsync()` | `repositoryId: string`, `pullRequestNumber: int`, `options: PullRequestCompletionOptions`, `ct: CancellationToken` | `Task<PullRequestCompletionResult>` | Schließt PR mit Merge/Squash/Rebase ab. |

### Repository & Allgemein

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CloneRepositoryAsync()` | `repositoryUrl: string`, `targetPath: string`, `ct: CancellationToken` | `Task` | Klont Repository in Zielverzeichnis. |
| `CommitAsync()` | `localPath: string`, `message: string`, `ct: CancellationToken` | `Task` | Führt Commit durch. |
| `PushBranchAsync()` | `localPath: string`, `branchName: string`, `ct: CancellationToken` | `Task` | Pusht Branch auf Remote. |
| `PullAsync()` | `localPath: string`, `ct: CancellationToken` | `Task` | Holt Änderungen vom Remote. |
| `ResetAsync()` | `localPath: string`, `resetType: string`, `targetRef?: string`, `ct: CancellationToken` | `Task` | Setzt Commits zurück (hard/soft/mixed). |
| `CheckHealthAsync()` | `ct: CancellationToken` | `Task<bool>` | Prüft Verfügbarkeit (CLI installiert, Token gültig). |
| `GetRepositoryStructureAsync()` | `repositoryUrl: string`, `maxDepth: int = 2`, `ct: CancellationToken` | `Task<IEnumerable<RepositoryDirectoryEntry>>` | Ruft Verzeichnisstruktur ab. |
| `MergeToSourceAsync()` | `localPath: string`, `ct: CancellationToken` | `Task` | Übernimmt Änderungen vom Arbeitsverzeichnis ins Quellverzeichnis. |
| `GetAvailableRepositoriesAsync()` | `ct: CancellationToken` | `Task<IEnumerable<AvailableRepository>>` | Liefert verfügbare Repositories aus externer Quelle. |
| `ResolveEffectiveRepositoryPathAsync()` | `localPath: string`, `ct: CancellationToken` | `Task<string>` | Löst tatsächlichen Repository-Pfad auf. |
| `GetGitActionCapabilitiesAsync()` | `localPath?: string`, `ct: CancellationToken` | `Task<GitActionCapabilities>` | Liefert verfügbare Git-Aktions-Capabilities. |

**Kritisch für Anforderung:** 
- `CreateBranchAsync()` spezifiziert keinen Basis-Branch-Parameter.
- `CreatePullRequestAsync()` spezifiziert keinen `baseBranch`-Parameter.

Diese beiden Methoden müssen erweitert werden, um den konfigurierten Basis-Branch zu unterstützen.
