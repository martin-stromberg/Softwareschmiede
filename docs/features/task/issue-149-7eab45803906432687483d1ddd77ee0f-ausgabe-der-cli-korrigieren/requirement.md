# Übersetzte Anforderung: Korrektur der CLI-Ausgabeanzeige

## Fachliche Zusammenfassung

Die `TerminalControl`-Anzeige der CLI-Prozessausgabe zeigt Darstellungsfehler auf: Zeilen werden ausgelassen, Texte doppelt angezeigt, alte Zeilen bleiben sichtbar, obwohl sie längst überschrieben sein sollten, und Zeilenumbrüche entstehen, die nicht in der originalen Windows-Konsolenausgabe vorkommen. Die Ursache liegt darin, dass bei vollständigem Neuaufbau der Konsolenausgabe nicht der gesamte betroffene Ausgabenbereich aktualisiert wird, insbesondere beim Screen-Resize oder bei Bildschirm-Lösch-Operationen (`ScreenClearedEvent`), und dass Zeilenvorschübe nicht korrekt als Kombination aus Carriage Return (CR, `\r`) und Line Feed (LF, `\n`) verarbeitet werden. Dies führt zu Inkonsistenzen zwischen der echten Windows-Konsolenausgabe und ihrer Darstellung in `TerminalBuffer` und `TerminalControl`.

## Betroffene Klassen und Komponenten

- **`TerminalBuffer`** (Domäne, `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`) — Verwaltung des 2D-Grids aus `TerminalCell`-Werten, Cursor-Position, SGR-Attribute und Scrollback-Ringpuffer. Betroffen: Logik für `ApplyText`, `ApplyClearScreen`, `ApplyEraseLine`; möglicherweise neue Methoden zur vollständigen Grid-Bereinigung vor Resize/Clear-Operationen.
- **`TerminalControl`** (WPF-UI, `src/Softwareschmiede.App/Controls/TerminalControl.cs`) — Rendert einen `TerminalBuffer` auf WPF-Canvas. Betroffen: `OnRender`-Logik, um sicherzustellen, dass Lösch-Operationen den gesamten sichtbaren Bereich bereinigen; möglicherweise erweiterte Invalidierung (nicht nur `InvalidateVisual()` nach jedem Event, sondern auch Verfolgung der betroffenen Bereiche).
- **`AnsiSequenceParser`** (Infrastruktur, `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs`) — VT100/ANSI-Parser für Terminal-Escape-Sequenzen. Betroffen: Text-Handling in `FlushText()` und `ProcessCsiCommand()`; möglicherweise neue oder angepasste Behandlung von Zeilenvorschub-Sequenzen.
- **`PseudoConsoleSession`** (Infrastruktur, `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`) — Koordiniert Leseschleife und Ausgabe-Verarbeitung. Möglicherweise betroffen: Verarbeitung des Ausgabe-Chunks vor Übergabe an Parser (z. B. Normalisierung von CR/LF).
- **Domain-Modelle:** `TerminalEvent` (insbesondere `TextWrittenEvent`, `ScreenClearedEvent`, `LineErasedEvent`), `TerminalCell` — ggf. Überprüfung auf fehlende Event-Typen.
- **Tests:** `TerminalBufferTests`, `TerminalControlTests`, ggf. neue Regressionstests für Screen-Clear und Resize-Szenarien.

## Implementierungsansatz

### 1. Problem-Analyse und Reproduktion
- Sammlung von Testfällen, die die beschriebenen Fehler demonstrieren (fehlende Zeilen, Duplikate, ungültige Zeilenumbrüche).
- Prüfung, ob der Parser Zeilenvorschübe korrekt normalisiert (z. B. `\r\n` als atomare Einheit, nicht getrennt).

