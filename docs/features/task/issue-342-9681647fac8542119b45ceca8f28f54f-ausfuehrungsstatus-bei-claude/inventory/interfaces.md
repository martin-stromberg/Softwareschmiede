# Interfaces — Bestandsaufnahme

## `ITerminalSession`

Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs`

Abstraktion einer interaktiven Terminal-Session. Implementierungen: `PseudoConsoleSession` (ConPTY/Pipe), `ScmCoordinatedTerminalSession` (interner Coordinator-Wrapper), `TerminalReplaySession` (Aufzeichnungswiedergabe).

| Methode / Member | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `InputStream` | — | `Stream` | Eingaben an den Prozess |
| `OutputStream` | — | `Stream` | Prozessausgabe |
| `Process` | — | `Process` | Verwalteter Prozess |
| `Buffer` | — | `TerminalBuffer` | Gerenderter Terminal-Puffer |
| `RuntimeStatus` | — | `CliRuntimeStatus` | **Live-Laufzeitstatus (Quelle der Fußzeile)** |
| `IsPseudoTerminal` | — | `bool` | ConPTY-Backend ja/nein |
| `SupportsResize` | — | `bool` | Laufzeit-Resize erlaubt |
| `ExitCode` | — | `int?` | Exit-Code nach Prozessende |
| `Failure` | — | `TerminalSessionFailedEventArgs?` | Erster fataler Laufzeitfehler (vor `Failed`-Event abrufbar) |
| `WriteInputAsync` | `bytes`, `ct` | `Task` | Kodierte Eingabe serialisiert schreiben |
| `WritePromptAsync` | `prompt`, `ct` | `Task` | Prompt inkl. Absenden (CR) schreiben |
| `Resize` | `cols`, `rows` | `bool` | Geometrie ändern (dedupliziert) |
| `MarkInputActivity` | — | `void` | Eingabe an Status-Erkennung melden |
| `MarkOutputActivity` | — | `void` | Ausgabe an Status-Erkennung melden |
| `DrainOutputAsync` | `timeout`, `ct` | `Task<bool>` | Auf Leseschleifen-Ende warten |
| `RebuildBufferFromReplay` | — | `void` | Buffer aus Replay-Chunks neu aufbauen |
| `OutputChunk` | — | event | Pro gelesenem Roh-Chunk (vor Parsing) |
| `Exited` | — | event | Prozessende mit Exit-Code |
| `Failed` | — | event | Fataler Session-Laufzeitfehler |
| `BufferChanged` | — | event | Nach verarbeitetem Output-Chunk |
| `RuntimeStatusChanged` | — | event | **`CliRuntimeStatus`-Wechsel (Fußzeilen- + LaufStatus-Quelle)** |

## `IRunningAutomationStatusSource`

Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`

Implementiert von `KiAusfuehrungsService`. Genutzt von `MainWindowViewModel` (Refresh-Trigger), `AufgabeRecoveryService` (Still-Running-Prüfung), `CliUpdateSafetyService`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunningCountChanged` | — | event `Action<int,int>` | Anzahl laufender Automatisierungen geändert (alt/neu) |
| `GetRunningCount` | — | `int` | Anzahl laufender Prozesse |
| `IsRunning` | `aufgabeId` | `bool` | Läuft für diese Aufgabe ein Prozess (`!HasExited`; Exception → `false`) |

## `IAktiveAufgabenService`

Datei: `src/Softwareschmiede/Application/Services/IAktiveAufgabenService.cs`

Implementiert von `AufgabeService`. Genutzt von `MainWindowViewModel`, `CliUpdateSafetyService`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAktiveAufgabenAsync` | `ct` | `Task<List<Aufgabe>>` | Aktive Aufgaben (`Status ∈ {Gestartet, Wartend}`) für die Seitenleiste |

## `AufgabeLaufdatenChangedNotifier` (Klasse, kein Interface)

Datei: `src/Softwareschmiede/Application/Services/AufgabeLaufdatenChangedNotifier.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `LaufdatenChanged` | — | event `Action<Guid>` | Laufdaten einer Aufgabe wurden persistiert |
| `NotifyLaufdatenChanged` | `aufgabeId` | `void` | Benachrichtigung auslösen |
