# Softwareschmiede StatusServer

Eigens gehostete Status-Webseite für die Remote-Übersicht (Issue 478). Der Server nimmt
Status-Snapshots der Softwareschmiede-App-Instanzen über einen SignalR-Hub entgegen,
holt ergänzend öffentliche GitHub-Daten (Pull Requests, Releases, offene Issues) und
rendert alles als schlanke, read-only HTML-Seite.

## Endpunkte

| Endpunkt | Zweck | Zugriff |
|----------|-------|---------|
| `GET /` | Server-gerenderte Status-Seite (HTML, Meta-Refresh `Page:RefreshSeconds`) | Basic Auth (sobald konfiguriert) |
| `GET /status.json` | Maschinelle Sicht (JSON: `GeneratedUtc`, `Instances`, `GitHub`) | Basic Auth (sobald konfiguriert) |
| `POST /relay` | SignalR-Hub für die App-Instanzen (`RegisterInstance`, `PushSnapshot`) | Relay-Token (`access_token`-Query oder Bearer-Header) |

## Lokal starten

```bash
cd server/Softwareschmiede.StatusServer
dotnet run
# lauscht auf http://localhost:5055 (Properties/launchSettings.json)
```

Für die lokale Entwicklung akzeptiert der Hub den Dev-Token aus
`appsettings.Development.json` (`Relay:DevToken = "dev-token"`). In
`appsettings.json` ist `DevToken` bewusst `null` — ein produktiv betriebener Server
lehnt ihn ab.

## Konfiguration (`appsettings.json`)

| Sektion | Schlüssel | Bedeutung |
|---------|-----------|-----------|
| `Relay` | `Tokens` | Liste erlaubter Relay-Tokens der App-Instanzen (Pflicht im Betrieb) |
| `Relay` | `DevToken` | Dev-Fallback-Token; im Produktiv-Profil `null` lassen |
| `App` | `StaleAfterSeconds` | Ab wann ein verbundener Snapshot als veraltet gilt (Default 300 s) |
| `GitHub` | `Repositories` | `["owner/repo", …]` — Repos für PR-/Release-/Issue-Sektionen |
| `GitHub` | `Token` | Optionales PAT für höhere Rate-Limits (nur `public_repo`-Scope nötig) |
| `GitHub` | `CacheTtlSeconds` | Mindestabstand zwischen GitHub-Abrufen; ETag/304 wird zusätzlich genutzt |
| `Page` | `Username`/`Password` | Basic-Auth für `/` und `/status.json`; **vor dem Livegang Pflicht** |
| `Page` | `RefreshSeconds` | Meta-Refresh-Intervall der HTML-Seite |

Secrets (Tokens, Passwort) nicht im Repo einchecken — per Umgebungsvariable oder
User-Secrets setzen, z. B.:

```bash
export Relay__Tokens__0="langes-zufaelliges-token-1"
export Relay__Tokens__1="langes-zufaelliges-token-2"
export Page__Username="status"
export Page__Password="geheim"
export GitHub__Token="github_pat_..."
```

### Token-Einrichtung und -Rotation

1. Pro App-Instanz ein ausreichend langes, zufälliges Token erzeugen
   (`pwgen 48 1`, `New-Guid` o. ä.).
2. Token in `Relay:Tokens` auf dem Server eintragen **und** in der App unter
   `Einstellungen → Remote-Übersicht → Relay-Token` hinterlegen (wird im
   Credential Store der App gehalten, nie im Klartext angezeigt).
3. Rotation: neues Token zuerst in `Relay:Tokens` **ergänzen** (altes bleibt
   bestehen), in der App umstellen, dann das alte Token aus der Liste entfernen.
   So gibt es kein Verbindungsloch. App-Instanzen, die nach dem Entfernen noch das
   alte Token senden, werden vom Hub abgelehnt und melden den Fehler in den
   App-Einstellungen.

## Veröffentlichen

```bash
dotnet publish server/Softwareschmiede.StatusServer -c Release -o publish
# z. B. linux-x64 self-contained:
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
Environment=Relay__Tokens__0=langes-zufaelliges-token-1
Environment=Page__Username=status
Environment=Page__Password=geheim
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now softwareschmiede-status
```

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
        proxy_read_timeout 3600s;   # langlebige SignalR-Verbindung
    }

    location / {
        proxy_pass         http://127.0.0.1:5055;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

## Vor dem Livegang

- `Page:Username`/`Page:Password` setzen — ohne Basic Auth steht die Seite offen.
- `Relay:Tokens` pflegen, `Relay:DevToken` bleibt `null`.
- TLS über den Reverse Proxy terminieren (`https://`/`wss://`).

## Bekannte Einschränkungen

- **Authentifizierter Unternehmensproxy**: die ausgehende `wss://`-Verbindung der
  App nutzt den Systemproxy-Default; Proxy-Authentifizierung ist nicht abgedeckt.
  In Umgebungen mit Pflicht-Proxy-Auth schlägt der Connect fehl und wird in den
  App-Einstellungen als Verbindungsfehler angezeigt.
- **Kein Checks-/Actions-Spalten-Platz in v1**: Die Seite ist bewusst rein lesend —
  weder GitHub-Check-Runs noch Aktionen (z. B. Kommentar-Anstoß) sind Teil dieses
  Schritts (Anwender-Entscheidung; Erweiterungspfad im Feature-Plan `plan.md`).
- Die Seite rendert bewusst serverseitig ohne JavaScript; Updates laufen über
  Meta-Refresh (`Page:RefreshSeconds`).
