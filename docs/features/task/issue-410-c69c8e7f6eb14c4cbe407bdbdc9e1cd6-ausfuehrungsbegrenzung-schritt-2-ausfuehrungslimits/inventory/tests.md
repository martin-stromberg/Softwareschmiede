# Tests

## Baseline (aktueller Branch, HEAD)

| Suite | Konfiguration | Befehl | Ergebnis | Bericht |
|-------|--------------|--------|----------|---------|
| Softwareschmiede.Tests | Debug, Sandbox: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | ✅ **2591 erfolgreich / 0 Fehler / 3 übersprungen** (gesamt 2594, ~3 m 13 s) | [baseline-tests.log](test-results/baseline-tests.log) |
| Softwareschmiede.IntegrationTests | Debug, Sandbox: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` | ✅ **302 erfolgreich / 0 Fehler / 0 übersprungen** (gesamt 302, ~37 s) | [baseline-integrationtests.log](test-results/baseline-integrationtests.log) |

`dotnet test` enthält den vollständigen Build (kein `--no-build`). Übersprungene Tests sind die plattformabhängigen `SystemShutdownServiceTests` (Linux/macOS) — kein Mangel.

## Bestehende Infrastruktur

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` — echter SQLite-DB-Kontext, geteiltes Link-Item auch in `Softwareschmiede.IntegrationTests`. Exponiert u. a.: `Repository` (Mock `IScmEventRepository` mit `ScmAction`, `Publish`, `GetRepositoryRootPath`, `GetNewCommentsAfter`), `KiPlugin` (Mock `IKiPlugin`, Default-Prefix `Softwareschmiede.KiSimulator`), `ScmPlugin`, `Terminal`, `TaskCoordinator`, `PluginManager`, `PluginSelection`, `PluginDefaults`, `EventStore`, `Notifier`, `DbOptions`; Methoden `Scope()`, `EnsureTask(...)` (u. a. `ausfuehrungsStatus`, `pausiertBisUtc`), `AddEvent(...)` (u. a. `status`, `sequence`, `nextAttemptUtc`), `DispatchAsync`, `AddScmPlugin`, `AddTerminalSession`, `SetSetting`. Sequenz-`Mock<IScmPromptSessionAdapter>` ist umschaltbar (`ConfigureAdapterAlive` o. ä.) — Dispatch-Tests können Start vs. „Sitzung lebt" steuern.

→ Erweiterung nötig: (a) Registrierung des neuen Limit-Dienstes im Scope; (b) Option, den `KiAusfuehrungsService` im Fixture durch eine zählbare Testdouble-Quelle zu ersetzen oder echte Ping-Prozesse über `StartCliAsync` zu starten (Muster: `KiAusfuehrungsServiceTests`).

### Bestehende relevante Tests

| Bereich | Datei | Was abgedeckt ist |
|---------|-------|--------------------|
| Orchestrator | `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs` | Standard-Plugin-Start+Zustellung; nicht auflösbares Plugin → `Wartend` ohne `long.MaxValue`-Sperre; Aufgaben-Plugin deaktiviert → aktivierter Fallback (Schritt 1); expliziter Prefix strikt; Beendet/Archiviert → BlockReason; abgelaufene Pause startet; `OpenFromQueueAsync` Sperrfälle. |
| Dispatch/Store (Integration) | `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests*.cs`, `ScmEventMonitoringServiceTests*.cs` | Polling-Dispatch, Zustellung, Wartegründe, Outbox, Auto-Start. |
| Priorisierung | `src/Softwareschmiede.Tests/Application/Services/ScmDispatchPrioritaetTests.cs` | Klassen 1/2/3-Reihenfolge inkl. `NextAttemptUtc`-Sortierung, Beendet/Archiviert-Ausschluss, fehlende Aufgabe → Klasse 3, echter `ScmEventStore`-Pfad. |
| Limit-Service | `src/Softwareschmiede.Tests/Application/Services/KiPluginLimitServiceTests.cs` | Rate-Limit-Pause-Mechanik (Schlüsselkollision `plugins.sessionlimit.*` belegt!). |
| SettingsViewModel | `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests*.cs` | Pflichtfeld-/Integer-Validierung, `ScmTaskCompletionWaitMinutes`-Muster, `LadePluginEinstellungen`/`SpeicherePluginEinstellungen`-Mechanik. |
| E2E Einstellungen | `src/Softwareschmiede.Tests/E2E/E2E_SettingsFeatureFlags.cs`, `E2E_SettingsKiPluginPersistence.cs` | Strukturtest, Plugin-Persistenz über `ICredentialStore` + `OpenTestDbContext`-Assertion. |

### Lücken

- Keine Tests für parallele CLI-Begrenzung (global/pro Plugin), Plugin-Attribution der Läufe, Limit-Prioritätsinteraktion, UI-Persistenz der neuen Felder, Limit-E2E.
- `ScmEventMonitoringService`-Batch-Reihenfolge ist nicht auf Priorisierung getestet (bisher nur Sequence).
