# Übersetzte Anforderung: Automatische Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen

## Fachliche Zusammenfassung

Die SCM-Ereignisüberwachung verarbeitet qualifizierte `ScmEventObservation`s (Trigger `$Softwareschmiede`, angemeldeter Benutzer = Repository-Owner = Bearbeiter der Textversion, keine eigene Antwort) künftig auch dann, wenn zur Referenz noch keine lokale `Aufgabe` existiert. Ein neuer servicenaher Auflösungsschritt „finde passende Aufgabe oder lege sie an" wirkt vor der Inbox-Aufnahme in `ScmEventStore.AcceptBatchAsync`: Bei genau einer passenden Aufgabe wird diese wiederverwendet, bei keiner wird sie aus den Ereignisdaten angelegt (Projekt aus `GitRepository.ProjektId`, Titel/Auftragstext aus Issue-/PR-Daten plus qualifiziertem Text mit Herkunftsverweis), bei mehreren bleibt die Mehrdeutigkeitsdiagnose bestehen. Die Invariante `ScmIncomingEvent.AufgabeId` nicht-nullable bleibt unverändert.

## Betroffene Klassen und Komponenten

- **Logikklassen / Services**
  - `ScmEventMonitoringService` (`src/Softwareschmiede/Application/Services/`) — Ruft bei `taskIds.Length == 0` und qualifizierter Beobachtung den Auflösungsschritt auf und übergibt das Ergebnis an `ScmEventStore.AcceptBatchAsync`.
  - **Neu:** servicenaber Auflösungsservice (Arbeitsname `ScmEventTaskResolver`) — Finden/Anlegen der Aufgabe zu einer `ScmEventReference` inkl. Dedup und Anlagerennen-Absicherung.
  - `ScmEventStore` — Aufnahmesemantik bleibt; der `unassigned`-Pfad gilt nur noch für unauflösbare/nicht qualifizierte Fälle.
  - `AufgabeService` — `CreateFromIssueAsync`/`CreateFromPullRequestAsync` werden wiederverwendet bzw. servicenah zugänglich gemacht (inkl. `BuildPullRequestAnforderungsBeschreibung`/`CleanPullRequestBody` und des `DbUpdateException`-Rennschutzes).
- **Interfaces**
  - `IScmEventSource` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/`) — Erweiterung um das Laden der Anlagedaten einer Referenz (Titel, Text, URL; bei PRs Quell-Branch-Daten), da `ScmEventObservation` keinen Titel und keine PR-Branch-Informationen trägt.
- **Datenmodellklassen**
  - `IssueReferenz` — voraussichtlich neuer deduplizierender `SourceKey` (kanonischer Referenzschlüssel) mit Unique-Index als Rennschutz analog zum `PullRequestReferenzen`-Index `(Provider, RepositoryId, PullRequestNumber)`; EF-Migration erforderlich.
- **Plugin**
  - `GitHubPlugin` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`) — Implementierung der neuen `IScmEventSource`-Methoden über die vorhandene `IGitHubEventApiClient`-/`GraphAsync`-Infrastruktur.
  - `ScmE2EApiClient` (`src/Softwareschmiede.App/Services/Testing/`) — Simulator-Antworten für die neuen Transportaufrufe (Enumeration + Referenzdetails), damit der E2E-Pfad die Anlage durchläuft.
- **Tests**
  - `ScmEventMonitoringServiceTests_*` (`src/Softwareschmiede.IntegrationTests/Services/`) — neue Themendatei für Anlage/Wiederverwendung/Mehrdeutigkeit/nicht qualifizierte Fälle.
  - `GitHubEventSourceTests` (`src/Softwareschmiede.Tests/Infrastructure/Plugins/`) — neue Methoden der Quelle.
  - `AufgabeServiceTests` — neuer Anlagebaustein / SourceKey-Dedup.
  - `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/`) — Mock-Defaults der erweiterten Schnittstelle.
  - `E2E_ScmEvents`/`ScmE2EFixture` — ein konsolidiertes E2E-Szenario für die automatische Anlage.

## Implementierungsansatz

- Auflösung erfolgreicher Referenzgruppen ohne Aufgabe (`taskIds.Length == 0`): Nur wenn der gelesene `ScmEventBatch` mindestens eine Beobachtung enthält, für die `ScmEventValidationService.GetRejection` `null` liefert, wird der Resolver aufgerufen; sonst bleibt der bisherige `UnassignedDiagnostic`-Pfad erhalten.
- Ergebnis der Auflösung: `Assigned(Guid)` → `AcceptBatchAsync(id, …, unassigned: false)`; `Ambiguous` → bisherige `AmbiguousAssignmentDiagnostic` (`aufgabeId: null, unassigned: false`); `Unassigned` → unveränderter `unassigned: true`-Pfad.
- Wiederverwendung: Issue über `IssueReferenz.IssueNummer` + Aufgaben-`GitRepository` (Menge der lokalen Repositories, die kanonisch auf denselben `ScmEventRepository`-Datensatz aufgelöst wurden); PR über `PullRequestReferenz`-Schlüssel `(Provider, RepositoryId, PullRequestNumber)` mit `PullRequestRepositoryId.Normalize(provider, "{Owner}/{Name}")`.
- Anlage: deterministische Wahl eines lokalen `GitRepository` (Projektzuordnung), Detaildaten über neue `IScmEventSource`-Methoden, Anlage über `AufgabeService`-Bausteine; `DbUpdateException`-Muster bei PRs bereits vorhanden, bei Issues über den neuen `SourceKey`-Unique-Index nachgebildet (bei Konflikt wird die vorhandene Aufgabe geliefert).
- Fehlgeschlagene Detail-/Anlageaufrufe laufen in den bestehenden `RecordFailureAsync`-Checkpoint-Pfad (Backoff, typisierte Providerfehler).
- Der automatische Start/Zustellung bleibt der bestehenden Dispatch-Pipeline überlassen (Folgeschritt 4 sichert dies End-to-End ab).

## Konfiguration

Keine neue Konfiguration erforderlich; Intervall/Backoff über `ScmEventOptions` unverändert.

## Offene Fragen

- Auswahl des lokalen `GitRepository`, wenn mehrere lokale Aliase auf dasselbe kanonische Repository auflösen (Vorschlag: deterministisch erstes Alias im Auflösungsdurchlauf).
- Verhalten, wenn PR-Anlagedaten nicht ermittelbar sind (z. B. gelöschter Fork ohne Quell-Referenz): als kontrollierter Quellfehler (`RecordFailureAsync`) statt stiller `Unassigned`-Diagnose.
