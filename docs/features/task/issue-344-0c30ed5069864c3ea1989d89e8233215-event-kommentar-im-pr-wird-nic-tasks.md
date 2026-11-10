# Tasks: SCM-Ereignis an aktive Aufgabe desselben Kontexts zustellen statt Doppelaufgabe

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `PullRequestBodyBuilder.ExtractClosingIssueNumbers` anlegen (Closing-Direktiven → Issue-Nummern, optionaler `repositoryId`-Qualifizierer-Filter) | Offen | — |
| 2 | Logik | `PullRequestBodyBuilder.ContainsClosingDirectiveForIssue` auf `ExtractClosingIssueNumbers` umstellen (Verhalten identisch) | Offen | — |
| 3 | Logik | `ScmEventTaskResolver.FindActiveContextTaskIdsAsync` anlegen (aktive Aufgaben `Status ∉ {Beendet, Archiviert}` im `repositories`-Scope inkl. `IssueReferenz`, Vereinigung Branch-/Issue-Match) | Offen | — |
| 4 | Logik | Private Match-Hilfen `MatchesBranch` (Same-Repository + `BranchName`-Vergleich) und `MatchesClosingIssue` in `ScmEventTaskResolver` anlegen | Offen | — |
| 5 | Logik | Kontextsuche in `ScmEventTaskResolver.ResolvePullRequestAsync` zwischen `PullRequestTaskIdsAsync`-Lookup und `CreateFromPullRequestAsync` einschieben (`>1` → `Ambiguous`, `==1` → `Assigned`, `0` → bisherige Anlage) | Offen | — |
| 6 | Tests | Hilfsmethode `AddTaskAsync` in `ScmEventMonitoringServiceTests_TaskAssignment` anlegen (`Aufgabe` mit `Status`, `BranchName`, `PausiertBisUtc`, optionaler `IssueReferenz`) | Offen | — |
| 7 | Tests | `ConfigurePullRequest` in `ScmEventMonitoringServiceTests_TaskAssignment` um optionale `branchName`-/`body`-/`sourceRepositoryId`-Parameter erweitern | Offen | — |
| 8 | Tests | Integrationstest `Monitor_PullRequestOnActiveTaskBranch_AssignsWithoutSecondCreation` schreiben | Offen | — |
| 9 | Tests | Integrationstest `Monitor_PullRequestClosingActiveTaskIssue_AssignsWithoutSecondCreation` schreiben | Offen | — |
| 10 | Tests | Integrationstest `Monitor_PullRequestBranchOfEndedTask_CreatesReviewSourceTask` schreiben | Offen | — |
| 11 | Tests | Integrationstest `Monitor_PullRequestConflictingContextSignals_ReportsAmbiguityWithoutTask` schreiben | Offen | — |
| 12 | Tests | Integrationstest `Monitor_ForkedPullRequestWithSameBranch_CreatesReviewSourceTask` schreiben | Offen | — |
| 13 | Tests | Integrationstest `Monitor_PullRequestOnPausedTaskBranch_AssignsAndWaits` schreiben | Offen | — |
| 14 | Tests | Unit-Testklasse `PullRequestBodyBuilderTests` mit `ExtractClosingIssueNumbers`-Fällen anlegen | Offen | — |
| 15 | Verifikation | `dotnet build` + stabile Testlane (`--filter "Category!=OsInterface"`) + `src/Softwareschmiede.IntegrationTests` ausführen (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, synchron) | Offen | — |
