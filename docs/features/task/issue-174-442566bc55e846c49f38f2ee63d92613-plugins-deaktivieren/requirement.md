# Issue 174: Plugins deaktivieren — Anforderungsanalyse

## Fachliche Zusammenfassung

Das Plugin-System wird um ein Aktivierungsmerkmal erweitert. Bisher sind alle entdeckten Plugins automatisch aktiv; die Anforderung führt ein explizites Aktivierungs-/Deaktivierungsfeature in den Einstellungen ein. Ein neues Register „Plugins" in der `SettingsView` bietet zwei Listen (SCM-Plugins und KI-Plugins), über die der Benutzer den Aktivierungsstatus einzeln steuern kann. Außerdem werden deaktivierte Plugins in allen Auswahlfeldern (Projektbearbeitung, Aufgabenbearbeitung) gefiltert, und bei Vorhandensein eines einzigen Plugins wird die Auswahl gänzlich ausgeblendet.

## Betroffene Klassen und Komponenten

### Datenmodellklassen

- **`AppEinstellung`**: Neues Feld `IKiPluginEnabledStatuses` (JSON-Serialisierung von Plugin-Namen zu Boolean) oder alternative Struktur `PluginEnabledStatus` zur Persistierung der Aktivierungsstatus
- Alternativ: Neue Tabelle `PluginEnabledStatus` mit Spalten `PluginName`, `PluginType` (enum: SCM oder KI), `Enabled` (Boolean), `CreatedAt`, `UpdatedAt`

### Logikklassen / Services

- **`IPluginManager`**: Neue Methoden hinzufügen:
  - `GetEnabledSourceCodeManagementPlugins()` — gibt nur aktive SCM-Plugins zurück
  - `GetEnabledDevelopmentAutomationPlugins()` — gibt nur aktive KI-Plugins zurück
  - `SetPluginEnabled(string pluginName, bool enabled)` — speichert den Aktivierungsstatus
  - `IsPluginEnabled(string pluginName)` — prüft, ob ein Plugin aktiv ist

- **`PluginManager`**: Implementierung der obigen Methoden; Zugriff auf den Persistierungsmechanismus für Aktivierungsstatus

- **Neue Service-Klasse `PluginActivationService`** (optional, aber empfohlen):
  - Lädt beim Appstart die Aktivierungsstatus aus der Datenbank
  - Managed den In-Memory-Cache der Aktivierungsstatus
  - Persistiert Änderungen

- **`PluginSelectionService`**: Anpassung bestehender Filterlogik, um nur aktive Plugins zurückzugeben (sofern noch nicht automatisch durch `GetEnabledSourceCodeManagementPlugins()` abgedeckt)

### UI-Komponenten / ViewModels

- **`SettingsViewModel`**: Neue Property und Bindung für das neue „Plugins"-Register:
  - `SourceCodeManagementPlugins` — ObservableCollection<`IPluginActivationViewModel`>
  - `DevelopmentAutomationPlugins` — ObservableCollection<`IPluginActivationViewModel`>
  - `SelectedPlugin` — aktuell gewähltes Plugin
  - `PluginSettings` — UI-Felder des gewählten Plugins (wiederverwendet aus bestehender Logik)
  - `TogglePluginEnabled(IPluginActivationViewModel)` — Command zum Umschalten

- **Neue ViewModel-Klasse `IPluginActivationViewModel`** (oder ähnlich):
  - `PluginName` — Name des Plugins (z. B. „GitHub", „Claude CLI")
  - `PluginDescription` — Kurzbeschreibung (optional)
  - `IsEnabled` — Boolean zum Aktivierungs-/Deaktivierungsstatus
  - `PluginSettings` — Untermodell für Plugin-spezifische Einstellungen (bindet sich an `IPlugin.GetPluginSettings()`)

- **`SettingsView.xaml`**: Neuer Tab „Plugins" mit:
  - Zwei-spaltig aufgebauter Oberfläche (links: Plugin-Listen, rechts: Plugin-Detailansicht)
  - Für jede Plugin-Liste ein `ListBox` oder ähnliches mit ItemsSource zur ObservableCollection
  - Toggle/CheckBox je Listenelement für den Aktivierungsstatus
  - Auf der rechten Seite die bereits bestehende Plugin-Settings-Render-Logik

