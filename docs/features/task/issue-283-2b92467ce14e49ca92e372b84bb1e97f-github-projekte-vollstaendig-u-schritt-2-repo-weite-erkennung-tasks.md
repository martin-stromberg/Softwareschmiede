# Tasks: Repository-weite GitHub-Ereigniserkennung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Plugin-Vertrag | `IScmEventSource.EnumerateReferencesAsync` deklarieren (Signatur, XML-Doku) | Erledigt | `GitHubEventSourceTests.Enumeration_ReturnsOpenReferencesAcrossPages` |
| 2 | Plugin-Vertrag | `GitHubPlugin.EnumerateReferencesAsync` implementieren (offene Issues/PRs, geschlossene im `sinceUtc`-Fenster, Pagination, typisierte Fehler) | Erledigt | `GitHubEventSourceTests.Enumeration_SinceWindow_AddsOnlyRecentlyUpdatedClosedReferences`, `Enumeration_Failure_RemainsTypedProviderError` |
| 3 | Logik | `ScmEventStore.AcceptBatchAsync` um `unassigned`-Parameter und Diagnostic-Konstanten erweitern | Erledigt | `ScmEventMonitoringServiceTests_RepoWide.Monitor_ActiveRepositoryWithoutTask_EnumeratesAndStoresSnapshotsWithoutInbox` |
| 4 | Logik | `ScmEventMonitoringService.RunOnceAsync`: `db.GitRepositories` (Aktiv) laden und in Alias-Gruppierung einbeziehen | Erledigt | `ScmEventMonitoringServiceTests_RepoWide.Monitor_InactiveOrNonGitHubRepositories_AreNotEnumerated` |
| 5 | Logik | `ScmEventMonitoringService.RunOnceAsync`: Enumeration pro kanonischem Repo mit `enumeration/`-Checkpoint, `resolved` auf `Guid?`-TaskIds, `unassigned` an `AcceptBatchAsync` | Erledigt | `ScmEventMonitoringServiceTests_RepoWide.Monitor_EnumerationFailure_UsesBackoffWithoutBlockingLinkedReferences`, `Monitor_EnumeratedReferenceWithTask_EntersInboxUnchanged` |
| 6 | Testinfrastruktur | `ScmDispatchFixture`: `EnumerateReferencesAsync`-Default-Mock ergänzen | Erledigt | alle `RunOnceAsync`-nutzenden Integrationstests |
| 7 | Tests | Bestehende Integrationstests auf `enumeration/`-Checkpoints anpassen (`ScmEventDispatchTests`, `ScmEventMonitoringServiceTests_AmbiguityRecovery`) | Erledigt | `ScmEventDispatchTests.*` (61 ScmEvent-Integrationstests grün) |
| 8 | Tests | Plugin-Enumerationstests: offene Referenzen + Pagination, `sinceUtc`-Fenster für Geschlossene, typisierte Fehler | Erledigt | `GitHubEventSourceTests.Enumeration_*` (3 Tests) |
| 9 | Tests | Monitor-Integrationstests: Repo ohne Aufgabe (Snapshots ohne Inbox), Referenz mit Aufgabe unverändert, keine Duplikate bei Folgepolls, inaktive/Nicht-GitHub-Repos übersprungen, Enumerations-Backoff | Erledigt | `ScmEventMonitoringServiceTests_RepoWide` (5 Tests) |
