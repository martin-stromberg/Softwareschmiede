# Umsetzungsplan: Lazy-Loading des Verzeichnisbaums mit progressiver Tiefenentwicklung

## Übersicht

Der Standardmodus des `FileExplorer` lädt aktuell den gesamten Arbeitsbaum unbegrenzt rekursiv über `GitWorkspaceBrowserService.LoadWorkingTreeAsync`. Umgestellt wird auf ein Lazy-Loading-Modell: initial werden nur die obersten zwei Ebenen geladen; beim Aufklappen eines Verzeichnisknotens wird dessen nächste Ebene nachgeladen; beim Zuklappen wird stets auf die Invariante „pro Verzeichnis immer eine Ebene mehr geladen als angezeigt" bereinigt (Groß-Enkel-Knoten werden entfernt). Betroffen sind Datenmodell (`WorkspaceFileNode`), Service (`IGitWorkspaceBrowserService`/`GitWorkspaceBrowserService`), ViewModel (`FileExplorerViewModel`) und UI (`FileExplorerView`). Das analoge, bereits vorhandene Lazy-Loading des Vergleichsmodus (`BranchCommit.ChildrenLoaded` + `CommitAufklappenAsync`) dient als Vorbild und bleibt unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `WorkspaceFileNode.Children`-Typ | Von `List<WorkspaceFileNode>` auf `ObservableCollection<WorkspaceFileNode>` umstellen (in-place-Mutation via bestehende `ReplaceAll`-Extension). | Beim Lazy-Laden werden Kinder zu einem bereits gebundenen, sichtbaren Knoten hinzugefügt. Ein `List` löst kein `CollectionChanged` aus, die TreeView würde die neuen Knoten nicht anzeigen. Der Vergleichsmodus nutzt für exakt dieses Muster bereits `ObservableCollection<WorkspaceFileNode> BranchCommit.Files` mit `ReplaceAll` — konsistente Präsentationsmodell-Konvention statt neuem Mechanismus. |
| Expander-Anzeige für ungeladene Verzeichnisse | Platzhalter-Kindknoten (`IsPlaceholder = true`) an jedes Verzeichnis anhängen, dessen Kinder noch nicht geladen sind. Beim ersten Aufklappen wird der Platzhalter durch die echten Kinder ersetzt. | Eine WPF-`TreeViewItem` zeigt den Aufklapp-Pfeil nur, wenn ihre gebundene `ItemsSource` (`Children`) mindestens ein Element enthält. Ohne Platzhalter hätte ein Verzeichnis mit `ChildrenLoaded == false` keinen Pfeil und wäre nicht aufklappbar — das `Expanded`-Event würde nie ausgelöst. Der Platzhalter ist der kanonische WPF-Lazy-Tree-Idiom, hält den Speicher beschränkt (kein Vorladen einer zusätzlichen Ebene) und passt 1:1 zum knoten-bezogenen `ChildrenLoaded`-Gate aus der Anforderung. |
| Lazy-Loading-Trigger | `TreeViewItem.Expanded`-Event am `StandardBaum`, Code-Behind-Handler ruft `FileExplorerViewModel.LadeKinderAsync(node)` auf (analog zu `OnCommitKnotenExpanded` → `CommitAufklappenAsync`). | Bestehendes, erprobtes Muster im selben View für den Vergleichsbaum. Kein Bedarf für ein separates Attached-Behavior. |
| Zuklapp-Bereinigung (`BeraeumeKnoten`) | Beim Zuklappen stets aktiv, ausgelöst über `TreeViewItem.Collapsed`. Kein Feature-Flag — die Bereinigung ist Teil des normalen Ablaufs. | Die Anforderung verlangt ausdrücklich die Invariante „pro Verzeichnis stets eine Ebene mehr geladen als angezeigt". Die Bereinigung stellt genau diese Invariante beim Zuklappen wieder her, indem Groß-Enkel-Knoten (`Depth > node.Depth + 1`) entfernt werden. Das ist deterministisch, hält den Speicherverbrauch bei tiefer Navigation beschränkt und benötigt keinen zusätzlichen Konfigurationszustand. |
| `WorkspaceFileNode`-Charakter | `Depth` als `init`-Eigenschaft; `ChildrenLoaded`/`IsExpanded` bleiben mutabel (bereits vorhanden); `Children` wird mutable `ObservableCollection`. Keine Umstellung auf `INotifyPropertyChanged` nötig. | Nur die Collection-Identität muss Änderungen melden — das leistet `ObservableCollection` selbst. Die Skalar-Flags werden nur vor dem Binden bzw. auf dem UI-Thread gesetzt; kein `INotifyPropertyChanged` erforderlich (minimale Eingriffstiefe). |

