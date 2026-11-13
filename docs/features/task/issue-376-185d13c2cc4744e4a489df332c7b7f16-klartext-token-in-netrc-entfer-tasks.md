# Tasks: Klartext-Token in `_netrc` entfernen (Issue #376)

|| # | Bereich | Aufgabe | Status | Testnachweis |
||---|---------|---------|--------|--------------|
|| 1 | Logik | `GitHubPlugin`: optionales `_netrcPath`-Feld + Konstruktorparameter `string? netrcPath = null` am vollen Konstruktor ergänzen; `ResolveNetrcPath()` implementieren | Offen | — |
|| 2 | Logik | `GitHubPlugin`: `RemoveNetrcStanzas(content, hosts)` als private statische Parsefunktion implementieren (Stanza-Parsing analog `BitBucketPlugin.UpdateNetrcEntry`, Z. 1498–1515; Rückgabe `null` bei keinem Treffer) | Offen | — |
|| 3 | Logik | `GitHubPlugin`: `RemoveLegacyNetrcEntries()` implementieren (lesen → GitHub-Stanzas entfernen → nur bei Änderung schreiben; bei leerem Rest `File.Delete`; komplett try/catch → `LogWarning`) und aus dem Konstruktor aufrufen | Offen | — |
|| 4 | Logik | `GitHubPlugin`: `AddGitHubHttpAuthHeader(env, token)` als privaten statischen Helfer extrahieren (`GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`, `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64>`) | Offen | — |
|| 5 | Logik | `GetGitEnvironment`: `NETRC`-Belegung (Z. 196–199) entfernen und bei vorhandenem Token `AddGitHubHttpAuthHeader` aufrufen | Offen | — |
|| 6 | Logik | `GetAgentEnvironmentAsync`: extraheader-Belegung (Z. 172–175) auf `AddGitHubHttpAuthHeader` umstellen (keine Verhaltensänderung) | Offen | — |
|| 7 | Logik | `ConfigureGitCredentialsAsync`: kompletten netrc-Schreibblock (Z. 334–355) entfernen | Offen | — |
|| 8 | Logik | `GetDefaultBranchAsync`: `GetGhEnvironment()` → `GetGitEnvironment()` umstellen (Z. 1476) | Offen | — |
|| 9 | Logik | `CheckoutPullRequestSourceAsync` im `GitHubPlugin` überschreiben: `CheckoutPullRequestSourceCoreAsync(..., GetGitEnvironment(), ct)` aufrufen | Offen | — |
|| 10 | Logik | Veraltete Kommentare anpassen (Z. 357–358 netrc-Bezug, Z. 878–879 „nur GH_TOKEN") | Offen | — |
|| 11 | Tests | Testklasse `GitHubPluginNetrcCleanupTests` in neuer Datei `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginNetrcTests.cs` anlegen inkl. Temp-Pfad-Hilfsmethoden und `IDisposable`-Aufräumen | Offen | — |
|| 12 | Tests | Cleanup-Test: `_netrc` mit GitHub- + Fremd-Stanzas → nur GitHub-Einträge entfernt, Fremd-Inhalt unverändert | Offen | — |
|| 13 | Tests | Cleanup-Test: `_netrc` nur mit GitHub-Stanzas → Datei gelöscht | Offen | — |
|| 14 | Tests | Cleanup-Test: `_netrc` ohne GitHub-Stanzas → Inhalt unverändert (kein Rewrite) | Offen | — |
|| 15 | Tests | Cleanup-Test: nicht-existenter `netrcPath` → keine Datei angelegt | Offen | — |
|| 16 | Tests | Cleanup-Test: ungültiger `netrcPath` (Verzeichnis) → Konstruktion wirft nicht, `LogWarning` verifiziert | Offen | — |
|| 17 | Tests | Env-Test: `CloneRepositoryAsync`/git-Env enthält `GIT_CONFIG_*`-extraheader + `GH_TOKEN`, kein `NETRC` | Offen | — |
|| 18 | Tests | Env-Test: ohne Token nur `GIT_TERMINAL_PROMPT` + `GIT_SSH_COMMAND`, keine `GIT_CONFIG_*`/`GH_TOKEN`/`NETRC` | Offen | — |
|| 19 | Tests | Env-Test: `GetDefaultBranchAsync` übergibt Env mit `GIT_CONFIG_*` an `ls-remote --symref` | Offen | — |
|| 20 | Tests | Env-Test: `CheckoutPullRequestSourceAsync` übergibt Env mit `GIT_CONFIG_*` an `git fetch`/`checkout` | Offen | — |
|| 21 | Tests | Bestehende Konstruktor-Aufrufe auf Temp-`netrcPath` umstellen: `GitHubPluginTests` (Z. 26), `GitHubPluginTests_GetRepositoryStructureAsync` (Z. 30), `GitHubSinceWindowTests` (Z. 31, 45) | Offen | — |
|| 22 | Tests | Bestehende Env-Asserts schärfen: `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled`, `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush`, `PullAsync_ShouldRunGitPull_WithConfiguredCredentials` um `GIT_CONFIG_*`-Präsenz/`NETRC`-Abwesenheit erweitern | Offen | — |
|| 23 | Verifikation | `dotnet format Softwareschmiede.slnx` ausführen; vollständiger Build beider Testprojekte | Offen | — |
|| 24 | Verifikation | Reguläre Testlane laufen lassen: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/... --filter "Category!=OsInterface"` + IntegrationTests | Offen | — |
