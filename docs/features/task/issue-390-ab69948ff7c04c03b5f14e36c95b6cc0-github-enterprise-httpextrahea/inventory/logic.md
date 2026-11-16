# Logik

## `GitHubPlugin`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` (`partial`, zweite Datei: `GitHubPlugin.Events.cs`)

Implementiert u. a. `IScmAgentAccess` (einziger Implementierer im Repo). Relevante Konstanten/Felder: `GitHubTokenCredentialKey` (Z. 21), `GitHubAgentTokenCredentialKey` (Z. 22), `EmbeddedTokenPattern` (`oauth2:…@`, Z. 25), `LegacyNetrcHosts` = `["github.com", "api.github.com"]` (Z. 26), `NetrcPathEnvironmentVariable` = `SOFTWARESCHMIEDE_NETRC_PATH` (Z. 43, internal).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAgentEnvironmentAsync(ScmKontoModus, CancellationToken)` | `public` | Z. 183–198. `IScmAgentAccess`-Implementierung: liefert `GIT_TERMINAL_PROMPT=0`, bei Token `GH_TOKEN` + `AddGitHubHttpAuthHeader`. Token über `CredentialKeyFor(konto)`. Synchron (`Task.FromResult`), ohne I/O — hat keinerlei Zugriff auf Arbeitskopie oder Repository-URL. |
| `AddGitHubHttpAuthHeader(IDictionary<string,string?>, string token)` | `private static` | Z. 206–212. Setzt `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0="http.https://github.com/.extraheader"` (hartcodiert), `GIT_CONFIG_VALUE_0="AUTHORIZATION: basic <base64>"`. Wird aufgerufen von `GetAgentEnvironmentAsync` (Z. 195) und `GetGitEnvironment` (Z. 231). |
| `GetGitEnvironment(string? token = null)` | `private` | Z. 214–235. Token-Fallback `GitHubTokenCredentialKey`; setzt `GIT_TERMINAL_PROMPT=0`, bei Token `GH_TOKEN` + `AddGitHubHttpAuthHeader` (Cast auf `IDictionary<string,string?>`). Kein Host-/URL-Parameter. 7 Aufrufstellen, siehe unten. |
| `GetGhEnvironment()` | `private` | Z. 162–171. Nur `GH_TOKEN` für `gh`-CLI-Aufrufe; kein `GIT_CONFIG_*`, kein git-Transport. |
| `NormalizeRemoteUrlAsync(string localPath, CancellationToken)` | `private` | Z. 453–507. Liest `git config remote.origin.url` aus der Arbeitskopie (Z. 458–463), strippt `EmbeddedTokenPattern`, normalisiert via `ToHttpsRepositoryUrl`, schreibt `git remote set-url origin` nur bei Differenz. Alle Fehler nur `LogWarning`. Rückgabetyp `Task` — die gelesene/normalisierte URL wird verworfen. Läuft am Anfang von `PushBranchAsync`, `PullAsync`, `CheckoutPullRequestSourceAsync`. |
| `RemoveInsecureSshCommandAsync(string localPath, CancellationToken)` | `private` | Z. 517–555. `git config --get core.sshCommand`; entfernt nur exakt `LegacyInsecureSshCommand`. Best-effort. |
| `ConfigureGitCredentialsAsync(string localPath, string repositoryUrl, CancellationToken)` | `private` | Z. 557–613. `GetGitEnvironment(token)` (Z. 566) für `git config user.name`/`user.email` und `git remote set-url origin` (nur bei `https://`-URL, Z. 595). Kennt die `repositoryUrl` als Parameter. Aufgerufen von `CloneRepositoryAsync` (Z. 1129). |
| `CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken)` | `public override` | Z. 1089–1130. Normalisiert `repositoryUrl` via `ToHttpsRepositoryUrl` (Fail-fast `InvalidOperationException` bei `null`), Token-Fail-fast für HTTPS-URLs, `git clone` mit `GetGitEnvironment(token)` (Z. 1113), danach `ConfigureGitCredentialsAsync`. |
| `PushBranchAsync(string localPath, string branchName, CancellationToken)` | `public override` | Z. 1133–1156. Hygiene (`NormalizeRemoteUrlAsync` + `RemoveInsecureSshCommandAsync`), dann `git push --set-upstream origin <branch>` mit `GetGitEnvironment(token)` (Z. 1148). Kennt nur `localPath` — Ziel ist fest `origin`. |
| `PullAsync(string localPath, CancellationToken)` | `public override` | Z. 1159–1182. Hygiene, dann `git pull` mit `GetGitEnvironment(token)` (Z. 1174). Kennt nur `localPath`. |
| `GetRemoteBranchesAsync(string repositoryUrl, CancellationToken)` | `public override` | Z. 1645–1676. `remoteUrl = ToHttpsRepositoryUrl(repositoryUrl) ?? repositoryUrl` (Z. 1649), `git ls-remote --heads <remoteUrl>` mit `GetGitEnvironment()` (Z. 1655). Fehlschlag → leere Liste (Soft-Fail). |
| `GetDefaultBranchAsync(string repositoryUrl, CancellationToken)` | `public override` | Z. 1703–1732. Gleiche Normalisierung (Z. 1707), `git ls-remote --symref <remoteUrl> HEAD` mit `GetGitEnvironment()` (Z. 1713). Fehlschlag → Fallback `"main"`. |
| `CheckoutPullRequestSourceAsync(string localPath, PullRequestCheckoutSpec, CancellationToken)` | `public override` | Z. 1743–1763. Hygiene, `SourceRepositoryUrl` per `with` auf HTTPS normalisiert (`ToHttpsRepositoryUrl(...) ?? url`, Z. 1753–1760), dann `CheckoutPullRequestSourceCoreAsync(localPath, spec, GetGitEnvironment(), ct)` (Z. 1762). |
| `CheckHealthAsync(CancellationToken)` | `public override` | Z. 1629–1642. `_eventApiClient.SendAsync(new("github.com", "GET", "user"))` — Host fest `github.com`, reiner API-Health (kein git). |
| `GetAvailableRepositoriesAsync(CancellationToken)` | `public override` | Z. 1679–1700. `RestPagesAsync(_eventApiClient, "github.com", "user/repos…")` — fest `github.com`. |
| `IsHttpsRepositoryUrl(string)` | `private static` | Z. 404–406. `Uri.TryCreate` + Scheme `https`. |
| `IsAuthenticationFailure(string)` | `private static` | Z. 408–419. Erkennt u. a. „terminal prompts disabled", „could not read username", „authentication failed", 403. |
| `SanitizeSensitiveOutput(string?)` | `private` | Z. 421–442. Maskiert beide Token + `EmbeddedTokenPattern` in Fehlertexten. |
| `TryExtractRepositoryId(string)` | `private static` | Z. 1867–1917. `owner/repo` aus HTTPS-/SCP-URL; liefert `null` statt zu werfen. |
| `ParsePullRequest(JsonElement, string)` | `private static` | Z. 1040–1086. Liest `head.repo.clone_url`/`ssh_url` aus PR-Metadaten als `sourceRepositoryUrl` (Z. 1067) — Datenquelle des Fork-Falls. |

