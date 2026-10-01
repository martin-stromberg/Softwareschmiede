# Interfaces — Replay-Geometrie

## `ITerminalSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs` (74 Zeilen)

Implementierungen: `PseudoConsoleSession` (Live, `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`), `TerminalReplaySession` (Replay, `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`). Keine weiteren Produktiv-Implementierungen; im Testprojekt existiert kein generisches `ITerminalSession`-Testdouble (die `TerminalControlTests` arbeiten mit echten `PseudoConsoleSession`-Instanzen via `TestPseudoConsoleSessionFactory`).

| Methode / Member | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `InputStream` | — | `Stream` | Schreibbarer Eingabe-Stream (Replay: `Stream.Null`) |
| `OutputStream` | — | `Stream` | Lesbarer Ausgabe-Stream (Replay: `Stream.Null`) |
| `Process` | — | `Process` | Verwalteter Prozess (Replay: nicht gestarteter Stub) |
| `Buffer` | — | `TerminalBuffer` | Gerenderter Zustand der Session — **trägt die aktuelle Geometrie** (`Cols`/`Rows`-Getter) |
| `RuntimeStatus` | — | `CliRuntimeStatus` | Laufzeitstatus der CLI |
| `IsPseudoTerminal` | — | `bool` | `true` bei echtem ConPTY; Replay liefert `false` — existierendes Unterscheidungsmerkmal, sagt aber nichts über Resize-Fähigkeit aus |
| `ExitCode` | — | `int?` | Exit-Code oder `null` |
| `Failure` | — | `TerminalSessionFailedEventArgs?` | Erster fataler Fehler oder `null` |
| `WriteInputAsync` | `ReadOnlyMemory<byte>`, `CancellationToken` | `Task` | Serialisierte Eingabe |
| `WritePromptAsync` | `string`, `CancellationToken` | `Task` | Prompt + CR schreiben |
| **`Resize`** | `int cols`, `int rows` | **`bool`** | „Ändert die Größe des Terminals/der Pseudo Console. Identische Aufrufe werden dedupliziert." — Rückgabesemantik ist **Erfolg**, nicht „wird unterstützt": `PseudoConsoleSession` liefert `false` für ungültige Werte/fehlgeschlagene PTY-Calls (Z. 174–175, 182), `TerminalReplaySession` liefert immer `true` ohne Wirkung (Z. 314). **Kein geeignetes Signal für fixe Geometrie** — die Anforderung nennt deshalb Capability-Member/separates Interface als Optionen |
| `MarkInputActivity` / `MarkOutputActivity` | — | `void` | Aktivitäts-Markierung für Status-Erkennung |
| `DrainOutputAsync` | `TimeSpan`, `CancellationToken` | `Task<bool>` | Warten auf Ende der Leseschleife |
| `RebuildBufferFromReplay` | — | `void` | Synchroner Buffer-Neuaufbau aus gespeicherten Chunks (Neuanbindung eines Controls) |
| `OutputChunk` | — | `EventHandler<TerminalOutputChunkEventArgs>?` | Pro gelesenem Roh-Chunk |
| `Exited` | — | `EventHandler<TerminalSessionExitedEventArgs>?` | Prozessende/Ende der Aufzeichnung |
| `Failed` | — | `EventHandler<TerminalSessionFailedEventArgs>?` | Fataler Laufzeitfehler (Replay: nie) |
| `BufferChanged` | — | `EventHandler?` | Nach jedem verarbeiteten Chunk |
| `RuntimeStatusChanged` | — | `EventHandler<CliRuntimeStatusChangedEventArgs>?` | Statuswechsel |

**Vertragsbefund:** Der Vertrag hat heute keinen Weg, „fixe Geometrie" auszudrücken. `TerminalControl` ruft `Resize` nur in `OnRenderSizeChanged` auf (Z. 311); das `Buffer.Resize` des Controls (Z. 126, 310) läuft ohnehin an der Session vorbei — selbst ein `Resize => false` der Replay-Session würde den Buffer-Eingriff des Controls nicht verhindern.

## `IScrollInfo` (WPF-Vertrag, implementiert von `TerminalControl`)
Namespace: `System.Windows.Controls.Primitives`

| Member | Umsetzungsstand in `TerminalControl` |
|--------|--------------------------------------|
| `CanVerticallyScroll` / `CanHorizontallyScroll` | Auto-Properties (Z. 55/58); vom `ScrollViewer` gesetzt wenn `CanContentScroll="True"` |
| `ExtentWidth` / `ExtentHeight` | Width: **Stub** `=> ViewportWidth` (Z. 61); Height: `_extentHeight` in Zeilen (Z. 64) |
| `ViewportWidth` / `ViewportHeight` | Width: `ActualWidth` in Pixeln (Z. 67); Height: `_viewportHeight` in Zeilen (Z. 70) |
| `HorizontalOffset` / `VerticalOffset` | Horizontal: **Stub** `=> 0` (Z. 73); Vertikal: `_verticalOffset` (Z. 76) |
| `ScrollOwner` | Auto-Property, vom ScrollViewer gesetzt (Z. 79) |
| `Line*`/`Page*`/`MouseWheel*` (8 Methoden) | Vertikal implementiert (Z. 367–402); **horizontal alle leer** (Z. 373–412) |
| `SetHorizontalOffset` / `SetVerticalOffset` | Horizontal: **leer** (Z. 415–417); Vertikal: echt mit Clamp + Invalidate (Z. 420–437) |
| `MakeVisible` | `=> rectangle` (Z. 440) |

Der `ScrollViewer` fragt `CanHorizontallyScroll` ab: `true` + `ExtentWidth > ViewportWidth` ergibt eine aktive horizontale Scrollbar — bei `HorizontalScrollBarVisibility="Disabled"` (heute in beiden Views) wird sie vom Host unterdrückt; die Property ändert ohne XAML-Änderung am Dialog nichts Sichtbares.

## `ITerminalOutputSink`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalOutputSink.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `OnOutputChunk` | `ReadOnlySpan<byte>` | `void` | Rohen Ausgabe-Chunk melden (Bytes sofort kopieren) |
| `Complete` | — | `void` | Senke idempotent abschließen |
| `CompleteAsync` | `TimeSpan`, `CancellationToken` | `Task` | Abschluss + begrenztes Warten auf Persistenz |

Implementiert von `CliOutputRecorder` (Erzeuger der `Cols`/`Rows`-Header-Werte) und `CompositeTerminalOutputSink`; nicht Teil der Wiedergabe-Geometrie.

## `IDialogService` (ausschnittsweise)
Datei: `src/Softwareschmiede.App/Services/IDialogService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ShowOpenFileDialogAsync` | `title`, `filter`, `initialDirectory?`, `ct` | `Task<string?>` | Nativer Öffnen-Dialog (`.clireplay`-Auswahl, Z. 64–68) |
| `ShowKonsolenTestDialogAsync` | `KonsolenTestViewModel`, `ct` | `Task` | Nicht-modales Konsolentestfenster (Z. 72–74) |

## `ITerminalSessionFactory` (Kontext, unverändert)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSessionFactory.cs` — `StartAsync(aufgabeId, spec, outputSink, healthCheck, ct)` → `TerminalSessionStartResult`. Erzeugt nur Live-Sessions; für die Replay-Geometrie nicht relevant. Test-Double: `TestTerminalSessionFactory` (src/Softwareschmiede.Tests/Helpers/).
