# Anforderung – Entwicklungsschritt 1: Konsistente SCM-Repository-Identität im SeparatesKonto-Modus

Quelle: GitHub-Issues #436 + #456 (gleiches Defektbild), Projektschritt 1.

## Ziel

Im SeparatesKonto-Modus der SCM-Überwachung werden persistierte
`ScmEventRepository`-Referenzen der Issue-Aufbereitung mit einer zum `Account`-Flag
konsistenten Viewer-Identität gespeichert, sodass Remote-Antwortaktionen
(`issueBodyUpdate`, `comment`) nicht mehr am `ViewerId`-Umleitungsschutz von
`GitHubPlugin.ExecuteResponseAsync` scheitern.

## Fachliche Anforderungen

1. **Snapshot-Korrektur beim Tagging:** In
   `ScmIssuePreparationMonitoringService.EvaluateIssueAsync` wird die per
   `with { Account = accountMode }` getaggte Repository-Referenz im
   `SeparateAccount`-Fall unter dem Ausführungskonto erneut aufgelöst
   (`source.GetEventRepositoryAsync` mit `accountMode`) und der vollständig
   konsistente Snapshot übernommen (`ViewerId`, `ViewerLogin`, `CanWrite`,
   kanonisierte `Owner`/`Name`, `OwnerId`, `NumericRepositoryId`). Die
   `RepositoryId` der Neuauflösung wird gegen die Entdeckungs-Auflösung
   abgeglichen — ein Mismatch ist ein ehrlicher Fehler (Issue-Auswertungs-
   Checkpoint), kein stilles Weitermachen.
2. **Entdeckung unverändert:** Die Repository-Enumeration der Aufbereitung bleibt
   unter `ScmAccountMode.UserAccount` (Notifications-Inbox des separaten Kontos
   kann die Enumeration nicht ersetzen).
3. **Schutz unverändert scharf:** `ExecuteResponseAsync` bleibt für echte
   Konto-/Repository-Wechsel blockierend (`RepositoryId`- + `ViewerId`-Vergleich);
   es wird keine Prüfung gelockert.
4. **Reconcile-Konsistenz:** `ReconcileResponseAsync`/`ReconcileReviewReactionAsync`
   erkennen vom Ausführungskonto geschriebene Kommentare/Reaktionen als eigene
   (kein Doppelpost bei Outbox-Reconcile-Retries) — implizit durch den
   konsistenten Snapshot, abgesichert durch Test.
5. **Benutzerkonto-Modus unverändert:** Bei `ScmAccountMode.UserAccount` ist die
   Neuauflösung identitätsgleich — keine Verhaltens- oder Transportänderung
   (Auflösungscache je Konto; im UserAccount-Fall darf kein zusätzlicher
   Transport-Call entstehen).
6. **Bestandsdaten (entschieden — Ops-Reparatur):** Für bereits persistierte
   gemischte Snapshots wird eine dokumentierte Vorgehensweise bereitgestellt
   (betroffene Ereignisse verwerfen, je-Issue-Auswertungs-Checkpoints
   `issueaufbereitung-issue/{provider}/{host}/{repoId}/{number}` zurücksetzen,
   Neueinreihung); kein Code-Reparaturmechanismus. Die Doku vermerkt, dass keine
   doppelten Remote-Aktionen für bereits manuell erledigte Änderungen entstehen
   dürfen (Marker-Idempotenz der Outbox bleibt die Schutzschicht).

## Akzeptanzszenarien

- **Happy Path SeparatesKonto:** Entdeckung unter UserAccount → Issue-Referenz
  wird unter SeparateAccount aufgelöst getaggt → `ObservationJson` enthält
  Viewer-Identität des separaten Kontos → Outbox-Ausführung
  (`ExecuteResponseAsync`) findet denselben `ViewerId` → `issueBodyUpdate`/
  `comment` werden remote ausgeführt statt mit `Permission` abgelehnt.
- **Happy Path UserAccount:** Verhalten bitgleich zu heute (kein zusätzlicher
  Provider-Aufruf, Snapshot unverändert).
- **Fehler: kein Zugriff des separaten Kontos:** Neuauflösung schlägt fehl →
  Fehler landet ehrlich am Issue-Auswertungs-Checkpoint (Backoff/Sperre wie bei
  anderen Auswertungsfehlern), nicht als späterer „Identität geändert"-Alarm.
- **Echter Mismatch:** Weicht die Neuauflösung in `RepositoryId` von der
  Entdeckungs-Auflösung ab, wird der Fehler sichtbar (nicht ignoriert).
- **Reconcile-Idempotenz:** Ein vom Ausführungskonto geschriebener Kommentar mit
  Marker wird vom Reconcile als eigen erkannt → kein zweiter POST.

## Explizite Nicht-Ziele

- Keine Änderung an `ExecuteResponseAsync`-Schutzlogik.
- Keine Änderung am regulären Ereignispfad (`ScmEventMonitoringService` — bereits
  konsistent).
- Kein Code-Reparaturmechanismus für Bestandsdaten.
- Keine Änderung an Geschlossen-Re-Checks (Schritt 2) oder Auftragsart
  (Schritt 4).

## Scope-Klärung

- Verarbeitungseinheit: Tagging-Punkt `EvaluateIssueAsync` (je Issue);
  Cache-Eigenschaften machen pro-Repository-Variante funktional äquivalent.
- Auslösendes Ereignis: Polling-Zyklus der Issue-Aufbereitung im
  SeparatesKonto-Modus.
- Erwartete Wirkung: Remote-Antwortaktionen der Aufbereitung laufen fehlerfrei;
  Fehlalarm „Repository- oder Benutzeridentität hat sich geändert." entfällt.
- **Scope-Ausschlüsse:** Keine.

## Offene Punkte

Keine.
