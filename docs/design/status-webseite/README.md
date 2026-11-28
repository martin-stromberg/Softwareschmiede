# UI-Design-Entwurf: Status-Webseite (Issue #500)

Statische, JS-freie HTML/CSS-Mockups aller zwölf Ansichten des StatusServers
(plus zwei Leer-Zustände) auf einem gemeinsamen neuen Design-System —
kühles Navy-Slate-System mit Teal-Akzent, Light- und Dark-Variante.
Erstellt als Entwurf zur Abnahme; der Produktivcode (`PageShell`, Composer,
Endpunkte) ist unverändert — die Umsetzung erfolgt separat nach Bestätigung.

## Struktur

Kanonisch (manuell gepflegt):

| Datei | Inhalt |
|-------|--------|
| `theme.css` | Farbwelt als CSS Custom Properties — Light als `:root`-Default, Dark unter `:root[data-theme="dark"]` |
| `site.css` | Komponenten-/Layout-CSS (Topbar/Branding, Nav, Karten, Tabellen mit Mobil-Umbruch, Pills, Formulare, Hinweisboxen) |
| `status.html` | Status-Übersicht `/`, befüllt: 2 Instanzen (build-server-01 aktuell, laptop-martin stale → `dim`-Sektionen + `.stale-note`), 2 GitHub-Repos (ok + Abruffehler-`.stale-note`) |
| `status-leer.html` | `/` ohne Instanzen (`.stale-note`-Anleitung + `.empty`) |
| `login.html` | `/login` mit `.msg err`-Sperr-Box inkl. Restdauer-Text; bewusst **kein** Registrierungs-Link (Anwender-Entscheidung) |
| `register.html` | `/register` Ersteinrichtung |
| `profil.html` | `/profile` mit `.msg`-Erfolgsbox + Theme-Auswahl (auto/light/dark) + Passwort-Formular |
| `admin-benutzer.html` | `/admin/users` — letzter-Admin-Pill + deaktivierte Schutz-Buttons mit `title` **und** sichtbarer `.action-note` in der Zelle; `.action-na`-Platzhalter „—" für nicht auslösbare Aktionen (Sperrung zurücksetzen bei ungesperrtem Konto); gesperrter + deaktivierter Benutzer, Anlege-Formular |
| `admin-benutzer-loeschen.html` | `GET /admin/users/{id}/delete` Bestätigungsseite |
| `admin-tokens.html` | `/admin/tokens` mit `.token-once`-Klartextblock (43 Zeichen, Base64url — wie `RelayTokenService`) + gültigem + widerrufenem Token (`.action-na` statt deaktiviertem Button) |
| `admin-tokens-leer.html` | `/admin/tokens` Leer-Zustand |
| `admin-token-widerrufen.html` | `GET /admin/tokens/{id}/revoke` Bestätigungsseite |
| `admin-einstellungen.html` | `/admin/settings` — Sitzungsdauer, Update-Status inkl. verwaister Installationssperre (Reset-Formular), Fehler-Pill, Kanal/Modus/Uhrzeit, Aktionen |
| `admin-update-installation.html` | Ergebnisseite „Der Server startet neu" |
| `zugriff-verweigert.html` | `/access-denied` (mit Nav eines Überwachers) |
| `formular-abgelaufen.html` | Antiforgery-400-Seite |
| `generate-previews.ps1` | Generator für die Artefakte unten |
| `screenshots/` | 16 gerenderte PNGs (siehe unten) |

Generiert (Build-Artefakte, nicht manuell editieren):

- `*.dark.html` — identischer Inhalt, `data-theme="light"` → `"dark"`, UTF-8 ohne BOM
- `*.composite.html` — zwei randlose iframes à 50 %: links Light, rechts Dark;
  `<html lang="de">`, UTF-8 ohne BOM, Titel bewusst ASCII (`Name - Light + Dark`,
  damit Windows PowerShell 5.1 kein Mojibake erzeugt — das Skript selbst ist
  UTF-8 mit BOM gespeichert)
- `status.mobil.composite.html`, `admin-benutzer.mobil.composite.html` —
  Varianten mit größerer Iframe-Höhe für die Mobil-Screenshots

## Regeneration + Screenshots

```powershell
cd docs/design/status-webseite
.\generate-previews.ps1               # dark + composite neu erzeugen
.\generate-previews.ps1 -Screenshots  # zusätzlich alle 16 PNGs rendern
```

Einzelner Screenshot (Desktop 1920 px, zwei Panes à ~950 px):

```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --headless --disable-gpu `
  --hide-scrollbars --force-device-scale-factor=1 `
  --screenshot="<abs>\screenshots\status.png" --window-size=1920,2120 `
  "file:///<abs>/status.composite.html"
