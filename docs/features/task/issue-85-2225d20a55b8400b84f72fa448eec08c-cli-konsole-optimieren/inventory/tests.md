# Tests: Bestandsaufnahme

## Testklassen

### `TerminalBufferTests`
**Datei:** `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`

| Test-Methode | Getestetes Verhalten |
|--------------|---------------------|
| `Buffer_SchreibtText_AktualisiertZellen()` | `Apply(TextWrittenEvent)` schreibt Zeichen an korrekte Position im Grid |
| `Buffer_CursorMove_AktualisiertPosition()` | `Apply(CursorMovedEvent)` aktualisiert CursorRow und CursorCol |
| `Buffer_Newline_ScrolltBeiLetzterZeile()` | Newline in letzter Zeile scrollt Buffer um eine Zeile nach oben |
| `Buffer_Resize_ErhaeltSichtbarenInhalt()` | `Resize()` erhält sichtbaren Inhalt im sichtbaren Bereich |
| `Buffer_ClearScreen_SetzAllesZurueck()` | `Apply(ScreenClearedEvent(2))` setzt alle Zellen zurück und Cursor auf (0,0) |
| `Buffer_ColorChange_NachfolgenderTextErbtFarbe()` | `Apply(ColorChangedEvent)` setzt SGR-Attribut, nachfolgende Zeichen erben Farbe |
| `Buffer_GetRow_GibtKopieZurueck()` | `GetRow()` gibt Kopie zurück, nicht Referenz |
| `Buffer_Resize_KleinerAlsInhalt_WirftNicht()` | `Resize()` auf kleinere Größe wirft keine Exception |

**Abdeckung:** Basis-Funktionalität von `Apply()` und `Resize()`, Farbattribute, Zellen-Kopien. Keine Thread-Sicherheits-Tests.

---

### `TerminalControlTests`
**Datei:** `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`

| Test-Methode | Getestetes Verhalten |
|--------------|---------------------|
| `OnTextInput_WriteThrows_LogsWarning()` | Schreibfehler auf `InputStream` wird über Logger protokolliert |
| `OnSessionChanged_RegistersBufferChangedHandler()` | Session-Wechsel registriert Handler auf `BufferChanged` und stößt Neuzeichnung an |
| `OnSessionChanged_ToNewSession_DeregistersOldHandler()` | Wechsel zu neuer Session deregistriert Handler der alten Session (kein Memory-Leak) |
| `OnSessionChanged_ToNull_DeregistersAllHandlers()` | Setzen auf `null` deregistriert alle Handler |
| `ParallelSessions_NoBufferInterference()` | Zwei parallele Sessions mit unterschiedlicher Ausgabe beeinflussen sich nicht gegenseitig |
| `SessionSwitch_BackToPreviousSession_PreservesBuffer()` | Wechsel von Session A zu B und zurück zu A erhält Bufferinhalt von A |

**Hilfsmethoden/Streams:**
- `ImmediateEofStream` – Stream, der sofort 0 Bytes liefert
- `FixedContentStream` – Stream mit festem Inhalt, dann EOF
- `WriteThrowingStream` – Stream, der beim Schreiben Exception wirft
- `ControllableStream` – Stream mit Queue für gezieltes Bereitstellen von Inhalt

**Abdeckung:** Event-Handler-Registrierung, parallele Sessions, Fehlerbehandlung. Nutzt Reflection zum Zugriff auf private Logger und Streams.

---

### `AnsiSequenceParserTests`
**Datei:** `src/Softwareschmiede.Tests/Infrastructure/Terminal/AnsiSequenceParserTests.cs`

