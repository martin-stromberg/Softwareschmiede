← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Technischer Ablauf

## Übersicht

Das Terminal-System startet KI-CLI-Prozesse über die Windows Pseudo Console (ConPTY) API, liest den Output aus einer Pipe, parst ANSI-Escape-Sequenzen zu strukturierten Events, verwaltet den Terminal-Zustand in einem 2D-Buffer und rendert diesen per WPF-Control.

## Ablauf

### 1. Prozessstart mit ConPTY

Beteiligte Komponenten:
- `TaskDetailViewModel.StartCliAndUpdateStateAsync` — ruft `KiAusfuehrungsService.StartWithPseudoConsoleAsync` auf
- `KiAusfuehrungsService.StartWithPseudoConsoleAsync` — erzeugt Pseudo Console, startet Prozess, erstellt `PseudoConsoleSession`
- `IKiPlugin.StartCliAsync` — liefert `ProcessStartInfo` mit Executable-Pfad, Argumente, Arbeitsverzeichnis
- `PseudoConsole.Create` — erstellt HPCON-Handle und Pipes via `CreatePseudoConsole` API
- `PseudoConsoleProcessStarter.Start` — startet Win32-Prozess mit `STARTUPINFOEX` und `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`
- `CliOutputProtokollWriter` — optionale Output-Senke für automatische Aufgabenprotokollierung
- `PseudoConsoleSession` — koordiniert `PseudoConsole`, `Process`, Input-Stream, Output-Stream
- `CliProcessHandle.PseudoConsoleSession` — Referenz zur Session für späteren Zugriff

**Detailschritte:**

1. `StartWithPseudoConsoleAsync` ruft `kiPlugin.StartCliAsync(localRepoPath, parameters)` auf → `ProcessStartInfo`
2. `PseudoConsole.Create(cols, rows)` erstellt Input- und Output-Pipes via `CreatePipe`
3. `CreatePseudoConsole(inputReadHandle, outputWriteHandle, size, ...)` erstellt HPCON
4. `PseudoConsoleProcessStarter.Start(psi, pseudoConsole)` startet Prozess mit ConPTY via `CreateProcess`
5. `KiAusfuehrungsService` erzeugt einen `CliOutputProtokollWriter` für die `aufgabeId`
6. `PseudoConsoleSession` wird erzeugt mit Pipe-Streams, `PseudoConsole` und optionaler Output-Senke
7. `CliProcessHandle` wird mit `PseudoConsoleSession`-Referenz und `OutputSink` erstellt
8. Event `CliProcessStatusChanged(Gestartet)` wird gefeuert
9. Event `PseudoConsoleSessionGestartet(session)` wird an `TaskDetailViewModel` propagiert

### 1.5. Automatische CLI-Ausgabe-Protokollierung

Beteiligte Komponenten:
- `PseudoConsoleSession.ReadLoopAsync` — liest Output-Bytes aus der ConPTY-Pipe
- `ITerminalOutputSink` — optionale Schnittstelle für rohe Terminal-Output-Chunks
- `CliOutputProtokollWriter` — kopiert Chunks, segmentiert Zeilen und schreibt im Hintergrund
- `CliOutputLineAccumulator` — dekodiert UTF-8 zustandsbehaftet und trennt auf `\n`, `\r\n` und einzelne `\r`
- `ProtokollService.AddCliOutputAsync` — speichert eine Zeile als `ProtokollTyp.CliOutput`

**Detailschritte:**

