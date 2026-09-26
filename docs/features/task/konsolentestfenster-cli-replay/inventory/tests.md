# Tests — Konsolentestfenster für CLI-Ausgabe-Replay

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-26, ca. 14:12–14:26 Uhr MESZ (UTC+2; Rechner `DESKTOP-CM8OBSG`)
- **Branch und Commit-ID:** `task/konsolentestfenster-cli-replay`, Commit `7e007c63684063207d7a70cfb75e1846ca03f70c` (2026-09-25 23:09:21 +0200)
- **Uncommittete Änderungen im getesteten Stand:** keine Änderungen an getrackten Dateien; untracked: `.agents/` (Lifecycle-Skills) und `docs/features/task/konsolentestfenster-cli-replay/` (Anforderungs-/Inventardateien inkl. der hier abgelegten TRX-Reports). Getesteter Stand entspricht damit dem Commit-Inhalt.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows 10/11 (WPF-Desktop), .NET SDK `10.0.401`, Test-Runtime `.NET 10.0.12`, xUnit.net VSTest-Adapter 3.1.5. Alle Läufe mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (Sandbox-Regel: ConPTY-Kindprozesse werden unter `dotnet test` nicht isoliert — betroffene Tests werden sauber übersprungen statt mit Timeout zu scheitern).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte, je zwei Kategorie-Lanes — Quelle: `.github/workflows/pr-staging-ci.yml` (Z. 121–146) und `CLAUDE.md` (stabile Lane = `Category!=OsInterface`; `Category=OsInterface` separat, enthält E2E/FlaUI, ConPTY, Clipboard u. a.):
  - `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` (`net10.0-windows10.0.17763.0`, xUnit + FlaUI)
  - `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` (`net10.0`)
  - Vor allen Testläufen: `dotnet build Softwareschmiede.slnx -c Debug` — **erfolgreich** (0 Fehler, 1 Warnung `CS8602` in `CliOutputProtokollWriterTests.cs` Z. 123, Dauer ~15 s).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | — | — | — | Konsolenlog (kein TRX) |
| 1 Regulär | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface" --results-directory docs/features/task/konsolentestfenster-cli-replay/inventory/test-results --logger "trx;LogFileName=test-results-regular-tests.trx"` | Repo-Root | 0 | 1781 | 0 | 1 | [TRX](test-results/test-results-regular-tests.trx) |
| 2 Regulär Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface" --results-directory docs/features/task/konsolentestfenster-cli-replay/inventory/test-results --logger "trx;LogFileName=test-results-regular-integration.trx"` | Repo-Root | 0 | 69 | 0 | 0 | [TRX](test-results/test-results-regular-integration.trx) |
| 3 OsInterface | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface" --results-directory docs/features/task/konsolentestfenster-cli-replay/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-tests.trx"` | Repo-Root | 1 | 52 | 1 | 2 | [TRX](test-results/test-results-os-interface-tests.trx) |
| 4 OsInterface Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface" --results-directory docs/features/task/konsolentestfenster-cli-replay/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-integration.trx"` | Repo-Root | 0 | 9 | 0 | 0 | [TRX](test-results/test-results-os-interface-integration.trx) |
| 5 Wiederholungslauf (isoliert) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "FullyQualifiedName~ReadClipboardAndInsertAsync_SessionWechseltWaerendPaste_SchreibtInSnapshotSession" --results-directory docs/features/task/konsolentestfenster-cli-replay/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-rerun-clipboard.trx"` | Repo-Root | 0 | 1 | 0 | 0 | [TRX](test-results/test-results-os-interface-rerun-clipboard.trx) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|-------------------|----------------------------|-------------------|
| `Softwareschmiede.Tests.App.Controls.TerminalControlTests.ReadClipboardAndInsertAsync_SessionWechseltWaerendPaste_SchreibtInSnapshotSession` | `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs` (Z. 226–245, Assert Z. 242) | `Expected inputStreamA.ToArray() to be equal to {0x73, ...} ("snapshot paste" kodiert), but found empty collection.` — `TerminalControl.GetClipboardText()` lieferte im Volllauf einen Leerstring, sodass nichts geschrieben wurde. | Lauf 3 (OsInterface-Volllauf), [TRX](test-results/test-results-os-interface-tests.trx). **Isolierter Wiederholungslauf (Lauf 5) bestanden** → instabil im Volllauf: Die Zwischenablage ist eine prozessübergreifende OS-Ressource; der Test nutzt (im Gegensatz zu den Nachbartests mit `InvokeReadClipboardAndInsertAsyncWithClipboardRetry`) keine Lese-Wiederholung — im Testcode selbst als bekanntes Rauschen dokumentiert (Kommentar Z. 18–28). Weitere Clipboard-Tests desselben Laufs bestanden. |

