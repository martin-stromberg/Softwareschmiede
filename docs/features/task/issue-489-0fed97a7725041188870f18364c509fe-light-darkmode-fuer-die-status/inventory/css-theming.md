# CSS / Theming-Istzustand

Drei eingebettete CSS-Blöcke liefern das komplette Styling; es gibt **keine** externen Stylesheets, kein JavaScript, keine CSS Custom Properties, keine `prefers-color-scheme`-Media-Query, kein `data-theme`-Attribut und keine `color-scheme`-Deklaration (Repo-weite Suche: 0 Treffer in `server/`). Alle Farben sind literale Hex-Werte einer Light-Palette.

## `PageShell.Css` (private const, Z. 35–84 in `Pages/PageShell.cs`)

Gilt für alle `BeginPage`-Seiten (Login, Registrierung, Profil, Access-Denied, Formular-abgelaufen, alle Admin-Seiten).

| Selektor | Farbwerte (Hex) |
|----------|-----------------|
| `body` | `background: #f4f5f7`, `color: #1f2430` |
| `header.top` | `background: #1f2937` (dunkler Header), `color: #f8fafc` |
| `section` | `background: #fff`, `border: 1px solid #e5e7eb` |
| `.muted` | `#6b7280` |
| `.msg` | `background: #d1fae5`, `border: #34d39955`, `color: #065f46` |
| `.msg.err` | `background: #fee2e2`, `border: #f8717155`, `color: #991b1b` |
| `form label` | `color: #374151` |
| `form input[*], form select` | `border: #d1d5db` |
| `form button` | `background: #4f46e5`, `color: #fff`; `:hover #4338ca`; `[disabled] #9ca3af` |
| `a.action-link` | `background: #fff`, `border: #d1d5db`, `color: #374151`; `:hover #f3f4f6`; `.danger` `color: #991b1b`, `border: #f87171`, `:hover #fee2e2` |
| `form button.danger` | `#b91c1c`; `:hover #991b1b` |
| `td, th` | `border-top: #f0f1f3` |
| `.pill.done/.err/.pause/.wait` | `#d1fae5/#065f46`, `#fee2e2/#991b1b`, `#e5e7eb/#4b5563`, `#fef3c7/#92400e` |
| `a` | `#4f46e5` |
| `.token-once` | `background: #fef3c7`, `border: #f59e0b55`, `color: #92400e`; `code` `background: #fff`, `border: #f59e0b`, `color: #1f2430` |
| `.empty` | `#6b7280` |
| `footer` | `#9ca3af` |

## `PageShell.NavCss` (internal const, Z. 23–33 in `Pages/PageShell.cs`)

Wird von `PageShell.BeginPage` **und** `StatusPageComposer.Render` eingebettet (Z. 92 bzw. 43).

| Selektor | Farbwerte (Hex) |
|----------|-----------------|
| `nav.topnav` | `background: #fff`, `border: #e5e7eb` |
| `nav.topnav a.nav-link` | `#4f46e5` |
| `nav.topnav .nav-user` | `#6b7280` |
| `nav.topnav form.nav-logout button` | `border: #d1d5db`, `color: #374151`; `:hover #f3f4f6` |

## `StatusPageComposer.Css` (private const, Z. 341–395 in `Status/StatusPageComposer.cs`)

Eigenes CSS für die Status-Seite — teils deckungsgleich mit `PageShell.Css` (`body`, `.page`, `header.top`, `section`, `footer`, Pills, Links), teils status-spezifisch:

| Selektor | Farbwerte (Hex) |
|----------|-----------------|
| `.badge.online` | `background: #065f46`, `color: #d1fae5`; `.dot #34d399` |
| `.badge.offline` | `background: #7f1d1d`, `color: #fee2e2`; `.dot #f87171` |
| `.badge.stale` | `background: #78350f`, `color: #fef3c7`; `.dot #fbbf24` |
| `.meta-line` | `#6b7280` |
| `.src` | `color: #fff`; `.src.app #4f46e5`, `.src.gh #0d9488` |
| `.count`, `td.muted`, `.inst`, `.empty`, `.dim td` | `#6b7280` |
| `td.num` | `#4f46e5` |
| `.pill.run` | `#e0e7ff/#3730a3` (weitere Pills wie `PageShell.Css`) |
| `.stale-note` | `background: #fef3c7`, `border: #f59e0b55`, `color: #92400e` |
| `@media (max-width: 600px)` | Layout-Query — einzige vorhandene Media-Query (responsiv, nicht farblich) |

## Inline-Styles im gerenderten Markup

Exakt zwei `style=`-Attribute im Produktivcode (Suche `style=` in `server/Softwareschmiede.StatusServer/`):

| Stelle | Code | Kontext |
|--------|------|---------|
| `StatusPageComposer.cs` Z. 90 | `<span class="inst" style="color:#cbd5e1">` | Instanz-Tag im dunklen `header.top` der Status-Seite |
| `StatusPageComposer.cs` Z. 288 | `<h3 class="count" style="margin-top:12px">` | Überschrift „Letzte Releases" in GitHub-Sektionen |

## HTML-Dokument-Skelette (Einbauorte eines Theme-Attributs)

Zwei getrennte Skelette — ein `data-theme`-/`color-scheme`-Anker müsste an beiden Stellen greifen:

- `PageShell.BeginPage` (Z. 87–94): `<!doctype html><html lang="de"><head><meta charset="utf-8"><meta name="viewport" …><title>…</title><style>Css + NavCss</style></head><body><div class="page">` — Parameter: `sb`, `title` (kein Theme/Benutzer).
- `StatusPageComposer.Render` (Z. 39–44): gleiches Grundgerüst plus `<meta http-equiv="refresh" content="{n}">`; eigene `Css` + `PageShell.NavCss`; der `PageUserContext user` ist als Parameter vorhanden.
