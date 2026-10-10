# Umsetzungsplan: Texte aus CLI-Ausgaben kopieren

## Übersicht

`TerminalControl` erhält eine zellbasierte Textauswahl für den dargestellten `TerminalBuffer`. Nutzende markieren Text mit der Maus oder erweitern eine Auswahl mit der Tastatur. Die Markierung bleibt bei normaler neuer Ausgabe erhalten und wird bei Scrollback-Verschiebungen an dieselben Buffer-Zeilen und Zellen gebunden. `Ctrl+Shift+C` kopiert den ausgewählten Text; `Ctrl+C` bleibt unverändert CLI-Eingabe bzw. Abbruchsignal. Live-Ausgabe und Replay werden verbindlich abgenommen.

## Verbindliche Produktentscheidungen

- Bei aktiver Auswahl kopiert `Ctrl+Shift+C` den ausgewählten Text.
- `Ctrl+C` wird nicht zum Kopieren verwendet und bleibt als CLI-Eingabe/Abbruchsignal erhalten.
- Eine Auswahl bleibt bei normaler neuer Ausgabe erhalten. Wenn sich die Anzeige durch Scrollback-Verschiebung bewegt, bleibt sie am ursprünglichen Inhalt verankert.
- Die Auswahl wird verworfen, wenn ausgewählte Zellen überschrieben/gelöscht oder aus dem verfügbaren Buffer entfernt werden, wenn sich die Terminalgeometrie ändert oder wenn Session, Terminal-Reset oder aktiver Bildschirm (Haupt-/Alternate-Screen) wechseln.
- `Ctrl+Shift+C` ohne aktive Auswahl ändert die Zwischenablage nicht und wird nicht an den CLI-Prozess gesendet.

## Designentscheidungen

| Bereich | Ansatz | Begründung |
|---|---|---|
| Implementierungsort | Auswahlzustand, Koordinatenumrechnung, Extraktion und Overlay in `src/Softwareschmiede.App/Controls/TerminalControl.cs`; notwendige Identitätsdaten im `TerminalBuffer`/`TerminalBufferSnapshot` | Das Control zeichnet sowohl Live- als auch Replay-Ausgabe und besitzt Scrolloffsets sowie Zellgeometrie. Rohdaten- und Replay-Export bleiben getrennte Funktionen. |
| Auswahlbindung | Auswahlgrenzen referenzieren stabile logische Zeilen-IDs sowie Spalten und Zell-Schreibversionen. Der Buffer vergibt monoton steigende Zeilen-IDs beim Anlegen neuer logischer Zeilen und Zellversionen bei jedem Schreiben oder Löschen. `TerminalBufferSnapshot` stellt diese Identitätsdaten, die Buffer-/Screen-Generation und die Geometrie atomar mit den Zellen bereit. | Ein Ringpuffer kann dieselbe Zeilenanzahl behalten, obwohl alte Zeilen herausfallen. Textvergleiche sind keine Identität: dieselben Zeichen an derselben Position dürfen eine zuvor entfernte/überschriebene Auswahl nicht wiederbeleben. |
| Buffer-Generationen | Buffer-/Screen-Generation steigt bei Reset, Geometrieänderung und Wechsel des aktiven Bildschirms. Die aktuelle Generation und Zeilen-IDs bleiben über normale Ausgabe und Scrollback-Verschiebungen stabil. | Die Auswahl kann gewöhnliche Ausgabe überleben und strukturelle Wechsel deterministisch erkennen, auch wenn zwischen zwei UI-Snapshots mehrere Ereignisse eingetroffen sind. |
| Extraktion | Auswahlgrenzen in Leserichtung normalisieren und zeilenweise aus einem konsistenten Snapshot extrahieren. Zwischen ausgewählten Zeilen `Environment.NewLine` einfügen; abschließende terminalbreite Leerzeichen je Zeile trimmen, Leerzeichen innerhalb der Auswahl und leere Zwischenzeilen erhalten. | Der Clipboardtext entspricht den ausgewählten Zellen und der sichtbaren Zeilenstruktur ohne Padding der festen Terminalbreite. |
| Auswahl-Lifecycle | Nach jeder Bufferänderung prüfen, ob Generation, ausgewählte Zeilen-IDs und ausgewählte Zellversionen weiter gültig sind. Sind alle gültig, Auswahl auf aktuelle logische Koordinaten abbilden; fehlt eine ausgewählte Zeile/Zelle oder hat sich deren Version geändert, Auswahl vollständig löschen. | Verhindert sowohl ein ungewolltes Aufheben bei normaler Ausgabe als auch das unbemerkte Markieren neu entstandenen Inhalts. |
| Maus und Tastatur | Mausdrag bildet Pixelkoordinaten unter Beachtung von Zellmaß, sichtbarem Start, Scrollback sowie horizontalem und vertikalem Offset auf Zellen ab. `Shift`+Pfeiltasten erweitert die Auswahl zellweise; `Shift`+`Home`/`End` erweitert bis Zeilenanfang/-ende. | Ermöglicht Auswahl in Grid und Scrollback ohne UIA-TextPattern und erhält Navigation außerhalb einer begonnenen Auswahl. |
| Markierung | `OnRender` zeichnet ein geclipptes Auswahl-Overlay über den ausgewählten Zellen, ohne Zeichen, Zellfarben oder Terminalcursor zu verändern. | Die Markierung bleibt mit dem vorhandenen Custom-Renderer kompatibel. |
| Clipboard | `Ctrl+Shift+C` liest ausgewählten Text aus einem konsistenten Snapshot und schreibt ihn über `Clipboard.SetText`. Clipboard-Ausnahmen werden abgefangen und protokolliert; sie dürfen weder die UI noch den Prozess beenden. Clipboardzugriffe in Tests laufen auf STA und mit begrenzten Wiederholungen bei temporärer Belegung. | Verwendet die bestehende WPF-Clipboard-Integration und legt ein stabiles Fehlerverhalten fest. |
| UI-Testnachweis | Für die sichtbare Auswahl Vorher-/Nachher-Screenshots desselben Terminalausschnitts aufnehmen und die Pixel im erwarteten Zellrechteck vergleichen. Den Clipboardtext separat über STA auslesen. | Das Control stellt kein UIA-TextPattern bereit; die Abnahme muss die tatsächliche Darstellung sowie den tatsächlichen Clipboardinhalt prüfen. |

