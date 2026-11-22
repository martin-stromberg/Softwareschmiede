# Offene Aufgaben

Erstellt am: 2026-11-22
Abbruchgrund: Maximale Iterationsanzahl erreicht (3 Iterationen; offene Punkte sanken 5 → 2 → 1)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt Status `Vollständig umgesetzt`.

## Code-Review-Befunde

- [ ] `ScmEventDispatchTests_Reconcile.cs` / `ScmTaskCompletionMonitoringServiceTests.cs` — **Doppelter Code:** Die deterministische Zeitquelle ist doppelt vorhanden (`TestClock` in `ScmEventDispatchTests_Reconcile.cs` Z. 214–218 vs. `TestTimeProvider` in `ScmTaskCompletionMonitoringServiceTests.cs` Z. 603–608); ebenso das wortgleiche Feld `private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)` in beiden Dateien (Z. 18 bzw. Z. 21). Empfehlung: eine gemeinsame Test-`TimeProvider` (setzbares `Now` + `Advance`) in `Softwareschmiede.TestSupport` neben `ScmDispatchFixture` anbieten; das `Json`-Feld in dieselbe Hilfsstelle oder zu `ScmEventStore.JsonOptions` verschieben.

## Usability-Befunde

Keine — `review-usability.md` trägt Status `Keine Befunde` (keine UI-Änderungen).

## Fehlgeschlagene Tests / ausstehende Abnahme

- [ ] Reale End-to-End-Abnahme der 8 Akzeptanzszenarien: in dieser Umgebung `Nicht durchführbar` dokumentiert (siehe `test-results.md`). **Anwender-Entscheidung liegt vor: manuelle Abnahme durch den Anwender.** Eintragen `Bestanden (manuell, {Datum})` je Szenario in `test-results.md`, sobald der Anwender die Abnahme bestätigt — mindestens Happy Path (Szenario 1: Aufgabe mit `VerarbeitungUngeklärt`-Ereignis + geschlossener/gemergter Referenz → Auto-Abschluss).
