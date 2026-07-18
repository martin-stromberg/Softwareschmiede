# Tasks: Korrektur der CLI-Ausgabeanzeige

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik (Buffer) | `ClearAllCells()` in `TerminalBuffer` extrahieren (Grid füllen + `_scrollback.Clear()`) und `ApplyClearScreen(2)` darauf umstellen | Offen | — |
| 2 | Logik (Buffer) | `ApplyText`: Fall `'\n'` zusätzlich `_cursorCol = 0` setzen (LF wirkt wie CRLF) | Offen | — |
| 3 | Logik (Buffer) | `Resize`: beim Verkleinern der Zeilen untere Zeilen kopieren, Cursor-Versatz anwenden, obere Zeilen in `_scrollback` einreihen (Spalten rechts abschneiden, kein Reflow) | Offen | — |
| 4 | Logik (Buffer) | Test-sichtbaren Scrollback-Zugriff bereitstellen (`internal ScrollbackCount` + `InternalsVisibleTo`, falls noch nicht vorhanden) | Offen | — |
| 5 | UI (Control) | `TerminalControl.OnRenderSizeChanged`: `InvalidateVisual()` nach Resize ergänzen | Offen | — |
| 6 | Tests | `Buffer_LineFeed_SetztSpalteAufNull` in `TerminalBufferTests` schreiben | Offen | — |
| 7 | Tests | `Buffer_CarriageReturnLineFeed_ErgibtEinenUmbruch` in `TerminalBufferTests` schreiben | Offen | — |
| 8 | Tests | `Buffer_CarriageReturnAllein_BleibtInZeile` in `TerminalBufferTests` schreiben | Offen | — |
| 9 | Tests | `Buffer_ResizeKleiner_ErhaeltUntereZeilen` in `TerminalBufferTests` schreiben | Offen | — |
| 10 | Tests | `Buffer_ResizeKleiner_CursorFolgtUnterenZeilen` in `TerminalBufferTests` schreiben | Offen | — |
| 11 | Tests | `Buffer_ResizeSchmaler_SchneidetRechtsAb` in `TerminalBufferTests` schreiben | Offen | — |
| 12 | Tests | `Buffer_ClearScreenMode2_AlleZellenLeer` in `TerminalBufferTests` schreiben | Offen | — |
| 13 | Tests | `Buffer_ClearScreenMode2_LeertScrollback` in `TerminalBufferTests` schreiben | Offen | — |
| 14 | Tests | `Parse_CrLfText_ErgibtTextMitCrLf` in `AnsiSequenceParserTests` schreiben | Offen | — |
| 15 | Tests | Bestehenden `Buffer_Newline_ScrolltBeiLetzterZeile` an neue LF-Spaltensemantik anpassen | Offen | — |
| 16 | E2E-Tests | E2E-Szenario „mehrzeilige CLI-Ausgabe ohne Treppeneffekt/ausgelassene Zeilen" (Category `OsInterface`) schreiben | Offen | — |
