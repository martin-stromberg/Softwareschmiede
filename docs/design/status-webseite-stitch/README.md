# Status-Webseite — Design-Entwurf via Google Stitch

Alternativer Design-Entwurf für Issue #500, vollständig mit **Google Stitch** generiert
(vom bisherigen Layout unabhängig). Der manuell erstellte Vergleichsentwurf liegt in
`docs/design/status-webseite/`.

## Stitch-Artefakte

- **Projekt:** `projects/7540435568831394111` ("Softwareschmiede Status-Webseite Redesign (Issue 500)")
- **Design-System:** `assets/25e6f8dbb3604bef8ac4be3fa1415efc` — "Precision Operations"
  (Light, Inter + JetBrains Mono-Labels, Primary `#2563EB`, datendichtes Dashboard)
- **API:** `POST https://stitch.googleapis.com/mcp` (JSON-RPC, `X-Goog-Api-Key`-Header;
  Key in der Benutzer-Umgebungsvariable `GOOGLE_STITCH`)

## Dateien

Pro Ansicht:

- `{slug}.png` — Light-Screenshot (von Stitch gerendert)
- `{slug}.dark.png` — Dark-Variante (via `generate_variants`, `COLOR_SCHEME`/`REFINE`)
- `{slug}.composite.png` — Light|Dark nebeneinander (lokal erzeugt, für Issue-Nachweis)
- `{slug}.html` — generierter HTML-Code (Referenz für die spätere Umsetzung)
- `_gen_{slug}.json` — rohe Generate-Antwort inkl. Screen-IDs

Ansichten (14): status, status-leer, login, register, profil, admin-benutzer,
admin-benutzer-loeschen, admin-tokens, admin-tokens-leer, admin-token-widerrufen,
admin-einstellungen, admin-update-installation, zugriff-verweigert, formular-abgelaufen.

Hinweis: `status.html` fehlt (erster Screen — HTML nicht separat abgerufen).

Die `.html`-Dateien sind statische Stitch-Exporte und **keine** lauffähigen Razor-Seiten;
sie dienen als visuelle/semantische Vorlage für die Umsetzung auf die `PageShell`-Token
aus `StatusServer` (#489).
