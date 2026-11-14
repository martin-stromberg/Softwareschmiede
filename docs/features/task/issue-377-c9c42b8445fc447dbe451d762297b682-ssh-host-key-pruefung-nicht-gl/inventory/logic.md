# Logik

## `GitHubPlugin`

Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` (1833 Zeilen; zweite Teilklasse in `GitHubPlugin.Events.cs`)

Klasse: `public sealed partial class GitHubPlugin : GitPluginBase<GitHubPlugin>, IScmEventSource, IScmEventResponder, IScmAgentAccess, IScmNotificationSource` (Z. 19). Konstruktoren Z. 124-152; der volle Konstruktor (Z. 140-152) ruft am Ende `RemoveLegacyNetrcEntries()` auf.

### Klassenkonstanten/-felder (Z. 21-39)

| Member | Wert/Zweck |
|--------|------------|
| `GitHubTokenCredentialKey` | `"Softwareschmiede.GitHub.Token"` — Credential-Store-Key des Benutzer-Tokens |
| `GitHubAgentTokenCredentialKey` | `"Softwareschmiede.GitHub.AgentToken"` — Token des separaten Kontos |
| `RepositoryUrlKey` / `RepositoryNameKey` | Feld-Keys der Repository-Verknüpfung (`GetRepositoryLinkFields`, Z. 105-121) |
| `EmbeddedTokenPattern` (Z. 25) | Regex `oauth2:[^@\s]+@` — erkennt/entfernt eingebettete Legacy-Tokens in Remote-URLs (`NormalizeRemoteUrlAsync`, `SanitizeSensitiveOutput`) |
| `LegacyNetrcHosts` (Z. 26) | `["github.com", "api.github.com"]` — Stanza-Hosts der `_netrc`-Legacy-Bereinigung |
| `NetrcPathEnvironmentVariable` (Z. 35) | `"SOFTWARESCHMIEDE_NETRC_PATH"` — Env-Testseam für `ResolveNetrcPath` |

### Umgebungs-/Credential-Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetGhEnvironment()` (Z. 154-163) | private | Liefert `{GH_TOKEN}` falls Token vorhanden; Aufrufer ausschließlich `gh`-CLI-Aufrufe: `CreateIssueAsync` (Z. 662), `CreatePullRequestAsync` (Z. 1154), `CompletePullRequestAsync` (Z. 1307), `MarkPullRequestReadyAsync` (Z. 1344) |
| `GetGitEnvironment(string? token = null)` (Z. 206-230) | private | Baut die Prozessumgebung aller Host-git-Aufrufe: `GIT_TERMINAL_PROMPT=0` (Z. 212), **`GIT_SSH_COMMAND=ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null` (Z. 215)** — die erste der beiden zu entfernenden Belegungen — und bei vorhandenem Token `GH_TOKEN` + `AddGitHubHttpAuthHeader` (Z. 217-227). Token-Fallback: `ICredentialStore.GetCredential(GitHubTokenCredentialKey)` |
| `AddGitHubHttpAuthHeader(env, token)` (Z. 198-204) | private static | Setzt `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`, `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64("x-access-token:"+token)>`. **Der extraheader-Key ist fest auf github.com verdrahtet** — für GHE-Hosts würde git die Auth nicht an den richtigen Host binden. Geteilt von `GetGitEnvironment` und `GetAgentEnvironmentAsync` |
| `GetAgentEnvironmentAsync(konto, ct)` (Z. 175-190) | public (`IScmAgentAccess`) | Agenten-Umgebung: `GIT_TERMINAL_PROMPT=0`, bei Token `GH_TOKEN` + `AddGitHubHttpAuthHeader`. **Setzt bereits heute kein `GIT_SSH_COMMAND`** |
| `CredentialKeyFor(konto)` (Events.cs:345-346) | private static | Wählt Token-Key je Kontomodus |

