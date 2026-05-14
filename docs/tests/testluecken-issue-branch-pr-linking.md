# Testlücken – Issue-Auswahl, Branch-Verknüpfung und PR Auto-Close

## Ergebnis

Für den Scope
- Issue-Auswahl beim Anlegen einer Aufgabe
- Branch-Erzeugung und Verknüpfung zur Aufgabe beim Prozessstart
- PR-Erstellung mit Closing-Direktive (`Closes #<Issue>`) 

wurden **keine offenen Testlücken** mehr festgestellt.

## Verifizierte Testabdeckung

- `NeueAufgabeBunitTests`
  - `NeueAufgabe_ShouldLoadIssuesAndCreateTaskFromSelectedIssue`
  - `NeueAufgabe_ShouldShowWarning_WhenIssuesAbrufenAsyncThrows`
- `EntwicklungsprozessServiceTests`
  - `ProzessStartenAsync_ShouldCreateTaskBranch_WhenBasisBranchEqualsDefaultBranch_CaseInsensitive`
  - `GetRemoteBranchesAsync_ShouldResolvePluginBySelectedPrefix_AndReturnPluginBranches`
- `GitOrchestrationServiceTests`
  - `PullRequestErstellenAsync_ShouldUseOnlyClosingDirective_WhenBodyIsWhitespaceAndIssueExists`
  - `PullRequestErstellenAsync_ShouldAppendClosingDirectiveForCurrentIssue_WhenBodyContainsDirectiveForAnotherIssue`
  - `PullRequestErstellenAsync_ShouldThrowInvalidOperationException_WhenNoActiveRepositoryExists`
  - `PullRequestErstellenAsync_ShouldResolveRepositoryId_FromSshRepositoryUrl`

## Abschlusskriterium

- Status: **abgeschlossen**
- Offene P1/P2-Lücken: **keine**
