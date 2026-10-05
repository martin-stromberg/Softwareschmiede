# Plan-Review: Texte aus CLI-Ausgaben kopieren

## Status: Offene Aufgaben vorhanden

Die Kernimplementierung erfüllt wesentliche Planbestandteile: `TerminalControl` bietet zellbasierte Maus- und Tastaturauswahl, sichtbares Overlay, Snapshot-basierte Textextraktion und `Ctrl+Shift+C`; `Ctrl+C` bleibt unverändert dem Terminaleingabepfad vorbehalten. Zeilen-IDs, Zellversionen und Buffer-Generationen stützen die Auswahl-Lifecycle-Regeln.

Der verbindliche Funktionsnachweis ist dennoch nicht vollständig. Insbesondere E-01 testet den vorgesehenen Live-Auswahl- und Kopierablauf nicht. E-02 verwendet inzwischen korrekte echte CR/LF-Zeilen, weist die sichtbare Auswahl aber weiterhin nicht nach. Die Pflicht-E2E-Szenarien wurden für den aktuellen Stand nicht erfolgreich ausgeführt.

Für dieses Review wurden keine Tests ausgeführt.

## Umgesetzte Planelemente

- `TerminalControl` unterstützt Mausdrag sowie `Shift`+Pfeile und `Shift`+`Home`/`End`, zeichnet die Auswahl über den Terminalzellen und extrahiert sie aus einem konsistenten Snapshot.
- `Ctrl+Shift+C` kopiert nur bei Auswahl und schreibt dann keine Terminal-Input-Bytes; ohne Auswahl bleiben Clipboard und CLI-Eingabe unverändert. Der vorhandene `Ctrl+C`-Eingabepfad bleibt bestehen.
- Auswahlgrenzen enthalten Zeilen-IDs, Zellversionen und Generation. Änderungen an ausgewählten Zellen, verlorene Zeilen, Reset, Resize und Screenwechsel invalidieren sie; normales Scrollen kann die logische Auswahl erhalten.
- E-02 ist in `RunGeneralTests` registriert und erzeugt nun zwei echte Terminalzeilen mit `"erste Zeile\r\nzweite Zeile"`. Maus- und Tastaturauswahl vergleichen den Clipboardinhalt.

## Offene Aufgaben

1. **E-01 weist den Live-Benutzerfluss nicht nach.** `TerminalText_LiveMarkCopyAndKeepSelection` in `E2E_ConPtyLifecycle.cs` gibt nur einen eindeutigen Marker aus und prüft Terminalausgabe sowie Prozessstatus. Es fehlen Mausauswahl, sichtbare Hervorhebung, `Ctrl+Shift+C`, STA-Clipboardvergleich, Erhalt nach weiterer Ausgabe, Scrollback-Verschiebung mit Rückscrollen und der geforderte Tastaturauswahl-Durchlauf. Der Kommentar, die detaillierten Prüfungen lägen bewusst bei Replay, widerspricht der verbindlichen E-01-Abnahme im Plan.
2. **E-02 prüft das Auswahl-Overlay nicht.** `ReplayText_MarkAndCopy` führt Drag, Tastaturauswahl und Clipboardvergleiche aus, nimmt aber keinen Vorher-/Nachher-Screenshot auf und vergleicht keine Pixel im erwarteten Zellenrechteck. Damit fehlt der planmäßig verbindliche sichtbare Nachweis.
3. **Geplante Control-/Buffer-Testabdeckung fehlt weitgehend.** Es fehlen gezielte Tests für Vorwärts-/Rückwärts-Extraktion, Teilzeilen, Leerzeilen und Leerzeichen-/Zeilenumbruchregeln, Mauskoordinaten mit horizontalem und vertikalem Offset, Overlay-Clipping sowie Invalidierung durch Überschreiben, Erase, Scrollback-Abwurf, Reset, Resize, Alternate-Screen- und Sessionwechsel. Ebenso fehlt ein deterministischer Clipboard-Schreibfehler-Test.
4. **Es liegt kein erfolgreicher aktueller E2E-Nachweis vor.** `test-results.md` dokumentiert einen früheren Lauf und nennt noch die inzwischen korrigierte `\\r\\n`-Fixture als Fehler. Die nachträglichen Änderungen an E-02 und am normalen Scrollback-Test sind dort nicht abgenommen. E-01 und E-02 müssen seriell mit Desktop-, Maus-, Tastatur- und Clipboardzugriff erfolgreich durchlaufen; nicht ausgeführte Szenarien gelten gemäß Plan nicht als grün.

## E2E-Nachweisstatus

| Szenario | Aktueller Code | Plan-Nachweisstatus |
|---|---|---|
| E-01 `TerminalText_LiveMarkCopyAndKeepSelection` | Registrierte ConPTY-Phase, aber nur Marker-Ausgabe und Prozessstatus. | **Nicht bestanden / nicht nachgewiesen:** Auswahl, Overlay, Erhalt, Scrollback, Tastaturauswahl und Clipboardvergleich fehlen. |
| E-02 `ReplayText_MarkAndCopy` | Registriert; echte zwei Zeilen sowie Maus-/Tastaturauswahl und Clipboardvergleiche vorhanden. | **Nicht vollständig nachgewiesen:** Pixel-/Overlayvergleich fehlt; kein erfolgreicher aktueller E2E-Lauf dokumentiert. |

## Erforderliche Nacharbeiten

- E-01 als echten Live-UI-Test vollständig umsetzen und ausführen.
- E-02 um Screenshot-/Pixelprüfung der sichtbaren Auswahl ergänzen und ausführen.
- Die fehlenden Buffer-/Control-Tests aus dem Plan ergänzen, insbesondere alle Invalidierungsfälle und den Clipboard-Fehlerpfad.
- Die betroffenen Tests sowie beide Pflicht-E2E seriell erfolgreich ausführen und `test-results.md` auf den tatsächlichen Quellstand aktualisieren.
