# Bestandsaufnahme: Tests (Terminalintegration)

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-24, ~23:40–23:59 +02:00 (Mitteleuropäische Sommerzeit)
- **Branch und Commit-ID:** `task/issue-271-077f09dcf2fb4f5d8c3659df3d5d530a-terminalintegration-verbessern` @ `8b5cfd9513597a84e6054d2e1ca394ab6946c975` ("Backmerge from main to staging (#270)")
- **Uncommittete Änderungen im getesteten Stand:** nur untracked — `.agents/` (Skill-Definitionen) und `docs/features/task/issue-271-…/` (requirement.md + diese Bestandsaufnahme). Keine Änderungen an `src/`- oder `plugins/`-Code.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows 11 Pro (10.0.26200), .NET SDK 10.0.401, .NET Runtime 10.0.12, xUnit 2.9.3 + xunit.runner.visualstudio 3.1.5 + Xunit.SkippableFact, interaktive Konsolen-Session (SessionId 1, `UserInteractive=true`). `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei allen vier Läufen gesetzt (Sandbox-Auflagen gemäß `CLAUDE.md`). Vorbereitung: `dotnet build Softwareschmiede.slnx -c Debug` — erfolgreich, 0 Fehler, 1 Warnung (CS8602 in `CliOutputProtokollWriterTests.cs:123`, vorhanden).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte (`src/Softwareschmiede.Tests`, `src/Softwareschmiede.IntegrationTests`), jeweils mit Filtern `Category!=OsInterface` (regulär) und `Category=OsInterface` (OS-Schnittstellen: E2E/FlaUI, ConPTY, Zwischenablage, echte Prozessstarts). Quellen: `CLAUDE.md` (Abschnitt Testing) und `.github/workflows/pr-staging-ci.yml` bzw. `staging-ci.yml` (identische Lanes inkl. TRX-Logger).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | — | — | — | Konsolen-Ausgabe (0 Fehler, 1 Warnung) |
| Regular Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 1685 | 0 | 1 | [inventory-baseline-regular-tests.trx](test-results/inventory-baseline-regular-tests.trx) |
| Regular IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 69 | 0 | 0 | [inventory-baseline-regular-integration.trx](test-results/inventory-baseline-regular-integration.trx) |
| OsInterface Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Root | 0 | 49 | 0 | 2 | [inventory-baseline-osinterface-tests.trx](test-results/inventory-baseline-osinterface-tests.trx) |
| OsInterface IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Root | 0 | 9 | 0 | 0 | [inventory-baseline-osinterface-integration.trx](test-results/inventory-baseline-osinterface-integration.trx) |

### Nachgewiesene bestehende Testfehler

**Keine.** Alle vier Lanes liefen ohne Fehlschlag durch (1812 ausgeführte Tests bestanden: 1685 + 69 + 49 + 9). Es existiert kein nachgewiesener Pre-Existing-Fehler für spätere Vergleiche.

### Testlücken und Ausführungsprobleme

- `End2EndTest.RunConPtyTests` (`MainTest.cs`, `[SkippableFact]`, konsolidierter ConPTY-Runner mit ~20 Szenario-Aufrufen inkl. `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E`, `CliPanel_BleibtSichtbarNachBeendigung_E2E`, `CliRawExport_*`, zeitgesteuerter Prompt, Plugin-Wechsel u. a.) — **übersprungen** via `SkipWennConPtyNichtVerfuegbar()` wegen `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (bestätigte Sandbox-Limitation: ConPTY-Kindprozess wird nicht in die Pseudo-Konsole isoliert, siehe `ConPtyEnvironmentProbe.cs` und `CLAUDE.md`).
- `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` — **übersprungen**, gleiche Ursache.
- `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` (reguläre Lane) — **übersprungen** via `Skip.If` (Test gilt nur für Nicht-Windows; Umgebung ist Windows). Erwartetes Verhalten.
- Kein automatisierter Test deckt den **echten** `Win32PseudoConsoleProcessLauncher`-Pfad ab: Im E2E-Modus (`SOFTWARESCHMIEDE_TEST_DB_PATH`) registriert `App.xaml.cs` (Zeilen 287–298) den `SimulatedPseudoConsoleProcessLauncher` — die "ConPTY"-E2E-Tests fahren damit auf dem Pipe-Simulationspfad. Echtes ConPTY wird nur durch den `SimulatedPseudoConsoleProcessLauncherTests`-Vergleichspfad indirekt und durch `Win32PseudoConsoleProcessLauncher` gar nicht getestet (die Klasse hat keinen dedizierten Unit-Test).
- Der übersprungene `RunConPtyTests`-Block enthält die einzigen E2E-Abdeckungen für Terminal-Eingabe/Resize/Prozessende (`E2E_ConPtyLifecycle`), CLI-Panel-Sichtbarkeit (`E2E_CliPanelVisibility`), Raw-Export (`E2E_CliRawExport`) und Auto-Start (`E2E_AutoStartCli`) — diese Aspekte sind im Baseline-Lauf **nicht ausgeführt** worden (Skip ist weder Erfolg noch Fehlschlag).

