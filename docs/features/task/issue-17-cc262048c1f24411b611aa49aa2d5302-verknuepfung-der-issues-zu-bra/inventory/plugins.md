# Plugin-Vertrag und GitHub-Integration

## `IGitPlugin`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

| Element | Zeile | Relevanz |
|---------|-------|----------|
| `GetIssuesAsync(...)` | 20 | Liefert Issues aus dem Provider fuer die Aufgabenerstellung. |
| `CreateBranchAsync(...)` | 32 | Legt lokale Branches an. |
| `PushBranchAsync(...)` | 38 | Pusht Aufgabenbranches. |
| `CreatePullRequestAsync(...)` | 51 | Provider-neutrale PR-Schnittstelle mit `repositoryId`, `branchName`, `title`, `body`. |
| `GetRemoteBranchesAsync(...)` | 74 | Liefert vorhandene Remote-Branches fuer Startauswahl. |
| `GetDefaultBranchAsync(...)` | 80 | Hilft beim Entscheiden, ob ein neuer Task-Branch erstellt wird. |
| `CheckoutRemoteBranchAsync(...)` | 86 | Wechselt auf vorhandenen Branch. |

Der Vertrag kennt keine `Aufgabe` und keine `IssueReferenz`. Das passt zur Anforderung, die Closing-Direktive vor dem Plugin-Aufruf in der Service-Schicht zu bauen.

## `GitHubPlugin`

**Datei:** `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`

| Methode | Zeile | Relevanz |
|---------|-------|----------|
| `GetIssuesAsync(...)` | 316-334 | Ruft `gh issue list` auf und parst Issues. |
| `ParseIssues(...)` | 336-357 | Erzeugt `Issue`-Value-Objects inklusive Nummer, Titel, Body, Labels, Milestone, URL. |
| `PushBranchAsync(...)` | 403-421 | Pusht Branches via Git. |
| `CreatePullRequestAsync(...)` | 445-491 | Ruft `gh pr create --repo --head --title --body` auf. |
| `TryExtractRepositoryId(...)` | 731-781 | Extrahiert `owner/repo` aus GitHub-URLs fuer Remote-Strukturabfragen. |

`GitHubPlugin.CreatePullRequestAsync(...)` uebergibt den Body unveraendert an die GitHub-CLI. Eine im Service erzeugte Zeile `Closes #<IssueNummer>` erreicht damit GitHub direkt.

## Andere Plugins

| Plugin | Verhalten |
|--------|-----------|
| `Softwareschmiede.Plugin.LocalDirectory` | `CreatePullRequestAsync(...)` ist nicht unterstuetzt. |
| Bitbucket/GitHubCopilot/Codex/ClaudeCli/KiSimulator | Nicht primaerer Zielpfad fuer GitHub-Issue-Auto-Close; bei SCM-Plugins mit PR-Unterstuetzung wuerde die Service-Schicht denselben Body liefern. |

## Architekturbewertung

- Keine Vertragsaenderung an `IGitPlugin` erforderlich.
- Keine Domain-Logik im GitHub-Plugin erforderlich.
- Provider-Frage bleibt fachlich offen: Die vorhandene Service-Schicht fuegt `Closes #...` provider-neutral ein, sobald eine Aufgabe eine Issue-Nummer besitzt und das ausgewaehlte Plugin PRs erstellt.
