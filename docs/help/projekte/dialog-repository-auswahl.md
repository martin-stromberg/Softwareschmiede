← [Zurück zur Übersicht](index.md)

# Repository-Auswahl-Dialog

## Übersicht

Der Repository-Auswahl-Dialog ermöglicht die Zuweisung von Git-Repositories zu einem Projekt mit expliziter SCM-Plugin-Auswahl. Nach Auswahl des SCM-Plugins werden nur Repositories dieser Quelle angezeigt. Wenn keine SCM-Plugins installiert sind, wird ein Hilfe-Panel mit Installationsanweisungen angezeigt. Nach Auswahl eines Repositories kann der Benutzer optional ein Arbeitsverzeichnis innerhalb des Repositories wählen — dieses wird später beim CLI-Start als Working Directory verwendet.

## Komponenten

### Dialog-Fenster (`RepositoryAssignDialog`)

Das Dialog-Fenster ist ein modales WPF-Window mit der Größe 500×400 Pixel, zentriert zum Parent-Window.

**Elemente:**
- **Titel:** „Repository zuweisen" (Kopfzeile)
- **ComboBox für Plugin-Auswahl:** Zeigt verfügbare SCM-Plugins; wechselbar mit Tastatur oder Maus
- **Repository-ListBox:** Zeigt gefilterte Repositories mit Name und URL
- **Arbeitsverzeichnis-Sektion** (nach Repository-Auswahl):
  - **Label:** „Arbeitsverzeichnis im Repository"
  - **ComboBox für Arbeitsverzeichnis:** Zeigt verfügbare Verzeichnisse des ausgewählten Repositories; deaktiviert während Lade-Vorgang
  - **Lade-Indikator:** Text „Wird geladen…" (sichtbar während `IsLoadingDirectoryStructure == true`)
  - **Hinweis-Text:** „Hinweis: '.' bedeutet Wurzelverzeichnis des Repositories"
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
| `VerfuegbareRepositories` | `ObservableCollection<AvailableRepository>` | Gefilterte Liste von Repositories des ausgewählten Plugins |
| `SelectedRepository` | `AvailableRepository?` | Vom Benutzer ausgewähltes Repository; triggert Laden der Verzeichnisstruktur bei Änderung |
| `IsLoading` | `bool` | Flag, das während async Operationen gesetzt ist |
| `AvailableWorkingDirectories` | `ObservableCollection<string>` | Liste verfügbarer Arbeitsverzeichnisse des ausgewählten Repositories; enthält mindestens `"."` (Repository-Root) |
| `SelectedWorkingDirectory` | `string?` | Vom Benutzer ausgewähltes Arbeitsverzeichnis (relativer Pfad, `"."` = Repository-Root); Default ist `"."` |
| `IsLoadingDirectoryStructure` | `bool` | Flag, das während des Abrufens der Verzeichnisstruktur gesetzt ist |

## Arbeitsverzeichnis-Auswahl

Nach Auswahl eines Repositories lädt die Anwendung automatisch die Verzeichnisstruktur des externen Repositories und zeigt verfügbare Unterverzeichnisse zur Auswahl an. Das gewählte Arbeitsverzeichnis wird später beim Starten der KI-CLI als Working Directory verwendet — der Prozess führt dann in diesem Unterverzeichnis statt im Repository-Root aus.

### Technische Details zur Verzeichnisstruktur-Ladung

Beim Ändern des `SelectedRepository` wird die folgende Sequenz ausgelöst:

1. `SelectedWorkingDirectory` wird auf `null` zurückgesetzt (neuer Anfang)
2. Alte `CancellationTokenSource` wird abgebrochen (falls noch laufend)
3. Neue `CancellationTokenSource` wird erzeugt
4. `LoadDirectoryStructureAsync()` wird asynchron gestartet
5. `IsLoadingDirectoryStructure` wird auf `true` gesetzt
6. `DirectoryStructureBrowserService.GetDirectoriesAsync()` wird aufgerufen:
   - Falls Feature deaktiviert (`DirectoryStructure.Enabled == false`): leere Liste zurückgeben
   - Falls Cache-Hit: gecachte Liste zurückgeben (5-Minuten-TTL)
   - Sonst: `IGitPlugin.GetRepositoryStructureAsync()` aufrufen, Ergebnisse filtern (nur Verzeichnisse), sortieren und cachen
7. Ergebnisse werden in `AvailableWorkingDirectories` eingefüllt, mit `"."` (Repository-Root) als erste Option
8. `SelectedWorkingDirectory` wird auf `"."` (Default) gesetzt
9. `IsLoadingDirectoryStructure` wird auf `false` gesetzt

