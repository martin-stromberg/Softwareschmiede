# Interfaces — Verträge der Token-Kette

## Credential- und Prozess-Verträge

### `ICredentialStore` — `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs`

```csharp
string? GetCredential(string target);          // Zeile 8
void SetCredential(string target, string value); // Zeile 13
void DeleteCredential(string target);          // Zeile 17
```

Schlanker Speichervertrag ohne Längen-/Formatbedingungen. Implementierungen:
`WindowsCredentialStore` (produktiv, siehe [logic.md](logic.md) Abschnitt 2),
`ScmE2ECredentialStore` (In-Memory, E2E-Modus), diverse In-Memory-Testdoubles
(siehe [tests.md](tests.md)).

### `ICliRunner` — `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`

```csharp
Task<CliResult> RunAsync(string command, IEnumerable<string> args,
    string? workingDirectory, IDictionary<string, string>? environmentVariables,
    CancellationToken ct = default);                        // Zeilen 17–22
IAsyncEnumerable<string> StreamAsync(/* dieselbe Signatur */); // Zeilen 30–35
```

Vertrag: `environmentVariables` trägt die Geheimnisse; `args` gehen sicher über
`ArgumentList`. Implementierung `CliRunner` (siehe [logic.md](logic.md) Abschnitt 7).
Testseam: `Mock<ICliRunner>` fängt die übergebenen Umgebungs-Dictionaries ab
(`HasGitHubExtraHeader`-Assertions, siehe [tests.md](tests.md)).

## GitHub-Transport

### `IGitHubEventApiClient` — `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs`

```csharp
Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default);   // Zeile 7
Task<GitHubEventApiResponse> UploadAsync(GitHubAssetUploadRequest request, CancellationToken ct = default); // Zeile 12
```

Credentialfreier Transportvertrag: Requests tragen Host/Method/Endpoint/JsonBody —
der Token kommt ausschließlich über den `tokenProvider` der Implementierung
(`GitHubHttpApiClient`) in den `Authorization`-Header. Testseam:
`FakeGitHubEventApi` (Warteschlangen-Fake, siehe [tests.md](tests.md)).

## SCM-Fähigkeiten (Plugin → Host)

### `IScmEventSource` — `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

- `GetEventRepositoryAsync(ScmRepositoryContext, ct)` → `ScmEventRepository`
  (Zeile 9): Viewer-Identität + `CanWrite` — der zentrale Auflösungspunkt des
  Tokens gegen GitHub.
- `ReadEventsAsync` (Zeile 12), `EnumerateReferencesAsync` (Zeilen 16–17),
  `ReadIssueAsync` (Zeile 21), `ReadPullRequestAsync` (Zeile 25).

### `IScmEventResponder` — `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`

- `ReconcileResponseAsync` (Zeile 13), `ExecuteResponseAsync` (Zeile 16) —
  Fehler als typisierte `ScmProviderException`, nie still ignorieren.

### `IScmNotificationSource` — `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`

- `EnumerateNotificationsAsync(host, sinceUtc, ct)` (Zeilen 11–12): Inbox des
  separaten Kontos.

### `IScmAgentAccess` — `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`

- `GetAgentEnvironmentAsync(ScmAccountMode account, string? repositoryUrl, ct)`
  → `IReadOnlyDictionary<string, string?>` (Zeile 25). Vertrag: Rückgabewerte können
  Geheimnisse enthalten, gehören ausschließlich in die Prozessumgebung — niemals
  Prompts, Protokolle, Persistenz (Zeilen 15–18). Leere Map = keine Anbindung,
  blockiert den CLI-Start nicht (Zeilen 18–19).

### `IScmRepositoryAliasNormalizer` — `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmRepositoryAliasNormalizer.cs`

- Alias-Normalisierung für die kanonische Gruppierung (`NormalizeRepositoryAlias`,
  `GitHubPlugin.cs` Zeilen 77–81).

## Git-Plugin-Verträge

### `IGitPlugin` — `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

Token-relevante Member (vollständige Liste siehe Datei, 212 Zeilen):

| Member | Zeile | Tokenpfad |
|--------|-------|-----------|
| `CheckHealthAsync` | 117 | `GET user` via `_eventApiClient` — **kein produktiver Aufrufer** |
| `GetAvailableRepositoriesAsync` | 164 | `GET user/repos` |
| `CloneRepositoryAsync` | 30 | git + `GetGitEnvironment` |
| `PushBranchAsync` / `PullAsync` | 43 / 48 | git + `GetGitEnvironment` |
| `GetRemoteBranchesAsync` / `GetDefaultBranchAsync` | 123 / 129 | `git ls-remote` + `GetGitEnvironment` |
| `CheckoutPullRequestSourceAsync` | 138–142 | `git fetch/checkout` + `GetGitEnvironment` |
| `CreateIssueAsync` (via `IIssueCreateProvider`) | — | `gh issue create` + `GetGhEnvironment` |
| `CreatePullRequestAsync` | 57 | `gh pr create` + `GetGhEnvironment` |
| `CompletePullRequestAsync` / `MarkPullRequestReadyAsync` | 85–90 / 96–100 | `gh pr merge/review/ready` + `GetGhEnvironment` |
| `GetIssueTemplatesAsync` (via `IIssueTemplateProvider`) | — | REST `contents/` via `_eventApiClient` |
| `GetAlertsAsync` (via `IScmAlertProvider`) | — | REST `code-scanning/alerts` |
| `GetRepositoryStructure(LoadResult)Async` | 171–179 | REST `git/trees` |

`GitPluginBase<TPlugin>` (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`):
`CheckHealthAsync` abstrakt (Zeile 75), `GetAvailableRepositoriesAsync` virtual →
leere Liste (Zeilen 338–339), `RunGitAsync` (Zeilen 329–336) mit optionalem
`environmentVariables`-Parameter, `CheckoutPullRequestSourceCoreAsync`
(Zeilen 189–222), `ToHttpsRepositoryUrl` (271–323), `ScpSshUrlPattern` (Zeile 12),
`ResolvePullRequestFetchRemote` (232–238).

### `IPlugin` / Metadaten

`GitHubPlugin : GitPluginBase<GitHubPlugin>, IScmEventSource, IScmEventResponder,
IScmAgentAccess, IScmNotificationSource, IScmRepositoryAliasNormalizer`
(`GitHubPlugin.cs` Zeile 20). `PluginPrefix` = `Softwareschmiede.GitHub` (Zeile 54),
`PluginType` = `SourceCodeManagement` (Zeile 57).

## KI-Plugin-Vertrag (Kontext)

`IAiPlugin.CheckHealthAsync` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IAiPlugin.cs`
Zeile 26) — die **einzige** produktiv gerufene `CheckHealthAsync`-Variante
(`ScmEventOrchestrator.cs` Zeilen 257/724, `AiExecutionService.cs` Zeile 356,
`ITerminalSessionFactory`/`TerminalSessionDiagnostics` als Delegate). Relevant für die
Bewertung der `IGitPlugin.CheckHealthAsync`-Bruchstelle.
