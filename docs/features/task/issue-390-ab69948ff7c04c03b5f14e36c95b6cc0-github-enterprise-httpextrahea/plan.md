# Umsetzungsplan: `http.extraheader`-Key hostgebunden machen (GitHub-Enterprise)

## Übersicht

Der `http.extraheader`-Key der GitHub-HTTPS-Authentifizierung wird von der festen Konstante `http.https://github.com/.extraheader` auf einen hostgebundenen Key `http.https://<zielhost>[:port]/.extraheader` umgestellt. Der Zielhost wird pro git-Operation aus der tatsächlich kontaktierten Remote-URL abgeleitet und durch die Kette `GetGitEnvironment`/`GetAgentEnvironmentAsync` → `AddGitHubHttpAuthHeader` gereicht. Betroffen sind das `GitHubPlugin` (alle sieben `GetGitEnvironment`-Aufrufstellen plus die `IScmAgentAccess`-Implementierung), das Interface `IScmAgentAccess` (Signaturbruch), sowie die Aufrufer-Kette `KiAusfuehrungsService`/`EntwicklungsprozessService` für Agenten-Terminal-Sessions. Ein generischer, hostloser `http.extraheader` wird bewusst nicht erzeugt (Leak-Vektor); bei nicht bestimmbarem Host bleibt das bisherige fail-closed-Verhalten (`GIT_TERMINAL_PROMPT=0`, kein `GIT_CONFIG_*`-Block) bestehen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Host-Quelle für `localPath`-Operationen (`PushBranchAsync`, `PullAsync`, `CheckoutPullRequestSourceAsync`) | `NormalizeRemoteUrlAsync` gibt die gelesene/normalisierte `remote.origin.url` als `Task<string?>` zurück; Aufrufer verwenden sie als Ziel-URL. | Die Methode liest die URL ohnehin per `git config remote.origin.url` — die Rückgabe-Wiederverwendung vermeidet einen zweiten git-Aufruf und jeden Signaturbruch an `IGitPlugin` für alle Plugins. Bei Lesefehler `null` → fail-closed. |
| Fork-Fall im PR-Checkout (`SourceRepositoryUrl`) | Der extraheader-Key bindet sich pro Operation an den Host der **tatsächlich kontaktierten** Remote-URL: Same-Repository-Fall → Host der `remote.origin.url`; Fork-Fall → Host der (HTTPS-normalisierten) `SourceRepositoryUrl`. | Der Leak-Vektor des Issues betrifft den generischen/hostlosen Key. Bei hostgebundenem Key geht der Token nur an den Host, mit dem git ohnehin spricht — ein abweichender `clone_url`-Host erhält den Token nicht „zusätzlich", sondern exakt so, wie der Fetch ihn transportbezogen benötigt. Ein Eintrag pro Prozessumgebung genügt, da `CheckoutPullRequestSourceCoreAsync` nur den einen `git fetch` gegen dieses Remote führt. |
| `sameRepository`-Entscheidung im `GitHubPlugin` | `CheckoutPullRequestSourceAsync` repliziert den Vergleich `TargetRepositoryId == SourceRepositoryId` (OrdinalIgnoreCase) aus `GitPluginBase.CheckoutPullRequestSourceCoreAsync`, um die kontaktierte URL vorab zu bestimmen. | Der Vergleich liegt aktuell in der Basisklasse; die Fetch-Zielwahl dort umzubauen (z. B. kontaktierte URL zurückmelden) wäre ein Eingriff in `GitPluginBase` für alle Plugins. Die dreizeilige Duplikation ist der kleinere Eingriff und bleibt im Plugin lokal. |
| Host-Extraktion und Key-Bildung | Neuer privater statischer Helfer `TryBuildHttpExtraHeaderKey(string? url)` im `GitHubPlugin`: `Uri.TryCreate` (absolute URI), Scheme `https`, nicht-leerer Host → Key `http.https://<host>[:port]/.extraheader`; sonst `null`. | Ein einziger Extraktionspunkt für alle sieben Aufrufstellen und die Agenten-Umgebung; `null` macht fail-closed zum Nebenprodukt ohne Zusatzlogik an den Aufrufern. Die Helfer erhalten die URL (nicht den vorab extrahierten Host), damit Normalisierung nur an einer Stelle existiert. |
| Host-Normalisierung im Key | `uri.IdnHost` (IDN→Punycode), IPv6-Literale über `uri.Host` (Klammern bleiben erhalten). | Konsistent zu `ParseEventRepository` (`GitHubPlugin.Events.cs`, Z. 397–424) und `ToHttpsRepositoryUrl` (`GitPluginBase`, Z. 308–309), die IPv6-Klammerlogik wird übernommen. |
| Nicht-Default-Ports im Key | Port wird in den Key aufgenommen (`http.https://host:port/`), wenn `!uri.IsDefaultPort`. | git matcht `http.<url>`-Keys inklusive Port; GHE-Instanzen auf Nicht-Standard-Ports würden sonst unauthentifiziert bleiben. |
| `IScmAgentAccess.GetAgentEnvironmentAsync`-Signatur | Neuer Parameter `string? repositoryUrl` (zwischen `konto` und `ct`): `GetAgentEnvironmentAsync(ScmKontoModus konto, string? repositoryUrl, CancellationToken ct = default)`. | Die URL (statt des vorab extrahierten Hosts) wird durchgereicht — dieselbe Semantik wie `GetGitEnvironment`, Extraktion bleibt im Plugin an einer Stelle. `GitHubPlugin` ist der einzige Implementierer; Signaturbruch ist akzeptabel, Moq-Setups werden angepasst. |
| Durchreichung der Repository-URL an die Agenten-Umgebung | Neuer optionaler Parameter `string? repositoryUrl = null` an `StartTerminalSessionAsync`/`StartTerminalSessionCoreAsync`; `WithScmAgentEnvironmentAsync` reicht ihn an `GetAgentEnvironmentAsync` durch. Quelle: `repository.RepositoryUrl` im `EntwicklungsprozessService` (beide Aufrufstellen haben ihn vorliegen). | Alternative über `RepositoryStartKonfiguration.GitRepository`-Navigation scheidet aus: Die Navigation wird nirgends per `Include` geladen (`AufgabeService.GetDetailAsync` inkludiert nur `Aufgabe.GitRepository`, `GetByIdAsync` gar nichts) — ein optionaler Parameter auf dem ohnehin optionalen Parameter-Block ist die minimalinvasive, ladefehlerfreie Lösung. |
| `StartCliCoreAsync` (Pipe-Pfad) | Unverändert — kein `repositoryUrl`-Parameter, keine `IScmAgentAccess`-Nutzung. | Bewusste Vorgabe: Der Pipe-Pfad injiziert die Agenten-Umgebung heute nicht; das bleibt so. |
| `CheckHealthAsync` | Unverändert (fest `github.com`, API-Health). | Explizit außerhalb des Scopes dieses Issues. |

