# Bestandsaufnahme: Konsolentestfenster für CLI-Ausgabe-Replay

Bestandsaufnahme des Terminal-/Replay-/Dialog-Codes in `Softwareschmiede.App`, `Softwareschmiede` und `Softwareschmiede.Plugin.Contracts` bezogen auf die Anforderung in `requirement.md`: Rohbyte-Mitschnitt mit Zeitstempeln, Export der Aufzeichnung, neues Konsolentestfenster mit Replay über den echten Renderpfad (`AnsiSequenceParser` → `TerminalBuffer` → `TerminalControl` via `ITerminalSession`-Stub) sowie Pausieren/Zeitraffer und Vergleichsansicht. Baut auf dem Stand nach dem Issue-271-Umbau auf (frühere Bestandsaufnahme: `docs/features/task/issue-271-077f09dcf2fb4f5d8c3659df3d5d530a-terminalintegration-verbessern/inventory.md` — verifiziert gegen den aktuellen Code).

## Zusammenfassung

**Vorhanden und direkt nutzbar:**

- Vollständige Session-Abstraktion `ITerminalSession` (seit Issue 271) mit allen vom `TerminalControl` benötigten Members — u. a. `Buffer`, `BufferChanged`, `OutputChunk`, `Exited`, `Failed`, `RebuildBufferFromReplay`. Der Referenzpfad einer echten Session liegt in `PseudoConsoleSession.ReadLoopAsync` (`src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` Z. 275–336): Replay-Buffer → `ITerminalOutputSink.OnOutputChunk` → `OutputChunk`-Event → Parse/`Buffer.Apply` unter `_renderLock` → `BufferChanged`.
- `ITerminalOutputSink` als natürlicher Mitschnitt-Hook (Rohbytes vor jeder Normalisierung, Z. 309 des Read-Loops); `TimeProvider` wird bereits über `PseudoConsoleSessionContext` gereicht und ist als Test-Hook etabliert.
- `TerminalReplayBuffer` (Rohchunks, Eingangsreihenfolge) und `PseudoConsoleSession.RebuildBufferFromReplay` als Vorbild für den deterministischen Neuaufbau.
- `AnsiSequenceParser` deckt inzwischen deutlich mehr VT-Funktionalität ab als zur Issue-271-Inventur (Alternate Screen `?47/1047/1049`, IL/DL/ICH/DCH/ECH, SU/SD, DECSTB-Scroll-Region, Save/Restore); chunk-übergreifende UTF-8-Mehrbyte-Sequenzen werden korrekt über den `Decoder`-Zustand fortgesetzt (`flush:false`, Z. 168–191) — die damals dokumentierte U+FFFD-Lücke ist geschlossen. `AnsiSequenceParser.Reset()` existiert für den Replay-Neuaufbau.
- Export-Fluss-Vorbild: `TaskDetailViewModel.ExportCliRawAsync` (Z. 2309–2346) mit `IDialogService.ShowSaveFileDialogAsync`, Dateinamens-Konvention `cli-output-{aufgabeId:N}.raw`, Fehler über `FehlerMeldung`; `CliRawExportService` (zeilenbasierter `.raw`-Export aus `ProtokollTyp.CliOutput`-Einträgen).
- Fenster-/Dialog-Konventionen: `IDialogService`/`WpfDialogService` öffnen modale `Window`-Dialoge mit `Owner = MainWindow` auf dem UI-Dispatcher; Dialog-ViewModels per DI (`AddTransient`, `App.xaml.cs` Z. 360–374) und per `GetRequiredService` im aufrufenden ViewModel aufgelöst; Seitennavigation + implizite `DataTemplate`s in `MainWindow.xaml`; Abschnitte in `SettingsView.xaml` als möglicher Einstiegspunkt.
- Test-Infrastruktur: `TestPseudoConsoleSessionFactory` (echte `PseudoConsoleSession` auf MemoryStreams, optionaler `TimeProvider`), `TestTerminalSessionFactory` (reicht `outputSink` durch), `TestKiAusfuehrungsServiceFactory`, FlaUI-E2E-Gerüst (`WpfTestBase`, View-/Dialog-Wrapper, `RunConPtyTests`-Bündelung).

**Nicht vorhanden (zentral für die Anforderung):**

