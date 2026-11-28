← [Zurück zur Übersicht](index.md)

# Status-Webseite — Business Rules

## Erst-Registrierung schließt sich atomar

**Beschreibung:** Nur der allererste Benutzer darf sich selbst registrieren;
danach ist die Registrierung dauerhaft geschlossen — auch bei parallelen
Setup-Versuchen darf nie ein zweiter Selbst-Registrierungs-Admin entstehen.

**Bedingungen:**
- `GET`/`POST /register` liefern nur dann das Formular bzw. verarbeiten es,
  wenn `UserManager.Users.Any() == false`.
- `UserAdminService.RegisterFirstAdminAsync` schreibt in derselben Transaktion
  den `ServerSettings`-Eintrag `SetupCompleted` (Primärschlüssel) **vor** der
  Kontoanlage.

**Verhalten:**
- Existiert bereits ein Benutzer → Redirect `/` (GET/POST) bzw. Fehler „Es
  existiert bereits ein Benutzerkonto."
- Gewinnt ein paralleler Setup-Lauf die `SetupCompleted`-Markierung, schlägt
  der andere an der `DbUpdateException` (PK-Verletzung) — atomarer
  TOCTOU-Guard ohne Schutzfenster.

**Umsetzung:** `UserAdminService.RegisterFirstAdminAsync` —
PK-Markierung + `UserManager.CreateAsync` + `AddToRoleAsync(Administration)`
in einer Transaktion.

## Zwei Berechtigungsstufen, zeitnahe Stufenänderung

**Beschreibung:** `Administration` verwaltet, `Ueberwacher` liest. Eine vom
Admin geänderte Stufe wirkt zeitnah auf bestehende Sessions — ohne dass der
betroffene Benutzer sich neu anmelden muss.

**Bedingungen:**
- Policies: `PageAccess` (beide Rollen) für `/`, `/status.json`, `/profile`,
  `/logout`; `AdminOnly` für `/admin/*`.
- `UserAdminService.SetRoleAsync` führt den Wechsel atomar aus
  (`RemoveFromRolesAsync` + `AddToRoleAsync` + `UpdateSecurityStampAsync`).

**Verhalten:**
- Bei Abweichung zwischen den Rollen-Claims des Cookies und
  `UserManager.GetRolesAsync` ersetzt `StatusServerCookieEvents.ValidatePrincipal`
  den Principal (`ReplacePrincipal` + `ShouldRenew`) — neue Stufe gilt ab dem
  nächsten Request.
- Zusätzlich fängt der Security-Stamp parallele Sessions ab
  (Standard-Validierungsintervall).

**Umsetzung:** `StatusServerCookieEvents.ValidatePrincipal`,
`UserAdminService.SetRoleAsync`.

## Letzter-Admin-Schutz

**Beschreibung:** Der letzte aktive Benutzer mit Stufe `Administration` darf
nicht deaktiviert, gelöscht oder herabgestuft werden — sonst gäbe es keinen
Verwaltungszugang mehr (es existiert bewusst kein Recovery-Weg).

**Bedingungen:**
- „Letzter aktiver Admin" = genau ein Benutzer mit Rolle `Administration` und
  `IsDeactivated == false`.
- Gilt gleichermaßen für das eigene wie für fremde Konten.

