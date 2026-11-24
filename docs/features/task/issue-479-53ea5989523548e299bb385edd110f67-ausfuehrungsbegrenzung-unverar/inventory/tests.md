# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-24 ~18:30 UTC+1 (lokale Systemzeit)
- Branch und Commit-ID: `task/issue-479-53ea5989523548e299bb385edd110f67-ausfuehrungsbegrenzung-unverar` @ `da9c51e6` (= `origin/staging`, 0 Commits voraus)
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Artefakte unter `docs/features/task/issue-479-*/` (kein Code)
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK für `net11.0`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (Sandbox-Beschränkung, siehe Projektregeln)
- Ermittelte Testsuiten und Quellen der Testbefehle: `Softwareschmiede.Tests` + `Softwareschmiede.IntegrationTests`, Filter `Category!=OsInterface` — entspricht den CI-Lanes in `.github/workflows/pr-staging-ci.yml` und der stabilen Lane laut Projektregeln

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline Unit | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 2825 | 0 | 3 | [baseline-tests.log](test-results/baseline-tests.log) |
| Baseline Integration | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 430 | 0 | 0 | [baseline-integration-tests.log](test-results/baseline-integration-tests.log) |

### Nachgewiesene bestehende Testfehler

Keine — beide Baseline-Läufe fehlerfrei.

### Testlücken und Ausführungsprobleme

- `Category=OsInterface`-Tests wurden entsprechend der stabilen Lane nicht ausgeführt (separate Lane in CI; umgebungssensitiv).
- ConPTY-E2E-Tests laufen in dieser Sandbox grundsätzlich nicht (Agent-Sandbox ohne isolierte Pseudo-Konsole); mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` als übersprungen gemeldet — 3 Übersprungene im Unit-Lauf sind OS-bedingte Skips (`ResolveShutdownCommand_Linux/MacOS`, `Open_OnNotWindows`).
- 14 ConPTY-E2E-Tests sind für diese Aufgabe nicht relevant (kein UI-/Terminal-Fluss betroffen); die Platzlogik ist über Unit- und Integrationstests mit `ScmDispatchFixture` vollständig abdeckbar.

## Testklassen

### `ScmEventExecutionLimitServiceTests` (`src/Softwareschmiede.Tests/Application/Services/`)

- `GetStartBlockReasonAsync_ReadySessionWithPendingEvent_CountsAsOccupied` (Theory) — **betroffen**: erwartet aktuell Belegung für ALLE nicht-finalen Stati (`Aufgenommen`, `Wartend`, `Verzögert`, `Zugestellt`, `InBearbeitung`, `ZustellungUngewiss`, `VerarbeitungUngeklärt`); muss auf die neue Platzbelegungsregel angepasst werden.
- `GetStartBlockReasonAsync_ReadySessionEventInEndStatus_ReturnsSlotFree` (Theory `Verarbeitet`/`Abgebrochen`) — unverändert gültig.
- `GetStartBlockReasonAsync_ReadySessionNoPending_ReturnsGlobalSlotFree` / `..._ReturnsPluginSlotFree` — unverändert.
- `GetStartBlockReasonAsync_SessionNotReady_CountsAsOccupied` (Theory über nicht-`Ready`-Stati) — unverändert.
- `GetStartBlockReasonAsync_ReadySession_PendingOfOtherTask_Ignored` — unverändert.
- `CheckAndReserveStartSlotAsync_*` — Reservierungssemantik, unverändert.
- Hilfsmethoden: `RunningWorkItemAsync` (legt `WorkItem` + Sitzungs-Mock an), `AddEventAsync(workItemId, status)` (fügt `ScmIncomingEvent` ein — ohne `NextAttemptUtc`-Parameter), `LiveSession(prefix, readyStatus, isAlive)`, `SetRunning`.

### `ScmIncomingEventStatusTests` (`src/Softwareschmiede.Tests/Domain/Entities/`)

- `NonBlocking_ShouldContainExactlyTheFourReleasedStatuses` — unverändert.
- `NonBlocking_ShouldClassifyAllKnownStatuses` / `..._ExcludeUnknownStatuses` — unverändert.
- `Constants_ShouldNameAllTenKnownStatuses` — unverändert.

### `ScmEventDispatchTests_Limit` (`src/Softwareschmiede.IntegrationTests/Services/`)

- `Dispatch_EndedOrArchivedTask_StaysBlocked` (Theory Completed/Archived) — **betroffen**: erwartet `Wartend` + „Aufgabe beendet/archiviert; Start gesperrt."; muss auf den neuen Endzustand angepasst werden.
- `Dispatch_TaskReadyAtPrompt_SlotFreed_WaitingEventStarts` — Ready + `Verarbeitet` → frei; unverändert.
- `Dispatch_TaskReadyWithPendingEvent_SlotStaysOccupied` — Ready + `Zugestellt` (In-Flight) → belegt; bleibt gültig.
- `Dispatch_GlobalLimitReached_EventWaitsWithReason` u. a. Limit-Tests — unverändert.

### `ScmEventDispatchTests_Ordering` (`src/Softwareschmiede.IntegrationTests/Services/`)

- `Dispatch_FollowUpBehindTerminalUnresolved_IsDelivered` (`VerarbeitungUngeklärt`, `ZustellungUngewiss`, `Verarbeitet`, `Abgebrochen`) — unverändert (Serialisierung nutzt `NonBlocking`, unberührt).
- `Dispatch_FollowUpBehindTransientPredecessor_StaysWaiting` — unverändert.

### `ScmEventMonitoringServiceTests_AutoStartDelivery` (`src/Softwareschmiede.IntegrationTests/Services/`)

- `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` (Theory Completed/Archived/New+paused) — **betroffen**: Completed/Archived ändern sich auf den neuen Endzustand; New+paused bleibt `Wartend`.

### `ScmTaskCompletionMonitoringServiceTests` (`src/Softwareschmiede.IntegrationTests/Services/`)

- `NonBlocking`-basierte Abschlussprüfung — unverändert.

## Hilfsmethoden

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/`)

- `AddAsync(status, configured, ...)` — legt Projekt + Repository + `WorkItem` + ein `ScmIncomingEvent` (`Aufgenommen`) via `ScmEventStore.AcceptBatchAsync` an.
- `DispatchAsync(eventId)` — scoped `ScmEventOrchestrator.DispatchAsync`; bei `Wartend` mit steuerbarer Zeit ein Wiederanlauf nach Stabilisierungsfrist.
- `EventAsync(eventId)` / `SetEventStatusAsync(eventId, status)` — Lesen bzw. direktes Status-Setzen via `ExecuteUpdate`.
- `SetPrompt(taskId, prompt)` — simuliert den verifizierten Eingabeprompt im Session-Buffer (für `ReadyStatus.Ready`).
- `StartCheck`/`Trigger`/`WaitForTriggeredDeliveryAsync` — ereignisgesteuerte Startprüfung.
- `SetSettingAsync(key, value)` — `AppSettingService` (Limits).
- `Cli` (`AiExecutionService`), `Sessions`, `StartSpecs`, `Prompts`, `Responses` — Beobachtungspunkte.

### `TestDbContextFactory` (`src/Softwareschmiede.Tests/Helpers/`)

- `Create()` — SQLite-In-Memory-`SoftwareschmiedeDbContext` für Unit-Tests.
