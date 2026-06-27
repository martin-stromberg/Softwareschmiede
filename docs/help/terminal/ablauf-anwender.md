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

- Startet das CLI-Programm des Plugins im Aufgabenverzeichnis über die Pseudo Console API.
- Initialisiert das Terminal-Rendering mit der aktuellen Fenster-Größe.
- Wechselt den Aufgabenstatus auf **Gestartet**.

> **Hinweis:** Das Terminal wird unmittelbar angezeigt. Output erscheint in Echtzeit, während das CLI läuft.

### 3. Mit dem CLI arbeiten

Das Terminal im Fenster verhält sich wie ein normales Befehlsfenster mit vollständiger Farbunterstützung. Eingaben werden direkt an das laufende Programm weitergeleitet.

Unterstützte Eingaben:
- Normale Zeichen und Ziffern
- Pfeiltasten (auf/ab/links/rechts) für Zeilen-Navigation
- Funktionstasten F1–F12
- Ctrl+C zum Abbrechen
- Enter zum Ausführen
- Backspace und Delete zum Löschen

Die Ansicht passt sich automatisch bei Größenänderungen an; das Terminal wird neu dimensioniert.

### 4. CLI beenden

Das CLI beendet sich entweder selbst (nach Abschluss einer Sitzung) oder kann über **Beenden** im Ribbon manuell beendet werden. Nach dem Beenden bleibt der letzte Zustand sichtbar. Der Button **Starten** wird wieder aktiv.

## Ergebnis

Das CLI hat seine Arbeit verrichtet und wird beendet. Der Anwender kann anschließend mit **Aufgabe abschließen** den Status auf **Beendet** setzen oder das CLI erneut starten.

## Besonderheiten

- **Volle Farbe:** Das Terminal unterstützt ANSI 3-bit-, 8-bit- und 24-bit-Farben. Farbige CLI-Ausgaben werden korrekt dargestellt.
- **Scroll-History:** Die letzten 1000 Zeilen bleiben im Speicher. Ältere Zeilen werden verworfen.
- **Tastatur-Direktweitergabe:** Tastatureingaben werden nicht gepuffert, sondern unmittelbar an den Prozess weitergeleitet.
