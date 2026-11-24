# Testergebnisse

## Ergebnis

**Status:** Alle Test-Lanes grün

## Finaler Testlauf (nach Umsetzung und Review-Korrekturen)

| Lane | Befehl | Ergebnis | Nachweis |
|------|--------|----------|----------|
| Unit/Stabil | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) | **2834 bestanden / 0 fehlgeschlagen / 3 übersprungen** (3 m 25 s) | `inventory/test-results/final-tests.log` |
| Integration | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) | **434 bestanden / 0 fehlgeschlagen / 0 übersprungen** (52 s) | `inventory/test-results/final-integration-tests.log` |

Vorheriger vollständiger Build: `dotnet build Softwareschmiede.slnx` fehlerfrei (Testprojekte wurden im Testlauf jeweils neu gebaut — kein `--no-build`).

Format-Check: `dotnet format Softwareschmiede.slnx --verify-no-changes` → sauber (Exit 0).

## Baseline (vor Umsetzung, Commit `270fd770`-Stand davor)

| Lane | Ergebnis | Nachweis |
|------|----------|----------|
| Unit/Stabil | 2825 bestanden / 0 fehlgeschlagen / 3 übersprungen | `inventory/test-results/baseline-tests.log` |
| Integration | 430 bestanden / 0 fehlgeschlagen / 0 übersprungen | `inventory/test-results/baseline-integration-tests.log` |

Delta zur Baseline: +9 Unit-Tests, +4 Integrationstests (neue Theorien werden je Datenzeile gezählt).

## Neue Tests der Änderung

- `ScmEventExecutionLimitServiceTests`: `GetStartBlockReasonAsync_ReadySessionInFlightEvent_CountsAsOccupied` (Theory inkl. unbekanntem Status), `GetStartBlockReasonAsync_ReadySessionNotYetDeliveredEvent_FreesSlot` (Theory), `GetStartBlockReasonAsync_ReadySessionWaitingWithMaxAttempt_FreesSlot`
- `ScmIncomingEventStatusTests`: `StartSlotFreeing_ShouldContainExactlyTheNonOccupyingStatuses`, `StartSlotFreeing_ShouldExcludeOccupyingStatuses`, `StartSlotFreeing_WartendIsDecoupledFromNonBlocking`
- `ScmEventDispatchTests_Limit`: `Dispatch_EndedOrArchivedTask_MarkedUndeliverable` (Theory), `Dispatch_RemovedTask_MarkedUndeliverable`, `Dispatch_TaskWithoutRepository_MarkedUndeliverable`, `Dispatch_ReadySessionWaitingEvent_SlotFreedForNextStart`, `Dispatch_ReadySessionUnclarifiedEvent_SlotFreedForNextStart`
- `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_ReusedBlockedTask_StaysWaitingWithoutStart`: auf neue Endzustände differenziert
- `ScmEventOrchestratorTests.BlockedTask_IsNeverActivated`: auf neue Endzustände angepasst
- `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_GlobalLimitOne_ContinuationWinsSlotOverSequence`: Erwartung an die neue Platzfreigabe-Semantik angepasst (Dokumentation im Test)

## E2E-Lane (`Category=OsInterface`)

`E2E_ScmEvents` wurde auf die neuen Wartestati (`VerarbeitungUngeklärt` für beendete/archivierte Aufgaben) angepasst; die Lane läuft in dieser Sandbox nicht (FlaUI/ConPTY) und ist vom stabilen Filter ausgenommen — dieselbe Ausnahme wie für den Baseline-Lauf dokumentiert. Der Funktionsnachweis liegt auf den Integrationstests (echtes SQLite, echter Orchestrator, deterministische Terminal-Grenze).
