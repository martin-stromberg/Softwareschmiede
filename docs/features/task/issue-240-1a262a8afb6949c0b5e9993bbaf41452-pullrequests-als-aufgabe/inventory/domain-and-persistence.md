# Domäne und Persistenz

## Vorhandene Bausteine

- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs:12-23` definiert bereits Nummer, Titel, URL, Quell-Branch, Provider, Repository-ID, Provider-ID, Ziel-Branch und Head-SHA.
- `src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs:6-81` persistiert diese PR-Daten pro Aufgabe sowie Monitoring- und Workflow-Informationen.
- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs:104-108` besitzt `PullRequests` als Navigation. Eine Aufgabe kann damit bereits Pullrequest-Referenzen tragen.
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:42-48,193-199,295-322` registriert die DbSets, die 1:n-Beziehung und einen eindeutigen Index auf Provider, Repository und PR-Nummer.
- `src/Softwareschmiede/Application/Services/PullRequestReferenzService.cs` sowie die zugehörigen Tests kapseln das Speichern, Laden und Aktualisieren der bestehenden PR-Referenzen.

## Aktuelle Lücke

- `AufgabeService` erstellt ab `src/Softwareschmiede/Application/Services/AufgabeService.cs:190` Aufgaben aus `Issue` und legt dabei ausschließlich eine `IssueReferenz` an. Ein analoger Create-Pfad für `PullRequest` fehlt.
- Die Aufgabenabfragen laden ab `AufgabeService.cs:37-46` bzw. `84-101` Issue-/Alert-Referenzen, aber keine Pullrequest-Referenzen. Das ist für Anzeige und Zuordnungsprüfung zu ergänzen.
- Die bestehende Issue-Referenz ist ein separates 1:1-Modell. Für Pullrequests sollte die vorhandene `PullRequestReferenz` verwendet werden; die Geschäftsregel "bereits zugeordnet" kann über Provider, Repository-ID und PR-Nummer geprüft werden.

## Betroffene Persistenzentscheidungen

Es ist keine neue Tabelle für die Grundanforderung erforderlich. Zu prüfen sind lediglich passende Ladepfade, ein atomarer Zuordnungs-/Erstellungspfad und die Initialisierung aller Pflichtfelder (`Provider`, `RepositoryId`, `PullRequestNumber`, `Url`, `Titel`, `SourceBranch`, `TargetBranch`).
