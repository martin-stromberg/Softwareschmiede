# Datenmodell

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID des Repositories |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `PluginTyp` | `string` | Plugin-Typ, z.B. "GitHub" |
| `RepositoryUrl` | `string` | URL des Repositories |
| `RepositoryName` | `string` | Name des Repositories |
| `Aktiv` | `bool` | Gibt an, ob das Repository aktiv ist (default: true) |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startkonfiguration (Navigationseigenschaft) |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für dieses Repository |

**Fehlend:** Eigenschaft für konfigurierten Basis-Branch (z.B. `DefaultSourceBranchName: string?`)

## `RepositoryStartKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/RepositoryStartKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID der Konfiguration |
| `GitRepositoryId` | `Guid` | Referenz auf das zugehörige Repository |
| `StartScriptRelativePath` | `string` | Relativer Pfad zum Startskript im Repository |
| `WorkingDirectoryRelativePath` | `string?` | Relativer Pfad zum Arbeitsverzeichnis; `null` = Repository-Root |
| `Aktiv` | `bool` | Gibt an, ob die Startkonfiguration aktiv ist (default: true) |
| `GitRepository` | `GitRepository` | Navigationseigenschaft zum Repository |

**Fehlend:** Eigenschaft für Basis-Branch-Konfiguration (könnte hier oder in `GitRepository` ergänzt werden)
