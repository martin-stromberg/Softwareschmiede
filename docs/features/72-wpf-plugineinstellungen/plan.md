# Umsetzungsplan: WPF Plugin-Einstellungen und Styling

## Übersicht

Die Einstellungsansicht wird um zwei neue Register mit dynamisch geladenen Plugin-spezifischen Einstellungspanels erweitert. Nutzer können für SCM- und KI-Plugins Standard-Plugins auswählen und deren Einstellungen über generierte UI-Komponenten konfigurieren. Parallel werden einheitliche, Dark-Mode-kompatible Styles für alle Eingabekomponenten (TextBox, ComboBox, Label, CheckBox, Button) implementiert, um Konsistenz und Lesbarkeit über alle Ansichten zu gewährleisten.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Speichern von Standard-Plugins | String-Schlüssel in `AppEinstellung` (konsistent mit `DefaultKiPlugin`) | Konsistenz: `DefaultKiPlugin` wird bereits als String gespeichert. `DefaultScmPlugin` wird zur Laufzeit aus dem String-Schlüssel rekonstruiert, indem das Plugin mit matchemdem `PluginName` gesucht wird. |
| Plugin-Einstellungen Darstellung | `PluginSettingEntry` und `PluginSettingGroupEntry` Hilfsklassen aus `PluginSettingsViewModel` im `SettingsViewModel` verwenden | Diese Klassen bieten bereits bidirektionales Binding für String- und Boolean-Werte (`Value`, `BoolValue`) und strukturieren die Daten (Gruppen, Felder) für DataBinding. Wiederverwendung statt Neuimplementierung. |
| Data-Templates für Eingabefelder | Ein DataTemplate pro `PluginSettingFieldType` über `DataTemplateSelector` oder mehrere bedingte DataTemplates in XAML | Zentrale Verwaltung im Theme-Dictionary ermöglicht einfache Dark-Mode-Behandlung und Konsistenz. Bevorzugt: ContentControl mit DataTemplateSelector in ViewModel oder mehrere x:Key-basierte Templates mit ItemsControl-ElementTemplate. |
| Placeholder und Description Darstellung | Description unter dem Eingabefeld als `TextBlock` mit sekundärer Textfarbe; Placeholder im Eingabefeld selbst (TextBox.Placeholder, PasswordBox.Placeholder) | Bessere Lesbarkeit und UX als Tooltip; folgt Web-Konventionen. |
| Bedingte Einstellungen | Nicht unterstützt in dieser Phase (Feature-Scope bleibt fokussiert) | Kann in zukünftigen Versionen über erweiterte Metadaten in `PluginSettingField` hinzugefügt werden (z.B. `DependsOnFieldKey`, `VisibilityCondition`). Anforderung enthält keine solchen Fälle. |
| Standard-Werte | `PluginSettingField.Default` Property wird nicht implementiert; aktuell können nur leere oder bereits gespeicherte Werte angezeigt werden | Kein Default-Property in aktueller `PluginSettingField`-Definition vorhanden und nicht in Anforderung verlangt. Kann später hinzugefügt werden. |
| Performance: Lazy Loading von Plugin-Einstellungen | Settings werden beim Plugin-Wechsel geladen (nicht beim Öffnen aller Settings) | Reduktion der initialen Ladezeit; Wechsel zwischen Plugins erfolgt meist selten. Wenn Performance-Problem: Asynchrones Laden hinter Loading-Spinner. |
| Styling-Scope | Neue globale Styles werden in zentrale Theme-Dictionary platziert und können auf alle Ansichten angewendet werden; Fokus auf SettingsView | Globale Styles ermöglichen künftige Erweiterung auf andere Ansichten (z.B. Projektdetail) ohne Duplizierung. Aktuelle Tests/Anforderung konzentrieren sich auf SettingsView. |
| Dark-Mode Transition | Keine Reaktion auf Wechsel während offener Einstellungsansicht; Wechsel erfolgt beim nächsten Dialog-Öffnen | SettingsView ist Modal, daher Wechsel während Nutzung unwahrscheinlich. Implementierung würde zusätzliche Event-Listener erfordern. Kann als Enhancement später hinzugefügt werden. |

## Programmabläufe

### Laden von Plugin-Einstellungen beim Öffnen der Settings-Ansicht

