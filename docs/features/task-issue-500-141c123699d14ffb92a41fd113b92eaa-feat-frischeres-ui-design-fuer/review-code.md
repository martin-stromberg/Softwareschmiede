# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### generate-previews.ps1 (New-Composite / Ausgabe-Encoding)

- **Fehlerbehandlung / Korrektheit der generierten Artefakte** — Alle 16 erzeugten `*.composite.html`-Dateien enthalten Mojibake im `<title>`: `… â€“ Light + Dark` (Bytes `C3 A2 E2 82 AC E2 80 9C`). Ursache: `generate-previews.ps1` ist als UTF-8 **ohne BOM** gespeichert; Windows PowerShell 5.1 liest BOM-lose Skripte als ANSI (cp1252), wodurch der En-Dash `–` im Here-String-Literal (Zeile 50) bereits beim Parsen zu `â€“` verstümmelt und anschließend als UTF-8 weggeschrieben wird. Unter PowerShell 7 würde die Datei korrekt als UTF-8 gelesen — die produzierten Artefakte sind aber falsch und werden es bei jedem Lauf unter PS 5.1 bleiben.

  Empfehlung: In Zeile 50 einen ASCII-Bindestrich verwenden (`<title>$Name - Light + Dark</title>`) oder das Skript als UTF-8 **mit** BOM speichern (damit PS 5.1 es korrekt einliest); anschließend die Composites neu generieren. Die Kommentar-Zeichen `—`/`→` (Zeilen 5, 8, 9, 30) sind unkritisch, da sie nicht ausgegeben werden.

- **Namenskonventionen und Einheitlichkeit / Dokumentationsabweichung** — `Set-Content -Encoding utf8` schreibt unter Windows PowerShell 5.1 eine UTF-8-BOM. Alle generierten Dateien (`*.dark.html`, `*.composite.html`, `*.mobil.composite.html`) beginnen daher mit `EF BB BF`, während die kanonischen Dateien BOM-los sind. Das README behauptet für `*.dark.html` „identischer Inhalt, `data-theme="light"` → `"dark"`" — der Dateiinhalt weicht damit um mehr als nur das Attribut ab (BOM vor `<!doctype`, zudem fehlt die abschließende Newline durch `-NoNewline`).

  Empfehlung: Ausgabe BOM-los schreiben, z. B. `[System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))` (funktioniert in PS 5.1 und PS 7), und regenerieren — oder die BOM-Abweichung im README explizit dokumentieren. Die BOM vor `<!doctype` ist für Browser unschädlich, daher niedrige Priorität.

### *.composite.html (generierte Wrapper)

- **HTML-Validität** — Das `<html>`-Element der Composite-Wrapper trägt kein `lang`-Attribut (`<html><head>…`). Da die Wrapper eigenständige Dokumente sind, die im Browser geöffnet werden (Screenshot-Renderings, manuelle Vorschau), gehört auch hier `lang="de"` hin.

  Empfehlung: In `New-Composite` das Template zu `<html lang="de">` ergänzen und regenerieren.

### admin-tokens.html

- **Randfälle bei datenabhängigen Prüfungen / Beispieldaten-Fidelity** — Der gezeigte Token-Klartext `s9vXQ2mZkR8jN4wTtP0oL7bH5yUcA3eF6gI1dKzWnJ` (Zeile 30) hat 42 Zeichen; produktiv erzeugt `RelayTokenService` 32 Zufallsbytes als Base64url ohne Padding = **43 Zeichen** (`server/Softwareschmiede.StatusServer/Security/RelayTokenService.cs:44`, auch in `inventory/admin-pages.md` dokumentiert: „43 Zeichen, Base64url").

  Empfehlung: Den Beispiel-Klartext um ein Zeichen auf 43 Zeichen verlängern (und `admin-tokens.dark.html` neu generieren), damit das Mockup die reale Ausgabe widerspiegelt.

## Geprüfte Dateien

Liste aller geprüften Dateien:

- `docs/design/status-webseite/theme.css`
- `docs/design/status-webseite/site.css`
- `docs/design/status-webseite/status.html`
- `docs/design/status-webseite/status-leer.html`
- `docs/design/status-webseite/login.html`
- `docs/design/status-webseite/register.html`
- `docs/design/status-webseite/profil.html`
- `docs/design/status-webseite/admin-benutzer.html`
- `docs/design/status-webseite/admin-benutzer-loeschen.html`
- `docs/design/status-webseite/admin-tokens.html`
- `docs/design/status-webseite/admin-tokens-leer.html`
- `docs/design/status-webseite/admin-token-widerrufen.html`
- `docs/design/status-webseite/admin-einstellungen.html`
- `docs/design/status-webseite/admin-update-installation.html`
- `docs/design/status-webseite/zugriff-verweigert.html`
- `docs/design/status-webseite/formular-abgelaufen.html`
- `docs/design/status-webseite/generate-previews.ps1`
- `docs/design/status-webseite/*.dark.html` (14 generierte Dateien, Diff gegen kanonische Dateien)
- `docs/design/status-webseite/*.composite.html` + `*.mobil.composite.html` (16 generierte Dateien)
- `docs/design/status-webseite/screenshots/*.png` (16 Dateien, Dimensions- und Stichproben-Sichtprüfung: status.png, admin-benutzer.png, admin-einstellungen.png, status-mobil.png)
- `docs/design/status-webseite/README.md`

Geprüft, ohne Befund: JS-Freiheit (kein `<script>`, keine `on*`-Handler), keine `style=`-Attribute, keine Hex-Literale außerhalb `theme.css` (nur `#<nr>`-Issue-Referenzen), `method="post"` + `__RequestVerificationToken` in jedem mutierenden Formular (verifiziert per Skript), produktive Routen als `action`, korrekte Feldnamen (`username`, `password`, `confirmPassword`, `currentPassword`, `newPassword`, `theme`, `role`, `label`, `minutes`, `channel`, `mode`, `installTime`, `confirmDowntime`), Token-Namen 1:1 zu `PageShell.ThemeCss` mappbar inkl. dokumentierter Ergänzungstokens, `lang="de"` + Viewport-Meta in allen kanonischen Dateien, `aria-current="page"` in den Navs, SCM-Event-Duplikat in „Wartet auf Anwender" + „Letzte SCM-Ereignisse" entspricht produktivem Verhalten (`waitingEvents.Count` + `events.Count`), `name="minutes"` korrekt gemäß `inventory/admin-pages.md` (der Prüfauftrag nannte `sessionMinutes` — das produktive Feld heißt `minutes`).
