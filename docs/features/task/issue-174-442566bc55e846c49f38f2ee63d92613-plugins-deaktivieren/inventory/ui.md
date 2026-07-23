# UI-Komponenten

## `SettingsView.xaml`
Datei: `src/Softwareschmiede.App/Views/SettingsView.xaml`

WPF UserControl für die Einstellungsseite mit Plugins-Register-Tab.

### Plugins-Tab Struktur (Zeilen 226–376)

**Grid-Layout (Zeilen 233–238):**
- Column 0 (Width=360): Linke Spalte — Standard-Plugin-Auswahl und Aktivierungslisten
- Column 1 (Width=16): Spacer
- Column 2 (Width=*): Rechte Spalte — Einstellungsgruppen des ausgewählten Plugins

### Linke Spalte: Standard-Plugins und Aktivierungslisten (Zeilen 241–339)

**Standard SCM-Plugin (Zeilen 244–259):**
- TextBlock "Standard SCM-Plugin" (Zeilen 245–248)
- ComboBox `SelectedValue="{Binding DefaultScmPlugin}"` mit SelectionChanged-Handler
- ItemTemplate: TextBlock mit `Text="{Binding PluginName}"`

**Standard KI-Plugin (Zeilen 262–277):**
- TextBlock "Standard KI-Plugin" (Zeilen 262–265)
- ComboBox `SelectedValue="{Binding DefaultKiPlugin}"` mit SelectionChanged-Handler
- ItemTemplate: TextBlock mit `Text="{Binding PluginName}"`

**Aktivierung SCM-Plugins (Zeilen 281–308):**
- TextBlock "Quellcodeverwaltung" (Header)
- ListBox `ItemsSource="{Binding SourceCodeManagementPlugins}"` mit `SelectionChanged="OnPluginSelectionChanged"`
- ItemTemplate (Zeilen 294–307):
  ```
  StackPanel (Horizontal)
  ├─ CheckBox: IsChecked="{Binding IsEnabled, Mode=TwoWay}" 
  └─ TextBlock: Text="{Binding PluginName}", Foreground="{DynamicResource PrimaryTextBrush}"
  ```
- ItemContainerStyle: EventSetter für PreviewMouseLeftButtonDown → `OnPluginActivationItemPreviewMouseLeftButtonDown`

**Aktivierung KI-Plugins (Zeilen 311–337):**
- TextBlock "KI" (Header)
- ListBox `ItemsSource="{Binding DevelopmentAutomationPlugins}"` mit `SelectionChanged="OnPluginSelectionChanged"`
- ItemTemplate (Zeilen 323–336):
  ```
  StackPanel (Horizontal)
  ├─ CheckBox: IsChecked="{Binding IsEnabled, Mode=TwoWay}"
  └─ TextBlock: Text="{Binding PluginName}", Foreground="{DynamicResource PrimaryTextBrush}"
  ```
- ItemContainerStyle: EventSetter für PreviewMouseLeftButtonDown → `OnPluginActivationItemPreviewMouseLeftButtonDown`

### Rechte Spalte: Einstellungsgruppen des ausgewählten Plugins (Zeilen 342–374)

**ScrollViewer mit ItemsControl (Zeilen 342–374):**
- `ItemsSource="{Binding SelectedPluginSettings}"`
- Template zeigt Einstellungsgruppen und deren Felder des aktuellen `SelectedPlugin`
- Verwendet `PluginSettingFieldTemplateSelector` für verschiedene Feldtypen (Text, Secret, Url, Integer, Boolean, Enum, FilePath, CommandLineParameters)

**Fehlende Elemente (gemäß Anforderung):**
- Keine Header mit Plugin-Namen im rechten Bereich
- Keine Toggle/CheckBox für Plugin-Aktivierung im rechten Bereich
- Kontrastprobleme mit TextBlock-Farben in den ListBox-Items möglich

---

## `SettingsView.xaml.cs`
Datei: `src/Softwareschmiede.App/Views/SettingsView.xaml.cs`

### Event-Handler

**`OnPluginSelectionChanged` (Zeilen 32–49):**
- Event: `SelectionChanged` auf ComboBoxen und ListBoxen im Plugins-Tab
- Funktion: Leitet das ausgewählte Element an das passende ViewModel-Kommando weiter
  - `IGitPlugin` → `ScmPluginSelectedCommand`
  - `IKiPlugin` → `KiPluginSelectedCommand`
  - `PluginActivationEntry` → `PluginSelectedCommand`
- Prüfung: `e.AddedItems.Count == 0` — verhindert Fehler bei leerer Auswahl

**`OnPluginActivationItemPreviewMouseLeftButtonDown` (Zeilen 62–66):**
- Event: `PreviewMouseLeftButtonDown` auf ListBoxItem-Containern
- Funktion: Selektiert den ListBoxItem **vor** CheckBox-Klick-Verarbeitung (Tunneling-Phase)
- Grund: Ohne dies wird ein Klick auf die CheckBox nicht als Listenauswahl registriert
- Setzt `ListBoxItem.IsSelected = true`, wenn noch nicht ausgewählt

**Weitere Handler:**
- `OnPasswordBoxLoaded` (Zeilen 68–69) — delegiert an `PluginSettingEntryEditHelper`
- `OnPasswordChanged` (Zeilen 71–72) — delegiert an `PluginSettingEntryEditHelper`
- `OnDateiAuswaehlenClick` (Zeilen 74–75) — delegiert an `PluginSettingEntryEditHelper`
- `OnHilfeButtonClick` (Zeilen 77–101) — zeigt CLI-Hilfetexte für KI-Plugin-CommandLineParameters

### Initialisierung
- `Loaded`-Event (Zeilen 18–22): Führt `LadenCommand` des SettingsViewModel aus
