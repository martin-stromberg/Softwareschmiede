# Interfaces — Konsolentestfenster Schrittmodus

## `ITerminalSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs` (74 Zeilen)

| Methode / Member | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `InputStream` | — | `Stream` | Eingabe-Stream (Replay: `Stream.Null`) |
| `OutputStream` | — | `Stream` | Ausgabe-Stream (Replay: `Stream.Null`) |
| `Process` | — | `Process` | Verwalteter Prozess (Replay: nicht gestarteter Stub) |
| `Buffer` | — | `TerminalBuffer` | Gerenderter Buffer der Session |
| `RuntimeStatus` | — | `CliRuntimeStatus` | Laufzeitstatus |
| `IsPseudoTerminal` | — | `bool` | ConPTY ja/nein (Replay: `false`) |
| `ExitCode` | — | `int?` | Exit-Code (Replay: `null`) |
| `Failure` | — | `TerminalSessionFailedEventArgs?` | Erster fataler Fehler, vor `Failed` gesetzt (Replay: `null`) |
| `WriteInputAsync(bytes, ct)` | `ReadOnlyMemory<byte>`, `CancellationToken` | `Task` | Serialisierte Eingabe (Replay: No-Op) |
| `WritePromptAsync(prompt, ct)` | `string`, `CancellationToken` | `Task` | Prompt + CR (Replay: No-Op) |
| `Resize(cols, rows)` | `int`, `int` | `bool` | Geometrieänderung (Replay: immer `true`; Control resized `Buffer` selbst) |
| `MarkInputActivity()` | — | `void` | Input-Aktivität melden (Replay: No-Op) |
| `MarkOutputActivity()` | — | `void` | Output-Aktivität melden (Replay: No-Op) |
| `DrainOutputAsync(timeout, ct)` | `TimeSpan`, `CancellationToken` | `Task<bool>` | Warten auf verarbeitete Ausgabe (Replay: sofort `true`) |
| `RebuildBufferFromReplay()` | — | `void` | Synchroner Buffer-Neuaufbau aus gespeicherten Replay-Chunks — wird von `TerminalControl.OnSessionChanged` bei jeder Neubindung aufgerufen |
| `OutputChunk` | — | `event EventHandler<TerminalOutputChunkEventArgs>?` | Pro gelesenem/angewendetem Roh-Chunk (vor dem Parsen) |
| `Exited` | — | `event EventHandler<TerminalSessionExitedEventArgs>?` | Session-Ende mit Exit-Code |
| `Failed` | — | `event EventHandler<TerminalSessionFailedEventArgs>?` | Fataler Laufzeitfehler |
| `BufferChanged` | — | `event EventHandler?` | Nach jeder verarbeiteten Ausgabe (UI-Invalidierung + ViewModel-Positionsupdate) |
| `RuntimeStatusChanged` | — | `event EventHandler<CliRuntimeStatusChangedEventArgs>?` | Statuswechsel |

Erbt `IDisposable`. Befund: Die bestehenden Abspiel-Member der `TerminalReplaySession` (`WiedergabeStarten`, `Pausieren`, `Fortsetzen`, `ZeitrafferSchwelle`, `AktuellerChunkIndex`, `IstPausiert`) sind **nicht** Teil der Schnittstelle — Schritt-Member würden dem Muster folgend ebenfalls nur auf der konkreten Klasse leben (`KonsolenTestViewModel` hält `_replaySession` als `TerminalReplaySession`, Z. 23).

## `ITerminalOutputSink`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalOutputSink.cs`

Mitschnitt-Senke (Implementierung: `CliOutputRecorder`); erhält Rohbytes vor jeder Normalisierung (`OnOutputChunk(ReadOnlySpan<byte>)`, `Complete()`, `CompleteAsync(timeout, ct)`). Für den Schrittmodus ohne Änderungsbedarf — liefert den Aufzeichnungsstrom, aus dem `.clireplay`-Dateien entstehen.

## `IDialogService` (relevante Member)
Datei: `src/Softwareschmiede.App/Services/IDialogService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ShowOpenFileDialogAsync(title, filter, initialDirectory, ct)` | `string`, `string`, `string?`, `CancellationToken` | `Task<string?>` | Öffnen-Dialog (nativer `OpenFileDialog`), Pfad oder `null` — lädt die `.clireplay`-Datei |
| `ShowKonsolenTestDialogAsync(viewModel, ct)` | `KonsolenTestViewModel`, `CancellationToken` | `Task` | Nicht-modales Konsolentestfenster (`dialog.Show()`, `Owner = MainWindow`, WpfDialogService Z. 208–225) |
| `ShowSaveFileDialogAsync(...)` | `string`, `string`, `string`, `string?`, `CancellationToken` | `Task<string?>` | Speichern-Dialog (Export-Pfad der `.clireplay`-Erzeugung) |

Übrige Member (Solution-/Pause-/Initialisierungs-/Save-Dialoge) für diese Anforderung nicht relevant.
