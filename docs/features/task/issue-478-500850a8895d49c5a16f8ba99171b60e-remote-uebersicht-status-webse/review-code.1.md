# Code-Review — Remote-Übersicht: Status-Webseite (Relay-Architektur) (Issue #478)

Status: Befunde vorhanden

## Befunde

### Kritisch

**K1 — Nach SignalR-Auto-Reconnect (oder abgelehnter Registrierung) bleibt die Verbindung „verbunden, aber nicht registriert" — Snapshots schlagen dauerhaft fehl und die Instanz erscheint nie wieder online**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:296-314` (`OnClientReconnected`), `:179-188` (Ablehnungs-Pfad), `:117-131` (äußere Schleife)
- `src/Softwareschmiede/Application/Services/RemoteStatusRelayClient.cs:101` (`WithAutomaticReconnect()`)
- `server/Softwareschmiede.StatusServer/Relay/StatusRelayHub.cs:71,85-88` (`Context.Items[InstanceIdItemKey]`, `PushSnapshot` wirft `HubException`)

Zwei zusammenhängende Lücken im Zustandsautomaten:

1. `WithAutomaticReconnect()` stellt nach einem Verbindungsabbruch serverseitig eine **neue** Verbindung her. Der Hub-`Context.Items`-Eintrag `status.instanceId` ist verbindungsgebunden; `MarkConnected` läuft nur in `RegisterInstance`. Gelinge der Auto-Reconnect, während die Pump-Schleife in `WaitOrWakeAsync` schläft (Standard: erster Retry nach 0 s, Schleife wartet bis zu 10 s), ist `_client.IsConnected` danach wieder `true` — der Dienst bleibt in `PumpWhileConnectedAsync` und betritt `TryConnectAndRegisterAsync` **nie wieder**. Jeder `PushSnapshot` wirft `HubException("Instanz ist nicht registriert.")` → `ReportError`, Endlosschleife. Serverseitig wurde die alte `connectionId` per `OnDisconnectedAsync` entfernt, die neue nie hinzugefügt → `Connected=false`, `IsStale=true` → die Status-Seite zeigt die Instanz dauerhaft „offline", während die App „verbunden" meldet. Erholung nur über zufälligen `Closed`-Fall oder App-Neustart.
2. Bei abgelehnter `RegisterInstance` (`Accepted=false`, z. B. Protokoll-Mismatch — genau der vorgesehene Upgrade-Pfad) wird die Verbindung **nicht getrennt**: `_client.IsConnected` bleibt `true`, die äußere Schleife geht nach dem Backoff direkt in `PumpWhileConnectedAsync` und pusht dort unregistrierte Snapshots in Intervall-Fehlern — nie ein erneuter Registrierungsversuch.