## Programmabläufe

### Initiales Laden des Arbeitsbaums (Standardmodus)

1. `FileExplorerViewModel.InitialisierenAsync` bzw. `StandardAnsichtAsync`/`AktualisierenAsync` ruft `LadeArbeitsbaumAsync` auf.
2. `LadeArbeitsbaumAsync` ruft `_gitWorkspaceBrowserService.LoadWorkingTreeAsync(_repositoryPath, InitialLoadDepth, ct)` mit `InitialLoadDepth = 2`.
3. `LoadWorkingTreeAsync` startet `WalkWorkingTreeDirectory` mit `currentDepth = 0` und `maxDepth = InitialLoadDepth`.
4. `WalkWorkingTreeDirectory` erzeugt für jeden Eintrag einen `WorkspaceFileNode` mit gesetztem `Depth`:
   - Verzeichnisse oberhalb der Grenztiefe (`currentDepth + 1 < maxDepth`): Rekursion in die Kinder, `ChildrenLoaded = true`, kein Platzhalter.
   - Verzeichnisse auf der Grenztiefe (Kinder nicht mehr enumeriert): `ChildrenLoaded = false`, ein Platzhalter-Kind wird angehängt.
   - Dateien: keine Kinder, kein Platzhalter.
5. Ergebnis wird per `SortNodes` sortiert und als flache Wurzelliste zurückgegeben.
6. `LadeArbeitsbaumAsync` befüllt `Wurzelknoten` (auf dem Dispatcher-Thread).

Beteiligte Klassen/Komponenten: `FileExplorerViewModel`, `IGitWorkspaceBrowserService`, `GitWorkspaceBrowserService`, `WorkspaceFileNode`.

### Nachladen einer Ebene beim Aufklappen

1. Benutzer klappt einen Verzeichnisknoten im `StandardBaum` auf → `TreeViewItem.Expanded` feuert.
2. Code-Behind-Handler `OnBaumKnotenExpanded` prüft `e.OriginalSource is TreeViewItem { DataContext: WorkspaceFileNode node }` und ruft `vm.LadeKinderAsync(node)` (fire-and-forget mit `SafeFireAndForget`).
3. `LadeKinderAsync` validiert: `node.IsDirectory == true && node.ChildrenLoaded == false` (sonst Abbruch) und `_repositoryPath` gesetzt.
4. Aufruf `_gitWorkspaceBrowserService.LoadSubtreeAsync(_repositoryPath, node.RelativePath, node.Depth + 1, ct)`.
5. `LoadSubtreeAsync` enumeriert die unmittelbaren Kinder von `parentPath`, erzeugt `WorkspaceFileNode`s mit `Depth = depth`; Unterverzeichnisse erhalten `ChildrenLoaded = false` + Platzhalter, Dateien keine; sortierte flache Liste als Rückgabe.
6. Auf dem Dispatcher-Thread: `node.Children.ReplaceAll(neueKinder)` (entfernt den Platzhalter, fügt echte Kinder ein), `node.ChildrenLoaded = true`.
7. Bei Fehler: Exception wird geloggt, `node.ChildrenLoaded` bleibt `false` (erneutes Aufklappen möglich); der Platzhalter bleibt erhalten.

