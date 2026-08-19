← [Zurück zur Übersicht](index.md)

# Entwicklungsumgebungen — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|-----------|-----|-------|
| `IIdePlugin` | Interface | Plugin-Vertrag für IDE-Implementierungen; definiert Kompatibilitätsprüfung (`CheckCompatibilityAsync()`) sowie das generische Mehreinstiegspunkt-Paar `FindEntryPointsAsync()`/`OpenEntryPointAsync()` |
| `IdeEntryPoint` | Value Object (Record) | Immutabler Datenträger für einen konkreten IDE-Einstiegspunkt (`Path`, optional `DisplayName`); liegt in `Softwareschmiede.Plugin.Contracts` |
| `VisualStudioIdePlugin` | Plugin-Klasse | Implementierung für Visual Studio; prüft auf `.sln`/`.slnx`-Dateien; `FindEntryPointsAsync()` liefert einen `IdeEntryPoint` je gefundener Solution-Datei |
| `VisualStudioCodeIdePlugin` | Plugin-Klasse | Implementierung für VS Code; dient als universeller Fallback; `FindEntryPointsAsync()` liefert immer genau einen `IdeEntryPoint` (das Repository-Root) |
| `PluginSelectionService` | Service | Koordiniert die IDE-Plugin-Auflösung basierend auf Aktivierungsstatus, Reihenfolge und Kompatibilität; stellt zwei Auflösungsmethoden bereit: `ResolveIdePluginAsync()` (ein priorisiertes Plugin, für den Haupt-Button) und `ResolveAlleKompatiblenIdePluginsAsync()` (alle kompatiblen Plugins, für den Dropdown-Button); beide teilen sich die private Hilfsmethode `GetOrderedEnabledIdePluginsAsync()` zum Laden/Sortieren der aktivierten Plugins |
| `PluginActivationService` | Service | Verwaltet den Aktivierungsstatus von Plugins (Abfrage: `GetEnabledIdePluginsAsync()`) |
| `PluginManager` | Service | Registry für alle Plugins; registriert IDE-Plugins beim Start (`GetIdePlugins()`, `GetDefaultIdePlugin()`) |
| `AppEinstellungService` | Service | Persistiert Einstellungen in der Datenbank (Aktivierungsstatus und Reihenfolge) |
| `IdePluginOrderResolver` | Service-Klasse | Hilfsmethode zum Sortieren von Plugins nach `plugins.ide.order` Setting |
| `TaskDetailViewModel.OeffneIdeAsync` | Methode | Public API für den Haupt-Button des Split-Buttons; delegiert (über `ErmittleIdeEntryPointsAsync()`) an `PluginSelectionService.ResolveIdePluginAsync()` (ein priorisiertes Plugin) und nutzt `IIdePlugin.FindEntryPointsAsync()`/`OpenEntryPointAsync()` direkt — unabhängig von der konkreten Plugin-Implementierung |
| `TaskDetailViewModel.OeffneIdeAuswahlAsync` | Methode | Public API für den Dropdown-Teil des Split-Buttons; delegiert (über `ErmittleAggregierteIdeEinstiegspunkteAsync()`) an `PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync()` (alle aktivierten, kompatiblen Plugins) und aggregiert deren `IIdePlugin.FindEntryPointsAsync()`-Ergebnisse zu `(Plugin, EntryPoint)`-Tupeln, plugin-qualifiziert formatiert über `FormatiereAnzeigeWert()` |
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
     ┌─────────▼──────────────────────┐
     │  PluginSelectionService        │
     │  ResolveIdePluginAsync()       │  ← Haupt-Button (1 Plugin)
     │  ResolveAlleKompatiblenIde-    │  ← Dropdown-Button (alle
     │  PluginsAsync()                │    kompatiblen Plugins)
     └──────────┬──────────────────────┘
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
    ├─ Haupt-Button: TaskDetailViewModel.OeffneIdeCommand.OeffneIdeAsync()
    │      │
    │      ▼
    │  PluginSelectionService.ResolveIdePluginAsync()  ← EIN priorisiertes Plugin
    │      │
    │  ┌───┴───────────────┐
    │  ▼                   ▼
    │  PluginActivationService │ AppEinstellungService
    │  .GetEnabledIdePlugins() │ (liest plugins.ide.order)
    │      │                   │
    │      └────┬──────────────┘
    │           ▼
    │  Aktivierte Plugins in benutzerkonfigurierter Reihenfolge
    │           │
    │           ▼
    │  Für jedes Plugin: plugin.CheckCompatibilityAsync()
    │           │
    │      ┌────┴────┬─────────┐
    │      │          │        │
    │  Explicit    Fallback  Incompatible
    │      │          │        │
    │  Sofort      Merken   nächstes
    │  Auswahl,    (falls   Plugin
    │  Schleife    noch
    │  endet       keiner)
    │      │          │        │
    │      └─┬────────┘────────┘
    │        ▼
    │  EIN ausgewähltes Plugin (Explicit gewinnt, sonst erster Fallback,
    │  sonst GetDefaultIdePlugin())
    │        │
    │        ▼
    │  plugin.FindEntryPointsAsync()
    │        │
    │        ▼
    │  ├─ 0 → FileNotFoundException
    │  └─ ≥1 → OpenEntryPointAsync(ersterEntryPoint) direkt, ohne Dialog
    │
    └─ Dropdown-Button (nur sichtbar, wenn die AGGREGIERTE Gesamtanzahl
       über alle kompatiblen Plugins ≥2 ist — siehe KannIdeAuswaehlen):
           │
           ▼
       TaskDetailViewModel.OeffneIdeAuswahlCommand.OeffneIdeAuswahlAsync()
           │
           ▼
       PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync()
       ← ALLE aktivierten, kompatiblen Plugins (nicht nur das priorisierte)
           │
       ┌───┴───────────────┐
       ▼                   ▼
       PluginActivationService │ AppEinstellungService
       .GetEnabledIdePlugins() │ (liest plugins.ide.order)
           │                   │
           └────┬──────────────┘
                ▼
       Aktivierte Plugins in benutzerkonfigurierter Reihenfolge
                │
                ▼
       Für JEDES Plugin (kein früher Abbruch): plugin.CheckCompatibilityAsync()
                │
           ┌────┴────┬─────────┐
           │          │        │
       Explicit    Fallback  Incompatible
           │          │        │
       zu explicit  zu fall-  ignorieren
       Plugins      backPlugins
       hinzufügen   hinzufügen
           │          │        │
           └─┬────────┘────────┘
             ▼
       explicitPlugins ++ fallbackPlugins (beide in konfigurierter Reihenfolge)
       — leer? → einelementige Liste mit GetDefaultIdePlugin()
             │
             ▼
       Für JEDES kompatible Plugin: plugin.FindEntryPointsAsync()
             │
             ▼
       Zu (Plugin, EntryPoint)-Tupeln aggregieren
       (Plugin-Reihenfolge + Einstiegspunkt-Reihenfolge je Plugin bleiben erhalten)
             │
             ▼
       Anzahl aggregierter Einstiegspunkte?
             │
             ├─ 0 → FileNotFoundException
             │
             ├─ 1 → sofort OpenEntryPointAsync (kein Dialog)
             │
             └─ >1 → FormatiereAnzeigeWert() je Tupel
                      ("{PluginName}: {Bezeichnung}" bzw. nur "{PluginName}")
                      → Dialog zeigt alle aggregierten, plugin-qualifizierten
                        Einstiegspunkte
                         │
                         └─ Benutzer wählt oder bricht ab
                            → gewähltes Tupel: dessen Plugin öffnet den
                              gewählten Einstiegspunkt via OpenEntryPointAsync()
                              (nicht zwingend das für den Haupt-Button
                              priorisierte Plugin) oder nichts tun (Abbruch)
    │
    ▼
