# Tasks: Automatische Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `IssueReferenz.SourceKey` (string?, max 700) anlegen | Offen | — |
| 2 | Datenmodell | Unique-Index auf `IssueReferenzen.SourceKey` in `SoftwareschmiededDbContext` konfigurieren | Offen | — |
| 3 | Datenmodell | EF-Migration `AddIssueReferenzSourceKey` generieren | Offen | — |
| 4 | Vertrag | `IScmEventSource.ReadIssueAsync` deklarieren | Offen | — |
| 5 | Vertrag | `IScmEventSource.ReadPullRequestAsync` deklarieren | Offen | — |
| 6 | Plugin | `GitHubPlugin.ReadIssueAsync` (REST `issues/{n}`, PR-Guard → null) implementieren | Offen | — |
| 7 | Plugin | `GitHubPlugin.ReadPullRequestAsync` (REST `pulls/{n}`, Parser aus `AddOpenPullRequest` extrahieren) implementieren | Offen | — |
| 8 | Logik | `ScmTaskResolution`/`ScmTaskResolutionKind` anlegen | Offen | — |
| 9 | Logik | `ScmEventTaskResolver.ResolveAsync` (Details → Finden → Anlegen; Compose-Body; Rennschutz) implementieren | Offen | — |
| 10 | Logik | `AufgabeService.CreateFromIssueEventAsync` mit SourceKey-Dedup + `DbUpdateException`-Muster | Offen | — |
| 11 | Logik | `ScmEventMonitoringService`: `localRepositories`-Map + Resolver-Aufruf bei unzugeordneter qualifizierter Beobachtung | Offen | — |
| 12 | Infrastruktur | `ScmEventTaskResolver` in `App.xaml.cs` und `ScmDispatchFixture` registrieren | Offen | — |
| 13 | Tests | `ScmDispatchFixture`: Mock-Defaults `ReadIssueAsync`/`ReadPullRequestAsync` | Offen | — |
| 14 | Tests | `ScmEventMonitoringServiceTests_TaskAssignment` (Anlage Issue/PR, Wiederverwendung, Dedup, nicht qualifiziert, Fehler, verschwundene Referenz) | Offen | — |
| 15 | Tests | `AufgabeServiceTests`: `CreateFromIssueEventAsync` Dedup-Verhalten | Offen | — |
| 16 | Tests | `GitHubEventSourceTests`: `ReadIssueAsync`/`ReadPullRequestAsync` | Offen | — |
| 17 | Tests | `ScmEventMonitoringServiceTests_RepoWide` an neues Verhalten anpassen | Offen | — |
| 18 | Tests | Migrations-Test für `AddIssueReferenzSourceKey` | Offen | — |
| 19 | E2E | `ScmE2EApiClient`: Enumeration + Detail-Endpunkte + `title` beantworten; `ScmE2EText.Titel` | Offen | — |
| 20 | E2E | E2E-Szenario automatische Aufgabenanlage in konsolidierte `ScmEventsE2ETests`-Methode integrieren | Offen | — |
| 21 | Doku | `docs/help/plugins/github-ereignisse/` aktualisieren | Offen | — |
| 22 | Doku | `changes.log`-Eintrag | Offen | — |
| 23 | Doku | `README.md` prüfen/aktualisieren | Offen | — |
