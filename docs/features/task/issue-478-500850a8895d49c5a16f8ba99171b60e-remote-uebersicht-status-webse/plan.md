# Umsetzungsplan: Remote-Übersicht über externe Status-Webseite

## Übersicht

Es wird eine externe, zugriffsgeschützte Status-Webseite eingeführt, die den Gesamtzustand der Softwareschmiede-Anwendung auch ohne laufende App-Sicht einsehbar macht. Die Anwendung erhält einen ausgehenden SignalR-Relay-Client (`RemoteStatusRelayService`), der einen periodisch projizierten Status-Snapshot an einen neuen ASP.NET-Core-Dienst (`Softwareschmiede.StatusServer`) liefert; der Server cached den Snapshot je Instanz mit `lastSeenUtc`, ergänzt GitHub-Sektionen eigenständig per GitHub-API und rendert die read-only Seite. Steuerung erfolgt ausschließlich über serverseitig geschriebene GitHub-Kommentare (enge Allowlist), die über den bestehenden SCM-Ereigniseingang in die App zurückfließen. Da in dieser Umgebung kein produktiver Linux-Server und keine echte Domain verfügbar ist, wird der Server-Dienst als neues .NET-Projekt im Repo implementiert und lokal auf `localhost` ausgeführt; Deployment-/Betriebsdetails werden dokumentiert (`server/README.md`) und bleiben offene Punkte.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Der *App-Status-Snapshot* je App-Instanz — zeitpunktbezogene, read-only projizierte Zustandsdaten aus `SoftwareschmiedeDbContext`-Beständen (`WorkItem` mit `Status`/`RunStatus`/`PausedUntilUtc`/`LastHeartbeatUtc`/`ActiveRunId`/`Kind`/`IssueReference`/`PullRequests`, `ScmIncomingEvent`-Warteschlange mit `Status`/`Reason`) plus Laufzeit-Singletons (`IRunningAutomationStatusSource.GetLiveRunStatus`, `IRunningAutomationsCount.CountAsync`, `IApplicationVersionProvider`). Serverseitige Beobachtungseinheit ist die *Relay-Verbindung je Instanz-ID* (verbunden = online, getrennt = „offline seit `lastSeenUtc`"). |
| Auslösende Ereignisse | App-Start bei aktivierter Remote-Übersicht → Verbindungsaufbau + initialer Snapshot. Snapshot-Intervall (Default 60 s) → periodischer Push. Change-Signale (`WorkItemRunDataChangedNotifier.RunDataChanged`, `ScmEventChangeNotifier.Changed`) → gedrosselter Zusatz-Push. Verbindungsabbruch → Server markiert App-Sektionen veraltet; Reconnect → wieder live. Browser-Aufruf der Status-URL → Server rendert aus Snapshot-Cache + GitHub-Cache. Die Seite ist in v1 rein lesend (Anwender-Entscheidung 2026-11-25): keine Aktionsschaltflächen, kein serverseitiges Kommentar-Posting; ein Steuerpfad über allowlistete GitHub-Kommentare bleibt spätere Erweiterung. |
| Erwartete Wirkung | Die Seite ist lokal unter `http://localhost:<port>/` (produktiv: `status.<domain>`, siehe Offene Punkte) zugriffsgeschützt erreichbar und zeigt die Sektionen „Laufende Aufgaben", „Wartet auf Anwender", „Offene Pull Requests", „Letzte Releases/Versionen", „Letzte SCM-Ereignisse/Diagnosen" sowie einen Heartbeat-/Offline-Badge. Alle referenzierten Objekte (Issues, Pull Requests, Releases) sind als Links zu GitHub klickbar (Anwender-Vorgabe). Bei verbundener App: Live-Zustand; bei getrennter Relay-Verbindung: App-Sektionen eindeutig veraltet markiert mit „offline seit `lastSeenUtc`", GitHub-Sektionen bleiben live; bei nicht erreichbarer GitHub-API werden GitHub-Sektionen mit Zeitstempel/Fehler gekennzeichnet. |
| Umgang mit fehlenden Objekten | **Instanz-ID:** erzeugen und persistent wiederverwenden — generierte GUID als `AppSetting`-Schlüssel `status.remote.instanceid` (analog `GetOrCreate`-Muster); optionaler Anzeigename `status.remote.instancename`. **Relay-Token fehlt/ungültig:** Verbindung wird nicht aufgebaut; Zustand wird ehrlich gemeldet (`RemoteStatusConnectionInfo` + Serilog + Einstellungsanzeige), keine stille Dauerschleife — Reconnect mit Backoff nur bei „echten" Transportsfehlern, nicht bei Konfigurationsfehlern. **Kein Snapshot im Server-Cache:** App-Sektionen zeigen „keine Daten / noch nie verbunden", GitHub-Sektionen bleiben live — kein falscher „alles läuft"-Zustand. **Serverseitiges GitHub-Token fehlt oder Rate-Limit erschöpft:** GitHub-Sektionen werden mit Zeitstempel/Fehler markiert; ohne Token werden öffentliche Daten weiterhin (ratenlimitiert) gelesen, sofern möglich. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein Kommando-Rückkanal über den Relay (keine allowlisteten Remote-Befehle von Server zu App). | Bewusste Architekturentscheidung der Anforderung (Auditierbarkeit, keine neue Kommandoschnittstelle). | bestätigt |
| 2 | Keine neue Schema-/Persistenz-Ebene in der Anwendung; der Snapshot ist reine Projektion aus `SoftwareschmiedeDbContext` + Laufzeit-Singletons. | Anforderung; keine EF-Migration nötig. | bestätigt |
| 3 | Kein eingehender Netzwerkzugriff auf den Windows-PC — nur ausgehende `wss://`-/`ws://`-Verbindung. | PC hinter NAT; Anforderung. | bestätigt |
| 4 | Kein Multi-User-Portal; die Seite ist reine externe Übersicht und ersetzt nicht die Aufgabenliste der Anwendung. | Anforderung. | bestätigt |
| 5 | Keine Kommentar-Aktionen/Aktionsbuttons in v1 — die Seite ist rein lesend. | Anwender-Entscheidung 2026-11-25 („Lesend genügt fürs Erste"); der vorgesehene Steuerpfad (allowlistete Kommentare, Owner-PAT, `sws-statusseite:`-Marker) bleibt als spätere Erweiterung dokumentiert. | entschieden |
| 6 | MQTT-Variante (Broker mit retained messages) ist nicht der Ansatz — eigener SignalR-Dienst. | In der Anforderung abgewogen und verworfen (kein Drill-down-Query-Kanal, kein einfacher Rückkanal). | bestätigt |
| 7 | Echter Relay-Rückkanal langfristig (Server → App Befehle) ist nicht Teil dieser Umsetzung. | Bewusst separat zu entscheiden laut Issue. | bestätigt |
| 8 | Produktives Deployment (echte Domain, Linux-Server, systemd, nginx, produktive Tokens) kann in dieser Umgebung nicht ausgeführt oder nachgewiesen werden — geliefert werden der lauffähige Dienst inkl. localhost-Ausführung und Betriebsdoku; der produktive Livegang ist separater Schritt. | Kein Zugriff auf den produktiven Server und keine echte Domain in dieser Umgebung (Auftraggeber-Vorgabe). | bestätigt |
| 9 | Produktiver GitHub-Schreibzugriff entfällt — mit dem Wegfall der Kommentar-Aktionen (Ausschluss 5) benötigt der Server nur lesenden GitHub-Zugriff. | Folge der Anwender-Entscheidung zu v1. | entschieden |
| 10 | Bitbucket-Repositories werden serverseitig nicht aggregiert — die GitHub-Sektionen decken nur GitHub-Repositories ab. | Server-Client wird bewusst minimal gehalten; Bitbucket-Aggregation ist spätere Erweiterung. | bestätigt |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Server-Technologie | ASP.NET Core (`Microsoft.NET.Sdk.Web`, `net11.0`) mit SignalR-Hub | .NET-nativ wie der Rest des Repos; SignalR liefert Auto-Reconnect, Presence (OnConnected/OnDisconnected) und geteilte DTOs gratis; keine Zusatzpakete serverseitig nötig (in `Microsoft.AspNetCore.App` enthalten). |
| Ablageort Server | Neuer `/Server/`-Solution-Folder in `Softwareschmiede.slnx` mit `server/Softwareschmiede.StatusServer` und `server/Softwareschmiede.StatusServer.Tests` | Die `slnx` ist bereits Einheit für `dotnet build`, `dotnet format` und den Security-Scan in CI — der Server-Code wird ohne Workflow-Umbau mitgebaut und format-geprüft. Eine eigene Deploy-Einheit entsteht trotzdem (eigenes Projekt, eigenes Publish-Artefakt); ein separates Repo würde die geteilten Vertrags-DTOs erschweren. |
| Geteilte Protokoll-DTOs | Eigenes Contract-Projekt `src/Softwareschmiede.StatusContracts` (net11.0, abhängigkeitsfrei), referenziert von `Softwareschmiede` (App-Core) und `Softwareschmiede.StatusServer` | Beide Seiten serialisieren dieselben Typen; Ablage unter `src/` folgt der `Softwareschmiede.Plugin.Contracts`-Konvention. |
| Relay-Protokoll | SignalR-Hub `StatusRelayHub` unter `/relay`; App → Server: `RegisterInstance` (Instanz-ID, Protokollversion, Instanzname) und `PushSnapshot` (`StatusSnapshot`); Server → App: keine Methoden. Protokollversion als Konstante im Contract-Projekt, im Handshake geprüft. | Presence über Hub-Lebenszyklus gratis; Hub-Methoden in beide Richtungen bleiben für spätere Erweiterung offen, ohne sie jetzt zu nutzen. |
| App-seitiger Transport | `Microsoft.AspNetCore.SignalR.Client` 11.0.0 in `src/Softwareschmiede/Softwareschmiede.csproj`, gekapselt hinter `RemoteStatusRelayClient` (Wrapper um `HubConnection`) | `WithAutomaticReconnect` liefert Reconnect/Backoff in-box; der Wrapper macht den Client in Tests ersetzbar und hält die SignalR-Abhängigkeit an einer Stelle. |
| Token-Transport | `AccessTokenProvider` des SignalR-Clients (bei WebSocket-Transport `access_token`-Query-Parameter, sonst Bearer-Header); Server validiert in einer Hub-Pipeline-Prüfung gegen konfigurierte Token-Liste | Standardmechanismus; über `wss://` ist der Query-Parameter transportverschlüsselt. Lokal (`ws://`/`http://`) akzeptabel, da localhost. |
| Seiten-Aktualisierung | Server-gerendertes HTML (Minimal-API-Endpunkt + HTML-Composer, kein Frontend-Framework) mit `<meta http-equiv="refresh">` (15 s) plus `GET /status.json` für maschinelle Abfrage | Read-only-Übersicht mit ~Minuten-Aktualitätsbedarf — Polling ist einfacher und robuster als ein zweiter SignalR-Kanal zum Browser. |
| Seiten-Zugriffsschutz | Einfache, konfigurierbare Basic-Auth-Middleware serverseitig (`Page:Username`/`Page:Password` in `appsettings`/Env; deaktiviert = lokaler Dev-Modus) | Pflicht vor Livegang, minimal-invasiv; IP-Allowlist/Tailscale kann produktiv zusätzlich auf nginx-Ebene ergänzt werden (Doku). |
| Kommentar-Aktionen | **Entfallen in v1** (Anwender-Entscheidung: Seite rein lesend). Vorgemerkt für die spätere Erweiterung: Server postet mit Owner-PAT (`ScmEventValidationService` akzeptiert Akteur = Repository-Owner ohne App-Änderung), Vorlagentext mit eigenem Marker `<!-- sws-statusseite:{id} -->` — bewusst **kein** `softwareschmiede:`-Präfix, sonst „Eigene Antwort"-Verwerfung. | Anwender-Vorgabe; der Aufzeichnungspfad bleibt dokumentiert, damit die Erweiterung ohne Recherche nachgezogen werden kann. |
| GitHub-Verlinkung | Alle referenzierten Objekte werden als HTML-Links gerendert: Issue-/PR-Nummern und Titel → `html_url` des Objekts, Releases → Release-URL, SCM-Ereignis-Referenzen → verlinktes Issue/PR soweit bekannt. | Anwender-Vorgabe („Links zu GitHub dürften überall sinnvoll sein"); GitHub-REST liefert `html_url` mit — kein URL-Bauen nötig. |
| Responsive Darstellung | Fluid-Layout ohne feste Inhaltsbreiten; unter ~600 px Viewport umbrechende Darstellung (komprimierte Spalten, Zeilenumbruch in Zellen, kein horizontales Überragen). | Anwender-Feedback zum Entwurf: Inhalt ragte in der mobilen Ansicht über den rechten Rand — feste Spaltenbreiten/nowrap vermeiden, `overflow-wrap` und schmale Paddings einsetzen. |
| Relay-Dienst-Typ | `RemoteStatusRelayService : BackgroundService` (Singleton + `AddHostedService`), nicht `PeriodicBackgroundService` | Der Dienst verwaltet eine persistente Verbindung mit eigenem Zustandsautomaten (deaktiviert/konfigurationsfehler/verbindend/verbunden/backoff), kein einfaches Poll-Intervall; `TimeProvider` wird trotzdem injiziert (Testbarkeit von Backoff/Throttle). `ScmEventStartupGate.WaitAsync` vor erstem Connect (Migration muss gelaufen sein). |
| Snapshot-Auslöser | Periodischer Push (Default 60 s) + gedrosselte Zusatz-Pushes auf `WorkItemRunDataChangedNotifier.RunDataChanged` und `ScmEventChangeNotifier.Changed` (Mindestabstand 10 s) | Die Change-Signale existieren bereits als Singleton-Events; Drossel nötig, weil `RunDataChanged` je Aufgabe im 30-s-Heartbeat-Takt feuert. |
| Veraltungs-Schwelle Server | Primär Signal: SignalR-Verbindung der Instanz getrennt → sofort „offline seit `lastSeenUtc`". Sekundär: verbunden, aber letzter Snapshot älter als `App:StaleAfterSeconds` (Default 300 s = `WorkItemRecoveryService.HeartbeatTimeoutMinutes`) → „veraltet"-Markierung. | Presence ist das ehrlichste Signal; die Zeit-Schwelle fängt halb-tote Verbindungen ab; 5 min orientiert sich an der bestehenden Heartbeat-Regel. |
| Sektion „Wartet auf Anwender" | Projektiert werden: `WorkItem`s mit Live-`RunStatus == WaitingForInput` (via `IRunningAutomationStatusSource.GetLiveRunStatus`, Fallback persistierter `RunStatus`), wartende `ScmIncomingEvent`s (`Status` nicht in `ScmIncomingEventStatus.NonBlocking`) inkl. `Reason`, sowie pausierte Aufgaben (`PausedUntilUtc` in Zukunft, als „pausiert" markiert). | Deckt die in der Anforderung beispielhaft genannten Wartezustände mit vorhandenen Daten ab, ohne neue Wartezustands-Modellierung. |
| Multi-Instanz-Anzeige | Snapshot-Cache keyed by Instanz-ID; die Seite rendert je sendender Instanz einen eigenen App-Sektions-Block mit Instanzname/-ID. | Anforderung („falls mehrere Installationen existieren") ohne UI-Auswahl-Komplexität. |
| Server-Persistenz | Reiner In-Memory-Cache (`ConcurrentDictionary` Instanz-ID → letzter Snapshot + `lastSeenUtc`; GitHub-Ergebnisse mit ETag/Abrufzeit). | Read-only-Anzeige; Server-Neustart führt ehrlich zu „keine Daten" statt falschem Frische-Zustand — keine DB nötig. |
| GitHub-Zugriff Server | `HttpClient` via `IHttpClientFactory` mit ETag-/TTL-Cache (Default 60 s) und Fehler-Zeitstempel je Endpunkt; Token optional (öffentliche Repos lesbar). | Rate-Limit-Schonung; Fehler werden sichtbar statt still veraltet. |

## Programmabläufe

### App-Start und Verbindungsaufbau

1. `App.xaml.cs` `StartupAsync` führt `MigrateAsync` aus und ruft danach `ScmEventStartupGate.Ready()` (bestehend).
2. `RemoteStatusRelayService.ExecuteAsync` wartet auf `ScmEventStartupGate.WaitAsync`, liest über `IServiceScopeFactory` + `AppSettingService` die `status.remote.*`-Einstellungen.
3. Deaktiviert (`status.remote.enabled != true`) → Dienst bleibt im Zustand `Disabled` und pollt die Einstellung in großem Intervall (60 s) erneut, damit ein nachträgliches Aktivieren ohne Neustart wirkt (Muster: `ScmEventMonitoringService.RunOnceAsync` prüft `GetScmMonitoringEnabledAsync` pro Durchlauf).
4. Aktiviert, aber URL ungültig oder Token (`ICredentialStore`, Target `Softwareschmiede:RemoteStatus:RelayToken`) fehlt → Zustand `ConfigurationError` mit verständlicher Meldung in `RemoteStatusConnectionInfo` (sichtbar in Einstellungen) + Serilog-Eintrag; **kein** Verbindungsversuch, erneute Prüfung nach Einstellungsänderung/Intervall.
5. Gültig → Instanz-ID via `GetOrCreateRemoteStatusInstanceIdAsync` sicherstellen, `RemoteStatusRelayClient.ConnectAsync` mit `WithAutomaticReconnect`; nach Connect `RegisterInstance` (Instanz-ID, `RelayProtocolVersion`, Instanzname), dann initialer Snapshot-Push.
6. Fehlschlag → Zustand `Reconnecting`/Fehler, exponentieller Backoff via `TimeProvider` (z. B. 5 s → max 5 min), Fehler im Status sichtbar.

Beteiligte Klassen/Komponenten: `RemoteStatusRelayService`, `RemoteStatusRelayClient`, `RemoteStatusConnectionInfo`, `AppSettingService`, `ICredentialStore`, `ScmEventStartupGate`, `IServiceScopeFactory`, `TimeProvider`.

### Periodischer und getriggerter Snapshot-Push

1. Solange `Connected`: `RemoteStatusRelayService` projiziert alle `SnapshotIntervalSeconds` (Default 60) via `StatusSnapshotProjector.CreateAsync` und ruft `RemoteStatusRelayClient.PushSnapshotAsync`.
2. `StatusSnapshotProjector` öffnet einen `IServiceScopeFactory`-Scope und liest `AsNoTracking`: aktive `WorkItem`s (inkl. `IssueReference`, `PullRequests`, `GitRepository`-Name), wartende/verzögerte `ScmIncomingEvent`s (Top-N, z. B. 20), plus `IRunningAutomationStatusSource.GetLiveRunStatus` je Aufgabe, `IRunningAutomationsCount.CountAsync`, `IApplicationVersionProvider.GetInstalledVersionAsync`.
3. Change-Signale `WorkItemRunDataChangedNotifier.RunDataChanged`/`ScmEventChangeNotifier.Changed` setzen ein „Dirty"-Flag; der Dienst pusht frühestens nach Mindestabstand (10 s) und spätestens beim nächsten Intervall.
4. `RemoteStatusConnectionInfo` aktualisiert `LastSnapshotUtc`/`LastError` je Push-Ergebnis.

Beteiligte Klassen/Komponenten: `RemoteStatusRelayService`, `StatusSnapshotProjector`, `RemoteStatusRelayClient`, `WorkItemRunDataChangedNotifier`, `ScmEventChangeNotifier`, `IRunningAutomationStatusSource`, `IRunningAutomationsCount`, `IApplicationVersionProvider`, `RemoteStatusConnectionInfo`.

### Reconnect- und Offline-Behandlung

1. `HubConnection.Closed`/`Reconnecting` → `RemoteStatusConnectionInfo.State` wechselt; `WithAutomaticReconnect` versucht Wiederverbindung (Backoff-Sequenz des Clients); der Dienst protokolliert und setzt nach erschöpften Reconnects eigene Retry-Schleife fort.
2. Serverseitig: `StatusRelayHub.OnDisconnectedAsync` → `InstanceSnapshotCache.MarkDisconnected(instanceId)`; Seite zeigt App-Sektionen als „offline seit `lastSeenUtc`".
3. Bei Reconnect → erneutes `RegisterInstance` + sofortiger Snapshot-Push → Seite wieder live.

Beteiligte Klassen/Komponenten: `RemoteStatusRelayClient`, `RemoteStatusRelayService`, `StatusRelayHub`, `InstanceSnapshotCache`, `RemoteStatusConnectionInfo`.

### Seitenaufruf und Rendering

1. `GET /` → (optional) Basic-Auth-Middleware → `StatusPageComposer.Render`: Header mit Gesamt-Badge (online/offline/veraltet), je Instanz App-Sektionen („Laufende Aufgaben", „Wartet auf Anwender", „Letzte SCM-Ereignisse/Diagnosen") aus `InstanceSnapshotCache` inkl. Quellen-/Zeitstempel-Markierung; danach GitHub-Sektionen („Offene Pull Requests", „Letzte Releases/Versionen") aus `GitHubStatusService`-Cache inkl. Abrufzeit/Fehler.
2. `GET /status.json` liefert dieselbe Sicht als JSON (für lokale Verifikation/Debugging).
3. `GitHubStatusService` holt pro konfiguriertem Repository `pulls?state=open`, `releases?per_page=5`, `issues?state=open&per_page=10` mit ETag/304-Handling; bei HTTP-Fehler/Timeout → Eintrag mit `FetchedUtc` + `Error`.

Beteiligte Klassen/Komponenten: `StatusPageComposer`, `InstanceSnapshotCache`, `GitHubStatusService`, Basic-Auth-Middleware.

### Kommentar-Aktion (Steuerpfad) — entfällt in v1

Anwender-Entscheidung 2026-11-25: Die Seite ist rein lesend, es werden keine Aktionsbuttons und kein `POST /actions/comment` umgesetzt. Vorgemerkter Erweiterungspfad: allowlisteter Aktions-Button → `POST /actions/comment` → `CommentActionService` (Repo- + Aktionstyp- + Ziel-Allowlist) → GitHub-Comment-POST mit Vorlagentext + `<!-- sws-statusseite:{id} -->` unter Owner-PAT → App-seitig unverändert über `ScmEventMonitoringService`/`ScmEventStore`/`ScmEventValidationService`.

### Instanz-ID-Erzeugung und -Persistenz

1. `AppSettingService.GetOrCreateRemoteStatusInstanceIdAsync`: liest `status.remote.instanceid`; fehlend/leer → `Guid.NewGuid().ToString("N")` speichern und zurückgeben; vorhanden → wiederverwenden.
2. Einstellungs-UI zeigt die ID + optionalen `status.remote.instancename` an; der Server ordnet Verbindung und Cache darüber zu.

Beteiligte Klassen/Komponenten: `AppSettingService`, `RemoteStatusRelayService`, `SettingsViewModel`, `StatusRelayHub`, `InstanceSnapshotCache`.

## Neue Klassen

### App-seitig (`src/Softwareschmiede/`)

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RemoteStatusRelayService` | Klasse (`BackgroundService`, `Application/Services/`) | Hält ausgehende Relay-Verbindung, orchestriert Connect/Backoff/Register/Snapshot-Push, prüft Aktivierungs-Schalter. |
| `RemoteStatusRelayClient` | Klasse (`Application/Services/` oder `Infrastructure/Services/`) | Dünner Wrapper um `HubConnection` (Connect/Register/Push/Closed-Events); austauschbar für Tests; injizierbarer `HttpConnectionOptions`-Hook (Message-Handler-/WebSocket-Factory/Transportwahl), damit der produktive Client im Integrationstest gegen den in-memory `TestServer` fährt. |
| `RemoteStatusConnectionInfo` | Klasse (Singleton, `Application/Services/`) | Prozessweiter Verbindungszustand (`Disabled`/`ConfigurationError`/`Connecting`/`Connected`/`Reconnecting`/`Error`, `LastError`, `LastSnapshotUtc`, `StateChanged`-Event) für UI und Diagnose. |
| `RemoteStatusConnectionState` | Enum | Zustandswerte der `RemoteStatusConnectionInfo`. |
| `StatusSnapshotProjector` | Klasse (`Application/Services/`, per `IServiceScopeFactory` genutzt) | Read-only-Projektion DB-Bestände + Laufzeit-Singletons → `StatusSnapshot`. |
| `RemoteStatusUrlParser` (o. ä.) | Klasse (static, `Application/Services/`) | Normalisiert `http(s)`/`ws(s)`-Eingaben zu Hub-URL + Validierungsfehler. |

### Geteilte Verträge (`src/Softwareschmiede.StatusContracts/`)

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RelayProtocolVersion` | Klasse (const) | Protokollversion (`Current = 1`), im Handshake geprüft. |
| `RegisterInstanceRequest` / `RegisterInstanceResult` | Datenmodellklasse | Handshake: Instanz-ID, Instanzname, Protokollversion; Ergebnis inkl. Ablehnungsgrund. |
| `StatusSnapshot` | Datenmodellklasse | Gesamt-Snapshot: `InstanceId`, `InstanceName`, `CreatedUtc`, `ProtocolVersion`, `AppVersion`, `RunningAutomationsCount`, `WorkItems`, `ScmEvents`. |
| `StatusSnapshotWorkItem` | Datenmodellklasse | Aufgabe: Titel, `Status`, `RunStatus` (live), `Kind`, pausiert-bis, letzter Heartbeat, Issue-/PR-Verweis (Nummer/URL), `Purpose`. |
| `StatusSnapshotScmEvent` | Datenmodellklasse | SCM-Ereignis: `Status`, `Reason`, `ReceivedUtc`, Referenzanzeige, `Url` (aus `ScmEventObservation.Url` — Linkziel für die Seite). |

### Server (`server/Softwareschmiede.StatusServer/`)

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `Program` | Klasse (`public partial`, Top-Level) | Host-Aufbau: SignalR, `IHttpClientFactory`, Options, Endpunkte `/`, `/status.json`, `/relay`. |
| `StatusRelayHub` | Klasse (`Hub`) | `RegisterInstance`, `PushSnapshot`, `OnConnectedAsync`/`OnDisconnectedAsync` → Presence in `InstanceSnapshotCache`. |
| `RelayTokenValidator` | Klasse | Prüft `access_token`-/Bearer-Token gegen `Relay:Tokens` (Liste oder Token→Instanz-Zuordnung); Ablehnung schließt Verbindung. |
| `InstanceSnapshotCache` | Klasse (Singleton) | Je Instanz-ID: verbunden-Flag, letzter `StatusSnapshot`, `lastSeenUtc`, `RegisteredUtc`; `IsStale`-Bewertung. |
| `GitHubStatusService` | Klasse (Singleton, `IHttpClientFactory`) | GitHub REST (PRs, Releases, Issues) mit ETag-/TTL-Cache und Fehler-Zeitstempel je Repository. |
| `GitHubRepositoryOptions` / `StatusServerOptions` | Options-Klassen | `GitHub:Repositories[]`, `GitHub:Token`, `GitHub:CacheTtlSeconds`, `App:StaleAfterSeconds`, `Relay:Tokens`, `Page:*`. |
| `StatusPageComposer` | Klasse | Rendert HTML-Seite (Sektionen, Quellen-/Zeitstempel-Badges, Offline-/Stale-Markierung, GitHub-Links, responsives CSS). |
| `PageAccessMiddleware` (Basic Auth) | Klasse | Optionaler Zugriffsschutz der Seiten-Endpunkte (nicht `/relay`). |

### Tests

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `Softwareschmiede.StatusServer.Tests` | Testprojekt (`server/`, xUnit v3) | Server-Unit-/Integrations-Tests inkl. `WebApplicationFactory`-Roundtrip. |

## Änderungen an bestehenden Klassen

### `AppSettingService` (`src/Softwareschmiede/Application/Services/AppSettingService.cs`)

- **Neue Schlüssel-Konstanten:** `RemoteStatusEnabledKey = "status.remote.enabled"`, `RemoteStatusUrlKey = "status.remote.url"`, `RemoteStatusInstanceIdKey = "status.remote.instanceid"`, `RemoteStatusInstanceNameKey = "status.remote.instancename"`, `RemoteStatusSnapshotIntervalSecondsKey = "status.remote.snapshotinterval.seconds"`.
- **Neue Methoden:** `GetRemoteStatusEnabledAsync`/`SetRemoteStatusEnabledAsync` (Default `false`), `GetRemoteStatusUrlAsync`/`SetRemoteStatusUrlAsync`, `GetRemoteStatusInstanceNameAsync`/`SetRemoteStatusInstanceNameAsync`, `GetRemoteStatusSnapshotIntervalAsync`/`SetRemoteStatusSnapshotIntervalAsync` (Default 60, Klemmung 15–3600), `GetOrCreateRemoteStatusInstanceIdAsync` (Erzeugen + persistieren bei Fehlen).

### `App.xaml.cs` (`src/Softwareschmiede.App/`)

- **DI-Registrierung** im Hosted-Service-Block (~Z. 319–326): `AddSingleton<RemoteStatusConnectionInfo>()`, `AddSingleton<RemoteStatusRelayClient>()`, `AddSingleton<StatusSnapshotProjector>()`, `AddSingleton<RemoteStatusRelayService>()` + `AddHostedService(sp => sp.GetRequiredService<RemoteStatusRelayService>())`.

### `Softwareschmiede.csproj` (`src/Softwareschmiede/`)

- **Neue `PackageReference`:** `Microsoft.AspNetCore.SignalR.Client` Version `11.0.0`.
- **Neue `ProjectReference`:** `..\Softwareschmiede.StatusContracts\Softwareschmiede.StatusContracts.csproj`.

### `Softwareschmiede.slnx`

- **Neuer Solution-Folder `/Server/`** mit `server/Softwareschmiede.StatusServer/Softwareschmiede.StatusServer.csproj`, `server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj`; `src/Softwareschmiede.StatusContracts/Softwareschmiede.StatusContracts.csproj` ebenfalls unter `/Server/` (oder Root-Liste) eintragen.

### `SettingsViewModel` (`src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`)

- **Neue Eigenschaften:** `IsRemoteStatusEnabled`, `RemoteStatusUrl`, `RemoteStatusInstanceName`, `RemoteStatusInstanceId` (read-only Anzeige), `RemoteStatusToken` (write-only Eingabe → `ICredentialStore`), `RemoteStatusConnectionText` (aus `RemoteStatusConnectionInfo`, z. B. „verbunden", „Fehler: …"), Sichtbarkeits-Flags `IsRemoteStatusFieldVisible` analog `IsTriggerKeywordVisible`.
- **Geänderte Methoden:** `LoadAsync`-Pfad liest neue Werte (~Z. 608 ff); `SaveAsync` persistiert neue Schlüssel + Token in `ICredentialStore` (~Z. 735 ff) inkl. `SetCredential`-Fehlerbehandlung (`InvalidOperationException` → verständliche Meldung) und `IsNullOrEmpty`-Token-Prüfung.
- **Neue Event-Handler:** Abonnement `RemoteStatusConnectionInfo.StateChanged` → `RemoteStatusConnectionText` aktualisieren (UI-Thread-Marshalling wie bei bestehenden Status-Texten).

### `SettingsView.xaml` (`src/Softwareschmiede.App/Views/`)

- **Neuer Abschnitt „Remote-Übersicht"** im Tab „Automatisierung" (unterhalb SCM-Ereignisüberwachung): CheckBox `IsRemoteStatusEnabled`, TextBox `RemoteStatusUrl`, TextBox `RemoteStatusInstanceName`, PasswordBox/Token-Eingabe, read-only Instanz-ID und Verbindungsstatus-TextBlock — jeweils mit `AutomationProperties.Name` nach bestehender Konvention (vgl. `IsScmMonitoringEnabled`, ~Z. 375).

### CI-Workflows (`.github/workflows/`)

- `pr-staging-ci.yml`, `staging-ci.yml` (und konsistent `release.yml`): zusätzlicher `dotnet test`-Schritt für `server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj` im regulären Testabschnitt.

## Datenbankmigrationen

Keine — die Einstellungen laufen über die generische `AppSetting`-Tabelle; der Snapshot ist reine Projektion.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `status.remote.url` | Bei aktivierter Remote-Übersicht Pflicht; muss `http(s)`- oder `ws(s)`-URI mit Host sein; `ws(s)` wird clientseitig zu `http(s)` normalisiert. | Ungültig/fehlend → Dienst-Zustand `ConfigurationError` mit Klartext-Meldung in `RemoteStatusConnectionInfo`/Einstellungen; Speichern der Einstellungen zeigt Feldvalidierung (Muster: bestehende Settings-Validierung). |
| Relay-Token | Bei aktivierter Remote-Übersicht Pflicht (`ICredentialStore`-Eintrag nicht leer). | Fehlend → `ConfigurationError` „Token fehlt"; serverseitige Ablehnung (401/Hub-Close) → Zustand `Error` mit Hinweis statt Endlos-Reconnect ohne Diagnose. |
| `status.remote.instanceid` | Wird nur systemseitig erzeugt (`GetOrCreate…`); nie manuell leerbar — leerer gespeicherter Wert wird als „fehlt" behandelt und neu erzeugt. | — |
| `status.remote.snapshotinterval.seconds` | Ganzzahl, geklemmt auf 15–3600, Default 60. | Ungültiger Wert → Default (Klemmungsmuster wie `GetScmTaskCompletionWaitMinutesAsync`). |
| `RegisterInstanceRequest` | `ProtocolVersion` muss serverseitig bekannt sein (gleiche Major-Version); `InstanceId` nicht leer. | Ablehnung mit `RegisterInstanceResult.Rejected` + Grund → App-Zustand `Error` „Protokollversion inkompatibel" (kein Reconnect-Sturm: begrenzte Versuche, dann Backoff). |
| Snapshot-Payload | Keine Geheimnisse/Tokens; nur Anzeigefelder (Titel, Stati, URLs, Zeitstempel). | — (statisch durch DTO-Design). |

## Konfigurationsänderungen

### App (`AppSettingService`-Schlüssel / `ICredentialStore`)

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `status.remote.enabled` | bool | `false` | Opt-in-Schalter Remote-Übersicht (Laufzeit-wirksam). |
| `status.remote.url` | string | `null` | Relay-Basis-URL (lokal `http://localhost:5055`, produktiv `https://status.<domain>`; Hub-Pfad `/relay` wird angehängt). |
| `status.remote.instanceid` | string | auto (GUID `N`-Format) | Persistente Instanz-ID. |
| `status.remote.instancename` | string | `null` | Optionaler Anzeigename auf der Seite. |
| `status.remote.snapshotinterval.seconds` | int | `60` (15–3600) | Snapshot-Push-Intervall. |
| `ICredentialStore`-Target `Softwareschmiede:RemoteStatus:RelayToken` | string | — | Relay-Token (Geheimnis; über Einstellungs-UI setz-/löschbar → manuelle Rotation ohne Update). |

### Server (`appsettings.json` / Environment)

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Relay:Tokens` | Liste/Map | leer (= alles ablehnen außer Dev-Modus `ASPNETCORE_ENVIRONMENT=Development` mit `Relay:DevToken`) | Gültige Relay-Tokens, optional Token→Instanz-Vorbelegung. |
| `App:StaleAfterSeconds` | int | `300` | Sekundäre Veraltungs-Schwelle bei bestehender Verbindung. |
| `GitHub:Repositories` | `owner/repo[]` | leer | Zu aggregierende Repositories. |
| `GitHub:Token` | string | `null` | Serverseitiges Token, nur Read-Scope (v1 lesend; optional — öffentliche Repos auch ohne Token, mit geringerem Rate-Limit). |
| `GitHub:CacheTtlSeconds` | int | `60` | Mindestabstand GitHub-Abrufe je Endpunkt (ETag/304 zusätzlich). |
| `Page:Username` / `Page:Password` | string | leer = Schutz aus (lokaler Dev) | Basic-Auth der Seite; produktiv Pflicht. |
| `Page:RefreshSeconds` | int | `15` | Meta-Refresh der Seite. |
| `ASPNETCORE_URLS` | string | `http://localhost:5055` (launchSettings) | Lokaler Port. |

## Seiteneffekte und Risiken

- **NuGet-Abhängigkeit:** `Microsoft.AspNetCore.SignalR.Client` in `src/Softwareschmiede` zieht `Microsoft.AspNetCore.*`-Client-Pakete in die Core-Library (und damit in `Softwareschmiede.App` + beide Testprojekte); die wöchentliche Paketprüfung (`review-nuget-packages.yml`, nur Issue-erzeugend, keine Freigabeliste) erfasst sie automatisch — kein Workflow-Eingriff nötig.
- **CI-Build/Format:** Server-Projekte in der `slnx` werden von `dotnet build … -p:TreatWarningsAsErrors=true` und `dotnet format … --verify-no-changes` erfasst — Server-Code muss denselben Konventionen (XML-Docs, `CS1591` wo gesetzt, Formatierung) genügen.
- **Einstellungs-UI:** neuer Abschnitt im Automatisierung-Tab; bestehende `E2E_SettingsFeatureFlags`/`SettingsViewModelTests` könnten Feld-/Zähler-Assertions enthalten — betroffene Assertions prüfen.
- **Change-Signal-Abonnement:** `RemoteStatusRelayService` hält als Singleton Abonnements auf `WorkItemRunDataChangedNotifier`/`ScmEventChangeNotifier` — Handler dürfen nicht blockieren (nur Flag setzen).
- **Token im Query-String:** bei `ws://` (localhost-Dev) läge das Token unverschlüsselt — akzeptabel lokal; produktiv nur `wss://` dokumentieren und in der UI warnend darstellen, wenn `ws://` außerhalb localhost konfiguriert wird.
- **GitHub-Rate-Limits:** ohne Token 60 req/h — bei mehreren Repos/Endpunkten schnell erschöpft; ETag-Cache + TTL mindern, Restrisiko wird sichtbar gemeldet.
- **Zweiter `HostedService` mit Startup-Gate:** muss `ScmEventStartupGate.WaitAsync` wie die SCM-Dienste respektieren, sonst EF-Zugriff vor Migration.
- **Proxy/NAT:** die ausgehende `wss://`-Verbindung nutzt per Default-`HttpMessageHandler` den Systemproxy; ein authentifizierter Unternehmensproxy ist nicht abgedeckt — bekannte Einschränkung, in `server/README.md` dokumentieren.
- **CredentialStore-Fehlerpfad:** `WindowsCredentialStore.SetCredential` wirft `InvalidOperationException` bei `CredWrite`-Fehler — `SettingsViewModel.SaveAsync` fängt das ab und meldet verständlich statt ungeprüfter Exception; `GetCredential` liefert `string.Empty` für leere Einträge → Token-Pflichtprüfung mit `string.IsNullOrEmpty`.
- **Selbsthosting:** kein Eingriff in laufende `Softwareschmiede.App.exe`; Server ist eigenständiger Prozess.

## Umsetzungsreihenfolge

1. **Contract-Projekt `src/Softwareschmiede.StatusContracts` anlegen** (net11.0, XML-Docs, `CS1591`-Konvention) + Eintrag in `Softwareschmiede.slnx`
   - Voraussetzungen: Keine.
   - Beschreibung: `RelayProtocolVersion`, `RegisterInstanceRequest`/`Result`, `StatusSnapshot`, `StatusSnapshotWorkItem`, `StatusSnapshotScmEvent` mit Protokollversion-Feld.

2. **Server-Gerüst `server/Softwareschmiede.StatusServer`** (`Microsoft.NET.Sdk.Web`, net11.0) + slnx-Eintrag + `appsettings.json`/`launchSettings.json` (localhost-Port)
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `Program.cs` mit SignalR-Registrierung, Options-Bindung, `IHttpClientFactory`, Health-Endpunkt; noch ohne Fachlogik.

3. **Relay-Endpunkt: `RelayTokenValidator`, `StatusRelayHub`, `InstanceSnapshotCache`**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Token-Prüfung am Hub, `RegisterInstance`/`PushSnapshot`/`OnDisconnectedAsync`, In-Memory-Cache mit `lastSeenUtc`/Stale-Regel.

4. **App-Client-Grundlage: NuGet + Projektreferenz**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `Microsoft.AspNetCore.SignalR.Client` 11.0.0 und `..\Softwareschmiede.StatusContracts`-Referenz in `Softwareschmiede.csproj`.

5. **App-Konfiguration: `AppSettingService`-Schlüssel + Instanz-ID**
   - Voraussetzungen: Keine.
   - Beschreibung: Schlüssel-Konstanten, typisierte Get/Set-Paare, `GetOrCreateRemoteStatusInstanceIdAsync`.

6. **`StatusSnapshotProjector`**
   - Voraussetzungen: Schritt 1, 5.
   - Beschreibung: Read-only-Projektion über `IServiceScopeFactory`-Scope (`AsNoTracking`) + `IRunningAutomationStatusSource`, `IRunningAutomationsCount`, `IApplicationVersionProvider`.

7. **`RemoteStatusRelayClient` + `RemoteStatusConnectionInfo`**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: `HubConnection`-Wrapper (`WithUrl`, `AccessTokenProvider`, `WithAutomaticReconnect`, `RegisterInstance`/`PushSnapshot`, `Closed`/`Reconnecting`-Events) und Singleton-Zustandsobjekt.

8. **`RemoteStatusRelayService` + DI-Registrierung**
   - Voraussetzungen: Schritt 5, 6, 7.
   - Beschreibung: `BackgroundService` mit `ScmEventStartupGate.WaitAsync`, Schalter-Prüfung, Konfigurationsfehler-Pfad, Backoff via `TimeProvider`, Change-Signal-Drossel; Registrierung in `App.xaml.cs`.

9. **GitHub-Aggregation: `GitHubStatusService` + Options**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `IHttpClientFactory`-Client mit ETag-/TTL-Cache, Fehler-Zeitstempel, Endpunkte pulls/releases/issues je konfiguriertem Repo.

10. **Status-Seite: `StatusPageComposer`, `GET /`, `GET /status.json`, `PageAccessMiddleware`**
    - Voraussetzungen: Schritt 3, 9.
    - Beschreibung: HTML-Rendering aller Sektionen mit Quellen-/Zeitstempel-Badges, Offline-/„keine Daten"-Zustände, Meta-Refresh, optionaler Basic-Auth.

11. **~~Kommentar-Aktionen~~ — entfällt in v1**
    - Anwender-Entscheidung 2026-11-25: Seite rein lesend; `CommentActionService`, `POST /actions/comment` und Seiten-Buttons werden nicht umgesetzt (Erweiterungspfad siehe Programmabläufe).

12. **Einstellungs-UI: Abschnitt „Remote-Übersicht"**
    - Voraussetzungen: Schritt 5, 8.
    - Beschreibung: `SettingsViewModel`-Eigenschaften + `SettingsView.xaml`-Abschnitt im Automatisierung-Tab inkl. `AutomationProperties.Name`, Token-Eingabe → `ICredentialStore`, Verbindungsstatus-Anzeige aus `RemoteStatusConnectionInfo`; Blockade-Erklärung (Felder deaktiviert + Hinweistext) bei ungültiger/unvollständiger Konfiguration.

13. **Server-Testprojekt + CI-Testschritt**
    - Voraussetzungen: Schritt 3, 10.
    - Beschreibung: `server/Softwareschmiede.StatusServer.Tests` (xUnit v3 4.0.3, `xunit.runner.visualstudio` 4.0.1, `coverlet.collector` 10.1.1, `FluentAssertions` 8.11.0, `Microsoft.AspNetCore.Mvc.Testing` 11.x, `Microsoft.AspNetCore.SignalR.Client` 11.0.0, `IsTestingPlatformApplication=false`, `GenerateDocumentationFile` + CS1591-Konvention — Vorlage `Softwareschmiede.IntegrationTests.csproj`) in `slnx`; `dotnet build`- + `dotnet test --no-build`-Schritte an denselben Stellen wie die bestehenden Testprojekte in `pr-staging-ci.yml`, `staging-ci.yml`, `release.yml` ergänzen; `Program` als `public partial` exposen. WebApplicationFactory nutzt in-memory `TestServer` (kein TCP-Port): die echte `HubConnection` fährt über `HttpConnectionOptions.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()` plus `WebSocketFactory` (`factory.Server.CreateWebSocketClient()`) oder `HttpTransportType.LongPolling`.

14. **App-seitige Unit-/Integrations-Tests**
    - Voraussetzungen: Schritt 5–8, 12.
    - Beschreibung: `AppSettingServiceTests_RemoteStatus`, `StatusSnapshotProjectorTests`, `RemoteStatusRelayServiceTests`, `SettingsViewModel`-Tests; Integrations-Test app↔server in `Softwareschmiede.IntegrationTests` (echte SignalR-Verbindung gegen `WebApplicationFactory` — benötigt `Microsoft.AspNetCore.Mvc.Testing` in IntegrationTests.csproj).

15. **E2E-Test Einstellungen**
    - Voraussetzungen: Schritt 12.
    - Beschreibung: `E2E_RemoteStatusSettings` (OsInterface-Lane).

16. **Betriebs-/Nutzungsdoku**
    - Voraussetzungen: Schritt 10.
    - Beschreibung: `server/README.md` (lokaler Start, systemd-Unit-Beispiel, nginx-Location-Beispiel, Token-Einrichtung/-Rotation, Zugriffsschutz-Pflicht, Proxy-Einschränkung); Hilfeseite unter `docs/help/einstellungen/` (Lifecycle-Schritt 12).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetRemoteStatusEnabledAsync_Default_IsFalse` u. a. | `AppSettingServiceTests_RemoteStatus` (`Softwareschmiede.Tests`) | Defaults, Persistenz, Klemmung des Intervalls. |
| `GetOrCreateRemoteStatusInstanceIdAsync_*` | `AppSettingServiceTests_RemoteStatus` | Erzeugt GUID beim ersten Aufruf, persistiert und liefert danach denselben Wert; leerer Wert → Neuerzeugung. |
| `CreateAsync_*` | `StatusSnapshotProjectorTests` | Gegebene `WorkItem`-/`ScmIncomingEvent`-Bestände (InMemory/Sqlite via `TestDbContextFactory`) + gemockte Laufzeit-Singletons → korrektes `StatusSnapshot`-DTO; `WaitingForInput`-Live-Status schlägt persistierten `RunStatus`; pausierte Aufgaben markiert; keine `IssueAufbereitung`-internen Artefakte im fachlichen Sinne ungefiltert nötig (Mapping dokumentiert). |
| `RunOnce/ExecuteAsync_*` | `RemoteStatusRelayServiceTests` | Deaktiviert → kein Connect; aktiviert ohne URL/Token → `ConfigurationError`-Zustand + keine Verbindungsschleife; URL-Normalisierung (`ws`→`http`); Connect-Fehler → `Reconnecting`/Backoff via `TestTimeProvider`; Change-Signal → gedrosselter Push (Transport-Mock). |
| `RemoteStatusRelayClient_*` | `RemoteStatusRelayClientTests` | `AccessTokenProvider` gesetzt; Fehler propagiert in `RemoteStatusConnectionInfo`. |
| Abschnitt Remote-Übersicht | `SettingsViewModelTests` (Erweiterung) | Laden/Speichern der neuen Felder, Token → `ICredentialStore`, Sichtbarkeits-Flags, Status-Text-Bindung. |
| `TokenValidator_*` / `Hub_*` | `Softwareschmiede.StatusServer.Tests` | Token akzeptiert/abgelehnt; `RegisterInstance` (Versions-Mismatch → Reject); `PushSnapshot` aktualisiert Cache; Disconnect → `IsStale`. |
| `InstanceSnapshotCache_*` | `Softwareschmiede.StatusServer.Tests` | `lastSeenUtc`-Fortschreibung, Stale-Schwelle, Multi-Instanz-Trennung. |
| `Page_*` | `Softwareschmiede.StatusServer.Tests` | `GET /` enthält Sektionen/Offline-Markierung/„keine Daten"-Zustand; `GET /status.json` liefert Struktur; Basic-Auth erzwingt 401/200 je Konfiguration; Link-Nachweis (Issue-/PR-/Release-Zeilen als `<a href>` zur `html_url`); mobiler Nachweis (`<meta name="viewport">`, keine festen Inhaltsbreiten/`nowrap`-Muster). |
| `GitHubStatusService_*` | `Softwareschmiede.StatusServer.Tests` | ETag-Cache (304 → kein Refresh), Fehler → `Error`+Zeitstempel im Cache (HttpMessageHandler-Mock, Muster `FakeGitHubEventApi`/`StaticHttpHandler`). |
| `RelayRoundtrip_*` | `Softwareschmiede.StatusServer.Tests` **und/oder** `RemoteStatusRelayIntegrationTests` (`Softwareschmiede.IntegrationTests`, `Microsoft.AspNetCore.Mvc.Testing`) | **Primärer lokaler Funktionsnachweis:** `WebApplicationFactory` hostet `StatusServer` (in-memory `TestServer`, kein TCP-Port); echte SignalR-`HubConnection` über `HttpConnectionOptions.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()` + `WebSocketFactory` (`factory.Server.CreateWebSocketClient()`) oder `HttpTransportType.LongPolling` — im Integrations-Test fährt der produktive `RemoteStatusRelayClient` über dessen Options-Hook → `RegisterInstance` → `PushSnapshot` → `InstanceSnapshotCache` gefüllt → `GET /` rendert App-Sektion live; Disconnect → offline-Markierung. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `SettingsViewModelTests*` | Neue ViewModel-Eigenschaften/Save-Pfad — ggf. ergänzende Assertions; bricht nur, wenn Feldzählungen/Keys exakt abgesichert sind (bei Implementierung prüfen). |
| `E2E_SettingsFeatureFlags` | Prüft Einstellungs-Schalter im Automatisierung-Tab; bei Feld-/Abschnitts-Assertions ggf. um neuen Abschnitt erweitern oder sicherstellen, dass neue IDs keine bestehenden Selektoren treffen. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|---------------------|
| Pflicht | Einstellungs-Abschnitt „Remote-Übersicht": Tab „Automatisierung" öffnen → Schalter aktivieren → URL/Instanzname/Token eintragen → speichern → Instanz-ID und ehrlicher Verbindungsstatus („nicht verbunden / Fehler …") sichtbar; deaktiviert → Felder gesperrt mit Hinweis | `src/Softwareschmiede.Tests/E2E/E2E_RemoteStatusSettings.cs` | UI-Konfigurierbarkeit + ehrliche Zustandsanzeige ohne gültiges Ziel | Der Benutzerfluss über die WPF-Einstellungs-UI (Binding, Persistenz via `SaveAsync`, `ICredentialStore`, Status-Bindung aus `RemoteStatusConnectionInfo`) ist nur per FlaUI gegen die laufende App verifizierbar. |
| Pflicht | Relay-Pfad „echt" lokal: echter `RemoteStatusRelayService`-Kern gegen lokal gehosteten `StatusServer` → Seite zeigt App-Sektionen live; nach Trennen offline-markiert | `RemoteStatusRelayIntegrationTests` (`Softwareschmiede.IntegrationTests`) bzw. `RelayRoundtrip_*` in `StatusServer.Tests` | „Seite zeigt bei verbundener Anwendung Live-Zustand" + „bei getrennter Verbindung veraltet markiert" | Unit-Tests decken Transport und Projektion getrennt ab; nur der Integrationstest weist das Zusammenspiel von DI-gesteuertem Dienst, echter SignalR-Verbindung, Server-Cache und Rendering nach — lokaler Ersatz für den (hier unmöglichen) produktiven Pfad. |

Hinweis zur Geometrie: Der neue Abschnitt liegt in einem `ScrollViewer` des Automatisierung-Tabs; wo praktikabel prüft der E2E-Test per BoundingBox, dass die neuen Elemente innerhalb des sichtbaren Containers liegen (Referenzmuster: `PostingRulesOverlay_OpensAndCloses_ViaRibbon`).

| Bestehender E2E-Test | Grund der Anpassung |
|----------------------|---------------------|
| `E2E_SettingsFeatureFlags` | Mögliche Betroffenheit durch neuen Abschnitt im selben Tab — zur Implementierungszeit prüfen und ggf. erweitern. |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | **Happy Path lokal:** Server-Dienst läuft auf `http://localhost:5055`; App mit `status.remote.enabled=true`, URL `http://localhost:5055`, gesetztem Token; `status.remote.instanceid` existiert noch nicht | App-Start (Systemereignis) | Instanz-ID fehlt; Token in `ICredentialStore`; `GitHub:Repositories` enthält mindestens ein Repo | Dienst wartet `ScmEventStartupGate`, erzeugt + persistiert Instanz-ID, verbindet per `ws`, `RegisterInstance`, initialer + periodischer Snapshot | `GET /` im Browser zeigt Instanz-Block „online", Sektionen „Laufende Aufgaben"/„Wartet auf Anwender"/„Letzte SCM-Ereignisse" mit aktuellem Zeitstempel sowie GitHub-Sektionen live; `RemoteStatusConnectionInfo`/Einstellungen zeigen „verbunden" | `RelayRoundtrip`-Integrationstest + reale Sichtprüfung/Screenshot der Seite in Desktop- **und** mobiler Breite (Abnahme; verbindlich „kein Überragen über den rechten Rand") |
| 2 | Offline-Markierung | App-Beendigung / `taskkill` des Relay-Client / Verbindungsabbruch | Zustand aus Szenario 1 (verbunden) | SignalR-Trennung → `OnDisconnectedAsync` → `MarkDisconnected` | Seite zeigt App-Sektionen grau mit „offline seit <lastSeenUtc>"; GitHub-Sektionen bleiben live mit eigenem Abruf-Zeitstempel; nach App-Neustart wieder live | `StatusServer.Tests` (Disconnect-Test) + Integrationstest + Sichtprüfung |
| 3 | „Keine Daten"-Zustand | Browser-Aufruf direkt nach Server-Start, App noch nie verbunden | `InstanceSnapshotCache` leer | `GET /` ohne registrierte Instanz | Seite zeigt ehrlich „keine App-Daten / noch nie verbunden" statt falschem „alles läuft"; GitHub-Sektionen live | `Page_*`-Test + Sichtprüfung |
| 4 | Konfigurationsfehler ehrlich | Aktivierung ohne URL oder ohne Token | `status.remote.enabled=true`, URL/Token fehlen | Dienst-Start | Zustand `ConfigurationError` mit Klartext in Einstellungen + Serilog; **kein** Verbindungsversuch, keine Exception-Flut; nach Konfigurationsergänzung verbindet der Dienst ohne App-Neustart | `RemoteStatusRelayServiceTests` + `E2E_RemoteStatusSettings` |
| 5 | GitHub-Störung sichtbar | GitHub-API-Fehler oder Rate-Limit | Konfiguriertes Repo, Token fehlt/ungültig oder API down | `GitHubStatusService`-Abruf schlägt fehl | GitHub-Sektionen zeigen letzten Stand oder „Fehler <Zeitstempel>: <Grund>" statt stiller Veraltung; App-Sektionen unbeeinflusst | `GitHubStatusService_*`-Test + Sichtprüfung mit ungültigem Token |
| 6 | ~~Kommentar-Aktion (Steuerpfad)~~ — entfällt in v1 (Anwender-Entscheidung: rein lesende Seite); Erweiterungspfad siehe Programmabläufe. | — | — | — | — | — |
| 7 | Protokollversion-Mismatch | Client mit abweichender `ProtocolVersion` verbindet | Server erwartet `RelayProtocolVersion.Current` | `RegisterInstance` → Ablehnung | Hub lehnt ab; App zeigt `Error` „Protokollversion inkompatibel" und reconnectet nicht endlos aggressiv | `Hub_*`-Test + `RemoteStatusRelayServiceTests` |

## Entscheidungen zu den offenen Punkten

Anwender-Freigabe 2026-11-25: „implementiere das bitte gemäß der Empfehlungen" — alle Empfehlungen gelten als angenommen; zusätzlich verbindlich: v1 rein lesend (keine Aktionsbuttons), GitHub-Links auf allen referenzierten Objekten, mobil darf kein Inhalt über den rechten Rand ragen.

| # | Ehemals offener Punkt | Entscheidung |
|---|-----------------------|--------------|
| 1 | Produktive Adresse | Subdomain `status.<domain>` mit nginx-`location /` → Kestrel-Upstream; WebSocket-Upgrade für `/relay` in `server/README.md` dokumentiert. |
| 2 | Zugriffsschutz | Basic Auth via `PageAccessMiddleware` (implementiert); produktiv zusätzlich IP-Allowlist auf nginx-Ebene empfohlen. |
| 3 | Kommentar-Identität | **Entfällt in v1** (keine Kommentar-Aktionen). Vorgemerkt: Owner-PAT, Marker `sws-statusseite:` — siehe Designentscheidung „Kommentar-Aktionen". |
| 4 | Repo-Aggregation | Serverseitige Liste `GitHub:Repositories`, initial `martin-stromberg/Softwareschmiede`. |
| 5 | Aktions-Allowlist | **Entfällt in v1** (keine Aktionen). |
| 6 | Token-Rotation | App-seitig über Einstellungs-Feld → `ICredentialStore` (wirkt beim nächsten Connect ohne Update); serverseitig `appsettings`/Env + Neustart. |
| 7 | Intervalle | Snapshot-Push 60 s (15–3600), Change-Push gedrosselt ≥10 s; Veraltung primär Presence, sekundär `App:StaleAfterSeconds=300`. |
| 8 | Deployment/Betrieb | `server/README.md` (dotnet publish, systemd, nginx); manueller Update-Weg, kein Auto-Update. |
| 9 | Multi-Instanz | Eigener Sektions-Block je Instanz-ID + Instanzname (Fallback gekürzte GUID). |
| 10 | „Wartet auf Anwender" | Wie geplant: `WaitingForInput` + wartende `ScmIncomingEvent`s + Pausen-Markierung. |