## Testklassen (terminal-/sessionrelevant)

### `PseudoConsoleSessionTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`

- `ReadLoopAsync_WithException_LogsAndContinues` — Fehler im ReadStream wird geloggt, Schleife bricht ab ohne Crash
- `ReadLoopAsync_CancellationToken_GracefulShutdown` — Cancellation beendet Leseschleife sauber
- `SessionDispose_CancelsReadLoop` — `Dispose` beendet Leseschleife
- `Dispose_ClosesOutputStreamImmediately_UnblocksNonCancelableRead` — `OutputStream.Dispose` löst blockierenden Read
- `Dispose_CalledConcurrently_RunsCleanupExactlyOnce` — Interlocked-Guard gegen doppelte Handle-Freigabe
- `Dispose_ReadLoopNeverCompletes_ReturnsPromptlyWithoutWaiting` — `Dispose` wartet nicht auf hängende Leseschleife
- `ReadLoopAsync_BufferChangedFiredAfterBufferUpdated` — `BufferChanged` erst nach `Buffer.Apply`
- `ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin` — `ITerminalOutputSink.OnOutputChunk` erhält Rohchunks parallel zum Buffer
- Hilfstypen: `CapturingOutputSink`, `ThrowingStream`, `BlockingUntilCancelledStream`, `DisposeCountingStream`, `NonCancelableBlockingStream`, `FixedContentStream`, `HangingForeverStream`

### `PseudoConsoleSessionTests_WriteInputAsync`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests_WriteInputAsync.cs`

- `WriteInputAsync_LangerMehrzeiligerText_SchreibtVollstaendigeBytesInReihenfolge`
- `WriteInputAsync_GrosseEingabe_SchreibtChunksSequentiellUndFlushtEinmal` — 4096-Byte-Chunking
- `WriteInputAsync_ParalleleWrites_WerdenNichtVerschachtelt` — `_inputWriteLock`-Serialisierung
- `Dispose_WaehrendWriteInputAsync_MaskiertNichtReleaseUndHaengtNicht`
- `WriteInputAsync_WriteFehler_MarkiertKeineInputAktivitaet`
- Hilfstypen: `RecordingInputStream`, `BlockingFirstWriteStream`, `WriteThrowingInputStream`, `ImmediateEofStream`

### `PseudoConsoleSessionTests_WritePromptAsync`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests_WritePromptAsync.cs`

- `WritePromptAsync_SchreibtEinzelnesCarriageReturnAlsSubmit_KeinCrLf`
- `WritePromptAsync_NormalisiertEingebetteteZeilenumbruecheAufCarriageReturn`

### `AnsiSequenceParserTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/AnsiSequenceParserTests.cs`

- `Parse_PlainText_ErgibtTextWrittenEvent`, `Parse_SgrFarbe_ErgibtColorChangedEvent`, `Parse_SgrReset_ErgibtColorChangedEventMitStandardfarben`, `Parse_Sgr24BitFarbe_WirdKorrektParsiert`, `Parse_CursorMove_ErgibtCursorMovedEvent`, `Parse_ClearScreen_ErgibtScreenClearedEvent`, `Parse_EraseLine_ErgibtLineErasedEvent`, `Parse_MehrteiligePakete_WerdenZusammengesetzt` (Escape-Sequenz über Chunks), `Parse_SgrBold_SetzBoldTrue`, `Parse_CursorHide/Show_ErgibtCursorVisibilityChangedEvent*`, `Parse_CrLfText_ErgibtTextMitCrLf`
- Keine Tests für: Alternate Screen, Scroll-Regionen, Insert/Delete Line, chunk-übergreifende UTF-8-Mehrbyte-Sequenzen

### `CliRuntimeStatusEvaluatorTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/CliRuntimeStatusEvaluatorTests.cs`

