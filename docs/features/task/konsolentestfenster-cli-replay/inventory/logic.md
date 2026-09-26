# Logik — Konsolentestfenster für CLI-Ausgabe-Replay

Logikklassen, die für die Anforderung relevant sind. Nicht vorhanden (kein Pendant im Code): Aufzeichnungs-Senke mit Zeitstempeln, Composite-Senke, Aufzeichnungs-Serialisierer/Dateiformat, Replay-`ITerminalSession`, Wiedergabe-Steuerung, Konsolentest-View/ViewModel sowie ein `ShowOpenFileDialogAsync` auf `IDialogService`.

## `PseudoConsoleSession` (Referenzpfad, den das Replay nachbilden muss)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` — `sealed class PseudoConsoleSession : ITerminalSession`

Interner Konstruktor (Z. 105–143) nimmt `IPseudoConsoleHandle`, `Process`, `InputStream`, `OutputStream` und `PseudoConsoleSessionContext`; legt `Buffer = new TerminalBuffer(options.DefaultCols, options.DefaultRows)` (Z. 124), `_replayBuffer = new TerminalReplayBuffer(options.ReplayBufferByteBudget)` (Z. 125), Runtime-Status-Timer (1 s) und startet sofort `_readLoopTask = Task.Run(() => ReadLoopAsync(_readCts.Token))` (Z. 142).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ReadLoopAsync(CancellationToken)` | private (Z. 275–336) | **Referenzpfad**: `OutputStream.ReadAsync` (4-KB-Puffer) → `MarkOutputActivity()` → `_replayBuffer.Append` (Z. 307) → `_outputSink?.OnOutputChunk` (Z. 309) → `OutputChunk`-Event (Z. 311) → unter `_renderLock`: `_parser.Parse(chunk)` + `Buffer.Apply(evt)` (Z. 315–319) → `BufferChanged`-Event (Z. 321). `finally`: `_outputSink?.Complete()` (Z. 334). |
| `RebuildBufferFromReplay()` | public (Z. 341–351) | Unter `_renderLock`: `Buffer.Reset()`, frischer `AnsiSequenceParser`, alle `_replayBuffer.GetChunks()` erneut parsen+anwenden. Wird von `TerminalControl.OnSessionChanged` zwingend aufgerufen. |
| `GetReplayChunks()` | internal (Z. 355) | Snapshot der Replay-Chunks (nur Rohbytes, keine Zeitstempel). |
| `Resize(int, int)` | public (Z. 172–188) | Dedupliziert, delegiert an `_pseudoConsole.Resize`. |
| `WriteInputAsync` / `WritePromptAsync` | public (Z. 505 / 494) | Serialisiertes Schreiben (4-KB-Chunks, `_inputWriteLock`), `MarkInputActivity`. |
| `DrainOutputAsync` | public (Z. 241) | Wartet begrenzt auf `_readLoopTask`. |
| `Dispose()` | public (Z. 191–235) | Bricht Leseschleife ab, schließt Streams/PTY/natives Handle; kein synchrones Warten auf den Read-Task. |
| `NormalizeToCarriageReturn` | public static (Z. 551) | `\r\n`/`\n` → `\r`. |

Publizierte Events: `BufferChanged`, `OutputChunk`, `Exited` (einmalig via `RaiseExited`, Z. 361 — prüft Prozessende über natives Handle/`_process.HasExited`), `Failed` (Z. 416, `Failure`-Zustand wird vor dem Event gesetzt), `RuntimeStatusChanged`.

## `PseudoConsoleSessionContext`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSessionContext.cs` — `internal sealed record`

Gebündelte optionale Parameter: `Logger`, `OutputSink` (`ITerminalOutputSink?`, Z. 13), `NativeProcessHandle`, `Options`, `IsPseudoTerminal`, `TimeProvider` (Default `TimeProvider.System`, Z. 26 — vorhandener Test-Hook für Zeitstempel), `WaitingThreshold`.