1. `StartWithPseudoConsoleAsync` erstellt pro ConPTY-Start einen `CliOutputProtokollWriter`.
2. Der Writer wird über `IPseudoConsoleProcessLauncher.Start(..., outputSink)` an die `PseudoConsoleSession` übergeben.
3. `ReadLoopAsync` liest einen Byte-Chunk aus `OutputStream`.
4. Nach `MarkOutputActivity()` und vor der ANSI-Parser-Verarbeitung ruft die Session `outputSink.OnOutputChunk(...)` auf.
5. Der Writer kopiert die Daten in die eigene Verarbeitung. `CliOutputLineAccumulator` hält UTF-8-Decoderzustand über Chunk-Grenzen und liefert abgeschlossene Zeilen.
6. Abgeschlossene Zeilen werden in eine bounded Queue mit Backpressure geschrieben. Ein Hintergrund-Worker liest sequenziell und ruft für jede Zeile `ProtokollService.AddCliOutputAsync(aufgabeId, line)` in einem Async-Scope auf.
7. Beim Ende der Leseschleife ruft `PseudoConsoleSession` `outputSink.Complete()` auf; beim Prozess-Cleanup ruft `KiAusfuehrungsService` zusätzlich `CompleteAsync(...)` mit Timeout auf.
8. Persistenzfehler werden geloggt. Sie beenden weder Prozess noch Terminal-Rendering.

**Hinweis:** Der drainbare Abschluss wartet auf bereits angenommene Queue-Einträge. Ein verbleibender bekannter Race-Fall bei voller Queue und parallelem `CompleteAsync` ist als Nacharbeit dokumentiert: Zeilen aus einem bereits dekodierten, aber noch nicht vollständig gequeuten Chunk können im Abschlussrennen verloren gehen.

### 2. Zeilenvorschub-Normalisierung in der Textverarbeitung

Beteiligte Komponenten:
- `TerminalBuffer.ApplyText(string text)` — Zeichen-für-Zeichen-Verarbeitung von Text-Events
- `TerminalBuffer.NewLine()` — kombinierte Zeilenvorschub und Spalte-0-Setzung

**Detailschritte:**

1. `TextWrittenEvent` enthält rohen Text mit möglichen Sonderzeichen (`\r`, `\n`, `\x08`, druckbare Zeichen)
2. `TerminalBuffer.ApplyText()` verarbeitet jedes Zeichen:
   - `\r` (Carriage Return) → `_cursorCol = 0` (Spalte 0, kein Zeilenvorschub)
   - `\n` (Line Feed) → `NewLine()` aufrufen, was `AdvanceLine()` + `_cursorCol = 0` ausführt
   - `\r\n` (CRLF) → zwei aufeinanderfolgende Zeichen: zuerst `\r` (nur Spalte 0), dann `\n` (Vorschub + Spalte 0) = genau ein Zeilenvorschub
   - `\x08` (Backspace) → `_cursorCol--` (minimal 0)
   - Druckbare Zeichen → bei Spaltenüberlauf `NewLine()` aufrufen, dann Zeichen in Grid schreiben, `_cursorCol++`

3. **Resultat:** Unix-LF (`\n`), Windows-CRLF (`\r\n`) und Mac-CR (`\r`) werden semantisch korrekt behandelt — kein Treppeneffekt.

### 3. Screen-Clear mit vollständiger Bereinigung

Beteiligte Komponenten:
- `TerminalBuffer.ApplyClearScreen(int mode)` — Mode-spezifische Clear-Operationen
- `TerminalBuffer.ClearAllCells()` — private Hilfsmethode für vollständige Bereinigung

**Detailschritte:**

1. Parser erzeugt `ScreenClearedEvent` mit Mode (0=Cursor bis Ende, 1=Anfang bis Cursor, 2=ganzer Bildschirm)
2. `TerminalBuffer.Apply(ScreenClearedEvent)` → `ApplyClearScreen(mode)`
3. **Mode 0 und 1:** Teilweise Löschen wie bisher (zeilenweise Clearup/Cleardown von aktueller Cursor-Position)
4. **Mode 2 (Ganzer Bildschirm):** Aufrufen von `ClearAllCells()`:
   - Gesamtes Grid wird mit `TerminalCell.Default` gefüllt (alle Zellen leer/weiß auf schwarz)
   - **Wichtig:** `_scrollback`-Ringpuffer wird geleert (`_scrollback.Clear()`)
   - Cursor wird auf (0, 0) gesetzt
