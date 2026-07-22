# Offene Aufgaben

Erstellt am: 2026-07-22
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] Cleanup kann bei Backpressure noch nicht gequeute Zeilen aus einem bereits gelesenen Chunk verwerfen: `CompleteAsync` darf den Channel nicht schliessen, waehrend `OnOutputChunk` noch Zeilen aus einem bereits dekodierten Chunk in die bounded Queue schreibt. Benoetigt wird eine Synchronisation zwischen aktiver Producer-Phase und Abschluss oder ein separater Cleanup-Pfad, der bei Drain-Timeout nicht selbst completed. Dazu gehoert ein Race-Test mit voller Queue, blockierter Persistenz, parallelem `CompleteAsync` und anschliessender Persistenzfreigabe.

## Fehlgeschlagene Tests

Keine.
