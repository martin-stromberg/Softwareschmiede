# Bestandsaufnahme: Interfaces (Terminalintegration)

## `IPseudoConsoleProcessLauncher`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/IPseudoConsoleProcessLauncher.cs`

Austauschpunkt für den ConPTY-Prozessstart in `KiAusfuehrungsService`. Implementierungen: `Win32PseudoConsoleProcessLauncher` (Produktion), `SimulatedPseudoConsoleProcessLauncher` (E2E-Modus bei `SOFTWARESCHMIEDE_TEST_DB_PATH`), Test-Doubles (`DeterministicPseudoConsoleProcessLauncher`, `FixedOutputPseudoConsoleProcessLauncher`, `OutputByTaskPseudoConsoleProcessLauncher`, `DelayedOutputPseudoConsoleProcessLauncher` in `Softwareschmiede.Tests`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Start` | `Guid aufgabeId`, `string effectiveWorkingDirectory`, `string pluginCommand`, `ITerminalOutputSink? outputSink` | `(Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle)` | Startet den CLI-Prozess in einer Pseudo Console; `pluginCommand` nur für Logging (wird separat via `SendCommandDelayedAsync` injiziert); `NativeProcessHandle` für `GetExitCodeProcess` (`IntPtr.Zero` beim simulierten Pfad) |

## `IPseudoConsoleHandle`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/IPseudoConsoleHandle.cs` — `internal`, `IDisposable`

Entkoppelt `PseudoConsoleSession` vom konkreten `PseudoConsole`. Implementierungen: `PseudoConsole`, `NullPseudoConsoleHandle` (Singleton).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Resize` | `short cols`, `short rows` | `bool` | `ResizePseudoConsole`-Aufruf |

## `ITerminalOutputSink`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalOutputSink.cs`

Optionale Senke für rohe Terminal-Ausgabe-Chunks einer `PseudoConsoleSession` (wird in `ReadLoopAsync` **vor** dem Parser aufgerufen). Einzige Produktiv-Implementierung: `CliOutputProtokollWriter`. Test-Implementierung: `CapturingOutputSink` in `PseudoConsoleSessionTests`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `OnOutputChunk` | `ReadOnlySpan<byte> bytes` | `void` | Roh-Chunk-Meldung; Implementierung muss Bytes sofort kopieren |
| `Complete` | — | `void` | Idempotenter Abschluss + Restdaten-Flush |
| `CompleteAsync` | `TimeSpan timeout`, `CancellationToken ct` | `Task` | Abschluss + begrenztes Warten auf Persistenz |

## `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs` — erweitert `IPlugin`

Contract der Anbieter-Plugins. Implementiert durch `CliKiPluginBase` (und dessen fünf Ableitungen unter `plugins/`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartCliAsync` | `string localRepoPath`, `string? parameters`, `CancellationToken ct` | `Task<ProcessStartInfo>` | Liefert die `ProcessStartInfo` für den CLI-Start (Executable, Args, Cwd, Env) — wird sowohl vom Pipe-Pfad (`KiAusfuehrungsService.StartCliAsync`) als auch vom ConPTY-Pfad (nur `FileName`/`Arguments` als injizierter Befehl) genutzt |
| `GetProcessWindowTitle` | `Guid aufgabeId` | `string` | Optionaler Fenstertitel-Hinweis |
| `SupportsSessionContinuation` | — | `bool` | Session-Fortsetzung (z. B. `--continue`) |
| `CheckHealthAsync` | `CancellationToken ct` | `Task<bool>` | Verfügbarkeitsprüfung (typisch `--version`) |

Hinweis: Es gibt keinen Capability-Deskriptor für PTY-Unterstützung; `StartCliAsync` liefert `ProcessStartInfo` (kein plattformneutraleres Start-Spec-Objekt).

## `IAiCliProvider`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IAiCliProvider.cs` — erweitert `IPlugin`

Älterer/paralleler Contract: `Task<Process> StartCliAsync(string workingDirectory, string? sessionParameter, CancellationToken ct)` + `SupportsSessionContinuation()`. Repo-weit ohne Implementierung in `plugins/` (nur `CliKiPluginBase`-Linie aktiv) — im interaktiven Pfad nicht verwendet.

## `ICliSessionService`
Datei: `src/Softwareschmiede/Infrastructure/Services/ICliSessionService.cs` — `IAsyncDisposable`

Zeilenbasierter Alt-Sessionpfad; einzige Implementierung `CliSessionService`, **keine DI-Registrierung, keine Aufrufer**.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsRunning` | — | `bool` | Prozess läuft |
| `StartAsync` | `string cliName`, `string workingDir`, `Func<string,Task> onOutput` | `Task` | Startet Prozess mit Pipe-Redirects; `onOutput` erhält Zeilen (`ReadLineAsync`-basiert) |
| `SendAsync` | `string input` | `Task` | `StandardInput.WriteLine` |
| `StopAsync` | — | `Task` | Cancel + `Kill(entireProcessTree)` + Dispose |

## `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`

Von `KiAusfuehrungsService` implementiert (DI-Alias in `App.xaml.cs` Zeile 309).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunningCountChanged` | — | `event Action<int,int>` | Änderung der Anzahl laufender Automatisierungen (vorher/aktuell) |
| `GetRunningCount` | — | `int` | Anzahl laufender Automatisierungen |
| `IsRunning` | `Guid aufgabeId` | `bool` | Läuft eine Automatisierung für die Aufgabe |

## `ICliRawExportService`
Datei: `src/Softwareschmiede.App/Services/CliRawExportService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ExportCliRawAsync` | `Guid aufgabeId`, `string zielPfad`, `CancellationToken ct` | `Task` | Exportiert `ProtokollTyp.CliOutput`-Zeilen als `.raw`-Datei (zeilenbasiert, kein Rohbyte-Dump) |

## `ICliRunner`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`

Nicht-interaktiver Einmal-CLI-Runner (`RunAsync` → `CliResult` mit stdout/stderr/ExitCode); Implementierung `CliRunner` (`src/Softwareschmiede/Infrastructure/Services/CliRunner.cs`) — kein Session-/Terminal-Konzept, nicht am interaktiven Pfad beteiligt.

## `IProzessStarter`
Datei: `src/Softwareschmiede/Domain/Interfaces/IProzessStarter.cs`

`Starten(ProzessStartAnfrage)` — Gateway für einfache OS-Prozessstarts (Explorer/IDE-Öffnen); `SystemProzessStarter` (Produktion) / `AufzeichnenderProzessStarter` (E2E: schreibt `prozess-starts.log`). Nicht am CLI-/Terminalpfad beteiligt.

## `IPlugin` (Basis)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Basis-Contract (`PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups`) — von `IKiPlugin`/`IAiCliProvider` erweitert.

## Nicht vorhanden
- `ITerminalSession` (mit `WriteInput`, `Resize`, `OutputChunk`, `Exited`, `Failed`, `Dispose`) — existiert nicht; `PseudoConsoleSession` ist eine konkrete `sealed class` ohne Interface.
- `ITerminalSessionFactory`/`TerminalSessionService` — Session-Erzeugung ist auf `IPseudoConsoleProcessLauncher.Start` + `PseudoConsoleSession`-Konstruktor verteilt.
