# Datenmodelle

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Repositories. |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts. |
| `PluginTyp` | `string` | Plugin-Typ, z.B. "GitHub", "LocalDirectoryPlugin". |
| `RepositoryUrl` | `string` | URL des Repositories. |
| `RepositoryName` | `string` | Name des Repositories. |
| `Aktiv` | `bool` | Gibt an, ob das Repository aktiv ist. |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startkonfiguration für Repository-Startskripte und Arbeitsverzeichnis. |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt. |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für dieses Repository. |

**Bemerkung:** Die Eigenschaft `StartKonfiguration` enthält bereits die Arbeitsverzeichnis-Konfiguration über die `RepositoryStartKonfiguration`-Klasse.

---

## `RepositoryStartKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/RepositoryStartKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Konfiguration. |
| `GitRepositoryId` | `Guid` | Referenz auf das zugehörige Repository. |
| `StartScriptRelativePath` | `string` | Relativer Pfad zum Startskript im Repository. |
| `WorkingDirectoryRelativePath` | `string?` | Relativer Pfad zum Arbeitsverzeichnis innerhalb des Repositories; `null` bedeutet Repository-Root. |
| `Aktiv` | `bool` | Gibt an, ob die Startkonfiguration aktiv verwendet wird. |
| `GitRepository` | `GitRepository` | Navigationseigenschaft zum Repository. |

**Bemerkung:** Das Feld `WorkingDirectoryRelativePath` existiert bereits und wird durch eine Migration (`20260708181234_202607080001_AddWorkingDirectoryToRepositoryStartKonfiguration`) persistiert. Es ist vom Typ `string?` (nullable), und `null` wird als Repository-Root interpretiert.
