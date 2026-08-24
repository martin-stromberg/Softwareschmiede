# Bestandsaufnahme

## Relevante Komponenten

### Pull-Request-Erstellung aus der Aufgabenansicht
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
  - `PullRequestErstellenAsync` (ab Zeile 1118) ruft `GitOrchestrationService.PullRequestErstellenAsync`.
- `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
  - `PullRequestErstellenAsync` (Zeile 183) orchestriert Push und Provider-PR-Erstellung.
  - `BuildPullRequestBodyAsync` (Zeile 238) baut den Beschreibungstext.
  - `ResolveDefaultSourceBranchNameAsync` (Zeile 371) ermittelt den konfigurierten Zielbranch (z. B. `main`).
- `src/Softwareschmiede/Application/Services/PullRequestBodyBuilder.cs`
  - `BuildFromCommits` (Zeile 33) formatiert die Commit-Liste.
- `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`
  - `LoadSnapshotAsync` (Zeile 34) lädt Commit-Count und `BranchCommits` über `git log {baseReference}..HEAD`.
  - `ReadBranchCommitsAsync` (Zeile 460) führt `git log --format=%H%x00%h%x00%x {baseReference}..HEAD` aus.

### Branch-Anlage für Aufgaben
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
  - `ProzessStartenAsync` (Zeile 87) erhält `basisBranchName`.
  - `SetupBranchAsync` (Zeile 501) legt den Task-Branch von `defaultSourceBranchName` oder vom HEAD an.
  - Der Parameter `defaultSourceBranchName` kommt aus `GitRepository.DefaultSourceBranchName`.
  - Der tatsächliche Start-Branch (Basis, von dem der Feature-Branch abgezweigt wurde) wird nicht persistiert.

### Datenmodell
- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`
  - Enthält `BranchName` und `LokalerKlonPfad` (als `GitArbeitsbereich` Value-Object).
  - Kein Feld für den ursprünglichen Basis-Branch, von dem der Feature-Branch erstellt wurde.
- `src/Softwareschmiede/Domain/ValueObjects/GitArbeitsbereich.cs`
  - Kapselt nur `BranchName` und `ClonePfad`.

### Tests
- `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceTests.cs`
  - Umfangreiche Tests für `LoadSnapshotAsync` mit simuliertem CLI-Output.
- `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
  - Enthält Tests für `PullRequestErstellenAsync`.

## Feststellung

Der PR-Body wird aus `git log origin/{DefaultSourceBranchName}..HEAD` gebaut. Wenn der Feature-Branch jedoch von einem Zwischen-Branch wie `staging` abgezweigt wurde (z. B. weil `GitRepository.DefaultSourceBranchName` oder der Startdialog `staging` als Quelle verwendet), enthält diese Commit-Range alle Commits, die in `staging` aber nicht in `main` sind, plus die echten Feature-Commits.

Git allein kann anhand des lokalen Repositories nicht mehr unterscheiden, welche Commits ursprünglich zum neu angelegten Feature-Branch gehören, sobald der Branch von einem Staging-Branch abstammt. Die notwendige Information ist der beim Branch-Checkout verwendete Start-Branch.

## Offene Punkte

Keine.
