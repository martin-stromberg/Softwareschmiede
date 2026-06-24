# Interfaces und Abstraktion

## `IGitPlugin`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

Haupt-Interface, das `BitbucketPlugin` implementiert. Erbt von `IPlugin`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRepositoryLinkFields()` | — | `IReadOnlyList<PluginSettingField>` | Liefert Felder für Repository-Verknüpfung (UI) |
| `GetIssuesAsync(string repositoryId, CancellationToken ct)` | repositoryId, ct | `Task<IEnumerable<Issue>>` | Ruft Issues (Jira) ab |
| `CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct)` | url, path, ct | `Task` | Klont Repository |
| `CreateBranchAsync(string localPath, string branchName, CancellationToken ct)` | localPath, branchName, ct | `Task` | Erstellt neuen Branch |
| `PushBranchAsync(string localPath, string branchName, CancellationToken ct)` | localPath, branchName, ct | `Task` | Pusht Branch |
| `PullAsync(string localPath, CancellationToken ct)` | localPath, ct | `Task` | Pullt Änderungen |
| `CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct)` | repositoryId, branchName, title, body, ct | `Task<PullRequest>` | Erstellt PR |
| `CommitAsync(string localPath, string message, CancellationToken ct)` | localPath, message, ct | `Task` | Erstellt Commit |
| `ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct)` | localPath, resetType, targetRef, ct | `Task` | Reset-Operationen |
| `CheckHealthAsync(CancellationToken ct)` | ct | `Task<bool>` | Prüft Verbindung |
| `GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct)` | repositoryUrl, ct | `Task<IEnumerable<string>>` | Listet Remote-Branches |
| `GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct)` | repositoryUrl, ct | `Task<string>` | Ermittelt Standard-Branch |
| `CheckoutRemoteBranchAsync(string localPath, string branchName, CancellationToken ct)` | localPath, branchName, ct | `Task` | Wechselt zu Remote-Branch |
| `GetGitActionCapabilitiesAsync(string? localPath, CancellationToken ct)` | localPath, ct | `Task<GitActionCapabilities>` | Liefert Capabilities für UI |
| `MergeToSourceAsync(string localPath, CancellationToken ct)` | localPath, ct | `Task` | Merge (nicht unterstützt) |
| `GetAvailableRepositoriesAsync(CancellationToken ct)` | ct | `Task<IEnumerable<AvailableRepository>>` | Listet verfügbare Repos |

## `ICliRunner`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`

Interface für CLI-Prozess-Ausführung. Wird von `BitbucketPlugin` verwendet.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync(string command, IEnumerable<string> args, string? workingDirectory, IDictionary<string, string>? environmentVariables, CancellationToken ct)` | command, args, workDir, env, ct | `Task<CliResult>` | Führt CLI-Befehl synchron aus und sammelt Output |
| `StreamAsync(string command, IEnumerable<string> args, string? workingDirectory, IDictionary<string, string>? environmentVariables, CancellationToken ct)` | command, args, workDir, env, ct | `IAsyncEnumerable<string>` | Streamt stdout zeilenweise |

Verwendung in `BitbucketPlugin`:
- `git clone`, `git pull`, `git push`, `git ls-remote` über `RunAsync()`
- `curl` für Bitbucket/Jira API über `RunAsync()`

## `ICredentialStore`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs`

Interface für sichere Credential-Verwaltung. Wird von `BitbucketPlugin` verwendet.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetCredential(string target)` | target (Key) | `string?` | Ruft Credential-Wert ab oder null |
| `SetCredential(string target, string value)` | target (Key), value | — | Speichert Credential-Wert |
| `DeleteCredential(string target)` | target (Key) | — | Löscht Credential-Eintrag |

Verwendung in `BitbucketPlugin` (Abrufe):
- `BitbucketUserKey` ("Softwareschmiede.Bitbucket.Username")
- `BitbucketAppPasswordKey` ("Softwareschmiede.Bitbucket.AppPassword")
- `BitbucketWorkspaceKey` ("Softwareschmiede.Bitbucket.Workspace")
- `BitbucketHostingModeKey` ("Softwareschmiede.Bitbucket.HostingMode")
- `BitbucketSelfHostedUrlKey` ("Softwareschmiede.Bitbucket.SelfHostedUrl")
- "Softwareschmiede.Bitbucket.JiraUrl"
- "Softwareschmiede.Bitbucket.JiraProjectKey"
- "Softwareschmiede.Bitbucket.JiraEmail"
- "Softwareschmiede.Bitbucket.JiraApiToken"

## `GitPluginBase<TPlugin>`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`