### Angepasste Komponenten (bestehend)

- **`TaskDetailViewModel`**: 
  - Selektion von SCM- und KI-Plugins erfolgt jetzt nur über `GetEnabledSourceCodeManagementPlugins()` / `GetEnabledDevelopmentAutomationPlugins()`
  - **Single-Plugin-Verhalten**: Wenn nur ein Plugin eines Typs aktiv ist, wird kein Dropdown/Picker angezeigt; das Plugin wird automatisch verwendet

- **`ProjektViewModel`** oder Projektbearbeitungs-View:
  - Analog Anpassung: Nur aktive Plugins in der Auswahl

- **Entfernung bestehender Plugin-Tabs**:
  - Die bisherigen Register für einzelne Plugins (z. B. ein separates „GitHub"-Tab, „Claude"-Tab) in der `SettingsView` müssen entfernt werden
  - Alle Plugin-Einstellungen werden zentralisiert ins neue „Plugins"-Register verlagert

### Datenbank / Persistierung

- **Migration (Entity Framework)**: 
  - Entweder Hinzufügen einer Spalte `PluginEnabledStatuses` (JSON) zur `AppEinstellung`-Tabelle, oder
  - Neue Tabelle `PluginEnabledStatus` mit PK `(PluginName, PluginType)` und Spalten `Enabled`, `CreatedAt`, `UpdatedAt`

- **Sensible Default-Initialisierung**: Beim ersten Laden einer neuen Datenbank oder beim Hinzufügen eines neuen Plugins wird der Status standardmäßig auf `Enabled = true` gesetzt

### Tests

- **Unit-Tests für `PluginManager`**:
  - `IsPluginEnabled()` mit bekannten Plugin-Namen
  - `GetEnabledSourceCodeManagementPlugins()` filtert deaktivierte SCM-Plugins
  - `GetEnabledDevelopmentAutomationPlugins()` filtert deaktivierte KI-Plugins
  - `SetPluginEnabled()` persistiert und lädt korrekt

- **Unit-Tests für `SettingsViewModel`**:
  - Toggle-Command ändert `IsEnabled`-Property
  - Neue Plugins werden standardmäßig mit `Enabled = true` initialisiert

- **E2E-Tests**:
  - Benutzer deaktiviert ein SCM-Plugin in den Einstellungen
  - In der Projektbearbeitung erscheint das deaktivierte Plugin nicht in der Auswahl
  - Wenn nur noch ein KI-Plugin aktiv ist, verschwindet die KI-Plugin-Auswahl in `TaskDetailView`

## Implementierungsansatz

### Persistierung des Aktivierungsstatus

**Empfohlener Pfad A (einfach):** Neue Spalte `PluginEnabledStatuses` in `AppEinstellung` als JSON-String:
```json
{
  "GitHub": true,
  "BitBucket": false,
  "ClaudeCli": true,
  "GitHubCopilot": false
}
```

Beim Startup liest `PluginActivationService` diese JSON, parst sie und cacht die Werte im Speicher. Bei Änderung schreibt der Service die JSON zurück.

**Empfohlener Pfad B (strukturiert):** Neue Tabelle `PluginEnabledStatus(PluginName, PluginType, Enabled, CreatedAt, UpdatedAt)`. Migration erstellt diese Tabelle und kopiert existierende Plugins als `Enabled = true`.

**Empfehlung:** Pfad A für Einfachheit; Pfad B, wenn zukünftig Audit-Log oder Zeitstempel relevant wird.

### Filterung in `PluginManager`

Der `PluginManager` erhält einen Verweis auf den `PluginActivationService` (via Dependency Injection). Neue Methoden sind:

