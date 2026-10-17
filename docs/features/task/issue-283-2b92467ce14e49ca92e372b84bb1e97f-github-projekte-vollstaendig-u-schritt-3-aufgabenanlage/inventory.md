# Bestandsaufnahme: Automatische Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen

Analyse des SCM-Ereignis-Überwachungs- und Aufgabenanlage-Bereichs bezogen auf die Anforderung, qualifizierte Ereignisse ohne bestehende lokale Aufgabe wirksam zu machen. Stand: Schritt-2-Codebasis (repo-weite Erkennung bereits umgesetzt).

## Zusammenfassung

- `ScmEventMonitoringService` enumeriert seit Schritt 2 alle Referenzen aktiver `GitRepositories`; Referenzen ohne Aufgabe erhalten über `AcceptBatchAsync(aufgabeId: null, …, unassigned: true)` Snapshots mit `ScmEventStore.UnassignedDiagnostic` und `IdentityResolved = false` (Vollscan beim Folgepoll). Genau dieser Pfad wird durch die Anforderung wirksam.
- `ScmEventValidationService.GetRejection` ist statisch und liefert die Qualifizierungsregeln (Trigger, Owner = Viewer = Bearbeiter, keine eigene Antwort); bei `aufgabeId != null` wird sie in `AcceptBatchAsync` bereits pro Beobachtung angewendet.
- Es existiert **kein** servicenaher „finde oder lege Aufgabe"-Pfad: `AufgabeService.CreateFromIssueAsync`/`CreateFromPullRequestAsync` werden ausschließlich aus `ProjectDetailViewModel` (UI) aufgerufen.
- `ScmEventObservation` trägt `Body` und `Url`, aber **keinen Titel** und keine PR-Branch-Daten — für die Anlage reichen die Beobachtungsdaten allein nicht aus; `IScmEventSource` hat bisher nur `GetEventRepositoryAsync`, `ReadEventsAsync`, `EnumerateReferencesAsync`.
- Rennschutz: `PullRequestReferenzen` besitzt den Unique-Index `(Provider, RepositoryId, PullRequestNumber)`, `CreateFromPullRequestAsync` nutzt das `DbUpdateException`-Muster. `IssueReferenzen` hat **keinen** deduplizierenden Index und kein Repository-Feld — dort fehlt die DB-seitige Absicherung.
- `ScmIncomingEvent.AufgabeId` ist `Guid` (nicht nullable); `aufgabeId == null` in `AcceptBatchAsync` dient als Marker für Mehrdeutigkeit (`unassigned: false`) bzw. Nicht-Zuordnung (`unassigned: true`).
- Der Dispatch (`ScmEventOrchestrator.DispatchAsync`) startet `AufgabeStatus.Neu`-Aufgaben bereits (`SetStatusAsync(Gestartet)`, Clone/CLI über `EntwicklungsprozessService`) — die Anlage muss nichts davon selbst tun.

## Details

- [Logik / Services](inventory/logic.md)
- [Datenmodell](inventory/models.md)
- [Interfaces und Wertobjekte](inventory/interfaces.md)
- [Plugin-Implementierung GitHub](inventory/github-plugin.md)
- [Tests und Testinfrastruktur](inventory/tests.md)