## Umsetzungsschritte

| ID | Abhängigkeit | Arbeit |
|---|---|---|
| U-01 | – | `TerminalBuffer` und `TerminalBufferSnapshot` um stabile logische Zeilen-IDs, Zell-Schreibversionen sowie Buffer-/Screen-Generation und Geometrie erweitern. IDs bleiben bei Scrollback-Verschiebung erhalten; neue Zeilen erhalten neue IDs. Jede Mutation einer Zelle einschließlich Löschen/Erase vergibt eine neue Zellversion. Reset, Resize und Alternate-Screen-Wechsel erhöhen die Generation. Snapshot-Zellen und Identitätsdaten werden konsistent in einem Snapshot gelesen. |
| U-02 | U-01 | Zellbasiertes Selection-Modell mit Anker, Endpunkt, Zeilen-ID und Zellversion einführen. Hilfslogik für Vergleich, normalisierte Grenzen, Koordinatenabbildung und Extraktion aus einem Snapshot implementieren. Tests für Vorwärts-/Rückwärtsauswahl, gleiche/einzelne und mehrere Zeilen, leere Zeilen sowie Leerzeichenregeln vorsehen. |
| U-03 | U-02 | Mausauswahl in `TerminalControl` ergänzen: Klick setzt Anker/Fokus, Drag aktualisiert das Ende, Loslassen beendet den Drag. Scrollback sowie beide Scrolloffsets berücksichtigen; außerhalb eines gestarteten Drags das ScrollViewer-Verhalten erhalten. |
| U-04 | U-02 | Sichtbares, geclipptes Overlay in `OnRender` zeichnen. Auswahlgeometrie für beide Ziehrichtungen, Scrollpositionen und Zellgrenzen berechnen. Darstellungstests für Markierung, Clipping und unveränderte Terminalzeichen/-farben/-cursor ergänzen. |
| U-05 | U-02 | Tastaturauswahl und Kopieren ergänzen. `Shift`+Pfeile und `Shift`+`Home`/`End` erweitern die Auswahl. `Ctrl+Shift+C` kopiert nur bei aktiver Auswahl; ohne Auswahl bleibt die Zwischenablage unverändert und es werden keine CLI-Bytes geschrieben. `Ctrl+C` wird unverändert kodiert/weitergeleitet; `Ctrl+V` und andere Eingaben behalten ihr bestehendes Verhalten. Clipboardfehler kontrolliert abfangen und protokollieren. |
| U-06 | U-01 bis U-05 | Lifecycle anbinden: Bei gültigen Zeilen-IDs und Zellversionen die Auswahl bei normaler Ausgabe erhalten und bei Scrollback-Verschiebung auf die aktuellen Koordinaten nachführen. Auswahl vollständig verwerfen, wenn eine ihrer Zeilen/Zellen entfernt oder mutiert wurde, einschließlich Erase/Löschen mit anschließendem Neuschreiben identischer Zeichen. Bei Resize, Reset, Screen-/Sessionwechsel verwerfen. |
| U-07 | U-01 bis U-06 | Unit-/Control-/Integrationstests ergänzen (Details unter „Verbindliche Testabdeckung“). Bestehende `KeyToVt100EncoderTests.KeyEncoding.cs`, `TerminalControlTests.KeyInput.cs` und `TerminalControlTests.ClipboardPaste.cs` ausführen bzw. gezielt erweitern. |
| U-08 | U-01 bis U-07 | Pflicht-E2E für Live-Ausgabe und Replay ergänzen und in `src/Softwareschmiede.Tests/E2E/MainTest.cs` in die Suite aufnehmen. `E2E_KonsolenTestfenster.cs` ist die Replay-Datei; Live-Auswahl wird in `E2E_ConPtyLifecycle.cs` oder einer gleichwertig registrierten E2E-Phase am kontrollierten Prozess umgesetzt. Konkrete Abläufe und Nachweise siehe „Verbindliche E2E-Abnahme“. |
| U-09 | U-07, U-08 | Build und betroffene Testgruppen einschließlich beider UI-E2E-Pflichtszenarien ausführen. Fehlende Desktop-/Clipboard-Voraussetzungen, nicht gestartete Szenarien oder fehlgeschlagene Nachweise werden als nicht bestanden dokumentiert; sie dürfen nicht als grüne Abnahme gewertet werden. |

