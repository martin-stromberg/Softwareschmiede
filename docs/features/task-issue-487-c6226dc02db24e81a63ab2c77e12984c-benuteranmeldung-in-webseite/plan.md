# Umsetzungsplan: Benutzeranmeldung in Webseite (StatusServer)

## Übersicht

Der `Softwareschmiede.StatusServer` erhält eine vollständige, persistente Benutzerverwaltung
auf Basis von ASP.NET Core Identity mit EF Core + SQLite (`data/statusserver.db`). Die
konfigurationsbasierte Absicherung (`PageAccessMiddleware` mit Basic-Auth gegen
`Page:Username`/`Page:Password`, `RelayTokenValidator` gegen `Relay:Tokens`/`Relay:DevToken`)
wird durch Cookie-Anmeldung mit den Rollen `Administration`/`Ueberwacher` und
datenbankgestützten, gehashten, widerrufbaren Relay-Tokens ersetzt. Neu sind serverseitig
gerenderte Formularseiten (Registrierung, Login, Profil, Admin-Verwaltung) im
`StatusPageComposer`-Stil sowie Admin-Funktionen für Benutzer, Tokens und Sitzungsdauer.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Lebenszyklus von Benutzerkonto, Cookie-Sitzung und Relay-Token — bewertet je HTTP-Anfrage auf `/` und `/status.json` (Session), je Hub-Verbindungsaufbau auf `/relay` (Token) sowie je Admin-/Profil-Aktion in der Verwaltungs-UI |
| Auslösende Ereignisse | `GET /` bzw. `GET /status.json` (Setup-Modus → Registrierung, keine Session → Login-Umleitung, Session → Auslieferung nach Stufe); `POST` der Formulare (Registrierung, Login, Logout, Passwortänderung); Admin-Aktionen (Benutzer anlegen/Berechtigungsstufe ändern/deaktivieren/aktivieren/löschen, Sperrung zurücksetzen, Token erstellen/widerrufen, Sitzungsdauer setzen); SignalR-Verbindungsaufbau auf `/relay` mit `access_token`-Query/Bearer-Header; Server-Start (`data`-Verzeichnis + Migrationen + Rollen-Seeding) |
| Erwartete Wirkung | Nur angemeldete Benutzer lesen die Status-Seite; Administratoren verwalten Benutzer/Sperren/Tokens/Sitzungsdauer über die UI; ein neues Relay-Token wird genau einmal angezeigt und berechtigt bis zum Widerruf zum `/relay`-Verbindungsaufbau; vom Admin geänderte Berechtigungsstufen wirken zeitnah auch auf bestehende Sessions des betroffenen Benutzers; alle Zustände überstehen Neustarts in `data/*.db`; die Anwendung läuft ohne die entfernten `Page:*`-/`Relay:*`-Konfigurationseinträge |
| Umgang mit fehlenden Objekten | Kein Benutzer → Setup-Modus: Registrierung anbieten, ersten Benutzer mit Stufe `Administration` **erzeugen**. `data`-Verzeichnis/DB fehlt → beim Start **erzeugen** (inkl. Migrationen + Rollen-Seeding). Keine Relay-Tokens → `/relay` lehnt alle Verbindungen ab; Tokens nur über Admin-UI **erzeugt**. Gesperrter/deaktivierter Benutzer → Anmeldung und Zugriff verweigert; **aktivieren** nur durch Admin (Sperrung zusätzlich automatisch nach 1 h). Alle Admins ausgesperrt → kein Recovery; `data`-Verzeichnis **verwerfen**, Setup-Lauf von vorn. `ServerSetting`-Zeile fehlt → Default 480 min **wiederverwenden** (erst bei Admin-Änderung persistiert) |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein zusätzlicher Schutz des Registrierungsfensters (kein Setup-Code, keine localhost-Beschränkung) | Risiko bewusst vom Auftraggeber akzeptiert | bestätigt (Auftraggeber-Vorgabe) |
| 2 | Kein Recovery-Weg (kein Passwort-Reset per E-Mail, keine Recovery-Codes); Recovery = `data`-Verzeichnis löschen | Auftraggeber-Vorgabe („Kein Recovery-Weg — Admin-Lockout = Neuaufsetzen") | bestätigt (Auftraggeber-Vorgabe) |
| 3 | Keine Migration/kein Fallback für `Page:Username`, `Page:Password`, `Relay:Tokens`, `Relay:DevToken` | Auftraggeber-Vorgabe; Einträge werden schlicht entfernt | bestätigt (Auftraggeber-Vorgabe) |
| 4 | Keine Selbstregistrierung weiterer Benutzer — nach dem ersten Benutzer legt nur die Administration Konten an | Auftraggeber-Vorgabe | bestätigt (Auftraggeber-Vorgabe) |
| 5 | Keine dritte Berechtigungsstufe zum Start; Modell nur erweiterbar ausgelegt | Auftraggeber-Vorgabe; Rollenmodell bleibt für spätere Schreib-Stufen offen | bestätigt (Auftraggeber-Vorgabe) |
| 6 | Keine TLS-Durchsetzung in der Anwendung; HTTPS terminiert am Reverse-Proxy, HTTP im LAN zulässig | Auftraggeber-Vorgabe | bestätigt (Auftraggeber-Vorgabe) |
| 7 | Kein Admin-seitiges Passwort-Reset für andere Benutzer; bei vergessenem Passwort bleibt nur Konto löschen + neu anlegen (oder Deaktivieren) | Folge aus der geforderten Admin-Funktionsliste (nur Anlegen/Stufenänderung/Deaktivieren/Löschen/Sperrung zurücksetzen + Profil-Passwortänderung); ein fremdes Passwort-Reset ist nicht gefordert | bestätigt (Auftraggeber-Vorgabe, 2026-11-27) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| UI-Technik der Formularseiten | String-basiertes, serverseitig gerendertes HTML im `StatusPageComposer`-Stil (keine Razor Pages, kein JavaScript) | Annahme der Anforderung; konsistent mit bestehendem `StatusPageComposer` (391 Zeilen, bewährte `E`/`IsSafeHttpUrl`-Helfer); kein Razor-Infrastruktur-Neubau; Composer-Ausgaben sind mit einfachen String-Assertions testbar (Vorbild `StatusPageComposerTests`) |
| Formular-CSRF-Schutz | `IAntiforgery` aus `Microsoft.AspNetCore.Antiforgery` (Framework-Bestandteil): GET-Handler holen Token via `GetAndStoreTokens` und übergeben ihn an die Composer als Hidden Field; POST-Handler validieren via `ValidateRequestAsync`, gekapselt in einem gemeinsamen Endpoint-Filter | Standard-Mechanismus für Minimal APIs ohne MVC/Razor; kein Eigenbau |
| Endpunkt-Organisation | Route-Gruppierung über statische Extension-Methoden `MapAuthEndpoints`/`MapAdminEndpoints` (`Endpoints/`), Handler als Transaction Scripts pro Route; `Program.cs` bleibt Composition Root | Bei ~20 neuen Routen würde `Program.cs` (heute 70 Zeilen) unleserlich; Gruppierung folgt der Minimal-API-Konvention ohne MVC-Einführung |
| Datenbankpfad | Fester Default `Path.Combine(app.Environment.ContentRootPath, "data", "statusserver.db")`, intern überschreibbar über Konfigurationsschlüssel `Data:DatabasePath` | Anforderung: fester `data`-Pfad, kein geforderter Config-Key — der interne Key dient ausschließlich der Test-Isolation in `StatusWebApplicationFactory` |
| Identitätspersistenz | `StatusServerDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>` im Namespace `Softwareschmiede.StatusServer.Data`; `AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<…>()` | Standard-ASP.NET-Core-Pfad; `AddIdentity` (nicht `AddDefaultIdentity`) liefert `UserManager`/`SignInManager`/`RoleManager` ohne unerwünschtes Default-UI |
| `ServerSetting`-Persistenz | Schlüssel-Wert-Tabelle (`Key` als PK-String, `Value` als String); einziger initialer Schlüssel `SessionDurationMinutes` | Erweiterbar für spätere Admin-Einstellungen ohne neue Migration; einzeilige Typed-Entity-Alternative wäre zwar typsicherer, aber bei genau einem Wert nicht flexibler |
| Sitzungsdauer-Anwendung | `CookieAuthenticationEvents`-Ableger `StatusServerCookieEvents`: `OnSigningIn` setzt `Properties.ExpiresUtc = now + SessionDuration` (persistentes Cookie), `OnValidatePrincipal` erneuert `ExpiresUtc` (Sliding) und prüft `IsDeactivated` | `CookieAuthenticationOptions.ExpireTimeSpan` ist statisch und kann keinen DB-Wert lesen; Ticket-Eigenschaften sind der vorgesehene Angriffspunkt |
| Deaktivierungs-Durchsetzung | `IsDeactivated`-Flag auf `ApplicationUser`; Prüfung beim Login (nach erfolgreichem `PasswordSignInAsync` → sofort `SignOutAsync`) **und** in `OnValidatePrincipal` je Request (`RejectPrincipal`) | Nur der Security-Stamp-Mechanismus wäre durch `ValidationInterval` (Default 30 min) verzögert; die Anforderung verlangt sofortigen Zugriffsverlust auch bei bestehender Session |
| Stufenänderungs-Durchsetzung | `UserAdminService.SetRoleAsync` ruft nach dem Rollenwechsel `UserManager.UpdateSecurityStampAsync`; `StatusServerCookieEvents.OnValidatePrincipal` gleicht zusätzlich je Request die Rollen-Claims des Cookies mit `UserManager.GetRolesAsync` ab und ersetzt bei Abweichung den Principal (`ReplacePrincipal` + `ShouldRenew = true`) | Wirkung zeitnah (spätestens nächster Request) **ohne** Zwangs-Re-Login des betroffenen Benutzers — derselbe Hook setzt bereits die Deaktivierungs-Prüfung um und liest ohnehin den Benutzer aus der DB; der Security-Stamp deckt zusätzlich parallele Sessions und den Fallback ab, falls der Claim-Abgleich ausfiele |
| Setup-Umleitung | `StatusServerCookieEvents.OnRedirectToLogin`: existiert kein Benutzer → Redirect `/register`, sonst `/login` | Ein einziger Auth-Umleitungspunkt deckt alle geschützten Endpunkte ab; der erste Aufruf ohne Benutzer landet damit automatisch im Setup |
| Relay-Token-Verifikation | `RelayTokenValidator` als Scoped-Service mit `Task<bool> IsValidAsync(string?, CancellationToken)` gegen `StatusServerDbContext`: SHA-256-Hash des eingehenden Tokens bilden, Entity per `TokenHash` (Unique-Index) suchen, `RevokedUtc is null` prüfen; `SecretComparer.FixedTimeEquals` für den Hash-Vergleich nach dem Laden | DB-Lookup per Hash ist O(1) und der Hash-Vergleich selbst ist die Prüfung; `SecretComparer` bleibt als Konstantzeit-Vergleich im Einsatz; Verbindungsaufbauten sind selten genug für einen DB-Zugriff je Connect (kein Cache nötig) |
| Relay-Token-Erzeugung | `RelayTokenService`: 32 Bytes via `RandomNumberGenerator`, Base64url-Klartext; SHA-256-Hash + `Label` + `CreatedUtc` persistieren; Klartext ausschließlich in der Response-Seite des Create-Handlers (Einmal-Anzeige); Widerruf via `RevokedUtc`-Zeitstempel (Datensatz bleibt zur Nachvollziehbarkeit bestehen) | Anforderung: einmalig anzeigen, gehasht speichern, widerrufbar; Soft-Revoke statt Delete zeigt widerrufene Tokens in der Liste |
| Admin-Operationen | `UserAdminService` (Transaction Script) kapselt `UserManager`/`RoleManager`-Aufrufe inkl. `SetRoleAsync` und „letzter-Admin"-Schutz (Deaktivieren, Löschen **und** Herabstufung des letzten aktiven Administrators — gleichermaßen für eigenes und fremdes Konto) und liefert domänische Ergebnisse (ok / Ablehnungsgrund) | Bündelt die nicht-triviale Schutzregel an einer testbaren Stelle statt sie über die Handler zu verteilen; verhindert den unrettbaren Zustand „alle Admins ausgesperrt" proaktiv in der UI |
| Hub-Anbindung | `StatusRelayHub.OnConnectedAsync` wird `async` und ruft `await IsValidAsync`; Konstruktor-Injektion des Scoped-Validators bleibt (Hubs werden pro Invocation im Verbindungs-Scope erzeugt) | Token-Transport (`access_token`-Query, Bearer-Fallback) bleibt exakt wie gefordert unverändert |
| Zugriffsverweigerung (403) | Eigene anonyme Seite `/access-denied` über `AuthPagesComposer.RenderAccessDenied`; `AccessDeniedPath` zeigt dorthin | Klare, verständliche Blockaden-Meldung statt leerer 403-Seite; Überwacher sieht erklärenden Text |
| Benutzernamen | Frei wählbarer Benutzername (kein E-Mail-Zwang, `User.RequireUniqueEmail`/`RequireConfirmedEmail` bleiben aus); nur Eindeutigkeit durch Identity erzwungen | Kein E-Mail-Versand im Scope; Benutzername dient nur der Anmeldung |

## Programmabläufe

### Server-Start und Datenbank-Initialisierung

1. `Program.cs` liest `Data:DatabasePath` (Default `<ContentRoot>/data/statusserver.db`), legt das Verzeichnis via `Directory.CreateDirectory` an (Muster aus `App.xaml.cs` Z. 192).
2. `builder.Services.AddDbContext<StatusServerDbContext>(UseSqlite)`; `AddIdentity<ApplicationUser, IdentityRole>` mit `IdentityOptions` (`Password.RequiredLength = 8`, `Lockout.MaxFailedAccessAttempts = 3`, `Lockout.DefaultLockoutTimeSpan = 1 h`, `Lockout.AllowedForNewUsers = true`) und `AddEntityFrameworkStores<StatusServerDbContext>`; `AddAntiforgery`; `AddAuthorization` mit Policies `PageAccess` (`RequireRole` beide Stufen) und `AdminOnly` (`RequireRole(Administration)`); `ConfigureApplicationCookie` (`LoginPath = /login`, `AccessDeniedPath = /access-denied`, `Secure = SameAsRequest`, `SameSite = Lax`, Events-Typ `StatusServerCookieEvents`); `Configure<ForwardedHeadersOptions>` (`ForwardedHeaders.All`, KnownNetworks/KnownProxies geleert für Self-Hosting).
3. `app.Run` vorab: `using var scope = app.Services.CreateScope()` → `StatusServerDatabaseInitializer.InitializeAsync(scope.ServiceProvider)`: `db.Database.MigrateAsync()` + `RoleManager` stellt sicher, dass `Administration` und `Ueberwacher` existieren (idempotent).
4. Pipeline: `UseForwardedHeaders` → `UseAuthentication` → `UseAuthorization` → `UseAntiforgery` → Endpunkte. `PageAccessMiddleware` entfällt.

Beteiligte Klassen: `Program`, `StatusServerDbContext`, `StatusServerDatabaseInitializer`, `StatusServerCookieEvents`, `ApplicationUser`, `IdentityRole`.

### Erst-Registrierung (Setup)

1. `GET /` ohne Session → `OnRedirectToLogin` prüft `UserManager.Users.Any()` → Redirect `/register` (kein Benutzer vorhanden).
2. `GET /register` (anonym): Handler prüft erneut `Users.Any()` — falls doch Benutzer existieren → Redirect `/`; sonst `IAntiforgery.GetAndStoreTokens` + `AuthPagesComposer.RenderRegister(token, fehler: null)`.
3. `POST /register` (anonym): Antiforgery-Validierung → Guard `!Users.Any()` (sonst Redirect `/`); Validierung (Benutzername nicht leer, Passwort ≥ 8, Passwort-Wiederholung identisch) → `UserAdminService.RegisterFirstAdminAsync` erzeugt `ApplicationUser`, weist Rolle `Administration` zu, `SignInManager.SignInAsync` → Redirect `/`.
4. Ab jetzt liefert `GET /register`/`POST /register` Redirects — die Registrierung ist dauerhaft geschlossen (kein zusätzlicher Schutz, Ausschluss 1).

Beteiligte Klassen: `AuthEndpoints`, `UserAdminService`, `SignInManager`, `AuthPagesComposer`, `IAntiforgery`.

### Anmeldung

1. `GET /` oder `GET /status.json` ohne Session → `OnRedirectToLogin` → Redirect `/login`.
2. `GET /login` (anonym): Antiforgery-Token + `AuthPagesComposer.RenderLogin(token, fehler: null)`; bereits angemeldete Benutzer werden zu `/` weitergeleitet.
3. `POST /login`: Antiforgery → `SignInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true)`.
   - `Succeeded` → `IsDeactivated`-Prüfung: deaktiviert → `SignOutAsync` + Login-Seite mit Meldung „Konto deaktiviert"; aktiv → `OnSigningIn` setzt `ExpiresUtc` aus `ServerSettingsService` → Redirect `/`.
   - `IsLockedOut` → Login-Seite mit Sperr-Hinweis (1 h bzw. Admin-Freischaltung).
   - Sonst → Login-Seite mit generischem Fehler (keine Unterscheidung Benutzer/Passwort).
4. Fehlversuche zählen über `AccessFailedCount`; ab dem 3. `LockoutEnabled`-Lockout für `DefaultLockoutTimeSpan`.

Beteiligte Klassen: `AuthEndpoints`, `SignInManager`, `UserManager`, `StatusServerCookieEvents`, `ServerSettingsService`, `AuthPagesComposer`.

### Abmeldung

1. `POST /logout` (Policy `PageAccess`, Antiforgery): `SignInManager.SignOutAsync` → Redirect `/login`.
2. Logout-Formular ist als kleiner POST-Button im Seiten-Header aller geschützten Seiten eingebettet (Antiforgery-Hidden-Field wird vom jeweiligen GET-Handler erzeugt).

Beteiligte Klassen: `AuthEndpoints`, `SignInManager`, `PageShell`.

### Cookie-Revalidierung je Request

1. `StatusServerCookieEvents.OnValidatePrincipal` läuft bei jeder authentifizierten Anfrage: `ApplicationUser` laden → `IsDeactivated` → `RejectPrincipal()` + `SignOutAsync` (sofortiger Zugriffsverlust).
2. Rollen-Abgleich: `UserManager.GetRolesAsync(user)` gegen die Rollen-Claims des Cookie-Principals → bei Abweichung (Stufe wurde inzwischen geändert) `context.ReplacePrincipal(await SignInManager.CreateUserPrincipalAsync(user))` + `context.ShouldRenew = true` — die neue Stufe gilt ab diesem bzw. dem nächsten Request, ohne dass der Benutzer sich neu anmelden muss.
3. Aktiv → `Properties.ExpiresUtc` auf `now + SessionDuration` aus `ServerSettingsService` erneuern (Sliding Expiration).
4. Identities Security-Stamp-Prüfung läuft zusätzlich im Standard-Intervall (Passwortänderung/Löschen/Rollenwechsel invalidiert Sessions spätestens dann).

Beteiligte Klassen: `StatusServerCookieEvents`, `UserManager`, `ServerSettingsService`.

### Passwortänderung im Profil

1. `GET /profile` (Policy `PageAccess`): `AuthPagesComposer.RenderProfile(token, user, meldung)` — zeigt Benutzername, Rolle, Formular „Passwort ändern" (aktuell, neu ≥ 8, Wiederholung).
2. `POST /profile/password` (Policy `PageAccess`, Antiforgery): `UserManager.ChangePasswordAsync(user, alt, neu)` → Erfolg: `RefreshSignInAsync` (neuer Security Stamp, Session bleibt) + Meldung; Fehler: Profil-Seite mit Fehlermeldung (u. a. falsches altes Passwort, zu kurzes Passwort).
3. Zu kurzes/falsches Passwort wird via `IdentityResult.Errors` in verständliche Meldung übersetzt.

Beteiligte Klassen: `AuthEndpoints`, `UserManager`, `SignInManager`, `AuthPagesComposer`.

### Admin: Benutzer anlegen

1. `GET /admin/users` (Policy `AdminOnly`): `AdminPagesComposer.RenderUserList(users, token, meldung)` — Tabelle (Name, Stufe, Status: aktiv/gesperrt bis/deaktiviert) plus Anlege-Formular (Benutzername, Passwort, Stufe-Auswahl `Administration`/`Ueberwacher`).
2. `POST /admin/users` (Policy `AdminOnly`, Antiforgery): Validierung (Name nicht leer/eindeutig, Passwort ≥ 8, Stufe ∈ Rollen) → `UserAdminService.CreateUserAsync` → Liste mit Erfolgsmeldung oder Fehler.

Beteiligte Klassen: `AdminEndpoints`, `UserAdminService`, `UserManager`, `RoleManager`, `AdminPagesComposer`.

### Admin: Benutzer deaktivieren/aktivieren und löschen

1. `POST /admin/users/{id}/deactivate` bzw. `/activate` (Policy `AdminOnly`, Antiforgery): `UserAdminService.SetDeactivatedAsync(id, flag)`; Deaktivieren setzt zusätzlich `UserManager.UpdateSecurityStampAsync` (Sessions sterben sofort per `OnValidatePrincipal`).
2. `POST /admin/users/{id}/delete`: `UserAdminService.DeleteUserAsync`.
3. Letzter-Admin-Schutz in `UserAdminService`: Aktion würde den letzten aktiven Benutzer mit Rolle `Administration` deaktivieren, löschen oder auf `Ueberwacher` herabstufen → Ablehnung mit klarem Grund — gilt gleichermaßen für das eigene wie für fremde Konten; `AdminPagesComposer` deaktiviert die entsprechenden Buttons zusätzlich mit Hinweistitel (keine wirkungslose scheinbar aktive Aktion).
4. Alle Antworten rendern die Benutzerliste erneut mit Meldung.

Beteiligte Klassen: `AdminEndpoints`, `UserAdminService`, `UserManager`, `AdminPagesComposer`.

### Admin: Berechtigungsstufe ändern

1. `GET /admin/users` (Policy `AdminOnly`) rendert je Benutzer ein Stufen-Control in `AdminPagesComposer.RenderUserList`: die jeweils andere Stufe als POST-Aktion (z. B. „Zu Überwacher herabstufen" / „Zu Administration befördern"); beim letzten aktiven Admin ist die Herabstufung deaktiviert und mit Hinweistitel begründet.
2. `POST /admin/users/{id}/role` (Policy `AdminOnly`, Antiforgery): Syntax-Validierung (Zielstufe ∈ {`Administration`, `Ueberwacher`}, ungleich der aktuellen Stufe) → `UserAdminService.SetRoleAsync(id, stufe)`: Letzter-Admin-Prüfung → `RemoveFromRoleAsync`/`AddToRoleAsync` → `UpdateSecurityStampAsync` → Benutzerliste mit Erfolg- oder Ablehnungsmeldung.
3. Wirkung auf bestehende Sessions des betroffenen Benutzers: ab dem nächsten authentifizierten Request greift der Rollen-Claim-Abgleich in `OnValidatePrincipal` (`ReplacePrincipal` + `ShouldRenew`) — ein herabgestufter Admin verliert den Admin-Zugriff zeitnah, ein hochgestufter Überwacher erhält ihn ohne erneute Anmeldung; parallele Sessions desselben Benutzers werden spätestens über den Security-Stamp erfasst.

Beteiligte Klassen: `AdminEndpoints`, `UserAdminService`, `UserManager`, `RoleManager`, `StatusServerCookieEvents`, `AdminPagesComposer`.

### Admin: Sperrung zurücksetzen

1. `POST /admin/users/{id}/unlock` (Policy `AdminOnly`, Antiforgery): `UserAdminService.ResetLockoutAsync` → `SetLockoutEndDateAsync(user, null)` + `ResetAccessFailedCountAsync` → Liste mit Erfolgsmeldung.
2. Unlock-Button wird nur bei `LockoutEnd > now` aktiviert gerendert.

Beteiligte Klassen: `AdminEndpoints`, `UserAdminService`, `UserManager`, `AdminPagesComposer`.

### Admin: Relay-Token erstellen und widerrufen

1. `GET /admin/tokens` (Policy `AdminOnly`): `AdminPagesComposer.RenderTokenList(tokens, token, meldung, createdToken: null)` — Tabelle (Label, Erstellt, Widerrufen) plus Anlege-Formular (Label).
2. `POST /admin/tokens` (Policy `AdminOnly`, Antiforgery): `RelayTokenService.CreateAsync(label)` → Seite `RenderTokenList(…, createdToken: plaintext)` — Klartext einmalig in einem markanten Block mit Hinweis „wird nur jetzt angezeigt".
3. `POST /admin/tokens/{id}/revoke`: `RelayTokenService.RevokeAsync(id)` setzt `RevokedUtc` → Liste zeigt Token als widerrufen (deaktivierter Revoke-Button).
4. Ohne Tokens zeigt die Liste einen Hinweis, dass `/relay` alle Verbindungen ablehnt (analog zum Empty-State-Muster in `StatusPageComposer`).

Beteiligte Klassen: `AdminEndpoints`, `RelayTokenService`, `StatusServerDbContext`, `AdminPagesComposer`.

### Admin: Sitzungsdauer einstellen

1. `GET /admin/settings` (Policy `AdminOnly`): `AdminPagesComposer.RenderSettings(sessionMinutes, token, meldung)` — Zahleneingabe in Minuten.
2. `POST /admin/settings/session-duration` (Policy `AdminOnly`, Antiforgery): Validierung (Ganzzahl 5–43200) → `ServerSettingsService.SetSessionDurationAsync` → Erfolgsmeldung; gilt ab der nächsten Cookie-Ausstellung/-Erneuerung (`OnValidatePrincipal`-Pfad), bestehende Tickets werden bei ihrer Erneuerung angepasst.

Beteiligte Klassen: `AdminEndpoints`, `ServerSettingsService`, `StatusServerDbContext`, `AdminPagesComposer`.

### Relay-Verbindungsaufbau mit DB-Token

1. Client öffnet SignalR-Verbindung `/relay?access_token=…` (oder Bearer-Header bei Nicht-WS-Transport) — Transport unverändert in `StatusRelayHub.OnConnectedAsync`.
2. `await RelayTokenValidator.IsValidAsync(token, ct)` → SHA-256-Hash → DB-Lookup → Treffer mit `RevokedUtc is null` → Verbindung läuft (`RegisterInstance`/`PushSnapshot` unverändert).
3. Kein Treffer oder widerrufen → Warn-Log + `Context.Abort()` (unverändert).

Beteiligte Klassen: `StatusRelayHub`, `RelayTokenValidator`, `StatusServerDbContext`, `SecretComparer`.

### Zugriffssteuerung der Seiten

1. `/` und `/status.json` tragen `RequireAuthorization("PageAccess")` (beide Rollen); Admin-Routen `RequireAuthorization("AdminOnly")`.
2. Nicht authentifiziert → Redirect `/login` (bzw. `/register` im Setup-Modus); authentifiziert als `Ueberwacher` auf Admin-Routen → 403 → `AccessDeniedPath` → `/access-denied` (klar erklärte Blockade).
3. `/register`, `/login`, `/access-denied` und `/relay` bleiben ohne Cookie-Authorization erreichbar (`/relay` schützt sich selbst per Token).

Beteiligte Klassen: `Program`, `AuthEndpoints`, `AdminEndpoints`, `StatusServerCookieEvents`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `Data/ApplicationUser` | Datenmodellklasse (`IdentityUser`-Ableger) | Benutzerkonto mit `IsDeactivated`-Flag |
| `Data/RelayToken` | Datenmodellklasse | Relay-Token-Datensatz: `Id` (long), `Label`, `TokenHash` (Unique-Index), `CreatedUtc`, `RevokedUtc` (nullable) |
| `Data/ServerSetting` | Datenmodellklasse | Schlüssel-Wert-Settings: `Key` (PK), `Value`; initialer Schlüssel `SessionDurationMinutes` |
| `Data/StatusServerDbContext` | Klasse (`IdentityDbContext<ApplicationUser, IdentityRole, string>`) | EF-Core-Kontext mit `DbSet<RelayToken>`, `DbSet<ServerSetting>`, Fluent-Konfiguration inkl. Unix-ms-`DateTimeOffset`-Konvertern (Konvention `SoftwareschmiedeDbContext`) |
| `Data/StatusServerDbContextDesignTimeFactory` | Klasse (`IDesignTimeDbContextFactory<…>`) | Design-Time-Kontext für `dotnet ef migrations add` (Konvention `SoftwareschmiedeDbContextDesignTimeFactory`) |
| `Data/StatusServerDatabaseInitializer` | Klasse (intern, statisch) | `InitializeAsync(IServiceProvider, CancellationToken)`: `MigrateAsync` + Rollen-Seeding (`Administration`, `Ueberwacher`) |
| `Security/StatusRoles` | Klasse (Konstanten) | `Administration`, `Ueberwacher` sowie Policy-Namen `PageAccess`/`AdminOnly` |
| `Security/StatusServerCookieEvents` | Klasse (`CookieAuthenticationEvents`-Ableger) | `OnSigningIn` (ExpiresUtc aus Settings), `OnValidatePrincipal` (Deaktivierungs-Prüfung + Rollen-Claim-Abgleich mit `ReplacePrincipal`/`ShouldRenew` + Sliding-Refresh), `OnRedirectToLogin` (Setup-aware Redirect) |
| `Security/RelayTokenService` | Klasse (Scoped) | `CreateAsync(label)` → Entität + einmaliger Klartext; `RevokeAsync(id)`; `ListAsync()` |
| `Security/ServerSettingsService` | Klasse (Scoped) | `GetSessionDurationAsync()` (Default 480 min), `SetSessionDurationAsync(minutes)` gegen `ServerSetting` |
| `Security/UserAdminService` | Klasse (Scoped, Transaction Script) | `RegisterFirstAdminAsync`, `CreateUserAsync(name, pw, role)`, `SetRoleAsync(id, role)`, `SetDeactivatedAsync`, `DeleteUserAsync`, `ResetLockoutAsync`, `ListUsersAsync`, Letzter-Admin-Schutz |
| `Security/RelayTokenValidator` (umbenannt/umbaut) | Klasse (Scoped) | `IsValidAsync(string?, CancellationToken)` — DB-Lookup per SHA-256-Hash, `SecretComparer`-Vergleich, `RevokedUtc`-Prüfung |
| `Pages/PageShell` | Klasse (intern, statisch) | Gemeinsamer HTML-Rahmen (doctype/head/CSS/Footer) + Seiten-Navigation (Status, Profil, Abmelden-Formular, Admin-Links nur für `Administration`) — extrahierte Gemeinsamkeit aller Composer |
| `Pages/PageUserContext` | Record | Kontext für Composer/Shell: `UserName`, `IsAdmin`, `AntiforgeryToken`, `AntiforgeryFieldName` |
| `Pages/AuthPagesComposer` | Klasse | `RenderRegister`, `RenderLogin`, `RenderProfile`, `RenderAccessDenied` — Formulare mit Fehler-/Erfolgsmeldungen, deutschem Text, `E`-Encoding |
| `Pages/AdminPagesComposer` | Klasse | `RenderUserList` (inkl. Anlege-Formular und Stufen-Änderungs-Control je Benutzer), `RenderTokenList` (inkl. Einmal-Klartext-Block), `RenderSettings` |
| `Endpoints/AuthEndpoints` | Klasse (statisch, Extension) | `MapAuthEndpoints`: `/register`, `/login`, `/logout`, `/profile`, `/profile/password`, `/access-denied` |
| `Endpoints/AdminEndpoints` | Klasse (statisch, Extension) | `MapAdminEndpoints`: `/admin/users*` (inkl. `…/{id}/role`), `/admin/tokens*`, `/admin/settings*` (jeweils Policy `AdminOnly`) |
| `Endpoints/EndpointSecurityExtensions` | Klasse (intern, statisch) | Endpoint-Filter für Antiforgery-Validierung auf allen POST-Routen |

## Änderungen an bestehenden Klassen

### `Program` (Composition Root, `server/Softwareschmiede.StatusServer/Program.cs`)

- **Neu:** `AddDbContext<StatusServerDbContext>` mit `Data:DatabasePath`-Default `data/statusserver.db` + `Directory.CreateDirectory`; `AddIdentity<ApplicationUser, IdentityRole>` + `IdentityOptions` (Passwort ≥ 8, Lockout 3/1 h); `AddAntiforgery`; `AddAuthorization` mit Policies `PageAccess`/`AdminOnly`; `ConfigureApplicationCookie` (LoginPath `/login`, AccessDeniedPath `/access-denied`, `Secure = SameAsRequest`, `SameSite = Lax`, `ExpireTimeSpan`-Fallback, `SlidingExpiration = true`, `EventsType = StatusServerCookieEvents`); `Configure<ForwardedHeadersOptions>` (`ForwardedHeaders.All`, KnownProxies/KnownNetworks leeren); Scoped-Registrierungen `RelayTokenService`, `ServerSettingsService`, `UserAdminService`, `RelayTokenValidator`; `StatusServerCookieEvents` registrieren.
- **Neu:** Start-Block `StatusServerDatabaseInitializer.InitializeAsync` vor `app.Run`.
- **Geänderte Pipeline:** `UseMiddleware<PageAccessMiddleware>` entfällt; neu `UseForwardedHeaders`, `UseAuthentication`, `UseAuthorization`, `UseAntiforgery`.
- **Geänderte Endpunkte:** `MapGet("/")` erhält `RequireAuthorization("PageAccess")` und zusätzlich `HttpContext`/`IAntiforgery`-Parameter (für Nav-Kontext/Logout-Token); `MapGet("/status.json")` erhält `RequireAuthorization("PageAccess")`; `MapHub<StatusRelayHub>` bleibt ohne Cookie-Auth (Token im Hub); neu `app.MapAuthEndpoints()` + `app.MapAdminEndpoints()`.
- **`/` -Handler-Änderung:** übergibt `PageUserContext` (Name, `IsAdmin`, Antiforgery-Token) an `StatusPageComposer.Render`.

### `PageAccessMiddleware` — wird vollständig entfernt

Datei `server/Softwareschmiede.StatusServer/Security/PageAccessMiddleware.cs` löschen (ersetzt durch Cookie-Auth + `RequireAuthorization`).

### `RelayTokenValidator` (Scoped-Service)

- **Geändert:** kompletter Umbau — Konstruktor `(StatusServerDbContext)` statt `(IOptions<StatusServerOptions>, IHostEnvironment)`; `bool IsValid(string?)` → `Task<bool> IsValidAsync(string? token, CancellationToken)`; Prüfung: leer → `false`; SHA-256-Hash → DB-Lookup `RelayTokens` (`TokenHash`, `RevokedUtc == null`); Dev-Token-Pfad entfällt.

### `StatusRelayHub` (Hub)

- **Geänderte Methode:** `OnConnectedAsync` → `async`, `await _tokenValidator.IsValidAsync(token, Context.ConnectionAborted)` statt sync `IsValid`; Rest unverändert.

### `StatusPageComposer` (Renderer)

- **Geänderte Methode:** `Render` erhält zusätzlichen Parameter `PageUserContext user` (Name, `IsAdmin`, Antiforgery-Token/Feldname); `RenderHeader`/Seitenrahmen erzeugt über `PageShell` die Navigationszeile (Profil, Abmelden-Formular, Admin-Links nur bei `IsAdmin`).

### `StatusServerOptions` / `PageOptions` / `RelayOptions` (`Options/StatusServerOptions.cs`)

- **Entfernt:** `StatusServerOptions.Relay` + komplette Klasse `RelayOptions`; `PageOptions.Username`, `PageOptions.Password`.
- **Bleibt:** `App`, `GitHub`, `Page.RefreshSeconds`.

### `appsettings.json` / `appsettings.Development.json`

- **Entfernt:** `Relay`-Sektion (`Tokens`, `DevToken`), `Page.Username`, `Page.Password`; `appsettings.Development.json` enthielt nur `Relay:DevToken` → Datei reduzieren auf leeres Objekt oder belassen mit Standard-Logging.

### `server/README.md`, `README.md`, `docs/RELEASE_NOTES.md`, `docs/help/einstellungen/remote-uebersicht.md`

- **`server/README.md`:** Endpunkte-Tabelle (Login statt Basic-Auth), Setup-Lauf beim ersten Start, Token-Verwaltung über Admin-UI statt Config-Rotation, `data`-Verzeichnis + Recovery (`data` löschen), entfernte Config-Keys, `X-Forwarded-Proto`-Hinweis im nginx-Abschnitt, systemd-Env-Beispiele bereinigen, „Vor dem Livegang" aktualisieren.
- **`README.md` (Z. 220–221):** „hinter Basic-Auth" → Cookie-Anmeldung mit Rollen.
- **`docs/RELEASE_NOTES.md`:** bestehenden StatusServer-Eintrag (Issue-478-Auth-Beschreibung) auf neuen Stand bringen.
- **`docs/help/einstellungen/remote-uebersicht.md`:** Token-Bezugsweg ändern: Erzeugung in der StatusServer-Admin-UI statt Server-Config.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `InitialCreate` | `AspNetUsers` (+ `IsDeactivated`), `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`, `RelayTokens` (`Id`, `Label`, `TokenHash` + Unique-Index, `CreatedUtc`, `RevokedUtc`), `ServerSettings` (`Key`, `Value`) | Erstmigration für `StatusServerDbContext`; `DateTimeOffset`-Spalten als Unix-ms-`long` (Konvention `SoftwareschmiedeDbContext`); Migrationen eingecheckt unter `server/Softwareschmiede.StatusServer/Migrations/` |

Erzeugung via `dotnet ef migrations add InitialCreate -p server/Softwareschmiede.StatusServer` mit `StatusServerDbContextDesignTimeFactory`.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Registrierung: Benutzername | Nicht leer, eindeutig (Identity) | Formular erneut mit Fehlermeldung |
| Registrierung/Anlegen/Profil: Passwort | ≥ 8 Zeichen (`IdentityOptions.Password.RequiredLength`), Wiederholung identisch, bei Profil korrektes altes Passwort | Formular erneut mit Fehlermeldung; Identity-Fehlerliste in Klartext übersetzt |
| Registrierung (Guard) | Nur solange `UserManager.Users.Any() == false` — serverseitig im GET **und** POST | Redirect `/` |
| Admin-Anlegen / Stufenänderung: Stufe | Wert ∈ {`Administration`, `Ueberwacher`}; bei Stufenänderung zusätzlich ungleich der aktuellen Stufe | 400/Fehlermeldung |
| Letzter Admin | Deaktivieren/Löschen/Herabstufen des letzten aktiven `Administration`-Benutzers abgelehnt — gleichermaßen für das eigene wie für fremde Konten | Liste mit klarer Fehlermeldung; Button in UI deaktiviert + Hinweistitel |
| Sitzungsdauer | Ganzzahl in Minuten, Bereich 5–43200 | Settings-Seite mit Fehlermeldung |
| Token-Label | 1–200 Zeichen, nicht leer | Token-Seite mit Fehlermeldung |
| Alle POST-Routen | Antiforgery-Token (`IAntiforgery.ValidateRequestAsync` über Endpoint-Filter) | 400 Bad Request |
| Login | `PasswordSignInAsync` mit `lockoutOnFailure: true`; deaktivierter Benutzer wird nach erfolgreicher Prüfung sofort abgemeldet | Sperr-Hinweis bzw. „Konto deaktiviert" bzw. generischer Fehler |
| Admin-Routen | Policy `AdminOnly` | 403 → `/access-denied`-Seite |

Semantische Regeln (Letzter-Admin-Schutz, Deaktivierung, Widerruf-Semantik) liegen in `UserAdminService`/`RelayTokenService`; die HTTP-Handler validieren nur Syntax und Pflichtfelder.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Relay:Tokens`, `Relay:DevToken` | — | entfernt | Tokens kommen aus der Datenbank |
| `Page:Username`, `Page:Password` | — | entfernt | Basic-Auth ersetzt durch Cookie-Anmeldung |
| `Data:DatabasePath` | `string?` (intern) | `<ContentRoot>/data/statusserver.db` | Interner Override für Test-Isolation; kein dokumentierter Betriebs-Key (optional in README erwähnen) |
| `Page:RefreshSeconds`, `App:StaleAfterSeconds`, `GitHub:*` | unverändert | — | bleiben |

Zur Laufzeit neu (Datenbank, nicht `appsettings`): `ServerSettings["SessionDurationMinutes"]`, Default 480, nur via Admin-UI gesetzt.

## Seiteneffekte und Risiken

- **`StatusPageComposer.Render`-Signatur:** neuer `PageUserContext`-Parameter → alle 9 `StatusPageComposerTests` müssen angepasst werden; der `/`-Handler muss Antiforgery-Token + Rollenkontext erzeugen.
- **`StatusRelayRoundtripTests`:** `/` und `/status.json` sind künftig authentifiziert — Tests brauchen einen angemeldeten `HttpClient`; Token-Seeding erfolgt per DB statt `UseSetting`.
- **`StatusWebApplicationFactory`:** `RelayTokens`/`PageUsername`/`PagePassword`/`DevToken` entfallen; pro Factory-Instanz muss eine isolierte SQLite-DB (`Data:DatabasePath` → Temp-Datei) gesetzt werden; `IAsyncDisposable` für Temp-Verzeichnis-Aufräumen.
- **`CS1591`-Warnungen-als-Fehler:** alle neuen öffentlichen Member brauchen XML-Doc (csproj-Vorgabe) — betrifft alle neuen Klassen.
- **Lockout-Zeitablauf:** ASP.NET Core Identity löst `TimeProvider` seit .NET 8 aus DI auf — in der `StatusWebApplicationFactory` wäre prinzipiell ein steuerbarer `TimeProvider` registrierbar, um den Auto-Unlock nach 1 h vorzuspulen. Geplant ist die robustere, einfachere Variante: `Login_ThreeFailures_LocksUser_AdminUnlockRestores` assertiert `LockoutEnd ≈ now + 1 h` (belegt die konfigurierte Sperrdauer) plus die sofort wirksame Admin-Freischaltung; ein Vorspulen des Zeitablaufs ist dafür nicht erforderlich.
- **Persistenter Cookie + `Secure=SameAsRequest`:** im LAN über HTTP funktioniert der Login; hinter HTTPS-Proxy muss `X-Forwarded-Proto` ankommen (ForwardedHeaders + README-Hinweis) — sonst wird der Cookie vom Browser verworfen.
- **`OnValidatePrincipal` liest DB je Request:** SQLite-Lokalzugriff pro authentifizierter Anfrage; bei den erwarteten Zugriffszahlen unkritisch (keine Caching-Schicht geplant).
- **Race beim Setup:** zwei gleichzeitige Erst-Registrierungen — DB-Unique-Constraint auf `NormalizedUserName` + `Users.Any()`-Guard im POST; ein Duplikat schlägt mit Identity-Fehler fehl (akzeptiert, Setup-Fenster bewusst ungeschützt).
- **`launchSettings.json` fehlt im Repo:** `dotnet run`-Port abweichend — README-Hinweis beibehalten, kein Planpunkt.
- **`dotnet-ef`-Werkzeug:** für die Migrationserzeugung nötig (ggf. `dotnet tool install -g dotnet-ef`); nur Entwicklungsvoraussetzung, keine Laufzeitabhängigkeit.
- **Release-Publish (self-contained, `release.yml`):** neue Pakete (EF Core, SQLite-Native) werden mitverpackt; Smoke-Test des Publish-Outputs nicht Teil dieser Änderung, aber `dotnet build`/`dotnet publish` des Projekts muss fehlerfrei bleiben.

## Umsetzungsreihenfolge

1. **Paketreferenzen ergänzen**
   - Voraussetzungen: keine.
   - `server/Softwareschmiede.StatusServer/Softwareschmiede.StatusServer.csproj`: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) je `11.0.0` hinzufügen; Build prüfen.

2. **Datenmodell + DbContext + Initializer + Migration**
   - Voraussetzungen: Schritt 1 (Pakete), `dotnet-ef`-Werkzeug verfügbar.
   - `Data/ApplicationUser`, `Data/RelayToken`, `Data/ServerSetting`, `Data/StatusServerDbContext` (Fluent-Konfiguration, Unix-ms-Konverter nach `SoftwareschmiedeDbContext`-Vorbild), `Data/StatusServerDbContextDesignTimeFactory`, `Data/StatusServerDatabaseInitializer`; Migration `InitialCreate` erzeugen und einchecken.

3. **Security-Services**
   - Voraussetzungen: Schritt 2 (Entities/DbContext).
   - `Security/StatusRoles`, `Security/ServerSettingsService`, `Security/RelayTokenService`, Umbau `Security/RelayTokenValidator`, `Security/UserAdminService` (inkl. `SetRoleAsync` und Letzter-Admin-Schutz für Deaktivieren/Löschen/Herabstufen), `Security/StatusServerCookieEvents` (inkl. Rollen-Claim-Abgleich).

4. **UI-Composer**
   - Voraussetzungen: keine harten (nur `PageShell`-Abhängigkeiten intern); sinnvoll nach Schritt 3 für `StatusRoles`.
   - `Pages/PageShell`, `Pages/PageUserContext`, `Pages/AuthPagesComposer`, `Pages/AdminPagesComposer` im `StatusPageComposer`-Stil; `StatusPageComposer.Render` um `PageUserContext` erweitern (Navigationszeile).

5. **Endpunkte + Program.cs-Umbau + Options-/Config-Bereinigung**
   - Voraussetzungen: Schritte 2–4.
   - `Endpoints/AuthEndpoints`, `Endpoints/AdminEndpoints`, `Endpoints/EndpointSecurityExtensions`; `Program.cs`: Identity/Cookie/Antiforgery/ForwardedHeaders/Authorization registrieren, `PageAccessMiddleware` und `UseMiddleware`-Aufruf entfernen, Auth-Policies auf `/`/`/status.json`, Initializer-Aufruf, `PageAccessMiddleware.cs` löschen; `StatusServerOptions`/`PageOptions`/`RelayOptions` bereinigen; `appsettings*.json` bereinigen.

6. **Dokumentation**
   - Voraussetzungen: Schritt 5 (verbindliches Verhalten).
   - `server/README.md`, `README.md`, `docs/RELEASE_NOTES.md`, `docs/help/einstellungen/remote-uebersicht.md` anpassen.

7. **Testinfrastruktur umbauen**
   - Voraussetzungen: Schritt 5 (lauffähiger Server).
   - `StatusWebApplicationFactory`: Config-Seeding entfernen, `Data:DatabasePath` auf Temp-Datei je Factory setzen, Cleanup; neue Test-Helfer `AuthTestClient` (Cookie-fähiger Client, Antiforgery-Extraktion aus HTML, Login/Register/Post-Formular) und `DbSeeder`/`DbInspector` (Scope-basierter Zugriff: Benutzer anlegen, Token seeden, Lockout-Status prüfen).

8. **Bestandstests anpassen + neue Unit-/Integrationstests**
   - Voraussetzungen: Schritt 7.
   - `PageAccessTests` → neue `Auth/SetupAndAuthTests` ersetzen; `Security/RelayTokenValidatorTests` auf DB umbauen; `Relay/StatusRelayRoundtripTests` (DB-Token-Seeding + authentifizierter Client); `StatusPageComposerTests` (neue Signatur); neue Composer-/Service-Tests.

9. **E2E-Benutzerfluss-Tests (WebApplicationFactory-HTTP-Flows)**
   - Voraussetzungen: Schritte 7–8.
   - Neue `E2E/StatusServerUserFlowTests` mit dem vollständigen Happy Path und den Fehlerflüssen (siehe E2E-Abschnitt).

10. **Gesamtverifikation**
    - Voraussetzungen: Schritte 1–9.
    - `dotnet build Softwareschmiede.slnx -c Debug`, `dotnet test server/Softwareschmiede.StatusServer.Tests/…` vollständig, `dotnet format Softwareschmiede.slnx --verify-no-changes`; danach reguläre Lanes der anderen Projekte unverändert grün.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AuthTestClient` | `TestSupport/AuthTestClient.cs` | Hilfsklasse: `HttpClient` mit Cookie-Container (`WebApplicationFactoryClientOptions.HandleCookies = true`), Antiforgery-Token-Extraktion aus gerendertem HTML, `PostFormAsync(url, fields)`, `RegisterFirstUserAsync`, `LoginAsync`, `LogoutAsync` |
| `StatusServerDbSeeder` | `TestSupport/StatusServerDbSeeder.cs` | Hilfsklasse: Scope über `factory.Services`, Methoden `CreateUserAsync(name, pw, role)`, `SeedRelayTokenAsync(label)` → Klartext, `SetDeactivatedAsync`, `GetLockoutEndAsync`, `SetSessionDurationAsync` |
| `Register_WithoutUsers_CreatesFirstAdmin_AndSignsIn` | `Auth/SetupAndAuthTests` | Setup-Happy-Path: `GET /` → Redirect `/register` → POST → Redirect `/`, Benutzer hat Rolle `Administration`, authentifizierter Zugriff auf `/` |
| `Register_WithExistingUsers_IsClosed` | `Auth/SetupAndAuthTests` | `GET`/`POST /register` bei vorhandenen Benutzern → kein zweiter Benutzer (Redirect, keine Anlage) |
| `Login_ValidCredentials_GrantsAccess` / `Login_WrongPassword_Denied` | `Auth/SetupAndAuthTests` | Redirect-Flow, Cookie gesetzt, `/`+`/status.json` liefern 200 bzw. Redirect nach `/login` |
| `Login_ThreeFailures_LocksUser_AdminUnlockRestores` | `Auth/SetupAndAuthTests` | 3 Fehlversuche → `LockoutEnd` gesetzt mit Assertion `LockoutEnd ≈ now + 1 h` (belegt die konfigurierte Sperrdauer), Login auch mit korrektem Passwort gesperrt; Admin-`unlock` → Login wieder möglich (siehe Risiken zum Zeitablauf) |
| `Login_DeactivatedUser_IsRejected` + `DeactivatedUser_LosesExistingSession` | `Auth/SetupAndAuthTests` | Deaktiviert → Anmeldung verweigert; bestehender Cookie wird via `OnValidatePrincipal` beim nächsten Request abgelehnt |
| `Logout_ClearsSession` | `Auth/SetupAndAuthTests` | POST `/logout` → `/` redirectet zu `/login` |
|| `Post_WithoutAntiforgery_IsRejected` | `Auth/SetupAndAuthTests` (HTTP-Flow, E2E-Nachweis) | CSRF-Negativtest: POST auf eine Formularroute (z. B. `POST /login` anonym sowie ein authentifizierter POST wie `POST /profile/password` oder `POST /logout` mit gültigem Cookie) **ohne** Antiforgery-Token → 400 Bad Request; mit **ungültigem/gefälschtem** Token → 400; Gegenprobe: gleicher POST mit aus dem GET-Formular extrahiertem Token → kein 400 (belegt, dass der Endpoint-Filter tatsächlich validiert und nicht nur tokenlos durchlässt) |
| `Profile_ChangePassword_Works_AndOldPasswordFails` | `Auth/ProfileTests` | Passwortänderung → Re-Login mit neuem Passwort ok, altem nicht; falschtes altes Passwort → Fehlermeldung |
| `AdminUsers_Create_Deactivate_Delete_Unlock` | `Auth/AdminUserTests` | Admin legt `Ueberwacher` an; Login als Überwacher; Deaktivieren/ Aktivieren; Sperrung zurücksetzen; Löschen |
| `AdminUsers_LastActiveAdmin_IsProtected` | `Auth/AdminUserTests` | Deaktivieren/Löschen/Herabstufen des letzten aktiven Admins wird abgelehnt (Meldung + kein Effekt) — für eigenes und fremdes Konto |
| `AdminUsers_SetRole_ChangesAccess` | `Auth/AdminUserTests` | `POST /admin/users/{id}/role`: `Ueberwacher` → `Administration` (Admin-Endpunkte danach erreichbar) und zurück (danach `/access-denied`, `/` bleibt erreichbar); ungültige Stufe → Ablehnung |
| `AdminEndpoints_WatcherRole_IsForbidden` | `Auth/AdminUserTests` | `Ueberwacher` auf `/admin/*` → Redirect `/access-denied` (403-Verhalten per `AccessDeniedPath`) |
| `AdminTokens_CreateShowsOnce_RevokeBlocks` | `Auth/AdminTokenTests` | Token-Erstellung: Klartext erscheint einmalig in der Antwortseite, nur Hash in DB; Widerruf → `RevokedUtc` gesetzt, Token ab dann ungültig |
| `AdminSettings_SessionDuration_IsApplied` | `Auth/AdminSettingsTests` | Setzen auf Wert X → `ServerSetting` persistiert; frisch ausgestelltes/erneuertes Cookie trägt `ExpiresUtc ≈ now + X` |
| `IsValid_*` (DB-Varianten) | `Security/RelayTokenValidatorTests` (umbaut) | Bekanntes DB-Token → true; unbekannt/leer → false; widerrufenes Token → false; leere Tabelle → false |
| `RelayTokenService_Create_StoresHash_Revoke_SetsTimestamp` | `Security/RelayTokenServiceTests` | Klartext ≠ gespeicherter Wert, Hash-Lookup möglich, `RevokedUtc` gesetzt, Label-Validierung |
| `ServerSettingsService_GetSet_DefaultAndPersisted` | `Security/ServerSettingsServiceTests` | Default 480 ohne Zeile; Setzen persistiert; Bereichsvalidierung |
| `RenderLogin`/`RenderRegister`/`RenderProfile`/`RenderAccessDenied` | `Pages/AuthPagesComposerTests` | Struktur-Assertions wie `StatusPageComposerTests`: Formular-Felder, Antiforgery-Hidden-Field, Fehlermeldungen, HTML-Encoding |
| `RenderUserList`/`RenderTokenList`/`RenderSettings` | `Pages/AdminPagesComposerTests` | Tabellen-Inhalte, Stufen-Anzeige + Stufen-Änderungs-Control je Benutzer, deaktivierte Buttons beim letzten Admin (inkl. Herabstufungs-Button), Einmal-Token-Block, Rollen-Bedingte Admin-Links in `PageShell` |
| `Startup_SecondFactoryOnSameDb_DataPersists` | `Persistence/StartupPersistenceTests` | Factory 1: Admin + Token + Setting anlegen → dispose → Factory 2 mit gleichem `Data:DatabasePath` → Login funktioniert, Token gültig, Setting übernommen; Migration idempotent |

**Einordnung:** Die `Auth/*`-Testklassen (`SetupAndAuthTests`, `ProfileTests`, `AdminUserTests`, `AdminTokenTests`, `AdminSettingsTests`) sind `WebApplicationFactory`-HTTP-Flows mit Cookie-Verwaltung und Antiforgery-Handling — sie gelten gemäß der Abnahmeform im E2E-Abschnitt als faktische E2E-Nachweise (als „HTTP-Flow, E2E-Nachweis" gekennzeichnet), auch wenn sie außerhalb von `E2E/` liegen.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PageAccessTests` (5 Methoden) | Komplett ersetzt durch `Auth/SetupAndAuthTests`/`AdminUserTests` — Basic-Auth entfällt, Cookie-/Rollen-Flüsse neu |
| `Security/RelayTokenValidatorTests` | `CreateSut` muss `StatusServerDbContext` statt `Options`/`IHostEnvironment` aufbauen; Dev-Token-Theorien entfallen; neue Widerruf-Assertions |
| `Relay/StatusRelayRoundtripTests` (6 Methoden) | Token-Seeding über `StatusServerDbSeeder` statt `Relay:Tokens`; `/` und `/status.json` nur mit authentifiziertem `AuthTestClient` erreichbar |
| `Status/StatusPageComposerTests` (9 Methoden) | `Render`-Signatur bekommt `PageUserContext`; zusätzlich Navigations-/Logout-Formular-Assertions |
| `StatusWebApplicationFactory` | `RelayTokens`/`PageUsername`/`PagePassword`/`DevToken` entfallen; `Data:DatabasePath`-Isolation + Temp-Verzeichnis-Cleanup neu |

Nicht betroffen: `InstanceSnapshotCacheTests`, `GitHubStatusServiceTests`.

### E2E-Tests (primärer Funktionsnachweis)

**Abnahmeform:** Der StatusServer rendert JavaScript-freies HTML serverseitig — der komplette Benutzerfluss (Redirects, Formulare, Cookies, CSRF-Token, Session-Verhalten, SignalR-Handshake) ist über `WebApplicationFactory`-HTTP-Client-Flows mit Cookie-Verwaltung und echter `HubConnection` vollständig und deterministisch überprüfbar. Diese Flows sind die E2E-Nachweise. Ein FlaUI-/Browser-Automatisierungs-Stack existiert nicht im Repo und ist für eine Minimal-API ohne UI-Thread nicht die passende Form; geometrische Assertions (BoundingBox) entfallen, da es keine WPF-Dialoge gibt — die HTML-Struktur wird per String-Assertions gesichert, die visuelle Abnahme erfolgt als manueller Browser-Check durch den Anwender (dokumentierter Abschlussschritt). Konsolidierung: der Happy Path ist bewusst ein einzelner Test mit allen Schritten an einer laufenden Instanz; die Fehlerflüsse sind je ein kompakter Test auf eigenem Factory-Setup (DB-Zustand ist pro Fluss verschieden).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Vollständiger Happy Path: Erstaufruf ohne Benutzer → Registrierung → Admin-Login → Überwacher anlegen → Überwacher-Login → Relay-Token anlegen → SignalR-Connect + Snapshot-Push → Status auf `/` sichtbar | `E2E/StatusServerUserFlowTests.HappyPath_FullSetupToRelayFlow` | Szenario 1 (siehe unten) | Nur der Gesamtfluss über Redirects/Cookies/CSRF/DB/Hub beweist das tatsächliche Funktionieren des Benutzerwegs |
| Pflicht | Registrierung nach Setup geschlossen + unauthentifizierter Zugriff → Login-Umleitung | `E2E/StatusServerUserFlowTests.RegistrationClosed_AndUnauthenticatedRedirect` | Szenario 2 | Redirect-Verhalten ist nur im HTTP-Flow prüfbar |
| Pflicht | Lockout nach 3 Fehlversuchen + Admin-Freischaltung | `E2E/StatusServerUserFlowTests.Lockout_AndAdminUnlock` | Szenario 3 | Zusammenspiel `SignInManager` ↔ Admin-Endpunkt ↔ erneutem Login nur E2E belegbar |
| Pflicht | Deaktivierter Benutzer verliert laufende Session | `E2E/StatusServerUserFlowTests.DeactivatedUser_SessionRejected` | Szenario 4 | `OnValidatePrincipal`-Durchsetzung nur über echte zweite Anfrage mit Alt-Cookie nachweisbar |
| Pflicht | Token-Einmalanzeige + Widerruf blockiert `/relay`-Connect | `E2E/StatusServerUserFlowTests.TokenLifecycle_OneTimeDisplayAndRevocation` | Szenario 5 | Einmal-Anzeige (UI) ↔ Widerruf (DB) ↔ Hub-Ablehnung ist eine End-to-End-Kette |
| Pflicht | Persistenz über „Neustart" (zweite Factory auf gleicher DB) | `E2E/StatusServerUserFlowTests.Restart_SameDatabase_AllStateSurvives` | Szenario 6 | Kern-Anforderung „übersteht Neustarts" — nur über realen zweiten App-Start nachweisbar |
| Pflicht | `Ueberwacher` kann Admin-Funktionen nicht auslösen (UI-Links unsichtbar + Endpunkte verweigern) | `E2E/StatusServerUserFlowTests.WatcherRole_AdminAccessDenied` | Szenario 7 | Berechtigungsmodell ist Benutzer-fluss-relevant (Angriff über direkte URL-Eingabe) |
| Pflicht | Stufenänderung wirkt auf bestehende Session: zweiter Admin wird per UI auf `Ueberwacher` herabgestuft → dessen nächster `/admin/*`-Request mit Alt-Cookie scheitert, `/` bleibt erreichbar; anschließend `Ueberwacher` → `Administration` → Admin-Zugriff ohne Re-Login | `E2E/StatusServerUserFlowTests.RoleChange_TakesEffectOnExistingSession` | Szenario 9 | Der Rollen-Claim-Abgleich in `OnValidatePrincipal` ist nur über einen echten zweiten Request mit bereits ausgestelltem Cookie nachweisbar |
| Pflicht | CSRF-Blockade: POST auf Formularroute ohne bzw. mit ungültigem Antiforgery-Token → 400 Ablehnung | `Auth/SetupAndAuthTests.Post_WithoutAntiforgery_IsRejected` (HTTP-Flow, E2E-Nachweis) | Validierungsregel „Alle POST-Routen → 400 Bad Request" (CSRF-Schutz aller POST-Formulare) | Nur ein echter HTTP-POST ohne/gefälschtem Token belegt, dass der Endpoint-Filter tatsächlich ablehnt — die positiven Flows senden den Token mit und würden eine fehlende Validierung nicht aufdecken |
| Soll | Sitzungsdauer-Änderung wirkt auf Cookie-Laufzeit | `E2E/StatusServerUserFlowTests.SessionDuration_ChangeAppliedToCookie` | Szenario 8 | Nur über Set-Cookie-Header im echten Flow prüfbar |

Betroffene bestehende E2E-Tests: keine — `StatusRelayRoundtripTests` sind Integrationstests (kein Benutzerfluss), werden aber wie oben angepasst. Die FlaUI-/ConPTY-E2E-Lane des Hauptprojekts ist statusserver-fern und unverändert.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | **Happy Path** (kompletter Benutzerfluss) | Nutzer öffnet `GET /` auf frisch installiertem Server | Kein `data`-Verzeichnis, keine Benutzer, keine Tokens, keine Settings (alles wird erzeugt) | `GET /` → Redirect `/register`; Admin registriert sich → angemeldet auf `/`; Admin legt Benutzer „watcher" (Stufe `Ueberwacher`) an; Admin legt Relay-Token an → Klartext einmalig sichtbar; App-Client verbindet `wss://…/relay?access_token=…` → `RegisterInstance` + `PushSnapshot`; Admin meldet ab; „watcher" meldet an → Status-Seite zeigt die Instanz, keine Admin-Links; `/admin/*`-Aufruf → `/access-denied` | Vollständig bedienbare Seite; Instanz online sichtbar; Token funktioniert am Hub; Stufen wirken | `StatusServerUserFlowTests.HappyPath_FullSetupToRelayFlow` + manuelle Browser-Abnahme |
| 2 | Registrierung geschlossen / unauthentifizierter Zugriff | `GET /register` bzw. `GET /` ohne Cookie | Benutzer existieren; keine Session | `GET /` → Redirect `/login`; `GET`/`POST /register` → Redirect `/` (keine Anlage); `POST /login` mit gültigen Credentials → Cookie + `/` erreichbar | Nur Login-Weg; kein Setup-Loch | `…RegistrationClosed_AndUnauthenticatedRedirect` |
| 3 | Sperrung + Admin-Freischaltung | 3× `POST /login` mit falschem Passwort | Aktiver Benutzer existiert | 3 Fehlversuche → `LockoutEnd` gesetzt; Login mit korrektem Passwort → Sperr-Hinweis; Admin öffnet `/admin/users`, klickt „Sperrung zurücksetzen" → Benutzer kann sich sofort anmelden. (Automatische 1-h-Freischaltung ist Zeit-basiert und wird nicht im Test vor-gespult — siehe Risiken) | Sperr-Status in Admin-UI sichtbar; Freischaltung wirkt sofort | `…Lockout_AndAdminUnlock` |
| 4 | Deaktivierung killt laufende Session | Admin deaktiviert Benutzer während dieser angemeldet ist | Benutzer mit gültigem Cookie angemeldet | Admin `POST /admin/users/{id}/deactivate`; Benutzer ruft `/` erneut ab → Cookie verworfen, Redirect `/login`; erneuter Login → „Konto deaktiviert"; Admin aktiviert → Login wieder möglich | Sofortiger Zugriffsverlust, klare Meldung | `…DeactivatedUser_SessionRejected` |
| 5 | Token-Einmalanzeige und Widerruf | Admin erstellt und widerruft Token | Admin angemeldet | `POST /admin/tokens` → Antwortseite zeigt Klartext einmalig (erneuter `GET /admin/tokens` zeigt keinen Klartext); Connect auf `/relay` mit Token → ok; `POST …/revoke` → erneuter Connect → `Context.Abort()`/Verbindung schlägt fehl | Klartext genau einmal; widerrufenes Token tot | `…TokenLifecycle_OneTimeDisplayAndRevocation` |
| 6 | Neustart-Persistenz | Server-Neustart | Gefüllte DB: Benutzer, Token, Setting | Zweite Server-Instanz mit gleichem `data`-Pfad startet (Migration idempotent, Rollen-Seeding idempotent) → Login mit bestehendem Benutzer ok; Token weiterhin gültig am Hub; Sitzungsdauer-Setting aktiv | Alle Zustände überleben Neustart | `…Restart_SameDatabase_AllStateSurvives` |
| 7 | Überwacher-Restriktion | `Ueberwacher` ruft Admin-URLs direkt auf | `Ueberwacher` angemeldet | `GET /admin/users` → Redirect `/access-denied` mit klarer Meldung; `POST /admin/users` → verweigert; Status-Seite zeigt keine Admin-Links; `/` und `/status.json` liefern Daten | Lesender Zugriff bleibt, Verwaltung blockiert und erklärt | `…WatcherRole_AdminAccessDenied` |
| 8 | Sitzungsdauer-Administration | Admin ändert Sitzungsdauer | Admin angemeldet | `GET /admin/settings` → Wert ändern (z. B. 60 min) → `POST`; anschließender Login → `Set-Cookie` trägt `Expires ≈ now + 60 min`; ungültiger Wert (0/negativ/Text) → Fehlermeldung | Einstellung wirkt auf neue/erneuerte Tickets | `…SessionDuration_ChangeAppliedToCookie` |
| 9 | Stufenänderung wirkt zeitnah | Admin ändert die Berechtigungsstufe eines Benutzers, der aktuell angemeldet ist | Admin angemeldet; zweiter Admin mit aktivem Cookie angemeldet | Admin stuft den zweiten Admin in `/admin/users` auf `Ueberwacher` herab → dessen nächster `/admin/*`-Request scheitert mit `/access-denied`, `/` bleibt lesbar (kein Re-Login nötig); Admin stuft ihn zurück auf `Administration` → Admin-Zugriff wieder vorhanden; Versuch, den einzig verbleibenden Admin herabzustufen → Ablehnung mit Meldung (Button deaktiviert), gleichermaßen für eigenes und fremdes Konto | Neue Stufe gilt ab nächstem Request ohne erneute Anmeldung; letzter Admin ist geschützt | `…RoleChange_TakesEffectOnExistingSession` + `AdminUsers_SetRole_ChangesAccess`/`…LastActiveAdmin_IsProtected` |
| 10 | Totaler Admin-Lockout (Recovery-Dokumentation) | Alle Admins gesperrt/deaktiviert | — | Kein In-App-Weg; Betreiber löscht `data`-Verzeichnis → Setup-Lauf (Szenario 1) von vorn | Dokumentiertes Verfahren in `server/README.md` | README-Abschnitt (kein automatisierter Test; bewusster Ausschluss 2) |
| 11 | Profil-Passwortänderung | Angemeldeter Benutzer ändert eigenes Passwort | Beliebiger Benutzer angemeldet | `GET /profile` → Formular; `POST /profile/password` (alt korrekt, neu ≥ 8) → Erfolgsmeldung, Session bleibt; Logout → Login mit altem Passwort schlägt fehl, mit neuem ok; falsches altes Passwort → Fehlermeldung ohne Änderung | Passwort-Rotation funktioniert self-service | `Profile_ChangePassword_Works_AndOldPasswordFails` |

## Offene Punkte

Keine — die in `requirement.md` genannten offenen Fragen wurden per dort dokumentierter
Annahme bzw. Auftraggeber-Entscheidung geklärt (Composer-Stil, Sitzungsdauer in
Minuten/Default 480, Stufenänderung verbindlich gefordert und im Plan verankert,
Token-Label ja, Letzter-Admin-Schutz eingebaut). Sämtliche Scope-Ausschlüsse sind
bestätigt; der frühere Ausschluss „keine nachträgliche Stufenänderung" ist auf
Anwenderentscheid verworfen und durch den Programmablauf „Admin: Berechtigungsstufe
ändern" ersetzt.
