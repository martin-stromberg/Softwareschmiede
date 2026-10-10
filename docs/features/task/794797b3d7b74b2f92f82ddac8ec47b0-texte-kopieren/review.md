# Plan-Review: Texte aus CLI-Ausgaben kopieren

## Status: Offene Aufgaben vorhanden

Die Produktimplementierung deckt die Kernanforderung ab: zellbasierte Auswahl per Maus und Tastatur, sichtbares Overlay, `Ctrl+Shift+C` sowie der unveränderte `Ctrl+C`-Eingabepfad sind vorhanden. Stabile Zeilen-IDs und Zellversionen halten eine unveränderte Auswahl bei normaler Ausgabe und Scrollback fest. Der gezielte Live-Maustest E-01 wurde erfolgreich ausgeführt.

Der Plan verlangt jedoch ausdrücklich echte E2E-Nachweise für die Tastaturauswahl in Live- und Replay-Terminal. Diese sind noch nicht erbracht: Die getrennten E2E-Tests belegen Mausauswahl und Kopier-Shortcut, aber keine per `Shift`+Navigation erzeugte Auswahl. Der WPF-Test mit konstruierten `KeyEventArgs` ist ein wertvoller Integrationstest, ersetzt die verpflichtende E2E-Abnahme aber nicht. Darüber hinaus fehlen noch Teile der verbindlichen Control-/Buffer-Testmatrix.

Für diesen Review wurden keine Tests ausgeführt; die Aussagen zu erfolgreichen Läufen folgen dem aktuellen `test-results.md` und `continue.md`.

## Erfüllte Planpunkte

- `TerminalControl` enthält zellbasierte Maus- und Tastaturauswahl, rendert ein Overlay und extrahiert Text aus dem Buffer-Snapshot. `Ctrl+Shift+C` kopiert; der bestehende `Ctrl+C`-Eingabepfad bleibt erhalten.
- Die Auswahl wird über stabile Zeilen-IDs und Zellversionen validiert. `OnBufferChanged` validiert die Auswahl vor dem Rendern.
- `TerminalControlTests.KeyInput.cs` enthält `Selection_NewOutputOutsideSelection_KeepsSelectionAndCopyText` und `Selection_NormalScroll_KeepsLogicalRowAndCopyText`. Beide prüfen, dass Auswahltext bei Ausgabe außerhalb der Auswahl beziehungsweise logischer Zeilenverschiebung erhalten bleibt.
- Der isolierte E-01-Test erzeugt eine Live-Ausgabe, markiert den sichtbaren Marker mit einem echten Mausdrag, weist das Overlay per Pixelvergleich nach und prüft Kopieren sowie Erhalt nach neuer Ausgabe und Scrollback-Ausgabe.
- E-02 ist in zwei frische, gezielt ausführbare Szenarien aufgeteilt. Der Mauspfad weist eine mehrzeilige Replay-Auswahl, sichtbares Overlay und den exakten Clipboardtext nach; der Shortcutpfad prüft Fokus und `Ctrl+Shift+C` auf einer vorhandenen Auswahl.
- Die UIA-Viewportinformation ist als schreibgeschütztes `AutomationProperties.ItemStatus` umgesetzt. Sie enthält keinen Terminalinhalt und erlaubt dem Live-E2E-Test eine zellgenaue Mauspositionierung ohne OCR oder zeilenabhängige Screenshot-Suche.

## Offene Aufgaben

