# Umsetzungsplan: Dateiexplorer für Aufgabendetailansicht

## Übersicht

In der WPF-`TaskDetailView` wird ein neues Ansichts-Register „Dateien" (neben Info/CLI/Diff) ergänzt. Der Explorer ist eine Split-View: links ein Verzeichnis-/Dateibaum, rechts der Dateiinhalt bzw. ein farblich hervorgehobener Diff. Zwei Modi werden angeboten – **Standard** (gesamter Arbeitsbaum des geklonten Repositories) und **Vergleich** (nur im Branch geänderte Dateien, gruppiert nach Commits). Der komplette Git-Backend-Teil (Basisreferenz-Ermittlung, geänderte Dateien, Commit-Dateien, Datei-/Commit-Vorschau, Binär-/Großdatei-Schutz) existiert bereits im Application-Layer als `IGitWorkspaceBrowserService` und wird wiederverwendet; ergänzt werden nur die Voll-Baum-Aufzählung (Standardmodus), ein Präsentations-Zeilendiff und die WPF-Oberfläche.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Baum-Knotentyp | Bestehendes `WorkspaceFileNode` (Domain Value Object) für **beide** Modi verwenden statt des in der Anforderung genannten neuen `FileTreeNode` | `WorkspaceFileNode` modelliert bereits `IsDirectory`, `Children`, `IsExpanded`, `ChildrenLoaded`, `RelativePath`, `Name`, `Status`, `CommitSha`; ein einziger `HierarchicalDataTemplate` bedient Standard- und Vergleichsmodus. Das vorhandene `FileTreeNode` ist ausschließlich für externe Agentenpaket-Strukturen gedacht. |
| Git-Backend Vergleichsmodus | Bestehenden `IGitWorkspaceBrowserService` wiederverwenden (`LoadSnapshotAsync` → `BranchCommits`, `LoadCommitFilesAsync`, `LoadCommitPreviewAsync`) statt neuer Services `DateibrowserService`/`GitDiffParserService` | Der Service parst `git diff-tree --name-status -z` bereits korrekt, ermittelt die Basisreferenz robust (`origin/HEAD` → `origin/main`/`master`/`main`) und ist getestet. `GitDiffParserService`, `CommitDiffGroup`, `FileChange` sind damit überflüssig (Gateway-/Service-Layer-Wiederverwendung). |
| Git-Backend Standardmodus | `IGitWorkspaceBrowserService` um eine Methode `LoadWorkingTreeAsync` erweitern (Voll-Aufzählung des Arbeitsbaums, `.git` ausgeschlossen) statt eines separaten `DateibrowserService` | Der Dienst ist bereits „der lokale Repository-Browser" und besitzt die Baum-Aufbau-Helfer (`InsertNode`/`SortNodes`); ein zweiter Service mit zweitem Knotentyp würde Logik und Datenmodell duplizieren. |
| Datei-/Commit-Vorschau | Bestehende `LoadPreviewAsync` (Arbeitsbaum) bzw. `LoadCommitPreviewAsync` (Commit) wiederverwenden | Beide liefern bereits Binär-Erkennung, 1-MB-Inline-Grenze und Alt-/Neu-Inhalt; kein neues Caching/keine neue Größenlogik nötig. |
| Zeilendiff für Anzeige | Neuer Präsentations-Service `TextDiffService` (Application-Layer) erzeugt UI-neutrale `FileTextDiff`/`TextDiffLine`-Value-Objects; wiederverwendet das bestehende Enum `DiffLineStatus` (Added/Removed/Modified/Context) | Der vorhandene `DiffAlgorithmService` ist an die DB-Entities `DiffResult`/`DiffBlock` gekoppelt, kennt keine „Modified"-Zeilen und kein Inline-Highlighting und ist nur im Web-Host registriert. Ein schlanker, testbarer Präsentations-Diff ohne DB-Abhängigkeit ist sauberer (Domain-Value-Object + Transaction-Script-artiger Service). |
| Inline-Highlighting bei Modified-Zeilen | Wortabschnitte über gemeinsames Präfix/Suffix der gepaarten Modified-Zeilen (`InlineDiffSegment` mit `IsChanged`) | Geklärt (Antwort zu ehemals offenem Punkt 1): deterministisch, ohne Fremdbibliothek, ausreichend für die geforderte orange Hervorhebung geänderter Teilbereiche. Ein feineres Token-/LCS-basiertes Inline-Highlighting ist **nicht** Teil dieser Umsetzung (siehe Abschnitt „Nicht im Umsetzungsumfang (Folgeaufgaben)"). |
| Syntax-Highlighting des Dateiinhalts | Nicht umsetzen; rechte Inhaltsanzeige und `DiffViewer` rendern monospace (`Consolas`/`FontFamily` fix) ohne sprachabhängige Kolorierung | Geklärt (Antwort zu ehemals offenem Punkt 2): die Anforderung stuft Syntax-Highlighting als optional/nicht zwingend ein; ausschließlich die Diff-Statusfarben (grün/rot/orange) und das Inline-Highlighting werden farblich dargestellt. |
| Aufbau des Standard-Baums | Eager: vollständige Directory-Walk-Aufzählung in `LoadWorkingTreeAsync`, `.git` ausgeschlossen, mit fester Knoten-Obergrenze; bei Überschreitung Hinweis statt vollständigem Baum | Geklärt (Antwort zu ehemals offenem Punkt 3): einfachster, testbarer Weg mit dem vorhandenen Baum-Aufbau (`InsertNode`/`SortNodes`). Lazy-Nachladen pro Verzeichnis ist als spätere Optimierung möglich, aber nicht Teil dieser Umsetzung. |
| Präsentationsmodell-Aufteilung | Neues, eigenständiges `FileExplorerViewModel` (Presentation Model), das `TaskDetailViewModel` über die Property `FileExplorer` komponiert; nur der Ansichts-Umschaltzustand (`Dateibrowser`-Enumwert, `IsFileExplorerViewSelected`, `ShowFileExplorerPanel`, `DateiViewCommand`) bleibt in `TaskDetailViewModel` | `TaskDetailViewModel` ist bereits ~1190 Zeilen; die Explorer-Logik (Baum, Auswahl, Inhalt, Diff, Moduswechsel, Refresh) wird dadurch isoliert testbar und hält das bestehende ViewModel schlank. |
| Baum-Steuerelement | Natives WPF `TreeView` mit `HierarchicalDataTemplate` | Bewährt, unterstützt Auswahl/Expansion out-of-the-box; das ItemsControl-Alternativmuster erfordert mehr Eigenbau ohne Mehrwert. |
| DiffViewer-Rendering | `ItemsControl` mit `TextDiffLine`-Items; Hintergrund je `DiffLineStatus` über neuen `DiffLineStatusToBrushConverter`; Inline-`Run`s pro `InlineDiffSegment` | Folgt dem in der Anforderung skizzierten zeilenweisen Rendering; Zeilennummernspalte inklusive. |

## Nicht im Umsetzungsumfang (Folgeaufgaben)

Folgende Punkte sind bewusst **außerhalb** dieser Implementierung und sollten – falls gewünscht – als eigenständige zukünftige Issues angelegt werden:

- **Feineres Inline-Highlighting (Token-/LCS-basiert):** Das in dieser Umsetzung realisierte Inline-Highlighting arbeitet auf Basis des gemeinsamen Präfix/Suffix der gepaarten Modified-Zeilen (Wortabschnitts-Granularität). Ein genaueres, token- bzw. LCS-/Levenshtein-basiertes Inline-Highlighting geänderter Teilbereiche ist **nicht** Teil dieser Aufgabe und ist als separates Folge-Issue vorzusehen.
- **Syntax-Highlighting des Dateiinhalts:** sprachabhängige Kolorierung von Code/Markup (siehe Designentscheidung) – spätere Ausbaustufe.
- **Lazy-Nachladen des Standard-Baums:** verzeichnisweises Nachladen sehr großer Repositories – spätere Performance-Optimierung.

## Programmabläufe

### Dateiexplorer öffnen (Standardmodus)

1. Nutzer klickt in der Ansicht-Umschaltgruppe der `TaskDetailView` auf „Dateien"; `DateiViewCommand` ruft `WaehleAnsicht(DetailAnsicht.Dateibrowser)` auf.
2. `TaskDetailViewModel` setzt `IsFileExplorerViewSelected`; die `FileExplorerView` wird sichtbar (Binding an `IsFileExplorerViewSelected`).
3. Beim ersten Sichtbarwerden (bzw. Setzen der Aufgabe) übergibt `TaskDetailViewModel` `Aufgabe.LokalerKlonPfad` an `FileExplorer` und ruft dessen `InitialisierenAsync` auf.
4. `FileExplorerViewModel` setzt den Modus auf `Standard`, ruft `IGitWorkspaceBrowserService.LoadWorkingTreeAsync(pfad, ct)` auf und füllt `Wurzelknoten` (ObservableCollection von `WorkspaceFileNode`).
5. Der Baum wird über `HierarchicalDataTemplate` gerendert; die rechte Seite bleibt leer/Platzhalter, bis eine Datei gewählt wird.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `FileExplorerViewModel`, `IGitWorkspaceBrowserService`, `FileExplorerView`, `WorkspaceFileNode`.

### Datei auswählen und Inhalt anzeigen (Standardmodus)

1. Nutzer selektiert einen Datei-Knoten im `TreeView`; die Bindung setzt `FileExplorerViewModel.AusgewaehlterKnoten`.
2. Der Setter startet `DateiLadenAsync`; bei Verzeichnis-Knoten passiert nichts.
3. `DateiLadenAsync` ruft `IGitWorkspaceBrowserService.LoadPreviewAsync(pfad, knoten, ct)` auf.
4. Ergebnis (`FilePreview`): Bei `IsBinary`/`IsTooBig` wird `DateiInhalt` auf den Hinweistext gesetzt; sonst auf `CurrentContent`.
5. Die rechte Standard-Ansicht (Read-Only-Text) zeigt `DateiInhalt`; der DiffViewer bleibt ausgeblendet.

Beteiligte Klassen/Komponenten: `FileExplorerViewModel`, `IGitWorkspaceBrowserService`, `FilePreview`.

### In Vergleichsmodus wechseln (Commits laden)

1. Nutzer klickt „Vergleich"; `VergleichCommand` setzt `AktuellerModus = DateibrowserAnsichtsmodus.Vergleich`.
2. `FileExplorerViewModel` ruft `IGitWorkspaceBrowserService.LoadSnapshotAsync(pfad, ct)` auf und übernimmt `BranchCommits` in `CommitGruppen` (ObservableCollection von `BranchCommit`).
3. Der Baum rendert Commits als aufklappbare Wurzelknoten (Subject + ShortSha), Kinder zunächst leer (`ChildrenLoaded == false`).
4. Beim Aufklappen eines Commits ruft `CommitAufklappenAsync` `LoadCommitFilesAsync(pfad, commit.Sha, ct)` auf, füllt `commit.Files` und setzt `ChildrenLoaded`.

Beteiligte Klassen/Komponenten: `FileExplorerViewModel`, `IGitWorkspaceBrowserService`, `BranchCommit`, `WorkspaceFileNode`.

### Geänderte Datei im Vergleichsmodus anzeigen (Diff)

1. Nutzer selektiert einen Datei-Knoten unterhalb eines Commits (`CommitSha != null`).
2. `DateiLadenAsync` ruft `LoadCommitPreviewAsync(pfad, knoten, ct)` auf → `FilePreview` mit `OriginalContent`/`CurrentContent`.
3. Bei `IsBinary`/`IsTooBig` wird der Hinweistext angezeigt; sonst ruft das ViewModel `ITextDiffService.BuildDiff(originalContent, currentContent)` auf.
4. Das Ergebnis `FileTextDiff` (Liste `TextDiffLine` mit `Status`, alter/neuer Zeilennummer, `InlineSegments`) wird an `DiffZeilen` gebunden.
5. Der `DiffViewer` rendert je Zeile Hintergrundfarbe nach `DiffLineStatus` (grün Added, rot Removed, orange Modified) und Inline-`Run`s für geänderte Wortteile.

Beteiligte Klassen/Komponenten: `FileExplorerViewModel`, `IGitWorkspaceBrowserService`, `ITextDiffService`, `FileTextDiff`, `TextDiffLine`, `DiffViewer`, `DiffLineStatusToBrushConverter`.

### Aktualisieren

1. Nutzer klickt „Aktualisieren"; `AktualisierenCommand` ruft `AktualisierenAsync`.
2. `FileExplorerViewModel` verwirft den aktuellen Baum-/Commit-Zustand und lädt entsprechend dem `AktuellerModus` neu (`LoadWorkingTreeAsync` bzw. `LoadSnapshotAsync`).

Beteiligte Klassen/Komponenten: `FileExplorerViewModel`, `IGitWorkspaceBrowserService`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `DateibrowserAnsichtsmodus` | Enum (`Softwareschmiede.App/ViewModels`) | Werte `Standard`, `Vergleich`; steuert Moduswahl im `FileExplorerViewModel`. |
| `TextDiffLine` | Datenmodellklasse / Value Object (`Domain/ValueObjects`) | Eine Diff-Zeile für die Anzeige: `Content`, `Status` (`DiffLineStatus`), `OldLineNumber` (`int?`), `NewLineNumber` (`int?`), `InlineSegments` (`IReadOnlyList<InlineDiffSegment>`). |
| `InlineDiffSegment` | Datenmodellklasse / Value Object (`Domain/ValueObjects`) | Teilabschnitt einer modifizierten Zeile: `Text`, `IsChanged`. |
| `FileTextDiff` | Datenmodellklasse / Value Object (`Domain/ValueObjects`) | Ergebnis eines Zeilendiffs: `Lines` (`IReadOnlyList<TextDiffLine>`), Zählwerte (`AddedCount`, `RemovedCount`, `ModifiedCount`). |
| `ITextDiffService` | Interface (`Application/Services`) | Abstraktion des Präsentations-Zeilendiffs; testbar/mockbar. |
| `TextDiffService` | Service-Klasse (`Application/Services`) | Baut aus altem/neuem Textinhalt ein `FileTextDiff` inkl. Modified-Paarung und Inline-Segmenten. |
| `FileExplorerViewModel` | ViewModel (`App/ViewModels`) | Presentation Model des Explorers: Baum, Commits, Auswahl, Dateiinhalt, Diff-Zeilen, Modus, Commands. |
| `FileExplorerView` | UserControl (`App/Views`) | Split-View mit Mode-Buttons, `TreeView` und rechter Inhalts-/Diff-Anzeige. |
| `DiffViewer` | UserControl (`App/Controls`) | Wiederverwendbarer, zeilenweiser Diff-Renderer für `TextDiffLine`-Items. |
| `DiffLineStatusToBrushConverter` | IValueConverter (`App/Converters`) | Wandelt `DiffLineStatus` in Hintergrund-Brush (grün/rot/orange/transparent). |

## Änderungen an bestehenden Klassen

### `IGitWorkspaceBrowserService` (Interface)

- **Neue Methoden:** `LoadWorkingTreeAsync(string repositoryPath, CancellationToken ct)` → `Task<IReadOnlyList<WorkspaceFileNode>>` — liefert den vollständigen Arbeitsbaum des geklonten Repositories (Verzeichnisse + Dateien, `.git` ausgeschlossen) für den Standardmodus.

### `GitWorkspaceBrowserService` (Service)

- **Neue Methoden:** Implementierung von `LoadWorkingTreeAsync` — rekursive Aufzählung ab `repositoryPath` (Directory-Walk), Skip von `.git`, Wiederverwendung von `InsertNode`/`SortNodes`, Sicherheits-Obergrenze für die Knotenanzahl; keine Git-Aufrufe nötig.

### `TaskDetailViewModel` (ViewModel)

- **Neue Eigenschaften:** `FileExplorer` (`FileExplorerViewModel`) — komponiertes Presentation Model; `IsFileExplorerViewSelected` (`bool`); `ShowFileExplorerPanel` (`bool`) — true, wenn `Aufgabe.LokalerKlonPfad` gesetzt ist und das Verzeichnis existiert.
- **Neue Commands:** `DateiViewCommand` — wechselt zu `DetailAnsicht.Dateibrowser` (CanExecute: `ShowFileExplorerPanel`).
- **Geänderte Methoden:** privates Enum `DetailAnsicht` erhält Wert `Dateibrowser`; `WaehleAnsicht` behandelt `Dateibrowser` (Fallback auf `Info`, wenn `ShowFileExplorerPanel` false) und meldet `IsFileExplorerViewSelected`; `Aufgabe`-Setter feuert zusätzlich `OnPropertyChanged(nameof(ShowFileExplorerPanel))` und übergibt `LokalerKlonPfad` an `FileExplorer`.
- **Geänderter Konstruktor:** zusätzliche Abhängigkeit `FileExplorerViewModel` (via DI injiziert).

### `TaskDetailView.xaml` (View)

- Neuer `<Button>` „Dateien" (AutomationName `DateiViewButton`) in der Ansicht-Umschaltgruppe, sichtbar bei `ShowFileExplorerPanel`.
- Neue `<views:FileExplorerView>` im Hauptinhalt-Grid, `DataContext="{Binding FileExplorer}"`, `Visibility="{Binding IsFileExplorerViewSelected, Converter={StaticResource BoolToVisibilityConverter}}"`.

### `App.xaml` (Ressourcen)

- Registrierung des `DiffLineStatusToBrushConverter` im globalen `ResourceDictionary` (analog zu `BoolToVisibilityConverter`).

### `App.xaml.cs` (`ConfigureServices`)

- `services.AddScoped<IGitWorkspaceBrowserService, GitWorkspaceBrowserService>();` (Abhängigkeit `ICliRunner` ist bereits registriert).
- `services.AddSingleton<ITextDiffService, TextDiffService>();`
- `services.AddTransient<FileExplorerViewModel>();`

## Datenbankmigrationen

Keine. Der Explorer liest ausschließlich aus dem Dateisystem/Git; die vorhandenen Diff-Persistenz-Entities (`DiffResult`/`DiffBlock`/`DiffLine`) werden nicht verwendet.

## Validierungsregeln

Keine. Es handelt sich um eine reine Leseansicht. Pfadsicherheit (kein Ausbruch aus dem Repository-Root) ist in `GitWorkspaceBrowserService.CombinePath` bereits implementiert und gilt für die neue Aufzählung ebenfalls.

## Konfigurationsänderungen

Keine. Basisreferenz, Inline-Grenze (1 MB) und Binär-Erkennung sind im bestehenden `GitWorkspaceBrowserService` fest hinterlegt und werden wiederverwendet.

## Seiteneffekte und Risiken

- **`IGitWorkspaceBrowserService`-Erweiterung:** Der Blazor-`AufgabeDetail`-Bereich nutzt das Interface; dessen bUnit-Tests mocken es mit Moq (`new Mock<IGitWorkspaceBrowserService>()`), sodass eine zusätzliche Methode ohne Setup automatisch einen Default zurückgibt — kein Bruch. Nur die konkrete Implementierung gewinnt eine Methode.
- **Neue WPF-DI-Registrierung:** `IGitWorkspaceBrowserService` war im WPF-Host bisher nicht registriert. Nach der Registrierung startet der Service bei Nutzung `git`-Prozesse via `ICliRunner` innerhalb der App — Aufrufe erfolgen ausschließlich mit gültigem `LokalerKlonPfad` und in `try/catch` des ViewModels.
- **`TaskDetailViewModel`-Konstruktorsignatur:** ändert sich; alle Erzeuger (`App.xaml.cs`, `TaskDetailViewModelTestFactory`) müssen die neue Abhängigkeit liefern.
- **Standardmodus-Baumgröße:** Voll-Aufzählung großer Repositories kann viele Knoten erzeugen; abgesichert durch `.git`-Ausschluss und eine Knoten-Obergrenze (bei Überschreitung Hinweis statt vollständigem Baum).
- **Keine weiteren bekannten Seiteneffekte** auf CLI-, Diff- oder Info-Ansicht (additive Umschaltung).

## Umsetzungsreihenfolge

1. **Value Objects für den Zeilendiff anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `TextDiffLine`, `InlineDiffSegment`, `FileTextDiff` in `Domain/ValueObjects` erstellen; `DiffLineStatus` (vorhanden) wiederverwenden.

2. **Enum `DateibrowserAnsichtsmodus` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Enum mit `Standard`, `Vergleich` in `App/ViewModels`.

3. **`ITextDiffService` + `TextDiffService` anlegen**
   - Voraussetzungen: Schritt 1 (Value Objects), `DiffLineStatus`.
   - Beschreibung: Zeilendiff (Context/Added/Removed/Modified) inkl. Modified-Paarung und Inline-Segmenten; gibt `FileTextDiff` zurück.

4. **`IGitWorkspaceBrowserService` um `LoadWorkingTreeAsync` erweitern**
   - Voraussetzungen: `WorkspaceFileNode` (vorhanden), `GitWorkspaceBrowserService` (vorhanden).
   - Beschreibung: Interface-Methode + Directory-Walk-Implementierung mit `.git`-Ausschluss und Knoten-Obergrenze.

5. **`FileExplorerViewModel` anlegen**
   - Voraussetzungen: Schritte 2, 3, 4; `IGitWorkspaceBrowserService`, `ITextDiffService`.
   - Beschreibung: Zustand (Baum, Commits, Auswahl, Inhalt, Diff, Modus) + Commands `StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand`, `CommitAufklappenAsync`, `InitialisierenAsync`.

6. **`DiffLineStatusToBrushConverter` anlegen und in `App.xaml` registrieren**
   - Voraussetzungen: `DiffLineStatus` (vorhanden).
   - Beschreibung: Konverter + Ressourceneintrag; Brushes grün/rot/orange/transparent.

7. **`DiffViewer`-UserControl anlegen**
   - Voraussetzungen: Schritt 1 (`TextDiffLine`), Schritt 6 (Converter).
   - Beschreibung: `ItemsControl` mit Zeilennummernspalte, Hintergrund je Status, Inline-`Run`s je `InlineDiffSegment`; DependencyProperty `Lines`.

8. **`FileExplorerView`-UserControl anlegen**
   - Voraussetzungen: Schritt 5 (`FileExplorerViewModel`), Schritt 7 (`DiffViewer`).
   - Beschreibung: Grid mit Mode-Buttons (Standard/Vergleich/Aktualisieren), `TreeView` (`HierarchicalDataTemplate`), `GridSplitter`, rechter Read-Only-Text bzw. `DiffViewer`.

9. **`TaskDetailViewModel` erweitern**
   - Voraussetzungen: Schritt 5 (`FileExplorerViewModel`).
   - Beschreibung: `DetailAnsicht.Dateibrowser`, `IsFileExplorerViewSelected`, `ShowFileExplorerPanel`, `DateiViewCommand`, `FileExplorer`-Property, Konstruktor-Abhängigkeit, `WaehleAnsicht`- und `Aufgabe`-Setter-Anpassung.

10. **`TaskDetailView.xaml` erweitern**
    - Voraussetzungen: Schritt 8 (`FileExplorerView`), Schritt 9.
    - Beschreibung: „Dateien"-Button + `FileExplorerView`-Panel einbinden.

11. **DI-Registrierungen in `App.xaml.cs` ergänzen**
    - Voraussetzungen: Schritte 3, 4, 5.
    - Beschreibung: `IGitWorkspaceBrowserService`, `ITextDiffService`, `FileExplorerViewModel` registrieren.

12. **`TaskDetailViewModelTestFactory` aktualisieren**
    - Voraussetzungen: Schritt 9.
    - Beschreibung: Neue Abhängigkeit (`FileExplorerViewModel` mit gemockten Diensten) im Factory-Aufbau bereitstellen.

13. **Unit-Tests schreiben**
    - Voraussetzungen: Schritte 3, 4, 5, 9, 12.
    - Beschreibung: Tests für `TextDiffService`, `LoadWorkingTreeAsync`, `FileExplorerViewModel`, `TaskDetailViewModel`-Erweiterung.

14. **E2E-Test schreiben**
    - Voraussetzungen: Schritte 10, 11.
    - Beschreibung: Umschalten auf „Dateien", Sichtbarkeit von Baum und Mode-Buttons prüfen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|------------|-------------------------------------|
| `BuildDiff_IdentischerInhalt_LiefertNurContextZeilen` | `TextDiffServiceTests` | Gleicher Alt-/Neu-Inhalt → alle Zeilen `Context`. |
| `BuildDiff_HinzugefuegteZeile_LiefertAddedStatus` | `TextDiffServiceTests` | Neue Zeile im Zielinhalt → `Added` mit `NewLineNumber`, ohne `OldLineNumber`. |
| `BuildDiff_GeloeschteZeile_LiefertRemovedStatus` | `TextDiffServiceTests` | Entfernte Zeile → `Removed` mit `OldLineNumber`, ohne `NewLineNumber`. |
| `BuildDiff_GeaenderteZeile_LiefertModifiedMitInlineSegmenten` | `TextDiffServiceTests` | Geänderte Zeile → `Modified`; Inline-Segmente markieren nur geänderten Wortteil (`IsChanged`). |
| `BuildDiff_LeererInhalt_KeineException` | `TextDiffServiceTests` | Leerer Alt- oder Neu-Inhalt liefert korrekten Diff ohne Ausnahme. |
| `LoadWorkingTreeAsync_ListetDateienUndVerzeichnisse` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Vollständiger Baum aus temporärem Repo; verschachtelte Verzeichnisse korrekt. |
| `LoadWorkingTreeAsync_SchliesstGitVerzeichnisAus` | `GitWorkspaceBrowserServiceWorkingTreeTests` | `.git` erscheint nicht im Baum. |
| `LoadWorkingTreeAsync_UngueltigerPfad_LiefertLeerOderFehler` | `GitWorkspaceBrowserServiceWorkingTreeTests` | Nicht existierender Pfad → definiertes, leeres/fehlerbehaftetes Ergebnis. |
| `Standard_LaedtWurzelknotenUeberWorkingTree` | `FileExplorerViewModelTests` | `InitialisierenAsync` im Standardmodus ruft `LoadWorkingTreeAsync` und füllt `Wurzelknoten`. |
| `DateiAuswahl_Standard_SetztDateiInhaltAusPreview` | `FileExplorerViewModelTests` | Auswahl eines Datei-Knotens → `LoadPreviewAsync`, `DateiInhalt` = `CurrentContent`. |
| `DateiAuswahl_BinaerOderZuGross_ZeigtHinweis` | `FileExplorerViewModelTests` | `FilePreview.IsBinary`/`IsTooBig` → Hinweistext statt Inhalt. |
| `VergleichCommand_LaedtCommitsAusSnapshot` | `FileExplorerViewModelTests` | Wechsel in Vergleich → `LoadSnapshotAsync`, `CommitGruppen` gefüllt. |
| `CommitAufklappen_LaedtGeaenderteDateien` | `FileExplorerViewModelTests` | Aufklappen eines Commits → `LoadCommitFilesAsync`, `Files` gefüllt, `ChildrenLoaded`. |
| `DateiAuswahl_Vergleich_ErzeugtDiffZeilen` | `FileExplorerViewModelTests` | Auswahl einer Commit-Datei → `LoadCommitPreviewAsync` + `ITextDiffService.BuildDiff`, `DiffZeilen` gefüllt. |
| `AktualisierenCommand_LaedtAktuellenModusNeu` | `FileExplorerViewModelTests` | Refresh lädt je nach Modus `LoadWorkingTreeAsync`/`LoadSnapshotAsync` neu. |
| `DateiViewCommand_SetztFileExplorerAnsicht` | `TaskDetailViewModelTests` (Erweiterung) | `DateiViewCommand` → `IsFileExplorerViewSelected == true`. |
| `ShowFileExplorerPanel_NurBeiVorhandenemKlonPfad` | `TaskDetailViewModelTests` (Erweiterung) | Panel/Button nur sichtbar, wenn `LokalerKlonPfad` gesetzt/vorhanden. |
| `Create` (Erweiterung) | `TaskDetailViewModelTestFactory` | Liefert `TaskDetailViewModel` inkl. neuer `FileExplorerViewModel`-Abhängigkeit mit gemockten Diensten. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTestFactory` | Konstruktorsignatur von `TaskDetailViewModel` erhält zusätzliche Abhängigkeit `FileExplorerViewModel`. |
| `TaskDetailViewModelTests`, `TaskDetailViewModelTests_ZeitgesteuerterPrompt` | Nutzen die Factory; kompilieren nach Factory-Anpassung ohne inhaltliche Änderung (nur falls direkt der Konstruktor aufgerufen wird). |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| „Dateien"-Register umschalten und Explorer-Oberfläche (Baum + Mode-Buttons) anzeigen | `E2E_FileExplorer` (`src/Softwareschmiede.Tests/E2E/E2E_FileExplorer.cs`) | Neues Dateiexplorer-Register in der `TaskDetailView`; Split-View mit Baum und Standard/Vergleich-Umschaltung ist erreichbar. |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine. (`E2E_TaskDetailNavigation` bleibt gültig; das neue Register ist additiv.)

## Offene Punkte

Keine. Die drei zuvor offenen Punkte (Tiefe des Inline-Highlightings, Syntax-Highlighting, Eager-/Lazy-Aufbau des Standard-Baums) wurden beantwortet und sind als Designentscheidungen bzw. als Folgeaufgaben im Abschnitt „Nicht im Umsetzungsumfang (Folgeaufgaben)" eingearbeitet.
