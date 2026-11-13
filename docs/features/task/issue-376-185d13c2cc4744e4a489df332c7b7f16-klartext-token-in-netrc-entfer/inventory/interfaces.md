# Interfaces — betroffene Contracts

## `ICredentialStore`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetCredential` | `string target` | `string?` | Gespeicherten Wert lesen (null wenn nicht vorhanden); im `GitHubPlugin` Quelle für `GitHubTokenCredentialKey`/`GitHubAgentTokenCredentialKey` |
| `SetCredential` | `string target`, `string value` | `void` | Credential speichern |
| `DeleteCredential` | `string target` | `void` | Credential löschen |

## `ICliRunner`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync` | `string command`, `IEnumerable<string> args`, `string? workingDirectory`, `IDictionary<string, string>? environmentVariables`, `CancellationToken ct = default` | `Task<CliResult>` | Führt einen CLI-Prozess aus; der Parameter `environmentVariables` ist der Transport, über den `GetGitEnvironment`/`GetGhEnvironment`/`GetAgentEnvironmentAsync` ihre Auth-Variablen an git/gh übergeben |
| `StreamAsync` | dieselben Parameter | `IAsyncEnumerable<string>` | stdout-Zeilenstreaming; im Credential-Kontext nicht genutzt |

## `IScmAgentAccess`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAgentEnvironmentAsync` | `ScmKontoModus konto`, `CancellationToken ct = default` | `Task<IReadOnlyDictionary<string, string?>>` | Liefert die Prozessumgebung für Agent-Kindprozesse (darf Secrets enthalten, nur Prozessumgebung — niemals Prompts/Logs/Persistenz). Bereits implementiertes Referenzmuster für nicht-persistente git-Auth (`GIT_CONFIG_*`/`http.extraheader`). |

## `GitPluginBase<TPlugin>` (abstrakte Basisklasse)

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`

Kein Interface im engeren Sinn, aber der Contract, den `GitHubPlugin` für die betroffenen git-Operationen implementiert:

| Member | Parameter | Rückgabewert | Zweck |
|--------|-----------|--------------|-------|
| `CloneRepositoryAsync` (abstract) | `string repositoryUrl`, `string targetPath`, `CancellationToken` | `Task` | Repository klonen; ruft im `GitHubPlugin` anschließend `ConfigureGitCredentialsAsync` |
| `PushBranchAsync` (abstract) | `string localPath`, `string branchName`, `CancellationToken` | `Task` | Branch pushen |
| `PullAsync` (abstract) | `string localPath`, `CancellationToken` | `Task` | `git pull` |
| `GetRemoteBranchesAsync` (abstract) | `string repositoryUrl`, `CancellationToken` | `Task<IEnumerable<string>>` | `git ls-remote --heads` |
| `GetDefaultBranchAsync` (abstract) | `string repositoryUrl`, `CancellationToken` | `Task<string>` | `git ls-remote --symref` |
| `CheckoutPullRequestSourceAsync` (virtual) | `string localPath`, `PullRequestCheckoutSpec checkoutSpec`, `CancellationToken` | `Task` | PR-Quelle fetchen/auschecken; `GitHubPlugin` nutzt die Basisimplementierung ohne eigene Umgebungsvariablen |