## Programmabläufe

### Hostgit-Operation mit bekannter Repository-URL (`CloneRepositoryAsync`, `ConfigureGitCredentialsAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`)

1. Die jeweilige Methode normalisiert ihre Repository-URL wie bisher (`ToHttpsRepositoryUrl`; `CloneRepositoryAsync` fail-fast bei `null`, die `ls-remote`-Methoden fallen auf die Eingabe zurück).
2. Sie ruft `GetGitEnvironment(token, targetUrl)` auf — `targetUrl` ist die normalisierte (bzw. bei `ConfigureGitCredentialsAsync` die übergebene) HTTPS-URL.
3. `GetGitEnvironment` setzt `GIT_TERMINAL_PROMPT=0` und bei vorhandenem Token `GH_TOKEN` sowie `AddGitHubHttpAuthHeader(env, token, targetUrl)`.
4. `AddGitHubHttpAuthHeader` ruft `TryBuildHttpExtraHeaderKey(targetUrl)` auf: liefert er einen Key, werden `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://<idnhost>[:port]/.extraheader` und `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64>` gesetzt; liefert er `null`, entsteht kein `GIT_CONFIG_*`-Eintrag (fail-closed).
5. Die Umgebung wird wie bisher an `_cliRunner.RunAsync("git", …)` übergeben.

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `GitPluginBase.ToHttpsRepositoryUrl`, `ICliRunner`.

