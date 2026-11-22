# Tests — Bestandsaufnahme

## Test-Ausgangszustand

| Rahmenbedingung | Wert |
|---|---|
| Zeitpunkt der Testläufe | 2026-11-22, ca. 07:00–07:30 Uhr (UTC+0100) |
| Branch | `task/issue-441-cf908fe2f4634f37862b1ba43b0e8ae8-abschlussueberwachung-terminal` |
| Commit | `be3d21854950ea078ad4091deed58d655b4dc196` |
| Uncommittete Änderungen | nur untracked: `docs/features/task-issue-441-cf908fe2f4634f37862b1ba43b0e8ae8-abschlussueberwachung-terminal/` (requirement.md + diese Inventory) |
| SDK | `dotnet --version` = 11.0.100 |
| Build | `dotnet build Softwareschmiede.slnx` — **erfolgreich**, 0 Warnungen, 0 Fehler (~17 s); kein `Softwareschmiede.App.exe`-Lock aufgetreten |
| Env beim Testlauf | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (14 ConPTY-E2E-Tests laufen in dieser Sandbox nicht; ohne die Variable Timeout-Fehlschläge) |

## Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|---|---|---|---|---|---|---|---|
| Unit-Tests (stabile Spur) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=unittests-inventory.trx"` | Repo-Wurzel | 0 | 2721 | 0 | 3 | [TRX](test-results/unittests-inventory.trx), [Konsole](test-results/unittests-console.log) |
| Integrationstests (stabile Spur) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=integrationtests-inventory.trx"` | Repo-Wurzel | 0 | 357 | 0 | 0 | [TRX](test-results/integrationtests-inventory.trx), [Konsole](test-results/integrationtests-console.log) |

## Nachgewiesene bestehende Testfehler

Keine — beide Läufe ohne Fehlschlag.

## Übersprungene Tests (Unit-Lauf)

| Test-ID | Suite | Grund |
|---|---|---|
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/SystemShutdownServiceTests.cs` | OS-spezifisch (nur Linux lauffähig). |
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/SystemShutdownServiceTests.cs` | OS-spezifisch (nur macOS lauffähig). |
| `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` | `src/Softwareschmiede.Tests/Application/Services/WorkingDirectoryOpenServiceTests.cs` | OS-spezifisch (nur Nicht-Windows lauffähig). |

## Testlücken / Ausführungsprobleme

- **`Category=OsInterface`-Spur nicht ausgeführt** (bewusst instabil): sämtliche FlaUI-E2E-, ConPTY-, Clipboard- und Prozess-Tests — u. a. `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`, das den SCM-Ereignisfluss (inkl. `VerarbeitungUngeklärt`-/`ZustellungUngewiss`-Szenarien und Neuversand) end-to-end abdeckt. Umfang: alle Dateien mit `OsInterface`-Trait (52 Testdateien referenzieren `Trait`/`OsInterface` in `src/Softwareschmiede.Tests`, 4 Fundstellen in `src/Softwareschmiede.IntegrationTests`).
- `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war gesetzt; in der ausgeführten stabilen Spur greift die Variable ohnehin nur für ConPTY-E2E-Tests (die durch den `OsInterface`-Filter bereits ausgeschlossen sind) — dokumentiert der Vollständigkeit halber.
- Keine Build-/Lock-Probleme: `Softwareschmiede.App.exe` war nicht gesperrt; voller Solution-Build lief durch.

## Testklassen und Hilfsmethoden für den Bereich

### `ScmTaskCompletionMonitoringServiceTests`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs` (14 Tests, Integrationstests mit echter SQLite + gemockten Plugins über `ScmDispatchFixture`).

| Testmethode | Abgedeckter Aspekt |
|---|---|
| `Complete_ReportedPullRequestNumber_LeftReferenceToTask` | `pullRequestNumber` aus `complete.json` → `PullRequestReference` (Rolle `CreatedByTask`) + GitAction-Protokolleintrag. |
| `Monitor_MergedPullRequest_StopsCliAndCompletesTaskAfterWait` | Gemergter PR → `ScmCompletionDetectedUtc` → Auto-Abschluss nach Wartezeit inkl. CLI-Stopp. |
| `Monitor_MergedPullRequest_CancelsScheduledPromptOnComplete` | Geplanter Prompt wird beim Abschluss zurückgezogen. |
| `Monitor_ReopenedPullRequest_ResetsCountdown` | Wieder geöffneter PR setzt Countdown zurück. |
| `Monitor_ClosedIssue_WithoutPullRequest_CompletesTaskAfterWait` | Issue-Fallback ohne PR-Verknüpfung. |
| `CompletionService_IssueCheck_UsesUseraccountContext` | Issue-Prüfung immer unter `ScmAccountMode.UserAccount`. |
| `Monitor_Disabled_SkipsObservation` | `scm.taskcompletion.enabled = false` → keine Beobachtung. |
| `Monitor_OpenPullRequestOrPendingEvent_KeepTaskRunning` | **Deckt das heutige Gate ab**: offener PR bzw. ein zweites, noch laufendes (Default-`Aufgenommen`-)Ereignis verhindern Countdown/Abschluss. |
| `Monitor_AllWorkItemsMode_ObservesManualTaskWithPullRequest` / `Monitor_Eventdriven_SkipsManualTaskWithPullRequest` | Prüfmodus `AllWorkItems` vs. `EventDriven` für manuell angelegte Aufgaben. |
| `Monitor_CheckmodeAllWorkItems_ManualCreatedWorkItemWithClosedIssue_WillCompleted` | `AllWorkItems` + Issue-Fallback an manueller Aufgabe. |
| `Monitor_Eventdriven_TaskWithInvalidIssueReference_IsNeverObserved` / `Monitor_AllWorkItems_TaskWithInvalidIssueReference_IsNeverObserved` | Issue-Referenz ohne Nummer → nie beobachtet. |
| `Monitor_WorkItemWithoutIssueAndWithoutPullRequest_WillNeverCompleted` (Theory, beide Modi) | Aufgabe ganz ohne SCM-Referenz bleibt unberührt. |

