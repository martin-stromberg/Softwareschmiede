# Ablauf – Issue-Auswahl, Branch-Verknüpfung und PR Auto-Close

## Kontext

Dieser Ablauf dokumentiert den End-to-End-Pfad des Features:

1. Issue in **Neue Aufgabe** auswählen
2. Aufgabe mit `IssueReferenz` persistieren
3. beim Prozessstart issuebezogenen Task-Branch erzeugen
4. bei PR-Erstellung die passende Closing-Direktive ergänzen

## Diagramm A – Sequenz: Von der Issue bis zum PR

```mermaid
sequenceDiagram
    actor U as Nutzer
    participant UI as NeueAufgabe/AufgabeDetail
    participant AS as AufgabeService
    participant ES as EntwicklungsprozessService
    participant GS as GitOrchestrationService
    participant GP as IGitPlugin

    U->>UI: Issue auswählen (#123)
    UI->>AS: CreateFromIssueAsync(projektId, issue, repositoryId)
    AS-->>UI: Aufgabe + IssueReferenz gespeichert

    U->>UI: Entwicklungsprozess starten
    UI->>ES: ProzessStartenAsync(aufgabeId, repositoryUrl, ...)
    ES->>ES: ErstelleTaskBranchName(Aufgabe)
    Note over ES: task/issue-123-{aufgabeIdN}-{titelSlug}
    ES->>GP: CreateBranchAsync(localPath, branchName)
    GP-->>ES: Branch erstellt

    U->>UI: Pull Request erstellen
    UI->>GS: PullRequestErstellenAsync(aufgabeId, title, body)
    GS->>GS: BuildPullRequestBody(aufgabe, body)
    Note over GS: Ergänzt "Closes #123", wenn nötig
    GS->>GP: CreatePullRequestAsync(repositoryId, branchName, title, bodyWithDirective)
    GP-->>GS: PullRequest
    GS-->>UI: PR erstellt (Issue Auto-Close aktiv)
```

## Diagramm B – Entscheidungslogik: PR-Body

```mermaid
flowchart TD
    A([PullRequestErstellenAsync]) --> B{IssueReferenz vorhanden?}
    B -- Nein --> C[Body unverändert verwenden]
    B -- Ja --> D{Body enthält Closing-Direktive\nfür dieselbe Issue?}
    D -- Ja --> C
    D -- Nein --> E{Body leer oder nur Whitespace?}
    E -- Ja --> F["Body = Closes #Issue"]
    E -- Nein --> G["Body + zwei Zeilenumbrüche + Closes #Issue"]
    C --> H[CreatePullRequestAsync aufrufen]
    F --> H
    G --> H
    H --> I([PR erstellt und protokolliert])
```

## Schrittübersicht

1. **Issue-Auswahl in der UI**
   - `NeueAufgabe.razor.cs` lädt Issues (`LadeIssuesAsync`) und übernimmt bei Auswahl Titel/Body.
2. **Persistenz der Issue-Verknüpfung**
   - `AufgabeService.CreateFromIssueAsync` speichert `IssueReferenz`.
3. **Issuebezogener Branchname**
   - `EntwicklungsprozessService.ErstelleTaskBranchName` nutzt `IssueNummer`, falls vorhanden.
4. **PR-Body mit Auto-Close**
   - `GitOrchestrationService.BuildPullRequestBody` ergänzt `Closes #<IssueNummer>` nur bei Bedarf.
5. **Nachvollziehbarkeit im Protokoll**
   - PR-Protokolleintrag enthält den Hinweis auf Issue und Auto-Close.

## Verknüpfte Dokumentation

- API-Contract: [issue-branch-pr-linking.md](../api/issue-branch-pr-linking.md)
- Business: [F019 – Issue-, Branch- und PR-Verknüpfung](../business/features/F019-issue-branch-pr-verknuepfung.md)
- Testplan: [testplan-issue-branch-pr-linking.md](../tests/testplan-issue-branch-pr-linking.md)
