# Bestandsaufnahme: SCM-Ereignisüberwachung erschöpft GitHub-REST-Rate-Limit durch zyklische Repository-Neuauflösung

Analysiert wurde der bestehende Code der SCM-Ereignisüberwachung und des GitHub-Plugins (`plugins/Softwareschmiede.Plugin.GitHub/`, `src/Softwareschmiede/Application/Services/`, `src/Softwareschmiede.Plugin.Contracts/`) bezogen auf die Anforderung aus GitHub-Issue #442 (`requirement.md`): zyklische, ungecachte Repository-Auflösung via `IScmEventSource.GetEventRepositoryAsync` erschöpft das primäre REST-Rate-Limit.

## Zusammenfassung

- **`GitHubPlugin.GetEventRepositoryAsync`** (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs:29`) kostet tatsächlich **zwei REST-Calls pro Aufruf** (`GET user` + `GET repos/{owner}/{name}`, Zeilen 35–36) und besitzt **keinerlei Cache** — weder für die Viewer-Identität noch für das Auflösungsergebnis.
- **Die Alias-Gruppierung erfolgt über rohe `RepositoryId`-/`RepositoryUrl`-Strings** (`ScmEventMonitoringService.cs:79–85`, `ScmIssuePreparationMonitoringService.cs:61–64`, `ScmBackmergeMonitoringService.cs:54–57`): `https://github.com/o/r` und `o/r` erzeugen getrennte Gruppen und damit getrennte Auflösungen pro Poll-Zyklus. Eine Kanonisierung zu `(Host, Owner, Name)` existiert nur plugin-intern (`ParseEventRepository`, `GitHubPlugin.Events.cs:405`) und wird von den Host-Diensten nicht genutzt.
- **Sechs Call-Sites** rufen `GetEventRepositoryAsync` ungecacht auf: drei Monitoring-Alias-Schleifen (Checkpoints `repository/`, `issueaufbereitung-repository/`, `backmerge-repository/`), `ScmTaskCompletionMonitoringService.DetermineClosureAsync` (je Issue-verknüpfter Aufgabe je Zyklus), `ScmEventOrchestrator` (generischer Dispatch `:193` und `DispatchPreparationAsync` `:654`, je Zustellung/Retry) sowie `GitHubPlugin.ExecuteResponseAsync` (`GitHubPlugin.EventResponses.cs:49`, je Outbox-Aktion).
- **RateLimit-Klassifikation und `RetryAfterUtc` existieren bereits** (`EvaluateResponse`, `GitHubPlugin.Events.cs:362–382`: 429 oder 403 mit `X-RateLimit-Remaining: 0`/`Retry-After`; Reset aus `Retry-After`/`X-RateLimit-Reset`), ebenso das Checkpoint-Backoff-Modell (`ScmCheckpointSupport.ApplyFailure` nutzt `RetryAfterUtc` über `ScmEventStore.RetryAt`). Ein **providerweites Fail-fast-Gate existiert nicht** — nach einem Rate-Limit-403 werden weitere Calls desselben Kontos unverändert abgesetzt.
- **Kontotrennung ist bereits strukturell vorhanden:** `_eventApiClient`/`_agentEventApiClient` (`GitHubPlugin.cs:156–157`) sind getrennte `IGitHubEventApiClient`-Instanzen je Credential — eine Cache-/Gate-Ebene auf Client-Instanz hätte die Konto-Dimension implizit.
- **`IsInaccessible`** (`ScmEventMonitoringService.cs:305`) kennt nur `Permanent`/`Permission`; ein als `RateLimit` geführter `repository/*`-Checkpoint blockiert im Inbox-Modus den kompletten Provider-Lauf (`complete = false`).
- **`ScmEventOptions`** hat nur `PollInterval` (60 s Default, keine appsettings-Überschreibung gefunden — `App.xaml.cs:222` bindet die Section `ScmEvents`), `AcceptanceTimeout`, `CompletionTimeout`; keine Cache-TTL-Option.
- **`ScmEventRepository`** ist ein unveränderliches Record-DTO (cachebar); `IScmEventSource` müsste für einen plugin-internen Cache nicht geändert werden.

**Test-Ausgangszustand:** Voller Build erfolgreich (0 Warnungen/0 Fehler). Stabile Lane `Softwareschmiede.Tests` (`Category!=OsInterface`): **2721/2721 ausgeführte Tests bestanden, 3 plattformbedingt übersprungen, 0 fehlgeschlagen**. `Softwareschmiede.IntegrationTests` (`Category!=OsInterface`): **357/357 bestanden**. Die `Category=OsInterface`-Lane (E2E/FlaUI, ConPTY, Clipboard — 87 `OsInterface`-Markierungen in `Softwareschmiede.Tests`, 4 in `Softwareschmiede.IntegrationTests`) wurde in dieser Sandbox **nicht ausgeführt** (umgebungssensitiv, kein zuverlässiger interaktiver Desktop/ConPTY); ihr Ausgangszustand ist unbekannt. Nachweis: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ScmEventRepository`, `ScmRepositoryContext`, `ScmEventReference`, `ScmObservationCheckpoint`, Transport-Records
- [Logik](inventory/logic.md) — `GitHubPlugin` (Events/EventResponses), `GitHubHttpApiClient`, Monitoring-Dienste, `ScmEventOrchestrator`, `ScmCheckpointSupport`, `ScmEventOptions`
- [Enums](inventory/enums.md) — `ScmProviderFailureKind`, `ScmAccountMode`, `ScmReferenceKind`, `ScmContentKind`, `ScmResponseKind`
- [Interfaces](inventory/interfaces.md) — `IScmEventSource`, `IScmEventResponder`, `IScmNotificationSource`, `IGitHubEventApiClient`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, bestehende Testklassen und Hilfsmethoden
