# Detailinventar: Relevante Tests und Dokumentationsorte

Bestandteil von [inventory.md](../inventory.md). Es wurden keine Tests ausgeführt — nur Bestandsaufnahme.

## Unit-Tests (`src/Softwareschmiede.Tests/`)

| Datei | Abdeckung |
|---|---|
| `Application/Services/ScmEventExchangeServiceTests.cs` (327 Z.) | Echtes Protokoll auf temp-Git-Repo: Git-Exklusion + Steuerzeichen-Escape (Z. 24–36), `promptSuffix` nur im Prompt/nicht in `request.json` (Z. 41–64), Probe ohne Anhang (Z. 68–105), fremde/versionierte Kanalinhalte (Z. 110–120), Stabilitäts-Zweitleseregel (Z. 123–135), Korrelation/Zielverbot (Z. 138–165), mutiertes `request.json` (Z. 169–174), Result-Immutabilität (Z. 178–189), `complete.json`-Validierung: `noRemoteAction`+`reason` (Z. 195–204), `report`-Pflicht (Z. 213–230), `pullRequestNumber`-Validierung und Prompt-Dokumentation (Z. 236–279), Anweisungsinhalt (Z. 285–300). |
| `Application/Services/ScmPromptAppendixTests.cs` | `SpecificKeyFor`-Mapping aller `ContentKind`s, `Combine`-Reihenfolge/Leersemantik, `Resolve`. |
| `Application/Services/AppEinstellungServiceTests_ScmMonitoring.cs` (Z. 90–101) | `GetScmPromptAppendicesAsync`: leer ohne Einträge, persistierte Werte werden geliefert. |
| `Infrastructure/Plugins/GitHubEventSourceTests.cs` (671 Z.) | U. a. Responder-Pfade: Marker-Reconcile statt Doppel-POST (Z. 339–358), 403 nicht-uncertain (Z. 360–371), Viewer-Wechsel blockiert (Z. 373–382), PR-Antwort am gespeicherten Ursprung inkl. `ResponseBody`-Beginn mit `request.Body` (Z. 384–400); Fake `FakeApi : FakeGitHubEventApi` (Z. 670); `Identity(api)`-Setup für `user`/`repos`-Auflösung (Z. 635–636). |
| `Infrastructure/Plugins/GitHubHttpApiClientTests.cs` (210 Z.) | Host-Mapping `api.github.com` vs. `{host}/api/v3` (Z. 19–29), Host-Validierung/Token-Grenze (Z. 33–61), Pflicht-Header/Bearer (Z. 63–88), JSON-Body `application/json` (Z. 108–123), Header-Durchreichen (Z. 125–143), Fehler-Matrix/`outcomeUncertain` (Z. 155–169), Token niemals in URL/Fehlern (Z. 182–203). |
| `Infrastructure/Plugins/GitHubNotificationSourceTests.cs`, `GitHubPluginTests*.cs`, `GitHubSinceWindowTests.cs`, `GitHubPluginNetrcCleanupTests.cs` | Notifications-Inbox, Plugin-Stammfunktionen, Since-Fenster, netrc-Bereinigung. |
| `Helpers/FakeGitHubEventApi.cs` | Warteschlangen-Fake für `IGitHubEventApiClient`: `Add`/`AddJson`/`AddGraph`/`AddStatus`/`AddFailure`, `Requests`-Aufzeichnung, Endpoint-Fragment-Assert, `AssertComplete`. |
| `Helpers/ScmDispatchFixture.cs` (291 Z.) | Integrations-Fixture: gemocktes `IGitPlugin`/`IKiPlugin`, echte Services+SQLite; `Responses` (ScmResponseRequest-Liste, Z. 45/76–77), `Repository`/`BotRepository` (Z. 47–51), `AddAsync` legt `ScmIncomingEvent` via `ScmEventStore.AcceptBatchAsync` an (Z. 172–192), `DispatchAsync`, `CompleteProbeAsync` (Dateitool-Probe, Z. 245–253), `InitGit`. |

## Integrationstests (`src/Softwareschmiede.IntegrationTests/Services/`)

- `ScmEventDispatchTests.cs`: Queue-Fairness, Alias-Mehrdeutigkeit, Concurrent-Dispatch.
- `ScmEventDispatchTests_AgentActions.cs`: Agent-Umgebungsinjektion (`GH_TOKEN`, extraheader — Z. 24–112), **selbst geschriebene `accepted.json`/`complete.json` ins `ExchangePath`-Verzeichnis** und Doppel-Dispatch zur Verarbeitung (Z. 117–199) — direkter Präzedenzfall für Tests, die Dateien im Austauschverzeichnis ablegen.
- `ScmEventDispatchTests_PromptAppendix.cs`: E2E der `promptSuffix`-Auflösung je Ereignisart (Z. 19–110).
- `ScmEventDispatchTests_Cancel.cs`, `_Delay.cs`, `_Devin.cs`, `_HostRetry.cs`: Abbruch, Verzögerungsdirektive, Devin-Startvertrag, Retry-Semantik.
- `ScmEventCliExchangeAccessTests.cs` (+ `.PendingProbe`, `.RestrictedProcess`, `ScmRestrictedWindowsProcess.cs`): echte Prozess-/ACL-gebundene Kanalprobe.
- `ScmEventMonitoringServiceTests_*`, `ScmEventPersistenceTests.cs`, `ScmBackmergeMonitoringServiceTests.cs`, `ScmTaskCompletionMonitoringServiceTests.cs`, `ScmEventDelayIntakeTests.cs`.

## E2E-Tests (`src/Softwareschmiede.Tests/E2E/`)

- `E2E_ScmEvents.cs` (771 Z.), `E2E_ScmRegressionSimulator.cs` (26 Z.), `ScmE2EFixture.cs`, `ScmE2EFixtureTests_RuntimeResult.cs` — FlaUI-gesteuerte App mit Simulator-Plugin; App-seitig `src/Softwareschmiede.App/Services/Testing/ScmE2ERuntimeControl.cs` (`runtime-*.command`-Steuerdateien, `--scm-exchange`-Sessionstart).
- CLAUDE.md-Regel: FlaUI-E2E-Tests konsolidiert halten (minimale Methodenzahl bei voller Abdeckung); reguläre Lane `dotnet test src/Softwareschmiede.Tests/...` mit Filter `Category!=OsInterface`.

## Dokumentationsorte

- `docs/help/plugins/github-ereignisse/` (9 Dateien): `index`, `beschreibung` (u. a. `report`-/`pullRequestNumber`-Semantik, Z. 19), `architektur` (Kanaladapter/Versionen, Z. 16/25/29), `ablauf-technisch` (Z. 46: Agentenanbindung + `report`-Feld; Z. 48: Probe + `promptSuffix`-Fluss; Z. 56: Abbruch), `ablauf-anwender`, `business-rules` (Z. 45–47: `scm.prompt.append.*`-Regeln), `datenmodell` (Z. 31: `pullRequestNumber` → `PullRequestReferenz`), `installation`, `troubleshooting`.
- `docs/help/einstellungen/datenmodell.md` (Z. 36–40) und `docs/help/einstellungen/installation.md` (Z. 66–70, 78): Schlüsseltabelle der `scm.prompt.append.*`-Einstellungen.
- Feature-Arbeitsverzeichnisse: `docs/features/task/<task>/` mit `requirement.md`, `inventory.md` + `inventory/` (Konvention, s. andere task-Ordner).
