# Bestandsaufnahme: Benutzeranmeldung in Webseite (StatusServer)

Bestandsaufnahme des `server/Softwareschmiede.StatusServer` und seines Testprojekts
sowie des EF-Core-Vorbilds im Hauptprojekt, bezogen auf die Anforderung
„Benutzeranmeldung in Webseite" (Issue #487): ASP.NET-Core-Identity-Benutzerverwaltung
mit EF Core + SQLite, Cookie-Login mit zwei Rollen, datenbankgestützte Relay-Tokens,
Entfernen der `Page:*`-/`Relay:*`-Konfiguration.

## Zusammenfassung

- Der StatusServer ist ein schlankes Minimal-API-Projekt (`net11.0`, `Microsoft.NET.Sdk.Web`, **keine Paketreferenzen**, nur `StatusContracts`-Projektverweis): drei Endpunkte (`/`, `/status.json`, `/relay`), optionaler Basic-Auth-`PageAccessMiddleware` gegen `Page:Username`/`Page:Password` und config-basierter `RelayTokenValidator` gegen `Relay:Tokens`/`Relay:DevToken`.
- Es existiert **keinerlei Persistenz-Infrastruktur** im StatusServer: kein DbContext, keine Entities, keine Migrationen, kein `data`-Verzeichnis, keine Identity-/Auth-Registrierung — der gesamte geforderte Daten-/Benutzer-Stack (`StatusServerDbContext`, `ApplicationUser`, `RelayToken`-Entity, `ServerSetting`, Rollen `Administration`/`Ueberwacher`, Formularseiten, Admin-UI) ist Neubau.
- Wiederverwendbar vorhanden: `SecretComparer.FixedTimeEquals` (Konstantzeit-Vergleich für Token-Hashes), `StatusPageComposer`-Stil (server-gerendertes, JS-freies HTML mit `HtmlEncode`/Scheme-Whitelist-Helfern) als Vorbild der Formularseiten, `StatusRelayHub.OnConnectedAsync` (Token-Transport bleibt, nur Quelle wechselt).
- EF-Core-Konventionen sind im Hauptprojekt etabliert und übernehmbar: `SoftwareschmiedeDbContext` mit Unix-ms-`DateTimeOffset`-Konvertern, `IDesignTimeDbContextFactory`, eingecheckte Migrationen, `Directory.CreateDirectory` + `MigrateAsync()` beim Start (`App.xaml.cs`); Versionen repo-weit `11.0.0`, **kein** `Directory.Packages.props` — `Microsoft.AspNetCore.Identity.EntityFrameworkCore` fehlt noch überall.
- `PageAccessMiddleware`, `PageOptions.Username`/`Password`, `RelayOptions.Tokens`/`DevToken`, die entsprechenden `appsettings`-Einträge und `StatusWebApplicationFactory`-UseSettings entfallen; `server/README.md` (Endpunkte, Token-Rotation, systemd-Env) und die StatusServer-Abschnitte in `README.md`/`docs/RELEASE_NOTES.md`/`docs/help/einstellungen/remote-uebersicht.md` sind anzupassen.
- Es gibt **keine Interfaces und keine Enums** im StatusServer (alles `sealed` Konkretklassen, Strings statt Enums in den DTOs) — entsprechende Detailabschnitte entfallen.
- Test-Ausgangszustand: reguläre Lane fehlerfrei — StatusServer.Tests 44/44, Softwareschmiede.Tests 2878/2881 (3 plattformbedingt übersprungen), IntegrationTests 436/436, alle auf Commit `7430f1af`; die `Category=OsInterface`-Lane (E2E/ConPTY/FlaUI, ohne StatusServer-Bezug) wurde nicht ausgeführt — Details und Nachweise unter [Tests](inventory/tests.md).

## Details

- [Datenmodell / Konfigurationsklassen](inventory/models.md) — `StatusServerOptions`-Baum, `GitHubRepositoryOptions`, `InstanceSnapshotView`, GitHub-View-Records, StatusContracts-DTOs; ausdrücklich: keine EF-Entities vorhanden
- [Logikklassen](inventory/logic.md) — `Program` (DI + Endpunkte), `PageAccessMiddleware`, `RelayTokenValidator`, `SecretComparer`, `StatusRelayHub`, `InstanceSnapshotCache`, `StatusPageComposer`, `GitHubStatusService`
- [EF-Core-Vorbild](inventory/efcore-vorbild.md) — `SoftwareschmiedeDbContext`-Konventionen, Design-Time-Factory, Migrationen, Start-Migration, Paketstände, Test-Persistenz-Muster
- [Konfiguration, Betrieb, CI, Dokumentation](inventory/config-betrieb-doku.md) — csproj, `appsettings*`, `server/README.md`, `.gitignore`, CI-Workflows, `docs/help`, `README.md`, `docs/RELEASE_NOTES.md`
- [Tests und Test-Ausgangszustand](inventory/tests.md) — Testklassen/Hilfen des Testprojekts, gesicherte Baseline-TRX-Reports, Testlücken
