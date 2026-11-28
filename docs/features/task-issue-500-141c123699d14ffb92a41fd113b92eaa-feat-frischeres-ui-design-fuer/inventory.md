# Bestandsaufnahme: Frischeres UI-Design für die Status-Webseite — Design-Mockups (Issue #500)

Analysiert wurde `server/Softwareschmiede.StatusServer` (server-gerendertes HTML ohne
JavaScript, ASP.NET Core Minimal APIs) bezogen auf die Anforderung
[requirement.md](requirement.md): HTML/CSS-Mockups aller neun Ansichten in Light +
Dark — erfasst wurden der Seitenrahmen samt Design-Tokens, der vollständige
Funktionsumfang jeder Ansicht, die Endpunkte/Interaktionsmuster, das Rollen-/Theme-Modell
sowie die bestehenden Tests mit Markup-Assertions.

## Zusammenfassung

- **Rendering-Architektur:** Vier Composer erzeugen komplettes HTML per
  `StringBuilder`: `PageShell` (Rahmen: `ThemeCss` + Basis-`Css` + `NavCss`,
  `BeginPage`/`AppendHeader`/`AppendNav`/`AppendMessage`/`AppendHiddenToken`/`EndPage`),
  `StatusPageComposer` (Status-Übersicht mit eigenem Dokument-Aufbau und eigenem
  CSS, zusätzlich Meta-Refresh), `AuthPagesComposer` (5 Methoden: Register, Login,
  Profil, AccessDenied, FormExpired), `AdminPagesComposer` (6 Methoden: UserList,
  UserDeleteConfirm, TokenList, TokenRevokeConfirm, Settings,
  UpdateInstallStarted). Gesamtes CSS liegt als `const string`-Literale im Code —
  keine externen Assets, kein JS.
- **Design-Tokens (Issue #489, bereits gemergt):** `PageShell.ThemeCss` definiert
  ~40 CSS Custom Properties — Light als `:root`-Default, Dark unter
  `:root[data-theme="dark"]` sowie `prefers-color-scheme`-Media-Query mit
  `:not([data-theme="light"])`-Guard (explizites Light gewinnt). Token-Gruppen:
  Flächen (`--bg`, `--fg`, `--surface`, `--border*`), Text (`--label`, `--muted*`),
  Header (`--header-*`, `--inst-header`), Accent/Buttons (`--accent*`, `--button-fg`),
  Danger (`--danger*`), Status (`--ok-*`, `--err-*`, `--warn-*`, `--pause-*`),
  Badges (`--badge-online/offline/stale-*`), Quellen (`--src-*`), Pills
  (`--pill-run-*`). Teil der Dark-Palette werden bewusst nicht überschrieben
  (Badge-/Danger-Strong-Tokens gelten in beiden Schemata).
- **Theme-Mechanik:** `ThemePreference` (`Auto`/`Light`/`Dark`, persistiert in
  `AspNetUsers.Theme`) → `data-theme`-Attribut am `<html>`-Tag. Anonyme Seiten
  (Login/Register/Fehlerseiten) rendern `data-theme="auto"`; angemeldete Seiten
  nutzen `PageUserContext.Theme`; Override via `POST /profile/theme` im Profil.
- **Alle neun Ansichten existieren** mit dem im Issue beschriebenen Funktionsumfang
  — inklusive der zwei Bestätigungs-Zwischenseiten (User löschen, Token widerrufen),
  der statischen Update-Install-Seite (bewusst ohne PRG-Redirect), dem
  Einmal-Klartext-Block (`.token-once`) und den Leer-/Fehler-/Schutz-Zuständen
  („letzter Admin", verwaiste Installationssperre, GitHub-Abruffehler).
- **Interaktionsmodell strikt JS-frei:** Links + POST-Formulare mit
  Hidden-Antiforgery-Feld in **jedem** Formular (inkl. Abmelden in der Nav);
  destruktive Aktionen als `GET`-Zwischenseite → `POST` auf denselben Pfad mit
  `danger`-Button + Abbrechen-Link; Erfolg/Fehler via `?msg=`/`?err=`-Schlüsseln
  (19 feste Texte in `StatusMessages`).
- **Rollen-Modell:** `StatusRoles` — `Administration` (Vollzugriff) und
  `Ueberwacher` (lesend); Policies `PageAccess` (beide) und `AdminOnly`.
  Nav-Conditional in `PageShell.AppendNav`.
- **Bekannte Lücke Registrierungs-Flow:** `/login` rendert auch ohne vorhandenes
  Benutzerkonto das Formular — nur geschützte Seiten leiten via
  `StatusServerCookieEvents.RedirectToLogin` auf `/register` um. Für Mockups ohne
  sichtbare Konsequenz, für die Umsetzung zu vermerken.

**Test-Ausgangszustand:** Alle drei regulären CI-Lanes fehlerfrei (Details:
[inventory/tests.md](inventory/tests.md)) — StatusServer-Tests 165/165,
reguläre Unit-Tests 2880 bestanden / 3 übersprungen (Plattform-Skips),
reguläre Integrationstests 437/437. **0 nachgewiesene Fehlschläge.** Nicht
ausgeführt: die `Category=OsInterface`-Lane (E2E/ConPTY/Clipboard — in dieser
Sandbox eingeschränkt lauffähig, enthält keine StatusServer-Tests; Ausgangszustand
unbekannt). Relevant: 35 der StatusServer-Tests prüfen wörtliche
Markup-Fragmente/CSS-Selektoren — sie bilden den Funktionsvertrag ab, den das
Redesign erhalten muss.

## Details

- [Seitenrahmen `PageShell`, Design-Tokens, `PageUserContext`, `ThemePreference`](inventory/pages-shell.md)
- [Ansicht 1: Status-Übersicht `/` — `StatusPageComposer` (inkl. Datenmodelle der Sektionen)](inventory/status-page.md)
- [Ansichten 2/3/4/8/9: Login, Registrierung, Profil, Access-Denied, Formular-abgelaufen — `AuthPagesComposer`](inventory/auth-pages.md)
- [Ansichten 5–7 + Zwischenseiten: Benutzer, Tokens, Einstellungen — `AdminPagesComposer`](inventory/admin-pages.md)
- [Endpunkte, Routen, Antiforgery- und POST-only-Interaktionsmodell](inventory/endpoints.md)
- [Datenmodelle, Enums, Konstanten](inventory/models.md)
- [Tests, Test-Ausgangszustand, redesign-relevante Markup-Assertions](inventory/tests.md)

## Konventionen für Assets/Screenshots im Repo

- `docs/images/` enthält aktuell nur eine SVG (`pr-pipeline-checks.svg`); es gibt
  **keine** Screenshot-Konvention für die Status-Webseite — `docs/help/status-webseite/`
  (12 Markdown-Dateien) referenziert keine Bilder.
- Präzedenzfall für HTML-Artefakte:
  `docs/features/task/issue-488-a80d2fd15b804579ae546803a5f930d6-automatisierte-updates-der-web/e2e-artifacts/`
  enthält gedumpte, gerenderte HTML-Seiten der Admin-Settings (als
  Review-/Nachweis-Artefakte, Stand vor #489 — noch ohne `data-theme`). Ein
  Ablageort `docs/features/task-issue-500-…/design/` für Mockup-HTML/CSS +
  Screenshots passt damit zur bestehenden Repo-Praxis.
- `.githooks/pre-push` spiegelt den CI-Format-Check (`dotnet format
  Softwareschmiede.slnx --verify-no-changes`) — betrifft nur C#-Dateien, für den
  Design-Lauf ohne Auswirkung.
