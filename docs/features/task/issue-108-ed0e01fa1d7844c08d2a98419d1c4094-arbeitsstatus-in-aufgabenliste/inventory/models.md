# Datenmodelle

## `Aufgabe`
Datei: `src\Softwareschmiede\Domain\Entities\Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `Titel` | `string` | Titel der Aufgabe |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe (Neu, Gestartet, Wartend, Beendet, Archiviert) |
| `AktiveRunId` | `string?` | Optionale ID einer aktiven KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Zeitstempel des letzten Heartbeats einer Ausführung |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum (null wenn noch nicht abgeschlossen) |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-Statusänderungen |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des für diese Aufgabe verwendeten KI-Plugins |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für den KI-Agenten |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für den nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt für den nächsten Prompt |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft zum verknüpften Git-Repository |
| `IssueReferenz` | `IssueReferenz?` | Verknüpfte Issue-Referenz aus dem Git-Provider |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge des KI-Prozesses |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für diese Aufgabe |

### Relevante Eigenschaften für die Anforderung:
- `AktiveRunId` und `LastHeartbeatUtc`: Zentral für die Berechnung des "Läuft"-Status
- `Status`: Bestimmt den "Wartet"-Status (wenn `Status == Wartend`)
- `ErstellungsDatum` und `LastHeartbeatUtc`: Verwendet für die Sortierung aktiver Aufgaben