### `GetGitEnvironment`-Aufrufstellen (vollständig)

| Zeile | Methode | Verfügbare Ziel-Information |
|-------|---------|------------------------------|
| 566 | `ConfigureGitCredentialsAsync` | `repositoryUrl`-Parameter (bereits HTTPS-normalisiert vom Aufrufer) |
| 1113 | `CloneRepositoryAsync` | `repositoryUrl` (bereits `ToHttpsRepositoryUrl`-normalisiert) |
| 1148 | `PushBranchAsync` | nur `localPath`; `remote.origin.url` wurde Zeilen vorher von `NormalizeRemoteUrlAsync` gelesen, aber verworfen |
| 1174 | `PullAsync` | nur `localPath`; wie `PushBranchAsync` |
| 1655 | `GetRemoteBranchesAsync` | `remoteUrl` (normalisiert) |
| 1713 | `GetDefaultBranchAsync` | `remoteUrl` (normalisiert) |
| 1762 | `CheckoutPullRequestSourceAsync` | `localPath` + `checkoutSpec` (`TargetRepositoryUrl`, ggf. `SourceRepositoryUrl` für Fork-Fall); `remote.origin.url` zuvor gelesen |

Abonnierte Events: keine. Publizierte Events: keine.

## `GitHubPlugin` (Ereignis-Teil)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ParseEventRepository(string)` | `private static` | Z. 397–424. Zerlegt `owner/repo`, HTTPS- oder SCP-URL in `(Host, Owner, Name)`; Default-Host `github.com`; validiert (nur `https` mit Default-Port/ohne UserInfo bzw. `ssh`+`git`+Port 22, nur `UriHostNameType.Dns`, kein Query/Fragment); Host per `uri.IdnHost`/`sshHost.IdnHost` normalisiert — bestehende GHE-Host-Extraktion für REST/GraphQL. Wirft `ScmProviderException` (Permanent) bei ungültiger Eingabe. |
| `CredentialKeyFor(ScmKontoModus)` | `private static` | Z. 345–346. `SeparatesKonto` → `GitHubAgentTokenCredentialKey`, sonst `GitHubTokenCredentialKey`. Wird von `GetAgentEnvironmentAsync` genutzt. |
| `ClientFor(ScmKontoModus)` | `private` | Z. 334–341. API-Client-Wahl nach Konto; wirft `Authentication` bei `SeparatesKonto` ohne AgentToken. Doc-Kommentar Z. 326–333 beschreibt die Token-Grenze (Nicht-Ereignis-Pfade laufen unter `GitHubTokenCredentialKey`/`GetGitEnvironment`). |
| `ValidateNextPage(string host, …)` | `private static` | Z. 386–395. Behandelt `github.com` → `api.github.com` + `/api/v3`-Präfix für GHE — vorhandenes Host-Mapping auf API-Ebene. |
| `GetEventRepositoryAsync` | `public` | Z. 29–47. Liefert `ScmEventRepository` inkl. `Host`. |
| `EnumerateNotificationsAsync(string host, …)` | `public` | Z. 170–199. Validiert Host via `GitHubHttpApiClient.ValidateHost`. |