1. **Tastaturauswahl ist nicht E2E-abgenommen.** `ReplayText_CopyShortcutWithFocusedSelection_E2E` erzeugt die Auswahl weiterhin mit `MarkiereReplayZellen` (Maus) und sendet nur den Kopier-Shortcut. `TerminalText_LiveMarkCopyAndKeepSelection_E2E` enthält gar keinen Tastaturdurchlauf. Damit fehlen die in E-01 und E-02 verbindlich geforderten echten `Shift`+Pfeil-/`Home`-/`End`-Abläufe inklusive sichtbarer Auswahl und Clipboardvergleich. Der WPF-Test `OnPreviewKeyDown_MausankerDannShiftRechts_ZeichnetAuswahlUndKopiertText` belegt die Produktlogik, ist aber wegen direkt konstruierter Ereignisdaten kein Ersatz für diese E2E-Anforderung.
2. **E-01 deckt den vollständigen Live-Erhaltspfad noch nicht sichtbar ab.** Der Test markiert einen einzelnen Marker statt der im Plan verlangten zwei kontrollierten Zeilen. Nach weiterer Ausgabe und nach Scrollback wird nur erneut kopiert; der Test scrollt nicht zur Auswahl zurück und vergleicht weder deren sichtbares Overlay noch den ursprünglichen Ausschnitt. Ergänzt werden müssen: mehrzeilige Auswahl, Pixelnachweis nach normaler Ausgabe, Rückscrollen zur verschobenen Auswahl und erneuter sichtbarer Pixelnachweis.
3. **Die Control-/Buffer-Testmatrix aus dem Plan ist unvollständig.** Weiterhin fehlen gezielte Tests für Vorwärts-/Rückwärts- und Teilzeilenextraktion, Leerzeilen sowie Endleerzeichen/Zeilenumbrüche, Mauskoordinaten mit horizontalem und vertikalem Offset, Overlay-Clipping und die Invalidierung nach Überschreiben, Erase mit identischem Neuschreiben, Scrollback-Abwurf, Reset, Resize, Alternate-Screen- und Sessionwechsel. Ein kontrollierter Schreibfehler beim Kopieren ist ebenfalls nicht nachgewiesen.
4. **Die neue UIA-Geometrie ist ein produktiver, impliziter Testvertrag und muss stabilisiert werden.** Die Wahl von `ItemStatus` ist für den E2E-Use-Case vertretbar, weil sie standardisiert, schreibgeschützt und inhaltsfrei ist. Das semikolongetrennte Format samt physischer Pixelmaße wird jedoch direkt vom Testparser als Protokoll konsumiert. Es braucht deshalb einen eng begrenzten Contract-Test für Format, DPI-Bezug und Aktualisierung bei Scroll/Resize. Zudem wird `FirstColumn` zwar veröffentlicht, von `LiveTerminalGeometry.CellCenter` aber nicht einbezogen; ein horizontal gescrollter Viewport würde daher an der falschen Zelle ziehen. Der Geometriehelfer muss relative sichtbare Spalten von logischen Spalten klar trennen und mit horizontalem Offset getestet werden.
5. **Der vollständige, aktuelle E2E-Nachweis fehlt noch in `test-results.md`.** Die Datei nennt E-02 noch als abgebrochen, während `continue.md` bereits getrennte grüne Nachweise behauptet. Nach Abschluss des Tastaturfalls müssen die drei gezielten E2E-Szenarien seriell ausgeführt und mit ihren tatsächlichen Ergebnissen dokumentiert werden.

## E2E-Nachweisstatus

| Szenario | Implementierungsstand | Nachweis |
|---|---|---|
| E-01 Live-Mausauswahl | Isoliert erfolgreich: Drag, Overlay, Clipboard, Erhalt per erneutem Kopieren. | Teilweise erfüllt; mehrzeiliger, sichtbarer Erhalt- und Tastaturpfad fehlen. |
| E-02 Replay-Mausauswahl | Getrennt implementiert und laut `continue.md` erfolgreich: Pixelvergleich und exakter Mehrzeilen-Clipboardtext. | Erfüllt für Maus. |
| E-02 Replay-Tastaturauswahl | E2E prüft Fokus und Kopier-Shortcut auf einer zuvor per Maus gesetzten Auswahl; WPF-Test prüft `Shift`+Rechts. | Nicht erfüllt: keine echte E2E-Tastaturauswahl. |

## Erforderliche Nacharbeiten

- Einen verlässlichen E2E-Eingabepfad für `Shift`+Navigation herstellen oder den Plan nach ausdrücklicher Produktentscheidung anpassen; anschließend Live- und Replay-Tastaturauswahl mit Overlay- und Clipboardnachweis ausführen.
- E-01 um den mehrzeiligen, nach Ausgabe und Rückscrollen weiterhin sichtbaren Auswahlablauf ergänzen.
- Die ausstehende Control-/Buffer-Testmatrix und den UIA-Geometrie-Contract einschließlich horizontalem Offset ergänzen.
- Alle gezielten Pflicht-E2E seriell ausführen und `test-results.md` konsistent aktualisieren.
