# Logikklassen

## `TerminalBuffer`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

Verwaltung des 2D-Zellen-Grids, Cursor-Position, SGR-Attribute und Scrollback.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TerminalBuffer(int, int)` | `public` | Konstruktor: erstellt ein neues Grid mit angegeben Zeilen/Spalten, füllt es mit `TerminalCell.Default` |
| `Apply(TerminalEvent)` | `public` | Verarbeitet ein Event (TextWrittenEvent, CursorMovedEvent, ColorChangedEvent, ScreenClearedEvent, LineErasedEvent) — Thread-sicher |
| `Resize(int, int)` | `public` | Passt Buffer-Größe an, erhält sichtbaren Inhalt, clamped Cursor-Position — Thread-sicher |
| `GetRow(int)` | `public` | Gibt eine Kopie der Zellen einer Zeile zurück (auch wenn Index ungültig) |
| `GetSnapshot()` | `public` | Erstellt einen konsistenten Snapshot (Grid-Kopie + Dimensionen + Cursor-Position) unter einem einzigen Lock |
| `ApplyText(string)` | `private` | Schreibt Text zeichenweise in Grid; behandelt `\r` (Carriage Return → Spalte 0), `\n` (Linefeed → nächste Zeile), `\x08` (Backspace) |
| `AdvanceLine()` | `private` | Bewegt Cursor zur nächsten Zeile, scrollt bei Erreichen des Zeilenende |
| `ScrollUp()` | `private` | Scrollt Buffer um eine Zeile nach oben, speichert oberste Zeile im Scrollback-Queue |
| `ApplyColor(ColorChangedEvent)` | `private` | Setzt SGR-Attribute (_currentForeground, _currentBackground, _currentBold, _currentDim, _currentUnderline) |
| `ApplyClearScreen(int)` | `private` | Löscht Bildschirm je nach Mode: 0=Cursor bis Ende, 1=Anfang bis Cursor, 2=Gesamtes Grid |
| `ApplyEraseLine(int)` | `private` | Löscht aktuelle Zeile je nach Mode: 0=Cursor bis Zeilenende, 1=Zeilenanfang bis Cursor, 2=Ganze Zeile |
| `FillGrid(TerminalCell[,], int, int)` | `private static` | Füllt ein Grid-Array vollständig mit `TerminalCell.Default` |
| `Clamp(int, int, int)` | `private static` | Begrenzt einen Wert auf [min, max] |

**Abonnierte Events:**
- Keine; `Apply()` wird von `PseudoConsoleSession.ReadLoopAsync()` aufgerufen

**Publizierte Events:**
- Keine; Events werden durch Änderungen am Buffer impliziert (via `PseudoConsoleSession.BufferChanged` Event)

**Beobachtungen:**
- `ApplyText()` behandelt CR/LF als separate Zeichen, kombiniert sie nicht zu CRLF-Sequenzen
- `ApplyClearScreen(mode=2)` füllt nur die sichtbaren Zeilen (0 bis _rows-1); ob hier Scrollback-Zeilen betroffen sind, ist unklar
- Kein spezieller Mechanismus `ClearAllCells()` wie in der Anforderung erwähnt

---

## `AnsiSequenceParser`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs`

