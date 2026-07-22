# Terminal-Buffer und PseudoConsoleSession

## TerminalBuffer

`TerminalBuffer` verwaltet ein sichtbares 2D-Grid aus `TerminalCell`, Cursorposition, SGR-Attribute und einen internen Scrollback-Ringpuffer.

Relevante Stellen:

- `TerminalBuffer.cs:15-16`: `_scrollback` ist eine `Queue<TerminalCell[]>`, begrenzt durch `MaxScrollbackLines = 1000`.
- `TerminalBuffer.cs:110`: `Resize(int cols, int rows)` erhaelt sichtbare Inhalte; bei kleinerer Zeilenzahl werden abgeschnittene obere Zeilen in den Scrollback geschoben.
- `TerminalBuffer.cs:153`: `GetRow(int rowIndex)` gibt nur sichtbare Grid-Zeilen zurueck.
- `TerminalBuffer.cs:218`: `ScrollUp()` schiebt die oberste sichtbare Zeile in den Scrollback.
- `TerminalBuffer.cs:236-240`: `PushToScrollback()` verwirft aelteste Zeilen, sobald 1000 Scrollback-Zeilen erreicht sind.
- `TerminalBuffer.cs:321`: `GetSnapshot()` liefert aktuell nur Grid, Rows, Cols und Cursorposition.
- `TerminalBuffer.cs:331-334`: `ClearAllCells()` leert auch den Scrollback.

## Bestehende Grenze

Der Scrollback ist bereits vorhanden, aber nicht Teil der oeffentlichen Renderdaten. `TerminalBufferSnapshot` enthaelt keine Scrollback-Zeilen. `ScrollbackCount` ist `internal` und nur fuer Tests sichtbar. Dadurch kann die UI alte Ausgabezeilen aktuell nicht anzeigen, obwohl sie im Buffer teilweise gehalten werden.

Wichtig: Der bestehende Scrollback ist auf 1000 Zeilen begrenzt. Die fachliche Frage aus `requirement.md`, ob alle Ausgaben vollstaendig erreichbar bleiben sollen, ist dadurch technisch bereits implizit mit "maximal 1000 Scrollback-Zeilen plus sichtbares Grid" beantwortet, sofern diese Grenze nicht geaendert wird.

## PseudoConsoleSession

`PseudoConsoleSession` besitzt pro Sitzung einen eigenen `TerminalBuffer` und startet die Output-Leseschleife bereits im Konstruktor.

Relevante Stellen:

- `PseudoConsoleSession.cs:15-16`: Default-Terminalgroesse `220x50`.
- `PseudoConsoleSession.cs:61`: `Buffer` wird pro Session erzeugt.
- `PseudoConsoleSession.cs:65`: `BufferChanged` informiert gebundene Controls.
- `PseudoConsoleSession.cs:99`: Leseschleife startet per `Task.Run`.
- `PseudoConsoleSession.cs:128-129`: `Resize()` leitet neue Terminaldimensionen an die native PseudoConsole weiter.
- `PseudoConsoleSession.cs:211-244`: `ReadLoopAsync()` liest Bytes, meldet Aktivitaet, gibt Rohbytes an optionale Senke, parsed ANSI, aktualisiert den Buffer und feuert danach `BufferChanged`.

## Nicht primaer betroffen

Die Anforderung zielt nicht auf Prozessstart, ConPTY-Handle-Lebensdauer, OutputStream-Lesen oder ANSI-Parsing. Diese Pfade muessen fuer Scrollbarkeit voraussichtlich nur dann angefasst werden, wenn die maximale Scrollback-Laenge oder Persistenz der Ausgabeverlaeufe geaendert werden soll.
