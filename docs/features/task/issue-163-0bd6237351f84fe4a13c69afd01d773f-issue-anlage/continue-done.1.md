# Erledigte Aufgaben

Erstellt am: 2026-07-21
Abgeschlossen am: 2026-07-21
Ausgangslage: Korrekturrueckmeldung nach Lifecycle-Abschluss

## Offene Planelemente

- [x] KI-Ausfuehrung im Issue-Anlage-Dialog korrigieren: Der KI-Provider erhaelt aktuell offenbar nicht den tatsaechlichen Inhalt des ausgewaehlten Issue-Templates und/oder nicht die Originalanforderung der Aufgabe.
- [x] Den Prompt-/Argumentaufbau fuer `IIssueTemplateTextGenerator.FillIssueTemplateAsync` und die konkreten Implementierungen, insbesondere Codex und Claude CLI, pruefen und so korrigieren, dass Template-Body und Originalanforderung verlaesslich im KI-Aufruf enthalten sind.
- [x] Tests ergaenzen, die nachweisen, dass beim Klick auf `Ausfuellen` der ausgewaehlte Template-Inhalt und die Originalanforderung an den KI-Provider uebergeben werden und das Ergebnis in die bearbeitbare Issue-Beschreibung uebernommen wird.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

Keine.
