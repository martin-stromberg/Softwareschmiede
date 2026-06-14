# Anforderungsanalyse: WPF Plugin-Einstellungen und Styling

## Fachliche Zusammenfassung

Die Einstellungsansicht `SettingsView` muss um zwei neue Register ("Quellcodeverwaltung" und "KI") erweitert werden, die es Nutzern ermöglichen, Plugin-spezifische Einstellungen pro Plugin zu konfigurieren. Beide Register folgen dem gleichen Muster: Auswahl eines Standard-Plugins per `ComboBox`, gefolgt von dynamisch geladenen Plugin-spezifischen Einstellungspanels, die jeweils die Felder definieren, die das Plugin via `GetSettingGroups()` bereitstellt. Zusätzlich müssen sämtliche Eingabekomponenten (TextBox, ComboBox, Label, etc.) über einheitliche, designgültige Styles in den Dark-Mode integriert werden, damit sie in allen Ansichten konsistent und lesbar erscheinen.

## Betroffene Klassen und Komponenten

### ViewModel-Erweiterungen
- `SettingsViewModel`
  - Neue Properties: `ScmPluginSettings`, `SelectedScmPluginSettings`, `KiPluginSettings`, `SelectedKiPluginSettings` (jeweils `IReadOnlyList<PluginSettingGroup>`)
  - Neue Commands: `ScmPluginSelectedCommand`, `KiPluginSelectedCommand` (um bei Plugin-Wechsel die Einstellungen neu zu laden)
  - Bestehende Properties `DefaultScmPlugin` und `DefaultKiPlugin` nutzbar (bereits vorhanden)

### UI-Komponenten (XAML)
- `SettingsView.xaml`
  - Register "Quellcodeverwaltung": Erweiterung der bestehenden `TabItem` (bereits vorhanden mit ComboBox für Plugin-Auswahl) um ein dynamisches Panel für Plugin-Einstellungen
  - Register "KI": Analog wie "Quellcodeverwaltung"
  - Neue `ItemsControl` oder ähnliche Struktur zur Darstellung der von `PluginSettingGroup` stammenden Felder

### Neue/geänderte Services
- `PluginSettingsService` (besteht bereits, ggf. Erweiterung um Bulk-Get-Methoden)
- `AppEinstellungService` (besteht bereits, ggf. Erweiterung um DefaultScmPluginKey)

### UI-Hilfskomponenten
- Neue generische Eingabekomponenten oder Data-Templates für verschiedene `PluginSettingFieldType`-Werte:
  - `Text`, `Secret`, `Url`, `Integer`, `Boolean`, `Enum`, `FilePath`

### Styling/Ressourcen
- `App.xaml` oder separate ResourceDictionary-Datei(en)
  - Einheitliche Styles für `TextBox`, `ComboBox`, `Label`, `Button`, `CheckBox`, etc.
  - Dark-Mode-kompatible Farben: `BackgroundBrush`, `SurfaceBrush`, `BorderBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, usw.
  - Die Brushes werden bereits teilweise verwendet (z.B. in SettingsView); Lücken müssen geschlossen werden

### Tests
- `SettingsViewModelTests` (neu oder erweitert)
  - Test für `ScmPluginSelectedCommand`: Plugin-Wechsel lädt korrekten Setting-Groups
  - Test für `KiPluginSelectedCommand`: Analog
  - Test für `SpeichernAsync`: Speichert Standard-Plugins und alle Einstellungswerte korrekt

## Implementierungsansatz

### Phase 1: Styling-Infrastruktur
1. Zentrale Resource-Dictionary-Datei (`Styles.xaml` oder ähnlich) erstellen
2. Globale Styles für Eingabekomponenten definieren (TextBox, ComboBox, Label, etc.)
3. Dark-Mode-Farben sicherstellen (nutze bestehende DynamicResource-Brushes)
4. Alle bestehenden Ansichten reviewen und hardcoded Farben durch DynamicResources ersetzen

### Phase 2: Dynamische Plugin-Einstellungen laden
1. `SettingsViewModel.LadenAsync()` erweitern:
   - Nach dem Laden von `ScmPlugins` und `KiPlugins`: je eines Standard-Plugin auswählen
   - `ScmPluginSelectedCommand` implementieren: wenn Nutzer ein SCM-Plugin wählt, dessen `GetSettingGroups()` laden und in `SelectedScmPluginSettings` speichern
   - Analog für KI-Plugin

2. Plugin-Setting-Values laden:
   - `PluginSettingsService.GetValue()` für jedes Feld nutzen
   - Werte in ViewModel-Eigenschaften oder temporärer Struktur speichern

### Phase 3: XAML-UI für Plugin-Settings
1. "Quellcodeverwaltung"-Register erweitern:
   - ComboBox für Plugin-Auswahl (bereits vorhanden)
   - Darunter: `ItemsControl` oder `StackPanel`, das `SelectedScmPluginSettings` (IReadOnlyList<PluginSettingGroup>) anzeigt
   - Pro `PluginSettingGroup`: Kopfzeile (GroupName), dann Felder via Data-Template pro `PluginSettingFieldType`
   - Jedes Eingabefeld mit `Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}"` gebunden

