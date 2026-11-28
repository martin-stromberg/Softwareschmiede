# Seitenrahmen: `PageShell` und `PageUserContext`

Analysierte Dateien:

- `server/Softwareschmiede.StatusServer/Pages/PageShell.cs`
- `server/Softwareschmiede.StatusServer/Pages/PageUserContext.cs`
- `server/Softwareschmiede.StatusServer/Data/ThemePreference.cs`

`PageShell` ist der gemeinsame HTML-Rahmen aller server-gerenderten Seiten. Das komplette
CSS liegt als `const string`-Literale im C#-Code (kein externes Stylesheet, kein
JavaScript). Drei Composer ziehen die Bausteine ein: `StatusPageComposer` nutzt
`ThemeCss` + `NavCss` + eigenes `Css` (Status-Übersicht hat ein eigenes Basis-CSS),
`AuthPagesComposer` und `AdminPagesComposer` nutzen `BeginPage` (Theme + Basis-CSS +
Nav-CSS gebündelt).

## Methoden und Konstanten

| Member | Sichtbarkeit | Zweck |
|--------|-------------|-------|
| `HtmlContentType` | `internal const` = `"text/html; charset=utf-8"` | Content-Type aller Seiten. |
| `BasePath` | `internal static string` | Konfigurierter PathBase-Präfix (Reverse-Proxy-Unterpfad); wird einmalig in `Program.cs:137` gesetzt und allen Links/Form-Actions/Redirects vorangestellt. |
| `ThemeCss` | `internal const` | Theme-Variablen beider Schemata als CSS Custom Properties (siehe unten). |
| `DarkThemeVars` | `private const` | Dark-Paletten-Deklarationsblock, in `ThemeCss` zweimal eingesetzt (`:root[data-theme="dark"]` und `@media (prefers-color-scheme: dark)` → `:root:not([data-theme="light"])`). |
| `NavCss` | `internal const` | CSS für `nav.topnav` (siehe unten). |
| `Css` | `private const` | Basis-CSS für Auth-/Admin-Seiten (siehe unten). |
| `BeginPage(sb, title, theme = Auto)` | `public static` | Öffnet Dokument: `<!doctype html><html lang="de" data-theme="auto\|light\|dark">`, `<meta charset="utf-8">`, `<meta name="viewport" content="width=device-width,initial-scale=1">`, `<title>`, `<style>` mit `ThemeCss + Css + NavCss`, `<body><div class="page">`. Kein Meta-Refresh (nur Status-Seite). |
| `ThemeName(theme)` | `internal static` | `ThemePreference` → `data-theme`-Wert (`auto`/`light`/`dark`). |
| `AppendHeader(sb, title)` | `public static` | `<header class="top"><h1>Titel</h1></header>` — dunkle Titelzeile. |
| `AppendNav(sb, user)` | `public static` | Navigationszeile (siehe unten). |
| `AppendMessage(sb, message, isError)` | `public static` | `<div class="msg">` bzw. `class="msg err"` — nur wenn message nicht leer. |
| `AppendHiddenToken(sb, fieldName, token)` | `public static` | Antiforgery-Hidden-Feld `<input type="hidden" name="…" value="…">`. |
| `EndPage(sb)` | `public static` | `<footer>softwareschmiede-status · server-gerendert</footer></div></body></html>`. |
| `E(value)` | `public static` | HTML-Encoding (`WebUtility.HtmlEncode`). |

## Design-Tokens (`ThemeCss`, Issue #489)

Light-Palette als `:root`-Default, Dark-Palette unter `:root[data-theme="dark"]` sowie
`@media (prefers-color-scheme: dark) :root:not([data-theme="light"])` — explizites
Light gewinnt also auch bei dunkler Systemeinstellung. `color-scheme: light/dark`
steuert native Controls.

### Token-Namen (Light → Dark)