Zustandsbehafteter VT100/ANSI-Parser. Konvertiert Byte-Blöcke in Terminal-Events.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Parse(ReadOnlySpan<byte>)` | `public` | Statmaschinen-basierter Parser; verarbeitet Bytes und gibt Liste von `TerminalEvent`-Instanzen zurück |
| `FlushText(List<TerminalEvent>)` | `private` | Dekodiert gesammelte Text-Bytes als UTF8, erstellt `TextWrittenEvent` und leert den Buffer |
| `ProcessCsiCommand(char, string, List<TerminalEvent>)` | `private static` | Verarbeitet CSI-Befehle (Cursor-Bewegung, Screen-Clear, SGR, etc.) |
| `ProcessCsiQuestionCommand(char, string, List<TerminalEvent>)` | `private static` | Verarbeitet `CSI ?` Befehle (z. B. Cursor-Sichtbarkeit) |
| `ParseSgr(int[], List<TerminalEvent>)` | `private static` | Dekodiert SGR-Parameter (Farben, Bold, Underline, etc.) und erstellt `ColorChangedEvent`-Instanzen |
| `ParseExtendedColor(int[], ref int)` | `private static` | Parst 256-Farben- oder 24-Bit-RGB-Farbbefehle aus SGR-Parametern |
| `GetColor256(int)` | `private static` | Konvertiert 256-Farb-Palette-Indizes zu RGB-Werten |
| `ParseParams(string)` | `private static` | Splittet Parameter-String nach `;` und konvertiert zu int-Array |
| `GetParam(int[], int, int)` | `private static` | Liest Parameter mit Default-Wert |

**Zustandsvariablen:**
- `_state` (enum: Normal, Escape, Csi, CsiQuestion, Osc) — aktuelle Parsing-State
- `_paramBuffer` (StringBuilder) — sammelt Parameter zwischen Escape und Command
- `_textBuffer` (List<byte>) — sammelt Klartext-Bytes bis zur nächsten Escape-Sequenz
- `StandardColors` (static Color[16]) — Palette der 16 Standard-ANSI-Farben

**Abonnierte Events:**
- Keine

**Publizierte Events:**
- Indirekt: erstellt `TextWrittenEvent`, `CursorMovedEvent`, `ColorChangedEvent`, `ScreenClearedEvent`, `LineErasedEvent`, `CursorVisibilityChangedEvent`

**Beobachtungen:**
- `FlushText()` dekodiert Bytes als UTF8 und erstellt `TextWrittenEvent` mit vollständigem Text
- Keine spezielle Behandlung von CR/LF-Kombinationen; diese werden als separater Text übermittelt
- Zustandsbehaftet: Ein einzelnes `Parse()`-Call kann über mehrere Aufrufe fragmentierte Escape-Sequenzen zusammensetzen

---

## `PseudoConsoleSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

