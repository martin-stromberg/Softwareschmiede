# Anforderung

## Fachliche Zusammenfassung

Der Verzeichnisbaum in der Aufgabenansicht (Standardmodus des `FileExplorer`) lädt derzeit alle Verzeichnisse und Dateien auf einmal. Dies führt bei großen Repositories zu Performance-Problemen. Die Anforderung ist, auf ein **Lazy-Loading-Modell mit progressiver Tiefenentwicklung** umzusteigen: Die obersten zwei Ebenen (Wurzel + eine Ebene tiefer) werden beim Initialisieren geladen. Beim Aufklappen eines Verzeichnisknotens wird die nächste Ebene unterhalb dieses Knotens nachgeladen. Beim Zuklappen kann eine optionale Bereinigung stattfinden, so dass pro Knoten maximal eine Ebene mehr Kinder geladen ist, als sichtbar sind (z. B. beim Zuklappen eines Ebene-1-Knotens bleiben die Ebene-2-Knoten geladen, aber Ebene-3-Knoten werden entfernt).

## Betroffene Klassen und Komponenten

### ViewModel-Klassen
- `FileExplorerViewModel` — Presentation Model des Explorers
  - Methode: `StandardAnsichtAsync()` (Zeile ~47–58 in ablauf-technisch.md) — lädt aktuell alle Knoten auf einmal
  - Property: `AktuellerModus` (Modus-Steuerung) — sollte zwischen zwei Lademodi unterscheiden
  - Property: `AusgewaehlterKnoten` — Knoten-Auswahl für Dateiinhalt
  - Command: `AktualisierenCommand` — löst komplettes Neuladen aus (bleibt bestehen)

### Service-Interfaces und Implementierungen
- `IGitWorkspaceBrowserService` — Abstrahiert Git-Backend und Arbeitsbaum-Operationen
  - Methode: `LoadWorkingTreeAsync(repositoryPath)` (Zeile ~50–56) — **muss adaptiert werden** auf `LoadWorkingTreeAsync(repositoryPath, maxDepth)`
  - **Neue Methode:** `LoadWorkingTreeSubtreeAsync(repositoryPath, parentPath, depth)` — lädt eine einzelne Ebene unterhalb eines Knotens
  - Implementierung: `GitWorkspaceBrowserService` — Directory-Walk-Implementierung

### Datenmodelle
- `WorkspaceFileNode` — Baum-Knoten mit Hierarchie
  - Property: `Name` — Datei-/Verzeichnisname
  - Property: `RelativePath` — relativer Pfad vom Repository-Wurzel
  - Property: `IsDirectory` — Flag für Verzeichnis-Knoten
  - Property: `Children` — Collection untergeordneter Knoten (`IList<WorkspaceFileNode>`)
  - **Neue Property:** `IsChildrenLoaded` — Flag, ob die direkte Kinder-Ebene geladen wurde (ähnlich `BranchCommit.ChildrenLoaded`)
  - **Neue Property:** `Depth` — Ebene des Knotens relativ zur Wurzel (0 = Wurzel, 1 = direkte Kinder, …)

### UI-Komponenten
- `FileExplorerView` — UserControl mit TreeView
  - `TreeView` mit `HierarchicalDataTemplate` — bindet `Children` an sichtbare Knoten
  - Command: `AktualisierenCommand`, `StandardCommand`, `VergleichCommand` — bestehende Buttons

### Tests
- `IGitWorkspaceBrowserServiceTests` (oder verwandter Test-Fixture) — Tests für Service-Adapter
- `FileExplorerViewModelTests` (oder verwandter Test-Fixture) — Tests für Lazy-Loading-Logik

## Implementierungsansatz

### 1. Datenmodell erweitern
- `WorkspaceFileNode`: Property `IsChildrenLoaded : bool` hinzufügen (default `false`)
- `WorkspaceFileNode`: Property `Depth : int` hinzufügen (berechnet beim Aufbau oder als Parameter)

### 2. Service-Interface erweitern
- `IGitWorkspaceBrowserService.LoadWorkingTreeAsync()`:
  - Signatur: `Task<IReadOnlyList<WorkspaceFileNode>> LoadWorkingTreeAsync(string repositoryPath, int maxInitialDepth = 2, CancellationToken ct = default)`
  - Lädt Verzeichnisbaum mit Beschränkung auf `maxInitialDepth` Ebenen
  - Für Knoten der Zieltiefe: setze `IsChildrenLoaded = false` (Indikatoren für Lazy-Loading)
  
- **Neue Methode:** `IGitWorkspaceBrowserService.LoadSubtreeAsync()`
  - Signatur: `Task<IReadOnlyList<WorkspaceFileNode>> LoadSubtreeAsync(string repositoryPath, string parentPath, int depth, CancellationToken ct = default)`
  - Lädt Verzeichnis-Kinder unterhalb `parentPath` auf Ebene `depth`
  - Rückgabe: flache Liste neuer `WorkspaceFileNode` unterhalb von `parentPath`

