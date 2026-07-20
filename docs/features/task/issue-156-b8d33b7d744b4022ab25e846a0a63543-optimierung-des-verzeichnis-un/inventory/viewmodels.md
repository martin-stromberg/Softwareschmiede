# ViewModel

## `FileExplorerViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs`

Sealed Presentation Model des Dateiexplorers. Verwaltet Baum, Commits, Auswahl, Dateiinhalt, Diff-Zeilen und Modus.

### Öffentliche Properties

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `Wurzelknoten` | `ObservableCollection<WorkspaceFileNode>` | Wurzelknoten des Arbeitsbaums im Standardmodus (read-only Collection, befüllt von `LadeArbeitsbaumAsync`) |
| `CommitGruppen` | `ObservableCollection<BranchCommit>` | Commits des aktuellen Branches im Vergleichsmodus (read-only Collection) |
| `DiffZeilen` | `ObservableCollection<TextDiffLine>` | Zeilen des aktuell angezeigten Diffs im Vergleichsmodus |
| `AusgewaehlterKnoten` | `WorkspaceFileNode?` | Aktuell im Baum ausgewählter Knoten. Setter triggert `DateiLadenAsync` über CancellationToken |
| `DateiInhalt` | `string?` | Inhalt bzw. Hinweistext der aktuell ausgewählten Datei (read-only, gesetzt von `DateiLadenAsync`) |
| `AktuellerModus` | `DateibrowserAnsichtsmodus` | Aktuell gewählter Anzeigemodus (Standard oder Vergleich) |
| `ZeigtDiffAnsicht` | `bool` | Calculated: `DiffZeilen.Count > 0` |
| `KannAktuelleDateiOeffnen` | `bool` (private) | Calculated: `AusgewaehlterKnoten?.IsDirectory == false && IsDeleted == false && CommitSha == null` |

### Öffentliche Commands

| Command | Typ | Zweck |
|---------|-----|-------|
| `StandardAnsichtCommand` | AsyncRelayCommand | Wechselt in Standardmodus und lädt Arbeitsbaum neu (ruft `StandardAnsichtAsync`) |
| `VergleichCommand` | AsyncRelayCommand | Wechselt in Vergleichsmodus und lädt Branch-Commits (ruft `VergleichAsync`) |
| `AktualisierenCommand` | AsyncRelayCommand | Lädt aktuellen Modus neu (Baum oder Commits) |
| `NaechsteAenderungCommand` | RelayCommand | Springt zur nächsten Änderung im Diff (wenn `ZeigtDiffAnsicht`) |
| `VorherigeAenderungCommand` | RelayCommand | Springt zur vorherigen Änderung im Diff (wenn `ZeigtDiffAnsicht`) |
| `DateiMitStandardanwendungOeffnenCommand` | RelayCommand | Öffnet Datei mit Standardanwendung (wenn `KannAktuelleDateiOeffnen`) |

### Öffentliche Events

| Event | Signatur | Zweck |
|-------|----------|-------|
| `DiffZeileFokussiert` | `Action<int>?` | Wird ausgelöst von Diff-Navigation, mit Index der Zielzeile in `DiffZeilen`. Abonniert von `FileExplorerView.OnDiffZeileFokussiert` |

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InitialisierenAsync` | public | Setzt Repository-Verzeichnis, wechselt in Standardmodus, lädt Arbeitsbaum |
| `CommitAufklappenAsync` | public | **Lazy-Loading für Commit-Kinder:** Lädt geänderte Dateien eines Commits nach, wenn noch nicht geladen. Setzt `commit.ChildrenLoaded = true`. Analoges Muster zur Anforderung! |

### Private Methoden (Implementierung)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DateiLadenAsync` | private | Lädt Datei-Vorschau oder Diff beim Auswählen eines Knotens |
| `StandardAnsichtAsync` | private | Wechselt in Modus `Standard` und ruft `LadeArbeitsbaumAsync` auf |
| `VergleichAsync` | private | Wechselt in Modus `Vergleich` und ruft `LadeCommitsAsync` auf |
| `AktualisierenAsync` | private | Räumt alles auf und lädt aktuellen Modus neu |
| `LadeArbeitsbaumAsync` | private | **Haupteinstiegspunkt für Arbeitsbaum-Laden:** Ruft `_gitWorkspaceBrowserService.LoadWorkingTreeAsync()` auf und befüllt `Wurzelknoten` |
| `LadeCommitsAsync` | private | Laden von Branch-Commits (nicht weiter relevant für Lazy-Loading) |

### Private State

- `_repositoryPath: string?` — Aktueller Repository-Pfad
- `_dateiLadenCts: CancellationTokenSource?` — Cancellation Token für laufende Datei-Ladevorgänge (wird bei schnellem Wechsel abgebrochen)
- `_aktuellerAenderungsIndex: int` — Index für Diff-Navigation
- `_dispatcherInvoke: Action<Action>` — UI-Thread-Dispatcher für Thread-sicher Zugriffe

### Fehlende Methoden (laut Anforderung)

- **`LadeKinderAsync(knoten: WorkspaceFileNode) : Task`** — Wird beim Aufklappen eines Knotens aufgerufen (vom `FileExplorerView.Expanded`-Event-Handler)
  - Validiert: `knoten.IsDirectory == true && knoten.ChildrenLoaded == false`
  - Ruft `_gitWorkspaceBrowserService.LoadSubtreeAsync(_repositoryPath, knoten.RelativePath, knoten.Depth + 1)` auf
  - Addiert neue Knoten zu `knoten.Children`
  - Setzt `knoten.ChildrenLoaded = true`
  - Mit CancellationToken für Abbruch bei Navigation

- **`BeraeumeKnoten(knoten: WorkspaceFileNode) : Task`** (optional, nur bei Cleanup-Feature aktiviert)
  - Wird beim Zuklappen aufgerufen
  - Entfernt Kinder mit `Tiefe > knoten.Depth + 1` (Groß-Enkel)
  - Optional, Feature-Flag-gesteuert

### Besonderheiten

- **Analoges Lazy-Loading-Muster existiert bereits für Commits:** `CommitAufklappenAsync` (Zeilen 147–177) ist das Vorbild für die zu implementierende `LadeKinderAsync`
- **ChildrenLoaded-Logik:** `BranchCommit.ChildrenLoaded` wird nach erfolgreichem Laden auf `true` gesetzt; identisches Pattern sollte für `WorkspaceFileNode.ChildrenLoaded` genutzt werden
- **Dispatcher-Invoke:** `_dispatcherInvoke` wird für UI-Thread-Sicherheit bei Property-Changes verwendet
- **CancellationToken-Handling:** Bestehende Patterns für Abbruch und Fehlerbehandlung sind vorhanden