```csharp
public IReadOnlyList<IGitPlugin> GetEnabledSourceCodeManagementPlugins()
{
    return GetSourceCodeManagementPlugins()
        .Where(p => _activationService.IsPluginEnabled(p.PluginName))
        .ToList();
}

public IReadOnlyList<IKiPlugin> GetEnabledDevelopmentAutomationPlugins()
{
    return GetDevelopmentAutomationPlugins()
        .Where(p => _activationService.IsPluginEnabled(p.PluginName))
        .ToList();
}
```

Die bestehenden Methoden `GetDefaultSourceCodeManagementPlugin()` und `GetDefaultDevelopmentAutomationPlugin()` können weiterhin alle Plugins durchsuchen oder angepasst werden, um nur aus aktiven zu wählen (empfohlen: anpassen).

### Settings-UI und ViewModel-Architektur

**`SettingsViewModel`** erhält neue Commands und Collections:

```csharp
public ObservableCollection<IPluginActivationViewModel> SourceCodeManagementPlugins { get; }
public ObservableCollection<IPluginActivationViewModel> DevelopmentAutomationPlugins { get; }
public IPluginActivationViewModel? SelectedPlugin { get; set; }
public ICommand TogglePluginEnabledCommand { get; }
```

Beim Binding eines `SelectedPlugin` werden dessen `PluginSettings` dynamisch geladen und angezeigt (wiederverwendung bestehender `RenderPluginSettings()`-Logik).

### Single-Plugin-Verhalten

In `TaskDetailViewModel` und `ProjektViewModel`:

```csharp
var enabledScmPlugins = _pluginManager.GetEnabledSourceCodeManagementPlugins();
if (enabledScmPlugins.Count == 1)
{
    // Kein Selector anzeigen, Plugin automatisch nutzen
    SelectedScmPlugin = enabledScmPlugins[0];
    ShowScmPluginSelector = false;
}
else if (enabledScmPlugins.Count > 1)
{
    // Selector anzeigen
    ShowScmPluginSelector = true;
    AvailableScmPlugins = new(enabledScmPlugins);
}
else
{
    // Keine aktiven Plugins — Fehlerbehandlung
    ShowScmPluginSelector = false;
    // Error-State oder Dialog
}
```

### Entfernung alter Plugin-Tabs

Die Views entsprechender Tabs (z. B. `GitHubPluginSettingsTab`, `ClaudePluginSettingsTab`) müssen aus der `SettingsView.xaml` entfernt und ihr Code in das neue zentralisierte Plugins-Register verlagert werden.

## Konfiguration

Der Aktivierungsstatus ist **persistiert und benutzerspezifisch**:
- Pro Benutzer (in der lokalen SQLite-Datenbank) wird für jedes Plugin separat entschieden, ob es aktiv ist
- Konfiguration erfolgt in den Einstellungen (keine Kommandozeilenflags oder Umgebungsvariablen nötig)
- Neu entdeckte Plugins erhalten standardmäßig `Enabled = true`

## Offene Fragen

1. **Entfernung alter Plugin-Tabs:** Welche bestehenden Tabs sind konkret gemeint? (z. B. ein dedizierten Tab pro Plugin in der `SettingsView`? Oder etwas anderes?) Sollten die Plugin-Einstellungen vollständig ins neue Plugins-Register übernommen werden?

2. **Aktivierungsstatus-Scope:** Gilt der Aktivierungsstatus global für die Anwendung, oder soll er pro Projekt konfigurierbar sein?

3. **Fehlerfall:** Wenn alle Plugins eines Typs deaktiviert werden, wie soll sich die Anwendung verhalten? (z. B. Fehlerfreigabe, Dialog, Fallback?)

4. **Persistierungs-Ansatz:** Soll die Spalte-JSON (`AppEinstellung.PluginEnabledStatuses`) oder eine neue Tabelle (`PluginEnabledStatus`) verwendet werden?

5. **Plugin-Discovery-Timing:** Sollen neu entdeckte Plugins sofort nach dem Startup aktiv sein, oder erst nach Neustart/Reload?

6. **Audit:** Sollen Aktivierungs-/Deaktivierungsänderungen protokolliert werden (z. B. Zeitstempel, Benutzer)?
