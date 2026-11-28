# Umsetzungsplan: Light-/Darkmode für die Status-Webseite (Issue #489)

## Übersicht

Die server-gerenderte Web-UI des `Softwareschmiede.StatusServer` erhält ein pro Benutzer persistiertes Light-/Dark-Theming (Automatisch/Light/Dark) ohne JavaScript. Dazu werden eine neue `ThemePreference`-Eigenschaft auf `ApplicationUser` inklusive EF-Core-Migration eingeführt, der `PageUserContext` um die wirksame Einstellung erweitert, beide HTML-Dokument-Pfade (`PageShell.BeginPage`, `StatusPageComposer.Render`) um ein `data-theme`-Attribut ergänzt und die drei eingebetteten CSS-Blöcke auf CSS Custom Properties mit `prefers-color-scheme`-Fallback umgestellt. Auf der Profilseite entsteht ein Auswahl-Formular mit neuem POST-Endpunkt `POST /profile/theme` (Antiforgery + PRG).

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein server-gerenderter Seitenaufruf der Status-Webanwendung (jedes HTML-Dokument über `PageShell.BeginPage` bzw. `StatusPageComposer.Render`) sowie eine Theme-Einstellung pro `ApplicationUser`-Datensatz |
| Auslösende Ereignisse | GET-Request auf HTML-Seiten (`/`, `/profile`, `/admin/*`, `/login`, `/register`, `/access-denied`, Formular-abgelaufen-Seite) inkl. Meta-Refresh der Status-Seite; POST der Theme-Auswahl auf `/profile` |
| Erwartete Wirkung | Angemeldete Benutzer sehen alle Seiten im gewählten Schema; „Automatisch" folgt `prefers-color-scheme` des Betrachters (Fallback Light); anonyme Seiten rendern immer im Modus „Automatisch"; die Auswahl ist persistent über Sitzungen und Server-Neustarts hinweg und gilt ab dem nächsten Seitenaufruf |
| Umgang mit fehlenden Objekten | Bestehende `ApplicationUser`-Datensätze ohne Theme-Spalte → per EF-Core-Migration neue Spalte mit Default `Auto` (erzeugen); `PageUserContext` ohne Theme → um wirksame Einstellung erweitern (aus `ApplicationUser` übernehmen, kein Mehr-DB-Zugriff); Seiten ohne Benutzerkontext → kein Objekt erzeugen, sie rendern im „Automatisch"-Modus |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein Live-Umschalten ohne Reload und kein JavaScript — die Einstellung wirkt erst ab nächstem Seitenaufruf/Meta-Refresh | Direkt aus der Anforderung abgeleitet: Anforderung verlangt explizit JS-freie, server-seitige Auslieferung des Themes | bestätigt (2026-11-28) |
| 2 | `/status.json` (JSON-API) und `/relay` (SignalR-Hub) bleiben unverändert | Direkt aus der Anforderung abgeleitet: kein HTML, kein Farbschema anwendbar | bestätigt (2026-11-28) |
| 3 | Kein serverweites bzw. Admin-konfigurierbares Standard-Schema — Vorgabe ist immer „Automatisch" | Direkt aus der Anforderung abgeleitet: nicht gefordert; `ServerSettings` hat keinen Benutzerbezug | bestätigt (2026-11-28) |
| 4 | Kein Theme-Umschalter auf öffentlichen/anonymen Seiten | Direkt aus der Anforderung abgeleitet: anonyme Besucher haben kein Konto, dem die Einstellung zugeordnet werden könnte | bestätigt (2026-11-28) |
| 5 | Keine per-Instanz- oder per-Sektion-Abweichung — das Schema gilt einheitlich für die gesamte Web-UI | Direkt aus der Anforderung abgeleitet: Anforderung sieht ein einheitliches Schema vor | bestätigt (2026-11-28) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Enum-Name und Persistenz | `ThemePreference` (`Auto = 0`, `Light`, `Dark`) im `Data`-Namespace; EF persistiert als `INTEGER` (Ordinal), kein `EnumToStringConverter` | Folgt dem `IsDeactivated`-Muster (schlichte Spalte, keine Modellkonfiguration); `Auto = 0` ist CLR-Default und damit automatisch Default aller Bestandszeilen |
| Theme-Transport zur Seite | `PageUserContext` um Member `Theme` erweitern; anonyme Seiten rendern mit `ThemePreference.Auto` | Der Kontext wird bereits überall gebaut (`BuildUserContextAsync` lädt `ApplicationUser` ohne Mehraufwand) und in alle Composer gereicht — kein paralleler Parameterpfad nötig |
| `PageShell.BeginPage`-Signatur | `BeginPage(StringBuilder sb, string title, ThemePreference theme = ThemePreference.Auto)` | Optionaler Parameter: anonyme Aufrufer (Login/Register/Formular-abgelaufen) bleiben unverändert im Auto-Modus; authentifizierte Composer reichen `user.Theme` durch |
| CSS-Architektur | Neuer gemeinsamer const `PageShell.ThemeCss` mit den Variablen-Blöcken (`:root` Light-Palette, `[data-theme="dark"]`-Override, `@media (prefers-color-scheme: dark)`-Block, `color-scheme`), eingebettet von `BeginPage` **und** `StatusPageComposer.Render`; Farbwerte in `PageShell.Css`, `PageShell.NavCss`, `StatusPageComposer.Css` referenzieren nur noch `var(--…)` | Die Dark-Palette darf nicht zweimal gepflegt werden; `NavCss` wird von beiden Dokument-Pfaden eingebettet und braucht die Variablen in beiden Kontexten — ein gemeinsamer Theme-Block statt Duplikation |
| `data-theme`-Ausgabe | `<html lang="de" data-theme="…">` immer mit Wert `auto`/`light`/`dark`; anonyme Seiten erhalten `auto` | Einheitliches, testbares Markup; CSS-Selektor `@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { … } }` deckt `auto` und fehlende Behandlung ab, explizites `light` gewinnt gegen Dark-System |
| POST-Route und Formular | `POST /profile/theme`, Feld `theme` mit Werten `auto`/`light`/`dark`, `<select>` im Profil (vor „Passwort ändern"), Fehlerfall: direktes Re-Render mit `isError`-Meldung (kein Redirect) | Exakt das `PostProfilePassword`-Muster (Antiforgery + `PageAccessPolicy` + PRG bei Erfolg); `form select`-Styling existiert bereits in `PageShell.Css` |
| Update-Installationsseite | `RenderUpdateInstallStarted` erhält `user.Theme` wie alle anderen Admin-Seiten | In `AdminEndpoints.PostUpdateInstall` steht der `PageUserContext` zum Renderzeitpunkt bereits zur Verfügung (Inventory `logic.md`, Z. 69) — offene Frage der Anforderung damit geklärt: Benutzer-Theme gilt normal |
| Kein `UserAdminService`-Eingriff | Theme-Update erfolgt im Endpunkt direkt via `userManager.UpdateAsync` | `PostProfilePassword` nutzt `userManager`/`signInManager` ebenfalls direkt; kein generischer Service-Mehrwert für ein Einzelfeld |
| Session-Verhalten | Keine Maßnahme — die Theme-Änderung invalidiert keine Session | `StatusServerCookieEvents.ValidatePrincipal` prüft Security-Stamp/Deaktivierung/Rollen; eine Anzeige-Eigenschaft berührt keinen dieser Mechanismen (Inventory `logic.md`, Z. 124) |

## Programmabläufe

### Authentifizierter Seitenaufruf (Status-Seite `/` und alle `PageShell`-Seiten mit Kontext)

1. Der GET-Handler (z. B. `Program.cs` Z. 153–169 für `/`, `AuthEndpoints.GetProfile`, `AdminEndpoints.Render*Async`) ruft `EndpointSecurityExtensions.BuildUserContextAsync` auf.
2. `BuildUserContextAsync` lädt `ApplicationUser` via `userManager.GetUserAsync`, liest `user.Theme` (Default `Auto` bei `null`-User) und baut den erweiterten `PageUserContext`.
3. Der Composer ruft `PageShell.BeginPage(sb, title, user.Theme)` bzw. `StatusPageComposer.Render` setzt `data-theme` aus `user.Theme` in sein eigenes `<html>`-Tag.
4. Das ausgelieferte HTML enthält `data-theme="light|dark|auto"` und den `ThemeCss`-Block; der Browser löst `auto` über `prefers-color-scheme` auf (Fallback Light).

Beteiligte Klassen/Komponenten: `EndpointSecurityExtensions`, `PageUserContext`, `PageShell`, `StatusPageComposer`, `AuthPagesComposer`, `AdminPagesComposer`, `AuthEndpoints`, `AdminEndpoints`, `Program.cs`

### Anonymer Seitenaufruf (Login, Registrierung, Access-Denied ohne Kontext, Formular-abgelaufen)

1. Der anonyme Handler ruft die Composer-Methode ohne `PageUserContext` (`RenderLogin`, `RenderRegister`, `RenderFormExpired`, `RenderAccessDenied(user: null)`).
2. `BeginPage` erhält keinen Theme-Parameter → Default `Auto` → `data-theme="auto"`.
3. Es greift ausschließlich die `prefers-color-scheme`-Media-Query; es gibt keine manuelle Auswahl.

Beteiligte Klassen/Komponenten: `AuthPagesComposer`, `PageShell`, `AuthEndpoints`, `EndpointSecurityExtensions` (`RequireAntiforgery`-Filter rendert `RenderFormExpired`)

### Theme-Auswahl speichern (`POST /profile/theme`)

1. `RequireAntiforgery`-Filter validiert das Token; bei Fehler → `RenderFormExpired` mit HTTP 400 (bestehendes Verhalten).
2. `PostProfileTheme` lädt den `ApplicationUser` via `userManager.GetUserAsync`; `null` → Redirect `/login`.
3. `form["theme"]` wird gegen die Enum-Werte geparst (`Enum.TryParse` case-insensitiv + `Enum.IsDefined`-Prüfung); ungültig/fehlend → direktes Re-Render von `RenderProfile` mit `isError: true` und Fehlermeldung (kein Redirect — `PostProfilePassword`-Muster).
4. Gültig → `user.Theme = wert`, `userManager.UpdateAsync(user)`, PRG-Redirect `Results.Redirect($"{PageShell.BasePath}/profile?msg={StatusMessages.ThemeSaved}")`.
5. `GetProfile` löst den `msg`-Schlüssel über `StatusMessages.Resolve` auf und rendert die Profilseite mit Erfolgsmeldung und aktualisierter Auswahl; alle folgenden Seitenaufrufe liefern das neue `data-theme` aus.

Beteiligte Klassen/Komponenten: `AuthEndpoints`, `UserManager<ApplicationUser>`, `StatusMessages`, `AuthPagesComposer`, `PageShell`, `EndpointSecurityExtensions`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ThemePreference` | Enum (`Data/ThemePreference.cs`) | Pro Benutzer persistierte Theme-Einstellung: `Auto` (System-Follow via `prefers-color-scheme`, Fallback Light), `Light`, `Dark` |

## Änderungen an bestehenden Klassen

### `ApplicationUser` (Datenmodellklasse, `Data/ApplicationUser.cs`)

- **Neue Eigenschaften:** `Theme` (`ThemePreference`, Default `ThemePreference.Auto`) — persistierte Anzeige-Einstellung, Muster analog `IsDeactivated`.

### `PageUserContext` (Record, `Pages/PageUserContext.cs`)

- **Neue Member:** `Theme` (`ThemePreference`) — wirksame Einstellung des angemeldeten Benutzers, an fünfter Position des Positional-Records.

### `EndpointSecurityExtensions` (`Endpoints/EndpointSecurityExtensions.cs`)

- **Geänderte Methoden:** `BuildUserContextAsync` — befüllt den neuen `Theme`-Member aus `user.Theme` (`user?.Theme ?? ThemePreference.Auto`); kein zusätzlicher DB-Zugriff, das `ApplicationUser`-Objekt liegt bereits vor.

### `PageShell` (`Pages/PageShell.cs`)

- **Neue Member:** `ThemeCss` (`internal const string`) — gemeinsamer Variablen-Block (`:root` Light, `[data-theme="dark"]`, `@media (prefers-color-scheme: dark)`-Block mit `:root:not([data-theme="light"])`, `color-scheme` je Schema).
- **Geänderte Methoden:** `BeginPage` — neuer optionaler Parameter `ThemePreference theme = ThemePreference.Auto`; `<html>`-Tag erhält `data-theme`; `ThemeCss` wird zusätzlich zu `Css` + `NavCss` eingebettet.
- **Geänderte Member:** `Css` und `NavCss` — alle Hex-Farbwerte werden durch `var(--…)`-Referenzen ersetzt; keine Farbliterale außerhalb von `ThemeCss`.

### `StatusPageComposer` (`Status/StatusPageComposer.cs`)

- **Geänderte Methoden:** `Render` — eigenes `<html>`-Tag erhält `data-theme` aus `user.Theme`; `PageShell.ThemeCss` wird zusätzlich zu `Css` + `PageShell.NavCss` eingebettet. `RenderHeader`/`RenderGitHubRepository` — die beiden Inline-Styles (`style="color:#cbd5e1"`, `style="margin-top:12px"`) werden durch Klassen ersetzt.
- **Geänderte Member:** `Css` — Hex-Farbwerte durch `var(--…)` ersetzt; neue Klassen für die ersetzten Inline-Styles; Statusfarben (`.badge.*`, `.pill.*`, `.src.*`, `.stale-note`, `.msg`-Äquivalente) erhalten je Schema kontrastsichere Variablen-Werte.

### `AuthPagesComposer` (`Pages/AuthPagesComposer.cs`)

- **Geänderte Methoden:** `RenderProfile` — neuer Abschnitt „Anzeige" mit `<select name="theme">` (Optionen „Automatisch (System)", „Light", „Dark", aktueller Wert vorselektiert — Muster `AdminPagesComposer.AppendOption`) und POST-Formular `action="/profile/theme"` mit Antiforgery-Hidden-Feld; `BeginPage`-Aufruf übergibt `user.Theme`. `RenderAccessDenied` — übergibt `user?.Theme` wenn Kontext vorhanden, sonst Default. `RenderRegister`/`RenderLogin`/`RenderFormExpired` — unverändert (Default `Auto`).

### `AdminPagesComposer` (`Pages/AdminPagesComposer.cs`)

- **Geänderte Methoden:** alle `Render*`-Methoden — `BeginPage`-Aufrufe übergeben `user.Theme`.

### `AuthEndpoints` (`Endpoints/AuthEndpoints.cs`)

- **Neue Methoden:** `PostProfileTheme` — siehe Programmablauf „Theme-Auswahl speichern".
- **Geänderte Methoden:** `MapAuthEndpoints` — Route `POST /profile/theme` mit `RequireAuthorization(StatusRoles.PageAccessPolicy)` + `RequireAntiforgery()`.

### `StatusMessages` (`Endpoints/StatusMessages.cs`)

- **Neue Member:** `ThemeSaved` (`const string`, Schlüssel `"theme-saved"`) + Eintrag in `s_texts`: „Die Anzeigeeinstellung wurde gespeichert."

### `StatusServerDbSeeder` (Test-Hilfsklasse, `server/Softwareschmiede.StatusServer.Tests/TestSupport/StatusServerDbSeeder.cs`)

- **Neue Methoden:** `GetUserThemeAsync(string userName)` — liest die persistierte `Theme`-Eigenschaft für Test-Assertions (Muster `GetLockoutEndAsync`/`GetUserIdAsync`).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `UserTheme` (via `dotnet ef migrations add UserTheme` im Projekt `server/Softwareschmiede.StatusServer`) | `AspNetUsers`: neue Spalte `Theme` (`INTEGER`, `NOT NULL`, `defaultValue: 0`) | Neue `ThemePreference`-Spalte; Bestandsbenutzer erhalten `Auto` (= 0). Wird beim nächsten Server-Start automatisch über `StatusServerDatabaseInitializer.InitializeAsync` → `MigrateAsync` angewendet. `StatusServerDbContextModelSnapshot` wird vom Tool aktualisiert. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Formularfeld `theme` (`POST /profile/theme`) | Wert muss auf einen der drei `ThemePreference`-Werte mappen (`auto`/`light`/`dark`, case-insensitiv, `Enum.IsDefined`-Prüfung) | Ungültig oder fehlend → Profilseite wird direkt mit Fehlermeldung (`isError: true`) neu gerendert; keine Persistenz, kein Redirect |

## Konfigurationsänderungen

Keine. Die Einstellung ist benutzerspezifisch in `AspNetUsers` persistiert; `appsettings`, `StatusServerOptions` und `ServerSettings` bleiben unverändert.

## Seiteneffekte und Risiken

- **`PageUserContext`-Signatur:** Der Record bekommt einen fünften Member — die drei Test-Hilfsmethoden `StatusPageComposerTests.UserContext`, `AuthPagesComposerTests.User`, `AdminPagesComposerTests.Admin` konstruieren ihn direkt und müssen angepasst werden. Einzige Produktiv-Konstruktion: `BuildUserContextAsync`.
- **`StatusPageComposerTests.Render_Css_AllowsWrappingForMobile`:** Der Test assertiert auf dem CSS-Text (kein `white-space: nowrap` außer `td.num, td.right`). Der CSS-Umbau darf diese Regel nicht verletzen; neue Variablen-/Media-Blöcke sind zu prüfen.
- **Alle HTML-Tests:** Composer-Tests, die Volltext-Assertions auf dem `<html>`-Tag oder CSS-Block machen, können durch `data-theme`/`ThemeCss` betroffen sein — beim Umbau prüfen.
- **Selektor-Reihenfolge im CSS:** Explizites `data-theme="light"` muss gegen eine Dark-System-Media-Query gewinnen — der `:not([data-theme="light"])`-Selektor im Media-Block ist zwingend, sonst ignoriert „Light" die Systemeinstellung.
- **Anonyme Seiten bei Dark-System:** Login/Register wechseln mit `auto` ins Dark-Schema — alle Formular-Controls (`input`, `select`, `button`) brauchen variablen-basierte Farben und `color-scheme`, sonst bleiben native Controls hell auf dunklem Grund.
- **Kontraste der Statusfarben:** `.badge.online/.offline/.stale`, `.pill.run/.wait/.done/.err/.pause`, `.src.app/.gh`, `.msg`, `.stale-note`, `.token-once` benötigen Dark-Varianten mit ausreichendem Kontrast — Palette beim CSS-Umbau definieren (Referenz: dunkler Header `#1f2937`).
- **Keine Seiteneffekte auf** `/status.json`, `/relay`, Cookie-/Session-Verhalten, `UserAdminService`, `ServerSettingsService` und die WPF-Testprojekte (`src/*` referenziert den StatusServer nicht).

## Umsetzungsreihenfolge

1. **`ThemePreference`-Enum anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `server/Softwareschmiede.StatusServer/Data/ThemePreference.cs` mit `Auto = 0`, `Light`, `Dark` (XML-Doc je Wert).

2. **`ApplicationUser.Theme` + EF-Core-Migration**
   - Voraussetzungen: Schritt 1; `StatusServerDbContextDesignTimeFactory` und `dotnet ef` (vorhanden).
   - Beschreibung: Eigenschaft `Theme` auf `ApplicationUser`; `dotnet ef migrations add UserTheme` ausführen; Migration + `StatusServerDbContextModelSnapshot` einchecken; Default `0`/`Auto` für Bestandszeilen verifizieren.

3. **`PageUserContext` erweitern und `BuildUserContextAsync` befüllen**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: Record-Member `Theme` hinzufügen; `BuildUserContextAsync` liefert `user?.Theme ?? ThemePreference.Auto`; die drei Test-Hilfsmethoden (`UserContext`/`User`/`Admin`) erhalten den neuen Parameter (mit Default `Auto`).

4. **`PageShell` umbauen (`ThemeCss`, `BeginPage`, `Css`, `NavCss`)**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `ThemeCss`-Block mit Light-`:root`-Variablen, `[data-theme="dark"]`-Override und `prefers-color-scheme`-Media-Query anlegen; `data-theme` in `BeginPage`; sämtliche Hex-Werte in `Css`/`NavCss` durch Variablen ersetzen; `color-scheme` setzen. Finale Dark-Palette hier festlegen (bestätigter Vorschlag): Orientierung am vorhandenen Header `header.top` (`#1f2937`) — `body`-Hintergrund etwas dunkler (`#111827`), `section`/`nav` `#1f2937`, Rahmen `#374151`, Text `#f1f5f9`, gedämpfte Texte `#9ca3af`, Link `#818cf8`.

5. **`StatusPageComposer` umbauen**
   - Voraussetzungen: Schritt 4 (`PageShell.ThemeCss` muss existieren).
   - Beschreibung: `data-theme` im eigenen `<html>`-Tag aus `user.Theme`; `ThemeCss` einbetten; `Css` auf Variablen umstellen; die beiden Inline-Styles durch Klassen ersetzen. Statusfarben (`.badge.*`, `.pill.*`, `.src.*`, `.stale-note`, `.msg`-Äquivalente) im Dark-Schema als getönte Varianten der bestehenden Hue-Familien festlegen (bestätigter Vorschlag: Grün `#34d399`-Familie für ok, Rot `#f87171`-Familie für Fehler, Amber `#fbbf24`-Familie für wartend/Hinweis) — mit ausreichendem Kontrast auf dunklem Grund.

6. **Composer-Aufrufe und Profil-UI**
   - Voraussetzungen: Schritte 4–5.
   - Beschreibung: `AuthPagesComposer.RenderProfile` (Theme-Sektion mit `<select name="theme">` + POST-Formular), `RenderAccessDenied` (Kontext-Theme); alle `AdminPagesComposer.Render*`-`BeginPage`-Aufrufe übergeben `user.Theme`.

7. **`StatusMessages.ThemeSaved` + `POST /profile/theme`-Endpunkt**
   - Voraussetzungen: Schritte 3, 6.
   - Beschreibung: Meldungsschlüssel anlegen; `PostProfileTheme` in `AuthEndpoints` implementieren und in `MapAuthEndpoints` registrieren (Policy + Antiforgery + PRG).

8. **Tests anpassen und neu schreiben**
   - Voraussetzungen: Schritte 3–7.
   - Beschreibung: Betroffene Tests anpassen (Record-Konstruktionen, CSS-Assertions); neue Composer- und Flow-Tests gemäß Abschnitt „Tests".

9. **Echtbrowser-Abnahme (Screenshot-Verifikation Light/Dark)**
   - Voraussetzungen: Schritte 1–8; Edge (`C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`) oder Chrome (`C:\Program Files\Google\Chrome\Application\chrome.exe`) vorhanden.
   - Beschreibung: `Softwareschmiede.StatusServer` real starten (`dotnet run` auf `server/Softwareschmiede.StatusServer` mit isolierter SQLite-Datei via `Data__DatabasePath`-Umgebungsvariable und eigenem Port via `--urls http://127.0.0.1:<port>`); Testbenutzer registrieren/anmelden; Theme im Profil auf `Dark` und `Light` stellen. Pro Schema headless-Screenshots von `/` (Status-Seite), `/profile` und `/login` erzeugen (`msedge --headless=new --screenshot=<png> --window-size=1400,900 <url>`); für den „Automatisch"-Nachweis `prefers-color-scheme` ggf. über `--force-dark-mode` emulieren. Greift die Emulation im headless-Browser nicht, wird das explizit dokumentiert — der System-Follow-Nachweis bleibt dann CSS-Struktur-Assertion (`Render_Css_DefinesThemeVariablesAndMediaQuery`) plus Screenshot mit explizitem `data-theme="dark"` beim Benutzer. Sichtprüfung der Screenshots auf Layout-/Kontrast-Defekte als dokumentierter Abnahmeschritt (analog Abschnitt „Sichtprüfung der Abnahme-Screenshots" in `test-results.md`).

