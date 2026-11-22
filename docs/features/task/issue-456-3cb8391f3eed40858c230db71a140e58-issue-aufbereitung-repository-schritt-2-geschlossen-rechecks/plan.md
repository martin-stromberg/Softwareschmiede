# Umsetzungsplan – Schritt 2: Re-Checks bei geschlossenem Issue

## Änderungen

1. **`DispatchPreparationAsync`** (`ScmEventOrchestrator.cs`, vor `exchange.PrepareAsync`
   ~`:758`): zweiter `ReadIssueAsync`-Aufruf, `issue` wiederverwendet.
   `null` → `WaitAsync` (gleicher Grund wie Vor-Check); `IsClosed` → `Verarbeitet`
   + Protokolleintrag „wurde vor der Zustellung geschlossen; ohne Änderung
   abgeschlossen". Der Frischwert `issue.IssueUrl` bleibt für die Request nutzbar.
2. **`ReconcilePreparationAttemptAsync`**: Signatur um `IScmEventSource source`
   erweitern (Aufrufer `:632` übergibt). Direkt vor der Aktionsschleife im
   `Accepted`-Block, nur bei `messages.Actions.Count > 0`: einmalig
   `ReadIssueAsync`. `null` → `SaveChangesAsync` + `return` (Status bleibt
   `InBearbeitung`; nächster Abgleich wiederholt). `IsClosed` → je Aktion
   `WriteResultAsync(success=false, "…wurde zwischenzeitlich geschlossen…")`,
   `item.Status = Verarbeitet`, Protokolleintrag, `SaveChangesAsync`, `return`.
   Abschlussblock `:917` greift dann nicht mehr (Status ≠ `InBearbeitung`).
3. **Tests** `ScmEventDispatchTests_IssuePreparation.cs`:
   - `…_IssueGeschlossenVorZustellung_VerarbeitetOhneZustellung`: sequentielle
     Mock-Antworten (1. Read offen, dann geschlossen) → `Verarbeitet`, Session
     gestartet, `Prompts`/`Responses` leer, Protokolleintrag.
   - `…_IssueGeschlossenWährendAnalyse_KeinRemoteUpdate`: nach Zustellung +
     accepted/actions/complete ReadIssueAsync auf geschlossen schalten →
     `Verarbeitet`, `fixture.Responses` leer, Ergebnisdateien mit Begründung.
   - `…_IssueUnlesbarVorZustellung_Wartet`: 1. Read offen, dann `null` →
     `Wartend`, kein Prompt; nach Rückschaltung auf offen → normaler Versand.
   - `…_IssueUnlesbarImAbgleich_WartetUndVerarbeitet`: nach Zustellung +
     Meldungen `ReadIssueAsync → null` → `InBearbeitung` bleibt, keine
     Remote-Aktion; danach offen → `Verarbeitet`.
4. **Doku**: `business-rules.md` Ausführungs-Punkt ergänzen (zwei Re-Check-
   Punkte + TOCTOU-Restrisiko); `RELEASE_NOTES.md` Eintrag (en/de);
   `troubleshooting.md` nicht betroffen (kein neues Fehlerbild — ehrliche Stati).

## Offene Punkte

Keine.
