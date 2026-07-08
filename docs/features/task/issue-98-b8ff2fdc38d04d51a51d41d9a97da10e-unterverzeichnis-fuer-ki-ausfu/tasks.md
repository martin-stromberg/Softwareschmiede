# Tasks: Unterverzeichnis für KI-Ausführung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Property `WorkingDirectoryRelativePath` (nullable string) zu `RepositoryStartKonfiguration` hinzufügen | Offen | — |
| 2 | Datenmodell | DbContext-Konfiguration: `WorkingDirectoryRelativePath` mit `HasMaxLength(512)` konfigurieren | Offen | — |
| 3 | Datenbankmigrationen | Migration `AddWorkingDirectoryToRepositoryStartKonfiguration` erstellen | Offen | — |
| 4 | Konfiguration | Configuration-Klasse `DirectoryStructureOptions` mit `CacheDurationSeconds`, `MaxDepth`, `Enabled` erstellen | Offen | — |
| 5 | Konfiguration | `appsettings.json` um `DirectoryStructure`-Einträge ergänzen (Standardwerte) | Offen | — |
| 6 | Konfiguration | Configuration in Dependency Injection registrieren | Offen | — |
| 7 | Logik | Service `DirectoryStructureBrowserService` erstellen | Offen | — |
| 8 | Logik | Methode `DirectoryStructureBrowserService.GetDirectoriesAsync()` implementieren | Offen | — |
| 9 | Logik | Methode `DirectoryStructureBrowserService.GetDirectoriesAsync()` mit Caching (TTL) erweitern | Offen | — |
| 10 | Logik | Fehlerbehandlung in `DirectoryStructureBrowserService` implementieren (Fallback auf leere Liste) | Offen | — |
| 11 | Logik | Methode `KiAusfuehrungsService.ResolveEffectiveWorkingDirectory()` hinzufügen | Offen | — |
| 12 | Logik | Methode `KiAusfuehrungsService.ValidateWorkingDirectory()` hinzufügen | Offen | — |
| 13 | Logik | Methodensignatur `KiAusfuehrungsService.StartCliAsync()` erweitern: Parameter `RepositoryStartKonfiguration? startConfig` hinzufügen | Offen | — |
| 14 | Logik | Methodensignatur `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` erweitern: Parameter `RepositoryStartKonfiguration? startConfig` hinzufügen | Offen | — |
| 15 | Logik | Logik in `KiAusfuehrungsService.StartCliAsync()` zur Auflösung des effektiven Arbeitsverzeichnisses hinzufügen | Offen | — |
| 16 | Logik | Methode `KiAusfuehrungsService.StartPseudoConsoleProcess()` anpassen: effektives Arbeitsverzeichnis verwenden | Offen | — |
| 17 | Logik | Methode `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync()` hinzufügen | Offen | — |
| 18 | UI | Property `AvailableWorkingDirectories` zu `RepositoryAssignViewModel` hinzufügen | Offen | — |
| 19 | UI | Property `SelectedWorkingDirectory` zu `RepositoryAssignViewModel` hinzufügen | Offen | — |
| 20 | UI | Property `IsLoadingDirectoryStructure` zu `RepositoryAssignViewModel` hinzufügen | Offen | — |
| 21 | UI | Property `CurrentLoadDirectoryStructureTask` zu `RepositoryAssignViewModel` hinzufügen (für Tests) | Offen | — |
| 22 | UI | Feld `_directoryStructureService` zu `RepositoryAssignViewModel` hinzufügen und via DI injizieren | Offen | — |
| 23 | UI | Feld `_dirStructureCts` (CancellationTokenSource) zu `RepositoryAssignViewModel` hinzufügen | Offen | — |
| 24 | UI | Methode `RepositoryAssignViewModel.LoadDirectoryStructureAsync()` implementieren | Offen | — |
| 25 | UI | Methode `RepositoryAssignViewModel.OnSelectedRepositoryChanged()` implementieren | Offen | — |
| 26 | UI | Setter `RepositoryAssignViewModel.SelectedRepository` um Callback `OnSelectedRepositoryChanged()` erweitern | Offen | — |
| 27 | UI (XAML) | `RepositoryAssignDialog.xaml` Grid.RowDefinitions anpassen (neue Row für Arbeitsverzeichnis) | Offen | — |
| 28 | UI (XAML) | `RepositoryAssignDialog.xaml` TextBlock für Label "Arbeitsverzeichnis im Repository" hinzufügen | Offen | — |
| 29 | UI (XAML) | `RepositoryAssignDialog.xaml` ComboBox für Verzeichnisauswahl hinzufügen | Offen | — |
| 30 | UI (XAML) | `RepositoryAssignDialog.xaml` ProgressRing/LoadingIndicator hinzufügen | Offen | — |
| 31 | UI (XAML) | `RepositoryAssignDialog.xaml` Info-TextBlock hinzufügen | Offen | — |
| 32 | Unit-Tests | Test `KiAusfuehrungsServiceTests.ResolveEffectiveWorkingDirectory_ShouldCombinePaths()` schreiben | Offen | — |
| 33 | Unit-Tests | Test `KiAusfuehrungsServiceTests.ResolveEffectiveWorkingDirectory_ShouldRejectPathTraversal()` schreiben | Offen | — |
| 34 | Unit-Tests | Test `KiAusfuehrungsServiceTests.ResolveEffectiveWorkingDirectory_ShouldAcceptDotAsRoot()` schreiben | Offen | — |
| 35 | Unit-Tests | Test `KiAusfuehrungsServiceTests.ValidateWorkingDirectory_ShouldThrowWhenNotExists()` schreiben | Offen | — |
| 36 | Unit-Tests | Test `KiAusfuehrungsServiceTests.ValidateWorkingDirectory_ShouldSucceedWhenExists()` schreiben | Offen | — |
| 37 | Unit-Tests | Test `KiAusfuehrungsServiceTests.StartCliAsync_ShouldUseEffectiveWorkingDirectory()` schreiben | Offen | — |
| 38 | Unit-Tests | Test `KiAusfuehrungsServiceTests.StartCliAsync_ShouldUseRepoRootWhenConfigNull()` schreiben | Offen | — |
| 39 | Unit-Tests | Bestehendes Test `KiAusfuehrungsServiceTests.TestCliStartAsync` anpassen (neuer Parameter) | Offen | — |
| 40 | Unit-Tests | Bestehendes Test `KiAusfuehrungsServiceTests.StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo` anpassen | Offen | — |
| 41 | Unit-Tests | Test-Klasse `DirectoryStructureBrowserServiceTests` erstellen | Offen | — |
| 42 | Unit-Tests | Test `DirectoryStructureBrowserServiceTests.GetDirectoriesAsync_ShouldReturnDirectories()` schreiben | Offen | — |
| 43 | Unit-Tests | Test `DirectoryStructureBrowserServiceTests.GetDirectoriesAsync_ShouldCache_WithTTL()` schreiben | Offen | — |
| 44 | Unit-Tests | Test `DirectoryStructureBrowserServiceTests.GetDirectoriesAsync_ShouldHandleErrors_Gracefully()` schreiben | Offen | — |
| 45 | Unit-Tests | Test `DirectoryStructureBrowserServiceTests.GetDirectoriesAsync_ShouldCallPluginMethod()` schreiben | Offen | — |
| 46 | Unit-Tests | Test `RepositoryAssignViewModelTests.SelectedRepositoryChanged_ShouldLoadDirectoryStructure()` schreiben | Offen | — |
| 47 | Unit-Tests | Test `RepositoryAssignViewModelTests.SelectedRepositoryChanged_ShouldResetSelectedWorkingDirectory()` schreiben | Offen | — |
| 48 | Unit-Tests | Test `RepositoryAssignViewModelTests.SelectedRepositoryChanged_ShouldCancelPreviousLoad()` schreiben | Offen | — |
| 49 | Unit-Tests | Test `RepositoryAssignViewModelTests.LoadDirectoryStructureAsync_ShouldSetIsLoading_Flag()` schreiben | Offen | — |
| 50 | Unit-Tests | Test `RepositoryAssignViewModelTests.LoadDirectoryStructureAsync_ShouldPopulateDirectories_WithDotRoot()` schreiben | Offen | — |
| 51 | Unit-Tests | Test `RepositoryAssignViewModelTests.LoadDirectoryStructureAsync_ShouldSetDefaultSelectedDirectory()` schreiben | Offen | — |
| 52 | Unit-Tests | Test `RepositoryAssignViewModelTests.LoadDirectoryStructureAsync_ShouldHandleNullRepository()` schreiben | Offen | — |
| 53 | Unit-Tests | Test `RepositoryAssignViewModelTests.LoadDirectoryStructureAsync_ShouldHandleErrors_WithLogging()` schreiben | Offen | — |
| 54 | Unit-Tests | Test `GitOrchestrationServiceTests.ValidateWorkingDirectoryAfterClone_ShouldThrowWhenDirectoryNotFound()` schreiben | Offen | — |
| 55 | Unit-Tests | Test `GitOrchestrationServiceTests.ValidateWorkingDirectoryAfterClone_ShouldLogError()` schreiben | Offen | — |
| 56 | E2E-Tests | Test für Repository-Dialog: Verzeichnisstruktur wird geladen und angezeigt | Offen | — |
| 57 | E2E-Tests | Test für Repository-Dialog: Arbeitsverzeichnis wird ausgewählt und gespeichert | Offen | — |
| 58 | E2E-Tests | Test für CLI-Ausführung: Prozess läuft im konfigurierten Arbeitsverzeichnis | Offen | — |
| 59 | E2E-Tests | Test für CLI-Ausführung: Prozess läuft im Root (Abwärtskompatibilität, wenn kein Arbeitsverzeichnis konfiguriert) | Offen | — |
| 60 | E2E-Tests | Test für Fehlerfall: Fehlgeschlagenes Verzeichnis-Abrufen wird gehandhabt | Offen | — |
| 61 | E2E-Tests | Test für Fehlerfall: Angegebenes Arbeitsverzeichnis existiert nicht nach Klon | Offen | — |
| 62 | E2E-Tests | Test für Fehlerfall: Path-Traversal-Versuch wird abgelehnt | Offen | — |
| 63 | Dokumentation | Plan-Datei (`plan.md`) erstellen | Offen | — |
| 64 | Dokumentation | Tasks-Datei (`plan.md`) erstellen | Offen | — |