Hilfsmethoden: `ProcessEventWithReportAsync` (Dispatch → `Zugestellt` → schreibt `accepted.json`+`complete.json` mit optionalem `pullRequestNumber` → 2× `DispatchAsync`), `SetupPullRequestStatus`, `SetWaitAsync`, `SetCheckmodeAsync`, `AddIssueReferenceAsync`, `GetOrCreateRepositoryAsync`, `AddManualTaskWithPullRequestAsync`, `AddManualTaskWithIssueAsync`, `TaskAsync`, `TestTimeProvider` (interne `TimeProvider`-Ableitung mit `Advance`).

**Lücke zur Anforderung:** Kein Test, der die Blockwirkung einzelner Ereignis-Stati parametrisiert prüft; `VerarbeitungUngeklärt` als Zustand einer Aufgabe mit geschlossener Referenz ist nicht abgedeckt; die `pullRequestNumber`-Registrierung ist nur im sauberen `Accepted`-Pfad getestet (kein Fall ohne `accepted.json`).

### `ScmEventDispatchTests*` (Integrationstests)
Basis: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests*.cs`, 12 Dateien, ~80 Testmethoden, alle auf `ScmDispatchFixture`.

| Datei | Schwerpunkt / anforderungsrelevante Fälle |
|---|---|
| `ScmEventDispatchTests.cs` | Alias-Zuordnung, >100 wartende Ereignisse, GitHub-Actions-Autor, nebenläufiger Dispatch + manueller Start. |
| `ScmEventDispatchTests_AgentActions.cs` | Agenten-Env/`-konto`; `Dispatch_AgentReportCompletion_ProcessesWithoutChannelAction` (report-basierter Abschluss); `Dispatch_EmptyCompletionWithoutReportOrReason_StaysUnresolved` → `VerarbeitungUngeklärt` (Z. 168); `Dispatch_NoRemoteActionCompletion_StillProcesses`. |
| `ScmEventDispatchTests_Attachments.cs` | Screenshot-Nachweise; u. a. `VerarbeitungUngeklärt`-Assertion (Z. 215). |
| `ScmEventDispatchTests_Cancel.cs` | `Abgebrochen` ist endgültig (kein Dispatch, kein Outbox-Flush). |
| `ScmEventDispatchTests_Delay.cs` | Verzögerungsdirektive (`Verzögert`). |
| `ScmEventDispatchTests_Devin.cs` | Devin-CLI-Startvertrag. |
| `ScmEventDispatchTests_HostRetry.cs` | Host-Antwort-Retry ohne erneuten Dispatch; `CompletedEvent_RetriesHostResponseWithoutNewDispatch` setzt Status per `ExecuteUpdate` direkt auf `Verarbeitet`. |
| `ScmEventDispatchTests_IssueBodyUpdate.cs` | `issueBodyUpdate`-Kanalaktionen. |
| `ScmEventDispatchTests_IssuePreparation.cs` | Aufbereitungspfad (`Purpose == "IssueAufbereitung"`); `VerarbeitungUngeklärt`-Assertions (Z. 142, 308); kein `pullRequestNumber`-Bezug. |
| `ScmEventDispatchTests_Limit.cs` | Ausführungslimits/Platzreservierung. |
| `ScmEventDispatchTests_PromptAppendix.cs` | Prompt-Anhänge. |
| `ScmEventDispatchTests_Startcheck.cs` | Ereignisgesteuerte Startprüfung. |

### `ScmEventOrchestratorTests` (Unit-Tests)
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs` (14 Tests, ebenfalls `ScmDispatchFixture`): Schreibrechte-Wartegrund, reagierte Beobachtung, CLI-Auflösung/Fallbacks, Blockgründe (`BlockedTask_IsNeverActivated`), Bestätigungs-/Trust-Flows (`ConfirmReadyAsync`), `ConfirmReady_LiftsNextAttemptLock`.

### `ScmEventQueueViewModelTests` (Unit-Tests)
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventQueueViewModelTests.cs` (15 Tests): Filter-/Leerzustände, Command-Aktivierung (`CancelCommand_EnabledOnlyForNonFinalEvents`, `RetryCommand_ProcessedEvent_RequiresOpenOutboxAction`, `ProcessedEvent_WithFailedAttachmentsAction_OffersWorkingRetry`), Diagnosezeilen (`FailedOutgoingAction_*`, `UncertainOutgoingAction_*`), `CancelledEvent_*`, Quellen-Diagnosen, `RetrySourcesCommand`-Nebenzyklen.

### Geteilte Test-Infrastruktur
- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (Namespace `Softwareschmiede.TestSupport`; per `<Compile Include>` in die Integrationstests gelinkt): echtes SQLite, `ServiceProvider` mit allen realen Services, gemockte `IGitPlugin`/`IAiPlugin`/`ITerminalSessionFactory`; Hilfen `AddAsync`, `DispatchAsync`, `EventAsync`, `ConfigureIssue`, `ConfigureMentionObservations`, `SetAccountModeAsync`, `SetSettingAsync`, `Cli`, `Monitor`, `Trigger`, `StartCheck`, `Sessions`/`Prompts`/`Responses`; `ScmAiProfile` (`Simulator`, `Devin`).
- `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (`OsInterface`, nicht ausgeführt): End-to-End-Abdeckung der Statuspfade inkl. Neuversand nach `ZustellungUngewiss`.
