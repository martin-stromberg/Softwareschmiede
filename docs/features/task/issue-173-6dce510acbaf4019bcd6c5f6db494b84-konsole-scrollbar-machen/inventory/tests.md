# Tests

## Vorhandene Testbereiche

### TerminalControl

Dateien:

- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs`

Bestehende Abdeckung in `TerminalControlTests.cs`:

- Fehler beim Schreiben in den Input-Stream werden geloggt.
- `OnSessionChanged` registriert `BufferChanged`.
- Wechsel zu neuer Session deregistriert alte Handler.
- Session auf `null` deregistriert Handler.
- Parallele Sessions beeinflussen ihre Buffer nicht gegenseitig.
- Wechsel zurueck zu vorheriger Session erhaelt deren Bufferinhalt.

Diese Tests pruefen Sessionbindung, Dispatcher-Neuzeichnung und Input-Fehlerpfade, aber nicht Scrollbarkeit, Scrolloffset, ScrollViewer-Einbindung oder sichtbare Scrollback-Zeilen.

### TerminalBuffer

Datei:

- `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`

Bestehende Abdeckung:

- Textschreiben, Cursorbewegung, Newline/Scroll, Resize, ClearScreen, Farben.
- Kopie bei `GetRow`.
- Parallelzugriffe und konsistenter Snapshot.
- CR/LF-Verhalten.
- Verkleinerung der Zeilen/Spalten.
- `ClearScreenMode2_LeertScrollback` prueft, dass Scrollback beim Clear geleert wird.

Nicht abgedeckt ist ein oeffentlicher Zugriff auf Scrollback-Zeilen oder ein Snapshot aus Scrollback plus sichtbarem Grid. Da `ScrollbackCount` bereits intern getestet wird, kann eine Erweiterung wahrscheinlich mit fokussierten Unit-Tests im selben Testprojekt abgedeckt werden.

### PseudoConsoleSession

Dateien:

- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests_WritePromptAsync.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/CliRuntimeStatusEvaluatorTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/AnsiSequenceParserTests.cs`

Bestehende Abdeckung in `PseudoConsoleSessionTests.cs`:

- Lesefehler und Cancellation.
- Dispose-Verhalten und Race Conditions.
- `BufferChanged` wird nach Bufferupdate gefeuert.
- Output-Sink erhaelt Rohbytes, Buffer wird weiter aktualisiert.

Diese Tests sind fuer Regressionen in der Output-Pipeline relevant. Scrollbarkeit sollte diese Pfade nicht veraendern muessen, ausser der Buffer-Snapshot oder Resize-Semantik wird geaendert.

## Sinnvolle neue Tests fuer die Planung

- `TerminalBuffer`: Snapshot enthaelt Scrollback-Zeilen in korrekter Reihenfolge, wenn mehr Zeilen geschrieben wurden als sichtbar sind.
- `TerminalBuffer`: Scrollback-Grenze bleibt definiert und aelteste Zeilen werden verworfen.
- `TerminalControl`: Bei mehr Verlauf als sichtbaren Zeilen wird ein groesseres Scroll-Extent oder ein Scrolloffset verarbeitet.
- `TerminalControl`/View: Manuelles Zurueckscrollen wird bei neuer Ausgabe nicht sofort ueberschrieben, falls Auto-Scroll nur am Ende aktiv sein soll.
- E2E/UI-Test: In der CLI-Ansicht existiert fuer `TerminalConsole` bzw. dessen Container eine vertikale Scrollmoeglichkeit, wenn langer Output erzeugt wurde.

## Testausfuehrung

Es wurden keine Tests ausgefuehrt, weil die Aufgabe ausdruecklich eine Bestandsaufnahme ohne Codeaenderungen verlangt.
