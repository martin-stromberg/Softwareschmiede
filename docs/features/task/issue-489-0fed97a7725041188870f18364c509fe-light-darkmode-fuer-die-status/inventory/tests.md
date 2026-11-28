# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 28.11.2026, 10:38–10:40 Uhr (UTC+01:00)
- **Branch und Commit-ID:** `task/issue-489-0fed97a7725041188870f18364c509fe-light-darkmode-fuer-die-status` @ `e0fbceac6a99ec34cb3a26bbb24532a6b80170c0` („Backmerge from main to staging (#507)")
- **Uncommittete Änderungen im getesteten Stand:** nur das neue, getrackt noch nicht hinzugefügte Verzeichnis `docs/features/task/issue-489-*/` (dieser Workflow; `requirement.md`, später `inventory*`). Keine Änderungen an Produktiv- oder Testcode.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, .NET SDK 11.0.100 (weitere installiert: 10.0.404); Ziel-Framework aller Projekte `net11.0`; xunit.v3 4.0.3, FluentAssertions 8.11.0, Microsoft.AspNetCore.Mvc.Testing 11.0.0.
- **Ermittelte Testsuiten und Quellen der Testbefehle:** `.github/workflows/pr-staging-ci.yml` (Spiegel: `staging-ci.yml`) führt drei Testprojekte aus — `src/Softwareschmiede.Tests` (Filter `Category!=OsInterface`, separate Lane `Category=OsInterface`), `src/Softwareschmiede.IntegrationTests` (gleiche Teilung) und `server/Softwareschmiede.StatusServer.Tests` (ungefiltert, kein Category-Filter nötig — enthält keine `OsInterface`-Tests). CLAUDE.md bestätigt die reguläre Lane.

### Build-Nachweis

| Befehl | Arbeitsverzeichnis | Ergebnis |
|--------|--------------------|----------|
| `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | Erfolg, 0 Warnungen, 0 Fehler (~16 s) |
| `dotnet build server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj -c Debug` | Repo-Root | Erfolg, 0 Warnungen, 0 Fehler (~9 s) |

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| StatusServer-Tests (Baseline) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj -c Debug --logger "trx;LogFileName=statusserver-tests-baseline.trx"` | Repo-Root | 0 | 154 | 0 | 0 (keine im TRX/Log berichtet) | [TRX](test-results/statusserver-tests-baseline.trx), [Log](test-results/statusserver-tests-baseline.log) |

Hinweis: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` wurde gesetzt (CLAUDE.md-Vorgabe für automatisierte Läufe aus dieser Sandbox). Die StatusServer-Suite enthält keine ConPTY-Tests — die Variable ist hier wirkungslos, es wurden keine Tests dadurch übersprungen.

### Nachgewiesene bestehende Testfehler

Keine. Der ausgeführte Lauf der StatusServer-Suite endete mit 154/154 erfolgreichen Tests (TRX: `total="154" executed="154" passed="154" failed="0"`).

### Testlücken und Ausführungsprobleme

- **`src/Softwareschmiede.Tests` und `src/Softwareschmiede.IntegrationTests` nicht ausgeführt.** Befehle laut CI: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` bzw. `...IntegrationTests...` analog. Grund: Beide Suiten sind vom serverseitigen Theming-Feature nicht betroffen (keine Referenz auf `Softwareschmiede.StatusServer`/`Pages`/`StatusPageComposer`). Zusätzlich sind Teile der Haupt-Suite in dieser Sandbox umgebungsabhängig (FlaUI-/ConPTY-/Clipboard-E2E laut CLAUDE.md; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` deckt nur die 14 ConPTY-Tests ab) und ein synchroner Vollauf überschreitet das Kommando-Zeitbudget — ein Hintergrundlauf ist per CLAUDE.md untersagt. Der Solution-Build selbst ist nachgewiesen sauber. Aussage über den Zustand dieser Suiten: **unbekannt, nicht ausgeführt**.
- **`Category=OsInterface`-Tests generell nicht ausgeführt** (in dieser Sandbox ohnehin als instabil dokumentiert, CLAUDE.md).

## Testklassen

### Feature-relevant (Composer, Profil, Auth-Flows)

#### `StatusPageComposerTests` — `Status/StatusPageComposerTests.cs` (12 Facts)

- `Render_OnlineInstance_ContainsAllSectionsAndNoButtons` — Sektionen, Meta-Refresh, read-only (nur Abmelde-Button)
- `Render_Navigation_AdminShowsAdminLinksAndLogoutForm` — Admin-Links + Antiforgery-Feld im Logout-Formular
- `Render_Navigation_WatcherHidesAdminLinks` — Überwacher sieht keine Admin-Links
- `Render_References_RenderedAsGitHubLinks` — Issue-/PR-/Release-Links
- `Render_DisconnectedInstance_ShowsOfflineNote` — „offline seit"-Hinweis + `badge offline`
- `Render_NoInstances_ShowsEmptyState` — Leerzustand
- `Render_GitHubError_ShowsErrorWithTimestamp` — Fehleranzeige mit Zeitstempel
- `Render_UserContent_IsHtmlEncoded` — Escaping fremder Inhalte
- `Render_NullCollections_RenderEmptySections` — Null-Listen → leere Sektionen
- `Render_NonHttpScheme_UrlRenderedAsTextOnly` — URL-Scheme-Whitelist
- `Render_AppSections_CarryInstanceTag` — Instanz-Kennung in allen drei Sektionen
- `Render_Css_AllowsWrappingForMobile` — CSS-Assertion: kein `white-space: nowrap` außer `td.num, td.right` (prüft den CSS-Text direkt — eine CSS-Umstellung muss diese Assertion beachten)

Hilfsmethode `UserContext(bool isAdmin)` (Z. 15–16) konstruiert `PageUserContext` direkt.

#### `AuthPagesComposerTests` — `Pages/AuthPagesComposerTests.cs` (9 Facts)

- `RenderRegister_ContainsFormAndToken`, `RenderRegister_ShowsUserNameRules`, `RenderRegister_Error_IsEncoded`
- `RenderLogin_ContainsFormAndToken`, `RenderLogin_Error_IsShown`
- `RenderProfile_ContainsUserAndPasswordForm` — Profilseite mit Passwort-Formular (aktuell der einzige Profil-Abschnitt mit Formular)
- `RenderProfile_Watcher_ShowsDisplayName`
- `RenderAccessDenied_ContainsReasonAndLink`, `RenderFormExpired_ContainsReasonAndLink`

Hilfsmethode `User(bool isAdmin)` (Z. 10–11) konstruiert `PageUserContext` direkt.

#### `AdminPagesComposerTests` — `Pages/AdminPagesComposerTests.cs` (11 Facts)

- `RenderUserList_ShowsRowsActionsAndLastAdminHint`, `RenderUserList_MessageEncoded_AndAdminNavVisible`
- `RenderTokenList_ShowsRowsAndOneTimePlaintext`, `RenderTokenList_ErrorMessage_UsesErrorBox`, `RenderTokenList_Empty_ShowsEmptyState`
- `RenderSettings_ShowsCurrentValueAndRange`, `RenderSettings_ShowsUpdateSection` (enthält Assertion `<option value="prerelease" selected>` — Muster für Auswahl-Control-Tests), `RenderSettings_StaleLockAndError_ShowsResetFormAndErrorPill`
- `RenderUpdateInstallStarted_ExplainsRestart`
- `RenderUserDeleteConfirm_ShowsTargetConsequenceAndForm`, `RenderTokenRevokeConfirm_ShowsLabelConsequenceAndForm`

Hilfsmethode `Admin()` (Z. 13–14) konstruiert `PageUserContext` direkt.

#### `ProfileTests` — `Auth/ProfileTests.cs` (1 Fact)

- `Profile_ChangePassword_Works_AndOldPasswordFails` — HTTP-Flow via `AuthTestClient`: falsches altes Passwort → Fehlermeldung auf Re-Render; korrekte Änderung → `302 /profile?msg=password-changed` → GET zeigt Meldungstext (PRG-Muster-Nachweis für künftige Theme-POST-Tests)

#### `SetupAndAuthTests` — `Auth/SetupAndAuthTests.cs` (12 Facts)

- `Register_WithoutUsers_CreatesFirstAdmin_AndSignsIn`, `Register_WithExistingUsers_IsClosed`, `Register_InvalidUserName_ExplainsAllowedCharacters`, `Register_ParallelPosts_CreateExactlyOneAdmin`
- `Login_ValidCredentials_GrantsAccess`, `Login_WrongPassword_Denied`, `Login_ThreeFailures_LocksUser_AdminUnlockRestores`, `Login_DeactivatedUser_IsRejected`
- `DeactivatedUser_LosesExistingSession`, `Logout_ClearsSession`, `AuthenticatedRequest_YoungTicket_DoesNotRenewCookie`
- `Post_WithoutAntiforgery_IsRejected` — POSTs ohne Token → `RenderFormExpired`-Seite (Antiforgery-Pattern für neue POST-Routen)

#### `AdminUserTests` (7), `AdminTokenTests` (1), `AdminSettingsTests` (1), `AdminUpdateSettingsTests` (8) — `Auth/`

HTTP-Flows der Admin-Endpunkte (PRG, `?msg=`/`?err=` Schlagworte, Policy-/Antiforgery-Prüfungen, `AdminPages_UnknownMessageKeys_RenderNoStatusBox` — Nachweis, dass unbekannte `msg`-Schlüssel keine Box rendern).

#### `StatusServerUserFlowTests` — `E2E/` (3 Facts)

- `HappyPath_FullSetupToRelayFlow`, `RegistrationClosed_AndUnauthenticatedRedirect`, `Restart_SameDatabase_AllStateSurvives` (Persistenz über Factory-Neustart — Muster für „Einstellung überlebt Server-Neustart")

#### `StatusWebApplicationFactoryTests` (1 Fact)

- `DisposeAsync_WithDatabasePathOverride_RemovesOwnedDirectory`

### Nicht feature-relevant (Vollständigkeit)

`InstanceSnapshotCacheTests` (9), `GitHubStatusServiceTests` (5), `StatusRelayRoundtripTests` (7), `RelayTokenServiceTests` (5), `RelayTokenValidatorTests` (4), `ServerSettingsServiceTests` (7), `UserAdminServiceTests` (6), `AutoUpdateApplicationExtensionsTests` (4), `StatusServerAutoUpdateOptionsMapperTests` (4), `StatusServerUpdateServiceTests` (11), `StatusServerUpdateSettingsCodecTests` (5), `StatusServerUpdateSourceFactoryTests` (5), `StatusServerUpdateFlowTests` (3, E2E).

## Hilfsmethoden

### `AuthTestClient` — `TestSupport/AuthTestClient.cs`

- `Create(factory, allowAutoRedirect)` — Cookie-fähiger `HttpClient` aus `WebApplicationFactory`
- `RefreshAntiforgeryAsync(path)` — GET + Extraktion des Antiforgery-Hidden-Felds (Regex auf `*VerificationToken*`)
- `ExtractAntiforgery(html, source)` — Token aus beliebigem HTML
- `PostFormAsync(path, fields, includeAntiforgery)` — `application/x-www-form-urlencoded`-POST
- `FollowRedirectAsync(response)` — GET auf `Location` (PRG-Verifikation)
- `RegisterFirstUserAsync`, `LoginAsync`, `LogoutAsync` — komplette Auth-Flows

### `StatusServerDbSeeder` — `TestSupport/StatusServerDbSeeder.cs`

Scope-basierter DB-Zugriff: `CreateUserAsync`, `RegisterFirstAdminAsync`, `DeleteRoleAsync`/`EnsureRoleAsync`, `AddServerSettingAsync`, `SeedRelayTokenAsync`, `SetDeactivatedAsync`, `DeleteUserAsync`, `MutateWithConcurrentChangeAsync` (Concurrency-Konflikt-Simulation), `CountRelayTokensAsync`/`CountUsersAsync`, `GetLockoutEndAsync`, `GetUserIdAsync`, `GetRolesAsync`, `SetSessionDurationAsync`/`GetSessionDurationAsync`, `GetUpdateSettingsAsync`, `GetRelayTokenAsync`, `RevokeRelayTokenAsync`, `IsRelayTokenValidAsync`. Für Theme-Persistenz-Assertions: `GetUserIdAsync` + direkter `db.Users`-Zugriff (Muster: `BumpConcurrencyStampAsync`).

### `StatusServerTestDb` — `TestSupport/StatusServerTestDb.cs`

In-Memory-SQLite-`StatusServerDbContext` (`EnsureCreated`, **nicht** `Migrate` — Migrations-Spalten werden über das Modell abgedeckt).

### `StatusWebApplicationFactory` — `StatusWebApplicationFactory.cs`

`WebApplicationFactory<Program>` mit isolierter SQLite-Datei pro Instanz (`Data:DatabasePath`), `DatabasePathOverride` für Neustart-Persistenz-Tests, Update-Subsystem hermetisch deaktiviert.

### `TestTimeProvider` — `TestSupport/TestTimeProvider.cs`

Deterministische `TimeProvider`-Implementierung (`Now`, `Advance`); wird dem `StatusPageComposer` direkt injiziert.

### `UpdateTestFactory` / `TempUpdateSource` — `TestSupport/`

Update-spezifische Factory (feste `InstalledVersion`, lokale Paketquelle) — nicht theme-relevant.
