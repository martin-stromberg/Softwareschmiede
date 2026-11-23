# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-23, ca. 12:23–12:48 Uhr (UTC+01:00, lokale Windows-Zeit; entspricht 11:23–11:48 UTC).
- **Branch und Commit-ID:** `task/issue-463-5f317feb985647d3bc523e8d440d1b8c-doppelte-aufgaben-bei-neuanlag` @ `c084897df16fc67108bd01a97dcd72082723fe74` („Symbol für Auftragsart-Badge auf den Aufgabenkacheln (#464)").
- **Uncommittete Änderungen im getesteten Stand:** Keine Änderungen an Produktiv- oder Testcode. Untracked: ausschließlich das Feature-Verzeichnis `docs/features/task/issue-463-…-doppelte-aufgaben-bei-neuanlag/` (Anforderungsdokument; die hier abgelegten Inventory-Dateien entstanden während/nach den Läufen).
- **Testumgebung und Runtime-/SDK-Versionen:** Windows-Desktop-Host `DESKTOP-CM8OBSG`, .NET SDK 11.0.100, Ziel `net11.0` bzw. `net11.0-windows10.0.17763.0` (App/Tests), xUnit.net VSTest Adapter v4.0.1. Interaktiver Desktop vorhanden (FlaUI-E2E lauffähig). Für alle `dotnet test`-Aufrufe war `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (Sandbox-Regel aus CLAUDE.md; nur für automatisierte Läufe).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte je Lane (Quelle: `.github/workflows/pr-staging-ci.yml`, `.github/workflows/staging-ci.yml`, `CLAUDE.md`):
  - Reguläre Lane: `dotnet test <Projekt> --filter "Category!=OsInterface"` für `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` und `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`.
  - OS-Interface-Lane: `--filter "Category=OsInterface"` für beide Projekte (E2E/FlaUI, ConPTY, Clipboard, reale Prozessstarts).
  - Build vorab: `dotnet build Softwareschmiede.slnx -c Debug` (Projektregel: niemals `--no-build`).

### Build vor den Läufen

| Befehl | Arbeitsverzeichnis | Ergebnis |
|--------|--------------------|----------|
| `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Wurzel | Exit 0 — 0 Warnungen, 0 Fehler (~18 s, inkrementell). Kein `Softwareschmiede.App.exe`-Prozess lief während des Builds (MSB3027-Risiko entfiel). |

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 — Unit regulär | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;…baseline-unit-regular.trx" --logger "console;verbosity=normal" --results-directory docs/…/inventory/test-results` | Repo-Wurzel | 0 | 2796 | 0 | 3 | [baseline-unit-regular.trx](test-results/baseline-unit-regular.trx) |
| 2 — Integration regulär | `… dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;…baseline-integration-regular.trx" …` | Repo-Wurzel | 0 | 406 | 0 | 0 | [baseline-integration-regular.trx](test-results/baseline-integration-regular.trx) |
| 3 — Integration OsInterface | `… dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface" --logger "trx;…baseline-integration-osinterface.trx" …` | Repo-Wurzel | 0 | 19 | 0 | 0 | [baseline-integration-osinterface.trx](test-results/baseline-integration-osinterface.trx) |
| 4 — Unit OsInterface | `… dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface" --logger "trx;…baseline-unit-osinterface.trx" …` | Repo-Wurzel | 1 | 69 | 1 | 5 | [baseline-unit-osinterface.trx](test-results/baseline-unit-osinterface.trx) |
| 5 — Wiederholung (nur der Fehlschlag aus Lauf 4) | `… dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~General_UpdateStartvarianten_E2E" --logger "trx;…baseline-unit-osinterface-retry-updatestartvarianten.trx" …` | Repo-Wurzel | 0 | 1 | 0 | 0 | [baseline-unit-osinterface-retry-updatestartvarianten.trx](test-results/baseline-unit-osinterface-retry-updatestartvarianten.trx) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `Softwareschmiede.Tests.E2E.End2EndTest.General_UpdateStartvarianten_E2E` (Sub-Szenario `UpdatePath_SelectsExclusiveChannelAsset`) | `src/Softwareschmiede.Tests/E2E/MainTest.cs` (Zeile 79), `E2E_UpdateSettings.cs:345`, `Views/Dialogs/UpdateProgressDialogView.cs:106` | `System.TimeoutException: Element verschwand nicht innerhalb von 20s.` beim Schließen des Update-Fortschrittsdialogs (FlaUI, `WaitUntilProcessWindowGone`). Kein Zusammenhang mit der Anforderung (Update-Dialog-Fluss). App-Log des Laufs ohne `MainWindow konnte nicht angezeigt`-Startexception → kein Startup-Absturz. **Wiederholungslauf 5 isoliert bestanden** → instabiler E2E-Timingfehler, kein dauerhafter Defekt nachgewiesen. | Lauf 4, [baseline-unit-osinterface.trx](test-results/baseline-unit-osinterface.trx); Retry: [baseline-unit-osinterface-retry-updatestartvarianten.trx](test-results/baseline-unit-osinterface-retry-updatestartvarianten.trx) |

Anmerkung zum App-Log (`src/Softwareschmiede.App/bin/Debug/net11.0-windows10.0.17763.0/logs/softwareschmiede-20261123.log`): Wiederkehrende `SQLite Error 1: 'no such table: PullRequestReferenzen'`-Einträge aus dem Pull-Request-Monitoring sowie `NotSupportedException`-Meldungen des `LocalDirectoryPlugin` treten in den isolierten E2E-Testdatenbanken auf (u. a. Update-Fixture-Szenarien mit Alt-Schema) — Umgebungsrauschen der E2E-Fixtures, keine Testfehler.

### Testlücken und Ausführungsprobleme

- **Übersprungen in Lauf 1 (3 Tests, Plattformbedingung, nicht env-var-bedingt):** `SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged`, `SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged`, `WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` — nicht-Windows-Pfade, auf diesem Host grundsätzlich nicht ausführbar.
- **Übersprungen in Lauf 4 (5 Tests, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`):** `End2EndTest.ConPty_FurtherLifecycleScenarios_E2E`, `End2EndTest.ConPty_LifecycleAndReplay_E2E`, `End2EndTest.ConPty_SessionReattach_NoDoubleoutput_E2E`, `End2EndTest.TerminalText_LiveMarkCopyAndKeepSelection_E2E`, `E2E_RepositoryInitializationExecutionTests.InitializationscriptExecution` — ConPTY-Kindprozesse werden in dieser Sandbox bestätigt nicht an die Pseudokonsole isoliert (Dokumentation in CLAUDE.md); die Skipps sind ehrlich protokolliert, keine Aussage über diese Tests in einer interaktiven Session.
- **Keine weiteren Ausführungsprobleme:** Keine Build-, Setup- oder Infrastrukturfehler; alle vier Suiten liefen vollständig durch. Für nicht ausgeführte (übersprungene) Tests wird keinerlei Erfolg oder Fehler behauptet.

## Testklassen (bereichsbezogen)

### `ScmEventMonitoringServiceTests_IssuePreparation`
`src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_IssuePreparation.cs` — „Issue erstellt"-Erkennung am Enumerationskanal.
- `Monitor_FirstObservation_CreatesBaselineOnly` — Erstbeobachtung legt nur Baseline an.
- `Monitor_NewIssueAfterBaseline_CreatesPreparationTaskAndInternalEvent` — Happy Path: Purpose-Aufgabe + internes Ereignis, sofort fällig.
- `Monitor_RepeatedPolls_CreateNoSecondPreparation` — Dedup über Auswertungs-Checkpoint/EventKey.
- `Monitor_GlobalSwitchDisabled_CreatesNothing`, `Monitor_RepositoryFlagDisabled_CreatesNothing`, `Monitor_RepositoryWithoutEventCapablePlugin_CreatesNothing`, `Monitor_IssueCreatedBeforeBaseline_CreatesNothing` — Schalter-/Altbestand-Fälle.
- `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` — Keine Aufbereitungs-Schleife.
- **`Monitor_TriggerOnPreparedIssue_ResolvesToCanonicalTask`** — **Dokumentiert das aktuelle (duplikat-erzeugende) Verhalten:** Trigger-Kommentar auf aufbereitetem Issue erzeugt eine **zweite** kanonische `…/Issue/{n}`-Aufgabe neben der Purpose-Aufgabe.
- `Monitor_InternalEvent_ExplicitDispatch_WaitsWithHonestReason` — Internes Ereignis wartet ehrlich bei nicht ausführbarem Plugin.

### `ScmEventMonitoringServiceTests_TaskAssignment`
`src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_TaskAssignment.cs` — Aufgabenauflösung/-anlage.
- `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry`, `Monitor_ExistingTask_IsReusedWithoutSecondCreation`, `Monitor_RepeatedPollsAndBatches_CreateSingleTask` — Anlage/Wiederverwendung/Dedup.
- `Monitor_UnqualifiedEvent_KeepsUnassignedWithoutTask`, `Monitor_ReactedCommentOnUnassignedReference_CreatesNoTask`, `Monitor_VanishedReference_StaysUnassigned`, `Monitor_ResolutionFailure_RecordsCheckpointFailure` — Ablehnungs-/Fehlerpfade.
- **`Monitor_TriggerOnIssueWithPreparationTask_CreatesOneInstructionTaskAlongside`** — **Dokumentiert ebenfalls das bisherige Verhalten:** Purpose-Aufgabe bleibt unberührt (Zweck-Ausschluss) und eine `Instruction`-Aufgabe wird parallel angelegt (Verweis #457).
- `Monitor_QualifiedPullRequestEvent_CreatesReviewSourceTask` sowie sechs weitere PR-Kontext-Tests (Branch-/Closing-Issue-Match, Ambiguität, Fork, pausierte Aufgabe).

### `ScmEventDispatchTests_IssuePreparation`
`src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_IssuePreparation.cs` — Dedizierter Aufbereitungs-Ausführungspfad (15 Tests): Analyse-Prompt + Ergebnisanwendung, Kommentar-Schalter, Plugin-/Limit-/Schalter-Wartegründe, geschlossenes/unlesbares Issue vor/während der Zustellung, Rückschreibefehler.

### `ScmEventDispatchTests_Ordering`
`src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Ordering.cs` — Vorgängerprüfung derselben Aufgabe.
- `Dispatch_FollowUpBehindTerminalUnresolved_IsDelivered` (Theory, NonBlocking-Stati) — Folgeereignis wird hinter terminalem Vorgänger zugestellt.
- `Dispatch_FollowUpBehindTransientPredecessor_StaysWaiting` (Theory) — Folgeereignis wartet hinter blockierendem Vorgänger.
- `Dispatch_MultipleFollowUps_KeepRelativeOrder` — Relative Reihenfolge bleibt.

### `ScmIssuePreparationMonitoringServiceTests`
`src/Softwareschmiede.IntegrationTests/Services/ScmIssuePreparationMonitoringServiceTests.cs` — Eigenständiger Entdeckungszyklus (17 Tests): SeparatesKonto, Baseline/Endzustand, Schalter, Konto-Umtag, Fehlerisolation, Moduswechsel ohne Duplikate, zyklusinterner Dispatch.

### `ScmEventDispatchTests_Limit` / `ScmEventDispatchTests_Startcheck` / `ScmEventDispatchTests`
`src/Softwareschmiede.IntegrationTests/Services/` — Ausführungsbegrenzung (16 Tests: globale/Plugin-Limits, Reservierung, Slot-Freigabe durch Pause/Ready, nebenläufige Starts), ereignisgesteuerte Startprüfung (9 Tests: Auslöser, Koaleszierung, Monitoring-Schalter) sowie Alias-/Mehrfach-Dispatch-Fälle (6 Tests).

### `ScmEventMonitoringServiceTests_AutoStartDelivery` / `ScmEventDelayIntakeTests` / `ScmPromptSessionAdapterReadySignalTests` / `ScmTaskCompletionMonitoringServiceTests`
`src/Softwareschmiede.IntegrationTests/Services/` — CLI-Autostart und Zustellung (5 Tests), Verzögerungsdirektive (6 Tests), `ReadyStatusChanged`-Signal (3 Tests), Abschluss-Gate der Ereignisstati.

### Unit-Tests (`src/Softwareschmiede.Tests/`)
- `Application/Services/ScmDispatchPriorityTests.cs` — Prioritätsklassen 1–3 und `OrderForDispatch`-Sortierung (10 Testmethoden).
- `Application/Services/ScmEventOrchestratorTests.cs` — Wartegründe, Plugin-Auflösung, ConfirmReady/TrustPrompt, Endstatus-Startcheck (16 Methoden).
- `Application/Services/ScmEventExecutionLimitServiceTests.cs` — Limit-Auswertung, Reservierung/Lebensdauer, Plugin-Attribution, Freigaberegeln (18 Methoden).
- `Application/Services/ScmStartCheckServiceTests.cs` — Trigger-/Countdown-Koaleszierung, Dispose (4 Methoden).
- `Application/Services/AppSettingServiceTests_IssuePreparation.cs` — Drei Aufbereitungs-Einstellungen inkl. Defaults (7 Methoden).
- `Application/Services/ScmIssuePreparationPromptTests.cs` — Prompt-Kontrakt/Deterministik (5 Methoden).
- `Domain/Entities/ScmIncomingEventStatusTests.cs` — Literalwerte und `NonBlocking`-Klassifikation.
- Weitere berührte Suiten: `ScmEventValidationServiceTests`, `ScmPromptAppendixTests`, `ScmEventWireCompatibilityTests`, `ScmEventExchangeServiceTests`, `ScmEventQueueViewModelTests`, `SettingsViewModelTests_IssuePreparation`.

### E2E (`src/Softwareschmiede.Tests/E2E/`, Category `OsInterface`)
- `End2EndTest.General_BaseAndSettings_E2E` (`MainTest.cs`) — enthält `Settings_ConfigureIssuePreparation_E2E` (`E2E_IssuePreparation.cs`): kompletter Einstellungsfluss der Aufbereitung über die UI inkl. Persistenzprüfung.
- `ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` (`E2E_ScmEvents.cs`) — reale SCM-Pipeline (Queue, Zustellung, Antworten) gegen isolierten Remote-Transport; `ScmEvents_RepositoryResolution_StaysWithinBudgetAndGates_OnRateLimit` — Auflösungsbudget/Rate-Limit. Beide im Baseline-Lauf bestanden.
- Kein E2E-Test zum Duplikat-Neuanlage-Szenario vorhanden.

## Hilfsmethoden / Fixtures

### `ScmDispatchFixture`
`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` — DI-Container-Fixture aller Dispatch-/Monitoring-Integrationstests (`IAsyncDisposable`).
- `InitializeAsync`, `AddAsync` (Aufgabe+Ereignis anlegen), `AddTaskAsync` (in `ScmTestServices`/partiellen Fixture-Dateien), `AddRepositoryAsync`-Helfer in den Testdateien.
- `ConfigureEnumeration`, `ConfigureDescriptions`, `ConfigureObservations`, `ConfigureIssue`, `ConfigurePullRequest`, `ConfigureMentionObservations`, `AddNotification` — Fake-SCM-Quelle über `Mock<IGitPlugin>`/`Mock<IAiPlugin>` (KiSimulator-Profil `ScmAiProfile`, optional zweites Plugin `Ki2`).
- `Monitor`, `StartCheck`, `Trigger`, `Cli` — Dienstzugriffe; `DispatchAsync`, `EventAsync`, `SetEventStatusAsync`, `SetSettingAsync`, `SetAccountModeAsync`.
- `Sessions`, `Prompts`, `Responses`, `StartSpecs`, `Notifications` — Beobachtungslisten; `SetPrompt` steuert das simulierte CLI-Prompt (`SCM_READY_V1`/`SCM_BUSY_V1`); `CompleteProbeAsync`, `WaitForAsync`, `CloneGate` — Kanalprobe/Wartehelfer.

### `ScmTestServices`
`src/Softwareschmiede.Tests/Helpers/ScmTestServices.cs` — `AddScmTestServices` registriert die produktiven SCM-Dienste gegen SQLite-InMemory/Test-Implementierungen für die Integration-Fixtures.

### `FakeGitHubEventApi` / `ScmE2EFixture` / `ScmTestServices`-Begleiter
- `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs` — gefakte GitHub-Ereignis-API.
- `src/Softwareschmiede.Tests/E2E/ScmE2EFixture.cs` + `src/Softwareschmiede.App/Services/Testing/ScmE2E*.cs` — E2E-Laufzeit (isolierte App-Instanz, KiSimulator, gefakte Remote-Transporte).
- `src/Softwareschmiede.Tests/E2E/ElementWaitHelper.cs`, `AppStartupLogInspector.cs` — FlaUI-Wartehelfer und Startfehler-Diagnose.
