# Tests und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-28, ca. 11:38–11:52 Uhr CET (UTC+01:00)
- Branch und Commit-ID: `task/issue-500-141c123699d14ffb92a41fd113b92eaa-feat-frischeres-ui-design-fuer`,
  `b9d5673b021776ed5d6a95b473c5b514f9385399`
- Uncommittete Änderungen im getesteten Stand: nur das Feature-Arbeitsverzeichnis
  `docs/features/task-issue-500-…/` (untracked: `input-issue.md`, `requirement.md`,
  `todo.md`, diese Inventory-Dateien). Keine Änderungen an Produktivcode.
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 11.0.100,
  TargetFrameworks `net11.0` / `net11.0-windows10.0.17763.0`; xunit.v3 4.0.3,
  FluentAssertions 8.11.0, `Microsoft.AspNetCore.Mvc.Testing` 11.0.0.
  `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bei allen Läufen gesetzt (Sandbox-Regel).
  `tasklist` zeigte **keine** laufende `Softwareschmiede.App.exe` — kein
  Self-Hosting-/Datei-Lock-Risiko beim Build.
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml`
  und `staging-ci.yml` definieren vier Lanes — (1) reguläre Unit-Tests
  `src/Softwareschmiede.Tests` (`Category!=OsInterface`), (2) reguläre
  Integrationstests `src/Softwareschmiede.IntegrationTests` (`Category!=OsInterface`),
  (3) StatusServer-Tests `server/Softwareschmiede.StatusServer.Tests` (ungefiltert),
  (4) OS-Interface-Lane (`Category=OsInterface`, E2E/ConPTY/Clipboard — in dieser
  Sandbox eingeschränkt lauffähig, siehe Testlücken). Zusätzlich verlangt CLAUDE.md
  vor jedem `dotnet test` einen vollständigen Build.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build StatusServer-Tests | `dotnet build server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj -c Debug` | Repo-Root | 0 | — | — | — | Konsole: „Buildvorgang wurde erfolgreich ausgeführt", 0 Warnungen/Fehler |
| Build Haupt-Tests | `dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug` | Repo-Root | 0 | — | — | — | Konsole: erfolgreich, 0 Warnungen/Fehler (inkl. `Softwareschmiede.App` WPF) |
| StatusServer-Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj -c Debug --logger "trx;LogFileName=inventory-baseline-statusserver.trx"` | Repo-Root | 0 | 165 | 0 | 0 | [TRX](test-results/inventory-baseline-statusserver.trx) |
| Reguläre Unit-Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-baseline-regular-tests.trx"` | Repo-Root | 0 | 2880 | 0 | 3 | [TRX](test-results/inventory-baseline-regular-tests.trx) |
| Reguläre Integrationstests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-baseline-regular-integration.trx"` | Repo-Root | 0 | 437 | 0 | 0 | [TRX](test-results/inventory-baseline-regular-integration.trx) |

Übersprungene Tests im regulären Lauf (Plattform-Abhängigkeit, kein Fehler):
`SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged`,
`SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged`,
`WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException`.

### Nachgewiesene bestehende Testfehler

Keine — alle ausgeführten Suiten waren fehlerfrei (165 + 2880 + 437 bestanden,
0 Fehlschläge).

### Testlücken und Ausführungsprobleme

- **OS-Interface-Lane (`--filter "Category=OsInterface"`) nicht ausgeführt.**
  Umfang: E2E-, ConPTY-, Clipboard- und echte Prozess-Start-Tests in
  `src/Softwareschmiede.Tests` und `src/Softwareschmiede.IntegrationTests`
  (CI: `pr-staging-ci.yml:228,232`). Grund: laut CLAUDE.md umgebungssensitiv —
  ConPTY-Tests schlagen in dieser Agent-Sandbox strukturell fehl (keine
  Pseudo-Console-Isolation), WPF-E2E brauchen eine interaktive Desktop-Session.
  Diese Lane ist für die Status-Webseite ohnehin ohne Befundwert (enthält keine
  StatusServer-Tests; `Softwareschmiede.StatusServer.Tests` hat keine
  `OsInterface`-Kategorie). Ausgangszustand dieser Lane: unbekannt.
