# Issue-, Branch- und PR-Verknüpfung

## Übersicht

Dieses Dokument beschreibt den internen Application-Contract für die durchgängige Verknüpfung von:

1. ausgewählter GitHub-Issue bei der Aufgabenanlage
2. branchName beim Prozessstart
3. Pull-Request-Body mit Closing-Direktive (`Closes #<IssueNummer>`)

Es handelt sich um einen Service-/Domänen-Contract und **nicht** um einen HTTP-Endpoint-Contract.

## Technische Komponenten

### Aufgabenanlage aus Issue

- `AufgabeService.CreateFromIssueAsync(...)`
- Persistiert `IssueReferenz` an der Aufgabe:
  - `IssueNummer`
  - `Titel`
  - `Body`
  - `LabelsJson`
  - `Milestone`
  - `IssueUrl`

### Branch-Erstellung beim Prozessstart

- `EntwicklungsprozessService.ProzessStartenAsync(...)`
- Branchname wird über `ErstelleTaskBranchName(Aufgabe)` erzeugt:
  - mit Issue: `task/issue-{IssueNummer}-{AufgabeIdN}-{TitelSlug}`
  - ohne Issue: `task/{AufgabeIdN}-{TitelSlug}`

### Pull-Request-Erstellung mit Auto-Close

- `GitOrchestrationService.PullRequestErstellenAsync(...)`
- PR-Body wird über `BuildPullRequestBody(...)` aufgebaut:
  - Bei verknüpfter Issue wird `Closes #<IssueNummer>` ergänzt.
  - Ist bereits eine Closing-Direktive für dieselbe Issue enthalten (`close/fix/resolve`), erfolgt **keine** Duplizierung.
  - Bei Whitespace-Body wird nur die Direktive verwendet.
- Protokoll erweitert den PR-Eintrag um Hinweis `Issue #<n>, Auto-Close aktiv`.

## Verhaltensmatrix

| Bedingung | Branchname | PR-Body |
|---|---|---|
| Aufgabe ohne IssueReferenz | `task/{AufgabeIdN}-{TitelSlug}` | unverändert (`title/body` bzw. Defaulttext) |
| Aufgabe mit IssueReferenz + Body ohne Direktive | `task/issue-{Issue}-{AufgabeIdN}-{TitelSlug}` | `... \n\nCloses #<Issue>` |
| Aufgabe mit IssueReferenz + Body mit Direktive für gleiche Issue | `task/issue-{Issue}-{AufgabeIdN}-{TitelSlug}` | unverändert |
| Aufgabe mit IssueReferenz + Body nur Whitespace | `task/issue-{Issue}-{AufgabeIdN}-{TitelSlug}` | `Closes #<Issue>` |

## Testnachweise

- `NeueAufgabeBunitTests`
  - `NeueAufgabe_ShouldLoadIssuesAndCreateTaskFromSelectedIssue`
- `EntwicklungsprozessServiceTests`
  - `ProzessStartenAsync_ShouldCreateIssueBranch_WhenAufgabeHasIssueReference`
- `GitOrchestrationServiceTests`
  - `PullRequestErstellenAsync_ShouldAppendClosingDirectiveAndLogIssue_WhenAufgabeHasIssueReference`
  - `PullRequestErstellenAsync_ShouldNotDuplicateClosingDirective_WhenBodyAlreadyContainsDirective`
  - `PullRequestErstellenAsync_ShouldUseOnlyClosingDirective_WhenBodyIsWhitespaceAndIssueExists`
  - `PullRequestErstellenAsync_ShouldAppendClosingDirectiveForCurrentIssue_WhenBodyContainsDirectiveForAnotherIssue`

## Verknüpfte Dokumentation

- Plugin-Contracts: [plugin-interfaces.md](./plugin-interfaces.md)
- Flow: [issue-branch-pr-linking-flow.md](../flows/issue-branch-pr-linking-flow.md)
- Business: [F019 – Issue-, Branch- und PR-Verknüpfung](../business/features/F019-issue-branch-pr-verknuepfung.md)
- Testplan: [testplan-issue-branch-pr-linking.md](../tests/testplan-issue-branch-pr-linking.md)
