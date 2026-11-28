# Übersetzte Anforderung – Light-/Darkmode für die Status-Webseite (Issue #489)

## Fachliche Zusammenfassung

Die server-seitig gerenderte Weboberfläche des `Softwareschmiede.StatusServer` (Status-Seite `/`, Login, Registrierung, Profil, Admin-Seiten sowie Fehler-/Bestätigungsseiten) erhält ein Light-/Darkmode-Theming. Das wirksame Farbschema wird aus einer pro Benutzer persistierten Einstellung (**Automatisch/Light/Dark**) ermittelt und bereits im ausgelieferten HTML verankert (z. B. `data-theme`-Attribut), sodass kein JavaScript und kein sichtbares Umschalten nötig ist. Die heute fest verdrahtete Light-Palette in den eingebetteten CSS-Blöcken (`PageShell.Css`, `PageShell.NavCss`, `StatusPageComposer.Css`) wird auf CSS Custom Properties umgestellt; „Automatisch" folgt der Media Query `prefers-color-scheme` mit Fallback Light.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein server-gerenderter Seitenaufruf der Status-Webanwendung (jedes HTML-Dokument, das über `PageShell.BeginPage` bzw. `StatusPageComposer.Render` erzeugt wird) sowie eine einzelne Theme-Einstellung pro `ApplicationUser`.
- **Auslösende Ereignisse:**
  - GET-Request auf eine der HTML-Seiten (`/`, `/profile`, `/admin/*`, `/login`, `/register`, `/access-denied`, Formular-abgelaufen-Seite) inkl. Meta-Refresh der Status-Seite.
  - POST der Theme-Auswahl durch den angemeldeten Benutzer auf der Profilseite.
- **Erwartete Wirkung:**
  - Angemeldete Benutzer sehen alle Seiten im gewählten Schema; bei „Automatisch" folgt die Darstellung `prefers-color-scheme` des Betrachters, bei nicht auslesbarer Systemeinstellung Light.
  - Nicht angemeldete Besucher (Login-/Registrierungs-/Fehlerseiten ohne `PageUserContext`) sehen ausschließlich das System-Schema (Fallback Light) — es gibt keine manuelle Auswahl ohne Konto.
  - Die Theme-Auswahl im Profil wird persistent gespeichert (über Sitzungen und Server-Neustarts hinweg) und gilt ab dem nächsten Seitenaufruf.
- **Umgang mit fehlenden Objekten:**
  - Bestehende `ApplicationUser`-Datensätze besitzen noch keine Theme-Einstellung → per EF-Core-Migration neue Spalte mit Default „Automatisch" erzeugen (Wiederverwenden des `AspNetUsers`-Speichers aus #487; die `ServerSettings`-Tabelle ist ungeeignet, da sie keinen Benutzerbezug hat).
  - `PageUserContext` kennt noch keine Theme-Information → um die wirksame Einstellung erweitern; `EndpointSecurityExtensions.BuildUserContextAsync` lädt bereits das `ApplicationUser`-Objekt und kann den Wert mitliefern.
  - Seiten ohne Benutzerkontext (anonym): kein Objekt erzeugen — sie rendern immer im „Automatisch"-Modus.
- **Scope-Ausschlüsse:**
  - Kein Live-Umschalten ohne Reload und kein JavaScript — die Einstellung wirkt erst ab nächstem Seitenaufruf/Meta-Refresh.
  - `/status.json` (JSON-API) und `/relay` (SignalR-Hub) bleiben unverändert.
  - Kein serverweites/Admin-konfigurierbares Standard-Schema — Vorgabe ist immer „Automatisch"; eine Admin-Vorgabe pro Benutzer oder global ist nicht gefordert.
  - Kein Theme-Umschalter auf öffentlichen/anonymen Seiten.
  - Keine per-Instanz- oder per-Sektion-Abweichung — das Schema gilt einheitlich für die gesamte Web-UI.

## Betroffene Klassen und Komponenten

