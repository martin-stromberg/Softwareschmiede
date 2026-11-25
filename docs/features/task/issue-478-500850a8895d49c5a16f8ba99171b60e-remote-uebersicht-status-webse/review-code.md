# Code-Review — Remote-Übersicht: Status-Webseite (Relay-Architektur) (Issue #478)

Re-Review (Iteration 2) gegen `review-code.1.md`. Alle Iteration-1-Befunde sind
adressiert (Nachweis unten je Bereich). Die folgenden Befunde sind Restlücken der
Fixes bzw. durch die Fixes neu eingeführte Probleme.

Status: Befunde vorhanden

## Befunde

### Mittel

**M1 — Ausnahme in `RegisterInstanceAsync` nach erfolgreichem `ConnectAsync` lässt die
Verbindung „verbunden, aber nicht registriert" zurück — Restlücke des K1-Fixes**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:201-210`
  (catch-Pfad von `TryConnectAndRegisterAsync`), `:120-134` (äußere Schleife),
  `:222-229` (`_registrationPending` wird ausschließlich durch `OnClientReconnected`
  gesetzt)

Der K1-Fix deckt zwei der drei Wege in den Zustand „connected, aber unregistriert" ab:
`Reconnected` setzt `_registrationPending` und `Rejected` trennt die Verbindung. Der
dritte Weg bleibt offen: `ConnectAsync` gelingt, `RegisterInstanceAsync` wirft danach
eine Ausnahme, die **nicht** vom Verbindungsabbruch kommt (z. B. `HubException` durch
einen serverseitigen Fehler in `RegisterInstance`, Invoke-Timeout, ältere
Server-Version, die wirft statt `Rejected` zurückzugeben) — die Verbindung bleibt
`IsConnected == true`. Der `catch (Exception)` meldet nur `Reconnecting` und gibt
`false` zurück; nach dem Backoff prüft die äußere Schleife `!_client.IsConnected` →
`false` → sie betritt direkt `PumpWhileConnectedAsync`. Dort ist
`_registrationPending == 0`, also läuft die normale Push-Logik: jeder Push endet in
`HubException("Instanz ist nicht registriert.")` → `ReportError` im Intervall, ohne je
wieder zu registrieren — exakt das K1-Fehlbild (Instanz dauerhaft offline, App meldet
Fehler), nur über einen selteneren Auslöser.

Konkrete Korrektur: Im `catch`-Pfad von `TryConnectAndRegisterAsync` die Verbindung wie
im Ablehnungs-Pfad per `DisconnectQuietlyAsync` schließen, oder — passend zum neuen
Mechanismus — `Interlocked.Exchange(ref _registrationPending, 1)` setzen, damit die
Pump-Schleife die Registrierung selbst nachholt. Ein Test, der `RegisterInstanceAsync`
einmalig werfen lässt (bei weiterhin `IsConnected == true`) und ein nachfolgendes
Re-Register bzw. Disconnect verifiziert, fehlt ebenfalls.

### Klein

**K1 — `_registrationPending`-Rücksetzung ist race-anfällig: ein `Reconnected` im
Fenster zwischen Register-Erfolg und Flag-Clear geht verloren**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:224-226`
  (`TryReregisterAsync` → `Interlocked.Exchange(ref _registrationPending, 0)` nach dem
  erfolgreichen Invoke) und `:196` (gleiche Reihenfolge in
  `TryConnectAndRegisterAsync`)

Das Flag wird **nach** dem erfolgreichen `RegisterInstanceAsync` gelöscht. Kommt in
dem (sehr schmalen) Fenster zwischen Invoke-Rückgabe und `Exchange` ein erneuter
Auto-Reconnect zustande (`OnClientReconnected` setzt `pending = 1`), löscht das
nachfolgende `Exchange` dieses Signal — die neue Verbindung bleibt unregistriert und
das K1-Fehlbild kehrt zurück. Die Wahrscheinlichkeit ist gering (erfordert
Drop+kompletten Reconnect innerhalb von Nanosekunden bzw. eine Thread-Unterbrechung
im kritischen Fenster), aber die richtige Reihenfolge kostet nichts: Flag **vor** dem
Registrierungsversuch löschen und im Fehlerfall wieder setzen — ein dadurch
mögliches zusätzliches `RegisterInstance` ist idempotent und unkritisch. Alternativ
ein monotone Epoche/Zähler statt Binärflag.

**K2 — Deaktivieren zur Laufzeit zeigt bis zu ~60 s fälschlich „verbinde erneut…"**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:107-117`
  (Reihenfolge `SetState(Disabled)` vor `DisconnectQuietlyAsync`), `:371-376`
  (`OnClientClosed` → `SetState(Reconnecting, "Verbindung geschlossen.")`)

