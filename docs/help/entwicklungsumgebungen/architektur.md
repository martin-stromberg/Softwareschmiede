← [Zurück zur Übersicht](index.md)

# Entwicklungsumgebungen — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|-----------|-----|-------|
| `IIdePlugin` | Interface | Plugin-Vertrag für IDE-Implementierungen; definiert Kompatibilitätsprüfung und Repository-Öffnen |
| `VisualStudioIdePlugin` | Plugin-Klasse | Implementierung für Visual Studio; prüft auf `.sln`/`.slnx`-Dateien |
| `VisualStudioCodeIdePlugin` | Plugin-Klasse | Implementierung für VS Code; dient als universeller Fallback |
| `PluginSelectionService` | Service | Koordiniert die IDE-Plugin-Auflösung basierend auf Aktivierungsstatus, Reihenfolge und Kompatibilität |
| `PluginActivationService` | Service | Verwaltet den Aktivierungsstatus von Plugins (Abfrage: `GetEnabledIdePluginsAsync()`) |
| `PluginManager` | Service | Registry für alle Plugins; registriert IDE-Plugins beim Start (`GetIdePlugins()`, `GetDefaultIdePlugin()`) |
| `AppEinstellungService` | Service | Persistiert Einstellungen in der Datenbank (Aktivierungsstatus und Reihenfolge) |
| `IdePluginOrderResolver` | Service-Klasse | Hilfsmethode zum Sortieren von Plugins nach `plugins.ide.order` Setting |
| `IdeOeffnenService` | Service | Public API; delegiert an `PluginSelectionService.ResolveIdePluginAsync()` und `IIdePlugin.OpenRepositoryAsync()` |
| `IProzessStarter` | Interface | Startet externe Prozesse (Visual Studio/VS Code); wird von beiden Plugin-Klassen verwendet |
| `IVisualStudioCodeLocator` | Interface | Ermittelt den VS Code Installationspfad; wird von `VisualStudioCodeIdePlugin` verwendet |

## Abhängigkeiten

```
┌─────────────────────────────┐
│   SettingsViewModel / UI    │
│ (zeigt IDE-Plugins an)      │
└──────────────┬──────────────┘
               │
     ┌─────────▼─────────┐
     │  PluginManager    │
     │ (registriert IDEs)│
     └────────┬──────────┘
              │
    ┌─────────┴──────────┐
    │                    │
┌───▼────────┐    ┌──────▼──────────┐
│VisualStudio│    │VisualStudioCode│
│ IdePlugin  │    │   IdePlugin     │
└───┬────────┘    └────────┬────────┘
    │                      │
    └──────────┬───────────┘
               │
     ┌─────────▼──────────────┐
     │PluginSelectionService │
     │  (ResolveIdePlugin)    │
     └──────────┬─────────────┘
               /  \
        ┌─────▼    ▼────────┐
        │                   │
   ┌────▼────────┐   ┌──────▼────────┐
   │PluginActivation│ │AppEinstellung │
   │   Service      │  │   Service     │
   └────────────────┘  └───────────────┘
```

## Datenfluss

### IDE-Öffnen auslösen

```
User klickt "IDE öffnen" (Ribbon-Button der Aufgabendetailansicht)
         │
         ▼
TaskDetailViewModel.OeffneIdeAsync()
         │
         ▼
PluginSelectionService.ResolveIdePluginAsync()
         │
    ┌────┴───────────────┐
    │                    │
    ▼                    ▼
PluginActivationService │ AppEinstellungService
.GetEnabledIdePlugins() │ (liest plugins.ide.order)
         │              │
         └────┬─────────┘
              │
              ▼
    Aktivierte Plugins in
    benutzerkonfigurierter Reihenfolge
              │
              ▼
    Für jedes Plugin:
    plugin.CheckCompatibilityAsync()
              │
         ┌────┴────┬─────────┐
         │          │        │
    Explicit    Fallback  Incompatible
         │          │        │
    ┌────▼──┐   ┌───▼──┐    │
    │Sofort │   │Merken│    │
    │Auswahl│   │Fallb.│    │
    │       │   │      │    │
    └──┬────┘   └──┬───┘    │
       │           │        │
       └─┬─────────┘        │
         │                  │
    ┌────▼──────────────────┘
    │
    ▼
Ausgewähltes Plugin
    │
    ▼
plugin.OpenRepositoryAsync()
    │
    ├─ VisualStudio: FindSolutionFiles() → .sln → IProzessStarter
    │
    └─ VSCode: IVisualStudioCodeLocator → code-CLI → IProzessStarter
         │
         ▼
    IDE startet
```

> **Hinweis:** Findet `VisualStudioIdePlugin` mehrere Solutions, zeigt `TaskDetailViewModel` vor `plugin.OpenRepositoryAsync()` zusätzlich einen Solution-Auswahl-Dialog an (siehe [Dateisystem-Integration](../dateisystem-integration/architektur.md)); dieser Sonderfall ist in der vereinfachten Darstellung oben nicht enthalten.

### IDE-Plugin-Aktivierung in der UI

```
User öffnet Settings → Plugins Tab
         │
         ▼
SettingsViewModel lädt IDEs
         │
    ┌────┴────────────────────┐
    │                         │
    ▼                         ▼
PluginManager          PluginActivationService
.GetIdePlugins()       .IsPluginEnabledAsync()
    │                         │
    └────┬─────────────────────┘
         │
         ▼
Alle IDEs mit Aktivierungsstatus
angezeigt in UI
         │
    User ändert Checkbox
         │
         ▼
PluginActivationService.SetPluginEnabledAsync()
         │
         ▼
AppEinstellungService.SetSetting()
         │
         ▼
Datenbank aktualisiert
(plugins.enabled.<PluginPrefix>)
```

### IDE-Plugin-Reihenfolge ändern

```
User zieht Plugin nach oben
oder klickt Up-Button
         │
         ▼
SettingsViewModel.IdePluginOrder
wird aktualisiert
         │
         ▼
AppEinstellungService.SetSettingAsync()
         │
         ▼
Datenbank aktualisiert
(plugins.ide.order)
         │
         ▼
Beim nächsten IDE-Öffnen:
IdePluginOrderResolver.Apply()
nutzt neue Reihenfolge
```

## Skalierung und Zuverlässigkeit

### Performance

- **Kompatibilitätsprüfung:** Die Prüfung ist schnell (Datei-Existenz-Check oder sofortiger Fallback). Keine Caching oder Optimierungen erforderlich bei normaler Nutzung.
- **Plugin-Registry:** IDE-Plugins werden beim Programmstart einmalig registriert. Keine dynamischen Änderungen zur Laufzeit.

### Fehlertoleranz

- **Fehlende Aktivierungen:** Falls keine IDE-Plugins aktiv sind, wird das Standardplugin verwendet (Fallback zur Systemkonfiguration).
- **Fehlende IDEs:** Falls keine IDE installiert ist, wird eine aussagekräftige Fehlermeldung angezeigt.
- **Ungültige Reihenfolge:** Falls `plugins.ide.order` ungültige Prefixe enthält, werden diese ignoriert und die Entdeckungsreihenfolge verwendet.

### Erweiterbarkeit

Das System ist für neue IDE-Plugins konzipiert:
1. Implementiere `IIdePlugin` (CheckCompatibilityAsync, OpenRepositoryAsync)
2. Registriere das Plugin in `PluginManager`
3. Das neue Plugin wird automatisch in Settings und IDE-Öffnen-Logik berücksichtigt

Beispiel für zukünftige Plugins: JetBrains Rider, Neovim, Sublime Text, etc.
