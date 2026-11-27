# Übersetzte Anforderung: Benutzeranmeldung in Webseite (StatusServer)

Quelle: GitHub-Issue #487 (martin-stromberg/Softwareschmiede)

## Fachliche Zusammenfassung

Der `Softwareschmiede.StatusServer` (`server/Softwareschmiede.StatusServer`, ASP.NET Core,
`net11.0`) erhält eine vollständige, persistente Benutzerverwaltung auf Basis von
ASP.NET Core Identity mit lokaler SQLite-Datenbank über EF Core. Die bisherige
konfigurationsbasierte Absicherung — optionale Basic-Auth-`PageAccessMiddleware` gegen
`Page:Username`/`Page:Password` sowie statische `Relay:Tokens`/`Relay:DevToken` — wird durch
Cookie-Anmeldung mit zwei Berechtigungsstufen (**Administration**, **Überwacher**) und
datenbankgestützten, gehasht gespeicherten und widerrufbaren Relay-Tokens ersetzt. Der
erste über das Setup-Registrierungsformular angelegte Benutzer wird Administrator; danach
ist die Registrierung dauerhaft geschlossen.

## Fachlicher Scope

- **Verarbeitungseinheit:** Der Lebenszyklus von Benutzerkonto, Sitzung und Relay-Token
  der Status-Webseite — jeweils bewertet je HTTP-Anfrage auf `/` und `/status.json`
  (Cookie-Session), je Hub-Verbindungsaufbau auf `/relay` (Token) sowie je
  Admin-/Profil-Aktion in der Verwaltungs-UI.
- **Auslösende Ereignisse:**
  - `GET /` bzw. `GET /status.json`: ohne vorhandenes Benutzerkonto → Anzeige des
    Registrierungsformulars; mit Benutzern, aber ohne gültige Session → Umleitung auf das
    Login-Formular; mit Session → Auslieferung entsprechend der Berechtigungsstufe.
  - `POST` der Formulare: Registrierung (nur solange kein Benutzer existiert), Anmeldung,
    Abmeldung, Passwortänderung im eigenen Profil.
  - Admin-Aktionen über die Verwaltungs-UI: Benutzer anlegen (inkl. Stufe),
    Berechtigungsstufe eines bestehenden Benutzers ändern, deaktivieren,
    löschen, Sperrung zurücksetzen; Relay-Token erstellen und widerrufen; Sitzungsdauer
    einstellen.
  - SignalR-Verbindungsaufbau auf `/relay` mit `access_token`-Query bzw. Bearer-Header
    (Transport unverändert, Prüfung neu gegen die Datenbank).
  - Server-Start: Anlage des `data`-Verzeichnisses und Ausführung ausstehender
    Datenbank-Migrationen.
- **Erwartete Wirkung:** Nach dem Setup können nur angemeldete Benutzer die Status-Seite
  lesen; Administratoren verwalten Benutzer, Sperren, Tokens und Sitzungsdauer über die
  UI; ein neu erstelltes Relay-Token wird genau einmal angezeigt und berechtigt bis zum
  Widerruf zum Verbindungsaufbau auf `/relay`; alle Zustände (Benutzer, Rollen, Sperren,
  Tokens, Einstellungen) überstehen Neustarts in `data/*.db`; die Anwendung funktioniert
  ohne die entfernten `Page:*`-/`Relay:*`-Konfigurationseinträge.
- **Umgang mit fehlenden Objekten:**
  - Kein Benutzer vorhanden → Setup-Modus: Registrierungsformular anbieten, ersten
    Benutzer mit Stufe **Administration** *erzeugen*.
  - `data`-Verzeichnis/SQLite-Datei fehlt → beim Start *erzeugen* (inkl. Migrationen).
  - Keine Relay-Tokens vorhanden → `/relay` lehnt alle Verbindungen ab; Tokens werden
    ausschließlich über die Admin-UI *erzeugt*.
  - Gesperrter (3 Fehlversuche) oder deaktivierter Benutzer → Anmeldung und
    Zugriff verweigert; *aktivieren* nur durch Admin-Freischaltung (bei Sperrung
    zusätzlich automatisch nach 1 Stunde).
  - Alle Administratoren ausgesperrt → kein In-App-Recovery; durch Löschen des
    `data`-Verzeichnisses *verwerfen* und Setup-Lauf von vorn.
