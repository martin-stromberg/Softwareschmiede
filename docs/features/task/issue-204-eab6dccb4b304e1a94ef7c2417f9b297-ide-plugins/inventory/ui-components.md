# UI-Komponenten – Bestandsaufnahme IDE-Plugin-System

## Settings-ViewModel

### `SettingsViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`

Zentrale ViewModel für die Einstellungsseite mit vollständiger Plugin-Verwaltung.

**Plugin-relevante Properties:**
- `SourceCodeManagementPlugins` (ObservableCollection<PluginActivationEntry>) – SCM-Plugins mit Aktivierungsstatus
- `DevelopmentAutomationPlugins` (ObservableCollection<PluginActivationEntry>) – KI-Plugins mit Aktivierungsstatus
- `ScmPlugins` (IReadOnlyList<IGitPlugin>) – Rohe SCM-Plugin-Liste
- `KiPlugins` (IReadOnlyList<IKiPlugin>) – Rohe KI-Plugin-Liste
- `DefaultScmPlugin` (IGitPlugin?) – Aktuell gewähltes Standard-SCM-Plugin
- `DefaultKiPlugin` (string?) – Aktuell gewähltes Standard-KI-Plugin-Prefix
- `SelectedPlugin` (PluginActivationEntry?) – Im Plugins-Register ausgewählter Eintrag
- `SelectedPluginSettings` (IReadOnlyList<PluginSettingGroupEntry>) – Einstellungsgruppen des ausgewählten Plugins
- `OpenVisualStudioCodeWhenNoSolutionFound` (bool) – IDE-bezogene Einstellung (VS-Code-Fallback)

**Plugin-relevante Commands:**
- `ScmPluginSelectedCommand` – Wird ausgelöst wenn Nutzer ein SCM-Plugin wählt (lädt Einstellungsgruppen)
- `KiPluginSelectedCommand` – Wird ausgelöst wenn Nutzer ein KI-Plugin wählt
- `PluginSelectedCommand` – Wird ausgelöst wenn Nutzer im Plugins-Register einen Listeneintrag wählt

**Abhängigkeiten:**
- `IPluginManager` – Lädt alle verfügbaren Plugins
- `PluginActivationService` – Prüft Aktivierungsstatus
- `PluginSettingsService` – Lädt Plugin-Einstellungen
- `AppEinstellungService` – Persistiert Einstellungen

**Zu erweitern laut Anforderung:**
- `IdePlugins` (IReadOnlyList<IIdePlugin>) – Rohe IDE-Plugin-Liste (analog SCM/KI)
- `DevelopmentEnvironmentPlugins` (ObservableCollection<PluginActivationEntry>) – IDE-Plugins mit Aktivierungsstatus (neue Gruppe)
- `DefaultIdePlugin` (string?) – Aktuell gewähltes Standard-IDE-Plugin-Prefix
- `IdePluginOrder` (List<string>) – Reihenfolge der IDE-Plugins (für Drag & Drop oder Up/Down-Buttons)

---

## Plugin-Eintrags-ViewModel

### `PluginActivationEntry`
Datei: `src/Softwareschmiede.App/ViewModels/PluginActivationEntry.cs`

Darstellbarer Listeneintrag im Plugins-Register mit Aktivierungsstatus.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Plugin` | IPlugin | Das zugehörige Plugin (read-only) |
| `PluginName` | string | Anzeigename des Plugins (read-only) |
| `PluginPrefix` | string | Eindeutiger Prefix des Plugins (read-only) |
| `IsEnabled` | bool | Aktivierungsstatus des Plugins (bindbar/edit) |

**Initialisierung:**
```csharp
new PluginActivationEntry(IPlugin plugin, bool isEnabled)
```

**Verwendung:**
- In `SettingsViewModel.SourceCodeManagementPlugins` (ObservableCollection)
- In `SettingsViewModel.DevelopmentAutomationPlugins` (ObservableCollection)
- In `SettingsViewModel.SelectedPlugin` (aktuell ausgewählter Eintrag)

**Zu erweitern laut Anforderung:**
- Wird auch in `SettingsViewModel.DevelopmentEnvironmentPlugins` (zukünftig: IDE-Plugins) verwendet
- Optional: Zusätzliche Eigenschaften für Reihenfolge (z.B. `OrderIndex` für Drag & Drop)

---

## Plugin-Einstellungs-ViewModel

### `PluginSettingEntry`
Datei: `src/Softwareschmiede.App/ViewModels/PluginSettingEntry.cs`

Repräsentiert eine bearbeitbare Einstellung eines Plugins.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Field` | PluginSettingField | Feld-Definition des Einstellungsfelds (read-only) |
| `FieldType` | PluginSettingFieldType | Feldt-Typ (Shortcut zu Field.FieldType) |
| `Value` | string | Aktueller Wert des Felds als Zeichenkette (bindbar) |
| `BoolValue` | bool | Aktueller Wert als Boolean für Checkbox-Binding (bindbar) |

**Verwendung:**
- In `PluginSettingGroupEntry.Entries` (IReadOnlyList<PluginSettingEntry>)
- Gebunden an Editier-UI für Plugin-Konfiguration

---

### `PluginSettingGroupEntry`
Datei: `src/Softwareschmiede.App/ViewModels/PluginSettingEntry.cs` (Klasse 2)

