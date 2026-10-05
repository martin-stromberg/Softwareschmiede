# Bestandsaufnahme: Texte in CLI-Ausgaben kopieren

## Zusammenfassung

Die CLI wird in der Aufgabenansicht durch das eigene WPF-Control `TerminalControl` als Raster aus Terminalzellen gezeichnet. Das Control nimmt bereits Tastatureingaben entgegen und leitet sie an den laufenden Prozess weiter; für Textauswahl und Kopieren existiert aktuell keine Unterstützung. Insbesondere sind keine Auswahlgrenzen und kein Auswahloverlay implementiert. Die Oberfläche kann daher nicht wie eine gewöhnliche Textkonsole per Maus markieren.

Die naheliegende Implementierungsstelle ist `TerminalControl`, das auch im Konsolentestfenster verwendet wird. Änderungen an der normalen Darstellung wirken damit voraussichtlich auch auf Replay-Ausgaben. Der Tastaturpfad benötigt besondere Beachtung: `Ctrl+V` ist bereits als Einfügen in den Prozess belegt, während andere Kombinationen grundsätzlich durch `KeyToVt100Encoder` kodiert und an die Session gesendet werden. `Ctrl+C` kann so als Prozess-Eingabe/Abbruchsignal wirken und ist nicht einfach als Kopierkürzel umzudeuten.

## Relevante Bereiche

- [Darstellung und Eingabe](inventory/terminal-control.md) — Rendering, Maus-/Tastaturpfad, fehlende Auswahl und Kopierfunktion
- [Daten und Ausgabe-Fluss](inventory/output-flow.md) — `TerminalBuffer`, Session-Anbindung, Status- und Replay-Verwendung
- [Vorhandene Tests](inventory/tests.md) — relevante Testklassen und aktuelle Abdeckungslücken

## Umfangshinweis

Die Anforderung betrifft die gerenderte CLI-Ausgabe, nicht das Exportieren der protokollierten CLI-Rohdaten. `TaskDetailView` enthält hierfür bereits separate Exportaktionen. Die Anforderung legt weder ein bestimmtes Kopierkürzel noch das Verhalten einer Auswahl bei neu eintreffender Ausgabe fest; beides bleibt für die Planung zu entscheiden.
