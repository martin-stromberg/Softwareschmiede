# Softwareschmiede StatusServer

Eigens gehostete Status-Webseite für die Remote-Übersicht (Issue 478, Benutzerverwaltung:
Issue 487). Der Server nimmt Status-Snapshots der Softwareschmiede-App-Instanzen über
einen SignalR-Hub entgegen, holt ergänzend öffentliche GitHub-Daten (Pull Requests,
Releases, offene Issues) und rendert alles als schlanke, read-only HTML-Seite. Zugang
zur Seite erhalten nur angemeldete Benutzer (Cookie-Anmeldung, ASP.NET Core
Identity + SQLite).

## Endpunkte

| Endpunkt | Zweck | Zugriff |
|----------|-------|---------|
| `GET /` | Server-gerenderte Status-Seite (HTML, Meta-Refresh `Page:RefreshSeconds`) | Anmeldung (Stufen `Administration` oder `Ueberwacher`) |
| `GET /status.json` | Maschinelle Sicht (JSON: `GeneratedUtc`, `Instances`, `GitHub`) | Anmeldung (beide Stufen) |
| `POST /relay` | SignalR-Hub für die App-Instanzen (`RegisterInstance`, `PushSnapshot`) | Relay-Token aus der Datenbank (`access_token`-Query oder Bearer-Header) |
| `GET`/`POST /register` | Erst-Registrierung (nur solange kein Benutzer existiert) | anonym |
| `GET`/`POST /login` | Anmeldung (3 Fehlversuche → Sperrung für 1 Stunde) | anonym |
| `POST /logout` | Abmeldung | angemeldet |
| `GET /profile`, `POST /profile/password` | Eigenes Profil / Passwort ändern | angemeldet |
| `GET /access-denied` | Hinweisseite bei fehlender Berechtigung (z. B. `Ueberwacher` auf Admin-URLs) | anonym |
| `/admin/users`, `/admin/tokens`, `/admin/settings` | Verwaltungs-UI (Benutzer, Relay-Tokens, Sitzungsdauer) | Stufe `Administration` |

## Erststart (Setup)

Beim ersten Start legt der Server das Verzeichnis `data/` samt SQLite-Datenbank
(`data/statusserver.db`) an und führt die Migrationen aus. Solange noch kein Benutzer
existiert, leitet jeder Aufruf von `/` auf `/register` weiter: Der **erste registrierte
Benutzer erhält die Stufe `Administration`**; danach ist die Registrierung dauerhaft
geschlossen und weitere Benutzer legt ausschließlich die Administration über
`/admin/users` an.

### Benutzerverwaltung (Admin-UI)

- **Benutzer** (`/admin/users`): anlegen (Name, Passwort mit mindestens 8 Zeichen,
  Stufe `Administration`/`Ueberwacher`), Stufe nachträglich ändern,
  deaktivieren/aktivieren, löschen, Sperrung zurücksetzen.
  Der letzte aktive Administrator kann weder deaktiviert, gelöscht noch herabgestuft werden.
  Stufenänderungen und Deaktivierungen wirken zeitnah auch auf bestehende Sessions.
- **Relay-Tokens** (`/admin/tokens`): Token mit Label anlegen — der **Klartext wird
  genau einmal** angezeigt und gehört in die App-Einstellungen (Remote-Übersicht);
  gespeichert wird nur der SHA-256-Hash. Widerrufene Tokens bleiben zur
  Nachvollziehbarkeit in der Liste, berechtigen aber nicht mehr zum `/relay`-Connect.
  Ohne Tokens lehnt der Hub alle Verbindungen ab.
- **Einstellungen** (`/admin/settings`): Sitzungsdauer der Anmeldesession in Minuten
  (5–43200, Default 480), gilt ab der nächsten Cookie-Ausstellung/-Erneuerung.

### Sperrung, Deaktivierung, Recovery

- Nach **3 fehlgeschlagenen Anmeldeversuchen** ist das Konto für **1 Stunde** gesperrt;
  die Administration kann die Sperrung vorzeitig zurücksetzen.
- **Deaktivierte** Konten können sich nicht anmelden und verlieren bestehende
  Sessions sofort; Aktivieren nur durch die Administration.
- **Alle Administratoren ausgesperrt?** Es gibt bewusst keinen In-App-Recovery-Weg:
  das `data`-Verzeichnis löschen und den Server neu starten — der Setup-Lauf
  (`/register`) beginnt von vorn. Snapshots im Arbeitsspeicher-Cache gehen dabei
  ohnehin verloren.

## Lokal starten

```bash
cd server/Softwareschmiede.StatusServer
dotnet run
# lauscht auf http://localhost:5055 (Properties/launchSettings.json)
```

Danach im Browser `http://localhost:5055` öffnen — der erste Aufruf landet im Setup
(`/register`). Ein Relay-Token für die App-Verbindung wird über `/admin/tokens`
erzeugt (kein Dev-Fallback mehr).

## Konfiguration (`appsettings.json`)

