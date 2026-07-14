# Übersetzte Anforderung: Dateiexplorer für Aufgabendetailansicht

## Fachliche Zusammenfassung

Nach dem Klonen eines Task-Repositories wird ein neues Dateiexplorer-Register in der `TaskDetailView` angeboten, das dem vorhandenen CLI- und Diff-Register hinzugefügt wird. Der Explorer zeigt eine split-view-Architektur mit links einem Verzeichnis-/Dateibaum und rechts dem Inhalt der ausgewählten Datei. Oberhalb des Baums sind Mode-Umschalt-Buttons (Standardansicht vs. Vergleichsmodus) platziert. Der Vergleichsmodus zeigt nur im Branch modifizierte Dateien (neu, geändert, gelöscht), gruppiert nach Commits; für geänderte Dateien wird eine neue WPF-Diff-Komponente mit farblicher Zeilenkennzeichnung (grün = neu, rot = gelöscht, orange = geändert mit Inline-Highlighting) genutzt.

## Betroffene Klassen und Komponenten

### ViewModels und Services
- **Geänderte Klasse:** `TaskDetailViewModel` — erweitert um neues enum-Wert `Dateibrowser` in `DetailAnsicht`; neue Properties für den ausgewählten Modus (`AktuellerDateibrowserModus` als enum `DateibrowserAnsichtsmodus` mit Werten `Standard`, `Vergleich`), Dateiauswahl (`AusgewaehlteDatui` als Relative Path), Dateiinhalt (`DateiInhalt`), sowie Commands für Modus-Umschaltung (`StandardAnsichtCommand`, `Vergleichsmodus Ansicht Command`); delegiert Dateioperationen an neue Services.
- **Neuer Service:** `DateibrowserService` — lädt Verzeichnisstruktur aus der geklonten Repository (File-System), verwaltet Dateiinhalt-Caching, und liefert bei Vergleichsmodus eine gefilterte Liste von geänderten Dateien pro Commit (nutzt Git diff-API, z. B. `git diff --name-status origin/main..HEAD` oder ähnlich, gruppiert das Ergebnis nach Commits).
- **Neuer Service:** `GitDiffParserService` — parst Git-Diff-Output und liefert strukturierte Informationen über Dateiänderungen (ChangeType: Added/Modified/Deleted, alte/neue Inhalte); wird von `DateibrowserService` genutzt.

### UI-Komponenten
- **Neue UserControl:** `FileExplorerView` (unter `src/Softwareschmiede.App/Views/`) — wird wie `TerminalControl` in `TaskDetailView` platziert und über Binding `Visibility="{Binding IsFileExplorerViewSelected, Converter={StaticResource BoolToVisibilityConverter}}"` an die ausgewählte Ansicht gebunden.
- **Neue UserControl:** `DiffViewer` (unter `src/Softwareschmiede.App/Controls/`, wiederverwendbar) — zeigt zwei Textinhalte (alt/neu) nebeneinander oder zeilenweise mit Diff-Highlighting:
  - Jede Zeile ist als `TextBlock` oder `DiffLine`-Modell mit Properties `Content` (Zeilentext), `ChangeType` (Added/Removed/Modified/Unchanged), `InlineChanges` (für modifizierte Zeilen: Liste von Spans mit alten/neuen Textabschnitten).
  - Farbschema: grüner Hintergrund (z. B. `#E8F5E9` hell, `#81C784` Text) für Added, roter Hintergrund (`#FFEBEE` hell, `#E57373` Text) für Removed, orange (`#FFF3E0` hell, `#FFB74D` Text) für Modified mit Inline-Highlighting der geänderten Wortteile.
  - Syntax-Highlighting für gängige Formate (Code, Markup) optional, aber nicht zwingend in Schritt 1.
- **Geänderte View:** `TaskDetailView` — neue `<Button>` für Dateiexplorer in der Ansicht-Umschalt-Gruppe (neben Info/CLI/Diff), neuer `<ScrollViewer>`/Grid-Abschnitt für `FileExplorerView` (analog zu CLI/Diff).

