← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Ablauf für Anwender

## Voraussetzungen

- Eine Aufgabe ist im Status **Neu** oder **Gestartet** (Repository geklont, Branch angelegt).
- Ein KI-Plugin ist in den Einstellungen oder als Projekt-Standard konfiguriert.

## Schritt-für-Schritt-Anleitung

### 1. KI-Plugin auswählen (falls nicht als Standard gespeichert)

In der Aufgabendetailansicht im Ribbon-Menü das gewünschte KI-Plugin auswählen. Falls das Plugin als Projekt-Standard gespeichert ist, wird es automatisch verwendet.

### 2. CLI starten

Button **Starten** im Ribbon klicken. Die Softwareschmiede:

- Prüft vorab die Voraussetzungen (Terminal-Unterstützung des Systems, Auffindbarkeit und Erreichbarkeit des CLI-Programms).
- Startet das CLI-Programm des Plugins direkt im Aufgabenverzeichnis über die Pseudo Console API — oder über das Fallback-Backend, wenn kein Pseudo-Terminal verfügbar ist.
- Initialisiert das Terminal-Rendering mit der aktuellen Fenster-Größe.
- Wechselt den Aufgabenstatus auf **Gestartet**.

> **Hinweis:** Das Terminal wird unmittelbar angezeigt. Output erscheint in Echtzeit, während das CLI läuft.

> **Eingeschränkter Modus:** Läuft die CLI ohne echtes Pseudo-Terminal (z. B. auf einem zu alten Windows-Build oder weil das Plugin keine Terminal-Unterstützung deklariert), zeigt die Statuszeile den Hinweis **„ (eingeschränkter Modus – kein Pseudo-Terminal)"** — z. B. „Gestartet (eingeschränkter Modus – kein Pseudo-Terminal)". Die CLI läuft weiter und bleibt bedienbar, einige Funktionen (z. B. Terminal-Größenanpassung oder vollbildartige Darstellungen) können jedoch eingeschränkt sein. Details zum Grund stehen als „[Terminal-Diagnose]"-Eintrag im Aufgabenprotokoll. CLIs, die zwingend ein Pseudo-Terminal benötigen, starten in diesem Fall nicht — stattdessen erscheint eine verständliche Fehlermeldung.

### 3. Mit dem CLI arbeiten

Das Terminal im Fenster verhält sich wie ein normales Befehlsfenster mit vollständiger Farbunterstützung. Eingaben werden direkt an das laufende Programm weitergeleitet.

Unterstützte Eingaben:
- Normale Zeichen und Ziffern
- Pfeiltasten (auf/ab/links/rechts) für Zeilen-Navigation
- Funktionstasten F1–F12
- Ctrl+C zum Abbrechen
- Enter zum Ausführen
- Backspace und Delete zum Löschen
- **Ctrl+V zum Einfügen aus der Zwischenablage** — auch längerer mehrzeiliger Text wird vollständig und zeilenweise eingefügt

Die Ansicht passt sich automatisch bei Größenänderungen an; das Terminal wird neu dimensioniert.

> **Hinweis zur Zwischenablage-Einfügung:** Wenn Sie `Ctrl+V` drücken, wird der gesamte Text aus der Windows-Zwischenablage eingefügt. Auch lange mehrzeilige Inhalte wie Stacktraces, Pfade, Klammern, generische Typnamen und Sonderzeichen bleiben erhalten. Multi-line-Text wird automatisch normalisiert: Alle Zeilenumbrüche (`\n`, `\r\n`, `\r`) werden als `\r` (Carriage Return) eingefügt, was dem Windows-CLI-Standard entspricht.

### 3.1. Lange Ausgaben lesen

Wenn die CLI mehr Text ausgibt, als im sichtbaren Terminalbereich Platz hat, erscheint eine vertikale Scrollbar. Ältere Ausgabezeilen bleiben im Verlauf erreichbar und können per Scrollbar, Mausrad, Page Up/Page Down sowie zeilenweisem Scrollen gelesen werden.

Die CLI-Ansicht folgt neuen Ausgaben automatisch, solange Sie am Ende des Verlaufs stehen. Wenn Sie manuell nach oben scrollen, bleibt diese Leseposition stabil und wird durch neue Ausgabe nicht sofort ans Ende zurückgesetzt. Sobald Sie wieder bis ans Ende scrollen, folgt die Ansicht neuen Ausgaben wieder automatisch.

Der Verlauf umfasst bis zu 1000 Scrollback-Zeilen zusätzlich zum aktuell sichtbaren Terminalbereich. Ältere Zeilen werden verworfen, wenn diese Grenze überschritten wird. Klicks in die Terminalfläche setzen den Fokus weiterhin auf das Terminal, sodass Tastatureingaben und `Ctrl+V` auch nach dem Scrollen direkt an die CLI gehen.

> **Vollbild-Programme:** Wenn die CLI eine Vollbild-Ansicht nutzt (z. B. interaktive Auswahllisten oder Texteditoren), wird sie auf den eigenen Bildschirmbereich der CLI umgeschaltet. In diesem Modus ist der Verlauf bewusst nicht scrollbar — die Anzeige folgt dem Programm; beim Verlassen der Vollbild-Ansicht kehrt das normale Scroll-Verhalten zurück.

### 4. CLI beenden

Das CLI beendet sich entweder selbst (nach Abschluss einer Sitzung) oder kann über **Beenden** im Ribbon manuell beendet werden. Nach dem Beenden bleibt der letzte Zustand sichtbar. Der Button **Starten** wird wieder aktiv.

## Ergebnis

Das CLI hat seine Arbeit verrichtet und wird beendet. Der Anwender kann anschließend mit **Aufgabe abschließen** den Status auf **Beendet** setzen oder das CLI erneut starten.