1. `SettingsViewModel.LadenAsync()` wird aufgerufen
2. `IPluginManager.GetSourceCodeManagementPlugins()` und `.GetDevelopmentAutomationPlugins()` liefern verfügbare Plugins
3. `AppEinstellungService.GetSettingAsync(DefaultScmPluginKey)` ruft den String-Namen des Standard-SCM-Plugins ab (falls gespeichert)
4. `DefaultScmPlugin` wird auf das Plugin mit matchemdem `PluginName` aus der SCM-Plugins-Liste gesetzt (oder auf das erste Plugin, falls kein Standard gespeichert)
5. Analog für `DefaultKiPlugin` (der String wird direkt gebunden)
6. Die "Quellcodeverwaltung" und "KI" Register zeigen die ComboBox mit dem Default-Plugin an

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `IPluginManager`, `AppEinstellungService`

### Plugin-Wechsel und Laden der Einstellungsgruppen (SCM-Plugin)

1. Nutzer wählt ein anderes SCM-Plugin in der ComboBox (Quellcodeverwaltung Register)
2. `ScmPluginSelectedCommand` wird ausgelöst (Parameter: neues `IGitPlugin`)
3. Command ruft `selectedPlugin.GetSettingGroups()` auf
4. Für jede `PluginSettingGroup` wird eine `PluginSettingGroupEntry` erstellt
5. Für jedes `PluginSettingField` wird eine `PluginSettingEntry` erstellt: `Value` wird über `PluginSettingsService.GetValue(selectedPlugin, field)` geladen
6. `SelectedScmPluginSettings` Property wird mit der neuen Liste aktualisiert
7. XAML bindet `SelectedScmPluginSettings` und rendert die Einstellungspanels neu

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `ScmPluginSelectedCommand`, `PluginSettingsService`, `PluginSettingEntry`, `PluginSettingGroupEntry`

### Plugin-Wechsel und Laden der Einstellungsgruppen (KI-Plugin)

Identisch zu SCM-Plugin-Wechsel, aber mit:
- `KiPluginSelectedCommand` statt `ScmPluginSelectedCommand`
- `DefaultKiPlugin` statt `DefaultScmPlugin`
- `SelectedKiPluginSettings` statt `SelectedScmPluginSettings`
- `IKiPlugin` statt `IGitPlugin`

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `KiPluginSelectedCommand`, `PluginSettingsService`

### Speichern von Plugin-Einstellungen

1. Nutzer klickt "Speichern" Button in SettingsView
2. `SettingsViewModel.SpeichernAsync()` wird aufgerufen
3. Standard-Plugins werden gespeichert:
   - `AppEinstellungService.SetSettingAsync(DefaultScmPluginKey, DefaultScmPlugin.PluginName)` 
   - `AppEinstellungService.SetSettingAsync(DefaultKiPluginKey, DefaultKiPlugin)` (String)
4. Für jedes Feld in `SelectedScmPluginSettings` und `SelectedKiPluginSettings`:
   - `PluginSettingsService.SetValue(plugin, field, entry.Value)` speichert den String-Wert
   - Für Boolean-Felder: `entry.BoolValue` wird zu String "true"/"false" konvertiert
5. Erfolgsmeldung wird angezeigt

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `AppEinstellungService`, `PluginSettingsService`, `PluginSettingEntry`

### Rendering von Plugin-Einstellungen in der XAML-UI

1. `SelectedScmPluginSettings` (vom Typ `IReadOnlyList<PluginSettingGroupEntry>`) wird an `ItemsControl.ItemsSource` gebunden
2. Für jede `PluginSettingGroupEntry` wird:
   - Eine Kopfzeile (z.B. `<TextBlock Text="{Binding GroupName}" FontWeight="Bold" Margin="0,10,0,5"/>`) gerendert
   - Für jedes `PluginSettingEntry` in `Entries` wird ein Eingabefeld gerendert:
     - Feld-Label: `<TextBlock Text="{Binding Field.Label}"/>`
     - Eingabekomponente: abhängig vom `Field.FieldType` (Text → TextBox, Boolean → CheckBox, etc.)
     - Description (wenn vorhanden): `<TextBlock Text="{Binding Field.Description}" Foreground="{DynamicResource SecondaryTextBrush}" FontSize="11"/>`
3. DataTemplate-Selektion erfolgt über:
   - Mehrere bedingte DataTemplates mit `x:Key` und `DataType` kombiniert, oder
   - ContentControl mit benutzerdefiniertem `DataTemplateSelector` im Code-Behind

