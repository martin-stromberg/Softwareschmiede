# Datenmodell

## `Projekt`
Datei: `src/Softwareschmiede/Domain/Entities/Projekt.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Projekt-ID. |
| `Name` | `string` | Projektname; relevanter Wert fuer `%ProjectName%`. |
| `Beschreibung` | `string?` | Optionale Projektbeschreibung. |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungszeitpunkt. |
| `Status` | `ProjektStatus` | Projektstatus. |
| `Repositories` | `List<GitRepository>` | Zugeordnete Repositories. |
| `Aufgaben` | `List<Aufgabe>` | Zugeordnete Aufgaben. |

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Aufgaben-ID. |
| `ProjektId` | `Guid` | Fremdschluessel zum Projekt. |
| `GitRepositoryId` | `Guid?` | Optionales verknuepftes Repository. |
| `Titel` | `string` | Aufgabentitel; relevanter Wert fuer `%TaskName%`. |
| `AnforderungsBeschreibung` | `string?` | Beschreibung fuer den KI-Agenten. |
| `Status` | `AufgabeStatus` | Lebenszyklusstatus. |
| `BranchName` | `string?` | Branchname der Aufgabe. |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories. |
| `AgentenpaketName` | `string?` | Persistiertes Agentenpaket. |
| `AgentenName` | `string?` | Persistierter Agentenname. |
| `KiPluginPrefix` | `string?` | KI-Plugin-Prefix fuer die Aufgabe. |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungszeitpunkt. |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlusszeitpunkt. |
| `AktiveRunId` | `string?` | Aktive KI-Lauf-ID. |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat. |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Letzter echter CLI-Prozessstart. |
| `LaufStatus` | `AufgabeLaufStatus?` | Feingranularer Laufzeitstatus der CLI. |
| `RecoveryVersion` | `int` | Concurrency-Token fuer Recovery-Aenderungen. |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag fuer den naechsten Prompt; keine allgemeine Promptvorlage. |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Zeitpunkt fuer `VorschlagPrompt`. |
| `Projekt` | `Projekt` | Navigation zum Projekt. |
| `GitRepository` | `GitRepository?` | Navigation zum Repository. |
| `IssueReferenz` | `IssueReferenz?` | Verknuepfte Issue-Referenz. |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokoll der Aufgabe. |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse. |

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Repository-ID. |
| `ProjektId` | `Guid` | Fremdschluessel zum Projekt. |
| `PluginTyp` | `string` | Plugin-Typ/Prefix. |
| `RepositoryUrl` | `string` | Repository-URL; relevanter Wert fuer `%RepositoryUrl%`. |
| `RepositoryName` | `string` | Anzeigename des Repositories. |
| `Aktiv` | `bool` | Aktivkennzeichen. |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startkonfiguration. |
| `Projekt` | `Projekt` | Navigation zum Projekt. |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse mit Repository-Kontext. |

## `AppEinstellung`
Datei: `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Einstellung-ID. |
| `Schluessel` | `string` | Maschinenlesbarer Key, eindeutig indiziert. |
| `Wert` | `string?` | Optionaler Wert. |
| `AktualisiertAm` | `DateTimeOffset` | Zeitpunkt der letzten Aktualisierung. |

## `SoftwareschmiededDbContext`
Datei: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Projekte` | `DbSet<Projekt>` | Persistenz fuer Projekte. |
| `GitRepositories` | `DbSet<GitRepository>` | Persistenz fuer Repositories. |
| `RepositoryStartKonfigurationen` | `DbSet<RepositoryStartKonfiguration>` | Persistenz fuer Start-/Arbeitsverzeichnis-Konfiguration. |
| `Aufgaben` | `DbSet<Aufgabe>` | Persistenz fuer Aufgaben. |
| `AppEinstellungen` | `DbSet<AppEinstellung>` | Globale App-Einstellungen. |

`OnModelCreating` konfiguriert Relationen `Projekt` -> `GitRepository`, `Projekt` -> `Aufgabe`, `Aufgabe` -> `GitRepository`, eindeutige Settings-Keys und DateTimeOffset-Konvertierungen fuer SQLite. Ein DbSet oder Mapping fuer Promptvorlagen existiert nicht.
