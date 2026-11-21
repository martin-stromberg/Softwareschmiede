# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-21, ab ~19:10 Uhr (+01:00, Windows/Europe-Berlin)
- Branch und Commit-ID: `task/issue-431-1102930a65ae4b2cb1c6afda71922ba5-ausfuehrungsbegrenzung-platz-b` @ `73182399a322fec7643711093d694435ff930b01`
- Uncommittete Änderungen im getesteten Stand: nur untracked Feature-Dokumente
  (`docs/features/task/issue-431-…-ausfuehrungsbegrenzung-platz-b/` mit `requirement.md`, `todo.md`);
  keinerlei Änderungen an Produktiv- oder Testcode.
- Testumgebung und Runtime-/SDK-Versionen: Windows 11 (Git-Bash), .NET SDK 11.0.100,
  Zielframework `net11.0` (App/Tests: `net11.0-windows10.0.17763.0`), xUnit.net v3
  (VSTest Adapter 4.0.1), Moq, FluentAssertions; Testprojekte erfordern das .NET-Desktop-Workload.
- Ermittelte Testsuiten und Quellen der Testbefehle: CI-Workflow `.github/workflows/pr-staging-ci.yml`
  (Z. 128, 132, 222, 226) definiert vier Lanes — regulär und `Category=OsInterface` für
  `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests`. Die regulären Lanes entsprechen
  der stabilen Testlane aus `CLAUDE.md`; die OsInterface-Lanes umfassen E2E/ConPTY/Clipboard/
  Prozess-Tests und sind umgebungssensitiv.
- Build vor den Tests: `dotnet build Softwareschmiede.slnx -c Debug` — erfolgreich, 0 Warnungen,
  0 Fehler (~27 s).