5. **Resultat:** Sauberer, komplett leerer Bildschirm ohne alte Zeilen im Scrollback (auch nicht sichtbar in Zukunft)

### 4. Terminal-Resize mit Erhalt aktueller Zeilen

Beteiligte Komponenten:
- `TerminalControl.OnRenderSizeChanged(SizeChangedInfo)` — misst neue Pixel-Dimensionen
- `TerminalBuffer.Resize(int cols, int rows)` — passt Grid-Größe an
- `PseudoConsoleSession.Resize(int cols, int rows)` — aktualisiert echte Terminal-Größe

**Detailschritte:**

1. Fenster wird vergrößert/verkleinert → `OnRenderSizeChanged` wird ausgelöst
2. Neue Spalten/Zeilen berechnet: `newCols = ActualWidth / _cellWidth`, `newRows = ActualHeight / _cellHeight`
3. `TerminalBuffer.Resize(newCols, newRows)`:
   - Neues, leeres Grid anlegen (gefüllt mit `TerminalCell.Default`)
   - **Falls Zeilenzahl vergrößert/gleich (`rows >= _rows`):** Kopie ab Zeile 0 (top-aligned), Rest leer
   - **Falls Zeilenzahl verkleinert (`rows < _rows`):**
     - Berechne Versatz: `offset = _rows - rows` (Anzahl herausgeschobener Zeilen)
     - Verschiebe obere `offset` Zeilen in `_scrollback` (unter Beachtung von `MaxScrollbackLines`)
     - Kopiere untere `rows` Zeilen des alten Grids in das neue Grid (bottom-aligned)
     - Spalten: immer rechts abschneiden, kein Reflow auf nächste Zeile
   - Cursor-Zeile: um `offset` reduzieren, dann auf `[0, rows-1]` klemmen
   - Cursor-Spalte: auf `[0, cols-1]` klemmen
4. `PseudoConsoleSession.Resize(newCols, newRows)` → API `ResizePseudoConsole` aktualisiert echtes Terminal
5. `TerminalControl.InvalidateVisual()` → erzwingt Neuzeichnung
6. **Resultat:** Nach Verkleinerung sieht Benutzer den aktuellen Prompt/Cursor am unteren Rand, nicht veraltete alte Zeilen oben

### 5. Terminal-Rendering-Loop mit Buffer-Snapshot (Leseschleife läuft in der Session, nicht im Control)

Seit der Behebung von Issue-86 (parallele CLI-Ausführungen) läuft die Leseschleife nicht mehr im
`TerminalControl`, sondern in `PseudoConsoleSession` selbst — ab Konstruktion der Session bis zu ihrem
`Dispose()`, unabhängig davon, ob überhaupt ein `TerminalControl` gebunden ist. Dadurch laufen mehrere
CLI-Prozesse parallel weiter und puffern ihre Ausgabe, auch wenn die zugehörige Aufgabenseite gerade nicht
angezeigt wird. `TerminalControl` ist ein reiner Renderer: Es abonniert `PseudoConsoleSession.BufferChanged`
und zeichnet bei jedem Ereignis den aktuellen Bufferinhalt neu.

**Stabilisierung durch Snapshot:** Um Race Conditions zwischen paralleler Ausgabe und Rendering zu vermeiden, erstellt `TerminalControl.OnRender()` einen konsistenten Snapshot des Buffer-Zustands über `TerminalBuffer.GetSnapshot()`, die unter einem einzigen Lock Grid, Cursor und Größe kopiert. Dies verhindert, dass Render-Operationen durch gleichzeitige Buffer-Updates gestört werden.

