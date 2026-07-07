# Logik-Klassen: Bestandsaufnahme

## `PseudoConsoleSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

**Zweck:** Koordiniert eine laufende Pseudo-Console-Sitzung bestehend aus `PseudoConsole`, `Process`, Eingabe- und Ausgabe-Stream. Führt die Leseschleife (`ReadLoopAsync`) ab Konstruktion bis `Dispose()` unabhängig vom Lebenszyklus eines anzeigenden Controls aus.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TerminalBuffer Buffer { get; }` | Public | Der Terminal-Buffer dieser Sitzung; wird bereits bei Konstruktion angelegt und von der Leseschleife befüllt |
| `Stream InputStream { get; }` | Public | Schreibbarer Stream für Tastatureingaben an den Prozess |
| `Stream OutputStream { get; }` | Public | Lesbarer Stream für die Prozessausgabe |
| `Process Process { get; }` | Public | Der verwaltete Prozess der Sitzung |
| `CliRuntimeStatus RuntimeStatus { get; }` | Public | Laufzeitstatus der aktiven CLI (Inaktiv, Laeuft, WartetAufEingabe) mit Lock-Synchronisierung |
| `MarkOutputActivity()` | Public | Meldet gelesene Ausgabe an die Status-Erkennung und setzt Status auf `Laeuft` |
| `MarkInputActivity()` | Public | Meldet Benutzereingabe an die Status-Erkennung und setzt Status auf `Laeuft` |
| `Resize(int cols, int rows)` | Public | Ändert die Größe der Pseudo Console; gibt `true` bei Erfolg zurück |
| `Dispose()` | Public | Beendet die Leseschleife und gibt Resources frei (atomar mit `Interlocked.CompareExchange`) |
| `ReadLoopAsync(CancellationToken ct)` | Private | Kontinuierliche Leseschleife: liest Bytes aus `OutputStream`, parsed mit `AnsiSequenceParser`, wendet Events auf `Buffer` an, löst `BufferChanged` aus |
| `RefreshRuntimeStatus()` | Private | Timer-Callback: wertet I/O-Aktivität aus und aktualisiert `RuntimeStatus` |
| `SetRuntimeStatus(CliRuntimeStatus status)` | Private | Aktualisiert Status und feuert `RuntimeStatusChanged`-Event, wenn Status sich ändert |

**Publizierte Events:**
- `BufferChanged: EventHandler?` – Nach jeder erfolgreichen Verarbeitung eines Ausgabe-Chunks durch die Leseschleife
- `RuntimeStatusChanged: EventHandler<CliRuntimeStatusChangedEventArgs>?` – Wenn sich der CLI-Laufzeitstatus ändert

**Synchronisierung:** 
- Leseschleife läuft asynchron in `_readLoopTask` unabhängig
- `Buffer.Apply()` wird für jeden geparsten Event aufgerufen (Lock in `TerminalBuffer`)
- `BufferChanged` wird nach Schleife gefeuert (außerhalb von `TerminalBuffer`-Locks)
- Runtime-Status hat eigenen Lock `_runtimeStatusLock`

**Besonderheiten:**
- Leseschleife läuft unabhängig vom `TerminalControl`-Lebenszyklus (ermöglicht parallele CLI-Ausführung)
- `Dispose()` nutzt `Interlocked.CompareExchange` für atomare Thread-Sicherheit bei gleichzeitigen Aufrufen

---

## `AnsiSequenceParser`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs`

