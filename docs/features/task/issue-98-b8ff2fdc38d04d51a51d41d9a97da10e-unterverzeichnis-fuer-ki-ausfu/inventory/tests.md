# Tests

## Testklassen

### `RepositoryAssignViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/RepositoryAssignViewModelTests.cs`

| Testmethode | Status | Relevanz |
|------------|--------|----------|
| `LadenAsync_ShouldLoadAvailablePlugins_WhenPluginsExist` | Vorhanden | Tests Plugin-Laden |
| `LadenAsync_ShouldSetHasScmPlugins_ToTrue_WhenPluginsAvailable` | Vorhanden | Tests HasScmPlugins Flag |
| `LadenAsync_ShouldSetHasScmPlugins_ToFalse_WhenNoPluginsAvailable` | Vorhanden | Tests Fehlerfall (keine Plugins) |
| `LadenAsync_ShouldSetSelectedScmPlugin_ToFirstPlugin` | Vorhanden | Tests Standard-Plugin-Auswahl |
| `SelectedScmPluginChanged_ShouldReloadRepositories_FromPlugin` | Vorhanden | Tests Repository-Reload beim Plugin-Wechsel |
| `SelectedScmPluginChanged_ShouldClearRepositories_WhenPluginUnselected` | Vorhanden | Tests Clearing bei Abwahl |
| `SelectedScmPluginChanged_ShouldSetIsLoading_FlagDuringReload` | Vorhanden | Tests IsLoading-Flag |
| `ReloadRepositoriesForSelectedPlugin_ShouldHandleError_Gracefully` | Vorhanden | Tests Fehlerbehandlung |
| `RepositorySelection_ShouldEnableBestaetigenCommand_WhenRepositorySelected` | Vorhanden | Tests Command-Aktivierung |
| `RepositorySelection_ShouldDisableBestaetigenCommand_WhenRepositoryUnselected` | Vorhanden | Tests Command-Deaktivierung |
| **Tests für Arbeitsverzeichnis-Funktionalität** | **FEHLT** | – Directory-Struktur-Laden beim Repository-Wechsel<br>– AvailableWorkingDirectories-Befüllung<br>– SelectedWorkingDirectory-Reset<br>– Fehlerbehandlung beim Laden |

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

| Testmethode | Status | Relevanz |
|------------|--------|----------|
| `IsRunning_ShouldReturnFalse_WhenNoProcessStarted` | Vorhanden | Tests Basisfunktionalität |
| `GetRunningCount_ShouldReturnZero_WhenNoProcessStarted` | Vorhanden | Tests Zähler |
| `GetLastExitCode_ShouldReturnNull_WhenNoProcessStarted` | Vorhanden | Tests Exit-Code |
| `StopCliAsync_ShouldNotThrow_WhenNoProcessStarted` | Vorhanden | Tests Fehlertoleranz |
| `UpdateHeartbeat_ShouldNotThrow_WhenNoProcessStarted` | Vorhanden | Tests Heartbeat-Fehlertoleranz |
| `TestCliStartAsync` | Vorhanden | Tests CLI-Start (Standard) |
| `StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo` | Vorhanden | Tests Handle-Rückgabe |
| **Tests für Working Directory Resolution** | **FEHLT** | – `ResolveEffectiveWorkingDirectory()` mit korrektem absoluten Pfad<br>– Path-Traversal-Validierung (z.B. `"../../../etc"`)<br>– Fehlerfall: Zielverzeichnis existiert nicht<br>– Fehlerfall: WorkingDirectory liegt außerhalb Repository |

### `GitOrchestrationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`

| Testmethode (gelesen bis Zeile 50) | Status | Relevanz |
|------------|--------|----------|
| Setup und Dependency Injection | Vorhanden | Tests verwenden Mock `IGitPlugin` |
| **Tests für Arbeitsverzeichnis-Validierung** | **FEHLT** | – Validierung des Verzeichnisses nach Klon<br>– Fehlerbehandlung bei fehlendem Verzeichnis<br>– Fehlerprotokollierung |

## Hilfsmethoden & Fixtures

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs` (indirekt verwendet)

- Erstellt In-Memory-Datenbank für Tests
- Wird in `GitOrchestrationServiceTests` verwendet

### `CreatePluginMock` (in `RepositoryAssignViewModelTests`)
- Erstellt Mock-Plugins für SCM-Tests
- Kann erweitert werden für Directory-Struktur-Mocks

## Erforderliche Tests (neu zu schreiben)

### `DirectoryStructureBrowserServiceTests` — **NEU**
Sollte testen:
- Erfolgreicher Abruf der Verzeichnisstruktur
- Caching-Verhalten (TTL-Validierung)
- Fehlerbehandlung bei API-Fehlern
- Fallback auf leere Liste bei Fehler

### `RepositoryAssignViewModelTests` — Erweiterungen
Zusätzliche Tests:
- `SelectedRepositoryChanged_ShouldLoadDirectoryStructure_Async`
- `SelectedRepositoryChanged_ShouldClearWorkingDirectories_BeforeReload`
- `SelectedRepositoryChanged_ShouldSetSelectedWorkingDirectory_ToNull`
- `LoadDirectoryStructureAsync_ShouldSetIsLoadingDirectoryStructure_Flag`
- `LoadDirectoryStructureAsync_ShouldPopulateAvailableWorkingDirectories_WithDotRoot`
- `LoadDirectoryStructureAsync_ShouldHandleErrors_Gracefully`

### `KiAusfuehrungsServiceTests` — Erweiterungen
Zusätzliche Tests:
- `ResolveEffectiveWorkingDirectory_ShouldCombineRootAndRelativePath`
- `ResolveEffectiveWorkingDirectory_ShouldRejectPathTraversal_Outside`
- `ValidateWorkingDirectory_ShouldThrow_WhenDirectoryDoesNotExist`
- `StartCliAsync_ShouldUseEffectiveWorkingDirectory_WhenConfigProvided`
- `StartWithPseudoConsoleAsync_ShouldSetCorrectWorkingDirectory_InPseudoConsole`

### `GitOrchestrationServiceTests` — Erweiterungen
Zusätzliche Tests:
- `ValidateWorkingDirectoryAfterClone_ShouldThrow_WhenDirectoryNotFound`
- `ValidateWorkingDirectoryAfterClone_ShouldLog_WithRepositoryAndPath`

