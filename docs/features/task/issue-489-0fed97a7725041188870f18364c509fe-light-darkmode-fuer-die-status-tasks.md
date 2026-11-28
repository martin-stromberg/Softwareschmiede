# Tasks: Light-/Darkmode für die Status-Webseite (Issue #489)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Enum `ThemePreference` (`Auto`/`Light`/`Dark`, `Auto = 0`) in `Data/ThemePreference.cs` anlegen | Offen | — |
| 2 | Datenmodell | Eigenschaft `Theme` (`ThemePreference`, Default `Auto`) auf `ApplicationUser` hinzufügen | Offen | — |
| 3 | Datenmodell | EF-Core-Migration `UserTheme` erzeugen (`AspNetUsers.Theme`, `INTEGER NOT NULL DEFAULT 0`) und Model-Snapshot einchecken | Offen | — |
| 4 | Datenmodell | Record-Member `Theme` (`ThemePreference`) zu `PageUserContext` hinzufügen | Offen | — |
| 5 | Logik | `EndpointSecurityExtensions.BuildUserContextAsync` um Befüllung des `Theme`-Members erweitern (`user?.Theme ?? Auto`) | Offen | — |
| 6 | Logik | `StatusMessages.ThemeSaved`-Schlüssel (`"theme-saved"`, „Die Anzeigeeinstellung wurde gespeichert.") hinzufügen | Offen | — |
| 7 | Logik | `AuthEndpoints.PostProfileTheme` implementieren (GetUserAsync, `theme` parsen + `Enum.IsDefined`-Prüfung, `userManager.UpdateAsync`, PRG nach `/profile?msg=theme-saved`, Fehler → Re-Render mit `isError`) | Offen | — |
| 8 | Logik | Route `POST /profile/theme` in `MapAuthEndpoints` mit `PageAccessPolicy` + `RequireAntiforgery` registrieren | Offen | — |
| 9 | UI | `PageShell.ThemeCss` anlegen (`:root` Light-Variablen, `[data-theme="dark"]`-Override, `@media (prefers-color-scheme: dark)` mit `:root:not([data-theme="light"])`, `color-scheme` je Schema) | Offen | — |
| 10 | UI | `PageShell.BeginPage` um optionalen Parameter `ThemePreference theme = Auto` erweitern und `data-theme` auf `<html>` ausgeben; `ThemeCss` einbetten | Offen | — |
| 11 | UI | `PageShell.Css` auf CSS Custom Properties umstellen (alle Hex-Farbwerte durch `var(--…)`) | Offen | — |
| 12 | UI | `PageShell.NavCss` auf CSS Custom Properties umstellen | Offen | — |
| 13 | UI | `StatusPageComposer.Render`: `data-theme` aus `user.Theme` ins eigene `<html>`-Tag; `PageShell.ThemeCss` einbetten | Offen | — |
| 14 | UI | `StatusPageComposer.Css` auf CSS Custom Properties umstellen (inkl. `.badge.*`, `.pill.*`, `.src.*`, `.stale-note` Dark-Varianten) | Offen | — |
| 15 | UI | Inline-Style `style="color:#cbd5e1"` (Z. ~90) durch Klasse ersetzen | Offen | — |
| 16 | UI | Inline-Style `style="margin-top:12px"` (Z. ~288) durch Klasse ersetzen | Offen | — |
| 17 | UI | `AuthPagesComposer.RenderProfile`: Theme-Abschnitt mit `<select name="theme">` (drei Optionen, aktueller Wert `selected`, POST `/profile/theme`, Antiforgery-Feld) einbauen; `BeginPage` mit `user.Theme` aufrufen | Offen | — |
| 18 | UI | `AuthPagesComposer.RenderAccessDenied`: `BeginPage` mit `user?.Theme` aufrufen | Offen | — |
| 19 | UI | Alle `AdminPagesComposer.Render*`: `BeginPage`-Aufrufe um `user.Theme` ergänzen | Offen | — |
| 20 | Validierung | `theme`-Formularwert gegen `auto`/`light`/`dark` validieren (case-insensitiv, `Enum.IsDefined`); ungültig → Profil-Re-Render mit Fehlermeldung | Offen | — |
| 21 | Tests | Test-Hilfsmethoden `StatusPageComposerTests.UserContext`, `AuthPagesComposerTests.User`, `AdminPagesComposerTests.Admin` um `Theme`-Argument ergänzen | Offen | — |
| 22 | Tests | `StatusServerDbSeeder.GetUserThemeAsync` hinzufügen | Offen | — |
| 23 | Tests | `AuthPagesComposerTests.RenderProfile_ShowsThemeSelectWithCurrentValue` schreiben | Offen | — |
| 24 | Tests | `AuthPagesComposerTests.RenderProfile_DarkTheme_SetsDataThemeAttribute` schreiben | Offen | — |
| 25 | Tests | `AuthPagesComposerTests.RenderLogin_Anonymous_RendersAutoTheme` schreiben | Offen | — |
| 26 | Tests | `StatusPageComposerTests.Render_DarkTheme_SetsDataThemeOnHtmlTag` schreiben | Offen | — |
| 27 | Tests | `StatusPageComposerTests.Render_Css_DefinesThemeVariablesAndMediaQuery` schreiben | Offen | — |
| 28 | Tests | `StatusPageComposerTests.Render_NoInlineStyles` schreiben | Offen | — |
| 29 | Tests | `AdminPagesComposerTests.RenderUserList_AdminTheme_SetsDataTheme` schreiben | Offen | — |
| 30 | E2E-Tests | `ProfileTests.Profile_SaveTheme_PersistsAndAppliesToAllPages` schreiben (POST → PRG → GET `/` und `/admin/users` mit `data-theme="dark"`, Seeder-Assertion) | Offen | — |
| 31 | E2E-Tests | `ProfileTests.Profile_SaveTheme_InvalidValue_ShowsError` schreiben | Offen | — |
| 32 | E2E-Tests | `ProfileTests.Profile_SaveTheme_WithoutAntiforgery_IsRejected` schreiben | Offen | — |
| 33 | E2E-Tests | `StatusServerUserFlowTests.Theme_SurvivesServerRestart` schreiben (Factory-Neustart auf gleicher `DatabasePathOverride`-DB) | Offen | — |
| 34 | Tests | Bestehende CSS-/Markup-Assertions verifizieren (`Render_Css_AllowsWrappingForMobile`, `RenderProfile_ContainsUserAndPasswordForm`) | Offen | — |
| 35 | Verifikation | `dotnet build Softwareschmiede.slnx` + vollständiger synchroner Lauf `dotnet test server/Softwareschmiede.StatusServer.Tests` (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) + `dotnet format Softwareschmiede.slnx --verify-no-changes` | Offen | — |
| 36 | Verifikation | StatusServer real starten (`dotnet run` auf `server/Softwareschmiede.StatusServer`, isolierte SQLite via `Data__DatabasePath`, eigener Port via `--urls`), Testbenutzer registrieren/anmelden, Theme auf Dark bzw. Light stellen | Offen | — |
| 37 | Verifikation | Headless-Edge-Screenshots (`msedge --headless=new --screenshot=<png> --window-size=1400,900`; Fallback Chrome) von `/`, `/profile`, `/login` in beiden Schemata erzeugen (ggf. `--force-dark-mode` für `prefers-color-scheme`-Emulation; sonst dokumentierter Nachweis via explizitem `data-theme="dark"`) und Sichtprüfung auf Layout-/Kontrast-Defekte in `test-results.md` dokumentieren | Offen | — |
| 38 | Dokumentation | `docs/help/status-webseite/datenmodell.md` um die neue `ApplicationUser`-Spalte `Theme` ergänzen | Offen | — |