Koordiniert laufende Pseudo-Console-Sitzung (Prozess, Streams, Leseschleife).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PseudoConsoleSession(...)` | `internal` | Konstruktor (2 Überladungen: eine mit SystemTimeProvider, eine mit injiziertem TimeProvider); startet `ReadLoopAsync` sofort |
| `Resize(int, int)` | `public` | Passt Pseudo-Console-Größe an |
| `MarkOutputActivity()` | `public` | Aktualisiert `_lastOutputUtc` für Status-Erkennung |
| `MarkInputActivity()` | `public` | Aktualisiert `_lastInputUtc` für Status-Erkennung |
| `Dispose()` | `public` | Bricht Leseschleife ab (via `_readCts.Cancel()`), schließt Streams; Thread-sicher mit `Interlocked.CompareExchange` |
| `ReadLoopAsync(CancellationToken)` | `private async` | Kontinuierliche Leseschleife: liest bis zu 4096 Bytes, parsed mit `AnsiSequenceParser`, wendet Events auf `Buffer` an, löst `BufferChanged` aus |
| `RefreshRuntimeStatus()` | `private` | Wertet Prozess- und I/O-Aktivität aus, bestimmt `CliRuntimeStatus` |
| `SetRuntimeStatus(CliRuntimeStatus)` | `private` | Aktualisiert `_runtimeStatus` und feuert `RuntimeStatusChanged` Event |
| `WritePromptAsync(string, CancellationToken)` | `public async` | Schreibt Prompt in Input-Stream, normalisiert Zeilenenden zu `\r` |
| `NormalizeToCarriageReturn(string)` | `public static` | Konvertiert `\r\n` und `\n` zu `\r` |

**Properties:**
- `InputStream` (Stream) — schreibbar, für Eingabe an Prozess
- `OutputStream` (Stream) — lesbar, für Prozessausgabe
- `Process` (Process) — der verwaltete Prozess
- `RuntimeStatus` (CliRuntimeStatus) — Laeuft, Inaktiv, WartetAufEingabe
- `Buffer` (TerminalBuffer) — der Terminal-Buffer dieser Sitzung (Default: 220×50)

**Abonnierte Events:**
- `Process.Exited` — impliziert durch `RefreshRuntimeStatus()` Polling

**Publizierte Events:**
- `RuntimeStatusChanged` (EventHandler<CliRuntimeStatusChangedEventArgs>)
- `BufferChanged` (EventHandler) — gelöst nach jedem erfolgreich verarbeiteten Ausgabe-Chunk

**Beobachtungen:**
- `ReadLoopAsync()` ruft `Buffer.Apply()` für jedes Event auf
- `WritePromptAsync()` normalisiert Zeilenenden bewusst zu nur `\r` (kein CRLF), um mit der physischen Tastatur-Enter-Kodierung übereinzustimmen
- `NormalizeToCarriageReturn()` konvertiert CRLF und LF zu CR
- Leseschleife ist unabhängig vom Lebenszyklus eines anzeigenden Controls (Issue-86)
- Default-Größe: 220 Spalten × 50 Zeilen

---

## `TerminalControl`
Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

WPF-Control zum Rendern eines `TerminalBuffer` auf Canvas und Weiterleitung von Tastatureingang.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TerminalControl()` | `public` | Konstruktor; setzt Focusable=true, ruft `MeasureCellSize()` auf |
| `OnSessionChanged(PseudoConsoleSession?)` | `private` | Property Changed Handler: bindet/entbindet `BufferChanged`, setzt Buffer, ruft `Resize()`, renders sofort |
| `OnBufferChanged(object?, EventArgs)` | `private` | Event Handler für `PseudoConsoleSession.BufferChanged`: ruft `InvalidateVisual()` auf |
| `OnCreateAutomationPeer()` | `protected override` | Erstellt einen `FrameworkElementAutomationPeer` für UI-Automation (UI-Testing) |
| `OnRender(DrawingContext)` | `protected override` | Rendert den Buffer: zeichnet schwarzen Hintergrund, iteriert über alle Zellen, zeichnet Hintergrundfarben (wenn != Schwarz), zeichnet Zeichen, zeichnet Cursor |
| `GetBrush(Color)` | `private` | Cached Brushes nach Farbe |
| `CreateFrozenBrush(Color)` | `private static` | Erstellt und friert einen SolidColorBrush ein |
| `OnPreviewKeyDown(KeyEventArgs)` | `protected override` | Verarbeitet Tastatureingang; Ctrl+V für Clipboard, ansonsten `KeyToVt100Encoder` |
| `OnTextInput(TextCompositionEventArgs)` | `protected override` | Verarbeitet Text-Eingabe |
| `WriteToInputStream(byte[])` | `private` | Schreibt Bytes synchron in Input-Stream, markiert Activity |
| `ReadClipboardAndInsertAsync()` | `private async` | Liest Clipboard, kodiert mit `KeyToVt100Encoder`, schreibt asynchron |
| `WriteToInputStreamAsync(byte[], string)` | `private async` | Schreibt Bytes asynchron in Input-Stream |
| `GetClipboardText()` | `private` | Liest Text aus Windows Clipboard mit Fehlerbehandlung |
| `OnMouseDown(MouseButtonEventArgs)` | `protected override` | Setzt Focus auf Control |
| `OnRenderSizeChanged(SizeChangedInfo)` | `protected override` | Bei Größenänderung: ruft `buffer.Resize()` und `session.Resize()` auf |
| `MeasureCellSize()` | `private` | Berechnet Zellengröße (Width, Height) basierend auf Consolas 13pt |
| `CalculateCols()` | `private` | Berechnet Spaltenanzahl basierend auf ActualWidth und `_cellWidth` |
| `CalculateRows()` | `private` | Berechnet Zeilenanzahl basierend auf ActualHeight und `_cellHeight` |

**Properties:**
- `Session` (DependencyProperty) — die aktive `PseudoConsoleSession`
- `_buffer` (TerminalBuffer) — Cache des Session-Buffers
- `_currentSession` (PseudoConsoleSession) — aktuelle Session
- `_cellWidth`, `_cellHeight` (double) — berechnete Zellengröße in Pixeln

**Abonnierte Events:**
- `Session.BufferChanged` — OnBufferChanged
- `SizeChanged` (WPF-Event) → OnRenderSizeChanged

**Publizierte Events:**
- Keine

**Render-Details:**
- Font: Consolas, 13pt
- Cursor: semi-transparentes Weiß (Alpha 180)
- Hintergrund: schwarz (nur wenn != Standard)
- Zeichen: werden übersprungen, wenn sie Leerzeichen oder Null-Zeichen sind

**Beobachtungen:**
- `OnRender()` rendert nur dann, wenn Grid nicht null ist
- Bei jedem Render wird `MeasureCellSize()` aufgerufen (möglicherweise für DPI-Awareness)
- Hintergrundfarben werden nur gezeichnet, wenn != schwarz (Optimierung)
- Cursor wird über alle Zellen gezeichnet (als letzte Render-Operation)
- Keine explizite Behandlung für "gesamten sichtbaren Bereich bereinigen" bei Clear-Operationen
