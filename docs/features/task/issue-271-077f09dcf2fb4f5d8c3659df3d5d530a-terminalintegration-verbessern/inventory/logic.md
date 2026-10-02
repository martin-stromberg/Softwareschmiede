# Bestandsaufnahme: Logik (Terminalintegration)

Logikklassen im Bereich PTY-Session, Prozessstart, Rendering, Protokollierung und Aufrufer.

## Session-Schicht (Infrastructure/Terminal)

### `PseudoConsoleSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (456 Zeilen)

`sealed class`, implementiert `IDisposable`. Koordiniert `IPseudoConsoleHandle` + `Process` + Input-/Output-Stream. Die Leseschleife läuft ab Konstruktion bis `Dispose`, unabhängig vom UI-Lebenszyklus (Issue-86). Konstruktor ist `internal` (Tests via `InternalsVisibleTo`).

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|--------------|------------------|
| `InputStream` | public `Stream` | Schreibbarer Eingabe-Stream an den Prozess |
| `OutputStream` | public `Stream` | Lesbarer Ausgabe-Stream |
| `Process` | public `Process` | Verwalteter Prozess |
| `RuntimeStatus` | public `CliRuntimeStatus` | Laufzeitstatus (thread-sicher via `_runtimeStatusLock`) |
| `Buffer` | public `TerminalBuffer` | Terminal-Buffer (220×50 Default), wird von der Leseschleife befüllt |
| `MarkOutputActivity()` | public | Meldet gelesene Ausgabe an Status-Erkennung |
| `MarkInputActivity()` | public | Meldet Benutzereingabe an Status-Erkennung |
| `Resize(int cols, int rows)` | public `bool` | Delegiert an `IPseudoConsoleHandle.Resize` (short-Cast) — ändert **nicht** den eigenen `TerminalBuffer` |
| `Dispose()` | public | Atomares Check-and-Set (`Interlocked`); bricht Leseschleife ab (`_readCts.Cancel` + `OutputStream.Dispose`), disposed Input/Timer/Handle/Prozess; wartet **nicht** synchron auf `_readLoopTask` |
| `DrainOutputAsync(TimeSpan, CancellationToken)` | public `Task<bool>` | Wartet begrenzt auf das Ende der Leseschleife (vor Dispose) |
| `ReadLoopAsync(CancellationToken)` | private | 4096-Byte-Buffer; `OutputStream.ReadAsync` → `MarkOutputActivity` → `_outputSink?.OnOutputChunk` → `_parser.Parse` → `Buffer.Apply` pro Event → `BufferChanged` feuern; `finally` → `_outputSink?.Complete()` |
| `RefreshRuntimeStatus()` | private | Timer-Callback (1 s): `Process.HasExited` + `CliRuntimeStatusEvaluator.Determine` |
| `WritePromptAsync(string, CancellationToken)` | public `Task` | UTF-8-kodiert `NormalizeToCarriageReturn(prompt).TrimEnd('\r') + "\r"` und schreibt via `WriteInputAsync` |
| `WriteInputAsync(ReadOnlyMemory<byte>, CancellationToken)` | public `Task` | Serialisiert Writes via `_inputWriteLock`, teilt in 4096-Byte-Chunks, flusht einmal am Ende, `MarkInputActivity` |
| `NormalizeToCarriageReturn(string)` | public static | `\r\n`/`\n` → `\r` (Enter-Äquivalent wie `KeyToVt100Encoder`) |

Publizierte Events: `BufferChanged` (nach jedem verarbeiteten Output-Chunk), `RuntimeStatusChanged`.
Abonniert: keine externen Events (eigener `Timer` für `RefreshRuntimeStatus`).
Konstanten: `DefaultCols = 220`, `DefaultRows = 50`, `InputWriteChunkSize = 4096`.

Hinweis: `RuntimeStatus`-Timer ruft `_process.HasExited` auf — beim ConPTY-Pfad ist `_process` ein `GetProcessById`-Objekt (PID-Wiederverwendung moglich, siehe `KiAusfuehrungsService.TryGetExitCode`).

### `AnsiSequenceParser`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` (307 Zeilen)

