# Bestandsaufnahme: Klartext-Token in `_netrc` entfernen

Bestandsaufnahme der Credential-Konfiguration für Host-git-Operationen des `GitHubPlugin` (Issue #376): Schreiben des GitHub-PAT nach `%USERPROFILE%\_netrc` in `ConfigureGitCredentialsAsync`, `NETRC`-Verweis in `GetGitEnvironment` und der bestehende nicht-persistierende `http.extraheader`-Mechanismus in `GetAgentEnvironmentAsync`.

## Zusammenfassung

- `GitHubPlugin.ConfigureGitCredentialsAsync` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, Zeilen 298–390) schreibt heute nach jedem erfolgreichen `git clone` zwei netrc-Stanzas (`machine github.com`, `machine api.github.com`, jeweils `login oauth2` / `password {token}`) per `File.WriteAllText` ins Benutzerprofil (Zeilen 334–355). Fehler beim Schreiben werden nur per `LogWarning` protokolliert.
- `GetGitEnvironment` (Zeilen 180–203) setzt für Host-git-Aufrufe `GIT_TERMINAL_PROMPT=0`, `GIT_SSH_COMMAND`, `GH_TOKEN` und die `NETRC`-Umgebungsvariable auf den Profil-Pfad (Zeilen 196–199). Es gibt **keinen** `GIT_CONFIG_*`/`extraheader`-Eintrag — die HTTPS-Auth der Host-git-Aufrufe läuft faktisch über die geschriebene `_netrc`.
- `GetAgentEnvironmentAsync` (Zeilen 158–178) nutzt bereits das Zielmuster: `GIT_CONFIG_COUNT`/`GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`/`GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64>` — rein prozessbasiert, ohne Persistenz.
- Aufrufer von `GetGitEnvironment`: `CloneRepositoryAsync` (Z. 884), `PushBranchAsync` (Z. 917), `PullAsync` (Z. 941), `GetRemoteBranchesAsync` (Z. 1420) sowie intern in `ConfigureGitCredentialsAsync` (Z. 307). `GetDefaultBranchAsync` führt `git ls-remote --symref` mit `GetGhEnvironment()` aus (Z. 1476) — d. h. ganz ohne git-Auth-Konfiguration.
- `GitPluginBase.CheckoutPullRequestSourceCoreAsync` (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`, Z. 185–222) führt `git fetch`/`checkout` für PR-Quellen aus; `GitHubPlugin` überschreibt `CheckoutPullRequestSourceAsync` nicht und übergibt keine Umgebungsvariablen.
- Referenz für Stanza-Parsing derselben Datei: `BitBucketPlugin.UpdateNetrcEntry` (`plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`, Z. 1487–1521), aufgerufen aus `BitBucketPlugin.GetGitEnvironment` (Z. 195) — schreibt `machine bitbucket.org` bzw. Self-Hosted-Hosts in dieselbe `_netrc`. Außerdem existiert dort mit `GetGitHttpAuthArgs` (Z. 201–213) ein zweites nicht-persistierendes Auth-Muster (`-c credential.helper=` + `-c http.extraheader=...` als CLI-Argumente).
- Es existiert **keine** netrc-Lese-/Bereinigungslogik im `GitHubPlugin` und **kein** Test, der `_netrc`/`NETRC` referenziert (Repo-weite Suche: Treffer nur in `GitHubPlugin.cs`, `BitBucketPlugin.cs` und Doku).

Test-Ausgangszustand: Reguläre Lane `Softwareschmiede.Tests` 2338/2341 bestanden (3 plattformbedingt übersprungen, 0 Fehler), `Softwareschmiede.IntegrationTests` 280/280 bestanden; OS-Interface-Lane wie in der CI mit `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` und `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`: 51 bestanden / 20 übersprungen (`Softwareschmiede.Tests`) bzw. 9/9 (`IntegrationTests`). Keine nachgewiesenen Testfehler — Details und Nachweise in [inventory/tests.md](inventory/tests.md).

## Details

- [Logik](inventory/logic.md) — `GitHubPlugin`-Methoden der Credential-/Auth-Pfade, Referenzimplementierung `BitBucketPlugin`
- [Enums](inventory/enums.md) — `ScmKontoModus`
- [Interfaces](inventory/interfaces.md) — `ICredentialStore`, `ICliRunner`, `IScmAgentAccess`, `GitPluginBase`
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit Läufen/Nachweisen sowie betroffene Testklassen und Hilfsmethoden
