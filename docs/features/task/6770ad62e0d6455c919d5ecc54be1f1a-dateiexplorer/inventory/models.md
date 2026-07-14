# Datenmodelle

## `FileTreeNode`
Datei: `src/Softwareschmiede/Domain/ValueObjects/FileTreeNode.cs`

ValueObject zur Repräsentation von Knoten in einer Dateibaum-Hierarchie. Wird für externe Repositories (Agentenpaket-Strukturen) verwendet.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Anzeigename des Knotens (Datei-/Verzeichnisname) |
| `RelativePath` | `string` | Pfad relativ zum Paket-Root; leer für Root selbst |
| `PackageName` | `string` | Name des zugehörigen Agentenpakets |
| `IsDirectory` | `bool` | Gibt an, ob der Knoten ein Verzeichnis ist |
| `IsPackageRoot` | `bool` | `true` wenn RelativePath leer und IsDirectory `true` (berechnete Eigenschaft) |
| `IsExpanded` | `bool` | Gibt an, ob der Knoten aufgeklappt angezeigt wird |
| `Children` | `List<FileTreeNode>` | Unterknoten dieses Verzeichnisses |

## `DiffLine`
Datei: `src/Softwareschmiede/Domain/Entities/DiffLine.cs`

Database Entity für eine einzelne Zeile innerhalb eines Diff-Blocks. Speichert die Änderungsinformation auf Zeilenebene.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Diff-Zeile |
| `DiffBlockId` | `Guid` | ID des zugehörigen Diff-Blocks (Fremdschlüssel) |
| `LineStatus` | `DiffLineStatus` | Status der Zeile (Added, Removed, Modified, Context) |
| `Content` | `string` | Inhalt der Zeile (Quellcode, Whitespace wird erhalten) |
| `SourceLineNumber` | `int?` | Zeilennummer in der Quelldatei; `null` für Added-Zeilen |
| `TargetLineNumber` | `int?` | Zeilennummer in der Zieldatei; `null` für Removed-Zeilen |
| `LineSequence` | `int` | Reihenfolge der Zeile im Block (für korrekte Sortierung) |
| `DiffBlock` | `DiffBlock` | Navigationseigenschaft zum zugehörigen Diff-Block |

**Hinweis:** Dies ist ein Persistierungs-Model (Database Entity). Für die UI-Anzeige im Dateiexplorer benötigen wir zusätzlich Modelle für Diff-Linien mit Inline-Highlighting.

## `DiffResult`
Datei: `src/Softwareschmiede/Domain/Entities/DiffResult.cs`

Database Entity für ein generiertes Diff-Ergebnis mit persistierten Daten zu Dateiänderungen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Diff-Ergebnisses |
| `AufgabeId` | `Guid` | ID der zugehörigen Aufgabe |
| `GitRepositoryId` | `Guid?` | Optionale ID des Git-Repositories |
| `FilePath` | `string` | Pfad der geänderten Datei |
| `SourceVersion` | `string` | Quell-Versions-Bezeichner (z. B. Commit-Hash, Tag) |
| `TargetVersion` | `string` | Ziel-Versions-Bezeichner |
| `SourceContent` | `string?` | Inhalt der Quelldatei (inline, wenn ≤ 100 KB) |
| `TargetContent` | `string?` | Inhalt der Zieldatei (inline, wenn ≤ 100 KB) |
| `DiffType` | `DiffType` | Rendering-Typ (Full, SideBySide, Split) |
| `Status` | `DiffResultStatus` | Status der Generierung (Pending, Generated, Cached, Error) |
| `AddedLines` | `int` | Anzahl hinzugefügter Zeilen |
| `RemovedLines` | `int` | Anzahl entfernter Zeilen |
| `ModifiedLines` | `int` | Anzahl modifizierter Zeilen |
| `LineCount` | `int` | Gesamtanzahl (Added + Removed + Modified) |
| `GeneratedAt` | `DateTimeOffset` | Zeitstempel der Generierung |
| `GeneratedBy` | `string` | Name des Services/Generators |
| `ExpiresAt` | `DateTimeOffset` | Cache-Ablauf-Zeit (TTL) |
| `DiffBlocks` | `List<DiffBlock>` | Unterblöcke des Diffs |

**Hinweis:** Diese Entity speichert komplette Diffs. Der Dateiexplorer wird zusätzliche Modelle für die Anzeige von Dateiänderungen benötigen (z. B. gefiltert nach Commit, mit Gruppierung).
