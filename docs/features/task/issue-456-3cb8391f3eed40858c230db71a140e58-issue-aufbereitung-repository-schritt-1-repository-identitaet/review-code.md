# Code Review: Konsistente SCM-Repository-Identität (Schritt 1)

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/ScmIssuePreparationMonitoringService.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmIssuePreparationMonitoringServiceTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`
- `docs/help/plugins/github-ereignisse/business-rules.md`
- `docs/help/plugins/github-ereignisse/troubleshooting.md`
- `docs/RELEASE_NOTES.md`

## Befund

- `EvaluateIssueAsync` löst das Repository im Modus `SeparatesKonto` jetzt unter dem
  Ausführungskonto neu auf (`ScmRepositoryContext` mit `Account = accountMode`), bevor
  die Beschreibung gelesen und `DetectAndEnqueueAsync` aufgerufen wird. Der neu
  aufgelöste Snapshot trägt `Account` bereits; das folgende `with { Account = accountMode }`
  ist für beide Pfade identisch und damit harmlos.
- `RepositoryId`-Abgleich zwischen Entdeckungs- und Ausführungskonto-Auflösung:
  Mismatch → `ScmProviderException(Permanent)` am Issue-Checkpoint — kein Beschreibungs-Read,
  kein Ereignis, kein späterer „Identität geändert"-Alarm.
- Fehlschlag der Neuauflösung (z. B. fehlender Bot-Zugriff) läuft in den bestehenden
  Issue-Fehlerpfad (`RecordFailureAsync`) — ehrlicher, typisierter Fehler statt
  stillem Weiterlauf mit inkonsistentem Snapshot.
- `localRepositories` ist an dieser Stelle immer nicht-leer (Liste der kanonischen
  Alias-Gruppe, nur mit ≥1 Kandidaten erreichbar); `localRepositories[0].Id` liefert
  eine lokale GitRepository-ID desselben Alias-Verbunds — ausreichend als Kontext-Anker,
  die Auflösung selbst wird über Provider-Prefix + kanonische Alias-URL gesteuert.
- Der Umleitungsschutz `ExecuteResponseAsync` (`RepositoryId` + `ViewerId`-Vergleich)
  bleibt unverändert scharf — nicht abgeschwächt.
- Transportkosten der Neuauflösung: Der Auflösungscache ist je Konto/Client getrennt;
  die SeparatesKonto-Auflösung ist ein einmaliger GraphQL-Call je TTL-Fenster, der
  danach ohnehin für alle Ereigniszugriffe des separaten Kontos benötigt wird.
- UserAccount-Modus: kein zusätzlicher Auflösungs-Call (Test verifiziert `Times.Never`
  für Nicht-UserAccount-Kontexte) — Verhalten und Transportaufrufe unverändert.
- Ops-Reparatur für bereits persistierte inkonsistente `ObservationJson` ist als
  dokumentiertes Verfahren umgesetzt (Ereignis abbrechen, optional
  `issueaufbereitung-issue/`-Checkpoint löschen), inklusive Warnung vor doppelten
  Remote-Kommentaren bei teilausgeführten Alt-Ereignissen — kein automatischer
  Reparaturmechanismus (produktseitig so entschieden).

## Offene Punkte

- Keine.
