# ViewModels

Analyse der Presentation-Model-Klassen bezüglich der Anforderung zur Integration von Datei-Aktionsbuttons in das Ribbon-Menü.

## `TaskDetailViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

**Zusammenfassung:** Zentrale ViewModel-Klasse für die Aufgabendetailansicht. Verwaltet Status, Protokoll, CLI-Prozessstart, Fenstereinbettung und Sichtbarkeit von Panels.

### Bestehende Properties für Sichtbarkeit

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `ShowEditPanel` | `bool` (Read-only) | `true` wenn Status == Neu, sonst `false` |
| `ShowCliPanel` | `bool` (Read-only) | `true` wenn Status ∈ {Gestartet, Wartend}, sonst `false` |
| `ShowDiffPanel` | `bool` (Read-only) | `true` wenn Status == Beendet, sonst `false` |
| `ShowFileExplorerPanel` | `bool` (Read-only) | `true` wenn Aufgabe.LokalerKlonPfad gesetzt ist und Verzeichnis existiert. Wird beim Setzen von `Aufgabe` einmalig ermittelt und gecacht. |

### Bestehende Commands für Dateiexplorer-Integration

| Command | Sichtbarkeit | Beschreibung |
|---|---|---|
| `DateiViewCommand` | Via `FileExplorerViewModel` | Wechselt zur Dateiexplorer-Ansicht, CanExecute prüft `ShowFileExplorerPanel` |

### Abhängigkeiten

**Injizierte Services im Constructor:**
- `FileExplorerViewModel _fileExplorerViewModel` - Presentation Model des Dateiexplorers
- Weitere 14 Services (AufgabeService, ProtokollService, KiAusfuehrungsService, etc.)

**Property-Zugriff:**
- `public FileExplorerViewModel FileExplorer => _fileExplorerViewModel;` - Öffentlicher Zugriff auf den Dateiexplorer

### Events

- `PseudoConsoleSessionGestartet` - Wird gefeuert, wenn neue PseudoConsoleSession gestartet wurde
- `CliGestoppt` - Wird gefeuert, wenn CLI-Prozess beendet wurde
- `PromptVorlageGesendet` - Wird gefeuert nach erfolgreichem Promptvorlagen-Versand

### Relevante Methoden für neue Funktionalität

- `SetProperty(ref field, value)` - MVVM Property-Change-Signalisierung
- `OnPropertyChanged(propertyName)` - Manuelles Property-Changed-Signalisieren
- `_dispatcherInvoke(action)` - UI-Thread-Marshalling

---

## `FileExplorerViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs`

**Zusammenfassung:** Presentation Model des Dateiexplorers. Verwaltet Baum, Commits, Auswahl, Dateiinhalt, Diff-Zeilen und Anzeigemodus (Standard/Vergleich).

### Bestehende Commands (relevant für Ribbon-Integration)

| Command | Signatur | Beschreibung | CanExecute-Bedingung |
|---|---|---|---|
| `StandardAnsichtCommand` | `AsyncRelayCommand` | Wechselt in Standardmodus und lädt Arbeitsbaum neu. | Immer ausführbar |
| `VergleichCommand` | `AsyncRelayCommand` | Wechselt in Vergleichsmodus und lädt Branch-Commits. | Immer ausführbar |
| `AktualisierenCommand` | `AsyncRelayCommand` | Lädt aktuellen Modus (Baum bzw. Commits) neu. | Immer ausführbar |
| `DateiMitStandardanwendungOeffnenCommand` | `RelayCommand` | Öffnet aktuell ausgewählte Datei mit Standardanwendung des OS. | `_ausgewaehlterKnoten is { IsDirectory: false, IsDeleted: false, CommitSha: null }` |
| `NaechsteAenderungCommand` | `RelayCommand` | Springt im Diff zur nächsten Änderung. | `DiffZeilen.Count > 0` |
| `VorherigeAenderungCommand` | `RelayCommand` | Springt im Diff zur vorherigen Änderung. | `DiffZeilen.Count > 0` |

### Bestehende Properties

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Wurzelknoten` | `ObservableCollection<WorkspaceFileNode>` | Wurzelknoten des Arbeitsbaums im Standardmodus |
| `CommitGruppen` | `ObservableCollection<BranchCommit>` | Commits des aktuellen Branches im Vergleichsmodus |
| `DiffZeilen` | `ObservableCollection<TextDiffLine>` | Zeilen des aktuell angezeigten Diffs im Vergleichsmodus |
| `AusgewaehlterKnoten` | `WorkspaceFileNode?` | Aktuell im Baum ausgewählter Knoten (setter lädt Dateivorschau) |
| `DateiInhalt` | `string?` | Inhalt bzw. Hinweistext der aktuell ausgewählten Datei |
| `AktuellerModus` | `DateibrowserAnsichtsmodus` | Aktuell gewählter Anzeigemodus (Standard oder Vergleich) |
| `ZeigtDiffAnsicht` | `bool` (Read-only) | `true` wenn DiffZeilen.Count > 0 |

### Abhängigkeiten

**Injizierte Services:**
- `IGitWorkspaceBrowserService _gitWorkspaceBrowserService` - Lädt Repository-Status, Baum, Dateivorschauen
- `ITextDiffService _textDiffService` - Berechnet Diffs

### Methoden für Externe Nutzung

- `InitialisierenAsync(string? repositoryPath, CancellationToken ct)` - Setzt Repository-Verzeichnis, lädt Arbeitsbaum
- `CommitAufklappenAsync(BranchCommit commit, CancellationToken ct)` - Lädt geänderte Dateien eines Commits nach

### Private Methoden (für UI via Commands aufgerufen)

- `DateiMitStandardanwendungOeffnen()` - Öffnet Datei mit Standardanwendung (implementiert via `Process.Start()`)

---

## Zusammenfassung für Ribbon-Integration

**Bestehende Komponenten:**
- TaskDetailViewModel besitzt bereits Property `ShowFileExplorerPanel` zum Kontrollen der Sichtbarkeit
- FileExplorerViewModel besitzt bereits alle notwendigen Commands (`StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand`, `DateiMitStandardanwendungOeffnenCommand`)
- Commands sind über die UI-Bindung in `TaskDetailView.xaml` einsatzbereit

**Fehlende Komponenten:**
- `WorkspaceExplorerService` / `WorkdirService` - zum Öffnen des Arbeitsverzeichnisses im OS-Explorer
- `IdeService` / `VisualStudioService` - zum Öffnen von Visual Studio mit der Solution-Datei
- Commands in TaskDetailViewModel für die neuen Aktionen
- Properties in TaskDetailViewModel für Sichtbarkeit der neuen Ribbon-Gruppen
- Property `SolutionFileExists` zur Steuerung des IDE-Open-Buttons
