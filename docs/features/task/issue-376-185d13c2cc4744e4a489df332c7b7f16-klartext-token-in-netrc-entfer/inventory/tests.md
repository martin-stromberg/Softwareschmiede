# Tests — Bestand und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-13, ca. 16:34–16:41 +0100 (CET)
- Branch und Commit-ID: `task/issue-376-185d13c2cc4744e4a489df332c7b7f16-klartext-token-in-netrc-entfer` @ `349616854b326956ab10d8f1e132b253d7e12316` („Backmerge from main to staging (#373)")
- Uncommittete Änderungen im getesteten Stand: keine Produktiv-/Teständerungen; nur untracked `docs/features/task/issue-376-…/` (requirement.md, todo.md und diese Inventory-Dateien). `git status --porcelain` zeigt ausschließlich `?? docs/features/task/issue-376-…/`.
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.403; `Softwareschmiede.Tests` Target `net10.0-windows10.0.17763.0` (WPF, xunit 2.9.3, VSTest-Adapter 4.0.1, Moq 4.21.0, FluentAssertions 8.11.0), `Softwareschmiede.IntegrationTests` Target `net10.0`.
- Ermittelte Testsuiten und Quellen der Testbefehle: Laut `CLAUDE.md` und `.github/workflows/pr-staging-ci.yml` existieren zwei Testprojekte (`Softwareschmiede.Tests`, `Softwareschmiede.IntegrationTests`) mit zwei Lanes: regulär (`--filter "Category!=OsInterface"`, blockierend in CI) und OS-Interface (`--filter "Category=OsInterface"`, in CI non-blocking/`continue-on-error`, mit `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` und `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`). Vor jedem Lauf erfolgte ein vollständiger Debug-Build beider Testprojekte (0 Warnungen, 0 Fehler).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build Tests | `dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug` | Repo-Root | 0 | — (Build: 0 Warnungen, 0 Fehler) | — | — | Konsolenausgabe im Lauf-Log |
| Build IntegrationTests | `dotnet build src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug` | Repo-Root | 0 | — (Build: 0 Warnungen, 0 Fehler) | — | — | Konsolenausgabe im Lauf-Log |
| Regular Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 2338 | 0 | 3 | [TRX](test-results/baseline-regular-tests.trx), [Konsole](test-results/baseline-regular-tests-console.log) |
| Regular IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 280 | 0 | 0 | [TRX](test-results/baseline-regular-integration.trx), [Konsole](test-results/baseline-regular-integration-console.log) |
| OS-Interface Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Root | 0 | 51 | 0 | 20 | [TRX](test-results/baseline-os-interface-tests.trx), [Konsole](test-results/baseline-os-interface-console.log) |
| OS-Interface IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Root | 0 | 9 | 0 | 0 | [TRX](test-results/baseline-os-interface-integration.trx), [Konsole](test-results/baseline-os-interface-integration-console.log) |

### Nachgewiesene bestehende Testfehler

Keine. In keinem der vier Läufe ist ein Test fehlgeschlagen (0 Fehler bei insgesamt 2701 gemeldeten Tests: 2341 + 280 + 71 + 9, davon 2678 bestanden und 23 übersprungen). Es gibt keinen dokumentierten „preexisting failure", auf den sich spätere Fehlschläge berufen könnten.

### Testlücken und Ausführungsprobleme

- **Reguläre Lane, 3 Übersprungen** (plattformbedingt, nicht anforderungsrelevant):
  - `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — Nicht-Windows-Pfad, auf Windows übersprungen.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_BleibtUnveraendert` — Linux-Pfad.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_BleibtUnveraendert` — macOS-Pfad.
- **OS-Interface-Lane, 20 Übersprungen** (`Softwareschmiede.Tests`): Alle WPF-/FlaUI-E2E- und ConPTY-E2E-Tests wurden gemäß CI-Konvention über `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` bzw. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` sauber als „Skipped" gemeldet statt in dieser Sandbox in Timeouts zu laufen (Grund laut `CLAUDE.md`: kein interaktiver Windows-Desktop / ConPTY-Isolierung in der Agent-Sandbox). Betroffen u. a. `WpfE2ETests.WpfBasisSzenarien`, `ProjectDetailE2ETests.ProjektDetailSzenarien`, `ScmEventsE2ETests.ScmEvents_…`, `E2E_RepositoryManagementTests.BasisBranchVerwaltung`, `E2E_RepositoryInitialisierung*`, `End2EndTest.ConPty_*`, `End2EndTest.General_*`, `End2EndTest.Simulator_*`, `End2EndTest.TerminalText_*`, `End2EndTest.ReplayText_*`. Diese Tests wurden **nicht ausgeführt** — über ihr Ergebnis ist keine Aussage möglich (in der CI ist die Lane non-blocking).
- Keine Build-, Setup- oder Infrastrukturfehler aufgetreten.

## Testklassen

### `GitHubPluginTests`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` (Moq-basiert: `Mock<ICliRunner>`, `Mock<ICredentialStore>`, `FakeGitHubEventApi`, `Mock<ILogger<GitHubPlugin>>`)

Anforderungsrelevante Testmethoden (Auswahl; netrc/`NETRC`-Variable wird in keinem Test referenziert):

- `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled` — verifiziert `git clone`-Aufruf inkl. `GH_TOKEN` in der Prozessumgebung.
- `CloneRepositoryAsync_ShouldNotEmbedToken_InCloneUrl` — kein Token/`oauth2:` in den Clone-Argumenten.
- `CloneRepositoryAsync_ShouldFailEarly_WhenHttpsTokenIsMissing` — HTTPS ohne Token wirft vor dem Clone (`InvalidOperationException`), kein CLI-Aufruf.
- `CloneRepositoryAsync_ShouldThrowInvalidOperationException_WhenCliFails` — Fehler-Mapping bei Clone-Fehlschlag.
- `CloneRepositoryAsync_ShouldMapAuthenticationErrors_ToHelpfulMessage` — „could not read Username"/„terminal prompts disabled" → Auth-Fehlermeldung.
- `CloneRepositoryAsync_ShouldSanitizeToken_InThrownExceptionMessage` — Token-Maskierung (`oauth2:***@`) in Exceptions.
- `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush` — Remote-URL-Normalisierung vor Push; Push-Env enthält `GIT_TERMINAL_PROMPT` + `GH_TOKEN`.
- `PushBranchAsync_ShouldNotEmbedToken_InRemoteUrl` — kein `set-url` bei bereits sauberer URL.
- `PushBranchAsync_ShouldNotSetRemoteUrl_WhenTokenIsMissing` — Normalisierung läuft token-unabhängig.
- `PushBranchAsync_ShouldThrow_WhenPushFails` — Exception bei Push-Fehlschlag.
- `PullAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm` / `PullAsync_ShouldRunGitPull_WithConfiguredCredentials` / `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl` / `PullAsync_ShouldThrow_WhenGitPullFails` — Pull-Pfad analog Push (Env mit `GH_TOKEN`).
- `SanitizeSensitiveOutput_ShouldMaskToken_InPushErrorMessages` / `…_InPullErrorMessages` / `…_ShouldMaskAgentToken_InErrorMessages` — Maskierung beider Tokens in Fehlermeldungen.
- `GetRemoteBranchesAsync_ShouldParseAndSortBranches` / `…_ShouldReturnEmpty_WhenCliFails` — `ls-remote`-Parsing und Fehlerpfad.
- `GetDefaultBranchAsync_ShouldReturnParsedBranch_WhenSymRefCanBeParsed` / `…_ShouldFallbackToMain_WhenSymRefFails` — `ls-remote --symref`-Auswertung und `main`-Fallback.
- `GetAgentEnvironmentAsync_WithToken_ProvidesGhAndGitAuth` — **prüft bereits das Zielmuster**: `GH_TOKEN`, `GIT_TERMINAL_PROMPT=0`, `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`, `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64>`.
- `GetAgentEnvironmentAsync_WithoutToken_OnlyDisablesGitPrompt` — ohne Token nur `GIT_TERMINAL_PROMPT`, kein `GH_TOKEN`/`GIT_CONFIG_VALUE_0`.
- `GetAgentEnvironmentAsync_SeparatesKonto_UsesAgentToken` — Kontomodus-Auswahl des AgentTokens.
- `SeparatesKonto_WithoutAgentToken_ThrowsAuthenticationBeforeAnyCall` — Ereignispfad-Auth vor API-Aufruf.
- Übrige Methoden in der Klasse betreffen Issues/Alerts/Templates/PRs/Workflow-Runs/Health/Metadaten — für die Anforderung nicht direkt relevant.

### `GitHubPluginTests_GetRepositoryStructureAsync`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests_GetRepositoryStructureAsync.cs`

- 10 Tests zu `GetRepositoryStructureAsync`/`GetRepositoryStructureLoadResultAsync` (Git-Trees-API, Branch-Auflösung via `git ls-remote` mit `It.IsAny<IDictionary<string,string>?>` — kein Env-Assert). Für die Anforderung nur indirekt relevant (`GetDefaultBranchAsync`-Aufrufpfad).

### `BitbucketPluginTests` / `BitbucketPluginTests_GetRepositoryStructureAsync`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/BitbucketPluginTests.cs`

- Tests zu Settings, API-Basis-URL, Health, Repositories, Issues, Jira, Clone-URL, Default-Branch. **Keine** Abdeckung von `UpdateNetrcEntry`/`GetGitEnvironment` (kein `netrc`-Treffer in den Tests) — die Referenzimplementierung für das Stanza-Parsing ist derzeit ungetestet.

### `GitPluginBaseTests`

Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`

- Tests der Basis-git-Operationen (`CreateBranchAsync`, `CommitAsync`, `CheckoutRemoteBranchAsync`, `EnsureGitRepositoryAsync`, `ResetAsync`, `GetGitActionCapabilitiesAsync`, Capability-Defaults) über `TestGitPlugin`-Stub; keine Auth-/Env- oder PR-Checkout-Abdeckung (`CheckoutPullRequestSourceCoreAsync` ungetestet).

## Hilfsmethoden

### `FakeGitHubEventApi`

Datei: `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs`

- `Add(endpoint, body, headers?)` / `AddJson(endpoint, rawBody, headers?)` / `AddGraph(data)` / `AddStatus(endpoint, status, headers?)` / `AddFailure(endpoint, error)` — Reiht erwartete `IGitHubEventApiClient`-Antworten/-Fehler ein.
- `Requests` — protokollierte `GitHubEventApiRequest`-Aufrufe für Asserts.
- `SendAsync(request, ct)` — liefert den eingeplanten Schritt, prüft Endpunkt-Fragment (`Assert.Contains`); `Assert.NotEmpty(_steps)` erzwingt vollständige Einplanung.
- `AssertComplete()` — stellt sicher, dass alle eingeplanten Schritte abgerufen wurden.

### `GitPluginMockExtensions`

Datei: `src/Softwareschmiede.Tests/Helpers/GitPluginMockExtensions.cs`

- `SetupPassthroughResolveEffectiveRepositoryPath(Mock<IGitPlugin>)` — Standard-Setup für `ResolveEffectiveRepositoryPathAsync` als Passthrough.

### Moq-Standardmuster (in den Testklassen)

- `Mock<ICliRunner>`: Setups auf `RunAsync("git"/"gh", args-Matcher, workingDirectory, env-Matcher, ct)` mit `CliResult(exitCode, stdOut, stdErr)`; Env-Asserts über `It.Is<IDictionary<string,string>?>(env => env.ContainsKey("GH_TOKEN") …)` — dasselbe Muster lässt sich für `GIT_CONFIG_*`-/fehlende `NETRC`-Asserts wiederverwenden.
- `Mock<ICredentialStore>`: `GetCredential(key)` → Token-String oder `null`.
