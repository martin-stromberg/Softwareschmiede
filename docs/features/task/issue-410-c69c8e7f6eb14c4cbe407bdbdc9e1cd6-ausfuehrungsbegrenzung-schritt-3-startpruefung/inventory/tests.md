# Inventory: Test-Infrastruktur (Schritt 3)

## Projekte und Lanes

- `src/Softwareschmiede.Tests` — Unit-/App-Tests, stabile Lane:
  `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`
  (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` in dieser Sandbox obligatorisch, synchron im Vordergrund).
- `src/Softwareschmiede.IntegrationTests` — SQLite-echte Integrationstests, verlinkt
  `ScmDispatchFixture.cs` (`<Compile Include="..\Softwareschmiede.Tests\Helpers\ScmDispatchFixture.cs" Link=…>`),
  keine Trait-Kategorien → kompletter Projekt-Lauf.
- Baseline-Referenz (Schritt-2-Abschluss): regulär 2639/2642 (3 plattform-Skips),
  Integration 322/322, OS-Interface 66/71 (5 ConPTY-Skips).

## `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`)

- SQLite-DB je Fixture (`EnsureCreated`), echte Services; `InitializeAsync` setzt
  `ScmEventStartupGate.Ready()`.
- `AddAsync(status, configured, cloned, …)` → `(TaskId, EventId)`: legt Projekt+Repo+Aufgabe
  + Observation über `ScmEventStore.AcceptBatchAsync` an.
- Terminal-Grenze: `ITerminalSessionFactory`-Mock startet echten
  `cmd.exe /c ping -n 300`-Prozess (kein ConPTY → sandbox-tauglich), Sessions unter
  `Sessions[taskId]`, `StartSpecs`, `Prompts`, `Responses` beobachtbar.
- `Monitor` → `ScmEventMonitoringService` (Singleton; `RunOnceAsync` direkt aufrufbar);
  `Cli` → `KiAusfuehrungsService` (`StopCliAsync`, `GetScmPromptSession` …);
  `DispatchAsync(id)` → Orchestrator-Dispatch in neuem Scope;
  `SetSettingAsync(key, wert)` → `AppEinstellungService`.
- Ctor `ScmDispatchFixture(root, time, ki, ki2)` akzeptiert `TimeProvider` (für Fake-Zeit
  injizierbar; bisherige Limit-Tests nutzen `TimeProvider.System`).
- Registrierungslücken für Schritt 3: `IRunningAutomationStatusSource` und der neue
  Auslöser/Trigger-Dienst sind im Fixture noch nicht registriert (nachzurüsten).

## Manuelle Konstruktionen (Kompatibilitätszwang für optionale Ctor-Parameter)

- `new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(…))`
  — ~30 Stellen (`AufgabeServiceTests*`, `EntwicklungsprozessServiceTests`,
  `ProjektServiceTests`, `ProtokollServiceTests`, `TodoServiceTests`,
  `AufgabeRecoveryServiceTests` u. a.).
- `new ScmEventStore(db, new(), TimeProvider.System)` — ~11 Stellen
  (`ScmEventDelayIntakeTests`, `ScmEventPersistenceTests`).
- `new KiPluginLimitService(…)` — 3 Stellen (`KiPluginLimitServiceTests`,
  `CliUpdateSafetyServiceTests`, `E2E_SessionLimitPause`).
- ⇒ Neue Abhängigkeit auf diesen drei Klassen ausschließlich als **optionaler
  Konstrukturparameter** (`… = null`), damit alle Bestandsaufrufe unverändert kompilieren;
  `ActivatorUtilities` injiziert den Singleton, sobald er registriert ist.

## Relevante Bestandstests

- `ScmEventDispatchTests_Limits` (Integration): Limit-/Wartegrund-/Priorisierungsfälle;
  `Dispatch_SlotFreed_WaitingEventStarts` zeigt Slot-Nachrücken — bisher über manuellen
  Zweit-Dispatch, Schritt-3-Tests ersetzen das durch den Trigger-Pfad.
- `PeriodicBackgroundServiceTests` (Unit): Muster für Hintergrunddienst-Tests.
- Keine `FakeTimeProvider`-Nutzung in `Softwareschmiede.IntegrationTests` bisher
  (Paket `Microsoft.Extensions.TimeProvider.Testing` nur im Unit-Projekt);
  Trigger-Tests arbeiten mit realem kurzem Settle-Zeitfenster + Wait-Loop.
