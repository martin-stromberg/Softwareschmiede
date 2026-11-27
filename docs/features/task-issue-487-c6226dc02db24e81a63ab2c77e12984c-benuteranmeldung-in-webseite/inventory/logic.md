# Bestandsaufnahme: Logikklassen des StatusServers

Alle Klassen im StatusServer sind `sealed` Konkretklassen ohne Interfaces; DI läuft
direkt auf die Klassen (Singleton bzw. typed `HttpClient`). Es gibt keine
Event-Mechanismen (keine abonnierten/publizierten Events); Kommunikation nach außen
läuft über den SignalR-Hub und die HTTP-Endpunkte.

## `Program` (Composition Root)
Datei: `server/Softwareschmiede.StatusServer/Program.cs` (70 Zeilen, Top-Level-Statements, `public partial class Program` für `WebApplicationFactory`)

| Element | Beschreibung |
|---------|--------------|
| `builder.Services.Configure<StatusServerOptions>` (Z. 10) | Bindet die gesamte Konfiguration |
| `AddSignalR()` (Z. 11) | SignalR-Registrierung |
| `AddSingleton(TimeProvider.System)` (Z. 12) | Zeitquelle (in Tests via `TestTimeProvider`-Muster ersetzbar, aktuell nicht überschrieben) |
| `AddSingleton(InstanceSnapshotCache)` (Z. 13–19) | Cache mit `App.StaleAfterSeconds` (Minimum 10 s) |
| `AddSingleton<RelayTokenValidator>()` (Z. 20) | Token-Prüfung (konfigurationsbasiert) |
| `AddSingleton<StatusPageComposer>()` (Z. 21) | HTML-Renderer |
| `AddHttpClient<GitHubStatusService>` (Z. 22–32) | Typed Client `https://api.github.com/`, UA-Header, Accept, Timeout 20 s, optionaler Bearer aus `GitHub:Token` |
| `app.UseMiddleware<PageAccessMiddleware>()` (Z. 36) | Basic-Auth-Middleware — **entfällt laut Anforderung** |
| `app.MapHub<StatusRelayHub>("/relay")` (Z. 38) | SignalR-Hub-Endpunkt |
| `MapGet("/")` (Z. 40–50) | Rendert Status-Seite (`InstanceSnapshotCache.GetAll` + `GitHubStatusService` + `StatusPageComposer.Render`) — **künftig `RequireAuthorization`** |
| `MapGet("/status.json")` (Z. 52–65) | JSON-Sicht (`GeneratedUtc`, `Instances`, `GitHub`) — **künftig `RequireAuthorization`** |

Nicht vorhanden: `UseAuthentication`/`UseAuthorization`, `AddDbContext`,
`AddIdentity`, `AddAntiforgery`, `ForwardedHeaders`, Datenbank-Migration beim Start,
`data`-Verzeichnis-Anlage, Login-/Register-/Admin-Endpunkte.

## `PageAccessMiddleware` — **entfällt vollständig**
Datei: `server/Softwareschmiede.StatusServer/Security/PageAccessMiddleware.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PageAccessMiddleware(RequestDelegate, IOptions<StatusServerOptions>)` | public ctor | Übernimmt Options-Snapshot |
| `InvokeAsync(HttpContext)` | public | Lässt `/relay`-Pfade, fehlende Credentials oder autorisierte Requests durch; sonst 401 + `WWW-Authenticate: Basic realm="Softwareschmiede-Status"` |
| `IsAuthorized(HttpContext)` | private | Parst `Authorization: Basic …`, vergleicht Benutzername/Passwort via `SecretComparer.FixedTimeEquals` |

Aktuell als **optionale** Middleware aktiv: ohne gesetzte `Page:Username`/`Page:Password`
ist die Seite offen (Dev-Modus). Wird durch Cookie-Auth + `RequireAuthorization` ersetzt.

## `RelayTokenValidator` — Umbau auf DB-Quelle
Datei: `server/Softwareschmiede.StatusServer/Security/RelayTokenValidator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RelayTokenValidator(IOptions<StatusServerOptions>, IHostEnvironment)` | public ctor | Liest `RelayOptions`, merkt `IsDevelopment()` |
| `IsValid(string? token)` | public | `false` bei null/leer; akzeptiert `Relay:DevToken` nur in Development; sonst Konstantzeit-Vergleich gegen jeden Eintrag von `Relay:Tokens` |

Synchron (`bool`), aktuell `Singleton`. Wird aufgerufen von `StatusRelayHub.OnConnectedAsync`.
Künftig: Prüfung gegen gehashte, nicht-widerrufene Tokens in der Datenbank — voraussichtlich
Scoped/DB-Zugriff statt Options-Snapshot; Signatur `IsValid` ggf. async.

