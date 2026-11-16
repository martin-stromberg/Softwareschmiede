# Tasks: GitHub-Enterprise: `http.extraheader`-Key hostgebunden machen (Issue #390)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `GitHubPlugin.TryBuildHttpExtraHeaderKey(string? url)` anlegen (`private static`, `Uri.TryCreate` + Scheme `https` + nicht-leerer Host → `http.https://<IdnHost>[:Port]/.extraheader`; IPv6 via `uri.Host`; Port nur bei `!IsDefaultPort`; sonst `null`) | Offen | — |
| 2 | Logik | `GitHubPlugin.AddGitHubHttpAuthHeader` um `targetUrl`-Parameter erweitern; Key aus `TryBuildHttpExtraHeaderKey`, bei `null` keinerlei `GIT_CONFIG_*`-Einträge; XML-Doc anpassen | Offen | — |
| 3 | Logik | `GitHubPlugin.GetGitEnvironment` um `targetUrl`-Parameter erweitern und durchreichen | Offen | — |
| 4 | Logik | `GitHubPlugin.NormalizeRemoteUrlAsync` auf `Task<string?>` umstellen (effektive origin-URL bzw. `null` bei Lesefehler; Best-effort-Semantik bleibt) | Offen | — |
| 5 | Logik | `GitHubPlugin.PushBranchAsync`/`PullAsync`: `NormalizeRemoteUrlAsync`-Rückgabewert nutzen, `GetGitEnvironment(token, remoteUrl)` | Offen | — |
| 6 | Logik | `GitHubPlugin.CloneRepositoryAsync`/`ConfigureGitCredentialsAsync`: `GetGitEnvironment(token, repositoryUrl)` mit normalisierter bzw. übergebener URL | Offen | — |
| 7 | Logik | `GitHubPlugin.GetRemoteBranchesAsync`/`GetDefaultBranchAsync`: `GetGitEnvironment(targetUrl: remoteUrl)` | Offen | — |
| 8 | Logik | `GitHubPlugin.CheckoutPullRequestSourceAsync`: `sameRepository`-Vergleich replizieren, kontaktierte URL bestimmen (origin vs. normalisierte `SourceRepositoryUrl`), `GetGitEnvironment(targetUrl: …)`; remarks-Doc ergänzen | Offen | — |
| 9 | Logik | `IScmAgentAccess.GetAgentEnvironmentAsync` um `string? repositoryUrl` erweitern (Signatur + XML-Doc); `GitHubPlugin`-Implementierung reicht URL an `AddGitHubHttpAuthHeader` durch | Offen | — |
| 10 | Logik | `KiAusfuehrungsService`: optionaler `repositoryUrl`-Parameter an `StartTerminalSessionAsync`/`StartTerminalSessionCoreAsync`/`WithScmAgentEnvironmentAsync`; Durchreichen an `GetAgentEnvironmentAsync` | Offen | — |
| 11 | Logik | `EntwicklungsprozessService`: beide `StartTerminalSessionAsync`-Aufrufe (~Z. 190, ~Z. 297) um `repositoryUrl: repository.RepositoryUrl` ergänzen | Offen | — |
| 12 | Tests (GitHub) | `HasGitHubExtraHeader(env, token, expectedKey)` um Key-Parameter erweitern; alle Aufrufer (u. a. Z. 517, 1019, 1207, 1474, 1858, 1919–1951) anpassen | Offen | — |
| 13 | Tests (GitHub) | `GetAgentEnvironmentAsync`-Tests (Z. 1465–1510) an neue Signatur anpassen | Offen | — |
| 14 | Tests (GitHub) | Neue Tests: `GetAgentEnvironmentAsync_GheHost_BindetExtraHeaderAnGheHost`, `…_NonDefaultPort_NimmtPortInKeyAuf`, `…_OhneBestimmbarenHost_KeinExtraHeader` (Theory) | Offen | — |
| 15 | Tests (GitHub) | Neue Tests: `CloneRepositoryAsync_GheUrl_BindetExtraHeaderAnGheHost`, `PushBranchAsync_GheRemote_BindetExtraHeaderAnOriginHost`, `PushBranchAsync_RemoteUrlNichtLesbar_KeinExtraHeader`, `PullAsync_GheRemote_BindetExtraHeaderAnOriginHost` | Offen | — |
| 16 | Tests (GitHub) | Neue Tests: `CheckoutPullRequestSourceAsync_Fork_BindetExtraHeaderAnSourceHost`, `…_SameRepo_BindetExtraHeaderAnOriginHost` | Offen | — |
| 17 | Tests (GitHub) | Bestehende `PushBranchAsync`-/`CheckoutPullRequestSourceAsync`-Tests: `git config remote.origin.url`-Mock-Setups prüfen/ergänzen (effektive URL für Host-Bindung) | Offen | — |
| 18 | Tests (Integration) | `ScmDispatchFixture` (Z. 80) + `ScmEventDispatchTests_AgentActions` (Z. 28, 86): Moq-Setups an neue Signatur anpassen; `Dispatch_PassesRepositoryUrlToAgentEnvironment` ergänzen | Offen | — |
| 19 | Doku | `docs/help/plugins/business-rules.md` (Z. 228–239), `docs/help/plugins/ablauf-technisch.md` (Z. 87–89), `docs/help/plugins/github-ereignisse/{business-rules,ablauf-technisch}.md`: feste `github.com`-Bindung/GHE-Einschränkung durch Host-Bindungs-Regel ersetzen; GH_TOKEN/`GH_HOST`-Einschränkung dokumentieren | Offen | — |
