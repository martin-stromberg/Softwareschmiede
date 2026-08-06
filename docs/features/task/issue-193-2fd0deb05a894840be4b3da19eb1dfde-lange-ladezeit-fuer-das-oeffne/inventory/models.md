# Datenmodell: Aufgaben und Protokolleinträge

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für den KI-Agenten |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum (null wenn noch nicht abgeschlossen) |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft zum verknüpften Git-Repository |
| `IssueReferenz` | `IssueReferenz?` | Verknüpfte Issue-Referenz |
| `AlertReferenz` | `AlertReferenz?` | Verknüpfte Alert-Referenz |
| `PullRequests` | `List<PullRequestReferenz>` | Verknüpfte Pull Requests |
| `Protokolleintraege` | `List<Protokolleintrag>` | **Navigationseigenschaft**: Protokolleinträge des KI-Prozesses (derzeit in `GetDetailAsync` mit `.Include()` geladen) |
| `Todos` | `List<Todo>` | To-Do-Elemente dieser Aufgabe |

## `Protokolleintrag`
Datei: `src/Softwareschmiede/Domain/Entities/Protokolleintrag.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID des Protokolleintrags |
| `AufgabeId` | `Guid` | ID der zugehörigen Aufgabe |
| `Typ` | `ProtokollTyp` | Typ des Protokolleintrags |
| `Inhalt` | `string` | Inhalt des Protokolleintrags |
| `AgentName` | `string?` | Name des beteiligten Agenten |
| `Zeitstempel` | `DateTimeOffset` | Zeitstempel des Eintrags |
| `Aufgabe` | `Aufgabe` | Navigationseigenschaft zur zugehörigen Aufgabe |
| `TestErgebnisse` | `List<TestErgebnis>` | **Nested Include**: Zugehörige Testergebnisse (bei Typ TestErgebnis) |