- Kein zeitgestempelter Chunk-Mitschnitt: `TerminalReplayBuffer` hält nur Rohbytes ohne Zeitstempel und ist budgetbegrenzt (512 KB, älteste Chunks werden verworfen — für vollständige Mitschnitte ungeeignet); `CliOutputProtokollWriter`/`CliRawExportService` arbeiten zeilennormalisiert und verlieren Chunk-Grenzen sowie `\r`-Semantik.
- Keine Composite-`ITerminalOutputSink`: `ITerminalSessionFactory.StartAsync`/`IPseudoConsoleProcessLauncher.Start`/`PseudoConsoleSessionContext` tragen jeweils genau eine Senke; die Verdrahtung erfolgt ausschließlich in `KiAusfuehrungsService.StartTerminalSessionAsync` (Z. 211–233).
- Kein Aufzeichnungs-Dateiformat/Serialisierer und kein Export-Pfad für einen Rohbyte-Mitschnitt.
- Keine Replay-`ITerminalSession`-Implementierung und keine Wiedergabe-Steuerung (Pause/Fortsetzen, Zeitraffer-Schwelle). Stolperstellen: `ITerminalSession.Process` ist nicht-nullbar (Z. 18); `TerminalControl.OnSessionChanged` ruft zwingend `RebuildBufferFromReplay()` auf und `OnRenderSizeChanged` ruft `session.Resize` — beides muss ein Stub korrekt bedienen.
- Kein `ShowOpenFileDialogAsync` auf `IDialogService` (nur `ShowSaveFileDialogAsync`; ein direkter `Microsoft.Win32.OpenFileDialog` existiert nur in `PluginSettingEntryEditHelper`, Z. 31). Keine Konvention für nicht-modale Fenster über den Dialog-Service.
- Kein Konsolentest-View/ViewModel, kein Einstiegspunkt (Navigation/`SettingsView`/Ribbon) und keine Quell-/Rohdaten-Vergleichsansicht.

**Test-Ausgangszustand:** Build erfolgreich (0 Fehler, 1 Warnung); alle vier CI-Testlanes ausgeführt — **1911 bestanden, 1 fehlgeschlagen, 3 übersprungen** (2 ConPTY-E2E-Runner via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, 1 Nicht-Windows-Test per Design). Der eine Fehlschlag (`TerminalControlTests.ReadClipboardAndInsertAsync_SessionWechseltWaerendPaste_SchreibtInSnapshotSession`, instabiler Clipboard-Test im OsInterface-Volllauf) ist im Baseline-Nachweis dokumentiert und bestand im isolierten Wiederholungslauf. Details und TRX-Reports: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `TerminalCell`, `TerminalEvent`-Hierarchie, `TerminalBuffer`/`TerminalBufferSnapshot`, `TerminalSessionOptions`, `TerminalReplayBuffer`, `TerminalSessionStartSpec`/`TerminalSessionStartResult`, `CliProcessHandle`, `Protokolleintrag`, EventArgs
- [Logik](inventory/logic.md) — `PseudoConsoleSession` (Referenzpfad), `PseudoConsoleSessionContext`, `TerminalSessionService`, Launcher (ConPTY/Pipe), `KiAusfuehrungsService` (Senken-Verdrahtung), `CliOutputProtokollWriter`/`CliOutputLineAccumulator`, `CliRawExportService`, `AnsiSequenceParser`, `TerminalControl`, `TaskDetailView(Model)`, `IDialogService`/`WpfDialogService`, Fenster-/Navigations-Konventionen
- [Enums](inventory/enums.md) — `CliRuntimeStatus`, `CliProcessStatus`, `ProtokollTyp`, `TerminalProviderCapabilities`, `TerminalBackendEmpfehlung`, `AnsiSequenceParser.State`
- [Interfaces](inventory/interfaces.md) — `ITerminalSession`, `ITerminalOutputSink`, `ITerminalSessionFactory`, `IPseudoConsoleProcessLauncher`, `IPseudoConsoleHandle`, `IDialogService`, `ICliRawExportService`, `IKiPlugin`
- [Tests](inventory/tests.md) — Test-Ausgangszustand (Zeitpunkt, Commit, Umgebung, Läufe mit TRX-Nachweisen, dokumentierter Baseline-Fehlschlag), anforderungsrelevante Testklassen und Hilfsmethoden
