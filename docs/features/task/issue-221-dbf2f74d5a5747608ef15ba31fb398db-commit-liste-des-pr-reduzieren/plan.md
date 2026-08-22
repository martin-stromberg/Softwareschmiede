# Umsetzungsplan

## Ziel
Bei der PR-Erstellung mit einem abweichenden Zielbranch (z. B. `staging`) sollen die aufgelisteten Commits ausschließlich die Commits enthalten, die im gewählten Zielbranch noch nicht vorhanden sind.

## Änderungen

1. **`IGitWorkspaceBrowserService`**: Neue Überladung `LoadSnapshotAsync(string repositoryPath, string? baseBranch, CancellationToken ct)`.
2. **`GitWorkspaceBrowserService`**:
   - Bestehende 2-Parameter-Implementierung ruft 3-Parameter-Implementierung mit `baseBranch = null` auf.
   - 3-Parameter-Implementierung: Falls `baseBranch` angegeben, wird `origin/{baseBranch}` als Basis-Referenz geprüft; falls vorhanden, verwendet. Andernfalls Fallback auf bisheriges Verhalten.
3. **`GitOrchestrationService.PullRequestErstellenAsync`**: `baseBranch` vor dem Body-Bau ermitteln und an `BuildPullRequestBodyAsync` übergeben.
4. **`GitOrchestrationService.BuildPullRequestBodyAsync`**: Nimmt `baseBranch` entgegen und ruft `LoadSnapshotAsync` mit Zielbranch auf.
5. **Tests**: Erweiterung der `GitWorkspaceBrowserServiceTests` um den Zielbranch-Fall. Anpassung von `GitOrchestrationServiceTests.ShouldUseCommitListBody_WhenWorkspaceSnapshotAvailable` an neue Überladung.

## Offene Punkte

- Keine.

## Abgrenzungen

- Keine UI-Änderung; der Zielbranch kommt weiterhin aus `GitRepository.DefaultSourceBranchName`.
- Keine Änderung an `GitHubPlugin.CreatePullRequestAsync`; `--base` wird bereits korrekt übergeben.
