← [Zurück zur Übersicht](index.md)

# Repository-Auswahl-Dialog

## Übersicht

Der Repository-Auswahl-Dialog ermöglicht die Zuweisung von Git-Repositories zu einem Projekt mit expliziter SCM-Plugin-Auswahl. Nach Auswahl des SCM-Plugins werden nur Repositories dieser Quelle angezeigt. Wenn keine SCM-Plugins installiert sind, wird ein Hilfe-Panel mit Installationsanweisungen angezeigt.

## Komponenten

### Dialog-Fenster (`RepositoryAssignDialog`)

Das Dialog-Fenster ist ein modales WPF-Window mit der Größe 500×400 Pixel, zentriert zum Parent-Window.

**Elemente:**
- **Titel:** „Repository zuweisen" (Kopfzeile)
- **ComboBox für Plugin-Auswahl:** Zeigt verfügbare SCM-Plugins; wechselbar mit Tastatur oder Maus
- **Repository-ListBox:** Zeigt gefilterte Repositories mit Name und URL
- **Hilfe-Panel:** Zeigt Instruktionen bei fehlenden Plugins
- **Buttons:** „Zuweisen" (aktiviert nur wenn Repository ausgewählt) und „Abbrechen"

### ViewModel (`RepositoryAssignViewModel`)

Das ViewModel verwaltet die Dialog-Logik und wird mit folgenden Dependencies injiziert:
- `ProjektService` — lädt verfügbare Repositories
- `ILogger<RepositoryAssignViewModel>` — protokolliert Fehler
- `IPluginManager` — lädt verfügbare SCM-Plugins

**Properties:**

| Property | Typ | Beschreibung |
|----------|-----|--------------|
| `AvailableScmPlugins` | `ObservableCollection<IGitPlugin>` | Liste aller verfügbaren SCM-Plugins |
| `SelectedScmPlugin` | `IGitPlugin?` | Aktuell vom Benutzer gewähltes Plugin; triggert Repository-Reload bei Änderung |
| `HasScmPlugins` | `bool` | Indikator, ob SCM-Plugins vorhanden sind; steuert Visibility von ComboBox und Hilfe-Panel |
| `VerfuegbareRepositories` | `ObservableCollection<GitRepository>` | Gefilterte Liste von Repositories des ausgewählten Plugins |
| `SelectedRepository` | `GitRepository?` | Vom Benutzer ausgewähltes Repository |
| `IsLoading` | `bool` | Flag, das während async Operationen gesetzt ist |

## Szenarios

### Szenario 1: Dialog mit verfügbaren Plugins

**Vorbedingung:** Mindestens ein SCM-Plugin ist installiert.

**Darstellung:**
- ComboBox zeigt Pluginnamen („GitHub", „GitLab", etc.)
- ListBox ist initially leer
- Hilfe-Panel ist ausgeblendet
- „Zuweisen"-Button ist deaktiviert

**Benutzerflow:**
1. Benutzer wählt ein Plugin aus ComboBox
2. Repositories werden gefiltert und angezeigt
3. Benutzer klickt auf ein Repository zum Auswählen
4. „Zuweisen"-Button wird aktiv und kann geklickt werden

### Szenario 2: Dialog ohne SCM-Plugins

**Vorbedingung:** Keine SCM-Plugins sind installiert.

**Darstellung:**
- ComboBox ist ausgeblendet
- ListBox ist ausgeblendet
- Hilfe-Panel wird angezeigt mit:
  - Titel: „Keine SCM-Plugins installiert"
  - Text: „Um Repositories zuzuweisen, installieren Sie bitte ein SCM-Plugin (z.B. GitHub Plugin). Weitere Informationen finden Sie in der Dokumentation."
- „Zuweisen"-Button ist deaktiviert
- Dialog ist funktional nicht nutzbar

**Benutzerflow:**
- Benutzer kann nur „Abbrechen" klicken
- Dialog schließt sich ohne Änderungen

### Szenario 3: Plugin-Wechsel

**Auslöser:** Benutzer wählt ein anderes Plugin aus ComboBox.

**Verhalten:**
1. `SelectedScmPlugin` wird gesetzt
2. Asynchrone `ReloadRepositoriesForSelectedPlugin()`-Methode wird ausgelöst (Fire-and-Forget)
3. `IsLoading` wird auf `true` gesetzt
4. Alle Repositories werden geladen und nach Plugin-Typ gefiltert
5. Gefilterte Repositories werden nach Name sortiert
6. `VerfuegbareRepositories` wird aktualisiert
7. `SelectedRepository` wird auf `null` zurückgesetzt (für sauberen Zustand)
8. `IsLoading` wird auf `false` gesetzt
9. ListBox wird mit neuen Repositories aktualisiert

**Fehlerfall:**
- Wenn `ProjektService.GetAllRepositoriesAsync()` wirft Exception:
  - `Logger.LogError()` wird aufgerufen
  - `VerfuegbareRepositories` wird geleert
  - Keine Exception wird propagiert; Dialog bleibt responsiv

## Dark-Mode Unterstützung

