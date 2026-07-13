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
  - **ComboBox für Arbeitsverzeichnis:** Zeigt verfügbare Verzeichnisse des ausgewählten Repositories; sichtbar bei erfolgreichem Strukturabruf und deaktiviert während des Lade-Vorgangs
  - **TextBox für manuelle Eingabe:** Sichtbar, wenn der Strukturabruf technisch fehlschlägt oder vom Plugin nicht unterstützt wird
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
| `IsWorkingDirectoryManualInput` | `bool` | Steuert, ob statt der ComboBox eine TextBox für das Arbeitsverzeichnis angezeigt wird |
| `WorkingDirectoryInputText` | `string?` | Manuell eingegebener relativer Pfad im Fallback-Modus |
| `WorkingDirectoryInputError` | `string?` | Validierungsfehler für die manuelle Eingabe |

## Arbeitsverzeichnis-Auswahl

Nach Auswahl eines Repositories lädt die Anwendung automatisch die Verzeichnisstruktur des externen Repositories und zeigt verfügbare Unterverzeichnisse zur Auswahl an. Das gewählte Arbeitsverzeichnis wird später beim Starten der KI-CLI als Working Directory verwendet — der Prozess führt dann in diesem Unterverzeichnis statt im Repository-Root aus.

Wenn der Remote-Abruf technisch nicht möglich ist, blockiert der Dialog die Zuweisung nicht. Statt der Auswahlbox wird dann eine TextBox angezeigt, in der der Benutzer einen relativen Pfad manuell erfassen kann.

### Technische Details zur Verzeichnisstruktur-Ladung

Beim Ändern des `SelectedRepository` wird die folgende Sequenz ausgelöst:

1. `SelectedWorkingDirectory` wird auf `null` zurückgesetzt (neuer Anfang)
2. Alte `CancellationTokenSource` wird abgebrochen (falls noch laufend)
3. Neue `CancellationTokenSource` wird erzeugt
4. `LoadDirectoryStructureAsync()` wird asynchron gestartet
5. `IsLoadingDirectoryStructure` wird auf `true` gesetzt
6. `DirectoryStructureBrowserService.GetDirectoryLoadResultAsync()` wird aufgerufen:
   - Falls Feature deaktiviert (`DirectoryStructure.Enabled == false`): `NotSupported` zurückgeben
   - Falls Cache-Hit für einen erfolgreichen Abruf: gecachtes Ergebnis zurückgeben (5-Minuten-TTL)
   - Sonst: `IGitPlugin.GetRepositoryStructureLoadResultAsync()` aufrufen, Ergebnisse filtern (nur Verzeichnisse), sortieren und erfolgreiche Ergebnisse cachen
7. Bei `RepositoryStructureLoadStatus.Success` werden Ergebnisse in `AvailableWorkingDirectories` eingefüllt, mit `"."` (Repository-Root) als erste Option
8. Bei `Failed` oder `NotSupported` wird `IsWorkingDirectoryManualInput = true` gesetzt und `WorkingDirectoryInputText` mit `"."` initialisiert
9. `SelectedWorkingDirectory` wird auf `"."` (Default) gesetzt oder beim Bestätigen aus der normalisierten manuellen Eingabe übernommen
10. `IsLoadingDirectoryStructure` wird auf `false` gesetzt

**Fehlerbehandlung:** Falls der Abruf fehlschlägt (z. B. Private Repository ohne Auth), wird eine Warnung geloggt und die UI wechselt in den manuellen Eingabemodus. Ein erfolgreich abgerufenes, aber leeres Repository gilt dagegen als Erfolg und zeigt weiterhin die Auswahlbox mit `"."`.

**Unterstützte Repository-Quellen:** Die Unterverzeichnis-Auswahl funktioniert für alle drei mitgelieferten SCM-Plugins, jeweils rein remote (ohne dass vorher geklont werden muss):