Beteiligte Klassen/Komponenten: `SettingsView`, `PluginSettingGroupEntry`, `PluginSettingEntry`, DataTemplates

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScmPluginSelectedCommand` | RelayCommand (in `SettingsViewModel` oder separate Klasse) | Wird ausgelöst, wenn der Nutzer ein SCM-Plugin wählt. Lädt die Einstellungsgruppen des Plugins. |
| `KiPluginSelectedCommand` | RelayCommand (in `SettingsViewModel` oder separate Klasse) | Wird ausgelöst, wenn der Nutzer ein KI-Plugin wählt. Lädt die Einstellungsgruppen des Plugins. |

Hinweis: `PluginSettingEntry` und `PluginSettingGroupEntry` existieren bereits in `PluginSettingsViewModel` und werden wiederverwendet.

## Änderungen an bestehenden Klassen

### `SettingsViewModel` (ViewModel)

- **Neue Eigenschaften:**
  - `ScmPluginSettings` (`IReadOnlyList<PluginSettingGroupEntry>`) — Einstellungsgruppen des aktuell gewählten SCM-Plugins; wird beim Plugin-Wechsel aktualisiert
  - `SelectedScmPluginSettings` (`IReadOnlyList<PluginSettingGroupEntry>`) — Bindet an die UI; identisch mit `ScmPluginSettings`, aber als separate Property für Konsistenz
  - `KiPluginSettings` (`IReadOnlyList<PluginSettingGroupEntry>`) — Einstellungsgruppen des aktuell gewählten KI-Plugins
  - `SelectedKiPluginSettings` (`IReadOnlyList<PluginSettingGroupEntry>`) — Bindet an die UI; identisch mit `KiPluginSettings`

- **Neue Methoden:**
  - `LoadScmPluginSettingsAsync(plugin)` — private async Methode zum Laden der Setting-Groups und deren Werte für ein SCM-Plugin
  - `LoadKiPluginSettingsAsync(plugin)` — private async Methode zum Laden der Setting-Groups und deren Werte für ein KI-Plugin

- **Neue Commands:**
  - `ScmPluginSelectedCommand` (`ICommand`) — wird ausgelöst mit Parameter `IGitPlugin`, ruft `LoadScmPluginSettingsAsync` auf
  - `KiPluginSelectedCommand` (`ICommand`) — wird ausgelöst mit Parameter `IKiPlugin`, ruft `LoadKiPluginSettingsAsync` auf

- **Geänderte Methoden:**
  - `LadenAsync()` — nach dem Laden von Plugins: die default-Plugins laden, dann initial die Setting-Groups für das Standard-Plugin abrufen (damit Settings beim Öffnen sichtbar sind)
  - `SpeichernAsync()` — zusätzlich: für jedes Feld in `SelectedScmPluginSettings` und `SelectedKiPluginSettings` `PluginSettingsService.SetValue()` aufrufen; Standard-Plugin-Namen speichern

- **Neue Event-Handler:**
  - Reaktion auf `DefaultScmPlugin` Property-Change: `LoadScmPluginSettingsAsync` aufrufen
  - Reaktion auf `DefaultKiPlugin` Property-Change: `LoadKiPluginSettingsAsync` aufrufen (optional, falls Commands nicht ausreichen)

### `AppEinstellungService` (Service)

- **Neue Konstanten:**
  - `DefaultScmPluginKey = "scm.plugin.default"` — Schlüssel für das Standard-SCM-Plugin im AppEinstellung-Storage

### `SettingsView.xaml` (XAML)

- **Neue XAML-Struktur im "Quellcodeverwaltung" Register:**
  - Existierende ComboBox für Plugin-Auswahl bleibt unverändert
  - Neue `ItemsControl` mit `ItemsSource="{Binding SelectedScmPluginSettings}"` unterhalb der ComboBox
  - ItemTemplate: `PluginSettingGroupEntry` mit verschachtelter Struktur (Gruppenname, dann Felder)
  - Command-Binding: `<ComboBox>` `.SelectedItemCommand="{Binding ScmPluginSelectedCommand}"` oder `SelectionChanged` Event mit Code-Behind-Handler

- **Neue XAML-Struktur im "KI" Register:**
  - Analog zu "Quellcodeverwaltung" Register

- **Data-Templates für Eingabefelder:**
  - Template für `PluginSettingFieldType.Text` → `TextBox` mit Placeholder, Description
  - Template für `PluginSettingFieldType.Secret` → `PasswordBox` (oder gemaskierte TextBox)
  - Template für `PluginSettingFieldType.Url` → `TextBox` mit URL-Validierung (optional)
  - Template für `PluginSettingFieldType.Integer` → `TextBox` mit Int-Validierung oder `IntegerUpDown`-Control
  - Template für `PluginSettingFieldType.Boolean` → `CheckBox` mit Label
  - Template für `PluginSettingFieldType.Enum` → `ComboBox` mit `EnumOptions` als ItemsSource
  - Template für `PluginSettingFieldType.FilePath` → `StackPanel` mit TextBox + Browse-Button

- **Verwendete DynamicResources:**
  - `BackgroundBrush`, `SurfaceBrush`, `BorderBrush`, `PrimaryTextBrush`, `SecondaryTextBrush` (bereits im Einsatz)
  - Neue Styles für Label und CheckBox (siehe unten)

### `DarkTheme.xaml` und `LightTheme.xaml` (Theme-Ressourcen)

- **Neue Styles für `Label` (beide Themes):**
  - `Background`: `DynamicResource BackgroundBrush`
  - `Foreground`: `DynamicResource PrimaryTextBrush`
  - `FontSize`: 12
  - `Padding`: 5,2

- **Neue Styles für `CheckBox` (beide Themes):**
  - `Foreground`: `DynamicResource PrimaryTextBrush`
  - `Background`: `DynamicResource SurfaceBrush`
  - `BorderBrush`: `DynamicResource BorderBrush`
  - `Padding`: 5,2
  - Triggers für Hover/Checked-State mit konsistenten Dark/Light-Farben

- **Styles für bestehende Komponenten (Überprüfung):**
  - `TextBox`: muss `Placeholder` Eigenschaft unterstützen (falls nicht bereits vorhanden)
  - `PasswordBox`: muss `Placeholder` Eigenschaft unterstützen (WPF-Standard nicht vorhanden, ggf. AttachedBehavior nötig)
  - `ComboBox`: `ItemContainerStyle` überprüfen, ob bereits Dark-Mode-kompatibel

## Datenbankmigrationen

Keine. Die `AppEinstellung` Tabelle existiert bereits und speichert Key-Value-Paare dynamisch. Der neue Schlüssel `"scm.plugin.default"` wird wie jeder andere Eintrag behandelt.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Textfeld (PluginSettingFieldType.Text) mit `IsRequired=true` | Darf nicht leer sein | Speichern schlägt fehl, Fehler wird angezeigt |
| Integer-Feld (PluginSettingFieldType.Integer) | Muss gültige Ganzzahl sein | Speichern schlägt fehl, Parse-Fehler wird angezeigt |
| Enum-Feld (PluginSettingFieldType.Enum) | Wert muss in `EnumOptions` enthalten sein | Speichern schlägt fehl, Validierungsfehler wird angezeigt |
| URL-Feld (PluginSettingFieldType.Url) — optional | Muss gültiges URL-Format sein (wenn `IsRequired=true`) | Speichern schlägt fehl, Format-Fehler wird angezeigt |

Validierung erfolgt im `SettingsViewModel.SpeichernAsync()` oder im Data-Validation-Layer (z.B. WPF ValidationRules) vor dem `PluginSettingsService.SetValue()` Aufruf.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `AppEinstellungService.DefaultScmPluginKey` | `string` (Konstante) | `"scm.plugin.default"` | Schlüssel für das in `AppEinstellung` gespeicherte Standard-SCM-Plugin-Name |

Keine weiteren Konfigurationsänderungen erforderlich. Plugins definieren ihre Einstellungen selbst via `GetSettingGroups()`.

## Seiteneffekte und Risiken

- **Styling-Konsistenz:** Neue globale Styles für `Label` und `CheckBox` können bestehende Ansichten beeinflussen. Sollte überprüft werden, ob andere Ansichten unerwünschte Styling-Änderungen erhalten (z.B. Projektdetail-Dialog).
- **PasswordBox und Placeholder:** WPF PasswordBox unterstützt nativ kein `Placeholder` Property. Lösung: Custom AttachedBehavior oder gemaskierte TextBox verwenden (mit Datenschutz-Überlegungen).
- **Performance bei vielen Plugins:** Wenn viele Plugins mit vielen Einstellungen existieren, könnte das Laden der Setting-Groups beim Plugin-Wechsel merklich verzögert werden. Wird aktuell mit synchronem `GetSettingGroups()` gelöst; kann später zu async erweitert werden.
- **Standard-Plugin Persistierung Inkonsistenz:** Nach dieser Implementierung wird `DefaultScmPlugin` als String gespeichert (wie `DefaultKiPlugin`), zur Laufzeit aber als `IGitPlugin` Objekt verwaltet. Rekonstruktion erfolgt über `PluginName` Abgleich — es muss sichergestellt werden, dass ein Plugin mit dem Namen existiert.
- **Plugin-Reihenfolge und Defaults:** Wenn der gespeicherte Plugin-Name nicht existiert, wird ein Fallback-Plugin (z.B. erstes Plugin) verwendet. Diese Logik muss explizit implementiert und getestet werden.
- **Betroffene bestehende Features:**
  - `PluginSettingsView.xaml` und `PluginSettingsViewModel` werden nicht direkt geändert, sollten aber auf neue Styles Rücksicht nehmen.
  - Bestehende TextBox/ComboBox/Button Styles werden nicht geändert, nur `Label` und `CheckBox` hinzugefügt.

## Umsetzungsreihenfolge

1. **Füge DefaultScmPluginKey-Konstante zu AppEinstellungService hinzu**
   - Voraussetzungen: Keine
   - Beschreibung: Konstante `DefaultScmPluginKey = "scm.plugin.default"` in `AppEinstellungService` Klasse definieren

2. **Erstelle oder erweitere Label und CheckBox Styles in Theme-Dictionaries**
   - Voraussetzungen: `DarkTheme.xaml` und `LightTheme.xaml` existieren
   - Beschreibung: 
     - Style für `Label` mit konsistenten Farben und Padding in `DarkTheme.xaml` und `LightTheme.xaml` hinzufügen
     - Style für `CheckBox` mit Foreground, Background, BorderBrush und Hover/Checked Triggers hinzufügen
     - Sicherstellen, dass alle neuen Styles `DynamicResource` für Farben verwenden

3. **Füge Styles für TextBox.Placeholder und PasswordBox zu Themes hinzu (falls noch nicht vorhanden)**
   - Voraussetzungen: `DarkTheme.xaml` und `LightTheme.xaml`
   - Beschreibung: Überprüfe, ob TextBox.Placeholder-Styling existiert; falls nicht, definiere Placeholder-Farbe in Themes. Implementiere WPF AttachedBehavior für PasswordBox.Placeholder oder nutze alternative Lösung.

4. **Füge PluginSettingEntry und PluginSettingGroupEntry in SettingsViewModel hinzu (oder importiere aus PluginSettingsViewModel)**
   - Voraussetzungen: Diese Klassen existieren bereits in `PluginSettingsViewModel`, müssen aber in `SettingsViewModel` verfügbar sein
   - Beschreibung: 
     - Entweder: `PluginSettingEntry` und `PluginSettingGroupEntry` aus `PluginSettingsViewModel` als öffentliche Klassen in eigene Datei auslagern und von beiden ViewModels importieren
     - Oder: In `SettingsViewModel` identische Hilfsklassen definieren (Duplikation)
     - Bevorzugte Lösung: Auslagern und gemeinsam nutzen (DRY)

5. **Erweitere SettingsViewModel um neue Properties und Laden-Logik**
   - Voraussetzungen: `PluginSettingEntry`, `PluginSettingGroupEntry`, `AppEinstellungService` mit `DefaultScmPluginKey`, `PluginSettingsService`, `IPluginManager`
   - Beschreibung:
     - Neue Properties hinzufügen: `ScmPluginSettings`, `SelectedScmPluginSettings`, `KiPluginSettings`, `SelectedKiPluginSettings`
     - Private Methode `LoadScmPluginSettingsAsync(plugin)` implementieren:
       - `plugin.GetSettingGroups()` aufrufen
       - Für jede Gruppe eine `PluginSettingGroupEntry` erstellen
       - Für jedes Feld eine `PluginSettingEntry` erstellen und Wert via `PluginSettingsService.GetValue(plugin, field)` laden
       - `SelectedScmPluginSettings` mit der neuen Liste aktualisieren
     - Analog `LoadKiPluginSettingsAsync(plugin)` implementieren
     - `LadenAsync()` erweitern:
       - Nach dem Laden der Plugins: `DefaultScmPlugin` via Lookup des String-Namens in der SCM-Plugins-Liste auflösen
       - `DefaultKiPlugin` wie bisher laden (String bleibt String)
       - Initial `LoadScmPluginSettingsAsync(DefaultScmPlugin)` und `LoadKiPluginSettingsAsync(DefaultKiPlugin)` aufrufen

6. **Implementiere ScmPluginSelectedCommand und KiPluginSelectedCommand in SettingsViewModel**
   - Voraussetzungen: `LoadScmPluginSettingsAsync`, `LoadKiPluginSettingsAsync` vorhanden
   - Beschreibung:
     - `ScmPluginSelectedCommand` als RelayCommand<IGitPlugin> oder mit Execute/CanExecute Methode definieren
     - Execute: `LoadScmPluginSettingsAsync` mit dem übergebenen Plugin aufrufen
     - Analog für `KiPluginSelectedCommand` mit RelayCommand<IKiPlugin>

7. **Erweitere SpeichernAsync() in SettingsViewModel um Plugin-Einstellungen und Standard-Plugin-Speicherung**
   - Voraussetzungen: `ScmPluginSettings`, `SelectedScmPluginSettings`, `KiPluginSettings`, `SelectedKiPluginSettings` vorhanden, `PluginSettingsService`, `AppEinstellungService` mit `DefaultScmPluginKey`
   - Beschreibung:
     - Standard-Plugins speichern:
       - `AppEinstellungService.SetSettingAsync(AppEinstellungService.DefaultScmPluginKey, DefaultScmPlugin?.PluginName)`
       - `AppEinstellungService.SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, DefaultKiPlugin)` (String)
     - Für jede `PluginSettingEntry` in `SelectedScmPluginSettings` und `SelectedKiPluginSettings`:
       - Validierung durchführen (Pflichtfelder, Typ-Validierung)
       - `PluginSettingsService.SetValue(plugin, entry.Field, entry.Value)` aufrufen
       - Boolean-Werte: `entry.BoolValue` zu "true"/"false" String konvertieren
     - Fehlerbehandlung: Falls Validierung fehlschlägt, Fehler anzeigen und nicht speichern

8. **Erweitere SettingsView.xaml — "Quellcodeverwaltung" Register**
   - Voraussetzungen: SettingsViewModel mit neuen Properties und Commands, Theme-Styles vorhanden
   - Beschreibung:
     - Lokalisiere das existierende "Quellcodeverwaltung" TabItem
     - Unterhalb der Plugin-Auswahl-ComboBox: neue `ItemsControl` mit `ItemsSource="{Binding SelectedScmPluginSettings}"` hinzufügen
     - ItemTemplate definieren: für jede `PluginSettingGroupEntry`:
       - `<TextBlock Text="{Binding GroupName}" FontWeight="Bold" Margin="0,10,0,5"/>`
       - Verschachtelte `ItemsControl` mit `ItemsSource="{Binding Entries}"` für die Einträge
       - Für jede `PluginSettingEntry`: Eingabefeld basierend auf `Field.FieldType` rendern (siehe nächster Schritt)
     - ComboBox SelectionChanged oder Command-Binding mit `ScmPluginSelectedCommand` verbinden

9. **Erweitere SettingsView.xaml — Data-Templates für verschiedene PluginSettingFieldType-Werte**
   - Voraussetzungen: Theme-Styles vorhanden, `PluginSettingEntry` Struktur bekannt
   - Beschreibung: Mehrere Data-Templates oder ein DataTemplateSelector implementieren:
     - **Text**: `<TextBox Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}" Placeholder="{Binding Field.Placeholder}"/>`
     - **Secret**: `<PasswordBox ... />` mit AttachedBehavior für Binding (oder gemaskierte TextBox)
     - **Url**: `<TextBox ... />` mit URL-Validierungs-Regel
     - **Integer**: `<TextBox ... />` mit Integer-Validierungs-Regel
     - **Boolean**: `<CheckBox IsChecked="{Binding BoolValue, UpdateSourceTrigger=PropertyChanged}" Content="{Binding Field.Label}"/>`
     - **Enum**: `<ComboBox ItemsSource="{Binding Field.EnumOptions}" SelectedItem="{Binding Value, UpdateSourceTrigger=PropertyChanged}"/>`
     - **FilePath**: `<Grid><TextBox .../><Button Click="Browse..."/></Grid>`
     - Optional: Description unterhalb des Feldes mit sekundärer Textfarbe

