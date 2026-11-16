# Übersetzte Anforderung: `http.extraheader`-Key hostgebunden machen (GitHub-Enterprise)

**Quelle:** Issue #390 / Aufgaben-ID `ab69948f-f7c0-4c03-b5f1-4e36c95b6cc0` (Scope-Ausschluss 3 aus Issue #377 „HTTPS-only-Umstellung", vom Anwender bestätigt)
**Branch:** `task/issue-390-ab69948ff7c04c03b5f14e36c95b6cc0-github-enterprise-httpextrahea`

## Fachliche Zusammenfassung

Der `http.extraheader`-Key für die GitHub-HTTPS-git-Authentifizierung ist in `GitHubPlugin.AddGitHubHttpAuthHeader` fest auf `http.https://github.com/.extraheader` verdrahtet (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, Zeilen 206–212). git-Operationen gegen GitHub-Enterprise-Remotes (Host ≠ `github.com`) greifen daher nicht auf den PAT zu und schlagen seit der HTTPS-only-Umstellung nicht-interaktiv fehl (`GIT_TERMINAL_PROMPT=0`, fail-closed). Die Anforderung macht den Key hostgebunden: Der Zielhost der jeweiligen konkreten git-Operation wird über `GetGitEnvironment` bzw. `GetAgentEnvironmentAsync` durchgereicht und der Key als `http.https://<zielhost>/.extraheader` gebildet. Ein generischer `http.extraheader` ohne Host-Bindung ist ausdrücklich ausgeschlossen — er würde den PAT an jeden kontaktierten Host senden (Leak-Vektor über Fork-`clone_url` bzw. `PullRequestCheckoutSpec.SourceRepositoryUrl` aus PR-Metadaten).

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Bildung der `GIT_CONFIG_*`-`http.extraheader`-Einträge in `GitHubPlugin.AddGitHubHttpAuthHeader` — aus einem durchgereichten Zielhost statt der festen Konstante `github.com` — sowie die Versorgung dieses Hosts an beiden Erzeugungsstellen (`GetGitEnvironment` für Host-git-Operationen, `GetAgentEnvironmentAsync` für die Prozessumgebung von Agenten-Terminal-Sessions).
- **Auslösende Ereignisse:**
  - Host-geführte git-Operationen des `GitHubPlugin`: `git clone` (`CloneRepositoryAsync`), `git push --set-upstream` (`PushBranchAsync`), `git pull` (`PullAsync`), `git ls-remote` (`GetRemoteBranchesAsync`, `GetDefaultBranchAsync`), `git fetch`/`git checkout` im PR-Quell-Checkout (`CheckoutPullRequestSourceAsync` → `CheckoutPullRequestSourceCoreAsync`) sowie die `git config`-Nebenaufrufe in `ConfigureGitCredentialsAsync`.
  - Start einer Agenten-Terminal-Session: `KiAusfuehrungsService.WithScmAgentEnvironmentAsync` ruft `IScmAgentAccess.GetAgentEnvironmentAsync` auf und mergt das Ergebnis in `TerminalSessionStartSpec.EnvironmentVariables`.
- **Erwartete Wirkung:**
  - HTTPS-git-Authentifizierung funktioniert gegen `github.com` und gegen GitHub-Enterprise-Hosts (`http.https://<ghe-host>/.extraheader`), ohne dass der Token in URLs, Kommandozeilenargumenten oder Dateien landet (bleibt rein prozessbasiert über `GIT_CONFIG_*`).
  - Der Basic-Auth-Header wird ausschließlich an den Host der konkreten Operation gebunden; es entsteht kein hostloser `http.extraheader`, der den PAT an beliebige Kontaktpunkte senden würde.
  - Kann kein Zielhost ermittelt werden, bleibt das bisherige fail-closed-Verhalten bestehen (kein extraheader-Eintrag, `GIT_TERMINAL_PROMPT=0` lässt die Auth nicht-interaktiv fehlschlagen) — kein Fallback auf einen generischen oder `github.com`-hartcodierten Key für Fremd-Hosts.
- **Umgang mit fehlenden Objekten:**
  - *Kein Token im `ICredentialStore`:* unverändert — die Umgebung enthält nur `GIT_TERMINAL_PROMPT=0`, kein `GIT_CONFIG_*`-Block.
  - *Zielhost nicht bestimmbar* (z. B. unparsebare/fehlende URL, `remote.origin.url` nicht lesbar): **fail-closed** — die Operation läuft ohne extraheader weiter in den bestehenden Fehlerpfad bzw. der Aufrufer übernimmt die bisherige Fehlerbehandlung; es wird kein Ersatz-Host geraten.
  - *Operationen, die nur `localPath` kennen* (`PushBranchAsync`, `PullAsync`, `CheckoutPullRequestSourceAsync`): Der Zielhost ist nicht direkt im Methodenparameter enthalten und muss aus der Arbeitskopie (`remote.origin.url`, bereits von `NormalizeRemoteUrlAsync` gelesen) oder vom Aufrufer durchgereicht werden — siehe Offene Fragen.
