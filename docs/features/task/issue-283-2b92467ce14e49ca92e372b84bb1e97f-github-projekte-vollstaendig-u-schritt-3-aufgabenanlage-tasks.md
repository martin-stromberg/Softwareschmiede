# Tasks: Automatische Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `IssueReferenz.SourceKey` (string?, max 700) anlegen | Erledigt | Integrationstests/Unit-Tests grün |
| 2 | Datenmodell | Unique-Index auf `IssueReferenzen.SourceKey` in `SoftwareschmiededDbContext` konfigurieren | Erledigt | Integrationstests/Unit-Tests grün |
| 3 | Datenmodell | EF-Migration `AddIssueReferenzSourceKey` generieren | Erledigt | Integrationstests/Unit-Tests grün |
| 4 | Vertrag | `IScmEventSource.ReadIssueAsync` deklarieren | Erledigt | Integrationstests/Unit-Tests grün |
| 5 | Vertrag | `IScmEventSource.ReadPullRequestAsync` deklarieren | Erledigt | Integrationstests/Unit-Tests grün |
| 6 | Plugin | `GitHubPlugin.ReadIssueAsync` (REST `issues/{n}`, PR-Guard → null) implementieren | Erledigt | Integrationstests/Unit-Tests grün |
| 7 | Plugin | `GitHubPlugin.ReadPullRequestAsync` (REST `pulls/{n}`, Parser aus `AddOpenPullRequest` extrahieren) implementieren | Erledigt | Integrationstests/Unit-Tests grün |
| 8 | Logik | `ScmTaskResolution`/`ScmTaskResolutionKind` anlegen | Erledigt | Integrationstests/Unit-Tests grün |
| 9 | Logik | `ScmEventTaskResolver.ResolveAsync` (Details → Finden → Anlegen; Compose-Body; Rennschutz) implementieren | Erledigt | Integrationstests/Unit-Tests grün |
| 10 | Logik | `AufgabeService.CreateFromIssueEventAsync` mit SourceKey-Dedup + `DbUpdateException`-Muster | Erledigt | Integrationstests/Unit-Tests grün |
| 11 | Logik | `ScmEventMonitoringService`: `localRepositories`-Map + Resolver-Aufruf bei unzugeordneter qualifizierter Beobachtung | Erledigt | Integrationstests/Unit-Tests grün |
| 12 | Infrastruktur | `ScmEventTaskResolver` in `App.xaml.cs` und `ScmDispatchFixture` registrieren | Erledigt | Integrationstests/Unit-Tests grün |
| 13 | Tests | `ScmDispatchFixture`: Mock-Defaults `ReadIssueAsync`/`ReadPullRequestAsync` | Erledigt | Integrationstests/Unit-Tests grün |
| 14 | Tests | `ScmEventMonitoringServiceTests_TaskAssignment` (Anlage Issue/PR, Wiederverwendung, Dedup, nicht qualifiziert, Fehler, verschwundene Referenz) | Erledigt | Integrationstests/Unit-Tests grün |
| 15 | Tests | `AufgabeServiceTests`: `CreateFromIssueEventAsync` Dedup-Verhalten | Erledigt | Integrationstests/Unit-Tests grün |
| 16 | Tests | `GitHubEventSourceTests`: `ReadIssueAsync`/`ReadPullRequestAsync` | Erledigt | Integrationstests/Unit-Tests grün |
| 17 | Tests | `ScmEventMonitoringServiceTests_RepoWide` an neues Verhalten anpassen | Erledigt | Integrationstests/Unit-Tests grün |
| 18 | Tests | Migrations-Test für `AddIssueReferenzSourceKey` | Erledigt | Integrationstests/Unit-Tests grün |
| 19 | E2E | `ScmE2EApiClient`: Enumeration + Detail-Endpunkte + `title` beantworten; `ScmE2EText.Titel` | Erledigt | Integrationstests/Unit-Tests grün |
| 20 | E2E | E2E-Szenario automatische Aufgabenanlage in konsolidierte `ScmEventsE2ETests`-Methode integrieren | Erledigt | Integrationstests/Unit-Tests grün |
| 21 | Doku | `docs/help/plugins/github-ereignisse/` aktualisieren | Erledigt | Integrationstests/Unit-Tests grün |
| 22 | Doku | `changes.log`-Eintrag | Erledigt | Integrationstests/Unit-Tests grün |
| 23 | Doku | `README.md` prüfen/aktualisieren | Erledigt | Integrationstests/Unit-Tests grün |