## `TerminalSessionService` (`ITerminalSessionFactory`)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartAsync(aufgabeId, spec, outputSink, healthCheck, ct)` | public (Z. 52–104) | Validiert Spec/Options, liest `Terminal.ForcePtyUnavailable`-Override, `TerminalExecutableResolver.Resolve`, Preflight über `TerminalSessionDiagnostics`, Fehlerfälle werfen, Backend-Wahl (`SelectBackend`), `launcher.Start(aufgabeId, spec, outputSink)` — **genau eine Senke wird durchgereicht** (Z. 103). |
| `SelectBackend` | private (Z. 109) | E2E-Modus (`SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt) erzwingt Pipe; sonst `TerminalPreflightResult.BackendEmpfehlung`. |
| `WriteDiagnosis` | private (Z. 131) | Schreibt `[Terminal-Diagnose]`-Markerzeilen **nur** in die Senke (Protokoll), nicht in den Replay-Puffer. |

Konstanten: `ForcePtyUnavailableKey = "Terminal.ForcePtyUnavailable"` (Z. 18), `TestDatenbankPfadVariable = "SOFTWARESCHMIEDE_TEST_DB_PATH"` (Z. 23).

## `IPseudoConsoleProcessLauncher`-Implementierungen

- `Win32PseudoConsoleProcessLauncher` (`src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs`): `Start` (Z. 31) erzeugt ConPTY + Prozess, `CreatePseudoConsoleSession` (Z. 96–126) übergibt `outputSink` via `PseudoConsoleSessionContext` (inkl. `NativeProcessHandle`, `Options`, `IsPseudoTerminal=true`).
- `SimulatedPseudoConsoleProcessLauncher` (`src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs`): Pipe-Backend (`IsPseudoTerminal=false`, Z. 34), `Start` (Z. 37–87) startet Prozess mit umgeleiteten Standard-Streams und erzeugt `PseudoConsoleSession` mit `CrSubmittingInputStream` (CR→CRLF-Übersetzung, Z. 97–235) und `outputSink`. Fehlschlag beim Session-Aufbau killt den Kindprozess.

## `KiAusfuehrungsService` (Sink-Verdrahtung / Session-Erzeugung)
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` — Singleton (`App.xaml.cs` Z. 310)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartTerminalSessionAsync` | public (Z. 182–285) | Unter `_startLock`: Duplikat-Check, `WorkingDirectoryResolver`, `kiPlugin.GetTerminalStartSpecAsync` (Z. 209), **`new CliOutputProtokollWriter(aufgabeId, _scopeFactory, logger)` (Z. 211–214 — einzige erzeugte Senke)**, `_sessionFactory.StartAsync(...)` (Z. 219), `CliProcessHandle` mit `Session` + `OutputSink` (Z. 229–233), Event-Verdrahtung `Exited`/`Failed` (Z. 241–242), Pre-Wiring-Failure/-Exit-Behandlung (Z. 250–266). |
| `GetTerminalSession(aufgabeId)` | public (Z. 290) | Liefert `handle.Session` oder null — Grundlage der `TaskDetailView`-Anbindung. |
| `StopCliAsync` | public (Z. 299 ff.) | SIGTERM → 5 s → Kill. |
| `DisposeSessionResourcesAsync` | private (Z. 621–633) | `Session.DrainOutputAsync(2 s)` → `Session.Dispose()` → `OutputSink.CompleteAsync(2 s)`. |

## `CliOutputProtokollWriter` (einzige produktive `ITerminalOutputSink`)
Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs`

`OnOutputChunk` (Z. 46–56) dekodiert über `CliOutputLineAccumulator` und reiht **Zeilen** (nicht Rohbytes) in einen bounded `Channel<string>` (4096) ein; `ProcessLinesAsync` persistiert via `ProtokollService.AddCliOutputAsync` + Rate-Limit-Marker-Scan. `Complete` (Z. 59) / `CompleteAsync(timeout, ct)` (Z. 68) flushen Restdaten und drainen den Worker. Für den byte-exakten Mitschnitt ungeeignet: Zeilennormalisierung verwirft Chunk-Grenzen und `\r`-Semantik; kein Zeitstempel pro Chunk.

## `CliOutputLineAccumulator`
Datei: `src/Softwareschmiede/Application/Services/CliOutputLineAccumulator.cs`

`Append(ReadOnlySpan<byte>)` (Z. 15): chunk-übergreifende UTF-8-Dekodierung via `Decoder(flush:false)`, zerlegt an `\r`/`\n` in Zeilen (`\r\n`-Doppelauslösung via `_skipNextLf` vermieden). `Flush()` (Z. 31) liefert Restzeile.

## `CliRawExportService` (`ICliRawExportService`)
Datei: `src/Softwareschmiede.App/Services/CliRawExportService.cs` — Singleton (`App.xaml.cs` Z. 324)

`ExportCliRawAsync(aufgabeId, zielPfad, ct)` (Z. 30–43): lädt `ProtokollService.GetByAufgabeAsync`, filtert `ProtokollTyp.CliOutput`, joint mit `Environment.NewLine`, schreibt UTF-8 ohne BOM. Zeilenbasiert — deckt den geforderten Rohbyte-Mitschnitt nicht ab.

## `AnsiSequenceParser`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs`

Zustandsbehafteter VT100/ANSI-Parser (`sealed`). `Parse(ReadOnlySpan<byte>)` (Z. 42–156) liefert `IEnumerable<TerminalEvent>`; `Reset()` (Z. 160–166) setzt State-Maschine und UTF-8-Decoder zurück. **Seit Issue-271-Umbau vorhanden:** chunk-übergreifende UTF-8-Mehrbyte-Sequenzen bleiben im `Decoder`-Zustand (`FlushText`, Z. 168–191, `flush:false` — unvollständige Bytes werden konsumiert und beim nächsten `Parse` fortgesetzt). CSI-Abdeckung u. a. A–F, G, H, d, e, `, a, J, K, L, M, @, P, X, S, T, r, s, u, m (SGR inkl. 256/TrueColor, Bold/Dim/Underline), `?25`, `?47/1047/1048/1049`; OSC wird bis BEL/`ESC \` überlesen (Z. 136–149); Charset-Selektoren `ESC ( ) * #` übersprungen (Z. 90–104).

## `TerminalControl`
Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs` — `sealed class TerminalControl : FrameworkElement, IScrollInfo`

Reiner Renderer; die Leseschleife liegt in der Session.

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `Session` (DependencyProperty `SessionProperty`) | public (Z. 41–52) | `ITerminalSession`-Bindung. |
| `OnSessionChanged` | private (Z. 94–132) | Deregistriert `BufferChanged` der alten Session; bei neuer Session: `BufferChanged` subscribieren (Z. 120), **`session.RebuildBufferFromReplay()` (Z. 124 — Pflicht für jede `ITerminalSession`-Implementierung)**, `_buffer = session.Buffer`, `_buffer.Resize(cols, rows)` (Z. 126). |
| `OnBufferChanged` | private (Z. 134) | `InvalidateVisual` via Dispatcher. |
| `OnRenderSizeChanged` | protected (Z. 301–317) | `buffer.Resize` + `session.Resize(cols, rows)` (Z. 311) — Replay-Stub muss `Resize` tolerieren. |
| `OnPreviewKeyDown` / `OnTextInput` | protected (Z. 243/268) | Tastatur/Paste nur bei `Session?.InputStream != null`; schreibt via `session.WriteInputAsync`. |
| `ReadClipboardAndInsertAsync(session)` | private async (Z. 493) | Zwischenablage → `KeyToVt100Encoder.EncodeClipboardText` → `WriteToInputStreamAsync`. |
| `GetClipboardText` | private (Z. 523) | Fehler → Leerstring + Log-Warnung. |
| `OnRender` | protected (Z. 152) | Zeichnet `buffer.GetSnapshot()` mit `System.Drawing.Color`→`SolidColorBrush`-Cache; Follow-End-Scrolling. |

## `TaskDetailViewModel` / `TaskDetailView` (Export-Einstiegspunkt)
Dateien: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`, `src/Softwareschmiede.App/Views/TaskDetailView.xaml(.cs)`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `ExportCliRawCommand` | public (Z. 647; erzeugt Z. 779 via `AsyncRelayCommand`) | Löst `ExportCliRawAsync` aus. |
| `KannCliRawExportieren` | public (Z. 557) | `true` wenn Aufgabe geladen. |
| `ExportCliRawAsync` | private async (Z. 2309–2346) | `ShowSaveFileDialogAsync` mit `cli-output-{aufgabeId:N}.raw`, `.raw`-Endung erzwungen, `_cliRawExportService.ExportCliRawAsync`, Fehler → `FehlerMeldung`. |
| `TerminalSessionGestartet` | public event `Action<ITerminalSession>` (Z. 670) | Löst Session-Bindung im Code-behind aus. |
| `GetTerminalSession()` | public (Z. 683) | Delegiert an `KiAusfuehrungsService.GetTerminalSession`. |

View: Ribbon-Gruppe `CLI` (`TaskDetailView.xaml` Z. 85–107) mit `RibbonLargeButton` `CliRawExport` (Z. 104–107); `TerminalControl x:Name="TerminalConsole"` (Z. 488) in `ScrollViewer`. Code-behind: `SetTerminalSession` (Z. 144–148) setzt `TerminalConsole.Session` + `AutomationProperties.HelpText` = Prozess-ID (`TryGetProcessId`, Z. 161 — fängt `InvalidOperationException` bei beendetem Prozess ab; Zugriff auf `session.Process.Id`).

## `IDialogService` / `WpfDialogService` (Dialog-Konventionen)
Dateien: `src/Softwareschmiede.App/Services/IDialogService.cs`, `WpfDialogService.cs` — Singleton (`App.xaml.cs` Z. 323)

Alle Dialoge sind **modal** (`ShowDialog`, `Owner = Application.Current.MainWindow`, auf dem UI-Dispatcher via `Dispatcher.InvokeAsync`). Vorhandene Methoden: `BestaetigenDialog`, `RepositoryZuweisenDialog`, `ArbeitsverzeichnisBearbeitenDialog`, `ShowPluginSelectionDialogAsync`, `ShowIssueSelectionDialogAsync`, `ShowIssueCreateDialogAsync`, `ShowOpenTodosDialogAsync`, `ShowSolutionSelectionDialogAsync`, `ShowAufgabePausierenDialogAsync`, `ShowAutonomAufgabeInitialisierungsDialogAsync`, `ShowSaveFileDialogAsync` (Z. 152–178, `Microsoft.Win32.SaveFileDialog`). **Es gibt kein `ShowOpenFileDialogAsync`** — ein `Microsoft.Win32.OpenFileDialog` wird nur einmal direkt in `PluginSettingEntryEditHelper.OnDateiAuswaehlenClick` (`src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs` Z. 31–41) verwendet. Keine Methode zum nicht-modalen Öffnen eines Fensters vorhanden; Dialog-ViewModels werden per DI (`AddTransient`, `App.xaml.cs` Z. 360–374) über `_serviceProvider.GetRequiredService<...>()` erzeugt und dem Dialog-Konstruktor übergeben (Konvention: `Window`-Klasse mit `viewModel`-Ctor, `DataContext = viewModel`, `CloseRequested`-Event, vgl. `AufgabePausierenDialog`).

## Fenster-/Navigations-Konventionen
- `MainWindow.xaml`: implizite `DataTemplate`s (Z. 13–29) für `DashboardViewModel`, `ProjectListViewModel`, `ProjectDetailViewModel`, `TaskDetailViewModel`, `SettingsViewModel`; seitliche Navigation über `NavigateToDashboard/ProjectList/SettingsCommand` (`MainWindowViewModel` Z. 102–158).
- `SettingsView.xaml`: Abschnitte u. a. „Arbeitsverzeichnis", „Automatisierung", „Updates", Plugin-Listen, Prompt-Vorlagen; CLI-Hilfe-Button `CliHilfeButton` (Z. 77–81) öffnet `HelpTextDialog` direkt im Code-behind (`SettingsView.xaml.cs` Z. 95–112, `Owner = Window.GetWindow(this)`).
- DI: neue Services `services.AddSingleton<...>`/`AddTransient<...>` in `App.xaml.cs` (`ConfigureServices`, Z. ~226–377); `TerminalSessionOptions`-Binding Z. 233.