- **Datenmodell:**
  - `ApplicationUser` (`server/Softwareschmiede.StatusServer/Data/ApplicationUser.cs`): neue persistierte Eigenschaft für die Theme-Präferenz (analog `IsDeactivated`).
  - Neues Enum für die Präferenz (z. B. `StatusTheme`/`ThemePreference` mit `Auto`/`Light`/`Dark`).
  - `StatusServerDbContext` + neue EF-Core-Migration unter `server/Softwareschmiede.StatusServer/Migrations/` (Default-Wert „Automatisch" für Bestandsdaten).
  - `PageUserContext` (`Pages/PageUserContext.cs`): zusätzliches Member für die wirksame Einstellung.
- **Logik / Services:**
  - `EndpointSecurityExtensions.BuildUserContextAsync` (`Endpoints/EndpointSecurityExtensions.cs`): Theme in den Kontext übernehmen.
  - `AuthEndpoints` (`Endpoints/AuthEndpoints.cs`): neuer POST-Endpunkt zum Speichern der Theme-Auswahl (mit `RequireAntiforgery`, `PageAccessPolicy`, Post/Redirect/Get über `?msg=`-Schlüssel).
  - `StatusMessages` (`Endpoints/StatusMessages.cs`): neuer Meldungsschlüssel (z. B. „Die Anzeigeeinstellung wurde gespeichert.").
- **UI / Composer:**
  - `PageShell` (`Pages/PageShell.cs`): `BeginPage` um Theme-Parameter erweitern; `Css` und `NavCss` komplett auf CSS Custom Properties umstellen (Light-Palette als `:root`-Default, Dark-Palette via `prefers-color-scheme` und/oder `data-theme`-Selektor; `color-scheme`-Eigenschaft setzen, damit Formular-Controls mitziehen).
  - `StatusPageComposer` (`Status/StatusPageComposer.cs`): eigenes `<html>`-Tag mit Theme-Attribut versehen; `Css` auf Variablen umstellen; Inline-Styles `style="color:#cbd5e1"` (Zeile ~90) und `style="margin-top:12px"` (Zeile ~288) durch Klassen ersetzen.
  - `AuthPagesComposer.RenderProfile` (`Pages/AuthPagesComposer.cs`): neuer Abschnitt mit Auswahl-Steuerelement (Automatisch (System)/Light/Dark) und Speichern-Formular.
  - `AdminPagesComposer` (`Pages/AdminPagesComposer.cs`): alle `Render*` übernehmen den erweiterten `PageShell`-Aufruf (Seiten für angemeldete Benutzer erhalten deren Theme; anonyme Composer-Aufrufe nutzen „Automatisch").
  - `Program.cs`: Theme im GET-Handler von `/` an `StatusPageComposer.Render` durchreichen (Bau des `PageUserContext` bereits vorhanden).
- **Tests:**
  - `Pages/AuthPagesComposerTests`, `Pages/AdminPagesComposerTests`, `Status/StatusPageComposerTests`: HTML-Assertions auf `data-theme`/CSS-Struktur.
  - `Auth/ProfileTests`: Endpunkt-Test für das Speichern der Theme-Einstellung (PRG, Persistenz, Invalid-Werte-Abweisung).
  - `AuthTestClient`/`StatusWebApplicationFactory`-basierte Flow-Tests ggf. erweitern.
  - Dokumentation `docs/help/status-webseite/datenmodell.md` später um neue `ApplicationUser`-Spalte ergänzen (Schritt 12 des Workflows).

## Implementierungsansatz

- **Persistenz:** Neue Enum-Eigenschaft auf `ApplicationUser` (z. B. `Theme`), per `dotnet ef migrations add` in `StatusServerDbContext` eingehängt; Default „Automatisch" für Bestandsbenutzer. Speicherort folgt damit der aus #487 entstandenen Datenstruktur (Spalte in `AspNetUsers`).
- **Auswahl-Interaktion (Fachpunkt):** Der Benutzer wählt auf `/profile` explizit aus den drei Optionen **Automatisch (System) / Light / Dark** — als `<select>` oder Radio-Gruppe im bestehenden Formular-Stil (`PageShell`-CSS für `form select` existiert bereits), nicht als Freitext. Absenden per POST auf einen neuen Profil-Endpunkt mit Antiforgery-Validierung und PRG-Pattern (Erfolgsmeldung via `StatusMessages`).
- **Auslieferung:** `PageShell.BeginPage` und `StatusPageComposer.Render` setzen das wirksame Schema ins Markup (z. B. `data-theme="light|dark|auto"` auf `<html>` oder `<body>`). CSS-Struktur: `:root` mit Light-Variablen; `[data-theme="dark"]` erzwingt Dark; `@media (prefers-color-scheme: dark)` aktiviert Dark nur wenn `data-theme` `auto`/nicht gesetzt ist (Selektor-Reihenfolge so wählen, dass explizites Light auch bei Dark-System greift). Anonyme Seiten rendern ohne Theme-Attribut bzw. mit `auto`, sodass nur die Media Query greift. `<meta name="color-scheme">` bzw. CSS `color-scheme` sollte gesetzt werden, damit Browser-Chrome (Scrollbars, Eingabefelder) konsistent bleibt.
- **CSS-Umbau:** Sämtliche Hex-Farbwerte in `PageShell.Css`, `PageShell.NavCss` und `StatusPageComposer.Css` durch benannte Custom Properties ersetzen; die beiden verbleibenden Inline-Styles in `StatusPageComposer` durch Klassen ersetzen. Statusfarben (`.badge.online/.offline/.stale`, `.pill.run/.wait/.done/.err/.pause`, Links, `.msg`/`.stale-note`-Hinweisboxen) erhalten je Schema kontrastsichere Werte.
- **Abhängigkeiten:** Baut auf #487 auf (`ApplicationUser`, `PageUserContext`, `PageShell`, Profilseite, `StatusServerDbContext`/Migrationen) — alle Voraussetzungen sind bereits gemergt. Kein neues Interface nötig; Erweiterung bestehender Records/Klassen reicht. Beim Sign-in-/Sign-out- und Security-Stamp-Verhalten ändert sich nichts (keine Session-Invalidierung für eine reine Anzeige-Einstellung erforderlich — Annahme, zu verifizieren).

## Konfiguration

Benutzerspezifisch, pro `ApplicationUser`-Datensatz in der SQLite-Datenbank (`Data:DatabasePath` aus `StatusServerOptions`/Konfiguration — unverändert). Keine Anwendungs- oder Server-Einstellung in `appsettings`/`ServerSettings` erforderlich.

## Offene Fragen

- **Benennung:** Konkrete Namen für Enum (`ThemePreference` o. ä.), `ApplicationUser`-Eigenschaft und POST-Route (`/profile/theme` o. ä.) sind nicht vorgegeben — Annahme: an den bestehenden Konventionen orientiert festlegen.
- **Dark-Palette:** Die konkreten Farbwerte des Dark-Schemas sind nicht spezifiziert — zu definieren unter der Bedingung ausreichender Kontraste für alle Statusfarben; der dunkle Header (`header.top`, `#1f2937`) kann als Referenz für das Dark-Grundschema dienen.
- **Update-Installationsseite:** `AdminPagesComposer.RenderUpdateInstallStarted` wird nach Update-Start ggf. ohne frischen Benutzerkontext gerendert — klären, ob dort „Automatisch" oder das Benutzer-Theme gilt (Annahme: Benutzerkontext ist vorhanden, Theme gilt normal).
- **Meta-Refresh der Status-Seite:** Bei „Automatisch" wechselt die Seite erst beim nächsten Refresh auf ein geändertes System-Schema — durch die Anforderung explizit abgedeckt (kein Live-Umschalten nötig), nur als Verhalten dokumentieren.
