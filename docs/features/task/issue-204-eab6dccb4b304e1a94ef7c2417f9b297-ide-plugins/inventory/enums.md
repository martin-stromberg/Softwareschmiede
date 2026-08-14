# Bestandsaufnahme: Enums

## `IdePluginCompatibility`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/IdePluginCompatibility.cs`

**Zweck:** Kompatibilitätsergebnis eines IDE-Plugins zu einem Repository, bestimmt die Auswahl-Priorität in `PluginSelectionService.ResolveIdePluginAsync()`

| Wert | Bedeutung | Verwendung in Plugins |
|------|-----------|---|
| `Explicit` | Das IDE-Plugin ist explizit kompatibel (z.B. `.sln` gefunden) — höchste Priorität. Plugin wird sofort ausgewählt und nicht weiter geprüft. | `VisualStudioIdePlugin.CheckCompatibilityAsync()` wenn `FindSolutionFiles()` ≥1 `.sln`/`.slnx` findet |
| `Fallback` | Das IDE-Plugin wird als Rückfall verwendet, wenn kein Plugin explizit kompatibel ist. Niedrigere Priorität als `Explicit`. | `VisualStudioCodeIdePlugin.CheckCompatibilityAsync()` (immer) |
| `Incompatible` | Das IDE-Plugin ist nicht kompatibel und wird bei der Auswahl nicht berücksichtigt. | `VisualStudioIdePlugin.CheckCompatibilityAsync()` wenn keine `.sln`/`.slnx` gefunden |

---

## `PluginType`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

**Zweck:** Plugin-Typen für Discovery und Registry im PluginManager

| Wert | Bedeutung |
|------|-----------|
| `SourceCodeManagement` | z.B. GitHub, GitLab |
| `DevelopmentAutomation` | z.B. Automatisierungs-Tools |
| `Ide` | IDE-Plugins (Visual Studio, VS Code, etc.) — **für die vorliegende Anforderung relevant** |

**Hinweis:** Beide IDE-Plugin-Implementierungen (`VisualStudioIdePlugin`, `VisualStudioCodeIdePlugin`) geben `PluginType.Ide` zurück.