10. **Verifikation**
    - Voraussetzungen: Schritte 1–9.
    - Beschreibung: `dotnet build Softwareschmiede.slnx`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj` (vollständig, synchron — nicht im Hintergrund); `dotnet format Softwareschmiede.slnx --verify-no-changes`.

11. **Dokumentation**
    - Voraussetzungen: Schritte 1–10.
    - Beschreibung: `docs/help/status-webseite/datenmodell.md` um die neue `ApplicationUser`-Spalte ergänzen (gemäß Workflow-Schritt 12 der Anforderung).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `RenderProfile_ShowsThemeSelectWithCurrentValue` | `AuthPagesComposerTests` | Theme-`<select>` mit drei Optionen, aktueller Wert `selected`, Formular-Action `/profile/theme`, Antiforgery-Hidden-Feld; Muster `<option … selected>`-Assertion wie `RenderSettings_ShowsUpdateSection` |
| `RenderProfile_DarkTheme_SetsDataThemeAttribute` | `AuthPagesComposerTests` | `<html … data-theme="dark"` bei `user.Theme = Dark` |
| `RenderLogin_Anonymous_RendersAutoTheme` | `AuthPagesComposerTests` | Anonyme Seiten liefern `data-theme="auto"` |
| `Render_DarkTheme_SetsDataThemeOnHtmlTag` | `StatusPageComposerTests` | `data-theme` im selbst gebauten `<html>`-Tag der Status-Seite |
| `Render_Css_DefinesThemeVariablesAndMediaQuery` | `StatusPageComposerTests` | `ThemeCss` ist eingebettet: `prefers-color-scheme`, Custom Properties, `color-scheme`, `:not([data-theme="light"])`-Selektor vorhanden |
| `Render_NoInlineStyles` | `StatusPageComposerTests` | Gerendertes Markup enthält kein `style=`-Attribut mehr (Regressionsschutz für die ersetzten Inline-Styles) |
| `RenderUserList_AdminTheme_SetsDataTheme` | `AdminPagesComposerTests` | Admin-Seiten übernehmen `user.Theme` ins `data-theme`-Attribut |
| `Profile_SaveTheme_PersistsAndAppliesToAllPages` | `ProfileTests` | HTTP-Flow via `AuthTestClient`: POST `/profile/theme` (`dark`) → `302 /profile?msg=theme-saved` → Redirect-GET zeigt Meldung + `selected` auf `dark`; GET `/` und GET `/admin/users` liefern `data-theme="dark"`; `StatusServerDbSeeder.GetUserThemeAsync` belegt Persistenz |
| `Profile_SaveTheme_InvalidValue_ShowsError` | `ProfileTests` | POST mit ungültigem `theme`-Wert → Re-Render mit Fehlermeldung, Wert bleibt unverändert |
| `Profile_SaveTheme_WithoutAntiforgery_IsRejected` | `ProfileTests` | POST ohne Token → `RenderFormExpired`-Seite (Muster `Post_WithoutAntiforgery_IsRejected`) |
| `Theme_SurvivesServerRestart` | `StatusServerUserFlowTests` (oder neuer Test analog `Restart_SameDatabase_AllStateSurvives`) | Gespeichertes `Dark` überlebt einen Factory-Neustart auf gleicher `DatabasePathOverride`-Datenbank |
| `GetUserThemeAsync` | `StatusServerDbSeeder` (Test-Hilfsmethode) | Liest `ApplicationUser.Theme` für Assertions |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `StatusPageComposerTests.UserContext`, `AuthPagesComposerTests.User`, `AdminPagesComposerTests.Admin` (Hilfsmethoden) | `PageUserContext` erhält fünften Member — Konstruktionsaufrufe um `Theme`-Argument ergänzen (Default `Auto`) |
| `StatusPageComposerTests.Render_Css_AllowsWrappingForMobile` | CSS-Umbau darf die `white-space: nowrap`-Einschränkung nicht verletzen; Assertion auf neuem CSS-Text verifizieren |
| `RenderProfile_ContainsUserAndPasswordForm` (`AuthPagesComposerTests`) | Profilseite erhält zusätzlichen Abschnitt — Assertions ggf. um Theme-Sektion ergänzen/scharf stellen |

### E2E-Tests (primärer Funktionsnachweis)

Es gibt keine FlaUI-/Browser-Automatisierung für den StatusServer — der „Benutzerfluss" ist ein HTTP-Formularfluss gegen die echte Server-Pipeline. Die `StatusWebApplicationFactory`-basierten HTTP-Flow-Tests (`AuthTestClient` mit Cookie-Jar, Antiforgery-Extraktion, Redirect-Verfolgung, isolierter SQLite-Datei) sind die hier wirksame E2E-Ebene: Sie fahren die reale Middleware-Kette (Auth, Antiforgery, Routing, Composer, DB) hoch, wie es die bestehenden `ProfileTests`/`StatusServerUserFlowTests` vormachen. Geometrische Assertions sind nicht anwendbar (kein rendernder Browser); der Theme-Nachweis erfolgt über das ausgelieferte `data-theme`-Attribut plus CSS-Struktur-Assertions.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Theme-Auswahl speichern → erscheint auf allen Seiten | `Auth/ProfileTests.cs` (`Profile_SaveTheme_PersistsAndAppliesToAllPages`) | Angemeldeter Benutzer sieht alle Seiten im gewählten Schema; Auswahl persistent | Gesamter Nutzerfluss: Formular → POST → PRG → Folgeseiten; nur die echte Pipeline verbindet `PageUserContext`, Composer und DB |
| Pflicht | Ungültiger Wert + fehlendes Antiforgery-Token | `Auth/ProfileTests.cs` | Fehlerfälle werden verständlich abgelehnt (Fehlermeldung bzw. Formular-abgelaufen-Seite) | Über UI auslösbarer Fehlerfall; Antiforgery-Pfad nur über echte Pipeline prüfbar |
| Pflicht | Persistenz über Server-Neustart | `E2E/StatusServerUserFlowTests.cs` (`Theme_SurvivesServerRestart`) | Einstellung gilt über Neustarts hinweg | Nachweis der echten Persistenz (Migration + Spalte), nicht nur In-Request-Verhalten |
| Pflicht | Anonyme Seiten liefern `auto` | Composer-Test `RenderLogin_Anonymous_RendersAutoTheme` + Flow-Assertion auf `/login` ohne Login | Anonyme Besucher sehen ausschließlich System-Schema, keine manuelle Auswahl | Scope-Regel aus Anforderung; auf Composer-Ebene direkt prüfbar |

Bestehende E2E-Tests, die angepasst werden müssen: **Keine** — die bestehenden Flow-Tests (`StatusServerUserFlowTests`, `ProfileTests`) werden erweitert, nicht geändert; sie brechen nur, wenn `PageUserContext`-Konstruktionen in Hilfsmethoden fehlen (abgedeckt in „Betroffene bestehende Tests").

### Reale Abnahme: Echtbrowser-Screenshots (Sichtprüfung)

Die verbindlichen Bearbeitungshinweise verlangen bei UI-Änderungen E2E-Abnahme-Screenshots inkl. Sichtprüfung — eine Echtbrowser-/Screenshot-Verifikation des Dark-Schemas ist daher **Teil der Umsetzung** (kein Scope-Ausschluss). Vorgehen (Umsetzungsschritt 9):

- `Softwareschmiede.StatusServer` real starten (`dotnet run` auf `server/Softwareschmiede.StatusServer`, isolierte Datenbank via `Data__DatabasePath`, eigener Port via `--urls`); Testbenutzer registrieren/anmelden; Theme auf Dark bzw. Light stellen.
- Headless-Screenshots je Schema von `/` (Status-Seite), `/profile` und `/login` mit `msedge --headless=new --screenshot=<png> --window-size=1400,900 <url>` (Edge: `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`; alternativ Chrome `C:\Program Files\Google\Chrome\Application\chrome.exe` mit gleichen Flags).
- `prefers-color-scheme`-Emulation ggf. über `--force-dark-mode`; greift die Emulation im headless-Browser nicht, wird das **explizit dokumentiert** — der System-Follow-Nachweis bleibt dann die CSS-Struktur-Assertion (`Render_Css_DefinesThemeVariablesAndMediaQuery`) plus Screenshot mit explizitem `data-theme="dark"` beim Benutzer.
- Sichtprüfung der Screenshots auf Layout-/Kontrast-Defekte als dokumentierter Abnahmeschritt (analog Abschnitt „Sichtprüfung der Abnahme-Screenshots" in `test-results.md`).

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Dark-Theme wählen und überall sehen | Angemeldeter Benutzer sendet Theme-Formular auf `/profile` ab | `ApplicationUser` existiert (Registrierung/Login erfolgt), `Theme = Auto` (Migrations-Default); System-Schema beliebig | POST `/profile/theme` mit `theme=dark` + Antiforgery → `302 /profile?msg=theme-saved` → GET `/profile` zeigt Meldung und `dark` vorselektiert → GET `/` und `/admin/users` enthalten `data-theme="dark"` | Alle Seiten werden mit Dark-Palette ausgeliefert; Auswahl persistent in `AspNetUsers.Theme` | `Profile_SaveTheme_PersistsAndAppliesToAllPages` + `GetUserThemeAsync`-Assertion |
| 2 | „Automatisch" folgt dem System | GET auf beliebige Seite | Benutzer mit `Theme = Auto` (z. B. frisch migrierter Bestandsbenutzer ohne jemals gesetzte Einstellung) | Seitenaufruf liefert `data-theme="auto"`; Browser wertet `prefers-color-scheme` aus | Darstellung folgt System-Schema; bei nicht auslesbarer Einstellung Light (`:root`-Default) | `Render_Css_DefinesThemeVariablesAndMediaQuery` + `data-theme="auto"`-Assertion |
| 3 | Anonymer Besucher | GET `/login` | Kein Benutzerkontext; keine Theme-Auswahl vorhanden | `RenderLogin` → `BeginPage` ohne Theme-Parameter → `data-theme="auto"` | Login-Seite folgt ausschließlich dem System-Schema; kein Umschalter sichtbar | `RenderLogin_Anonymous_RendersAutoTheme` |
| 4 | Ungültige Auswahl | POST `/profile/theme` mit `theme=neon` | Angemeldeter Benutzer, `Theme = Auto` | Parse schlägt fehl → direktes Re-Render der Profilseite mit Fehlermeldung, kein Redirect | Verständliche Fehlermeldung; gespeicherter Wert bleibt `Auto` | `Profile_SaveTheme_InvalidValue_ShowsError` |
| 5 | Einstellung überlebt Server-Neustart | Neustart des Status-Servers | `Theme = Dark` persistiert | Neue `StatusWebApplicationFactory` auf gleicher DB-Datei → Login → GET `/` | `data-theme="dark"` weiterhin aktiv | `Theme_SurvivesServerRestart` |
| 6 | System-Wechsel bei „Automatisch" (Meta-Refresh) | Betriebssystem wechselt auf Dark; Browser lädt Status-Seite per Meta-Refresh neu | Benutzer mit `Theme = Auto`, Status-Seite geöffnet | Nächster Meta-Refresh liefert identisches `data-theme="auto"`; Browser wendet nun Dark-Variablen an | Schema wechselt ohne Nutzeraktion beim nächsten Refresh — kein Live-Umschalten (dokumentiertes Verhalten, Scope-Ausschluss 1) | Manuell/browserseitig; kein automatisierter Test möglich — durch CSS-Struktur-Assertion abgesichert; Emulation via `--force-dark-mode` im Rahmen von Szenario 7 prüfen |
| 7 | Screenshot-Abnahme Light/Dark | Testbenutzer stellt Theme im Profil auf `Dark` bzw. `Light`; headless-Browser-Screenshots je Schema | Real gestarteter `Softwareschmiede.StatusServer` (`dotnet run`, isolierte SQLite via `Data__DatabasePath`, eigener Port via `--urls`); registrierter/angemeldeter Testbenutzer | Headless-Screenshots von `/` (Status-Seite), `/profile` und `/login` je Schema per `msedge --headless=new --screenshot=<png> --window-size=1400,900`; danach Sichtprüfung auf Layout-/Kontrast-Defekte | Beide Schemata rendern vollständig und kontrastsicher; keine Layout-/Kontrast-Defekte sichtbar | Dokumentierte Sichtprüfung der Abnahme-Screenshots (`test-results.md`); bei nicht greifender `prefers-color-scheme`-Emulation: CSS-Struktur-Assertion + Screenshot mit explizitem `data-theme="dark"` |

## Offene Punkte

Keine.