`HubConnection.StopAsync` feuert `Closed` synchron. In der Deaktiviert-/
Konfigurationsfehler-Spur wird der Zustand **vor** dem Trennen auf `Disabled` gesetzt;
das ausgelöste `Closed`-Event überschreibt ihn anschließend mit `Reconnecting`. Erst
die nächste Schleifeniteration (nach `SettingsPollInterval` = 60 s) stellt wieder
`Disabled` her. Die Ablehnungs-Pfade machen es richtig herum (`DisconnectQuietlyAsync`
vor `SetState(Error)` — `:188-193`, `:270-275`). Konkrete Korrektur: Reihenfolge
vertauschen (erst `DisconnectQuietlyAsync`, dann `SetState(Disabled/ConfigurationError)`)
oder `OnClientClosed` auf `Disabled` nicht überschreiben lassen.

**K3 — Der Reconnect-Backoff wird von Change-Signalen vorzeitig aufgeweckt und damit
unterlaufen**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:125`
  (`SleepAsync(backoff)` → `WaitOrWakeAsync`), `:352-369` (`MarkDirty`/`SignalWake`)

`SleepAsync` nutzt denselben Wake-Kanal wie die Pump-Drossel. Feuern
`RunDataChanged`-/`Changed`-Ereignisse (z. B. periodische Laufzeit-Persistenz
laufender Aufgaben) während eines Server-Ausfalls, wacht die Schleife je Ereignis auf
und versucht den Connect erneut — die effektive Retry-Kadenz wird ereignisgesteuert
statt vom exponentiellen Backoff (5 s → 5 min) bestimmt; der `backoff`-Zähler
verdoppelt zwar weiter, die Wartezeit greift aber nie. Gleiches gilt für den
60-s-Poll im deaktivierten Zustand (harmlos, aber zwecklos). Konkrete Korrektur: Für
den Backoff-Schlaf schlicht `Task.Delay(delay, _timeProvider, ct)` verwenden bzw.
das Wake-Signal nur im Pump-Loop konsumieren (z. B. eigenes Warte-Hilfsmittel oder
`SignalWake` auf Change-Kontexte beschränken).

**K4 — `ExecuteAsync` hat keinen Catch-all: eine unerwartete Ausnahme beendet den
Dienst endgültig und maskiert sich als „deaktiviert"**

- `src/Softwareschmiede/Application/Services/RemoteStatusRelayService.cs:100-146`
  (`finally` → `SetState(Disabled)`), `:151-174` (`ReadConfigurationAsync`: DB-Scope,
  `_credentialStore.GetCredential` — beide können werfen)

Ein einzelner nicht-OCE-Fehler in `ReadConfigurationAsync` (SQLite-/EF-Fehler,
CredentialStore-Ausnahme) faultet `ExecuteAsync`; der BackgroundService stoppt, der
`finally`-Block setzt `Disabled` — die Remote-Übersicht ist bis zum App-Neustart tot,
während die UI „deaktiviert" anzeigt, obwohl der Schalter an ist. Konkrete Korrektur:
Schleifenkörper in `try/catch (Exception) when (ex is not OperationCanceledException)`
hüllen → `_info.ReportError(...)` + kurzer Sleep + `continue`.

**K5 — Hinweistext „leer speichern entfernt den Eintrag" ist in der Praxis nicht
auslösbar**

- `src/Softwareschmiede.App/Views/SettingsView.xaml:789-794`,
  `src/Softwareschmiede.App/Views/SettingsView.xaml.cs:92-100`,
  `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:701-702,904-914`

Die PasswordBox ist nie bindbar und bleibt beim Öffnen leer, `RemoteStatusToken` im
ViewModel enthält aber den geladenen gespeicherten Token (für `HasRemoteStatusToken`).
`PasswordChanged` feuert nur bei Benutzereingabe — wer das Feld unberührt lässt,
speichert den alten Token erneut; „leer speichern" gibt es faktisch nicht. Token-
Entfernung funktioniert nur über den undokumentierten Umweg „etwas tippen und wieder
löschen" (→ `Password == ""` → `IsNullOrWhiteSpace` → `DeleteCredential`) bzw. ein
Whitespace-Zeichen. Konkrete Korrektur: Text anpassen (tatsächlicher
Entfern-Mechanismus benennen) oder eine explizite „Token entfernen"-Möglichkeit
ergänzen (z. B. Checkbox/Button, der ein separates `RemoteStatusTokenCleared`-Flag
setzt).

**K6 — `ExecuteAsync_AutoReconnect_ReregistersInstanceAndPushes` ist am
Zeitlimit-Rand flaky**

- `src/Softwareschmiede.Tests/Application/Services/RemoteStatusRelayServiceTests.cs:257-259`

Der Folge-Push nach `Reconnected` ist durch `MinPushInterval` (10 s) gedrosselt
(Real-Zeit, `TimeProvider.System`); die Poll-Deadline beträgt ebenfalls exakt 10 s ab
dem Raise. Push-Ausführung und Poll-Schritt konkurrieren auf dieselbe
10-s-Grenze — unter Last kippt das. Konkrete Korrektur: Deadline auf ~15 s erhöhen
oder den Test auf `FakeTimeProvider` umstellen (wie die Backoff-/Drossel-Tests).

**K7 — Die neuen Cache-Mechanismen (`EvictDisconnected`,
`RemoveConnectionIdFromAll`) sind ungetestet**

- `server/Softwareschmiede.StatusServer/Status/InstanceSnapshotCache.cs:41-58,115-143`,
  `server/Softwareschmiede.StatusServer.Tests/Status/InstanceSnapshotCacheTests.cs`

Kein Test prüft, dass `MarkConnected` mit abweichender `instanceId` die
`connectionId` aus dem alten Record entfernt (Geister-Verbindung aus K6 Iteration 1),
und keiner die 24-h-Eviction getrennter Records über `TestTimeProvider`.

## Verifikation der Iteration-1-Befunde (alle adressiert)

- **K1 (alt)**: `OnClientReconnected` → `_registrationPending` + Dirty + `SignalWake`;
  Pump-Loop registriert via `TryReregisterAsync` vor dem nächsten Push; Ablehnung →
  `DisconnectQuietlyAsync` + `Error` (beide Pfade: `:185-194`, `:270-275`). Restlücke
  siehe M1 oben.
- **M1 (alt)**: `SignalWake` fängt `SemaphoreFullException` (`:358-369`).
- **M2 (alt)**: Dirty-Flag wird erst beim Push-Versuch zurückgesetzt (`:231-239`).
- **M3 (alt)**: E2E assertiert die Instanz-ID erst nach `SaveSettings()`; Kommentar
  korrigiert (`E2E_RemoteStatusSettings.cs:41-51`).
- **M4 (alt)**: `item.PullRequests ?? []` (`StatusPageComposer.cs:201`); `WorkItems`/
  `ScmEvents` bereits `?? []` (`:121,142,163`).
- **M5 (alt)**: `IsSafeHttpUrl` (http/https-Whitelist) an allen `href`-Stellen:
  `AppendLinked` (`:309-315`), PR-Links (`:203`), Repo-URL (`:257`).
- **K2 (alt)**: `CacheEntry` trennt `FetchedUtc` (Abrufversuch/TTL) von
  `LastSuccessUtc`/`ErrorUtc` (`GitHubStatusService.cs:229-269`).
- **K3 (alt)**: `SetState(Disabled)` ohne Detailtext (`RemoteStatusRelayService.cs:109-114`).
- **K4 (alt)**: `observation is not null`-Guard (`StatusSnapshotProjector.cs:168-175`).
- **K5 (alt)**: `/status.json` injiziert `TimeProvider` (`Program.cs:55,61`).
- **K6 (alt)**: `RemoveConnectionIdFromAll` bei Re-Registrierung +
  `EvictDisconnected` (24-h-Retention) (`InstanceSnapshotCache.cs`); Testlücke → K7 oben.
- **K7 (alt)**: Gemeinsamer `SecretComparer` (SHA-256-Pre-Hash +
  `CryptographicOperations.FixedTimeEquals`); `DevToken` nach
  `appsettings.Development.json` verschoben (`appsettings.json` trägt `"DevToken": null`).
- **K8 (alt, optional)**: unverändert — `PumpWhileConnectedAsync` arbeitet mit der bei
  Connect gelesenen Konfiguration; Intervall-Änderungen greifen erst nach Reconnect.
  War als optional markiert, ggf. in der Hilfe benennen.
- **K9 (alt)**: Neue Tests für Re-Register, abgelehnte Re-Registrierung, Backoff via
  `FakeTimeProvider`, gedrosselten Change-Push, null-Listen und Nicht-http-Schemes
  vorhanden; Restlücken → K6/K7 oben.

## Geprüfte Bereiche

- **`RemoteStatusRelayService`** (komplett): Zustandsautomat, Re-Register-Mechanik
  (`_registrationPending`, `TryReregisterAsync`), Disconnect bei Rejected,
  `SignalWake`/`SemaphoreFullException`, Dirty-Flag-Reihenfolge, `WaitOrWakeAsync`
  (Semaphore + `Task.Delay` über `TimeProvider`), Event-(De)Registrierung,
  Cancellation-Pfade. Kein Deadlock zwischen Pump und `Reconnected`-Handler (Handler
  ist nicht-blockierend: nur Flags + `Release` + `SetState`), keine
  Doppel-Registrierung (Pump ist der einzige Register-Pfad nach Connect; Flag wird in
  `TryConnectAndRegisterAsync` nach Erfolg zurückgesetzt).
- **`RemoteStatusRelayClient`**: `ConnectAsync` disposed die Altverbindung (beendet
  ggf. laufenden Auto-Reconnect sauber), Event-Forwarding, `IAsyncDisposable` via DI;
  `IsConnected` = `HubConnectionState.Connected`.
- **`RemoteStatusConnectionInfo`**: Locking, `StateChanged` außerhalb des Locks,
  `BuildStatusText` (Disabled ohne Redundanz).
- **`StatusPageComposer`**: `IsSafeHttpUrl` an allen `href`-Ausgaben, alle Listen
  `?? []`, Escaping, `ErrorUtc`-Anzeige.
- **`GitHubStatusService`**: `FetchedUtc`/`LastSuccessUtc`/`ErrorUtc`-Trennung inkl.
  304-/Fehler-/TTL-Pfade; `StoreAsync` führt Vorwärtsreferenzen korrekt.
- **`InstanceSnapshotCache`**: Cross-Instance-ConnectionId-Entfernung,
  `EvictDisconnected` (Lock je Record, keine verschachtelten Locks — kein
  Deadlock-Risiko), gebundenes Wachstum.
- **`StatusRelayHub`**: unverändert korrekt (Token-Gate, `Context.Items`,
  serverseitige InstanceId-Bindung).
- **`SecretComparer`/`RelayTokenValidator`/`PageAccessMiddleware`**: Dedup +
  Konstantzeit, DevToken nur Development.
- **Settings-UI**: `IsEnabled`-Sperre statt Visibility (`IsRemoteStatusFieldEnabled`),
  Feldbereich dauerhaft sichtbar, Token-Stored-Hint (`HasRemoteStatusToken`),
  Fehler-Brush via `DataTrigger` auf `IsRemoteStatusConnectionError`,
  `PasswordChanged`-Code-Behind ohne Race.
- **`StatusSnapshotProjector`**: `observation is not null`-Guard.
- **`RemoteStatusUrlParser`**, **`AppSettingService`** (Intervall-Clamp 15–3600,
  Instanz-ID-Erzeugung), **`App.xaml.cs`**-DI (Singleton-Registrierung, HostedService).
- **Tests**: `RemoteStatusRelayServiceTests` (Re-Register, Rejected-Disconnect,
  Backoff/Drossel via `FakeTimeProvider`), `E2E_RemoteStatusSettings` (Reihenfolge
  korrigiert, IsEnabled-Sperre), `SettingsView` Page-Object (saubere Automation-Ids),
  `StatusPageComposerTests` (null-Listen, Schemes), `RemoteStatusRelayIntegrationTests`
  (Roundtrip via `WebApplicationFactory`).
- **Build-Verifikation**: `Softwareschmiede.csproj`, `Softwareschmiede.StatusServer`
  und `Softwareschmiede.StatusServer.Tests` kompilieren fehlerfrei (0 Warnungen);
  App-/E2E-Projekte aus Self-Hosting-Gründen nicht gebaut.
