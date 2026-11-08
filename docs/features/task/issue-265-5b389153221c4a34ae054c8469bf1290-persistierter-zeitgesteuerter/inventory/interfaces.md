# Interfaces — Bestandsaufnahme

## `ITerminalSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs`

Abstraktion einer interaktiven Terminal-Session (PTY- oder Pipe-Backend). Zustellpunkt des zeitgesteuerten Versands (`PromptZeitVersandService.SendPromptAsync`, Zeile 206). Anforderungsrelevante Member:

| Methode / Member | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `WritePromptAsync` | `string prompt`, `CancellationToken ct` | `Task` | Schreibt einen Prompt inkl. abschließendem Absenden (CR) in den Input-Stream — die eigentliche Zustellmethode des Versands. |
| `IsPseudoTerminal` | — | `bool` | Gibt an, ob die Session über ein echtes Pseudo-Terminal (ConPTY) läuft. |
| `RuntimeStatus` | — | `CliRuntimeStatus` | Laufzeitstatus der aktiven CLI innerhalb der Session. |
| `Exited` / `Failed` / `BufferChanged` / `RuntimeStatusChanged` / `OutputChunk` | — | Events | Session-Lebenszyklus-Ereignisse (u. a. `Exited` bei Prozessende). |
| `Dispose` | — | `void` | `IDisposable`; ein `ObjectDisposedException` beim Versand wird vom Service als Debug-Fall behandelt (`SendPromptAsync` Zeilen 209–215). |

Weitere Member (Kontext): `InputStream`, `OutputStream`, `Process`, `Buffer`, `SupportsResize`, `ExitCode`, `Failure`, `WriteInputAsync`, `Resize`, `MarkInputActivity`, `MarkOutputActivity`, `DrainOutputAsync`, `RebuildBufferFromReplay`.

## `ITimer` (System, `Microsoft.Bcl.TimeProvider`)
Externer Contract — Instanzen entstehen über `TimeProvider.CreateTimer` (`PromptZeitVersandService` Zeilen 78–82).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Change` | `TimeSpan dueTime`, `TimeSpan period` | `bool` | Scharfschalten/Umplanen des Timers; genutzt für das initiale Armieren (Zeile 89) und das Neuarmieren auf das Pausenende (Zeile 147). |
| `Dispose` | — | `void` | Aufräumen beim Ersetzen/Stornieren/Versenden (`RemoveEntry`-Aufrufer, Zeilen 65, 70, 96, 161, 167). |

Der Timer wird bewusst zunächst inaktiv (`Timeout.InfiniteTimeSpan`) erzeugt und erst nach dem Einfügen in `_scheduledPrompts` scharf gemacht (Reihenfolge-Garantie, Kommentar Zeilen 73–77). In Tests wird `FakeTimeProvider` (`Microsoft.Extensions.Time.Testing`) verwendet, dessen `Advance` die Timer feuert.

## `IServiceScopeFactory` (Microsoft.Extensions.DependencyInjection)
Externer Contract — bereits als optionaler Konstruktor-Parameter des `PromptZeitVersandService` vorhanden (`scopeFactory`, Zeilen 23, 32–38) und der bestehende Weg, aus dem Singleton scoped Dienste (`AufgabeService` → `SoftwareschmiededDbContext`) zu nutzen (`GetPausiertBisUtcAsync`, Zeilen 178–181). In der App-DI automatisch verfügbar; in Unit-Tests wird er weggelassen oder über einen echten `ServiceProvider` bezogen.
