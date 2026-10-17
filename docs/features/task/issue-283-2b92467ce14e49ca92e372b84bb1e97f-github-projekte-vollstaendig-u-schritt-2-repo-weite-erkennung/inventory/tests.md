# Tests und Testinfrastruktur

Zurück: [../inventory.md](../inventory.md)

## Testklassen (bestehend)

### `GitHubEventSourceTests` — `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`
Reale `GitHubPlugin`-Logik gegen `FakeApi` (`IGitHubEventApiClient`-Fake mit Queue aus Endpunkt-Matches, `api.Requests`-Protokoll, `AssertComplete`). Deckt Description/Comments/Reviews/Pagination/Fehlertypisierung ab. **Direkt erweiterbar** für eine neue Enumerationsmethode (GraphQL-Fakes via `api.AddGraph(...)`).

### `ScmEventOrchestratorTests` — `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs`
Nutzt `ScmDispatchFixture`; testet Dispatch-Pipeline, keine Monitor-Aufzählung.

### `ScmEventValidationServiceTests`, `ScmEventExchangeServiceTests`, `ScmEventQueueViewModelTests`, `ScmCliWorkspaceTrustTests` — `src/Softwareschmiede.Tests/Application/Services/`
Validierungsregeln, Exchange-Reconcile, Queue-UI, Trust-Vorwegnahme (Schritt 1).

### `E2E_ScmEvents` / `E2E_ScmRegressionSimulator` — `src/Softwareschmiede.Tests/E2E/`
FlaUI-E2E mit `ScmE2EFixture` (KiSimulator, echter App-Prozess). Für diesen Schritt voraussichtlich **kein** neuer E2E nötig (Zwischenstufe ohne UI-Änderung); Unit-/Integrationstests ausreichend.

## Hilfsklassen

### `ScmDispatchFixture` — `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`
- Reale SQLite-DB (`EnsureCreated`), echte Services (`ScmEventStore`, `ScmEventOrchestrator`, `ScmEventMonitoringService` als Singleton registriert, Z. 95).
- `Git` = `Mock<IGitPlugin>` mit `As<IScmEventSource>()`-Setups für `GetEventRepositoryAsync` (→ `Repository` mit `node_id R_1`, Viewer=Owner) und `ReadEventsAsync` (→ leerer Batch, überschreibbar) (Z. 41-45). Neue Interface-Member benötigen hier zusätzliche `As<IScmEventSource>()`-Setups.
- `AddAsync` legt `Projekt`+`GitRepository`+`Aufgabe`+Inbox-Event an (Z. 119-134).
- `ScmEventStartupGate` wird in `InitializeAsync` via `Ready()` freigegeben — `RunOnceAsync` ist damit direkt aufrufbar.

### `TestDbContextFactory` — `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`
Für DbContext-basierte Tests ohne vollständige Fixture.

## Lücken

- **Kein** Test ruft `ScmEventMonitoringService.RunOnceAsync` auf — weder die Referenzlisten-Bildung noch Checkpoint-/Backoff-Verhalten sind auf Service-Ebene getestet. Neue Tests für die repo-weite Erweiterung müssen diese Ebene erst erschließen (`ScmDispatchFixture` liefert die Infrastruktur).
- `FakeApi` in `GitHubEventSourceTests` ist `private` — neue Enumerations-Tests in derselben Datei oder Wiederverwendung des Musters.

## Konventionen (CLAUDE.md)

- Stabile Lane: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`, inline `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`.
- Voller Build vor Tests; `dotnet test` niemals im Hintergrund.
- Test-Kategorien: `Category=OsInterface` separat.