**Fehlerbehandlung:** Falls Abruf fehlschlägt (z. B. Private Repository ohne Auth), wird ein Fehler geloggt; die Collection wird geleert, aber `"."` wird dennoch angezeigt, sodass der Benutzer mindestens den Root wählen kann.

**Einschränkung:** Sofern das Git-Plugin die Methode `IGitPlugin.GetRepositoryStructureAsync()` nicht implementiert, wird `NotSupportedException` geworfen und nur `"."` angezeigt. Dies ist die aktuelle Situation — die Infrastruktur ist bereit, sobald Plugins die Methode implementieren.

### Verwendung des Arbeitsverzeichnisses

Das ausgewählte `SelectedWorkingDirectory` wird mit der `RepositoryStartKonfiguration` gespeichert (Property `WorkingDirectoryRelativePath`). Beim späteren Starten einer Aufgabe wird `WorkingDirectoryResolver` verwendet, um:
- Den absoluten Pfad zu kombinieren (`Path.Combine(repositoryRoot, relativePath)`)
- Path-Traversal-Angriffe zu verhindern (Validierung, dass normalisierter Pfad innerhalb normalisierten Roots liegt)
- Existenz des Zielverzeichnisses zu prüfen

Der resultierende Pfad wird an `KiAusfuehrungsService.StartCliAsync()` / `StartWithPseudoConsoleAsync()` als `ProcessStartInfo.WorkingDirectory` übergeben.

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
8. `AvailableWorkingDirectories` wird geleert (neue Repositories = neue Verzeichnisstrukturen)
9. `IsLoading` wird auf `false` gesetzt
10. ListBox wird mit neuen Repositories aktualisiert

**Fehlerfall:**
- Wenn `ProjektService.GetAllRepositoriesAsync()` wirft Exception:
  - `Logger.LogError()` wird aufgerufen
  - `VerfuegbareRepositories` wird geleert
  - Keine Exception wird propagiert; Dialog bleibt responsiv

### Szenario 4: Repository-Auswahl mit Arbeitsverzeichnis-Laden

**Auslöser:** Benutzer wählt ein Repository aus ListBox.

**Vorbedingung:** SCM-Plugin ist gewählt und verfügbar.

**Verhalten:**
1. `SelectedRepository` wird gesetzt
2. Setter ruft `OnSelectedRepositoryChanged()` auf:
   - `SelectedWorkingDirectory` wird auf `null` zurückgesetzt
   - Alte CancellationTokenSource wird abgebrochen
   - Neue CancellationTokenSource wird erzeugt
   - `LoadDirectoryStructureAsync()` wird asynchron gestartet
3. Während des Ladens:
   - `IsLoadingDirectoryStructure` wird auf `true` gesetzt
   - ComboBox für Arbeitsverzeichnis ist deaktiviert
   - Text „Wird geladen…" wird angezeigt
4. Nach erfolgreichem Abruf:
   - `AvailableWorkingDirectories` wird befüllt (mindestens `"."`, ggf. weitere Verzeichnisse)
   - `SelectedWorkingDirectory` wird auf `"."` gesetzt (Default Root)
   - `IsLoadingDirectoryStructure` wird auf `false` gesetzt
   - ComboBox wird aktiviert
5. Benutzer kann nun ein Arbeitsverzeichnis aus ComboBox wählen oder Default (`"."`) akzeptieren

**Fehlerfall — Verzeichnisstruktur nicht verfügbar:**
- Falls Plugin keine Struktur abrufen kann oder Feature deaktiviert ist:
  - `AvailableWorkingDirectories` enthält nur `"."`
  - Fehler wird geloggt (LogWarning)
  - `SelectedWorkingDirectory` wird auf `"."` gesetzt
  - Dialog bleibt funktional; Benutzer kann nur Root wählen

