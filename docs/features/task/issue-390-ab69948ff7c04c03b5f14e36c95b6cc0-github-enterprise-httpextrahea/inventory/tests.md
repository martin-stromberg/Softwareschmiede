# Tests

## Testklassen

### `GitHubPluginTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` (2647 Zeilen; `Mock<ICliRunner>` + `Mock<ICredentialStore>` + `FakeGitHubEventApi`; `_sut` wird mit `CreateMissingNetrcPath()` isoliert von der echten `_netrc` erzeugt, Z. 26–38)

- `GetAgentEnvironmentAsync_WithToken_ProvidesGhAndGitAuth` (Z. 1465–1479) — Assert auf `GH_TOKEN`, `GIT_TERMINAL_PROMPT=0`, `GIT_CONFIG_COUNT=1`, **`GIT_CONFIG_KEY_0 == "http.https://github.com/.extraheader"`** (Z. 1474), `GIT_CONFIG_VALUE_0` (Basic-Auth mit `x-access-token:`), kein `GIT_SSH_COMMAND`.
- `GetAgentEnvironmentAsync_WithoutToken_OnlyDisablesGitPrompt` (Z. 1484–1494) — ohne Token nur `GIT_TERMINAL_PROMPT=0`, kein `GH_TOKEN`/`GIT_CONFIG_VALUE_0`.
- `GetAgentEnvironmentAsync_SeparatesKonto_UsesAgentToken` (Z. 1498–1510) — `SeparatesKonto` nutzt `Softwareschmiede.GitHub.AgentToken` (`bot-token`).
- `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled` (Z. 496–519) — Verify `git clone` mit `GH_TOKEN` + `HasGitHubExtraHeader` + kein `NETRC`/`GIT_SSH_COMMAND`.
- `CloneRepositoryAsync_SshUrl_WirdZuHttpsGeklont` (Z. 1933–1959) / `CloneRepositoryAsync_SshSchemeUrl_WirdZuHttpsGeklont` (Z. 1963–1982) / `CloneRepositoryAsync_NichtHttpsSchema_WirdAbgelehnt` (Theory, Z. 1990–…: `http://`, `git://`, `ssh://` Fremd-Port) — HTTPS-only-Konvertierung/Ablehnung.
- `GetDefaultBranchAsync_Env_EnthaeltGitAuth` (Z. 1839–1860) — `ls-remote --symref` gegen `https://github.com/owner/repo.git` mit `GH_TOKEN` + `HasGitHubExtraHeader`.
- `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` (Z. 1865–1928) — stellt `remote.origin.url` auf `https://github.com/owner/repo.git`, prüft `git fetch --no-tags origin refs/heads/feature/pr` und `git checkout -B` jeweils mit `HasGitHubExtraHeader`; Hygiene-Aufrufe (`config remote.origin.url`, `config --get core.sshCommand`) ohne Env.
- `PushBranchAsync`-Tests (u. a. Z. ~983–1021: Env-Verify mit `HasGitHubExtraHeader` + `GIT_TERMINAL_PROMPT` + `GH_TOKEN`; Z. 1025–…: `remote.origin.url` gemockt auf `https://github.com/owner/repo.git`; Z. 2601–2633 `PushBranchAsync_NichtKonvertierbareRemoteUrl_KeinSetUrl`: `ssh://` Fremd-Port/UserInfo → kein `set-url`, Push läuft weiter) — zeigen das vorhandene Mock-Muster für `remote.origin.url` pro `localPath`.
- `SanitizeSensitiveOutput_ShouldMaskAgentToken_InErrorMessages` (Z. 1525–1542) — AgentToken-Maskierung.
- Weitere `HasGitHubExtraHeader`-Verwendungen: Z. 517, 1019, 1207, 1858, 1919/1926, 1951.

### `ScmEventDispatchTests_AgentActions`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_AgentActions.cs`

- `Dispatch_InjectsAgentEnvironmentIntoSessionSpec` (Z. 24–45) — Moq-Setup `GetAgentEnvironmentAsync(It.IsAny<ScmKontoModus>(), It.IsAny<CancellationToken>())` (Z. 28); prüft Merge der Agenten-Umgebung in `TerminalSessionStartSpec.EnvironmentVariables` und dass Geheimnisse nicht im Prompt landen.
- `Dispatch_FailingAgentEnvironment_KeepsSpecUntouched` (Z. 82–96) — Moq-Setup wirft (Z. 86); Spec bleibt unverändert, Zustellung läuft.
- `Dispatch_SeparatesKontoEvent_RevalidatesUnderAgentKonto` (Z. 51–77) — Konto-Routing der Revalidierung.
- `Dispatch_AgentReportCompletion_ProcessesWithoutChannelAction`, `Dispatch_EmptyCompletionWithoutReportOrReason_StaysUnresolved`, `Dispatch_NoRemoteActionCompletion_StillProcesses` — Abschluss-Semantik ohne Auth-Bezug.

## Hilfsmethoden

### `GitHubPluginTests` (Helfer)
- `HasGitHubExtraHeader(IDictionary<string,string> env, string token)` (Z. 2636–2643) — prüft exakt `GIT_CONFIG_COUNT == "1"`, **`GIT_CONFIG_KEY_0 == "http.https://github.com/.extraheader"`** und den erwarteten `GIT_CONFIG_VALUE_0`; zentrale Stelle für den hartcodierten Host in den Env-Verifies.
- `SequenceEqual(IEnumerable<string>, params string[])` (Z. 2645) — Argumentlisten-Vergleich.
- `CreateMissingNetrcPath()` (Z. 37–38) — nicht existierender `_netrc`-Pfad als Testseam (Konstruktorparameter `netrcPath`).

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`

- Konstruktor-Setup (Z. 80–81): `Git.As<IScmAgentAccess>().Setup(x => x.GetAgentEnvironmentAsync(It.IsAny<ScmKontoModus>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string,string?>())` — Default-Mock der Interface-Signatur; von Tests überschreibbar.
- `Git` = `Mock<IGitPlugin>` (Z. 40) mit `As<IScmEventSource>`/`As<IScmEventResponder>`/`As<IScmAgentAccess>`/`As<IScmNotificationSource>`-Facets; `StartSpecs`-Liste (Z. 43) zeichnet `TerminalSessionStartSpec` auf; `AddAsync` legt `GitRepository` mit `RepositoryUrl = "https://github.com/owner/repo"` an (Z. 179).

### `FakeGitHubEventApi`
Datei: `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs` — `IGitHubEventApiClient`-Fake für REST/GraphQL-Antworten in `GitHubPluginTests`.

### `TestKiAusfuehrungsServiceFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestKiAusfuehrungsServiceFactory.cs` — baut OS-freie `KiAusfuehrungsService`-Instanzen (`TestTerminalSessionFactory`, In-Memory-Launcher) für reguläre Tests; wird u. a. von `KiAusfuehrungsServiceTests*` genutzt.
