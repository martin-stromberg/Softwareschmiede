# Ansichten 5–7 (+ Zwischenseiten): `AdminPagesComposer`

Analysierte Datei: `server/Softwareschmiede.StatusServer/Pages/AdminPagesComposer.cs`.
Alle Seiten nur für Rolle `Administration` (`StatusRoles.AdminOnlyPolicy`), alle mit
Nav und `data-theme` = `user.Theme`.

## Ansicht 5: Benutzerverwaltung `/admin/users` — `RenderUserList(user, users, message?, isError)`

Zeilen 21–56. Zwei `<section>`-Karten:

**Sektion 1 — Tabelle** (`<table>` mit Kopf `Benutzername | Stufe | Status | Aktionen`):

Je Zeile (`RenderUserRow`, Zeilen 368–388):
- `td`: `<strong>{UserName}</strong>` + bei `IsLastActiveAdmin` `<span class="pill pause">letzter Admin</span>`
- `td`: Rollen-Anzeigename (`Überwacher`/`Administration`)
- `td`: Status-Pill — `IsDeactivated` → `pill err` „deaktiviert"; `IsLockedOut` →
  `pill wait` „gesperrt bis {dd.MM.yyyy HH:mm} UTC"; sonst `pill done` „aktiv"
- `td`: Aktionszelle mit vier Inline-Elementen (siehe unten)

Aktionen je Zeile (alle `form.inline` POST + Hidden-Antiforgery, nur Löschen ist ein Link):
1. **Stufenwechsel** (`AppendRoleAction`): Admin → POST `{BasePath}/admin/users/{id}/role`
   mit `hidden role=Ueberwacher`, Button „Zu Überwacher herabstufen" — bei
   `IsLastActiveAdmin` `disabled` + `title`-Tooltip „Der letzte aktive Administrator
   kann nicht herabgestuft werden." Überwacher → POST `role=Administration`, Button
   „Zu Administration befördern".
2. **Aktivieren/Deaktivieren** (`AppendActivationAction`): deaktiviert → POST
   `…/{id}/activate` „Aktivieren"; aktiv → POST `…/{id}/deactivate` „Deaktivieren"
   (beim letzten Admin `disabled` + Tooltip).
3. **Sperrung zurücksetzen** (`AppendUnlockAction`): POST `…/{id}/unlock`, Button nur
   aktiv wenn `IsLockedOut`, sonst `disabled` (ohne Tooltip).
4. **Löschen** (`AppendDeleteAction`): `<a class="action-link danger" href="…/{id}/delete">Löschen</a>`
   — führt auf die GET-Bestätigungsseite (5a). Beim letzten Admin: `form.inline` mit
   `button type="button" disabled` + Tooltip (kein Formular-Ziel!).

**Sektion 2 — „Benutzer anlegen"** (`<h2>`, POST `{BasePath}/admin/users`):
- `Benutzername` (`pattern="[a-zA-Z0-9._@+-]+"`, `title=`FieldHint, `autocomplete="off"`)
- `<p class="muted">` FieldHint „Erlaubte Zeichen: a–z, A–Z, 0–9 sowie -._@+ (keine
  Umlaute, keine Leerzeichen)."
- `Passwort (mindestens 8 Zeichen)` (`minlength="8"`, `autocomplete="new-password"`)
- `Berechtigungsstufe` — `<select name="role">`: `Ueberwacher` → „Überwacher (nur
  lesen)", `Administration` → „Administration (Verwaltung)"
- Button „Benutzer anlegen"

## Ansicht 5a: Bestätigungsseite „Benutzer löschen" — `RenderUserDeleteConfirm(user, targetUserId, targetUserName)`

Zeilen 144–162. `GET /admin/users/{id}/delete`.

- `<section><h1>Benutzer „{Name}“ löschen?</h1>`
- `<p class="muted">` Warnung: Konto wird **unwiderruflich** gelöscht, Person kann
  sich nicht mehr anmelden, nicht rückgängig machbar.
- `<form method="post" action="{BasePath}/admin/users/{id}/delete">` +
  Hidden-Antiforgery, `<button class="danger">Endgültig löschen</button>` +
  `<a href="{BasePath}/admin/users">Abbrechen</a>` (im selben Formular-Element).

## Ansicht 6: Token-Verwaltung `/admin/tokens` — `RenderTokenList(user, tokens, message?, isError, createdToken?)`

Zeilen 66–136. Zwei Sektionen.

**Sektion 1**:
- Optional `.token-once`-Block (nur direkt nach dem Anlegen via `?created={key}`,
  Klartext aus `RelayTokenPlaintextStore.Take` — Single-Use): „Neues Relay-Token
  erstellt. Der Klartext wird nur jetzt einmalig angezeigt — kopiere ihn und
  hinterlege ihn in den App-Einstellungen (Remote-Übersicht)." + `<code>` mit dem
  Token-Klartext (43 Zeichen, Base64url).
- Leer-Zustand (`.empty`): „Noch keine Relay-Tokens vorhanden — der
  `<code>/relay</code>`-Endpunkt lehnt aktuell alle Verbindungen ab."
- Sonst Tabelle `Bezeichnung | Erstellt (UTC) | Status | Aktionen`:
  - `CreatedUtc` als `dd.MM.yyyy HH:mm`
  - Status: `RevokedUtc != null` → `pill err` „widerrufen {Datum} UTC"; sonst
    `pill done` „gültig"
  - Aktion: gültig → `<a class="action-link danger" href="…/{id}/revoke">Widerrufen</a>`;
    widerrufen → `form.inline` mit `button type="button" disabled` „Widerrufen"