Beteiligte Komponenten:
- `TaskDetailView.xaml.cs` — empfängt `OnPseudoConsoleSessionGestartet(session)`
- `TerminalControl.Session` — DependencyProperty, triggert `OnSessionChanged`
- `PseudoConsoleSession.ReadLoopAsync` — liest bytes aus `OutputStream`, läuft ab Konstruktion der Session
- `AnsiSequenceParser.Parse` — zerlegt Bytes in `TerminalEvent`-Instanzen
- `TerminalBuffer.Apply` — wendet Events auf Grid an (Schreiben, Cursor-Bewegung, Farben, Erase), synchronisiert via `lock`
- `TerminalBuffer.GetSnapshot()` — erstellt konsistenten Snapshot unter Lock für sichere Render-Operationen
- `PseudoConsoleSession.BufferChanged` — Event, das nach jeder verarbeiteten Ausgabe gefeuert wird
- `TerminalControl.OnBufferChanged` / `TerminalControl.OnRender` — rendert über Snapshot-Daten per `DrawingContext`

**Detailschritte:**

1. `PseudoConsoleSession`-Konstruktor legt den `Buffer` an und startet `ReadLoopAsync()` als Hintergrund-Task (`_readLoopTask`), unabhängig vom UI-Lebenszyklus.
2. `TaskDetailView` setzt `TerminalConsole.Session = session`.
3. `TerminalControl.OnSessionChanged`:
   - Deregistriert den `BufferChanged`-Handler der zuvor gebundenen Session (falls vorhanden).
   - Übernimmt `session.Buffer` als eigene `_buffer`-Referenz und passt dessen Größe an die aktuellen Pixel-Dimensionen an.
   - Registriert `OnBufferChanged` auf `session.BufferChanged`.
   - Ruft `InvalidateVisual()` für die initiale Darstellung des bereits vorhandenen Bufferinhalts auf.
4. In `PseudoConsoleSession.ReadLoopAsync` (läuft unabhängig weiter, auch ohne gebundenes Control):
   - `await OutputStream.ReadAsync(buffer)` liest bytes
   - `_outputSink?.OnOutputChunk(...)` meldet den rohen Chunk an die Aufgabenprotokollierung
   - `foreach (var evt in _parser.Parse(bytes))` zerlegt bytes
   - `Buffer.Apply(evt)` aktualisiert Zustand
   - `BufferChanged?.Invoke(this, EventArgs.Empty)` benachrichtigt ein ggf. gebundenes `TerminalControl`
5. `TerminalControl.OnBufferChanged` ruft `Dispatcher.InvokeAsync(InvalidateVisual)` auf.
6. `TerminalControl.OnRender(DrawingContext dc)`:
   - Misst Zellenbreite/-höhe aus Schriftgröße (Consolas 13pt)
   - **Neu:** Ruft `buffer.GetSnapshot()` auf, um einen konsistenten Snapshot unter Lock zu erhalten
   - Iteriert über sichtbare Zeilen im Snapshot-Grid
   - Zeichnet Hintergrund-Rechtecke für jede Zelle
   - Zeichnet Vordergrund-Text (`FormattedText`) mit Font-Attributen
   - Rendert Cursor-Rechteck bei Snapshot-CursorRow/CursorCol

### 6. Tastatureingabe

Beteiligte Komponenten:
- `TerminalControl.PreviewKeyDown` / `TextInput` — WPF-Key-Events
- `KeyToVt100Encoder.Encode` — konvertiert Key zu VT100-Sequenz
- `PseudoConsoleSession.InputStream` — Pipe zum Schreiben

**Detailschritte:**

1. `TerminalControl` fängt `PreviewKeyDown` und `TextInput` ab
2. `KeyToVt100Encoder.Encode(keyEventArgs)` liefert `byte[]`:
   - Normale Tasten: ASCII (z. B. 'A' = 0x41)
   - Pfeiltasten: `\x1b[A` (Up), `\x1b[B` (Down), etc.
   - Funktionstasten: `\x1b[11~` (F1), `\x1b[12~` (F2), etc.
   - Ctrl+C: `\x03`, Ctrl+Z: `\x1a`
   - Enter: `\r`
3. Bytes werden asynchron in `session.InputStream` geschrieben

### 7. Clipboard-Paste-Eingabe (Ctrl+V)

