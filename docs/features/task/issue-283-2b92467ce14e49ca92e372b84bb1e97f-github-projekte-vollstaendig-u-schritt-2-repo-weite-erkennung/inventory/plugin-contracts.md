# Plugin-Verträge und GitHub-Implementierung

Zurück: [../inventory.md](../inventory.md)

## `IScmEventSource`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext` (gitRepositoryId, pluginPrefix, repositoryId), `ct` | `ScmEventRepository` | Authentifizierte Identität + kanonischer Repo-Kontext |
| `ReadEventsAsync` | `ScmEventReadRequest` (Reference mit fester Number, ContentKind, SinceUtc, WatermarkUtc), `ct` | `ScmEventBatch` | Vollständiges Lesen einer Quelle |

**Kein** Enumerationsvertrag vorhanden. Weitere Plugins: nur `GitHubPlugin` implementiert `IScmEventSource` (+`IScmEventResponder`).

## Value Objects (`ScmEvents.cs`, `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/`)

| Typ | Felder | Bemerkung |
|-----|--------|-----------|
| `ScmReferenceKind` | `Issue`, `PullRequest` | Enum |
| `ScmContentKind` | `Description`, `IssueComment`, `ReviewComment`, `PullRequestReview` | Enum |
| `ScmEventRepository` | ProviderPrefix, Host, RepositoryId (=node_id), Owner, Name, OwnerId, ViewerId, ViewerLogin, CanWrite | Kanonische Identität |
| `ScmEventReference` | Repository, Kind, Number | |
| `ScmEventReadRequest` | Reference, ContentKind, SinceUtc, WatermarkUtc | SinceUtc enthält Überlappung des Aufrufers |
| `ScmEventBatch` | Request, Observations | |
| `ScmEventObservation` | Reference, ContentKind, RemoteNodeId, RemoteCommentId, CreatedAt, UpdatedAt, LastEditedAt, AuthorId/Login, EditorId/Login, Body, Url, ObservedAt | `ContentKey` = Provider/Host/RepoId/Kind/Number/ContentKind/NodeId; `EventKey` = ContentKey + Revision + BodyHash; `Revision` = LastEditedAt ?? CreatedAt |
| `ScmProviderException` | FailureKind, RetryAfterUtc, OutcomeUncertain | FailureKind: Authentication, Permission, RateLimit, Temporary, Permanent |
| `ScmRepositoryContext` (`ScmRepositoryContext.cs`) | GitRepositoryId, PluginPrefix, RepositoryId | Eingabe von `GetEventRepositoryAsync` |

## `GitHubPlugin.Events.cs` (`plugins/Softwareschmiede.Plugin.GitHub/`)

| Mitglied | Zeilen | Kurzbeschreibung |
|----------|--------|------------------|
| `TextFields` | 11 | GraphQL-Feldset: `id body createdAt updatedAt lastEditedAt url author{login,...on Node{id}} editor{...}` — liefert alle für Validierung/Dedup nötigen Metadaten. |
| `GetEventRepositoryAsync` | 14-30 | `GET user` + `GET repos/{owner}/{name}`; `RepositoryId`=`node_id`, `CanWrite` aus permissions; `ParseEventRepository` für HTTPS/SSH/`owner/repo`. |
| `ReadEventsAsync` | 33-93 | `ValidateReference`; Description via GraphQL `issue(number)/pullRequest(number)` + `TextFields`; PullRequestReview via `reviews(first:100,after:$cursor)` paginiert, `state != PENDING`, `submittedAt <= WatermarkUtc`; IssueComment/ReviewComment via REST `…/comments?per_page=100(+since)` + pro Kommentar GraphQL `node(id:)`-Snapshot. |
| `AddObservation` | 95-121 | Verwirft Post-Watermark-Texte (created/updated/lastEdited > Watermark) und Viewer-eigene Marker-Kommentare; baut URLs mit `#issuecomment-`/`#discussion_r`/`#pullrequestreview-`; mappt JSON → `ScmEventObservation`. |
| `RestPagesAsync` | 123-141 | Link-Header-Pagination, Zyklusschutz, `ValidateNextPage` (Host/Pfad-Fixierung). |
| `GraphAsync`/`SendEventApiAsync` | 151-194 | GraphQL-Fehlertypisierung (RATE_LIMITED/FORBIDDEN/UNAUTHORIZED); HTTP→`ScmProviderException` (401→Authentication, 403→Permission, 429→RateLimit mit Retry-After, 5xx→Temporary). |
| `ParseEventRepository`/`ValidateSegment`/`ValidateReference` | 217-260 | URL-Parsing + Identifier-Validierung. |
| `NextCursor`/`RequiredArray`/`RequiredDate` u. a. | 267-297 | JSON-Helfer; `NextCursor` mit Zyklusschutz. |

Transport: `IGitHubEventApiClient`/`GhEventApiClient` — `gh api --include`, Body via Tempdatei, Token als `GH_TOKEN`-Env.

## Weitere Plugin-APIs (nicht geeignet)

`IGitPlugin.GetIssuesAsync`/`GetOpenPullRequestsAsync` (`GitHubPlugin.cs:334-358`): flache `Issue`/`PullRequest`-Objekte ohne Autor-/Editor-IDs/node_id — **nicht** als Quelle für die Ereignisvalidierung geeignet (Explizit-Vorgabe der Anforderung, bestätigt durch Codevergleich).
