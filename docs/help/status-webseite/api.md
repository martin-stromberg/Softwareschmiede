← [Zurück zur Übersicht](index.md)

# Status-Webseite — API

## Übersicht

Der Status-Server exponiert die HTML-Status-Seite, eine JSON-Sicht, den
SignalR-Relay-Hub für App-Instanzen sowie die serverseitig gerenderten
Authentifizierungs- und Verwaltungs-Endpunkte. Es gibt keine separates
REST-API für die Benutzerverwaltung — Verwaltungsaktionen laufen über
HTML-Formulare.

## Authentifizierung

| Bereich | Mechanismus |
|---------|-------------|
| `/`, `/status.json`, `/profile`, `/logout`, `/admin/*` | Identity-Anwendungs-Cookie (Policy `PageAccess` = Rollen `Administration` oder `Ueberwacher`; `/admin/*` zusätzlich Policy `AdminOnly`) |
| `/relay` (SignalR-Hub) | Relay-Token im Query-Parameter `access_token`, bei Nicht-WebSocket-Transporten als `Authorization: Bearer <token>` — geprüft gegen die Datenbank (SHA-256-Hash, nicht widerrufen) |
| `/register`, `/login`, `/access-denied` | anonym |

Nicht authentifizierte Aufrufe geschützter Seiten werden auf `/login`
umgeleitet — solange kein Benutzer existiert, auf `/register`.
Authentifizierte `Ueberwacher` auf Admin-Routen erhalten 403 mit der Seite
`/access-denied`.

## Endpunkte

### `GET /`

**Beschreibung:** Server-gerenderte HTML-Status-Seite (Meta-Refresh
`Page:RefreshSeconds`), inkl. Navigationszeile mit Abmelden-Formular.

**Authentifizierung:** Cookie, Policy `PageAccess`.

**Rückgabe:** `text/html; charset=utf-8` — Instanz-Snapshots aus
`InstanceSnapshotCache` plus GitHub-Aggregation.

### `GET /status.json`

**Beschreibung:** Maschinelle Sicht desselben Zustands.

**Authentifizierung:** Cookie, Policy `PageAccess`.

**Rückgabe:** `application/json` — `{ GeneratedUtc, Instances, GitHub }`.

### `POST /relay` (SignalR-Hub `StatusRelayHub`)

**Beschreibung:** Hub-Verbindung für App-Instanzen. Methoden:
`RegisterInstance(RegisterInstanceRequest)` → `RegisterInstanceResult`
(prüft `InstanceId` und `ProtocolVersion` gegen
`RelayProtocolVersion.Current`) und `PushSnapshot(StatusSnapshot)`.

**Authentifizierung:** Relay-Token (`access_token`-Query oder Bearer-Header),
Validierung durch `RelayTokenValidator.IsValidAsync` gegen `RelayTokens`
(Hash-Match, `RevokedUtc is null`).

**Fehler:** Ungültiges/widerrufenes Token → Verbindung wird im
`OnConnectedAsync` abgebrochen (`Context.Abort()`), Warn-Log.

### `GET`/`POST /register`

**Beschreibung:** Erst-Registrierung; nur ausgeliefert/verarbeitet, solange
`UserManager.Users.Any() == false` — sonst Redirect `/`.

**Formularfelder (POST):**

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `username` | string | Ja | Benutzername (Identity-Zeichensatz) |
| `password` | string | Ja | ≥ `PasswordPolicy.MinLength` (8) |
| `confirmPassword` | string | Ja | muss `password` entsprechen |
| `__RequestVerificationToken` | string | Ja | Antiforgery-Hidden-Feld |

**Rückgabe:** Erfolg → Redirect `/` (Konto angelegt, Rolle `Administration`,
sofort angemeldet); Fehler → Seite mit deutscher Fehlermeldung.

### `GET`/`POST /login`, `POST /logout`

**Beschreibung:** Anmeldung (`PasswordSignInAsync`, `lockoutOnFailure: true`)
und Abmeldung (`SignOutAsync` → Redirect `/login`). `GET /login` leitet
bereits Angemeldete auf `/` weiter.

**Formularfelder (login):** `username`, `password`, Antiforgery-Hidden-Feld.

