# Bestandsaufnahme: Lazy-Loading des Verzeichnisbaums

Analyse des bestehenden Projektcodes bezüglich der Anforderung zur Optimierung des Verzeichnis-Ladeverhaltens von vollständigem Laden auf Lazy-Loading mit progressiver Tiefenentwicklung.

## Zusammenfassung

Die Codebase hat bereits folgende Komponenten vorhanden:
- **Datenmodell** `WorkspaceFileNode` mit `ChildrenLoaded`-Flag (entspricht der geforderten `IsChildrenLoaded`-Semantik)
- **Service-Interface** `IGitWorkspaceBrowserService` mit `LoadWorkingTreeAsync` (lädt derzeit unbegrenzt)
- **Service-Implementierung** mit rekursiver Directory-Walk-Logik
- **ViewModel** `FileExplorerViewModel` mit Standardmodus-Unterstützung
- **UI-Komponente** `FileExplorerView` mit TreeView und HierarchicalDataTemplate

**Kritische Lücken:**
1. `WorkspaceFileNode` fehlt Property `Depth`
2. `IGitWorkspaceBrowserService` fehlt Methode `LoadSubtreeAsync` zum Nachladen einzelner Ebenen
3. `LoadWorkingTreeAsync` hat keinen `maxInitialDepth`-Parameter
4. `FileExplorerViewModel` fehlt Methode `LadeKinderAsync` für Lazy-Loading beim Aufklappen
5. `FileExplorerView` StandardBaum hat keinen `Expanded`-Event-Handler für Trigger des Lazy-Ladens
6. Cleanup-Methode `BeraeumeKnoten` für Collapse-Scenario nicht vorhanden

## Details

- [Datenmodelle](inventory/models.md)
- [Service-Interfaces](inventory/interfaces.md)
- [Service-Implementierung](inventory/logic.md)
- [ViewModel](inventory/viewmodels.md)
- [UI-Komponenten](inventory/ui.md)
- [Tests](inventory/tests.md)
