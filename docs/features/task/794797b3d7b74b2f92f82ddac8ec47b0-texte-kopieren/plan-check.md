# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Text aus der angezeigten CLI-Ausgabe kann mit Maus oder Tastatur markiert werden. | U-02 bis U-05 beschreiben Auswahlmodell, Zellkoordinaten, Mausdrag, sichtbares Overlay und Erweiterung mit Shift-Navigation. Scrollback und beide Scrolloffsets sind berücksichtigt. | Buffer-/Control-Tests für Auswahlrichtungen, Zellgrenzen, Scrolloffsets und Tastaturerweiterung; E-01 und E-02 prüfen Maus- und Tastaturauswahl am tatsächlichen Terminal-Control sowie sichtbare Hervorhebung durch Pixelvergleich. | Abgedeckt |
| Eine Markierung kann in die Zwischenablage kopiert werden. | U-05 bindet `Ctrl+Shift+C` an Snapshot-basierte Textextraktion und Clipboard-Schreiben mit kontrollierter Fehlerbehandlung. | E-01 und E-02 lesen die echte Windows-Zwischenablage auf STA und vergleichen den Inhalt exakt; zusätzliche Control-Tests prüfen Kopieren ohne Auswahl und Clipboard-Schreibfehler. | Abgedeckt |
| Kopierter Inhalt entspricht ausgewähltem Text und erhält sinnvolle Zeilenumbrüche. | U-02 normalisiert die Grenzen in Leserichtung und extrahiert konsistente Snapshot-Zellen. `Environment.NewLine`, interne Leerzeichen, leere Zwischenzeilen und Entfernen terminalbreiter Endleerzeichen sind festgelegt. | Konkrete Extraktionstests für Vorwärts-/Rückwärtsauswahl, Teilzeilen, mehrere Zeilen, Leerzeilen und Leerzeichenregeln; beide E2E-Szenarien vergleichen mehrzeiligen Clipboardtext einschließlich Zeilenumbrüchen. | Abgedeckt |
| Auswahl und Kopieren funktionieren während der Anzeige einer CLI-Ausgabe. | U-01 und U-06 schaffen stabile Zeilen- und Zellidentität sowie Generationen für die Auswahl während fortlaufender Ausgabe. Live- und Replay-Session verwenden dasselbe Control. | E-01 löst nach der Auswahl weitere kontrollierte Live-Ausgabe aus, prüft Erhalt der sichtbaren Markierung und kopiert den ursprünglichen Inhalt; E-02 nimmt den Replay-Fluss verbindlich ab. | Abgedeckt |
| Bestätigte Entscheidung: `Ctrl+Shift+C` kopiert, `Ctrl+C` bleibt CLI-Eingabe/Abbruchsignal. | Produktentscheidungen und U-05 legen beide Tastenkombinationen sowie das Verhalten ohne Auswahl fest. | Bestehende Encoder-/KeyInput-Tests und gezielte Regressionen prüfen `Ctrl+C`, fehlende Prozess-Eingabe beim Kopieren und unverändertes Clipboard ohne Auswahl. `Ctrl+V` bleibt Bestandteil der Bestandsprüfungen. | Abgedeckt |
| Bestätigte Entscheidung: Auswahl bleibt bei normaler neuer Ausgabe erhalten. | U-01 bis U-02 und U-06 binden Auswahl an stabile Zeilen-IDs und Zell-Schreibversionen. Scrollback-Verschiebung führt Koordinaten nach; Mutation, Abwurf, Resize, Reset sowie Screen-/Sessionwechsel verwerfen die Auswahl deterministisch. | Buffer-/Control-Tests decken Erhalt, Nachführung und sämtliche genannten Invalidierungen einschließlich identischen Neuschreibens ab. E-01 prüft Erhalt und erneutes Kopieren nach normaler Ausgabe sowie nach deterministischer Scrollback-Verschiebung. | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Im Aufgabenfenster mehrzeilig mit Maus markieren, sichtbare Markierung erkennen und exakt kopieren. | E-01 `TerminalText_LiveMarkCopyAndKeepSelection`: kontrollierte Live-Ausgabe zweier unterscheidbarer Zeilen, Mausdrag, Pixelnachweis und exakter STA-Clipboardvergleich. | Abgedeckt |
| Im Aufgabenfenster mit Tastatur auswählen und kopieren. | Zusätzlicher Durchlauf in E-01 mit dokumentiertem, reproduzierbarem Fokus-/Caret-Startzustand und exaktem Clipboardvergleich. | Abgedeckt |
| Auswahl während weiterer Live-Ausgabe erhalten und anschließend den ursprünglichen Inhalt kopieren. | E-01 löst neue Ausgabe außerhalb der Auswahl aus und prüft danach Markierung und Clipboardinhalt. | Abgedeckt |
| Nach Scrollback-Verschiebung zur Auswahl zurückscrollen und denselben Inhalt erneut kopieren. | E-01 fordert einen deterministischen Scrollback-Fall bei weiterhin verfügbaren ausgewählten Zeilen, Rückscrollen, sichtbare Markierung und erneuten exakten Clipboardvergleich. | Abgedeckt |
| Im Konsolentestfenster Replay-Text mit Maus und Tastatur auswählen und kopieren. | E-02 `ReplayText_MarkAndCopy`: synthetische `.clireplay`-Fixture über Einstellungen → Allgemein laden und abspielen, beide Auswahlarten, sichtbare Markierung und exakter Clipboardvergleich ohne Live-Prozess-Eingabe. | Abgedeckt |
| Berechtigungen, Datenfilter oder neue Navigationsregeln. | Keine Änderungen dieser Art vorgesehen. | Nicht erforderlich mit Begründung: Das Feature betrifft die Interaktion mit bereits angezeigtem Terminalinhalt; der vorhandene Einstieg in das Replay-Fenster wird in E-02 verwendet. |

## Fehlende oder unvollständige Planbestandteile

## Hinweise

Die Befunde T-01 bis T-04 und P-01 aus `plan-check.1.md` sind durch die Nachplanung abgedeckt: Beide E2E-Szenarien sind verbindlich, fortlaufende Ausgabe und Scrollback sind Teil des Live-Nachweises, negative Kopier- und Lifecycle-Fälle sind konkret benannt, und Testhelfer, Fixture, Runnerregistrierung sowie STA-/Desktop-Voraussetzungen sind festgelegt. U-01 stellt die zuvor fehlenden Identitäts- und Generationsdaten als Voraussetzung der Auswahlbindung bereit.

Die E2E-Tests sind laut Plan primärer Funktionsnachweis. Fehlender Desktop-/Clipboardzugriff oder nicht ausgeführte Szenarien dürfen beim späteren Testlauf nicht als bestandene Abnahme gewertet werden. Die vorhandenen Control-, Eingabe-, Clipboard- und Encoder-Testgruppen sind ausdrücklich als Bestandsprüfungen aufgenommen.

Die offenen Fragen in `requirement.md` und der Bestandsaufnahme sind historisch; die bestätigten Produktentscheidungen in `plan.md` beantworten sie. Es ist keine weitere Produktentscheidung erforderlich.

Geprüft wurden die vollständige Anforderung, die Bestandsaufnahme einschließlich aller drei verlinkten Detaildokumente, der aktualisierte Plan und die vorherige Gegenprüfung. Diese Gegenprüfung bewertet ausschließlich die Planvollständigkeit; sie enthält keine Aussage über einen Implementierungsstand oder ausgeführte Tests.