10. **Erweitere SettingsView.xaml — "KI" Register**
    - Voraussetzungen: "Quellcodeverwaltung" Register fertig, Data-Templates definiert
    - Beschreibung: Analog zu "Quellcodeverwaltung" aber mit `SelectedKiPluginSettings` und `KiPluginSelectedCommand`

11. **Schreibe Unit-Tests für SettingsViewModel**
    - Voraussetzungen: `SettingsViewModel` vollständig implementiert
    - Beschreibung:
        - Test: `ScmPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin`
        - Test: `KiPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin`
        - Test: `LadenAsync_LaedtDefaultPlugine_UndInitialeSettings`
        - Test: `SpeichernAsync_SpeichertDefaultPlugine_UndEinstellungswerte`
        - Test: `SpeichernAsync_ValidierungFehlgeschlagen_ZeigtFehlerMeldung`
        - Verwendung von Mocks für `IPluginManager`, `PluginSettingsService`, `AppEinstellungService`

12. **Schreibe E2E-Tests für SettingsView**
    - Voraussetzungen: UI vollständig implementiert, Tests-Infrastruktur vorhanden
    - Beschreibung:
        - Test: Öffne SettingsView, wähle SCM-Plugin, überprüfe ob Einstellungspanels erscheinen
        - Test: Ändere Einstellungswert (z.B. Text), klicke Speichern, überprüfe ob in Datenbank gespeichert
        - Test: Wechsle zwischen SCM-Plugins, überprüfe ob Einstellungspanels sich aktualisieren
        - Test: Boolean-Feld aktivieren/deaktivieren und speichern
        - Test: Pflichtfeld-Validierung: Versuche zu speichern ohne Pflichtfelder auszufüllen, überprüfe Fehler
        - Analog für KI-Plugin-Register

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ScmPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` | `SettingsViewModelTests` | ScmPluginSelectedCommand lädt Setting-Groups des ausgewählten SCM-Plugins und füllt SelectedScmPluginSettings |
| `ScmPluginSelectedCommand_WithMultipleFields_LoadsAllValues` | `SettingsViewModelTests` | Mehrere Felder verschiedener Typen werden korrekt geladen und gebunden |
| `KiPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` | `SettingsViewModelTests` | KiPluginSelectedCommand lädt Setting-Groups des ausgewählten KI-Plugins und füllt SelectedKiPluginSettings |
| `LadenAsync_LaedtDefaultPlugine_UndInitialeSettings` | `SettingsViewModelTests` | LadenAsync() ruft Defaults von AppEinstellungService ab, rekonstruiert DefaultScmPlugin aus String, laden initial Settings |
| `SpeichernAsync_SpeichertDefaultPlugine_Und_EinstellungswerteFuerScm` | `SettingsViewModelTests` | SpeichernAsync() speichert DefaultScmPlugin Name und alle SCM-Einstellungswerte via PluginSettingsService |
| `SpeichernAsync_SpeichertDefaultPlugine_Und_EinstellungswerteFuerKi` | `SettingsViewModelTests` | SpeichernAsync() speichert DefaultKiPlugin Name und alle KI-Einstellungswerte via PluginSettingsService |
| `SpeichernAsync_ValidierungFehlgeschlagen_ZeigtFehlerMeldung` | `SettingsViewModelTests` | Wenn Pflichtfeld leer ist oder Typ-Validierung fehlschlägt, zeigt SpeichernAsync() Fehlermeldung an und speichert nicht |
| `SpeichernAsync_BooleanFelder_KonvertiertCorrect` | `SettingsViewModelTests` | Boolean-Werte werden als "true"/"false" String gespeichert |
| `SettingsView_ScmPluginWechsel_AktualisiertEinstellungspanels` | E2E-Tests (WPF/Automation) | Öffne SettingsView, wähle anderes SCM-Plugin, überprüfe ob Einstellungspanels neu rendern |
| `SettingsView_AendereTextFeldUndSpeichere_WertPersistiert` | E2E-Tests | Ändere Textfeld, klicke Speichern, öffne SettingsView erneut, überprüfe ob Wert noch da ist |
| `SettingsView_BooleanFeld_Toggle_UndSpeichere` | E2E-Tests | Aktiviere CheckBox, klicke Speichern, überprüfe ob in Datenbank gespeichert |
| `SettingsView_PflichtfeldValidierung_ZeigtFehler` | E2E-Tests | Lasse Pflichtfeld leer, klicke Speichern, überprüfe Fehler |
| `SettingsView_EnumFeld_WaehleWert_UndSpeichere` | E2E-Tests | Wähle Wert aus ComboBox für Enum-Feld, speichere, überprüfe Persistierung |
| `SettingsView_KiPluginWechsel_AktualisiertEinstellungspanels` | E2E-Tests | Analog SCM-Plugin für KI-Register |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PluginSettingsServiceIntegrationTests.*` | Keine Anpassung erforderlich — Service-Schnittstelle ändert sich nicht |
| Bestehende `SettingsViewModel` Tests (falls vorhanden) | Falls `LadenAsync()` oder `SpeichernAsync()` bereits getestet werden, müssen Tests um neue Properties (`DefaultScmPlugin`, `SelectedScmPluginSettings`, etc.) erweitert werden |

