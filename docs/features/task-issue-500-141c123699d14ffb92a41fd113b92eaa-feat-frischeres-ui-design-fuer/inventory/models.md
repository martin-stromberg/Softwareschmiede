# Datenmodelle und Konstanten

## `ApplicationUser`

Datei: `server/Softwareschmiede.StatusServer/Data/ApplicationUser.cs`
(erbaut auf `Microsoft.AspNetCore.Identity.IdentityUser`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `IsDeactivated` | `bool` | Deaktivierte Konten: kein Login, bestehende Sessions sterben je Request (`StatusServerCookieEvents.ValidatePrincipal`). UI: Pill „deaktiviert", Fehlertext „Dieses Konto ist deaktiviert." |
| `Theme` | `ThemePreference` (Default `Auto`) | Pro-Benutzer-Theme (Light/Dark/Auto) → `data-theme`-Attribut; Spalte `AspNetUsers.Theme` (Migration `20261128095408_UserTheme`). |
| geerbt: `UserName`, `Id`, `LockoutEnd`, `AccessFailedCount`, `SecurityStamp`, … | — | Identity-Standard; `LockoutEnd` treibt Pill „gesperrt bis … UTC" und Button „Sperrung zurücksetzen". |

## `RelayToken`

Datei: `server/Softwareschmiede.StatusServer/Data/RelayToken.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel; Routenparameter `{id:long}` der Widerruf-Seite |
| `Label` | `string` | Freie Bezeichnung (`RelayTokenService.MaxLabelLength = 200`) |
| `TokenHash` | `string` | SHA-256-Hash (Base64) — Klartext nie gespeichert |
| `CreatedUtc` | `DateTimeOffset` | Anzeige `dd.MM.yyyy HH:mm` |
| `RevokedUtc` | `DateTimeOffset?` | `null` = gültig (`pill done`), sonst `pill err` „widerrufen {Datum} UTC" |

Klartext-Format: 43 Zeichen Base64url (E2E-Test-Regex `[A-Za-z0-9_-]{43}`); einmalige
Anzeige über `RelayTokenPlaintextStore` (Singleton, `Take(key)` Single-Use,
`?created={guid}` auf `GET /admin/tokens`).

## `UserAdminListItem` (Record)

Datei: `server/Softwareschmiede.StatusServer/Security/UserAdminService.cs:418`

| Eigenschaft | Typ | UI-Bedeutung |
|-------------|-----|--------------|
| `Id` | `string` | Routenparameter der Inline-Aktionen |
| `UserName` | `string` | Zeilenname |
| `Role` | `string` | `StatusRoles.*` → Anzeigename; steuert Stufenwechsel-Button |
| `IsDeactivated` | `bool` | Pill „deaktiviert"; Aktivieren- statt Deaktivieren-Button |
| `LockoutEnd` | `DateTimeOffset?` | Zeitpunkt der Sperr-Pill |
| `IsLockedOut` | `bool` | Pill „gesperrt bis … UTC"; aktiviert „Sperrung zurücksetzen" |
| `IsLastActiveAdmin` | `bool` | Pill „letzter Admin"; deaktiviert Herabstufen/Deaktivieren/Löschen (mit `title`-Tooltip) |

`ListUsersAsync` sortiert alphabetisch nach `NormalizedUserName`.

## `PageUserContext` (Record)

`server/Softwareschmiede.StatusServer/Pages/PageUserContext.cs` — siehe
[pages-shell.md](pages-shell.md): `UserName`, `IsAdmin`, `AntiforgeryToken`,
`AntiforgeryFieldName`, `Theme`.

## Status-Übersicht (Contracts)

Siehe [status-page.md](status-page.md): `InstanceSnapshotView` (Record,
`Status/InstanceSnapshotCache.cs:164`), `StatusSnapshot`,
`StatusSnapshotWorkItem`, `StatusSnapshotPullRequest`, `StatusSnapshotScmEvent`
(`src/Softwareschmiede.StatusContracts/`), `GitHubRepositoryStatus`,
`GitHubPullRequestView`, `GitHubReleaseView`
(`server/Softwareschmiede.StatusServer/GitHub/GitHubStatusService.cs:10-40`).

## Update-Einstellungen/-Status

`server/Softwareschmiede.StatusServer/Update/`:

| Typ | Member | UI-Bedeutung |
|-----|--------|--------------|
| `StatusServerUpdateSettings` (Record) | `Channel`, `Mode`, `ScheduledInstallTime` (`TimeOnly?`) | Select-Vorauswahl Kanal/Modus, `input type=time` |
| `StatusServerUpdateStatus` (Record) | `InstalledVersion?`, `AvailableVersion?`, `State` (`AutoUpdateState` aus `msTools.Updater`), `LastCheckedAt?`, `LastError?`, `IsLocked`, `IsLockStale`, `UpdateLogPath?` | komplette Update-Statustabelle + Lock-Reset-Sichtbarkeit |
| `StatusServerUpdateChannel` (Enum) | `Stable`, `ReleaseCandidate` | Codec-Werte `stable`/`prerelease` |
| `StatusServerAutomationMode` (Enum) | `Off`, `CheckOnly`, `CheckAndInstall` | Codec-Werte `off`/`check`/`install` |
| `StatusServerUpdateSettingsCodec` | `FormatChannel/Mode/InstallTime`, `TryParse*` | Formular-Serialisierung |

## Rollen- und Policy-Konstanten

`Security/StatusRoles.cs`: `Administration = "Administration"`,
`Ueberwacher = "Ueberwacher"`, `All`, `PageAccessPolicy = "PageAccess"`,
`AdminOnlyPolicy = "AdminOnly"`, `DisplayName` (`Ueberwacher` → „Überwacher").

## Regel-/Konstantenklassen

| Klasse | Konstante(n) | Wert / Zweck |
|--------|--------------|--------------|
| `Security/PasswordPolicy.cs` | `MinLength` | 8 — `minlength`-Attribut, Identity-`RequiredLength`, alle Fehlertexte |
| `Security/UserNamePolicy.cs` | `AllowedCharactersDescription`, `AllowedCharactersPattern`, `FieldHint` | „a–z, A–Z, 0–9 sowie -._@+", `pattern="[a-zA-Z0-9._@+-]+"`, Hinweistext (keine Umlaute/Leerzeichen) — Identity-Default |
| `Security/ServerSettingsService.cs` | `SessionDurationKey`, `DefaultSessionDurationMinutes = 480`, `Min/MaxSessionDurationMinutes = 5/43200`, `Update*Key`, `DefaultUpdateChannel = Stable`, `DefaultUpdateMode = Off`, `DefaultUpdateInstallTime = 03:00` | Einstellungsseite Sitzungsdauer + Update-Defaults |
| `Options/StatusServerOptions.cs` | `PathBase`, `App.StaleAfterSeconds = 300`, `GitHub.Repositories/Token/CacheTtlSeconds = 60`, `Page.RefreshSeconds = 15` | Konfiguration (appsettings/Env) — Stale-Schwelle der Instanzen, Meta-Refresh |
| `Security/RelayTokenService.cs` | `MaxLabelLength = 200` | `maxlength` des Label-Felds |
| `Security/UserAdminService.cs` | `SetupCompletedKey = "SetupCompleted"`, `DescribeErrors` (Identity-Fehlercodes → deutsche Texte) | Setup-Guard + Fehlertexte in Formularseiten |

## `ServerSetting` / `StatusServerDbContext`

`Data/ServerSetting.cs`: Key-Value-Tabelle (`Key` PK, `Value`) für Sitzungsdauer,
Update-Einstellungen und Setup-Markierung. `StatusServerDbContext` +
`StatusServerDatabaseInitializer` legen SQLite-DB, Identity-Tabellen und die beiden
Rollen (`Administration`, `Ueberwacher`) an.

## Weiterleitungs-/Session-Logik (für Mockup-Flows relevant)

- `StatusServerCookieEvents.RedirectToLogin` (`Security/StatusServerCookieEvents.cs:113`):
  ohne Benutzerkonto → `/register`, sonst → `LoginPath`.
- `ValidatePrincipal`: deaktivierte Benutzer → Session sofort tot; Rollen-Claim-Abgleich
  (Stufenänderung wirkt ohne Re-Login); Sliding-Refresh mit konfigurierter
  Sitzungsdauer (Default 480 min, halbe Ticketlaufzeit als Schwelle).
- Identity-Lockout: `MaxFailedAccessAttempts = 3`, `DefaultLockoutTimeSpan = 1h`
  (`Program.cs:78-82`).
