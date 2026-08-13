# Tests – Bestandsaufnahme IDE-Plugin-System

## Test-Klassen für Plugins

### `PluginSelectionServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests.cs`

Tests für die Plugin-Auflösung (explizit → Default → Fallback).

| Test-Methode | Was wird getestet? |
|-------------|-------------------|
| `ResolveDevelopmentAutomationPluginAsync_ShouldUseExplicitSelection_WhenProvided` | Verwendet explizit ausgewähltes Plugin mit höchster Priorität |
| `GetAvailableKiPluginPrefixesAsync_ShouldReturnOnlyActivePlugins` | Gibt nur die Prefixe aktiver KI-Plugins zurück |
| `ResolveDevelopmentAutomationPluginAsync_ShouldUseStoredDefault_WhenNoExplicitSelection` | Fällt auf gespeicherten Standard zurück |
| `ResolveDevelopmentAutomationPluginAsync_ShouldPreferCopilotProviderInFallback` | Bevorzugt Copilot im Fallback |

**Hilfsmethoden im Test:**
- `CreateKiPlugin(name, prefix)` – Mock für IKiPlugin
- `CreatePluginManager(plugins)` – Mock für IPluginManager
- `TestCliKiPlugin(name, prefix, providerDateiPraefix)` – Test-Implementation von CliKiPluginBase

**Abhängigkeiten:**
- `TestDbContextFactory.Create()` – In-Memory Test-DB
- `PluginDefaultSettingsService` – Persistierung von Standard-Plugins
- `PluginActivationService` – Filterung aktiver Plugins
- `PluginSelectionService` – System Under Test

---

### `PluginActivationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PluginActivationServiceTests.cs`

Tests für die Plugin-Aktivierungsverwaltung.

**Test-Struktur (typisch, vollständiger Inhalt nicht gelesen):**
- Tests für `IsPluginEnabledAsync()` (Default: true bei fehlendem Eintrag)
- Tests für `SetPluginEnabledAsync()` (Persistierung in DB)
- Tests für `GetEnabledSourceCodeManagementPluginsAsync()` (Filterung nach Status)
- Tests für `GetEnabledDevelopmentAutomationPluginsAsync()` (Filterung nach Status)

---

### `PluginManagerTests`
Datei: `src/Softwareschmiede.Tests\Infrastructure\Plugins\PluginManagerTests.cs`

Tests für Plugin-Discovery und -Registrierung.

**Test-Struktur (typisch):**
- Plugin-Discovery aus Test-Plugin-Verzeichnis
- Filterung nach PluginType
- Lazy-Initialization (Doppel-Locking)
- Test-Mode-Filter (Whitelist)
- Exception-Handling bei fehlerhaften DLLs

---

### `IdeOeffnenServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs`

Tests für IDE-Öffnen-Funktionalität.

| Test-Methode (typisch) | Was wird getestet? |
|-------------|-------------------|
| `FindeSolutions_ReturnsAllSlnAndSlnxFiles_WhenAvailable` | Findet `.sln` und `.slnx`-Dateien alphabetisch sortiert |
| `FindeSolutions_ReturnsEmpty_WhenDirectoryNotExists` | Gibt leere Liste bei fehlendem Verzeichnis |
| `FindeSolutions_ReturnsEmpty_WhenPathIsNull` | Gibt leere Liste bei null-Pfad |
| `OeffneSolution_ThrowsException_WhenPathIsEmpty` | Wirft Exception bei leerem Pfad |
| `OeffneSolution_CallsProzessStarter_WithCorrectArgument` | Ruft ProzessStarter auf (Shell-Execute: true) |
| `OeffneVisualStudioCode_ThrowsException_WhenDirectoryNotExists` | Wirft Exception bei fehlendem Verzeichnis |
| `OeffneVisualStudioCode_ThrowsException_WhenVsCodeNotAvailable` | Wirft Exception wenn VS Code nicht verfügbar |
| `OeffneVisualStudioCode_CallsProzessStarter_WithQuotedPath` | Ruft ProzessStarter mit gequottem Pfad auf |

**Mocks:**
- `IProzessStarter` – Für Aufrufverifikation
- `IVisualStudioCodeLocator` – Für VS-Code-Verfügbarkeit

---

## E2E-Tests für Plugins

### `E2E_PluginAktivierung`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_PluginAktivierung.cs`

End-to-End-Tests für Plugin-Aktivierung über die UI.

**Test-Struktur (typisch):**
- Deaktiviert ein Plugin über die Einstellungs-UI
- Verifiziert dass Aktivierungsstatus in DB persistiert wird
- Verifiziert dass Filterung das Plugin ausschließt

---

### `E2E_PluginAuswahlUndWechsel`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_PluginAuswahlUndWechsel.cs`

End-to-End-Tests für Plugin-Auswahl und -Wechsel.

---

### `E2E_PluginProjectDefault`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_PluginProjectDefault.cs`

End-to-End-Tests für Projekt-Defaults bei Plugins.

---

### `E2E_SettingsKiPluginPersistence`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_SettingsKiPluginPersistence.cs`

End-to-End-Tests für KI-Plugin-Einstellungen-Persistierung.

---

## Unit-Tests für Plugin-Abstraktion

### `GitPluginBaseTests`
Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`

Tests für die Git-Plugin-Basis-Abstraktion.

---

### `CliKiPluginBaseTests`
Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/CliKiPluginBaseTests.cs`

Tests für die CLI-KI-Plugin-Basis-Abstraktion.

---

## Infrastruktur-Tests

### Weitere Plugin-Tests
- `LocalDirectoryPluginIntegrationTests` – Integration Tests für LocalDirectory-Plugin
- `BitbucketPluginTests`, `GitHubPluginTests`, `CodexPluginTests`, etc. – Konkrete Plugin-Implementierungen

---

## Settings-ViewModel-Tests

### `SettingsViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`

Tests für die Settings-UI (teilweise für Plugins).

**Test-Struktur (typisch):**
- Laden aller Plugin-Listen (SCM, KI)
- Aktivierungs-Status-UI-Binding
- Persistierung von Einstellungen

---

### `TaskDetailViewModelTests_PluginAktivierung`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`

Tests für Plugin-Aktivierung im Task-Detail-ViewModel.

---

### `TaskDetailViewModelTests_VisualStudioCode`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs`

Tests für Visual-Studio-Code-Öffnen in Task-Details (betrifft zukünftige IDE-Plugin-Integration).

---

## Hilfsmethoden und Test-Infrastruktur

### `TestDbContextFactory`
Ort: `src/Softwareschmiede.Tests/Helpers/`

Erstellt In-Memory-Datenbank-Kontexte für Tests.

### `CreateKiPlugin()`, `CreateGitPlugin()`
Ort: `src/Softwareschmiede.Tests/Helpers/` (typisch in PluginSelectionServiceTests)

Hilfsmethoden zum Erstellen von Mock-Plugins für Unit-Tests.

### `TestCliKiPlugin`
Ort: In Test-Datei selbst

Test-Implementation von `CliKiPluginBase` mit konfigurierbarem `ProviderDateiPraefix`.

---

## Zu implementierende Tests (laut Anforderung)

- Unit-Tests für `VisualStudioIdePlugin.CheckCompatibilityAsync()`
- Unit-Tests für `VisualStudioCodeIdePlugin.CheckCompatibilityAsync()`
- Unit-Tests für `PluginSelectionService.ResolveIdePluginAsync()`
- Integration-Tests für IDE-Aufruf mit mehreren aktivierten Plugins