### Testlücken und Ausführungsprobleme

Übersprungene Tests (alle absichtlich/mechanisch begründet, keine Infrastrukturfehler):

| Test | Lane | Grund |
|------|------|-------|
| `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | 1 (regulär) | `SkippableFact` mit `Skip.If(OperatingSystem.IsWindows())` — prüft den Nicht-Windows-Pfad, auf diesem Windows-Host per Design übersprungen |
| `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` | 3 (OsInterface) | `SkipWennConPtyNichtVerfuegbar()` via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` — bündelt 21 ConPTY-abhängige E2E-Szenarien (u. a. `CliRawExport_*`, `ConPtyLifecycle_*`, Terminal-Interaktionen); in dieser Sandbox nicht ausführbar, in CI/interaktiver Session regulär grün |
| `Softwareschmiede.Tests.E2E.E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` | 3 (OsInterface) | Ebenfalls `SkipWennConPtyNichtVerfuegbar()` — startet echte CLI-Prozesse |

Damit: **1782 + 69 + 55 + 9 = 1915 entdeckte Tests**; davon 1911 bestanden, 1 fehlgeschlagen, 3 übersprungen (plus isolierter Wiederholungslauf mit 1 bestandenem Test). Der OsInterface-Lauf deckt den echten `Win32PseudoConsoleProcessLauncher`/ConPTY-Pfad mangels Sandbox-Isolierung nicht ab (E2E fährt bewusst auf dem simulierten Pipe-Backend über `SimulatedPseudoConsoleProcessLauncher`, erzwungen durch `SOFTWARESCHMIEDE_TEST_DB_PATH`).

Für den Anforderungsbereich selbst existieren noch keine Tests (Recorder, Replay-Format, Replay-Session, Konsolentestfenster sind neu).

## Testklassen (bestehend, anforderungsrelevant)

### `PseudoConsoleSessionTests` (+ `PseudoConsoleSessionTests_WriteInputAsync`, `PseudoConsoleSessionTests_WritePromptAsync`)
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`

- `ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin` (Z. 234) — Senke erhält Rohchunks, Buffer wird parallel befüllt.
- `ReadLoopAsync_OutputChunk_EventEnthaeltRohbytes` (Z. 255) — `OutputChunk`-Event-Vertrag (Rohbytes vor dem Parsen).
- `RebuildBufferFromReplay_StelltBufferzustandWiederHer` (Z. 278) — Neuaufbau aus `TerminalReplayBuffer`.
- `ReadLoopAsync_BufferChangedFiredAfterBufferUpdated` (Z. 210) — Event-Reihenfolge.
- `ReadLoopAsync_EofOhneProzessende_KeinExitedEvent` (Z. 308), `ReadLoopAsync_Lesefehler_SetztFailureZustand` (Z. 333), `IsPseudoTerminal_SpiegeltBackend` (Z. 322), `Resize_IdentischeDimensionen_WirdDedupliziert` (Z. 297), Dispose-/Drain-Tests — Lebenszyklus-Vertrag, den eine Replay-Session spiegeln muss.

### `TerminalReplayBufferTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplayBufferTests.cs`

- `ReplayBuffer_BudgetKleinerGleichNull_WirftArgumentOutOfRange`, `Append_UndGetChunks_LiefernChunksInReihenfolge`, `Append_BudgetUeberschreitung_VerwirftAeltesteChunks`, `Append_ChunkGroesserAlsBudget_BehaeltNurLetzteBytes`, `Append_LeererChunk_WirdIgnoriert` — dokumentiert das Budget-Verhalten (für vollständige Mitschnitte ungeeignet).

### `AnsiSequenceParserTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/AnsiSequenceParserTests.cs` — 21 Facts/Theories: CSI-Befehle, SGR, chunk-übergreifende UTF-8-Dekodierung, OSC-Überlesen, `Reset()`.

### `TerminalBufferTests`
Datei: `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs` — 35 Facts/Theories: `Apply`-Semantik aller `TerminalEvent`-Typen, Resize, Scrollback, Alternate Screen.

### `CliOutputProtokollWriterTests` / `CliOutputLineAccumulatorTests`
Dateien: `src/Softwareschmiede.Tests/Application/Services/CliOutputProtokollWriterTests.cs` (16), `CliOutputLineAccumulatorTests.cs` (6) — Zeilen-Normalisierung, Queue-/Drain-Verhalten.

### `TerminalSessionServiceTests` / `TerminalSessionDiagnosticsTests` / `TerminalExecutableResolverTests` / `SimulatedPseudoConsoleProcessLauncherTests`
Dateien unter `src/Softwareschmiede.Tests/Infrastructure/Terminal/` — Factory-/Preflight-/Resolver-Verträge (Senken-Durchreichung, Diagnose-Markerzeilen, Backend-Wahl).

### `TerminalControlTests` (+ `TerminalControlTests.ClipboardPaste`)
Dateien: `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`, `…/TerminalControlTests.ClipboardPaste.cs` — Session-Bindung (`OnSessionChanged`, BufferChanged-De/Registrierung, `RebuildBufferFromReplay`-Aufruf), Scrolling, Eingabe/Paste. Clipboard-Tests sind `OsInterfaceFact`.

### `TaskDetailViewModelTests_CliRawExport`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_CliRawExport.cs` — Export-Fluss mit `Mock<IDialogService>` (`ShowSaveFileDialogAsync`), `TestKiAusfuehrungsServiceFactory`, echtem `ProtokollService` auf In-Memory-DB (`TestDbContextFactory`).