- Für beide Läufe wurde `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (Sandbox-Vorgabe aus
  `CLAUDE.md`; betroffene ConPTY-Tests melden damit ehrlich „Skipped" statt Timeout).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=baseline-tests-regular.trx" --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 2744 | 0 | 3 | [TRX](test-results/baseline-tests-regular.trx), [Log](test-results/baseline-tests-regular.log) |
| 2 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=baseline-integration-regular.trx" --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 360 | 0 | 0 | [TRX](test-results/baseline-integration-regular.trx), [Log](test-results/baseline-integration-regular.log) |

Lauf 1 Gesamtdauer ~3,66 min (2747 Tests gesamt); Lauf 2 ~47 s (360 Tests).

### Nachgewiesene bestehende Testfehler

Keine. In beiden regulären Lanes schlug kein Test fehl.

Die 3 Übersprungenen in Lauf 1 (Outcome `NotExecuted` im TRX) sind plattformspezifisch
und ohne Bezug zur Anforderung:

| Test-ID | Suite | Grund |
|---------|-------|-------|
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/` | macOS-spezifisch; auf Windows übersprungen. |
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/` | Linux-spezifisch; auf Windows übersprungen. |
| `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` | `src/Softwareschmiede.Tests/Application/Services/` | Nicht-Windows-Test; auf Windows übersprungen. |

### Testlücken und Ausführungsprobleme

- **`Category=OsInterface`-Lanes nicht ausgeführt** (beide Testprojekte): umfassen WPF-E2E-Tests
  (FlaUI), ConPTY-Prozessstarts, Clipboard- und reale Prozess-/Dateisystem-Interaktionen.
  Sie sind in dieser Sandbox bekannt umgebungssensitiv (kein interaktiver Desktop; die 14
  ConPTY-E2E-Tests laufen laut `CLAUDE.md` nur mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
  als „Skipped"). Relevante betroffene Dateien im Feature-Bereich: `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`
  (`[OsInterface]`), `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.RestrictedProcess.cs`
  (`Trait Category=OsInterface`). Aus dem Ausgangszustand dieser Suite kann daher keine Aussage
  abgeleitet werden; Fehler dort gelten nicht als „preexisting" nachgewiesen.
- **Feature-Lücke (fachlich):** Kein bestehender Test prüft eine Platzfreigabe anhand von
  `ReadyStatus == Ready` oder eines Ereignis-Pending-Guards in `GetOccupancyAsync` — die Regel
  existiert noch nicht; alle Limit-Tests decken ausschließlich Limits, Reservierungen und die
  Pausen-Freigabe ab.

## Testklassen

### `ScmEventExecutionLimitServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventExecutionLimitServiceTests.cs`
(20 Methoden; `Mock<ICliExecutionLimitSource>` + `Mock<IScmPromptSessionAdapter>`,
In-Memory-DbContext über `TestDbContextFactory`)

- `GetStartBlockReasonAsync_WithoutBoundary_ReturnsNull` — ohne Limits immer frei.
- `GetStartBlockReasonAsync_InvalidBoundary_AppliesAsUnlimited` (Theory) — 0/negativ/Text = unbegrenzt.
- `GetStartBlockReasonAsync_GlobalLimitReaches_Locks` / `_GlobalLimitNotReaches_ReturnsNull` — globales Limit.
- `GetStartBlockReasonAsync_PluginLimitReaches_LocksOnlyThisPlugin` — Plugin-Limit sperrt nur eigenes Prefix.
- `GetStartBlockReasonAsync_BothLimitReaches_ReportsGlobalLimit` — globales Limit meldet zuerst.
- `GetStartBlockReasonAsync_MixedPlugins_CountsOnlyOwnPrefix` — getrennte Plugin-Zählung.
- `GetStartBlockReasonAsync_LiveSession_CountsActualPlugin` — lebende Session attribuiert zum tatsächlichen Plugin.
- `GetStartBlockReasonAsync_SessionDead_FallsOnWorkItemsPrefixBack` — tote Session → `AiPluginPrefix`-Fallback.
- `GetStartBlockReasonAsync_WithoutPrefix_CountsOnlyGlobal` — prefixlose Aufgabe zählt nur global.
- `GetStartBlockReason_ChecksGlobalBeforePluginLimit` — reine Auswertung (internal static).
- `ForPlugin_IgnoresLargeLowercase` — Prefix-Vergleich OrdinalIgnoreCase.
- `CheckAndReserveStartSlotAsync_WithoutBoundary_FreeWithoutReservation` — unbegrenzt ohne Reservierung.
- `CheckAndReserveStartSlotAsync_Reservation_OccupiedGlobalSlot` / `_OccupiedOnlyOwnPluginLimit` — Reservierung belegt Platz.
- `CheckAndReserveStartSlotAsync_AttemptsParallel_OnlyAReservation` — Atomarität: 8 parallele Services → 1 Reservierung.
- `CheckAndReserveStartSlotAsync_Reservation_DecaysAfterLifetime` — Verfall nach 5 min (`FakeTimeProvider`).
- `GetStartBlockReasonAsync_PausedWorkItem_ReturnsGlobalSlotFree` / `_ReturnsPluginSlotFree` / `_PausedWorkItemWithoutPrefix_OmittedOutBothCount` — **Pausen-Freigabe** (Vorbildmuster der neuen Regel).
- `GetStartBlockReasonAsync_ExpiredPause_CountsAsOccupied` — abgelaufene Pause zählt voll.
- `GetStartBlockReasonAsync_MixedPausedAndActive_CountsOnlyActive` — gemischte Belegung.
- `CheckAndReserveStartSlotAsync_PausedRunAndOpenReservation_ReservationOccupiedSlot` — Reservierung > Pausenfreigabe.

Hilfsmethoden der Klasse: `RunningWorkItemAsync` (Aufgabe + Session-Mock verdrahten),
`SetRunning` (Handle-IDs setzen), `LiveSession(prefix)` (`IsAlive=true`, `PluginPrefix` —
`ReadyStatus` wird nicht gesetzt → Moq-Default `Unknown`).

### `CliProcessManagerTests_RunStatus`
Datei: `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_RunStatus.cs`
(3 Methoden; Fake-`PseudoConsoleSession` via Reflection in `_handles`)

- `FullCycle_StartedWaitsStopped_PersistedRunStatusOverTheSession` — RunStatus-Persistenz über `OnRuntimeStatusChanged`.
- `RaisesRuntimeStatusChanged_AfterStopped_UpdatesRunStatusNotMore` — Abmeldung nach Stopp.
- `OnRuntimeStatusChanged_RepairsMissingActiveRunId` — Selbstheilung inkl. `WaitingForInput`-Substatus;
  ruft `OnRuntimeStatusChanged` per Reflection — natürlicher Erweiterungspunkt für den Trigger-Test.

### `ScmStartCheckServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmStartCheckServiceTests.cs` (4 Methoden)

- `Trigger_SignalWillAssumed` — `RequestStartCheck` koalesziert zu einer ausstehenden Prüfung.
- `Runcount_OnlyDeclineTriggersOut` / `_DeclineOnNullTriggersOut` — nur sinkende Laufzahl löst aus.
- `Dispose_DisconnectsSubscriptions` — Abmeldung nach Dispose.

### `ScmEventOrchestratorTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs`
(14 Methoden, `ScmDispatchFixture` mit echtem `ScmPromptSessionAdapter` an `Mock<ITerminalSession>`)

- `TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically` — `ReadyStatus.TrustPrompt` blockiert, `SetPrompt("SCM_READY_V1")` liefert zu.
- `Confirmation_ChannelFailureThenManualInput_DoesNotAuthorizeLaterUnknownPrompt` — `WriteInputAsync` → `MarkInputStarted` → `Unknown`.
- `ConfirmReady_LiftsNextAttemptLock`, `ConfirmReady_UnsupportedVersion_DeliversAfterChannelProbe` — manuelle Bestätigung.
- `UnknownCliFormat_WaitsWithoutConfirmOption` — unbekanntes Format bleibt `Unknown`.
- u. a. Dispatch-Guards (Write-Access, pausierte/beendete Aufgaben via `[Theory]`).

### `ScmEventDispatchTests_Limit` (Integrationstests)
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Limit.cs` (13 Methoden)

