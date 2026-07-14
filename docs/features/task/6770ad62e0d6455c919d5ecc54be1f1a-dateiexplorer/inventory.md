# Bestandsaufnahme: Dateiexplorer für Aufgabendetailansicht

Analyse des bestehenden Codes bezüglich der Anforderung „Dateiexplorer für Aufgabendetailansicht". Der Dateiexplorer zeigt eine split-view-Architektur mit Verzeichnis-/Dateibaum und Dateiinhaltsanzeige, mit Modi für Standardansicht und Vergleichsmodus.

## Zusammenfassung

**Vorhanden:**
- `TaskDetailViewModel` mit enum `DetailAnsicht` (Info, Cli, Diff), aber ohne Dateibrowser-Wert
- `FileTreeNode` (ValueObject) für Baum-Repräsentation
- `DiffLine` (Domain Entity) für Diff-Zeilen in der Datenbank
- `DiffService`, `DiffAlgorithmService`, `DiffCachingService` für Diff-Verwaltung
- `DirectoryStructureBrowserService` für Laden von Repository-Strukturen
- `GitWorkspaceBrowserService` für Git-Workspace-Verwaltung
- Enums: `DiffLineStatus`, `DiffType`, `DiffResultStatus`
- `TaskDetailView.xaml` mit Platzhalter für Diff-Ansicht

**Nicht vorhanden (noch zu implementieren):**
- `DateibrowserAnsichtsmodus` enum
- `DateibrowserService`
- `GitDiffParserService`
- `CommitDiffGroup` Datenmodell
- `FileChange` Datenmodell
- `FileExplorerView` UserControl
- `DiffViewer` UserControl
- Properties und Commands in `TaskDetailViewModel` für Dateibrowser-Funktionalität
- Tests für neue Services und UI-Komponenten

## Details

- [Datenmodelle](inventory/models.md)
- [Logik und Services](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
