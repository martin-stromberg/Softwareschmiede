# Code-Review: Texte kopieren

## Status: Befunde vorhanden

Geprüft wurden die Feature-Änderungen einschließlich der aktuellen, noch nicht
committeten Korrekturen. Der Auswahlzustand wird jetzt nicht mehr an die bei
jeder Ausgabe fortgeschriebene Buffer-Generation gebunden: stabile Zeilen-IDs
und Zellversionen erhalten eine Auswahl bei Ausgabe außerhalb der Auswahl und
bei normalem Scrollback-Scrollen. E-02 enthält nun den geforderten
Vorher-/Nachher-Pixelvergleich für Maus- und Tastaturauswahl.

`RaiseUiActionRequested` kommt im Produktquellcode unter `src/` nicht vor.
Es gibt deshalb keine ausgelöste UI-Aktion, für die ein Handler in einer
zugehörigen Blazor-Seite oder -Komponente fehlen könnte.

## Befunde

1. **Hoch – E-01 weist den verbindlichen Live-Fluss weiterhin nicht nach.**
   `TerminalText_LiveMarkCopyAndKeepSelection` in
   `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs` erzeugt nur einen
   Ausgabemarker und prüft sichtbare Ausgabe sowie den laufenden Prozess. Es
   führt keine Maus- oder Tastaturauswahl am Live-Terminal aus, vergleicht
   keine Vorher-/Nachher-Pixel, löst keine neue Ausgabe nach der Auswahl aus,
   prüft keinen Scrollback-Fall und kontrolliert weder `Ctrl+Shift+C` noch den
   Clipboardinhalt. Damit fehlt der im Plan als Pflichtnachweis definierte
   E-01-Ende-zu-Ende-Fluss vollständig.

2. **Mittel – Die verbindliche Control-/Buffer-Testmatrix ist weiterhin nur
   teilweise umgesetzt.** Es fehlen gezielte Tests für Vorwärts- und
   Rückwärtsauswahl, Teilzeilen, Leerzeilen und Endleerzeichen sowie für
   Mauskoordinaten mit horizontalem und vertikalem Offset und Overlay-Clipping.
   Ebenso fehlen die vorgesehenen Invalidierungstests für Überschreiben,
   Erase, Scrollback-Abwurf, Reset, Resize, Alternate-Screen- und
   Sessionwechsel sowie ein deterministisch ausgelöster Clipboard-Schreibfehler.
   Die vorhandenen Tests decken `Ctrl+Shift+C`, Cursorbegrenzung sowie Erhalt
   bei normaler Ausgabe und Scrollback-Verschiebung ab, ersetzen aber diese
   Fälle nicht.

3. **Mittel – E-02 besitzt noch keinen erfolgreichen Ausführungsnachweis.**
   Der Replay-Test ist in `RunGeneralTests` registriert und prüft jetzt
   Maus-/Tastaturpfad, Pixelabweichung und Clipboardtext. Laut
   `test-results.md` liegt aber kein erfolgreicher aktueller E2E-Lauf vor.
   Die Abnahme darf erst als bestanden gelten, wenn der echte Desktop- und
   Clipboardlauf erfolgreich dokumentiert ist.

## Positiv geprüft

- `Ctrl+Shift+C` wird nur bei Auswahl lokal behandelt; ohne Auswahl bleibt die
  Zwischenablage unverändert und es gehen keine Bytes an den CLI-Input.
- `Ctrl+C` durchläuft weiterhin den bestehenden VT100-Eingabepfad.
- Der Cursor hinter der letzten Spalte wird vor dem Zugriff auf
  Zellversionsdaten auf eine valide Spalte begrenzt.
- Die normale Scrollback-Verschiebung überträgt bei vollständigem
  Hauptscreen-Scrollen Zeilen-ID und Zellversionen, sodass eine Auswahl am
  ursprünglichen Inhalt gebunden bleibt.
- E-02 verwendet echte CR/LF-Zeichen und vergleicht für Maus- und
  Tastaturauswahl Screenshot-Pixel im gewählten Zellbereich.
- `git diff --check` liefert keine Whitespace-Fehler (nur bekannte
  CRLF-Normalisierungshinweise).

## Ergebnis

Die aktuellen Korrekturen schließen den zuvor gemeldeten E-02-Pixelnachweis
und den Erhalt bei normaler Ausgabe fachlich. Für den Abschluss müssen E-01
vollständig umgesetzt, die fehlende Testmatrix ergänzt und beide E2E-Nachweise
erfolgreich ausgeführt sowie dokumentiert werden.
