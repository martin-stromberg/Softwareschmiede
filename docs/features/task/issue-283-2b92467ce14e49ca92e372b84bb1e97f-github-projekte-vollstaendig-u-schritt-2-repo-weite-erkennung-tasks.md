# Tasks: Repository-weite GitHub-Ereigniserkennung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Plugin-Vertrag | `IScmEventSource.EnumerateReferencesAsync` deklarieren (Signatur, XML-Doku) | Offen | — |
| 2 | Plugin-Vertrag | `GitHubPlugin.EnumerateReferencesAsync` implementieren (offene Issues/PRs, geschlossene im `sinceUtc`-Fenster, Pagination, typisierte Fehler) | Offen | — |
| 3 | Logik | `ScmEventStore.AcceptBatchAsync` um `unassigned`-Parameter und Diagnostic-Konstanten erweitern | Offen | — |
| 4 | Logik | `ScmEventMonitoringService.RunOnceAsync`: `db.GitRepositories` (Aktiv) laden und in Alias-Gruppierung einbeziehen | Offen | — |
| 5 | Logik | `ScmEventMonitoringService.RunOnceAsync`: Enumeration pro kanonischem Repo mit `enumeration/`-Checkpoint, `resolved` auf `Guid?`-TaskIds umstellen, `unassigned` an `AcceptBatchAsync` | Offen | — |
| 6 | Testinfrastruktur | `ScmDispatchFixture`: `EnumerateReferencesAsync`-Default-Mock ergänzen | Offen | — |
| 7 | Tests | Bestehende Integrationstests auf `enumeration/`-Checkpoints anpassen (`ScmEventDispatchTests`, `ScmEventMonitoringServiceTests_AmbiguityRecovery`) | Offen | — |
| 8 | Tests | Plugin-Enumerationstests: offene Referenzen + Pagination, `sinceUtc`-Fenster für Geschlossene, typisierte Fehler | Offen | — |
| 9 | Tests | Monitor-Integrationstests: Repo ohne Aufgabe (Snapshots ohne Inbox), Referenz mit Aufgabe unverändert, keine Duplikate bei Folgepolls, inaktive/Nicht-GitHub-Repos übersprungen, Enumerations-Backoff | Offen | — |