**Sektion 2 — „Token anlegen"**: POST `{BasePath}/admin/tokens`, Feld `Bezeichnung`
(`name="label" required maxlength="200"`, `RelayTokenService.MaxLabelLength`),
Button „Token anlegen".

## Ansicht 6a: Bestätigungsseite „Token widerrufen" — `RenderTokenRevokeConfirm(user, token)`

Zeilen 169–187. `GET /admin/tokens/{id:long}/revoke` (nur für gültige Tokens —
bereits widerrufene/nicht gefundene rendern die Liste mit Fehlermeldung).

- `<section><h1>Token „{Label}“ widerrufen?</h1>`
- `<p class="muted">` Warnung: Token wird **unwiderruflich** ungültig, verbundene
  App-Instanzen verlieren die Relay-Anmeldung, neue Anbindung braucht neues Token.
- POST auf denselben Pfad, `<button class="danger">Token endgültig widerrufen</button>`
  + `<a href="{BasePath}/admin/tokens">Abbrechen</a>`.

## Ansicht 7: Einstellungen `/admin/settings` — `RenderSettings(user, sessionMinutes, updateSettings, updateStatus, message?, isError)`

Zeilen 199–230 + `AppendUpdateSection` (258–346). Zwei Sektionen.

**Sektion 1 — „Sitzungsdauer"** (`<h2>`):
- `<p class="muted">` „Dauer der Anmeldesession in Minuten (5–43200). Gilt ab der
  nächsten Ausstellung bzw. Erneuerung der Anmelde-Cookies."
- POST `{BasePath}/admin/settings/session-duration`: `input type="number"
  name="minutes" required min="5" max="43200" value="{sessionMinutes}"`
  (`ServerSettingsService.Min/MaxSessionDurationMinutes`), Button „Speichern".

**Sektion 2 — „Updates"** (`<h2>Updates</h2>`):

Statustabelle `Feld | Wert`:
| Feld | Wert |
|------|------|
| Installierte Version | `updateStatus.InstalledVersion` bzw. „unbekannt" |
| Zustand | `UpdateStateText` — Idle→„Bereit", Checking→„Prüfung läuft", UpdateAvailable→„Update verfügbar", Downloading→„Download läuft", ReadyToInstall→„Bereit zur Installation", Installing→„Installation läuft", Success→„Update installiert", Failed→„Fehlgeschlagen", Disabled→„Deaktiviert" (reiner Text, keine Pill) |
| Verfügbare Version | `AvailableVersion` bzw. „—" |
| Letzte Prüfung | `LastCheckedAt.LocalDateTime` `dd.MM.yyyy HH:mm` bzw. „—" |
| Letzter Fehler | nur bei `LastError`: `pill err` mit Fehlertext |
| Installationssperre | `pill wait` „aktiv" / `pill pause` „verwaist — Reset möglich" (`IsLockStale`) / `pill done` „keine" |
| Update-Protokoll | nur bei `UpdateLogPath`: `<code>`-Pfad |

- Bei `IsLockStale`: `form.inline` POST `{BasePath}/admin/settings/update/lock-reset`,
  Button „Update-Sperre zurücksetzen".
- `<h3>Kanal und Automatisierung</h3>` — POST `{BasePath}/admin/settings/update`:
  - `Release-Kanal` `<select name="channel">`: `stable` → „Stable (nur endgültige
    Releases)", `prerelease` → „Pre-Release (inkl. Vorabversionen)"
  - `Automatisierung` `<select name="mode">`: `off` → „Aus — nur manuelle Aktionen",
    `check` → „Nur prüfen — verfügbare Updates melden", `install` → „Prüfen und
    installieren — täglich zur angegebenen Uhrzeit"
  - `Installationsuhrzeit (bei „Prüfen und installieren")` `input type="time"
    name="installTime" value="{HH:mm}"` (Default 03:00)
  - Button „Update-Einstellungen speichern"
- `<h3>Aktionen</h3>`:
  - `form.inline` POST `{BasePath}/admin/settings/update/check` — „Jetzt nach Update suchen"
  - `form.inline` POST `{BasePath}/admin/settings/update/install` —
    `<label><input type="checkbox" name="confirmDowntime" value="true"> Downtime
    bestätigen — der Server wird während der Installation neu gestartet</label>`
    + `<button class="danger">Update jetzt installieren</button>` (ohne Checkbox →
    Fehlermeldung „Bitte bestätigen Sie die Downtime …")

## Ansicht 7a: Ergebnisseite „Update wird installiert" — `RenderUpdateInstallStarted(user, updateLogPath)`

Zeilen 241–256. **Statische Seite ohne Redirect** (bewusst kein PRG: der Server
wird vom Update-Skript gestoppt, ein Redirect-Ziel könnte nicht mehr ausgeliefert
werden). Gerendert als direkte Antwort auf `POST /admin/settings/update/install`
bei `AutoUpdateResultCode.InstallationStarted`.

- `<section><h1>Der Server startet neu</h1>`
- `<p class="muted">` Update-Paket wird eingespielt, Server wird angehalten, Dauer
  typischerweise < 1 Minute, danach unter gewohnter Adresse erreichbar (erneute
  Anmeldung kann nötig sein), Details im Update-Protokoll `<code>{updateLogPath}</code>`.

## Hilfsmethode `AppendOption`

`AppendOption(sb, value, label, selected)` — `<option value="{v}"{ selected}>{label}</option>`
— für `theme`-, `role`-, `channel`-, `mode`-Selects.