Falls keine bestehenden SettingsViewModel-Tests existieren, werden in Schritt 11 neu erstellt.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Öffne SettingsView, "Quellcodeverwaltung" Register zeigt verfügbare SCM-Plugins | `SettingsViewE2ETests.cs` | Alle SCM-Plugins sind in ComboBox sichtbar |
| Wähle SCM-Plugin, Einstellungspanels erscheinen mit Feldern aus GetSettingGroups() | `SettingsViewE2ETests.cs` | Plugin-Einstellungen werden dynamisch geladen und angezeigt |
| Ändere Text-Einstellungsfeld, klicke Speichern, überprüfe ob in Datenbank persistiert | `SettingsViewE2ETests.cs` | Speichern funktioniert für Text-Felder |
| Aktiviere Boolean-Feld (CheckBox), speichere, überprüfe Persistierung | `SettingsViewE2ETests.cs` | Speichern funktioniert für Boolean-Felder |
| Wähle Enum-Option aus ComboBox, speichere, überprüfe Persistierung | `SettingsViewE2ETests.cs` | Speichern funktioniert für Enum-Felder |
| Lasse Pflichtfeld leer, klicke Speichern, überprüfe Validierungsfehler | `SettingsViewE2ETests.cs` | Validierung funktioniert, Speichern wird verhindert |
| Öffne SettingsView erneut, überprüfe ob Standard-Plugin und Einstellungswerte geladen werden | `SettingsViewE2ETests.cs` | Laden funktioniert, persistierte Werte sind sichtbar |
| Wechsle zwischen SCM-Plugins, überprüfe ob Einstellungspanels aktualisiert werden | `SettingsViewE2ETests.cs` | Plugin-Wechsel funktioniert, UI aktualisiert sich |
| "KI" Register: Wähle KI-Plugin, Einstellungspanels erscheinen (analog SCM) | `SettingsViewE2ETests.cs` | KI-Plugin-Einstellungen werden geladen und angezeigt |
| "KI" Register: Ändere Einstellung, speichere, überprüfe Persistierung | `SettingsViewE2ETests.cs` | Speichern funktioniert für KI-Einstellungen |
| Dark-Mode: Öffne SettingsView im Dark-Mode, überprüfe ob alle Komponenten lesbar sind | `SettingsViewE2ETests.cs` (oder Style-Tests) | Styles für Label, CheckBox, TextBox sind Dark-Mode-kompatibel |
| Light-Mode: Öffne SettingsView im Light-Mode, überprüfe Lesbarkeit | `SettingsViewE2ETests.cs` (oder Style-Tests) | Styles funktionieren im Light-Mode |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine bekannten E2E-Tests für SettingsView vorhanden | Falls E2E-Tests für SettingsView existieren (z.B. "Öffne Settings, ändere Arbeitsverzeichnis"), müssen sie um die neuen Register erweitert oder separiert werden (z.B. als `SettingsViewScmPluginE2ETest`, `SettingsViewKiPluginE2ETest`) |

