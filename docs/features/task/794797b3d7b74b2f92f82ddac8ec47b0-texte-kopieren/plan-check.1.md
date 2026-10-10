# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Text aus der angezeigten CLI-Ausgabe kann mit Maus oder Tastatur markiert werden. | U-01 bis U-04: Zellgrenzen, Mausdrag, sichtbare Markierung und Shift-Navigation. | U-06 nennt Richtungen, Koordinaten und Tastaturerweiterung; E-01 fordert Maus- und Tastaturbedienung im Aufgabenfenster. Die Hilfsmittel für den tatsächlichen visuellen Nachweis sind noch zu konkretisieren. | Lücke |
| Eine Markierung kann in die Zwischenablage kopiert werden. | U-04: `Ctrl+Shift+C` bei aktiver Auswahl, Clipboard-Schreiben mit Fehlerbehandlung. | E-01 vergleicht den tatsächlichen Clipboardinhalt. Replay-E2E und negative Kopierfälle sind noch nicht verbindlich abgedeckt. | Lücke |
| Kopierter Inhalt entspricht ausgewähltem Text und erhält sinnvolle Zeilenumbrüche. | U-01: konsistenter Snapshot, normalisierte Grenzen, zeilenweise Extraktion, Entfernen terminalbreiter Leerzeichen. | U-06 prüft vorwärts/rückwärts, eine/mehrere Zeilen, Leerzeilen und Leerzeichen; E-01 vergleicht exakt zwei Zeilen samt `Environment.NewLine`. | Abgedeckt |
| Auswahl und Kopieren funktionieren während der Anzeige einer CLI-Ausgabe. | U-05 behandelt fortlaufende Ausgabe, Scrollback, überschriebenen Inhalt sowie Session- und Alternate-Screen-Wechsel. | E-01 beschreibt nur eine bereits ausgegebene Zweizeilenansicht. Der bestätigte Erhalt bei neuer Ausgabe und das anschließende Kopieren fehlen als konkreter E2E-Ablauf; E-02 bleibt optional. | Lücke |
| Bestätigte Entscheidung: `Ctrl+Shift+C` kopiert, `Ctrl+C` bleibt CLI-Eingabe/Abbruchsignal. | Designentscheidungen und U-04 setzen die Entscheidung um. | U-06 fordert die `Ctrl+C`-Regression; kein konkreter Negativtest für Kopieren ohne Auswahl bzw. unbeabsichtigte CLI-Eingabe beim Kopieren genannt. | Lücke |
| Bestätigte Entscheidung: Auswahl bleibt bei normaler neuer Ausgabe erhalten. | U-05 fordert Erhalt und Koordinatennachführung. | Unit-Abdeckung genannt; belastbare Erkennung von Scrollback-Abwurf/Screenwechsel und der zugehörige UI-Nachweis sind noch zu ergänzen. | Lücke |

## Fehlende oder unvollständige Testanforderungen

