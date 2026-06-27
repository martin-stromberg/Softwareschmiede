# Datenmodelle

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `GitRepositoryId` | `Guid?` | Optionale ID des verknüpften Git-Repositories |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für den KI-Agenten |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe (Enum) |
| **`BranchName`** | **`string?`** | **Name des Git-Branches für diese Aufgabe** ← **Relevant für diese Anforderung** |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des für diese Aufgabe verwendeten KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum der Aufgabe (null wenn noch nicht abgeschlossen) |
| `AktiveRunId` | `string?` | Optionale aktive Lauf-ID einer KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Zeitstempel des letzten Heartbeats einer Ausführung |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-relevante Statusänderungen |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für den nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt für den nächsten Prompt |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft zum verknüpften Git-Repository |
| `IssueReferenz` | `IssueReferenz?` | Verknüpfte Issue-Referenz aus dem Git-Provider |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge des KI-Prozesses für diese Aufgabe |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für diese Aufgabe |

**Hinweise:**
- Die Property `BranchName` ist bereits definiert und wird persistiert.
- Sie wird von verschiedenen Services befüllt (z.B. während des Prozessstarts oder der Aufgabenverwaltung).
