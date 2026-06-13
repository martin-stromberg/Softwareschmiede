# Datenmodell — Bestandsaufnahme

## `Projekt`
Datei: `src/Softwareschmiede/Domain/Entities/Projekt.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel |
| `Name` | `string` | Anzeigename des Projekts |
| `Beschreibung` | `string?` | Optionale Beschreibung des Projekts |
| `ErstellungsDatum` | `DateTimeOffset` | Anlagezeitpunkt |
| `Status` | `ProjektStatus` | Aktueller Status (Aktiv oder Archiviert) |
| `Repositories` | `List<GitRepository>` | Zugeordnete Git-Repositories |
| `Aufgaben` | `List<Aufgabe>` | Zugeordnete Aufgaben |

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel |
| `ProjektId` | `Guid` | Fremdschlüssel zum Projekt |
| `PluginTyp` | `string` | Plugin-Typ, z.B. "GitHub" oder "LocalDirectoryPlugin" |
| `RepositoryUrl` | `string` | URL des Repositories |
| `RepositoryName` | `string` | Anzeigename des Repositories |
| `Aktiv` | `bool` | Gibt an, ob das Repository aktiv ist |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startkonfiguration für Repository-Startskripte |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für dieses Repository |

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel |
| `ProjektId` | `Guid` | Fremdschlüssel zum Projekt |
| `GitRepositoryId` | `Guid?` | Optionale ID des verknüpften Git-Repositories |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für den KI-Agenten |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des für diese Aufgabe verwendeten KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum (null wenn noch nicht abgeschlossen) |
| `AktiveRunId` | `string?` | Aktive Lauf-ID einer KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Zeitstempel des letzten Heartbeats einer Ausführung |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-relevante Statusänderungen |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für den nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt für den nächsten Prompt |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft zum verknüpften Git-Repository |
| `IssueReferenz` | `IssueReferenz?` | Verknüpfte Issue-Referenz aus dem Git-Provider |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge des KI-Prozesses für diese Aufgabe |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für diese Aufgabe |
