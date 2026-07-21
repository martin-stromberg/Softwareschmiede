# Offene Aufgaben

Erstellt am: 2026-07-21
Abbruchgrund: Korrekturrueckmeldung nach Lifecycle-Abschluss

Die folgenden Aufgaben muessen in einem Lifecycle-Fortsetzungslauf bearbeitet werden.

## Offene Planelemente

- [x] KI-Ausgabe im Issue-Anlage-Dialog korrekt mit Sonderzeichen/Umlauten verarbeiten. Sichtbares Symptom: Von der KI generierter Text enthaelt Mojibake wie `plÃtzlich`, `fÃr`, `SelbstwertgefÃhl` statt korrekter deutscher Umlaute.
- [x] Den Zeichensatz-/Encoding-Pfad der KI-Ausfuehrung pruefen, insbesondere Prozess-Start, stdin/stdout-Weitergabe, `CliKiPluginBase`, Codex- und Claude-CLI-Implementierung sowie die Rueckgabe in `IssueCreateDialogViewModel.Body`.
- [x] Die Korrektur so umsetzen, dass UTF-8-Ausgaben der KI korrekt gelesen und in der editierbaren Issue-Beschreibung angezeigt werden.
- [x] Tests ergaenzen, die deutsche Umlaute und Sonderzeichen im KI-Ergebnis abdecken und sicherstellen, dass kein Mojibake in der Dialogbeschreibung landet.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

Keine.