2. "KI"-Register analog

### Phase 4: Data-Template-Factory für Eingabefelder
1. Value-Converter oder Trigger definieren, um je nach `PluginSettingFieldType` das richtige Template zu zeigen:
   - `Text` → `TextBox`
   - `Secret` → `PasswordBox` (oder gemaskerte TextBox)
   - `Url` → `TextBox` mit URL-Validierung (optional)
   - `Integer` → `TextBox` mit Int-Validierung oder `IntegerUpDown`
   - `Boolean` → `CheckBox`
   - `Enum` → `ComboBox` mit `EnumOptions` als ItemsSource
   - `FilePath` → `TextBox` + Browse-Button

### Phase 5: Speichern und Laden
1. `SettingsViewModel.SpeichernAsync()` erweitern:
   - Pro Feld in `SelectedScmPluginSettings` und `SelectedKiPluginSettings`: `PluginSettingsService.SetValue()` aufrufen
   - Auch die Standard-Plugins speichern (DefaultScmPlugin, DefaultKiPlugin)

2. `SettingsViewModel.LadenAsync()`:
   - Nach Plugin-Auswahl die Setting-Groups abrufen
   - Alle Werte via `PluginSettingsService.GetValue()` laden und in Anzeigemodellen speichern

## Konfiguration

### Anwendungsebene
- `AppEinstellungService.DefaultScmPluginKey`: bereits teilweise implementiert (existiert evtl. noch nicht für SCM)
- `AppEinstellungService.DefaultKiPluginKey`: bereits vorhanden

### Plugin-Ebene
- Jedes Plugin definiert via `GetSettingGroups()`: Gruppen mit Feldmetadaten
- Werte werden in `ICredentialStore` unter `<PluginPrefix>.<FieldKey>` gespeichert
- `PluginSettingsService` ist der zentrale Accessor

### UI-Styling
- Zentrale Theme-Resource-Dictionary mit konsistenten Dark-Mode-Farben
- Templates für alle Eingabekomponenten

## Offene Fragen

1. **Speicherung von Standard-Plugins:** Werden `DefaultScmPlugin` und `DefaultKiPlugin` als Plugin-Name (String) oder als `IPlugin`-Objekt in `AppEinstellung` gespeichert? Momentan verwendet `SettingsViewModel` den String `DefaultKiPlugin`, aber `DefaultScmPlugin` ist ein `IGitPlugin`-Objekt — Konsistenz nötig.

2. **Validierung von Plugin-Einstellungen:** Müssen Plugin-Einstellungen validiert werden (z.B. URL-Format, Pflichtfelder)? Falls ja, soll die Validierung im ViewModel oder im Service erfolgen?

3. **Placeholder und Description in der UI:** Wie sollen `PluginSettingField.Placeholder` und `Description` dargestellt werden? Unter dem Feld, als Tooltip, oder in einem Hilfe-Panel?

4. **Bedingte Einstellungen:** Können Plugin-Einstellungen voneinander abhängen (z.B. "Feld B nur wenn Feld A = true")? Falls ja, wie wird das definiert?

5. **Default-Werte:** Soll `PluginSettingField.Default` unterstützt werden? Derzeit ist kein Default-Property in der Definition vorhanden.

6. **Performance bei vielen Plugins:** Falls viele Plugins mit vielen Einstellungen existieren, könnte das Laden aller Settings beim Öffnen der Einstellungsansicht verlangsamt werden. Sollen Settings lazy geladen werden?

7. **Styling-Scope:** Sollen die neuen globalen Component-Styles auch auf bestehende Ansichten (z.B. Projektdetail) angewendet werden, oder nur auf SettingsView?

8. **Dark-Mode Transition:** Falls der Nutzer während einer offenen Einstellungsansicht den Dark-Mode umschaltet, sollen Plugin-Settings neu geladen werden?

