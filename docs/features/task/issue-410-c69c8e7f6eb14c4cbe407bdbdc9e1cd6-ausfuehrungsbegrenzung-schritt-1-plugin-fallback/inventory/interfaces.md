# Interfaces

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs` (Verwendung in `PluginSelectionService.cs`)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetDevelopmentAutomationPlugins` | — | `IReadOnlyList<IKiPlugin>` | Alle **geladenen** KI-Plugins, ungefiltert (inkl. deaktivierter) |
| `GetDefaultDevelopmentAutomationPlugin` | — | `IKiPlugin` | Fallback-Plugin, wenn Liste leer |
| `GetSourceCodeManagementPlugins` | — | `IReadOnlyList<IGitPlugin>` | SCM-Plugins (Orchestrator Z. 88) |

## `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

| Member | Typ | Zweck |
|--------|-----|-------|
| `PluginPrefix` / `PluginName` | `string` | Identifikation (Matching in der Auflösungskette) |
| `CheckHealthAsync` | `Task<bool>` | Startbereitschafts-Prüfung vor CLI-Start (Orchestrator Z. 168) |

## `IScmEventSource` / `IScmEventResponder`
Genutzt in `ScmEventOrchestrator.DispatchCoreAsync` Z. 88-93 — für diese Anforderung unverändert.