### 3. TreeView-Interaktion
- **Attach-Behavior oder Code-Behind:** TreeView-Item-Expansion-Handler
  - Event: `TreeViewItem.Expanded` (oder XAML-Attached-Behavior)
  - Logik beim Aufklappen:
    1. Prüfe: Ist der expandierte Knoten ein Verzeichnis (`IsDirectory == true`)?
    2. Prüfe: Wurde die Kinder-Ebene bereits geladen (`IsChildrenLoaded == true`)?
    3. Falls `false` und `false`:
       - Rufe `IGitWorkspaceBrowserService.LoadSubtreeAsync(repositoryPath, knoten.RelativePath, knoten.Depth + 1)` auf
       - Erhalte neue Knoten-Liste
       - Setze `knoten.IsChildrenLoaded = true`
       - Addiere neue Knoten zu `knoten.Children` (oder ersetze ggf. Platzhalter)
  - **Thread-Sicherheit:** Async-Aufruf, mit CancellationToken bei Navigationsabbruch

- **Zuklapp-Bereinigung (optional):**
  - Event: `TreeViewItem.Collapsed` (oder ähnlich)
  - Logik: Falls Cleanup aktiviert (z. B. per Feature-Flag oder Konfiguration):
    - Entferne alle Groß-Enkel-Knoten (Tiefe > Knoten.Depth + 2)
    - Setze `IsChildrenLoaded = true` (Ebene + 1 bleibt, Ebene + 2 wird gelöscht)
    - Dies ist eine **optionale Optimierung**; Fallback: keine Bereinigung, nur Lazy-Loading beim Aufklappen

### 4. ViewModel-Anpassungen
- `FileExplorerViewModel.StandardAnsichtAsync()`:
  - Rufe `IGitWorkspaceBrowserService.LoadWorkingTreeAsync(path, maxInitialDepth: 2)` auf statt bisherige unbegrenzte Version
  - Setze `Wurzelknoten ← geladene Knoten`
  - Für alle Knoten auf Tiefe 2: Markiere mit `IsChildrenLoaded = false` (haben noch keine Kinder geladen)

- `FileExplorerViewModel`:
  - **Neue Methode:** `LadeKinderAsync(knoten)` — wird von UI aufgerufen beim Expand
    - Validierung: `knoten.IsDirectory && !knoten.IsChildrenLoaded`
    - Rufe `IGitWorkspaceBrowserService.LoadSubtreeAsync(...)` auf
    - Update: `knoten.Children.AddRange(neue Knoten)`, `knoten.IsChildrenLoaded = true`
  
  - **Optionale Methode:** `BeraeumeKnoten(knoten)` — wird von UI aufgerufen beim Collapse (falls Cleanup aktiviert)
    - Entferne Kinder mit `Kind.Depth > knoten.Depth + 1` (Groß-Enkel)
    - Ggf. rekursiv in verbleibenden Kindern

### 5. Fehlerbehandlung
- Beim Laden der initialen Ebenen:
  - Fallback auf leere Liste (wie bisherig)
- Beim Lazy-Laden (Aufklappen):
  - Exception wird geloggt
  - UI zeigt optional Fehler-Hinweis bei dem Knoten (z. B. rotes Symbol oder disabelt den Expand-Button)
  - Knoten bleibt in `IsChildrenLoaded = false`, Neuladen möglich

### 6. Performance-Überlegung
- Initiales Laden: nur 2 Ebenen statt alle (deutlich schneller für große Repos)
- Lazy-Laden: async, mit Fortschritts-UI optional
- Bereinigung: optional, spart Speicher bei sehr tiefen Navigationen

## Konfiguration

- **Optional:** Feature-Flag oder Einstellung für Cleanup-Verhalten:
  - `EnableDirtyTreeCleanup : bool` (default: `false` für MVP, später optimieren)
  - Kann in `Einstellungen` oder als Konstante in `FileExplorerViewModel` gespeichert werden

- **Konstante:** `InitialLoadDepth : int = 2` (definiert die zwei Ebenen beim Start)

## Offene Fragen

1. Soll die Zuklapp-Bereinigung im MVP implementiert sein oder erst nach MVP als Optimierung hinzugefügt werden?
2. Wie wird die optionale Fortschritts-Anzeige beim Lazy-Laden der Kinder realisiert (Spinner, Hinweis, oder stumm)?
3. Gibt es eine maximale Tiefe, die geladen werden darf, um Endlosschleifen oder zirkuläre Symlinks zu verhindern?
4. Sollen bestehende Speicherorte des geladenen Baums (z. B. im ViewModel gecachte Bäume) invalidiert werden, oder wird bei jedem `AktualisierenCommand` ein vollständiger Neustart durchgeführt?
5. Sollte das Vergleichsmodus-Lazy-Loading (aktuell `BranchCommit.ChildrenLoaded`) analog angepasst werden, oder bleibt es unverändert?