Beteiligte Klassen/Komponenten: `FileExplorerView` (Code-Behind), `FileExplorerViewModel.LadeKinderAsync`, `IGitWorkspaceBrowserService.LoadSubtreeAsync`, `GitWorkspaceBrowserService`, `WorkspaceFileNode`.

### Bereinigung beim Zuklappen (stets aktiv)

1. Benutzer klappt einen Verzeichnisknoten zu → `TreeViewItem.Collapsed` feuert.
2. Code-Behind-Handler `OnBaumKnotenCollapsed` ruft `vm.BeraeumeKnoten(node)` auf.
3. `BeraeumeKnoten` iteriert über die direkten Kinder von `node`: für jedes Kind, das ein geladenes Verzeichnis ist (`IsDirectory && ChildrenLoaded`), wird `kind.Children.ReplaceAll([Platzhalter])` gesetzt und `kind.ChildrenLoaded = false`. Damit werden alle Knoten mit `Depth > node.Depth + 1` (Groß-Enkel) entfernt und die Platzhalter-Invariante wiederhergestellt.
4. Der zugeklappte Knoten selbst (`Depth = node.Depth`) und seine direkten Kinder (`Depth = node.Depth + 1`) bleiben geladen — so ist pro Verzeichnis stets genau eine Ebene mehr geladen als sichtbar. Ein erneutes Aufklappen eines der Kinder löst wieder `LadeKinderAsync` aus.

Beteiligte Klassen/Komponenten: `FileExplorerView` (Code-Behind), `FileExplorerViewModel.BeraeumeKnoten`, `WorkspaceFileNode`.

### Auswahl-Guard für Platzhalter

1. `SelectedItemChanged` bzw. der Setter `AusgewaehlterKnoten` erhält ggf. einen Platzhalterknoten.
2. Ist `node.IsPlaceholder == true`, wird die Auswahl ignoriert (kein `DateiLadenAsync`), damit keine Vorschau für den Platzhalter geladen wird.

Beteiligte Klassen/Komponenten: `FileExplorerViewModel.AusgewaehlterKnoten`/`DateiLadenAsync`, `WorkspaceFileNode`.

## Neue Klassen

Keine neuen Produktionsklassen. Lazy-Loading wird durch neue Methoden/Eigenschaften auf bestehenden Klassen realisiert. (Eine neue Testklasse `FileExplorerViewModelTests_LazyLoading` siehe Abschnitt Tests.)

## Änderungen an bestehenden Klassen

### `WorkspaceFileNode` (Datenmodellklasse / Value Object)

- **Neue Eigenschaften:**
  - `Depth` (`int`, `init`) — Ebene relativ zur Wurzel (0 = oberste Ebene). Wird für Lazy-Loading-Tiefe und Cleanup benötigt.
  - `IsPlaceholder` (`bool`, `init`, Default `false`) — kennzeichnet einen technischen Platzhalter-Kindknoten, der nur die Anzeige des Aufklapp-Pfeils erzwingt. Wird bei Auswahl/Vorschau ignoriert und beim ersten Laden ersetzt.
- **Geänderte Eigenschaften:**
  - `Children` — Typ von `List<WorkspaceFileNode>` auf `ObservableCollection<WorkspaceFileNode>` ändern (weiterhin Default leere Collection). Erforderlich, damit lazy hinzugefügte Kinder in der TreeView erscheinen.

### `IGitWorkspaceBrowserService` (Interface)

- **Geänderte Methoden:**
  - `LoadWorkingTreeAsync` — Signatur erweitern auf `Task<IReadOnlyList<WorkspaceFileNode>> LoadWorkingTreeAsync(string repositoryPath, int maxInitialDepth = 2, CancellationToken ct = default)`. Lädt den Arbeitsbaum nur bis `maxInitialDepth` Ebenen.
