# Logik / Services / Endpunkte

## `PageShell`

Datei: `server/Softwareschmiede.StatusServer/Pages/PageShell.cs` — `internal static class`

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `HtmlContentType` | `internal const` | `"text/html; charset=utf-8"` für alle HTML-Antworten |
| `BasePath` | `internal static string` (setzbar) | `PathBase`-Präfix für Reverse-Proxy-Betrieb; einmalig in `Program.cs` Z. 137 gesetzt; Präfix aller Links/Actions/Redirects |
| `NavCss` | `internal const string` | Navigations-CSS (fest verdrahtete Hex-Farben); wird auch von `StatusPageComposer` eingebettet |
| `Css` | `private const string` | Basis-CSS aller `PageShell`-Seiten (Light-Palette, Hex-Werte) — Details in [css-theming.md](css-theming.md) |
| `BeginPage(StringBuilder sb, string title)` | `public static` | Baut `<!doctype html><html lang="de"><head>…` inkl. `<meta charset>`, `viewport`, `<title>` und eingebettetem `Css` + `NavCss`; öffnet `<body><div class="page">`. **Kein Theme-Parameter.** |
| `AppendHeader(StringBuilder sb, string title)` | `public static` | Dunkler Titel-Header `<header class="top">` |
| `AppendNav(StringBuilder sb, PageUserContext user)` | `public static` | Navigationszeile (Status, Profil, Admin-Links nur bei `IsAdmin`, Benutzername, Abmelden-POST-Formular mit Antiforgery-Token) |
| `AppendMessage(StringBuilder sb, string? message, bool isError)` | `public static` | Status-/Fehlerbox (`msg`/`msg err`) |
| `AppendHiddenToken(StringBuilder sb, string fieldName, string token)` | `public static` | Antiforgery-Hidden-Feld |
| `EndPage(StringBuilder sb)` | `public static` | Footer + schließende Tags |
| `E(string? value)` | `public static` | `WebUtility.HtmlEncode` |

Aufgerufen von: `AuthPagesComposer` (alle Render-Methoden), `AdminPagesComposer` (alle Render-Methoden), `StatusPageComposer` (nur `NavCss`, `AppendNav`, `BasePath`, `HtmlContentType` — **nicht** `BeginPage`), `EndpointSecurityExtensions` (`HtmlContentType`), `AuthEndpoints`/`AdminEndpoints`/`Program.cs` (`BasePath`, `HtmlContentType`).

## `StatusPageComposer`

Datei: `server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs` — `public sealed class`, als Singleton registriert (`Program.cs` Z. 29), Ctor-Parameter `TimeProvider`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Render(IReadOnlyList<InstanceSnapshotView>, IReadOnlyList<GitHubRepositoryStatus>, int refreshSeconds, PageUserContext user)` | `public` | Komplette Status-Seite. Baut sein `<html>`-Tag **selbst** (Z. 39–44, nicht via `PageShell.BeginPage`), inkl. `<meta http-equiv="refresh">`. Bettet `Css` + `PageShell.NavCss` ein. `PageUserContext` wird nur für `PageShell.AppendNav` genutzt. |
| `RenderHeader` | `private` | Header mit Status-Badge (`online`/`offline`/`stale`) und Instanz-Tags; enthält Inline-Style `style="color:#cbd5e1"` (Z. 90) |
| `RenderInstance` | `private` | Drei `<section>`s je Instanz (Laufende Aufgaben, Wartet auf Anwender, Letzte SCM-Ereignisse) + `stale-note`-Hinweise + `dim`-Klasse |
| `IsWaiting` | `private static` | Hilfsfunktion „wartend" (RunStatus `WaitingForInput` oder `PausedUntilUtc` in Zukunft) |
| `RenderWorkItemRow` | `private` | Tabellenzeile je WorkItem (Issue-Link, Titel, Run-Pill, Details, PR-/Issue-Links) |
| `RunPill` | `private static` | Status-Pill (`run`/`wait`/`pause`) |
| `RenderScmEventRow`, `ScmStatusPill` | `private static` | SCM-Ereignis-Zeilen und -Pills (`done`/`err`/`wait`/`pause`) |
| `RenderGitHubRepository` | `private` | GitHub-Repo-Sektion (PRs, Releases); enthält Inline-Style `style="margin-top:12px"` (Z. 288) |
| `AppendLinked`, `IsSafeHttpUrl`, `ShortId`, `FmtTime`, `E` | `private static` | Link-Rendern mit http(s)-Whitelist, Id-Kürzung, Zeitformatierung, HTML-Encoding |
| `Css` | `private const` | Eigenes CSS für die Status-Seite (Light-Palette, Hex-Werte + Media-Query `max-width: 600px`) — Details in [css-theming.md](css-theming.md) |

Aufgerufen von: `Program.cs` GET-Handler `/` (Z. 153–169).

## `AuthPagesComposer`

Datei: `server/Softwareschmiede.StatusServer/Pages/AuthPagesComposer.cs` — `public static class`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RenderRegister(string antiforgeryFieldName, string antiforgeryToken, string? error = null)` | `public static` | Erst-Registrierungsformular (anonym, kein `PageUserContext`) |
| `RenderLogin(string antiforgeryFieldName, string antiforgeryToken, string? error = null)` | `public static` | Anmeldeformular (anonym) |
| `RenderProfile(PageUserContext user, string? message = null, bool isError = false)` | `public static` | Profilseite: Konto-Sektion (Name/Stufe) + „Passwort ändern"-Formular (`action="/profile/password"`). **Kein Theme-/Anzeige-Abschnitt.** |
| `RenderFormExpired()` | `public static` | Fehlerseite für Antiforgery-Validierungsfehler (anonym) |
| `RenderAccessDenied(PageUserContext? user = null)` | `public static` | 403-Seite; Nav nur wenn `user` übergeben |

