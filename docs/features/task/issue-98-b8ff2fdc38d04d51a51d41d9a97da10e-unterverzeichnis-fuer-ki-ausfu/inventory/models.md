# Datenmodelle

## `RepositoryStartKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/RepositoryStartKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Status |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Konfiguration — vorhanden |
| `GitRepositoryId` | `Guid` | Referenz auf das zugehörige Repository — vorhanden |
| `StartScriptRelativePath` | `string` | Relativer Pfad zum Startskript — vorhanden |
| `Aktiv` | `bool` | Gibt an, ob die Konfiguration aktiv ist — vorhanden |
| `GitRepository` | `GitRepository` | Navigationseigenschaft — vorhanden |
| `WorkingDirectoryRelativePath` | `string?` | **FEHLT** — Relativer Pfad zum Arbeitsverzeichnis (nullable, Default: null für Repository-Root) |

### DbContext-Konfiguration
Datei: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs` (Zeilen 94–102)

Die Konfiguration der Entity ist vorhanden, aber:
- Property `WorkingDirectoryRelativePath` ist nicht konfiguriert
- MaxLength-Beschränkung fehlt (sollte ähnlich wie `StartScriptRelativePath` auf 512 begrenzt werden)

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs` (indirekt referenziert)

| Relation | Beschreibung |
|----------|-------------|
| `StartKonfiguration` | Navigation zu `RepositoryStartKonfiguration` — vorhanden |

Das Modell selbst benötigt keine neuen Properties; die Arbeitsverzeichnis-Konfiguration ist Bestandteil von `RepositoryStartKonfiguration`.
