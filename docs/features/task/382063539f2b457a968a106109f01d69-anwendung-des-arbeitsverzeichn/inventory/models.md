# Datenmodelle (Entities)

## `RepositoryStartKonfiguration`

Datei: `src/Softwareschmiede/Domain/Entities/RepositoryStartKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | Guid | Eindeutige ID der Konfiguration |
| `GitRepositoryId` | Guid | Referenz auf das zugehörige Repository |
| `StartScriptRelativePath` | string | Relativer Pfad zum Startskript im Repository |
| `WorkingDirectoryRelativePath` | string? | **Für Anforderung zentral:** Relativer Pfad zum Arbeitsverzeichnis innerhalb des Repositories; `null` bedeutet Repository-Root wird verwendet |
| `Aktiv` | bool | Gibt an, ob die Konfiguration aktiv verwendet wird (Standard: true) |
| `GitRepository` | GitRepository | Navigationseigenschaft zum zugehörigen Repository |

---

## `Aufgabe`

Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | Guid | Eindeutige ID der Aufgabe |
| `LokalerKlonPfad` | string? | Lokaler Dateisystempfad des geklonten Repositories (Repository-Root) |
| `GitRepositoryId` | Guid? | Optionale ID des verknüpften Git-Repositories |
| `GitRepository` | GitRepository? | Navigationseigenschaft zum verknüpften Repository (enthält Repositories und deren RepositoryStartKonfigurationen) |

**Hinweis:** Zur Ermittlung der `RepositoryStartKonfiguration` muss über die Navigation `Aufgabe.GitRepository.Repositories` die richtige Konfiguration gefunden werden.
