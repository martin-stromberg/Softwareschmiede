# Anforderung

## Fachliche Zusammenfassung

Wenn eine Aufgabe aus einem GitHub-Issue erstellt und beim Starten der Aufgabe ein Branch angelegt wird, muss diese Issue-Referenz bis zur Pull-Request-Erstellung erhalten bleiben und automatisch in den Pull Request einfließen. Der erzeugte Pull Request soll das verknüpfte GitHub-Issue beim Merge automatisch schließen, z. B. über eine GitHub-Closing-Direktive wie `Closes #<IssueNummer>` im Pull-Request-Text.

Der Anwender soll keine manuelle Nacharbeit benötigen, um die Beziehung zwischen ausgewähltem Issue, erzeugtem Branch und Pull Request herzustellen.

## Betroffene Klassen und Komponenten

### Aufgaben- und Issue-Modell
- `Softwareschmiede.Domain.Entities.Aufgabe`
  - `BranchName`: speichert den beim Starten erzeugten oder ausgewählten Branch.
  - `IssueReferenz`: speichert das aus dem Git-Provider ausgewählte Issue.
- `Softwareschmiede.Domain.Entities.IssueReferenz`
  - `IssueNummer`: zentrale Information für GitHub-Closing-Direktiven.
  - `IssueUrl`, `Titel`, `Body`, `LabelsJson`, `Milestone`: Kontextdaten zum ausgewählten Issue.

### Aufgaben-Lifecycle
- `Softwareschmiede.Application.Services.AufgabeService`
  - `CreateFromIssueAsync(...)`: erstellt Aufgaben aus GitHub-Issues und persistiert die Issue-Referenz.
  - `UpdateIssueReferenzAsync(...)`: setzt oder entfernt die Issue-Referenz einer bestehenden Aufgabe.
  - `StartenAsync(...)`: persistiert den Branch zur Aufgabe.
- `Softwareschmiede.Application.Services.EntwicklungsprozessService`
  - Startet den Entwicklungsprozess, erzeugt bzw. übernimmt den Branch und erstellt später Pull Requests.
- `Softwareschmiede.Application.Services.GitOrchestrationService`
  - Erstellt Pull Requests aus Aufgaben- und Repository-Kontext.

### Git-Provider-Integration
- `Softwareschmiede.Plugin.GitHub.GitHubPlugin`
  - `CreatePullRequestAsync(...)`: ruft `gh pr create` auf und übergibt Titel und Body an GitHub.
- `Softwareschmiede.Plugin.Contracts.Domain.Interfaces.IGitPlugin`
  - Provider-unabhängige Pull-Request-Schnittstelle.

## Erwartetes Verhalten

1. Wird eine Aufgabe aus einem GitHub-Issue erstellt, bleibt die Issue-Referenz an der Aufgabe gespeichert.
2. Beim Starten der Aufgabe wird der Branch an derselben Aufgabe gespeichert.
3. Bei der Pull-Request-Erstellung wird die Issue-Referenz der Aufgabe ausgewertet.
4. Ist eine Issue-Nummer vorhanden, enthält der Pull-Request-Body genau eine passende GitHub-Closing-Direktive für diese Issue, z. B. `Closes #17`.
5. Enthält der vom Anwender angegebene Pull-Request-Body bereits eine Closing-Direktive für dieselbe Issue, wird keine zweite Direktive ergänzt.
6. Enthält der Pull-Request-Body Closing-Direktiven für andere Issues, bleibt deren Inhalt erhalten und die aktuelle Issue wird zusätzlich ergänzt.
7. Aufgaben ohne Issue-Referenz oder ohne Issue-Nummer behalten das bisherige Pull-Request-Verhalten ohne automatische Closing-Direktive.
8. Der Branch-Name darf weiterhin aus der Aufgabe bzw. Issue abgeleitet werden; die automatische Issue-Schließung darf jedoch nicht ausschließlich vom Branch-Namen abhängen, sondern muss über den Pull Request zuverlässig funktionieren.

## Implementierungsansatz

Die Verknüpfung soll fachlich über die bestehende Aufgabe hergestellt werden: Eine Aufgabe besitzt sowohl die `IssueReferenz` als auch den `BranchName`. Bei der Pull-Request-Erstellung wird der Body vor dem Provider-Aufruf normalisiert und um eine Closing-Direktive für `IssueReferenz.IssueNummer` ergänzt.

Für GitHub ist eine Formulierung wie `Closes #<IssueNummer>` ausreichend, damit GitHub das Issue beim Merge des Pull Requests automatisch schließt. Die Ergänzung sollte in der Service-Schicht erfolgen, bevor `IGitPlugin.CreatePullRequestAsync(...)` aufgerufen wird, damit das GitHub-Plugin keine Aufgaben-Domain-Logik kennen muss.

## Tests

Erforderliche Testfälle:
- Pull Request für Aufgabe mit Issue-Referenz ergänzt `Closes #<IssueNummer>` im Body.
- Bereits vorhandene Closing-Direktive für dieselbe Issue wird nicht dupliziert.
- Whitespace- oder leerer Body wird durch die Closing-Direktive ersetzt bzw. sinnvoll befüllt.
- Closing-Direktiven für andere Issues bleiben erhalten; die aktuelle Issue wird ergänzt.
- Aufgabe ohne Issue-Referenz erstellt Pull Requests wie bisher.
- Aufgabe mit Issue-Referenz, aber ohne Issue-Nummer erstellt Pull Requests wie bisher.
- Branch-Erstellung aus Issue-Aufgaben speichert weiterhin einen Branch-Namen an der Aufgabe.

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Die Funktion nutzt die vorhandene GitHub-CLI-/Plugin-Konfiguration für Pull-Request-Erstellung.

## Offene Fragen

- Soll die Closing-Direktive ausschließlich für GitHub-Repositories ergänzt werden oder provider-neutral auch bei anderen Git-Plugins im Pull-Request-Body erscheinen?
- Soll die konkrete Direktive immer `Closes #<IssueNummer>` sein, oder ist eine deutschsprachige/konfigurierbare Vorlage gewünscht?
- Soll in der Benutzeroberfläche sichtbar gemacht werden, welches Issue beim Pull Request automatisch geschlossen wird?
