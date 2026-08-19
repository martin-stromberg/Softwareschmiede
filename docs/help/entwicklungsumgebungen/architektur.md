← [Zurück zur Übersicht](index.md)

# Entwicklungsumgebungen — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|-----------|-----|-------|
| `IIdePlugin` | Interface | Plugin-Vertrag für IDE-Implementierungen; definiert Kompatibilitätsprüfung (`CheckCompatibilityAsync()`) sowie das generische Mehreinstiegspunkt-Paar `FindEntryPointsAsync()`/`OpenEntryPointAsync()` |
| `IdeEntryPoint` | Value Object (Record) | Immutabler Datenträger für einen konkreten IDE-Einstiegspunkt (`Path`, optional `DisplayName`); liegt in `Softwareschmiede.Plugin.Contracts` |
| `VisualStudioIdePlugin` | Plugin-Klasse | Implementierung für Visual Studio; prüft auf `.sln`/`.slnx`-Dateien; `FindEntryPointsAsync()` liefert einen `IdeEntryPoint` je gefundener Solution-Datei |
| `VisualStudioCodeIdePlugin` | Plugin-Klasse | Implementierung für VS Code; dient als universeller Fallback; `FindEntryPointsAsync()` liefert immer genau einen `IdeEntryPoint` (das Repository-Root) |
| `PluginSelectionService` | Service | Koordiniert die IDE-Plugin-Auflösung basierend auf Aktivierungsstatus, Reihenfolge und Kompatibilität |
| `PluginActivationService` | Service | Verwaltet den Aktivierungsstatus von Plugins (Abfrage: `GetEnabledIdePluginsAsync()`) |
| `PluginManager` | Service | Registry für alle Plugins; registriert IDE-Plugins beim Start (`GetIdePlugins()`, `GetDefaultIdePlugin()`) |
| `AppEinstellungService` | Service | Persistiert Einstellungen in der Datenbank (Aktivierungsstatus und Reihenfolge) |
| `IdePluginOrderResolver` | Service-Klasse | Hilfsmethode zum Sortieren von Plugins nach `plugins.ide.order` Setting |
| `TaskDetailViewModel.OeffneIdeAsync` / `OeffneIdeAuswahlAsync` | Methoden | Public API für Split-Button (Haupt- und Dropdown-Teil); delegieren an `PluginSelectionService.ResolveIdePluginAsync()` und nutzen `IIdePlugin.FindEntryPointsAsync()`/`OpenEntryPointAsync()` direkt — unabhängig von der konkreten Plugin-Implementierung |
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

### IDE-Öffnen auslösen (Split-Button)

```
User klickt "IDE öffnen" Split-Button (Ribbon in Aufgabendetailansicht)
    │
    ├─ Haupt-Button:
    │      │
    │      ▼
    │  TaskDetailViewModel.OeffneIdeCommand.OeffneIdeAsync()
    │      │
    │      └─ öffnet den ersten Einstiegspunkt direkt (ohne Dialog)
    │
    └─ Dropdown-Button (nur sichtbar bei ≥2 Einstiegspunkten):
           │
           ▼
       TaskDetailViewModel.OeffneIdeAuswahlCommand.OeffneIdeAuswahlAsync()
           │
           └─ zeigt Dialog bei mehreren Einstiegspunkten
              (wenn nur 1 gefunden → wird direkt geöffnet)
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
plugin.FindEntryPointsAsync()
    │
    ├─ VisualStudio: FindSolutionFiles() → je .sln/.slnx ein IdeEntryPoint
    │
    └─ VSCode: immer genau ein IdeEntryPoint (Repository-Root)
         │
         ▼
    Anzahl Einstiegspunkte?
    │
    ├─ 0 → FileNotFoundException
    │
    ├─ 1 → sofort plugin.OpenEntryPointAsync(einzigerEntryPoint)
    │
    └─ >1 → 
        ├─ Haupt-Button: OpenEntryPointAsync(ersterEntryPoint) direkt
        │
        └─ Dropdown-Button: waehleEntryPointAsync() aufrufen
                 │
                 ▼
            Dialog zeigt alle Einstiegspunkte
                 │
                 └─ Benutzer wählt oder bricht ab
                    → gewählten Einstiegspunkt via
                      plugin.OpenEntryPointAsync() öffnen
                      oder nichts tun (Abbruch)
         │
         ▼
    IProzessStarter startet die IDE
```

> **Hinweis:** Der frühere Sonderfall, bei dem eine eigene `IdeOeffnenService`-Klasse per Typ-Prüfung (`plugin is VisualStudioIdePlugin`) erkannte, ob ein Solution-Auswahl-Dialog nötig ist, wurde entfernt (die Klasse selbst existiert nicht mehr). Stattdessen liefert jedes `IIdePlugin` über `FindEntryPointsAsync()` generisch 0..n `IdeEntryPoint`-Kandidaten; `TaskDetailViewModel.OeffneIdeInternAsync()` verzweigt ausschließlich anhand der Anzahl der gefundenen Einstiegspunkte (und des aufrufenden Buttons), unabhängig von der konkreten Plugin-Implementierung. Bei mehreren Kandidaten übergibt der Dropdown-Button einen Auswahl-Callback (`WaehleEntryPointAsync`), der den bestehenden Solution-Auswahl-Dialog anzeigt; der Haupt-Button öffnet stattdessen direkt den ersten Kandidaten (siehe [Dateisystem-Integration](../dateisystem-integration/architektur.md)).

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
1. Implementiere `IIdePlugin` vollständig, inklusive der drei erzwungenen Methoden `CheckCompatibilityAsync`, `FindEntryPointsAsync` und `OpenEntryPointAsync` (letztere beiden liefern/öffnen die konkreten `IdeEntryPoint`-Kandidaten des Plugins, z. B. mehrere Workspace-Dateien) — der Compiler erzwingt die Implementierung aller drei Methoden
2. Registriere das Plugin in `PluginManager`
3. Das neue Plugin wird automatisch in Settings sowie in der generischen Mehreinstiegspunkt-Logik von `TaskDetailViewModel.OeffneIdeInternAsync`/`ErmittleIdeEntryPointsAsync` berücksichtigt, ohne dass dort Sonderfälle für das neue Plugin ergänzt werden müssen

Beispiel für zukünftige Plugins: JetBrains Rider, Neovim, Sublime Text, etc.
