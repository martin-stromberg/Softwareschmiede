← [Zurück zur Übersicht](index.md)

# Dateiexplorer — Architektur

## Beteiligte Komponenten

| Komponente | Schicht | Rolle |
|-----------|--------|-------|
| `TaskDetailViewModel` | Presentation (App) | Übergeordnetes ViewModel; delegiert Explorer-Logik und verwaltet Sichtbarkeitszustand |
| `FileExplorerViewModel` | Presentation (App) | Presentation Model des Explorers; orchestriert alle Explorer-Funktionen (Baum, Commits, Diff, Modus) |
| `FileExplorerView` | UI (App) | XAML-UserControl; Split-View mit Mode-Buttons, TreeView und Inhalts-/Diff-Anzeige |
| `DiffViewer` | UI (App) | Wiederverwendbarer UserControl für zeilenweises Diff-Rendering mit Farbkodierung |
| `DiffLineStatusToBrushConverter` | UI (App) | IValueConverter für Umwandlung von DiffLineStatus in Hintergrund-Brush |
| `DateibrowserAnsichtsmodus` | Presentation (App) | Enum für Modus-Steuerung (Standard vs. Vergleich) |
| `IGitWorkspaceBrowserService` | Application | Interface für Git-Backend-Operationen (Arbeitsbaum, Commits, Dateivorschauen) |
| `GitWorkspaceBrowserService` | Application | Konkrete Implementierung; nutzt `ICliRunner` für Git-Aufrufe und File-System-APIs |
| `ITextDiffService` | Application | Interface für zeilenweisen Präsentations-Diff |
| `TextDiffService` | Application | Konkrete Implementierung; berechnet Diff ohne DB-Abhängigkeit |
| `WorkspaceFileNode` | Domain | Value Object für Baum-Knoten (Directory-Walk und Commit-Files) |
| `TextDiffLine`, `InlineDiffSegment`, `FileTextDiff` | Domain | Value Objects für Diff-Daten |
| `WorkspaceSnapshot`, `BranchCommit`, `FilePreview` | Domain | Value Objects für Git-Backend-Daten |
| `DiffLineStatus` | Domain | Enum für Zeilen-Status (Added/Removed/Modified/Context) |

## Abhängigkeiten und Kommunikationsmuster

```mermaid
graph TB
    subgraph UI[UI-Schicht App]
        TDVM["TaskDetailViewModel<br/>(Übergeordnet)"]
        FEVM["FileExplorerViewModel<br/>(Presentation Model)"]
        FEV["FileExplorerView<br/>(XAML)"]
        DV["DiffViewer<br/>(XAML)"]
        CONV["DiffLineStatusToBrushConverter"]
    end

    subgraph APP[Application-Schicht]
        GWBS["IGitWorkspaceBrowserService"]
        TDS["ITextDiffService"]
    end

    subgraph DOMAIN[Domain-Schicht]
        NODE["WorkspaceFileNode"]
        DIFF["TextDiffLine<br/>InlineDiffSegment<br/>FileTextDiff"]
        SNAP["WorkspaceSnapshot<br/>BranchCommit<br/>FilePreview"]
        ENUM["DiffLineStatus<br/>DateibrowserAnsichtsmodus"]
    end

    TDVM -->|komponiert| FEVM
    TDVM -->|"Sichtbarkeit<br/>Register-Toggle"| FEV
    
    FEVM -->|"Standard/Vergleich<br/>Laden+Cachen"| GWBS
    FEVM -->|"Diff-Berechnung"| TDS
    FEVM -->|"DataContext"| FEV
    
    FEV -->|"TreeView<br/>Binding"| NODE
    FEV -->|"Diff-Anzeige<br/>Binding"| DIFF
    FEV -->|"Mode-Switch<br/>Commands"| FEVM
    
    DV -->|"Zeilen-Rendering"| DIFF
    DV -->|"Farb-Konvertierung"| CONV
    CONV -->|"Status→Farbe"| ENUM
    
    GWBS -->|"Directory-Walk<br/>Git diff/show"| NODE
    GWBS -->|"Vorschau-Daten"| SNAP
    
    TDS -->|"Input"| SNAP
    TDS -->|"Output"| DIFF
    
    NODE -->|"Entity"| SNAP
    DIFF -->|"nutzt"| ENUM
```

## Schichtenarchitektur

### UI-Schicht (App)
- **TaskDetailView** stellt das neue Register bereit
- **FileExplorerView** ist spezialisiert auf die Dateiexplorer-Oberfläche (Mode-Buttons, TreeView, Splitter)
- **DiffViewer** ist eine wiederverwendbare Komponente für Diff-Rendering
- Alle ViewModels nutzen Property-Binding für reaktive Aktualisierungen

### Presentation-Schicht (App.ViewModels)
- **FileExplorerViewModel** ist das Presentation Model für alle Explorer-Funktionen
- Verwaltet Observable Collections (`Wurzelknoten`, `CommitGruppen`, `DiffZeilen`)
- Commands für Benutzer-Interaktionen (`StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand`)
- Delegation an Application-Layer Services, nicht direkte Datenbankzugriffe

### Application-Schicht
- **IGitWorkspaceBrowserService**: Abstrahiert Git-Backend-Operationen
  - `LoadWorkingTreeAsync()` — Directory-Walk
  - `LoadSnapshotAsync()` — Commit-Listing
  - `LoadPreviewAsync()` / `LoadCommitPreviewAsync()` — Datei-Inhalt
  - `LoadCommitFilesAsync()` — Commit-spezifische Dateien
- **ITextDiffService**: Abstrahiert Zeilendiff-Erzeugung
  - `BuildDiff()` — aus Alt/Neu-Inhalt zu `FileTextDiff`

