# Datenmodelle

## `WorkspaceFileNode`
Datei: `src/Softwareschmiede/Domain/ValueObjects/WorkspaceFileNode.cs`

Sealed Value Object, das einen Eintrag im Workspace-Browser darstellt.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Anzeigename des Knotens (Datei-/Verzeichnisname). Init-only. |
| `RelativePath` | `string` | Pfad relativ zum Repository-Root. Init-only. |
| `IsDirectory` | `bool` | Flag: Ist der Knoten ein Verzeichnis? Init-only. |
| `IsDeleted` | `bool` | Flag: Ist der Knoten gelöscht (im Staging/Git-Status). Init-only. Default: `false`. |
| `SourceRelativePath` | `string?` | Ursprünglicher Pfad bei Rename/Copy-Operationen. Init-only. Optional. |
| `Status` | `WorkspaceFileStatus?` | Git-Status des Knotens (Modified, Added, Deleted, etc.). Init-only. Optional. |
| `CommitSha` | `string?` | Optionaler Commit-Hash, wenn Knoten aus einem Commit-Baum stammt (Vergleichsmodus). Init-only. Optional. |
| `IsExpanded` | `bool` | Flag: Wird das Verzeichnis aufgeklappt angezeigt? Read-write. Default: `false`. |
| `ChildrenLoaded` | `bool` | **Flag: Wurden die direkten Kinder bereits geladen?** Read-write. Default: `false`. (Entspricht der Anforderung `IsChildrenLoaded`, aber mit anderem Namen) |
| `Children` | `List<WorkspaceFileNode>` | Unterknoten (Verzeichnisse und Dateien unterhalb dieses Knotens). Init-only. Default: leere Liste. |

**Fehlend (laut Anforderung):**
- `Depth : int` — Ebene des Knotens relativ zur Wurzel (0 = Wurzel, 1 = direkte Kinder, etc.). Wird für Lazy-Loading und Cleanup-Logik benötigt.

**Verwandtschaft:**
- `BranchCommit` hat analoges `ChildrenLoaded`-Flag für Commit-Baum im Vergleichsmodus.
