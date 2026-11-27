# Bestandsaufnahme: Konfiguration, Betrieb, CI und Dokumentation

## `Softwareschmiede.StatusServer.csproj`
Datei: `server/Softwareschmiede.StatusServer/Softwareschmiede.StatusServer.csproj`

- `Microsoft.NET.Sdk.Web`, `net11.0`, `ImplicitUsings`, `Nullable`,
  `GenerateDocumentationFile=true`, `WarningsAsErrors=CS1591` (öffentliche Member brauchen XML-Doc — gilt auch für alle neuen Klassen)
- Einzige Referenz: `ProjectReference → src/Softwareschmiede.StatusContracts`
- **Keine Paketreferenzen** — für Identity + EF Core + SQLite sind Neuzugänge nötig
  (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`,
  `Microsoft.EntityFrameworkCore.Design`; Versionsstand repo-weit `11.0.0`, kein CPM)

## `appsettings.json` / `appsettings.Development.json`
Dateien: `server/Softwareschmiede.StatusServer/appsettings*.json`

Aktueller Bestand `appsettings.json`:

| Sektion | Inhalt | Status nach Anforderung |
|---------|--------|--------------------------|
| `Relay.Tokens` | `[]` | **entfernen** |
| `Relay.DevToken` | `null` | **entfernen** |
| `App.StaleAfterSeconds` | `300` | bleibt |
| `GitHub.Repositories` / `Token` / `CacheTtlSeconds` | `[]` / `null` / `60` | bleibt |
| `Page.Username` / `Page.Password` | `""` / `""` | **entfernen** |
| `Page.RefreshSeconds` | `15` | bleibt |
| `Logging`, `AllowedHosts` | Standard | bleibt |

`appsettings.Development.json` enthält nur `Relay:DevToken = "dev-token"` — **entfällt**;
die Datei kann danach leer bzw. entbehrlich sein.

Hinweis: `server/README.md` referenziert `Properties/launchSettings.json`
(Port `http://localhost:5055`) — diese Datei existiert **nicht** im Repo (vermutlich
nicht eingecheckt; `git status` zeigt sie auch nicht als untracked).

## `server/README.md`
Datei: `server/README.md` (151 Zeilen) — muss nach Anforderung angepasst werden

Betroffene Abschnitte:

- „Endpunkte"-Tabelle (Z. 10–14): `GET /`, `GET /status.json` mit „Basic Auth (sobald konfiguriert)", `POST /relay` mit Relay-Token
- „Lokal starten" (Z. 16–27): `dotnet run`, Dev-Token `dev-token` aus `appsettings.Development.json`
- „Konfiguration" (Z. 29–51): Config-Tabelle inkl. `Relay:Tokens`, `Relay:DevToken`, `Page:Username`/`Password`; Env-Beispiele `Relay__Tokens__0`, `Page__Username`, `Page__Password`
- „Token-Einrichtung und -Rotation" (Z. 53–64): beschreibt Rotation über Config-Liste — künftig Admin-UI
- „systemd-Unit" (Z. 85–103): Environment-Zeilen `Relay__Tokens__0`, `Page__Username`, `Page__Password` — entfallen
- „nginx" (Z. 110–133): WebSocket-Upgrade für `/relay` — bleibt relevant; für Cookies/Login ggf. `X-Forwarded-Proto` erwähnenswert (Anforderung: ForwardedHeaders)
- „Vor dem Livegang" (Z. 135–139): verweist auf `Page:Username`/`Page:Password` + `Relay:Tokens`
- „Bekannte Einschränkungen" (Z. 141–151)

Kein Abschnitt zu Datenbank/`data`-Verzeichnis vorhanden — neu zu dokumentieren
(Setup-Lauf, Persistenzort, Recovery = `data` löschen).

## `.gitignore`
Datei: `.gitignore` (Repo-Root)

- Bereits abgedeckt: `*.db` (Z. 365), `*.db-shm`, `*.db-wal` (Z. 366–367) — eine neue
  `data/*.db`-Datei ist damit ignoriert, ohne dass `.gitignore` angepasst werden muss.

## CI-Workflows
Verzeichnis: `.github/workflows/` — relevante Dateien:

| Workflow | StatusServer-Bezug |
|----------|--------------------|
| `pr-staging-ci.yml` | `dotnet build` des Test-Projekts (Z. 123); „Test regular (StatusServer.Tests)": `dotnet test server/Softwareschmiede.StatusServer.Tests/…csproj --no-build -c Debug` + TRX-Upload (Z. 136–137, 183). Format-Gate: `dotnet format Softwareschmiede.slnx --verify-no-changes` (Z. 85); Solution-Build mit `TreatWarningsAsErrors` (Z. 97) |
| `staging-ci.yml` | identische StatusServer-Schritte (Z. 115, 126–127, 170) |
| `release.yml` | Release-Gate „regular tests (StatusServer.Tests)" (Z. 111–113); Release-Assets `statusserver-win-x64.zip`, `statusserver-linux-x64.zip` (Z. 161, 176, 187) — self-contained Publish |
| `pages.yml` | Pages-Deployment (Doku-Site) — indirekt relevant für `docs/help` |
| `review-*.yml`, `security-scan.yml`, `verify-pr-source.yml`, `staging-to-main-promotion.yml`, `sync-staging-with-main.yml`, `create-review-issue.yml` | keine direkten StatusServer-Schritte |

Test-Befehle in der CI laufen mit `--no-build` nach explizitem Build-Schritt;
lokal gilt projektkonform: voller Build vor `dotnet test` (siehe CLAUDE.md).

## Dokumentationsstruktur

- `docs/RELEASE_NOTES.md` — zweisprachig (EN „What's New" / DE „Neuerungen"), StatusServer-Feature von Issue 478 ist bereits als Eintrag vorhanden (Basic-Auth-/Relay-Token-Beschreibung darin wird mit dieser Änderung überholt)
- `README.md` (Root) — Z. 220–221 beschreiben Remote-Übersicht/StatusServer inkl. „hinter Basic-Auth"; Z. 321 Projektstruktur-Eintrag `server/`
- `docs/help/` — thematische Hilfeseiten (`anwendung`, `aufgaben`, `einstellungen`, `plugins`, …); **`docs/help/einstellungen/remote-uebersicht.md`** dokumentiert die App-Seite der Remote-Übersicht inkl. Relay-Token-Eintrag im Credential Store — serverseitige Token-Erzeugung in der Admin-UI ändert den dokumentierten Bezugsweg des Tokens
- `server/README.md` — Betriebsdoku des StatusServers (s. o.)
- `docs/features/` — Feature-Arbeitsverzeichnisse des Lifecycle-Prozesses

## Weitere Berührungspunkte App-Seite (nicht im Umbau-Scope, aber fachlich verbunden)

- `RemoteStatusRelayService` (App-Seite, `src/Softwareschmiede*`): hält die ausgehende
  `wss://`-Verbindung und sendet das Relay-Token aus dem Credential Store — die
  Transportseite (`access_token`-Query/Bearer) bleibt unverändert, nur die
  Token-Herkunft auf dem Server wechselt von Config nach DB. App-seitig ist kein
  Umbau gefordert.
