# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-25, Läufe ab ca. 18:03 Uhr +0100 (lokale Rechnerzeit, Windows)
- **Branch und Commit-ID:** `task/issue-478-500850a8895d49c5a16f8ba99171b60e-remote-uebersicht-status-webse`, Commit `da9c51e689bf069b722eda4c930006787ad722a6` („Backmerge from main to staging (#477)")
- **Uncommittete Änderungen im getesteten Stand:** nur die unversionierten Feature-Dateien `docs/features/task/issue-478-…/` (requirement.md, todo.md, diese Inventory-Dateien); kein Produktiv-/Testcode verändert
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, .NET SDK 11.0.100 (installiert außerdem 10.0.404), Target `net11.0` / `net11.0-windows10.0.17763.0`, xUnit v3; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline pro `dotnet test`-Aufruf gesetzt (Sandbox-Auflage laut CLAUDE.md)
- **Ermittelte Testsuiten und Quellen der Testbefehle:** `CLAUDE.md` (Testing-Abschnitt) sowie die Lifecycle-Anweisung — stabile Lane `src/Softwareschmiede.Tests` mit `--filter "Category!=OsInterface"` und `src/Softwareschmiede.IntegrationTests` ohne Filter. Die `OsInterface`-Kategorie (E2E/FlaUI, ConPTY, Clipboard, echte Prozessstarts) ist eine separate, umgebungssensitive Lane und wurde nicht ausgeführt.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx` | Repo-Wurzel | 0 | – (0 Warnungen, 0 Fehler, 21,9 s) | – | – | [Log](test-results/dotnet-build-slnx-2026-11-25.log) |
| Unit | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | Repo-Wurzel | 0 | 2825 | 0 | 3 | [Log](test-results/dotnet-test-unittests-2026-11-25.log) |
| Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` | Repo-Wurzel | 0 | 449 | 0 | 0 | [Log](test-results/dotnet-test-integrationtests-2026-11-25.log) |

### Übersprungene Tests (Unit-Lane)

| Test-ID | Suite / Dateipfad | Grund | Nachweis |
|---------|------------------|-------|----------|
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/SystemShutdownServiceTests.cs` | Plattformbedingt (nicht-Windows-Pfad) | Unit-Log |
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/SystemShutdownServiceTests.cs` | Plattformbedingt | Unit-Log |
| `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` | `src/Softwareschmiede.Tests/Application/Services/` | Plattformbedingt | Unit-Log |

### Nachgewiesene bestehende Testfehler

Keine — beide ausgeführten Lanes sind fehlerfrei (0 Fehlschläge).

### Testlücken und Ausführungsprobleme

- **`OsInterface`-Kategorie nicht ausgeführt:** Die Filter-Lane `Category!=OsInterface` schließt alle E2E-Tests (FlaUI/WPF-Fenster, u. a. `src/Softwareschmiede.Tests/E2E/` mit ~50 `OsInterfaceFact`/`OsInterfaceTheory`-Methoden in 51 markierten Quelldateien), ConPTY-Tests (`E2E_ConPtyLifecycle` u. a.), Clipboard- und echte Prozessstart-/Terminal-Tests aus. Diese Lane ist per `CLAUDE.md` separat (`--filter "Category=OsInterface"`) und umgebungssensitiv; in dieser Sandbox schlagen die ConPTY-Tests bekanntermaßen fehl (daher `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`). Der Ausgangszustand dieser Suite ist **unbekannt** — dortige Fehler sind nicht als „preexisting" nachgewiesen.
- **Keine Tests für das neue Feature:** Es existiert noch kein `RemoteStatusRelayService`, keine Snapshot-Projektion, keine Instanz-ID und keine `status.remote.*`-Einstellungen — folglich auch keine Tests dafür.

## Testklassen (für das Feature relevante Bestandstests)

### `ScmIncomingEventStatusTests`
- Hält die persistierten Literalwerte der `ScmIncomingEventStatus`-Konstanten und die `NonBlocking`-Einteilung fest.

### `ScmEventValidationServiceTests`
- Akteurs-/Trigger-Validierung: Owner-Identität, `TrustedAutomationLogins`, `ViewerHasReacted`-Duplikate, Assign-Auslöser, Keyword-Normalisierung — direkt relevant für die Kommentar-Aktions-Frage (Identität der servergeschriebenen Kommentare).

### `ScmEventOrchestratorTests` / `ScmEventDispatchTests*` (IntegrationTests)
- Dispatch-Pfade, Wartegründe, Priorisierung, Retry/Reconcile, Abbruch, Limits, Prompt-Anhang, Stabilitätsfrist — decken den Ereigniskanal ab, über den Kommentar-Aktionen einfließen.

### `ScmEventMonitoringServiceTests_*` (IntegrationTests)
- Repository-Auflösung, Alias-Gruppen, repository-weite Enumeration, Inbox/Notifications, Assignments, Issue-Aufbereitung, Einstellungs-Schalter (`scm.monitoring.enabled`) — Muster für „Schalter deaktiviert ⇒ Durchlauf übersprungen".

### `ScmEventPersistenceTests` (IntegrationTests) / `ScmEventExchangeServiceTests` / `ScmEventQueueViewModelTests`
- Inbox-Persistenz, Antwortkanal-/Outbox-Verarbeitung, Ereignisübersicht-ViewModel.

### `AppSettingServiceTests_*`
- `_ScmMonitoring`, `_AccountMode`, `_Automation`, `_Backmerge`, `_IssuePreparation`, `_AutoShutdown`, `_UpdateSettings` — Konventionsvorlage für neue `GetXxxAsync`-Schlüssel (Defaults, Parsing, Klemmung, dynamische Keys).

### `WorkItemServiceTests` / `WorkItemServiceTests_ActiveRun` / `WorkItemServiceTests_Pause` / `WorkItemServiceTests_IssueEvents`
- Aufgaben-Lebenszyklus, `ActiveRunId`/Heartbeat-Verwaltung, `PausedUntilUtc`, ereignisgesteuerte Anlage.

### `WorkItemRecoveryServiceTests` (Unit + IntegrationTests) / `WorkItemRunActivityTests`
- Heartbeat-Timeout-Regel (`HeartbeatTimeoutMinutes = 5`), Recovery-Kandidaten, „läuft aktiv"-Klassifikation.

### `PullRequestMonitoringServiceTests` / `PullRequestReferenceServiceTests`
- PR-Polling und Referenz-Persistenz (appseitiger PR-Bestand).

### `UpdateServiceTests*` / `ApplicationVersionProviderTests` / `GitHubReleaseClientTests*` / `UpdateVersionComparerTests*`
- Versionslesen, Release-Client (HttpClient-Handler-Mocks), Vergleichs-/Kanal-Logik.

### `SettingsViewModelTests*`
- Einstellungs-ViewModel inkl. SCM-Monitoring-Schalter, Execution-Limit, Issue-Aufbereitung — Vorlage für einen Remote-Übersicht-Abschnitt.

### `AiExecutionServiceTests*` / `CliUpdateSafetyServiceTests` / `RunningAutomationsCount`-nahe Tests (`AutoShutdownOrchestrator`-Tests)
- Laufzeitstatus, Live-Run-Status, Running-Count-Klassifikation.

## Hilfsmethoden

### `TestDbContextFactory` (`src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`)
- `Create()` — frische EF-InMemory-DB je Test.
- `CreateSqlite(connectionString, interceptors)` — echte SQLite-DB, optionale EF-Interceptoren.

### `ScmTestServices` (`src/Softwareschmiede.Tests/Helpers/ScmTestServices.cs`)
- `AddScmTestServices` — registriert die SCM-Dienstlandschaft (Orchestrator, Store, Monitoring) in einem Test-`ServiceCollection`.

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`)
- Baut Aufgabe + SCM-Ereignis + Mock-CLI/Plugins; `RunMonitorAsync`, `WaitForTriggeredDeliveryAsync`, `ConfigureMentionObservations`/`ConfigureDescriptionObservations`, `SetAccountModeAsync`, `AdvancePastStabilityPeriod`; `TimeProvider`-Steuerung.

### `TestTimeProvider` (`src/Softwareschmiede.Tests/Helpers/TestTimeProvider.cs`)
- Steuerbarer `TimeProvider` für `PeriodicBackgroundService`-basierte Tests — Vorlage für Heartbeat-/Reconnect-Tests.

### `FakeGitHubEventApi` / `StaticHttpHandler` / `RoutingHttpHandler` / `DelegateHandler`
- HTTP-Handler-Mocks für GitHub-API-abhängige Tests (`GitHubReleaseClientTests`, Event-Source-Tests).

### `TestAiExecutionServiceFactory` / `TestTerminalSessionFactory` / `TestPseudoConsoleSessionFactory`
- Fabriken für CLI-Laufzeit-Mocks (Sessions, Status-Events).

### `CredentialStoreSnapshot` (`src/Softwareschmiede.Tests/Infrastructure/Services/`)
- Sichert/stellt `ICredentialStore`-Inhalte für Tests wieder her — Vorlage für Relay-Token-Tests.

### `UpdateE2EFixture` / `ScmE2EFixture` / `WpfTestBase`
- E2E-Fixtures (ersetzen `IUpdateReleaseClient`/`ICredentialStore`/Terminal per Test-Konfiguration) für die nicht ausgeführte `OsInterface`-Lane.
