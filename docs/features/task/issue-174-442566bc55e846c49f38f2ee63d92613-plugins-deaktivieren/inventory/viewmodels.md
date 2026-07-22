# ViewModels

## `SettingsViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`

### Eigenschaften (Plugin-bezogen)
| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `ScmPlugins` | `IReadOnlyList<IGitPlugin>` | Alle verfügbaren SCM-Plugins (ungefiltert) |
| `KiPlugins` | `IReadOnlyList<IKiPlugin>` | Alle verfügbaren KI-Plugins (ungefiltert) |
| `DefaultScmPlugin` | `IGitPlugin?` | Aktuell gewähltes Standard-SCM-Plugin |
| `DefaultKiPlugin` | `string?` | Name des Standard-KI-Plugins |
| `SelectedScmPluginSettings` | `IReadOnlyList<PluginSettingGroupEntry>` | Einstellungsgruppen des ausgewählten SCM-Plugins |
| `SelectedKiPluginSettings` | `IReadOnlyList<PluginSettingGroupEntry>` | Einstellungsgruppen des ausgewählten KI-Plugins |

### Commands
| Command | Beschreibung |
|---------|-------------|
| `LadenCommand` | Lädt alle Einstellungen (einschl. Plugins) |
| `SpeichernCommand` | Speichert alle Einstellungen (einschl. Plugin-Einstellungen) |
| `VerwerfenCommand` | Verwirft ungespeicherte Änderungen |
| `ScmPluginSelectedCommand` | Wird ausgelöst, wenn SCM-Plugin gewählt wird; lädt dessen Einstellungen |
| `KiPluginSelectedCommand` | Wird ausgelöst, wenn KI-Plugin gewählt wird; lädt dessen Einstellungen |

### Methoden (Plugin-bezogen)
| Methode | Kurzbeschreibung |
|---------|------------------|
| `LadenAsync()` | Lädt alle Einstellungen einschl. `ScmPlugins` und `KiPlugins` von `IPluginManager` |
| `SpeichernAsync()` | Speichert Plugin-Einstellungen über `PluginSettingsService` |
| `LoadScmPluginSettings()` | Lädt Einstellungsgruppen des ausgewählten SCM-Plugins |
| `LoadKiPluginSettings()` | Lädt Einstellungsgruppen des ausgewählten KI-Plugins |
| `LadePluginEinstellungen()` | Lädt Einstellungsgruppen für ein beliebiges Plugin |
| `SpeicherePluginEinstellungen()` | Speichert Einstellungen eines Plugins |

### Abhängigkeiten
- `IPluginManager`: Abrufen von verfügbaren Plugins
- `PluginSettingsService`: Persistierung von Plugin-Einstellungen
- `AppEinstellungService`: Persistierung von Default-Auswahlen

### Hinweise
- **FEHLEND**: Properties und Commands für Plugin-Aktivierungsstatus (z.B. `SourceCodeManagementPlugins`, `DevelopmentAutomationPlugins` als ObservableCollection mit Aktivierungsstatus, `TogglePluginEnabledCommand`).
- Lädt **alle** Plugins ohne Aktivierungsfilter (Zeile 210-214: `ScmPlugins = _pluginManager.GetSourceCodeManagementPlugins()` und `KiPlugins = _pluginManager.GetDevelopmentAutomationPlugins()`).

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

### Eigenschaften (Plugin-bezogen)
| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `VerfuegbareKiPlugins` | `ObservableCollection<string>` | Verfügbare KI-Plugin-Prefixe (ungefiltert) |
| `SelectedKiPluginPrefix` | `string?` | Aktuell gewähltes KI-Plugin-Prefix |

### Methoden (Plugin-bezogen)
| Methode | Kurzbeschreibung |
|---------|------------------|
| `LadeVerfuegbarePluginsAsync()` | Lädt verfügbare KI-Plugins vom `PluginSelectionService`; füllt `VerfuegbareKiPlugins` |
| `LadenAsync()` | Ruft `LadeVerfuegbarePluginsAsync()` auf |

### Abhängigkeiten
- `PluginSelectionService`: Abrufen verfügbarer KI-Plugin-Prefixe
- `IPluginManager`: (indirekt über PluginSelectionService)

### Hinweise
- Lädt Plugins in `LadeVerfuegbarePluginsAsync()` (Zeile 631-647):
  ```csharp
  var pluginNames = await _pluginSelectionService.GetAvailableKiPluginPrefixesAsync(ct);
  VerfuegbareKiPlugins.Clear();
  foreach (var name in pluginNames)
      VerfuegbareKiPlugins.Add(name);
  ```
- **FEHLEND**: Filterung nach Aktivierungsstatus — sollte nur aktive Plugins laden.
- **FEHLEND**: Single-Plugin-Verhalten — wenn nur ein Plugin aktiv ist, sollte es automatisch gewählt werden (ohne Dropdown angezeigt zu werden).

## `ProjectDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`

### Eigenschaften (Plugin-bezogen)
| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `SelectedRepository` | `GitRepository?` | Ausgewähltes Git-Repository |

### Hinweise
- Verwaltet Repositories und Aufgaben, nicht direkt Plugins.
- Könnte ebenfalls von Plugin-Aktivierungsfilter betroffen sein bei Repository-Auswahl (wenn ein Plugin nicht aktiv ist, können damit verknüpfte Repositories ggf. nicht mehr verwendet werden).

## Helper-Klassen

### `PluginSettingEntry`
Datei: `src/Softwareschmiede.App/ViewModels/PluginSettingEntry.cs`
- Editable Eintrag für ein einzelnes Plugin-Einstellungsfeld in der UI

### `PromptVorlageEntry`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` (innere Klasse)
- Editable Eintrag für eine Promptvorlage (nicht Plugin-bezogen, aber im `SettingsViewModel`)

### Hinweise
- **FEHLEND**: ViewModel-Klasse `IPluginActivationViewModel` oder ähnlich zur Darstellung von Plugins mit Aktivierungsstatus in der neuen Plugins-Einstellungsseite.