Beteiligte Komponenten:
- `TerminalControl.OnPreviewKeyDown` — fängt Tastaturereignisse ab
- `KeyToVt100Encoder.EncodeClipboardText` — normalisiert und kodiert Clipboard-Text
- `System.Windows.Clipboard` — WPF-API zum Zwischenablage-Zugriff
- `PseudoConsoleSession.InputStream` — Pipe zum Schreiben

**Detailschritte:**

1. Benutzer drückt `Ctrl+V` auf fokussiertem `TerminalControl`
2. `TerminalControl.OnPreviewKeyDown` prüft: `e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0`
3. Wenn erfüllt: `e.Handled = true`, `ReadClipboardAndInsertAsync()` wird aufgerufen
4. In `ReadClipboardAndInsertAsync()`:
   - `GetClipboardText()` liest `System.Windows.Clipboard.GetText()` (mit Fehlerbehandlung: Return `string.Empty` bei Fehler)
   - Falls Text nicht leer: `KeyToVt100Encoder.EncodeClipboardText(text)` normalisiert und kodiert den Text
     - Zeilenumbrüche: `\n` → `\r`, `\r\n` → `\r`, `\r` → `\r` (Windows-CLI-Standard)
     - Ergebnis: UTF-8-kodierte Bytes
   - Bytes werden asynchron via `await Session.InputStream.WriteAsync(bytes)` geschrieben
   - `Session.MarkInputActivity()` wird aufgerufen, um Runtime-Status zu aktualisieren
   - Bei Exception: Error wird geloggt (`_logger.LogWarning`), Tastatureingabe läuft weiter

### 8. ConPTY-Resize

Beteiligte Komponenten:
- `TerminalControl.SizeChanged` — Layout-Änderungen
- `TerminalControl.CalculateCols/Rows` — konvertiert Pixel zu Gitter-Dimensionen
- `PseudoConsoleSession.ResizeAsync` — delegiert an `PseudoConsole.Resize`
- `PseudoConsole.Resize` — ruft `ResizePseudoConsole` API auf
- `TerminalBuffer.Resize` — passt Grid an, erhält Scrollback

**Detailschritte:**

1. `SizeChanged` wird ausgelöst bei Layout-Änderung
2. `newCols = availableWidth / _cellWidth`, `newRows = availableHeight / _cellHeight`
3. `await session.ResizeAsync(newCols, newRows)`
4. `PseudoConsole.Resize(cols, rows)` ruft `ResizePseudoConsole(hpcon, size)` auf
5. `TerminalBuffer.Resize(newCols, newRows)` passt Grid an (erhält sichtbare Zeilen, trunciert wenn nötig)

### 9. Prozessende

Beteiligte Komponenten:
- `Process.Exited` — Win32-Event
- `KiAusfuehrungsService` — Handler entfernt Handle aus `_handles`
- `PseudoConsoleSession.Dispose` — schließt Pipes und ConPTY
- `TaskDetailViewModel.OnCliProcessStatusChanged` — setzt `IsCliRunning = false`

**Detailschritte:**

1. Prozess endet → `process.Exited` wird ausgelöst
2. `KiAusfuehrungsService.HandleProcessExited`-Handler wird aufgerufen
3. `CliProcessStatusChanged(aufgabeId, Gestoppt|Fehler)` wird gefeuert
4. `TaskDetailViewModel.OnCliProcessStatusChanged` setzt `IsCliRunning = false`; `TaskDetailView` setzt `TerminalControl.Session = null`, wodurch der `BufferChanged`-Handler deregistriert wird
5. `CancelAndDisposeConPtyResourcesAsync` versucht einen kurzen Output-Drain der Session, disposed danach die `PseudoConsoleSession` und ruft `OutputSink.CompleteAsync(...)` mit Timeout auf
6. `ReadLoopAsync` beendet sich (durch Abbruch oder EOF auf der Output-Pipe) — läuft bis dahin unabhängig davon weiter, ob ein `TerminalControl` gebunden war