- **`LocalDirectoryPlugin`:** Liest die Verzeichnisstruktur direkt vom lokalen Dateisystem (rekursiv bis `MaxDepth`).
- **`GitHubPlugin`:** Ruft die Struktur des Standard-Branches über die GitHub Git-Trees-API ab (`gh api repos/{owner}/{repo}/git/trees/{branch}?recursive=1`). Bei sehr großen Repositories (API-Limit ca. 100.000 Einträge/7 MB) kann die Antwort als `truncated` markiert sein; die Anwendung protokolliert dies als Warnung, zeigt aber weiterhin die ermittelten Verzeichnisse an.
- **`BitbucketPlugin`:** Ruft die Struktur über die Bitbucket-REST-API ab. Im Cloud-Modus über die Source-API (`GET /2.0/repositories/{workspace}/{repo_slug}/src/{branch}/`, paginiert über den `next`-Link, mit rekursivem `max_depth`-Parameter). Im Self-Hosted-Modus (Bitbucket Server/Data Center) über die `browse`-API, die anders als die Cloud-API keine rekursive Tiefenabfrage kennt — die Anwendung baut die Struktur daher levelweise über mehrere API-Aufrufe auf (begrenzt auf `MaxDepth` Ebenen und max. 500 Verzeichnisse pro Ebene als Guardrail).

Kann die Repository-ID nicht aus der URL ermittelt werden oder schlägt der API-Aufruf fehl (z. B. fehlende Berechtigung, nicht erreichbare Instanz), liefert `DirectoryStructureBrowserService` ein Fehlerergebnis. Der Dialog bleibt funktionsfähig und zeigt eine TextBox, in der der Benutzer das Arbeitsverzeichnis manuell angeben kann.

### Verwendung des Arbeitsverzeichnisses

Das ausgewählte `SelectedWorkingDirectory` wird mit der `RepositoryStartKonfiguration` gespeichert (Property `WorkingDirectoryRelativePath`). Beim späteren Starten einer Aufgabe wird `WorkingDirectoryResolver` verwendet, um:
- Den absoluten Pfad zu kombinieren (`Path.Combine(repositoryRoot, relativePath)`)
- Path-Traversal-Angriffe zu verhindern (Validierung, dass normalisierter Pfad innerhalb normalisierten Roots liegt)
- Existenz des Zielverzeichnisses zu prüfen

Der resultierende Pfad wird an `KiAusfuehrungsService.StartCliAsync()` / `StartWithPseudoConsoleAsync()` als `ProcessStartInfo.WorkingDirectory` übergeben. Zusätzlich validiert `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(...)` das konfigurierte Arbeitsverzeichnis bereits direkt nach dem Git-Klon (aufgerufen aus `EntwicklungsprozessService.ProzessStartenAsync`), sodass ein fehlendes oder ungültiges Verzeichnis früher und mit einem klareren Fehlerbild erkannt wird als erst beim CLI-Start.