### git-Transport-Methoden (alle über `GetGitEnvironment`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CloneRepositoryAsync(repositoryUrl, targetPath, ct)` (Z. 1037-1072) | public override | Fail-Fast `InvalidOperationException("…GitHub-Token fehlt…")` **nur** wenn `IsHttpsRepositoryUrl(url) && token leer` (Z. 1043-1047) — SSH-URLs klonen heute tokenlos. `git clone <url> <target>` mit `GetGitEnvironment(token)` (Z. 1051-1056); Auth-Fehler → verständliche `InvalidOperationException` via `IsAuthenticationFailure`; danach `ConfigureGitCredentialsAsync(targetPath, repositoryUrl, ct)` (Z. 1071) |
| `ConfigureGitCredentialsAsync(localPath, repositoryUrl, ct)` (Z. 492-561) | private | Nur bei Token aktiv (sonst `LogWarning` + Return, Z. 494-499). Führt mit `GetGitEnvironment(token)` aus: `git config user.name "Softwareschmiede Bot"` (Z. 504-514), `git config user.email bot@softwareschmiede.local` (Z. 516-526), `git remote set-url origin <url>` **nur für `https://`-URLs** (Z. 530-547), **`git config core.sshCommand "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null"` (Z. 549-560)** — die zweite zu entfernende Belegung; persistiert den Bypass in `.git/config` und hebelt damit auch Host-Key-Prüfung für spätere, nicht vom Plugin gestartete git-Aufrufe (Agenten/Nutzer) aus. Alle Einzelfehler nur `LogWarning`. Einziger Aufrufer: `CloneRepositoryAsync` |
| `PushBranchAsync(localPath, branchName, ct)` (Z. 1075-1096) | public override | `NormalizeRemoteUrlAsync` vorab (Z. 1080), dann `git push --set-upstream origin <branch>` mit `GetGitEnvironment(token)`; Fehler → `InvalidOperationException` |
| `PullAsync(localPath, ct)` (Z. 1099-1120) | public override | `NormalizeRemoteUrlAsync` vorab (Z. 1104), dann `git pull` mit `GetGitEnvironment(token)`; Fehler → `InvalidOperationException` |
| `GetRemoteBranchesAsync(repositoryUrl, ct)` (Z. 1583-1612) | public override | `git ls-remote --heads <url>` mit `GetGitEnvironment()`; Fehler → `LogWarning` + leere Liste |
| `GetDefaultBranchAsync(repositoryUrl, ct)` (Z. 1639-1666) | public override | `git ls-remote --symref <url> HEAD` mit `GetGitEnvironment()`; bei Fehler/Nicht-Parse Fallback `"main"` + `LogWarning` |
| `CheckoutPullRequestSourceAsync(localPath, checkoutSpec, ct)` (Z. 1674-1678) | public override | Delegiert an `GitPluginBase.CheckoutPullRequestSourceCoreAsync(localPath, spec, GetGitEnvironment(), ct)` — `git fetch --no-tags <remote> <sourceRef>` / `checkout -B` / optional `rev-parse HEAD`-Verifikation. Bei Fork-PRs ist `<remote>` die `SourceRepositoryUrl` (kann `ssh_url` sein, siehe `ParsePullRequest`) |

### Remote-URL- und ID-Behandlung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NormalizeRemoteUrlAsync(localPath, ct)` (Z. 448-490) | private | `git config remote.origin.url` lesen (**ohne Env**, Z. 451-456); nur wenn `EmbeddedTokenPattern` matcht, `git remote set-url origin <bereinigte URL>` (**ohne Env**, Z. 475-480). Fehler nur `LogWarning`. Aufgerufen von `PushBranchAsync` und `PullAsync`. **Natürlicher Erweiterungspunkt für SSH→HTTPS-Normalisierung bestehender Arbeitskopien** |
| `IsHttpsRepositoryUrl(repositoryUrl)` (Z. 399-401) | private static | `Uri.TryCreate` + Scheme == `https` |
| `TryExtractRepositoryId(repositoryUrl)` (Z. 1782-1832) | private static | Extrahiert `owner/repo`: `.git`-Suffix strip; ohne `://` SCP-Pfad-Parser (`git@host:owner/repo`, Z. 1793-1810); sonst `Uri`-Pfadsegmente (deckt `https://` und `ssh://` ab — Schema wird nicht geprüft). `null` bei unparsebar. Aufrufer: `NormalizeRepositoryId`, `GetRepositoryStructureLoadResultAsync` |
| `NormalizeRepositoryId(repositoryId)` (Z. 811-820) | private static | `TryExtractRepositoryId` mit Fallback auf getrimmten Input; wird von allen `repositoryId`-basierten Methoden aufgerufen (GetIssues/GetAlerts/CanCreateIssue/CreateIssue/GetIssueTemplates/GetOpenPullRequests/PR-Status/Completion) |
| `ParseEventRepository(value)` (Events.cs:397-424) | private static | Strenger Parser → `(Host, Owner, Name)`; akzeptiert SCP `git@host:path` (Regex Z. 401-408), `https://host/path` (default-Port, kein UserInfo) und `ssh://git@host[:22]/path` (Z. 409-417); wirft `ScmProviderException(Permanent)` bei Abweichung. GHE-fähig: liefert beliebigen DNS-Host zurück |
| `ParsePullRequest(element, repositoryId)` (Z. ~985-1034) | private static | `sourceRepositoryUrl = clone_url ?? ssh_url` (Z. 1015) — **kann eine `ssh://`-/`git@`-URL in den PR-Checkout-Pfad einspeisen** |