- `Determine_ReturnsInaktiv_WhenProcessIsNotRunning`, `Determine_ReturnsLaeuft_WhenOutputIsRecent`, `Determine_ReturnsWartetAufEingabe_WhenActivityIsStale`, `Determine_ReturnsLaeuft_WhenInputIsRecent`

### `TerminalBufferTests`
Datei: `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`

- Text/Cursor/Scroll: `Buffer_SchreibtText_AktualisiertZellen`, `Buffer_CursorMove_AktualisiertPosition`, `Buffer_Newline_ScrolltBeiLetzterZeile`, `Buffer_LineFeed_SetztSpalteAufNull`, `Buffer_CarriageReturnLineFeed_ErgibtEinenUmbruch`, `Buffer_CarriageReturnAllein_BleibtInZeile`
- Resize: `Buffer_Resize_ErhaeltSichtbarenInhalt`, `Buffer_Resize_KleinerAlsInhalt_WirftNicht`, `Buffer_ResizeKleiner_ErhaeltUntereZeilen`, `Buffer_ResizeKleiner_CursorFolgtUnterenZeilen`, `Buffer_ResizeSchmaler_SchneidetRechtsAb`
- Clear/Erase: `Buffer_ClearScreen_SetzAllesZurueck`, `Buffer_ClearScreenMode2_AlleZellenLeer`, `Buffer_ClearScreenMode2_LeertScrollback`
- Farben/Kopien: `Buffer_ColorChange_NachfolgenderTextErbtFarbe`, `Buffer_GetRow_GibtKopieZurueck`
- Concurrency/Snapshot: `Buffer_ParallelApplyAndRead_NoRaceCondition`, `Buffer_GetSnapshot_ReturnsConsistentState`, `Buffer_GetSnapshot_EnthaeltScrollbackVorSichtbaremGrid` (sowie weitere Snapshot-Tests ab Zeile 370)

### `SimulatedPseudoConsoleProcessLauncherTests` (`[OsInterfaceFact]`-Kandidaten/echte Prozessstarts)
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncherTests.cs`

- `Start_LiefertLaufendenProzessUndSession`, `Start_GesendetesKommandoWirdAusgefuehrt` (Befehls-Injektion über Pipe), `Start_UeberWritePromptAsyncGesendeterPromptWirdAusgefuehrt`, `Start_ProzessBeendetSichAufKillEntireProcessTree`

### `TerminalControlTests` (WPF, `[Fact]` — laufen über `WpfUnitTestHelpers` im STA-Kontext)
Datei: `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs` + `TerminalControlTests.ClipboardPaste.cs`

- `OnTextInput_WriteThrows_LogsWarning`
- Session-Bindung: `OnSessionChanged_RegistersBufferChangedHandler`, `OnSessionChanged_ToNewSession_DeregistersOldHandler`, `OnSessionChanged_ToNull_DeregistersAllHandlers`, `OnSessionChanged_SetztScrollzustandAufEnde`
- Neuanbindung: `ParallelSessions_NoBufferInterference`, `SessionSwitch_BackToPreviousSession_PreservesBuffer` (Buffer-Übernahme ohne Rohdaten-Replay)
- Scrollback/IScrollInfo: `ScrollInfo_LangerVerlauf_MeldetExtentGroesserAlsViewport`, `ScrollInfo_SetVerticalOffsetUndNavigation_KlemmenOffset`, `ScrollInfo_NeueAusgabeAmEnde_FolgtNeuemEnde`, `ScrollInfo_ManuellNachOben_NeueAusgabeErhaeltOffset`, `ScrollViewerLayout_CanContentScroll_BegrenztViewportAufSichtbareHoehe`
- Clipboard (alle `[OsInterfaceFact]`): `OnPreviewKeyDown_CtrlV_SetsHandledTrue`, `OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync`, `ReadClipboardAndInsertAsync_Success_WritesEncodedBytesToInputStream`, `ReadClipboardAndInsertAsync_ClipboardEmpty_DoesNothing`, `ReadClipboardAndInsertAsync_ClipboardAccessThrows_LogsWarningAndContinues`, `ReadClipboardAndInsertAsync_CallsMarkInputActivity`, `ReadClipboardAndInsertAsync_LangerMehrzeiligerText_WritesCompleteEncodedBytes`, `ReadClipboardAndInsertAsync_SessionWechseltWaerendPaste_SchreibtInSnapshotSession`, `GetClipboardText_ClipboardContainsText_ReturnsText`, `GetClipboardText_ClipboardAccessThrows_ReturnsEmptyString`
- Hilfstypen: `ImmediateEofStream`, `FixedContentStream`, `WriteThrowingStream`, `ControllableStream`

### `KeyToVt100EncoderTests`
Datei: `src/Softwareschmiede.Tests/App/Controls/KeyToVt100EncoderTests.cs` + `KeyToVt100EncoderTests.KeyEncoding.cs`

- Clipboard-Kodierung: 8 Facts (CR/CRLF-Normalisierung, UTF-8, leer/null)
- Tastenkodierung: `Encode_CtrlLeftKey/…CtrlRightKey/…CtrlUpKey/…CtrlDownKey/…CtrlShiftLeftKey`, Alt-/AltGr-Nullfälle, `Encode_CtrlQWithoutAlt_ReturnsControlCharacter`

### `KiAusfuehrungsServiceTests` (größtenteils `[OsInterfaceFact]` — echte Prozesse)
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs` (+ `_WorkingDirectory`, `_WorkingDirectory_InSourceDirectory`)

