# Tests

## Testklassen

### `ProjektServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProjektServiceTests.cs`

Umfangreiche Unit-Tests für `ProjektService`:

#### Projekt-CRUD-Tests
- `CreateAsync_ShouldCreateProjektWithAktivStatus_WhenCalledWithValidName()` — Projekt wird mit Status `Aktiv` erstellt
- `CreateAsync_ShouldPersistProjekt_WhenCalledWithValidName()` — Projekt wird in DB persistiert
- `GetAllAsync_ShouldReturnAllProjekteOrderedByName_WhenMultipleProjekteExist()` — Projekte werden alphabetisch sortiert
- `GetByIdAsync_ShouldReturnNull_WhenProjektDoesNotExist()` — Null bei fehlender ID
- `UpdateAsync_ShouldUpdateNameAndBeschreibung_WhenProjektExists()` — Update funktioniert
- `UpdateAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()` — Exception bei fehlendem Projekt
- `ArchivierenAsync_ShouldSetStatusToArchiviert_WhenProjektExists()` — Archivierung setzt Status
- `ArchivierenAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()` — Exception bei fehlendem Projekt
- `DeleteAsync_ShouldRemoveProjekt_WhenProjektExists()` — Projekt wird gelöscht
- `DeleteAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()` — Exception bei fehlendem Projekt

#### Repository-Verwaltungs-Tests
- `AddRepositoryAsync_ShouldAddRepository_WhenProjektExists()` — Repository wird hinzugefügt
- `AddRepositoryAsync_ShouldUseSourceDirectoryForLocalDirectoryPlugin_WhenFieldValuesAreValid()` — LocalDirectoryPlugin-Handling
- `AddRepositoryAsync_ShouldThrow_WhenSourceDirectoryIsMissingForLocalDirectoryPlugin()` — Validierung
- `AddRepositoryAsync_ShouldMapRepositoryUrlToSourceDirectory_WhenUsingStringOverloadForLocalDirectoryPlugin()` — String-Overload
- `AddRepositoryAsync_ShouldHandleTrimAndTrailingSeparator_ForLocalDirectorySourceDirectory()` — Pfad-Normalisierung
- `AddRepositoryAsync_ShouldThrow_WhenRepositoryUrlIsMissingForGitHubPlugin()` — Validierung GitHub
- `AddRepositoryAsync_ShouldThrow_WhenRepositoryNameIsMissingForGitHubPlugin()` — Validierung GitHub
- `AddRepositoryAsync_ShouldDeriveRepositoryName_FromRepositoryUrl_WhenNameMissing_ForNonGitHubPlugin()` — Name-Ableitung
- `AddRepositoryAsync_ShouldDeriveRepositoryName_FromLocalPath_WhenNameMissing_ForNonGitHubPlugin()` — Lokale Pfad-Ableitung
- `AddRepositoryAsync_ShouldThrow_WhenDerivedRepositoryNameIsEmpty_ForNonGitHubPlugin()` — Fehlerfall
- `AddRepositoryAsync_ShouldPreferExplicitRepositoryName_OverDerivedName_ForNonGitHubPlugin()` — Override-Bevorzugung
- `RemoveRepositoryAsync_ShouldRemoveRepository_WhenRepositoryExists()` — Repository-Entfernung

#### Start-Konfiguration-Tests
- `SaveRepositoryStartKonfigurationAsync_ShouldCreateConfiguration_WhenRepositoryExists()` — Konfiguration erstellen
- `SaveRepositoryStartKonfigurationAsync_ShouldUpdateExistingConfiguration_WhenAlreadyPresent()` — Konfiguration aktualisieren
- `GetRepositoryStartKonfigurationAsync_ShouldReturnNull_WhenNoConfigurationExists()` — Null bei fehlender Config
- `SaveRepositoryStartKonfigurationAsync_ShouldThrow_WhenScriptPathIsAbsolute()` — Validierung absoluter Pfade

#### Test-Setup
- Nutzt `TestDbContextFactory.Create()` für In-Memory-DB
- Mogt `ILogger<ProjektService>`

---

### `RepositoryAssignViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/RepositoryAssignViewModelTests.cs`

Tests für Repository-Auswahl-Dialog:

#### Plugin-Ladevorgänge
- `LadenAsync_ShouldLoadAvailablePlugins_WhenPluginsExist()` — Plugins werden geladen
- `LadenAsync_ShouldSetHasScmPlugins_ToTrue_WhenPluginsAvailable()` — Flag wird gesetzt
- `LadenAsync_ShouldSetHasScmPlugins_ToFalse_WhenNoPluginsAvailable()` — Flag bei keine Plugins
- `LadenAsync_ShouldSetSelectedScmPlugin_ToFirstPlugin()` — Erstes Plugin wird selektiert

#### Repository-Ladevorgänge
- `SelectedScmPluginChanged_ShouldReloadRepositories_FromPlugin()` — **Repositories werden aus Plugin geladen**
- `SelectedScmPluginChanged_ShouldClearRepositories_WhenPluginUnselected()` — Repositories werden geleert bei Plugin-Abwahl
- `SelectedScmPluginChanged_ShouldSetIsLoading_FlagDuringReload()` — IsLoading-Flag wird korrekt gesetzt

#### Fehlerbehandlung
- `ReloadRepositoriesForSelectedPlugin_ShouldHandleError_Gracefully()` — Fehler werden abgefangen (kein Exception)

#### Dialog-Interaktion
- `RepositorySelection_ShouldEnableBestaetigenCommand_WhenRepositorySelected()` — Command enabled bei Auswahl
- `RepositorySelection_ShouldDisableBestaetigenCommand_WhenRepositoryUnselected()` — Command disabled bei Abwahl

#### Test-Setup
- Nutzt `Moq` für `IPluginManager` und `IGitPlugin`-Mocks
- Nutzt `NullLogger<RepositoryAssignViewModel>.Instance`
- Erstellt Mock-Plugins mit `CreatePluginMock()`

### Hilfsmethoden

#### Testklasse: `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`

- `Create()` — Erstellt eine In-Memory-Datenbank für Tests

---

## Existierende Integrations- und E2E-Tests

### `ProjectDetailE2ETests`
Datei: `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`

End-to-End-Tests für die Projektdetailansicht (nicht detailliert analysiert, aber vorhanden).

---

## Notizen zu bestehenden Tests

1. **Repository-Verwaltung** ist bereits gut getestet
2. **Sortierung nach `UpdatedAt`** ist in `RepositoryAssignViewModelTests` bereits vorhanden:
   - Test: `SelectedScmPluginChanged_ShouldReloadRepositories_FromPlugin()`
   - Implementierung: `repos.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Name, ...)`
3. **Fehlerbehandlung** für Plugin-Fehler ist bereits implementiert und getestet
4. **Keine bestehenden Tests für `GetUnassignedRepositoriesAsync()`** — diese Methode existiert noch nicht und benötigt neue Tests
5. **ViewModels haben gute Property-Change- und Command-Tests**
