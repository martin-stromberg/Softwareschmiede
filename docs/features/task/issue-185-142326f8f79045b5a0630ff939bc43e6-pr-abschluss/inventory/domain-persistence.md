# Persistenz und Domain-Modell

## Aktueller Zustand

`Aufgabe` ist die zentrale Entity fuer Tasks. Sie enthaelt u. a. Projekt, optionales GitRepository, Status, BranchName, LokalerKlonPfad, IssueReferenz, AlertReferenz, Protokolleintraege und DiffResults.

`SoftwareschmiededDbContext` konfiguriert:

- `DbSet<Aufgabe>`
- `DbSet<IssueReferenz>`
- `DbSet<AlertReferenz>`
- `DbSet<Protokolleintrag>`
- `DbSet<DiffResult>` und zugehoerige Diff-Entities
- `DbSet<PluginKonfiguration>` und `DbSet<AppEinstellung>`

Es gibt noch keine PR- oder Workflow-Run-Entity.

## Bestehende Vergleichsmuster

`IssueReferenz` und `AlertReferenz` sind gute Orientierung fuer eine Aufgabe-gebundene Provider-Referenz. `AlertReferenz` zeigt zusaetzlich eindeutige SourceKey- und Provider-Felder mit Indexen.

`DiffResult` zeigt das bestehende Muster fuer eine Aufgabe-gebundene Hauptentity mit Kind-Entities und mehreren Indexen.

## Fehlende Modellierung

Fuer die Anforderung werden neue Domain-Entities benoetigt, voraussichtlich:

- `PullRequestReferenz` oder `AufgabePullRequest`
- `PullRequestActionStatus` oder `PullRequestWorkflowRun`

Mindestfelder Pull Request:

- `Id`
- `AufgabeId`
- `Provider` (`GitHub`)
- `RepositoryId` oder `RepositoryUrl`
- `PullRequestNumber`
- `ProviderPullRequestId` optional
- `Url`
- `Titel`
- `SourceBranch`
- `TargetBranch`
- `HeadSha`
- `MergeCommitSha`
- `Status`
- `MergeStatus`
- `MonitoringPhase`
- `LastCheckedUtc`
- `LastError`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Mindestfelder Workflow/Action:

- `Id`
- `PullRequestReferenzId`
- `ProviderRunId`
- `WorkflowName`
- `CheckName` optional
- `HeadSha`
- `EventName`
- `Status`
- `Conclusion`
- `Url`
- `StartedAtUtc`
- `CompletedAtUtc`
- `LastCheckedUtc`
- `LastError`

## EF-Core-Aenderungspunkte

- `Aufgabe` um `List<PullRequestReferenz>` erweitern.
- `SoftwareschmiededDbContext` um `DbSet` fuer PRs und Workflow-Runs erweitern.
- `OnModelCreating` mit string-Konvertierungen fuer Enums, DateTimeOffset-Konvertierungen und Cascade Delete konfigurieren.
- Indizes fuer `AufgabeId`, `Provider + RepositoryId + PullRequestNumber`, `MonitoringPhase`, `LastCheckedUtc`, `ProviderRunId` anlegen.
- Neue EF-Migration unter `src/Softwareschmiede/Migrations/` erstellen.

## Services

Der bestehende `AufgabeService.GetDetailAsync` sollte PR-Navigationen einschliessen, wenn die UI direkt aus `Aufgabe` lesen soll. Alternativ ist ein dedizierter Service besser begrenzt:

- `PullRequestReferenzService` fuer Persistenz und Abfragen je Aufgabe.
- `PullRequestMonitoringService` fuer Aktualisierung und automatische Folgeaktionen.

Ein dedizierter Service reduziert das Risiko, `AufgabeService` weiter aufzublasen.

## Tests

Vorhandene passende Testbasis:

- `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs` nutzt EF InMemory.
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProtokollServiceTests.cs`

Neue Tests sollten eindeutige PR-Zuordnung, Cascade Delete, Aktualisierung von Statusdaten und mehrere PRs pro Aufgabe abdecken.

