# Tests

## Testklassen

### `PluginManagerTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`

Testet die Plugin-Discovery und -Verwaltung. Abgedeckte Szenarien:
- `GetSourceCodeManagementPlugins_ShouldReturnEmpty_WhenPluginDirectoryMissing` — Plugin-Verzeichnis fehlt → leere Liste
- `GetSourceCodeManagementPlugins_ShouldSkipInvalidDll_WhenBadDllExists` — Ungültige DLL wird übersprungen
- `GetSourceCodeManagementPlugins_ShouldLoadGitAndKiPlugins_WhenValidPluginDllsExist` — Valide DLLs werden geladen (2 SCM, 4 KI)
- `GetDefaultSourceCodeManagementPlugin_ShouldThrow_WhenNoPluginLoaded` — Exception bei fehlenden Plugins
- `GetDefaultDevelopmentAutomationPlugin_ShouldThrow_WhenNoPluginLoaded` — Exception bei fehlenden KI-Plugins
- `GetDefaultDevelopmentAutomationPlugin_ShouldPreferCopilot_WhenMultipleKiPluginsLoaded` — Copilot wird priorisiert
- `GetDefaultDevelopmentAutomationPlugin_ShouldReturnClaude_WhenOnlyClaudePluginLoaded` — Claude wird als Default verwendet
- `GetSourceCodeManagementPlugins_ShouldNotDuplicatePlugins_WhenCalledMultipleTimes` — Lazy-Loading korrekt (keine Duplikate)

### Hinweise
- Tests **behandeln nicht** den Aktivierungsstatus — es gibt keine Tests für `GetEnabledSourceCodeManagementPlugins()` o.ä.
- Tests sind **eng gekoppelt** an Plugin-DLL-Pfade (Verwendung von `typeof(GitHubPlugin).Assembly.Location`).

### `SettingsViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`

Testet die Settings-UI und Plugin-Verwaltung. Abgedeckte Szenarien:
- `ScmPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` — Auswahl eines SCM-Plugins lädt dessen Einstellungsgruppen

### Hinweise
- Tests sind **unvollständig** (nur ein Test in der gelesenen Ausschnitt).
- Nutzt Mocks für `IPluginManager`.
- **FEHLEND**: Tests für Plugin-Aktivierungsstatus-Toggle, Persistierung, Filterung.

## Hilfsmethoden

### `TaskDetailViewModelTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- Factory zur Erstellung von `TaskDetailViewModel`-Test-Instanzen

## Test-Hilfklassen

### `TestDbContextFactory`
- Erzeugt In-Memory-Datenbank-Kontexte für Tests

### `InMemoryCredentialStoreForSettings`
- Mock für `ICredentialStore` in `SettingsViewModelTests`

## Hinweise

- **FEHLEND**: Unit-Tests für neue Plugin-Manager-Methoden:
  - `IsPluginEnabled(string pluginName)`
  - `GetEnabledSourceCodeManagementPlugins()`
  - `GetEnabledDevelopmentAutomationPlugins()`
  - `SetPluginEnabled(string pluginName, bool enabled)`

- **FEHLEND**: Unit-Tests für neue `SettingsViewModel`-Funktionalität:
  - Laden von aktivierten Plugins in Collections
  - Toggle-Command ändert Aktivierungsstatus
  - Single-Plugin-Verhalten (Auswahl verstecken, wenn nur ein Plugin aktiv)

- **FEHLEND**: E2E-Tests:
  - Benutzer deaktiviert ein SCM-Plugin in den Einstellungen
  - In der Projektbearbeitung erscheint das Plugin nicht
  - Wenn nur noch ein KI-Plugin aktiv ist, verschwindet die Auswahl aus TaskDetailView