| Sektion | Schlüssel | Bedeutung |
|---------|-----------|-----------|
| `App` | `StaleAfterSeconds` | Ab wann ein verbundener Snapshot als veraltet gilt (Default 300 s) |
| `GitHub` | `Repositories` | `["owner/repo", …]` — Repos für PR-/Release-/Issue-Sektionen |
| `GitHub` | `Token` | Optionales PAT für höhere Rate-Limits (nur `public_repo`-Scope nötig) |
| `GitHub` | `CacheTtlSeconds` | Mindestabstand zwischen GitHub-Abrufen; ETag/304 wird zusätzlich genutzt |
| `Page` | `RefreshSeconds` | Meta-Refresh-Intervall der HTML-Seite |
| `Data` | `DatabasePath` | Interner Override für den SQLite-Pfad (Default `<ContentRoot>/data/statusserver.db`); primär für Tests gedacht |

Die früheren Einträge `Relay:Tokens`, `Relay:DevToken`, `Page:Username` und
`Page:Password` sind **entfallen** — Relay-Tokens kommen aus der Datenbank,
der Seitenzugriff läuft über die Cookie-Anmeldung. Ein Migrationspfad existiert
nicht; alte Einträge werden ignoriert.

Das optionale `GitHub:Token` nicht ins Repo einchecken — per Umgebungsvariable
oder User-Secrets setzen, z. B. `export GitHub__Token="github_pat_..."`.

### Token-Einrichtung und -Rotation

1. In der Admin-UI unter **Tokens** ein Token mit sprechendem Label anlegen
   (z. B. Name der App-Instanz); den einmalig angezeigten Klartext kopieren.
2. In der App unter `Einstellungen → Automatisierung → Remote-Übersicht →
   Relay-Token` hinterlegen (wird im Credential Store der App gehalten, nie im
   Klartext angezeigt).
3. Rotation: neues Token anlegen und in der App umstellen, danach das alte Token
   widerrufen. App-Instanzen, die weiterhin das widerrufene Token senden, werden
   vom Hub abgelehnt und melden den Fehler in den App-Einstellungen.

## Veröffentlichen

Der StatusServer wird mit jedem Release und Pre-Release als eigenständiges Asset
ausgeliefert — self-contained, auf der Zielmaschine ist **keine** .NET-Laufzeit
erforderlich:

- `statusserver-linux-x64.zip` — der dokumentierte systemd/nginx-Pfad (siehe unten)
- `statusserver-win-x64.zip` — optionaler Betrieb unter Windows (direkter Start,
  Windows-Dienst oder IIS über das ASP.NET Core Hosting Bundle)

Alternativ manuell paketieren:

```bash
dotnet publish server/Softwareschmiede.StatusServer -c Release -r linux-x64 \
    --self-contained -o publish
```

## systemd-Unit (Beispiel)

```ini
# /etc/systemd/system/softwareschmiede-status.service
[Unit]
Description=Softwareschmiede StatusServer
After=network.target

[Service]
WorkingDirectory=/opt/softwareschmiede-status
ExecStart=/opt/softwareschmiede-status/Softwareschmiede.StatusServer
Environment=ASPNETCORE_URLS=http://127.0.0.1:5055
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now softwareschmiede-status
```

Die Datenbank liegt unter `data/` im `WorkingDirectory` — Verzeichnis mit in die
Sicherung/Monitoring-Checks aufnehmen.

## nginx (Reverse Proxy mit WebSocket-Upgrade für `/relay`)

```nginx
server {
    listen 443 ssl;
    server_name status.example.org;

    location /relay {
        proxy_pass         http://127.0.0.1:5055;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 3600s;   # langlebige SignalR-Verbindung
    }

    location / {
        proxy_pass         http://127.0.0.1:5055;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

`X-Forwarded-Proto` ist wichtig: Der Server nutzt Forwarded-Headers, damit das
Anmelde-Cookie hinter dem HTTPS-Proxy als Secure markiert wird — ohne den Header
setzt der Server den Cookie für `http` und der Browser lehnt ihn beim Login ggf. ab.

## Vor dem Livegang

- Ersten Administrator über `/register` anlegen; weitere Konten in der Admin-UI.
- Pro App-Instanz ein Relay-Token in der Admin-UI erzeugen und in der App hinterlegen.
- TLS über den Reverse Proxy terminieren (`https://`/`wss://`) und
  `X-Forwarded-Proto` weiterreichen (siehe nginx-Beispiel).

## Bekannte Einschränkungen

- **Authentifizierter Unternehmensproxy**: die ausgehende `wss://`-Verbindung der
  App nutzt den Systemproxy-Default; Proxy-Authentifizierung ist nicht abgedeckt.
  In Umgebungen mit Pflicht-Proxy-Auth schlägt der Connect fehl und wird in den
  App-Einstellungen als Verbindungsfehler angezeigt.
- **Kein Checks-/Actions-Spalten-Platz in v1**: Die Seite ist bewusst rein lesend —
  weder GitHub-Check-Runs noch Aktionen (z. B. Kommentar-Anstoß) sind Teil dieses
  Schritts (Anwender-Entscheidung; Erweiterungspfad im Feature-Plan `plan.md`).
- **Kein Passwort-Reset für fremde Konten**: die Administration kann Konten nur
  anlegen, stufen, (de)aktivieren, löschen und Sperrungen zurücksetzen — bei einem
  vergessenen Passwort bleibt Löschen + Neu-Anlegen (oder die eigene Änderung im
  Profil durch den Benutzer selbst).
- Die Seite rendert bewusst serverseitig ohne JavaScript; Updates laufen über
  Meta-Refresh (`Page:RefreshSeconds`).
