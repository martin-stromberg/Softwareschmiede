# Bestandsaufnahme: Korrektur der CLI-Ausgabeanzeige (Issue 149)

Diese Bestandsaufnahme dokumentiert den bestehenden Code für die Terminal-Rendering-Pipeline (Buffer, Parser, Control) und testet die Anforderung zur Behebung von Darstellungsfehlern in der `TerminalControl`-Anzeige.

---

## Zusammenfassung

### Vorhanden

- **Datenmodelle**: Vollständige Event-Hierarchie (TextWrittenEvent, CursorMovedEvent, ColorChangedEvent, ScreenClearedEvent, LineErasedEvent, CursorVisibilityChangedEvent)
- **Terminal-Buffer**: Thread-sicherer 2D-Grid (TerminalBuffer), Cursor-Verwaltung, SGR-Attribute, Scrollback-Ringpuffer
- **ANSI-Parser**: Zustandsbehafteter VT100-Parser mit Unterstützung für Escape-Sequenzen, Farben (16er, 256er, 24-Bit), Cursor-Bewegung, Screen-Clear
- **Render-Pipeline**: TerminalControl als WPF-Control mit OnRender() für Canvas-Rendering
- **Grundtests**: Unit-Tests für Buffer, Parser, PseudoConsoleSession
- **Leseschleife**: PseudoConsoleSession mit kontinuierlicher Leseschleife, Buffer-Aktualisierung nach jedem Chunk
- **Größenänderung**: Buffer.Resize() und Propagierung an TerminalControl via OnRenderSizeChanged

### Offene oder potenzielle Probleme

1. **Zeilenumbruch-Normalisierung**
   - `AnsiSequenceParser.FlushText()` gibt Text als Ganzes an `TextWrittenEvent` (einschließlich `\r`, `\n`)
   - `TerminalBuffer.ApplyText()` verarbeitet `\r` und `\n` als separate Zeichen, nicht als kombinierte CRLF-Sequenz
   - Keine Tests für CRLF-Normalisierung

2. **Screen-Clear-Operationen**
   - `TerminalBuffer.ApplyClearScreen(mode=2)` füllt nur das visible Grid (0 bis _rows-1)
   - Unklar, ob Altinhalte außerhalb des erwarteten Bereichs garantiert gelöscht werden
   - Keine `ClearAllCells()`-Methode wie in der Anforderung erwähnt

3. **Render-Integrität**
   - `TerminalControl.OnRender()` zeichnet nur Hintergrund, wenn != Schwarz
   - Bei Clear-Operationen ist unklar, ob der gesamte sichtbare Bereich neu gezeichnet wird
   - `InvalidateVisual()` wird nach jedem BufferChanged aufgerufen, aber keine Tracking von betroffenen Bereichen

4. **Resize-Handling**
   - `TerminalBuffer.Resize()` kopiert alte Inhalte in neue Grid-Größe
   - Bei Verkleinerung können Teile alter Zeilen noch sichtbar sein (abhängig von Render-Logik)
   - Kein explizites Aufräumen von Altinhalten vor Resize

5. **Test-Lücken**
   - Keine Tests für vollständiges Screen-Clear mit Verifizierung aller Zellen
   - Keine Tests für Resize mit Altinhalten-Reinigung
   - Keine E2E-Tests für Render-Verhalten nach Clear/Resize

---

## Details

### [Datenmodelle](inventory/models.md)

- `TerminalBuffer` — 2D-Grid-Verwaltung, Cursor, SGR-Attribute, Scrollback
- `TerminalCell` — einzelne Zelle mit Zeichen und Formatierung
- `TerminalEvent` (und Untertypen) — Event-Hierarchie für Parser-Output
- `TerminalBufferSnapshot` — konsistenter Snapshot für sichere Render-Operationen

### [Logikklassen](inventory/logic.md)

- `TerminalBuffer` — Apply(), Resize(), GetRow(), GetSnapshot(), ApplyText(), ApplyClearScreen(), ApplyEraseLine()
- `AnsiSequenceParser` — Parse(), FlushText(), ProcessCsiCommand(), ParseSgr(), ParseExtendedColor()
- `PseudoConsoleSession` — ReadLoopAsync(), Resize(), MarkOutputActivity(), WritePromptAsync(), NormalizeToCarriageReturn()
- `TerminalControl` — OnRender(), OnSessionChanged(), OnRenderSizeChanged(), OnPreviewKeyDown()

### [Tests und Hilfsmethoden](inventory/tests.md)

- `TerminalBufferTests` — Tests für Apply(), Resize(), GetRow(), Parallelität
- `AnsiSequenceParserTests` — Tests für Parse(), Escape-Sequenzen, Farben, Cursor-Bewegung
- `PseudoConsoleSessionTests` — Tests für Fehlerbehandlung, Shutdown, Parallelität
- Test-Lücken: Screen-Clear-Vollständigkeit, Resize-Reinigung, Zeilenumbruch-Normalisierung, Render-Integrität

---

## Kritische Befunde für die Anforderungsbearbeitung

1. **CRLF-Verarbeitung**: Die Trennung von CR/LF in `ApplyText()` könnte zu fehlerhaften Zeilenumbrüchen führen. Eine Normalisierung in der Parse-Phase (AnsiSequenceParser) oder eine kombinierte Behandlung in `ApplyText()` wäre konsistent mit der Windows-Console-Semantik.

2. **Screen-Clear-Vollständigkeit**: Es ist unklar, ob `ApplyClearScreen(mode=2)` alle Zellen wirklich auf `TerminalCell.Default` setzt oder nur den sichtbaren Bereich. Eine explizite `ClearAllCells()`-Methode würde dies klären.

3. **Render-Abdeckung**: `TerminalControl.OnRender()` zeichnet Hintergrund nur wenn != Schwarz. Bei Clear-Operationen müssen explizit alle Zellen neu gezeichnet werden, nicht nur die gefärbten.

4. **Resize-Konsistenz**: Beim Resize auf kleinere Größe sollten Zeilen außerhalb des neuen Bereichs nicht sichtbar sein. Die aktuelle `Resize()`-Implementierung kopiert nur Inhalte in den neuen Bereich, füllt aber bereits die Rest-Zeilen mit `FillGrid()`.

5. **Test-Abdeckung**: Regress-Tests für diese Fehlerszenarien sind notwendig, um Regressions zu vermeiden.