- **Neue Methoden:**
  - `LoadSubtreeAsync(string repositoryPath, string parentPath, int depth, CancellationToken ct = default) : Task<IReadOnlyList<WorkspaceFileNode>>` — lädt genau eine Ebene unmittelbarer Kinder unterhalb `parentPath`, vergibt `Depth = depth`, markiert Unterverzeichnisse mit `ChildrenLoaded = false` + Platzhalter.

### `GitWorkspaceBrowserService` (Service-Implementierung)

- **Geänderte Methoden:**
  - `LoadWorkingTreeAsync` — `maxInitialDepth`-Parameter durchreichen; `WalkWorkingTreeDirectory` mit Tiefenzähler aufrufen.
  - `WalkWorkingTreeDirectory` — um `int currentDepth, int maxDepth` erweitern; `Depth` auf jeden erzeugten Knoten setzen; nur rekursieren solange `currentDepth + 1 < maxDepth`; Grenztiefen-Verzeichnisse mit `ChildrenLoaded = false` + Platzhalter statt `ChildrenLoaded = true` markieren.
  - `SortNodes` — Signatur von `List<WorkspaceFileNode>` auf `IList<WorkspaceFileNode>` umstellen und Sortierung so implementieren, dass sie auf `ObservableCollection` funktioniert (Sortierreihenfolge ermitteln und Collection in-place neu ordnen, statt `List.Sort`). Platzhalter-Kinder von der Sortierung ausnehmen bzw. unverändert belassen.
  - `InsertNode` — Parameter-/Zugriffstypen auf `IList<WorkspaceFileNode>` anpassen (wegen `Children`-Typwechsel); Verhalten (Commit-Baum) unverändert, ohne Platzhalter.
- **Neue Methoden:**
  - `LoadSubtreeAsync` — analog zu `WalkWorkingTreeDirectory`, aber nur eine Ebene: enumeriert Kinder von `parentPath`, `.git` ausschließen, `Depth = depth`, Verzeichnisse `ChildrenLoaded = false` + Platzhalter, Dateien normal; `MaxWorkingTreeNodeCount` weiterhin respektieren; leere Liste bei nicht existierendem Pfad. Läuft in `Task.Run` wie `LoadWorkingTreeAsync`.
- **Neue Konstanten:**
  - `InitialLoadDepth = 2` (oder als Default am Interface-Parameter belassen und im ViewModel referenzieren — siehe Konfiguration).
  - `MaxLazyLoadDepth` — harte Obergrenze gegen zirkuläre Symlinks/Endlostiefe (siehe Validierung).

### `FileExplorerViewModel` (Presentation Model)

