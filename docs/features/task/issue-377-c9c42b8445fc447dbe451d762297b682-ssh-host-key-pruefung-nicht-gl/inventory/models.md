# Datenmodell

## `GitRepository`

Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs` — EF-Core-Entity (`_db.GitRepositories`), persistiert die verknüpften Remote-Repositories.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel |
| `ProjektId` | `Guid` | Zugehöriges Projekt |
| `PluginTyp` | `string` | Plugin-Prefix (z. B. `"Softwareschmiede.GitHub"`); steuert die Plugin-Auflösung |
| `RepositoryUrl` | `string` | Remote-URL, wie sie beim Verknüpfen eingegeben/übernommen wurde — **kein Format-Constraint**: `https://`, `git@host:…` und `ssh://…` landen unverändert in dieser Spalte und werden 1:1 an `CloneRepositoryAsync`/`GetRemoteBranchesAsync`/`GetDefaultBranchAsync` weitergereicht |
| `RepositoryName` | `string` | `owner/repo`-ID für API-Aufrufe |
| `Aktiv` | `bool` | Aktiv-Flag |
| `DefaultSourceBranchName` | `string?` | Konfigurierter Basis-Branch |
| `StartKonfiguration` / `InitialisierungKonfiguration` | Objekte | Unabhängig von dieser Anforderung |

## `PullRequestReferenz`

Datei: `src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs` — persistierte PR-Referenz einer Aufgabe.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceRepositoryUrl` | `string?` (Z. 51) | Clone-URL des PR-Quell-Repositories (Fork-Fall); wird aus `PullRequest.SourceRepositoryUrl` übernommen (`AufgabeService` Z. 400, `PullRequestReferenzService` Z. 75) und als `PullRequestCheckoutSpec.SourceRepositoryUrl` an `CheckoutPullRequestSourceAsync` weitergereicht — kann eine `ssh_url` sein |
| `SourceRepositoryId` / `SourceBranch` / `SourceRef` / `HeadSha` | `string?` | Übrige Checkout-Angaben |

## `PullRequest` (Value Object)

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs` — pluginseitige PR-Repräsentation; `SourceRepositoryUrl` wird im `GitHubPlugin.ParsePullRequest` aus `clone_url ?? ssh_url` befüllt (`GitHubPlugin.cs:1015`).

## `PullRequestCheckoutSpec`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequestCheckoutSpec.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TargetRepositoryId` / `TargetRepositoryUrl` | `string` | Ziel-Repo (`owner/repo` + Clone-URL) |
| `SourceRepositoryId` | `string` | Quell-Repo-ID; bei Gleichheit mit `TargetRepositoryId` wird Remote `"origin"` verwendet |
| `SourceRepositoryUrl` | `string?` | Remote-URL für den `git fetch` im Fork-Fall — potenziell SSH-Format |
| `SourceBranch` / `SourceRef` / `HeadSha` | `string`/`string?` | Checkout-Angaben |

## `AvailableRepository`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AvailableRepository.cs` — `Url` wird im `GitHubPlugin.GetAvailableRepositoriesAsync` aus `html_url` befüllt (`GitHubPlugin.cs:1627`) — also immer HTTPS; nur relevant, weil die Verknüpfungs-UI hieraus `GitRepository.RepositoryUrl` befüllen kann.
