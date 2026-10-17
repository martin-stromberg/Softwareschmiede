# Bestandsaufnahme — Tests und Testinfrastruktur

Zurück: [../inventory.md](../inventory.md)

## Testprojekte

| Projekt | Zweck |
|---------|-------|
| `src/Softwareschmiede.Tests` | Unit-Tests + FlaUI-E2E (`[OsInterface]`); stabile Lane `--filter "Category!=OsInterface"` |
| `src/Softwareschmiede.IntegrationTests` | Service-Integrationstests mit echter SQLite (`ScmDispatchFixture` oder `DatabaseFixture` mit `MigrateAsync`) |

## Testklassen (relevant)

### `ScmEventMonitoringServiceTests_RepoWide` (`src/Softwareschmiede.IntegrationTests/Services/`)

- `Monitor_ActiveRepositoryWithoutTask_EnumeratesAndStoresSnapshotsWithoutInbox` — Repo ohne Aufgabe → Snapshots `UnassignedDiagnostic`, keine Inbox.
- `Monitor_EnumeratedReferenceWithTask_EntersInboxUnchanged` — verknüpfte + unverknüpfte Referenz nebeneinander.
- `Monitor_RepeatedPolls_ProduceNoDuplicates`
- `Monitor_InactiveOrNonGitHubRepositories_AreNotEnumerated`
- `Monitor_EnumerationFailure_UsesBackoffWithoutBlockingLinkedReferences`
- Hilfen: `AddRepositoryAsync`, `AddTaskWithIssueAsync`, `ConfigureEnumeration`, `ConfigureObservations` (qualifizierter IssueComment `"$Softwareschmiede test"` von `U_1`/`owner`).

### `ScmEventMonitoringServiceTests_AmbiguityRecovery`, `ScmEventDispatchTests`, `ScmEventDispatchTests_HostRetry`, `ScmEventPersistenceTests`

Dispatch-, Ambiguitäts- und Persistenzverhalten über `ScmDispatchFixture`; `AcceptBatchAsync` wird auch direkt aufgerufen.

### `GitHubEventSourceTests` (`src/Softwareschmiede.Tests/Infrastructure/Plugins/`)

`FakeApi` (`IGitHubEventApiClient`-Fake mit `Add`/`AddGraph`/`AddStatus`/`AddFailure`, `Requests`-Protokoll, `AssertComplete`); Tests für alle `IScmEventSource`-Methoden inkl. Enumeration (Schritt 2).

### `AufgabeServiceTests` (`src/Softwareschmiede.IntegrationTests/Services/`)

Service-Tests auf echter SQLite.

### `E2E_ScmEvents` (`src/Softwareschmiede.Tests/E2E/`)

`ScmEventsE2ETests` ([OsInterface], Collection `E2E`) — konsolidierte FlaUI-Methoden; `ScmE2EFixture` legt Projekt + Repository + 17 Aufgaben mit `IssueReferenz` (Nr. 1-17, außer 7/9 = `PullRequestReferenz`) an; `AddText(number, id, body, kind)` speist Szenario-Texte; `WaitForPollAsync` erwartet ≥ +8 Einträge in `CallsPath`; `WaitEventAsync` pollt Inbox-Status.

## Hilfsklassen

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/`, Namespace `Softwareschmiede.TestSupport`)

- `Mock<IGitPlugin> Git` mit `As<IScmEventSource>()`-Defaults: `GetEventRepositoryAsync` → `Repository` (test-scm/github.com/R_1/owner/repo, Owner = Viewer `U_1`/`owner`), `ReadEventsAsync` → leerer Batch, `EnumerateReferencesAsync` → leer, `IScmEventResponder.ExecuteResponseAsync` → `Responses`-Liste.
- Echte SQLite (`UseSqlite`, `EnsureCreatedAsync` — Modellzustand, keine Migrationen nötig), `ScmEventStartupGate`, `IServiceProvider` mit `ScmEventMonitoringService`, `ScmEventStore`, `ScmEventOrchestrator`, `AufgabeService`, `EntwicklungsprozessService`, Terminal-Mocks.
- `AddAsync(status, configured, cloned)` — Aufgabe + Inbox-Ereignis in einem Schritt.

### `DatabaseFixture` (`src/Softwareschmiede.IntegrationTests/Infrastructure/`)

SQLite mit `MigrateAsync` — echtes Migrationsschema; `WorkdirMigrationTests` testen einzelne Migrationen auf- und abwärts.

## Konventionen

- Testklassen-Themenaufteilung: `{Klasse}Tests_{Thema}` in eigenen Dateien.
- FlaUI-E2E: minimale, konsolidierte Testmethoden; Sandbox erfordert `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`.
