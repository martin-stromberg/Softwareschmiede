# Code-Review: Texte kopieren

## Status: Befunde vorhanden

Geprüft wurden die aktuellen Änderungen an `TerminalControl`, `TerminalBuffer` und den zugehörigen Tests. Zusätzlich wurden sämtliche Vorkommen von `RaiseUiActionRequested` unter `src/` geprüft. Es gibt dort keine Vorkommen; damit existiert auch keine ausgelöste Aktion ohne Handler in einer Blazor-Seite oder -Komponente.

## Befunde

1. **Hoch – Eine Auswahl wird bei gewöhnlichem Scrollback-Scrollen fälschlich ungültig.**  
   `ScrollRangeUp` und `ScrollRangeDown` verschieben vollständige logische Zeilen, übernehmen danach aber deren Zeilen-ID (`src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs:343-357`, `367-381`). Das verwendete `CopyCell` vergibt dabei jeder Zielzelle eine neue Version (`:652-659`). `TerminalControl.TryNormalizeSelection` verlangt weiterhin die ursprünglich gespeicherte Zellversion (`src/Softwareschmiede.App/Controls/TerminalControl.cs:742-755`), sodass `ValidateSelection` die Auswahl bei der nächsten Ausgabe löscht – obwohl Inhalt und logische Zeile lediglich ihre sichtbare Position geändert haben. Das verletzt die verbindliche Entscheidung, dass eine Auswahl normale Ausgabe und Scrollback-Verschiebungen überdauert.  
   **Korrektur:** Für Verschiebungen vollständiger logischer Zeilen Zellversionen gemeinsam mit den Zellen verschieben. Für Insert/Delete-Zeichen muss dagegen weiterhin eine neue Zielversion vergeben werden. Ein Control-Test muss eine Auswahl auf einer bei einem Scroll verschobenen (nicht herausfallenden) Zeile anlegen und nach dem Scroll sowohl ihre Gültigkeit als auch den unveränderten Kopiertext prüfen.

2. **Mittel – Die verpflichtenden E2E-Abnahmen fehlen weiterhin.**  
   Weder `TerminalText_LiveMarkCopyAndKeepSelection` noch `ReplayText_MarkAndCopy` sind in `src/Softwareschmiede.Tests/E2E/` implementiert oder in `MainTest` registriert. Damit fehlen insbesondere der Nachweis im echten UI für Mausauswahl, sichtbare Hervorhebung, Clipboardinhalt, Tastaturauswahl und den Erhalt der Auswahl nach Ausgabe bzw. Scrollback. Laut Plan sind diese zwei Szenarien Pflicht und können nicht durch die vorhandenen Unit-Tests ersetzt werden.

## Prüfung

- `git diff --check`: keine Whitespace-Befunde (nur Hinweise zur CRLF-Normalisierung).
- Tests wurden in diesem Review nicht ausgeführt; der gesonderte Testschritt liefert deren Ausführungsergebnis.

## Ergebnis

Vor der Abnahme müssen die Befunde behoben und die beiden verpflichtenden E2E-Szenarien erfolgreich ausgeführt werden.