**Fehlerfall — Abruf abgebrochen:**
- Falls Benutzer schnell Plugin oder Repository wechselt, wird alte `CancellationTokenSource` abgebrochen
- `LoadDirectoryStructureAsync()` wirft `OperationCanceledException`
- Fehler wird abgefangen; kein Logging (normales Verhalten bei Wechsel)

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
| `SelectedRepositoryChanged_ShouldLoadDirectoryStructure` | Überprüft, dass `LoadDirectoryStructureAsync()` aufgerufen wird bei Repository-Änderung |
| `SelectedRepositoryChanged_ShouldResetSelectedWorkingDirectory` | Überprüft, dass `SelectedWorkingDirectory` auf `null` gesetzt wird |
| `SelectedRepositoryChanged_ShouldCancelPreviousLoad` | Überprüft, dass alte CancellationTokenSource abgebrochen wird |
| `LoadDirectoryStructureAsync_ShouldSetIsLoading_Flag` | Überprüft, dass `IsLoadingDirectoryStructure` während Abruf auf `true`, danach auf `false` gesetzt wird |
| `LoadDirectoryStructureAsync_ShouldPopulateDirectories_WithDotRoot` | Überprüft, dass `AvailableWorkingDirectories` mit `"."` (Root) + abgerufene Verzeichnisse befüllt wird |
| `LoadDirectoryStructureAsync_ShouldSetDefaultSelectedDirectory` | Überprüft, dass `SelectedWorkingDirectory` auf `"."` (Default) gesetzt wird |
| `LoadDirectoryStructureAsync_ShouldHandleNullRepository` | Überprüft, dass Collection geleert wird bei `SelectedRepository = null` |
| `LoadDirectoryStructureAsync_ShouldHandleErrors_WithLogging` | Überprüft, dass bei Service-Fehler Collection mit nur `"."` befüllt und Fehler geloggt wird |

### Unit-Tests in `DirectoryStructureBrowserServiceTests`

| Test | Beschreibung |
|---|---|
| `GetDirectoriesAsync_ShouldReturnDirectories` | Überprüft, dass Service Liste von Verzeichnis-Pfaden zurückgibt |
| `GetDirectoriesAsync_ShouldCache_WithTTL` | Überprüft, dass zweiter Abruf aus Cache kommt und TTL respektiert wird |
| `GetDirectoriesAsync_ShouldHandleErrors_Gracefully` | Überprüft, dass bei Fehler leere Liste zurückgegeben wird, kein Exception |
| `GetDirectoriesAsync_ShouldCallPluginMethod` | Überprüft, dass `IGitPlugin.GetRepositoryStructureAsync()` aufgerufen wird |
| `GetDirectoriesAsync_ShouldReturnEmpty_WhenFeatureDisabled` | Überprüft, dass leere Liste zurückgegeben wird wenn Feature deaktiviert ist |

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

### Arbeitsverzeichnis-Auswahl: Asynchrones Laden

Das Laden der Verzeichnisstruktur erfolgt asynchron über `LoadDirectoryStructureAsync()`:

1. Wird aufgerufen, wenn `SelectedRepository` sich ändert (Property-Setter mit Callback)
2. Ruft `DirectoryStructureBrowserService.GetDirectoriesAsync()` auf
3. Nutzt `CancellationTokenSource` zur Abbrechung bei Plugin-/Repository-Wechsel
4. Fehler werden abgefangen und geloggt; Dialog bleibt responsiv

**Cancellation-Handling:**
- Wenn Benutzer schnell ein anderes Repository wählt, wird alte CancellationTokenSource abgebrochen
- `OperationCanceledException` wird abgefangen (kein Fehler-Logging für normal abgebrochene Tasks)
- ComboBox-Inhalt wird mit aktuellem Repository aktualisiert

### Path-Traversal-Prevention

Die `WorkingDirectoryResolver`-Klasse validiert, dass der aufgelöste Pfad innerhalb des Repository-Roots bleibt:

```csharp
var normalizedRoot = Path.GetFullPath(repositoryRoot);
var normalized = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
if (!normalized.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, ...))
    throw new InvalidOperationException("Pfad verlässt Repository-Verzeichnis");
```

Diese Logik verhindert Pfad-Traversal-Angriffe (z. B. `"../../../etc"`) und ist plattformübergreifend korrekt (Windows und Unix).

### Caching von Verzeichnisstrukturen

`DirectoryStructureBrowserService` cacht abgerufene Verzeichnisstrukturen:

- **Cache-Key:** `"dirs:{repositoryUrl}"` (pro Repository eindeutig)
- **TTL:** 5 Minuten (konfigurierbar via `DirectoryStructureOptions.CacheDurationSeconds`)
- **Größenlimit:** Keine explizite Obergrenze, aber typischerweise klein (< 1 MB pro Repository)
- **Invalidierung:** Automatisch nach TTL; kein manuelles Refresh im MVP

### Backward Compatibility

Der `DirectoryStructureBrowserService`-Parameter ist optional mit Standardwert `null`:
```csharp
public RepositoryAssignViewModel(..., DirectoryStructureBrowserService? directoryStructureService = null)
```

Falls `directoryStructureService == null`, bleibt `AvailableWorkingDirectories` leer. Der Dialog wird aber nicht blockiert; Benutzer kann trotzdem ein Repository zuweisen.
