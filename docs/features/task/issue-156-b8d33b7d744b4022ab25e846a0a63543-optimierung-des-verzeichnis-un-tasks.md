# Aufgaben: Lazy-Loading des Verzeichnisbaums (Issue #156)

| # | Aufgabe | Status | Testnachweis |
|---|---------|--------|--------------|
| 1 | `WorkspaceFileNode`: `Depth` (int, init) hinzufügen | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_SetztDepthKorrekt` |
| 2 | `WorkspaceFileNode`: `IsPlaceholder` (bool, init, Default false) hinzufügen | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_GrenztiefeVerzeichnis_ChildrenLoadedFalseUndPlatzhalter` |
| 3 | `WorkspaceFileNode`: `Children` von `List` auf `ObservableCollection` umstellen | Erledigt | `FileExplorerViewModelTests_LazyLoading.LadeKinderAsync_LaedtKinderUndSetztChildrenLoaded` (ReplaceAll auf Children) |
| 4 | `IGitWorkspaceBrowserService.LoadWorkingTreeAsync`: Signatur um `maxInitialDepth = 2` erweitern | Erledigt | `FileExplorerViewModelTests.Standard_LaedtWurzelknotenUeberWorkingTree` |
| 5 | `IGitWorkspaceBrowserService.LoadSubtreeAsync` deklarieren | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadSubtreeAsync_LaedtEineEbeneUnterhalbParent` |
| 6 | `GitWorkspaceBrowserService.LoadWorkingTreeAsync`: `maxInitialDepth` durchreichen | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_LaedtNurMaxInitialDepthEbenen` |
| 7 | `WalkWorkingTreeDirectory`: `currentDepth`/`maxDepth`, `Depth` setzen, Grenztiefe = ChildrenLoaded=false + Platzhalter | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_ObereEbeneVerzeichnis_ChildrenLoadedTrue` + `..._GrenztiefeVerzeichnis_ChildrenLoadedFalseUndPlatzhalter` |
| 8 | `SortNodes` auf `IList`, in-place-Sortierung, Platzhalter ausgenommen | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_ListetDateienUndVerzeichnisse` |
| 9 | `InsertNode` auf `IList` anpassen (Commit-Baum unverändert) | Erledigt | `FileExplorerViewModelTests.CommitAufklappen_LaedtGeaenderteDateien` |
| 10 | `GitWorkspaceBrowserService.LoadSubtreeAsync` implementieren (eine Ebene, .git aus, Platzhalter, Limit, MaxLazyLoadDepth, leere Liste bei ungültigem Pfad) | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadSubtreeAsync_SetztDepthAufUebergebenenWert`, `..._UnterverzeichnisMitPlatzhalterUndChildrenLoadedFalse`, `..._NichtExistierenderPfad_LeereListe` |
| 11 | Konstante `MaxLazyLoadDepth = 64` im Service | Erledigt | Kein direkter Test (Begrenzung greift in `LoadWorkingTreeAsync`/`LoadSubtreeAsync`; fehlte sie, würde die Tiefenbegrenzung nicht deckeln) |
| 12 | `FileExplorerViewModel`: Konstante `InitialLoadDepth = 2` | Erledigt | `FileExplorerViewModelTests.Standard_LaedtWurzelknotenUeberWorkingTree` (Aufruf mit int-Argument) |
| 13 | `FileExplorerViewModel.LadeArbeitsbaumAsync`: mit `InitialLoadDepth` aufrufen | Erledigt | `FileExplorerViewModelTests.Standard_LaedtWurzelknotenUeberWorkingTree` |
| 14 | `FileExplorerViewModel.LadeKinderAsync` implementieren | Erledigt | `FileExplorerViewModelTests_LazyLoading.LadeKinderAsync_LaedtKinderUndSetztChildrenLoaded`, `..._BereitsGeladen_LaedtNichtErneut`, `..._KeinVerzeichnis_TutNichts`, `..._Fehler_LaesstChildrenLoadedFalse` |
| 15 | `FileExplorerViewModel.BeraeumeKnoten` implementieren (stets aktiv) | Erledigt | `FileExplorerViewModelTests_LazyLoading.BeraeumeKnoten_EntferntGrossEnkel`, `..._BehaeltDirekteKinderUndPlatzhalterInvariante` |
| 16 | Platzhalter-Guard in `DateiLadenAsync`/`AusgewaehlterKnoten` | Erledigt | `FileExplorerViewModelTests_LazyLoading.Platzhalterknoten_WirdNichtAlsAuswahlBehandelt` |
| 17 | `FileExplorerView.xaml`: `TreeViewItem.Expanded`/`Collapsed` am `StandardBaum` | Erledigt | `E2E_FileExplorer.DateiExplorer_KlapptVerzeichnisAufUndLaedtKinderNach_E2E` |
| 18 | Code-Behind `OnBaumKnotenExpanded` | Erledigt | `E2E_FileExplorer.DateiExplorer_KlapptVerzeichnisAufUndLaedtKinderNach_E2E` |
| 19 | Code-Behind `OnBaumKnotenCollapsed` | Erledigt | `E2E_FileExplorer.DateiExplorer_KlapptVerzeichnisZuUndErneutAuf_LaedtKinderNach_E2E` |
| 20 | Bestehende Service-/VM-Tests an neue Signatur anpassen | Erledigt | `FileExplorerViewModelTests.*` (Moq `It.IsAny<int>()`), `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_ListetDateienUndVerzeichnisse` |
| 21 | Neue Unit-Tests (Service + VM Lazy-Loading) | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests` (8 Tests), `FileExplorerViewModelTests_LazyLoading` (7 Tests) |
| 22 | Neue E2E-Tests für Aufklappen und Zuklappen/erneut Aufklappen | Erledigt | `E2E_FileExplorer.DateiExplorer_KlapptVerzeichnisAufUndLaedtKinderNach_E2E`, `..._KlapptVerzeichnisZuUndErneutAuf_LaedtKinderNach_E2E` |
