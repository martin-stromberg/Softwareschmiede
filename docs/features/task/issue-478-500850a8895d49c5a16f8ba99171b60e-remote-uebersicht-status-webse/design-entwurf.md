# Designentwurf: Remote-Übersicht (Status-Webseite, Relay-Architektur)

Empfehlungsstand für Issue #478 — Entscheidungsvorlage zu den offenen Punkten.

## Gesamtbild

```
 Windows-PC (Softwareschmiede.App)              Linux-Server (nginx, status.<domain>)
┌─────────────────────────────┐   wss://…/relay   ┌────────────────────────────────┐
│ RemoteStatusRelayService    │ ────────────────► │ Softwareschmiede.StatusServer  │
│ (BackgroundService, Token   │  + RegisterInst.  │  ├ StatusRelayHub (SignalR)    │
│  aus ICredentialStore)      │  + PushSnapshot   │  ├ RelayTokenValidator         │
│ StatusSnapshotProjector     │   (60 s + Events) │  ├ InstanceSnapshotCache       │
│ (Projektion aus DB+Runtime) │                   │  ├ GitHubStatusService         │
└─────────────────────────────┘                   │  │  └ GitHub REST + ETag-Cache │
                                                  │  ├ StatusPageComposer (HTML)  │
                                                  │  └ CommentActionService ────┐ │
                                                  └───────────▲─────────────────┼─┘
                                                              │ GET /           │ POST
                                                     Browser (Basic Auth)       │ Kommentar
                                                                               ▼
                                                                    GitHub Issues/PRs
                                                                          │ SCM-Ereignis-
                                                                          ▼ eingang (bestehend)
                                                              App verarbeitet Kommentar
```

## Bausteine

### 1. Geteilte Verträge — `src/Softwareschmiede.StatusContracts` (neu, abhängigkeitsfrei)

- `StatusSnapshot`, `StatusSnapshotWorkItem`, `StatusSnapshotScmEvent` (DTOs)
- `RegisterInstanceRequest`/`Result` (Handshake: Instanz-ID, Instanzname, `RelayProtocolVersion.Current = 1`)
- Folgt der `Softwareschmiede.Plugin.Contracts`-Konvention; wird von App-Core und Server referenziert.

### 2. App-Seite — `src/Softwareschmiede` + `src/Softwareschmiede.App`

- `RemoteStatusRelayService : BackgroundService` — hält die ausgehende Verbindung (eigener Zustandsautomat: deaktiviert / Konfigurationsfehler / verbindend / verbunden / Reconnect-Backoff), wartet auf `ScmEventStartupGate` wie die SCM-Dienste.
- `RemoteStatusRelayClient` — Wrapper um `HubConnection` (`Microsoft.AspNetCore.SignalR.Client` 11.x, `WithAutomaticReconnect`, Token via `AccessTokenProvider` aus `ICredentialStore`).
- `StatusSnapshotProjector` — read-only Projektion: `WorkItem`s (inkl. Live-`RunStatus`, Pausen, Issue-/PR-Verweise), wartende `ScmIncomingEvent`s (mit `Reason`), Versionsinfo. **Keine neue Persistenz.**
- `RemoteStatusConnectionInfo` — Singleton-Zustandsobjekt für die Einstellungs-UI (Verbindungsstatus, `LastSnapshotUtc`, `LastError`).
- Einstellungen `status.remote.*` in `AppSettingService` (Schalter, URL, Instanzname, Intervall 15–3600 s, Default 60); Instanz-ID einmalig generiert + persistent (`status.remote.instanceid`); Relay-Token in `ICredentialStore` → **Rotation ohne App-Update**.
- Neuer Abschnitt „Remote-Übersicht" im Einstellungen-Tab „Automatisierung": Schalter, URL, Instanzname, Token-Eingabe, Instanz-ID + Live-Verbindungsstatus.

### 3. Server — `server/Softwareschmiede.StatusServer` (neu, `Microsoft.NET.Sdk.Web`, net11, in `Softwareschmiede.slnx`)

