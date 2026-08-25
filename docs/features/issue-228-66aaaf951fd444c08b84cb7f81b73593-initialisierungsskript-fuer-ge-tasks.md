# Tasks: Initialisierungsskript für geklonte Repositories

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `RepositoryInitialisierungKonfiguration`-Entity anlegen mit Properties `Id`, `GitRepositoryId`, `InitialisierungsskriptRelativePfad`, `Aktiv`, Navigationseigenschaft `GitRepository` | Offen | — |
| 2 | Datenmodell | `GitRepository`-Entity um Navigationseigenschaft `InitialisierungKonfiguration` erweitern | Offen | — |
| 3 | Datenmodell | `SoftwareschmiededDbContext` um `DbSet<RepositoryInitialisierungKonfiguration>` erweitern | Offen | — |
| 4 | Datenbank | Migration `AddRepositoryInitialisierungKonfiguration` erstellen | Offen | — |
| 5 | Logik | `RepositoryInitialisierungService`-Klasse anlegen mit `RunAsync()`-Methode (analog zu `RepositoryStartskriptService`) | Offen | — |
| 6 | Logik | `RepositoryInitialisierungService.ResolveScriptPath()`-Hilfsmethode implementieren (Path-Traversal-Validierung) | Offen | — |
| 7 | Logik | `RepositoryInitialisierungService.BuildArguments()`-Hilfsmethode implementieren (PowerShell-Argumente) | Offen | — |
| 8 | Logik | `RepositoryInitialisierungService` in DI-Container registrieren | Offen | — |
| 9 | Logik | `EntwicklungsprozessService.FinalizeStartAsync()` erweitern: Initialisierungsskript-Ausführung hinzufügen (nach Klon, vor Startskript) | Offen | — |
| 10 | Logik | `EntwicklungsprozessService` um optionale `RepositoryInitialisierungService`-Abhängigkeit in `EntwicklungsprozessServiceOptions` erweitern | Offen | — |
| 11 | Logik | `ProjektService.SaveRepositoryInitialisierungskriptAsync()`-Methode implementieren | Offen | — |
| 12 | UI-ViewModel | `ProjectDetailViewModel` um Properties erweitern: `InitialisierungsskriptSuggestionen`, `SelectedInitialisierungsskript`, `IsEditingInitialisierungsskript`, `InitialisierungsskriptLoadingFailed` | Offen | — |
| 13 | UI-ViewModel | `ProjectDetailViewModel.LoadInitialisierungsskriptSuggestionsAsync()`-Methode implementieren | Offen | — |
| 14 | UI-ViewModel | `ProjectDetailViewModel.SaveInitialisierungsskriptAsync()`-Methode implementieren | Offen | — |
| 15 | UI-ViewModel | `ProjectDetailViewModel.CancelInitialisierungsskriptEdit()`-Methode implementieren | Offen | — |
| 16 | UI-ViewModel | `ProjectDetailViewModel.LadenAsync()` erweitern: Optional Initialisierungsskript-Suggestionen beim Laden eines Repositories laden | Offen | — |
| 17 | UI-View | `ProjectDetailView.xaml` um Label „Initialisierungsskript:" erweitern | Offen | — |
| 18 | UI-View | `ProjectDetailView.xaml` um AutoComplete/ComboBox-Feld für `SelectedInitialisierungsskript` mit `ItemsSource`-Binding zu `InitialisierungsskriptSuggestionen` erweitern | Offen | — |
| 19 | UI-View | `ProjectDetailView.xaml` um Button „Laden" erweitern (triggert `LoadInitialisierungsskriptSuggestionsAsync()`) | Offen | — |
| 20 | UI-View | `ProjectDetailView.xaml` um Button „Speichern" erweitern (triggert `SaveInitialisierungsskriptAsync()`) | Offen | — |
| 21 | UI-View | `ProjectDetailView.xaml` um Button „Abbrechen" erweitern (triggert `CancelInitialisierungsskriptEdit()`) | Offen | — |
| 22 | UI-View | `ProjectDetailView.xaml` optional um ProgressRing und Error-TextBlock für Ladefehlermeldungen erweitern | Offen | — |
| 23 | Validierung | `RepositoryInitialisierungKonfiguration`: Relative-Pfad-Validierung (keine absoluten Pfade) implementieren | Offen | — |
| 24 | Validierung | `RepositoryInitialisierungService.RunAsync()`: Foreign-Key-Validierung für `GitRepositoryId` (in DB-Constraints oder Service-Logic) | Offen | — |
| 25 | Unit-Test | `RepositoryInitialisierungServiceTests`-Klasse anlegen | Offen | — |
| 26 | Unit-Test | `RepositoryInitialisierungServiceTests.RunAsync_ShouldSucceed_WhenInitializationScriptExecutes()` implementieren | Offen | — |
| 27 | Unit-Test | `RepositoryInitialisierungServiceTests.RunAsync_ShouldLogWarning_WhenInitializationScriptFails()` implementieren | Offen | — |
| 28 | Unit-Test | `RepositoryInitialisierungServiceTests.RunAsync_ShouldThrow_WhenPathTraversalAttempted()` implementieren | Offen | — |
| 29 | Unit-Test | `RepositoryInitialisierungServiceTests.RunAsync_ShouldSkipExecution_WhenConfigurationIsInactive()` implementieren | Offen | — |
| 30 | Unit-Test | `RepositoryInitialisierungServiceTests.RunAsync_ShouldThrow_WhenScriptFileNotFound()` implementieren | Offen | — |
| 31 | Unit-Test | `RepositoryInitialisierungServiceTests.ResolveScriptPath_ShouldValidatePathBoundary()` implementieren | Offen | — |
| 32 | Unit-Test | `RepositoryInitialisierungServiceTests`-Hilfsmethoden implementieren: `CreateSut()`, `CreateConfig()`, `CreateScript(relativePath)` | Offen | — |
| 33 | Integration-Test | `EntwicklungsprozessServiceTests.ProzessStartenAsync_ShouldExecuteInitializationScript_AfterClone()` implementieren | Offen | — |
| 34 | Integration-Test | `EntwicklungsprozessServiceTests.ProzessStartenAsync_ShouldNotBlockTask_WhenInitializationScriptFails()` implementieren | Offen | — |
| 35 | Integration-Test | `EntwicklungsprozessServiceTests.ProzessStartenAsync_ShouldExecuteInitializationThenStartScript_InOrder()` implementieren | Offen | — |
| 36 | Unit-Test | `ProjectDetailViewModelTests.LoadInitialisierungsskriptSuggestionsAsync_ShouldFetchFromRemote()` implementieren | Offen | — |
| 37 | Unit-Test | `ProjectDetailViewModelTests.LoadInitialisierungsskriptSuggestionsAsync_ShouldHandleNetworkError_Gracefully()` implementieren | Offen | — |
| 38 | Unit-Test | `ProjectDetailViewModelTests.SaveInitialisierungsskriptAsync_ShouldPersist_SelectedScript()` implementieren | Offen | — |
| 39 | Unit-Test | `ProjectDetailViewModelTests.SaveInitialisierungsskriptAsync_ShouldCreateConfiguration_IfNotExists()` implementieren | Offen | — |
| 40 | E2E-Test | `E2E_RepositoryInitialisierungTests.cs` anlegen (oder in bestehender E2E-Testklasse) | Offen | — |
| 41 | E2E-Test | `E2E_RepositoryInitialisierungTests.HappyPath_InitializationScriptConfiguredAndExecuted()` implementieren: Projekt öffnen, Repository mit Init-Skript konfigurieren, Aufgabe starten, Skript-Ausführung verifizieren | Offen | — |
| 42 | E2E-Test | `E2E_RepositoryInitialisierungTests.ErrorTolerance_InitializationScriptFailureDoesNotBlockTask()` implementieren: Aufgabe wird trotz Init-Skript-Fehler normal fortgesetzt | Offen | — |
| 43 | E2E-Test | `E2E_ProjectDetailViewTests.UI_InitialisierungsskriptFields_AreDisplayedAndResponsive()` implementieren (oder erweitern): UI-Felder sind sichtbar, Buttons funktionieren | Offen | — |
| 44 | Bestehende Tests | `EntwicklungsprozessServiceTests`: Bei Bedarf anpassen, wenn Tests `FinalizeStartAsync()` mocken | Offen | — |
| 45 | Bestehende Tests | `ProjectDetailViewModelTests`: Bei Bedarf anpassen für neue Properties/Methoden | Offen | — |
| 46 | Bestehende Tests | E2E Tests für Repository-Klon: Bei Bedarf Logging-Validierung aktualisieren für neue Initialisierungs-Logs | Offen | — |
