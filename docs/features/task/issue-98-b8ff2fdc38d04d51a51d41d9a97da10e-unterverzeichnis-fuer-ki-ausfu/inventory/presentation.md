# Presentation Layer

## `RepositoryAssignViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`

| Property | Typ | Sichtbarkeit | Status |
|----------|-----|-------------|--------|
| `VerfuegbareRepositories` | `ObservableCollection<AvailableRepository>` | Public | Vorhanden — enthält externe Repositories |
| `SelectedRepository` | `AvailableRepository?` | Public | Vorhanden — Setter ruft **noch nicht** `LoadDirectoryStructureAsync` auf |
| `AvailableScmPlugins` | `ObservableCollection<IGitPlugin>` | Public | Vorhanden |
| `SelectedScmPlugin` | `IGitPlugin?` | Public | Vorhanden — Setter ruft `OnSelectedScmPluginChanged` auf |
| `IsLoading` | `bool` | Public | Vorhanden — wird bei Repository-Laden gesetzt |
| `HasScmPlugins` | `bool` | Public | Vorhanden |
| `BestaetigenCommand` | `ICommand` | Public | Vorhanden |
| `AbbrechenCommand` | `ICommand` | Public | Vorhanden |
| `CurrentReloadTask` | `Task?` | Internal | Vorhanden (nur für Tests) |
| `AvailableWorkingDirectories` | `ObservableCollection<string>` | **FEHLT** — Read-only Collection der verfügbaren Verzeichnisse |
| `SelectedWorkingDirectory` | `string?` | **FEHLT** — Speichert vom Benutzer ausgewählten relativen Pfad (Default: null für Root) |
| `IsLoadingDirectoryStructure` | `bool` | **FEHLT** — Zeigt Lade-Status der Verzeichnisstruktur |
| `CurrentLoadDirectoryStructureTask` | `Task?` | **FEHLT** — Speichert laufenden Task für Tests |

### Erforderliche Methoden

| Methode | Status |
|---------|--------|
| `LoadDirectoryStructureAsync(CancellationToken ct)` | **FEHLT** — wird aufgerufen beim `SelectedRepository`-Wechsel |
| `OnSelectedRepositoryChanged()` | **FEHLT** oder zu erweitern — sollte Directory-Struktur laden und `SelectedWorkingDirectory` auf null setzen |

### Private Felder

- `_availableWorkingDirectories` — **FEHLT**
- `_selectedWorkingDirectory` — **FEHLT**
- `_isLoadingDirectoryStructure` — **FEHLT**
- `_dirStructureCts` — **FEHLT** — CancellationTokenSource für Directory-Lade-Task
- `_directoryStructureService` — **FEHLT** — Referenz zu `DirectoryStructureBrowserService`

## `RepositoryAssignDialog.xaml`
Datei: `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml`

### Aktuelle Struktur (Zeilen 1–104)

| Bereich | Status |
|--------|--------|
| Titel ("Repository zuweisen") | Vorhanden |
| Plugin-Auswahl (ComboBox) | Vorhanden |
| Repository-Liste (ListBox) | Vorhanden — mit Name und URL |
| Hilfe-Panel (bei fehlenden Plugins) | Vorhanden |
| Buttons (Zuweisen/Abbrechen) | Vorhanden |
| **Arbeitsverzeichnis-Auswahl** | **FEHLT** |

### Erforderliche UI-Erweiterungen

Nach der Repository-Liste (nach Grid.Row="2") ist eine neue Sektion erforderlich:

1. **Label:** "Arbeitsverzeichnis im Repository"
2. **ComboBox** oder **ListBox:** Mit `ItemsSource="{Binding AvailableWorkingDirectories}"` und `SelectedItem="{Binding SelectedWorkingDirectory}"`
3. **ProgressRing/Spinner:** Gebunden an `IsLoadingDirectoryStructure` (LoadingIndicator während Struktur-Abruf)
4. **Info-Text:** "Hinweis: '.' bedeutet Wurzelverzeichnis des Repositories"
5. **Visuelle Hinweise:** ComboBox deaktiviert während Laden

### Grid-Layout-Anpassung

Die aktuelle RowDefinition-Struktur (Zeilen 11–16):
```xaml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />      <!-- Titel -->
    <RowDefinition Height="Auto" />      <!-- Plugin-Auswahl -->
    <RowDefinition Height="*" />         <!-- Repository-Liste -->
    <RowDefinition Height="Auto" />      <!-- Buttons -->
</Grid.RowDefinitions>
```

Muss angepasst werden zu:
```xaml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />      <!-- Titel -->
    <RowDefinition Height="Auto" />      <!-- Plugin-Auswahl -->
    <RowDefinition Height="*" />         <!-- Repository-Liste -->
    <RowDefinition Height="Auto" />      <!-- Arbeitsverzeichnis-Auswahl -->
    <RowDefinition Height="Auto" />      <!-- Buttons -->
</Grid.RowDefinitions>
```

## `RepositoryAssignDialog.xaml.cs`
Datei: `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs`

Status: **Nicht gelesen** — Üblicherweise nur Code-Behind für Dialog-Management (Schließen, Navigation).

### Erforderliche Erweiterungen (wenn nötig)

- Dependency Injection von `DirectoryStructureBrowserService` in den ViewModel-Konstruktor