**Fehlerfälle:** generische Meldung bei falschen Daten; `IsLockedOut` →
Sperr-Hinweis mit Restdauer; `IsDeactivated` → „Dieses Konto ist deaktiviert."

### `GET /profile`, `POST /profile/password`, `POST /profile/theme`

**Beschreibung:** Profilseite mit Konto-Übersicht, Anzeige-Einstellung und
Passwortänderung des eigenen Kontos. `POST /profile/password` ruft
`UserManager.ChangePasswordAsync` (danach `RefreshSignInAsync`);
`POST /profile/theme` speichert die Theme-Einstellung über
`UserManager.UpdateAsync` in der `AspNetUsers`-Spalte `Theme`.

**Formularfelder (password):** `currentPassword`, `newPassword`,
`confirmPassword`, Antiforgery-Hidden-Feld. Erfolg → Redirect
`/profile?msg=password-changed`.

**Formularfelder (theme):** `theme` (`auto`/`light`/`dark`, case-insensitiv,
`Enum.IsDefined`-Prüfung auf `ThemePreference`), Antiforgery-Hidden-Feld.
Erfolg → Redirect `/profile?msg=theme-saved`; ungültige oder fehlende Werte
rendern die Profilseite direkt mit der Fehlermeldung „Unbekannte
Anzeigeeinstellung." (kein Redirect).

### `GET /access-denied`

**Beschreibung:** Verständliche 403-Seite (anonym erreichbar, rendert bei
angemeldetem Benutzer die Navigation).

### Admin-Endpunkte (Policy `AdminOnly`, alle POSTs mit Antiforgery)

| Route | Methode | Zweck |
|-------|---------|-------|
| `/admin/users` | GET | Benutzerliste + Anlege-Formular |
| `/admin/users` | POST | Benutzer anlegen (`username`, `password`, `role`) |
| `/admin/users/{id}/role` | POST | Stufe ändern (`role` ∈ `Administration`/`Ueberwacher`) |
| `/admin/users/{id}/deactivate` | POST | Konto deaktivieren |
| `/admin/users/{id}/activate` | POST | Konto aktivieren |
| `/admin/users/{id}/delete` | GET/POST | Bestätigungsseite / unwiderrufliches Löschen |
| `/admin/users/{id}/unlock` | POST | Sperrung zurücksetzen |
| `/admin/tokens` | GET | Token-Liste (`?created={guid}` liefert einmalig den Klartext) |
| `/admin/tokens` | POST | Token anlegen (`label`, 1–200 Zeichen) → Redirect `?created={guid}` |
| `/admin/tokens/{id}/revoke` | GET/POST | Bestätigungsseite / Widerruf |
| `/admin/settings` | GET | Einstellungsseite (Sitzungsdauer, Update-Status/-Formular) |
| `/admin/settings/session-duration` | POST | Sitzungsdauer setzen (`minutes`, 5–43200) |
| `/admin/settings/update` | POST | Update-Einstellungen speichern (`channel` ∈ `stable`/`prerelease`, `mode` ∈ `off`/`check`/`install`, `installTime` `HH:mm` — Pflicht bei `mode=install`) |
| `/admin/settings/update/check` | POST | Update-Prüfung auslösen (Prüfung + Download eines gefundenen Pakets) |
| `/admin/settings/update/install` | POST | Installation starten — erfordert die gesetzte Checkbox `confirmDowntime`; bei Erfolg statische Seite „Der Server startet neu" statt Redirect |
| `/admin/settings/update/lock-reset` | POST | Verwaiste Installations-Sperre zurücksetzen (nur wenn Lock existiert und `IsLockStale` gilt) |

**Erfolgsantworten:** Redirect auf die jeweilige GET-Seite mit
`?msg=<meldung>` (Post/Redirect/Get) — Ausnahme: Ein erfolgreich gestarteter
Update-Installationslauf antwortet mit der statischen Seite „Der Server
startet neu", weil das Installationsskript den Prozess anhält und ein
Redirect-Ziel nicht mehr ausgeliefert werden könnte. Fachliche Fehlschläge
rendern die Seite direkt mit `?err`-bzw. Inline-Fehlermeldung.