### 2. Screen-Wipe beim Clear und Resize
- **`TerminalBuffer`:** Bei `ScreenClearedEvent` mit Mode 2 (Ganzer Bildschirm) oder bei `Resize()`: Gesamtes Grid wird mit Leerzeichen und Standardattributen gefüllt, nicht nur der sichtbare Textbereich. Eine neue Methode `ClearAllCells()` kapselt diese Logik.
- **`TerminalControl`:** Bei `OnRender()`: Nach `ScreenClearedEvent` oder Resize wird die gesamte Kontrollfläche neugezeichnet; möglicherweise wird eine Viewport-Bereinigung eingefügt, bevor neue Inhalte gezeichnet werden.

### 3. Zeilenvorschub-Normalisierung
- **`AnsiSequenceParser`:** Beim Parsen von `TextWrittenEvent` wird sichergestellt, dass:
  - Sequenzen `\r\n` (CRLF, Windows-Standard) oder `\n` alleine (Unix) beide korrekt als **Zeile-Vorschub + Carriage Return** (nächste Zeile, Spalte 0) interpretiert werden.
  - `\r` alleine (altes Mac-Format) ebenfalls korrekt behandelt wird (aktueller Zeile, Spalte 0, ohne Vorschub).
- Möglicherweise neue Events: `LineBreakEvent` (mit Info zur Art des Breaks) oder erweiterte `TextWrittenEvent`, um Text und Breaks separat zu behandeln.

### 4. Partial-Rendering vermeiden
- **`TerminalControl.OnRender()`:** Derzeit wird bei jedem `BufferChanged`-Event `InvalidateVisual()` aufgerufen. Dies ist korrekt, aber `OnRender()` muss sicherstellen, dass:
  - Beim Clear-Befehl (`ScreenClearedEvent` wurde vorher angewendet) der gesamte Hintergrund korrekt gezeichnet wird, nicht nur Zellen mit nicht-schwarzem Hintergrund.
  - Beim Resize der sichtbare Bereich neu vermessen wird (`MeasureCellSize()` wird bereits aufgerufen; zu prüfen: wird `_buffer.Resize()` synchron mit der Control-Größe aufgerufen?).

### 5. Speicherung und Validierung des Rendering-Zustands
- **Test-Infrastruktur:** Tests für `TerminalBuffer` und `TerminalControl` sollten nach jedem Szenario überprüfen:
  - Alle Zellen außerhalb des erwarteten Inhalts sind wirklich leer (nicht alte Daten).
  - Keine doppelten Zeichen an ungültigen Positionen.
  - Zeilenumbrüche führen zu Cursor-Positionen auf Spalte 0 der nächsten Zeile.

## Konfiguration

Keine Endbenutzer-sichtbare Konfiguration. Das Feature ist eine Bugfixing-Maßnahme für die Anzeige-Integrität.

## Offene Fragen

1. **Art der Zeilenumbruch-Normalisierung:** Werden alle eingehenden Bytes bereits von ConPTY normalisiert (z. B. auf CRLF), oder erwartet der Parser Varianten (CR, LF, CRLF)?
2. **Timing von Buffer-Resize:** Wird `_buffer.Resize()` vom `TerminalControl` synchron aufgerufen, wenn sich die Kontrollgröße ändert (`SizeChanged`-Event), oder passiert das asynchron? Besteht die Möglichkeit, dass neue Text-Events ankommen, während das Resize läuft?
3. **Scrollback-Handling:** Verschwinden alte Zeilen korrekt aus dem sichtbaren Bereich, wenn neue Text-Events am unteren Rand ankommen, oder bleiben Teile davon stehen?
4. **Resize-Verhalten beim Verkleinern:** Wenn die Kontrolle verkleinert wird (z. B. 50 Zeilen → 30 Zeilen), werden die oberen 20 Zeilen garantiert aus dem sichtbaren Grid entfernt, oder können sie noch sichtbar sein?
5. **Performance:** Sollte `InvalidateVisual()` für jeden Ausgabe-Chunk aufgerufen werden, oder ist eine Batch-Aktualisierung (z. B. alle 16ms) sinnvoller, um Flimmern zu vermeiden?
