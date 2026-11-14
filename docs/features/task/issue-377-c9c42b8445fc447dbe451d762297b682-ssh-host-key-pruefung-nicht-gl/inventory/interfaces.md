# Interfaces

## `IGitPlugin`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs` — Vertrag aller SCM-Plugins; für diese Anforderung sind die git-Transport-Signaturen relevant (keine Änderung am Vertrag selbst nötig).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CloneRepositoryAsync` | `repositoryUrl`, `targetPath`, `ct` | `Task` | Klont das Repository; `repositoryUrl` ist formatagnostisch deklariert (Z. 26-30) |
| `PushBranchAsync` | `localPath`, `branchName`, `ct` | `Task` | Push mit `--set-upstream` (Z. 39-43) |
| `PullAsync` | `localPath`, `ct` | `Task` | Holt Remote-Änderungen (Z. 45-48) |
| `GetRemoteBranchesAsync` | `repositoryUrl`, `ct` | `Task<IEnumerable<string>>` | `ls-remote --heads` ohne Klon (Z. 119-123) |
| `GetDefaultBranchAsync` | `repositoryUrl`, `ct` | `Task<string>` | Standard-Branch via `ls-remote --symref` (Z. 125-129) |
| `CheckoutPullRequestSourceAsync` | `localPath`, `checkoutSpec`, `ct` | `Task` | PR-Quell-Checkout; Default wirft `NotSupportedException` (Z. 137-142) |
| `GetRepositoryLinkFields` | — | `IReadOnlyList<PluginSettingField>` | UI-Felder der Repository-Verknüpfung (Z. 9-15) |

## `ICliRunner`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs` — einzige Prozess-Ausführungsbrücke; `environmentVariables` ist der einzige Weg, dem Kindprozess `GIT_SSH_COMMAND`/`GIT_CONFIG_*` mitzugeben.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync` | `command`, `args`, `workingDirectory`, `environmentVariables`, `ct` | `Task<CliResult>` | Führt einen CLI-Prozess aus (Z. 17-22); `environmentVariables` darf `null` sein |
| `StreamAsync` | gleiche Signatur | `IAsyncEnumerable<string>` | Zeilenweises stdout-Streaming (Z. 30-35) |

## `IScmAgentAccess`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAgentEnvironmentAsync` | `konto` (`ScmKontoModus`), `ct` | `Task<IReadOnlyDictionary<string, string?>>` | Prozessumgebung für Agent-CLI-Starts (Z. 22); `GitHubPlugin`-Implementierung setzt heute kein `GIT_SSH_COMMAND` |

## `IGitHubEventApiClient`

Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs` — interner API-Transport (`SendAsync(GitHubEventApiRequest, ct)`); für die git-Transportfrage nicht betroffen, aber `GitHubEventApiRequest.Host` trägt den GHE-fähigen Host, der auch in `ParseEventRepository` produziert wird.
