# Bestandsaufnahme

## Überblick

Der Geschäftsvorgang „Aufgabe abschließen" besteht aus bis zu fünf Schritten, die
heute von den Aufrufern unterschiedlich komponiert werden:

1. `KiAusfuehrungsService.StopCliAsync` — CLI-Prozess beenden (SIGTERM → 5 s → Kill)
2. `PromptZeitVersandService.CancelScheduledPrompt` — geplanten Prompt zurückziehen
3. `AufgabeService.CanCompleteTaskAsync` — Offene-To-do-Prüfung
4. Klon-Verzeichnis löschen (`DeleteDirectoryForce` in `EntwicklungsprozessService`)
5. `AufgabeService.AbschliessenAsync` + `ProtokollService.AddStatusUebergangAsync`

`EntwicklungsprozessService.AbschliessenAsync` deckt heute nur die Schritte 3–5 ab
und nimmt die `ScmTaskCoordinator`-Aufgabensperre **nicht** selbst — im Gegensatz zu
`ProzessStartenAsync`, `ProzessStartenUndCliStartenAsync` und `CliNeustartenAsync`
im selben Service.

## Aufrufer-Kompositionen (Ist-Zustand)

| Aufrufer | Komposition |
|---|---|
| `TaskDetailViewModel.AufgabeAbschliessenAsync` (TaskDetailViewModel.cs ~Z. 1069) | `CanCompleteTaskAsync` → `CancelScheduledPrompt` → `AbschliessenAsync`; ohne Sperre, CLI-Stopp nur indirekt über `KannAufgabeAbschliessen` (`!_isCliRunning`) |
| `ScmTaskCompletionMonitoringService.CheckTaskAsync` (~Z. 112–139) | `ScmTaskCoordinator`-Sperre → Revalidierung → `StopCliAsync` → `AbschliessenAsync`; ohne `CancelScheduledPrompt` |
| `ScmE2ERuntimeControl` (E2E-Testinfrastruktur, App/Services/Testing) | Sperre → `StopCliAsync` → `StartTerminalSessionAsync` — **CLI-Sitzungs-Ersatz, kein Aufgaben-Abschluss** |

## Koordinationsmuster

`ScmTaskCoordinator.Shared.RunAsync(taskId, operation, ct)` serialisiert
prozessweit pro Aufgabe über `SemaphoreSlim`-Gates; ein `AsyncLocal`-Besitzer-Set
macht die Sperre reentrant (verschachtelte Aufrufe desselben Tasks laufen direkt).
Bereits koordinierte Einstiege in `EntwicklungsprozessService`:
`ProzessStartenAsync` → `ProzessStartenCoordinatedAsync`,
`ProzessStartenUndCliStartenAsync` → `ProzessUndCliCoordinatedAsync`,
`CliNeustartenAsync` → `CliNeustartenCoordinatedAsync`.

Weitere Nutzer der Sperre: `ScmEventOrchestrator` (Dispatch), `ScmPromptSessionAdapter`,
`ScmCoordinatedTerminalSession`, `KiAusfuehrungsService` (Z. 120/215),
`ScmTaskCompletionMonitoringService`, `ScmE2ERuntimeControl`.

## Injektion

- `EntwicklungsprozessServiceOptions` (record): optionale Singleton-/Scoped-Dienste
  (`ProjektService`, `RepositoryStartskriptService`, `RepositoryInitialisierungService`,
  `KiAusfuehrungsService`, `GitOrchestrationService`).
- `PromptZeitVersandService` ist als Singleton registriert (`App.xaml.cs` Z. 280)
  und damit wie `KiAusfuehrungsService` in die Options injizierbar.
- DI-Erzeugung des `EntwicklungsprozessService`: `App.xaml.cs` Z. 220–232 (scoped,
  Options mit allen fünf Diensten).
- Weitere Options-Konstruktionen: `TaskDetailViewModelTestFactory` (nur
  `KiAusfuehrungsService`), `ScmDispatchFixture` (nur `KiAusfuehrungsService`),
  diverse Test-`CreateSut`-Pfade mit Teilbelegung oder leeren Options.

## Detaildokumente

- [`inventory/abschluss-pfad.md`](inventory/abschluss-pfad.md) — Ist-Ablauf
  `EntwicklungsprozessService.AbschliessenAsync`, Monitoring-Pfad und
  Sperr-Semantik im Detail.
- [`inventory/prozess-starten-sichtbarkeit.md`](inventory/prozess-starten-sichtbarkeit.md) —
  Aufrufer von `ProzessStartenAsync` (nur Tests) und `InternalsVisibleTo`-Lage.

## Relevante Tests (Ist)

- `Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs` —
  `AbschliessenAsync_ShouldSetStatusAbgeschlossenAndAddProtokoll_WhenAufgabeExists`
  (Status + Statusübergang-Protokoll).
- `Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs` —
  `AbschliessenAsync_ShouldSetStatusBeendetAndCreateStatusUebergang_WhenAufgabeInBearbeitung`.
- `Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs` —
  `Monitor_MergedPullRequest_StopsCliAndCompletesTaskAfterWait` u. a. (echte
  Session-Prozesse über `ScmDispatchFixture`).
- `Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_Todos.cs` —
  `AufgabeAbschliessenCommand_WithOpenTodos_ShowsError` (Meldung, kein Abschluss)
  und `AufgabeAbschliessenCommand_WithoutOpenTodos_Succeeds`.
- `Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs` —
  `CancelScheduledPrompt`-Verhalten.
