# Anforderung – Schritt 2: Re-Checks bei geschlossenem Issue (Issue #452)

## Ausgangslage

Issue #452: Die Issue-Aufbereitung muss den Live-Status des Issues nicht nur vor dem
CLI-Start, sondern auch (a) unmittelbar vor der Zustellung des Analyse-Prompts und
(b) vor der Anwendung der von der CLI gelieferten Remote-Aktionen erneut prüfen.

## Anforderungen

1. **Finaler Check vor `exchange.PrepareAsync`:** Nach dem (ggf. langwierigen)
   CLI-Start und unmittelbar vor dem Schreiben der `request.json` wird das Issue
   erneut live gelesen. Ist es geschlossen, wird kein Prompt zugestellt, keine
   Analyse gestartet; das Ereignis endet sauber ohne Remote-Änderung.
2. **Check im Abgleichspfad:** Bevor `ReconcilePreparationAttemptAsync` gelieferte
   `issueBodyUpdate`-/`comment`-Aktionen remote anwendet, wird das Issue erneut
   gelesen. Wurde es während der Analyse geschlossen, werden die Aktionen nicht
   ausgeführt; die CLI erhält je Aktion ein ehrliches Misserfolgs-Result mit
   Begründung, das Ereignis endet sauber.
3. **Unlesbares Issue = warten:** `ReadIssueAsync == null` bedeutet an beiden
   Stellen konsistent „warten und später erneut prüfen" — niemals als geschlossen
   interpretieren und niemals Aktionen ausführen.
4. **Status-Semantik:** Geschlossen = `Verarbeitet` mit begründendem
   Aufgabenprotokoll-Eintrag (gleiche Semantik wie der vorhandene
   Vor-Dispatch-Check in `DispatchPreparationAsync`) — kein neuer Status, kein
   `Abgebrochen` (kein Benutzerabbruch), kein Fehlerkommentar am Issue.
5. **Restrisiko:** Die verbleibende TOCTOU-Lücke zwischen letztem Lesen und
   Remote-POST ist inhärent und wird dokumentiert.

## Nicht-Ziele

- Keine generische Issue-Prüfung vor jeder beliebigen Outbox-Aktion anderer
  Ereignisarten (Scope: nur Aufbereitungspfad).
- Keine Beendigung laufender CLI-Sitzungen bei Schließung während der Analyse
  (die Analyse darf zu Ende laufen; nur die Remote-Anwendung wird unterdrückt).

## Akzeptanzszenarien

- Issue offen beim Dispatch-Check, geschlossen vor Zustellung → `Verarbeitet`,
  Protokolleintrag, CLI gestartet aber kein Prompt, keine Remote-Aktion.
- Issue während der Analyse geschlossen → gelieferte Aktionen nicht angewendet,
  `Verarbeitet`, Ergebnisse an CLI mit Begründung.
- Issue unlesbar vor Zustellung → `Wartend` mit Grund, späterer Versuch läuft
  normal weiter (kein `Verarbeitet`, kein `Abgebrochen`).
- Issue unlesbar im Abgleich → Status bleibt `InBearbeitung`, nächster
  Abgleich verarbeitet normal.
- Happy Path unverändert: offenes Issue → Prompt, Aktionen, `Verarbeitet`.