```

Mobil-Nachweise: `status.mobil.composite.html` /
`admin-benutzer.mobil.composite.html` mit `--window-size=920,<MobileHeight>`
(Panes à ~450 px). Die Höhen stehen in `$Pages` im Skript (`Height`/
`MobileHeight`) und sind auf den Inhalt abgestimmt — nach Inhaltsänderungen
prüfen, dass nichts abgeschnitten ist und unten kaum Leerraum bleibt.

Ergebnis: 14 Ansichts-PNGs + `status-mobil.png` + `admin-benutzer-mobil.png`.

## Token-Mapping zu `PageShell.ThemeCss`

`theme.css` verwendet die Token-Namen aus
`server/Softwareschmiede.StatusServer/Pages/PageShell.cs` (`ThemeCss`/
`DarkThemeVars`): `--bg`, `--fg`, `--surface`, `--border`, `--border-input`,
`--border-row`, `--label`, `--muted`, `--muted-faint`, `--header-bg`,
`--header-fg`, `--inst-header`, `--accent`, `--accent-hover`, `--button-fg`,
`--danger*`, `--ok-*`, `--err-*`, `--warn-*`, `--pause-*`, `--hover`,
`--badge-*`, `--src-*`, `--pill-run-*`. In der Umsetzung können die
Werte-Blöcke direkt nach `ThemeCss`/`DarkThemeVars` übertragen werden;
`color-scheme` ist je Block gesetzt. Die produktive
`prefers-color-scheme`-Mechanik für `data-theme="auto"` entfällt in den
Mockups (Attribut steht explizit auf light/dark) — sie bleibt zu übernehmen.

Neue, zu ergänzende Tokens (im Kopf von `theme.css` dokumentiert):
`--radius`, `--radius-sm`, `--shadow`, `--shadow-sm`, `--header-brand`,
`--header-line`, `--accent-soft(-fg)`, `--ring`, `--th-bg`,
`--danger-strong-fg`.

## Bewusste Markup-Neuerungen gegenüber dem Ist-Markup

- `header.top` enthält `.brand-block` (Wortmarke „softwareschmiede" +
  CSS-Diamant `.brand-mark` + `.brand-tag` „status") mit `h1` und optional
  `.top-aside` für Badge/Instanz-Tags.
- Aktiver Nav-Link trägt `aria-current="page"` (neu, einfach aus der
  aktuellen Route setzbar).
- Zähler als Chip: `<span class="count">n</span>` statt `(n)`-Text.
- `td[data-label]` in Kopfzeilen-Tabellen — auf Mobil werden die Zellen
  blockiert und `td::before { content: attr(data-label) }` zeigt die
  Spalten-Beschriftung (CSS-only, kein JS).
- `.pill` erhält den Punkt-Indikator via `::before` (statt Markup-Element).
- `form.inline button` ist jetzt Ghost-Style (Rahmen), `a.action-link`
  dazu optisch gleich.
- Nicht auslösbare Aktionen erscheinen nicht als ausgegrauter Pseudo-Button:
  `.action-na` rendert einen muted „—"-Platzhalter (mit `title` als Zusatz),
  `.action-note` gibt den Blockade-Grund sichtbar als muted-Zeile in der
  Aktions-Zelle aus (Tooltip allein wäre auf Touch/Tastatur unsichtbar).

Interaktionsmodell unverändert JS-frei: Links + `form method="post"` mit
`__RequestVerificationToken`-Hidden-Feld, `action-link danger` →
Bestätigungsseiten, `disabled` + `title` für Schutz-Aktionen, produktive
Validierungsattribute (`required`, `minlength="8"`,
`pattern="[a-zA-Z0-9._@+-]+"`, `min`/`max`, `maxlength="200"`).

## Hinweis für die Umsetzungsphase

- **Randfall `GET /login`:** Direkter `GET /login` ohne vorhandenes
  Benutzerkonto rendert heute das Login-Formular; die Weiterleitung nach
  `/register` greift nur über `StatusServerCookieEvents.RedirectToLogin`
  bei geschützten Seiten (`Security/StatusServerCookieEvents.cs:113-122`).
  Für die Mockups ohne Konsequenz, bei der Code-Umsetzung zu prüfen.
- Die 35 StatusServer-Tests mit wörtlichen Markup-Assertions
  (`inventory/tests.md`, u. a. `class="msg"`, `badge online|offline|stale`,
  `action-link danger`, `token-once`, `disabled`-Muster, `data-theme`)
  bilden den Funktionsvertrag — beim Redesign anzupassen bzw. zu erhalten.