### `KiAusfuehrungsServiceTests` (+ `…_WorkingDirectory`, `…_InSourceDirectory`)
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs` — Session-Start, Senken-Erzeugung, Exit-/Failure-Verdrahtung.

### `End2EndTest` (E2E, FlaUI)
Dateien: `src/Softwareschmiede.Tests/E2E/MainTest.cs` + partielle Szenario-Dateien

- `RunGeneralTests` (Z. 18–66): UI-Szenarien ohne ConPTY.
- `RunConPtyTests` (Z. 72–102): ConPTY-abhängige Szenarien, darunter `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` und `CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E` (`E2E_CliRawExport.cs`) — diese automatisieren den Save-Dialog (`TaskDetailView.ExportCliRaw(zielPfad)` → `HandleSaveFileDialog`, `E2E/Views/TaskDetailView.cs` Z. 336 ff.).

## Hilfsmethoden / Test-Doubles

### `TestPseudoConsoleSessionFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestPseudoConsoleSessionFactory.cs`

- `Create(inputStream, outputStream, logger?, outputSink?, options?)` — echte `PseudoConsoleSession` mit `NullPseudoConsoleHandle`, `Process.GetCurrentProcess()`, Streams; optionaler `TimeProvider`-Overload (`Create(..., TimeProvider, TimeSpan waitingThreshold, ...)`) für zeitgesteuerte Tests — direkt wiederverwendbar für Recorder-/Sink-Tests.

### `TestTerminalSessionFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestTerminalSessionFactory.cs`

- `StartAsync` delegiert an `IPseudoConsoleProcessLauncher` (Default: interner `DeterministicPseudoConsoleProcessLauncher` mit MemoryStreams); reicht `outputSink` durch — für `KiAusfuehrungsService`-Tests.

### `WpfTestBase` (E2E-Infrastruktur)
Datei: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`

- `LaunchApp(...)` (Z. 170 ff.): startet `Softwareschmiede.App.exe` mit `SOFTWARESCHMIEDE_TEST_DB_PATH` (Test-SQLite, erzwingt Pipe-Backend); `SOFTWARESCHMIEDE_E2E_APP_PATH`-Override möglich.
- `SkipWennConPtyNichtVerfuegbar()` (Z. 922): `Skip.If(!ConPtyEnvironmentProbe.IsAvailable)` — benötigt `[SkippableFact]`.
- `WarteAufEchtesHauptfenster`, `ElementWaitHelper`-Timeouts (`Short`/`Medium`/`Long`), `SetupProjectMitNeuerAufgabe*`, `SeedCliOutputForAktuelleAufgabe`, `ConfirmLocalDirectoryGitInitInSourceDirectory`.
- View-Wrapper unter `src/Softwareschmiede.Tests/E2E/Views/` (u. a. `TaskDetailView` mit `Start`, `SwitchPanel`, `ExportCliRaw`, `WaitForCliRunning`; `Dialogs/`-Wrapper pro Dialogfenster, `DialogView`-Basisklasse, `WindowExtensions`-Dialogerkennung).

### Weitere
- `OsInterfaceAttribute`/`OsInterfaceFactAttribute` (`src/Softwareschmiede.Tests/Infrastructure/Testing/`) — markieren `Category=OsInterface`.
- `TestDbContextFactory`, `TestKiAusfuehrungsServiceFactory` (`src/Softwareschmiede.Tests/Helpers/`) — In-Memory-DB bzw. `KiAusfuehrungsService` mit Test-Factory.
- `WpfUnitTestHelpers.RunOnSta` (`src/Softwareschmiede.Tests/Helpers/WpfUnitTestHelpers.cs` Z. 31) — STA-Thread-Ausführung für WPF-Unit-Tests.
