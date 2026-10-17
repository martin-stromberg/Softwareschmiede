# Bestandsaufnahme — Interfaces und Wertobjekte

Zurück: [../inventory.md](../inventory.md)

## `IScmEventSource`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext` | `Task<ScmEventRepository>` | Authentifizierte Identität + kanonischer Repository-Kontext (node_id, Owner, Viewer, CanWrite) |
| `ReadEventsAsync` | `ScmEventReadRequest` | `Task<ScmEventBatch>` | Vollständiges Lesen einer (Referenz, ContentKind)-Quelle |
| `EnumerateReferencesAsync` | `ScmEventRepository`, `DateTimeOffset? sinceUtc` | `Task<IReadOnlyList<ScmEventReference>>` | Repo-weite Referenzliste (offene immer, geschlossene im Aktivitätsfenster) |

**Lücke:** Kein Vertrag zum Laden der Anlagedaten (Titel, Body, URL, PR-Quell-Branch) einer einzelnen Referenz.

## `IGitPlugin` (Ausschnitt)

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

- `GetIssuesAsync(repositoryId)` → `IEnumerable<Issue>` (flache Issues ohne Ereignis-Metadaten; `repositoryId` = `owner/repo`-Form bzw. `RepositoryUrl`).
- `GetOpenPullRequestsAsync(repositoryId)` → `IEnumerable<PullRequest>` — nur **offene** PRs; liefert die vollständigen `PullRequest`-VODaten inkl. `SourceRef`/`SourceRepositoryUrl`, wie `CreateFromPullRequestAsync` sie verlangt.

## Wertobjekte (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/`)

| Typ | Relevante Member |
|-----|------------------|
| `ScmEventRepository` (record) | `ProviderPrefix`, `Host`, `RepositoryId` (node_id), `Owner`, `Name`, `OwnerId`, `ViewerId`, `ViewerLogin`, `CanWrite` — Record-Gleichheit über alle Werte |
| `ScmEventReference` (record) | `Repository`, `Kind` (`Issue`/`PullRequest`), `Number` |
| `ScmEventReadRequest` (record) | `Reference`, `ContentKind`, `SinceUtc`, `WatermarkUtc` |
| `ScmEventBatch` (record) | `Request`, `IReadOnlyList<ScmEventObservation>` |
| `ScmEventObservation` (record) | `Reference`, `ContentKind`, `RemoteNodeId`, `RemoteCommentId`, `CreatedAt`, `UpdatedAt`, `LastEditedAt`, `Author*`/`Editor*`, `Body`, `Url`, `ObservedAt`; abgeleitet `BodyHash`, `Revision`, `ContentKey`, `EventKey`. **Kein Titel, keine Branch-Daten.** |
| `Issue` (record) | `Nummer`, `Titel`, `Body`, `Labels`, `Milestone`, `IssueUrl` |
| `PullRequest` (record) | `Nummer`, `Titel`, `Url`, `BranchName`, `Provider`, `RepositoryId`, `ProviderPullRequestId`, `TargetBranch`, `HeadSha`, `SourceRepositoryId`, `SourceRepositoryUrl`, `SourceRef`, `Body` |
| `ScmRepositoryContext` | `GitRepositoryId`, `PluginPrefix`, `RepositoryId` (providerinterne API-ID, kein URL) |
| `PullRequestRepositoryId` (static) | `Normalize(provider, repositoryId)` → `owner/name` (GitHub: lowercase); `TryNormalize` |
| `PullRequestProviderDescriptor` (static) | `GetPluginPrefix(provider)` (GitHub → `Softwareschmiede.GitHub`), `GetDisplayName`, `ParseBitbucketHostingMode` — **kein** inverses Mapping Prefix → Provider |
| `ScmProviderException` / `ScmProviderFailureKind` | `Authentication`, `Permission`, `RateLimit`, `Temporary`, `Permanent`; `RetryAfterUtc`, `OutcomeUncertain` |
| `ScmRepositoryContext` | Validierter lokaler+externer Kontext für `CreateFromPullRequestAsync` |
