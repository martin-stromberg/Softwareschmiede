# Datenmodell

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige Kennung der Aufgabe |
| `ProjektId` | `Guid` | Verweis auf das übergeordnete Projekt |
| `GitRepositoryId` | `Guid?` | Optionale Verknüpfung zu Git-Repository |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Detaillierte Anforderung für KI-Agent |
| `Status` | `AufgabeStatus` | Aktueller Lebenszyklus-Status (Neu, Gestartet, InArbeit, Wartend, Beendet, Archiviert) |
| `BranchName` | `string?` | Git-Branch-Name für die Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokales Arbeitsverzeichnis |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des KI-Plugins für diese Aufgabe |
| `ErstellungsDatum` | `DateTimeOffset` | Zeitstempel der Erstellung |
| `AbschlussDatum` | `DateTimeOffset?` | Zeitstempel des Abschlusses (null wenn nicht beendet) |
| `AktiveRunId` | `string?` | Optional: Laufende KI-Ausführungs-ID |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Optional: Letzter Heartbeat einer Ausführung |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-Operationen |
| `VorschlagPrompt` | `string?` | Persistierter Prompt-Vorschlag |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplante Ausführungszeit des Vorschlags |
| `Projekt` | `Projekt` | Navigationseigenschaft: übergeordnetes Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft: verknüpftes Repository |
| `IssueReferenz` | `IssueReferenz?` | Optional: Referenz zu Git-Issue |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokoll aller KI-Ausführungen |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse (für Dateien oder Vergleiche) |

## `Projekt`
Datei: `src/Softwareschmiede/Domain/Entities/Projekt.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige Kennung des Projekts |
| `Name` | `string` | Name des Projekts |
| `Beschreibung` | `string?` | Optionale Projektbeschreibung |
| `ErstellungsDatum` | `DateTimeOffset` | Zeitstempel der Erstellung |
| `Status` | `ProjektStatus` | Aktueller Status (Aktiv, Archiviert) |
| `Repositories` | `List<GitRepository>` | Zugeordnete Git-Repositories |
| `Aufgaben` | `List<Aufgabe>` | Untergeordnete Aufgaben |

**Bemerkung:** Die `Aufgaben`-Navigationseigenschaft in `Projekt` wird von `ProjectDetailViewModel` verwendet, um die Aufgabenliste zu laden und anzuzeigen.