- **Scope-Ausschlüsse:**
  - Kein generischer `http.extraheader` (ohne Host) — explizit vom Anwender als Leak-Vektor ausgeschlossen.
  - Keine Rückkehr zu SSH/`core.sshCommand`/`GIT_SSH_COMMAND`; die HTTPS-only-Festlegung aus #377 bleibt unangetastet.
  - Keine Änderung am `BitbucketPlugin` (eigener Auth-Mechanismus: `_netrc`-Pflege bzw. `-c http.extraheader`-Args bei Self-Hosted) und keine Änderung an `IScmAgentAccess`-Nicht-Implementierern — das `GitHubPlugin` ist der einzige Implementierer des Interfaces.
  - Keine neue Benutzer-/Anwendungskonfiguration für den Host — er wird aus der URL der Operation abgeleitet, nicht vom Anwender gepflegt.
  - Keine Änderung an den REST-/GraphQL-Pfaden (`GitHubHttpApiClient`/`ParseEventRepository` sind bereits hostfähig und liefern den GHE-Host für API-Aufrufe).
  - `CheckHealthAsync` prüft weiterhin fest gegen `github.com` (API-Health, kein git-Transport) — nicht Teil dieses Issues, sofern nicht anders entschieden (siehe Offene Fragen).

## Betroffene Klassen und Komponenten

