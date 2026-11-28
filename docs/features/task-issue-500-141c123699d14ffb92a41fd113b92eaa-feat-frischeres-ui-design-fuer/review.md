# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Geprüft wurden alle 27 Aufgaben der Tasks-Datei bzw. alle Planelemente aus
`plan.md` gegen die Artefakte unter `docs/design/status-webseite/`.
26 von 27 Aufgaben sind vollständig umgesetzt — sämtliche Repo-Artefakte
(Design-System, 14 kanonische Mockups, generierte Dark-/Composite-Dateien,
16 Screenshots, README) liegen vollständig und nachweisbar geprüft vor.
Offen ist ausschließlich die Bereitstellung der Screenshots als Issue-Kommentar
(Aufgabe 27) — eine externe Aktion außerhalb des Repos.

## Umgesetzte Planelemente

### Design-System

- [x] `docs/design/status-webseite/` (Verzeichnis) — angelegt
- [x] `theme.css` — vorhanden: Light-Palette als `:root`-Default
  (`color-scheme: light`), Dark-Palette unter `:root[data-theme="dark"]`
  (`color-scheme: dark`); alle Farben als Custom Properties; Token-Namen
  decken die `PageShell.ThemeCss`-Gruppen ab (`--bg`, `--fg`, `--surface`,
  `--border`/`--border-input`/`--border-row`, `--label`, `--muted`,
  `--muted-faint`, `--header-*`, `--inst-header`, `--accent*`, `--button-fg`,
  `--danger*`, `--ok-*`, `--err-*`, `--warn-*`, `--pause-*`, `--hover`,
  `--badge-*`, `--src-*`, `--pill-run-*`); neue Ergänzungstokens
  (`--radius*`, `--shadow*`, `--header-brand`, `--header-line`,
  `--accent-soft(-fg)`, `--ring`, `--th-bg`, `--danger-strong-fg`) im Kopf
  dokumentiert; Hinweis auf die produktive `prefers-color-scheme`-Mechanik
  für die Umsetzungsphase enthalten
- [x] `site.css` — vorhanden mit allen geforderten Komponenten:
  `.page`, `header.top` + Branding (`.brand-block`, `.brand-mark`,
  `.brand-tag`, `.top-aside`), `nav.topnav` (inkl. `aria-current`-Styling,
  `.nav-user`, `form.nav-logout`), `.meta-line`, `section`-Karten,
  `.count`-Chip, `.src .app/.gh`, Tabellen mit `overflow-wrap` +
  `@media (max-width: 640px)`-Mobil-Umbruch via `td[data-label]::before`,
  `.badge .online/.offline/.stale` + `.dot`, `.pill` mit `::before`-Punkt
  (`.run/.wait/.done/.err/.pause`), `.inst`/`.inst.on-dark`, `.stale-note`,
  `.msg`/`.msg err`, `.token-once`, `.empty`, `.dim`, `a.action-link(.danger)`,
  Formular-Styles inkl. Fokus-Ring, `form.inline`-Ghost-Buttons,
  `button.danger`, `footer` — kein Farb-Literal im gesamten Stylesheet
  (einziger `#…`-Treffer ist die Issue-Nummer `#500` im Kommentar)

### Kanonische Mockup-HTML-Dateien (14/14)

Alle Dateien: vollständiges Dokument `<!doctype html><html lang="de"
data-theme="light">`, Viewport-Meta, `<link>` auf `theme.css` + `site.css`,
produktive Routen als root-absolute Pfade (auf `{BasePath}` mappbar),
`__RequestVerificationToken`-Hidden-Feld in jedem POST-Formular,
JS-frei (0× `<script`), keine `style=`-Attribute.

- [x] `status.html` — Gesamt-Badge `stale` „teilweise veraltet", zwei
  `inst on-dark`-Tags, Meta-Zeile mit `/status.json`-Link und
  `<meta http-equiv="refresh" content="15">`, Instanz build-server-01
  (aktuell: Laufende Aufgaben/Wartet auf Anwender/Letzte SCM-Ereignisse
  mit `count`, `src app`, `inst`, Run-Pills `run`/`wait`/`pause`,
  SCM-Pills `done`/`wait`/`err`, PR-/Issue-Links mit `↗`), Instanz
  laptop-martin (stale: `.stale-note` + drei `section.dim`), GitHub-Repo
  softwareschmiede (fehlerfrei: PR-Tabelle + Release-Tabelle,
  `src gh`, Issue-Zähler), GitHub-Repo softwareschmiede-docs
  (`.stale-note` Abruffehler + Alt-/Leerdaten mit `.empty`)
