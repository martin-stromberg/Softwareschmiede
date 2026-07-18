# Tests und Hilfsmethoden

## Testklassen

### `TerminalBufferTests`
Datei: `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`

Unit-Tests für `TerminalBuffer`:

- `Buffer_SchreibtText_AktualisiertZellen()` — Verifiziert, dass `TextWrittenEvent("ABC")` Zeichen an korrekten Positionen schreibt
- `Buffer_CursorMove_AktualisiertPosition()` — Testet `CursorMovedEvent` für Cursor-Repositionierung
- `Buffer_Newline_ScrolltBeiLetzterZeile()` — Testet, dass Linefeed in letzter Zeile scrollt
- `Buffer_Resize_ErhaeltSichtbarenInhalt()` — Testet `Resize()`-Methode und Erhalt von Inhalt bei Größenänderung
- `Buffer_ClearScreen_SetzAllesZurueck()` — Testet `ScreenClearedEvent(2)` (Mode 2 = gesamter Screen), erwartet Cursor bei (0,0) und alle Zellen als Leerzeichen
- `Buffer_ColorChange_NachfolgenderTextErbtFarbe()` — Testet `ColorChangedEvent` und Vererbung auf nachfolgenden Text
- `Buffer_GetRow_GibtKopieZurueck()` — Verifiziert, dass `GetRow()` eine Kopie gibt (Isolierung des Buffers)
- `Buffer_Resize_KleinerAlsInhalt_WirftNicht()` — Resize auf kleinere Größe sollte keine Exception werfen
- `Buffer_ParallelApplyAndRead_NoRaceCondition()` — Stress-Test: parallele Apply() und GetRow() aus mehreren Threads, erwartet keine Exceptions oder Inkonsistenzen
- `Buffer_GetSnapshot_ReturnsConsistentState()` — Stress-Test: parallele Resize() und Apply() während GetSnapshot()-Aufrufe, erwartet stets konsistente Snapshot-Dimensionen

**Beobachtungen:**
- Tests prüfen hauptsächlich Unit-Verhalten von Apply(), Resize() und GetRow()
- Es gibt Tests für Parallelität, aber keine speziellen Tests für:
  - Vollständige Screen-Clear-Operationen mit Überprüfung aller Zellen außerhalb des erwarteten Inhalts
  - Resize mit Reinigung von Altinhalten
  - Kombination von CR/LF in `ApplyText()`

---

### `AnsiSequenceParserTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/AnsiSequenceParserTests.cs`

Unit-Tests für `AnsiSequenceParser`:

