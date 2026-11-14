# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-14, Testläufe ca. 07:10–07:30 (W. Europe Standard Time, UTC+01:00)
- Branch und Commit-ID: `task/issue-377-c9c42b8445fc447dbe451d762297b682-ssh-host-key-pruefung-nicht-gl` @ `711781302b1257d7b806c585325a261a73ea9cb2`
- Uncommittete Änderungen im getesteten Stand: nur die neuen Feature-Dokumente unter `docs/features/task/issue-377-…-ssh-host-key-pruefung-nicht-gl/` (`requirement.md`, `todo.md`, `inventory/`); keinerlei Änderungen an Produktiv- oder Testcode.
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.403, Runtimes Microsoft.NETCore.App/WindowsDesktop.App 10.0.15, Ziel-Frameworks `net10.0` (IntegrationTests) / `net10.0-windows10.0.17763.0` (Tests, WPF), git 2.53.0.windows.2, Windows, Debug-Konfiguration. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei beiden Läufen gesetzt (Sandbox-Auflage; wirkt nur auf ConPTY-E2E-Tests der OsInterface-Lane und zeigte hier keinen sichtbaren Effekt).
- Build vor den Tests: `dotnet build Softwareschmiede.slnx -c Debug` — erfolgreich, 0 Warnungen, 0 Fehler (~16 s).
- Ermittelte Testsuiten und Quellen der Testbefehle: CI-Workflow `.github/workflows/pr-staging-ci.yml` (Z. 128, 132, 222, 226) definiert vier Lanes — je `Category!=OsInterface` und `Category=OsInterface` für `src/Softwareschmiede.Tests` und `src/Softwareschmiede.IntegrationTests`. CLAUDE.md nennt die `Category!=OsInterface`-Lane des `Softwareschmiede.Tests`-Projekts als stabile Regulär-Lane; OsInterface enthält die umgebungssensitiven FlaUI-/ConPTY-/Clipboard-/Credential-Store-Tests.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 — Regulär-Lane `Softwareschmiede.Tests` | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=baseline-regular-lane.trx" --logger "console;verbosity=normal" --results-directory docs/features/task/…/inventory/test-results` | Repo-Root | 0 | 2352 | 0 | 3 | [TRX](test-results/baseline-regular-lane.trx), [Konsole](test-results/baseline-regular-lane.console.log) |
| 2 — Regulär-Lane `Softwareschmiede.IntegrationTests` | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=baseline-integration-regular-lane.trx" --logger "console;verbosity=normal" --results-directory docs/features/task/…/inventory/test-results` | Repo-Root | 0 | 280 | 0 | 0 | [TRX](test-results/baseline-integration-regular-lane.trx), [Konsole](test-results/baseline-integration-regular-lane.console.log) |

### Nachgewiesene bestehende Testfehler

Keine. Beide ausgeführten Läufe endeten mit 0 Fehlgeschlagenen.

### Testlücken und Ausführungsprobleme

- **3 übersprungene Tests in Lauf 1** — alle plattformbedingt (`SkippableFact`/`Skip.If`), keine Fehler:
  - `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — gilt nur für Nicht-Windows.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_BleibtUnveraendert` — Linux-spezifisch.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_BleibtUnveraendert` — macOS-spezifisch.
- **OsInterface-Lanes nicht ausgeführt** (`Category=OsInterface` in beiden Testprojekten): umgebungssensitive, langlaufende FlaUI-/ConPTY-/Credential-Store-Tests. Für die Anforderung nicht feature-relevant — keine E2E-Datei referenziert `GitHubPlugin`-git-Env, `GIT_SSH_COMMAND` oder SSH-Transport (Repo-weite Suche in `src/Softwareschmiede.Tests/E2E` ohne Treffer); `E2E_ScmEvents` nutzt zwar ein echtes `GitHubPlugin`, aber `ScmE2EGitRunner` verwirft die Prozessumgebung und ersetzt Remotes durch lokale Pfade. Für den nicht ausgeführten Umfang ist der Ausgangszustand unbekannt; es wird keine Aussage getroffen.

## Testklassen

### `GitHubPluginTests`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` (1865 Zeilen, ~70 `[Fact]`s). Konstruktor-Setup: `Mock<ICliRunner>` + `Mock<ICredentialStore>` + `FakeGitHubEventApi` + `CreateMissingNetrcPath()` (Z. 21-38) — die `_netrc`-Konstruktor-Bereinigung läuft im Test gegen einen garantiert fehlenden Temp-Pfad.

Env-/Transport-relevante Tests (Auswahl, alle für die Anforderung bedeutsam):