- `Dispatch_GlobalLimitReached_EventWaitsWithReason` / `Dispatch_SlotFreed_WaitingEventStarts` — globales Limit + Nachrücken.
- `Dispatch_PluginLimitReached_OnlyThatPluginWaits` / `Dispatch_BothLimit_GlobalDecidesWhenReached` — Plugin-/Kombinationslimits.
- `Dispatch_ReconcileAttempt_IgnoresLimit` — Reconciliation unbegrenzt.
- `Dispatch_EndedOrArchivedTask_StaysBlocked` (Theory) — Lebenszyklus-Sperre.
- `Monitor_PrioritizedDispatch_ContinuationThenDelayThenSequence` / `Monitor_GlobalLimitOne_ContinuationWinsSlotOverSequence` — Priorisierung.
- `OpenFromQueue_LimitReached_WaitsWithReason` — UI-Pfad durch dieselbe Zulassung.
- `Dispatch_ConcurrentStartDuringHealthCheck_GlobalLimitOne_ExactlyOneStarts` — Reservierung im Startfenster.
- `Dispatch_StartFails_ReservationReleasedForNextDispatch` — Reservierungsfreigabe bei Fehlschlag.
- `Dispatch_HostActions_UnlimitedWhileSlotOccupied` — Hostaktionen unbegrenzt.
- `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts` / `_PluginSlotFreed_WaitingEventStarts` — **Pausen-Freigabe end-to-end**.
- `Dispatch_TaskPauseExpired_SlotStillOccupied` — abgelaufene Pause belegt weiter.

