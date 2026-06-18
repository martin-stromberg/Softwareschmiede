# Datenmodell

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `GitRepositoryId` | `Guid?` | Optionale ID des verknüpften Git-Repositories |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für KI-Agenten |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe (Neu, Gestartet, Wartend, Beendet, Archiviert) |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum (null wenn noch nicht abgeschlossen) |
| `AktiveRunId` | `string?` | Optionale aktive Lauf-ID einer KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Zeitstempel des letzten Heartbeats |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-relevante Statusänderungen |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt für nächsten Prompt |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft zum verknüpften Git-Repository |
| `IssueReferenz` | `IssueReferenz?` | **Verknüpfte Issue-Referenz aus dem Git-Provider** |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge des KI-Prozesses |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für diese Aufgabe |

**Hinweis:** Die Navigation `IssueReferenz` ist bereits vorhanden und kann verwendet werden.

---

## `IssueReferenz`
Datei: `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID der Issue-Referenz |
| `AufgabeId` | `Guid` | ID der zugehörigen Aufgabe |
| `IssueNummer` | `int?` | Nummer des Issues im Git-Provider |
| `Titel` | `string` | Titel des Issues |
| `Body` | `string?` | Beschreibungstext des Issues |
| `LabelsJson` | `string` | JSON-Array der Labels des Issues (Default: "[]") |
| `Milestone` | `string?` | Milestone des Issues |
| `IssueUrl` | `string?` | URL des Issues im Git-Provider |
| `Aufgabe` | `Aufgabe` | Navigationseigenschaft zur zugehörigen Aufgabe |

**Hinweis:** Diese Entity ist bereits vollständig implementiert und benötigt keine Änderungen.

---

## `Issue` (Value Object)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs`

Record mit folgenden Eigenschaften:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Nummer` | `int` | Issue-Nummer im Provider |
| `Titel` | `string` | Titel des Issues |
| `Body` | `string?` | Beschreibungstext des Issues |
| `Labels` | `IReadOnlyList<string>` | Labels des Issues |
| `Milestone` | `string?` | Milestone des Issues |
| `IssueUrl` | `string?` | URL des Issues im Provider |

**Hinweis:** Dieses Value Object ist bereits implementiert und wird von `GetIssuesAsync` zurückgegeben.