- `StatusRelayHub` unter `/relay`: `RegisterInstance` + `PushSnapshot`; `OnConnected/OnDisconnected` = Presence.
- `RelayTokenValidator`: Token-Liste aus Konfiguration; Ablehnung schließt die Verbindung.
- `InstanceSnapshotCache`: In-Memory, je Instanz-ID letzter Snapshot + `lastSeenUtc` + verbunden-Flag. Veraltung primär über Disconnect, sekundär über `App:StaleAfterSeconds` (Default 300 s).
- `GitHubStatusService`: GitHub REST (offene PRs, Releases, Issues) je konfiguriertem Repo, ETag/304 + TTL-Cache (60 s), Fehler je Endpunkt mit Zeitstempel sichtbar.
- `StatusPageComposer`: server-gerendertes HTML, `<meta refresh>` 15 s, `GET /status.json` als JSON-Zwilling.
- `CommentActionService` + `POST /actions/comment`: Allowlist (Repo ∈ Config, Aktionstyp ∈ Templates, Ziel existiert) → GitHub-Kommentar mit Vorlage + eigenem Marker `<!-- sws-statusseite:{id} -->`.
- `PageAccessMiddleware`: Basic Auth (`Page:Username`/`Page:Password`; leer = lokaler Dev-Modus).

### 4. Status-Seite (server-gerendert, read-only)

```
┌──────────────────────────────────────────────────────────┐
│ Softwareschmiede-Status   ● online  · Instanz „Martin-PC" │
│ Stand: 18:32:14 · Snapshot: vor 12 s · Seite aktualisiert  │
│ sich alle 15 s                                           │
├──────────────────────────────────────────────────────────┤
│ Laufende Aufgaben (2)                       [Quelle: App] │
│  ▸ #479 XAML-Validierung   Running · Session aktiv       │
│  ▸ #481 Release-Notes      Started · Heartbeat 18:32     │
│ Wartet auf Anwender (1)                     [Quelle: App] │
│  ▸ #478 Remote-Übersicht   WaitingForInput — „Rückfrage  │
│    Zugriffsschutz?" seit 17:04                           │
│ Letzte SCM-Ereignisse (5)                   [Quelle: App] │
├──────────────────────────────────────────────────────────┤
│ Offene Pull Requests (3)                [Quelle: GitHub] │
│  ▸ #482 Backmerge → staging   Checks: ✗ Blocked          │
│ Letzte Releases (3)                     [Quelle: GitHub] │
│ Abruf: 18:32:02                                          │
└──────────────────────────────────────────────────────────┘
Offline: App-Sektionen ausgegraut + „offline seit 18:01:22"
```

## Empfehlungen zu den offenen Punkten

| # | Punkt | Empfehlung |
|---|-------|-----------|
| 1 | Server-Technologie | ASP.NET Core SignalR im Repo (`/Server/`-Ordner in slnx) |
| 2 | Umfang v1 | Übersicht + Aktionsgerüst; Kommentar-Aktionen hinter `Actions:Enabled`-Schalter |
| 3 | Kommentar-Identität | Owner-PAT → Validierung greift ohne App-Änderung |
| 4 | Repo-Aggregation | serverseitige Konfigurationsliste, initial nur Softwareschmiede |
| 5 | Zugriffsschutz | Basic-Auth-Middleware (Pflicht vor Livegang; nginx/Tailscale ergänzbar) |
| 6 | Adresse | Subdomain `status.<domain>` + nginx-Location mit WebSocket-Upgrade |
| 7 | Intervalle | Push 60 s, Change-Push ≥10 s gedrosselt, Stale 300 s / Disconnect |
| 8 | Token-Rotation | UI-Feld → CredentialStore; serverseitig Config + Neustart |
| 9 | Multi-Instanz | eigener Sektions-Block je Instanz-ID + optionaler Name |
| 10 | „Wartet auf Anwender" | `WaitingForInput` + `ScmIncomingEvent.Reason` + Pausen |

## Lokale Nachweisbarkeit (diese Umgebung)

- Server-Dienst auf `http://localhost:5055`; App-Relay verbindet sich dorthin.
- Primärer Funktionsnachweis: `WebApplicationFactory`-Integrationstest (echte SignalR-Verbindung → Cache → gerenderte Seite) + FlaUI-E2E für den neuen Einstellungs-Abschnitt.
- Screenshots der lokal laufenden Seite (online + offline-Zustand) als Issue-Beleg.
- Produktiver Livegang (Domain, systemd, nginx, Tokens) = `server/README.md` + separater Schritt.

## Nicht-Ziele (bestätigungspflichtig)

Kein Kommando-Rückkanal, keine App-Persistenz neu, kein eingehender Zugriff auf den PC, kein Multi-User-Portal, kein Freitext auf der Seite, kein MQTT, kein Relay-Rückkanal, kein produktives Deployment aus dieser Umgebung, kein echter GitHub-E2E-Nachweis der Kommentar-Aktion, keine Bitbucket-Aggregation.
