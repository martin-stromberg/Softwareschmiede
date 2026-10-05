# Plan-Review: Texte aus CLI-Ausgaben kopieren

## Status: Offene Aufgaben vorhanden

Die Implementierung deckt Auswahl, Kopieren und Buffer-Identitäten in wesentlichen Teilen ab. Der aktuelle Code erfüllt den Plan noch nicht vollständig. Es wurden für dieses Review keine Tests ausgeführt; der E2E-Status wurde anhand der Testquellen und des vorhandenen Testberichts geprüft.

## Umgesetzte Planelemente

- `TerminalBuffer`/Snapshot enthalten Zeilen-IDs, Zellversionen und eine Generation. Zellkopien erhalten jetzt eine neue Zielversion; der frühere Befund zu `CopyCell` ist damit behoben.
- `TerminalControl` enthält Maus- und Tastaturauswahl, ein Auswahl-Overlay und Extraktion aus einem Buffer-Snapshot. `Ctrl+Shift+C` kopiert bei Auswahl; ohne Auswahl wird nichts an die CLI gesendet. Der Kontextmenübefehl „Kopieren“ ist ebenfalls vorhanden.
- Die hinzugefügten Control-Tests prüfen `Ctrl+Shift+C` mit und ohne Auswahl sowie einzelne Tastatur-Auswahlfälle. Buffer-Tests prüfen unter anderem die Snapshot-Geometrie nach Resize.

## Offene Aufgaben

1. **Pflicht-E2E E-01 fehlt und ist nicht bestanden:** `TerminalText_LiveMarkCopyAndKeepSelection` ist weder als E2E-Szenario vorhanden noch in `MainTest.RunConPtyTests` registriert. Damit sind Live-Auswahl, sichtbare Pixelhervorhebung, Erhalt bei normaler Ausgabe, Scrollback-Verschiebung, Tastaturauswahl und Clipboardinhalt im tatsächlichen Aufgabenfenster nicht nachgewiesen. Status: **nicht vorhanden / nicht ausgeführt / nicht bestanden**.
2. **Pflicht-E2E E-02 fehlt und ist nicht bestanden:** `ReplayText_MarkAndCopy` ist weder vorhanden noch in `MainTest.RunGeneralTests` registriert. Das dort aufgerufene allgemeine Replay-Szenario lädt und spielt eine Aufzeichnung ab, prüft aber weder Auswahl noch Markierungs-Pixel, Clipboardinhalt oder Tastaturkopie. Status: **nicht vorhanden / nicht ausgeführt / nicht bestanden**.
3. **Geforderte Testabdeckung ist weiterhin lückenhaft:** Die Änderungen ergänzen einige Tastatur- und Snapshot-Tests, aber keine ausreichende Abdeckung der im Plan geforderten Auswahl-Extraktion (Grenzen, Zeilenumbrüche, Leerzeichen), Maus-/Scrolloffset-Koordinaten, sichtbaren Markierung, Lifecycle-Erhaltung und -Invalidierung (Mutation, Erase, Zeilenabwurf, Reset, Resize, Screen-/Sessionwechsel), Clipboard-Schreibfehler sowie vollständiger `Ctrl+C`-/`Ctrl+V`-Regression. Die vorhandenen Feature-Tests decken diese Abnahmepunkte daher nicht ab.

## E2E-Nachweis

Der vorhandene Testbericht dokumentiert beide Pflichtszenarien als fehlend/nicht ausgeführt. Unit- oder Control-Tests und das bestehende Replay-Lade-/Abspiel-Szenario ersetzen E-01 und E-02 laut Plan nicht. Beide müssen implementiert, in die jeweilige `MainTest`-Suite aufgenommen und erfolgreich ausgeführt werden; fehlender Desktop-/Clipboardzugriff wäre als fehlgeschlagener Nachweis festzuhalten.

## Hinweise

- Der Befund zu neu vergebenen Zellversionen in `CopyCell` aus dem vorherigen Review ist im aktuellen Code behoben.
- Vor Abschluss sind die drei oben genannten offenen Aufgaben zu bearbeiten. Insbesondere sind E-01 und E-02 verpflichtende Abnahmen und können nicht durch andere Tests kompensiert werden.
