# Plan-Review: Texte aus CLI-Ausgaben kopieren

## Status: Offene Aufgaben vorhanden

Die Kernimplementierung in `TerminalControl` und die Buffer-Identitäten decken wesentliche Teile des Plans ab. Der aktuelle Nachweisstand erfüllt die verbindlichen Abnahmekriterien jedoch nicht. E-01 ist zwar in den Quellen aufgerufen, führt die geplante Auswahl-/Kopierabnahme aber nicht aus. E-02 ist registriert, enthält jedoch eine fehlerhafte Fixture und prüft die sichtbare Markierung nicht. Für dieses Review wurden keine Tests ausgeführt.

## Umgesetzte Planelemente

- `TerminalControl` enthält Zell-Auswahl per Maus und Tastatur, Auswahl-Overlay, Snapshot-basierte Extraktion sowie `Ctrl+Shift+C`. `Ctrl+C` wird nicht zum Kopieren verwendet; bei `Ctrl+Shift+C` ohne Auswahl wird die Taste nicht an den Prozess gesendet.
- Auswahlvalidierung bindet Zeilen und Zellen an IDs, Versionen und Buffer-Generation. Unit-Tests decken ausgewählte Tastaturpfade, Kopieren mit/ohne Auswahl sowie den Erhalt einer Auswahl bei normalem Scrollen ab.
- Die E2E-Aufrufe sind vorhanden: E-01 wird über den ConPTY-Lifecycle aufgerufen; E-02 steht in `MainTest.RunGeneralTests`.

## Offene Aufgaben

1. **E-01 ist kein Nachweis für die geplante Live-Auswahl und das Kopieren.** `TerminalText_LiveMarkCopyAndKeepSelection` in `E2E_ConPtyLifecycle.cs` tippt lediglich einen Echo-Befehl, wartet auf dessen Marker im Ausgabeprotokoll und prüft `HasTerminalOutput()` sowie `IsCliRunning()`. Die Methode markiert keinen Text, prüft keine Pixel, löst keine weitere Ausgabe nach der Auswahl aus und scrollt den Buffer nicht. Sie sendet auch kein `Ctrl+Shift+C` und liest die Zwischenablage nicht aus. Damit sind sämtliche wesentlichen E-01-Abnahmepunkte unbelegt. Der Aufruf innerhalb der registrierten ConPTY-Phase ändert daran nichts.
2. **E-02-Fixture erzeugt nicht die zwei geplanten Terminalzeilen.** In `E2E_KonsolenTestfenster.cs` wird `Encoding.UTF8.GetBytes("erste Zeile\\r\\nzweite Zeile")` verwendet. Die doppelten Backslashes kodieren wörtliche `\r\n`-Zeichen statt CR/LF-Steuerzeichen; der Terminalbuffer erhält daher keine zwei getrennten Zeilen wie im erwarteten Clipboardwert. Der erwartete Vergleich `erste Zeile{Environment.NewLine}zweite Zeile` belegt so nicht den geplanten mehrzeiligen Auswahlfluss und dürfte mit dieser Fixture fehlschlagen.
3. **E-02 prüft die sichtbare Markierung nicht.** Zwar führt der Test Mausdrag am `ReplayTerminal` aus und vergleicht danach den Clipboardinhalt; ein Vorher-/Nachher-Screenshot oder Pixelvergleich im Auswahlrechteck fehlt. Der Plan fordert diesen sichtbaren Nachweis ausdrücklich. Der Tastaturpfad wird ebenfalls ausgeführt, sein Ergebnis kann wegen der fehlerhaften Fixture aber nicht den geplanten Zwei-Zeilen-Fluss belegen.
4. **Die geplante Testabdeckung ist nur teilweise vorhanden.** Es gibt einige Tests für Kopieren mit/ohne Auswahl und Scroll-Erhalt. In den geprüften Testdateien fehlen weiterhin gezielte Fälle für Extraktionsgrenzen und Leerzeichen-/Zeilenumbruchregeln, Mauskoordinaten mit Scrolloffsets, sichtbares Overlay, Invalidierung durch Mutation/Erase/Abwurf/Reset/Resize/Screen-/Sessionwechsel sowie einen kontrolliert ausgelösten Clipboard-Schreibfehler. Die vollständigen Tastaturregressionen `Ctrl+C` und `Ctrl+V` sind durch den vorhandenen Bestand teilweise abgedeckt, ersetzen diese Auswahltests aber nicht.
5. **Der vorhandene Testbericht ist für den aktuellen Quellstand veraltet.** `test-results.md` behauptet, E-01 und E-02 seien nicht vorhanden/registriert. In den aktuellen Quellen existieren beide Methoden und ihre Aufrufe. Der Bericht enthält daher keinen gültigen Ausführungsnachweis für den jetzigen Stand; E-01/E-02 müssen nach Korrektur ausgeführt und die Ergebnisse aktualisiert dokumentiert werden.

## E2E-Nachweisstatus

| Szenario | Im aktuellen Code | Plan-Nachweisstatus |
|---|---|---|
| E-01 `TerminalText_LiveMarkCopyAndKeepSelection` | In der ConPTY-Lifecycle-Phase aufgerufen, aber nur Marker-Ausgabe und Prozessstatus geprüft. | **Nicht bestanden / nicht nachgewiesen:** Auswahl, UI-Hervorhebung, Erhalt nach Ausgabe, Scrollback, Tastaturauswahl und Clipboardvergleich fehlen. |
| E-02 `ReplayText_MarkAndCopy` | In `RunGeneralTests` aufgerufen; Maus-/Tastaturaktionen und Clipboardvergleich sind enthalten. | **Nicht bestanden / nicht nachgewiesen:** Fixture nutzt wörtliche `\\r\\n` statt Zeilenumbrüchen; Pixel-Hervorhebung wird nicht geprüft. Der aktuelle Testlauf ist zudem nicht belegt. |

Der frühere Eintrag in `test-results.md`, ein Filterlauf habe keinen der beiden Tests gefunden, ist wegen der inzwischen vorhandenen und registrierten Methoden überholt. Ein erfolgreicher Lauf der aktuellen E2E-Szenarien liegt in den geprüften Artefakten nicht vor. Ein erfolgreicher Unit-/Control-Testlauf kann die verbindlichen E2E-Abnahmen laut Plan nicht ersetzen.

## Erforderliche Nacharbeiten

- E-01 um den tatsächlichen Live-UI-Ablauf ergänzen: kontrollierte Ausgabe, Maus- und Tastaturauswahl, sichtbare Pixelprüfung, Erhalt nach weiterer Ausgabe und Scrollback-Verschiebung sowie exakter STA-Clipboardvergleich.
- E-02 mit echten CR/LF-Zeilen erstellen, die sichtbare Auswahl per Vorher-/Nachher-Pixelvergleich nachweisen und beide Clipboardpfade am tatsächlichen Replay-Control bestätigen.
- Fehlende geplante Control-/Buffer-Tests ergänzen und danach E-01 sowie E-02 tatsächlich ausführen. `test-results.md` auf den aktuellen Stand bringen; fehlende oder fehlgeschlagene Abnahmen als solche dokumentieren.