| Token | Light | Dark | Verwendung |
|-------|-------|------|-----------|
| `--bg` | `#f4f5f7` | `#111827` | Seitenhintergrund |
| `--fg` | `#1f2430` | `#f1f5f9` | Haupttext |
| `--surface` | `#fff` | `#1f2937` | Karten/Sections/Eingabefelder |
| `--border` | `#e5e7eb` | `#374151` | Kartenrand |
| `--border-input` | `#d1d5db` | `#4b5563` | Eingaberand/Buttons-Rahmen |
| `--border-row` | `#f0f1f3` | `#374151` | Tabellenzeilen |
| `--label` | `#374151` | `#d1d5db` | Formular-Labels |
| `--muted` | `#6b7280` | `#9ca3af` | Sekundärtext |
| `--muted-faint` | `#9ca3af` | `#6b7280` | Footer, Disabled-Button |
| `--header-bg` | `#1f2937` | — (nur Light gesetzt; Dark erbt Light-Wert) | Kopfzeile `header.top` |
| `--header-fg` | `#f8fafc` | — | Kopfzeilentext |
| `--inst-header` | `#cbd5e1` | — | Instanz-Tag auf dunklem Header (`.inst.on-dark`) |
| `--accent` | `#4f46e5` | `#818cf8` | Links, Primär-Buttons |
| `--accent-hover` | `#4338ca` | `#6366f1` | Button-Hover |
| `--button-fg` | `#fff` | — | Primär-Button-Text |
| `--danger` | `#991b1b` | `#f87171` | Danger-Link-Text |
| `--danger-strong` | `#b91c1c` | — | Danger-Button-Hintergrund |
| `--danger-strong-hover` | `#991b1b` | — | Danger-Button-Hover |
| `--danger-border` | `#f87171` | — | Danger-Link-Rahmen |
| `--ok-bg`/`--ok-fg`/`--ok-border` | `#d1fae5`/`#065f46`/`#34d39955` | `#065f46`/`#a7f3d0` | Erfolg (msg, pill.done) |
| `--err-bg`/`--err-fg`/`--err-border` | `#fee2e2`/`#991b1b`/`#f8717155` | `#7f1d1d`/`#fecaca` | Fehler (msg.err, pill.err) |
| `--warn-bg`/`--warn-fg`/`--warn-border`/`--warn-strong` | `#fef3c7`/`#92400e`/`#f59e0b55`/`#f59e0b` | `#78350f`/`#fde68a` | Warnung (pill.wait, stale-note, token-once) |
| `--pause-bg`/`--pause-fg` | `#e5e7eb`/`#4b5563` | `#374151`/`#d1d5db` | Neutral (pill.pause) |
| `--hover` | `#f3f4f6` | `#374151` | Hover-Flächen |
| `--badge-online-bg/-fg/-dot` | `#065f46`/`#d1fae5`/`#34d399` | — | Status-Badge „online" |
| `--badge-offline-bg/-fg/-dot` | `#7f1d1d`/`#fee2e2`/`#f87171` | — | Status-Badge „offline" |
| `--badge-stale-bg/-fg/-dot` | `#78350f`/`#fef3c7`/`#fbbf24` | — | Status-Badge „teilweise veraltet" |
| `--src-fg` | `#fff` | — | Quellen-Badge-Text |
| `--src-app`/`--src-gh` | `#4f46e5`/`#0d9488` | `#6366f1`/`#14b8a6` | Quellen-Badges App/GitHub |
| `--pill-run-bg`/`--pill-run-fg` | `#e0e7ff`/`#3730a3` | `#312e81`/`#c7d2fe` | Pill „läuft" |

Hinweis: Die Badge-Token (`--badge-*`), `--header-*`, `--inst-header`, `--src-fg`,
`--button-fg`, `--danger-strong*`, `--*-border`, `--warn-strong` sind in `DarkThemeVars`
nicht überschrieben — sie gelten in beiden Schemata (bewusst: Badges sollen in Dark
gleich aussehen).

## CSS-Klassen des Basis-CSS (`PageShell.Css`, gilt für Auth-/Admin-Seiten)

