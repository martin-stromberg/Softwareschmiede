# Logik — Credential-/Auth-Pfade der Host-git-Operationen

## `GitHubPlugin`

Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` (partial; weitere Teile in `GitHubPlugin.Events.cs`, `GitHubPlugin.EventResponses.cs`)

Klasse: `public sealed partial class GitHubPlugin : GitPluginBase<GitHubPlugin>, IScmEventSource, IScmEventResponder, IScmAgentAccess, IScmNotificationSource` (Z. 19)

Relevante Felder/Konstanten:

| Member | Typ | Beschreibung |
|--------|-----|--------------|
| `GitHubTokenCredentialKey` | `const string` (Z. 21) | Credential-Store-Key `"Softwareschmiede.GitHub.Token"` für den Benutzer-PAT |
| `GitHubAgentTokenCredentialKey` | `const string` (Z. 22) | Key `"Softwareschmiede.GitHub.AgentToken"` für das separate Konto |
| `EmbeddedTokenPattern` | `static readonly Regex` (Z. 25) | `oauth2:[^@\s]+@` — erkennt eingebettete Legacy-Tokens in Remote-URLs/Fehlertexten |
| `_cliRunner` | `ICliRunner` (Z. 26) | Prozessausführung (git/gh) |
| `_credentialStore` | `ICredentialStore` (Z. 27) | Token-Quelle |
| `_logger` | `ILogger<GitHubPlugin>` (Z. 28) | Logging |

### Methoden der Credential-Konfiguration

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetGhEnvironment()` (Z. 137–146) | `private` | Liefert nur `GH_TOKEN` (aus `GitHubTokenCredentialKey`); wird für `gh`-CLI-Aufrufe und — Auffälligkeit — für `git ls-remote --symref` in `GetDefaultBranchAsync` verwendet. Keine git-Auth-Konfiguration. |
| `GetAgentEnvironmentAsync(ScmKontoModus, CancellationToken)` (Z. 158–178) | `public` (`IScmAgentAccess`) | Setzt `GIT_TERMINAL_PROMPT=0` immer; bei vorhandenem Token (`CredentialKeyFor(konto)`) zusätzlich `GH_TOKEN` und `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`, `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64("x-access-token:"+token)>` (Z. 172–175). Ausschließlich Prozessumgebung, keine Persistenz. |
| `GetGitEnvironment(string? token = null)` (Z. 180–203) | `private` | Prozessumgebung für Host-git-Aufrufe: `GIT_TERMINAL_PROMPT=0` (Z. 186), `GIT_SSH_COMMAND=ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null` (Z. 189); bei Token zusätzlich `GH_TOKEN` (Z. 194) und `NETRC=<UserProfile>\_netrc` bzw. `~/.netrc` (Z. 196–199, Kommentar: „.netrc bleibt als optionaler Fallback erhalten (For curl, das git unter Windows/HTTPS nutzt)"). Kein `GIT_CONFIG_*`-Eintrag. |
| `IsHttpsRepositoryUrl(string)` (Z. 205–207) | `private static` | Prüft, ob die Repository-URL HTTPS ist (steuert den frühen Token-Zwang beim Klonen). |
| `IsAuthenticationFailure(string)` (Z. 209–220) | `private static` | Erkennt Auth-Fehler in git/gh-StdErr (u. a. „terminal prompts disabled", „could not read username", „authentication failed", „403"). |
| `SanitizeSensitiveOutput(string?)` (Z. 222–243) | `private` | Ersetzt beide gespeicherten Tokens durch `***` und maskiert `oauth2:...@`-Muster via `EmbeddedTokenPattern` zu `oauth2:***@`. |
| `IsNoCommitsBetweenFailure(string)` (Z. 245–252) | `private static` | Erkennt „no commits between"-Fehler für PR-Erstellung. |
| `NormalizeRemoteUrlAsync(string localPath, CancellationToken)` (Z. 254–296) | `private` | Liest `git config remote.origin.url`, entfernt per `EmbeddedTokenPattern` eingebettete Tokens und setzt die URL via `git remote set-url` neu. Läuft ohne Umgebungsvariablen (`environmentVariables: null`). |
| `ConfigureGitCredentialsAsync(string localPath, string repositoryUrl, CancellationToken)` (Z. 298–390) | `private` | Nach erfolgreichem Clone: (1) Token lesen, ohne Token nur `LogWarning` + Return (Z. 300–305); (2) `git config user.name`/`user.email` (Z. 310–332); (3) **netrc-Schreibblock (Z. 334–355):** `netrcPath = <UserProfile>\_netrc` bzw. `~/.netrc` (Z. 335–337), `netrcContent` mit Stanzas `machine github.com` und `machine api.github.com`, jeweils `login oauth2`/`password {token}` (Z. 339–345), `File.WriteAllText(netrcPath, netrcContent)` im try/catch — Fehler nur `LogWarning` (Z. 347–355); (4) `git remote set-url origin <repositoryUrl>` für HTTPS-URLs ohne eingebettetes Token (Z. 359–376); (5) `git config core.sshCommand` (Z. 379–389). Wird ausschließlich von `CloneRepositoryAsync` (Z. 900) aufgerufen. |
| `CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken)` (Z. 866–901) | `public override` | Wirft bei HTTPS-URL ohne Token früh `InvalidOperationException` (Z. 872–876); `git clone` mit unauthentifizierter URL und `GetGitEnvironment(token)` (Z. 880–885); Auth-Fehler-Mapping via `IsAuthenticationFailure`/`SanitizeSensitiveOutput` (Z. 887–897); danach `ConfigureGitCredentialsAsync` (Z. 900). |
| `PushBranchAsync(string localPath, string branchName, CancellationToken)` (Z. 904–925) | `public override` | `NormalizeRemoteUrlAsync` (Z. 909), dann `git push --set-upstream origin <branch>` mit `GetGitEnvironment(token)` (Z. 913–918). |
| `PullAsync(string localPath, CancellationToken)` (Z. 928–949) | `public override` | `NormalizeRemoteUrlAsync` (Z. 933), dann `git pull` mit `GetGitEnvironment(token)` (Z. 937–942). |
| `GetRemoteBranchesAsync(string repositoryUrl, CancellationToken)` (Z. 1412–1441) | `public override` | `git ls-remote --heads <url>` mit `GetGitEnvironment()` (Z. 1416–1421); parst `refs/heads/`-Ausgabe. |
| `GetDefaultBranchAsync(string repositoryUrl, CancellationToken)` (Z. 1468–1495) | `public override` | `git ls-remote --symref <url> HEAD` mit `GetGhEnvironment()` (Z. 1472–1477) — enthält nur `GH_TOKEN`, keinerlei git-Auth-Konfiguration; Fallback `„main"` bei Misserfolg (Z. 1493–1494). |
| `CredentialKeyFor(ScmKontoModus)` | `private static` | `GitHubPlugin.Events.cs` Z. 345–346: wählt `GitHubAgentTokenCredentialKey` bei `SeparatesKonto`, sonst `GitHubTokenCredentialKey`. |

Weitere `GetGhEnvironment()`-Aufrufer (alle `gh`-CLI, für die Anforderung nicht relevant, aber vollständigkeithalber): `CreateIssueAsync` (Z. 491), `CreatePullRequestAsync` (Z. 983), `CompletePullRequestAsync` (Z. 1136), `MarkPullRequestReadyAsync` (Z. 1173).

Abonnierte Events: keine.
Publizierte Events: keine (Ereignispfade laufen über `IScmEventSource`/`IScmNotificationSource` in `GitHubPlugin.Events.cs`, ohne Bezug zur netrc-Problematik).

## `GitPluginBase<TPlugin>`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckoutPullRequestSourceCoreAsync(...)` (Z. 185–222) | `protected` | Führt `git fetch --no-tags <remote> <sourceRef>`, `git checkout -B ... FETCH_HEAD` und optional `git rev-parse HEAD` aus; akzeptiert optionale `environmentVariables`. `GitHubPlugin` überschreibt `CheckoutPullRequestSourceAsync` **nicht** — der Basispfad läuft ohne Umgebungsvariablen (Z. 178–182). |
| `RunGitAsync(args, workingDirectory, ct, environmentVariables)` (Z. 245–252) | `protected` | Delegiert an `_cliRunner.RunAsync("git", ...)`. |
| Abstrakte Member | — | u. a. `CloneRepositoryAsync` (Z. 42), `PushBranchAsync` (Z. 43), `PullAsync` (Z. 44), `GetRemoteBranchesAsync` (Z. 72), `GetDefaultBranchAsync` (Z. 73) — alle im `GitHubPlugin` implementiert (s. o.). |

## `BitBucketPlugin` (Referenz, laut Anforderung unverändert zu lassen)

Datei: `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetGitEnvironment()` (Z. 164–199) | `private` | Setzt `GIT_TERMINAL_PROMPT`/`GIT_SSH_COMMAND`; bei vorhandenen Credentials ermittelt es den Host (`bitbucket.org` oder Self-Hosted-URL-Host, Z. 181–193) und ruft `UpdateNetrcEntry(netrcPath, host, user, appPassword)` auf (Z. 195) — schreibt in **dieselbe** `%USERPROFILE%\_netrc`/`~/.netrc` (Z. 177–179). |
| `GetGitHttpAuthArgs()` (Z. 201–213) | `private` | Nicht-persistierendes Auth-Muster für Self-Hosted: CLI-Argumente `-c credential.helper=` (deaktiviert GCM/Keychain) und `-c http.extraheader=Authorization: Basic <base64>` — Alternative zum `GIT_CONFIG_*`-Ansatz, bereits im Repo vorhanden. |
| `UpdateNetrcEntry(string netrcPath, string host, string user, string appPassword)` (Z. 1487–1521) | `private static` | Stanza-Parser für das netrc-`machine`-Block-Format: liest bestehende Datei (Z. 1489), ersetzt den Block `machine <host>` samt Folgezeilen bis zur nächsten `machine `-Zeile (Z. 1498–1515) oder hängt ihn an (Z. 1517–1518), schreibt via `File.WriteAllText` zurück (Z. 1520). Referenzimplementierung für das in der Anforderung geforderte Stanza-Parsing. |