Abstrakte Basisklasse, von der `BitbucketPlugin` erbt. Bietet Standard-Implementierungen für Git-Operationen.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSettingGroups()` | — | `IReadOnlyList<PluginSettingGroup>` | Abstract - muss implementiert sein |
| `GetRepositoryLinkFields()` | — | `IReadOnlyList<PluginSettingField>` | Virtual - Default ist leer |
| `GetIssuesAsync(string repositoryId, CancellationToken ct)` | repositoryId, ct | `Task<IEnumerable<Issue>>` | Abstract - muss implementiert sein |
| `CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct)` | url, path, ct | `Task` | Abstract - muss implementiert sein |
| `PushBranchAsync(string localPath, string branchName, CancellationToken ct)` | localPath, branchName, ct | `Task` | Abstract - muss implementiert sein |
| `PullAsync(string localPath, CancellationToken ct)` | localPath, ct | `Task` | Abstract - muss implementiert sein |
| `CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct)` | repositoryId, branchName, title, body, ct | `Task<PullRequest>` | Abstract - muss implementiert sein |
| `CheckHealthAsync(CancellationToken ct)` | ct | `Task<bool>` | Abstract - muss implementiert sein |
| `GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct)` | repositoryUrl, ct | `Task<IEnumerable<string>>` | Abstract - muss implementiert sein |
| `GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct)` | repositoryUrl, ct | `Task<string>` | Abstract - muss implementiert sein |
| `CreateBranchAsync(string localPath, string branchName, CancellationToken ct)` | localPath, branchName, ct | `Task` | Virtual - Default: `git checkout -b` |
| `CommitAsync(string localPath, string message, CancellationToken ct)` | localPath, message, ct | `Task` | Virtual - Default: `git add . && git commit -m` |
| `ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct)` | localPath, resetType, targetRef, ct | `Task` | Virtual - Default: `git reset --{resetType}` |
| `CheckoutRemoteBranchAsync(string localPath, string branchName, CancellationToken ct)` | localPath, branchName, ct | `Task` | Virtual - Default: `git checkout -b ... --track` |
| `AddAllAsync(string localPath, CancellationToken ct)` | localPath, ct | `Task` | Protected - Führt `git add .` aus |
| `EnsureGitRepositoryAsync(string localPath, CancellationToken ct)` | localPath, ct | `Task` | Protected - Validiert Git-Repo |
| `RunGitAsync(IEnumerable<string> args, string? workingDirectory, CancellationToken ct, IDictionary<string, string>? environmentVariables)` | args, workDir, ct, env | `Task<CliResult>` | Protected - Wrapper für `ICliRunner.RunAsync()` |
| `GetAvailableRepositoriesAsync(CancellationToken ct)` | ct | `Task<IEnumerable<AvailableRepository>>` | Virtual - Default: leere Liste |
| `GetGitActionCapabilitiesAsync(string? localPath, CancellationToken ct)` | localPath, ct | `Task<GitActionCapabilities>` | Virtual - Default: Standard-Capabilities |
| `MergeToSourceAsync(string localPath, CancellationToken ct)` | localPath, ct | `Task` | Virtual - Wirft NotSupportedException |

Verwendete Protected-Member in `BitbucketPlugin`:
- `CliRunner` Property: Zugriff auf `ICliRunner`
- `RunGitAsync()`: Vereinfachter Zugriff auf Git-Befehle

Überschriebene Methoden in `BitbucketPlugin`:
- Alle abstrakten Methoden (GetSettingGroups, GetRepositoryLinkFields, CloneRepositoryAsync, PullAsync, PushBranchAsync, etc.)
- Keine virtuellen Methoden überschrieben (nutzt Standard-Implementierungen)

## `IPlugin`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Haupt-Interface, von dem `IGitPlugin` erbt. Über Basisschnittstelle verbunden.

| Methode | Zweck |
|---------|-------|
| `PluginName` Property | Name des Plugins (readonly) |
| `PluginPrefix` Property | Prefix für Credential-Keys (readonly) |
| `PluginType` Property | Plugin-Typ (readonly) |
| `GetSettingGroups()` | Konfigurationsgruppen |

Implementiert durch `BitbucketPlugin`:
- `PluginName` = "Bitbucket"
- `PluginPrefix` = "Softwareschmiede.Bitbucket"
- `PluginType` = PluginType.SourceCodeManagement