### `localPath`-Operationen (`PushBranchAsync`, `PullAsync`)

1. `NormalizeRemoteUrlAsync(localPath, ct)` läuft wie bisher zuerst (Token-Stripping, HTTPS-Normalisierung, `set-url`), gibt jetzt aber die **effektive** origin-URL zurück: die normalisierte URL nach erfolgreichem `set-url` bzw. bei Gleichstand, sonst die gelesene (token-bereinigte) URL; `null`, wenn `remote.origin.url` nicht lesbar war.
2. `RemoveInsecureSshCommandAsync` läuft unverändert.
3. `GetGitEnvironment(token, remoteUrl)` wird mit der effektiven URL aufgerufen — bei `null`/unparsebarer URL entsteht kein extraheader-Eintrag, Push/Pull läuft fail-closed in den bestehenden `IsAuthenticationFailure`-Fehlerpfad (`GIT_TERMINAL_PROMPT=0`).
4. `git push --set-upstream origin <branch>` bzw. `git pull` wie bisher.

Beteiligte Klassen/Komponenten: `GitHubPlugin` (`NormalizeRemoteUrlAsync`, `RemoveInsecureSshCommandAsync`, `GetGitEnvironment`), `ICliRunner`.

### PR-Quell-Checkout (`CheckoutPullRequestSourceAsync`)

1. `NormalizeRemoteUrlAsync` liefert die effektive origin-URL (siehe vorheriger Ablauf); `RemoveInsecureSshCommandAsync` unverändert.
2. `SourceRepositoryUrl` wird wie bisher per `ToHttpsRepositoryUrl` auf HTTPS normalisiert (`with`-Ausdruck).
3. `CheckoutPullRequestSourceAsync` bestimmt die kontaktierte URL: `sameRepository = TargetRepositoryId == SourceRepositoryId` (OrdinalIgnoreCase — dieselbe Bedingung wie `CheckoutPullRequestSourceCoreAsync`). Same-Repository → `originUrl` aus Schritt 1; Fork → die normalisierte `SourceRepositoryUrl`.
4. `GetGitEnvironment(targetUrl: kontaktierteUrl)` erzeugt den an genau diesen Host gebundenen extraheader-Key (bzw. keinen, wenn nicht bestimmbar).
5. `CheckoutPullRequestSourceCoreAsync` läuft unverändert: `git fetch --no-tags <remote> <sourceRef>` ist der einzige Remote-Kontakt — der Key passt zum Fetch-Ziel.

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `GitPluginBase.CheckoutPullRequestSourceCoreAsync`, `PullRequestCheckoutSpec`, `ICliRunner`.

### Agenten-Terminal-Session (SCM-Anbindung des KI-Agenten)

1. `EntwicklungsprozessService.ProzessUndCliCoordinatedAsync` bzw. `CliNeustartenCoordinatedAsync` rufen `StartTerminalSessionAsync` mit dem neuen optionalen Argument `repositoryUrl: repository.RepositoryUrl` auf (liegt an beiden Stellen vor).
2. `StartTerminalSessionAsync` reicht den Wert an `StartTerminalSessionCoreAsync` durch (optionale-Parameter-Kette).
3. `StartTerminalSessionCoreAsync` ruft nach `GetTerminalStartSpecAsync` die Methode `WithScmAgentEnvironmentAsync(spec, gitPlugin, repositoryUrl, ct)` auf.
4. `WithScmAgentEnvironmentAsync` (unveränderte Fehlersemantik: nicht-`IScmAgentAccess` oder Fehler → Spec unverändert) ruft `agentAccess.GetAgentEnvironmentAsync(konto, repositoryUrl, ct)` auf und mergt das Ergebnis in `spec.EnvironmentVariables`.
5. `GitHubPlugin.GetAgentEnvironmentAsync` bildet die Umgebung wie in Ablauf 1: `GIT_TERMINAL_PROMPT=0`, bei Token `GH_TOKEN` + hostgebundener `GIT_CONFIG_*`-Block aus `repositoryUrl`; `repositoryUrl == null` oder unparsebar → kein extraheader (fail-closed, Agent-Auth fällt nicht-interaktiv aus).

