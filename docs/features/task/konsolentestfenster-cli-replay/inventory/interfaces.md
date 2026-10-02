# Interfaces — Konsolentestfenster für CLI-Ausgabe-Replay

## `ITerminalSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs` — Vertrag, den eine Replay-Implementierung erfüllen muss.

| Member | Signatur | Zweck |
|--------|----------|-------|
| `InputStream` | `Stream` (Z. 12) | Schreibbarer Input-Stream; `TerminalControl` prüft nur auf null |
| `OutputStream` | `Stream` (Z. 15) | Lesbarer Output-Stream |
| `Process` | `System.Diagnostics.Process` (Z. 18) | **Nicht-nullbar** — ein Replay-Stub muss hier ein gültiges Objekt liefern oder die Schnittstelle muss angepasst werden; `TaskDetailView.TryGetProcessId` greift auf `Process.Id` zu (fängt `InvalidOperationException` ab) |
| `Buffer` | `TerminalBuffer` (Z. 21) | Gerenderter Buffer, wird von `TerminalControl` direkt übernommen |
| `RuntimeStatus` | `CliRuntimeStatus` (Z. 24) | Laufzeitstatus |
| `IsPseudoTerminal` | `bool` (Z. 27) | PTY-Flag |
| `ExitCode` | `int?` (Z. 30) | Exit-Code nach Beendigung |
| `Failure` | `TerminalSessionFailedEventArgs?` (Z. 36) | Erster fataler Fehler (vor `Failed` gesetzt) |
| `WriteInputAsync(ReadOnlyMemory<byte>, CancellationToken)` | `Task` (Z. 39) | Serialisiertes Eingabeschreiben |
| `WritePromptAsync(string, CancellationToken)` | `Task` (Z. 42) | Prompt + CR senden |
| `Resize(int cols, int rows)` | `bool` (Z. 45) | Wird von `TerminalControl.OnRenderSizeChanged` aufgerufen — Stub muss Aufrufe tolerieren |
| `MarkInputActivity()` / `MarkOutputActivity()` | `void` (Z. 48/51) | Status-Erkennung |
| `DrainOutputAsync(TimeSpan, CancellationToken)` | `Task<bool>` (Z. 54) | Begrenztes Warten auf Leseschleifen-Ende |
| `RebuildBufferFromReplay()` | `void` (Z. 58) | **Wird von `TerminalControl.OnSessionChanged` zwingend aufgerufen** — muss den Buffer aus der geladenen Aufzeichnung deterministisch neu aufbauen |
| `OutputChunk` | `event EventHandler<TerminalOutputChunkEventArgs>?` (Z. 61) | Pro Roh-Chunk vor dem Parsen |
| `Exited` | `event EventHandler<TerminalSessionExitedEventArgs>?` (Z. 64) | Prozessende |
| `Failed` | `event EventHandler<TerminalSessionFailedEventArgs>?` (Z. 67) | Fataler Laufzeitfehler |
| `BufferChanged` | `event EventHandler?` (Z. 70) | Nach jedem verarbeiteten Chunk — treibt das Neuzeichnen |
| `RuntimeStatusChanged` | `event EventHandler<CliRuntimeStatusChangedEventArgs>?` (Z. 73) | Statuswechsel |

Einzige Implementierung heute: `PseudoConsoleSession` (`sealed`). Test-Doubles erzeugen echte `PseudoConsoleSession`-Instanzen über `TestPseudoConsoleSessionFactory`/`TestTerminalSessionFactory`.

## `ITerminalOutputSink`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalOutputSink.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `OnOutputChunk` | `ReadOnlySpan<byte> bytes` | `void` | Roh-Chunk melden — **Implementierungen müssen Bytes sofort kopieren**; idealer Mitschnitt-Hook (vor der Zeilennormalisierung) |
| `Complete` | — | `void` | Idempotenter Abschluss/Flush (am Ende von `ReadLoopAsync`) |
| `CompleteAsync` | `TimeSpan timeout, CancellationToken ct` | `Task` | Abschluss + begrenzter Drain der Persistenz |

Einzige produktive Implementierung: `CliOutputProtokollWriter`. Es gibt **keine Composite-Implementierung** — `ITerminalSessionFactory.StartAsync`/`IPseudoConsoleProcessLauncher.Start` nehmen jeweils genau eine Senke entgegen. Die Senke wird in `KiAusfuehrungsService.StartTerminalSessionAsync` (Z. 211–214) erzeugt, über `PseudoConsoleSessionContext.OutputSink` an die Session gereicht und beim Aufräumen über `CliProcessHandle.OutputSink` abgeschlossen.