## Offene Punkte

Keine. Alle in der Anforderung genannten Fragen wurden durch die Bestandsaufnahme und die Designentscheidungen geklärt:

| # | Offener Punkt | Antwort / Lösung |
|---|---|---|
| 1 | Speicherung von Standard-Plugins als String oder IPlugin-Objekt? | **Geklärt:** String (wie DefaultKiPlugin). DefaultScmPlugin wird zur Laufzeit aus String via PluginName-Abgleich rekonstruiert. |
| 2 | Validierung von Plugin-Einstellungen im ViewModel oder Service? | **Geklärt:** Im SettingsViewModel.SpeichernAsync() vor dem SetValue-Aufruf. |
| 3 | Placeholder und Description Darstellung? | **Geklärt:** Placeholder im Eingabefeld, Description als TextBlock unter dem Feld mit sekundärer Farbe. |
| 4 | Bedingte Einstellungen? | **Geklärt:** Nicht in dieser Phase. Kann in zukünftigen Versionen über erweiterte PluginSettingField-Metadaten hinzugefügt werden. |
| 5 | Default-Werte in PluginSettingField? | **Geklärt:** Nicht in dieser Phase. Kein Default-Property in aktueller Definition. |
| 6 | Performance bei vielen Plugins? | **Geklärt:** Settings werden lazy beim Plugin-Wechsel geladen. Wenn nötig, kann später zu async Streaming erweitert werden. |
| 7 | Styling-Scope: nur SettingsView oder global? | **Geklärt:** Styles werden global in Theme-Dictionaries platziert. Fokus auf SettingsView, aber andere Ansichten können später profitieren. |
| 8 | Dark-Mode Transition während offener SettingsView? | **Geklärt:** Nicht unterstützt. SettingsView ist Modal; Wechsel beim nächsten Öffnen möglich. |