- `Parse_PlainText_ErgibtTextWrittenEvent()` — Klartext ohne Escapes ergibt `TextWrittenEvent`
- `Parse_SgrFarbe_ErgibtColorChangedEvent()` — ESC[31m (rot) ergibt `ColorChangedEvent` mit roter Vordergrundfarbe
- `Parse_SgrReset_ErgibtColorChangedEventMitStandardfarben()` — ESC[0m (Reset) ergibt `ColorChangedEvent` mit `Reset=true`
- `Parse_Sgr24BitFarbe_WirdKorrektParsiert()` — ESC[38;2;100;200;50m (24-Bit-RGB) wird korrekt geparsed
- `Parse_CursorMove_ErgibtCursorMovedEvent()` — ESC[5;10H (absolute Positionierung) ergibt `CursorMovedEvent` mit Row=4, Col=9 (1-basiert → 0-basiert)
- `Parse_ClearScreen_ErgibtScreenClearedEvent()` — ESC[2J ergibt `ScreenClearedEvent`
- `Parse_EraseLine_ErgibtLineErasedEvent()` — ESC[K ergibt `LineErasedEvent`
- `Parse_MehrteiligePakete_WerdenZusammengesetzt()` — Split ESC[31m über zwei Parse()-Calls wird zusammengesetzt (Zustandsbehaftung)
- `Parse_SgrBold_SetzBoldTrue()` — ESC[1m setzt `Bold=true`
- `Parse_CursorHide_ErgibtCursorVisibilityChangedEventFalse()` — ESC[?25l ergibt `CursorVisibilityChangedEvent` mit `Visible=false`
- `Parse_CursorShow_ErgibtCursorVisibilityChangedEventTrue()` — ESC[?25h ergibt `CursorVisibilityChangedEvent` mit `Visible=true`

**Beobachtungen:**
- Tests prüfen hauptsächlich einzelne Escape-Sequenzen
- Keine speziellen Tests für:
  - CR/LF-Kombinationen (CRLF) in `TextWrittenEvent`
  - Mehrere Escape-Sequenzen in einem Chunk
  - Komplexe Multi-Byte-Szenarien

---

### `PseudoConsoleSessionTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`

Unit-Tests für `PseudoConsoleSession`:

- `ReadLoopAsync_WithException_LogsAndContinues()` — Wenn Output-Stream Exception wirft, muss Leseschleife diese loggen und sauber beenden
- `ReadLoopAsync_CancellationToken_GracefulShutdown()` — Bei `Dispose()` muss Leseschleife sauber beendet werden
- `SessionDispose_CancelsReadLoop()` — `Dispose()` muss Output-Stream schließen und Leseschleife beenden
- `Dispose_ClosesOutputStreamImmediately_UnblocksNonCancelableRead()` — Selbst wenn Stream-Read nicht kooperativ abbrechbar ist, muss `Dispose()` durch Stream-Schließen sofort zum Beenden führen
- weitere Tests zur Thread-Sicherheit und Parallelität (PseudoConsoleSessionTests_WritePromptAsync.cs, SimulatedPseudoConsoleProcessLauncherTests.cs)

**Beobachtungen:**
- Tests prüfen hauptsächlich Fehlerbehandlung und Shutdown-Sicherheit der Leseschleife
- Keine Integrations-Tests für Parser-Buffer-Interaktion oder Render-Verhalten

---

## Hilfsmethoden und Test-Utilities

### In Testdateien verwendete Helfer

**`Encoding.UTF8.GetBytes(string)` — in `AnsiSequenceParserTests`**
- Konvertiert String-Escapes zu Byte-Array für Parser-Tests

**`Stream`-Mocks/Test-Implementierungen**
- `ThrowingStream` — wirft Exception beim Lesen (PseudoConsoleSessionTests)
- `BlockingUntilCancelledStream` — blockiert bis `CancellationToken` ausgelöst wird
- `NonCancelableBlockingStream` — blockiert ohne Kooperation mit CancellationToken

**`NormalizeToCarriageReturn(string)` (öffentliche static Methode in `PseudoConsoleSession`)**
- Konvertiert CRLF und LF zu CR
- Verwendet in `WritePromptAsync()`, aber auch in Tests nutzbar

---

## Beobachtungen zu Test-Lücken

Basierend auf der Anforderung zu Darstellungsfehlern sind folgende Test-Bereiche unterrepräsentiert:

1. **Screen-Clear und Resize-Szenarien**
   - Keine Tests, die verifizieren, dass `TerminalBuffer.ApplyClearScreen(mode=2)` wirklich alle Zellen außerhalb des erwarteten Inhalts löscht
   - Keine Tests für Resize mit Reinigung von Altinhalten bei Verkleinerung

2. **Zeilenumbruch-Normalisierung**
   - Keine Tests für `\r\n` (CRLF) als kombinierte Sequenz in `TextWrittenEvent`
   - Keine Tests für unterschiedliche Zeilenumbruch-Varianten (CR allein, LF allein, CRLF)

3. **Render-Integrität**
   - Keine E2E-Tests für `TerminalControl.OnRender()` nach Clear/Resize
   - Keine Verifikation, dass Hintergrund-Render beim Clear den gesamten sichtbaren Bereich bereinigt

4. **Partial-Rendering-Probleme**
   - Keine Tests, die Partial-Rendering-Fehler demonstrieren (z. B. alte Zeilen bleiben sichtbar)
   - Keine Tests für Race Conditions zwischen Buffer-Updates und Render-Operationen

5. **Scrollback-Konsistenz**
   - Keine Tests für Scrollback-Handling beim Clear
   - Keine Verifikation, dass Scrollback-Zeilen korrekt erhalten bleiben
