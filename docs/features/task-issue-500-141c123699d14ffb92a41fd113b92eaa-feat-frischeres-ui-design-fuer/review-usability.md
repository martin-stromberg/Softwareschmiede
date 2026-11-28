# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### admin-benutzer.html (Benutzerverwaltung `/admin/users`)

- **Unerklärte Blockade** — Beim letzten aktiven Administrator („martin") sind die Schutz-Aktionen „Zu Überwacher herabstufen", „Deaktivieren" und „Löschen" deaktiviert; die Begründung steht ausschließlich im `title`-Attribut (Hover-Tooltip). Dieselbe Mechanik gilt beim bereits widerrufenen Token in `admin-tokens.html` („Widerrufen" deaktiviert) und bei „Sperrung zurücksetzen" für nicht gesperrte Benutzer — dort fehlt sogar jeglicher `title`, die Schaltfläche ist komplett ohne erkennbaren Grund ausgegraut. Ein `title`-Tooltip ist auf Touch-Geräten (Mobil-Screenshots sind Teil des Entwurfs) und für Tastatur-Bedienung praktisch unsichtbar; auch am Desktop muss die Anwenderin ihn erst durch Mausverweilen entdecken. Die Pille „letzter Admin" gibt zwar Kontext, verknüpft ihn aber nicht sichtbar mit den ausgegrauten Aktionen.

  Empfehlung: Blockade-Grund sichtbar im Markup ausgeben — z. B. eine kurze `muted`-Hinweiszeile in der Aktionen-Zelle („Letzter aktiver Administrator — kann nicht geändert oder gelöscht werden.") oder unter der Tabelle; für „Sperrung zurücksetzen" bei nicht gesperrten Konten entweder `title` ergänzen („Nur möglich, solange das Konto gesperrt ist.") oder die Aktion im nicht gesperrten Zustand gar nicht anbieten; den deaktivierten „Widerrufen"-Button bei bereits widerrufenem Token weglassen (die Status-Pille „widerrufen …" sagt bereits alles). Der `title` kann zusätzlich bleiben, darf aber nicht die einzige Erklärung sein.

### login.html, register.html, zugriff-verweigert.html, formular-abgelaufen.html (Einzelzweck-Seiten)

- **Erreichbarkeit** — Auf diesen Seiten steht dieselbe Überschrift zweimal unmittelbar übereinander: der `header.top`-`h1` („Anmeldung", „Ersteinrichtung", „Zugriff verweigert", „Formular abgelaufen") wird wortgleich als `section`-`h1` wiederholt. In den Screenshots ist das als redundant aussehender Doppel-Titel erkennbar — wirkt ungeordnet und lässt die eigentliche Karte wie eine Wiederholung erscheinen, ohne Informationsgewinn. Die Bestätigungsseiten (`admin-benutzer-loeschen.html`, `admin-token-widerrufen.html`) zeigen das bessere Muster bereits vor: generischer Titel im Kopf, konkrete Frage („Benutzer „stefan.b" löschen?") in der Karte.

  Empfehlung: Auf Einzelzweck-Seiten den `section`-`h1` entweder streichen (die Kopfzeile trägt den Titel bereits) oder in einen konkreten, handlungsorientierten Text umformulieren — analog zu den Bestätigungsseiten.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Anmeldung (`/login`) inkl. Fehler-/Sperr-Hinweis → unauffällig (Sperr-Box mit verständlichem Text und Restdauer-Angabe; bewusst kein Registrierungs-Link — entspricht Anforderung)
- Ersteinrichtung (`/register`): Administratorkonto anlegen → unauffällig (Erklärtext, Zeichen-Regel sichtbar unter dem Feld, Passwort-Mindestlänge im Label) — nur Doppel-Überschrift (s. Befund 2)
- Profil (`/profile`): Farbschema-Auswahl (Automatisch/Light/Dark per Dropdown), Passwort ändern → unauffällig
- Abmeldung (POST-Button in der Nav) → unauffällig
- Navigation Status/Profil/Benutzer/Tokens/Einstellungen mit aktivem Zustand (`aria-current`) → unauffällig
- Status-Übersicht (`/`): Status-Pills (Punkt + Text), Quellen-Badges, Zähler-Chips, abgedimmte Stale-Sektionen mit erklärendem Hinweisbalken, GitHub-Fehlerhinweis, Links zu Issues/PRs/Releases → unauffällig
- Status-Übersicht Leer-Zustand: Anleitungstext, wo die Remote-Übersicht aktiviert wird → unauffällig
- Benutzerverwaltung: Benutzer anlegen (Name, Passwort, Stufen-Dropdown mit Klartext), befördern/herabstufen, aktivieren/deaktivieren, Sperrung zurücksetzen, löschen → Befund vorhanden (deaktivierte Schutz-Aktionen nur per Tooltip erklärt, teils ganz ohne Grund — s. Befund 1)
- Bestätigungsseite „Benutzer löschen" (GET-Zwischenseite, Danger-Button + Abbrechen-Link) → unauffällig
- Token-Verwaltung: Token anlegen, Einmal-Klartext-Hinweisblock, Widerrufen → Befund vorhanden (deaktivierter „Widerrufen"-Button bei bereits widerrufenem Token ohne Erklärung — Teil von Befund 1)
- Bestätigungsseite „Token widerrufen" → unauffällig
- Einstellungen (`/admin/settings`): Sitzungsdauer, Update-Status inkl. verwaister Installationssperre (Pille „verwaist — Reset möglich" + Reset-Button), Kanal/Modus/Uhrzeit, „Jetzt nach Update suchen", „Update jetzt installieren" mit Downtime-Checkbox → unauffällig (Blockade fehlender Checkbox-Bestätigung wird server-seitig als Fehlermeldung gezeigt — zulässiges JS-freies Muster; Pille erklärt die verwaiste Sperre verständlich)
- Ergebnisseite „Update wird installiert" → unauffällig
- Zugriff verweigert (`/access-denied`, Überwacher-Nav ohne Admin-Links, Erklärtext + Rück-Link) → unauffällig — nur Doppel-Überschrift (s. Befund 2)
- Formular-abgelaufen-Seite (Erklärtext + Rück-Link) → unauffällig — nur Doppel-Überschrift (s. Befund 2)
- Mobiler Umbruch (≤ 640 px, `status-mobil.png`, `admin-benutzer-mobil.png`): Tabellen werden zu gestapelten Karten mit `data-label`-Beschriftung → unauffällig
- Lesbarkeit Light/Dark: Screenshots bestätigen ausreichende Kontraste für Pills, Badges, Hinweisboxen, Ghost-Buttons → unauffällig
- Interne Kennungen als Eingabe: keine — sämtliche Formulare verlangen nur Klartext-Eingaben (Benutzername, Passwort, Bezeichnung, Zahlenwerte); Instanz-Ids/GUIDs erscheinen nur als abgekürzte Anzeige bzw. in Formular-`action`-URLs, nie als Eingabefeld → unauffällig

## Geprüfte Dateien

Kanonische Mockups (14) + gemeinsame Styles:

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

Gerenderte Screenshots (16, je Light + Dark nebeneinander):

- `docs/design/status-webseite/screenshots/status.png`
- `docs/design/status-webseite/screenshots/status-leer.png`
- `docs/design/status-webseite/screenshots/status-mobil.png`
- `docs/design/status-webseite/screenshots/login.png`
- `docs/design/status-webseite/screenshots/register.png`
- `docs/design/status-webseite/screenshots/profil.png`
- `docs/design/status-webseite/screenshots/admin-benutzer.png`
- `docs/design/status-webseite/screenshots/admin-benutzer-mobil.png`
- `docs/design/status-webseite/screenshots/admin-benutzer-loeschen.png`
- `docs/design/status-webseite/screenshots/admin-tokens.png`
- `docs/design/status-webseite/screenshots/admin-tokens-leer.png`
- `docs/design/status-webseite/screenshots/admin-token-widerrufen.png`
- `docs/design/status-webseite/screenshots/admin-einstellungen.png`
- `docs/design/status-webseite/screenshots/admin-update-installation.png`
- `docs/design/status-webseite/screenshots/zugriff-verweigert.png`
- `docs/design/status-webseite/screenshots/formular-abgelaufen.png`