## Diagramm

```mermaid
sequenceDiagram
    participant VM as TaskDetailViewModel
    participant SVC as KiAusfuehrungsService
    participant PLUGIN as IKiPlugin
    participant VIEW as TaskDetailView
    participant CTRL as TerminalControl
    participant PARSER as AnsiSequenceParser
    participant BUF as TerminalBuffer
    participant SESSION as PseudoConsoleSession
    participant WIN32 as ConPTY API

    VM->>SVC: StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, repoPath)
    SVC->>PLUGIN: StartCliAsync(repoPath, params)
    PLUGIN-->>SVC: ProcessStartInfo
    SVC->>WIN32: CreatePseudoConsole(...)
    SVC->>WIN32: CreateProcess(psi, pseudoConsole)
    SVC->>SVC: new CliOutputProtokollWriter(aufgabeId)
    SVC->>SESSION: new PseudoConsoleSession(...)
    activate SESSION
    SESSION->>SESSION: ReadLoopAsync() (Hintergrund-Task, startet sofort)
    SVC-->>VM: PseudoConsoleSessionGestartet(session)
    VM-->>VIEW: OnPseudoConsoleSessionGestartet(session)
    VIEW->>CTRL: Session = session
    CTRL->>SESSION: BufferChanged += OnBufferChanged
    par ReadLoop (läuft unabhängig vom Control weiter)
        SESSION->>SESSION: OutputStream.ReadAsync()
        SESSION->>SVC: ITerminalOutputSink.OnOutputChunk(bytes)
        SVC->>SVC: CliOutputProtokollWriter -> ProtokollService.AddCliOutputAsync
        SESSION->>PARSER: Parse(bytes)
        PARSER-->>SESSION: TerminalEvents
        SESSION->>BUF: Apply(event)
        SESSION->>CTRL: BufferChanged
    and Rendering
        CTRL->>CTRL: OnBufferChanged() -> InvalidateVisual()
        CTRL->>CTRL: OnRender(DrawingContext)
        CTRL->>BUF: Read cells, cursor
        CTRL->>CTRL: DrawRectangle, FormattedText
    end
    deactivate SESSION
```

## Fehlerbehandlung

| Situation | Verhalten |
|-----------|-----------|
| `CreatePseudoConsole` schlägt fehl | `InvalidOperationException` propagiert; UI zeigt Fehlermeldung im Fehler-Banner |
| Unvollständige ANSI-Sequenz über Paket-Grenzen | `AnsiSequenceParser` speichert Zustand; nächstes Paket setzt Verarbeitung fort |
| `ResizePseudoConsole` schlägt fehl | Rückgabewert `false`; Buffer wird trotzdem angepasst, ConPTY-Größe stimmt nicht mit Buffer überein (seltener Fall) |
| `ReadLoopAsync` bei Prozessende | EOF wird gelesen; Schleife terminiert ordnungsgemäß; Buffer bleibt im letzten Zustand erhalten |
| Unerwartete Exception in `ReadLoopAsync` | Generisches `catch (Exception)` protokolliert den Fehler und beendet die Schleife geordnet, statt sie unbehandelt zu lassen |
| `TerminalControl` nicht gebunden, während Prozess Ausgabe produziert | `ReadLoopAsync` liest und puffert die Ausgabe trotzdem weiter im `Buffer` der Session; `BufferChanged` wird gefeuert, hat aber keinen Abonnenten — kein Datenverlust, sobald wieder ein Control bindet |
| Persistenz eines CLI-Ausgabeprotokolls schlägt fehl | `CliOutputProtokollWriter` loggt den Fehler; die Terminal-Leseschleife und das Rendering werden nicht abgebrochen |
| CLI-Ausgabe erzeugt schneller Zeilen als die DB persistiert | Die bounded Queue des Writers erzeugt Backpressure; Warnungen zeigen an, dass Persistenz hinterherläuft |
