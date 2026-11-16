# Datenmodell

## `PullRequestCheckoutSpec`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequestCheckoutSpec.cs` (`record`, Z. 4–11)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TargetRepositoryId` | `string` | ID des Ziel-Repositories (`owner/repo`); Same-Repo-/Fork-Vergleich in `CheckoutPullRequestSourceCoreAsync`. |
| `TargetRepositoryUrl` | `string` | URL des Ziel-Repositories — Kandidat als Host-Quelle für den PR-Checkout. |
| `SourceRepositoryId` | `string` | ID des Quell-Repositories (Fork-Fall abweichend). |
| `SourceRepositoryUrl` | `string?` | Clone-URL der PR-Quelle aus PR-Metadaten (`head.repo.clone_url`/`ssh_url`, `GitHubPlugin.ParsePullRequest` Z. 1067) — Fetch-Ziel im Fork-Fall und zugleich genannter Leak-Vektor. |
| `SourceBranch` | `string` | Quell-Branch des Pull Requests. |
| `SourceRef` | `string?` | Expliziter Ref; sonst `refs/heads/<SourceBranch>`. |
| `HeadSha` | `string?` | Erwarteter Head-SHA zur Verifikation nach Checkout. |

## `TerminalSessionStartSpec`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/TerminalSessionStartSpec.cs` (`record`, Z. 8–51)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FileName` | `string` | Executable-Name/-Pfad des CLI-Prozesses. |
| `Arguments` | `string` | Startargumente. |
| `WorkingDirectory` | `string` | Arbeitsverzeichnis. |
| `EnvironmentVariables` | `IReadOnlyDictionary<string, string?>` | Z. 20. Zusätzliche Prozessumgebung — Ziel des Merges aus `GetAgentEnvironmentAsync` in `WithScmAgentEnvironmentAsync` (`KiAusfuehrungsService.cs` Z. 457–462). |
| `Capabilities`, `PromptSubmitDelay`, `PromptSubmitRepeatCount`, `PromptSubmitRepeatInterval`, `PluginName`, `OptionalParameters` | div. | Terminal-/Diagnose-Felder ohne Auth-Bezug. |

## `GitRepository` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs` (Z. 4–38)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `RepositoryUrl` | `string` | Z. 16. Repository-URL — bereits an beiden `StartTerminalSessionAsync`-Aufrufstellen des `EntwicklungsprozessService` verfügbar (Z. 190, 297). |
| `PluginTyp` | `string` | SCM-Plugin-Prefix. |
| `RepositoryName` | `string` | Anzeigename. |
| `DefaultSourceBranchName` | `string?` | Konfigurierter Basis-Branch. |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Navigation, eager geladen via `AufgabeService.GetDetailAsync` (Z. 110). |
| `InitialisierungKonfiguration` | `RepositoryInitialisierungKonfiguration?` | Navigation, eager geladen (Z. 112). |
| `Projekt`, `DiffResults`, `Id`, `ProjektId`, `Aktiv` | div. | Weitere Standard-/Navigations-Felder. |

## `RepositoryStartKonfiguration` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/RepositoryStartKonfiguration.cs` (Z. 4–23)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GitRepositoryId` | `Guid` | FK auf das Repository. |
| `GitRepository` | `GitRepository` | Navigation (Z. 22) — wird nirgends per `Include`/`ThenInclude` geladen; `AufgabeService.GetDetailAsync` inkludiert nur `Aufgabe.GitRepository` + dessen `StartKonfiguration`/`InitialisierungKonfiguration` (Z. 109–112), die Rücknavigation bliebe `null`. |
| `StartScriptRelativePath`, `WorkingDirectoryRelativePath`, `Aktiv`, `Id` | div. | Startskript-/Arbeitsverzeichnis-Konfiguration. |

## `GitHubEventApiRequest` / `GitHubEventApiResponse`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs` (Z. 11, 14)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GitHubEventApiRequest.Host` | `string` | Zielhost eines API-Aufrufs — der API-Pfad trägt den Host bereits explizit (vom Provider via `ParseEventRepository` ermittelt; `CheckHealthAsync`/`GetAvailableRepositoriesAsync` fest `"github.com"`). |
| `GitHubEventApiRequest.Method`/`Endpoint`/`JsonBody` | `string`/`string?` | REST-Aufrufdetails. |
| `GitHubEventApiResponse.StatusCode`/`Body`/`Headers` | `int`/`string`/`IReadOnlyDictionary?` | Rohe API-Antwort. |

## `CliResult`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/CliResult.cs` (`record`, Z. 7–15)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ExitCode`, `StdOut`, `StdErr` | `int`/`string` | Ergebnis eines CLI-Aufrufs; `IsSuccess` = `ExitCode == 0`. `NormalizeRemoteUrlAsync` liest `remote.origin.url` aus `StdOut`. |
