# Bestandsaufnahme: SCM-Ereignis an aktive Aufgabe desselben Kontexts zustellen statt Doppelaufgabe

Analysiert wurde die SCM-Ereignisverarbeitung (Aufgabenauflösung, Monitoring, Dispatch) bezogen auf die Anforderung, ein Pull-Request-Ereignis einer bereits aktiven Aufgabe desselben Kontexts zuzuordnen, statt eine zweite `ReviewSource`-Aufgabe anzulegen.

## Zusammenfassung

- **Aufgabenauflösung ist ausschließlich referenzbasiert:** `ScmEventTaskResolver.ResolvePullRequestAsync` sucht nur über persistierte `PullRequestReferenz`-Datensätze (Provider + normalisierte `RepositoryId` + `PullRequestNumber`); `ResolveIssueAsync` nur über `IssueReferenz.IssueNummer` im Repository-Scope. Kontextsignale wie `Aufgabe.BranchName`, PR-Head-Branch oder eine `Closes #N`-Direktive im PR-Body werden nirgends ausgewertet — bei `taskIds.Count == 0` wird sofort `CreateFromPullRequestAsync` aufgerufen.
- **Die `linked`-Liste im Monitoring hat dieselbe Lücke:** `ScmEventMonitoringService` (Zeilen 52–59) ordnet Aufgaben nur über `IssueReferenz.IssueNummer` und `task.PullRequests` zu; ein enumerierter PR wird einer laufenden Aufgabe ohne persistierte Referenz nie zugeordnet.
- **Der Verknüpfungszeitpunkt ist die Ursache der Lücke:** Eine `PullRequestReferenz` entsteht erst bei hostseitiger PR-Anlage (`GitOrchestrationService.PullRequestErstellenAsync` → `PullRequestReferenzService.SaveCreatedAsync`) oder beim Abschluss-Report der CLI (`complete.json` → `ScmEventOrchestrator.LinkReportedPullRequestAsync` → `SaveReportedAsync`). Ein von der CLI selbst angelegter PR ist solange unbekannt, wie die Aufgabe noch läuft.
- **Alle benötigten Kontextsignale existieren bereits im Datenmodell:** `Aufgabe.BranchName` (Branch enthält nach `EntwicklungsprozessService.ErstelleTaskBranchName` sogar `task/issue-{Nr}-{AufgabeId:N}-{Slug}`), `IssueReferenz.IssueNummer`, `PullRequest.BranchName`/`Body` (via `IScmEventSource.ReadPullRequestAsync`), `PullRequestReferenz.SourceBranch`. Kein Schema-Eingriff ersichtlich.
- **„Aktiv" hat zwei bestehende, unterschiedliche Definitionen:** `AufgabeStatusExtensions.AktivOderWartendStatus` = `[Gestartet, Wartend]` (ohne `Neu`), während `ScmEventOrchestrator.BlockReason` nur `Beendet`/`Archiviert` (plus `PausiertBisUtc` in der Zukunft) als Startsperre behandelt. `ScmTaskCompletionMonitoringService` nutzt ebenfalls `Status != Beendet && != Archiviert` als Aktivitätskriterium.
- **Ergebnissemantik und Race-Schutz sind vorhanden:** `ScmTaskResolution` kennt `Assigned`/`Ambiguous`/`Unassigned`; die Mehrdeutigkeitsdiagnose liegt als `ScmEventStore.AmbiguousAssignmentDiagnostic` vor. Gegen Anlagerennen sichern der Unique-Index `(Provider, RepositoryId, PullRequestNumber)` auf `PullRequestReferenz`, der Unique-Index auf `IssueReferenz.SourceKey` und der Retry-Pfad in `ResolvePullRequestAsync` (Zeilen 99–105) ab.
- **Dispatch-Pipeline benötigt keine Erweiterung:** Sobald `ScmIncomingEvent.AufgabeId` auf die laufende Aufgabe zeigt, laufen Zustellung, Wartelogik bei beschäftigter CLI (`WaitForInputReadyAsync`, „CLI beschäftigt; wartet auf Eingabebereitschaft."), Sequentialisierung (`ScmTaskCoordinator`, `Sequence`-Ordnung) und Pausenbehandlung (`BlockReason`) unverändert.
- **Testbasis existiert:** `ScmEventMonitoringServiceTests_TaskAssignment` deckt Anlage, Wiederverwendung und Unassigned-Fälle ab; `ScmDispatchFixture` liefert gemockte `IScmEventSource`-Defaults (u. a. `ReadPullRequestAsync` → `null`), registriert aber noch keinen Zustand „laufende Aufgabe mit Branch ohne PR-Referenz".

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