## `ITerminalSessionFactory`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSessionFactory.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartAsync` | `Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink, Func<CancellationToken,Task<bool>>? healthCheck, CancellationToken ct` | `Task<TerminalSessionStartResult>` | Zentrale Session-Erzeugung (Auflösung, Preflight, Backend-Wahl, Start) — genau **eine** optionale Senke |

Implementierung: `TerminalSessionService`; Test-Double: `TestTerminalSessionFactory` (`src/Softwareschmiede.Tests/Helpers/TestTerminalSessionFactory.cs`).

## `IPseudoConsoleProcessLauncher`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/IPseudoConsoleProcessLauncher.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsPseudoTerminal` | — | `bool` | Backend-Art |
| `Start` | `Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink = null` | `TerminalSessionStartResult` | Backend-spezifischer Start (ConPTY oder Pipe) |

Implementierungen: `Win32PseudoConsoleProcessLauncher`, `SimulatedPseudoConsoleProcessLauncher`; Test-Double `DeterministicPseudoConsoleProcessLauncher` (privat in `TestTerminalSessionFactory`).

## `IPseudoConsoleHandle`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/IPseudoConsoleHandle.cs` — PTY-Handle-Abstraktion (`Resize(short,short)`, `Dispose`); No-Op-Implementierung `NullPseudoConsoleHandle` (für Pipe-Backend und Tests).

## `IDialogService`
Datei: `src/Softwareschmiede.App/Services/IDialogService.cs` — Implementierung `WpfDialogService` (Singleton, `App.xaml.cs` Z. 323)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `BestaetigenDialog` | `string nachricht, string titel` | `bool` | Ja/Nein-MessageBox |
| `RepositoryZuweisenDialog` | `RepositoryAssignViewModel` | `bool` | Modaler Dialog |
| `ArbeitsverzeichnisBearbeitenDialog` | `ArbeitsverzeichnisBearbeitenViewModel` | `bool` | Modaler Dialog |
| `ShowPluginSelectionDialogAsync` | `IEnumerable<string>, string?, CancellationToken` | `Task<PluginSelectionResult>` | Plugin-Auswahl |
| `ShowIssueSelectionDialogAsync` | `IssueSelectionDialogViewModel, CancellationToken` | `Task<Issue?>` | Issue-Auswahl |
| `ShowIssueCreateDialogAsync` | `IssueCreateDialogViewModel, CancellationToken` | `Task<IssueCreateDialogResult?>` | Issue-Anlage |
| `ShowOpenTodosDialogAsync` | `OpenTodosDialogViewModel, CancellationToken` | `Task` | Read-only To-do-Anzeige |
| `ShowSolutionSelectionDialogAsync` | `IReadOnlyList<string>, CancellationToken` | `Task<string?>` | Listen-Auswahl |
| `ShowAufgabePausierenDialogAsync` | `AufgabePausierenDialogViewModel, CancellationToken` | `Task<AufgabePausierenErgebnis?>` | Pause-Dialog |
| `ShowAutonomAufgabeInitialisierungsDialogAsync` | `AutonomAufgabeInitialisierungsDialogViewModel, CancellationToken` | `Task<AutonomAufgabeKonfiguration?>` | Initialisierungsdialog |
| `ShowSaveFileDialogAsync` | `string title, string filter, string defaultFileName, string? initialDirectory, CancellationToken` | `Task<string?>` | `SaveFileDialog` (einzige Dateidialog-Methode — **kein OpenFileDialog**) |

Für das Konsolentestfenster fehlt: eine Datei-Öffnen-Methode und eine Methode/Konvention zum Öffnen eines (ggf. nicht-modalen) Fensters.

## `ICliRawExportService`
Datei: `src/Softwareschmiede.App/Services/CliRawExportService.cs` (Z. 10–14)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ExportCliRawAsync` | `Guid aufgabeId, string zielPfad, CancellationToken ct` | `Task` | Schreibt zeilennormalisierte `CliOutput`-Protokolleinträge als `.raw`-Datei (UTF-8 ohne BOM) |

## `IKiPlugin` (Terminal-relevante Member)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

| Methode | Zweck |
|---------|-------|
| `GetTerminalStartSpecAsync(string localRepoPath, string? parameters, CancellationToken)` (Z. 36) | Liefert `TerminalSessionStartSpec` für den Terminal-Pfad (Basisimplementierung `CliKiPluginBase`, Z. 39) |
| `CheckHealthAsync` | Health-Probe, wird als Delegate an `StartAsync` weitergereicht |