### Zu ändernde Artefakte

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
  - `AddGitHubHttpAuthHeader` (Zeilen 206–212): Signatur um den Zielhost erweitern; `GIT_CONFIG_KEY_0` wird `http.https://<host>/.extraheader` statt des festen `http.https://github.com/.extraheader`. Der Basic-Auth-Wert (`AUTHORIZATION: basic <base64("x-access-token:"+token)>`) bleibt unverändert.
  - `GetGitEnvironment` (Zeilen 214–235): Signatur um Zielhost/-URL erweitern und an den Helfer durchreichen.
  - `GetAgentEnvironmentAsync` (Zeilen 183–198): erhält den Zielhost der Arbeitskopie (siehe Interface-Änderung) und reicht ihn an den Helfer durch.
  - Aufrufer-Anpassungen — jeder Aufrufstelle muss ihr Ziel mitgeben:
    - `CloneRepositoryAsync` (Zeilen 1089–1130): Host der bereits auf HTTPS normalisierten `repositoryUrl`.
    - `ConfigureGitCredentialsAsync` (Zeilen 557–613): Host der übergebenen `repositoryUrl`.
    - `PushBranchAsync` (Zeilen 1133–1156) und `PullAsync` (Zeilen 1159–1182): Ziel ist `origin`; Host muss aus `remote.origin.url` der Arbeitskopie ermittelt werden (nach `NormalizeRemoteUrlAsync`, die die URL bereits liest/normalisiert).
    - `GetRemoteBranchesAsync` (Zeilen 1645–1676) und `GetDefaultBranchAsync` (Zeilen 1703–1732): Host der normalisierten `remoteUrl`.
    - `CheckoutPullRequestSourceAsync` (Zeilen 1743–1763): Ziel des `git fetch` ist `origin` (Same-Repository-Fall) bzw. `checkoutSpec.SourceRepositoryUrl` (Fork-Fall, siehe `CheckoutPullRequestSourceCoreAsync`, `GitPluginBase` Zeilen 201–212) — der Fork-Fall kann einen zweiten, abweichenden Host betreffen; Behandlung siehe Offene Fragen.
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`
  - `GetAgentEnvironmentAsync` (Zeile 22): Signatur um einen Host-/Repository-URL-Parameter erweitern, damit die Agenten-Umgebung denselben hostgebundenen Key erhält. XML-Doc anpassen.
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
  - `WithScmAgentEnvironmentAsync` (Zeilen 431–463): den Zielhost/die Repository-URL an `GetAgentEnvironmentAsync` durchreichen; die Quelle (neuer Parameter an `StartTerminalSessionAsync`/`StartCliAsync` oder Navigation über `RepositoryStartKonfiguration.GitRepository`) ist Entwurfsentscheidung — siehe Offene Fragen.
  - Aufrufer-Kette `StartTerminalSessionAsync`/`StartCliAsync` (Zeilen 174–183, 270–282): ggf. neuer optionaler Parameter.
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
  - Aufrufstellen `StartTerminalSessionAsync` (Zeilen 190–191, 297–304): `repository.RepositoryUrl` liegt hier bereits vor und kann als Quelle des Zielhosts dienen.
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
  - `CheckoutPullRequestSourceCoreAsync` (Zeilen 189–226): kein zwingender Eingriff, aber die Fetch-Zielwahl (`origin` vs. `SourceRepositoryUrl`) bestimmt, welcher Host den extraheader benötigt — relevant für die Mehr-Host-Behandlung im Fork-Fall.
- Tests:
  - `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`: Bestehende Assertions auf den hartcodierten Key (`GIT_CONFIG_KEY_0 == "http.https://github.com/.extraheader"`, Zeilen 1474, 2641) sowie die `GetAgentEnvironmentAsync`-Tests (Zeilen 1462–1510) an die neue Signatur/den hostgebundenen Key anpassen; neue Tests für GHE-Host (z. B. `http.https://ghe.example.com/.extraheader`), fehlenden Host (fail-closed) und ggf. Fork-Fall.
  - `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (Zeile 80) und `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_AgentActions.cs` (Zeilen 28, 86): Moq-Setups von `GetAgentEnvironmentAsync` auf die neue Signatur umstellen.
- Dokumentation (Lifecycle-Schritt 12, nicht Teil der Code-Änderung): `docs/help/plugins/business-rules.md` — die Regel „GitHub-Enterprise-Hosts authentifizieren nicht über HTTPS-git" (Zeilen 228–239) entfällt bzw. wird in eine Regel zur Host-Bindung des extraheader-Keys überführt; `docs/help/plugins/ablauf-technisch.md` (Zeilen 87–89) beschreibt den festen Key und ist zu aktualisieren.

### Nicht betroffene Artefakte

- `BitbucketPlugin` — eigener Auth-Mechanismus (`GetGitEnvironment` mit `_netrc`/`UpdateNetrcEntry`), implementiert `IScmAgentAccess` nicht.
- `GitHubHttpApiClient`/`IGitHubEventApiClient`, `ParseEventRepository` — REST-/Ereignis-APIs sind bereits hostfähig.
- `GitRepository`, `RepositoryStartKonfiguration` (Entities) — voraussichtlich keine Schema-/Modelländerung; der Host wird aus vorhandenen URLs abgeleitet.
- `LocalDirectoryPlugin`, UI-Komponenten — ohne Berührungspunkt.
- Keine neuen Enums oder Datenmodellklassen erforderlich.

## Implementierungsansatz

- Zentrale Änderung in `AddGitHubHttpAuthHeader`: Der extraheader-Key wird aus einem hostgebundenen Präfix gebildet (`http.https://<host>/.extraheader`). Als Host-Quelle bietet sich die bereits vorhandene URL-Normalisierung an: `ToHttpsRepositoryUrl` liefert eine HTTPS-URL, aus der der Host per `Uri.IdnHost` extrahiert werden kann (konsistent zu `ParseEventRepository`, das GHE-Hosts bereits per `IdnHost` normalisiert).
- `GetGitEnvironment` erhält den Zielhost bzw. die Ziel-URL als zusätzlichen Parameter und reicht ihn durch; jede Aufrufstelle übergibt den Host ihrer konkreten Operation. Für `localPath`-basierte Operationen (`PushBranchAsync`, `PullAsync`, `CheckoutPullRequestSourceAsync`) stehen zwei Wege offen (siehe Offene Fragen): Lesen von `remote.origin.url` aus der Arbeitskopie (bereits Teil von `NormalizeRemoteUrlAsync`, kann dessen Ergebnis wiederverwenden) oder Durchreichen der Repository-URL vom Aufrufer.
- `IScmAgentAccess.GetAgentEnvironmentAsync` wird um den Zielhost/die Repository-URL erweitert; `KiAusfuehrungsService.WithScmAgentEnvironmentAsync` reicht den Wert durch. Die URL liegt beim einzigen relevanten Aufrufer (`EntwicklungsprozessService`) als `repository.RepositoryUrl` vor; eine Durchreichung über die bestehenden optionalen Parameter der Start-Methoden ist naheliegend (Alternativen siehe Offene Fragen).
- Mehrere kontaktierte Hosts in einer Operation (Fork-Fetch): Das `GIT_CONFIG_COUNT`/`GIT_CONFIG_KEY_n`/`GIT_CONFIG_VALUE_n`-Schema erlaubt mehrere hostgebundene Einträge pro Prozessumgebung — es dürfen jedoch nur Hosts aufgenommen werden, denen das Credential zugehen darf; ein hostloser Eintrag bleibt ausgeschlossen.
- Fehlschlagsemantik bleibt unverändert: fehlender Token oder nicht bestimmbarer Host → kein `GIT_CONFIG_*`-Block, `GIT_TERMINAL_PROMPT=0` sorgt für nicht-interaktiven Fehlschlag (fail-closed statt Failover).
- Abhängigkeiten: keine neuen externen Abhängigkeiten; die Änderung ist auf das GitHub-Plugin, das `IScmAgentAccess`-Interface und die Aufrufer-Kette im Anwendungskern lokal begrenzt.

