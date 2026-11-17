# Detailinventar: GitHub-Plugin — Authentifizierung, HTTP-Transport, Antwortwege

Bestandteil von [inventory.md](../inventory.md). Dateien unter `plugins/Softwareschmiede.Plugin.GitHub/`.

## Authentifizierung und Konto-Routing

- `GitHubPlugin` (partial, `GitHubPlugin.cs` Z. 19) implementiert `IScmEventSource`, `IScmEventResponder`, `IScmAgentAccess`, `IScmNotificationSource`. Es ist das **einzige** Plugin mit `IScmEventResponder` (BitBucket implementiert die Ereignis-Interfaces nicht).
- Credential-Keys (Z. 21–22): `Softwareschmiede.GitHub.Token` (Hauptkonto), `Softwareschmiede.GitHub.AgentToken` (Separates Konto); gelesen via `ICredentialStore`.
- Zwei Transportinstanzen (Z. 156–157): `_eventApiClient` (Haupttoken) und `_agentEventApiClient` (AgentToken), je `GitHubHttpApiClient(Func<string?> tokenProvider)` — Token wird pro Request neu aus dem Credential-Store gezogen.
- `ClientFor(ScmKontoModus)` (`GitHubPlugin.Events.cs` Z. 334–341): `SeparatesKonto` ohne AgentToken → `ScmProviderException(Authentication)`; Routing gilt nur für Ereignispfade.
- `GetAgentEnvironmentAsync` (`GitHubPlugin.cs` Z. 184–199): gibt dem CLI-Agenten `GH_TOKEN`, hostgebundenen `http.extraheader` (`AddGitHubHttpAuthHeader`, Z. 209–221) und `GIT_TERMINAL_PROMPT=0` als Prozessumgebung — nie in Prompt/Log/Persistenz.

## HTTP-Transport (`GitHubHttpApiClient.cs`, `IGitHubEventApiClient.cs`)

- `GitHubEventApiRequest(Host, Method, Endpoint, JsonBody?)` / `GitHubEventApiResponse(StatusCode, Body, Headers?)` — **nur JSON-String-Bodies** (`StringContent`, `application/json`, UTF-8, Z. 45–46). Kein binärer/multipart Content-Pfad.
- `SendAsync` (Z. 34–61): statisch geteilter `HttpClient` (100 s Timeout, Z. 13); Header `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`, `UserAgent: Softwareschmiede`, `Authorization: Bearer {token}` nur bei vorhandenem Token; Antwortheader werden komplett durchgereicht (Pagination/Backoff). Transportfehler → `ScmProviderException(Temporary)` mit `outcomeUncertain` für Nicht-GET außer `graphql` (Z. 56–60).
- `BuildRequestUri` (Z. 63–72): Host-Mapping — `github.com` → `https://api.github.com/{endpoint}` bzw. `.../graphql`; GHE → `https://{host}/api/v3/{endpoint}` bzw. `.../api/graphql`. **Ein Host wie `uploads.github.com` würde nach `{host}/api/v3/...` gemappt** — der Upload-Endpoint `uploads.github.com/user-attachments/assets` ist mit diesem Mapping nicht abbildbar.
- `ValidateHost` (Z. 77–88, `internal`): erzwingt reinen DNS-Hostnamen ohne UserInfo/Port/Pfad/Query/Fragment — dokumentierte Token-Grenze (Bearer darf nie an fremden Origin).
- Test-Seam: zweiter Konstruktor mit `HttpMessageHandler` (Z. 27–31).

## API-Hilfsschicht (`GitHubPlugin.Events.cs`)

- `EventApiAsync` (Z. 301–307): serialisiert Body → `SendEventApiAsync` → `ParseJson`; bei Mutation unleserliche Antwort → `Temporary` mit `outcomeUncertain: true`.
- `GraphAsync` (Z. 309–324): GraphQL-`errors[]` → `RATE_LIMITED`/`FORBIDDEN`/`UNAUTHORIZED`-Mapping.
- `SendEventApiAsync` (Z. 348–374): Status→`ScmProviderFailureKind`-Matrix (429 / 403+RateLimit → RateLimit; 401 → Authentication; 403 → Permission; ≥500 → Temporary; sonst Permanent), `Retry-After`/`X-RateLimit-Reset`-Auswertung, `outcomeUncertain` für Mutationen mit ≥500.
- `RestPagesAsync`/`RestObjectPagesAsync` (Z. 266–299): Link-Header-Pagination mit Zyklusschutz; `ValidateNextPage` (Z. 386–395) erzwingt gleichen API-Host + gleichen Pfad (gilt nicht für `uploads.*`-Origins).
- `RepositoryEndpoint` (Z. 442): `repos/{Owner}/{Name}`.
- `ParseEventRepository` (Z. 397–424), `ValidateSegment` (Z. 436–440), `ValidateReference` (Z. 426–434).
- `GetEventRepositoryAsync` (Z. 29–47): `GET user` + `GET repos/{owner}/{name}`; liefert `ScmEventRepository` mit `RepositoryId` = **`node_id`** (GraphQL-Knoten-ID, Z. 43), `ViewerId/ViewerLogin` aus `user`, `CanWrite` aus `permissions.push|admin|maintain`. **Die numerische `id` des Repositories wird nirgends gelesen** — der Attachment-Upload verlangt sie als `repository_id`-Queryparameter.
- JSON-Hilfen: `ParseJson`, `Object`, `String`, `Required`, `RequiredArray`, `NextCursor` u. a. (Z. 447–476).