- **Scope-Ausschlüsse:**
  - Kein zusätzlicher Schutz des Registrierungsfensters (kein Setup-Code, keine
    localhost-Beschränkung) — Risiko bewusst vom Auftraggeber akzeptiert.
  - Kein dedizierter Recovery-Weg (kein Passwort-Reset, keine E-Mail, keine
    Recovery-Codes); Recovery = `data`-Verzeichnis löschen/Neuinstallation.
  - Keine Migration und kein Fallback für die entfernten Konfigurationseinträge
    (`Page:Username`, `Page:Password`, `Relay:Tokens`, `Relay:DevToken`).
  - Keine Selbstregistrierung weiterer Benutzer — nach dem ersten Benutzer legt
    ausschließlich die Administration Konten an.
  - Keine dritte Berechtigungsstufe zum Start; das Modell wird lediglich erweiterbar
    für spätere Schreib-Stufen ausgelegt.
  - Keine TLS-Durchsetzung in der Anwendung selbst — HTTPS terminiert am Reverse
    Proxy; im LAN bleibt HTTP zulässig.

## Betroffene Klassen und Komponenten

- `server/Softwareschmiede.StatusServer/Program.cs` — DI-Registrierung (EF Core,
  Identity, Cookie-Authentifizierung), Pipeline (`UseAuthentication`,
  `UseAuthorization`, `PageAccessMiddleware` entfällt), neue Endpunkte für
  Registrierung, Login, Profil und Admin-Verwaltung; `RequireAuthorization` auf
  `/` und `/status.json`.
- `Security/PageAccessMiddleware.cs` — wird vollständig entfernt (ersetzt durch
  Cookie-Authentifizierung).
- `Security/RelayTokenValidator.cs` — Umbau: Prüfung des `access_token` gegen
  gehasht in der Datenbank gespeicherte, nicht-widerrufene Tokens statt
  `RelayOptions.Tokens`/`DevToken`.
- `Security/SecretComparer.cs` — weiterverwendbar für den Konstantzeit-Vergleich
  der Token-Hashes.
- `Options/StatusServerOptions.cs`, `appsettings.json`, `appsettings.Development.json` —
  `PageOptions.Username`/`PageOptions.Password`, `RelayOptions.Tokens` und
  `RelayOptions.DevToken` entfallen; `PageOptions.RefreshSeconds`,
  `AppSectionOptions` und `GitHubOptions` bleiben.
- Neu (Vorschlag, Namespace `Softwareschmiede.StatusServer.Data` o. ä.):
  - `StatusServerDbContext : IdentityDbContext<…>` plus EF-Core-Migrationen
    (Konvention wie `SoftwareschmiedeDbContext` inkl. Design-Time-Factory und
    `DateTimeOffset`-Konvertern).
  - `ApplicationUser` (bzw. benannter `IdentityUser`-Ableger) mit
    Deaktivierungs-Flag.
  - `RelayToken`-Entity: Token-Hash, Bezeichnung, Erstellt-/Widerrufen-Zeitpunkt.
  - `ServerSetting`-Entity o. ä. für die admin-konfigurierbare Sitzungsdauer.
- Neu (Logik): Service für Token-Erzeugung (kryptografisch zufällig, Einmal-Anzeige,
  Hash-Speicherung) und -Widerruf; Setup-Statusprüfung (»kein Benutzer vorhanden«);
  Settings-Service für die Sitzungsdauer.