### `ScmEventDispatchTests_Startcheck` (Integrationstests)
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Startcheck.cs` (8 Methoden)

- `StartCheck_CliCompleted_WaitingEventStartsWithoutPoll` — `RunningCountChanged`-Auslöser bei Prozessende.
- `StartCheck_PauseSet_WaitingEventStartsWithoutPoll` / `_PauseLifted_EventStartsWithoutPoll` — Pausen-Auslöser.
- `StartCheck_DelayedCapture_TriggersSignalOut` / `_RateLimitPause_TriggersSignalOut` — Direktiven- und Session-Limit-Pfad.
- `StartCheck_OverlappingTrigger_NoDoublestart` — Idempotenz/Serialisierung.
- `StartCheck_MonitoringDisabled_NoDispatch` — Laufzeit-Schalter gilt.
- `StartCheck_NotScmRun_CountCorrectAndTriggersOut` — Nicht-SCM-Läufe zählen und lösen aus.

### Weitere im Umfeld relevante Testklassen (nicht erschöpfend)

- `AiExecutionStatusClassificationTests` (`src/Softwareschmiede.Tests/Application/Services/`) — `IsCurrentlyPaused`-Referenztests.
- `SettingsViewModelTests_ExecutionLimit` — UI-Bindung der Limit-Felder.
- `WorkItemServiceTests_ActiveRun` — Laufdaten/`RunStatus`-Persistenz.
- `ScmTaskCompletionMonitoringServiceTests` (Integration) — Pending-Guard-Verhalten der Abschlussüberwachung.
- `ScmEventCliExchangeAccessTests` (+ `.PendingProbe`, `.RestrictedProcess`) — Kanalprobe/`ReadyStatus`-Pfade; teils `OsInterface`.
- `E2E_ScmEvents` (`src/Softwareschmiede.Tests/E2E/`, `[OsInterface]`) — End-to-End-SCM-Szenarien inkl. Bereitschaftsbestätigung.

## Hilfsmethoden / Testinfrastruktur

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, Namespace `Softwareschmiede.TestSupport`)

- Echte Services auf echtem SQLite (`AddScmTestServices`), nur Grenzen gemockt: `IGitPlugin`/`IScmEventSource`/`IScmEventResponder`, `IAiPlugin`, `IWorkingDirectoryResolver`, `ITerminalSessionFactory`.
- `ScmAiProfile.Simulator` (Prefix `Softwareschmiede.KiSimulator`, Banner `SCM_SIMULATOR_PROTOCOL_V1`, Prompt `SCM_READY_V1`) und `ScmAiProfile.Devin` (Prompt `❭`).
- `Sessions` (`Mock<ITerminalSession>` je Aufgabe) mit echtem `TerminalBuffer` (`Screen(banner, prompt)`); `SetPrompt(task, prompt)` tauscht den Buffer und feuert `BufferChanged` — so wird `ReadyStatus` des echten Adapters gesteuert (`SCM_READY_V1`/`SCM_BUSY_V1`/Trust-Texte).
- `AddAsync` legt Projekt+Repository+Aufgabe+Ereignis an; `DispatchAsync`, `EventAsync`, `Monitor`, `StartCheck`, `Trigger`, `Cli`, `SetSettingAsync` als Zugriffspunkte.
- `CompleteProbeAsync` vollzieht die Dateitool-Kanalprobe; `OnWritePrompt`-Hook für Probesimulation.

### `ScmTestServices` (`src/Softwareschmiede.Tests/Helpers/ScmTestServices.cs`)

- `AddScmTestServices` registriert den kompletten SCM-Dienststapel inkl. `ScmStartCheckTrigger`,
  `ScmStartCheckService`, `ScmStartSlotReservationPool` (Singleton je Fixture), `AiExecutionService`
  als `ICliExecutionLimitSource`/`IRunningAutomationStatusSource`, `ScmEventMonitoringService`,
  `ScmEventOrchestrator`, `WorkItemService`, `AiPluginLimitService`, `ScmEventStore` u. a.

### `TestDbContextFactory` / `TestAiExecutionServiceFactory` / `TestPseudoConsoleSessionFactory`

- In-Memory-EF-Core-Context für Unit-Tests (`ScmEventExecutionLimitServiceTests`), Fabrik für
  `AiExecutionService` ohne echte Prozessstarts bzw. `PseudoConsoleSession` ohne ConPTY
  (`CliProcessManagerTests_RunStatus`: `NeverEndingStream`, 1-ms-Warteschwelle).

### Attribute

- `OsInterface`/`OsInterfaceFact`/`OsInterfaceTheory` (`src/Softwareschmiede.Tests/Infrastructure/Testing/`)
  → Trait `Category=OsInterface`; `TestCategories`-Konstanten; `Flaky`-Konvention
  (OsInterface-Tests dürfen nicht zusätzlich `Flaky` sein, `FlakyConventionTests`).
