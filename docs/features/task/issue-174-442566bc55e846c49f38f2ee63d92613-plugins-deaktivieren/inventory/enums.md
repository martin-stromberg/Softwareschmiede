# Enums

## `PluginKategorie`
Datei: `src/Softwareschmiede/Domain/Enums/PluginKategorie.cs`

| Wert | Bedeutung |
|------|-----------|
| `Git` | Git-Provider Plugin (z.B. GitHub, GitLab) |
| `Ki` | KI-Plugin (z.B. GitHub Copilot) |

### Hinweise
- Wird in `PluginKonfiguration` genutzt zur Klassifizierung von Plugins.
- Spiegelt die beiden Plugin-Typen wider (`PluginType.SourceCodeManagement` und `PluginType.DevelopmentAutomation` aus dem Plugin-Contract).

## `PluginType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs` (nicht vollständig gelesen)

- Definiert Plugin-Typen im Plugin-Contract
- Wird von geladenen Plugins implementiert
