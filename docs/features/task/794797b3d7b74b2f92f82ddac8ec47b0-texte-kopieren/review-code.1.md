# Code-Review: Texte kopieren

## Status: Befunde vorhanden

Geprüft wurden die aktuellen Änderungen in `TerminalControl` und `TerminalBuffer` sowie alle Vorkommen von `RaiseUiActionRequested` im Verzeichnis `src/`. Es gibt im Projekt aktuell keine Vorkommen dieses Ereignisses; folglich existiert im geänderten Bereich keine ausgelöste UI-Aktion ohne zugehörigen Blazor-Handler.

## Befunde

1. **Hoch – `Shift+Up` und `Shift+Down` können bei einer neuen Tastaturauswahl abstürzen.**  
   In `src/Softwareschmiede.App/Controls/TerminalControl.cs:664-676` wird bei noch fehlender Auswahl `snapshot.CursorCol` unverändert verwendet. Nach dem Schreiben eines Zeichens in die letzte Spalte darf der Terminalbuffer jedoch `CursorCol == Cols` enthalten. `Shift+Up` oder `Shift+Down` verändert nur die Zeile; anschließend indiziert `GetCellVersion` mit der ungültigen Spalte `Cols`, was zu `IndexOutOfRangeException` führt.  
   **Korrektur:** Vor dem Erzeugen des `TerminalSelectionPoint` die Ausgangsspalte auf `0..Cols - 1` begrenzen. Tests müssen insbesondere eine erste Tastaturauswahl mit `Shift+Up`/`Shift+Down` bei Cursor am rechten Rand abdecken.

2. **Hoch – Nach einem Resize kann eine Auswahl im vorhandenen Scrollback mit einer Ausnahme enden.**  
   `Resize` behält den Scrollback mit seiner ursprünglichen Spaltenzahl bei (`src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs:201-251`). `GetSnapshot` normiert zwar die Zellwerte auf die neue Breite (`:606-610`), kopiert die Versionsarrays der Scrollback-Zeilen aber unverändert (`:612`). Nach einer Verbreiterung kann ein Klick in einer alten Scrollback-Zeile rechts ihrer alten Breite daher über `GetCellVersion` in ein zu kurzes `ScrollbackCellVersions`-Array laufen (`TerminalControl.cs:623-626`).  
   **Korrektur:** Die Versionsarrays beim Snapshot analog zu den Zellen immer auf `Cols` normalisieren (für hinzugefügte Default-Zellen eine definierte Version verwenden), oder Scrollback-Zeilen beim Resize vollständig auf die neue Breite migrieren. Ergänzend Resize-plus-Scrollback-Auswahl in beide Breitenrichtungen testen.

3. **Mittel – Zellverschiebungen vergeben keine neue Schreibversion für das Ziel.**  
   `CopyCell` übernimmt in `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs:645-649` die Quellversion. Damit erhalten Zielzellen bei Insert/Delete-Zeichen und bei Zeilenverschiebungen keine neue Version, obwohl sie mutiert wurden. Das verletzt die in U-01 festgelegte Invariante „jede Mutation einer Zelle … vergibt eine neue Zellversion“ und lässt insbesondere unveränderte Default-Zellen mit Version `0` trotz Verschiebung gültig erscheinen.  
   **Korrektur:** Für echte Zielzellen-Mutationen eine neue Version vergeben. Reine, als solche modellierte Verschiebungen einer ganzen logischen Zeile müssen dagegen ihre Zeilenidentität und passend die Zellidentitäten erhalten, damit die geforderte Scrollback-Nachführung funktioniert.

4. **Mittel – Der Feature-Nachweis fehlt vollständig.**  
   Die Implementierung enthält keine neuen Unit-/Control- oder E2E-Tests. Insbesondere fehlen die im Plan verpflichtenden registrierten Szenarien für Live- und Replay-Auswahl einschließlich Clipboard, sichtbarer Hervorhebung, Tastaturauswahl und Scrollback-Erhalt. Dadurch sind die kritischen Interaktions- und Fehlerpfade nicht regressionsgesichert.

## Prüfergebnisse

- `git diff --check`: ohne Whitespace-Befunde (nur Git-Hinweise zur CRLF-Normalisierung).
- `dotnet build --no-restore`: nicht erfolgreich ausführbar, weil der Sandboxzugriff auf `C:\\Users\\Martin\\AppData\\Local\\Microsoft SDKs` verweigert wird (`MSB4184` in App- und Testprojekt). Die nicht betroffenen Projekte wurden gebaut.

## Ergebnis

Vor der Abnahme müssen die Befunde behoben und die geplanten Tests, einschließlich beider verpflichtenden E2E-Szenarien, erfolgreich ausgeführt werden.