Der Dialog unterstützt beide Themes (Light und Dark) vollständig:
- **ComboBox:** `Background`, `Foreground`, `BorderBrush` via `DynamicResource`
- **ListBox:** `Background`, `BorderBrush` via `DynamicResource`; Text über `PrimaryTextBrush` und `SecondaryTextBrush`
- **Hilfe-Panel:** `Background`, Texte über `PrimaryTextBrush` und `SecondaryTextBrush`
- **Buttons:** 
  - „Zuweisen"-Button: `Foreground="{DynamicResource PrimaryTextBrush}"` (ersetzt hardcodiertes Weiß)
  - „Abbrechen"-Button: `Foreground="{DynamicResource PrimaryTextBrush}"`

## Validierung

| Feld / Zustand | Regel | Aktion bei Verletzung |
|---|---|---|
| SCM-Plugin-Liste leer | Keine Plugins verfügbar | Hilfe-Panel zeigen; Dialog deaktivieren |
| Plugin nicht gewählt | `SelectedScmPlugin == null` | Repository-Liste bleibt leer; „Zuweisen"-Button deaktiviert |
| Repository nicht gewählt | `SelectedRepository == null` | „Zuweisen"-Button deaktiviert |
| Plugin-Typ-Vergleich | `GitRepository.PluginTyp` muss mit `IGitPlugin.PluginType.ToString()` exakt übereinstimmen (case-sensitive) | Repositories mit nicht übereinstimmendem PluginTyp werden gefiltert |

## Konverter und Ressourcen

### Verwendete Konverter

- **`BoolToVisibilityConverter`:** Steuert Visibility der ListBox (visible wenn `HasScmPlugins == true`)
- **`InverseBoolToVisibilityConverter`:** Steuert Visibility des Hilfe-Panels (visible wenn `HasScmPlugins == false`)

### Verwendete Theme-Ressourcen

- **`BackgroundBrush`:** Hintergrund von ComboBox, ListBox und Hilfe-Panel
- **`BorderBrush`:** Rahmen von Eingabe-Elementen
- **`PrimaryTextBrush`:** Haupttext (Plugin-Namen, Repository-Namen, Button-Text)
- **`SecondaryTextBrush`:** Sekundärtext (Repository-URLs, Hilfetext)
- **`SurfaceBrush`:** Dialog-Hintergrund
- **`AccentBrush`:** Background des „Zuweisen"-Buttons

## Tests

### Unit-Tests in `RepositoryAssignViewModelTests`

| Test | Beschreibung |
|---|---|
| `LadenAsync_ShouldLoadAvailablePlugins_WhenPluginsExist` | Überprüft, dass `LadenAsync()` Plugins lädt |
| `LadenAsync_ShouldSetHasScmPlugins_ToTrue_WhenPluginsAvailable` | Überprüft, dass `HasScmPlugins = true` wenn Plugins vorhanden |
| `LadenAsync_ShouldSetHasScmPlugins_ToFalse_WhenNoPluginsAvailable` | Überprüft, dass `HasScmPlugins = false` wenn keine Plugins vorhanden |
| `SelectedScmPluginChanged_ShouldReloadRepositories_FilteredByPluginType` | Überprüft, dass Repositories nach Plugin-Typ gefiltert werden |
| `SelectedScmPluginChanged_ShouldClearRepositories_WhenPluginUnselected` | Überprüft, dass Repositories geleert werden wenn Plugin deselektiert |
| `SelectedScmPluginChanged_ShouldSetIsLoading_FlagDuringReload` | Überprüft, dass `IsLoading` während Reload gesetzt wird |
| `ReloadRepositoriesForSelectedPlugin_ShouldLogError_WhenServiceThrows` | Überprüft Error-Handling und Logging |
| `RepositorySelection_ShouldEnableBestaetigenCommand_WhenRepositorySelected` | Überprüft, dass Command aktiviert wird bei Auswahl |
| `RepositorySelection_ShouldDisableBestaetigenCommand_WhenRepositoryUnselected` | Überprüft, dass Command deaktiviert wird ohne Auswahl |

## Implementierungsnotizen

### Plugin-Typ-Vergleich

Die Repository-Filterung erfolgt über String-Vergleich:
```
r.PluginTyp == SelectedScmPlugin.PluginType.ToString()
```

Dies ist case-sensitiv. Beispiele:
- `GitRepository.PluginTyp = "SourceCodeManagement"` ✓ matched `IGitPlugin.PluginType = PluginType.SourceCodeManagement`
- `GitRepository.PluginTyp = "DevelopmentAutomation"` ✓ matched `IGitPlugin.PluginType = PluginType.DevelopmentAutomation`

### Fire-and-Forget Async

Die `ReloadRepositoriesForSelectedPlugin()`-Methode wird aus dem Property-Setter ausgelöst:
```csharp
value => SetProperty(ref _selectedScmPlugin, value, () => _ = ReloadRepositoriesForSelectedPlugin());
```

Die Task wird ignoriert (Discard `_`) und Fehler werden lokal abgefangen und geloggt.

### Backward Compatibility

Der `IPluginManager`-Parameter ist optional mit Standardwert `null`:
```csharp
public RepositoryAssignViewModel(..., IPluginManager? pluginManager = null)
```

Falls `pluginManager == null`, bleibt `AvailableScmPlugins` leer und der Dialog zeigt das Hilfe-Panel.
