# Bestandsaufnahme: `http.extraheader`-Key hostgebunden machen (GitHub-Enterprise)

Analysiert wurden die HTTPS-git-Authentifizierung des `GitHubPlugin` (`AddGitHubHttpAuthHeader`, `GetGitEnvironment`, `GetAgentEnvironmentAsync`), sämtliche Aufrufstellen davon, die Aufruferkette für Agenten-Terminal-Sessions im Anwendungskern sowie die beteiligten Contracts, Datenmodelle und Tests — bezogen auf die Anforderung in `requirement.md`.

## Zusammenfassung

- `GitHubPlugin.AddGitHubHttpAuthHeader` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, Z. 206–212) verdrahtet `GIT_CONFIG_KEY_0` fest auf `http.https://github.com/.extraheader`; der Basic-Auth-Wert (`AUTHORIZATION: basic <base64("x-access-token:"+token)>`) ist hostunabhängig. Kein Host-Parameter existiert an keiner Stelle der Kette.
- Zwei Erzeugungsstellen teilen den Helfer: `GetGitEnvironment` (private, Z. 214–235) für Host-git-Operationen und `GetAgentEnvironmentAsync` (`IScmAgentAccess`, Z. 183–198) für die Prozessumgebung von Agenten-Terminal-Sessions. Beiden fehlt jede Host-/URL-Information.
- `GetGitEnvironment` hat sieben Aufrufstellen im `GitHubPlugin`: `ConfigureGitCredentialsAsync` (Z. 566), `CloneRepositoryAsync` (Z. 1113), `PushBranchAsync` (Z. 1148), `PullAsync` (Z. 1174), `GetRemoteBranchesAsync` (Z. 1655), `GetDefaultBranchAsync` (Z. 1713) und `CheckoutPullRequestSourceAsync` (Z. 1762). Vier davon kennen eine Repository-URL direkt; `PushBranchAsync` und `PullAsync` kennen nur `localPath`, `CheckoutPullRequestSourceAsync` kennt `localPath` plus `PullRequestCheckoutSpec` (mit `TargetRepositoryUrl` und `SourceRepositoryUrl`).
- `NormalizeRemoteUrlAsync` (Z. 453–507) liest `remote.origin.url` bereits per `git config remote.origin.url` aus der Arbeitskopie und normalisiert sie via `ToHttpsRepositoryUrl` — läuft vor `PushBranchAsync`, `PullAsync` und `CheckoutPullRequestSourceAsync`, gibt die gelesene URL aber nicht zurück (Rückgabetyp `Task`).
- `ToHttpsRepositoryUrl` (`GitPluginBase`, Z. 259–311) liefert eine HTTPS-URL, aus der der Host per `Uri.IdnHost` extrahierbar wäre; `ParseEventRepository` (`GitHubPlugin.Events.cs`, Z. 397–424) demonstriert die vorhandene `IdnHost`-Host-Normalisierung für GHE-Hosts. `GitHubHttpApiClient.ValidateHost` (Z. 77–88) validiert DNS-Hosts ohne Port/UserInfo.
- `IScmAgentAccess.GetAgentEnvironmentAsync` (`IScmAgentAccess.cs`, Z. 22) hat die Signatur `(ScmKontoModus konto, CancellationToken ct)` — `GitHubPlugin` ist der einzige Implementierer (grep-Verifikation: kein weiterer `IScmAgentAccess`-Implementierer im Repo); einziger produktiver Aufrufer ist `KiAusfuehrungsService.WithScmAgentEnvironmentAsync` (Z. 431–463, Aufruf Z. 444).
- `StartTerminalSessionAsync`/`StartCliAsync` tragen bereits `gitPlugin` und `startConfig` als optionale Parameter; nur `StartTerminalSessionCoreAsync` ruft `WithScmAgentEnvironmentAsync` auf (Z. 304), `StartCliCoreAsync` (Pipe-Pfad) nicht. Produktive `StartTerminalSessionAsync`-Aufrufer: `EntwicklungsprozessService` (Z. 190–191 und 297–304 — dort liegt `repository.RepositoryUrl` bereits vor) sowie `ScmE2ERuntimeControl` (E2E-Teststeuerung, ohne `gitPlugin`/`startConfig`).
- `RepositoryStartKonfiguration` besitzt eine `GitRepository`-Navigation (Z. 22), die aber nirgends per `Include` geladen wird — `AufgabeService.GetDetailAsync` inkludiert `GitRepository` + `StartKonfiguration`/`InitialisierungKonfiguration` (Z. 109–112), nicht die Rücknavigation der Startkonfiguration; `GetByIdAsync` lädt `GitRepository` gar nicht.
- Offensichtlich fehlt: jede Host-Übergabe an `AddGitHubHttpAuthHeader`; ein Mechanismus, `remote.origin.url` für `localPath`-Operationen wiederzuverwenden (bisher verwirft `NormalizeRemoteUrlAsync` das Ergebnis); die Port-/IPv6-Behandlung im Key (nur `ToHttpsRepositoryUrl`/`ParseEventRepository` haben `IdnHost`-/`IsDefaultPort`-Logik, nicht der extraheader-Pfad).
- Bestehende Fail-closed-Semantik: ohne Token nur `GIT_TERMINAL_PROMPT=0`; `IsAuthenticationFailure` (Z. 408–419) erkennt u. a. „terminal prompts disabled". `CheckHealthAsync` (Z. 1629–1642) und `GetAvailableRepositoriesAsync` (Z. 1684) adressieren fest `github.com` (API-Ebene, kein git-Transport).
- Tests decken den hartcodierten Key an zwei Stellen ab (`GitHubPluginTests.cs` Z. 1474 und Hilfsmethode `HasGitHubExtraHeader` Z. 2636–2643); Moq-Setups der Interface-Methode existieren in `ScmDispatchFixture.cs` (Z. 80) und `ScmEventDispatchTests_AgentActions.cs` (Z. 28, 86).

## Details

- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Datenmodell](inventory/models.md)
- [Enums](inventory/enums.md)
- [Tests](inventory/tests.md)