## Konfiguration

- **Keine neue Konfigurierbarkeit:** Der Zielhost wird nicht vom Anwender eingestellt, sondern aus der URL der konkreten git-Operation abgeleitet. Es wird weder ein `PluginSettingField` noch eine `AppEinstellungen`-Option ergänzt.
- Bestehende Plugin-Einstellungen (`Token`, `AgentToken`) bleiben unverändert; der Kontomodus `ScmKontoModus` wählt weiterhin nur das Credential, nicht den Host.

## Offene Fragen

1. **Host-Quelle für `localPath`-basierte Operationen:** `PushBranchAsync`, `PullAsync` und `CheckoutPullRequestSourceAsync` kennen nur die Arbeitskopie. Soll der Host aus `remote.origin.url` gelesen werden (zusätzlicher `git config`-Aufruf; `NormalizeRemoteUrlAsync` liest die URL ohnehin bereits — das Ergebnis könnte wiederverwendet werden), oder soll die Repository-URL vom Aufrufer (`EntwicklungsprozessService`) in die Plugin-Methoden durchgereicht werden? Letzteres wäre ein Signaturbruch an `IGitPlugin` für alle Plugins.
2. **Fork-Fall im PR-Checkout:** `CheckoutPullRequestSourceCoreAsync` fetches im Fork-Fall gegen `checkoutSpec.SourceRepositoryUrl` — eine URL aus PR-Metadaten. Bindet man den extraheader an diesen Host, könnte eine gefälschte/abweichende `clone_url` den PAT an einen Fremd-Host senden (genau der genannte Leak-Vektor, nur mit hostgebundenem Key). Gewünschte Semantik: (a) extraheader nur an den Host der verknüpften Repository-URL binden — ein Fork-Fetch auf einem Fremd-Host schlägt dann fail-closed fehl; oder (b) zusätzlicher hostgebundener Eintrag für den `SourceRepositoryUrl`-Host — mit dem Restrisiko, dass PR-Metadaten den Zielhost wählen. Variante (a) deckt den praxisüblichen Fall (GHE-Forks liegen auf derselben Instanz) ab.
3. **`IScmAgentAccess`-Signatur:** Soll `GetAgentEnvironmentAsync` die Repository-URL (String) oder den bereits extrahierten Host entgegennehmen — und woher bezieht `KiAusfuehrungsService` ihn? Naheliegend: neuer optionaler Parameter an `StartTerminalSessionAsync`/`StartCliAsync`, gespeist aus `repository.RepositoryUrl` im `EntwicklungsprozessService`. Alternativ: Zugriff über `RepositoryStartKonfiguration.GitRepository.RepositoryUrl` (Navigation-Property — das Ladeverhalten/Inkludieren müsste geprüft werden).
4. **`StartCliCoreAsync`-Pfad:** Nur `StartTerminalSessionCoreAsync` injiziert die Agenten-Umgebung (`WithScmAgentEnvironmentAsync`, Zeile 304); der einfache `StartCliCoreAsync` tut es nicht. Bleibt das bewusst so, oder soll die Host-Durchreichung dort ebenfalls erfolgen (derzeit keine `IScmAgentAccess`-Nutzung im Pipe-Pfad)?
5. **Ports im extraheader-Key:** GHE-Instanzen können auf Nicht-Standard-Ports laufen; git matched `http.https://host:port/` mit Port. Soll der Key bei Nicht-Default-Port den Port enthalten (`Uri.IsDefaultPort`-Prüfung analog `ParseEventRepository`/`ToHttpsRepositoryUrl`), oder beschränkt sich der Scope auf Standard-443-Instanzen?
6. **Host-Normalisierung:** Soll im Key die `IdnHost`-Form (IDN→Punycode) verwendet werden — konsistent zu `ParseEventRepository` — und wie sind IPv4-/IPv6-Literale zu behandeln (`ToHttpsRepositoryUrl` akzeptiert beide als gültige Hosts)?
7. **`CheckHealthAsync`:** Die Health-Probe adressiert fest `github.com` (API-Aufruf, kein git-Transport). Soll sie im gleichen Zug hostfähig gemacht werden, damit GHE-Verknüpfungen keinen dauerhaft „unhealthy"-Status melden — oder bleibt das bewusst außerhalb dieses Issues?