### Datenmodelle/Enums
- **Neues Enum:** `DateibrowserAnsichtsmodus` (z. B. in `src/Softwareschmiede.App/ViewModels/` oder Domain-Enums) mit Werten: `Standard`, `Vergleich`.
- **Neuer Typ:** `FileTreeNode` (ViewModel oder DTO) — Datenmodell für Baum-Items mit Properties: `Name`, `Path`, `IsDirectory`, `Children` (ObservableCollection), `Icon` (optional für visuelle Unterscheidung).
- **Neuer Typ:** `DiffLine` — ViewModel für eine Zeile im Diff-Viewer mit Properties: `Content` (Zeilentext), `LineNumber` (alt/neu), `ChangeType` (AddedLine/RemovedLine/ModifiedLine/UnchangedLine), `InlineChanges` (List<(Start, End, IsChange)> für Inline-Spans).
- **Neuer Typ:** `CommitDiffGroup` — gruppiert geänderte Dateien nach Commit mit Properties: `CommitHash`, `CommitMessage`, `Files` (List<FileChange>).
- **Neuer Typ:** `FileChange` — Representation einer einzelnen Dateiänderung mit Properties: `Path`, `ChangeType` (Added/Modified/Deleted), `OldContent` (optional), `NewContent` (optional).

### Tests
- Unit-Tests für `DateibrowserService` — Verzeichnisladung, Dateifilterung im Vergleichsmodus, Edge-Cases (leere Repos, Binärdateien, große Dateien).
- Unit-Tests für `GitDiffParserService` — korrekte Parsing von `git diff`-Output, Gruppierung nach Commits.
- Optional E2E/UI-Tests für `FileExplorerView` — Baum-Navigation, Dateiauswahl, Modus-Umschaltung, DiffViewer-Rendering.

## Implementierungsansatz

### Architektur und Integration
- **Erweiterungsmuster:** `DetailAnsicht`-enum in `TaskDetailViewModel` erhält neuen Wert `Dateibrowser`; neue boolean-Property `IsFileExplorerViewSelected` folgt dem Pattern von `IsCliViewSelected`, `IsDiffViewSelected`.
- **Service-Injection:** `DateibrowserService` und `GitDiffParserService` werden in `TaskDetailViewModel` als Konstruktor-Abhängigkeiten injiziert (via DI-Container in `App.xaml.cs`).
- **Datei-Caching:** `DateibrowserService` cached das Verzeichnisbaum-Modell pro Repository (identifiziert über `Aufgabe.RepositoryPath`) und invalidiert es ggf. bei Benutzeraktion (Refresh-Button).
- **Mode-Switch:** Umschalten zwischen `Standard` und `Vergleich` über Commands (`StandardAnsichtCommand`, `VergleichCommand`) im ViewModel; Binding der `FileExplorerView.Mode`-Property triggert Neuladung des Datei-Filterung im Service.
- **Datei-Inhalt:** Beim Auswählen einer Datei im Baum wird der Inhalt async geladen (mit Spinner/Placeholder) und gebunden an Property `DateiInhalt`. Im Vergleichsmodus wird zusätzlich der alte Inhalt geladen und an `DiffViewer` übergeben.
- **Git-Integration:** `GitDiffParserService` nutzt das bereits vorhandene `IScmProvider`/Plugin-Mechanismus (via `_pluginManager`) oder direkten `Process`-Call zu `git diff` in RepositoryPath, um Änderungen zu ermitteln.

