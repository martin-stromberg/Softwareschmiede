# Bestandsaufnahme: Issue-Aufbereitung im Überwachungsmodus „Separates Konto“

Bestandsaufnahme des SCM-Ereignis-/Issue-Aufbereitungs-Bereichs bezogen auf die Anforderung in
`requirement.md`: Die „Issue erstellt“-Erkennung soll modusunabhängig über einen eigenständigen
Entdeckungsdienst (Vorbild `ScmBackmergeMonitoringService`) laufen, statt nur am
repository-weiten Enumerationskanal des `ScmEventMonitoringService`.

## Zusammenfassung

- **Anlagepfad vollständig vorhanden:** `ScmIssueAufbereitungsService.ErkennenUndEinreihenAsync`
  deckt Baseline (`issueaufbereitung/{provider}/{host}/{repoId}`), `CreatedAt`-Filter,
  Erstbeobachtung per `ScmContentSnapshot`, `EventKey`-/`SourceKey`-Dedup, `Zweck`-Aufgabe und
  internes `ScmIncomingEvent` ab — der einzige Anlagepfad des Systems.
- **Vorbild-Dienst vorhanden:** `ScmBackmergeMonitoringService` ist ein gehosteter
  `PeriodicBackgroundService` mit exakt dem geforderten Muster: eigener `RunOnceAsync`-Zyklus,
  Laufzeit-Schalter, kanonische Alias-Auflösung unter `ScmKontoModus.Benutzerkonto`, eigene
  Checkpoint-Namensräume (`backmerge-repository/`, `backmerge-enumeration/`) und
  `retryPermanent`-Fehlerisolation je Repository.
- **Checkpoint-Infrastruktur wiederverwendbar:** `ScmCheckpointSupport.EnumerateRepositoryAsync`
  nimmt das `checkpointKeyPrefix` als Parameter entgegen; `RecordFailureAsync` unterstützt
  `retryPermanent`. Keine Schema-Änderung nötig — `ScmObservationCheckpoint.SourceKey` ist
  frei belegbar.
- **Aktueller Engpass bestätigt:** `ErkennenUndEinreihenAsync` wird im
  `ScmEventMonitoringService.RunOnceAsync` nur unter `!inboxMode` aufgerufen
  (`ScmEventMonitoringService.cs:201-206`). Im `SeparatesKonto` (`scm.monitoring.accountmode = 1`)
  ersetzt die Notifications-Inbox die Enumeration komplett — die Aufbereitung wird dort nie
  ausgelöst.
- **Dedup-Schichten vorhanden:** Unique-Index auf `ScmIncomingEvent.EventKey` und
  `IssueReferenz.SourceKey`, `ScmContentSnapshot`-Erstbeobachtungsprüfung und
  `DbUpdateException`-Anlagerennen-Behandlung in `CreateFromIssueEventAsync` und
  `ErkennenUndEinreihenAsync` — Doppelanlagen aus zwei parallelen Erkennungspfaden sind bereits
  abgesichert. `ContentKey`/`EventKey`/`SourceKey` enthalten kein Konto (konto-agnostisch).
- **Laufzeit-Schalter vorhanden:** `scm.issueaufbereitung.enabled` (Standard `false`, opt-in),
  Repository-Flag `GitRepository.IssueAufbereitungAktiv`, `scm.issueaufbereitung.plugin`,
  `scm.issueaufbereitung.comment.enabled`, `scm.monitoring.accountmode` — alle pro Zyklus lesbar.
- **Ausführungspfad vorhanden und modusgebunden:** `ScmEventOrchestrator.DispatchAufbereitungAsync`
  verarbeitet `Zweck`-Ereignisse dediziert (Prompt, CLI-Ausführung, `IssueBodyUpdate`/Kommentar
  über die Outbox); bleibt laut Anforderung unverändert.
- **Fehlt:** der neue gehostete Dienst `ScmIssueAufbereitungMonitoringService` samt
  Checkpoint-Namensräumen `issueaufbereitung-repository/` und `issueaufbereitung-enumeration/`
  (sowie ggf. `issueaufbereitung-issue/`), dessen DI-Registrierung (`App.xaml.cs`,
  `ScmDispatchFixture`) und Testabdeckung.
- **UI-Anstoß vorhanden:** `ScmEventQueueViewModel.RetrySourcesCommand` („Quellen erneut
  prüfen“) ruft bereits `ScmEventMonitoringService` und `ScmBackmergeMonitoringService` per
  `RunOnceAsync` auf — Erweiterungspunkt nach Backmerge-Präzedenz.
- **Test-Infrastruktur vorhanden:** `ScmDispatchFixture` registriert alle Monitoring-Dienste,
  mockt `IScmEventSource`/`IScmNotificationSource`/`IScmEventResponder` vollständig; es existieren
  bereits `ScmBackmergeMonitoringServiceTests` (Referenzmuster),
  `ScmEventMonitoringServiceTests_IssueAufbereitung` (Erkennung) und
  `ScmEventDispatchTests_IssueAufbereitung` (Ausführung).

## Details

- [Datenmodell und Value Objects](inventory/models.md)
- [Logik und Dienste](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
