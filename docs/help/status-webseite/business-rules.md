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

## Kein Recovery-Weg

**Beschreibung:** Bewusste Scope-Entscheidung: kein Passwort-Reset, keine
E-Mail, keine Recovery-Codes, kein zusätzlicher Schutz des
Registrierungsfensters.

**Verhalten:** Alle Administratoren ausgesperrt → `data`-Verzeichnis löschen,
Server neu starten, Setup-Lauf von vorn. Passwort vergessen → Konto durch
Admin löschen und neu anlegen.

**Umsetzung:** Anforderungs-Ausschluss; Absicherung gegen den unrettbaren
Zustand ist der Letzter-Admin-Schutz.