| Test-Methode | Getestetes Verhalten |
|--------------|---------------------|
| `Parse_PlainText_ErgibtTextWrittenEvent()` | Klartext ohne Escapes ergibt `TextWrittenEvent` mit korrektem Text |
| `Parse_SgrFarbe_ErgibtColorChangedEvent()` | SGR-Sequenz ESC[31m ergibt `ColorChangedEvent` mit roter Vordergrundfarbe |
| `Parse_SgrReset_ErgibtColorChangedEventMitStandardfarben()` | SGR-Reset ESC[0m ergibt `ColorChangedEvent` mit Reset=true |
| `Parse_Sgr24BitFarbe_WirdKorrektParsiert()` | SGR 24-Bit-Farbe ESC[38;2;100;200;50m ergibt korrekte RGB-Vordergrundfarbe |
| `Parse_CursorMove_ErgibtCursorMovedEvent()` | ESC[5;10H ergibt `CursorMovedEvent` mit Row=4, Col=9 (0-basiert) |
| `Parse_ClearScreen_ErgibtScreenClearedEvent()` | ESC[2J ergibt `ScreenClearedEvent` |
| `Parse_EraseLine_ErgibtLineErasedEvent()` | ESC[K ergibt `LineErasedEvent` |
| `Parse_MehrteiligePakete_WerdenZusammengesetzt()` | Escape-Sequenz über zwei Parse-Aufrufe aufgeteilt wird vollständig verarbeitet |
| `Parse_SgrBold_SetzBoldTrue()` | SGR-Sequenz Bold ESC[1m setzt Bold=true |
| `Parse_CursorHide_ErgibtCursorVisibilityChangedEventFalse()` | Cursor-Sichtbarkeit ESC[?25l ergibt `CursorVisibilityChangedEvent` mit Visible=false |
| `Parse_CursorShow_ErgibtCursorVisibilityChangedEventTrue()` | Cursor-Sichtbarkeit ESC[?25h ergibt `CursorVisibilityChangedEvent` mit Visible=true |

**Abdeckung:** VT100-Parsing (Klartext, SGR-Farben, Cursor-Bewegung, Screen-Clear, Split-Sequenzen). Standard-Farben, 256er-Palette und RGB-Farben.

---

### `PseudoConsoleSessionTests`
**Datei:** `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`

| Test-Methode | Getestetes Verhalten |
|--------------|---------------------|
| `ReadLoopAsync_WithException_LogsAndContinues()` | Lesefehler wird über Logger protokolliert, Schleife beendet sauber |
| `ReadLoopAsync_CancellationToken_GracefulShutdown()` | Abbruch des CancellationTokens beendet Schleife sauber |
| `SessionDispose_CancelsReadLoop()` | `Dispose()` bricht Leseschleife ab und schließt Streams |
| `Dispose_ClosesOutputStreamImmediately_UnblocksNonCancelableRead()` | `Dispose()` schließt Output-Stream sofort, um blockierte Reads zu unterbrechen |
| `Dispose_CalledConcurrently_RunsCleanupExactlyOnce()` | Gleichzeitiger `Dispose()`-Aufruf aus mehreren Threads führt Cleanup nur einmal aus (Interlocked-Guard) |
| `Dispose_ReadLoopNeverCompletes_ReturnsPromptlyWithoutWaiting()` | `Dispose()` wartet nicht synchron auf Leseschleife (verhindert ThreadPool-Blockade) |

**Hilfsmethoden/Streams:**
- `ThrowingStream` – Stream, der beim Lesen Exception wirft
- `BlockingUntilCancelledStream` – Stream, dessen Read blockiert bis Token abgebrochen wird
- `NonCancelableBlockingStream` – Stream, dessen Read Token ignoriert (nur Schließen unterbricht)
- `DisposeCountingStream` – Zählt Dispose-Aufrufe
- `HangingForeverStream` – Read kehrt nie zurück

**Abdeckung:** Leseschleife-Fehlerbehandlung, Disposal-Sicherheit (Concurrency, ThreadPool), Synchronisierung. Intensive Tests für Dispose-Verhalten bei paralleler CLI-Ausführung (Issue-86).

---

## Test-Konfiguration

**Framework:** xUnit  
**Assertions:** FluentAssertions  
**Mocking:** Moq (für Logger-Mocks)  
**Besonderheiten:** 
- `TerminalControlTests` nutzt STA-Thread (WPF Dispatcher) via `RunOnSta()`
- `PseudoConsoleSessionTests` nutzt SpinWait für Race-Condition-Tests
- Custom Stream-Implementierungen für verschiedene Fehlerszenarien

---

## Fehlende Tests (Implementierungsbedarf)

- **TerminalBuffer Thread-Sicherheit:** Parallele `Apply()` + `GetRow()` Zugriffe
- **KeyToVt100Encoder Clipboard-Text:** `EncodeClipboardText()` Methode (Single-line, Multi-line, Unicode, Newlines)
- **TerminalControl Ctrl+V:** `Ctrl+V`-Eingabe-Handling, Clipboard-Lesefehler
- **Integrations-Tests:** Parallele CLI-Ausgabe + Clipboard-Paste ohne Blockade
