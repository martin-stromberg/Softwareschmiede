# Plugin-Implementierungen

Alle Plugins liegen unter `plugins/` als eigene Projekte in der Solution und werden zur Laufzeit als DLLs aus dem `plugins`-Ordner des Ausgabeverzeichnisses geladen (`PluginManager.DiscoverPlugins`, `AssemblyLoadContext.Default.LoadFromAssemblyPath`). Keines deklariert derzeit ein Symbol.

## SCM-Plugins (`GitPluginBase<TPlugin>`, `PluginType.SourceCodeManagement`)

| Klasse | Datei | `PluginName` | `PluginPrefix` |
|--------|-------|--------------|----------------|
| `BitbucketPlugin` | `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs` | "Bitbucket" | `Softwareschmiede.Bitbucket` |
| `GitHubPlugin` | `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` | "GitHub" | `Softwareschmiede.GitHub` |
| `LocalDirectoryPlugin` | `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs` | "Local Directory" | `LocalDirectoryPlugin` |

## KI-Plugins (`CliKiPluginBase`, `PluginType.DevelopmentAutomation`)

| Klasse | Datei | `PluginName` | `PluginPrefix` |
|--------|-------|--------------|----------------|
| `ClaudeCliPlugin` | `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs` | "Claude CLI" | `Softwareschmiede.ClaudeCli` |
| `CodexPlugin` | `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs` | "Codex CLI" | `Softwareschmiede.Codex` |
| `DevinPlugin` | `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs` | "Devin CLI" | `Softwareschmiede.Devin` |
| `GitHubCopilotPlugin` | `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` | "GitHub Copilot" | `Softwareschmiede.GitHubCopilot` |
| `KiSimulatorPlugin` | `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs` | "KI Simulator" | `Softwareschmiede.KiSimulator` |

## IDE-Plugins (built-in, `PluginType.Ide`)

`VisualStudioIdePlugin` und `VisualStudioCodeIdePlugin` — keine DLL-Plugins, sondern feste Bestandteile von `Softwareschmiede` (registriert über `PluginManager.RegisterBuiltInIdePlugins`, `PluginManager.cs:164–168`).

## Test-Modus-Filter

`PluginManager.IsAllowedInTestMode` (`PluginManager.cs:115–124`) lässt im Test-Modus (gesetzte `TestDatenbankPfad`-Umgebungsvariable) nur `LocalDirectory`, `KiSimulator`, `ClaudeCli`, `Codex`, `Devin` und `GitHubCopilot` zu — relevant für die in E2E-Tests sichtbaren KI-Symbole.
