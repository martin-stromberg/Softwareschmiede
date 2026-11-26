# Plan-Review: Remote-Übersicht über externe Status-Webseite (Issue #478)

Geprüft am 2026-11-26 gegen `plan.md`, `requirement.md`, `inventory.md`, `plan-check.md`
und `docs/features/task-issue-478-…-tasks.md`. Gegenstand ist ausschließlich die
Vollständigkeit der Umsetzung (Schritte 1–16, Tasks 1–44 ohne die entfallenen 17/18),
jeweils mit Code-Ankern verifiziert.

Status: Offene Aufgaben vorhanden

## Offene Aufgaben

- [ ] **Betriebs-/Nutzungsdoku fehlt komplett** (Schritt 16, Tasks 45+46): Weder
  `server/README.md` (lokaler Start, `dotnet publish`, systemd-Unit, nginx-Location inkl.
  WebSocket-Upgrade, Token-Einrichtung/-Rotation, Zugriffsschutz-Pflicht,
  Unternehmensproxy-Einschränkung) noch eine Hilfeseite zur „Remote-Übersicht" unter
  `docs/help/einstellungen/` existiert (verifiziert: `find server -name "README*"` leer;
  `docs/help/einstellungen/` enthält keinen Remote-Status-Eintrag).
- [ ] **Kein erneutes `RegisterInstance` nach SignalR-Auto-Reconnect** (Programmablauf
  „Reconnect- und Offline-Behandlung" Schritt 3: „Bei Reconnect → erneutes
  RegisterInstance + sofortiger Snapshot-Push → Seite wieder live"):
  `OnClientReconnected` in `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:309-314`
  setzt nur Zustand `Connected` + Dirty-Flag. Ein Auto-Reconnect erzeugt serverseitig eine
  neue Verbindung ohne `Context.Items[InstanceIdItemKey]` — `MarkConnected` läuft nur in
  `StatusRelayHub.RegisterInstance` (`server/Softwareschmiede.StatusServer/Relay/StatusRelayHub.cs:61-77`).
  Folge: Die Instanz bleibt serverseitig offline und jeder weitere `PushSnapshot` endet in
  der `HubException` „Instanz ist nicht registriert" (`StatusRelayHub.cs:85-86`) — der
  Reconnect-Pfad ist fachlich nicht funktionsfähig.
- [ ] **Ablehnungs-Pfad erzeugt Dauer-Push-Fehlschläge** (Validierungsregel
  `RegisterInstanceRequest`: Ablehnung → `Error`, „kein Reconnect-Sturm"): Nach
  `Rejected` bleibt `_client.IsConnected` == `true`
  (`RemoteStatusRelayService.cs:182-188`), die Schleife geht in `PumpWhileConnectedAsync`
  und pusht im Intervall unregistrierte Snapshots, die alle mit `HubException` scheitern.
  Der `Error`-Zustand wird korrekt gesetzt, aber es fehlt das Trennen der Verbindung
  (oder eine Re-Register-Strategie) — angestrebte „begrenzte Versuche" werden nicht
  umgesetzt. Hängt mit dem vorigen Punkt zusammen.
- [ ] **Service-Tests ohne Backoff-/Drossel-Nachweis** (Task 40, Plan-Tests-Tabelle):
  `src/Softwareschmiede.Tests/Application/Services/RemoteStatusRelayServiceTests.cs`
  injiziert `TimeProvider.System` (Z. 296); es gibt keinen Test für exponentiellen
  Backoff via `TestTimeProvider` und keinen Test für den gedrosselten Change-Push
  (`MarkDirty`/`MinPushInterval` = 10 s, `RemoteStatusRelayService.cs:286-294`).
  Vorhandene Tests decken Disabled, ConfigurationError, Connect→Register→Initial-Push,
  Reject→Error und Connect-Fehler→Reconnecting ab.
- [ ] **Mobil-Nachweis nur teilweise assertiert** (Task 23): `StatusPageComposerTests.cs:87`
  prüft `<meta name="viewport">`, aber nicht „keine festen Inhaltsbreiten/`nowrap`-Muster".
  Das CSS enthält `white-space: nowrap` (`.pill`, `StatusPageComposer.cs:336`) und eine
  feste Spaltenbreite `td.num { width: 70px }` (Z. 333; wird erst in `@media
  (max-width:600px)` auf `auto` gesetzt, Z. 356). Entweder Assertion ergänzen und CSS
  bereinigen oder die Regel bewusst präzisieren (Pills gelten nicht als Inhaltsbreiten).
- [ ] **`RegisteredUtc` im `InstanceSnapshotCache` nicht umgesetzt** (Plan-Klassentabelle):
  `server/Softwareschmiede.StatusServer/Status/InstanceSnapshotCache.cs` führt
  `ConnectionIds`, `InstanceName`, `Snapshot`, `LastSeenUtc` — das geplante Feld
  `RegisteredUtc` fehlt (kosmetisch; `LastSeenUtc` beim `MarkConnected` deckt die
  Anzeige ab).

## Umgesetzte Elemente

- **Schritt 1 / Tasks 1–4 — Contracts:** `src/Softwareschmiede.StatusContracts` (net11.0,
  XML-Docs, CS1591) mit `RelayProtocolVersion.Current = 1`, `RegisterInstanceRequest`/
  `RegisterInstanceResult` (inkl. Ablehnungsgrund), `StatusSnapshot` (alle Pflichtfelder),
  `StatusSnapshotWorkItem` (inkl. Issue-/PR-Verweis mit `IssueUrl`/`StatusSnapshotPullRequest.Url`,
  Pausiert-bis, Heartbeat, Kind, Purpose), `StatusSnapshotScmEvent` **inkl. `Url`**;
  slnx-Eintrag `/Server/` (`Softwareschmiede.slnx:13-16`).
- **Schritt 2 / Tasks 5–8 — Server-Gerüst:** `server/Softwareschmiede.StatusServer`
  (`Microsoft.NET.Sdk.Web`, net11.0); `Program.cs` mit SignalR, `IHttpClientFactory`,
  Options-Bindung, `/relay`, `GET /`, `GET /status.json`, `public partial class Program`
  (Z. 69); `appsettings.json` + `launchSettings.json` (`localhost:5055`,
  Sektionen Relay/App/GitHub/Page); Options-Klassen `StatusServerOptions` +
  `GitHubRepositoryOptions` (TryParse `owner/repo`).
- **Schritt 3 / Tasks 9–11 — Relay:** `RelayTokenValidator` (access_token-Query +
  Bearer-Header-Fallback, `Relay:Tokens`, `DevToken` nur in Development,
  FixedTimeEquals); `StatusRelayHub` (Token-Prüfung in `OnConnectedAsync` →
  `Context.Abort()`; `RegisterInstance` mit InstanceId-/Protokollversion-Prüfung +
  verständlichem Reject; `PushSnapshot` verlangt Registrierung;
  `OnDisconnectedAsync` → `MarkDisconnected`); `InstanceSnapshotCache` (Singleton,
  je Instanz-ID: Verbindungsmenge, Snapshot, `LastSeenUtc`, `IsStale` = getrennt
  **oder** älter als `App:StaleAfterSeconds`, Multi-Instanz-Trennung, Multi-Connection).
- **Schritt 4 / Task 28 — App-Abhängigkeiten:** `Microsoft.AspNetCore.SignalR.Client`
  11.0.0 + `ProjectReference` auf StatusContracts in `src/Softwareschmiede/Softwareschmiede.csproj:14,18`.
- **Schritt 5 / Tasks 25–27 — App-Konfiguration:** alle `status.remote.*`-Schlüssel
  (`AppSettingService.cs:130-154`), typisierte Get/Set-Paare, Intervall-Klemmung
  15–3600/Default 60 (`:707-712`), `GetOrCreateRemoteStatusInstanceIdAsync` mit
  Neuerzeugung bei fehlendem/leerem Wert (`:728-735`), CredentialTarget
  `Softwareschmiede:RemoteStatus:RelayToken`.
- **Schritt 6 / Task 35 — Projektion:** `StatusSnapshotProjector.cs` — Scope via
  `IServiceScopeFactory`, `AsNoTracking`, Includes IssueReference/PullRequests/
  GitRepository, aktive/wartende WorkItems, Live-`RunStatus` mit Vorrang
  (`GetLiveRunStatus ?? RunStatus`, Z. 108), Pausen (`PausedUntilUtc`),
  `ActiveRunId`-/Heartbeat-Aktivitätsregel, SCM-Events Top-20 (blockierend + jüngste
  gemerged), `ScmEventObservation.Url` → DTO-`Url`, `IRunningAutomationsCount`,
  `IApplicationVersionProvider`.
- **Schritt 7 / Tasks 29–31 — Client-Grundlage:** `RemoteStatusUrlParser` (ws→http,
  `/relay`-Anhängung, Fehlermeldungen); `RemoteStatusConnectionState` (6 Werte) +
  `RemoteStatusConnectionInfo` (Singleton, `StateChanged`, `BuildStatusText`);
  `RemoteStatusRelayClient` hinter `IRemoteStatusRelayClient` — `AccessTokenProvider`,
  `WithAutomaticReconnect`, Closed/Reconnecting/Reconnected-Weitergabe und der
  geforderte **`ConfigureOptions`-Hook** für `HttpConnectionOptions` (Z. 68).
- **Schritt 8 / Tasks 32–34 — Dienst + DI:** `RemoteStatusRelayService : BackgroundService`
  mit `ScmEventStartupGate.WaitAsync`, Disabled/ConfigurationError **ohne**
  Verbindungsversuch + 60-s-Einstellungs-Poll, Connect → Register → Initial-Push →
  Pump-Schleife, exponentieller Backoff 5 s → 5 min via `TimeProvider`
  (`Task.Delay(delay, _timeProvider)`), Change-Drossel `MinPushInterval` = 10 s mit
  nicht blockierenden Handlern + Wake-Semaphore; DI in `App.xaml.cs:326-331` exakt wie
  geplant.
- **Schritt 9 / Tasks 12–13 — GitHub-Aggregation:** `GitHubStatusService` mit
  pulls/releases/issues je `GitHub:Repositories`-Eintrag, optionalem Token,
  User-Agent/Accept, ETag/`If-None-Match` (304 → Cache-Payload), TTL-Mindestabstand,
  `FetchedUtc`/`Error`/`ErrorUtc` je Repo inkl. Stale-Value-Fallback bei Fehler.
- **Schritt 10 / Tasks 14–16 — Seite:** `StatusPageComposer` rendert alle Sektionen
  (Laufende Aufgaben, Wartet auf Anwender inkl. pausiert/`WaitingForInput` + wartende
  SCM-Events, Letzte SCM-Ereignisse, pro Repo Offene Pull Requests + Letzte Releases +
  Issue-Zähler), Quellen-Badges „App · Zeit" / „GitHub · Zeit", Gesamt-Badge
  online/offline/veraltet, Offline-„seit"-Notiz, ehrlicher „keine Daten"-Zustand,
  `<a href>`-Links auf Issue, PR, Release, Repo und SCM-Ereignis-`Url`, HTML-Encoding,
  `<meta http-equiv="refresh">` (Page:RefreshSeconds) + viewport + `@media
  (max-width:600px)` + `overflow-wrap: anywhere`; `PageAccessMiddleware` (Basic Auth,
  schützt `/` und `/status.json`, `/relay` ausgenommen, deaktiviert bei leeren
  Credentials).
- **Schritt 11 — Kommentar-Aktionen:** korrekt entfallen (kein `CommentActionService`,
  kein `/actions/comment`, keine Buttons — verifiziert, Composer-Test assertiert
  `NotContain("<button")`).
- **Schritt 12 / Tasks 36–37 — Einstellungs-UI:** `SettingsViewModel`-Eigenschaften
  (IsRemoteStatusEnabled, Url, InstanceName, InstanceId read-only, Token, ConnectionText
  aus `RemoteStatusConnectionInfo.BuildStatusText`, `IsRemoteStatusFieldVisible`,
  Header-Statustext), Load-/Save-Verdrahtung (`:680-688`, `:884-907`) inkl.
  `IsNullOrWhiteSpace`→`DeleteCredential` und `SetCredential`-`InvalidOperationException`
  → verständliche Fehlermeldung; `SettingsView.xaml:719-812` Expander im
  Automatisierung-Tab mit `AutomationProperties.Name`, PasswordBox via Code-Behind
  → `ICredentialStore`, read-only Instanz-ID + Verbindungsstatus.
- **Schritt 13 / Tasks 19–24 — Server-Tests + CI:** Testprojekt mit allen csproj-
  Konventionen (`IsTestingPlatformApplication=false`, xunit.v3 4.0.3,
  xunit.runner.visualstudio 4.0.1, coverlet 10.1.1, FluentAssertions 8.11.0,
  Mvc.Testing 11.0.0, SignalR.Client 11.0.0, CS1591); `RelayTokenValidatorTests`,
  `InstanceSnapshotCacheTests` (lastSeenUtc, Stale-Schwelle, Multi-Instanz,
  Multi-Connection), `StatusRelayRoundtripTests` (echte `HubConnection` über in-memory
  `TestServer` via `HttpMessageHandlerFactory` + `HttpTransportType.LongPolling`:
  Register/Push → `/status.json` + HTML, Versions-Reject, unregistrierter Push,
  ungültiges/fehlendes Token, Disconnect → `connected:false` + `isStale:true`),
  `GitHubStatusServiceTests` (TTL-Cache, ETag/304-Revalidierung, Fehler-Zeitstempel,
  ungültige Repo-Einträge), `PageAccessTests` (401/200 je Konfiguration, `/relay`
  ausgenommen), `StatusPageComposerTests` (Sektionen, `<a href>`-Nachweis für Issue/PR/
  Release/SCM-Event, Offline-Notiz, „keine Daten", GitHub-Fehleranzeige, HTML-Escape,
  viewport); CI-Einträge build + test + trx-Upload in `pr-staging-ci.yml:123,136-137,183`,
  `staging-ci.yml:115,126-127,170`, `release.yml:113` (Test-Build dort über den
  slnx-Gesamtbuild gedeckt).
- **Schritt 14 / Tasks 38–42 — App-Tests:** `AppSettingServiceTests_RemoteStatus`
  (Defaults, Persistenz über Context-Grenzen, Klemmung, Ungültig→Default, GetOrCreate-
  Stabilität), `StatusSnapshotProjectorTests` (Feld-Mapping, Aktiv-Filter, Live-Vorrang
  `WaitingForInput`, SCM-Sortierung + Bad-Json-Fallback), `RemoteStatusRelayServiceTests`,
  `RemoteStatusUrlParserTests`, `SettingsViewModelTests_RemoteStatus` (Load/Save, Token →
  CredentialStore, Leeren → DeleteCredential, Sichtbarkeit, StateChanged→Statustext),
  `RemoteStatusRelayIntegrationTests` (produktiver `RemoteStatusRelayClient` über
  `ConfigureOptions`-Hook gegen `WebApplicationFactory`-TestServer → Snapshot in
  `/status.json`; `Microsoft.AspNetCore.Mvc.Testing` in
  `Softwareschmiede.IntegrationTests.csproj:23`).
- **Schritt 15 / Tasks 43–44 — E2E:** `E2E_RemoteStatusSettings.cs` (Expander,
  Schalter an/aus, Feld-Sichtbarkeit, Instanz-ID + Verbindungsstatus lesbar,
  Speichern-Neuladen-Persistenz, Rückbau) — in `MainTest.cs` verdrahtet und mit
  Page-Object-Methoden in `E2E/Views/SettingsView.cs`. `E2E_SettingsFeatureFlags`
  benötigt keine Anpassung (assertiert nur die drei bestehenden Expander
  WorkItemsExecute/WorkItemsExit/ProcessRuns).

## Hinweise / Abweichungen (kein eigenständiger Aufgabenpunkt)

- Deaktivierte Remote-Übersicht blendet die Felder **aus** (`IsRemoteStatusFieldVisible`)
  statt sie gesperrt mit Hinweistext darzustellen — entspricht dem geplanten
  Sichtbarkeits-Flag-Ansatz, weicht aber vom Task-37-Wortlaut „deaktivierte Felder +
  Hinweistext" ab; Erklärtext unter dem Schalter vorhanden.
- `MarkDisconnected` schreibt `lastSeenUtc` nicht auf den Trennungszeitpunkt fort —
  „offline seit" zeigt den letzten Snapshot-Kontakt, semantisch konsistent zur
  „last seen"-Deutung.
- `tasks.md`-Statusspalte steht weiterhin komplett auf „Offen" (Buchhaltung wurde
  nicht nachgezogen; betrifft nicht die Umsetzung).
- Mobiler/Desktop-Screenshot-Beleg für `test-results.md` (Lifecycle-Schritt 10) steht
  noch aus.
