# UI-Komponenten

## `FileExplorerView`
Datei (XAML): `src/Softwareschmiede.App/Views/FileExplorerView.xaml`  
Datei (CodeBehind): `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs`

UserControl mit zwei TreeViews (Standard- und Vergleichsmodus) plus Dateiinhalt/Diff-Viewer auf der rechten Seite.

### XAML-Struktur

#### HierarchicalDataTemplate für `WorkspaceFileNode`
```xml
<HierarchicalDataTemplate DataType="{x:Type vo:WorkspaceFileNode}"
                           ItemsSource="{Binding Children}">
```
- Bindet `Children`-Collection an TreeView-Items
- Rendert Name, Icon und Status-Icon für jeden Knoten
- **Aktueller Zustand:** Statische Hierarchie — alle Kinder werden sofort gerendert, wenn in Children geladen

#### StandardBaum (TreeView für Arbeitsbaum)
- `x:Name="StandardBaum"`
- Bindet an `Wurzelknoten` des ViewModels
- `SelectedItemChanged="OnBaumSelectedItemChanged"` — ruft ViewModel-Setter `AusgewaehlterKnoten` auf
- **Fehlend:** `TreeViewItem.Expanded`-Event-Handler für Lazy-Loading
- `ItemContainerStyle="{StaticResource FileExplorerBaumItemContainerStyle}"` — setzt `HorizontalContentAlignment="Stretch"`

#### VergleichBaum (TreeView für Commits)
- `x:Name="VergleichBaum"`
- Bindet an `CommitGruppen` des ViewModels
- `TreeViewItem.Expanded="OnCommitKnotenExpanded"` — **triggert ViewModel.CommitAufklappenAsync!** Dies ist das Vorbild für StandardBaum
- Hat eigene `HierarchicalDataTemplate` für `BranchCommit` mit Loading-Spinner und Error-Message-Anzeige

#### Inhaltsbereich (rechts)
- Zeigt Dateiinhalt oder Diff je nach `ZeigtDiffAnsicht`-Property
- TextBlock mit Monospace-Font für Datei-Inhalt
- DiffViewer-Control für Diff-Anzeige
- Navigation-Buttons (Previous/Next) für Diff-Änderungen

### CodeBehind

#### Event-Handler

| Handler | Quell-Event | Zweck |
|---------|------------|-------|
| `OnDataContextChanged` | DataContextChanged | Abonniert/Unabonniert `_subscribedViewModel.DiffZeileFokussiert`-Event (für Diff-Navigation) |
| `OnUnloaded` | Unloaded | Räumt ViewModel-Events auf |
| `OnDiffZeileFokussiert` | Event von `_subscribedViewModel.DiffZeileFokussiert` | Ruft `DiffViewerControl.ScrollToIndex(index)` auf |
| `OnBaumSelectedItemChanged` | TreeView.SelectedItemChanged (beide Bäume) | Setzt `vm.AusgewaehlterKnoten = e.NewValue as WorkspaceFileNode` |
| `OnCommitKnotenExpanded` | TreeViewItem.Expanded (nur VergleichsBaum!) | Ruft `vm.CommitAufklappenAsync(commit)` auf, wenn TreeViewItem mit `BranchCommit` aufgeklappt wird |

### Fehlende Implementierungen für Lazy-Loading

1. **StandardBaum benötigt `TreeViewItem.Expanded`-Event-Handler**
   - Sollte wie `OnCommitKnotenExpanded` funktionieren, aber für `WorkspaceFileNode`
   - Logik: Wenn `node.IsDirectory && !node.ChildrenLoaded`, dann `vm.LadeKinderAsync(node)` aufrufen

2. **Optional: TreeViewItem.Collapsed-Event-Handler** (für Cleanup)
   - Sollte bei Feature-Flag-Aktivierung `vm.BeraeumeKnoten(node)` aufrufen
   - Könnte Groß-Enkel-Knoten bei Zuklappen entfernen

### Dependencies (Code-Behind)

- `ILogger<FileExplorerView>` — über Dependency Injection via `App.Services`
- `FileExplorerViewModel` — als DataContext
- `DiffViewerControl` — lokales Control für Diff-Rendering (mit `ScrollToIndex`-Methode)
