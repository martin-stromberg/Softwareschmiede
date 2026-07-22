# Interfaces

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins` | - | `IReadOnlyList<IGitPlugin>` | Gibt alle geladenen SCM-Plugins zurück |
| `GetDevelopmentAutomationPlugins` | - | `IReadOnlyList<IKiPlugin>` | Gibt alle geladenen KI-Plugins zurück |
| `GetDefaultSourceCodeManagementPlugin` | - | `IGitPlugin` | Gibt das erste verfügbare SCM-Plugin zurück oder wirft Exception |
| `GetDefaultDevelopmentAutomationPlugin` | - | `IKiPlugin` | Gibt das priorisierte KI-Plugin zurück oder wirft Exception |

### Hinweise
- Interface ist minimal und fokussiert auf Abfrage ohne Aktivierungsstatus-Filterung.
- **FEHLEND**: Methoden wie `GetEnabledSourceCodeManagementPlugins()`, `GetEnabledDevelopmentAutomationPlugins()`, `SetPluginEnabled()`, `IsPluginEnabled()` (gemäß Anforderung).

## `IPlugin` / `IGitPlugin` / `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/...`

(Nicht vollständig gelesen, aber referenziert in `PluginManager`)

- `IPlugin`: Basis-Interface für alle Plugins
  - Eigenschaften: `PluginName`, `PluginPrefix`, `PluginType`
  - Methode: `GetSettingGroups()` — gibt Einstellungsgruppen zurück
- `IGitPlugin`: Erbt von `IPlugin` — SCM-Plugins
- `IKiPlugin`: Erbt von `IPlugin` — KI-Plugins