**Zweck:** Zustandsbehafteter VT100/ANSI-Parser, der Byte-Blöcke verarbeitet und `TerminalEvent`-Instanzen erzeugt.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Parse(ReadOnlySpan<byte> data)` | Public | Verarbeitet einen Byte-Block und gibt erzeugte Terminal-Ereignisse zurück; ist zustandsbehaftet (kann Split-Sequenzen über mehrere Calls zusammensetzen) |
| `FlushText(List<TerminalEvent> events)` | Private | Gibt gepufferten Text als `TextWrittenEvent` aus |
| `ProcessCsiCommand(char command, string paramStr, List<TerminalEvent> events)` | Private Static | Verarbeitet CSI-Kommandos (Cursor, Screen Clear, SGR) |
| `ProcessCsiQuestionCommand(char command, string paramStr, List<TerminalEvent> events)` | Private Static | Verarbeitet CSI-?-Kommandos (Cursor-Sichtbarkeit) |
| `ParseSgr(int[] parts, List<TerminalEvent> events)` | Private Static | Parst SGR (Select Graphic Rendition) Parameter und erzeugt `ColorChangedEvent` |
| `ParseExtendedColor(int[] parts, ref int i)` | Private Static | Parst 8-Bit- oder 24-Bit-Farb-Codes |
| `GetColor256(int index)` | Private Static | Konvertiert 256er-Farbindex zu RGB-`Color` |
| `ParseParams(string paramStr)` | Private Static | Split-Hilfsmethode für CSI-Parameter |
| `GetParam(int[] parts, int index, int defaultValue)` | Private Static | Array-Zugriff mit Default-Wert |

**Interne Zustandsvariablen:**
- `_state: State` – Aktuelle Parser-Zustand (Normal, Escape, Csi, CsiQuestion, Osc)
- `_paramBuffer: StringBuilder` – Puffer für Kommando-Parameter
- `_textBuffer: List<byte>` – Puffer für Klartext

**Besonderheiten:**
- Zustandsbehaftet: Kann VT100-Sequenzen über mehrere `Parse()`-Aufrufe verarbeiten
- Unterstützt Standard-Farben, 256er-Palette und 24-Bit RGB
- Nutzt UTF-8-Decodierung für Klartext

---

## `KeyToVt100Encoder`
Datei: `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs`

**Zweck:** Statische Klasse, die WPF-Tastaturereignisse in VT100-Byte-Sequenzen konvertiert.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Encode(KeyEventArgs e)` | Internal Static | Kodiert WPF-`KeyEventArgs` als VT100-Byte-Sequenz oder `null` wenn über `TextInput` zu senden |
| `EncodeText(string text)` | Internal Static | Kodiert einen Text als UTF-8-Byte-Array |

**Unterstützte Tasten in `Encode()`:**
- Ctrl+A-Z → Kontroll-Bytes (0x01-0x1A)
- Enter, Back, Tab, Escape, Delete
- Cursor-Tasten (Up, Down, Left, Right)
- Home, End, PageUp, PageDown
- F1-F12 → Entsprechende VT100-Sequenzen

**Fehlende Methode (Implementierungsbedarf):**
- `EncodeClipboardText(string text): byte[]` – Für Clipboard-Paste-Text (Newline-Behandlung, UTF-8-Encoding)

---

## `TerminalControl`
Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

**Zweck:** WPF `FrameworkElement`, das eine `PseudoConsoleSession` rendert und Tastatureingaben weiterleitet.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Session { get; set; }` | Public | Dependency Property für die aktive `PseudoConsoleSession` |
| `OnSessionChanged(PseudoConsoleSession? session)` | Private | Handler für Session-Wechsel: registriert/deregistriert `BufferChanged`-Handler, resized Buffer |
| `OnBufferChanged(object? sender, EventArgs e)` | Private | Handler für `BufferChanged`-Event: ruft `Dispatcher.InvokeAsync(InvalidateVisual)` auf |
| `OnRender(DrawingContext dc)` | Protected Override | Rendert Terminal-Grid: zeichnet Hintergrund, Zeichen mit Farben und Cursor |
| `OnPreviewKeyDown(KeyEventArgs e)` | Protected Override | Handler für Tastatureingabe: nutzt `KeyToVt100Encoder.Encode()`, schreibt Bytes in `InputStream` |
| `OnTextInput(TextCompositionEventArgs e)` | Protected Override | Handler für Text-Eingabe: nutzt `KeyToVt100Encoder.EncodeText()`, schreibt Bytes in `InputStream` |
| `WriteToInputStream(byte[] bytes)` | Private | Schreibt Bytes in `Session.InputStream` und ruft `MarkInputActivity()` auf; fehlerbehandlung mit Logger |
| `OnMouseDown(MouseButtonEventArgs e)` | Protected Override | Fokussiert Control auf Mausklick |
| `OnRenderSizeChanged(SizeChangedInfo sizeInfo)` | Protected Override | Passt Buffer und Pseudo Console an Größenänderungen an |
| `MeasureCellSize()` | Private | Berechnet Zellenbreite/-höhe mit Consolas-Font (Size 13) |
| `CalculateCols()` | Private | Berechnet Spaltenanzahl aus `ActualWidth` |
| `CalculateRows()` | Private | Berechnet Zeilenanzahl aus `ActualHeight` |

**Fehler-Handler-Registrierung:**
- In `OnSessionChanged()`: Registriert Handler auf `Session.BufferChanged` für Neuzeichnung

**Fehlende Funktionalität (Implementierungsbedarf):**
- `Ctrl+V`-Handler in `OnPreviewKeyDown()` → `ReadClipboardAndInsertAsync()`
- `ReadClipboardAndInsertAsync()` – Lesemethode für Clipboard
- `GetClipboardText()` – WPF Clipboard-Integration
