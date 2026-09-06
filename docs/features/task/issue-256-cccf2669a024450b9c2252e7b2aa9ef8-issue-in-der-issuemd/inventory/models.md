# Datenmodell

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | Fremdschlüssel zum Projekt |
| `GitRepositoryId` | `Guid?` | Optionaler Fremdschlüssel zum Git-Repository |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Anforderungstext für den KI-Agenten; wird als Hauptinhalt in die `issue.md` geschrieben |
| `Status` | `AufgabeStatus` | Aktueller Lebenszyklus-Status |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter Ausführungsstatus |
| `BranchName` | `string?` | Git-Branch-Name der Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Klon-Pfad des Repositories |
| `BasisBranchName` | `string?` | Ursprünglicher Branch, von dem abgezweigt wurde |
| `GitArbeitsbereich` | `GitArbeitsbereich?` | `[NotMapped]` Value Object über `BranchName`, `LokalerKlonPfad`, `BasisBranchName` |
| `AgentenpaketName` | `string?` | Verwendetes Agentenpaket |
| `AgentenName` | `string?` | Verwendeter Agent |
| `KiPluginPrefix` | `string?` | Prefix des KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungszeitpunkt; wird in die `issue.md` geschrieben |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlusszeitpunkt |
| `AktiveRunId` | `string?` | Lauf-ID einer aktiven KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Letzter CLI-Prozessstart |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus der aktiven Ausführung |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery |
| `VorschlagPrompt` | `string?` | Vorschlag für den nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt des Prompts |
| `AutonomKonfiguration` | `AutonomAufgabeKonfiguration?` | Navigationseigenschaft zur Konfiguration autonomer Aufgaben |
| `Projekt` | `Projekt` | Navigationseigenschaft zum Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft zum Git-Repository |
| **`IssueReferenz`** | **`IssueReferenz?`** | **Verknüpfte Issue-Referenz aus dem Git-Provider — zentral für die Anforderung** |
| `AlertReferenz` | `AlertReferenz?` | Verknüpfte Alert-Referenz |
| `PullRequests` | `List<PullRequestReferenz>` | Verknüpfte Pull Requests |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge des KI-Prozesses |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse |
| `Todos` | `List<Todo>` | To-Do-Elemente |

---

## `IssueReferenz`
Datei: `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID der Issue-Referenz |
| `AufgabeId` | `Guid` | Fremdschlüssel zur zugehörigen Aufgabe |
| **`IssueNummer`** | **`int?`** | **Nummer des Issues im Git-Provider — soll in die `issue.md` geschrieben werden** |
| **`Titel`** | **`string`** | **Titel des Issues — soll in die `issue.md` geschrieben werden** |
| `Body` | `string?` | Beschreibungstext des Issues |
| `LabelsJson` | `string` | JSON-Array der Labels |
| `Milestone` | `string?` | Milestone des Issues |
| `IssueUrl` | `string?` | URL des Issues im Git-Provider |
| `Aufgabe` | `Aufgabe` | Navigationseigenschaft zur zugehörigen Aufgabe |