Repräsentiert eine Plugin-Einstellungsgruppe mit ihren Feldern.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `GroupName` | string | Name der Gruppe (read-only) |
| `Entries` | IReadOnlyList<PluginSettingEntry> | Felder der Gruppe als bearbeitbare Einträge (read-only) |

**Verwendung:**
- In `SettingsViewModel.SelectedPluginSettings` (IReadOnlyList<PluginSettingGroupEntry>)
- UI-Gruppierung von Plugin-Einstellungen

---

## Plugin-Einstellungs-Services

### `PluginSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`

Lädt und persistiert Plugin-Einstellungen aus/zu `ICredentialStore` (oder äquivalent).

**Hauptmethoden (typisch):**
- `GetPluginSettingsAsync(plugin, ct)` → `IReadOnlyList<PluginSettingGroupEntry>`
- `SavePluginSettingsAsync(plugin, settings, ct)` → Task

**Speicherort:** Credential Store unter Schlüsseln `<PluginPrefix>.<FieldKey>`

---

## View-Komponenten (XAML)

### `PluginSelectionDialog.xaml` / `PluginSelectionDialog.xaml.cs`
Datei: `src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml(.cs)`

Dialog zur Auswahl eines Plugins (z.B. wenn mehrere Plugins verfügbar sind).

**DataContext:** `PluginSelectionDialogViewModel`

---

### `PluginSettingFieldTemplateSelector.cs`
Datei: `src/Softwareschmiede.App/Views/PluginSettingFieldTemplateSelector.cs`

Selektiert passende DataTemplate für `PluginSettingEntry` basierend auf `FieldType` (Text, Boolean, Dropdown, etc.).

---

### `PluginSettingEntryEditHelper.cs`
Datei: `src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs`

Hilfklasse für Editier-Logik von Plugin-Einstellungen in der UI.

---

## Zu implementierende UI-Komponenten (laut Anforderung)

### Neue IDE-Plugins-Sektion im Plugins-Tab
**Ort:** SettingsView (bestehende Einstellungs-Seite)

**Struktur:**
- **„Integrierte Entwicklungsumgebungen (IDE)"** – Neue Gruppe neben bestehenden „Quellcodeverwaltungs-Plugins" und „KI-Plugins"
- **IDE-Plugins-Liste:** ObservableCollection analog zu SCM/KI
  - Aktivierungs-CheckBox für jedes IDE-Plugin
  - PluginName anzeigen
  - Beispiel: „Visual Studio" mit Checkbox, „Visual Studio Code" mit Checkbox
- **Reihenfolge-Control:** Für Priorisierung
  - Option 1: Drag & Drop Sortierung
  - Option 2: Up/Down-Buttons zum Verschieben
  - Speichert Reihenfolge in `plugins.ide.order`-Setting
- **Aktivierungs-Validierung:** Mindestens ein IDE-Plugin muss aktiv bleiben
  - Deaktivierung verhindern wenn nur ein Plugin aktiv ist
  - Optional: Meldung „Sie müssen mindestens ein IDE-Plugin aktiviert lassen"

**ViewModel-Erweiterung (SettingsViewModel):**
- Neue Property: `DevelopmentEnvironmentPlugins` (ObservableCollection<PluginActivationEntry>)
- Neue Property: `DefaultIdePlugin` (string?)
- Neue Property: `IdePluginOrder` (List<string>)
- Neuer Command: `IdePluginSelectedCommand`
- Neue Befehle für Reihenfolge: `IdePluginMoveUpCommand`, `IdePluginMoveDownCommand` (bei Up/Down-Buttons)

**Binding-Punkte:**
- `DevelopmentEnvironmentPlugins` → ListBox/DataGrid für Plugin-Liste
- `IsEnabled` → CheckBox für jedes Plugin
- `SelectedPlugin` → Lädt zugehörige Einstellungen
- `DefaultIdePlugin` → ComboBox oder AutoComplete für Standard-Plugin

---

## Value-Objects

### `PluginSettingGroup`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingGroup.cs`

Definiert eine Gruppe von Einstellungsfeldern für ein Plugin.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `GroupName` | string | Name der Gruppe (z.B. "Authentifizierung") |
| `Fields` | IReadOnlyList<PluginSettingField> | Felder der Gruppe |

---

### `PluginSettingField`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs`

Definiert ein bearbeitbares Einstellungsfeld.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `FieldKey` | string | Eindeutiger Schlüssel (wird mit PluginPrefix kombiniert) |
| `Label` | string | Anzeigename für die UI |
| `Placeholder` | string? | Platzhalter-Text (optional) |
| `FieldType` | PluginSettingFieldType | Typ des Felds (Text, Boolean, etc.) |
| `IsRequired` | bool | Ist das Feld Pflichtfeld? |
| `DefaultValue` | string? | Standardwert (optional) |

---

### `PluginSettingFieldType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`

Enum für Feldtypen bei Plugin-Einstellungen.

| Wert | Bedeutung |
|------|-----------|
| `Text` | Einfaches Text-Eingabefeld |
| `Password` | Passwort-Eingabefeld (maskiert) |
| `Boolean` | Checkbox (true/false) |
| `Dropdown` | Dropdown/Auswahlliste (Optionen definierbar) |

