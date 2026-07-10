# Interfaces

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

| Methode / Eigenschaft | Parameter | Rueckgabewert | Zweck |
|-----------------------|-----------|---------------|-------|
| `PluginName` | - | `string` | Anzeigename des Plugins. |
| `PluginPrefix` | - | `string` | Prefix fuer Credential-Store-Keys. |
| `GetSettingGroups` | - | `IReadOnlyList<PluginSettingGroup>` | Liefert dynamische Setting-Gruppen fuer die Settings-UI. |
| `PluginType` | - | `PluginType` | Typ des Plugins. |

## `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

| Methode / Eigenschaft | Parameter | Rueckgabewert | Zweck |
|-----------------------|-----------|---------------|-------|
| `StartCliAsync` | `string localRepoPath`, `string? parameters`, `CancellationToken ct` | `Task<ProcessStartInfo>` | Liefert Startinformationen fuer den CLI-Prozess. |
| `GetProcessWindowTitle` | `Guid aufgabeId` | `string` | Liefert erwarteten Fenstertitel. |
| `SupportsSessionContinuation` | - | `bool` | Gibt an, ob Session-Fortsetzung unterstuetzt wird. |
| `CheckHealthAsync` | `CancellationToken ct` | `Task<bool>` | Prueft Plugin-Verfuegbarkeit. |

## `IAiCliProvider`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IAiCliProvider.cs`

| Methode / Eigenschaft | Parameter | Rueckgabewert | Zweck |
|-----------------------|-----------|---------------|-------|
| `StartCliAsync` | `string workingDirectory`, `string? sessionParameter`, `CancellationToken ct` | `Task<Process>` | Alternativer Contract zum direkten Starten eines CLI-Prozesses. |
| `SupportsSessionContinuation` | - | `bool` | Gibt an, ob Session-Fortsetzung unterstuetzt wird. |

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Parameter | Rueckgabewert | Zweck |
|---------|-----------|---------------|-------|
| `GetSourceCodeManagementPlugins` | - | `IReadOnlyList<IGitPlugin>` | Liefert geladene SCM-Plugins. |
| `GetDevelopmentAutomationPlugins` | - | `IReadOnlyList<IKiPlugin>` | Liefert geladene KI-/Automatisierungs-Plugins. |
| `GetDefaultSourceCodeManagementPlugin` | - | `IGitPlugin` | Liefert Standard-SCM-Plugin. |
| `GetDefaultDevelopmentAutomationPlugin` | - | `IKiPlugin` | Liefert Standard-KI-Plugin. |
