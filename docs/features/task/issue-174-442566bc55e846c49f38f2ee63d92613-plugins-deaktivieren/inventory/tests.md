# Tests

## Testklassen

### `SettingsViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`

Unit-Tests für `SettingsViewModel` mit Mock-Setup für Services und Plugins.

**Test-Infrastruktur:**
- TestDbContextFactory für In-Memory-Datenbank
- Mock-Objekte für `IPluginManager`, `IGitPlugin`, `IKiPlugin`
- In-Memory-Implementierung `InMemoryCredentialStoreForSettings` (Zeilen 513–523)

**Tests für Plugin-Auswahl und Einstellungen:**
- `ScmPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` — Testet, dass ScmPluginSelectedCommand die Einstellungsgruppen lädt
- `ScmPluginSelectedCommand_WithMultipleFields_LoadsAllValues` — Testet mehrere Feldtypen
- `KiPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` — Testet KI-Plugin-Auswahl
- `LadenAsync_LaedtDefaultPlugine` — Testet Laden gespeicherter Standard-Plugins

**Tests für Plugin-Aktivierung:**
- `Laden_BefuelltAktivierungsCollections_MitStatus` (Zeilen 455–474) — Testet, dass `SourceCodeManagementPlugins` und `DevelopmentAutomationPlugins` Collections mit Aktivierungsstatus befüllt werden
- `TogglePlugin_UndSpeichern_PersistiertStatus` (Zeilen 477–492) — Testet Aktivierungsstatus-Änderung und Persistierung
- `Speichern_VerhindertDeaktivierenDesLetztenPlugins` (Zeilen 495–510) — Testet Validierung `ValidierePluginAktivierung`

**Tests für Speicherung:**
- `SpeichernAsync_SpeichertDefaultPlugine_Und_EinstellungswerteFuerScm` — Testet Plugin- und Einstellungswert-Persistierung
- `SpeichernAsync_PersistiertVsCodeFallback` — Testet App-Einstellungen
- `SpeichernAsync_ValidierungFehlgeschlagen_ZeigtFehlerMeldung` — Testet Validierungslogik

**Hilfsmethoden:**
- `CreateSut()` (Zeilen 54–63) — Erzeugt SettingsViewModel-Instanz mit mocked Dependencies
- `CreateScmPluginMock(string pluginName, IReadOnlyList<PluginSettingGroup>? groups = null)` (Zeilen 65–73) — Mock-Factory für SCM-Plugins
- `CreateKiPluginMock(string pluginName, IReadOnlyList<PluginSettingGroup>? groups = null, string? pluginPrefix = null)` (Zeilen 75–86) — Mock-Factory für KI-Plugins

---

### `PluginActivationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PluginActivationServiceTests.cs`

Unit-Tests für `PluginActivationService`.

**Tests:**
- `IsPluginEnabled_LiefertTrue_WennKeinEintragVorhanden` (Zeilen 14–28) — Testet Default-Verhalten (Fehlender Eintrag = aktiviert)
- `SetPluginEnabled_PersistiertUndLiestZurueck` (Zeilen 31–45) — Testet Set/Get-Zyklus
- `GetEnabledSourceCodeManagementPlugins_FiltertDeaktivierte` (Zeilen 48–66) — Testet Filterung deaktivierter SCM-Plugins
- `GetEnabledDevelopmentAutomationPlugins_FiltertDeaktivierte` (Zeilen 69–87) — Testet Filterung deaktivierter KI-Plugins

**Hilfsmethoden:**
- `CreateGitPlugin(string name, string prefix)` (Zeilen 89–97) — Mock-Factory für IGitPlugin
- `CreateKiPlugin(string name, string prefix)` (Zeilen 99–107) — Mock-Factory für IKiPlugin

---

## Integration Tests

### `PluginSettingsServiceIntegrationTests`
Datei: `src/Softwareschmiede.Tests/ServiceIntegration/PluginSettingsServiceIntegrationTests.cs`

Integration-Tests für `PluginSettingsService` (nicht fully gelesen, aber referenziert).

---

## E2E-Tests

### SettingsView E2E
Datei: Möglicherweise in `src/Softwareschmiede.Tests/E2E/` (nicht explizit untersucht)

Die requirement.md erwähnt mögliche E2E-Test-Anpassungen für die neue UI-Struktur (Zeile 106), suggeriert aber, dass bei Bestehen keine neuen Tests geschrieben werden müssen — nur Anpassung, falls `OnPluginActivationItemPreviewMouseLeftButtonDown`-Logik geändert wird.
