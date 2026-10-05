# Übersetzte Anforderung: CLI-Ausgaben markieren und kopieren

**Quelle:** `issue.md` (Aufgabe `794797b3-d7b7-4b2f-92f8-2ddac8ec47b0`)
**Branch:** `task/794797b3d7b74b2f92f82ddac8ec47b0-texte-kopieren`

## Fachliche Zusammenfassung

Nutzende sollen Text in den Ausgaben der CLI wie in einer üblichen Konsole mit der Maus markieren und in die Zwischenablage kopieren können.

## Ziel und Nutzen

Die CLI-Ausgabe lässt sich gezielt auswählen und außerhalb des Ausgabefensters weiterverwenden, beispielsweise zum Teilen oder zur Analyse von Fehlermeldungen.

## Akzeptanzkriterien

- Text aus der angezeigten CLI-Ausgabe kann mit Maus oder Tastatur markiert werden.
- Eine Markierung kann in die Zwischenablage kopiert werden.
- Der kopierte Inhalt entspricht dem ausgewählten Text und erhält Zeilenumbrüche in sinnvoller Form.
- Die Auswahl- und Kopierfunktion funktioniert während der Anzeige einer CLI-Ausgabe.

## Umfang

- Betrifft die Darstellung der CLI-Ausgaben und die Interaktion zum Auswählen und Kopieren von Text.
- Ein bestimmter Kopiermechanismus oder eine bestimmte Tastenkombination ist nicht vorgegeben.

## Offene Fragen

- Sollen zusätzlich zur Mausmarkierung bestimmte Konsolen-Tastenkürzel, etwa `Ctrl+C` zum Kopieren, unterstützt werden? In Konsolen kann diese Kombination auch eine laufende CLI abbrechen.
- Soll die Markierung über neue Ausgaben hinweg erhalten bleiben oder bei eingehender Ausgabe aufgehoben werden?
