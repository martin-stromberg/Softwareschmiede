## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

Singleton-Service (`services.AddSingleton<KiAusfuehrungsService>()` in `App.xaml.cs`, DI ohne Factory — reiner Konstruktor-Injection über `ILogger<KiAusfuehrungsService>`, `ILoggerFactory`, `IServiceScopeFactory`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning(Guid aufgabeId)` | public | Prüft `!handle.Process.HasExited` |
| `GetRunningProcess(Guid aufgabeId)` | public | Liefert laufenden `Process` oder null |
| `GetRunningCount()` | public | Zählt laufende Handles |
| `StartCliAsync(...)` | public async | Klassischer Start via `Process.Start()` (kein ConPTY) |
| `StartWithPseudoConsoleAsync(Guid, IKiPlugin, string, string?, CancellationToken, RepositoryStartKonfiguration?, IGitPlugin?)` | public async | Ermittelt Arbeitsverzeichnis, holt `ProcessStartInfo` vom `IKiPlugin`, ruft `StartPseudoConsoleProcess` auf, registriert Handle, sendet Plugin-Befehl verzögert |
| `StartPseudoConsoleProcess(Guid, string, string)` | private | Erzeugt `PseudoConsole.Create(220, 50)`, ruft `PseudoConsoleProcessStarter.Start(psi, pseudoConsole)`, holt `Process.GetProcessById(startResult.Pid)`, erstellt `PseudoConsoleSession` über `CreatePseudoConsoleSession` |
| `GetPseudoConsoleSession(Guid aufgabeId)` | public | Liefert `handle.PseudoConsoleSession` oder null |
| `StopCliAsync(Guid, CancellationToken)` | public async | `AbsichtlichGestoppt=true`, `CloseMainWindow()`, 5s warten, sonst `Kill(entireProcessTree: true)` |
| `GetLastExitCode(Guid)` | public | Ruft `TryGetExitCode` |
| `UpdateHeartbeat(Guid)` | public | Setzt `handle.LastHeartbeat` |
| `Dispose()` | public | Killt alle laufenden Handles, disposed ConPTY-Ressourcen |
| `HandleProcessExited(Guid, Process, CliProcessHandle, string, Action?)` | private | Gemeinsame Exit-Behandlung für Standard- und ConPTY-Start; ermittelt `CliProcessStatus` (`Gestoppt`/`Fehler`/`Gestartet`) |
| `CreatePseudoConsoleSession(Guid, PseudoConsole, Process)` | private | Erstellt `FileStream`-Wrapper um `pseudoConsole.InputWritePipe`/`OutputReadPipe`, konstruiert `PseudoConsoleSession` |
| `TryGetExitCode(Process, IntPtr)` | private | Nutzt `GetExitCodeProcess` auf nativem Handle bei ConPTY-Start, sonst `Process.ExitCode` |
| `SendCommandDelayedAsync(PseudoConsoleSession, string, Guid, CancellationToken)` | private async | 300ms Delay, schreibt Kommando in `session.InputStream` |
| `CancelAndDisposeConPtyResources(CliProcessHandle)` | private static | Storniert `SendCts`, disposed `PseudoConsoleSession`, schließt `NativeProcessHandle` |
| `BuildCliCommand(ProcessStartInfo)` | private static | Baut `"FileName Arguments"`-String für den verzögerten Sendevorgang |

Abonnierte Events: `Process.Exited` (pro gestartetem Handle)
Publizierte Events: `CliProcessStatusChanged(Guid, CliProcessStatus)`, `RunningCountChanged(int, int)` (aus `IRunningAutomationStatusSource`)

`CliProcessHandle` (public, gleiche Datei): kapselt `AufgabeId`, `Process`, `LastHeartbeat`, `AbsichtlichGestoppt`, `PseudoConsoleSession?`, `NativeProcessHandle` (nur bei ConPTY-Start ≠ `IntPtr.Zero`), `SendCts?`.

## `PseudoConsoleProcessStarter`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs`

`internal static class`. Einzige Methode `Start(ProcessStartInfo psi, PseudoConsole pc) : ProcessStartResult` — baut Kommandozeile, Environment-Block, `STARTUPINFOEX` mit `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`, ruft `CreateProcess` mit `EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT`. `ProcessStartResult` kapselt `ProcessHandle`/`Pid`.

## `PseudoConsole`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs`

`internal sealed class`, `IDisposable`. `Create(short cols, short rows)` erzeugt Pipes + `CreatePseudoConsole`. `Resize(short, short)`. `Handle`/`InputWritePipe`/`OutputReadPipe` als `internal` Properties. `Dispose()` ruft `ClosePseudoConsole` + schließt beide Pipe-Handles (Interlocked-geschützt gegen Doppel-Dispose).

## `PseudoConsoleSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

`public sealed class`, `IDisposable`. Konstruktor (`internal`) nimmt zwingend eine konkrete `PseudoConsole pseudoConsole`, einen `Process process`, `Stream inputStream`, `Stream outputStream`, optional `TimeProvider`/`waitingThreshold`/`ILogger`. Startet sofort `ReadLoopAsync` als Hintergrund-Task. Öffentliche API: `InputStream`, `OutputStream`, `Process`, `RuntimeStatus` (`CliRuntimeStatus`: `Inaktiv`/`Laeuft`/`WartetAufEingabe`), `RuntimeStatusChanged`-Event, `Buffer` (`TerminalBuffer`, wird von der Leseschleife befüllt), `BufferChanged`-Event, `MarkOutputActivity()`, `MarkInputActivity()`, `Resize(int, int)` (ruft `_pseudoConsole.Resize`), `Dispose()` (disposed `_pseudoConsole`, `_process`, Streams; Interlocked-geschützt).

`CliRuntimeStatusEvaluator.Determine(...)`: reine Funktion, bestimmt Status aus `isRunning`/Zeitstempeln — keine Abhängigkeit von echtem ConPTY.

## `PluginManager`
Datei: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`

Enthält das **bereits etablierte Muster** für Test-Modus-Verzweigung, das als Vorlage für den neuen Launcher-Seam dient:

- `IsTestMode()` (`private static`): `!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SOFTWARESCHMIEDE_TEST_DB_PATH"))`.
- `_applyTestModeFilter` (Konstruktor-Feld): nur `true`, wenn kein explizites `pluginDirectory` übergeben wurde (Produktivbetrieb); bei explizitem Verzeichnis (Unit-Tests) deaktiviert.
- `DiscoverPlugins()`: `var testMode = _applyTestModeFilter && IsTestMode();` — filtert im Testmodus auf eine Allowlist (`IsAllowedInTestMode`: `LocalDirectory`, `KiSimulator`, `ClaudeCli`, `Codex`, `GitHubCopilot`).

Dieselbe Umgebungsvariable (`SOFTWARESCHMIEDE_TEST_DB_PATH`) wird auch in `App.xaml.cs:146` zur DB-Pfad-Wahl verwendet und von `WpfTestBase.LaunchApp` (`Environment.SetEnvironmentVariable("SOFTWARESCHMIEDE_TEST_DB_PATH", _testDbPath)`) für jeden E2E-Testlauf prozessweit gesetzt.

## `KiSimulatorPlugin`
Datei: `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`

`sealed class : CliKiPluginBase`, `PluginPrefix = "Softwareschmiede.KiSimulator"`. `BuildProcessStartInfo(string localRepoPath, string? parameters)` liefert:
```
FileName = "cmd.exe"
Arguments = "/c echo KI-Simulator läuft... && ping -n 31 127.0.0.1 > nul"
UseShellExecute = false
CreateNoWindow = false
```
(~31 Sekunden Laufzeit, ein Kommentar erklärt bewusst `ping` statt `timeout.exe`, da Letzteres ohne Konsolen-Handle mit ExitCode 125 abbricht — bereits eine frühere Anpassung an ConPTY-Eigenheiten). Liefert nur die Kommandozeile, wird unverändert sowohl vom echten als auch vom künftigen simulierten Start-Pfad konsumiert (`KiAusfuehrungsService.StartWithPseudoConsoleAsync` ruft `kiPlugin.StartCliAsync(...)` unabhängig vom gewählten Launcher).
