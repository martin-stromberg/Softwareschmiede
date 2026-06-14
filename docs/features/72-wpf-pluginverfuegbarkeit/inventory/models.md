# Bestandsaufnahme: Datenmodelle

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Repositories |
| `ProjektId` | `Guid` | Verweis auf das übergeordnete Projekt |
| `PluginTyp` | `string` | Plugin-Typ, z.B. "GitHub", "LocalDirectoryPlugin" |
| `RepositoryUrl` | `string` | URL des Repositories (oder lokaler Pfad für LocalDirectory) |
| `RepositoryName` | `string` | Anzeigename des Repositories |
| `Aktiv` | `bool` | Gibt an, ob das Repository aktiv ist (Standard: true) |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startkonfiguration mit Skriptpfad |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `DiffResults` | `List<DiffResult>` | Zugeordnete Diff-Ergebnisse (optional) |

## `Projekt`
Datei: `src/Softwareschmiede/Domain/Entities/Projekt.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Projekts |
| `Name` | `string` | Name des Projekts |
| `Beschreibung` | `string?` | Optionale Beschreibung des Projekts |
| `ErstellungsDatum` | `DateTimeOffset` | Zeitstempel der Erstellung |
| `Status` | `ProjektStatus` | Aktueller Status (z.B. Aktiv, Archiviert) |
| `Repositories` | `List<GitRepository>` | Zugeordnete Repositories (Navigation) |
| `Aufgaben` | `List<Aufgabe>` | Aufgaben des Projekts (Navigation) |

## Hinweise

- `GitRepository.PluginTyp` ist derzeit ein einfacher String; wird von `ProjektService` zur Validierung und Feldauflösung verwendet
- Die Entities werden via Entity Framework Core verwaltet
- `RepositoryStartKonfiguration` ist nicht in dieser Bestandsaufnahme detailliert aufgeführt, existiert aber und wird über `ProjektService` verwaltet
