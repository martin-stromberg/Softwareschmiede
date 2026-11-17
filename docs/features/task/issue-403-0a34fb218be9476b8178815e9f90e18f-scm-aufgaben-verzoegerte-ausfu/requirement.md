# Anforderung: Verzögerte Ausführung ereignisgesteuerter Aufgaben per Keyword

Quelle: GitHub-Issue martin-stromberg/Softwareschmiede#403 (angefordert aus Issue #393,
Kommentar „Bitte warte 4 Stunden und erledige dann diese Aufgabe" — das aktuelle
Ereignismodell verarbeitet jeden Trigger sofort und kann keine zeitversetzte
Ausführung abbilden).

## Originaltext (untrusted request)

> in den beschreibungstexten ereignisgesteuerter aufgaben soll es möglich sein mit einem
> speziellen keyword eine verzögerte ausführung auszulösen. die aufgabe muss dann einen
> status erhalten, der auch von der funktion des shutdowns beachtet wird.

## Strukturierte Anforderung

- **Auslöser:** Ein spezielles Keyword im Beschreibungstext eines SCM-Ereignisses
  (Issue-/PR-Beschreibung oder Kommentar einer ereignisgesteuerten Aufgabe).
- **Wirkung:** Statt der sofortigen Zustellung an die CLI wird die Ausführung der
  Aufgabe auf einen späteren Zeitpunkt verzögert.
- **Status:** Die Aufgabe erhält einen persistierten Status, der den Wartezustand
  abbildet und von der Shutdown-Funktion (Auto-Shutdown bzw. Anwendungsbeendigung)
  beachtet wird.
- **Verarbeitungseinheit:** Das einzelne SCM-Ereignis / die dadurch ausgelöste
  Aufgabenbearbeitung.
- **Nach dem Zielzeitpunkt:** Die zurückgestellte Zustellung läuft regulär an —
  kein Ereignis geht verloren, kein Ereignis wird doppelt zugestellt.

## Nicht-Ziele (Annahmen, zu bestätigen)

- Keine natürlichsprachliche Datumsinterpretation („übermorgen mittags"); ein
  maschinell auswertbares Format genügt.
- Kein Kalender-/Cron-Feature; nur relative Verzögerung (bzw. ein Zielzeitpunkt).