- `CloneRepositoryAsync_OhneToken_NurTerminalPromptUndSshInEnv` (Z. 1761-1785) — **einziger Test, der den Bypass positiv assertiert**: klont per `git@github.com:owner/repo.git` ohne Token und verlangt `env["GIT_TERMINAL_PROMPT"]=="0"`, `env.ContainsKey("GIT_SSH_COMMAND")`, kein `GH_TOKEN`, keine `GIT_CONFIG_*`, kein `NETRC`. Muss für die Anforderung umgestellt werden (SSH-Klon ohne Token ist künftig nicht mehr vorgesehen).
- `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled` (Z. 496-519) — verifiziert `clone`-Args und Env mit `GH_TOKEN`, `HasGitHubExtraHeader(env,"token")`, `!NETRC`; assertiert **nicht** die `git config`-Folgeaufrufe von `ConfigureGitCredentialsAsync` (`user.name`, `user.email`, `set-url`, `core.sshCommand`) — die laufen gegen den generischen Mock still mit durch.
- `CloneRepositoryAsync_ShouldFailEarly_WhenHttpsTokenIsMissing` (Z. 551-568) — HTTPS + kein Token → `InvalidOperationException` vor jedem CLI-Aufruf (`Times.Never`).
- `CloneRepositoryAsync_ShouldNotEmbedToken_InCloneUrl` / `…_ThrowInvalidOperationException_WhenCliFails` / `…_MapAuthenticationErrors` / `…_SanitizeToken_InThrownExceptionMessage` (Z. 523-628) — URL-/Fehler-Semantik des Klons.
- `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush` (Z. 968-1014) — Normalisierung: eingebetteter `oauth2:`-Token in `remote.origin.url` wird vor dem Push per `set-url` entfernt; Push-Env enthält `GIT_TERMINAL_PROMPT`, `GH_TOKEN`, extraheader, kein `NETRC`. **Muster für künftige SSH→HTTPS-Normalisierungs-Asserts.**
- `PushBranchAsync_ShouldNotEmbedToken_InRemoteUrl` / `…_ShouldNotSetRemoteUrl_WhenTokenIsMissing` / `…_ShouldThrow_WhenPushFails` (Z. 1018-1099) — Normalisierungs-Nebenfälle; `set-url` wird bei sauberer URL nicht aufgerufen (`Times.Never`).
- `PullAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm` / `…_ShouldRunGitPull_WithConfiguredCredentials` / `…_ShouldNotRequireEmbeddedToken` / `…_ShouldThrow_WhenGitPullFails` (Z. 1103-~1250) — analoge Pull-Normalisierung.
- `GetDefaultBranchAsync_Env_EnthaeltGitAuth` (Z. 1790-1811) — `ls-remote --symref` erhält Env mit `GH_TOKEN` + extraheader.
- `GetDefaultBranchAsync_ShouldReturnParsedBranch_WhenSymRefCanBeParsed` / `…_ShouldFallbackToMain_WhenSymRefFails` — Auswertelogik.
- `GetRemoteBranchesAsync_ShouldParseAndSortBranches` / `…_ShouldReturnEmpty_WhenCliFails` — `ls-remote --heads`-Auswertung.
- `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` (Z. 1816-1851) — `git fetch --no-tags origin refs/heads/…` und `checkout -B` erhalten Env mit `GH_TOKEN` + extraheader; kein `NETRC`.
- `GetAgentEnvironmentAsync_WithToken_ProvidesGhAndGitAuth` / `…_WithoutToken_OnlyDisablesGitPrompt` / `…_SeparatesKonto_UsesAgentToken` (Z. 1409-1450) — Agent-Env: `GIT_TERMINAL_PROMPT`, `GH_TOKEN`, `GIT_CONFIG_*`; assertiert noch nicht explizit `!ContainsKey("GIT_SSH_COMMAND")`.
- Übrige ~45 Tests: Issues/Alerts/Templates/PR-Status/PR-Completion/Workflow-Runs/Health/Metadata — nicht transportrelevant.

**Testlücke der Anforderung:** Kein Test verifiziert den `git config core.sshCommand`-Aufruf (weder Präsenz noch Abwesenheit), kein Test prüft SSH-URL-Behandlung beim Klon (`git@`/`ssh://` außer dem OhneToken-Test), kein Test prüft `core.sshCommand`-Entfernung in Bestands-Kopien, kein Env-Matcher assertiert `!env.ContainsKey("GIT_SSH_COMMAND")`.