## Verbindliche Testabdeckung

### Buffer- und Control-Tests

- Extraktion: Vorwärts- und Rückwärtsgrenzen, eine und mehrere Zeilen, Teilzeilen, Leerzeilen, interne Leerzeichen und entfernte terminalbreite Endleerzeichen; exakte Zeilenumbrüche mit `Environment.NewLine`.
- Koordinaten: Mausposition bei Scrollback, vertikalem/horizontalem Scrolloffset und Zellrändern; Tastaturerweiterung in beide Richtungen sowie mit `Home`/`End`.
- Erhalt/Nachführung: Normale Ausgabe außerhalb der Auswahl erhält sie; neu angelegte Zeilen und Ringpuffer-Verschiebung ändern die logischen Anzeigezeilen, während Auswahl und extrahierter Inhalt an denselben stabilen Zeilen-IDs bleiben.
- Invalidierung: Entfernen eines ausgewählten Bereichs durch Scrollback-Abwurf; Überschreiben und Erase innerhalb ausgewählter Zellen; danach Neuschreiben identischer Zeichen an derselben Position; Buffer-Reset; Resize; Alternate-Screen-Wechsel; Sessionwechsel. Jeweils muss die Auswahl leer sein und neue Zeichen dürfen nicht versehentlich ausgewählt werden.
- Kopieren: Mit Auswahl wird exakt der extrahierte Text in die Zwischenablage geschrieben; ohne Auswahl bleibt ein vorher gesetzter Clipboardwert unverändert und es gibt keine Prozess-Eingabe; ein abgefangener Clipboard-Schreibfehler wirft nicht aus und beendet die UI-Aktion kontrolliert.
- Tastaturregression: `Ctrl+C` wird weiterhin an die Live-Session weitergereicht/als Abbruchsignal erhalten und löst keinen Clipboard-Schreibvorgang aus; `Ctrl+Shift+C` mit Auswahl schreibt keine Bytes/kein ETX in den CLI-Input; bestehendes `Ctrl+V` und gewöhnliche Eingabe werden weiterhin weitergeleitet.

Vorzugsweise werden isolierbare Auswahl-/Extraktions- und Lifecycle-Regeln in `TerminalBufferTests.cs` bzw. `TerminalControlTests.cs` geprüft; Tastatur und Clipboard in `TerminalControlTests.KeyInput.cs` sowie `TerminalControlTests.ClipboardPaste.cs`. Fehlerpfade erhalten einen injizierbaren Clipboard-Schreibpfad oder äquivalenten kontrollierten Testzugriff, sodass der Schreibfehler deterministisch ausgelöst werden kann.

### Pflicht-E2E-Abnahme

Die Szenarien laufen seriell, weil sie dieselbe Windows-Zwischenablage verwenden. Für Maus- und Tastaturaktionen werden wiederverwendbare FlaUI-Helfer in den E2E-Views ergänzt: tatsächliches Terminal-Control lokalisieren, dessen Bildschirmrechteck und Zellmaße bestimmen, Zellkoordinaten in Bildschirmkoordinaten abbilden, Mausdrag ausführen und `Ctrl+Shift+C` senden. Für den visuellen Nachweis Screenshot vor und nach der Auswahl am gleichen Ausschnitt aufnehmen und die Pixel im erwarteten Auswahlrechteck auf die Hervorhebung prüfen. Clipboardlesen erfolgt in einem STA-Thread mit begrenztem Retry bei temporärer Clipboardbelegung.