- [x] `status-leer.html` — Badge `offline` „keine Instanz",
  `.stale-note`-Anleitung (Einstellungen → Automatisierung →
  Remote-Übersicht), `section` mit `.empty`, weiterhin gerenderte
  GitHub-Sektion (entspricht dem produktiven Verhalten: GitHub-Sektionen
  sind instanzunabhängig)
- [x] `login.html` — `.msg err` mit exakt dem geforderten Sperr-Text
  inkl. Restdauer („…endet automatisch nach einer Stunde…"), Formular
  `POST /login` (username/password `required` + `autocomplete`, Button
  „Anmelden", Hidden-Antiforgery), **kein** Registrierungs-Link,
  **keine** Nav (anonyme Seite)
- [x] `register.html` — Erklärtext Ersteinrichtung, `username` mit
  `pattern="[a-zA-Z0-9._@+-]+"` + `title`-Hint + `p.muted`-Hinweis,
  `password` mit `minlength="8"`, `confirmPassword`, Button
  „Administratorkonto anlegen", keine Nav
- [x] `profil.html` — Konto-Block („Angemeldet als martin · Stufe
  Administration"), `.msg`-Erfolgsbox „Das Passwort wurde geändert.",
  `POST /profile/theme` mit Select auto/light/dark (auto `selected`),
  `POST /profile/password` (currentPassword/newPassword `minlength="8"`/
  confirmPassword), Nav mit `form.nav-logout` POST `/logout`
- [x] `admin-benutzer.html` — Tabelle mit Kopfzeile, drei Beispielbenutzer:
  letzter Admin (`pill pause` „letzter Admin" + alle vier Aktionen
  `disabled` mit `title`-Tooltips, Löschen als `form.inline` +
  `button type="button" disabled` — exakt das produktive Muster),
  gesperrter Überwacher (`pill wait` „gesperrt bis … UTC", aktive
  „Sperrung zurücksetzen"), deaktivierter Benutzer (`pill err`,
  „Aktivieren"-Button); vier Inline-Aktionen je Zeile, Löschen als
  `a.action-link danger` auf `…/{id}/delete`; Formular „Benutzer anlegen"
  mit pattern-Hint, `minlength="8"`, `role`-Select
- [x] `admin-benutzer-loeschen.html` — `h1` „Benutzer „stefan.b“
  löschen?", Warnhinweis „unwiderruflich", POST auf denselben Pfad,
  `button.danger` „Endgültig löschen" + Abbrechen-Link
- [x] `admin-tokens.html` — `.token-once`-Block mit Hinweistext +
  43-stelligem `<code>`-Klartext, Tabelle gültig (`pill done` +
  `action-link danger` Widerrufen) + widerrufen (`pill err` „widerrufen
  … UTC" + `form.inline`/`button disabled`), Formular „Token anlegen"
  (`name="label" required maxlength="200"`)
- [x] `admin-tokens-leer.html` — `.empty` mit `<code>/relay</code>`-
  Ablehnungshinweis + Anlege-Formular
- [x] `admin-token-widerrufen.html` — `h1` „Token „Laptop Martin“
  widerrufen?", Warnhinweis (Instanzen verlieren Zugang, neues Token
  nötig), POST auf denselben Pfad, `button.danger` „Token endgültig
  widerrufen" + Abbrechen-Link
- [x] `admin-einstellungen.html` — Sitzungsdauer-Formular
  (`input type="number" min="5" max="43200"`, POST
  `/admin/settings/session-duration`), Update-Statustabelle Feld|Wert
  (Installierte Version, Zustand „Update verfügbar" als Text, Verfügbare
  Version, Letzte Prüfung, Letzter Fehler als `pill err`,
  Installationssperre `pill pause` „verwaist — Reset möglich",
  Update-Protokoll als `<code>`-Pfad), Reset-Formular POST
  `/admin/settings/update/lock-reset`, „Kanal und Automatisierung"
  (channel-/mode-Selects + `input type="time"`, POST
  `/admin/settings/update`), Aktionen (POST `…/check` + POST
  `…/install` mit `confirmDowntime`-Checkbox + `button.danger`)
- [x] `admin-update-installation.html` — statische Seite „Der Server
  startet neu" mit Erklärung + Update-Protokoll-Pfad, ohne Redirect
- [x] `zugriff-verweigert.html` — Begründungstext + Link „Zur
  Status-Übersicht"; Nav korrekt als Überwacher ohne `/admin/*`-Links
- [x] `formular-abgelaufen.html` — Erklärtext + Neuladen-Hinweis +
  Link, ohne Nav (anonym möglich) — entspricht `RenderFormExpired`

### Generator und generierte Artefakte

- [x] `generate-previews.ps1` — erzeugt je kanonischer Datei
  `*.dark.html` (Replace `data-theme="light"` → `"dark"` mit
  Validierung) und `*.composite.html` (zwei randlose `<iframe>` à 50 %,
  links kanonisch, rechts dark); `-Screenshots`-Schalter rendert die
  PNGs per Headless-Chrome (1920 px Desktop, 920 px Mobil); Höhen je
  Seite in `$Pages` konfigurierbar — exakt der geplante Ansatz
- [x] 14× `*.dark.html` — alle geprüft mit `data-theme="dark"`
- [x] 16× `*.composite.html` — 14 Desktop-Wrapper + 2 Mobil-Wrapper
  (`status.mobil.composite.html`, `admin-benutzer.mobil.composite.html`)

### Screenshots (16/16, alle einzeln sichtgeprüft)

- [x] 14 Ansichts-PNGs (1920 px breit, valide PNG-Dateien): status,
  status-leer, login, register, profil, admin-benutzer,
  admin-benutzer-loeschen, admin-tokens, admin-tokens-leer,
  admin-token-widerrufen, admin-einstellungen,
  admin-update-installation, zugriff-verweigert, formular-abgelaufen —
  jeweils Light links / Dark rechts mit identischem Inhalt, kein
  Farbrest im falschen Schema, keine abgeschnittenen Inhalte, alle
  geforderten Nebenzustände sichtbar (Leer-Zustand, `.token-once`,
  letzter-Admin-Schutz mit deaktivierten Buttons, verwaiste Sperre +
  Reset + Fehler-Pill, GitHub-Abruffehler, `.msg err` in login.png,
  `.msg` in profil.png)
- [x] `status-mobil.png` (920×5060) + `admin-benutzer-mobil.png`
  (920×1430): Tabellen stapeln via `data-label`-Beschriftung zu Karten,
  lesbar, kein horizontaler Überlauf

### Qualitäts-/Sichtprüfungen

- [x] JS-Freiheit: 0× `<script` in allen 14 kanonischen Dateien
- [x] Mutations-Modell: jedes zustandsändernde Formular `method="post"`
  + Hidden-`__RequestVerificationToken`; deaktivierte Schutz-Aktionen
  als `form.inline` + `button type="button" disabled` ohne POST-Ziel —
  entspricht exakt dem produktiven Muster (`AppendDeleteAction`,
  widerrufenes Token)
- [x] Keine Inline-Farbwerte: 0× `style=` in kanonischen Dateien,
  keine Hex-Literale in `site.css`
- [x] Scope-Ausschluss eingehalten: `git status` zeigt keine Änderungen
  unter `src/` oder `server/` — Produktivcode unverändert
- [x] `docs/design/status-webseite/README.md` — vollständig: Struktur-
  Tabelle, Generiert-vs-Kanonisch-Abgrenzung, Regenerations- und
  Screenshot-Befehle, Token-Mapping zu `PageShell.ThemeCss` inkl. Liste
  der neuen Tokens, dokumentierte Markup-Neuerungen, Hinweis Randfall
  `GET /login` für die Umsetzungsphase

## Offene Aufgaben

- [ ] Issue-Kommentar mit den 16 PNGs erstellen — fehlt: Issue #500
  enthält drei Kommentare (Analyse des Bots, Anwender-Antworten zu den
  offenen Fragen, automatische Eingangsbestätigung dieses Laufs); kein
  Kommentar stellt die Screenshots als Bildanhänge vor. Laut Plan
  (Programmablauf „Bereitstellung und Issue-Kommentar" / Tasks #27)
  sollen die 16 PNGs je Ansicht mit Kurzbeschreibung der gezeigten
  Zustände im Issue vorgestellt und per Kommentardiskussion abgenommen
  werden.

## Hinweise

- Die geforderte Korrekturschleife (Sichtprüfung → ggf. Nachbesserung)
  ist nicht mehr nötig: Alle 16 PNGs wurden im Rahmen dieses Reviews
  einzeln geöffnet und sind ohne Layout-Defekte.
- `status.html` zeigt für Instanz laptop-martin die Stale-Variante
  („liefert keinen Snapshot" + `dim`-Sektionen) — der Plan ließ
  „stale/offline" offen; die Offline-Variante („offline seit …") ist
  durch denselben `.stale-note`-Mechanismus abgedeckt. Keine Lücke.
- Kosmetik am Rand: In den generierten `*.composite.html`-Titeln steht
  `â€“` statt „–" (UTF-8-Fehlkodierung des Skript-Ausgabestrings) —
  betrifft nur den nicht sichtbaren `<title>` der Wrapper-Dateien, kein
  Screenshot-Inhalt. Bei einer Nachbesserung des Skripts optional
  korrigierbar.
- Die Aufgabe „Issue-Kommentar" ist die einzige verbleibende und hängt
  nicht von weiteren Repo-Artefakten ab — sie kann direkt aus dem
  vorliegenden `screenshots/`-Verzeichnis bedient werden.