IProzessStarter startet die IDE
```

> **Hinweis:** Der frühere Sonderfall, bei dem eine eigene `IdeOeffnenService`-Klasse per Typ-Prüfung (`plugin is VisualStudioIdePlugin`) erkannte, ob ein Solution-Auswahl-Dialog nötig ist, wurde entfernt (die Klasse selbst existiert nicht mehr). Stattdessen liefert jedes `IIdePlugin` über `FindEntryPointsAsync()` generisch 0..n `IdeEntryPoint`-Kandidaten. Seit der Multi-Plugin-Aggregation lösen Haupt- und Dropdown-Button außerdem unterschiedlich viele Plugins auf: Der Haupt-Button verwendet weiterhin unverändert `PluginSelectionService.ResolveIdePluginAsync()` (genau ein priorisiertes Plugin, `TaskDetailViewModel.ErmittleIdeEntryPointsAsync()`) und öffnet dessen ersten Einstiegspunkt direkt; der Dropdown-Button verwendet `PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync()` (alle aktivierten, `Explicit`- oder `Fallback`-kompatiblen Plugins, `TaskDetailViewModel.ErmittleAggregierteIdeEinstiegspunkteAsync()`) und übergibt die plugin-qualifiziert aggregierten Kandidaten an den Auswahl-Callback (`WaehleEntryPointAsync`), der den bestehenden Solution-Auswahl-Dialog anzeigt (siehe [Dateisystem-Integration](../dateisystem-integration/architektur.md)).

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
3. Das neue Plugin wird automatisch in Settings, in der generischen Mehreinstiegspunkt-Logik des Haupt-Buttons (`TaskDetailViewModel.OeffneIdeInternAsync`/`ErmittleIdeEntryPointsAsync`) sowie in der aggregierten Mehrplugin-Logik des Dropdown-Buttons (`ErmittleAggregierteIdeEinstiegspunkteAsync`/`PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync`) berücksichtigt, ohne dass dort Sonderfälle für das neue Plugin ergänzt werden müssen

Beispiel für zukünftige Plugins: JetBrains Rider, Neovim, Sublime Text, etc.
