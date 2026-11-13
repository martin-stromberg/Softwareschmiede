# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Kein GitHub-Token mehr nach `_netrc`/`.netrc` schreiben; Pfad enthält keine plugin-geschriebenen Klartext-Tokens | Schritt 4 (netrc-Schreibblock `GitHubPlugin.cs` Z. 334–355 komplett entfernt); Schritt 2 (`NETRC`-Env-Belegung Z. 196–199 entfällt); Schritt 1 (Konstruktor-Bereinigung entfernt Altbestände) | `Ctor_LegtKeineNetrcAn_WhenFehlend` (kein Anlegen), `Ctor_LaesstNetrcUnveraendert_OhneGithubStanzas` (kein Rewrite), Env-Tests `…_SetztExtraHeaderUndKeinNetrc` (kein `NETRC`) | Abgedeckt |
| In existierender `_netrc` werden `machine github.com`/`machine api.github.com` entfernt, Fremd-Einträge bleiben | Schritt 1 + Programmablauf „Einmalige `_netrc`-Bereinigung" (Stanza-Parsing analog `BitBucketPlugin.UpdateNetrcEntry`, Zurückschreiben nur bei Änderung) | `Ctor_BereinigtGithubStanzas_UndBehalteFremdeintraege` (inkl. `machine bitbucket.org`/Self-Hosted), `Ctor_LoeschtNetrc_WennNurGithubStanzas` | Abgedeckt |
| `_netrc` nach Bereinigung leer → Datei löschen; nicht existierende Datei wird nicht angelegt | Validierungsregeln + Programmablauf 1, Schritte 3/5/6 (`File.Exists`-Kurzschluss, `File.Delete` bei leerem Rest) | `Ctor_LoeschtNetrc_WennNurGithubStanzas`, `Ctor_LegtKeineNetrcAn_WennFehlend` | Abgedeckt |
| `clone`/`push`/`pull`/`ls-remote` gegen private Repos funktionieren nicht-interaktiv (`GIT_TERMINAL_PROMPT=0` bleibt); Auth via nicht-persistierendem Mechanismus | Schritt 2 (`GIT_CONFIG_*`/`http.extraheader` in `GetGitEnvironment` via `AddGitHubHttpAuthHeader`); Schritt 3 (`GetDefaultBranchAsync` → `GetGitEnvironment()`, `CheckoutPullRequestSourceAsync`-Override) | `GetGitEnvironment_MitToken_SetztExtraHeaderUndKeinNetrc`, `GetGitEnvironment_OhneToken_…`, Env-Matcher-Erweiterungen an Clone-/Push-/Pull-Tests, `GetDefaultBranchAsync_Env_EnthaeltGitAuth`, `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` | Abgedeckt |
| Auth-Fehler weiterhin über `IsAuthenticationFailure`/`SanitizeSensitiveOutput`; Token nicht in URLs/Logs/Fehlermeldungen | Unverändertes Fehler-Mapping (Programmablauf 2, Schritt 4); extraheader hält Token aus URL/Args; `SanitizeSensitiveOutput` bleibt | Bestehende Tests (`…_ShouldMapAuthenticationErrors_…`, `…_ShouldSanitizeToken_…`, `…_ShouldNotEmbedToken_…`) unverändert + neue Env-Asserts | Abgedeckt |
| Kein Token im `ICredentialStore` → Warnung, kein Dateizugriff; HTTPS-Clone wirft früh | Verhalten unverändert (Programmablauf 3; Szenario 7; Bereinigung läuft token-unabhängig im Konstruktor) | Bestehender Test `CloneRepositoryAsync_ShouldFailEarly_WhenHttpsTokenIsMissing`; `GetGitEnvironment_OhneToken_NurTerminalPromptUndSsh` | Abgedeckt |
| Bereinigungsfehler (gesperrte/unlesbare Datei) brechen Ablauf nicht, nur `LogWarning` | Validierungsregel „Bereinigungs-Laufzeitfehler"; `RemoveLegacyNetrcEntries` komplett in try/catch | `Ctor_Bereinigungsfehler_WirftNicht` (inkl. `LogWarning`-Verify) | Abgedeckt |
| Scope-Ausschluss: `BitBucketPlugin` unverändert, dessen `_netrc`-Einträge erhalten | Designentscheidung + Validierungsregel (nur exakte GitHub-Stanzas entfernen) | `Ctor_BereinigtGithubStanzas_UndBehalteFremdeintraege` | Abgedeckt |
| Scope-Ausschluss: `GetAgentEnvironmentAsync`, `NormalizeRemoteUrlAsync`, `SanitizeSensitiveOutput` verhaltensgleich | Designentscheidungen (nur Helfer-Extraktion ohne Verhaltensänderung am Agent-Pfad) | Bestehende `GetAgentEnvironmentAsync_*`-Tests bleiben unverändert | Abgedeckt |
| ACL-restriktive Schreibvariante nur Fallback, primär Streichen | Designentscheidung: verworfen mit Begründung (extraheader deckt dieselben Hosts ab wie bisher `_netrc`) | — (Nicht-Umsetzung, begründet) | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| `_netrc`-Bereinigung bei Plugin-Instanziierung | — | Nicht erforderlich mit Begründung: kein UI-auslösbarer Benutzerfluss; Datei-Migration ist über öffentliche Oberfläche (`new GitHubPlugin(…, netrcPath)`) direkt und präziser per Unit-Test prüfbar |
| Klonen/Push/Pull/ls-remote mit nicht-persistenter Auth | — | Nicht e2e-pflichtig, begründet im Plan: kein neuer/geänderter UI-Fluss (kein Dialog, kein ViewModel); ein echter E2E-Nachweis erforderte privates GitHub-Repo + echten PAT — in Sandbox/CI nicht verfügbar; Env-Belegung über `ICliRunner`-Mock vollständig assertierbar; bestehende E2E-Suiten nutzen `LocalDirectory`-Plugin/`ScmE2EPluginManager` und sind unberührt |