- **Neue Methoden:**
  - `LadeKinderAsync(WorkspaceFileNode knoten, CancellationToken ct = default) : Task` — Lazy-Load einer Ebene (Ablauf siehe „Nachladen einer Ebene beim Aufklappen"). Muster analog `CommitAufklappenAsync`.
  - `BeraeumeKnoten(WorkspaceFileNode knoten) : void` — Cleanup beim Zuklappen; stets aktiv (Ablauf siehe „Bereinigung beim Zuklappen").
- **Geänderte Methoden:**
  - `LadeArbeitsbaumAsync` — `LoadWorkingTreeAsync` mit explizitem `InitialLoadDepth`-Argument aufrufen.
  - `AusgewaehlterKnoten`-Setter bzw. `DateiLadenAsync` — Guard ergänzen: Platzhalterknoten (`IsPlaceholder`) ignorieren.
- **Neue Konstanten/Felder:**
  - `InitialLoadDepth` (`int`, `2`) — siehe Konfiguration. (Kein `EnableDirtyTreeCleanup`-Flag — die Zuklapp-Bereinigung ist stets aktiv.)

### `FileExplorerView` (UserControl, XAML + Code-Behind)

- **XAML (`FileExplorerView.xaml`):** Am `StandardBaum` `TreeViewItem.Expanded="OnBaumKnotenExpanded"` und `TreeViewItem.Collapsed="OnBaumKnotenCollapsed"` ergänzen — analog zum `VergleichBaum`. Optional: `HierarchicalDataTemplate` für `WorkspaceFileNode` um eine unaufdringliche Lade-/Platzhalteranzeige erweitern (siehe offene Punkte).
- **Neue Event-Handler (Code-Behind):**
  - `OnBaumKnotenExpanded` — reagiert auf `TreeViewItem.Expanded` des `StandardBaum`; ruft `vm.LadeKinderAsync(node)` via `SafeFireAndForget`. Vorbild: `OnCommitKnotenExpanded`.
  - `OnBaumKnotenCollapsed` — reagiert auf `TreeViewItem.Collapsed` des `StandardBaum`; ruft `vm.BeraeumeKnoten(node)`.

## Datenbankmigrationen

Keine. Das Feature betrifft ausschließlich das In-Memory-Baummodell und Dateisystem-Enumeration; keine Persistenz.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `LadeKinderAsync(knoten)` | Nur ausführen bei `knoten.IsDirectory == true && knoten.ChildrenLoaded == false` | Andernfalls stiller No-Op (kein erneutes Laden, keine Doppel-Enumeration). |
| `AusgewaehlterKnoten` / `DateiLadenAsync` | Platzhalterknoten (`IsPlaceholder == true`) nicht als Datei-/Verzeichnisauswahl behandeln | Andernfalls würde eine Vorschau für einen technischen Knoten geladen. |
| Lazy-Load-Tiefe | `depth` bzw. rekursive Tiefe darf `MaxLazyLoadDepth` nicht überschreiten | Schutz gegen zirkuläre Symlinks/Endlostiefe; bei Überschreitung wird nicht weiter enumeriert und geloggt. |
| `LoadSubtreeAsync(parentPath)` | Nicht existierender/nicht lesbarer `parentPath` | Leere Liste zurückgeben, Warnung loggen (wie `LoadWorkingTreeAsync` bei fehlendem Pfad). |

## Konfigurationsänderungen

Keine `appsettings`-Einträge. Es werden Code-Konstanten eingeführt:

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `InitialLoadDepth` | `int` (Konstante, ViewModel bzw. Interface-Default) | `2` | Anzahl initial geladener Ebenen (Wurzel + eine Ebene). |
| `MaxLazyLoadDepth` | `int` (Konstante, `GitWorkspaceBrowserService`) | z. B. `64` | Harte Obergrenze gegen zirkuläre Symlinks/Endlostiefe. |

Die Zuklapp-Bereinigung ist fest im Ablauf verankert (stets aktiv beim Zuklappen) und benötigt kein Konfigurations-Flag.

## Seiteneffekte und Risiken

- **`WorkspaceFileNode.Children`-Typwechsel (`List` → `ObservableCollection`):** Wirkt auf alle Nutzer von `.Children` — `WalkWorkingTreeDirectory`, `InsertNode`, `SortNodes` (Service) sowie Tests (`GitWorkspaceBrowserServiceWorkingTreeTests`, `GitWorkspaceBrowserServiceTests.FindNode`). `List.Sort` und `List`-typisierte Parameter müssen auf `IList`-kompatible Varianten umgestellt werden. `ReplaceAll` (Extension) muss auch für `Children` verfügbar sein (wird bereits für `BranchCommit.Files` genutzt).
- **`LoadWorkingTreeAsync`-Signaturänderung:** Bricht bestehende Mock-Setups (`FileExplorerViewModelTests`) und die produktiven Aufrufer (`LadeArbeitsbaumAsync`). Durch optionalen Default `maxInitialDepth = 2` kompiliert produktiver Aufruf weiter, Moq-Setups müssen jedoch das zusätzliche Argument (`It.IsAny<int>()`) berücksichtigen.
- **Bestehendes Verhalten „gesamter Baum sofort sichtbar":** Ändert sich bewusst — tiefe Knoten sind erst nach Aufklappen sichtbar. Features/Abläufe, die den vollständigen Baum sofort erwarten, gibt es nicht (Auswahl/Vorschau arbeitet knotenweise); Risiko gering.
- **Vergleichsmodus (`CommitAufklappenAsync`, `BranchCommit`):** Bleibt unverändert. Der Commit-Baum wird weiterhin vollständig via `BuildCommitFileTree`/`InsertNode` aufgebaut (ohne Platzhalter, `ChildrenLoaded = true`). Nur sicherstellen, dass der `Children`-Typwechsel dort keine Sortier-/Aufbaufehler verursacht.
- **Platzhalter-Leckage:** Platzhalterknoten dürfen nicht in Auswahl, Vorschau oder „Mit Standardanwendung öffnen" landen — Guard erforderlich (siehe Validierung).

## Umsetzungsreihenfolge

1. **`WorkspaceFileNode` erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: `Depth` (`int`, init) und `IsPlaceholder` (`bool`, init) hinzufügen; `Children` von `List<WorkspaceFileNode>` auf `ObservableCollection<WorkspaceFileNode>` umstellen.

2. **Service-Builder an neuen `Children`-Typ anpassen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `SortNodes`, `InsertNode`, `WalkWorkingTreeDirectory` auf `IList<WorkspaceFileNode>` bzw. in-place-Sortierung umstellen, damit der Vergleichs-/Commit-Baumaufbau und der bestehende Working-Tree-Aufbau weiter kompilieren und funktionieren (noch ohne Tiefenbegrenzung).

3. **`IGitWorkspaceBrowserService` erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `LoadWorkingTreeAsync` um `maxInitialDepth = 2` erweitern; `LoadSubtreeAsync` deklarieren.

4. **`GitWorkspaceBrowserService` — Tiefenbegrenzung + Platzhalter beim initialen Laden**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: `WalkWorkingTreeDirectory` um `currentDepth`/`maxDepth` erweitern, `Depth` setzen, Grenztiefen-Verzeichnisse mit `ChildrenLoaded = false` + Platzhalter versehen; `MaxLazyLoadDepth`-Konstante ergänzen; `LoadWorkingTreeAsync` reicht `maxInitialDepth` durch.

5. **`GitWorkspaceBrowserService.LoadSubtreeAsync` implementieren**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: Eine Ebene unterhalb `parentPath` enumerieren, `Depth = depth`, Verzeichnisse mit Platzhalter + `ChildrenLoaded = false`, Dateien normal; `.git` ausschließen; Knotenlimit + `MaxLazyLoadDepth` respektieren; leere Liste bei ungültigem Pfad.

6. **`FileExplorerViewModel` — Lazy-Load-Logik**
   - Voraussetzungen: Schritte 1, 3, 5.
   - Beschreibung: `InitialLoadDepth`-Konstante; `LadeArbeitsbaumAsync` mit `InitialLoadDepth` aufrufen; `LadeKinderAsync` (analog `CommitAufklappenAsync`) und `BeraeumeKnoten` (stets aktive Zuklapp-Bereinigung) implementieren; Platzhalter-Guard in `AusgewaehlterKnoten`/`DateiLadenAsync`.

7. **`FileExplorerView` — Expand/Collapse-Anbindung**
   - Voraussetzungen: Schritt 6.
   - Beschreibung: `TreeViewItem.Expanded="OnBaumKnotenExpanded"` und `TreeViewItem.Collapsed="OnBaumKnotenCollapsed"` am `StandardBaum` in XAML; Code-Behind-Handler `OnBaumKnotenExpanded`/`OnBaumKnotenCollapsed` analog `OnCommitKnotenExpanded`; optional Lade-/Platzhalteranzeige im `HierarchicalDataTemplate`.

8. **Tests anpassen und ergänzen**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: Bestehende Service-/VM-Tests an neue Signaturen und Tiefenbegrenzung anpassen; neue Unit-Tests für Tiefe, Platzhalter, `ChildrenLoaded`, `LoadSubtreeAsync`, `LadeKinderAsync`, `BeraeumeKnoten`; neuen E2E-Test für Aufklapp-Lazy-Load.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `LoadWorkingTreeAsync_LaedtNurMaxInitialDepthEbenen` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Bei `maxInitialDepth = 2` sind nur Ebene 0 und 1 als echte Knoten geladen; tiefere Einträge fehlen. |
| `LoadWorkingTreeAsync_SetztDepthKorrekt` | `GitWorkspaceBrowserServiceWorkingTreeTests` | `Depth` = 0 für oberste Ebene, 1 für deren Kinder. |
| `LoadWorkingTreeAsync_GrenztiefeVerzeichnis_ChildrenLoadedFalseUndPlatzhalter` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Verzeichnis auf Grenztiefe hat `ChildrenLoaded == false` und genau einen Platzhalter-Kindknoten. |
| `LoadWorkingTreeAsync_ObereEbeneVerzeichnis_ChildrenLoadedTrue` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Verzeichnis oberhalb der Grenztiefe hat echte Kinder und `ChildrenLoaded == true`, keinen Platzhalter. |
| `LoadSubtreeAsync_LaedtEineEbeneUnterhalbParent` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Gibt genau die unmittelbaren Kinder von `parentPath` zurück. |
| `LoadSubtreeAsync_SetztDepthAufUebergebenenWert` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Alle zurückgegebenen Knoten haben `Depth == depth`. |
| `LoadSubtreeAsync_UnterverzeichnisMitPlatzhalterUndChildrenLoadedFalse` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Zurückgegebene Unterverzeichnisse tragen Platzhalter + `ChildrenLoaded == false`. |
| `LoadSubtreeAsync_NichtExistierenderPfad_LeereListe` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Ungültiger `parentPath` → leere Liste, keine Exception. |
| `LadeKinderAsync_LaedtKinderUndSetztChildrenLoaded` | `FileExplorerViewModelTests_LazyLoading` (neu) | Aufklappen eines Grenztiefen-Verzeichnisses ruft `LoadSubtreeAsync`, ersetzt Platzhalter durch echte Kinder, setzt `ChildrenLoaded = true`. |
| `LadeKinderAsync_BereitsGeladen_LaedtNichtErneut` | `FileExplorerViewModelTests_LazyLoading` | Bei `ChildrenLoaded == true` erfolgt kein Service-Aufruf. |
| `LadeKinderAsync_KeinVerzeichnis_TutNichts` | `FileExplorerViewModelTests_LazyLoading` | Für Datei-Knoten erfolgt kein Service-Aufruf. |
| `LadeKinderAsync_Fehler_LaesstChildrenLoadedFalse` | `FileExplorerViewModelTests_LazyLoading` | Wirft der Service, wird geloggt und `ChildrenLoaded` bleibt `false` (erneutes Laden möglich). |
| `BeraeumeKnoten_EntferntGrossEnkel` | `FileExplorerViewModelTests_LazyLoading` | Beim Zuklappen werden Knoten mit `Depth > node.Depth + 1` entfernt, die betroffenen Kinder auf `ChildrenLoaded = false` + Platzhalter zurückgesetzt. |
| `BeraeumeKnoten_BehaeltDirekteKinderUndPlatzhalterInvariante` | `FileExplorerViewModelTests_LazyLoading` | Nach dem Zuklappen bleiben der Knoten (`Depth`) und seine direkten Kinder (`Depth + 1`) erhalten — pro Verzeichnis ist stets genau eine Ebene mehr geladen als sichtbar. |
| `Platzhalterknoten_WirdNichtAlsAuswahlBehandelt` | `FileExplorerViewModelTests_LazyLoading` | Auswahl eines Platzhalters lädt keine Vorschau. |
| `CreateSut()` / `WaitForAsync(...)` | `FileExplorerViewModelTests_LazyLoading` | Wiederverwendung der bestehenden Helper (ggf. gemeinsam über partielle Klasse/Basis). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_ListetDateienUndVerzeichnisse` | Erwartet aktuell unbegrenzte Rekursion (`nested/Deep.cs` auf Tiefe 2 geladen). Mit Default `maxInitialDepth = 2` ist `Deep.cs` nicht mehr initial geladen; Test auf Tiefenbegrenzung + Platzhalter umstellen bzw. `Deep.cs` über `LoadSubtreeAsync` nachladen. |
| `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_SchliesstGitVerzeichnisAus` | Prüfen, dass Aufruf/Erwartung mit neuer Signatur weiterhin passt (ggf. `maxInitialDepth` explizit übergeben). |
| `FileExplorerViewModelTests.Standard_LaedtWurzelknotenUeberWorkingTree` | Moq-Setup für `LoadWorkingTreeAsync` muss zusätzliches `int`-Argument (`It.IsAny<int>()`) berücksichtigen. |
| `FileExplorerViewModelTests` (weitere Setups mit `LoadWorkingTreeAsync`) | Gleiche Moq-Signaturanpassung. |
| `GitWorkspaceBrowserServiceTests.FindNode`-Nutzung | `Children` ist jetzt `ObservableCollection`; sicherstellen, dass Hilfsmethode/Assertions weiterhin kompilieren (Zugriff über `IList`/`IEnumerable`). |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Standardbaum wird angezeigt; ein Verzeichnisknoten wird aufgeklappt und ein zuvor nicht geladenes Kind (tieferer Knoten) erscheint. | `E2E_FileExplorer` (neue Testmethode `DateiExplorer_KlapptVerzeichnisAufUndLaedtKinderNach_E2E`) | Lazy-Loading beim Aufklappen lädt die nächste Ebene nach. Voraussetzung: geklontes Test-Repository enthält eine mindestens dreistufige Verzeichnisstruktur (Test-Datenbereitstellung im Setup ergänzen). |
| Ein zuvor tief navigierter Verzeichnisknoten wird zugeklappt und erneut aufgeklappt; die nächste Ebene erscheint wieder korrekt (Bereinigung entfernt Groß-Enkel, erneutes Aufklappen lädt nach). | `E2E_FileExplorer` (neue Testmethode `DateiExplorer_KlapptVerzeichnisZuUndErneutAuf_LaedtKinderNach_E2E`) | Zuklapp-Bereinigung ist stets aktiv und beschädigt den Baum nicht; die Invariante „eine Ebene mehr geladen als sichtbar" bleibt beim erneuten Aufklappen konsistent. Nutzt dieselbe dreistufige Test-Verzeichnisstruktur. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_FileExplorer.DateiExplorer_ZeigtBaumUndModeButtons_UndWechseltZuInfoUndZurueck_E2E` | Keine funktionale Anpassung nötig — prüft nur Sichtbarkeit von `FileExplorerBaum` und Ribbon-Gruppen (oberste Ebene bleibt sofort sichtbar). Nur gegenprüfen, dass Lazy-Loading die oberste Ebene nicht verzögert. |

## Offene Punkte

Keine — alle zuvor offenen Punkte wurden geklärt und sind in den Plan eingearbeitet:

- **Zuklapp-Bereinigung:** stets aktiv beim Zuklappen (kein Feature-Flag) — siehe Designentscheidungen und Ablauf „Bereinigung beim Zuklappen".
- **Fortschrittsanzeige:** dezenter „Lädt…"-Hinweis am aufklappenden Knoten/Platzhalter analog Vergleichsmodus; kein separater Spinner (optional im `HierarchicalDataTemplate`).
- **Maximale Ladetiefe:** harte Obergrenze `MaxLazyLoadDepth` (z. B. 64) im `GitWorkspaceBrowserService`; bei Überschreitung keine weitere Enumeration + Warnung.
- **Cache-Invalidierung:** `AktualisierenCommand` verwirft `Wurzelknoten` vollständig und lädt initial neu — kein zusätzlicher Cache, keine Teil-Invalidierung.
- **Vergleichsmodus:** bleibt unverändert; dient nur als Vorbild. Der `Children`-Typwechsel darf den Commit-Baumaufbau nicht beeinträchtigen (in Schritt 2 abgesichert).
