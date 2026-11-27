# StatusServer — Ist-Zustand

Basis: `server/Softwareschmiede.StatusServer` (SDK `Microsoft.NET.Sdk.Web`, `TargetFramework net11.0`, `WarningsAsErrors CS1591` → XML-Doc-Pflicht auf allen public-Membern). Projektreferenz auf `src/Softwareschmiede.StatusContracts` (net11.0); Pakete: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Sqlite` (je 11.0.0).

## `Program.cs` (145 Zeilen)

- **Options:** `builder.Services.Configure<StatusServerOptions>(builder.Configuration)` (Z. 17) — bindet die gesamte Root-Konfiguration.
- **DI:** `AddSignalR`, `TimeProvider.System` (Singleton), `InstanceSnapshotCache` (Singleton, TTL aus `App:StaleAfterSeconds`), `StatusPageComposer` (Singleton), `AddHttpClient<GitHubStatusService>` mit BaseAddress `api.github.com`, UserAgent, 20 s Timeout und optionalem Bearer-Token aus `GitHub:Token` (Z. 28–38).
- **Datenbank:** Pfad aus `Data:DatabasePath`, sonst `<ContentRoot>/data/statusserver.db`; Verzeichnis wird angelegt; `AddDbContext<StatusServerDbContext>` mit SQLite (Z. 40–46). Das `data`-Verzeichnis liegt **im Anwendungsstamm** — relevant für Update-Ausschlüsse.
- **Identity/AuthZ:** `AddIdentity<ApplicationUser, IdentityRole>` + EF-Stores; `AddAntiforgery()`; Policies `StatusRoles.PageAccessPolicy` (beide Rollen) und `StatusRoles.AdminOnlyPolicy` (nur `Administration`) (Z. 58–65); Application-Cookie `/login`, `SameSite=Lax`, `ExpireTimeSpan` aus `ServerSettingsService.DefaultSessionDurationMinutes`, `SlidingExpiration`, `StatusServerCookieEvents` (Z. 66–78); `ForwardedHeadersOptions` (KnownProxies/Networks geleert) (Z. 79–86).
- **Scoped Services:** `RelayTokenValidator`, `RelayTokenService`, `ServerSettingsService`, `UserAdminService`, `StatusServerCookieEvents`; Singleton `RelayTokenPlaintextStore` (Z. 88–93).
- **Pipeline:** `StatusServerDatabaseInitializer.InitializeAsync` in Scope (Migrationen + Rollen sicherstellen, Z. 97–98); `UseForwardedHeaders` → `UseAuthentication` → `UseAuthorization` → `UseAntiforgery` (Z. 100–103).
- **Endpunkte:** `MapHub<StatusRelayHub>("/relay")`, `MapAuthEndpoints()`, `MapAdminEndpoints()` (Z. 105–107); `GET /` (HTML-Statusseite) und `GET /status.json` — beide `PageAccessPolicy` (Z. 109–140).
- `public partial class Program;` am Ende — Ansatzpunkt für `WebApplicationFactory<Program>`.

## Admin-Oberfläche (#487)

### `Endpoints/AdminEndpoints.cs` (362 Zeilen)

- Mapping (Z. 34–49): `GET/POST /admin/users`, `POST /admin/users/{id}/role|deactivate|activate|unlock`, `GET/POST /admin/users/{id}/delete`, `GET/POST /admin/tokens`, `GET/POST /admin/tokens/{id:long}/revoke`, **`GET /admin/settings` + `POST /admin/settings/session-duration`** — alle unter `AdminOnlyPolicy`, alle POST mit `.RequireAntiforgery()`.
- **Post/Redirect/Get-Muster:** Erfolgs-POST → `Results.Redirect("{path}?msg={key}")` (`RedirectWithMessage`, Z. 290); Nicht-gefunden-Fehler → `?err={key}` (`RedirectWithError`, Z. 295); sonstige fachliche Fehler rendern die Seite direkt mit Fehlermeldung (Kommentar Z. 9–24).
- `GetSettings` (Z. 258) → `RenderSettingsAsync` (Z. 348): lädt `PageUserContext` via `EndpointSecurityExtensions.BuildUserContextAsync` + `settings.GetSessionDurationAsync` → `AdminPagesComposer.RenderSettings(user, minutes, message, isError)`.
- `PostSessionDuration` (Z. 267): `ReadFormAsync`, `int.TryParse` + Range-Check (`Min`/`MaxSessionDurationMinutes`) → Fehler direkt rendern; Erfolg → `SetSessionDurationAsync` + Redirect `?msg=settings-saved`.
- Formularwerte kommen aus `form["name"]` (kein Model-Binding), Handler sind `private static` mit DI-Parametern.

### `Pages/AdminPagesComposer.cs` (317 Zeilen)

- `RenderSettings(PageUserContext, int sessionMinutes, string? message, bool isError)` (Z. 193–220): Sektion „Sitzungsdauer" mit `<form method="post" action="/admin/settings/session-duration">`, `PageShell.AppendHiddenToken` (Antiforgery), Number-Input `minutes`, Submit-Button. JS-frei, alles HTML-escaped über `PageShell.E`.
- Weitere Composer: `RenderUserList`, `RenderTokenList`, `RenderUserDeleteConfirm`, `RenderTokenRevokeConfirm` — dieselbe Struktur (Section + Message + Form/Table).

### `Pages/PageShell.cs` / `PageUserContext.cs`

- `PageShell`: `BeginPage`, `AppendHeader`, `AppendNav` (Status/Profil + Admin-Links Benutzer/Tokens/Einstellungen bei `IsAdmin`), `AppendMessage`, `AppendHiddenToken`, `EndPage`, `E` (HTML-Encode), `HtmlContentType`. Navigations-Links der Admin-Seiten sind hier gepflegt (Z. 94–114) — neue Admin-Seiten bräuchten hier ggf. einen Link.
- `PageUserContext(UserName, IsAdmin, AntiforgeryToken, AntiforgeryFieldName)` — wird von `BuildUserContextAsync` (User + Rollen + `antiforgery.GetAndStoreTokens`) gebaut.

### `Endpoints/EndpointSecurityExtensions.cs`

- `RequireAntiforgery()` (Z. 40): Endpoint-Filter, `antiforgery.ValidateRequestAsync`; `AntiforgeryValidationException` → 400 mit `AuthPagesComposer.RenderFormExpired()`.

### `Endpoints/StatusMessages.cs`

- Feste Schlüssel→Text-Tabelle (Z. 45–59): `user-created`, `role-changed`, `user-deactivated`, `user-activated`, `user-deleted`, `lockout-reset`, `token-revoked`, `settings-saved`, `password-changed` (+ Fehler `user-not-found`, `token-not-found`). `Resolve(msg)`/`Resolve(msg, err)` — unbekannte Schlüssel → `null` (kein Freitext in URLs möglich). Neue Update-Meldungen müssen hier als Schlüssel+Text ergänzt werden.

### `Security/StatusRoles.cs`

- `Administration`, `Ueberwacher`, `All`, `PageAccessPolicy = "PageAccess"`, `AdminOnlyPolicy = "AdminOnly"`, `DisplayName`.

## Persistente Einstellungen

### `Security/ServerSettingsService.cs`

- `SessionDurationKey = "SessionDurationMinutes"`, `DefaultSessionDurationMinutes = 480`, `Min = 5`, `Max = 43200`.
- `s_writeLock` (statisches `SemaphoreSlim`, Z. 29) serialisiert Check-then-act-Upserts (Single-Instanz-Annahme dokumentiert).
- `GetSessionDurationAsync` / `SetSessionDurationAsync` als Muster für neue Schlüssel (z. B. Update-Kanal, Automatisierungsmodus — gleiche Key/Value-Tabelle, **keine Migration nötig**).

### `Data/`

- `ServerSetting{Key(PK, ≤100), Value(≤500)}`; `StatusServerDbContext` (IdentityDbContext + `DbSet<RelayToken>`, `DbSet<ServerSetting>`; `DateTimeOffset` als Unix-ms konvertiert); `StatusServerDatabaseInitializer` (`MigrateAsync` + Rollen-Seed); `StatusServerDbContextDesignTimeFactory`; `ApplicationUser`, `RelayToken`.

## Konfiguration

- `Options/StatusServerOptions.cs`: `App` (`StaleAfterSeconds`), `GitHub` (`Repositories`, **`Token`** — optionales PAT für Rate-Limit), `Page` (`RefreshSeconds`); `Options/GitHubRepositoryOptions.cs` (`owner/name`-Parser, `DisplayName`).
- `appsettings.json`: `App`, `GitHub`, `Page`, `Logging`, `AllowedHosts` — **keine** Update-Sektion bisher. `appsettings.Development.json` ist leer.
- `Properties/launchSettings.json` existiert (Port 5055 laut README).
- **Kein `version.json`-Handling und kein `release-metadata.json`** im StatusServer-Projekt — die installierte Version ist derzeit nirgends ermittelbar (Lücke für msTools.Updater, siehe `release-pipeline.md`).

## Weitere Komponenten

- `GitHub/GitHubStatusService.cs`: serverseitige GitHub-Aggregation (PRs, Releases, Issues) mit ETag/TTL-Cache; nutzt `GitHub:Token` als Bearer — Referenzpunkt für Token-Wiederverwendung.
- `Relay/StatusRelayHub.cs`: SignalR-Hub (`RegisterInstance`, `PushSnapshot`).
- `Status/InstanceSnapshotCache.cs`, `Status/StatusPageComposer.cs`: Snapshot-Cache + Statusseiten-Rendering.
- `AuthEndpoints.cs`, `AuthPagesComposer.cs`, `PasswordPolicy`, `UserNamePolicy`, `UserAdminService`, `RelayTokenService/Validator`, `RelayTokenPlaintextStore`, `SecretComparer`, `StatusServerCookieEvents`.

## Deployment-Doku

`server/README.md` dokumentiert: self-contained ZIPs `statusserver-linux-x64.zip` (systemd/nginx-Pfad) und `statusserver-win-x64.zip` (direkter Start, Windows-Dienst oder IIS über Hosting Bundle); Beispiel-systemd-Unit mit `Restart=on-failure`, `WorkingDirectory=/opt/softwareschmiede-status`; `data/` im WorkingDirectory → Sicherungshinweis. Abschnitt „Veröffentlichen" beschreibt die Release-Assets. Für das Update-Feature ist hier die Doku der Update-Konfiguration und des Dienst-Neustarts einzuhängen.