- Neu (UI): Formularseiten Registrierung, Login, Profil (Passwort ändern),
  Admin-Übersichten Benutzer/Tokens/Einstellungen — im bestehenden
  serverseitig gerenderten HTML-Stil (vgl. `Status/StatusPageComposer.cs`), inkl.
  CSRF-Schutz der POST-Formulare.
- Rollenmodell: Identity-Rollen `Administration` und `Ueberwacher` plus
  Authorization-Policies (Seiten-Lesezugriff beide Stufen; Verwaltung nur
  `Administration`).
- `Relay/StatusRelayHub.cs` — Verbindungsaufbau bleibt tokenbasiert
  (`OnConnectedAsync`, ~Z. 37–56), nur die Validator-Quelle wechselt zur DB.
- `server/README.md` — Betriebs-/Konfigurationsdoku anzupassen (Setup-Lauf,
  Token-Verwaltung über UI, `data`-Verzeichnis, entfallene Config-Keys).
- Neue Paketreferenzen im `Softwareschmiede.StatusServer.csproj`
  (Versionsstand 11.0.0 wie im Repo): `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
  `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`.
- Tests (`server/Softwareschmiede.StatusServer.Tests`):
  - `StatusWebApplicationFactory` — Umbau auf isolierte SQLite-DB je Testlauf;
    `RelayTokens`/`PageUsername`/`PagePassword`/`DevToken`-UseSettings entfallen.
  - `PageAccessTests` — Ersatz durch Setup-/Login-/Rollen-Tests.
  - `Security/RelayTokenValidatorTests` — Umbau auf DB-gestützte Tokens.
  - `Relay/StatusRelayRoundtripTests` — Token-Seeding über die Datenbank statt
    `Relay:Tokens`.
  - Neu: Sperrung nach 3 Fehlversuchen + Admin-Freischaltung, Einmal-Anzeige und
    Widerruf von Tokens, Persistenz über Neustart, Passwortänderung,
    Sitzungsdauer-Konfiguration, Zugriffsverweigerung für `Ueberwacher` auf
    Admin-Endpunkte.

## Implementierungsansatz

- Identity-Einbindung: `AddIdentity<ApplicationUser, IdentityRole>()` mit
  `AddEntityFrameworkStores<StatusServerDbContext>()`; `IdentityOptions`:
  `Password.RequiredLength = 8`, `Lockout.MaxFailedAccessAttempts = 3`,
  `Lockout.DefaultLockoutTimeSpan = TimeSpan.FromHours(1)`,
  `Lockout.AllowedForNewUsers = true`. Admin-Freischaltung über
  `UserManager.SetLockoutEndDateAsync(user, null)` und `ResetAccessFailedCountAsync`.
- Authentifizierung: Identity-Applikations-Cookie; Login-Seite anonym erreichbar;
  `/` und `/status.json` per Policy für beide Rollen, Admin-Endpunkte per Policy
  nur `Administration`; `/register` nur ausliefern, solange
  `UserManager.Users.Any() == false` (zusätzlich serverseitig im POST prüfen).
- Sitzungsdauer: Wert in der Datenbank (Admin-Einstellung), beim Ausstellen/
  Erneuern des Auth-Cookies als `ExpireTimeSpan` angewendet (Sliding-Expiration-
  berücksichtigen).
- Deaktivierung: Flag auf `ApplicationUser`; Prüfung beim Sign-in **und** bei der
  Cookie-Revalidierung (`SecurityStampValidator`/`OnValidatePrincipal`), damit
  deaktivierte Benutzer auch mit bestehender Session den Zugriff verlieren.
- Relay-Tokens: ausreichend lange Zufallstokens erzeugen, SHA-256-gehasht
  speichern (Lookup über Hash, Vergleich via `SecretComparer.FixedTimeEquals`);
  Klartext einmalig im Erstell-Dialog ausgeben; Widerruf über Flag
  (`RevokedUtc`) oder Löschen; `StatusRelayHub.OnConnectedAsync` bleibt
  unverändert (`access_token`-Query, Bearer-Fallback).
- Persistenz: `data`-Unterverzeichnis relativ zum Programm-/Content-Root; beim
  Start `Directory.CreateDirectory` + `Database.Migrate()`; EF-Core-Migrationen
  im Projekt einchecken (Konvention des Hauptprojekts übernehmen, u. a.
  `DateTimeOffset`→Unix-ms-Konverter für SQLite-Sortierung).
- Reverse-Proxy-/LAN-Betrieb: `ForwardedHeaders`-Middleware
  (`X-Forwarded-Proto`) konfigurieren und Cookie-`Secure`-Policy so wählen, dass
  der Cookie hinter HTTPS-Proxy sicher ist und im LAN über HTTP funktioniert
  (z. B. `SameAsRequest`) — relevant, weil Identity-Cookies sonst bei
  HTTP-Login verworfen werden.
- CSRF: Alle POST-Formulare mit Antiforgery-Tokens absichern (`IAntiforgery` bei
  Minimal-APIs bzw. eingebaut bei Razor Pages).
- UI-Ansatz: Neuen Formularseiten im selben serverseitig gerenderten,
  JavaScript-freien Stil wie `StatusPageComposer` umsetzen (keine SPA); bei
  wachsender Formularzahl ggf. Razor Pages erwägen (siehe Offene Fragen).

## Konfiguration

- **Entfernt** (vollständig, ohne Migration/Fallback): `Page:Username`,
  `Page:Password`, `Relay:Tokens`, `Relay:DevToken` — betrifft
  `appsettings.json`, `appsettings.Development.json`, `StatusServerOptions`,
  `RelayOptions`, `PageOptions` sowie die Env-Beispiele und die systemd-Unit in
  `server/README.md`.
- **Unverändert in `appsettings.json`:** `Page:RefreshSeconds`,
  `App:StaleAfterSeconds`, `GitHub:*` (Repositories, Token, CacheTtlSeconds).
- **Neu zur Laufzeit (Datenbank, Admin-UI):** Sitzungsdauer der Anmeldesession —
  globaler, anwendungsweiter Wert (nicht pro Benutzer).
- Datenbankpfad: fest `data/*.db` unter dem Programmverzeichnis; kein
  Konfigurationsschlüssel gefordert (bei Bedarf intern überschreibbar machen,
  Annahme).

## Offene Fragen

Vor der Implementierung intern zu klären (fachlich bereits entschiedene Punkte
sind oben verbindlich):

1. **UI-Technik der Formularseiten:** Weiterhin String-basiert serverseitig
   gerendertes HTML im `StatusPageComposer`-Stil oder Umstieg auf Razor Pages
   (eingebautes Model-Binding/Antiforgery)? — Annahme: Composer-Stil beibehalten.
2. **Sitzungsdauer:** Einheit und sinnvoller Default (z. B. Minuten oder Stunden,
   Default 8 h?) sowie gültiger Wertebereich in der Admin-Einstellung.
3. **Berechtigungsstufe nachträglich ändern:** Geklärt (Auftraggeber,
   Issue-Kommentar): Die Stufe eines bestehenden Benutzers muss nachträglich
   änderbar sein — ist verbindlich gefordert. Kein Admin-Passwort-Reset für
   fremde Konten (ebenfalls geklärt).
4. **Token-Metadaten:** Sollen Relay-Tokens eine frei vergebene Bezeichnung/Notiz
   führen, damit sie in der Verwaltung unterscheidbar sind? — Annahme: ja.
5. **Letzten Admin schützen:** Soll das Deaktivieren/Löschen des letzten
   verbleibenden Administrators in der UI blockiert werden, um den
   Setup-Neustart-Fall (`data` löschen) zu vermeiden? — Annahme: sinnvoll, nicht
   explizit gefordert.
