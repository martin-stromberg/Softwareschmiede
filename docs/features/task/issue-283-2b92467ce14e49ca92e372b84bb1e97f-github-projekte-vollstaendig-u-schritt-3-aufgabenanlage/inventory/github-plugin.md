# Bestandsaufnahme — GitHub-Plugin und E2E-Simulator

Zurück: [../inventory.md](../inventory.md)

## `GitHubPlugin` (partial, `plugins/Softwareschmiede.Plugin.GitHub/`)

`GitHubPlugin : GitPluginBase<GitHubPlugin>, IScmEventSource, IScmEventResponder`; `PluginPrefix = "Softwareschmiede.GitHub"`. Einziger `IScmEventSource`-Implementierer.

### `GitHubPlugin.Events.cs`

| Bereich | Inhalt |
|---------|--------|
| `TextFields` (Z. 11) | GraphQL-Feldliste `id body createdAt updatedAt lastEditedAt url author{…} editor{…}` — **kein `title`** |
| `GetEventRepositoryAsync` | `GET user` + `GET repos/{o}/{n}` → `ScmEventRepository` (node_id, CanWrite aus permissions) |
| `ReadEventsAsync` | Description: GraphQL `issue(n)`/`pullRequest(n)` mit `TextFields`; IssueComment/ReviewComment: REST `…/comments` + GraphQL-`node(id:)`-Nachschlag; PullRequestReview: GraphQL `reviews(first:100)` paginiert |
| `EnumerateReferencesAsync` | `issues`/`pullRequests` paginiert, `orderBy UPDATED_AT DESC`, offene immer + geschlossene im `sinceUtc`-Fenster |
| `AddObservation` | Watermark-Filter, Viewer-Marker-Filter, URL-Aufbau mit Fragmenten |
| `GraphAsync`/`EventApiAsync`/`SendEventApiAsync`/`RestPagesAsync` | Transport über `IGitHubEventApiClient` (`gh api`-basiert); typisierte `ScmProviderException` |
| `ParseEventRepository`/`ValidateNextPage`/`ValidateReference` | URL-Parsing und Sicherheitsvalidierung (Host/Segment-Checks) |

### `GitHubPlugin.cs` (Stammdatel, relevante Teile)

- `GetIssuesAsync` (Z. 334+): REST `issues` → `Issue`-VOs (Titel, Body, Labels, Milestone, `html_url`).
- `GetOpenPullRequestsAsync` (Z. 722-752) + `ParseOpenPullRequests`/`AddOpenPullRequest` (Z. 755-828): REST `pulls?state=open` → `PullRequest` inkl. `head.ref`/`head.sha`/`head.repo.full_name`/`clone_url`, `base.ref`, `node_id`; `SourceRef = "refs/heads/{branch}"`. Filtert `state != "open"`.
- `NormalizeRepositoryId`/`PullRequestRepositoryId.Normalize` für `owner/repo`-IDs.

## E2E-Simulator `ScmE2EApiClient`

Datei: `src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs` — interner `IGitHubEventApiClient`-Ersatz, der Szenario-Texte (`ScmE2EText` aus `ScmE2ETestConfiguration`, Felder `Number`, `Id`, `NodeId`, `Body`, `Kind`, `PullRequest`, `Author`, `CreatedAt`, `EditedAt`) als GitHub-API-Antworten ausspielt; schreibt jede Anfrage in `CallsPath`.

| Behandelte Aufrufe | Antwort |
|--------------------|---------|
| `GET user` | viewer `scm-owner`/`OWNER` |
| `GET repos/scm-owner/scm-e2e` | `node_id = REPO`, `CanWrite` aus Szenario |
| `POST graphql` mit `variables.id` | `node(id:)`-Nachschlag einzelner Texte |
| `POST graphql` mit `variables.number` | `issue(n)`/`pullRequest(n)`-Beschreibung bzw. `reviews()` (leer) |
| `GET …/(issues|pulls)/{n}/comments` | REST-Kommentarliste aus Szenario-Texten |
| `POST …/comments|reactions`, `GET …/reactions` | Antwortkanal-Mutationen/Abgleich |

**Lücken:** `graphql`-Aufrufe mit `variables.owner/name/cursor` (Enumeration `issues(`/`pullRequests(`) werfen (`GetProperty("number")` schlägt fehl) — Enumerations-Checkpoint schlägt im E2E derzeit fehl; die neuen Detail-Aufrufe (`issue/pullRequest` mit Titel, REST `pulls/{n}`) müssen ergänzt werden, ebenso ein Titel-Feld in den Szenario-Texten (derzeit kein `Titel` in `ScmE2EText`/`Node(...)`).