Beteiligte Klassen/Komponenten: `EntwicklungsprozessService`, `KiAusfuehrungsService`, `IScmAgentAccess`, `GitHubPlugin`, `TerminalSessionStartSpec`.

### Fail-closed-Pfade (unveränderte Semantik)

- Kein Token im `ICredentialStore` → nur `GIT_TERMINAL_PROMPT=0`, kein `GH_TOKEN`, kein `GIT_CONFIG_*`.
- Zielhost nicht bestimmbar (URL `null`/unparsebar/nicht-HTTPS/kein Host; `remote.origin.url` nicht lesbar) → `GIT_TERMINAL_PROMPT=0` + `GH_TOKEN`, **kein** `GIT_CONFIG_*`-Block, kein Fallback auf `github.com` oder einen generischen Key.
- `IsAuthenticationFailure` erkennt den resultierenden „terminal prompts disabled"-Fehlschlag wie bisher.

Beteiligte Klassen/Komponenten: `GitHubPlugin` (`TryBuildHttpExtraHeaderKey`, `AddGitHubHttpAuthHeader`, `IsAuthenticationFailure`).

## Neue Klassen

Keine — die Änderung besteht aus Signaturerweiterungen und einer neuen privaten Hilfsmethode im `GitHubPlugin`.

## Änderungen an bestehenden Klassen

### `GitHubPlugin` (Klasse, `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`)