## `GitHubHttpApiClient`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubHttpApiClient.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ValidateHost(string)` | `internal static` | Z. 77–88. Validiert DNS-Host ohne UserInfo/Port/Pfad/Query/Fragment; liefert `uri.IdnHost`; wirft `ScmProviderException` (Permanent). |
| `BuildRequestUri(GitHubEventApiRequest)` | `private static` | Z. 63–72. `github.com` → `api.github.com`; GHE → `https://<host>/api/v3/<ep>` bzw. `/api/graphql`. REST/GraphQL bereits hostfähig. |

## `GitPluginBase<TPlugin>`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ToHttpsRepositoryUrl(string)` | `protected static` | Z. 259–311. `https://` unverändert; SCP `git@host:pfad` → `https://host/pfad`; `ssh://[git@]host[:22]/pfad` → HTTPS (nur User `git`/leer, Default-Port oder 22, kein Query/Fragment). Hosts: DNS, IPv4, IPv6 (`uri.IdnHost`; IPv6 via `uri.Host` für Klammern, Z. 309). Liefert `null` für alle anderen Eingaben. Wiederverwendbare URL→HTTPS-Normalisierung; aus dem Ergebnis ist der Zielhost per `Uri` extrahierbar. |
| `IsConvertibleHostNameType(UriHostNameType)` | `private static` | Z. 313–314. `Dns`/`IPv4`/`IPv6` erlaubt. |
| `ScpSshUrlPattern` | `protected static readonly` | Z. 12. SCP-Erkennungsregex, auch von `ParseEventRepository` genutzt. |
| `CheckoutPullRequestSourceAsync` | `public virtual` | Z. 182–186. Delegiert an `CheckoutPullRequestSourceCoreAsync` ohne Umgebung; `GitHubPlugin`/`BitBucketPlugin` überschreiben. |
| `CheckoutPullRequestSourceCoreAsync(localPath, spec, env, ct)` | `protected` | Z. 189–226. `sameRepository = TargetRepositoryId == SourceRepositoryId` (OrdinalIgnoreCase); `remote = sameRepository ? "origin" : spec.SourceRepositoryUrl` (Z. 201–205 — Fork-Fall fetcht gegen die URL aus PR-Metadaten); `git fetch --no-tags <remote> <sourceRef>`, `git checkout -B <SourceBranch> FETCH_HEAD`, optional `rev-parse HEAD` vs. `HeadSha`. Alle drei git-Aufrufe erhalten dieselbe `environmentVariables`-Map. |
| `RunGitAsync(args, workdir, ct, env)` | `protected` | Z. 317–324. Dünne Hülle um `_cliRunner.RunAsync("git", …)`; `environmentVariables` ist `IDictionary<string,string>?`. |
| Abstrakte Signaturen | — | `CloneRepositoryAsync` (Z. 46), `PushBranchAsync(localPath, branchName)` (Z. 47), `PullAsync(localPath)` (Z. 48), `CheckHealthAsync` (Z. 75), `GetRemoteBranchesAsync(repositoryUrl)` (Z. 76), `GetDefaultBranchAsync(repositoryUrl)` (Z. 77) — die `localPath`-Signaturen tragen keine Repository-URL; ein Durchreichen vom Aufrufer wäre ein Signaturbruch für alle `IGitPlugin`-Implementierer. |

