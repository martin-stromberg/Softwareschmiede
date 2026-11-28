# Datenmodell

## `ApplicationUser`

Datei: `server/Softwareschmiede.StatusServer/Data/ApplicationUser.cs`

`sealed class ApplicationUser : IdentityUser` (ASP.NET Core Identity). Tabelle `AspNetUsers`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `IsDeactivated` | `bool` | Deaktivierte Konten: kein Login, bestehende Sessions sterben sofort (Prüfung in `StatusServerCookieEvents.ValidatePrincipal`) — einziges feature-eigenes Feld neben den geerbten `IdentityUser`-Eigenschaften |
| (geerbt) `Id`, `UserName`, `NormalizedUserName`, `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `LockoutEnd`, `LockoutEnabled`, `AccessFailedCount`, `Email*`, `PhoneNumber*`, `TwoFactorEnabled` | diverse | Identity-Standard; `LockoutEnd` wird per `ValueConverter` als Unix-ms (`long`) persistiert (`StatusServerDbContext.OnModelCreating`) |

Eine Theme-Präferenz existiert **nicht**. `IsDeactivated` ist das direkte Muster für ein neues Feld (Spalte in `AspNetUsers`, kein Converter, kein eigenes DbSet).

## `PageUserContext`

Datei: `server/Softwareschmiede.StatusServer/Pages/PageUserContext.cs`

`public sealed record PageUserContext` — Positional-Record mit vier Members, keine weiteren Member/Methoden:

| Member | Typ | Beschreibung / Zweck |
|--------|-----|----------------------|
| `UserName` | `string?` | Benutzername des angemeldeten Benutzers (Anzeige in der Navigationszeile) |
| `IsAdmin` | `bool` | Ob die Stufe `Administration` vorliegt (Admin-Links in der Nav sichtbar) |
| `AntiforgeryToken` | `string` | Antiforgery-Request-Token für Hidden-Formularfelder |
| `AntiforgeryFieldName` | `string` | Feldname des Antiforgery-Hidden-Felds |

Erzeugt ausschließlich in `EndpointSecurityExtensions.BuildUserContextAsync` (`Endpoints/EndpointSecurityExtensions.cs`, Z. 28–32). Konsumiert von `PageShell.AppendNav`, `StatusPageComposer.Render`, `AuthPagesComposer.RenderProfile`/`RenderAccessDenied` und allen `AdminPagesComposer.Render*`-Methoden. In Tests wird der Record in drei Hilfsmethoden direkt konstruiert: `StatusPageComposerTests.UserContext`, `AuthPagesComposerTests.User`, `AdminPagesComposerTests.Admin` — eine Signaturerweiterung betrifft alle Aufrufstellen.

## `StatusServerDbContext`

Datei: `server/Softwareschmiede.StatusServer/Data/StatusServerDbContext.cs`

`sealed class StatusServerDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>` (SQLite).

| Member | Typ | Beschreibung / Zweck |
|--------|-----|----------------------|
| `RelayTokens` | `DbSet<RelayToken>` | Relay-Zugangstokens (nur Hash) |
| `ServerSettings` | `DbSet<ServerSetting>` | Admin-konfigurierbare Schlüssel-Wert-Einstellungen |
| `OnModelCreating` | `override` | `ApplicationUser`: `LockoutEnd` → Unix-ms-Converter; `RelayToken`: Key/MaxLength/Unique-Index auf `TokenHash`, `CreatedUtc`/`RevokedUtc` → Unix-ms; `ServerSetting`: Key `Key` (max 100), `Value` (max 500) |

Für `ApplicationUser` ist nur der `LockoutEnd`-Converter konfiguriert — ein neues Feld bräuchte keine Modellkonfiguration (Konvention), sofern kein Enum-zu-String-Converter gewünscht ist. Registriert in `Program.cs` Z. 72–73 via `AddDbContext` + `UseSqlite("Data Source={databasePath}")`; `databasePath` kommt aus `Data:DatabasePath` oder `data/statusserver.db` unter dem Content-Root (Z. 47–48).

## `ServerSetting`

Datei: `server/Softwareschmiede.StatusServer/Data/ServerSetting.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Key` | `string` (PK, max 100) | Einstellungs-Schlüssel |
| `Value` | `string` (max 500) | Einstellungs-Wert |

Bekannte Schlüssel (`ServerSettingsService`): `SessionDurationMinutes` (Default 480, Min 5, Max 43200), `SetupCompleted` (in `UserAdminService.SetupCompletedKey`), `UpdateChannel`, `UpdateAutomationMode`, `UpdateInstallTime`. **Kein Benutzerbezug** — für eine pro-Benutzer-Theme-Einstellung laut Anforderung ungeeignet.

## `RelayToken`

Datei: `server/Softwareschmiede.StatusServer/Data/RelayToken.cs`

Eigenschaften: `Id` (`long`, autoincrement), `Label` (max 200), `TokenHash` (max 100, unique), `CreatedUtc`, `RevokedUtc`. Nicht theme-relevant; nur der Vollständigkeit halber als Teil des Schemas genannt.

## Migrationen

Verzeichnis: `server/Softwareschmiede.StatusServer/Migrations/`

- Genau eine Migration: `20261127095016_InitialCreate` (`_20261127095016_InitialCreate`) — legt das gesamte Schema an (Identity-Tabellen, `AspNetUsers` inkl. `IsDeactivated` Z. 32, `RelayTokens`, `ServerSettings`).
- `StatusServerDbContextModelSnapshot.cs` — aktueller Modellstand.
- `Data/StatusServerDbContextDesignTimeFactory.cs` — `IDesignTimeDbContextFactory<StatusServerDbContext>` mit SQLite `Data Source=statusserver-design.db`; Voraussetzung für `dotnet ef migrations`.
- `Data/StatusServerDatabaseInitializer.cs` — `InitializeAsync` ruft `db.Database.MigrateAsync()` beim Start auf (`Program.cs` Z. 124–125) und seedet die Rollen `Administration`/`Ueberwacher`; eine neue Migration wird dadurch beim nächsten Server-Start automatisch angewendet.
