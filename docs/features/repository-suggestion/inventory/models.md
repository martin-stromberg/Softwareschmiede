# Datenmodellklassen

## `Projekt`
Datei: `src/Softwareschmiede/Domain/Entities/Projekt.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Projekts |
| `Name` | `string` | Name des Projekts |
| `Beschreibung` | `string?` | Optionale Beschreibung |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungszeitpunkt |
| `Status` | `ProjektStatus` | Status (Aktiv/Archiviert) |
| `Repositories` | `List<GitRepository>` | Zugeordnete Git-Repositories pro Projekt |
| `Aufgaben` | `List<Aufgabe>` | Aufgaben des Projekts |

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Repositories |
| `ProjektId` | `Guid` | Foreign Key zum zugehörigen Projekt |
| `PluginTyp` | `string` | Plugin-Typ, z.B. "GitHub", "Softwareschmiede.GitHub", "LocalDirectoryPlugin" |
| `RepositoryUrl` | `string` | URL oder lokaler Pfad des Repositories |
| `RepositoryName` | `string` | Anzeigename des Repositories |
| `Aktiv` | `bool` | Kennzeichen für Aktivität (Standard: true) |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startkonfiguration für Repository-Startskripte |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse (optionale Zuordnung) |

## `AvailableRepository`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AvailableRepository.cs`

Record-Typ mit folgenden Eigenschaften:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Anzeigename des Repositories (z.B. "owner/repo" oder Verzeichnisname) |
| `UpdatedAt` | `DateTime` | Datum und Uhrzeit der letzten Aktualisierung — **entscheidend für Sortierung** |
| `NameWithOwner` | `string` | Vollständiger Name einschließlich Besitzer (z.B. "owner/repo") |
| `Url` | `string` | URL oder Pfad, der das Repository identifiziert (wird als `RepositoryUrl` gespeichert) |
