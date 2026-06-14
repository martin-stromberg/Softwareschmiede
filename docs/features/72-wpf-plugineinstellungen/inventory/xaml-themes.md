# XAML und Themes

## `SettingsView.xaml`
Datei: `src/Softwareschmiede.App/Views/SettingsView.xaml`

Einstellungsansicht mit Ribbon-ähnlichem Menü und Registerkarten.

### Registerkarten (TabItems)
1. **Allgemein** — Design-Modus (ComboBox) und Arbeitsverzeichnis (TextBox)
2. **Quellcodeverwaltung** — Standard SCM-Plugin Auswahl via ComboBox
3. **KI** — Standard KI-Plugin Auswahl via ComboBox

### Vorhandene Bindungen
- `DesignMode`: 2-Way Binding zur ComboBox
- `Arbeitsverzeichnis`: 2-Way Binding zur TextBox
- `DefaultScmPlugin`: 2-Way Binding zu SCM-Plugin ComboBox
- `DefaultKiPlugin`: 2-Way Binding zu KI-Plugin ComboBox

### Nicht vorhanden (erforderlich für Feature 72)
- Dynamische Panels/ItemsControl zur Anzeige von Plugin-Einstellungsgruppen in den "Quellcodeverwaltung" und "KI" Registern
- Data-Templates für verschiedene `PluginSettingFieldType`-Werte

### Verwendete DynamicResources
- `BackgroundBrush` (für UserControl Background)
- `SurfaceBrush` (für Ribbon Border und TabControl)
- `BorderBrush` (für Borders)
- `PrimaryTextBrush` (für primäre Textfarben)
- `SecondaryTextBrush` (für sekundäre Textfarben)
- `SuccessBrush` (für Erfolgsmeldung)
- `ErrorBrush` (für Fehlermeldung)
- `SelectedTabBackgroundBrush` und `UnselectedTabBackgroundBrush` (für TabItem Styling)

---

## `App.xaml`
Datei: `src/Softwareschmiede.App/App.xaml`

Haupt-Application-ResourceDictionary. Merged das Light-Theme und definiert globale Converter.

### Merged Dictionaries
- `Themes/LightTheme.xaml` (Light-Mode als Default)

### Definierte Converter
- `BoolToVisibilityConverter`
- `BoolToWidthConverter`
- `InverseBoolToVisibilityConverter`
- `NullOrEmptyToVisibilityConverter`
- `EnumToBoolConverter`

Dark-Mode wird zur Laufzeit dynamisch wechselnd geladen (über `DarkModeService`).

---

## `DarkTheme.xaml`
Datei: `src/Softwareschmiede.App/Themes/DarkTheme.xaml`

Dark-Mode Theme mit dunkelgrauen Farben und hellen Texten.

### Definierte Farben (Samples)
| Farb-Schlüssel | Wert |
|---|---|
| `BackgroundColor` | #1C1C1C (dunkelgrau) |
| `SurfaceColor` | #2D2D2D |
| `SidebarBackgroundColor` | #252525 |
| `BorderColor` | #3D3D3D |
| `PrimaryTextColor` | #F0F0F0 (helles Grau) |
| `SecondaryTextColor` | #A0A0A0 |
| `AccentColor` | #4CC2FF (Hellblau) |
| `SuccessColor` | #6CCB5F (Grün) |
| `ErrorColor` | #FF6B6B (Rot) |

### Definierte Styles
- `Button` — mit Transparent-Overlay auf Hover/Press
- `TextBox` — mit Dark-Mode Farben
- `PasswordBox` — mit Dark-Mode Farben
- `ComboBox` — mit Custom ControlTemplate (Dropdown mit Dark-Mode Farben)
- `ComboBoxItem` — mit Hover/Selected Triggers
- `DatePicker` — mit Dark-Mode Farben
- `DatePickerTextBox` — mit Dark-Mode Farben

Nicht vorhanden:
- Style für `Label`
- Style für `CheckBox`

---

## `LightTheme.xaml`
Datei: `src/Softwareschmiede.App/Themes/LightTheme.xaml`

Light-Mode Theme mit weißen/hellen Farben und dunkelgrauen Texten. Parallel zu DarkTheme aufgebaut.

### Definierte Farben (Samples)
| Farb-Schlüssel | Wert |
|---|---|
| `BackgroundColor` | #FAFAFA (helles Grau) |
| `SurfaceColor` | #FFFFFF (weiß) |
| `SidebarBackgroundColor` | #F0F0F0 |
| `BorderColor` | #E0E0E0 |
| `PrimaryTextColor` | #1A1A1A (dunkelgrau) |
| `SecondaryTextColor` | #666666 |
| `AccentColor` | #0078D4 (Blau) |
| `SuccessColor` | #107C10 (Grün) |
| `ErrorColor` | #C42B1C (Rot) |

### Definierte Styles
- `Button`, `TextBox`, `PasswordBox`, `ComboBox`, `DatePicker` — analog DarkTheme aber mit Light-Mode Farben

Nicht vorhanden:
- Style für `Label`
- Style für `CheckBox`

---

## `PluginSettingsView.xaml`
Datei: `src/Softwareschmiede.App/Views/PluginSettingsView.xaml`

Separate Ansicht zur Anzeige aller Plugin-Einstellungen. Wird von `PluginSettingsViewModel` verwendet.

Struktur:
- Ribbon-Menü mit Laden/Speichern-Buttons
- Meldungen (Erfolg/Fehler)
- `ItemsControl` oder ähnliche Struktur zur Anzeige von `PluginWithSettingsEntry` Objekten

Diese Ansicht kann als Referenz für die Erweiterung von `SettingsView` dienen.

---

## Global verfügbare Brushes (aus Dark-/LightTheme)
- `BackgroundBrush` — Haupthintergrund
- `SurfaceBrush` — Kontroller/Dialog-Hintergrund
- `SidebarBackgroundBrush` — Sidebar-Hintergrund
- `BorderBrush` — Border/Separator
- `SeparatorBrush` — Separatorlinien
- `PrimaryTextBrush` — Primäre Textfarbe
- `SecondaryTextBrush` — Sekundäre Textfarbe
- `DisabledTextBrush` — Deaktivierte Textfarbe
- `AccentBrush` — Akzentfarbe
- `AccentHoverBrush` — Akzent-Hover-Farbe
- `SuccessBrush` — Erfolgsmeldungs-Farbe
- `WarningBrush` — Warnmeldungs-Farbe
- `ErrorBrush` — Fehlermeldungs-Farbe
- `SelectedTabBackgroundBrush` — Ausgewählter TabItem-Hintergrund
- `UnselectedTabBackgroundBrush` — Nicht ausgewählter TabItem-Hintergrund