- **E-01 `TerminalText_LiveMarkCopyAndKeepSelection`:** Im registrierten ConPTY-Live-Szenario einen kontrollierten Prozessausgabeschritt mit den eindeutigen Zeilen `erste Zeile` und `zweite Zeile` auslösen. Diese Zeilen per Maus markieren; die Pixelprüfung muss die sichtbare Markierung beider Zeilen bestätigen. Danach über denselben kontrollierten Prozess weitere Ausgabe außerhalb der Auswahl auslösen und nachweisen, dass die Markierung weiter sichtbar und der Originalausschnitt weiterhin ausgewählt ist. `Ctrl+Shift+C` auslösen und den STA-Clipboardinhalt exakt mit `erste Zeile{Environment.NewLine}zweite Zeile` vergleichen. Den Ausgabeschritt so dimensionieren, dass mindestens ein deterministischer Scrollback-Fall eintritt, bei dem die ausgewählten Zeilen im Buffer verbleiben und ihre logischen Positionen verschoben werden; zur Auswahl zurückscrollen, Sichtbarkeit prüfen und denselben Text erneut kopieren. Zusätzlich einen Tastaturauswahl-Durchlauf aus einem dokumentierten, reproduzierbaren Fokus-/Caret-Startzustand ausführen und den Clipboardinhalt exakt vergleichen. Vor dem Copy-Schritt bestätigen, dass die Auswahl nur nach der normalen Ausgabe und Scrollverschiebung fortbesteht.
- **E-02 `ReplayText_MarkAndCopy`:** In `E2E_KonsolenTestfenster.cs` eine deterministische synthetische `.clireplay`-Fixture mit zwei unterscheidbaren Textzeilen erzeugen, über Einstellungen → Allgemein im Konsolentestfenster laden und vollständig abspielen. Text am tatsächlichen Terminal-Control mit Maus markieren; Pixel-Hervorhebung und exakten Clipboardinhalt einschließlich `Environment.NewLine` prüfen. Einen zweiten Durchlauf per Tastaturauswahl und `Ctrl+Shift+C` ausführen. Bestätigen, dass keine Live-Prozess-Eingabe ausgelöst wird.

Beide Tests sind Pflicht und müssen in `MainTest.RunGeneralTests` registriert bzw. als zwingende Phase der dort gestarteten E2E-Tests aufgenommen sein. Sie dürfen nicht wegen fehlenden Clipboardzugriffs bedingt übersprungen werden. Ist Desktop-, Maus-/Tastatur- oder Clipboardzugriff nicht verfügbar, gilt der jeweilige E2E-Nachweis als nicht ausgeführt/fehlgeschlagen und wird so dokumentiert; andere Tests kompensieren ihn nicht.

### Auszuführende Bestandsprüfungen

- `src/Softwareschmiede.Tests/App/Controls/KeyToVt100EncoderTests.KeyEncoding.cs` einschließlich der `Ctrl+C`-Kodierungs-/Weiterleitungsregression.
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.KeyInput.cs` und `TerminalControlTests.ClipboardPaste.cs` einschließlich der bestehenden `Ctrl+V`-Regression.
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs` und `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs` für Render-, Snapshot-, Scroll- und Bufferverhalten.
- E2E-Runner `src/Softwareschmiede.Tests/E2E/MainTest.cs` einschließlich E-01 und E-02.

## Abnahmekriterien

- Maus und Tastatur markieren den entsprechenden Text sichtbar; vorwärts und rückwärts gerichtete Auswahl liefern dieselben Grenzen.
- Kopieren liefert exakt die gewählten Zeichen, interne Leerzeichen und Leerzeilen bleiben erhalten, terminalbreite Endleerzeichen werden entfernt und Zeilenumbrüche sind `Environment.NewLine`.
- `Ctrl+Shift+C` kopiert bei aktiver Auswahl. Ohne Auswahl bleibt die Zwischenablage unverändert und der CLI-Input unangetastet. `Ctrl+C` bleibt CLI-Eingabe/Abbruchsignal; `Ctrl+V` fügt weiterhin in die Live-CLI ein.
- Normale neue Ausgabe erhält die Auswahl. Scrollback-Verschiebung bindet sie an denselben ausgewählten Inhalt. Entfernte oder mutierte ausgewählte Zellen, identisches Neuschreiben, Resize, Reset, Alternate-Screen- und Sessionwechsel löschen die Auswahl deterministisch.
- E-01 weist Maus-, Tastatur-, Live-Ausgabe-, Erhalt-, Scrollback- und Clipboardfluss im tatsächlichen Aufgabenfenster nach. E-02 weist Maus-, Tastatur- und Clipboardfluss im tatsächlichen Replay-Konsolenfenster nach. Beide Pflicht-E2E müssen erfolgreich durchlaufen.
- Clipboardfehler werden kontrolliert behandelt und beeinträchtigen weder UI noch Prozess.

## Offene Punkte

Keine. Produktverhalten und technische Invalidierungsregeln sind festgelegt; Live- und Replay-E2E sind verbindliche Abnahmen.