### FileExplorerView XAML-Struktur (Skizze)
```
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />  <!-- Mode-Buttons -->
    <RowDefinition Height="*" />     <!-- Split-View -->
  </Grid.RowDefinitions>
  
  <!-- Oberste Reihe: Mode-Toggle-Buttons -->
  <StackPanel Grid.Row="0" Orientation="Horizontal">
    <Button Content="☐ Standard" Command="{Binding StandardAnsichtCommand}" />
    <Button Content="≍ Vergleich" Command="{Binding VergleichCommand}" />
    <Button Content="↻ Aktualisieren" Command="{Binding AktualisierenCommand}" />
  </StackPanel>
  
  <!-- Split-View: Baum + Dateiinhalt -->
  <Grid Grid.Row="1">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="250" />  <!-- Baum, min. Breite -->
      <ColumnDefinition Width="Auto" /> <!-- Splitter -->
      <ColumnDefinition Width="*" />    <!-- Inhalt -->
    </Grid.ColumnDefinitions>
    
    <!-- Baum: TreeView oder ItemsControl mit hierarchischen Items -->
    <ScrollViewer Grid.Column="0">
      <controls:FileTreeControl ItemsSource="{Binding Wurzelbaume}" />
    </ScrollViewer>
    
    <!-- Splitter (optional) -->
    <GridSplitter Grid.Column="1" ... />
    
    <!-- Rechts: Datei-Inhalt oder DiffViewer -->
    <Grid Grid.Column="2">
      <!-- Standard-Ansicht: TextBlock oder TextBox (Read-Only) -->
      <ScrollViewer Visibility="{Binding IsStandardModeSelected, ...}">
        <TextBlock Text="{Binding DateiInhalt}" FontFamily="Consolas" />
      </ScrollViewer>
      
      <!-- Vergleichs-Ansicht: DiffViewer -->
      <controls:DiffViewer Lines="{Binding DiffLines}"
                            Visibility="{Binding IsVergleichsModeSelected, ...}" />
    </Grid>
  </Grid>
</Grid>
```

### DiffViewer ItemsControl / Rendering
- `DiffViewer` ist eine `ItemsControl` mit `DiffLine`-Items und ItemTemplate, die für jede Zeile einen Container (z. B. `Border` + `TextBlock`) rendered.
- Pro Zeile: TextBlock mit `Background`-Binding (konvertiert `ChangeType` → Farbe) und optional Inline-Runs für Highlight-Spans bei modifizierten Zeilen.
- Zeilennummern optional (Links-Column mit Nummern).

## Konfiguration

Kein endbenutzer-sichtbares/konfigurierbares Verhalten. Der Dateiexplorer ist ein statischer Teil der `TaskDetailView`, sobald ein Repository geklont vorliegt.

**Optional zukünftig:** Benutzerpräferenz für Splitter-Größe (TreeView-Breite) speichern, Default-Modus speichern (Standard vs. Vergleich).

## Offene Fragen

1. **Repository-Zugriff:** Wie wird der Pfad zum geklonten Repository aus der `Aufgabe` ermittelt? Via `Aufgabe.RepositoryPath`, `Aufgabe.ArbeitVerzeichnis`, oder über den `IScmProvider`/Plugin-Mechanismus?

2. **Git-Diff-Basis:** Gegen welchen Branch wird die Diff gemessen? Gegen `origin/main`/`main`/`upstream/main`? Oder wird der User aufgefordert, die Basis-Branch auszuwählen?

3. **Dateigrößenbeschränkung:** Sollen sehr große Dateien (z. B. > 5 MB) mit Warnung/Platzhalter behandelt werden, oder ist „Alles laden" erwünscht?

4. **Binärdateien:** Wie soll der Dateiexplorer mit Binärdateien umgehen? Anzeige wie „[Binärdatei]" vs. Gar nicht auflisten vs. Hexdump?

5. **Inline-Highlighting bei Diffs:** Ist die erwähnte Inline-Highlighting von geänderten Wortteilen in modifizierten Zeilen (z. B. via Longest Common Subsequence / Levenshtein-Distance) gewünscht, oder reicht eine Zeilengranularität (ganze Zeile = geändert)?

6. **Commit-Gruppierung im Vergleichsmodus:** Sollen die Commits in chronologischer Reihenfolge angezeigt werden, oder expandierbar als Tree (Commit → Dateien) mit UI zur Filterung?

7. **File-Tree-Rendering:** TreeView (native WPF) vs. hierarchisches ItemsControl? TreeView ist bewährter, aber custom-Styling benötigt mehr Setup.

8. **Speicherung von Moduswahl:** Soll die zuletzt gewählte Ansicht (Standard/Vergleich) pro Task gespeichert werden, oder zurückgesetzt beim Neustart?

9. **Refresh-Verhalten:** Soll das Verzeichnis beim Öffnen des Explorers automatisch aktualisiert werden, oder nur auf Button-Click?

10. **Diff-Parser Implementierung:** Wird Git-CLI direkt aufgerufen (`git diff ...`), oder wird das bestehende SCM-Plugin-Interface (`IScmProvider`) erweitert?
