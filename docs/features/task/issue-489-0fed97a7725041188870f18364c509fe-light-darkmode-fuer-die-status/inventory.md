# Bestandsaufnahme: Light-/Darkmode für die Status-Webseite (Issue #489)

Analysiert wurde die server-gerenderte Web-UI des `Softwareschmiede.StatusServer` (`server/Softwareschmiede.StatusServer`) — Seitenrahmen, Composer, Benutzerkontext, Datenmodell und Endpunkte — bezogen auf die Anforderung eines pro Benutzer persistierten Light-/Dark-Themes ohne JavaScript (`requirement.md`).

## Zusammenfassung

- **Kein Theming vorhanden:** Es gibt kein `data-theme`-Attribut, keine `prefers-color-scheme`-Media-Query und keine `color-scheme`-Eigenschaft. Alle drei eingebetteten CSS-Blöcke (`PageShell.Css`, `PageShell.NavCss`, `StatusPageComposer.Css`) sind reine Hex-Paletten mit fest verdrahteten Light-Werten.
- **Zwei HTML-Dokument-Pfade existieren:** `PageShell.BeginPage` (für Auth-/Admin-/Fehlerseiten) und `StatusPageComposer.Render` (baut sein `<html>`-Tag selbst, bettet aber `PageShell.NavCss` ein). Eine Theme-Auslieferung müsste beide Pfade erfassen.
- **Zwei Inline-Styles** in `StatusPageComposer.cs`: `style="color:#cbd5e1"` (Z. 90) und `style="margin-top:12px"` (Z. 288) — die einzigen `style=`-Attribute im ausgelieferten Markup.
- **Datenmodell:** `ApplicationUser` besitzt nur das Zusatzfeld `IsDeactivated`; `PageUserContext` ist ein Record mit vier Members (kein Theme). `AspNetUsers` ist der vorgesehene Speicherort; die `ServerSettings`-Tabelle ist benutzerunabhängig.
- **Kontext-Bau zentral:** `EndpointSecurityExtensions.BuildUserContextAsync` lädt bereits das `ApplicationUser`-Objekt und wird von allen authentifizierten Seiten-Handlern genutzt (inkl. GET `/` in `Program.cs`).
- **Profil-Endpunkte:** `AuthEndpoints` kennt `GET /profile` und `POST /profile/password`; PRG über `?msg=`-Schlüssel aus `StatusMessages` ist das etablierte Muster. Ein Theme-POST-Endpunkt existiert noch nicht.
- **Anonyme Seiten** (`/register`, `/login`, `/access-denied` ohne Kontext, `RenderFormExpired`) erhalten keinen `PageUserContext` — die Composer-Signaturen unterscheiden bereits zwischen „mit Benutzer" und „ohne Benutzer".
- **Persistenz-Infrastruktur:** Eine einzige Migration `20261127095016_InitialCreate` liegt vor; `StatusServerDbContextDesignTimeFactory` für `dotnet ef` ist vorhanden; `StatusServerDatabaseInitializer` führt `MigrateAsync` beim Start aus.
- **Session-Verhalten:** `StatusServerCookieEvents.ValidatePrincipal` prüft Security-Stamp, Deaktivierung und Rollenabgleich je Request — eine reine `ApplicationUser`-Eigenschaft wie ein Theme löst keine Session-Invalidierung aus (Cookie-Inhalt bleibt unberührt).

**Test-Ausgangszustand:** `server/Softwareschmiede.StatusServer.Tests` — 154/154 Tests bestanden, 0 fehlgeschlagen, 0 übersprungen (Lauf vom 28.11.2026, Nachweis in [inventory/tests.md](inventory/tests.md)). Die beiden WPF-Testprojekte (`src/Softwareschmiede.Tests`, `src/Softwareschmiede.IntegrationTests`) wurden nicht ausgeführt — sie sind vom serverseitigen Feature nicht betroffen; die Solution baut sauber (0 Warnungen/Fehler). Details und Begründung: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ApplicationUser`, `PageUserContext`, `StatusServerDbContext`, `ServerSetting`, `RelayToken`, Migration
- [Logik](inventory/logic.md) — `PageShell`, `StatusPageComposer`, `AuthPagesComposer`, `AdminPagesComposer`, `EndpointSecurityExtensions`, `AuthEndpoints`, `AdminEndpoints`, `StatusMessages`, `Program.cs`, `StatusServerCookieEvents`, `UserAdminService`
- [CSS / Theming-Istzustand](inventory/css-theming.md) — die drei CSS-Blöcke, Farbpalette, Inline-Styles, HTML-Skelette
- [Enums](inventory/enums.md) — vorhandene Enums als Konventionsreferenz; kein Theme-Enum vorhanden
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Testklassen und Hilfsmethoden