- **Neue Methoden:** `TryBuildHttpExtraHeaderKey(string? url)` (`private static`, Rückgabewert `string?`) — bildet aus einer Repository-/Remote-URL den hostgebundenen extraheader-Key `http.https://<host>[:port]/.extraheader`. Regeln: `Uri.TryCreate` absolute URI erforderlich, Scheme `https`, Host nicht leer; Host-Form `uri.IdnHost` bzw. `uri.Host` bei `UriHostNameType.IPv6`; Port-Anteil `:$Port` nur bei `!uri.IsDefaultPort`. Liefert `null` bei `null`, unparsebarer Eingabe, Nicht-HTTPS-Schema oder leerem Host.
- **Geänderte Methoden:**
  - `AddGitHubHttpAuthHeader` — Signatur `(IDictionary<string, string?> environment, string token, string? targetUrl)`; `GIT_CONFIG_KEY_0` wird aus `TryBuildHttpExtraHeaderKey(targetUrl)` gebildet statt der festen `github.com`-Konstante; bei `null` werden keinerlei `GIT_CONFIG_*`-Einträge gesetzt. XML-Doc (Z. 200–205, beschreibt aktuell „für github.com") anpassen.
  - `GetGitEnvironment` — Signatur `(string? token = null, string? targetUrl = null)`; reicht `targetUrl` an `AddGitHubHttpAuthHeader` durch.
  - `GetAgentEnvironmentAsync` — neue Signatur `(ScmKontoModus konto, string? repositoryUrl, CancellationToken ct = default)` (`IScmAgentAccess`-Implementierung); reicht `repositoryUrl` an `AddGitHubHttpAuthHeader` durch.
  - `NormalizeRemoteUrlAsync` — Rückgabetyp `Task` → `Task<string?>`; gibt die effektive origin-URL zurück (normalisierte URL bei `set-url`-Erfolg bzw. unveränderter URL, sonst die gelesene token-bereinigte URL; `null` bei Lesefehler). Bestehende Best-effort-/LogWarning-Semantik bleibt.
  - `PushBranchAsync` — nutzt den Rückgabewert von `NormalizeRemoteUrlAsync`; `GetGitEnvironment(token, remoteUrl)`.
  - `PullAsync` — wie `PushBranchAsync`.
  - `CloneRepositoryAsync` — `GetGitEnvironment(token, repositoryUrl)` mit der bereits normalisierten URL (Z. 1113).
  - `ConfigureGitCredentialsAsync` — `GetGitEnvironment(token, repositoryUrl)` mit dem vorhandenen Parameter (Z. 566).
  - `GetRemoteBranchesAsync` — `GetGitEnvironment(targetUrl: remoteUrl)` (Z. 1655).
  - `GetDefaultBranchAsync` — `GetGitEnvironment(targetUrl: remoteUrl)` (Z. 1713).
  - `CheckoutPullRequestSourceAsync` — `sameRepository`-Vergleich replizieren; kontaktierte URL bestimmen (origin-Rückgabe von `NormalizeRemoteUrlAsync` vs. normalisierte `SourceRepositoryUrl`); `GetGitEnvironment(targetUrl: kontaktierteUrl)` (Z. 1762). `remarks`-Doc (Z. 1735–1742) um die Host-Bindung ergänzen.

### `IScmAgentAccess` (Interface, `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`)

- **Geänderte Methoden:** `GetAgentEnvironmentAsync` — Signatur `(ScmKontoModus konto, string? repositoryUrl, CancellationToken ct = default)`; XML-Doc um `repositoryUrl`-Param ergänzen (Zielhost-Quelle für die hostgebundene git-Auth; `null` zulässig → Anbindung ohne hostgebundenen Key).

### `KiAusfuehrungsService` (Klasse, `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`)

- **Geänderte Methoden:**
  - `StartTerminalSessionAsync` (Z. 270–279) — neuer optionaler Parameter `string? repositoryUrl = null` (nach `gitPlugin`); Durchreichen an `StartTerminalSessionCoreAsync`. XML-Doc ergänzen.
  - `StartTerminalSessionCoreAsync` (Z. 281–421) — neuer Parameter `string? repositoryUrl`; an `WithScmAgentEnvironmentAsync` durchreichen (Z. 304).
  - `WithScmAgentEnvironmentAsync` (Z. 431–463) — neuer Parameter `string? repositoryUrl`; Aufruf `agentAccess.GetAgentEnvironmentAsync(konto, repositoryUrl, ct)` (Z. 444). Fehlersemantik unverändert.
- `StartCliAsync`/`StartCliCoreAsync` bleiben unverändert (Pipe-Pfad ohne Agenten-Umgebung — bewusste Entscheidung).

### `EntwicklungsprozessService` (Klasse, `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`)

- **Geänderte Methoden:**
  - `ProzessUndCliCoordinatedAsync` — `StartTerminalSessionAsync`-Aufruf (Z. 190–191) um `repositoryUrl: repository.RepositoryUrl` ergänzen.
  - `CliNeustartenCoordinatedAsync` — `StartTerminalSessionAsync`-Aufruf (Z. 297–304) um `repositoryUrl: repository.RepositoryUrl` ergänzen.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `targetUrl`/`repositoryUrl` an `TryBuildHttpExtraHeaderKey` | Absolute URI, Scheme `https`, nicht-leerer Host | `null`, unparsebar, Nicht-HTTPS oder leerer Host → Rückgabe `null` → kein `GIT_CONFIG_*`-Block (fail-closed, kein Throw) |
| `sameRepository`-Vergleich im PR-Checkout | `TargetRepositoryId == SourceRepositoryId` (OrdinalIgnoreCase), identisch zu `GitPluginBase.CheckoutPullRequestSourceCoreAsync` | Fork-Fall ohne `SourceRepositoryUrl` → weiterhin `InvalidOperationException` aus der Basisklasse (unverändert) |
| Host-Form im Key | `IdnHost` (Punycode), IPv6 mit Klammern (`uri.Host`), Port nur bei Nicht-Default-Port | Entsteht implizit aus `Uri`-Parsing; kein separater Fehlerpfad |

## Konfigurationsänderungen

Keine — der Zielhost wird aus der URL der konkreten Operation abgeleitet, nicht konfiguriert. `Token`-/`AgentToken`-Plugin-Einstellungen und `ScmKontoModus` bleiben unverändert.

## Seiteneffekte und Risiken

- **`IScmAgentAccess`-Signaturbruch:** Compile-Fehler an allen Mock-Setups (`ScmDispatchFixture` Z. 80, `ScmEventDispatchTests_AgentActions` Z. 28 und 86) bis zur Anpassung — bewusst in Kauf genommen, `GitHubPlugin` ist einziger Implementierer.
- **`ScmE2ERuntimeControl` (E2E-Teststeuerung):** ruft `StartTerminalSessionAsync` ohne `repositoryUrl` und ohne `gitPlugin` — dank optionalem Parameter kompilier- und verhaltenskompatibel; die `IScmAgentAccess`-Frühabfrage greift ohnehin nicht.
- **UI-/manuelle `StartTerminalSessionAsync`-Aufrufer ohne `repositoryUrl`:** Agenten-Sessions erhalten dann `GIT_TERMINAL_PROMPT=0` + `GH_TOKEN`, aber keinen extraheader — dasselbe Verhalten wie heute für Nicht-`github.com`-Hosts (fail-closed statt Regression). Beide produktiven Aufrufstellen werden mit URL versorgt.
- **`GH_TOKEN`/`gh`-CLI bei GHE:** `GH_TOKEN` bleibt gesetzt, `gh` adressiert aber ohne `GH_HOST` weiterhin `github.com` — fachlich außerhalb dieses Issues (Scope: nur der extraheader-Key); als bekannte Einschränkung dokumentieren, kein Code-Eingriff.
- **`sameRepository`-Duplikation:** `GitHubPlugin.CheckoutPullRequestSourceAsync` spiegelt die Vergleichslogik aus `GitPluginBase` — bei künftiger Änderung der Basisklassen-Logik beidseitig pflegen (im Code-Kommentar querverweisen).
- **`GetAvailableRepositoriesAsync`/`CheckHealthAsync`:** adressieren weiterhin fest `github.com` (API-Ebene, kein git-Transport) — unverändert, außerhalb des Scopes.
- **Dokumentation (Lifecycle-Schritt 12):** `docs/help/plugins/business-rules.md` (Z. 228–239), `docs/help/plugins/ablauf-technisch.md` (Z. 87–89) und `docs/help/plugins/github-ereignisse/{business-rules,ablauf-technisch}.md` beschreiben den festen `github.com`-Key bzw. die GHE-Einschränkung und sind zu aktualisieren.

## Umsetzungsreihenfolge

1. **`TryBuildHttpExtraHeaderKey` + Signaturerweiterung von `AddGitHubHttpAuthHeader`/`GetGitEnvironment` im `GitHubPlugin`**
   - Voraussetzungen: Keine (nur BCL-`Uri`-APIs; `ToHttpsRepositoryUrl`/`IdnHost`-Muster existieren bereits in `GitPluginBase`/`ParseEventRepository`).
   - Beschreibung: Neuen privaten statischen Helfer anlegen; `AddGitHubHttpAuthHeader` um `targetUrl` erweitern (Key aus Helfer, `null` → kein `GIT_CONFIG_*`); `GetGitEnvironment` um `targetUrl` erweitern und durchreichen.

2. **`NormalizeRemoteUrlAsync` auf `Task<string?>` umstellen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Rückgabewert einführen (effektive origin-URL bzw. `null`); die drei Aufrufer (`PushBranchAsync`, `PullAsync`, `CheckoutPullRequestSourceAsync`) auf den neuen Rückgabewert umstellen.

3. **Alle `GetGitEnvironment`-Aufrufstellen mit Ziel-URL versorgen**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `CloneRepositoryAsync`, `ConfigureGitCredentialsAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync` mit ihrer URL; `PushBranchAsync`/`PullAsync` mit der `NormalizeRemoteUrlAsync`-Rückgabe; `CheckoutPullRequestSourceAsync` mit `sameRepository`-Vergleich und kontaktierter URL.

4. **`IScmAgentAccess.GetAgentEnvironmentAsync` um `repositoryUrl` erweitern + `GitHubPlugin`-Implementierung anpassen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Interface-Signatur und XML-Doc ändern; `GitHubPlugin.GetAgentEnvironmentAsync` reicht `repositoryUrl` an `AddGitHubHttpAuthHeader` durch.

5. **`KiAusfuehrungsService`-Kette und `EntwicklungsprozessService`-Aufrufstellen**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: Optionaler `repositoryUrl`-Parameter an `StartTerminalSessionAsync`/`StartTerminalSessionCoreAsync`/`WithScmAgentEnvironmentAsync`; beide `EntwicklungsprozessService`-Aufrufe übergeben `repository.RepositoryUrl`.

6. **Tests anpassen und neue Tests schreiben**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `HasGitHubExtraHeader`-Helfer um Key-Parameter erweitern, alle Aufrufer und `GetAgentEnvironmentAsync`-Tests an die neue Signatur anpassen, Moq-Setups in `ScmDispatchFixture`/`ScmEventDispatchTests_AgentActions` umstellen, neue GHE-/Port-/Fail-closed-/Fork-Tests ergänzen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `HasGitHubExtraHeader(env, token, expectedKey)` — Signaturerweiterung | `GitHubPluginTests` | Hilfsmethode prüft `GIT_CONFIG_COUNT == "1"`, `GIT_CONFIG_KEY_0 == expectedKey`, `GIT_CONFIG_VALUE_0` (Basic-Auth) — statt des hartcodierten `github.com`-Keys. |
| `GetAgentEnvironmentAsync_GheHost_BindetExtraHeaderAnGheHost` | `GitHubPluginTests` | `repositoryUrl = "https://ghe.example.com/owner/repo.git"` → `GIT_CONFIG_KEY_0 == "http.https://ghe.example.com/.extraheader"`. |
| `GetAgentEnvironmentAsync_NonDefaultPort_NimmtPortInKeyAuf` | `GitHubPluginTests` | `repositoryUrl` mit `:8443` → `GIT_CONFIG_KEY_0 == "http.https://ghe.example.com:8443/.extraheader"`; `:443` erzeugt keinen Port im Key. |
| `GetAgentEnvironmentAsync_OhneBestimmbarenHost_KeinExtraHeader` | `GitHubPluginTests` | Theory: `repositoryUrl` = `null`, `""`, `"keine-url"`, `"ssh://git@host:2222/x"` → keine `GIT_CONFIG_*`-Keys; `GIT_TERMINAL_PROMPT=0` + `GH_TOKEN` bleiben. |
| `CloneRepositoryAsync_GheUrl_BindetExtraHeaderAnGheHost` | `GitHubPluginTests` | `git clone` gegen GHE-URL → Env enthält `http.https://ghe.example.com/.extraheader`. |
| `PushBranchAsync_GheRemote_BindetExtraHeaderAnOriginHost` | `GitHubPluginTests` | `remote.origin.url` gemockt auf GHE-URL → `git push`-Env mit GHE-Key. |
| `PushBranchAsync_RemoteUrlNichtLesbar_KeinExtraHeader` | `GitHubPluginTests` | `git config remote.origin.url` schlägt fehl → Push läuft ohne `GIT_CONFIG_*` weiter (fail-closed, kein Abbruch der Operation). |
| `PullAsync_GheRemote_BindetExtraHeaderAnOriginHost` | `GitHubPluginTests` | Analog `PushBranchAsync` für `git pull`. |
| `CheckoutPullRequestSourceAsync_Fork_BindetExtraHeaderAnSourceHost` | `GitHubPluginTests` | Fork-Fall: `SourceRepositoryUrl` auf `ghe-fork.example.com`, `SourceRepositoryId ≠ TargetRepositoryId` → `git fetch`-Env-Key `http.https://ghe-fork.example.com/.extraheader` (Key an die kontaktierte URL gebunden). |
| `CheckoutPullRequestSourceAsync_SameRepo_BindetExtraHeaderAnOriginHost` | `GitHubPluginTests` | Same-Repo-Fall mit GHE-`remote.origin.url` → fetch/checkout-Env mit origin-Host-Key (kann in den bestehenden Test `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` integriert oder separat ergänzt werden). |
| `Dispatch_PassesRepositoryUrlToAgentEnvironment` (Integration) | `ScmEventDispatchTests_AgentActions` | Moq-Verify: `GetAgentEnvironmentAsync` wird mit der `RepositoryUrl` des `GitRepository` (Fixture: `https://github.com/owner/repo`) aufgerufen — prüft die Durchreichung durch `StartTerminalSessionAsync` → `WithScmAgentEnvironmentAsync`. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `HasGitHubExtraHeader` + alle Aufrufer (`GitHubPluginTests` Z. 517, 1019, 1207, 1858, 1919, 1926, 1951 sowie Z. 1474) | Hilfsmethode erhält den erwarteten Key als Parameter; Aufrufer übergeben `http.https://github.com/.extraheader` (bzw. den jeweiligen Test-Host). |
| `GetAgentEnvironmentAsync_WithToken_ProvidesGhAndGitAuth` (Z. 1465–1479) | Neue Signatur — Aufruf mit `repositoryUrl`; Key-Assert auf Host der übergebenen URL. |
| `GetAgentEnvironmentAsync_WithoutToken_OnlyDisablesGitPrompt` (Z. 1484–1494) | Neue Signatur. |
| `GetAgentEnvironmentAsync_SeparatesKonto_UsesAgentToken` (Z. 1498–1510) | Neue Signatur. |
| `PushBranchAsync`-Tests (u. a. Z. ~983–1021, 1025 ff., 2601–2633) | `GetGitEnvironment` erfordert Host aus `remote.origin.url`; Mock-Setups für `git config remote.origin.url` müssen die jeweils erwartete URL liefern (teilweise vorhanden); `PushBranchAsync_NichtKonvertierbareRemoteUrl_KeinSetUrl` assertiert zusätzlich fail-closed ohne extraheader. |
| `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` (Z. 1865–1928) | Erwarteter Key weiterhin `http.https://github.com/.extraheader` über angepasste `HasGitHubExtraHeader`-Signatur. |
| `ScmDispatchFixture` Konstruktor-Setup (Z. 80) | Moq-Setup an neue Signatur `(konto, repositoryUrl, ct)` anpassen. |
| `ScmEventDispatchTests_AgentActions` Z. 28, 86 | Moq-Setups an neue Signatur anpassen (`It.IsAny<string?>()`-Matcher ergänzen). |

### E2E-Tests (Pflicht)

Keine neuen E2E-Tests — die Änderung betrifft ausschließlich die prozessinterne Prozessumgebung git-Operationen und Agenten-Sessions; es gibt keine neue oder geänderte Benutzerinteraktion. Die Wirkung (HTTPS-Auth gegen GHE-Hosts, fail-closed ohne generischen Key) wird über die Unit-Tests in `GitHubPluginTests` und den Integrationstest der Dispatch-Kette abgedeckt.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | `ScmE2ERuntimeControl.StartTerminalSessionAsync` ist dank optionalem Parameter kompatibel; kein bestehender E2E-Test berührt den extraheader-Pfad. |

## Offene Punkte

Keine.
