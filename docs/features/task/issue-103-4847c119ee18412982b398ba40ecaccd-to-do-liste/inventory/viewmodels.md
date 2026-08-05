# ViewModels und UI - Bestandsaufnahme

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

### Aktuelle Collections und Properties (bezüglich ähnlicher Features)

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| Aufgabe | Aufgabe? | Die aktuell angezeigte Aufgabe |
| Protokolleintraege | ObservableCollection<Protokolleintrag> | Protokolleinträge der Aufgabe |
| PullRequests | ObservableCollection<PullRequestReferenz> | Pull Requests der Aufgabe |
| VerfuegbareKiPlugins | ObservableCollection<string> | Verfügbare KI-Plugins |
| PromptVorlagen | ObservableCollection<PromptVorlage> | Verfügbare Promptvorlagen |

### Aktuelle Ansichten (DetailAnsicht enum)
```csharp
enum DetailAnsicht
{
    Info,           // Info-Tab
    Cli,            // CLI-Fenster
    Diff,           // Diff-Ansicht
    Dateibrowser,   // Dateiexplorer
    PullRequests    // Pull Requests
}
```

### Aktuelle Commands (Auswahl relevanter)
- `AufgabeAbschliessenCommand` - Schließt Aufgabe ab
- `SpeichernCommand` - Speichert Änderungen
- `LoeschenCommand` - Löscht Aufgabe
- `PullRequestErstellenCommand` - Erstellt PR
- `PromptVorlageAuswaehlenCommand` - Sendet Prompt
- `InfoViewCommand`, `CliViewCommand`, `DiffViewCommand`, `DateiViewCommand`, `PullRequestViewCommand` - Ansicht-Navigation

### View-Selektoren (IsXViewSelected patterns)
```csharp
public bool IsInfoViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Info;
public bool IsCliViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Cli;
public bool IsDiffViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Diff;
public bool IsFileExplorerViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Dateibrowser;
public bool IsPullRequestViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.PullRequests;
```

### LadenAsync-Methode (Übersicht)
Laden-Logik:
1. LadenAsync wird aufgerufen, wenn AufgabeId gesetzt wird
2. Aufgabe wird via AufgabeService.GetDetailAsync geladen
3. Protokolleinträge werden via ProtokollService geladen
4. Pull Requests werden via PullRequestReferenzService geladen
5. Verfügbare Plugins und Vorlagen werden geladen

**Fehlend:** Todos-Loading - sollte nach Pull Requests geladen werden

### Aufgaben-Abschluss-Logik
```csharp
private async Task AufgabeAbschliessenAsync(CancellationToken ct)
{
    // ... 
    await _entwicklungsprozessService.AbschliessenAsync(_aufgabeId, ct);
    await LadenAsync(ct);
}
```

**Fehlend:** Validierung auf offene Todos vor Abschluss. Sollte Fehler zeigen, wenn Todos offen sind.

## `TaskDetailView.xaml`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`

Existiert als XAML-View mit verschiedenen Bereichen, aber ohne Todo-Komponenten.

## Zu erstellende/erweiternde ViewModels

### `TodoViewModel` (neu)
Sollte enthalten:
- Properties: Id, Beschreibung, IstErledigt, ErstellungsDatum
- Commands: DeleteCommand, MarkCompletedCommand
- Implementiert INotifyPropertyChanged für MVVM-Bindung

### `TaskDetailViewModel` (Erweiterung)
Sollte erweitert werden um:

**Collections:**
- `ObservableCollection<TodoViewModel> Todos { get; }`

**Properties:**
- `int OffeneTodoCount { get; }` - Für Badge-Anzeige im Ribbon
- `bool ShowTodoPanel { get; }` - Bestimmt, ob Todo-Tab angezeigt wird

**Commands:**
- `ICommand TodoHinzufuegenCommand { get; }` - Neues Todo erstellen
- `ICommand TodoLoeschenCommand { get; }` - Todo löschen
- `ICommand TodoAlsErledeltMarkierenCommand { get; }` - Todo als erledigt markieren
- `ICommand TodoAnsichtCommand { get; }` - Wechsel zur Todo-Ansicht

**Ansicht:**
- `DetailAnsicht.Todos` zur enum hinzufügen
- `IsTodoViewSelected` Property hinzufügen
- View-Selector-Logik erweitern

**LadenAsync erweitern:**
- Todos laden nach Pull Requests
- Include Todos in AufgabeService.GetDetailAsync

**AufgabeAbschliessenAsync erweitern:**
- Vor AbschliessenAsync aufrufen: `CanCompleteTaskAsync(aufgabeId)` prüfen
- Bei offenen Todos: Fehlermeldung anzeigen
- Beispielmeldung: "Diese Aufgabe kann nicht beendet werden, solange noch 3 offene To-Dos vorhanden sind."

## `FileExplorerViewModel` (für Referenz)
Datei: `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs`

Beispiel für komplexere ViewModel-Struktur mit Collections und Commands. Wird als Property im TaskDetailViewModel kompositoriert:
```csharp
private readonly FileExplorerViewModel _fileExplorerViewModel;
public FileExplorerViewModel FileExplorer => _fileExplorerViewModel;
```

**Pattern:** TodoViewModel sollte ähnlich in TaskDetailViewModel kompositoriert werden.

## UI-Komponenten (zu erstellen)

### `TodoListView.xaml` (UserControl oder Integration in TaskDetailView)
Sollte enthalten:
- TextBox für neue Todo-Eingabe
- ItemsControl/ListBox für Todo-Liste
- Jedes Item mit:
  - CheckBox für Erledigungsstatus
  - Text der Beschreibung
  - Delete-Button
  - Visuelle Unterscheidung zwischen erledigt/offen

### Ribbon-Erweiterung (in TaskDetailView.xaml)
- Badge/Label mit OffeneTodoCount
- Bindung zu `OffeneTodoCount` Property
- Visuelle Hervorhebung bei offenen Todos
- Beispiel: "Beenden (⚠ 3 offene Todos)"
