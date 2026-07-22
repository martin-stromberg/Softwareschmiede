# Anforderung

## Fachliche Zusammenfassung

Der Ausgabenbereich der CLI-Konsole zeigt aktuell nur einen begrenzten Ausschnitt der erzeugten Konsolenausgaben. Wenn ein CLI-Lauf mehr Text produziert, als gleichzeitig sichtbar ist, verschwinden ältere Ausgaben aus dem sichtbaren Bereich und können vom Anwender nicht mehr eingesehen werden.

Erwartetes Verhalten: Der Ausgabenbereich der CLI-Konsole soll scrollbar sein, sodass Anwender auch ältere, bereits aus dem sichtbaren Bereich verdrängte Ausgaben nachträglich lesen können.

## Betroffene Klassen und Komponenten

### UI / Anzeige
- CLI-Ausgabenbereich innerhalb der Anwendung
- Komponente oder View, die laufende und abgeschlossene CLI-Ausgaben rendert
- ScrollViewer-, TextBox-, RichTextBox-, ListBox- oder vergleichbare Container-Konfiguration des Ausgabenbereichs

### Laufzeitdaten
- Gespeicherte oder gepufferte Konsolenausgabe eines CLI-Laufs
- Aktualisierung der Ausgabe während laufender CLI-Prozesse

### Bedienverhalten
- Scrollbarkeit im Ausgabenbereich
- Sichtbarkeit neuer Ausgaben bei laufender Aktualisierung
- Nachträgliches Einsehen älterer Ausgaben

## Implementierungsansatz

Der Ausgabenbereich der CLI-Konsole soll in einen scrollbaren Container eingebettet oder so konfiguriert werden, dass vertikales Scrollen möglich ist.

Zu prüfen ist insbesondere:
- Ob der aktuell verwendete Ausgabencontainer eine feste Höhe besitzt, aber keine Scrollbarkeit aktiviert hat.
- Ob eine vorhandene ScrollViewer-Konfiguration durch Layout-Eigenschaften wie `Height`, `MaxHeight`, `VerticalAlignment`, Grid-Zeilen oder verschachtelte Container wirkungslos bleibt.
- Ob der Inhalt bei laufender Ausgabe automatisch ans Ende scrollen soll, ohne dem Anwender das manuelle Zurückscrollen auf ältere Ausgaben zu erschweren.

Das Zielverhalten ist:
- Bei mehr Ausgabe als sichtbarem Platz erscheint eine vertikale Scrollmöglichkeit im Ausgabenbereich.
- Ältere Ausgaben bleiben innerhalb des Ausgabenverlaufs erreichbar.
- Neue Ausgaben werden weiterhin im Ausgabenbereich ergänzt.
- Die Änderung betrifft nur den Ausgabenbereich der CLI und verändert nicht die Ausführung der CLI-Prozesse selbst.

## Konfiguration

Keine fachliche Konfiguration erforderlich. Die Änderung ist voraussichtlich eine UI-/Layout-Anpassung.

## Offene Fragen

- Soll der Ausgabenbereich bei neuen CLI-Ausgaben automatisch ans Ende scrollen, solange der Anwender nicht manuell nach oben gescrollt hat?
- Gibt es eine maximale Länge des gespeicherten Ausgabeverlaufs, oder sollen alle Ausgaben eines CLI-Laufs vollständig scrollbar bleiben?
- Soll horizontales Scrollen ebenfalls unterstützt werden, falls einzelne Ausgabezeilen sehr lang sind, oder sollen lange Zeilen umgebrochen werden?