## `SecretComparer` — weiterverwendbar
Datei: `server/Softwareschmiede.StatusServer/Security/SecretComparer.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `FixedTimeEquals(string?, string?)` | public static | Hasht beide Seiten (SHA-256) und vergleicht via `CryptographicOperations.FixedTimeEquals` — längenunabhängiger Konstantzeit-Vergleich |

Wird genutzt von `PageAccessMiddleware` (Basic-Auth) und `RelayTokenValidator`
(Relay-Tokens). Laut Anforderung für den Token-Hash-Vergleich weiterverwendbar.

## `StatusRelayHub`
Datei: `server/Softwareschmiede.StatusServer/Relay/StatusRelayHub.cs` — `: Hub`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InstanceIdItemKey` (`const string`) | public | `Context.Items`-Schlüssel `"status.instanceId"` |
| `StatusRelayHub(RelayTokenValidator, InstanceSnapshotCache, ILogger)` | public ctor | DI |
| `OnConnectedAsync()` | public override | Liest `access_token`-Query, Fallback `Authorization: Bearer`; `Context.Abort()` + Warn-Log bei ungültigem Token — **Transport unverändert, nur Validator-Quelle wechselt zur DB** |
| `RegisterInstance(RegisterInstanceRequest)` | public | Prüft `InstanceId` nicht leer + `ProtocolVersion == RelayProtocolVersion.Current`; legt `InstanceId` in `Context.Items` ab, `cache.MarkConnected`; Ergebnis `RegisterInstanceResult` |
| `PushSnapshot(StatusSnapshot)` | public | Wirft `HubException` ohne Registrierung; `cache.UpdateSnapshot` |
| `OnDisconnectedAsync(Exception?)` | public override | `cache.MarkDisconnected`, Warn-Log bei Fehler |

## `InstanceSnapshotCache`
Datei: `server/Softwareschmiede.StatusServer/Status/InstanceSnapshotCache.cs` — in-memory, `ConcurrentDictionary`, nicht persistent

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DefaultDisconnectedRetention` (`static readonly TimeSpan` = 24 h) | public | Aufbewahrung getrennter Records |
| `InstanceSnapshotCache(TimeProvider, TimeSpan staleAfter, TimeSpan? disconnectedRetention)` | public ctor | `staleAfter` muss > 0 sein |
| `MarkConnected(connectionId, instanceId, instanceName)` | public | Registriert Verbindung; entfernt `connectionId` zuerst aus anderen Instanzen; `EvictDisconnected` |
| `MarkDisconnected(connectionId)` | public | Entfernt Verbindung aus allen Instanzen |
| `UpdateSnapshot(instanceId, StatusSnapshot)` | public | Speichert Snapshot, setzt `LastSeenUtc` |
| `GetAll()` | public | Momentaufnahme aller Instanzen als `InstanceSnapshotView`, sortiert nach Name/ID; `EvictDisconnected` |
| `RemoveConnectionIdFromAll`, `EvictDisconnected` | private | Interne Pflege |
| `InstanceRecord` (nested private class) | private | `Sync`, `ConnectionIds`, `InstanceName`, `Snapshot`, `RegisteredUtc`, `LastSeenUtc` |

## `StatusPageComposer` — Stil-Vorbild für neue Formularseiten
Datei: `server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs` (391 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StatusPageComposer(TimeProvider)` | public ctor | |
| `Render(instances, gitHub, refreshSeconds)` | public | Komplettes HTML-Dokument via `StringBuilder` + `const string Css`; Meta-Refresh, Sections, Badges, Footer „read-only" |
| `RenderHeader`, `RenderInstance`, `RenderWorkItemRow`, `RenderScmEventRow`, `RenderGitHubRepository` | private | Abschnitts-Renderer |
| `IsWaiting`, `RunPill`, `ScmStatusPill`, `AppendLinked`, `IsSafeHttpUrl`, `ShortId`, `FmtTime`, `E` | private static | Formatierung; `E` = `WebUtility.HtmlEncode`; `IsSafeHttpUrl` = http(s)-Scheme-Whitelist |
| `Css` (`const string`) | private | Eingebettetes, responsives CSS |

Wichtig für die Anforderung: Die neuen Formularseiten (Registrierung, Login, Profil,
Admin) sollen denselben serverseitig gerenderten, JavaScript-freien Stil übernehmen
(Annahme in der Anforderung); `E`/`IsSafeHttpUrl` sind die bestehenden
Encoding-/URL-Schutz-Helfer.

## `GitHubStatusService`
Datei: `server/Softwareschmiede.StatusServer/GitHub/GitHubStatusService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GitHubStatusService(HttpClient, IOptions<StatusServerOptions>, TimeProvider, ILogger)` | public ctor | TTL aus `GitHub.CacheTtlSeconds` (Minimum 5 s) |
| `GetRepositoryStatusesAsync(CancellationToken)` | public | Iteriert `GitHub.Repositories`, parst via `GitHubRepositoryOptions.TryParse`, aggregiert je Repo |
| `GetRepositoryStatusAsync`, `GetCachedJsonAsync`, `StoreAsync` | private | ETag-/TTL-Cache (`SemaphoreSlim`, In-Memory-`Dictionary`), 304-Revalidierung, Fehler mit `ErrorUtc` |
| `DescribeError`, `TryDeserialize` | private static | Deutsche Fehlertypen für die Anzeige |
| `CacheEntry`, `EndpointResult<T>`, `GitHubPullRequestResponse`, `GitHubReleaseResponse`, `GitHubIssueResponse` | private nested | Cache- und API-Response-Records |

Nicht direkt von der Anforderung betroffen — bleibt funktional unverändert; relevant nur,
weil `/` und `/status.json` es aufrufen und die Seiten künftig hinter Auth liegen.
