# Plan-Review: Texte aus CLI-Ausgaben kopieren

## Status: Offene Aufgaben vorhanden

Die Implementierung deckt wesentliche Teile der Auswahl und des Kopierens bereits ab, erfüllt den Plan aber noch nicht vollständig. Die Änderungen wurden anhand des aktuellen Codes geprüft; es wurden keine Tests ausgeführt.

## Umgesetzte Planelemente

- `TerminalBuffer` liefert in einem konsistenten Snapshot Grid- und Scrollback-Zeilen-IDs, Zellversionen sowie eine Generation. Schreib-/Erase-Pfade und Alternate-Screen-, Reset- und Resize-Wechsel aktualisieren diese Identitätsdaten.
- `TerminalControl` unterstützt Mausauswahl, Shift-Pfeile sowie Shift-Home/End, zeichnet ein Auswahl-Overlay und extrahiert Text zeilenweise mit `Environment.NewLine` und getrimmten terminalbreiten Endleerzeichen.
- `Ctrl+Shift+C` kopiert bei gültiger Auswahl; ohne Auswahl wird die Taste nicht als CLI-Eingabe kodiert. `Ctrl+C` ist nicht als Kopieren belegt. Clipboard-Schreibfehler werden abgefangen und protokolliert.
- Auswahlvalidierung prüft Buffergeneration, ausgewählte Zeilen-IDs und Zellversionen. Normale Ausgabe außerhalb der Auswahl kann dadurch erhalten bleiben; Resize, Reset und Screenwechsel invalidieren sie.

## Offene Aufgaben

1. **Pflicht-E2E fehlt:** Es gibt keine registrierten Szenarien `TerminalText_LiveMarkCopyAndKeepSelection` und `ReplayText_MarkAndCopy` in `MainTest.cs` oder den E2E-Tests. Damit sind Live- und Replay-Auswahl, sichtbare Pixelhervorhebung, Clipboardinhalt, Scrollback-Erhalt und Tastaturauswahl im tatsächlichen UI nicht nachgewiesen.
2. **Geplante Unit-/Control-Tests fehlen:** In `TerminalControlTests`, `TerminalBufferTests`, `TerminalControlTests.KeyInput` und `TerminalControlTests.ClipboardPaste` wurden keine Tests für Auswahl/Extraktion, Version-/Generationsinvalidierung, Scrollback-Nachführung, Ctrl+Shift+C oder Clipboard-Schreibfehler ergänzt. Die geforderten Regressionen für Ctrl+C und Ctrl+V sind somit ebenfalls nicht im Feature-Testnachweis ergänzt.
3. **Zellversionen bei Zellverschiebungen:** `TerminalBuffer.CopyCell` übernimmt die Versionsnummer der Quellzelle, statt für die Zielzelle eine neue Version zu vergeben. Bei Insert/Delete-/Scroll-Operationen wird dadurch Zellinhalt innerhalb derselben Zeilen-ID verschoben, ohne jede betroffene Zielzelle mit einer neuen Schreibversion zu versehen. Das weicht von U-01 („jede Mutation einer Zelle … vergibt eine neue Zellversion“) ab und kann die Auswahlbindung an eine logische Zelle statt an den ursprünglichen Zellinhalt koppeln. Schreibversionen für Zielzellen müssen bei solchen Mutationen neu vergeben werden; reine Zeilenverschiebung in den Scrollback darf Identitäten dagegen erhalten.

## Hinweise

- Der Review bewertet den aktuellen Implementierungsstand, nicht die erfolgreiche Ausführung der Tests oder E2E-Abnahme.
- Vor Abschluss sind die drei offenen Punkte zu bearbeiten; insbesondere gelten die beiden E2E-Szenarien laut Plan als Pflicht und dürfen nicht durch Unit-Tests ersetzt werden.