Abonnierte/Publizierte Events: keine.

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct, startConfig, gitPlugin)` | `public` | Z. 174–183. Pipe-Start; delegiert über `ScmTaskCoordinator` an `StartCliCoreAsync`. Kein `IScmAgentAccess`-Bezug. Produktiv nur aus Tests aufgerufen. |
| `StartCliCoreAsync(…)` | `private` | Z. 185–255. `ProcessStartInfo`-Pfad via `kiPlugin.StartCliAsync`; ruft `WithScmAgentEnvironmentAsync` **nicht** auf. |
| `StartTerminalSessionAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct, startConfig, gitPlugin)` | `public` | Z. 270–279. Terminal-Pfad; delegiert an `StartTerminalSessionCoreAsync`. |
| `StartTerminalSessionCoreAsync(…)` | `private` | Z. 281–421. Holt `TerminalSessionStartSpec` via `kiPlugin.GetTerminalStartSpecAsync` (Z. 303) und ruft `WithScmAgentEnvironmentAsync(spec, gitPlugin, ct)` auf (Z. 304) — einzige Stelle, die die Agenten-Umgebung injiziert. `gitPlugin` kommt als optionaler Parameter herein; die Arbeitskopie liegt als `localRepoPath`/effective workdir vor, die Repository-URL ist hier nicht bekannt. |
| `WithScmAgentEnvironmentAsync(spec, gitPlugin, ct)` | `private` | Z. 431–463. `gitPlugin is not IScmAgentAccess` → Spec unverändert; `ScmKontoModus` via DI-Scope `AppEinstellungService.GetScmKontoModusAsync` (Z. 443); `agentAccess.GetAgentEnvironmentAsync(konto, ct)` (Z. 444); Merge in `spec.EnvironmentVariables` (Ordinal-Vergleicher, Z. 457–462). Jeder Fehler außer `OperationCanceledException` → `LogWarning`, Spec unverändert (Start nie blockiert). |
| `GetTerminalSession`/`GetScmPromptSession` | `public` | Z. 468–477. Session-Abfragen. |

Publizierte Events: `CliProcessStatusChanged` (Z. 68), `CliLaufAktiv` (Z. 82), `RunningCountChanged` (Z. 85, via `IRunningAutomationStatusSource`).

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessUndCliCoordinatedAsync(…)` | `private` | Z. 137–205. `ProzessStartenCoreAsync` liefert `(repository, gitPlugin)`; `StartTerminalSessionAsync(aufgabeId, kiPlugin, aufgabe.LokalerKlonPfad, optionalParameters, ct, repository.StartKonfiguration, gitPlugin)` (Z. 190–191). `repository.RepositoryUrl` liegt hier vor. |
| `CliNeustartenCoordinatedAsync(…)` | `private` | Z. 244–305. `repository` via `ResolveRepositoryAsync` (Z. 294, Quelle `aufgabe.GitRepository?.RepositoryUrl`); `StartTerminalSessionAsync` (Z. 297–304) mit `repository.StartKonfiguration`/`gitPlugin`. `repository.RepositoryUrl` liegt vor. |
| `ProzessStartenCoreAsync(…)` | `private` | Z. 207–232. Orchestriert `ResolveRepositoryAsync`, `ResolvePluginAsync`, `ValidateBaseBranchExistsAsync`, `PrepareCloneDirectoryAsync`, `SetupBranchAsync`, `FinalizeStartAsync`; liefert `GitRepository` + `IGitPlugin`. |
| `ValidateBaseBranchExistsAsync` | `private static` | Z. 971–983. `gitPlugin.GetRemoteBranchesAsync(gitRepository.RepositoryUrl)` (Z. 978). |
| `PrepareCloneDirectoryAsync` | `private` | Z. 553–583. `gitPlugin.CloneRepositoryAsync(repositoryUrl, lokalerKlonPfad)` (Z. 581). |
| `SetupBranchAsync`/`CreateNewTaskBranchAsync` | `private` | Z. 585–621/662–695. `GetDefaultBranchAsync(repositoryUrl)` (Z. 611, 676). |
| `CheckoutReviewSourceAsync` | `private` | Z. 623–649. Baut `PullRequestCheckoutSpec` aus `PullRequestReferenz`-Feldern + `repositoryUrl` als `TargetRepositoryUrl` (Z. 633–640); `gitPlugin.CheckoutPullRequestSourceAsync` (Z. 647). |
| `PushDurchfuehrenAsync`/`PullDurchfuehrenAsync` | `public` | Z. 352–368/373–386. `_gitPlugin.PushBranchAsync`/`PullAsync` (Z. 361/379) auf dem injizierten Default-Plugin; Aufgabe via `GetAufgabeMitKlonPfadAsync` → `GetByIdAsync` (ohne `GitRepository`-Include). |
| `GetRemoteBranchesAsync(repositoryUrl, prefix?)` | `public` | Z. 481–485. Plugin-Auflösung + `GetRemoteBranchesAsync(repositoryUrl)`. |
| `ResolveRepositoryAsync(aufgabe, repositoryUrl, ct)` | `private` | Z. 776–820. `aufgabe.GitRepository` oder synthetisiertes `GitRepository` mit `RepositoryUrl` oder Projekt-Lookup. |