## Kommentar-/PR-Antwortwege (`GitHubPlugin.EventResponses.cs`)

- `ExecuteResponseAsync` (Z. 39–77): Identitäts-Gegenprüfung via `GetEventRepositoryAsync` (RepositoryId + ViewerId müssen gespeicherten Werten entsprechen, `CanWrite` Pflicht, Z. 45–49) → `ReconcileResponseAsync` (Z. 50–51) → POST:
  - `Comment` → `repos/{o}/{r}/issues/{Number}/comments`, Body `{ body = ResponseBody(request) }` (Z. 64–65)
  - `PullRequest` → `repos/{o}/{r}/pulls`, Body `{ title, body = ResponseBody(request), head, base }` (Z. 67–68)
  - `Reaction` → REST- oder GraphQL-`addReaction` (Z. 53–58, 66)
  - Fehlende `id` in der Antwort → `Temporary` + `outcomeUncertain` (Z. 73–75).
- `ResponseBody` (Z. 140): `"{Body}\n\n{Marker}"` — einzige Textzusammensetzung; Marker = `<!-- softwareschmiede:action:{ActionId} -->` (`ScmResponseRequest.Marker`, `ScmEvents.cs` Z. 64).
- `ReconcileResponseAsync` (Z. 10–36): liest **alle** Seiten (Kommentare `/issues/{n}/comments`, PRs `/pulls?state=all`, Reaktionen) und matcht `IsViewer` + `body.Contains(Marker)`; Treffer → `AlreadyExisted = true` ohne erneuten POST.
- `SafeResponseUrl` (Z. 168–176): `html_url` nur bei https + Default-Port + `repo.Host` + Pfadpräfix `/{owner}/{name}/`.
- `ValidateResponse` (Z. 109–138): `ActionId`-Regex, Pflichtfelder je Kind; PR-Branches ohne `:`/Steuerzeichen.
- Kein `PATCH`-/`PUT`-Aufruf im Plugin; kein Code, der einen bereits gesendeten PR-/Kommentartext nachträglich verändert; kein Upload-/Attachment-Code.

## Host-Seite der Antworten (Application)

- `ScmResponseRequest` (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` Z. 58–65): `Reference, ActionId, Kind, Body, Reaction, ContentKind, RemoteCommentId, RemoteNodeId, Title, HeadBranch, BaseBranch`; serialisiert in `ScmOutgoingAction.RequestJson`.
- `IScmEventResponder` (`.../Domain/Interfaces/IScmEventResponder.cs`): `ReconcileResponseAsync`, `ExecuteResponseAsync`.
- `ScmEventOutbox` (`src/Softwareschmiede/Application/Services/ScmEventOutbox.cs`): `EnsureAsync` (Z. 14–30, Purpose ∈ `receipt`, `waiting`, `ai:{actionId}`) persistiert vor Remoteaufruf; `ExecuteAsync` (Z. 33–74) reconciliert zuerst, postet nie doppelt, `Uncertain`/`Failed`-Semantik mit `NextAttemptUtc`-Backoff.
- Erzeuger von `ScmResponseRequest` im Orchestrator (`ScmEventOrchestrator.cs`): Empfangsreaktion `eyes` (Z. 96–100), Wartehinweis-Kommentar (Z. 500–507, Marker `<!-- softwareschmiede:event:{id}:waiting -->`), KI-Kanalaktionen aus `actions/*.json` (Z. 406–408: `action.Body` → `request.Body`).

## Fehlertaxonomie

- `ScmProviderFailureKind`: `Authentication, Permission, RateLimit, Temporary, Permanent` (`ScmEvents.cs` Z. 80); `ScmProviderException` trägt `RetryAfterUtc` und `OutcomeUncertain` (Z. 83–100) — die Outbox-/Orchestrator-Retries hängen daran (Authentication/Permission → `NextAttemptUtc = long.MaxValue`).
