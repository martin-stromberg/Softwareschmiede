# Tasks: Benutzeranmeldung in Webseite (StatusServer)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Pakete | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (11.0.0) im StatusServer-csproj referenzieren | Offen | — |
| 2 | Pakete | `Microsoft.EntityFrameworkCore.Sqlite` (11.0.0) im StatusServer-csproj referenzieren | Offen | — |
| 3 | Pakete | `Microsoft.EntityFrameworkCore.Design` (11.0.0, `PrivateAssets=all`) im StatusServer-csproj referenzieren | Offen | — |
| 4 | Datenmodell | `Data/ApplicationUser` (`IdentityUser` + `IsDeactivated`) anlegen | Offen | — |
| 5 | Datenmodell | `Data/RelayToken`-Entity anlegen (`Id`, `Label`, `TokenHash`+Unique-Index, `CreatedUtc`, `RevokedUtc`) | Offen | — |
| 6 | Datenmodell | `Data/ServerSetting`-Entity anlegen (`Key` PK, `Value`) | Offen | — |
| 7 | Datenmodell | `Data/StatusServerDbContext` (`IdentityDbContext<ApplicationUser, IdentityRole, string>`) mit Fluent-Konfiguration + Unix-ms-`DateTimeOffset`-Konvertern anlegen | Offen | — |
| 8 | Datenmodell | `Data/StatusServerDbContextDesignTimeFactory` (`IDesignTimeDbContextFactory`) anlegen | Offen | — |
| 9 | Datenmodell | `Data/StatusServerDatabaseInitializer` anlegen (`MigrateAsync` + idempotentes Rollen-Seeding `Administration`/`Ueberwacher`) | Offen | — |
| 10 | Migration | Migration `InitialCreate` erzeugen (`dotnet ef migrations add`) und unter `Migrations/` einchecken | Offen | — |
| 11 | Logik | `Security/StatusRoles` (Rollen- + Policy-Konstanten) anlegen | Offen | — |
| 12 | Logik | `Security/ServerSettingsService` anlegen (`GetSessionDurationAsync` Default 480 min, `SetSessionDurationAsync` mit Bereich 5–43200) | Offen | — |
| 13 | Logik | `Security/RelayTokenService` anlegen (`CreateAsync` → Klartext einmalig + SHA-256-Hash persistiert, `RevokeAsync` → `RevokedUtc`, `ListAsync`) | Offen | — |
| 14 | Logik | `Security/RelayTokenValidator` auf Scoped-DB-Prüfung umbauen (`IsValidAsync`, Hash-Lookup + `SecretComparer` + `RevokedUtc`-Check) | Offen | — |
| 15 | Logik | `Security/UserAdminService` anlegen (`RegisterFirstAdminAsync`, `CreateUserAsync`, `SetRoleAsync` inkl. `UpdateSecurityStampAsync`, `SetDeactivatedAsync`, `DeleteUserAsync`, `ResetLockoutAsync`, `ListUsersAsync`, Letzter-Admin-Schutz für Deaktivieren/Löschen/Herabstufen — eigenes und fremdes Konto) | Offen | — |
| 16 | Logik | `Security/StatusServerCookieEvents` anlegen (`OnSigningIn` ExpiresUtc aus Settings, `OnValidatePrincipal` Deaktivierungs-Prüfung + Rollen-Claim-Abgleich mit `ReplacePrincipal`/`ShouldRenew` + Sliding-Refresh, `OnRedirectToLogin` Setup-aware) | Offen | — |
| 17 | UI | `Pages/PageShell` (gemeinsamer HTML-Rahmen + Navigation inkl. Abmelden-Formular, Admin-Links rollenbedingt) anlegen | Offen | — |
| 18 | UI | `Pages/PageUserContext`-Record anlegen | Offen | — |
| 19 | UI | `Pages/AuthPagesComposer` anlegen (`RenderRegister`, `RenderLogin`, `RenderProfile`, `RenderAccessDenied` im `StatusPageComposer`-Stil) | Offen | — |
| 20 | UI | `Pages/AdminPagesComposer` anlegen (`RenderUserList` mit Anlege-Formular + Stufen-Änderungs-Control je Benutzer + Status-/Lockout-Anzeige, `RenderTokenList` mit Einmal-Klartext-Block, `RenderSettings`) | Offen | — |
| 21 | UI | `StatusPageComposer.Render` um `PageUserContext`-Parameter erweitern (Navigationszeile/Abmelden) | Offen | — |
| 22 | Endpunkte | `Endpoints/EndpointSecurityExtensions` (Antiforgery-Endpoint-Filter für alle POST-Routen) anlegen | Offen | — |
| 23 | Endpunkte | `Endpoints/AuthEndpoints` anlegen (`/register` GET+POST mit Setup-Guard, `/login` GET+POST mit Lockout/Deaktivierungs-Handling, `POST /logout`, `/profile`, `POST /profile/password`, `/access-denied`) | Offen | — |
| 24 | Endpunkte | `Endpoints/AdminEndpoints` anlegen (`/admin/users` GET+POST, `…/{id}/role` POST (Stufenänderung), `…/{id}/deactivate`, `…/activate`, `…/delete`, `…/unlock`; `/admin/tokens` GET+POST, `…/{id}/revoke`; `/admin/settings` GET, `…/session-duration` POST — jeweils Policy `AdminOnly`) | Offen | — |
| 25 | Composition | `Program.cs`: `AddDbContext` mit `Data:DatabasePath`-Default `data/statusserver.db` + `Directory.CreateDirectory` ergänzen | Offen | — |
| 26 | Composition | `Program.cs`: `AddIdentity` + `IdentityOptions` (Passwort ≥ 8, Lockout 3/1 h), `AddEntityFrameworkStores`, `AddAntiforgery`, `AddAuthorization` (Policies `PageAccess`/`AdminOnly`), `ConfigureApplicationCookie` (`/login`, `/access-denied`, `SameAsRequest`, `Lax`, `StatusServerCookieEvents`) registrieren | Offen | — |
| 27 | Composition | `Program.cs`: `Configure<ForwardedHeadersOptions>` (`ForwardedHeaders.All`, KnownNetworks/KnownProxies leeren) ergänzen | Offen | — |
| 28 | Composition | `Program.cs`: Pipeline umbauen — `UseMiddleware<PageAccessMiddleware>` entfernen, `UseForwardedHeaders`/`UseAuthentication`/`UseAuthorization`/`UseAntiforgery` einfügen; `RequireAuthorization("PageAccess")` auf `/` und `/status.json`; `MapAuthEndpoints`/`MapAdminEndpoints` aufrufen; `StatusServerDatabaseInitializer.InitializeAsync` vor `app.Run`; `/`-Handler um `PageUserContext` erweitern | Offen | — |
| 29 | Composition | `PageAccessMiddleware.cs` löschen | Offen | — |
| 30 | Composition | `StatusRelayHub.OnConnectedAsync` auf `await IsValidAsync` umstellen | Offen | — |
| 31 | Konfiguration | `StatusServerOptions`: `Relay`-Property + `RelayOptions`-Klasse entfernen; `PageOptions.Username`/`Password` entfernen | Offen | — |
| 32 | Konfiguration | `appsettings.json`: `Relay`-Sektion und `Page:Username`/`Password` entfernen | Offen | — |
| 33 | Konfiguration | `appsettings.Development.json`: `Relay:DevToken` entfernen (Datei auf leeren Rest reduzieren) | Offen | — |
| 34 | Doku | `server/README.md` überarbeiten (Login statt Basic-Auth, Setup-Lauf, Benutzer- inkl. Stufenverwaltung über Admin-UI, Token über Admin-UI, `data`-Verzeichnis + Recovery, entfernte Config-Keys, `X-Forwarded-Proto`-Hinweis, systemd-Env bereinigen) | Offen | — |
| 35 | Doku | `README.md` (Z. 220–221): „Basic-Auth" → Cookie-Anmeldung mit Stufen | Offen | — |
| 36 | Doku | `docs/RELEASE_NOTES.md`: StatusServer-Auth-Eintrag auf neuen Stand bringen | Offen | — |
| 37 | Doku | `docs/help/einstellungen/remote-uebersicht.md`: Token-Bezug über StatusServer-Admin-UI dokumentieren | Offen | — |
| 38 | Tests | `StatusWebApplicationFactory` umbauen (Config-Seeding entfernen, `Data:DatabasePath`-Temp-Isolation, Cleanup via `IAsyncDisposable`) | Offen | — |
| 39 | Tests | `TestSupport/AuthTestClient` anlegen (Cookie-fähiger Client, Antiforgery-Extraktion, `PostFormAsync`, `RegisterFirstUserAsync`, `LoginAsync`, `LogoutAsync`) | Offen | — |
| 40 | Tests | `TestSupport/StatusServerDbSeeder` anlegen (User/Token seeden, `SetDeactivatedAsync`, `GetLockoutEndAsync`, `SetSessionDurationAsync`) | Offen | — |
| 41 | Tests | `PageAccessTests` entfernen und durch `Auth/SetupAndAuthTests` ersetzen (Setup, geschlossene Registrierung, Login/Logout, Lockout inkl. `LockoutEnd ≈ now + 1 h`-Assertion, Deaktivierung) | Offen | — |
| 42 | Tests | `Auth/AdminUserTests` anlegen (Anlegen/Stufenänderung `…/{id}/role`/Deaktivieren/Löschen/Unlock, Letzter-Admin-Schutz inkl. Herabstufung eigenes+fremdes Konto, `Ueberwacher`-403) | Offen | — |
| 43 | Tests | `Auth/ProfileTests` anlegen (Passwortänderung, falsches altes Passwort) | Offen | — |
| 44 | Tests | `Auth/AdminTokenTests` anlegen (Einmal-Anzeige, Hash-in-DB, Widerruf) | Offen | — |
| 45 | Tests | `Auth/AdminSettingsTests` anlegen (Sitzungsdauer setzen + Cookie-Expires-Auswirkung) | Offen | — |
| 46 | Tests | `Security/RelayTokenValidatorTests` auf DB-Kontext umbauen (inkl. Widerruf-Fall, Dev-Token-Theorien entfernen) | Offen | — |
| 47 | Tests | `Security/RelayTokenServiceTests` anlegen (Hash-Speicherung, Label-Validierung, `RevokedUtc`) | Offen | — |
| 48 | Tests | `Security/ServerSettingsServiceTests` anlegen (Default, Persistenz, Bereich) | Offen | — |
| 49 | Tests | `Relay/StatusRelayRoundtripTests` anpassen (DB-Token-Seeding, authentifizierter Client für `/` und `/status.json`) | Offen | — |
| 50 | Tests | `Status/StatusPageComposerTests` an neue `Render`-Signatur anpassen + Nav-/Logout-Assertions | Offen | — |
| 51 | Tests | `Pages/AuthPagesComposerTests` anlegen (Felder, Antiforgery-Hidden, Fehlermeldungen, Encoding) | Offen | — |
| 52 | Tests | `Pages/AdminPagesComposerTests` anlegen (Tabellen, Stufen-Änderungs-Control, Letzter-Admin-Buttons inkl. deaktivierter Herabstufung, Einmal-Token-Block, rollenbedingte Links) | Offen | — |
| 53 | Tests | `Persistence/StartupPersistenceTests` anlegen (zweite Factory auf gleicher DB → Daten überleben Neustart) | Offen | — |
| 54 | E2E-Tests | `E2E/StatusServerUserFlowTests.HappyPath_FullSetupToRelayFlow` (Registrierung → Admin → Überwacher → Token → `/relay`-Connect → Status sichtbar) | Offen | — |
| 55 | E2E-Tests | `…RegistrationClosed_AndUnauthenticatedRedirect` | Offen | — |
| 56 | E2E-Tests | `…Lockout_AndAdminUnlock` | Offen | — |
| 57 | E2E-Tests | `…DeactivatedUser_SessionRejected` | Offen | — |
| 58 | E2E-Tests | `…TokenLifecycle_OneTimeDisplayAndRevocation` | Offen | — |
| 59 | E2E-Tests | `…Restart_SameDatabase_AllStateSurvives` | Offen | — |
| 60 | E2E-Tests | `…WatcherRole_AdminAccessDenied` | Offen | — |
| 61 | E2E-Tests | `…SessionDuration_ChangeAppliedToCookie` | Offen | — |
| 62 | E2E-Tests | `…RoleChange_TakesEffectOnExistingSession` (Admin→`Ueberwacher`-Herabstufung wirkt auf bestehende Session ohne Re-Login; `Ueberwacher`→`Administration` ebenso; letzter Admin geschützt) | Offen | — |
| 63 | Tests | `Auth/SetupAndAuthTests.Post_WithoutAntiforgery_IsRejected` anlegen (CSRF-Negativtest: POST auf Formularroute ohne Antiforgery-Token → 400, mit ungültigem Token → 400, Gegenprobe mit gültigem Token → kein 400) | Offen | — |
| 64 | Verifikation | Vollständiger Build (`dotnet build Softwareschmiede.slnx -c Debug`), StatusServer-Testlauf, `dotnet format Softwareschmiede.slnx --verify-no-changes` | Offen | — |
