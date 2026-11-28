# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Pro Benutzer persistierte Theme-Einstellung (Automatisch/Light/Dark) | `ThemePreference`-Enum (`Auto=0`, `Light`, `Dark`), `ApplicationUser.Theme`, EF-Migration `UserTheme` (`INTEGER`, `defaultValue: 0`) — Umsetzungsschritte 1–2 | `GetUserThemeAsync`-Hilfsmethode + `Theme_SurvivesServerRestart` | Abgedeckt |
| Wirksames Schema im ausgelieferten HTML verankert (`data-theme`), kein JS, kein Live-Umschalten | `data-theme="auto|light|dark"` auf `<html>` in beiden Dokument-Pfaden (`PageShell.BeginPage` optionaler Parameter, `StatusPageComposer.Render` eigenes Tag) — Schritte 4–5 | `Render_DarkTheme_SetsDataThemeOnHtmlTag`, `RenderProfile_DarkTheme_SetsDataThemeAttribute`, `RenderUserList_AdminTheme_SetsDataTheme` | Abgedeckt |
| Fest verdrahtete Light-Palette auf CSS Custom Properties umstellen (`PageShell.Css`, `NavCss`, `StatusPageComposer.Css`) | Gemeinsamer `PageShell.ThemeCss`-Block (`:root` Light, `[data-theme="dark"]`, Media-Query), alle Hex-Werte → `var(--…)` — Schritte 4–5 | `Render_Css_DefinesThemeVariablesAndMediaQuery`; `Render_Css_AllowsWrappingForMobile` als betroffener Test benannt | Abgedeckt |
| „Automatisch" folgt `prefers-color-scheme`, Fallback Light; explizites Light gewinnt gegen Dark-System | `:root`-Light-Default + `@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) … }` — Designentscheidung `data-theme`-Ausgabe + Risikoabschnitt | `Render_Css_DefinesThemeVariablesAndMediaQuery` (inkl. `:not([data-theme="light"])`-Assertion) | Abgedeckt |
| Angemeldete Benutzer sehen alle Seiten im gewählten Schema | `PageUserContext.Theme`-Member; `BuildUserContextAsync` befüllt aus `ApplicationUser` (kein Mehr-DB-Zugriff); alle Composer reichen `user.Theme` an `BeginPage`/`Render` — Schritte 3, 6 | `Profile_SaveTheme_PersistsAndAppliesToAllPages` (prüft `/`, `/profile`, `/admin/users` auf `data-theme="dark"`) | Abgedeckt |
| Anonyme Besucher nur System-Schema (Fallback Light), keine manuelle Auswahl | `BeginPage`-Default `Auto` → `data-theme="auto"`; anonyme Composer (`RenderLogin`/`RenderRegister`/`RenderFormExpired`/`RenderAccessDenied` ohne Kontext) bleiben auf Auto; kein Umschalter — Programmablauf „Anonymer Seitenaufruf", Ausschluss 4 | `RenderLogin_Anonymous_RendersAutoTheme` + Flow-Assertion `/login` ohne Login | Abgedeckt |
| Theme-Auswahl persistent über Sitzungen und Server-Neustarts, gilt ab nächstem Seitenaufruf | Persistenz in `AspNetUsers.Theme`; `ValidatePrincipal` unberührt (Designentscheidung „Session-Verhalten", verifiziert an `StatusServerCookieEvents`) | `Theme_SurvivesServerRestart` (Factory-Neustart auf gleicher `DatabasePathOverride`-DB) | Abgedeckt |
| Bestandsbenutzer erhalten Default „Automatisch" per Migration | Migration `UserTheme` mit `defaultValue: 0` (`Auto` = CLR-Default); automatische Anwendung via `StatusServerDatabaseInitializer.MigrateAsync` — Schritt 2 | Implizit: Szenario 2 (`data-theme="auto"`-Assertion bei Benutzer ohne gesetzte Einstellung) + `GetUserThemeAsync` | Abgedeckt |
| Theme-Auswahl auf `/profile`: explizite Auswahl (3 Optionen, kein Freitext), POST mit Antiforgery + PRG + `StatusMessages` | `<select name="theme">` in `RenderProfile`, `POST /profile/theme` (`PageAccessPolicy` + `RequireAntiforgery`), PRG `?msg=theme-saved`, neuer Schlüssel `StatusMessages.ThemeSaved` — Schritte 6–7 | `RenderProfile_ShowsThemeSelectWithCurrentValue`, `Profile_SaveTheme_PersistsAndAppliesToAllPages`, `Profile_SaveTheme_WithoutAntiforgery_IsRejected` | Abgedeckt |
| Ungültige/fehlende Werte werden verständlich abgelehnt, keine Persistenz | `Enum.TryParse` + `Enum.IsDefined`; Fehlerfall → direktes Re-Render mit `isError` (kein Redirect) — Validierungsregel + Programmablauf | `Profile_SaveTheme_InvalidValue_ShowsError` | Abgedeckt |
| `StatusPageComposer`: eigenes `<html>`-Tag mit Theme-Attribut; Inline-Styles (Z. 90 `color:#cbd5e1`, Z. 288 `margin-top:12px`) durch Klassen ersetzt | Schritt 5 — verifiziert: exakt diese beiden `style=` vorhanden (`StatusPageComposer.cs` Z. 90, 288) | `Render_NoInlineStyles` (kein `style=` im Markup) | Abgedeckt |
| `color-scheme`-Eigenschaft für native Controls | In `ThemeCss` je Schema — Designentscheidung + Risiko „Anonyme Seiten bei Dark-System" | `Render_Css_DefinesThemeVariablesAndMediaQuery` (assertiert `color-scheme`) | Abgedeckt |
| Kontrastsichere Dark-Varianten aller Statusfarben (`.badge.*`, `.pill.*`, `.src.*`, `.stale-note`, `.msg`, `.token-once`) | Dark-Palette in Schritt 4 (bestätigter Vorschlag, Orientierung `#1f2937`), Statusfarben in Schritt 5; Risikoabschnitt benannt | Echtbrowser-Screenshot-Abnahme mit Sichtprüfung (Schritt 9, Szenario 7) | Abgedeckt |
| `AdminPagesComposer`: alle `Render*` übernehmen erweiterten `PageShell`-Aufruf; `RenderUpdateInstallStarted` mit Benutzer-Theme | Schritt 6; offene Frage geklärt — `PageUserContext` steht in `AdminEndpoints.PostUpdateInstall` Z. 386–391 zur Verfügung (verifiziert) | `RenderUserList_AdminTheme_SetsDataTheme` | Abgedeckt |
| `Program.cs` GET `/`: Theme an `StatusPageComposer.Render` durchreichen | Implizit über `PageUserContext.Theme` (Kontext wird in `Program.cs` Z. 164–166 bereits gebaut — verifiziert); keine Signaturänderung nötig | `Profile_SaveTheme_PersistsAndAppliesToAllPages` (GET `/` → `data-theme="dark"`) | Abgedeckt |
| `/status.json` und `/relay` unverändert | Scope-Ausschluss 2 (bestätigt) | Nicht erforderlich (Ausschluss) | Abgedeckt |
| Kein Admin-/Server-Standard-Schema, kein anonymer Umschalter, kein per-Instanz-Schema | Scope-Ausschlüsse 3–5 (jeweils bestätigt, begründet) | Nicht erforderlich (Ausschluss) | Abgedeckt |
| Meta-Refresh: Schema-Wechsel bei „Automatisch" erst beim nächsten Refresh | Als dokumentiertes Verhalten (Ausschluss 1, Szenario 6) | Nicht automatisierbar — dokumentiert; Emulation via `--force-dark-mode` in Schritt 9 | Abgedeckt |
| Dokumentation `docs/help/status-webseite/datenmodell.md` um neue Spalte ergänzen | Umsetzungsschritt 11 | Dokumentationsschritt (kein Test) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— (keine; Status `Plan vollständig`)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Theme wählen auf `/profile` → speichern → auf allen Seiten wirksam (Happy Path) | `Profile_SaveTheme_PersistsAndAppliesToAllPages` (`ProfileTests`) — echte Pipeline via `StatusWebApplicationFactory`/`AuthTestClient`: POST → 302 PRG → Redirect-GET → GET `/` und `/admin/users` mit `data-theme="dark"` + `GetUserThemeAsync`-Persistenz-Assertion | Abgedeckt |
| Ungültiger Theme-Wert → Fehlermeldung, Wert unverändert | `Profile_SaveTheme_InvalidValue_ShowsError` (`ProfileTests`) | Abgedeckt |
| POST ohne Antiforgery-Token → Formular-abgelaufen-Seite (400) | `Profile_SaveTheme_WithoutAntiforgery_IsRejected` (`ProfileTests`) | Abgedeckt |
| Persistenz über Server-Neustart | `Theme_SurvivesServerRestart` (`StatusServerUserFlowTests`, Muster `Restart_SameDatabase_AllStateSurvives` mit `DatabasePathOverride`) | Abgedeckt |
| Anonyme Seiten rendern `auto`, kein Umschalter | `RenderLogin_Anonymous_RendersAutoTheme` + Flow-Assertion `/login` ohne Login | Abgedeckt |
| Visuelle Abnahme Light/Dark (Layout/Kontrast) | Echtbrowser-Headless-Screenshots (`msedge --headless=new --screenshot`) von `/`, `/profile`, `/login` je Schema + dokumentierte Sichtprüfung; `prefers-color-scheme`-Emulation via `--force-dark-mode` mit dokumentiertem Fallback (CSS-Struktur-Assertion) — Umsetzungsschritt 9 | Abgedeckt |
| Geometrische Assertions (BoundingBox) | — | Nicht erforderlich mit Begründung: kein rendernder Browser in der Test-Ebene; keine Dialoge/Overlays geplant; visuelle Geometrie wird über die Screenshot-Sichtprüfung abgenommen |

Die E2E-Ebene ist begründet: Das StatusServer-Testprojekt hat keine Browser-Automatisierung; die `StatusWebApplicationFactory`-HTTP-Flow-Tests (Cookie-Jar, Antiforgery-Extraktion, Redirect-Verfolgung, isolierte SQLite-Datei) fahren die reale Middleware-Kette und sind das etablierte E2E-Muster des Projekts (`E2E/StatusServerUserFlowTests`). Zusätzlich ist eine reale Echtbrowser-Abnahme eingeplant.

## Fehlende oder unvollständige Planbestandteile

— (keine; Status `Plan vollständig`)

## Hinweise

- **Migrations-Default-Nachweis:** Der Test `GetUserThemeAsync`-Assertion „frisch angelegter/migrierter Benutzer liefert `Auto`" ist nicht explizit als eigener Test benannt, aber über Szenario 2 (`data-theme="auto"` bei Benutzer ohne gesetzte Einstellung) und den Mechanismus `Auto = 0`/`defaultValue: 0` abgesichert. Bei der Umsetzung kann `Theme_SurvivesServerRestart` oder der Happy-Path-Test die `Auto`-Ausgangslage vor dem ersten POST mit assertieren — kein zusätzlicher Aufwand.
- **Überwacher-Stufe:** `POST /profile/theme` steht unter `PageAccessPolicy` (beide Stufen) — korrekt. Ein expliziter Überwacher-Fall ist nicht geplant, aber auch nicht gefordert; `PageAccessPolicy` unterscheidet nicht zwischen Admin/Überwacher.
- **Verifizierte Plan-Behauptungen (Stichproben):** `PageUserContext` hat exakt 4 Members (`Pages/PageUserContext.cs` Z. 8–12); `BuildUserContextAsync` lädt `ApplicationUser` bereits (`EndpointSecurityExtensions.cs` Z. 23–32); `StatusPageComposer.Render` baut sein `<html>` selbst (Z. 39–44) und enthält die beiden Inline-Styles (Z. 90, 288); `PageShell.BeginPage` hat keinen Theme-Parameter und `form select`-Styling existiert (`PageShell.cs` Z. 51–53, 87–94); `AuthEndpoints` kennt `PostProfilePassword` als PRG-Muster (Z. 170–219); `RenderUpdateInstallStarted` erhält den Kontext in `AdminEndpoints.cs` Z. 386–391; die drei Test-Hilfsmethoden (`StatusPageComposerTests.cs` Z. 15–16, `AuthPagesComposerTests.cs` Z. 10–11, `AdminPagesComposerTests.cs` Z. 13–14) sind die einzigen `PageUserContext`-Konstruktionen im Testprojekt; kein bestehender Test assertiert auf `<html>`/`lang="de"` — der benannte Bestandstest-Impact ist vollständig; `StatusServerDbContextDesignTimeFactory` und genau eine Migration `20261127095016_InitialCreate` liegen vor; `AuthTestClient`/`StatusServerDbSeeder`-Muster (`GetLockoutEndAsync` Z. 151–155, `BumpConcurrencyStampAsync` Z. 222–229) tragen die geplanten Erweiterungen; `docs/help/status-webseite/datenmodell.md` existiert und dokumentiert `IsDeactivated` (Z. 24, 79).