Konkrete Korrektur:
- Nach `Reconnected` zwingend erneut `RegisterInstance` aufrufen (Request im Client/Service merken und im `Reconnected`-Handler oder vor dem nächsten Push nachholen) — alternativ `WithAutomaticReconnect` entfernen und den eigenen Backoff-Loop nutzen, oder im `Reconnected`-Handler die Verbindung bewusst trennen, damit die äußere Schleife sauber `Connect+Register` durchläuft.
- Bei `Rejected`/`Error` die Verbindung per `DisconnectAsync` schließen (oder einen „nicht erneut versuchen"-Zustand einführen), damit keine unregistrierten Pushes laufen.
- Test ergänzen, der nach simuliertem `Reconnected` ein erneutes `RegisterInstanceAsync` verifiziert.

### Mittel

**M1 — Race in `MarkDirty`: `SemaphoreSlim.Release()` kann `SemaphoreFullException` in die Notifier-Aufrufer werfen**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:289-294`

`if (_wakeSignal.CurrentCount == 0) _wakeSignal.Release();` ist ein TOCTOU: Feuern `RunDataChanged` (Heartbeat-/Persistenzpfade) und `ScmEventChangeNotifier.Changed` gleichzeitig auf unterschiedlichen Threads, sehen beide `CurrentCount == 0`, beide rufen `Release()` → der zweite wirft `SemaphoreFullException` (MaxCount 1). Die Ausnahme propagiert synchron in `NotifyRunDataChanged`/`Notify` und damit in unbeteiligte Subsysteme (z. B. Laufzeit-Heartbeat-Persistenz).

Konkrete Korrektur: `try { _wakeSignal.Release(); } catch (SemaphoreFullException) { /* bereits signalisiert */ }` oder atomar signalisieren (z. B. `Interlocked`-Flag + bedingtes Release im try/catch).

**M2 — `PumpWhileConnectedAsync` löscht `_snapshotDirty`, ohne zu pushen: einzelne Change-Signale gehen verloren**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:212-216`

`Interlocked.Exchange(ref _snapshotDirty, 0)` wird ausgewertet, bevor die `MinPushInterval`-Drossel geprüft wird: Trifft eine Änderung <10 s nach dem letzten Push ein, wird das Flag gelöscht, die Bedingung `(dirty && sinceLast >= MinPushInterval)` ist aber `false` → kein Push und Flag weg. Der nächste Push erfolgt erst zum Intervall-Ende (Default 60 s) statt gedrosselt nach ~10 s — die Change-getriebene Schnellaktualisierung ist damit faktisch wirkungslos, wenn nicht ständig neue Signale eintrudeln.

Konkrete Korrektur: Flag nur beim tatsächlichen Push zurücksetzen, z. B. `var dirty = Volatile.Read(ref _snapshotDirty) == 1;` und erst nach erfolgreichem/versuchtem Push `Interlocked.Exchange(ref _snapshotDirty, 0)` (bzw. Flag bei Nicht-Push wieder setzen).

**M3 — E2E-Test assertiert die Instanz-ID vor dem Speichern; auf frischer DB schlägt der Test fehl (und „sofort vergeben" ist nicht implementiert)**

- `src/Softwareschmiede.Tests/E2E/E2E_RemoteStatusSettings.cs:36-44`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` (`IsRemoteStatusEnabled`-Setter ~:365-375, `LoadAsync` ~:683, `SaveAsync` ~:889)

Der Test aktiviert den Schalter und liest `GetRemoteStatusInstanceId()` **vor** `SaveSettings()` mit `Assert.False(string.IsNullOrWhiteSpace(instanceId))`. Die ID wird aber nur in `LoadAsync` erzeugt, wenn die Übersicht **bereits** aktiviert war, oder in `SaveAsync`. Auf einer frischen Test-DB ist `RemoteStatusInstanceId` nach dem Anhaken leer → Assert schlägt fehl. Entweder der Test ist gegen die Implementierung falsch angeordnet, oder die kommentierte UX („Instanz-ID wird sofort vergeben") fehlt.

Konkrete Korrektur: Beim Aktivieren (`IsRemoteStatusEnabled`-Setter oder beim Sichtbarwerden des Feldbereichs) `GetOrCreateRemoteStatusInstanceIdAsync` aufrufen und `RemoteStatusInstanceId` befüllen — oder die Assert-Reihenfolge nach `SaveSettings()` verschieben und den Kommentar korrigieren.

**M4 — `RenderWorkItemRow` iteriert `item.PullRequests` ungeschützt: `null`-Liste aus einem Snapshot lässt die ganze Status-Seite mit 500 fallen**

- `server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs:196`

`WorkItems`/`ScmEvents` werden defensiv mit `?? []` gelesen, `item.PullRequests` nicht: Ein Client, der `"PullRequests": null` serialisiert (ältere/buggy App-Version, Fremd-Client mit gültigem Token), erzeugt eine `NullReferenceException` beim Rendern von `/` — die Status-Seite fällt für **alle** Betrachter aus, bis ein neuer Snapshot überschreibt. Gleiches gilt analog für weitere nicht-required Felder in Fremd-DTOs.

Konkrete Korrektur: `item.PullRequests ?? []` (und prinzipiell alle verschachtelten Listen im Renderpfad absichern); optional zusätzlich serverseitige Minimal-Validierung in `PushSnapshot`.

**M5 — Gerenderte `href`-Ziele prüfen kein URI-Scheme: `javascript:`/`data:`-Links aus Snapshots bleiben klickbar**

- `server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs:182,198,202,220,248,261,278` (`AppendLinked`, PR-/Issue-/Release-Links)

`E()` verhindert Attribut-Breakout (`&quot;`), nicht aber aktive Schemes: Ein Snapshot mit `IssueUrl = "javascript:…"` oder `data:text/html,…` erzeugt einen klickbaren Link, der im Seitenkontext Script ausführt (XSS gegen Status-Seiten-Betrachter). Angriffsvoraussetzung ist ein gültiges Relay-Token bzw. kontrollierte Provider-URLs — niedrig, aber die Seite rendert fremdgesteuerte Daten und sollte scheme-whitelisten.

Konkrete Korrektur: In `AppendLinked`/allen `href`-Ausgaben nur `http`/`https` zulassen (`Uri.TryCreate` + Scheme-Prüfung), sonst als reinen Text ausgeben.

### Klein

**K2 — `GitHubStatusService` vermischt „letzter erfolgreicher Abruf" und „letzter Fehler" in `FetchedUtc`**

- `server/Softwareschmiede.StatusServer/GitHub/GitHubStatusService.cs:181,201-216,218-226`

`StoreAsync` schreibt bei Fehlern einen `CacheEntry` mit `FetchedUtc = Fehlerzeitpunkt`. Ab dem zweiten Fehlschlag ist der echte „last success"-Zeitpunkt verloren: TTL-Treffer liefern per `ToResult` die Fehlerzeit als `FetchedUtc`, die die Seite als Quellen-Zeitstempel „GitHub · HH:mm:ss UTC" anzeigt — obwohl die Daten älter sein können. Konkrete Korrektur: `LastSuccessUtc` und `LastErrorUtc` im `CacheEntry` getrennt führen und jeweils unverändert weiterreichen.

**K3 — Doppelte Statustext-Anzeige bei deaktivierter Remote-Übersicht**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:106-115,155` + `RemoteStatusConnectionInfo.BuildStatusText()` (`RemoteStatusConnectionInfo.cs:132-135`)

`SetState(Disabled, "Remote-Übersicht ist deaktiviert.")` ergibt in `BuildStatusText` die redundante Zeile „deaktiviert: Remote-Übersicht ist deaktiviert.". Konkrete Korrektur: Bei `Disabled` keinen `statusMessage`-Text mitgeben (`SetState(Disabled)`).

**K4 — `StatusSnapshotProjector` fängt nur `JsonException`; `ObservationJson = "null"` erzeugt eine NRE außerhalb des Schutzes**

- `src/Softwareschmiede/Application/Services/StatusSnapshotProjector.cs:166-175`

`ScmEventStore.Observation(item)` nutzt `Deserialize(...)!`: Ist das JSON literal `null`, liefert es `null` → `observation.ContentKind` wirft `NullReferenceException`, die der `catch (JsonException)` nicht fängt → die komplette Snapshot-Erzeugung fällt aus (Push-Fehler). Konkrete Korrektur: `observation is null`-Fall behandeln (ReferenceDisplay/Url bleiben `null`) oder `catch` auf `Exception`-Minus-OCE erweitern.

**K5 — `/status.json` verwendet `DateTimeOffset.UtcNow` statt der registrierten `TimeProvider`**

- `server/Softwareschmiede.StatusServer/Program.cs:60`

Inkonsistent zur sonst überall injizierten `TimeProvider.System`-Registrierung (Zeile 12) und schlecht testbar. Konkrete Korrektur: `TimeProvider` in die Lambda injizieren und `GetUtcNow()` verwenden.

**K6 — `InstanceSnapshotCache` räumt nie auf; Re-Registrierung mit anderer Instanz-ID hinterlässt „Geister-Verbindungen"**

- `server/Softwareschmiede.StatusServer/Status/InstanceSnapshotCache.cs:30-51`, `server/Softwareschmiede.StatusServer/Relay/StatusRelayHub.cs:61-77`

Einträge werden nie entfernt: Einmal registrierte Instanzen bleiben (inkl. Snapshot) für die Lebensdauer des Prozesses im Dictionary und auf der Seite. Token-authentisierte Clients können die Liste unbegrenzt wachsen lassen. Zusätzlich kann eine Verbindung per erneutem `RegisterInstance` auf eine andere `InstanceId` wechseln — die alte `connectionId` verbleibt dann im alten Record und markiert die alte Instanz bis zum Disconnect fälschlich als verbunden. Konkrete Korrektur: optional TTL-Eviction für lange getrennte Records; bei `RegisterInstance` die `connectionId` aus anderen Records entfernen (oder Wechsel ablehnen).

**K7 — `FixedTimeEquals` dupliziert; Längen-Short-Circuit und Basic-Auth-Härtung**

- `server/Softwareschmiede.StatusServer/Security/RelayTokenValidator.cs:41-51`, `server/Softwareschmiede.StatusServer/Security/PageAccessMiddleware.cs:75-85`

Die Methode existiert zweimal identisch; der frühe `Length`-Vergleich leakt die Token-Länge (üblich, aber `CryptographicOperations.FixedTimeEquals` auf UTF8-Bytes wäre die BCL-Kanone und vermeidet die Duplikation). Hinweis zudem: `DevToken: "dev-token"` liegt in `appsettings.json` — sicher durch das `IsDevelopment`-Gate, aber bei falsch gesetzter `ASPNETCORE_ENVIRONMENT=Development` auf einem exponierten Deployment trivial bekannt. Konkrete Korrektur: gemeinsame Helper-Methode/`CryptographicOperations`; DevToken optional nach `appsettings.Development.json` verschieben.

**K8 — Pump-Schleife arbeitet mit der beim Connect gelesenen Konfiguration; Intervall-/URL-/Namensänderungen greifen erst nach Reconnect**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:131,206-209`

`config` wird nur pro äußerer Schleifen-Iteration gelesen; innerhalb `PumpWhileConnectedAsync` bleibt u. a. `SnapshotIntervalSeconds` eingefroren. Laufzeit-Änderungen am Intervall wirken erst nach Verbindungsabbruch. Konkrete Korrektur (optional): Konfiguration periodisch innerhalb der Pump-Schleife neu lesen oder bei Settings-Änderung `MarkDirty`/Reconnect anstoßen; andernfalls Verhalten in der Doku/Hilfe benennen.

**K9 — Testlücken und fragile Asserts**

- `src/Softwareschmiede.Tests/Application/Services/RemoteStatusRelayServiceTests.cs:209-221` (`ExecuteAsync_RejectedRegistration_SetsErrorState`): `PushSnapshotAsync Times.Never` ist zwar aktuell erfüllbar, aber irreführend — mit `IsConnected=true`-Mock betritt der Dienst nach dem Backoff `PumpWhileConnectedAsync` und pusht unregistrierte Snapshots (verwandt mit K1); die Verifikation deckt das Fehlverhalten eher zu als auf.
- Der kritische Pfad „Reconnect → Re-Register" hat keinen Test; Backoff/Drossel werden mit `TimeProvider.System` statt Fake-TimeProvider getestet (keine deterministische Backoff-Verifikation).
- `server/Softwareschmiede.StatusServer.Tests/Relay/StatusRelayRoundtripTests.cs:169-178` pollt mit echtem `Task.Delay`/`DateTime.UtcNow` (mildes Flaky-Risiko unter Last, durch 5-s-Fenster aber vertretbar).
- `StatusPageComposerTests` deckt kein `PullRequests: null` und kein nicht-http(s)-Scheme ab (siehe M4/M5).
- `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs:458`: `IsRemoteStatusFieldsAreaVisible` prüft `"RemoteStatusUrl"` statt des benannten Containers `"RemoteStatusFieldsArea"` — funktional ok, semantisch fragil.

## Geprüfte Bereiche

- **Contracts** (`src/Softwareschmiede.StatusContracts/`): `StatusSnapshot`, `StatusSnapshotWorkItem`, `StatusSnapshotPullRequest`, `StatusSnapshotScmEvent`, `RegisterInstanceRequest`/`Result`, `RelayProtocolVersion` — DTO-Nullabilität plausibel, XML-Docs vorhanden, keine Secret-Felder.
- **Server** (`server/Softwareschmiede.StatusServer/`): `Program.cs` (DI, Endpunkte), `StatusRelayHub` (Token-Prüfung in `OnConnectedAsync`, Register/Push/Disconnect, Token wird nicht geloggt, serverseitiges Überschreiben der `InstanceId` verhindert Spoofing), `RelayTokenValidator` (Fail-closed, DevToken nur Development), `InstanceSnapshotCache` (Locking, Stale-Logik), `GitHubStatusService` (ETag/`If-None-Match`, TTL, Request-CT weitergegeben, Token nicht geloggt), `StatusPageComposer` (HTML-Escaping aller Textwerte, viewport/`overflow-wrap`, keine Aktionsbuttons), `PageAccessMiddleware` (Basic-Auth nur bei konfigurierten Credentials, `/relay` ausgenommen), `Options`, `appsettings.json`, `launchSettings.json`, csproj.
- **App-Seite** (`src/Softwareschmiede/Application/Services/`): `RemoteStatusRelayService` (Zustandsautomat, Backoff via `TimeProvider`, Startup-Gate, Event-(De)Registrierung, `DisconnectQuietly`, CT-Weitergabe), `RemoteStatusRelayClient` (IAsyncDisposable, `ConfigureOptions`-Test-Hook), `RemoteStatusConnectionInfo` (Locking), `RemoteStatusUrlParser` (Scheme-Normalisierung, `/relay`-Pfad), `StatusSnapshotProjector` (`AsNoTracking`, `Include`s ohne N+1, Live-`RunStatus`-Vorrang, Top-N-SCM-Events).
- **App-Settings** (`AppSettingService`-Diff): Schlüssel/Defaults, Intervall-Clamp 15–3600 s, `GetOrCreateRemoteStatusInstanceId` persistiert GUID `N`.
- **UI** (`App.xaml.cs`-DI, `SettingsViewModel`, `SettingsView.xaml`/`xaml.cs`-Diff): Sichtbarkeitslogik, PasswordBox-Handling via Code-Behind, Token im `ICredentialStore`, `Dispose` unsubscribt.
- **Tests**: `server/Softwareschmiede.StatusServer.Tests/` (Roundtrip, Token-Validator, Page-Access, Cache, Composer, GitHub inkl. 304/ETag — `TestTimeProvider` deterministisch), `src/Softwareschmiede.Tests/` neue RemoteStatus-Tests (Konfiguration, Zustandsautomat, Projektion, ViewModel, E2E-Abschnitt), `RemoteStatusRelayIntegrationTests` (Long-Polling gegen `TestServer`).
- **Build-/CI-Integration**: `Softwareschmiede.slnx`, Workflow-Diffs (pr-staging-ci, staging-ci, release — Build- und Test-Schritte sowie TRX-Upload konsistent ergänzt), csproj-Diffs (`Microsoft.AspNetCore.SignalR.Client`, `Microsoft.AspNetCore.Mvc.Testing`, Projektreferenzen).
