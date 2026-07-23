# Datenmodelle

## `PluginActivationEntry`
Datei: `src/Softwareschmiede.App/ViewModels/PluginActivationEntry.cs`

Listeneintrag im Plugins-Register mit Aktivierungsstatus und Anzeigeinformationen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Plugin` | `IPlugin` | Das zugehörige Plugin-Objekt (IGitPlugin oder IKiPlugin) |
| `PluginName` | `string` | Anzeigename des Plugins (z. B. "GitHub", "Claude") |
| `PluginPrefix` | `string` | Eindeutiger Präfix des Plugins (z. B. "github", "claude") |
| `IsEnabled` | `bool` (Property mit SetProperty) | Aktivierungsstatus des Plugins, bindbar, TwoWay-Binding genutzt |

**Binding-Kontext:** Wird in `SourceCodeManagementPlugins` und `DevelopmentAutomationPlugins` ObservableCollections der SettingsViewModel verwendet. Der aktuelle Eintrag wird in `SelectedPlugin` gepuffert.

---

## `PromptVorlageEntry`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` (Lines 507–541)

Editierbarer Eintrag für Promptvorlagen in den Einstellungen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Promptvorlage |
| `IsNeu` | `bool` | Gibt an, ob der Eintrag noch nicht persistiert ist |
| `Name` | `string` (Property mit SetProperty) | Anzeigename der Vorlage |
| `Prompttext` | `string` (Property mit SetProperty) | Der Prompttext der Vorlage |
