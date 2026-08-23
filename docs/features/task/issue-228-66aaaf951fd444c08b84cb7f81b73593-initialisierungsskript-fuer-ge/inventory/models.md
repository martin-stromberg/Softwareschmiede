# Datenmodelle

## `GitRepository`

Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Repositories |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `PluginTyp` | `string` | Plugin-Typ, z.B. "GitHub", "Bitbucket" |
| `RepositoryUrl` | `string` | URL des Repositories |
| `RepositoryName` | `string` | Name des Repositories |
| `Aktiv` | `bool` | Gibt an, ob das Repository aktiv ist (Default: `true`) |
| `DefaultSourceBranchName` | `string?` | Konfigurierter Basis-Branch; `null` bedeutet Remote-Standard-Branch wird verwendet |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startkonfiguration für Repository-Startskripte (Navigationseigenschaft) |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für dieses Repository |

**Bemerkungen:**
- Aktuell existiert keine `InitialisierungsskriptRelativePfad` Property — dies ist eine der offenen Fragen der Anforderung
- Hat bereits `StartKonfiguration` Navigationseigenschaft als Vorbild für die zu implementierende `InitialisierungKonfiguration`

## `RepositoryStartKonfiguration`

Datei: `src/Softwareschmiede/Domain/Entities/RepositoryStartKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Konfiguration |
| `GitRepositoryId` | `Guid` | Referenz zum zugehörigen Repository |
| `StartScriptRelativePath` | `string` | Relativer Pfad zum Startskript im Repository |
| `WorkingDirectoryRelativePath` | `string?` | Relativer Pfad zum Arbeitsverzeichnis innerhalb des Repositories; `null` bedeutet Repository-Root |
| `Aktiv` | `bool` | Gibt an, ob die Startkonfiguration aktiv verwendet wird (Default: `true`) |
| `GitRepository` | `GitRepository` | Navigationseigenschaft zum Repository |

**Bemerkungen:**
- Diese Entität dient als Architektur-Vorbild für die zu implementierende `RepositoryInitialisierungKonfiguration`
- Separate Entität für zusätzliche Konfigurationsflexibilität
- Hat `Aktiv` Schalter zur Aktivierung/Deaktivierung
- Wird über `RepositoryStartskriptService` verarbeitet