- Ohne Prozess: `IsRunning_ShouldReturnFalse_*`, `GetRunningCount_ShouldReturnZero_*`, `GetLastExitCode_ShouldReturnNull_*`, `StopCliAsync_ShouldNotThrow_*`, `UpdateHeartbeat_ShouldNotThrow_*`, `GetPseudoConsoleSession_GibtNull_OhneSession`
- Klassischer Start: `TestCliStartAsync`, `StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo`
- Exited-Handling: `ProcessExited_ScopeFactoryDisposed_*`, `ProcessExited_ExitCodeZero_*`, `ProcessExited_ExitCodeNonZero_*`, `ProcessExited_SubscriberThrows_*`, `ConPtyProcessExited_SubscriberThrows_*`, `StopCliAsync_PersistiertAusfuehrungBeendet*`
- ConPTY-Pfad (mit injizierten Fake-Launchern / `SimulatedPseudoConsoleProcessLauncher`): `KiAusfuehrungsService_HandleProcessExited_DisposesSession`, `KiAusfuehrungsService_Dispose_CancelsAllSessions`, `StartWithPseudoConsoleAsync_ProzessEndetVorVerzoegertemSenden_KeineWarnungWegenGeschlossenemStream`, `StartWithPseudoConsoleAsync_MitInjiziertemFakeLauncher_ErreichtGestartet`, `StartWithPseudoConsoleAsync_PersistiertSessionOutputAlsCliOutput`, `StartWithPseudoConsoleAsync_ParalleleAufgaben_TrenntProtokolleNachAufgabeId`, `AddCliOutputAsync_RateLimitMarkerAusConPtyOutput_ErzeugtRateLimitEintrag`, `StartWithPseudoConsoleAsync_RestzeileOhneZeilentrenner_PersistiertCliOutput`, `StartWithPseudoConsoleAsync_ProzessEndeVorReadLoopDrain_VerliertTailOutputNicht`
- Working-Directory: `ResolveEffectiveWorkingDirectory_*` (4 Facts), `ValidateWorkingDirectory_*` (2 Facts), `StartCliAsync_ShouldUseEffectiveWorkingDirectory`, `StartCliAsync_ShouldUseRepoRootWhenConfigNull`, `StartCliAsync_ShouldResolveConfiguredWorkingDirectory_ViaSourcePath_*`, `StartCliAsync_ShouldThrow_WhenGitPluginNotPassed_*`
- Hilfstypen: `FixedOutputPseudoConsoleProcessLauncher`, `OutputByTaskPseudoConsoleProcessLauncher`, `DelayedOutputPseudoConsoleProcessLauncher`

### `CliProcessManagerTests` / `CliProcessManagerTests_LaufStatus`
Datei: `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests.cs`, `CliProcessManagerTests_LaufStatus.cs`

- `AktualisierungAsync_WithConcurrentTimerTicks_Serializes`, `AktualisierungAsync_WithDifferentAufgaben_DoesNotSerializeAcrossTasks`, `AktivenLaufSetzenAsync/AktivenLaufBeendenAsync/AktualisiereLaufStatusAsync_WithDisposedScopeFactory_Completes`
- `VollerZyklus_GestartetWartetGestoppt_PersistiertLaufStatusUeberDieSitzung` — End-to-End von `RuntimeStatusChanged` bis `Aufgabe.LaufStatus`
- `RaisesRuntimeStatusChanged_AfterGestoppt_AktualisiertLaufStatusNichtMehr` — Unsubscribe-Verhalten