`sealed class`, zustandsbehafteter VT100/ANSI-Parser mit privatem `State`-Enum (`Normal`, `Escape`, `Csi`, `CsiQuestion`, `Osc`) und persistenten Puffern (`_paramBuffer`, `_textBuffer` als `List<byte>`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `Parse(ReadOnlySpan<byte>)` | public `IEnumerable<TerminalEvent>` | Verarbeitet einen Byte-Block; erzeugt `TerminalEvent`-Instanzen |
| `FlushText` | private | Dekodiert `_textBuffer` via `Encoding.UTF8.GetString` → `TextWrittenEvent`. **Wird am Ende jedes `Parse`-Aufrufs geflusht** — eine über Chunk-Grenzen geteilte UTF-8-Mehrbyte-Sequenz erzeugt Ersatzzeichen (U+FFFD), kein chunk-übergreifendes Decoding |
| `ProcessCsiCommand` | private static | CSI `A`/`B`/`C`/`D` (Cursor relativ), `H`/`f` (absolut), `J` (Clear), `K` (Erase Line), `m` (SGR) |
| `ProcessCsiQuestionCommand` | private static | Nur `?25` h/l → `CursorVisibilityChangedEvent`; alle anderen `?`-Modi (inkl. `?1049` Alternate Screen) werden still ignoriert |
| `ParseSgr` | private static | SGR 0/1/2/4/22/24, 30–37, 38 (256er- und TrueColor), 39, 40–47, 48, 49, 90–97, 100–107 |
| `ParseExtendedColor` | private static | 256er-Palette (`38;5;n`) und TrueColor (`38;2;r;g;b`) |
| `GetColor256` | private static | 256er-Farbtabelle (16 Standard + 6×6×6-Cube + Grayscale) |
| `ParseParams`/`GetParam` | private static | `;`-getrennte CSI-Parameter, `int.TryParse` (ungültige → 0) |

Nicht abgedeckt: Alternate Screen, Insert/Delete Line/Char, Scroll-Regionen, Tabulator-Handling, OSC-Auswertung, DCS/APC, unbekannte CSI-Kommandos werden still ignoriert; Escape-Zustand außer `ESC[`/`ESC]` fällt zurück auf `Normal` (Sequenz verworfen).

### `Win32PseudoConsoleProcessLauncher`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs` (99 Zeilen)

`sealed class`, implementiert `IPseudoConsoleProcessLauncher`. DI-Default in Produktion (`App.xaml.cs`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `Start(Guid, string, string, ITerminalOutputSink?)` | public → `(Process, PseudoConsoleSession, IntPtr)` | Startet **`cmd.exe`** (nicht die Anbieter-CLI direkt!) in einer `PseudoConsole` (220×50); `pluginCommand` wird nur geloggt — der eigentliche Befehl wird später per `KiAusfuehrungsService.SendCommandDelayedAsync` als Tastatureingabe injiziert. Arbeitsverzeichnis-Fallback auf `Path.GetTempPath()`; setzt `PATH` aus dem App-Prozess |
| `CreatePseudoConsoleSession` | private | Öffnet `FileStream`s auf `InputWritePipe` (bufferSize 1, sync) und `OutputReadPipe` (4096, sync) der ConPTY und erzeugt die `PseudoConsoleSession` |

### `SimulatedPseudoConsoleProcessLauncher`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs` (153 Zeilen)

`sealed class`, implementiert `IPseudoConsoleProcessLauncher`. Wird registriert, wenn `SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt ist (E2E-Modus, `App.xaml.cs` Zeile 287–289). **Kein echtes ConPTY**: `cmd.exe` mit `RedirectStandardInput/Output/Error` + `NullPseudoConsoleHandle`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `Start(...)` | public → `(Process, PseudoConsoleSession, IntPtr)` | Startet `cmd.exe` mit Pipe-Redirects; `NativeProcessHandle` = `IntPtr.Zero` (Exit-Code dann via `Process.ExitCode`) |
| `CrSubmittingInputStream` | private nested `Stream` | Übersetzt alleinstehendes `\r` in `\r\n`, weil `cmd.exe` auf STDIN-Pipe nur `CRLF` als Zeilenende akzeptiert |

### `PseudoConsole`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs` (81 Zeilen)

`internal sealed class`, `IPseudoConsoleHandle`. Kapselt HPCON + Pipe-Handles.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `Create(short cols, short rows)` | internal static | `CreatePipe` (Input+Output) → `CreatePseudoConsole` (kernel32); wirft `InvalidOperationException` bei Win32-Fehlern |
| `Resize(short, short)` | public `bool` | `ResizePseudoConsole` (HRESULT 0 = Erfolg) |
| `Dispose()` | public | Atomar; `ClosePseudoConsole` + `CloseHandle` auf beiden Pipes |

### `PseudoConsoleProcessStarter`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs` (162 Zeilen)

`internal static class` — roher Win32-Prozessstart mit `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `Start(ProcessStartInfo, PseudoConsole)` | internal static `ProcessStartResult` | Baut Kommandozeile (`fileName + " " + arguments`, Quoting nur bei Leerzeichen im FileName), Unicode-Environment-Block (Parent-Env + `psi.EnvironmentVariables`), `STARTUPINFOEX` mit Attribute-List, `CreateProcess` mit `EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT`; schließt `hThread`, gibt `hProcess` + PID zurück |
| `BuildCommandLine` / `BuildEnvironmentBlock` / `BuildEnvironmentPtr` | private | Kommandozeilen- und Environment-Block-Aufbau |

### `PseudoConsoleNativeMethods`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleNativeMethods.cs` (121 Zeilen)

`internal static class` — P/Invoke: `CreatePseudoConsole`, `ResizePseudoConsole`, `ClosePseudoConsole`, `CreatePipe`, `CloseHandle`, `GetExitCodeProcess`, `InitializeProcThreadAttributeList`, `UpdateProcThreadAttribute`, `DeleteProcThreadAttributeList`, `CreateProcess`; Konstanten `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`, `EXTENDED_STARTUPINFO_PRESENT`, `CREATE_UNICODE_ENVIRONMENT`, `STILL_ACTIVE=259`; Structs `COORD`, `STARTUPINFO`, `STARTUPINFOEX`, `PROCESS_INFORMATION`.

### `NullPseudoConsoleHandle`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/NullPseudoConsoleHandle.cs`

`internal sealed class`, Singleton `Instance`; `Resize` → `true`, `Dispose` → No-Op. Für simulierte Sessions und Tests.

## Prozess-Lifecycle (Application/Services)

### `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (739 Zeilen)

`sealed class`, Singleton, `IRunningAutomationStatusSource`, `IDisposable`. Verwaltet `CliProcessHandle` pro Aufgabe in `_handles` (`ConcurrentDictionary`), `_startLock` serialisiert Starts.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `StartCliAsync(Guid, IKiPlugin, string, string?, CancellationToken, RepositoryStartKonfiguration?, IGitPlugin?)` | public `Task<CliProcessHandle>` | Klassischer Pipe-Start **ohne PTY**: `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync` → `kiPlugin.StartCliAsync` → `Process.Start` mit `EnableRaisingEvents`; PATH-Ergänzung wenn `UseShellExecute=false` und kein PATH gesetzt |
| `StartWithPseudoConsoleAsync(...)` (gleiche Signatur) | public `Task<CliProcessHandle>` | ConPTY-Pfad: `pluginPsi` → `BuildCliCommand` → `CliOutputProtokollWriter` anlegen → `_launcher.Start(aufgabeId, effectiveWorkdir, pluginCommand, outputWriter)` → `CliProcessHandle` mit `PseudoConsoleSession`, `SendCts`, `NativeProcessHandle`, `OutputSink` → `Exited`-Handler → `SendCommandDelayedAsync` (Fire-and-Forget) |
| `GetPseudoConsoleSession(Guid)` | public `PseudoConsoleSession?` | Session-Lookup aus `_handles` |
| `StopCliAsync(Guid, CancellationToken)` | public `Task` | `CloseMainWindow` → 5 s warten → `Kill(entireProcessTree)` |
| `IsRunning` / `GetRunningProcess` / `GetRunningCount` / `GetLastExitCode` / `UpdateHeartbeat` | public | Statusabfragen auf `_handles` |
| `HandleProcessExitedAsync` | private | Atomares `TryRemove`; Exit-Code **vor** Aufräumen via `TryGetExitCode` (Handle wird danach geschlossen); optionales `vorAufraeumenAsync` (ConPTY: `CancelAndDisposeConPtyResourcesAsync`); Status-Mapping `AbsichtlichGestoppt`/`ExitCode!=0` → `Gestoppt`/`Fehler`; `PersistAusfuehrungBeendetAsync` + `CliProcessStatusChanged` |
| `TryGetExitCode(Process, IntPtr)` | private `int?` | Bei `NativeProcessHandle != 0`: `GetExitCodeProcess` (robust gegen PID-Wiederverwendung); sonst `Process.HasExited`/`ExitCode` mit Exception-Fallback |
| `SendCommandDelayedAsync(PseudoConsoleSession, string, Guid, CancellationToken)` | private | `Task.Delay(300)` → `WriteInputAsync(UTF8(command + "\r\n"))` — **die cmd.exe-Hülle-Injektion**, die laut Anforderung entfallen soll |
| `CancelAndDisposeConPtyResourcesAsync(CliProcessHandle)` | private | `SendCts.Cancel/Dispose` → `Session.DrainOutputAsync(2s)` → `Session.Dispose` → `OutputSink.CompleteAsync(2s)` → `CloseHandle(NativeProcessHandle)` |
| `BuildCliCommand(ProcessStartInfo)` | private static | `FileName [+ " " + Arguments]` für die cmd.exe-Injektion |
| `Dispose()` | public | Killt alle Prozesse, `CancelAndDisposeConPtyResourcesAsync` synchron |

Publizierte Events: `CliProcessStatusChanged` (`Action<Guid, CliProcessStatus>`), `RunningCountChanged` (`Action<int,int>` aus `IRunningAutomationStatusSource`).

### `CliProcessManager`
Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs` (312 Zeilen)

`sealed class`, Singleton. Heartbeat-Verwaltung + `RuntimeStatusChanged`-Brücke zur Persistenz.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `StartHeartbeat` / `StopHeartbeat` | public | 30-s-`Timer` pro Aufgabe + per-Aufgabe `SemaphoreSlim` gegen Tick-Überlappung |
| `OnCliProcessStatusChanged` | private | `Gestartet` → Heartbeat + `AktivenLaufSetzenAsync` + `SubscribeRuntimeStatus`; `Gestoppt`/`Fehler` → `StopHeartbeat` + `UnsubscribeRuntimeStatus` + `AktivenLaufBeendenAsync` |
| `SubscribeRuntimeStatus` / `UnsubscribeRuntimeStatus` | private | Abonniert `PseudoConsoleSession.RuntimeStatusChanged` via `GetPseudoConsoleSession`; Tracking in `_runtimeStatusSubscriptions` (defensive Bereinigung vor Neu-Subscribe) |
| `OnRuntimeStatusChanged` | private | `Inaktiv` ignoriert; `WartetAufEingabe`/`Laeuft` → `AufgabeService.AktualisiereLaufStatusAsync` |
| `Dispose` | public | Meldet alle Events ab, disposed Timer/Semaphoren |

Abonnierte Events: `KiAusfuehrungsService.CliProcessStatusChanged`, `PseudoConsoleSession.RuntimeStatusChanged`.

### `CliOutputProtokollWriter`
Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs` (237 Zeilen)

`sealed class`, `ITerminalOutputSink`. Rohbytes → `CliOutputLineAccumulator` → Zeilen → `Channel<string>` (bounded, 4096, `FullMode.Wait`) → Hintergrund-`ProcessLinesAsync` → `ProtokollService.AddCliOutputAsync` + Rate-Limit-Marker-Erkennung (`ProtokollService.TryParseRateLimitMarker` → `KiPluginLimitService.VerarbeiteRateLimitAsync`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `OnOutputChunk(ReadOnlySpan<byte>)` | public | Zeilen extrahieren + `TryQueueLine` (Backpressure mit Warn-Logs ab 1000 Zeilen, wiederholend alle 1000) |
| `Complete()` | public | Idempotent; flusht Restzeile, `TryComplete` auf Channel-Writer |
| `CompleteAsync(TimeSpan, CancellationToken)` | public `Task` | `Monitor.TryEnter` mit Timeout + `_workerTask.WaitAsync(remaining)` |
| `TryQueueLine` / `ProcessLinesAsync` / `PersistLineAsync` | private | Queueing, Hintergrund-Worker, DB-Persistenz via DI-Scope |

### `CliOutputLineAccumulator`
Datei: `src/Softwareschmiede/Application/Services/CliOutputLineAccumulator.cs` (86 Zeilen)

`sealed class`. UTF-8-`Decoder` (chunk-übergreifend korrekt, anders als `AnsiSequenceParser`) + zeilenbasierte Segmentierung: `\r` und `\n` schließen Zeilen ab, `\r\n` zählt nicht doppelt (`_skipNextLf`). `Append(ReadOnlySpan<byte>)` → `IReadOnlyList<string>`; `Flush()` liefert Restzeile.

### `PromptZeitVersandService`
Datei: `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` (221 Zeilen)

`sealed class`, Singleton. Plant zeitgesteuerte Prompts (`ScheduledPromptInfo`) via `TimeProvider.CreateTimer`; berücksichtigt `PausiertBisUtc` (Versand wird auf Pausenende verschoben). `SendPromptAsync` → `_kiService.GetPseudoConsoleSession` → `session.WritePromptAsync`; feuert `PromptSent` (`Action<Guid>`). Kein Session vorhanden → Warn-Log, Prompt verworfen.

### `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

Koordiniert Git-Setup + CLI-Start. `ProzessStartenUndCliStartenAsync` (Zeile 116 ff.) und `CliNeustartenAsync` (Zeile 201 ff.) rufen `_options.KiAusfuehrungsService.StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, lokalerKlonPfad, optionalParameters, ct, repository.StartKonfiguration, gitPlugin)` (Zeilen 157/239). Plugin-Auflösung über `PluginSelectionService.ResolveDevelopmentAutomationPluginAsync`.

### `ProjektleiterAgentService`
Datei: `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`

`StarteAgentAsync` ruft `StartWithPseudoConsoleAsync` (Zeile 94) mit `optionalParameters = "--continue"` bei Resume + `SupportsSessionContinuation()`; `SendeInitialPromptVerzoegertAsync` (Zeile 311) sendet nach `PromptSendeVerzoegerungMs = 3000` den Initial-/Weitermachen-Prompt via `GetPseudoConsoleSession` → `WritePromptAsync` (kein Ready-Signal — feste Verzögerung).

### `CliSessionService` (unbenutzter Alt-Pfad)
Datei: `src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs` (168 Zeilen)

`sealed class`, `ICliSessionService` (`IAsyncDisposable`). Zeilenbasierter Pipe-Pfad: `Process.Start` mit Redirects, `ReadOutputLoop`/`DrainStderrLoop` via `StandardOutput.ReadLineAsync`/`StandardError.ReadLineAsync`, `SendAsync` via `StandardInput.WriteLine`, `StopAsync` mit `Kill(entireProcessTree)`. **Repo-weit ohne Aufrufer und ohne DI-Registrierung** (Suche nur Eigentreffer + requirement.md) — de facto toter Code.

### `WorkingDirectoryResolver`
Datei: `src/Softwareschmiede/Application/Services/WorkingDirectoryResolver.cs`

`static class`, `DetermineEffectiveWorkingDirectoryAsync(localRepoPath, startConfig, gitPlugin, ct)` — löst das effektive Arbeitsverzeichnis inkl. `RepositoryStartKonfiguration`/`LocalDirectoryPlugin`-`InSourceDirectory`-Modus; wird von `KiAusfuehrungsService.StartCliAsync`/`StartWithPseudoConsoleAsync` und `GitOrchestrationService` genutzt.

## Renderer (App)

### `TerminalControl`
Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs` (526 Zeilen)

`sealed class TerminalControl : FrameworkElement, IScrollInfo`. Reiner Renderer — keine eigene Leseschleife.

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|--------------|------------------|
| `Session` (DP `SessionProperty`) | public `PseudoConsoleSession?` | Typ hart auf `PseudoConsoleSession` verdrahtet — kein Interface |
| `OnSessionChanged` | private | Alte `BufferChanged`-Registrierung lösen, neue setzen; **übernimmt `session.Buffer` direkt** (`_buffer = session.Buffer` + `Resize`) — Session-Neuanbindung nutzt den gerenderten Buffer-Zustand, kein Rohdaten-Replay |
| `OnBufferChanged` | private | `Dispatcher.InvokeAsync` → `UpdateScrollInfo` + `InvalidateVisual` |
| `OnRender` | protected override | Zeichnet Zeilen aus `GetSnapshot()` (inkl. Scrollback), Hintergrund-Rechtecke, `FormattedText` pro Zelle, Cursor-Rechteck |
| `OnPreviewKeyDown` | protected override | `Ctrl+V` → Clipboard-Paste; sonst `KeyToVt100Encoder.Encode` → `WriteToInputStream` (**synchroner** `InputStream.Write` + `MarkInputActivity`, nicht `WriteInputAsync`) |
| `OnTextInput` | protected override | `KeyToVt100Encoder.EncodeText` → `WriteToInputStream` |
| `OnRenderSizeChanged` | protected override | `MeasureCellSize` → `CalculateCols`/`CalculateRows` → `buffer.Resize` + `session.Resize` — das WPF-Äquivalent zum geforderten ResizeObserver, ohne Debounce/Filterung |
| `OnMouseDown` | protected override | `Keyboard.Focus(this)` |
| `OnCreateAutomationPeer` | protected override | `FrameworkElementAutomationPeer` (damit FlaUI `AutomationProperties.Name`/`HelpText` sieht) |
| `ReadClipboardAndInsertAsync` | private | `Clipboard.GetText` → `KeyToVt100Encoder.EncodeClipboardText` → `WriteInputAsync` auf der zum Paste-Start gesnapshotteten Session |
| `IScrollInfo`-Member | public | `LineUp/Down`, `PageUp/Down`, `MouseWheel*`, `SetVerticalOffset` (Auto-Follow-Ende), `SetHorizontalOffset`/`LineLeft`/`LineRight`/`PageLeft`/`PageRight` No-Op |

### `KeyToVt100Encoder`
Datei: `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs` (78 Zeilen)

`internal static class`. `Encode(KeyEventArgs)`: `Ctrl+A–Z` → Control-Bytes; `Ctrl+Left/Right` → `\x1b[1;5D/C`; `Enter` → `0x0D`; `Back` → `0x7F`; `Tab`, `Escape`, `Delete`/`Home`/`End`/`PageUp`/`PageDown`/Pfeile/F1–F12 → VT100-Sequenzen; `Alt`-Kombis → `null`. `EncodeText` → UTF-8. `EncodeClipboardText` → `NormalizeToCarriageReturn` + UTF-8.

### `TaskDetailView` (Code-Behind)
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs` (+ `TaskDetailView.xaml` Zeile 488: `controls:TerminalControl x:Name="TerminalConsole"` in `TerminalScrollViewer`)

Bindet Session an `TerminalControl`: `DataContextChanged` → `vm.PseudoConsoleSessionGestartet += OnPseudoConsoleSessionGestartet` + `SetTerminalSession(vm.GetPseudoConsoleSession())`; `CliGestoppt` → `SetTerminalSession(null)`; `SetTerminalSession` legt `AutomationProperties.HelpText` = Prozess-ID (E2E-Hilfskanal). `OnTerminalScrollViewerPreviewMouseDown` fokussiert das Terminal.

### `TaskDetailViewModel` (relevante Ausschnitte)
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (2376 Zeilen)

- `PseudoConsoleSessionGestartet` (`Action<PseudoConsoleSession>`), `CliGestoppt`, `PromptVorlageGesendet` (Zeilen 665–671)
- `GetPseudoConsoleSession()` → `_kiService.GetPseudoConsoleSession(_aufgabeId)` (Zeile 678)
- `LadenAsync` (Zeile 799–810): Wiederanbindung laufender Sessions beim Öffnen der Aufgabenseite
- `AttachCliStatusSession` (Zeile 1939): abonniert `RuntimeStatusChanged` → `CliStatusText`-Mapping (Laeuft/WartetAufEingabe/Inaktiv), Referenzgleichheits-Guard gegen Doppel-Subscribe
- `StartenAsync`/`StartCliAndUpdateStateAsync`/`PluginWechselAsync` (Zeilen 1779–1901): Start über `EntwicklungsprozessService`, danach Session an View feuern

### `AutonomAufgabeDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs`

Hält `KiAusfuehrungsService` (`IsRunning`, `CliProcessStatusChanged`), Start/Stop/Resume-Commands über `ProjektleiterAgentService` (Zeilen 98–137).

### `CliRawExportService` / `ICliRawExportService`
Datei: `src/Softwareschmiede.App/Services/CliRawExportService.cs`

Exportiert persistierte `ProtokollTyp.CliOutput`-Einträge als `.raw`-Datei (`ExportCliRawAsync`) — zeilenbasiert, kein Rohbyte-Replay.

## Plugin-Schicht

### `CliKiPluginBase`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs` (308 Zeilen, Namespace `Softwareschmiede.Domain.Abstractions`)

Abstrakte Basisklasse, implementiert `IKiPlugin`. Zentrale Member:

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `ProviderDateiPraefix` / `PluginName` / `PluginPrefix` / `PluginType` | public abstract | Anbieter-Metadaten |
| `GetSettingGroups` | public abstract | Plugin-Einstellungen (u. a. `CommandLineParameters`, Tokens) |
| `BuildProcessStartInfo(string, string?)` | protected abstract `ProcessStartInfo` | **Einzige Startbeschreibung** — Executable, Argumente, Arbeitsverzeichnis, Env-Vars kommen hier zusammen |
| `StartCliAsync` | public `Task<ProcessStartInfo>` | Delegiert an `BuildProcessStartInfo` (synchron, Task.FromResult) |
| `GetCliHelpTextAsync` / `RunHelpCommandAsync` | virtual / protected static | `--help`-Aufruf (10 s Timeout) für den Einstellungs-Hilfedialog |
| `ResolveExecutablePath` | protected static | `{prefix}.ExecutablePath` aus Credential Store |
| `AppendCommandLineParameters` | protected static | `{prefix}.CommandLineParameters` an `psi.Arguments` anhängen |
| `CheckHealthWithVersionCommandAsync` | protected static `Task<bool>` | `<exe> --version`, ExitCode 0, 10 s Timeout — vorhandene Pre-Flight-Basis für CLI-Verfügbarkeit |
| `GetProcessWindowTitle` / `SupportsSessionContinuation` / `CheckHealthAsync` | virtual/abstract | Fenstertitel-Hinweis, Session-Fortsetzung, Healthcheck |
| `RunOneShotTextGenerationAsync` | protected static | Nicht-interaktiver Einmal-Prompt (`-p`-Modus) für `IIssueTemplateTextGenerator` |
| `DiscoverAgents` | protected static | Agenten-MD-Dateien aus Agentenpaketen |

### Plugin-Implementierungen unter `plugins/`
Alle `sealed`, `CliKiPluginBase` + `IIssueTemplateTextGenerator`, Namespace `Softwareschmiede.Infrastructure.Plugins`:

| Plugin | Datei | CLI | `SupportsSessionContinuation` |
|--------|-------|-----|-------------------------------|
| `ClaudeCliPlugin` | `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs` | `claude` (PATH-Suche `FindClaudeExecutable`, `ANTHROPIC_API_KEY` Env) | `true` |
| `CodexPlugin` | `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs` | `codex` (`GetCodexCommand`) | `false` |
| `GitHubCopilotPlugin` | `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` | `copilot` (`GetCopilotCommand`) | `false` |
| `DevinPlugin` | `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs` | `devin` (`GetDevinCommand`) | `true` |
| `KiSimulatorPlugin` | `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs` | `cmd.exe /c echo ... && ping -n 31 127.0.0.1` (Test-CLI, keine Env/Settings) | `false` |

Keine der Implementierungen deklariert PTY-/Terminal-Fähigkeiten — ein solcher Mechanismus existiert nicht.

## Sonstige relevante Artefakte

### `terminal-backend` (Node.js-Prototyp)
Verzeichnis: `src/Softwareschmiede/terminal-backend/` (`server.js` 71 Zeilen, `package.json` mit `node-pty ^1.0.0` + `ws ^8.17.0`, `package-lock.json`)

WebSocket-Server (Port 3001) mit `node-pty`-Prozessmanagement: `SET_CWD:` → `pty.spawn` (powershell/bash), `START_CLI:` → `ptyProcess.write("copilot\r"/"claude\r")`, sonstige Nachrichten → `write`. **Keine Referenz aus C#-Code oder Build** — nicht in `Softwareschmiede.csproj` eingebunden; vermutlich früherer Integrationsprototyp (die Anforderung referenziert `node-pty`).

### Hilfsdokumentation
`docs/help/terminal/` enthält `architektur.md` (vollständige Ist-Architektur inkl. Datenfluss-Diagrammen; ConPTY-Voraussetzung Windows 10 Build 17763+, Zeile 271), `ablauf-anwender.md`, `ablauf-technisch.md`, `api.md`, `eingabeverarbeitung.md`, `beschreibung.md`, `installation.md`, `index.md`. Das TargetFramework `net10.0-windows10.0.17763.0` + `TargetPlatformMinVersion` in `Softwareschmiede.App.csproj` setzt das ConPTY-Minimum bereits auf Compile-Ebene.