- [ ] **T-01 – Replay verbindlich abnehmen:** E-02 einschließlich tatsächlicher Maus-/Tastaturauswahl und `Ctrl+Shift+C` zum Pflichtszenario machen. Replay ist ausdrücklich Teil der geplanten Funktion; eine bedingte Ausführung bei zuverlässigem Clipboardzugriff lässt diesen Benutzerfluss ohne Abnahmenachweis. Fehlender Desktop-/Clipboardzugriff muss als nicht ausgeführte bzw. fehlgeschlagene Abnahme dokumentiert werden und darf kein grünes Ergebnis erzeugen.
- [ ] **T-02 – Fortlaufende Ausgabe im UI prüfen:** E-01 um einen kontrolliert ausgelösten weiteren Ausgabeabschnitt nach der Auswahl erweitern. Sichtbare Markierung und anschließend kopierter ursprünglicher Ausschnitt müssen erhalten bleiben. Mindestens ein deterministischer Scrollback-Fall muss den tatsächlichen Auswahl-/Kopierfluss nach Scrollverschiebung prüfen; genaue Abwurf-/Reset-Randfälle können ergänzend auf Buffer-/Control-Ebene liegen.
- [ ] **T-03 – Negative Kopier- und Lifecycle-Fälle festlegen:** Konkrete Control-/Integrationstests für Kopieren ohne aktive Auswahl (Clipboard bleibt unverändert), Kopieren mit Auswahl ohne ETX/sonstige CLI-Eingabe, abgefangenen Clipboard-Schreibfehler und Auswahlverwerfen bei Session-/Alternate-Screen-Wechsel ergänzen. Die bereits genannten Überschreiben-/Löschen-Tests um den Fall ergänzen, dass dieselben Zeichen an derselben Position neu entstehen: veraltete Auswahl darf dadurch nicht unbemerkt auf neuen Inhalt zeigen. Bestehende `KeyToVt100EncoderTests.KeyEncoding.cs` und die Clipboard-Paste-E2E-Regression als auszuführende Bestandsprüfungen nennen.
- [ ] **T-04 – E2E-Voraussetzungen ausführbar konkretisieren:** Testdatei/Runnerregistrierung, kontrollierten Live-Prozess und Replay-Fixture sowie die Helfer für Terminalposition, Zellkoordinaten, Mausdrag und Tastenkombinationen nennen. Für die sichtbare Markierung eine konkrete Beobachtung festlegen, etwa Vorher-/Nachher-Pixelprüfung am tatsächlichen Terminalausschnitt, da kein UIA-TextPattern vorhanden ist. STA-Clipboardlesen, begrenzte Wiederholungen bei temporärer Belegung und serielle Ausführung der clipboardabhängigen Fälle vorsehen; der vorhandene STA-Clipboardhelfer in `E2E_ConPtyLifecycle.cs` ist ein geeigneter Ausgangspunkt.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Im Aufgabenfenster mehrzeilig mit Maus markieren und exakt kopieren. | E-01 `TerminalText_MarkAndCopy`. | Abgedeckt; Testhilfsmittel gemäß T-04 konkretisieren. |
| Im Aufgabenfenster mit Tastatur auswählen und kopieren. | Zweiter Teil von E-01. | Abgedeckt; Startzustand und Tastensequenz gemäß T-04 konkretisieren. |
| Auswahl während weiterer Live-Ausgabe erhalten und danach denselben Text kopieren. | Kein E2E-Ablauf mit neuer Ausgabe nach der Auswahl. | Lücke – T-02. |
| Nach Scrollverschiebung den ausgewählten Inhalt kopieren. | Nur Koordinaten-/Lifecycle-Tests in U-06. | Lücke – T-02. |
| Im Konsolentestfenster Replay-Text auswählen und kopieren. | E-02 `ReplayText_MarkAndCopy`, mehrfach als bedingt ausführbar bezeichnet. | Lücke – T-01. |
| Berechtigungen und Datenfilter. | Keine entsprechenden Änderungen vorgesehen. | Nicht erforderlich mit Begründung: Die Anforderung ändert ausschließlich die Interaktion mit bereits angezeigtem Terminalinhalt. |

## Fehlende oder unvollständige Planbestandteile

- [ ] **P-01 – Voraussetzungen für zuverlässige Auswahlbindung ergänzen:** U-05 muss eine konkrete, ausführbare Grundlage für Zeilenidentität bzw. kumulierten Scrollback-Abwurf und Screen-/Reset-Wechsel nennen und die nötigen Änderungen am Buffermodell als Voraussetzung aufnehmen. `TerminalBufferSnapshot` enthält aktuell nur Zellen, Geometrie, Cursor und Scrollback-Zeilen. Am vollen Ringpuffer bleibt die Zeilenanzahl trotz Abwurf konstant; identische Ausgabezeilen lassen sich durch bloßen Textvergleich nicht sicher zuordnen. Ein Alternate-Screen-Wechsel kann ebenfalls zwischen zwei UI-Snapshots erfolgen. Geeignete Identitäts-/Generationsinformationen oder äquivalente verlässliche Änderungsdaten sind daher für die zugesagte Nachführung und Invalidierung nötig. Für vollständig entfernten ausgewählten Inhalt und Größenänderungen ein deterministisches Verhalten einschließlich Test festlegen.

## Hinweise

Die beiden Produktentscheidungen sind durch die Zustimmung des Nutzers geklärt. Die Nachplanung benötigt keine weitere Produktfreigabe; die Befunde betreffen technische Voraussetzungen und verbindliche Testnachweise. `requirement.md` und die Bestandsaufnahme enthalten noch historische offene Fragen, die durch die Entscheidungen in `plan.md` überholt sind.

Die Prüfung bewertet ausschließlich Planvollständigkeit. Es wurden keine Produktdateien geändert und keine Tests ausgeführt. Alle angegebenen Eingaben und verlinkten Inventurdetails wurden gelesen; bestehende Buffer-Snapshot- und E2E-Helfer wurden gezielt zur Prüfung der genannten Voraussetzungen herangezogen.
