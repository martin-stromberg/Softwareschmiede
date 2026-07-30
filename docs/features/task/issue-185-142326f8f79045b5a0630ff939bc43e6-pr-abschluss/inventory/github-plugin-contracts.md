# GitHub-Plugin und Plugin-Vertraege

## Aktueller Plugin-Vertrag

`IGitPlugin` enthaelt derzeit u. a.:

- `CreatePullRequestAsync(repositoryId, branchName, title, body)`
- `GetGitActionCapabilitiesAsync(localPath)`
- Git-Basisoperationen wie Clone, Push, Pull, Commit, Reset
- Issue- und Repository-Struktur-Funktionen

Das bestehende `PullRequest`-Record enthaelt nur:

- `Nummer`
- `Titel`
- `Url`
- `BranchName`

Fuer Statusueberwachung und automatischen Abschluss reicht dieser Vertrag nicht.

## Aktuelle GitHub-Implementierung

`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` nutzt:

- `gh` fuer GitHub-API-nahe Funktionen,
- `git` fuer Clone/Push/Pull,
- `GH_TOKEN` aus dem Credential Store,
- Fehler-Sanitizing fuer Token-Ausgaben.

`CreatePullRequestAsync` ruft aktuell `gh pr create --repo ... --head ... --title ... --body ...` auf und parst die PR-Nummer aus der URL.

## Noetige Vertragserweiterungen

Es bieten sich providernahe Methoden im Contract an, mit GitHub als erster Implementierung:

- `GetPullRequestStatusAsync(repositoryId, pullRequestNumber)`
- `GetPullRequestWorkflowRunsAsync(repositoryId, pullRequestNumber, headSha?)`
- `CompletePullRequestAsync(repositoryId, pullRequestNumber, strategy, mergeMethod)`
- optional `GetRequiredChecksAsync(repositoryId, branchName)` oder ein Ergebnis, das required checks aus Branch Protection mitliefert.

Rueckgabeobjekte sollten providerneutral sein, aber GitHub-spezifische IDs und URLs behalten.

## GitHub-CLI/API-Ansaetze

Moegliche `gh`-Operationen:

- `gh pr view <number> --repo <owner/repo> --json number,title,url,state,mergeStateStatus,mergedAt,mergeCommit,headRefName,baseRefName,headRefOid`
- `gh run list --repo <owner/repo> --branch <branch> --json databaseId,name,status,conclusion,event,headSha,url,createdAt,updatedAt`
- `gh pr merge <number> --repo <owner/repo> --merge|--squash|--rebase`
- `gh pr review <number> --approve` nur wenn "bestaetigt" wirklich Approval bedeutet.
- Fuer Bypass/Branch-Protection sind je nach GitHub-Regeln API- oder Rollenrechte noetig; ein normales Approval durch den Ersteller kann von GitHub ignoriert werden.

## Plugin-Einstellungen

`GitHubPlugin.GetSettingGroups` hat aktuell nur die Token-Einstellung. Der Contract unterstuetzt bereits:

- `PluginSettingFieldType.Boolean`
- `PluginSettingFieldType.Enum`
- `DefaultValue`

Neue Einstellungen koennen daher ohne UI-Grundumbau als weitere Felder im GitHub-Plugin ergaenzt werden:

- `AutoCompletePullRequests` Boolean, Standard `false`
- `PullRequestCompletionStrategy` Enum, z. B. `Merge`, `AutoMerge`, `ApprovalOnly`
- `PullRequestMergeMethod` Enum, z. B. `MergeCommit`, `Squash`, `Rebase`
- optional `AllowBypass` Boolean oder `BypassMode` Enum

Die Speicherung laeuft ueber `PluginSettingsService` mit Schluesseln im Format `<PluginPrefix>.<FieldKey>`.

## Kompatibilitaet

Da externe Plugin-Assemblies den Contract referenzieren, sollten neue `IGitPlugin`-Methoden moeglichst Default-Implementierungen erhalten, die `NotSupported`-Ergebnisse liefern. So bleiben BitBucket, LocalDirectory und andere Plugins buildbar, waehrend GitHub die Funktionen implementiert.

## Tests

Vorhandene Tests:

- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
- `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`
- `src/Softwareschmiede.Tests/ServiceIntegration/PluginSettingsServiceIntegrationTests.cs`

Neue Tests sollten CLI-Argumente, JSON-Parsing, Token-Sanitizing, NotSupported-Fallbacks und Setting-Defaults abdecken.

