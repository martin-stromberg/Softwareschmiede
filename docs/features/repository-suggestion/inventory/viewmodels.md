# ViewModels und UI

## `ProjectDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`

### Relevante Properties für Repository-Suggestion

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `ProjektId` | `Guid` | Die angezeigte Projekt-ID |
| `Projekt` | `Projekt?` | Das geladene Projekt-Entity |
| `IsLoading` | `bool` | Loading-Flag während Datenladeoperationen |
| `Aufgaben` | `ObservableCollection<Aufgabe>` | Geladene Aufgaben des Projekts |
| `SelectedRepository` | `GitRepository?` | Das aktuell ausgewählte Repository |
| `IssueVorschlaege` | `ObservableCollection<Issue>` | Issues aus dem aktuellen SCM-Plugin |
| `IsLoadingIssues` | `bool` | Loading-Flag für Issues |

### Relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LadenAsync(CancellationToken)` | private | Lädt Projekt, Aufgaben, Repositories und Issues asynchron |
| `LadenIssuesAsync(CancellationToken)` | private | Lädt Issues für das aktuell ausgewählte Repository |
| `RepositoryZuweisenAsync(CancellationToken)` | private | Öffnet Dialog zum Zuweisen eines Repositories |

### Commands

| Command | Zweck |
|---------|-------|
| `RepositoryZuweisenCommand` | Öffnet den Repository-Zuweisungs-Dialog — **nutzt `RepositoryAssignViewModel`** |
| `RepositoryOeffnenCommand` | Öffnet das ausgewählte Repository im Browser |
| `LadenCommand` | Lädt das Projekt neu |

### Abhängigkeiten
- `ProjektService` — für Projekt-CRUD-Operationen
- `AufgabeService` — für Aufgabenladeoperationen
- `IPluginManager` — für SCM-Plugin-Zugriff (zur Bestimmung von Plugin-Capabilities)
- `IDialogService` — für Dialog-Anzeige
- `IServiceProvider` — für VM-Instanziierung

### Notizen
- ViewModel ruft bereits `LadenIssuesAsync()` auf, nutzt aber nur das aktuell ausgewählte Repository
- **Noch keine Property für unzugeordnete Repositories** — muss hinzugefügt werden
- Issue-Laden ist bereits implementiert und kann als Vorbild dienen

---

## `RepositoryAssignViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`

### Relevante Properties

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `VerfuegbareRepositories` | `ObservableCollection<AvailableRepository>` | Verfügbare Repositories zur Auswahl (aus Plugin-Quelle) |
| `SelectedRepository` | `AvailableRepository?` | Vom Benutzer ausgewähltes Repository |
| `AvailableScmPlugins` | `ObservableCollection<IGitPlugin>` | Alle verfügbaren SCM-Plugins |
| `SelectedScmPlugin` | `IGitPlugin?` | Aktuell vom Benutzer gewähltes Plugin |
| `IsLoading` | `bool` | Loading-Flag |
| `HasScmPlugins` | `bool` | Gibt an, ob SCM-Plugins vorhanden sind |

### Relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LadenAsync(CancellationToken)` | public | Lädt alle SCM-Plugins über `IPluginManager` |
| `ReloadRepositoriesForSelectedPlugin(CancellationToken)` | private | Lädt Repositories für das ausgewählte Plugin via `GetAvailableRepositoriesAsync()` |

### Sortierlogik
In `ReloadRepositoriesForSelectedPlugin()`:
```csharp
var sorted = repos.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
```
- **Primär**: Absteigend nach `UpdatedAt` (neueste zuerst)
- **Sekundär**: Aufsteigend nach `Name` (alphabetisch, Case-insensitive)

### Commands

| Command | Zweck |
|---------|-------|
| `BestaetigenCommand` | Bestätigt Auswahl (enabled wenn Repository ausgewählt) |
| `AbbrechenCommand` | Bricht Dialog ab |

### Events
- `CloseRequested` — Wird aufgerufen zum Schließen des Dialogs (Parameter: `true` = bestätigt, `false` = abgebrochen)

### Abhängigkeiten
- `IPluginManager` — zum Laden von Plugins und deren Repositories
- `ILogger<RepositoryAssignViewModel>` — zum Logging

### Notizen
- ViewModel lädt Repositories aus jedem Plugin einzeln
- **Sortiert bereits Repositories nach `UpdatedAt` absteigend** — dies ist die gewünschte Sortierlogik
- Dieser ViewModel könnte als Vorbild für die neue `UnassignedRepositories`-Liste dienen, **aber aggregiert bereits verfügbare Repositories pro Plugin**
- Für die neue Feature könnten wir eine ähnliche Logik verwenden, aber alle Repositories über **alle** Plugins sammeln und filtern nach "nicht zugeordnet"

---

## `ProjectDetailView`
Datei: `src/Softwareschmiede.App/Views/ProjectDetailView.xaml` (XAML-View)

- Zeigt Projektdetails an
- Enthält Aufgabenliste
- Enthält Repository-Liste für zugeordnete Repositories
- Bindet auf `ProjectDetailViewModel`
- **Noch kein Panel für unzugeordnete Repositories** — muss hinzugefügt werden
