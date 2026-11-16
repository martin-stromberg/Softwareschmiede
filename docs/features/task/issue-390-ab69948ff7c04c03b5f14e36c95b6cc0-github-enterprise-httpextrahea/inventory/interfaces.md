# Interfaces

## `IScmAgentAccess`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAgentEnvironmentAsync` | `ScmKontoModus konto`, `CancellationToken ct = default` | `Task<IReadOnlyDictionary<string, string?>>` | Z. 22. Liefert Umgebungsvariablen für den CLI-Kindprozess des Agenten (kann Geheimnisse enthalten — nur Prozessumgebung, nie Prompts/Logs/Persistenz). Leere Map = keine Anbindung; darf den CLI-Start nicht blockieren. XML-Doc Z. 12–21. |

Implementierer: ausschließlich `GitHubPlugin` (`GitHubPlugin.cs` Z. 183; grep-Verifikation — kein weiterer Implementierer, Tests nutzen `Mock<IGitPlugin>.As<IScmAgentAccess>()`).
Einziger produktiver Aufrufer: `KiAusfuehrungsService.WithScmAgentEnvironmentAsync` (`KiAusfuehrungsService.cs` Z. 444).

## `IGitPlugin` (auszugsweise — betroffene Signaturen)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CloneRepositoryAsync` | `repositoryUrl`, `targetPath`, `ct` | `Task` | Z. 30. URL-basiert — Zielhost aus Parameter ableitbar. |
| `PushBranchAsync` | `localPath`, `branchName`, `ct` | `Task` | Z. 43. Nur Arbeitskopie — keine Repository-URL im Contract. |
| `PullAsync` | `localPath`, `ct` | `Task` | Z. 48. Nur Arbeitskopie. |
| `GetRemoteBranchesAsync` | `repositoryUrl`, `ct` | `Task<IEnumerable<string>>` | Z. 123. URL-basiert. |
| `GetDefaultBranchAsync` | `repositoryUrl`, `ct` | `Task<string>` | Z. 129. URL-basiert. |
| `CheckoutPullRequestSourceAsync` | `localPath`, `PullRequestCheckoutSpec checkoutSpec`, `ct` | `Task` | Z. 138–142 (Default-Implementierung wirft `NotSupportedException`). `localPath` + Spec mit `TargetRepositoryUrl`/`SourceRepositoryUrl`. |
| `CheckHealthAsync` | `ct` | `Task<bool>` | Z. 117. Parameterlos — kein Host-Kontext. |

Die `localPath`-basierten Signaturen tragen keine Repository-URL; jede Änderung daran wäre ein Signaturbruch für alle Implementierer (`GitHubPlugin`, `BitBucketPlugin`, `LocalDirectoryPlugin`).

## `ICliRunner`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync` | `command`, `args`, `workingDirectory`, `IDictionary<string, string>? environmentVariables`, `ct` | `Task<CliResult>` | Z. 17–22. Einziger Transport der `GIT_CONFIG_*`-Umgebung an git-Kindprozesse. |
| `StreamAsync` | gleiche Signatur | `IAsyncEnumerable<string>` | Z. 30–35. Zeilen-Streaming. |

## `IGitHubEventApiClient`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `SendAsync` | `GitHubEventApiRequest request`, `ct` | `Task<GitHubEventApiResponse>` | Z. 7. Credentialfreier Ereignistransport; `GitHubEventApiRequest.Host` (Z. 11) trägt den Zielhost bereits — REST/GraphQL sind hostfähig. |