Abonnierte/Publizierte Events: keine.

## Weitere Aufrufer der betroffenen Plugin-Methoden

| Datei | Stelle | Aufruf |
|-------|--------|--------|
| `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs` | Z. 111, 134, 209 | `gitPlugin.PushBranchAsync`/`PullAsync` (per Aufgabe aufgelöstes Plugin; hat `aufgabe.GitRepository` im Kontext) |
| `src/Softwareschmiede/Application/Services/ScmBackmergeService.cs` | Z. 166 | `kontext.Plugin.GetDefaultBranchAsync(kandidat.RepositoryUrl)` |
| `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` | Z. 163–166 | `ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync` — der SCM-Ereignis-Zustellpfad, der Agenten-Sessions startet (`task.GitRepository!.RepositoryUrl` vorhanden) |
| `src/Softwareschmiede.App/Services/Testing/ScmE2ERuntimeControl.cs` | Z. 42 | `cli.StartTerminalSessionAsync(task.Id, plugin, task.LokalerKlonPfad!, "--scm-exchange", stoppingToken)` — E2E-Teststeuerung, ohne `gitPlugin`/`startConfig` |
| `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs` | Z. 337, 345 | `GetRemoteBranchesAsync`/`GetDefaultBranchAsync` mit UI-Repository-URL |
| `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs` | Z. 686 | `GetRemoteBranchesAsync(repository.RepositoryUrl)` |
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | Z. 1621, 1712 | `ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync` (UI-Pfade) |

## Kontrast: `BitBucketPlugin`
Datei: `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

Eigener `GetGitEnvironment()` (Z. 187–219): `GIT_TERMINAL_PROMPT=0` + `_netrc`-Pflege (`UpdateNetrcEntry`, hostbezogen — `bitbucket.org` oder SelfHosted-URL-Host); implementiert `IScmAgentAccess` **nicht**; eigene `CheckoutPullRequestSourceAsync`-Override (Z. ~1187–1210) mit `CheckoutPullRequestSourceCoreAsync`. Laut Anforderung nicht im Scope.

## Dokumentationsstellen mit Bezug zum festen Key

- `docs/help/plugins/business-rules.md` Z. 228–239: Regel „GitHub-Enterprise-Hosts authentifizieren nicht über HTTPS-git" — beschreibt den hartcodierten Key als bewusste Fail-closed-Entscheidung.
- `docs/help/plugins/ablauf-technisch.md` Z. 87–89: beschreibt `AddGitHubHttpAuthHeader`/`GetGitEnvironment` inkl. festem Key und Aufruferliste.
- `docs/help/plugins/github-ereignisse/business-rules.md` Z. 82 und `docs/help/plugins/github-ereignisse/ablauf-technisch.md` Z. 46: erwähnen den „auf `github.com` beschränkten `http.extraheader`" der Agenten-Umgebung.
