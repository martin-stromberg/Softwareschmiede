← [Zurück zur Übersicht](index.md)

# Projekte — Architektur und Komponentenübersicht

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|-----------|-----|-------|
| `ProjectDetailView.xaml` | UserControl (XAML) | Benutzeroberfläche mit Ribbon-Menü, Projekt-Kachel, Aufgaben-Kachel und Filter-Overlay |
| `ProjectDetailView.xaml.cs` | Code-behind | Event-Handler für Aufgaben-Doppelklick |
| `ProjectDetailViewModel` | ViewModel (C#) | Logik für Laden, Speichern, Löschen, Repository-Management und Filter |
| `RepositoryAssignDialog.xaml` | Window (XAML) | Modaler Dialog zur Repository-Auswahl |
| `RepositoryAssignDialog.xaml.cs` | Code-behind | Dialog-Steuerung und Lifecycle-Management |
| `RepositoryAssignViewModel` | ViewModel (C#) | Logik für Laden der verfügbaren Repositories und Auswahl |
| `RibbonGroup` | Custom Control | Visuelle Gruppe von Ribbon-Buttons |
| `RibbonLargeButton` | Custom Control | Großer Ribbon-Button mit Icon und Text |
| `RibbonSmallButton` | Custom Control | Kleiner Ribbon-Button mit Icon und Text |
| `ProjectService` | Service (C#) | Geschäftslogik für Projekt-CRUD und Repository-Management |
| `WorkItemService` | Service (C#) | Geschäftslogik für Aufgaben-Verwaltung |
| `TaskDetailViewModel` | ViewModel (C#) | Verwaltung der Aufgabendetail-Ansicht (inline eingebunden) |

## Abhängigkeiten

```
ProjectDetailView (XAML)
├── ProjectDetailViewModel (Binding)
│   ├── ProjectService (Dependency Injection)
│   │   └── SoftwareschmiedeDbContext
│   ├── WorkItemService (Dependency Injection)
│   │   └── SoftwareschmiedeDbContext
│   ├── IServiceProvider (DI-Container)
│   │   └── TaskDetailViewModel (On-demand instantiation)
│   │   └── RepositoryAssignViewModel (On-demand instantiation)
│   └── ILogger<ProjectDetailViewModel>
│
├── RepositoryAssignDialog (via RepositoryDialogOeffnenFunc)
│   └── RepositoryAssignViewModel
│       ├── ProjectService
│       └── ILogger<RepositoryAssignViewModel>
│
├── TaskDetailView (via SelectedTaskViewModel binding)
│   └── TaskDetailViewModel
│
└── Custom Controls (Ribbon UI)
    ├── RibbonGroup
    ├── RibbonLargeButton
    └── RibbonSmallButton
```

## UI-Struktur

Die Projektdetailansicht ist in drei visuelle Bereiche unterteilt:

1. **Ribbon-Menü** (GridRow 0)
   - StackPanel mit horizontaler Anordnung
   - Vier RibbonGroup-Container mit jeweils Buttons
   - Buttons sind Command-gebunden

2. **ScrollViewer mit Inhalts-Kacheln** (GridRow 1)
   - **Projekt-Kachel:** TextBlock (Icon) + TextBox (Name) + TextBox (Beschreibung)
   - **Aufgaben-Kachel:** ListBox mit DataTemplate (Titel + Status)
   - **Filter-Overlay:** Border mit RadioButtons (Alle/Aktiv/Archiviert)
   - **Aufgabendetail-Inline:** TaskDetailView wenn SelectedTaskViewModel nicht null

## Datenfluss

**Projekt laden:**
```
BenutzernavigationZuProjekt
    ↓
ProjectListViewModel setzt ProjectDetailViewModel.ProjectId
    ↓
PropertyChanged löst LoadAsync aus
    ↓
ProjectService.GetDetailAsync() + WorkItemService.GetByProjektAsync()
    ↓
Projekt, Aufgaben, ProjektName, ProjektBeschreibung, SelectedRepository werden gesetzt
    ↓
UI aktualisiert via Data Binding
```

**Projekt speichern:**
```
Benutzer bearbeitet Name/Beschreibung in TextBoxen
    ↓
ProjektName/ProjektBeschreibung Properties ändern sich (UpdateSourceTrigger=PropertyChanged)
    ↓
Benutzer klickt "Speichern"-Button
    ↓
SaveCommand ruft ProjektSaveAsync auf
    ↓
Prüfung: IsNeuanlage (leere ID)?
    ├─ Ja: ProjectService.CreateAsync() → neue ID → ZurueckAction
    └─ Nein: ProjectService.UpdateAsync() → LoadAsync() → UI aktualisiert
    ↓
ProjektListeAktualisierenCallback wird aufgerufen
```

**Repository zuweisen:**
```
Benutzer klickt "Zuweisen"-Button
    ↓
RepositoryZuweisenCommand ruft RepositoryZuweisenAsync auf
    ↓
RepositoryAssignViewModel wird instantiiert und LoadAsync() aufgerufen
    ↓
ProjectService.GetAllRepositoriesAsync() lädt Repositories in VerfuegbareRepositories
    ↓
RepositoryDialogOeffnenFunc öffnet RepositoryAssignDialog modal
    ↓
Benutzer wählt Repository aus (SelectedRepository wird gesetzt)
    ↓
Benutzer klickt "Zuweisen" oder "Abbrechen"
    ├─ Zuweisen: Dialog gibt true zurück → ProjectService.AddRepositoryAsync() → LoadAsync()
    └─ Abbrechen: Dialog gibt false zurück → keine Änderung
    ↓
UI aktualisiert mit neuen Repositories
```

## Converter

Folgende WPF-Value-Converter werden in der Ansicht verwendet:

- **`BoolToVisibilityConverter`** — Zeigt Loading-Symbol und Filter-Overlay wenn entsprechende Properties true sind
- **`InverseBoolToVisibilityConverter`** — Versteckt Aufgaben-Kachel im Anlage-Modus (IsNeuanlage ist true)
- **`NullOrEmptyToVisibilityConverter`** — Zeigt TaskDetailView nur wenn SelectedTaskViewModel nicht null ist
- **`EnumToBoolConverter`** — Bindet WorkItemFilterType Enum-Werte an RadioButton-Selection

## Disposa​l und Cleanup

- `ProjectDetailViewModel` implementiert `IDisposable`
- `_loadCts` (CancellationTokenSource) wird bei jedem neuen Laden abgebrochen und disposed
- Wenn `SelectedTaskViewModel` eine neue Instanz wird, wird die alte disposed (falls `IDisposable`)
- Bei Disposal des ViewModels: `_loadCts` wird disposed und `_selectedTaskViewModel` wird disposed

Dies verhindert Memory Leaks und stellt sicher, dass Background-Tasks nicht auf disposed ViewModels zugreifen.