Das einmal zugewiesene Arbeitsverzeichnis kann jederzeit nachträglich geändert werden, ohne das Repository neu zuzuweisen — siehe [Dialog „Arbeitsverzeichnis bearbeiten"](dialog-arbeitsverzeichnis-bearbeiten.md).

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
   - ComboBox oder TextBox für Arbeitsverzeichnis ist deaktiviert
   - Text „Wird geladen…" wird angezeigt
4. Nach erfolgreichem Abruf:
   - `AvailableWorkingDirectories` wird befüllt (mindestens `"."`, ggf. weitere Verzeichnisse)
   - `IsWorkingDirectoryManualInput` ist `false`
   - `SelectedWorkingDirectory` wird auf `"."` gesetzt (Default Root)
   - `IsLoadingDirectoryStructure` wird auf `false` gesetzt
   - ComboBox wird aktiviert
5. Benutzer kann nun ein Arbeitsverzeichnis aus ComboBox wählen oder Default (`"."`) akzeptieren

**Fehlerfall — Verzeichnisstruktur nicht verfügbar:**
- Falls Plugin keine Struktur abrufen kann, der Abruf fehlschlägt oder Feature deaktiviert ist:
  - `IsWorkingDirectoryManualInput` wird `true`
  - `WorkingDirectoryInputText` wird auf `"."` gesetzt
  - Fehler wird geloggt (LogWarning)
  - Dialog bleibt funktional; Benutzer kann einen relativen Pfad manuell eingeben und speichern

**Leeres Repository:**
- Ein erfolgreicher Abruf ohne Unterverzeichnisse ist kein Fehler.
- Die ComboBox bleibt sichtbar und enthält nur `"."`.

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
| Manuelles Arbeitsverzeichnis leer | `WorkingDirectoryInputText` ist leer oder Whitespace | Wird als `"."` normalisiert |
| Manuelles Arbeitsverzeichnis absolut | `Path.IsPathRooted(...) == true` | „Zuweisen"-Button bleibt deaktiviert und `WorkingDirectoryInputError` wird gesetzt |
| Manuelles Arbeitsverzeichnis verlässt Repository | Pfad enthält ein Segment `..` | „Zuweisen"-Button bleibt deaktiviert und `WorkingDirectoryInputError` wird gesetzt |

## Konverter und Ressourcen

### Verwendete Konverter

- **`BoolToVisibilityConverter`:** Steuert Visibility der ListBox (visible wenn `HasScmPlugins == true`)
- **`InverseBoolToVisibilityConverter`:** Steuert Visibility des Hilfe-Panels (visible wenn `HasScmPlugins == false`) und blendet die Arbeitsverzeichnis-ComboBox aus, wenn der manuelle Modus aktiv ist

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
| `LoadDirectoryStructureAsync_ShouldEnableManualInput_WhenDirectoryLoadFails` | Überprüft, dass bei Service-Fehler der manuelle Eingabemodus mit `"."` aktiviert und Fehler geloggt wird |
| `LoadDirectoryStructureAsync_ShouldKeepSelectionMode_WhenRepositoryIsEmpty` | Überprüft, dass ein erfolgreicher leerer Abruf die ComboBox mit `"."` zeigt |
| `BestaetigenCommand_ShouldUseManualWorkingDirectoryInput` | Überprüft, dass die manuelle Eingabe normalisiert nach `SelectedWorkingDirectory` übernommen wird |

### Unit-Tests in `DirectoryStructureBrowserServiceTests`

| Test | Beschreibung |
|---|---|
| `GetDirectoriesAsync_ShouldReturnDirectories` | Überprüft, dass Service Liste von Verzeichnis-Pfaden zurückgibt |
| `GetDirectoriesAsync_ShouldCache_WithTTL` | Überprüft, dass zweiter Abruf aus Cache kommt und TTL respektiert wird |
| `GetDirectoriesAsync_ShouldHandleErrors_Gracefully` | Überprüft, dass bei Fehler leere Liste zurückgegeben wird, kein Exception |
| `GetDirectoriesAsync_ShouldCallPluginMethod` | Überprüft die Kompatibilitätsmethode für direkte Verzeichnislisten-Aufrufe |
| `GetDirectoriesAsync_ShouldReturnEmpty_WhenFeatureDisabled` | Überprüft, dass leere Liste zurückgegeben wird wenn Feature deaktiviert ist |
| `GetDirectoryLoadResultAsync_ShouldReturnSuccess_ForEmptyRepository` | Überprüft, dass ein erfolgreicher leerer Plugin-Result kein Fehlerfallback ist |
| `GetDirectoryLoadResultAsync_ShouldReturnFailed_WhenPluginThrows` | Überprüft, dass technische Fehler als Fehlerstatus zurückgegeben und nicht gecacht werden |

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
2. Ruft `DirectoryStructureBrowserService.GetDirectoryLoadResultAsync()` auf
3. Nutzt `CancellationTokenSource` zur Abbrechung bei Plugin-/Repository-Wechsel
4. Fehlerstatus wird geloggt und als manueller Eingabemodus an das ViewModel weitergegeben; Dialog bleibt responsiv

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

- **Cache-Key:** Plugin-Präfix, Repository-URL und `MaxDepth`, damit verschiedene Plugins oder Tiefen nicht kollidieren
- **TTL:** 5 Minuten (konfigurierbar via `DirectoryStructureOptions.CacheDurationSeconds`)
- **Größenlimit:** Keine explizite Obergrenze, aber typischerweise klein (< 1 MB pro Repository)
- **Invalidierung:** Automatisch nach TTL; fehlgeschlagene Abrufe werden nicht gecacht

### Backward Compatibility

Der `DirectoryStructureBrowserService`-Parameter ist optional mit Standardwert `null`:
```csharp
public RepositoryAssignViewModel(..., DirectoryStructureBrowserService? directoryStructureService = null)
```

Falls `directoryStructureService == null`, bleibt `AvailableWorkingDirectories` leer. Der Dialog wird aber nicht blockiert; Benutzer kann trotzdem ein Repository zuweisen.