**Verhalten:**
- `UserAdminService` lehnt die Aktion ab („Der letzte aktive Administrator
  kann nicht …"), die UI deaktiviert die betreffenden Schaltflächen mit
  Hinweistitel und markiert das Konto als „letzter Admin".
- Prüfung und Mutation laufen unter einem prozessweiten `SemaphoreSlim` —
  zwei parallele Admin-Requests können den Check-then-act-TOCTOU nicht
  ausnutzen (Single-Instanz-Betrieb).

**Umsetzung:** `UserAdminService.IsLastActiveAdminAsync` +
`s_lastAdminLock`; `AdminPagesComposer` (`IsLastActiveAdmin`-Flag).

## Sperrung nach drei Fehlversuchen

**Beschreibung:** `IdentityOptions.Lockout`: `MaxFailedAccessAttempts = 3`,
`DefaultLockoutTimeSpan = 1 Stunde`, `AllowedForNewUsers = true`.

**Verhalten:**
- `POST /login` ruft `PasswordSignInAsync` mit `lockoutOnFailure: true`.
- Gesperrt → Hinweis mit formatierter Restdauer; deaktiviertes Konto mit
  korrektem Passwort → sofortiges `SignOutAsync` + „Dieses Konto ist
  deaktiviert."
- Admin-Freischaltung (`SetLockoutEndDateAsync(null)` +
  `ResetAccessFailedCountAsync`) wirkt sofort; sonst endet die Sperrung
  automatisch nach einer Stunde.

**Umsetzung:** `AuthEndpoints.PostLogin`,
`UserAdminService.ResetLockoutAsync`, `Program` (IdentityOptions).

## Deaktivierung wirkt sofort

**Beschreibung:** Ein deaktiviertes Konto verliert nicht nur den neuen Login,
sondern auch bereits ausgestellte Sessions.

**Bedingungen:** `ApplicationUser.IsDeactivated`; `SetDeactivatedAsync`
aktualisiert beim Deaktivieren zusätzlich den Security-Stamp.

**Verhalten:** `ValidatePrincipal` prüft das Flag je Request — `RejectPrincipal`
+ `SignOutAsync`; der nächste Request des deaktivierten Benutzers landet auf
der Anmeldung. Das Verhalten ist bewusst nicht an das
`SecurityStampValidator`-Intervall gekoppelt.

**Umsetzung:** `StatusServerCookieEvents.ValidatePrincipal`,
`UserAdminService.SetDeactivatedAsync`.

## Relay-Token: Klartext einmal, Hash immer

**Beschreibung:** Ein Relay-Token berechtigt bis zum Widerruf zum
Verbindungsaufbau auf `/relay`; gespeichert wird ausschließlich der
SHA-256-Hash.

**Bedingungen:**
- Erzeugung: 32 Bytes `RandomNumberGenerator`, Base64url; `Label` 1–200
  Zeichen.
- Speicherung: `TokenHash` (Unique-Index); Widerruf setzt `RevokedUtc`
  (Soft-Revoke, Datensatz bleibt sichtbar).

**Verhalten:**
- Der Klartext verlässt den Server genau einmal über den Single-Use-Puffer
  `RelayTokenPlaintextStore` (`?created={guid}`, `Take` einmalig, TTL 2 min).
- `RelayTokenValidator.IsValidAsync`: Hash bilden → DB-Lookup →
  `RevokedUtc is null` → `SecretComparer.FixedTimeEquals`. Kein Token in der
  Datenbank → `/relay` lehnt alle Verbindungen ab.
- Widerrufene Tokens berechtigen nicht mehr; bereits verbundene Sessions
  werden nicht nachträglich getrennt (Prüfung nur beim Connect).

**Umsetzung:** `RelayTokenService.CreateAsync/RevokeAsync/HashToken`,
`RelayTokenValidator.IsValidAsync`,
`AdminEndpoints.GetTokens`/`PostToken` mit `RelayTokenPlaintextStore`.

## Sitzungsdauer aus der Datenbank

**Beschreibung:** Die Sitzungsdauer ist eine admin-konfigurierbare,
anwendungsweite Einstellung — keine `appsettings`-Konfiguration.

**Bedingungen:** `ServerSettings["SessionDurationMinutes"]`; fehlt die Zeile,
gilt der Default 480 Minuten; zulässig 5–43200.

**Verhalten:**
- `SigningIn` setzt `ExpiresUtc = now + Dauer` (persistentes Cookie).
- `ValidatePrincipal` erneuert `IssuedUtc`/`ExpiresUtc` erst nach Verstreichen
  der halben Ticket-Laufzeit (Sliding) — ein bedingungsloses `ShouldRenew`
  würde jede Antwort neu signieren und Sliding-Expiration wirkungslos machen.
- Eine Änderung gilt ab der nächsten Ausstellung/Erneuerung, nicht rückwirkend.

**Umsetzung:** `ServerSettingsService`,
`StatusServerCookieEvents.SigningIn`/`ValidatePrincipal`.

## Theme-Einstellung pro Benutzer — anonyme Seiten folgen dem System

**Beschreibung:** Das Farbschema der Web-UI ist eine benutzergebundene,
persistierte Einstellung (`ApplicationUser.Theme`, `ThemePreference`
`Auto`/`Light`/`Dark`) — es gibt bewusst keinen serverweiten Standard und
keinen Umschalter für anonyme Besucher.

**Bedingungen:**
- Vorgabe ist `Auto` (Ordinal `0`) — für alle Bestandskonten und für Seiten
  ohne `PageUserContext` (Login, Registrierung, „Formular abgelaufen",
  anonymes `RenderAccessDenied`).
- `BuildUserContextAsync` übernimmt `user?.Theme ?? ThemePreference.Auto`
  in den `PageUserContext`; die Composer reichen ihn als `data-theme` am
  `<html>`-Tag aus (`PageShell.ThemeName`).

**Verhalten:**
- `auto`: `PageShell.ThemeCss` löst das Schema clientseitig über
  `@media (prefers-color-scheme: dark)` auf — der Selektor
  `:root:not([data-theme="light"])` stellt sicher, dass eine explizite
  `light`-Wahl auch bei dunkler Systemeinstellung gewinnt. Ist keine
  Systempräferenz auslesbar, gilt die `:root`-Light-Palette (Fallback
  Light).
- `light`/`dark`: erzwingen das Schema systemunabhängig via
  `data-theme="light"` bzw. `:root[data-theme="dark"]`-Override.
- Die Einstellung gilt ab dem nächsten Seitenaufruf — server-gerendert ohne
  JavaScript, kein Live-Umschalten; die Änderung invalidiert keine Session
  (kein Security-Stamp-Eingriff).
- Ungültige Formularwerte werden abgewiesen (`Enum.TryParse` +
  `Enum.IsDefined`), die Profilseite rendert direkt mit Fehlermeldung —
  ohne Persistenz.

**Umsetzung:** `AuthEndpoints.PostProfileTheme`,
`PageShell.ThemeCss`/`DarkThemeVars`, `PageShell.BeginPage`,
`StatusPageComposer.Render`.

## Kein Recovery-Weg

**Beschreibung:** Bewusste Scope-Entscheidung: kein Passwort-Reset, keine
E-Mail, keine Recovery-Codes, kein zusätzlicher Schutz des
Registrierungsfensters.

**Verhalten:** Alle Administratoren ausgesperrt → `data`-Verzeichnis löschen,
Server neu starten, Setup-Lauf von vorn. Passwort vergessen → Konto durch
Admin löschen und neu anlegen.

**Umsetzung:** Anforderungs-Ausschluss; Absicherung gegen den unrettbaren
Zustand ist der Letzter-Admin-Schutz.

## Update-Einstellungen: Persistierte Admin-Werte gewinnen

**Beschreibung:** Kanal, Automatisierungsmodus und Installationsuhrzeit der
Update-Funktion sind anwendungsweite Admin-Einstellungen — wie die
Sitzungsdauer liegen sie in der Datenbank, nicht in `appsettings.json`.

**Bedingungen:**
- `ServerSettings`-Schlüssel `UpdateChannel` (`stable`/`prerelease`),
  `UpdateAutomationMode` (`off`/`check`/`install`), `UpdateInstallTime`
  (`HH:mm`); Defaults `stable`/`off`/`03:00`, fehlende oder korrupte Werte
  fallen darauf zurück.
- Die `appsettings`-`Update:`-Sektion liefert nur technische Parameter
  (Repository, Manifestname, Workspace, Zeitfenster, Neustart-Ziel).

**Verhalten:**
- `ApplyPersistedUpdateSettingsAsync` wendet die persistierten Werte bei
  jedem Start vor `app.Run()` auf `AutoUpdateOptions` an — die
  Bibliotheks-Hintergrunddienste lesen ihre Optionen erst beim Start und
  sehen so die Admin-Werte.
- `POST /admin/settings/update` persistiert und wendet sofort an
  (`StatusServerUpdateService.ApplySettingsAsync`), inklusive Neuaufbau der
  Update-Quelle beim Kanalwechsel — kein Neustart nötig.
- `installTime` ist Pflicht bei Modus `install` (der Scheduler feuert ohne
  `ScheduledInstallTime` nie), bei den anderen Modi optional.

**Umsetzung:** `ServerSettingsService.GetUpdateSettingsAsync`/
`SetUpdateSettingsAsync`, `StatusServerAutoUpdateOptionsMapper.ApplySettings`,
`AutoUpdateApplicationExtensions.ApplyPersistedUpdateSettingsAsync`.

## Manuelle Update-Aktionen laufen auch bei Modus „Aus"

**Beschreibung:** `AutoUpdateOptions.Enabled` ist der Master-Schalter der
Bibliothek — die Hintergrund-Automation darf bei Modus `off` nicht laufen,
ein explizit angeklickter Admin-Button aber schon.

**Bedingungen:** Modus `off` setzt `Enabled=false`; die manuellen Endpunkte
`/admin/settings/update/check` und `…/install` bleiben bedienbar.

**Verhalten:**
- `RunWithManualOverrideAsync` setzt `Enabled` für die Dauer des manuellen
  Laufs kurzzeitig auf `true` und stellt den Wert danach wieder her — die
  Hintergrunddienste bleiben deaktiviert.
- Der Override-Block läuft unter dem prozessweiten `s_manualOverrideLock`:
  zwei parallele manuelle Läufe serialisieren sich, und der
  `finally`-Restore kann keinen parallel gespeicherten Einstellungswert
  überschreiben (derselbe Lock schützt `ApplySettingsAsync`).

**Umsetzung:** `StatusServerUpdateService.RunWithManualOverrideAsync` +
`s_manualOverrideLock`.

## Separates Server-Manifest `statusserver-update.json`

**Beschreibung:** Die Update-Prüfung des Servers liest ein eigenes
Release-Asset-Manifest — das Desktop-`update.json` bleibt unverändert.

**Bedingungen:**
- Die Paketwahl (`IAutoUpdatePlatformResolver.SelectPackage`) erfolgt
  ausschließlich über `platform` + `runtimeIdentifier` — zwei `win-x64`-
  Assets (`release.zip` + `statusserver-win-x64.zip`) in einem Manifest
  wären uneindeutig.
- Die Release-Pipeline schreibt je RID eine `release-metadata.json` in die
  StatusServer-Pakete (Versionsquelle via
  `ReleaseMetadataInstalledVersionProvider`) und lädt
  `statusserver-update.json` (Rezepte-Format: `platform`,
  `runtimeIdentifier`, `assetName`, `assetUrl`, `sha256`, `sizeBytes`) als
  zusätzliches Release-Asset hoch.

**Verhalten:**
- Ohne `release-metadata.json` meldet die Seite die installierte Version
  als „unbekannt" und die Prüfung bleibt wirkungslos — Alt-Pakete sind erst
  nach einem manuell eingespielten aktuellen Release update-fähig.
- Gefundene Pakete werden per SHA-256 gegen das Manifest verifiziert, bevor
  sie zur Installation bereitliegen.

**Umsetzung:** `.github/actions/build-and-package/action.yml` (Schritte
„Write StatusServer release metadata", „Create StatusServer update
manifest"), `UpdateStatusMapper`/`IInstalledVersionProvider`.

## Datenerhalt beim Update-Dateiaustausch

**Beschreibung:** Datenbestand und lokale Konfiguration dürfen kein Update
überstehen müssen — sie werden vom Installationsskript gesichert und
restauriert.

**Bedingungen:**
- `PreserveFile("appsettings*.json")` und `PreserveFile("data/*")` (deckt
  `statusserver.db` samt `-wal`/`-shm` direkt unter `data/`).
- Ein per `Data:DatabasePath` konfigurierter SQLite-Pfad innerhalb des
  Installationsverzeichnisses, aber außerhalb der `data/*`-Abdeckung, wird
  samt `<pfad>-*`-Seitendateien zusätzlich geschützt; ein Pfad außerhalb des
  Installationsverzeichnisses wird vom Austausch ohnehin nicht berührt.

**Verhalten:** Das Skript sichert die geschützten Dateien vor dem Austausch
und restauriert sie danach — Benutzer, Tokens, `ServerSettings` (inkl. der
Update-Einstellungen) und lokale Konfigurationsanpassungen bleiben über
jedes Update erhalten.

**Umsetzung:** `AutoUpdateApplicationExtensions.SetInitialConfiguration` /
`PreserveDatabaseFiles`.

## Installations-Sperre nur bei Verwaistheit zurücksetzbar

**Beschreibung:** `update.lock` verhindert parallele Installationsläufe;
bleibt der Lock nach einem Prozessabbruch liegen, blockiert er alle
weiteren Updates dauerhaft — der Admin darf ihn aber nur zurücksetzen, wenn
er nachweislich verwaist ist.

**Bedingungen:**
- `IsLockStale` bewertet das Lock-Alter gegen
  `Update:HealthTimeoutSeconds` (Default 120 s); ohne Lock oder bei
  unverwaistem Lock schlägt der Reset fehl.
- Die UI zeigt „aktiv" / „verwaist — Reset möglich" / „keine" und blendet
  den Button **Update-Sperre zurücksetzen** nur bei `IsLockStale` ein.

**Verhalten:** `ResetLockAsync` löscht den Lock über `DeleteLockAsync` und
korrigiert den persistierten Status (`IsLocked=false`,
`LockCreatedAt=null`) → `?msg=update-lock-reset`; sonst
`?err=update-lock-reset-failed`.

**Umsetzung:** `StatusServerUpdateService.ResetLockAsync` über
`IAutoUpdatePackageStore` + `AutoUpdateStatusService`.