| Selektor/Klasse | Erscheinungsbild |
|-----------------|------------------|
| `.page` | `max-width: 1080px`, zentriert, `padding: 24px 20px 60px` |
| `header.top` | Flex-Zeile, dunkler Hintergrund (`--header-bg`), Radius 10px; `h1` flex:1, 20px |
| `section` | Karte: `--surface`-Hintergrund, `--border`-Rahmen, Radius 10px, `margin-top/bottom: 16px` |
| `section h1/h2` | 17px semibold; `section h3` 13px |
| `.muted` | `--muted`, 13px |
| `.msg` / `.msg.err` | Erfolgs-/Fehlerbox (ok-/err-Tokens), Radius 8px |
| `form label` | Block-Label, 13px semibold |
| `form input[type=text|password|number], form select` | Block, volle Breite bis 380px, Radius 6px |
| `form button` | Primär-Button `--accent`/`--button-fg`; `:hover` `--accent-hover`; `[disabled]` `--muted-faint` + `not-allowed` |
| `form.inline` | `display:inline`; Buttons kleiner (12px) |
| `a.action-link` | Sekundär-Button als Link (Rahmen, `--label`); `.danger`-Variante rot |
| `form button.danger` | Danger-Button `--danger-strong` |
| `table`, `td, th` | Volle Breite, `border-collapse`, Zeilen-Top-Border `--border-row`, `overflow-wrap: anywhere` (Mobil-Umbruch) |
| `.pill` + `.done`/`.err`/`.pause`/`.wait` | Status-Pillen 11px, Radius 999px |
| `a` | `--accent`, ohne Unterstrich |
| `.token-once` | Warnbox mit `<code>`-Block (dashed border `--warn-strong`) für einmalige Token-Anzeige |
| `.empty` | Leer-Zustandstext |
| `footer` | Zentrierte, blasse Fußzeile |

## Navigations-CSS (`NavCss`) und Navigationsstruktur

`nav.topnav`: Flex-Zeile mit `gap:14px`, Karten-Optik wie `section`. Kinder:
`a.nav-link` (Accent, semibold), `span.nav-user` (rechts via `margin-left:auto`),
`form.nav-logout` (inline POST-Formular mit Button — JS-freies Abmelden).

`AppendNav` rendert in dieser Reihenfolge:

1. `Status` → `{BasePath}/`
2. `Profil` → `{BasePath}/profile`
3. Nur bei `IsAdmin`: `Benutzer` → `/admin/users`, `Tokens` → `/admin/tokens`, `Einstellungen` → `/admin/settings`
4. `<span class="nav-user">{UserName}</span>` — bei Admin zusätzlich ` · Administration`
   (Überwacher erhalten **keinen** Rollenzusatz in der Nav)
5. `<form class="nav-logout" method="post" action="{BasePath}/logout">` + Hidden-Antiforgery + Button „Abmelden"

## `PageUserContext` (Record)

`server/Softwareschmiede.StatusServer/Pages/PageUserContext.cs` — Kontext für Nav und
Formulare, wird je Request in `EndpointSecurityExtensions.BuildUserContextAsync`
(`Endpoints/EndpointSecurityExtensions.cs:18`) erzeugt:

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `UserName` | `string?` | Anzeigename in `nav-user` und Profil |
| `IsAdmin` | `bool` | Sichtbarkeit der Admin-Links |
| `AntiforgeryToken` | `string` | Wert des Hidden-Felds aller POST-Formulare |
| `AntiforgeryFieldName` | `string` | Feldname des Antiforgery-Tokens |
| `Theme` | `ThemePreference` | Wirksames `data-theme` der Seite |

## `ThemePreference` (Enum)

`server/Softwareschmiede.StatusServer/Data/ThemePreference.cs` — Ordinalwert in
`AspNetUsers.Theme`: `Auto = 0` (folgt `prefers-color-scheme`, Fallback Light),
`Light`, `Dark`. Persistiert wird sie via `POST /profile/theme` (Profil).
`ApplicationUser.Theme` (`Data/ApplicationUser.cs:14`) ist der Speicherort;
Default `Auto`.
