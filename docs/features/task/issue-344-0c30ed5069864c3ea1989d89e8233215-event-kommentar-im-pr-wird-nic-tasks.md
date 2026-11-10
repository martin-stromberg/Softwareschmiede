# Tasks: SCM-Ereignis an aktive Aufgabe desselben Kontexts zustellen statt Doppelaufgabe

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `PullRequestBodyBuilder.ExtractClosingIssueNumbers` anlegen (Closing-Direktiven → Issue-Nummern, optionaler `repositoryId`-Qualifizierer-Filter) | Erledigt | `PullRequestBodyBuilderTests.ExtractClosingIssueNumbers_*` (21 Fälle, grün) |
| 2 | Logik | `PullRequestBodyBuilder.ContainsClosingDirectiveForIssue` auf `ExtractClosingIssueNumbers` umstellen (Verhalten identisch) | Erledigt | `GitOrchestrationServiceTests.PullRequestErstellenAsync_*` (Closing-Direktiven-Pfade über `Build`, grün in stabiler Lane) |
| 3 | Logik | `ScmEventTaskResolver.FindActiveContextTaskIdsAsync` anlegen (aktive Aufgaben `Status ∉ {Beendet, Archiviert}` im `repositories`-Scope inkl. `IssueReferenz`, Vereinigung Branch-/Issue-Match) | Erledigt | `Monitor_PullRequestOnActiveTaskBranch_AssignsWithoutSecondCreation`, `Monitor_PullRequestBranchOfEndedTask_CreatesReviewSourceTask` |
| 4 | Logik | Private Match-Hilfen `MatchesBranch` (Same-Repository + `BranchName`-Vergleich) und `MatchesClosingIssue` in `ScmEventTaskResolver` anlegen | Erledigt | `Monitor_ForkedPullRequestWithSameBranch_CreatesReviewSourceTask` (Branch-Match), `Monitor_PullRequestClosingActiveTaskIssue_AssignsWithoutSecondCreation` (Issue-Match) |
| 5 | Logik | Kontextsuche in `ScmEventTaskResolver.ResolvePullRequestAsync` zwischen `PullRequestTaskIdsAsync`-Lookup und `CreateFromPullRequestAsync` einschieben (`>1` → `Ambiguous`, `==1` → `Assigned`, `0` → bisherige Anlage) | Erledigt | Alle sechs neuen `Monitor_*`-Tests in `ScmEventMonitoringServiceTests_TaskAssignment` (Zuordnung, Ambiguous, Zweitanlage-Pfade) |
| 6 | Tests | Hilfsmethode `AddTaskAsync` in `ScmEventMonitoringServiceTests_TaskAssignment` anlegen (`Aufgabe` mit `Status`, `BranchName`, `PausiertBisUtc`, optionaler `IssueReferenz`) | Erledigt | Wird von allen sechs neuen `Monitor_*`-Tests verwendet (grün) |
| 7 | Tests | `ConfigurePullRequest` in `ScmEventMonitoringServiceTests_TaskAssignment` um optionale `branchName`-/`body`-/`sourceRepositoryId`-Parameter erweitern | Erledigt | `Monitor_ForkedPullRequestWithSameBranch_CreatesReviewSourceTask` (`sourceRepositoryId`), `Monitor_PullRequestClosingActiveTaskIssue_*` (`body`) |
| 8 | Tests | Integrationstest `Monitor_PullRequestOnActiveTaskBranch_AssignsWithoutSecondCreation` schreiben | Erledigt | Test selbst, grün |
| 9 | Tests | Integrationstest `Monitor_PullRequestClosingActiveTaskIssue_AssignsWithoutSecondCreation` schreiben | Erledigt | Test selbst, grün |
| 10 | Tests | Integrationstest `Monitor_PullRequestBranchOfEndedTask_CreatesReviewSourceTask` schreiben | Erledigt | Test selbst, grün |
| 11 | Tests | Integrationstest `Monitor_PullRequestConflictingContextSignals_ReportsAmbiguityWithoutTask` schreiben | Erledigt | Test selbst, grün |
| 12 | Tests | Integrationstest `Monitor_ForkedPullRequestWithSameBranch_CreatesReviewSourceTask` schreiben | Erledigt | Test selbst, grün |
| 13 | Tests | Integrationstest `Monitor_PullRequestOnPausedTaskBranch_AssignsAndWaits` schreiben | Erledigt | Test selbst, grün |
| 14 | Tests | Unit-Testklasse `PullRequestBodyBuilderTests` mit `ExtractClosingIssueNumbers`-Fällen anlegen | Erledigt | `PullRequestBodyBuilderTests` (21 Fälle, grün) |
| 15 | Verifikation | `dotnet build` + stabile Testlane (`--filter "Category!=OsInterface"`) + `src/Softwareschmiede.IntegrationTests` ausführen (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, synchron) | Erledigt | Build beider Testprojekte 0 Fehler/0 Warnungen; stabile Lane: 2245 bestanden, 1 übersprungen, 0 fehlgeschlagen; IntegrationTests: 272/272 bestanden |