### `CliOutputProtokollWriterTests` / `CliOutputLineAccumulatorTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/CliOutputProtokollWriterTests.cs`, `CliOutputLineAccumulatorTests.cs`

- Writer: `CompleteAsync_DraintAngenommeneZeilen_BevorProviderDisposedWird`, `CliOutputProtokollWriter_Persistenzfehler_BeeintraechtigtSessionNicht`, `CliOutputProtokollWriter_HoheAusgabe_BegrenztQueueMitBackpressure`, `CompleteAsync_BackpressureWaerendAktivemChunk_VerliertKeineDekodiertenZeilen`, `RateLimitMarker_PersistiertSessionLimit_UndPausiertAufgabe`, `MarkerfreieZeile_LoestKeineLimitVerarbeitungAus`
- Accumulator: `Chunks_MitMehrerenLfZeilen_*`, `Chunks_MitGeteilterZeile_UeberChunkGrenze_*`, `Chunks_MitCrLf_ZaehltNichtDoppelt`, `Chunks_MitEinzelnemCr_FlushtProgressZeile`, `Chunks_MitUtf8MultibyteGrenze_DekodiertKorrekt`, `Flush_MitRestzeile_*`

### `PromptZeitVersandServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs`

- `SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort`, `SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt`, `Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch`, `CancelScheduledPrompt_EntferntGeplantenPrompt`, `SchedulePromptAsync_ZweiterPromptFuerSelbeAufgabe_ErsetztErsten`, `Timer_OhneSession_VerwirftPromptStill`, Pause-Verschiebungs-Tests (2×)

### `ProjektleiterAgentServiceTests_CliIntegration`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests_CliIntegration.cs`

- `StarteAgentAsync_CallsKiAusfuehrungsService_WithNullOptionalParameters`, `StarteAgentAsync_SendetInitialPromptUeberPseudoConsoleSession`, `StarteAgentAsync_MitResumePrompt_SendetWeitermachenPromptUeberPseudoConsoleSession`, `StarteAgentAsync_MitResumePromptUndSessionContinuationPlugin_UebergibtContinueFlag`, `…_UebergibtKeinContinueFlag`, `StarteAgenNachAppNeustartAsync_*` (2×), `StoppeAgenExplizitAsync_SetzExplizitGestoppt`

### `CliEmbeddingServiceIntegrationTests`
Datei: `src/Softwareschmiede.Tests/ServiceIntegration/CliEmbeddingServiceIntegrationTests.cs`

- `IsRunning_GibtFalse_WennKeinProzessGestartet`, `GetLastExitCode_GibtNull_WennKeinProzessGestartet`, `StartCliAsync_StartetProzess_UndSetztIsRunningAufTrue`, `StartWithPseudoConsoleAsync_StartetProzess_UndSetztPseudoConsoleSession`

### `CliKiPluginBaseTests` + Plugin-Tests
Dateien: `src/Softwareschmiede.Tests/Domain/Abstractions/CliKiPluginBaseTests.cs`, `src/Softwareschmiede.Tests/Infrastructure/Plugins/{ClaudeCliPluginTests,CodexPluginTests,GitHubCopilotPluginTests,DevinPluginTests,KiSimulatorPluginTests}.cs`, `src/Softwareschmiede.Tests/ServiceIntegration/CliKiPluginCommandLineParametersIntegrationTests.cs`

- Pro Plugin u. a. `BuildProcessStartInfo`-/StartCliAsync-Verhalten (FileName, Arguments, WorkingDirectory, Env-Vars wie `ANTHROPIC_API_KEY`), `CommandLineParameters`-Anhängen aus Credential Store

### `TaskDetailViewModelTests` (relevante Ausschnitte)
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

- `GetPseudoConsoleSession_ReturnsSession_AfterExplicitStart`, `TestLoadAsync_BindetBereitsLaufendeSessionWiederAn` (Session-Reattach), `NachNavigateBack_WiederoeffnenFindetLaufendeSessionUndSetzIsCliRunning`, `TestLoadAsync_StartetCliNichtImplizit_*`, `TestStartenAsync_InvokesCombinedProcess_StartsCliUponSuccess`, `TestPluginWechselAsync_StopsCliAndStartsNew`, `CliNeustartenCommand_NachPluginWechsel_*`

### E2E (FlaUI, `End2EndTest` / weitere `[Collection("E2E")]`-Klassen)
Dateien: `src/Softwareschmiede.Tests/E2E/*.cs`

