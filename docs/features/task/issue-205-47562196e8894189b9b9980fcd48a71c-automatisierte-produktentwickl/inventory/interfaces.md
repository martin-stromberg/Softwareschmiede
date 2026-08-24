# Interfaces

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| **`CloneRepositoryAsync`** | `repositoryUrl: string`, `targetPath: string`, `ct: CancellationToken` | `Task` | **Klont ein Repository in das Zielverzeichnis. Wird von `AutonomAufgabenInitialisierungsService.KloneHauptRepositoryAsync` aufgerufen (Zeile 166).** |
| **`CreateBranchAsync`** | `localPath: string`, `branchName: string`, `sourceBranchName: string?`, `ct: CancellationToken` | `Task` | **Erstellt einen neuen Branch im lokalen Klon (mit "git checkout -b"). Wird von `AutonomAufgabenInitialisierungsService.ErstelleProjektbranchAsync` aufgerufen (Zeile 214).** |
| `CheckoutRemoteBranchAsync` | `localPath: string`, `branchName: string`, `ct: CancellationToken` | `Task` | Wechselt zu einem vorhandenen Remote-Branch (erstellt lokalen Tracking-Branch). Wird von `ErstelleProjektbranchAsync` aufgerufen, falls Remote-Branch existiert (Zeile 197). |
| `GetRemoteBranchesAsync` | `repositoryUrl: string`, `ct: CancellationToken` | `Task<IEnumerable<string>>` | Listet Remote-Branches auf (ohne Klon). Wird von `LadeRemoteBranchesAsync` aufgerufen (Zeile 257). |
| `GetDefaultBranchAsync` | `repositoryUrl: string`, `ct: CancellationToken` | `Task<string>` | Ermittelt den Standard-Branch eines Repositories (z. B. "main"). |
| `ResolveEffectiveRepositoryPathAsync` | `localPath: string`, `ct: CancellationToken` | `Task<string>` | Löst den tatsächlichen Repository-Pfad auf (für Plugins mit indirektem Workspace-Mapping). Wird von `LokalerBranchExistiertBereitsAsync` aufgerufen (Zeile 238). |
| `PushBranchAsync` | `localPath: string`, `branchName: string`, `ct: CancellationToken` | `Task` | Pusht einen Branch auf den Remote. |
| `PullAsync` | `localPath: string`, `ct: CancellationToken` | `Task` | Holt Änderungen vom Remote. |
| `CommitAsync` | `localPath: string`, `message: string`, `ct: CancellationToken` | `Task` | Führt einen Commit durch. |
| `ResetAsync` | `localPath: string`, `resetType: string`, `targetRef: string?`, `ct: CancellationToken` | `Task` | Setzt Commits zurück. |
| `CreatePullRequestAsync` | `repositoryId: string`, `branchName: string`, `baseBranch: string?`, `title: string`, `body: string`, `ct: CancellationToken` | `Task<PullRequest>` | Erstellt einen Pull Request. |
| `GetIssuesAsync` | `repositoryId: string`, `ct: CancellationToken` | `Task<IEnumerable<Issue>>` | Ruft Issues aus dem Repository ab. |
| `CheckHealthAsync` | `ct: CancellationToken` | `Task<bool>` | Prüft ob das Plugin verfügbar ist (CLI installiert, Token gültig). |
| `GetAvailableRepositoriesAsync` | `ct: CancellationToken` | `Task<IEnumerable<AvailableRepository>>` | Liefert die verfügbaren Repositories aus der externen Quelle. |
| `GetGitActionCapabilitiesAsync` | `localPath: string?`, `ct: CancellationToken` | `Task<GitActionCapabilities>` | Liefert die verfügbaren Git-Aktionen für die UI (default: Remote-Git mit Push/Pull/PR-Fähigkeit). |

### Implementierung im `AutonomAufgabenInitialisierungsService`:
Das Plugin wird via `PluginSelectionService.ResolveSourceCodeManagementPluginAsync(aufgabe.GitRepository?.PluginTyp, ct)` aufgelöst (Zeile 45 der Service-Datei) und dann als Parameter an die Hilfsmethoden übergeben:
- `KloneHauptRepositoryAsync(gitPlugin, aufgabe, zielPfad, ct)` — nutzt `gitPlugin.CloneRepositoryAsync(...)`
- `ErstelleProjektbranchAsync(gitPlugin, aufgabe, repoMainPfad, projektBranchName, ct)` — nutzt `gitPlugin.CreateBranchAsync(...)`, `gitPlugin.CheckoutRemoteBranchAsync(...)`, `gitPlugin.GetRemoteBranchesAsync(...)`
- `LokalerBranchExistiertBereitsAsync(gitPlugin, repoPfad, branchName, ct)` — nutzt `gitPlugin.ResolveEffectiveRepositoryPathAsync(...)`

---

## `IPlugin` (Base-Interface)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `PluginPrefix` | `string` | Eindeutiges Präfix des Plugins (z. B. "github", "local-directory") |
| `PluginName` | `string` | Menschenlesbarer Name des Plugins |

Alle Git-Plugins implementieren `IGitPlugin : IPlugin`.

---

## `ICliRunner`
Datei: Wird von `AutonomAufgabenInitialisierungsService` zur Ausführung von Git-Befehlen verwendet.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync` | `command: string`, `args: IEnumerable<string>`, `workingDirectory: string?`, `environmentVariables: IDictionary<string, string>?`, `ct: CancellationToken` | `Task<CliResult>` | Führt CLI-Befehle aus. Wird in `LokalerBranchExistiertBereitsAsync` für "git branch --list" verwendet (Zeile 239). |

### Resultat-Struktur (`CliResult`):
- `ExitCode: int`
- `StdOut: string`
- `StdErr: string`
- `IsSuccess: bool` (ExitCode == 0)