### Legacy-Bereinigung (Issue-#376-Muster)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RemoveLegacyNetrcEntries()` (Z. 276-348) | private | Einmalig pro Instanziierung aus dem Konstruktor (Z. 151): `_netrc` lesen → `RemoveNetrcStanzas(content, LegacyNetrcHosts)` → Rest zurückschreiben oder Datei löschen; alles try/catch → nur `LogWarning`/`LogInformation`, nie werfend |
| `RemoveNetrcStanzas(content, hosts)` (Z. 360-397) | private static | Tokenisierter Stanza-Parser; liefert bereinigten Inhalt oder `null` ohne Treffer |
| `ResolveNetrcPath()` (Z. 239-255) | private | Auflösung: `netrcPath`-Ktor-Parameter → `SOFTWARESCHMIEDE_NETRC_PATH` → `%USERPROFILE%\_netrc` |
| `SanitizeSensitiveOutput(message)` (Z. 416-437) | private | Maskiert beide Token + `oauth2:`-Credentials in Fehlertexten |
| `IsAuthenticationFailure(error)` (Z. 403-414) | private static | stderr-Heuristik für Auth-Fehler („terminal prompts disabled", 403 u. a.) |

## `GitPluginBase<TPlugin>`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckoutPullRequestSourceAsync` (Z. 178-182) | public virtual | Default: `CheckoutPullRequestSourceCoreAsync(localPath, spec, null, ct)` — Basisklasse übergibt keine Env |
| `CheckoutPullRequestSourceCoreAsync(localPath, spec, environmentVariables, ct)` (Z. 185-222) | protected | Fork-fähiger PR-Checkout: bei `TargetRepositoryId == SourceRepositoryId` Remote `"origin"`, sonst `checkoutSpec.SourceRepositoryUrl` (Fehler wenn leer, Z. 201-203); `fetch --no-tags`, `checkout -B`, optional `rev-parse HEAD`-Verifikation gegen `HeadSha` |
| `RunGitAsync(args, workingDirectory, ct, environmentVariables)` (Z. 245-252) | protected | Einzige RunAsync-Brücke der Basisklasse; `environmentVariables` default `null` |
| `CreateBranchAsync` / `CheckoutRemoteBranchAsync` / `CommitAsync` / `ResetAsync` / `AddAllAsync` / `EnsureGitRepositoryAsync` | public/protected | Lokale git-Operationen ohne Remote-Kontakt — für diese Anforderung nicht relevant |

## `BitBucketPlugin` (parallele Konfiguration — laut Anforderung außer Scope)

Datei: `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

| Methode | Zeilen | Relevanz |
|---------|--------|----------|
| `GetGitEnvironment()` | 164-199 | Setzt `GIT_TERMINAL_PROMPT=0` und **`GIT_SSH_COMMAND` mit identischem Wert** (Z. 172); schreibt zusätzlich bei jedem Aufruf `machine <host>`-Stanza in die `_netrc` (`UpdateNetrcEntry`, Z. 1487+) — anders als GitHub persistiert Bitbucket Auth-Daten weiterhin |
| Aufrufer von `GetGitEnvironment` | 305 (`clone`), 319 (`pull`), 333 (`push`), 940 (`ls-remote --heads`), 1051 (`ls-remote --symref`) | Alle git-Transportpfade des BitBucket-Plugins |
| `GetGitHttpAuthArgs()` | 201-213 | `-c credential.helper= -c http.extraheader=Authorization: Basic …` nur im SelfHosted-Modus (CLI-Args statt Env) |
| `BuildAuthenticatedCloneUrl` | 235-244 | Bettet `user:appPassword` in die Clone-URL ein (Cloud: bitbucket.org) |
| `ResolveGitCloneUrl` | 252-279 | SelfHosted Browser-/API-URL → SCM-Clone-URL |
| `TryExtractRepositoryId` | 1444-1485 | Reine URI-Pfad-Extraktion; kein SCP-Format |

**Kein `core.sshCommand`-Schreiben im BitBucketPlugin** (Repo-weite Suche: der einzige `core.sshCommand`-String steht in `GitHubPlugin.cs:552`).

## Aufrufer der Plugin-git-Operationen in `src/`

| Aufrufer | Stelle | Aufgerufene Methode(n) |
|----------|--------|------------------------|
| `EntwicklungsprozessService` (`src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`) | Z. 581 (`PrepareCloneDirectoryAsync`) | `CloneRepositoryAsync(repository.RepositoryUrl, lokalerKlonPfad)` — `RepositoryUrl` kommt unverändert aus `GitRepository` |
| | Z. 611, 676 (`SetupBranchAsync`/`CreateNewTaskBranchAsync`) | `GetDefaultBranchAsync(repositoryUrl)` |
| | Z. 647 (`CheckoutReviewSourceAsync`) | `CheckoutPullRequestSourceAsync` — `TargetRepositoryUrl = repository.RepositoryUrl`, `SourceRepositoryUrl` aus `PullRequestReferenz` |
| | Z. 481-485 (`GetRemoteBranchesAsync`), Z. 971-983 (`ValidateBaseBranchExistsAsync`) | `GetRemoteBranchesAsync(gitRepository.RepositoryUrl)` — u. a. Basis-Branch-Validierung beim Aufgabenstart |
| | Z. 361, 379 | `PushBranchAsync`, `PullAsync` |
| `GitOrchestrationService` (`src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`) | Z. 111, 209 | `PushBranchAsync` (Push bzw. Push vor PR-Erstellung) |
| | Z. 134 | `PullAsync` |
| | Z. 297-326, 403-447 | `ResolveRepositoryIdAsync`/`ExtractRepositoryIdFromUrl` — eigener SCP-Parser für `git@host:owner/repo` (Z. 410-426); Fehler bei ungültiger URL |
| `ScmBackmergeService` (`src/Softwareschmiede/Application/Services/ScmBackmergeService.cs`) | Z. 162-177 (`LadeHauptbranchAsync`) | `GetDefaultBranchAsync(kandidat.RepositoryUrl)` |
| `RepositoryAssignViewModel` (`src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`) | Z. ~330-347 | `GetRemoteBranchesAsync` + `GetDefaultBranchAsync(repositoryUrl)` beim Befüllen der Branch-Vorschläge im „Repository verknüpfen"-Dialog; Fehler → manuelle Eingabe |
| `ProjektService` (`src/Softwareschmiede/Application/Services/ProjektService.cs`) | Z. 133-197, 445-501 | `AddRepositoryAsync` persistiert `GitRepository.RepositoryUrl`; `ValidateRequiredFields`/`ResolveRepositoryUrl` prüfen nur Nicht-Leere — **keine Formatvalidierung, SSH-URLs sind speicherbar** |
| `AufgabeService` / `PullRequestReferenzService` | AufgabeService.cs Z. 351-400; PullRequestReferenzService.cs Z. 75 | `PullRequestReferenz.SourceRepositoryUrl` wird aus `PullRequest.SourceRepositoryUrl` persistiert (Herkunft: `clone_url ?? ssh_url`) |
| `ScmE2EPluginManager`/`ScmE2EGitRunner` (`src/Softwareschmiede.App/Services/Testing/`) | ScmE2EPluginManager.cs Z. 27-29; ScmE2EGitRunner.cs Z. 10-21 | E2E-Fixture erzeugt echtes `GitHubPlugin`; `ScmE2EGitRunner` lässt nur `git`-Kommandos zu, ersetzt die Remote-URL durch einen lokalen Pfad und **verwirft `environmentVariables` vollständig** (`null` an inner) — `GIT_SSH_COMMAND`/`GIT_CONFIG_*` erreichen im E2E nie den Prozess, `ConfigureGitCredentialsAsync` läuft aber real (inkl. `core.sshCommand`-Schreiben in die Fixture-Kopie) |

## `GitHubHttpApiClient` (Host-Auflösung für API-Pfade)

Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubHttpApiClient.cs`

- `BuildRequestUri` (Z. 63-71): `github.com` → `api.github.com`/`api.github.com/graphql`; GHE → `https://{host}/api/v3/…` bzw. `…/api/graphql`.
- `ValidateHost` (Z. 77-88): strikter DNS-Host-Check, IDN-Normalisierung — kein UserInfo/Port/Pfad/Query/Fragment.
- Relevanz: GHE-Hosts sind API-seitig voll unterstützt; die git-Auth (`AddGitHubHttpAuthHeader`) ist dagegen fest auf `http.https://github.com/.extraheader` verdrahtet (Offene Frage 4 der Anforderung).