- Für den Design-Lauf selbst sind laut Anforderung keine Tests betroffen; die
  unten gelisteten Composer-/Endpunkt-Tests sind erst in der späteren
  Code-Umsetzung relevant — sie prüfen teilweise **konkretes Markup** und können
  bei einer Umgestaltung brechen (siehe „Für das Redesign relevante
  Markup-Assertions").

## Testklassen (StatusServer, `server/Softwareschmiede.StatusServer.Tests`)

### `Pages/AuthPagesComposerTests.cs` — `AuthPagesComposerTests` (12 Tests)

- `RenderRegister_ContainsFormAndToken` / `RenderRegister_ShowsUserNameRules` /
  `RenderRegister_Error_IsEncoded` — Registrierungs-Formular, Felder,
  Zeichensatz-Hinweis + `pattern`, Escaping
- `RenderLogin_ContainsFormAndToken` / `RenderLogin_Error_IsShown` /
  `RenderLogin_Anonymous_RendersAutoTheme` — Login-Formular, Fehlerbox,
  `data-theme="auto"`
- `RenderProfile_ContainsUserAndPasswordForm` — Profil: Name, beide Form-Actions,
  Felder, Logout-Form
- `RenderProfile_ShowsThemeSelectWithCurrentValue` — Theme-Select mit 3 Optionen,
  `selected`-Vormerkung
- `RenderProfile_DarkTheme_SetsDataThemeAttribute` — `data-theme="dark"`
- `RenderProfile_Watcher_ShowsDisplayName` — „Stufe &#220;berwacher" (encodiert)
- `RenderAccessDenied_ContainsReasonAndLink` / `RenderFormExpired_ContainsReasonAndLink`
  — Fehlerseiten mit Grund + `href="/"`

### `Pages/AdminPagesComposerTests.cs` — `AdminPagesComposerTests` (10 Tests)

- `RenderUserList_ShowsRowsActionsAndLastAdminHint` — Zeilen, Pillen („letzter
  Admin", „gesperrt bis … UTC", „deaktiviert", „aktiv"), alle Form-Actions,
  Delete-**Link** (kein Direkt-POST), Antiforgery-Feld, Zeichensatz-Hinweis,
  encodierter Rollenname, `disabled`-Buttons (Regex `disabled[^>]*>…`)
- `RenderUserList_AdminTheme_SetsDataTheme` — `data-theme="dark"`
- `RenderTokenList_ShowsRowsAndOneTimePlaintext` — „nur jetzt"-Hinweis, Klartext,
  Pillen „gültig"/„widerrufen … UTC", Revoke-**Link** statt POST
- `RenderTokenList_ErrorMessage_UsesErrorBox` — `class="msg err"`
- `RenderTokenList_Empty_ShowsEmptyState` — Leer-Text + Anlege-Formular
- `RenderSettings_ShowsCurrentValueAndRange` — session-duration-Action, Wert,
  Grenzen 5/43200
- `RenderSettings_ShowsUpdateSection` — Update-Tabelle, Selects (`prerelease`,
  `install` selected), `installTime`, check/install-Actions, Downtime-Checkbox,
  Protokoll-Pfad, kein lock-reset ohne Stale-Lock
- `RenderSettings_StaleLockAndError_ShowsResetFormAndErrorPill` — lock-reset-Action,
  „verwaist", Fehler-Pill
- `RenderUpdateInstallStarted_ExplainsRestart` — „startet neu", konfigurierbarer
  Log-Pfad (kein Literal)
- `RenderUserList_MessageEncoded_AndAdminNavVisible` — Escaping, Admin-Nav-Links
- `RenderUserDeleteConfirm_ShowsTargetConsequenceAndForm` /
  `RenderTokenRevokeConfirm_ShowsLabelConsequenceAndForm` — Bestätigungsseiten:
  Ziel, „unwiderruflich", POST-Action, Antiforgery, „Abbrechen"

### `Status/StatusPageComposerTests.cs` — `StatusPageComposerTests` (13 Tests)

- `Render_OnlineInstance_ContainsAllSectionsAndNoButtons` — Sektionen, Meta-Refresh
  15, Viewport, `badge online`; **außer Abmelden kein `<button`** (read-only)
- `Render_Navigation_AdminShowsAdminLinksAndLogoutForm` /
  `Render_Navigation_WatcherHidesAdminLinks` — Rollen-Conditional der Nav
- `Render_References_RenderedAsGitHubLinks` — Issue/PR/Release/SCM-Links
- `Render_DisconnectedInstance_ShowsOfflineNote` — „offline seit", `badge offline`
- `Render_NoInstances_ShowsEmptyState` — Leer-Zustand + `badge offline`
- `Render_GitHubError_ShowsErrorWithTimestamp` — Fehlerhinweis mit Zeit
- `Render_UserContent_IsHtmlEncoded` — XSS-Escaping
- `Render_NullCollections_RenderEmptySections` — defensive null-Listen
- `Render_NonHttpScheme_UrlRenderedAsTextOnly` — URL-Whitelist (kein
  `javascript:`/`file:` als href)
- `Render_AppSections_CarryInstanceTag` — `class="inst"` je Sektion (≥3)
- `Render_Css_AllowsWrappingForMobile` — **kein `white-space: nowrap` außer
  `td.num, td.right`**; Viewport
- `Render_DarkTheme_SetsDataThemeOnHtmlTag` — `data-theme="dark"`
- `Render_Css_DefinesThemeVariablesAndMediaQuery` — **`--bg:`,
  `:root[data-theme="dark"]`, `@media (prefers-color-scheme: dark)`,
  `:root:not([data-theme="light"])`, `color-scheme: light/dark`, keine
  Literal-Hexfarben außerhalb der Variablen** (`NotContain("background: #f4f5f7")`)
- `Render_NoInlineStyles` — **kein `style=`-Attribut**, Klassen `inst on-dark`,
  `count gap-top`

### `Auth/SetupAndAuthTests.cs` — `SetupAndAuthTests` (12 Tests, via `StatusWebApplicationFactory`)

Registrierung (erstes Konto → Admin + SignIn; danach geschlossen; parallele Posts →
genau ein Admin), Zeichensatz-Fehlermeldung, Login (gültig/falsch/Lockout nach 3
Fehlversuchen + Admin-Unlock/deaktiviert), Session-Tod bei Deaktivierung, Logout,
`Post_WithoutAntiforgery_IsRejected` (Antiforgery-Fehlerseite),
Cookie-Renew-Verhalten.

### `Auth/AdminUserTests.cs` — `AdminUserTests` (7 Tests)

User-CRUD-Flow (anlegen/deaktivieren/löschen/unlock), Letzter-Admin-Schutz,
Zeichensatz-Fehler, Stufenwechsel mit Zugriffswirkung, NotFound-Redirects,
`AdminPages_UnknownMessageKeys_RenderNoStatusBox` (unbekannte `?msg=`/`?err=` →
keine `class="msg`), `AdminEndpoints_WatcherRole_IsForbidden` (Überwacher →
`/access-denied`).

### `Auth/AdminTokenTests.cs` — `AdminTokenTests` (1 Test)

Token-Flow: Anlegen → `?created=` → Klartext einmalig sichtbar; Widerruf blockiert
die Relay-Nutzung; zweiter Abruf ohne Klartext.

### `Auth/AdminSettingsTests.cs` — `AdminSettingsTests` (1 Test)

Sitzungsdauer-POST persistiert und wirkt auf die Cookie-Laufzeit.

### `Auth/AdminUpdateSettingsTests.cs` — `AdminUpdateSettingsTests` (8 Tests)

Update-Einstellungen (persistieren + Runtime-Optionen), Validierungsfehler,
Rollen-/Antiforgery-Pflicht, Update-Check-Redirects, Install ohne Downtime-Checkbox
→ Fehlerseite, kein bereites Paket → `update-install-not-started`, abgebrochener
Install, Lock-Reset ohne Lock → `?err=`.

### `Auth/ProfileTests.cs` — `ProfileTests` (4 Tests)

Passwortänderung (Erfolg, altes Passwort danach ungültig), Theme-POST persistiert +
`data-theme="dark"` auf `/`, `/admin/users`, `/profile`; ungültiger Theme-Wert →
`class="msg err"`; Antiforgery-Pflicht.

### `E2E/StatusServerUserFlowTests.cs` — `StatusServerUserFlowTests` (4 Tests)

`HappyPath_FullSetupToRelayFlow` (Registrierung → Überwacher + Token → SignalR-Relay
→ Seite zeigt Instanz + `badge online`), `RegistrationClosed_AndUnauthenticatedRedirect`
(ungeschützte Umleitungen: `/` → `/login`, `/register` → `/`), Restart-Persistenz
(Benutzer/Token/Settings), `Theme_SurvivesServerRestart`.

### `E2E/StatusServerUpdateFlowTests.cs` — `StatusServerUpdateFlowTests` (3 Tests)

Lokaler Update-Quellen-Flow bis `ReadyToInstall`, Hash-Mismatch → Check-Fehler,
persistierte Update-Settings nach Neustart.

### Übrige Testklassen (für das Design indirekt)

`GitHubStatusServiceTests`, `InstanceSnapshotCacheTests`, `RelayTokenServiceTests`,
`RelayTokenValidatorTests`, `ServerSettingsServiceTests`, `UserAdminServiceTests`,
`StatusRelayRoundtripTests`, Update-*-Tests, `StatusWebApplicationFactoryTests` —
Daten-/Logik-Ebene, keine Markup-Assertions.

## Hilfsmethoden

### `TestSupport/AuthTestClient.cs`

Cookie-erhaltender `HttpClient` für Form-Flows: `RegisterFirstUserAsync`,
`LoginAsync`, `PostFormAsync` (fügt Antiforgery-Token automatisch hinzu),
`RefreshAntiforgeryAsync(path)`, `FollowRedirectAsync`.

### `TestSupport/StatusServerDbSeeder.cs`

`CreateUserAsync(name, pw, role)`, `SeedRelayTokenAsync(label)` (liefert Klartext),
`RevokeRelayTokenAsync`, `IsRelayTokenValidAsync`, `SetSessionDurationAsync` /
`GetSessionDurationAsync`, `GetUserThemeAsync`.

### `TestSupport/StatusServerTestDb.cs`, `TestTimeProvider.cs`,
### `TestSupport/TempUpdateSource.cs`, `TestSupport/UpdateTestFactory.cs`

Temp-SQLite-DB-Helfer, steuerbarer `TimeProvider`, lokale Update-Quelle samt
Factory für Update-E2E.

### `StatusWebApplicationFactory.cs`

`WebApplicationFactory<Program>` mit isolierter Temp-SQLite-DB je Instanz,
`DatabasePathOverride` für Neustart-Tests; Update-Subsystem hermetisch deaktiviert
(`Update:Enabled=false`, eigener WorkingDirectory im Temp-Pfad).

## Für das Redesign relevante Markup-Assertions

Die spätere Umsetzung muss beachten, dass Tests wörtliche HTML-Fragmente prüfen —
bei Layout-Änderung vermutlich betroffen:

- `action="…"`- und `href="…"`-Pfade **aller** Formulare/Links (viele Tests)
- `class="msg"` / `class="msg err"` (Fehlerbox-Erkennung)
- `name="__RequestVerificationToken"`-Feld pro Formular
- `<button`-Verbot auf `/` (read-only-Assertion)
- `disabled`-Regex-Muster auf geschützten Buttons
- `data-theme`-Attribut, Theme-CSS-Selektoren (`:root[data-theme="dark"]`,
  `prefers-color-scheme`-Media-Query, `--bg:`-Token)
- `NotContain("style=")`, `white-space: nowrap`-Einschränkung, `overflow-wrap`-
  Erwartungen (Mobil-Umbruch)
- Klassen-Strings: `badge online|offline`, `inst on-dark`, `count gap-top`,
  `action-link danger`, `pill …`, `token-once` („nur jetzt"-Text)
- Deutsche UI-Texte wortgleich („unwiderruflich", „letzter Admin", „Erlaubte
  Zeichen", „Überwacher" encodiert als `&#220;` …)
