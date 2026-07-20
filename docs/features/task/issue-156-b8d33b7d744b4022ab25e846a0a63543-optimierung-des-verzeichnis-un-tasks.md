# Tasks: Lazy-Loading des Verzeichnisbaums mit progressiver Tiefenentwicklung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `WorkspaceFileNode.Depth` (`int`, init) hinzufügen | Offen | — |
| 2 | Datenmodell | `WorkspaceFileNode.IsPlaceholder` (`bool`, init, Default `false`) hinzufügen | Offen | — |
| 3 | Datenmodell | `WorkspaceFileNode.Children` von `List<WorkspaceFileNode>` auf `ObservableCollection<WorkspaceFileNode>` umstellen | Offen | — |
| 4 | Logik | `GitWorkspaceBrowserService.SortNodes` auf `IList<WorkspaceFileNode>` + in-place-Sortierung umstellen | Offen | — |
| 5 | Logik | `GitWorkspaceBrowserService.InsertNode` an neuen `Children`-Typ anpassen | Offen | — |
| 6 | Logik | `IGitWorkspaceBrowserService.LoadWorkingTreeAsync` um `maxInitialDepth = 2` erweitern | Offen | — |
| 7 | Logik | `GitWorkspaceBrowserService.WalkWorkingTreeDirectory` um `currentDepth`/`maxDepth` erweitern, `Depth` setzen, Rekursion begrenzen | Offen | — |
| 8 | Logik | Grenztiefen-Verzeichnisse mit `ChildrenLoaded = false` + Platzhalter-Kind markieren | Offen | — |
| 9 | Logik | `MaxLazyLoadDepth`-Konstante gegen zirkuläre Symlinks ergänzen | Offen | — |
| 10 | Logik | `IGitWorkspaceBrowserService.LoadSubtreeAsync(...)` deklarieren | Offen | — |
| 11 | Logik | `GitWorkspaceBrowserService.LoadSubtreeAsync` implementieren (eine Ebene, Depth, Platzhalter, `.git`-Ausschluss, Knotenlimit) | Offen | — |
| 12 | ViewModel | `InitialLoadDepth`-Konstante in `FileExplorerViewModel` | Offen | — |
| 13 | ViewModel | `LadeArbeitsbaumAsync` mit `InitialLoadDepth` aufrufen | Offen | — |
| 14 | ViewModel | `FileExplorerViewModel.LadeKinderAsync(knoten, ct)` implementieren (Lazy-Load beim Aufklappen) | Offen | — |
| 15 | ViewModel | `FileExplorerViewModel.BeraeumeKnoten(knoten)` implementieren (stets aktive Cleanup beim Zuklappen) | Offen | — |
| 16 | Validierung | Platzhalter-Guard in `AusgewaehlterKnoten`/`DateiLadenAsync` (Auswahl/Vorschau ignorieren) | Offen | — |
| 17 | UI | `StandardBaum` in `FileExplorerView.xaml` um `TreeViewItem.Expanded="OnBaumKnotenExpanded"` erweitern | Offen | — |
| 18 | UI | `StandardBaum` um `TreeViewItem.Collapsed="OnBaumKnotenCollapsed"` erweitern (stets aktive Zuklapp-Bereinigung) | Offen | — |
| 19 | UI | Code-Behind-Handler `OnBaumKnotenExpanded` (ruft `vm.LadeKinderAsync`) | Offen | — |
| 20 | UI | Code-Behind-Handler `OnBaumKnotenCollapsed` (ruft `vm.BeraeumeKnoten`) | Offen | — |
| 21 | UI | Optional: dezente Lade-/Platzhalteranzeige im `HierarchicalDataTemplate` für `WorkspaceFileNode` | Offen | — |
| 22 | Tests | `LoadWorkingTreeAsync_LaedtNurMaxInitialDepthEbenen` | Offen | — |
| 23 | Tests | `LoadWorkingTreeAsync_SetztDepthKorrekt` | Offen | — |
| 24 | Tests | `LoadWorkingTreeAsync_GrenztiefeVerzeichnis_ChildrenLoadedFalseUndPlatzhalter` | Offen | — |
| 25 | Tests | `LoadWorkingTreeAsync_ObereEbeneVerzeichnis_ChildrenLoadedTrue` | Offen | — |
| 26 | Tests | `LoadSubtreeAsync_LaedtEineEbeneUnterhalbParent` | Offen | — |
| 27 | Tests | `LoadSubtreeAsync_SetztDepthAufUebergebenenWert` | Offen | — |
| 28 | Tests | `LoadSubtreeAsync_UnterverzeichnisMitPlatzhalterUndChildrenLoadedFalse` | Offen | — |
| 29 | Tests | `LoadSubtreeAsync_NichtExistierenderPfad_LeereListe` | Offen | — |
| 30 | Tests | `FileExplorerViewModelTests_LazyLoading` (neue Testklasse) anlegen | Offen | — |
| 31 | Tests | `LadeKinderAsync_LaedtKinderUndSetztChildrenLoaded` | Offen | — |
| 32 | Tests | `LadeKinderAsync_BereitsGeladen_LaedtNichtErneut` | Offen | — |
| 33 | Tests | `LadeKinderAsync_KeinVerzeichnis_TutNichts` | Offen | — |
| 34 | Tests | `LadeKinderAsync_Fehler_LaesstChildrenLoadedFalse` | Offen | — |
| 35 | Tests | `BeraeumeKnoten_EntferntGrossEnkel` | Offen | — |
| 36 | Tests | `BeraeumeKnoten_BehaeltDirekteKinderUndPlatzhalterInvariante` | Offen | — |
| 37 | Tests | `Platzhalterknoten_WirdNichtAlsAuswahlBehandelt` | Offen | — |
| 38 | Tests | `LoadWorkingTreeAsync_ListetDateienUndVerzeichnisse` an Tiefenbegrenzung anpassen | Offen | — |
| 39 | Tests | Moq-Setups in `FileExplorerViewModelTests` auf neue `LoadWorkingTreeAsync`-Signatur (`It.IsAny<int>()`) anpassen | Offen | — |
| 40 | Tests | `GitWorkspaceBrowserServiceTests.FindNode`/Assertions an `ObservableCollection`-`Children` anpassen | Offen | — |
| 41 | E2E-Tests | `E2E_FileExplorer.DateiExplorer_KlapptVerzeichnisAufUndLaedtKinderNach_E2E` (Aufklappen lädt Kinder nach) | Offen | — |
| 42 | E2E-Tests | `E2E_FileExplorer.DateiExplorer_KlapptVerzeichnisZuUndErneutAuf_LaedtKinderNach_E2E` (Zuklappen bereinigt, erneutes Aufklappen lädt nach) | Offen | — |
| 43 | E2E-Tests | Test-Datenbereitstellung: geklontes Test-Repository um mindestens dreistufige Verzeichnisstruktur ergänzen | Offen | — |