## Hinweise

- **Expliziter Nicht-Schreib-Nachweis im Klon-Pfad (optional):** Kein Test assertiert direkt, dass nach `CloneRepositoryAsync` die Temp-`_netrc` weiterhin nicht existiert. Da alle Test-Konstruktionen den `netrcPath`-Seam nutzen, wäre ein `…_LegtKeineNetrcAn`-Assert nach dem Clone-Aufruf als Regressionssicherung naheliegend — nicht lückenhaft, da der Schreibblock entfernt wird und Nicht-Anlegen/Nicht-Verändern bereits über die Ctor-Tests abgedeckt ist.
- **`GitHubSinceWindowTests` Z. 45** nutzt die verkettete 4-Argument-Überladung, die laut Plan unverändert bleibt und keinen `netrcPath`-Parameter hat — „Temp-`netrcPath` übergeben" impliziert dort (und analog Z. 31) die Umstellung auf den vollen Konstruktor mit `null` für fehlende Transporte. Aus der Betroffenen-Tabelle ableitbar; bei der Umsetzung darauf achten.
- **`GetRemoteBranchesAsync`** erhält keinen eigenen Env-Assert-Test benannt — inhaltlich durch den generischen `GetGitEnvironment`-Test abgedeckt; ein Env-Matcher am bestehenden `…_ShouldParseAndSortBranches` wäre konsistent zu den anderen Aufrufern.
- **Code-Fakten verifiziert:** `new GitHubPlugin(`-Aufrufstellen im Repo sind exakt die vier genannten Teststellen plus `ScmE2EPluginManager.cs:24` (im Plan als Risiko benannt, kompiliert dank optionalem Parameter ohne Anpassung). `IPlugin` hat tatsächlich keinen Lifecycle-Hook; `ActivatorUtilities.CreateInstance` (`PluginManager.cs:204`) unterstützt optionale Parameter mit Defaults — Konstruktor-Trigger und Seam-Ansatz sind zulässig.
- **`ActivatorUtilities`-Hinweis:** `string` ist kein registrierter Dienst — der optionale `netrcPath`-Parameter fällt auf den Default `null` zurück. Funktioniert wie geplant.