Alle Methoden nutzen `PageShell.BeginPage` → gleiches Dokument-Skelett. Formular-Stile: `PageShell.Css` enthält bereits `form select`-Selektor (Z. 51–53 von `PageShell.cs`).

Aufgerufen von: `AuthEndpoints` (alle Seiten), `EndpointSecurityExtensions.RequireAntiforgery` (`RenderFormExpired`).

## `AdminPagesComposer`

Datei: `server/Softwareschmiede.StatusServer/Pages/AdminPagesComposer.cs` — `public static class`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RenderUserList(PageUserContext, IReadOnlyList<UserAdminListItem>, string? message, bool isError)` | `public static` | Benutzertabelle + Anlege-Formular (enthält `form select` für Stufe) |
| `RenderTokenList(PageUserContext, IReadOnlyList<RelayToken>, string? message, bool isError, string? createdToken)` | `public static` | Token-Verwaltung + `token-once`-Block |
| `RenderUserDeleteConfirm(PageUserContext, string targetUserId, string targetUserName)` | `public static` | JS-freie Lösch-Bestätigungsseite |
| `RenderTokenRevokeConfirm(PageUserContext, RelayToken)` | `public static` | JS-freie Widerruf-Bestätigungsseite |
| `RenderSettings(PageUserContext, int sessionMinutes, StatusServerUpdateSettings, StatusServerUpdateStatus, string? message, bool isError)` | `public static` | Sitzungsdauer-Formular + Update-Sektion |
| `RenderUpdateInstallStarted(PageUserContext, string updateLogPath)` | `public static` | Statische Ergebnisseite nach Installationsanlauf (kein PRG — Server stoppt); `PageUserContext` steht beim Aufruf in `AdminEndpoints.PostUpdateInstall` zur Verfügung (Z. 386–391) |
| `AppendOption`, `UpdateStateText`, `RenderUserRow`, `AppendRoleAction`, `AppendActivationAction`, `AppendUnlockAction`, `AppendDeleteAction` | `private static` | Hilfsmethoden (u. a. `<option selected>`-Bau via `AppendOption` — Muster für Auswahl-Controls) |

Alle Render-Methoden gehen über `PageShell.BeginPage` + `AppendNav` — sie erhalten den `PageUserContext` bereits und würden eine darin transportierte Theme-Information implizit mitbekommen.

## `EndpointSecurityExtensions`

Datei: `server/Softwareschmiede.StatusServer/Endpoints/EndpointSecurityExtensions.cs` — `internal static class`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `BuildUserContextAsync(HttpContext, UserManager<ApplicationUser>, IAntiforgery)` | `internal static` | Lädt `ApplicationUser` via `GetUserAsync`, Rollen via `GetRolesAsync`, Antiforgery-Token via `GetAndStoreTokens`; baut `PageUserContext` (Z. 28–32). **Das `ApplicationUser`-Objekt liegt hier bereits vor** — eine neue Eigenschaft könnte ohne zusätzlichen DB-Zugriff mitgeliefert werden. |
| `RequireAntiforgery(this RouteHandlerBuilder)` | `public static` | Endpoint-Filter: `ValidateRequestAsync`; bei `AntiforgeryValidationException` → `AuthPagesComposer.RenderFormExpired` mit HTTP 400 |

`BuildUserContextAsync`-Aufrufstellen (alle authentifizierten HTML-Seiten): `Program.cs` Z. 164 (GET `/`), `AuthEndpoints.GetProfile`, `AuthEndpoints.PostProfilePassword`, `AuthEndpoints.GetAccessDenied`, `AdminEndpoints.GetUserDeleteConfirm`, `GetTokenRevokeConfirm`, `PostUpdateInstall`, `RenderUsersAsync`, `RenderTokensAsync`, `RenderSettingsAsync`.

## `AuthEndpoints`

Datei: `server/Softwareschmiede.StatusServer/Endpoints/AuthEndpoints.cs` — `public static class`

| Route | Handler | Beschreibung |
|-------|---------|--------------|
| `GET /register` | `GetRegister` | Setup-Formular, `AllowAnonymous`; Redirect `/` sobald Benutzer existieren |
| `POST /register` | `PostRegister` | `AllowAnonymous` + `RequireAntiforgery`; legt ersten Admin an, sign-in |
| `GET /login` | `GetLogin` | `AllowAnonymous`; Redirect `/` wenn bereits angemeldet |
| `POST /login` | `PostLogin` | `AllowAnonymous` + `RequireAntiforgery`; Lockout-/Deaktivierungs-Prüfung |
| `POST /logout` | `PostLogout` | `PageAccessPolicy` + `RequireAntiforgery` |
| `GET /profile` | `GetProfile` | `PageAccessPolicy`; `BuildUserContextAsync` → `RenderProfile(user, StatusMessages.Resolve(msg))` |
| `POST /profile/password` | `PostProfilePassword` | `PageAccessPolicy` + `RequireAntiforgery`; bei Erfolg PRG → `/profile?msg=password-changed`; bei Fehler direkte Re-Render mit Meldung (kein Redirect) |
| `GET /access-denied` | `GetAccessDenied` | `AllowAnonymous`; baut `PageUserContext` nur wenn authentifiziert |

**Kein Endpunkt für eine Theme-/Anzeige-Einstellung vorhanden.** `PostProfilePassword` (Z. 170–219) ist das direkte Muster: `userManager.GetUserAsync` → Form lesen → validieren → Mutation (`ChangePasswordAsync`) → Erfolg `Results.Redirect($"{PageShell.BasePath}/profile?msg={StatusMessages.PasswordChanged}")`, Fehler → `RenderProfile` mit `isError: true`.

## `AdminEndpoints`

Datei: `server/Softwareschmiede.StatusServer/Endpoints/AdminEndpoints.cs` — `public static class`

Alle Admin-Routen unter `AdminOnlyPolicy`; POSTs mit `RequireAntiforgery`; PRG über `RedirectWithMessage`/`RedirectWithError` (`?msg=`/`?err=` + `StatusMessages`). Jede gerenderte Seite baut den `PageUserContext` via `BuildUserContextAsync`. Nicht direkt theme-relevant, aber alle Admin-Seiten laufen über `PageShell.BeginPage` — sie erben eine dort verankerte Theme-Ausgabe automatisch.

## `StatusMessages`

Datei: `server/Softwareschmiede.StatusServer/Endpoints/StatusMessages.cs` — `internal static class`

Feste `const string`-Schlüssel (z. B. `PasswordChanged = "password-changed"`, `SettingsSaved = "settings-saved"`), Dictionary `s_texts` (Schlüssel → deutscher Text), `Resolve(string? key)` und `Resolve(string? msg, string? err)` (err hat Vorrang). Unbekannte/leere Schlüssel → `null` (keine Meldungsbox). Ein neuer Schlüssel (z. B. „Anzeigeeinstellung gespeichert") würde hier ergänzt.

## `Program.cs` — GET `/`

Datei: `server/Softwareschmiede.StatusServer/Program.cs`, Z. 153–169.

Handler `GET /` (`PageAccessPolicy`): `GitHubStatusService.GetRepositoryStatusesAsync` → `BuildUserContextAsync` → `composer.Render(cache.GetAll(), repositories, options.Value.Page.RefreshSeconds, user)` → `Results.Content(html, PageShell.HtmlContentType)`. Der `PageUserContext` wird hier bereits gebaut; eine Theme-Information käme also implizit über `user` mit (keine Signaturänderung zwingend nötig, sofern das Theme im Kontext steckt).

## `StatusServerCookieEvents`

Datei: `server/Softwareschmiede.StatusServer/Security/StatusServerCookieEvents.cs`

`ValidatePrincipal` (Z. 63–107): `SecurityStampValidator.ValidatePrincipalAsync` → Deaktivierungs-Prüfung → Rollen-Claim-Abgleich (erneuert Principal bei Stufenänderung) → Sliding-Renewal nach halber Ticket-Laufzeit. Eine neue `ApplicationUser`-Eigenschaft (Theme) berührt keinen dieser Mechanismen: Sie steht nicht im Cookie-Claim, ändert weder Security-Stamp noch Rollen — eine Theme-Änderung würde **keine** Session-Invalidierung auslösen (Fakt zur offenen Frage der Anforderung).

## `UserAdminService`

Datei: `server/Softwareschmiede.StatusServer/Security/UserAdminService.cs` — `public sealed class` (scoped)

Methoden: `RegisterFirstAdminAsync`, `CreateUserAsync`, `SetRoleAsync`, `SetDeactivatedAsync`, `DeleteUserAsync`, `ResetLockoutAsync`, `ListUsersAsync`, `DescribeErrors` (internal static), `IsLastActiveAdminAsync` (private). Ergebnis-Typen: `UserAdminResult` (Record mit `Succeeded`/`Error`/`IsNotFound`/`User`), `UserAdminListItem`. **Kein generisches „Profilfeld aktualisieren"** — für die Theme-Speicherung stünde `userManager.UpdateAsync` direkt zur Verfügung (`PostProfilePassword` nutzt `userManager`/`signInManager` ebenfalls direkt, nicht den `UserAdminService`).