## Besonderheiten

- **Volle Farbe:** Das Terminal unterstützt ANSI 3-bit-, 8-bit- und 24-bit-Farben. Farbige CLI-Ausgaben werden korrekt dargestellt.
- **Scrollbare Ausgabe:** Lange CLI-Ausgaben sind über vertikale Scrollbar, Mausrad und Page-Scroll erreichbar.
- **Scroll-History:** Bis zu 1000 Scrollback-Zeilen bleiben im Speicher. Ältere Zeilen werden verworfen.
- **Auto-Follow:** Neue Ausgabe bleibt sichtbar, solange Sie am Ende stehen; manuelles Hochscrollen wird respektiert.
- **Tastatur-Direktweitergabe:** Tastatureingaben werden nicht gepuffert, sondern unmittelbar an den Prozess weitergeleitet.

## Parallele Ausführung mehrerer CLIs

Die Softwareschmiede unterstützt die parallele Ausführung mehrerer CLI-Prozesse:

1. **CLI startet und läuft weiter:** Das CLI läuft unabhängig davon weiter, ob Sie seine Aufgabenseite gerade anzeigen oder zu einer anderen Aufgabe navigieren.
2. **Navigation zwischen Aufgaben:** Wenn Sie während eines laufenden CLIs zu einer anderen Aufgabe wechseln, wird dessen Terminal nicht mehr angezeigt, **das CLI läuft aber im Hintergrund weiter** und produziert Ausgabe.
3. **Rückkehr zur Aufgabe:** Wenn Sie zur ursprünglichen Aufgabe zurückkehren, sehen Sie:
   - Die komplette Ausgabe-Historie seit Start (keine Lücke)
   - Den aktuellen Zustand des Terminals mit allen neuesten Ausgaben

> **Hinweis:** Dies ermöglicht Ihnen, mehrere lange laufende CLI-Prozesse parallel zu starten und zwischen ihnen zu navigieren, ohne dass eine Blockade entsteht. Jedes CLI läuft eigenständig weiter und puffert seine Ausgabe unabhängig von der UI-Anzeige.

## CLI-Aufzeichnung exportieren und im Konsolentestfenster wiedergeben

Zur Analyse von Darstellungsfehlern in der Terminal-Ausgabe zeichnet die Softwareschmiede die Ausgabe jeder CLI-Sitzung automatisch mit. Diese Aufzeichnung können Sie exportieren und im Konsolentestfenster Schritt für Schritt bzw. zeitversetzt erneut abspielen.

### 1. Aufzeichnung exportieren

1. Öffnen Sie die Aufgabendetailansicht einer Aufgabe, deren CLI bereits gelaufen ist oder läuft.
2. Klicken Sie in der Ribbon-Gruppe **CLI** auf **Aufzeichnung exportieren**.
3. Wählen Sie im Speichern-Dialog einen Zielpfad mit der Endung `.clireplay` und bestätigen Sie.

> **Hinweis:** Sehen Sie stattdessen die Meldung „Für diese Aufgabe liegt noch keine Aufzeichnung vor", wurde für diese Aufgabe noch keine CLI-Sitzung gestartet — die Aufzeichnung läuft erst während einer Ausführung automatisch mit. Der Button **Rohausgabe exportieren** daneben erzeugt weiterhin die zeilenbasierte `.raw`-Textdatei.

### 2. Konsolentestfenster öffnen

1. Öffnen Sie die **Einstellungen**.
2. Klicken Sie auf der Registerkarte **Allgemein** im Abschnitt **Diagnose** auf **Konsolentestfenster öffnen**.
3. Das Fenster „Konsolentest" öffnet sich als eigenes Fenster — die Hauptanwendung bleibt parallel bedienbar.

### 3. Aufzeichnung laden und abspielen

1. Klicken Sie im Konsolentestfenster auf **Aufzeichnung öffnen…** und wählen Sie die exportierte `.clireplay`-Datei.
2. Links erscheint die Terminal-Ausgabe, rechts die Liste aller aufgezeichneten Ausgabe-Blöcke mit Position, Zeitpunkt und Quelltext (Steuerzeichen sind sichtbar gemacht, z. B. `␛` für Escape-Sequenzen).
3. Klicken Sie auf **Abspielen** — die Aufzeichnung wird in echtem Zeittempo wiedergegeben, genau wie sie damals am Bildschirm erschien.

### 4. Wiedergabe steuern

- **Pausieren/Fortsetzen** hält die Wiedergabe an und setzt sie an derselben Stelle fort.
- **Neu starten** bricht den laufenden Durchlauf ab und spielt sofort wieder von vorn.
- **Zeitraffer-Schwelle (s)** legt fest, wie lange die Wiedergabe bei längeren Ausgabe-Pausen maximal wartet: Steht dort z. B. `1`, werden Denkpausen der CLI auf eine Sekunde verkürzt; `0` spielt so schnell wie möglich ab. Der Wert kann jederzeit geändert werden.

> **Hinweis:** Zeigt das Fenster das Band „Aufzeichnung unvollständig", wurde das Speicher-Limit der Aufzeichnung erreicht — die Wiedergabe endet dann vor dem tatsächlichen Ende der Sitzung.

### Ergebnis

Die Terminal-Ausgabe wird exakt so wiedergegeben, wie sie ursprünglich gerendert wurde. Der gerade wiedergegebene Block ist in der rechten Liste markiert, sodass Sie für jede sichtbare Ausgabe den zugehörigen Quelltext nachvollziehen können — etwa um festzustellen, an welcher Stelle Zeichen ausgelassen oder doppelt erscheinen.