- Konsolidierte Runner: `End2EndTest.RunGeneralTests` (`[Fact]`, lief im Baseline-Lauf in 7 m 12 s) und `RunConPtyTests` (`[SkippableFact]`, im Baseline-Lauf übersprungen)
- Terminal-/CLI-Szenario-Methoden (protected, aufgerufen von den Runnern): `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E` (`E2E_ConPtyLifecycle.cs`: Start → Resize → Tastatureingabe inkl. AltGr/Ctrl+Pfeile → Prozessende über Stoppen-Button, eine Session), `AufgabeOeffnen_NachStoppen_StartetCliNichtAutomatischErstExplizit_E2E` (`E2E_AutoStartCli.cs`), `CliPanel_BleibtSichtbarNachBeendigung_E2E` (`E2E_CliPanelVisibility.cs`), `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` + `CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E` (`E2E_CliRawExport.cs`), `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` (`E2E_ZeitgesteuerterPrompt.cs`), `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (`E2E_SessionLimitPause.cs`), `AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E` (`E2E_TaskWechselUeberMenue.cs`), `SeitenleistenKachel_AktualisiertStatusAutomatisch_*` (`E2E_ArbeitsstatusAktualisierung.cs`), `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E` u. a. (`E2E_WorkingDirectory.cs`), `PluginAuswahlAbbrechenOkUndWechsel_E2E` (`E2E_PluginAuswahlUndWechsel.cs`), `AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E` (`E2E_TaskExecutionCommandLineParameters.cs`)

## Hilfsmethoden / Test-Infrastruktur

### `TestPseudoConsoleSessionFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestPseudoConsoleSessionFactory.cs`

- `Create(Stream inputStream, Stream outputStream, ILogger?, ITerminalOutputSink?)` — Session mit `NullPseudoConsoleHandle` + `Process.GetCurrentProcess()`
- `Create(..., TimeProvider, TimeSpan waitingThreshold, ...)` — mit kontrollierbarer Zeitquelle

### `TestKiAusfuehrungsServiceFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestKiAusfuehrungsServiceFactory.cs`

- `Create()` — `KiAusfuehrungsService` mit `DeterministicPseudoConsoleProcessLauncher` (MemoryStreams, `IntPtr.Zero` NativeHandle)

### `WpfTestBase` / `WpfUnitTestHelpers` / `ElementWaitHelper`
Dateien: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs` (1193 Zeilen), `src/Softwareschmiede.Tests/Helpers/WpfUnitTestHelpers.cs`, `src/Softwareschmiede.Tests/E2E/ElementWaitHelper.cs`

- `WpfTestBase`: FlaUI-App-Start (`LaunchApp`, `ResolveAppExePath` mit `SOFTWARESCHMIEDE_E2E_APP_PATH`-Override), Fenster-/Element-Wartehelfer (`WaitForElement`, `WaitWhileMainHandleIsMissing`), Projekt-/Aufgaben-Setup, `SkipWennConPtyNichtVerfuegbar`, `AppStartupLogInspector`-Integration (liest App-Log `logs/softwareschmiede-*.log` für Startup-Exceptions)
- `WpfUnitTestHelpers`: STA-Thread-Ausführung für WPF-Unit-Tests ohne App-Start

### `ConPtyEnvironmentProbe` / `OsInterface*`-Attribute
Dateien: `src/Softwareschmiede.Tests/E2E/ConPtyEnvironmentProbe.cs`, `src/Softwareschmiede.Tests/Infrastructure/Testing/{OsInterfaceAttribute,OsInterfaceFactAttribute,OsInterfaceTheoryAttribute,OsInterfaceTraitDiscoverer,TestCategories}.cs`

- `ConPtyEnvironmentProbe.IsAvailable` — liest `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` (kein automatischer Probe mehr, siehe Kommentar/Vorgeschichte Issue-114)
- `[OsInterface]`/`[OsInterfaceFact]`/`[OsInterfaceTheory]` + `OsInterfaceTraitDiscoverer` — setzen xUnit-Trait `Category=OsInterface` (Klasse oder Methode)

### `AppStartupLogInspector`
Datei: `src/Softwareschmiede.Tests/E2E/AppStartupLogInspector.cs` — liest das Log der gestarteten Test-App (`logs/softwareschmiede-*.log`) offset-basiert und erkennt Startup-Exceptions (Unterscheidung App-Crash vs. fehlendes Fenster).