### `GitHubPluginNetrcCleanupTests`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginNetrcCleanupTests.cs` (233 Zeilen, 11 `[Fact]`s) — etabliertes Muster der einmaligen Legacy-Bereinigung (Issue #376): Temp-`_netrc` je Test, Plugin-Konstruktion mit `netrcPath`-Override, Fremd-Einträge bleiben, Fehler nur `LogWarning` (`Ctor_Bereinigungsfehler_WirftNicht`). Fachliches Vorbild für eine `core.sshCommand`-Bestandsbereinigung (dort allerdings `git config --unset` pro Arbeitskopie statt Dateibearbeitung).

### `GitHubPluginTests_GetRepositoryStructureAsync`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests_GetRepositoryStructureAsync.cs`

- `GetRepositoryStructureAsync_ShouldResolveRepositoryId_FromSshUrl` (Z. 148-157) — `git@github.com:owner/repo.git` wird zur ID `owner/repo` aufgelöst; deckt ab, dass `TryExtractRepositoryId` SCP-URLs weiterhin zur ID-Erkennung annimmt (Scope-Annahme der Anforderung: darf bestehen bleiben).
- `SetupDefaultBranch` (Z. 39-48) — mockt `git ls-remote` global; `GetDefaultBranchAsync` läuft damit mit durch.
- Übrige Tests: Trees-API-Parsing, Truncated, Trailing-Slash, Branch-Escaping, Fehlerpfade — nicht transportrelevant.

### `GitPluginBaseTests`

Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs` — Basisklassen-git-Operationen mit gemocktem `ICliRunner`; `environmentVariables` wird als `null` erwartet. Kein `CheckoutPullRequestSourceAsync`-Test vorhanden (die Fork-/Fetch-Logik von `CheckoutPullRequestSourceCoreAsync` ist nur indirekt über `GitHubPluginTests.CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` abgedeckt).

### `GitOrchestrationServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs` — `IGitPlugin` ist gemockt, keine Env-Relevanz.

- `PullRequestErstellenAsync_ShouldResolveRepositoryId_FromSshRepositoryUrl` (Z. 1100-1136) — `GitRepository.RepositoryUrl = "git@github.com:owner/repo.git"` wird über `ExtractRepositoryIdFromUrl` zu `owner/repo` aufgelöst; relevante Annahme für den Umgang mit persistierten SSH-URLs.

### Weitere GitHubPlugin-Konstruktionsstellen in Tests (Testseam-Nutzung)

- `GitHubSinceWindowTests` (Z. 31-32, 46-47) und `GitHubEventSourceTests`/`GitHubNotificationSourceTests` — instanziieren `GitHubPlugin` mit `CreateMissingNetrcPath()` bzw. explizitem Temp-`netrcPath`.
- `BitbucketPluginTests` — BitBucket-Plugin-Tests, ohne `GIT_SSH_COMMAND`-Assert.

### E2E-Nähe (nicht ausgeführt, OsInterface-Lane)

- `ScmEventsE2ETests` (`src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`, `[OsInterface]`, `[Trait("Category","E2E")]`) — reale WPF-App + `ScmE2EFixture`; das Fixture erzeugt ein echtes `GitHubPlugin` über `ScmE2EPluginManager` (`src/Softwareschmiede.App/Services/Testing/ScmE2EPluginManager.cs:27`), aber `ScmE2EGitRunner` verwirft Env und ersetzt die Remote durch einen lokalen Pfad — `ConfigureGitCredentialsAsync` läuft real (schreibt `core.sshCommand` in die Fixture-Kopie), wird aber nicht assertiert.

## Hilfsmethoden

### `GitHubPluginTests`
- `HasGitHubExtraHeader(env, token)` (Z. 1854-1861) — prüft die drei `GIT_CONFIG_*`-Einträge gegen den erwarteten `AUTHORIZATION: basic <base64>`-Wert.
- `SequenceEqual(actual, expected…)` (Z. 1863-1864) — Argumentlisten-Vergleich.
- `CreateMissingNetrcPath()` (Z. 37-38) — garantiert nicht existierender `_netrc`-Pfad als Konstruktor-Testseam.

### `FakeGitHubEventApi`
Datei: `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs` — warteschlangenbasierter `IGitHubEventApiClient`; `Add`/`AddJson`/`AddGraph`/`AddStatus`/`AddFailure` planen Antworten, `Requests` zeichnet auf, `AssertComplete` verifiziert Vollverbrauch.

### `GitHubPluginNetrcCleanupTests` (private Helfer)
- `CreateNetrc(content)` (Z. 218-223) — schreibt Temp-`_netrc`, liefert Pfad.
- `CreatePlugin(netrcPath, loggerMock?)` (Z. 225-232) — Plugin-Konstruktion mit reinen Mocks + `netrcPath`-Override.

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/` — InMemory-`SoftwareschmiededDbContext` für Service-Tests (u. a. `GitOrchestrationServiceTests`).
