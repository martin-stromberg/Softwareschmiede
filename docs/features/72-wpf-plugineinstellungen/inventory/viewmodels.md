# ViewModels

## `SettingsViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Arbeitsverzeichnis` | `string?` | Arbeitsverzeichnis für Repository-Klone |
| `DesignMode` | `string` | Aktueller Design-Modus (Light/Dark) |
| `DesignModes` | `IEnumerable<string>` | Verfügbare Design-Modi |
| `DefaultKiPlugin` | `string?` | Standard-KI-Plugin (gespeichert als String) |
| `ScmPlugins` | `IReadOnlyList<IGitPlugin>` | Alle verfügbaren SCM-Plugins |
| `KiPlugins` | `IReadOnlyList<IKiPlugin>` | Alle verfügbaren KI-Plugins |
| `DefaultScmPlugin` | `IGitPlugin?` | Aktuell gewähltes Standard-SCM-Plugin (als IGitPlugin-Objekt) |
| `BenachrichtigungsModus` | `BenachrichtigungsModus` | Benachrichtigungsmodus-Einstellung |
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | `string?` | Fehlermeldung bei Fehler |
| `ErfolgsMeldung` | `string?` | Erfolgsmeldung nach dem Speichern |
| `LadenCommand` | `ICommand` | Lädt alle Einstellungen asynchron |
| `SpeichernCommand` | `ICommand` | Speichert alle Einstellungen asynchron |
| `VerwerfenCommand` | `ICommand` | Verwirft alle nicht gespeicherten Einstellungen |

Abhängigkeiten:
- `AppEinstellungService` für Persistierung
- `ArbeitsverzeichnisSettingsService` für Arbeitsverzeichnis
- `DarkModeService` für Design-Modus
- `IPluginManager` für Plugin-Discovery

Nicht vorhanden (erforderlich für Feature 72):
- `ScmPluginSettings`: `IReadOnlyList<PluginSettingGroup>` — Setting-Groups des aktuell gewählten SCM-Plugins
- `SelectedScmPluginSettings`: `IReadOnlyList<PluginSettingGroup>` — Aktuell angezeigte SCM-Plugin-Einstellungen
- `KiPluginSettings`: `IReadOnlyList<PluginSettingGroup>` — Setting-Groups des aktuell gewählten KI-Plugins
- `SelectedKiPluginSettings`: `IReadOnlyList<PluginSettingGroup>` — Aktuell angezeigte KI-Plugin-Einstellungen
- `ScmPluginSelectedCommand`: Command für SCM-Plugin-Wechsel
- `KiPluginSelectedCommand`: Command für KI-Plugin-Wechsel

---

## `PluginSettingsViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | `string?` | Fehlermeldung bei Fehler |
| `ErfolgMeldung` | `string?` | Erfolgsmeldung nach Speichern |
| `Plugins` | `ObservableCollection<PluginWithSettingsEntry>` | Alle Plugins mit ihren Einstellungsgruppen |
| `LadenCommand` | `ICommand` | Lädt alle Plugin-Einstellungen |
| `SpeichernCommand` | `ICommand` | Speichert alle geänderten Plugin-Einstellungen |

Abhängigkeiten:
- `IPluginManager` für Plugin-Discovery
- `PluginSettingsService` für Persistierung von Einstellungswerten

---

## `PluginSettingEntry`
Datei: `src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`

Hilfsklasse für die Darstellung eines einzelnen Einstellungsfeldes.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Field` | `PluginSettingField` | Feld-Definition |
| `FieldType` | `PluginSettingFieldType` | Feldtyp (Shortcut zu `Field.FieldType`) |
| `Value` | `string` | Aktueller Wert als Zeichenkette (bidirektionales Binding mit BoolValue) |
| `BoolValue` | `bool` | Aktueller Wert als Boolean (für Checkbox bei `FieldType == Boolean`) |

---

## `PluginSettingGroupEntry`
Datei: `src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`

Hilfsklasse für die Darstellung einer Plugin-Einstellungsgruppe.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GroupName` | `string` | Name der Gruppe (z.B. "Authentifizierung") |
| `Entries` | `IReadOnlyList<PluginSettingEntry>` | Bearbeitbare Einträge der Felder |

---

## `PluginWithSettingsEntry`
Datei: `src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`

Hilfsklasse für die Darstellung eines Plugins mit seinen Einstellungsgruppen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Plugin` | `IPlugin` | Das Plugin-Objekt |
| `SettingGroups` | `IReadOnlyList<PluginSettingGroupEntry>` | Einstellungsgruppen des Plugins |