### Domain-Schicht
- **Value Objects** für Datenmodelle: `WorkspaceFileNode`, `TextDiffLine`, `InlineDiffSegment`, `FileTextDiff`, `BranchCommit`, `FilePreview`, etc.
- **Enums** für Typsicherheit: `DiffLineStatus`, `DateibrowserAnsichtsmodus`
- Keine Geschäftslogik, reine Datenschichten

## Datenfluss

### Standardmodus — Directory-Walk

```
Benutzer öffnet Register
  ↓
TaskDetailViewModel.Aufgabe.LokalerKlonPfad
  ↓
FileExplorerViewModel.InitialisierenAsync(pfad)
  ↓
IGitWorkspaceBrowserService.LoadWorkingTreeAsync(pfad)
  ↓
  Directory.EnumerateFileSystemEntries() → File-System
  skip .git, baue WorkspaceFileNode-Baum
  ↓
Wurzelknoten: ObservableCollection<WorkspaceFileNode>
  ↓
FileExplorerView.TreeView.ItemsSource → Wurzelknoten
  ↓
HierarchicalDataTemplate rendert Baum
```

### Vergleichsmodus — Git Diff

```
Benutzer: Klick auf "Vergleich"
  ↓
FileExplorerViewModel.VergleichAsync()
  ↓
IGitWorkspaceBrowserService.LoadSnapshotAsync(pfad)
  ↓
  git diff-tree --name-status origin/HEAD..HEAD
  git log --reverse main..HEAD (robust Basisreferenz)
  parse Output → WorkspaceSnapshot { BranchCommits[] }
  ↓
CommitGruppen: ObservableCollection<BranchCommit>
  ↓
FileExplorerView.TreeView.ItemsSource → CommitGruppen
  ↓
Commit-Knoten sind collapsed (ChildrenLoaded=false)
  
Benutzer: Klick auf ▶ Commit
  ↓
CommitAufklappenAsync(commit)
  ↓
IGitWorkspaceBrowserService.LoadCommitFilesAsync(pfad, sha)
  ↓
  git show --name-status <sha> → WorkspaceFileNode[]
  commit.ChildrenLoaded=true
  ↓
TreeView zeigt Dateien des Commits
  
Benutzer: Klick auf geänderte Datei
  ↓
FileExplorerViewModel.DateiLadenAsync(knoten)
  ↓
IGitWorkspaceBrowserService.LoadCommitPreviewAsync(pfad, knoten)
  ↓
  git show <sha>:<path> (commit-Version)
  Arbeitsbaum oder HEAD (alte Version je nach ChangeType)
  → FilePreview { OriginalContent, CurrentContent }
  ↓
ITextDiffService.BuildDiff(original, current)
  ↓
  ComputeLineOperations() → Added/Removed/Modified/Context
  BuildModifiedLine() → Inline-Segmente
  → FileTextDiff { Lines[] }
  ↓
DiffZeilen: ObservableCollection<TextDiffLine>
  ↓
DiffViewer.ItemsSource → DiffZeilen
  ↓
Hintergrund-Farbe pro Zeile via DiffLineStatusToBrushConverter
Inline-Runs für geänderte Wortteile
```

## Caching und Speicherung

- **Baum im Standard-Modus**: Gecacht in `Wurzelknoten` bis `AktualisierenCommand`
- **Commits im Vergleichsmodus**: Gecacht in `CommitGruppen`; Dateien eines Commits lazy geladen (`ChildrenLoaded` Flag)
- **Datei-Inhalte**: Nicht gecacht; jede Auswahl löst `LoadPreviewAsync` aus (einfacher, aber ggf. langsam bei häufigem Wechsel)
- **Diff-Zeilen**: Gecacht in `DiffZeilen` Collection; neue Diff bei neuer Dateiauswahl

## Sicherheit

- **Path-Traversal-Schutz**: `GitWorkspaceBrowserService.CombinePath()` prüft, dass aufgelöste Pfade innerhalb des Repository-Roots bleiben
- **`.git`-Ausschluss**: Directory-Walk in `LoadWorkingTreeAsync()` skippt `.git` Verzeichnis
- **Binär-Erkennung**: `FilePreview` prüft auf Null-Bytes; große Dateien (> 1 MB) werden nicht vollständig geladen
- **Read-Only-Anzeige**: Inhalte werden nur zur Anzeige geladen, nicht zum Bearbeiten

## Performance-Charakteristiken

- **Standardmodus:** O(n) Directory-Walk mit n = Dateien im Repo; Obergrenze ~10.000 Knoten zur Rendering-Sicherheit
- **Vergleichsmodus:** O(c) für Commits, dann O(f) pro aufgeklapptem Commit mit f = Dateien im Commit
- **Diff-Berechnung:** O(n log n) für `ComputeLineOperations()` (Longest Common Subsequence-ähnlich); inline-Segmente O(m) mit m = Zeilenlänge
- **UI-Rendering:** TreeView ist virtualisiert; DiffViewer ist nicht virtualisiert (kann bei 1000+ Zeilen langsam werden)

## Zuverlässigkeit und Fehlertoleranz

- **Git-Fehler** beim Snapshot-Laden: Exception wird geloggt; `CommitGruppen` wird nicht gefüllt, Hinweis wird angezeigt
- **Nicht existierender Pfad**: `LoadWorkingTreeAsync()` liefert leere Liste; UI zeigt leeren Baum
- **Cancellation**: Alle async Operationen unterstützen `CancellationToken`; Benutzer-Umschaltung zwischen Modi bricht alte Operationen ab
- **Speicher**: Große Dateien (> 1 MB) und Binärdateien werden geschützt (kein vollständiges Laden in Speicher)
