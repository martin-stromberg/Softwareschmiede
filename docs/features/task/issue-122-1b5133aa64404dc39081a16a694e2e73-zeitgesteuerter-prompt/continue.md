# Offene Aufgaben

Erstellt am: 2026-07-12
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `E2E_ZeitgesteuerterPrompt.cs` — Testqualität (Rest-Mitternachts-Flakiness, niedrige Priorität): Der Mitternachts-Guard begrenzt eine in der Zukunft liegende Zielzeit auf `23:59:00`, wenn `jetzt.AddMinutes(5)` in den nächsten Tag überlaufen würde. Läuft der Test zwischen 23:59:00 und 23:59:59, liegt diese begrenzte Zielzeit bereits in der Vergangenheit, die Produktivlogik versendet dann sofort statt zu puffern, und `WaitForElement` läuft in ein Timeout. Empfehlung: Für dieses terminale 1-Minuten-Fenster den Test überspringen (`Skip.If(jetzt.Hour == 23 && jetzt.Minute >= 59, ...)`) statt eine unerreichbare Zielzeit zu wählen.

## Fehlgeschlagene Tests

Keine.
