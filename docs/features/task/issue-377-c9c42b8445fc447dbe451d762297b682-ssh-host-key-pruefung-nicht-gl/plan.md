# Umsetzungsplan: SSH-Host-Key-Prüfung nicht global aushebeln (Issue #377)

## Übersicht

Das `GitHubPlugin` und das `BitbucketPlugin` werden vollständig auf HTTPS-git-Transport festgelegt: Die `GIT_SSH_COMMAND`-Belegung in beiden `GetGitEnvironment`-Implementierungen und das persistente `git config core.sshCommand` im `GitHubPlugin.ConfigureGitCredentialsAsync` werden entfernt; SSH-Remote-URLs (`git@host:…`, `ssh://…`) werden an allen plugin-geführten Transportstellen beider Plugins deterministisch in HTTPS-URLs umgewandelt. Bestehende betreute Arbeitskopien werden bei der nächsten plugin-geführten Operation hygienisch bereinigt (SSH-`remote.origin.url` → HTTPS; beim GitHub-Plugin zusätzlich plugin-geschriebenes unsicheres `core.sshCommand` → `git config --unset` — das BitbucketPlugin hat diesen Eintrag nie geschrieben). Betroffen sind `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`, eine gemeinsame Hilfsmethode in `GitPluginBase` sowie die zugehörigen Unit-Tests; keine Interface-, Datenmodell-, UI- oder Konfigurationsänderung.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus `requirement.md` und den übermittelten Anwender-Antworten (2026-11-14):

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Jede vom `GitHubPlugin` bzw. `BitbucketPlugin` ausgeführte git-Transportoperation (`clone`, `push`, `pull`, `ls-remote`, PR-`fetch`/Checkout) sowie die vom `GitHubPlugin` in betreute Arbeitskopien geschriebene bzw. in beiden Plugins dort vorgefundene lokale git-Konfiguration (`remote.origin.url`, `core.sshCommand`) |
| Auslösende Ereignisse | Bestehende Plugin-Aufrufe ohne neuen Nutzertrigger: `CloneRepositoryAsync`, `PushBranchAsync`, `PullAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`, `CheckoutPullRequestSourceAsync` (Bitbucket: neuer Override über `CheckoutPullRequestSourceCoreAsync`), intern `ConfigureGitCredentialsAsync` (GitHub) und `NormalizeRemoteUrlAsync` (GitHub bestehend / Bitbucket neu). Kein UI-Eingriff. |
| Erwartete Wirkung | Keine plugin-gebaute Prozessumgebung beider Plugins enthält `GIT_SSH_COMMAND`; neue GitHub-Klone erhalten kein `core.sshCommand` in `.git/config`; alle plugin-geführten git-Aufrufe laufen gegen HTTPS-Remote-URLs — GitHub mit bestehender `GH_TOKEN`/`GIT_CONFIG_*`-`http.extraheader`-Auth, Bitbucket mit bestehendem Auth-Modell (Credentials-Einbettung in Clone-URL via `BuildAuthenticatedCloneUrl`, `_netrc`-Pflege via `UpdateNetrcEntry`, SelfHosted-`http.extraheader`-Args). Ohne Credentials greift der jeweilige nicht-blockierende Fail-Fast (`GIT_TERMINAL_PROMPT=0`; GitHub: `InvalidOperationException` mit Token-Hinweis, Bitbucket: „Bitbucket-Authentifizierung fehlt."). Sichtbar: Klonen/Pullen/Pushen/PR-Checkout funktionieren mit konfigurierten Zugangsdaten unverändert; die MITM-Angriffsfläche entfällt. |
| Umgang mit fehlenden Objekten | **SSH-`RepositoryUrl`** (`GitRepository.RepositoryUrl`, Parameter aller Remote-Methoden): deterministisch in HTTPS umwandeln (`git@host:path` → `https://host/path`, `ssh://[git@]host[:22]/path` → `https://host/path`); nicht umwandelbare Schemata werden beim Klon mit verständlicher `InvalidOperationException` abgelehnt. **SSH-`remote.origin.url` in Bestands-Arbeitskopien**: bei der nächsten `Push`/`Pull`-Operation (Bitbucket zusätzlich beim PR-Checkout) über `NormalizeRemoteUrlAsync` auf HTTPS umschreiben; beim GitHub-PR-Checkout ebenfalls. **Persistiertes unsicheres `core.sshCommand` in Bestands-Arbeitskopien**: nur GitHub — aktiv entfernen (`git config --unset`), aber nur wenn der Wert die Plugin-Signatur `StrictHostKeyChecking=no` trägt — wertbasiert, analog der `_netrc`-Legacy-Bereinigung aus #376 (Fehler nur `LogWarning`); Bitbucket hat den Eintrag nie geschrieben → keine Bereinigung. **Fehlende Credentials**: bestehende Fail-Fasts bleiben; GitHubs HTTPS-Token-Fail-Fast gilt durch die vorangestellte Umwandlung jetzt auch für ehemalige SSH-URLs, Bitbuckets Credential-Fail-Fast galt bereits für alle URLs. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine abgesicherte SSH-Alternative (`StrictHostKeyChecking=accept-new` + `known_hosts`) wird implementiert — SSH wird vollständig entfernt | Owner-Entscheidung „nur HTTPS nutzen", übermittelt über den Orchestrierungs-Kontext | bestätigt (Owner-Direktive) |
| 2 | GitHub-Enterprise-Hosts (≠ `github.com`): Der `http.extraheader`-Key bleibt fest auf `http.https://github.com/.extraheader` verdrahtet — HTTPS-Auth gegen GHE-Remotes greift nicht | Ein hostgebundener Key erfordert, den Zielhost durch `GetGitEnvironment`/`GetAgentEnvironmentAsync` zu reichen — eigene, querschnittliche Änderung. Ein generischer `http.extraheader` ist **keine** Option: Er würde den Token an jeden kontaktierten Host senden (Leak-Vektor über Fork-`clone_url`/`SourceRepositoryUrl` aus PR-Metadaten). Bislang lief GHE-git ggf. über den SSH-Bypass; künftig schlägt es nicht-interaktiv fehl statt unsicher zu funktionieren. **Folge-Issue für den hostgebundenen extraheader-Key wird separat durch den Orchestrator angelegt.** | bestätigt (Anwender, 2026-11-14) |
| 3 | Kein Opt-out-/Konfigurationsschalter, um SSH weiterhin zu erlauben | Owner-Direktive „nur HTTPS"; kein Setting in `GetRepositoryLinkFields`/`appsettings` vorgesehen | bestätigt (Owner-Direktive) |
| 4 | Keine DB-Migration/Normalisierung persistierter `GitRepository.RepositoryUrl`-Werte und keine Formatvalidierung in `ProjektService`/`RepositoryAssignViewModel` | SSH-URLs bleiben gültige Verknüpfungsdaten und werden transparent am Verwendungspunkt konvertiert; `RepositoryName`/ID-Extraktion bleibt davon unberührt | bestätigt (Anwender, 2026-11-14) |
| 5 | Bitbucket-Server-SSH-URLs mit Nicht-Standard-Port (typisch `ssh://git@host:7999/…`) werden nicht auf einen geratenen HTTPS-Port bzw. die konfigurierte `SelfHostedUrl` umgeschrieben — beim Klon verständliche `InvalidOperationException`, bei Bestands-Remotes bleibt die URL stehen und der normale git-Fehlerpfad greift | Der Konverter hat identische, deterministische Akzeptanzregeln für beide Plugins (Anwender-Direktive „gleiche Fehlersemantik"); der HTTPS-Port eines Bitbucket-Servers ist aus der SSH-URL nicht ableitbar — Raten wäre nicht deterministisch, ein sauberer Fehler mit HTTPS-Hinweis ist ehrlicher | bestätigt (Anwender-Direktive „gleiche Fehlersemantik", 2026-11-14) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Umgang mit SSH-`RepositoryUrl`s | Umwandeln statt Ablehnen (Gateway-Helper `ToHttpsRepositoryUrl`) | Ablehnen würde alle bestehenden SSH-Verknüpfungen hart brechen; die Auth läuft bei beiden Plugins ohnehin über HTTPS, und Host/Pfad sind verlustfrei aus der SSH-URL ableitbar — Umwandlung hält Bestandsdaten funktional und ist deterministisch. Nicht umwandelbare Schemata (`http://`, `git://`, Pfade, `ssh://` mit Fremd-Port) werden dagegen abgelehnt. |
| SSH→HTTPS-Konverter | **Gemeinsame `protected static`-Methode `ToHttpsRepositoryUrl` in `GitPluginBase<TPlugin>`** (`string?`-Rückgabe: HTTPS-URL oder `null`) | Beide Plugins brauchen identische Akzeptanzregeln; sie leben in getrennten Assemblies, die beide `Softwareschmiede.Plugin.Contracts` referenzieren — `GitPluginBase` ist der gemeinsame, bereits logiktragende Basistyp (`internal` würde die Assembly-Grenze nicht überwinden). `ParseEventRepository` (`GitHubPlugin.Events.cs:397-424`) wird bewusst **nicht** wiederverwendet: Es wirft `ScmProviderException(Permanent)` und verlangt exakt `owner/repo` (2 Segmente); der Transport-Konverter soll beliebig tiefe Pfade erhalten, das `.git`-Suffix bewahren und `null` statt Exception liefern. Akzeptanzregeln werden an `ParseEventRepository` gespiegelt (SCP-Regex, `ssh://` mit User `git`/leer und Port default/22). |
| Legacy-`core.sshCommand`-Bereinigung (nur GitHub) | Neue private Methode `RemoveInsecureSshCommandAsync(localPath, ct)`: `git config --get core.sshCommand` lesen, `git config --unset core.sshCommand` nur wenn der Wert `StrictHostKeyChecking=no` enthält; fehlender Schlüssel (`!IsSuccess`) und Fremd-Werte bleiben unangetastet; alle Fehler nur `LogWarning` | Wertbasiert statt unbedingtem Unset: Ein vom Anwender bewusst gesetztes eigenes `core.sshCommand` (z. B. `ssh -i key`) bleibt erhalten; nur die plugin-geschriebene, unsichere Signatur wird entfernt. Fachliches Vorbild: `GitHubPluginNetrcCleanupTests`-Muster aus #376. Nur repo-lokale Config — das Plugin hat nie globale Config geschrieben. **Bitbucket entfällt:** `BitbucketPlugin` hat den Eintrag nie geschrieben (Repo-weite Suche: einziger `core.sshCommand`-String in `GitHubPlugin.cs:552`); ein fremd gesetzter Wert darf nicht angefasst werden. |
| Auslöser der Bestandsbereinigung | Bei der nächsten plugin-geführten Operation auf der Arbeitskopie (`PushBranchAsync`, `PullAsync`, `CheckoutPullRequestSourceAsync`) — **nicht** im Konstruktor | Das #376-Konstruktor-Muster passt hier nicht: `core.sshCommand`/`remote.origin.url` liegen pro Arbeitskopie in `.git/config`, die Plugins kennen die Pfade aller betreuten Kopien zum Instanziierungszeitpunkt nicht. Push/Pull sind ohnehin die natürlichen nächsten Operationen; der PR-Checkout wird mitbehandelt, da er auf derselben betreuten Kopie arbeitet und bei SSH-`origin` sonst nicht-interaktiv scheitern würde. |
| `remote.origin.url`-Umschreibung (GitHub) | `NormalizeRemoteUrlAsync` erweitern: nach Token-Stripping zusätzlich `ToHttpsRepositoryUrl` anwenden; `set-url` nur bei Differenz | `NormalizeRemoteUrlAsync` ist der etablierte Erweiterungspunkt (wird bereits vor Push/Pull aufgerufen, Fehler nur `LogWarning`). |
| `remote.origin.url`-Umschreibung (Bitbucket) | Neue private `NormalizeRemoteUrlAsync(localPath, ct)` im `BitbucketPlugin`: `git config remote.origin.url` lesen → `ToHttpsRepositoryUrl`; bei SSH-Form Ziel-URL = `BuildAuthenticatedCloneUrl(httpsUrl, user, appPassword)` wenn Credentials vorhanden (entspricht exakt dem Zustand eines frischen Klons), sonst nackte HTTPS-URL; `set-url` nur bei Differenz; Fehler nur `LogWarning` | Bitbucket hat kein `NormalizeRemoteUrlAsync`-Äquivalent — Push/Pull laufen direkt gegen `origin`. Das Bitbucket-Auth-Modell bettet `user:appPassword` in die Remote-URL ein (`BuildAuthenticatedCloneUrl`, Z. 235-244); eingebettete Credentials sind dort **kein** Legacy-Dreck wie GitHubs `oauth2:`-Tokens, sondern das vorgesehene Auth-Verfahren für Cloud-Pull/Push — daher kein Token-Stripping, sondern Re-Einbettung mit den aktuellen Credentials. `GetGitEnvironment` schreibt zusätzlich die `_netrc`-Stanza, sodass auch eine nackte HTTPS-URL authentifizieren würde. |
| Bitbucket-PR-Checkout | Neuer `CheckoutPullRequestSourceAsync`-Override: `NormalizeRemoteUrlAsync` vorab, `SourceRepositoryUrl` der Spec via `with`-Ausdruck normalisieren, dann `CheckoutPullRequestSourceCoreAsync(localPath, spec, GetGitEnvironment(), ct)` | `BitbucketPlugin` überschreibt die Methode bisher nicht — der Basisklassen-Default läuft mit `environmentVariables: null`, d. h. weder `GIT_TERMINAL_PROMPT=0` (theoretische Prompt-Blockade bei Fork-Fetch gegen HTTPS) noch die `_netrc`-Seitenwirkung von `GetGitEnvironment`. Der Override liefert erstmals Env + netrc-Auth für Same-Host-Forks und schließt den letzten Transportpfad. `-c http.extraheader`-Args können durch `CheckoutPullRequestSourceCoreAsync` nicht gereicht werden — die `_netrc`-Stanza des konfigurierten Hosts deckt denselben Auth-Fall ab. Credentials werden **nicht** in die konvertierte `SourceRepositoryUrl` eingebettet (netrc authentifiziert hostbezogen; die URL wird durch `EntwicklungsprozessService`/`PullRequestReferenz` persistiert und weitergereicht). |
| Bitbucket-`_netrc`-Testseam | `GetGitEnvironment` ermittelt den netrc-Pfad künftig über eine neue private `ResolveNetrcPath()` — optionaler Konstruktorparameter `netrcPath` → Env `SOFTWARESCHMIEDE_NETRC_PATH` → bisheriger Profilpfad (`%USERPROFILE%\_netrc`/`~/.netrc`). **Verdrahtung:** Beide Bitbucket-Testfixtures (`BitbucketPluginTests`, `BitbucketPluginTests_GetRepositoryStructureAsync`) instanziieren `_sut` mit eindeutiger Temp-Datei pro Testinstanz | `UpdateNetrcEntry` schreibt heute ungeschützt die echte Benutzer-`_netrc` (kein Seam): Jeder Test, der Credentials setzt und eine git-Operation ausführt, würde eine reale `machine bitbucket.org`-Stanza mit Test-Credentials **überschreiben** — dieser Seiteneffekt existiert bereits heute in `BitbucketPluginTests_GetRepositoryStructureAsync` (Fixture setzt `GetCredential(It.IsAny) → "test"`; `SetupDefaultBranch`-Tests laufen über `GetDefaultBranchAsync` → `GetGitEnvironment` → `UpdateNetrcEntry`). Vorbild `GitHubPlugin.ResolveNetrcPath` (Z. 239-255) aus #376 — gleiche Auflösungsreihenfolge, gleiche Env-Variable. Produktionsverhalten unverändert (optionaler Parameter, Default = bisheriger Pfad). |
| Fehlersemantik nicht-HTTPS-URLs | `CloneRepositoryAsync` (beide Plugins): harte `InvalidOperationException` mit verständlicher Meldung, vor jedem CLI-Aufruf. `GetRemoteBranchesAsync`/`GetDefaultBranchAsync` (beide): `ToHttpsRepositoryUrl(url) ?? url` — nicht umwandelbare URLs laufen in den bestehenden Soft-Fail-Pfad (LogWarning + `[]`/„main"-Fallback) | Der Klon ist die einzige Stelle mit bestehendem Fail-Fast-Charakter; die `ls-remote`-Methoden sind per Vertrag tolerant (leere Liste/Fallback statt Exception, u. a. für die Branch-Vorschlags-UI in `RepositoryAssignViewModel`). Bitbucket: Der Credential-Fail-Fast (`InvalidOperationException` „Bitbucket-Authentifizierung fehlt.") bleibt an seiner Position; die URL-Konvertierung folgt danach und vor `ResolveGitCloneUrl`/`BuildAuthenticatedCloneUrl`. |
| SSH-Toleranz bei ID-Extraktion | GitHub: `TryExtractRepositoryId`, `ParseEventRepository`, `GitOrchestrationService.ExtractRepositoryIdFromUrl` und der `ssh_url`-Fallback in `ParsePullRequest` bleiben unverändert. Bitbucket: `TryExtractRepositoryId` bleibt unverändert (`new Uri`-basiert; SCP wirft `UriFormatException` → `null`), aber `GetRepositoryStructureLoadResultAsync` konvertiert die Eingangs-URL vorher per `ToHttpsRepositoryUrl` | Diese Pfade extrahieren nur Host/`owner`-/`repo`-IDs aus URLs — kein git-Transport über SSH. Entfernen würde Bestandsverknüpfungen mit SSH-`RepositoryUrl` brechen (z. B. `GitOrchestrationServiceTests.PullRequestErstellenAsync_ShouldResolveRepositoryId_FromSshRepositoryUrl`). Die Vorab-Konvertierung im Bitbucket-Structure-Pfad macht SCP-`RepositoryUrl`s dort erstmals überhaupt auflösbar (heute `null` → „Repository-ID konnte nicht ermittelt werden") — gewollter Nebennutzen der transparenten Konvertierung. |

## Programmabläufe

### Klon (`GitHubPlugin.CloneRepositoryAsync`)

1. `repositoryUrl = ToHttpsRepositoryUrl(repositoryUrl)` — liefert `null` → `InvalidOperationException` mit verständlicher Meldung („nur HTTPS-Repository-URLs werden verwendet; SSH-URLs werden automatisch konvertiert"), **vor jedem CLI-Aufruf**.
2. Token aus `ICredentialStore` lesen; `IsHttpsRepositoryUrl` greift jetzt für jede ankommende URL → ohne Token bestehender Fail-Fast `InvalidOperationException` (Token-Hinweis).
3. `git clone <https-url> <targetPath>` mit `GetGitEnvironment(token)` — Env ohne `GIT_SSH_COMMAND`; bei `GH_TOKEN`/`GIT_CONFIG_*`-extraheader wie bisher. Fehlerbehandlung (`IsAuthenticationFailure` → Token-Meldung, sonst sanitisierte Fehlermeldung) unverändert.
4. `ConfigureGitCredentialsAsync(targetPath, httpsUrl, ct)`: `git config user.name`, `git config user.email`, `git remote set-url origin <https-url>` unverändert; der gesamte `git config core.sshCommand …`-Block (`GitHubPlugin.cs:549-560`) entfällt ersatzlos.

Beteiligt: `GitHubPlugin.CloneRepositoryAsync`, `GitPluginBase.ToHttpsRepositoryUrl`, `IsHttpsRepositoryUrl`, `GetGitEnvironment`, `ConfigureGitCredentialsAsync`, `ICliRunner`, `ICredentialStore`.

### Push / Pull (`GitHubPlugin.PushBranchAsync`, `GitHubPlugin.PullAsync`)

1. `NormalizeRemoteUrlAsync(localPath, ct)`: `git config remote.origin.url` lesen → eingebetteten Legacy-Token per `EmbeddedTokenPattern` strippen → `ToHttpsRepositoryUrl` auf das Ergebnis anwenden (deckt `git@`- und `ssh://`-Remotes ab) → `git remote set-url origin <normalisierte-url>` nur bei Differenz; Fehler nur `LogWarning`.
2. `RemoveInsecureSshCommandAsync(localPath, ct)`: `git config --get core.sshCommand` → bei Erfolg und Wert enthält `StrictHostKeyChecking=no` → `git config --unset core.sshCommand` + `LogInformation`; sonst Rückkehr; Fehler nur `LogWarning`. Null-/Fehlschlag-tolerant (`result?.IsSuccess != true` → Return), damit lose Mocks und Nicht-git-Pfade nicht brechen.
3. `git push --set-upstream origin <branch>` bzw. `git pull` mit `GetGitEnvironment(token)` — ohne `GIT_SSH_COMMAND`.

Beteiligt: `GitHubPlugin.PushBranchAsync`/`PullAsync`, `NormalizeRemoteUrlAsync`, `RemoveInsecureSshCommandAsync`, `GitPluginBase.ToHttpsRepositoryUrl`, `GetGitEnvironment`.

### Branch-Abfragen (`GitHubPlugin.GetRemoteBranchesAsync`, `GitHubPlugin.GetDefaultBranchAsync`)

1. `var remoteUrl = ToHttpsRepositoryUrl(repositoryUrl) ?? repositoryUrl;`
2. `git ls-remote --heads <remoteUrl>` bzw. `git ls-remote --symref <remoteUrl> HEAD` mit `GetGitEnvironment()` — ohne `GIT_SSH_COMMAND`. Bestehende Fehler-/Fallback-Semantik unverändert.

Beteiligt: `GitHubPlugin.GetRemoteBranchesAsync`/`GetDefaultBranchAsync`, `GitPluginBase.ToHttpsRepositoryUrl`, `GetGitEnvironment`.

### PR-Quell-Checkout (`GitHubPlugin.CheckoutPullRequestSourceAsync`)

1. `NormalizeRemoteUrlAsync(localPath, ct)` + `RemoveInsecureSshCommandAsync(localPath, ct)` auf der betreuten Kopie (Legacy-Hygiene; bei same-repo-PRs läuft der `fetch` gegen `origin` — eine SSH-`origin`-URL würde ohne Bypass nicht-interaktiv scheitern).
2. Spec-Normalisierung: ist `checkoutSpec.SourceRepositoryUrl` gesetzt, `checkoutSpec with { SourceRepositoryUrl = ToHttpsRepositoryUrl(url) ?? url }` (Fork-Fall, z. B. `ssh_url`-Fallback aus `ParsePullRequest`).
3. `CheckoutPullRequestSourceCoreAsync(localPath, spec, GetGitEnvironment(), ct)` — Basisklassenlogik unverändert (`fetch --no-tags`, `checkout -B`, optionale `rev-parse HEAD`-Verifikation).

Beteiligt: `GitHubPlugin.CheckoutPullRequestSourceAsync`, `GitPluginBase.CheckoutPullRequestSourceCoreAsync`, `PullRequestCheckoutSpec` (record — `with`-Ausdruck), `NormalizeRemoteUrlAsync`, `RemoveInsecureSshCommandAsync`, `GitPluginBase.ToHttpsRepositoryUrl`.

### Prozessumgebung (`GitHubPlugin.GetGitEnvironment`)

1. `GIT_TERMINAL_PROMPT=0` bleibt; die `GIT_SSH_COMMAND`-Belegung (`GitHubPlugin.cs:215`) samt Kommentar entfällt.
2. `GH_TOKEN` + `AddGitHubHttpAuthHeader` bei vorhandenem Token unverändert (extraheader-Key bleibt `http.https://github.com/.extraheader`, siehe Scope-Ausschluss 2).

Beteiligt: `GitHubPlugin.GetGitEnvironment`, `AddGitHubHttpAuthHeader`.

### Klon (`BitbucketPlugin.CloneRepositoryAsync`)

1. Bestehender Credential-Fail-Fast unverändert: `user`/`appPassword` aus `ICredentialStore`; leer → `InvalidOperationException` „Bitbucket-Authentifizierung fehlt." (gilt bereits für alle URL-Formen).
2. `var httpsUrl = ToHttpsRepositoryUrl(repositoryUrl)` — `null` → `InvalidOperationException` mit verständlicher Meldung (HTTPS-only, SSH-URLs werden automatisch konvertiert), **vor jedem CLI-Aufruf**. Damit wird auch die heutige `UriFormatException` aus `BuildAuthenticatedCloneUrl` für SCP-`git@`-URLs in einen geplanten, klaren Pfad überführt — `git@bitbucket.org:ws/repo` klont künftig erstmalig korrekt.
3. Hosting-Modus: SelfHosted → `resolvedUrl = ResolveGitCloneUrl(httpsUrl)` (Browser-/API-Formen → `https://host/scm/KEY/SLUG.git`); Cloud → `resolvedUrl = httpsUrl`.
4. `cloneUrl = BuildAuthenticatedCloneUrl(resolvedUrl, user, appPassword)` — Credentials-Einbettung (`user:appPassword@`) unverändert, bleibt das Auth-Modell.
5. `git clone <cloneUrl> <targetPath>` mit `GetGitEnvironment()` — Env ohne `GIT_SSH_COMMAND`; `_netrc`-Pflege (`UpdateNetrcEntry` über `ResolveNetrcPath()`) unverändert.

Beteiligt: `BitbucketPlugin.CloneRepositoryAsync`, `GitPluginBase.ToHttpsRepositoryUrl`, `ResolveGitCloneUrl`, `BuildAuthenticatedCloneUrl`, `GetGitEnvironment`, `ResolveNetrcPath`, `ICliRunner`, `ICredentialStore`.

### Push / Pull (`BitbucketPlugin.PushBranchAsync`, `BitbucketPlugin.PullAsync`)

1. `NormalizeRemoteUrlAsync(localPath, ct)` (neu): `git config remote.origin.url` lesen (**ohne Env**, Fehler nur `LogWarning`, null-tolerant) → `ToHttpsRepositoryUrl` anwenden; liefert `null` oder identische URL → keine Änderung; bei SSH-Form: Ziel = `BuildAuthenticatedCloneUrl(httpsUrl, user, appPassword)` bei vorhandenen Credentials, sonst `httpsUrl` → `git remote set-url origin <ziel>` nur bei Differenz.
2. `git [-c credential.helper= -c http.extraheader=…] pull` bzw. `push --set-upstream origin <branch>` — `GetGitHttpAuthArgs()` (SelfHosted) unverändert — mit `GetGitEnvironment()` ohne `GIT_SSH_COMMAND`. Bestehende Fehlersemantik (`InvalidOperationException` bei `!IsSuccess`) unverändert.

Beteiligt: `BitbucketPlugin.PushBranchAsync`/`PullAsync`, `NormalizeRemoteUrlAsync` (neu), `GitPluginBase.ToHttpsRepositoryUrl`, `BuildAuthenticatedCloneUrl`, `GetGitHttpAuthArgs`, `GetGitEnvironment`.

### Branch-Abfragen (`BitbucketPlugin.GetRemoteBranchesAsync`, `BitbucketPlugin.GetDefaultBranchAsync`)

1. `var remoteUrl = ToHttpsRepositoryUrl(repositoryUrl) ?? repositoryUrl;`
2. `git ls-remote --heads <remoteUrl>` bzw. `git ls-remote --symref <remoteUrl> HEAD` mit `GetGitEnvironment()` — ohne `GIT_SSH_COMMAND`. Bestehende Fehler-/Fallback-Semantik unverändert (leere Liste / `"main"`).

Beteiligt: `BitbucketPlugin.GetRemoteBranchesAsync`/`GetDefaultBranchAsync`, `GitPluginBase.ToHttpsRepositoryUrl`, `GetGitEnvironment`.

### PR-Quell-Checkout (`BitbucketPlugin.CheckoutPullRequestSourceAsync`, neuer Override)

1. `NormalizeRemoteUrlAsync(localPath, ct)` auf der betreuten Kopie (SSH-`origin` → HTTPS vor dem `fetch`).
2. Spec-Normalisierung: ist `checkoutSpec.SourceRepositoryUrl` gesetzt, `checkoutSpec with { SourceRepositoryUrl = ToHttpsRepositoryUrl(url) ?? url }` — deckt den Fall ab, dass `ReadBitbucketCloneLink` keinen `https`-Clone-Link findet und auf einen `ssh`-Link zurückfällt (Z. 571-576).
3. `CheckoutPullRequestSourceCoreAsync(localPath, spec, GetGitEnvironment(), ct)` — erstmals mit Env: `GIT_TERMINAL_PROMPT=0` verhindert Prompt-Blockaden, der `GetGitEnvironment`-Aufruf pflegt die `_netrc`-Stanza für den konfigurierten Host (Auth für Same-Host-Forks und `origin`-Fetch ohne Userinfo-URL). Basisklassenlogik unverändert.

Beteiligt: `BitbucketPlugin.CheckoutPullRequestSourceAsync` (neu), `GitPluginBase.CheckoutPullRequestSourceCoreAsync`, `PullRequestCheckoutSpec` (record), `NormalizeRemoteUrlAsync` (neu), `GitPluginBase.ToHttpsRepositoryUrl`, `GetGitEnvironment`.

### Repository-Struktur (`BitbucketPlugin.GetRepositoryStructureLoadResultAsync`)

1. `repositoryUrl = ToHttpsRepositoryUrl(repositoryUrl) ?? repositoryUrl` am Methodenanfang — macht SCP-/`ssh://`-URLs für das `new Uri`-basierte `TryExtractRepositoryId` (Z. 1444-1485) auflösbar; die konvertierte URL fließt auch in den internen `GetDefaultBranchAsync`-Aufruf (Z. 1097).
2. Rest unverändert (Bitbucket-Source-API via `curl`, kein git-Transport).

Beteiligt: `BitbucketPlugin.GetRepositoryStructureLoadResultAsync`, `GitPluginBase.ToHttpsRepositoryUrl`, `TryExtractRepositoryId`, `GetDefaultBranchAsync`.

### Prozessumgebung (`BitbucketPlugin.GetGitEnvironment`)

1. `GIT_TERMINAL_PROMPT=0` bleibt; die `GIT_SSH_COMMAND`-Belegung (`BitBucketPlugin.cs:172`) entfällt.
2. `_netrc`-Pflege unverändert (`UpdateNetrcEntry` bei vorhandenem `user`/`appPassword` für `bitbucket.org` bzw. `SelfHostedUrl`-Host); der Pfad wird künftig über die neue `ResolveNetrcPath()` ermittelt (Ctor-Parameter `netrcPath` → `SOFTWARESCHMIEDE_NETRC_PATH` → bisheriger Profilpfad) — reine Testseam, kein Verhaltensunterschied produktiv.

Beteiligt: `BitbucketPlugin.GetGitEnvironment`, `ResolveNetrcPath` (neu), `UpdateNetrcEntry`.

## Neue Klassen

Keine — alle Erweiterungen sind (private bzw. `protected static`) Hilfsmethoden in den bestehenden Klassen `GitPluginBase<TPlugin>`, `GitHubPlugin` und `BitbucketPlugin`.

## Änderungen an bestehenden Klassen

### `GitPluginBase<TPlugin>` (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`)

- **Neue Methoden:**
  - `ToHttpsRepositoryUrl(string repositoryUrl)` — `protected static`, Rückgabe `string?`: `https://`-URLs unverändert; `git@host:path` (SCP-Regex gespiegelt zu `GitHubPlugin.ParseEventRepository`) → `https://host/path`; `ssh://[git@]host[:default|22]/path` → `https://host/path`; alle anderen Schemata/Eingaben → `null`. Pfad und `.git`-Suffix bleiben erhalten; kein `owner/repo`-Zwang; kein Userinfo außer `git`/leer; kein Fremd-Port.

### `GitHubPlugin` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`)

- **Neue Methoden:**
  - `RemoveInsecureSshCommandAsync(string localPath, CancellationToken ct)` — `private`: `git config --get core.sshCommand`; bei `result?.IsSuccess == true` und stdout enthält `StrictHostKeyChecking=no` → `git config --unset core.sshCommand` + `LogInformation`; alle Fehler nur `LogWarning`.
- **Geänderte Methoden:**
  - `GetGitEnvironment` — `GIT_SSH_COMMAND`-Zeile und Kommentar entfernen; Rest unverändert.
  - `CloneRepositoryAsync` — Eingangs-URL per `ToHttpsRepositoryUrl` (Basisklasse) normalisieren bzw. mit `InvalidOperationException` ablehnen; konvertierte URL für Klon, Token-Check und `ConfigureGitCredentialsAsync` verwenden. Konsequenz: Jede konvertierbare SSH-URL unterliegt jetzt dem HTTPS-Token-Fail-Fast.
  - `NormalizeRemoteUrlAsync` — zusätzlich zum Token-Stripping `ToHttpsRepositoryUrl` anwenden; `set-url` bei Differenz gegenüber der gelesenen URL.
  - `ConfigureGitCredentialsAsync` — `git config core.sshCommand …`-Aufruf (Z. 549-560) samt Kommentar entfernen.
  - `PushBranchAsync`, `PullAsync` — nach `NormalizeRemoteUrlAsync` zusätzlich `RemoveInsecureSshCommandAsync` aufrufen.
  - `GetRemoteBranchesAsync`, `GetDefaultBranchAsync` — `repositoryUrl` vor `ls-remote` per `ToHttpsRepositoryUrl(url) ?? url` normalisieren.
  - `CheckoutPullRequestSourceAsync` — `NormalizeRemoteUrlAsync` + `RemoveInsecureSshCommandAsync` vorweg; `SourceRepositoryUrl` der Spec via `with`-Ausdruck normalisieren, dann `CheckoutPullRequestSourceCoreAsync`.

### `BitbucketPlugin` (`plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`)

- **Neue Methoden:**
  - `NormalizeRemoteUrlAsync(string localPath, CancellationToken ct)` — `private`: `git config remote.origin.url` (ohne Env) lesen → `ToHttpsRepositoryUrl`; SSH-Form → Ziel-URL mit `BuildAuthenticatedCloneUrl` (Credentials vorhanden) oder nackte HTTPS-URL; `git remote set-url origin` nur bei Differenz; alle Fehler nur `LogWarning`; null-/fehlschlagtolerant (`result?.IsSuccess != true` → Return).
  - `ResolveNetrcPath()` — `private`: Konstruktorparameter `netrcPath` → `SOFTWARESCHMIEDE_NETRC_PATH` → `Path.Combine(UserProfile, "_netrc"|".netrc")` (bisheriger Inline-Pfad); Vorbild `GitHubPlugin.ResolveNetrcPath`.
  - `CheckoutPullRequestSourceAsync(string localPath, PullRequestCheckoutSpec checkoutSpec, CancellationToken ct)` — `public override`: `NormalizeRemoteUrlAsync` vorab; `SourceRepositoryUrl` der Spec via `with` normalisieren; Delegation an `CheckoutPullRequestSourceCoreAsync(localPath, spec, GetGitEnvironment(), ct)`.
- **Geänderte Methoden:**
  - `GetGitEnvironment` — `GIT_SSH_COMMAND`-Belegung (Z. 172) entfernen; netrc-Pfad über `ResolveNetrcPath()` statt Inline-Ermittlung; Rest unverändert.
  - `CloneRepositoryAsync` — nach dem Credential-Fail-Fast `repositoryUrl` per `ToHttpsRepositoryUrl` normalisieren bzw. bei `null` mit `InvalidOperationException` ablehnen; konvertierte URL in `ResolveGitCloneUrl` (SelfHosted) bzw. direkt in `BuildAuthenticatedCloneUrl` verwenden.
  - `PullAsync`, `PushBranchAsync` — `NormalizeRemoteUrlAsync(localPath, ct)` vor dem git-Aufruf.
  - `GetRemoteBranchesAsync`, `GetDefaultBranchAsync` — `repositoryUrl` vor `ls-remote` per `ToHttpsRepositoryUrl(url) ?? url` normalisieren.
  - `GetRepositoryStructureLoadResultAsync` — `repositoryUrl` am Methodenanfang per `ToHttpsRepositoryUrl(url) ?? url` normalisieren (macht SCP-URLs für `TryExtractRepositoryId` auflösbar).
- **Konstruktor:** optionaler Parameter `string? netrcPath = null` (Testseam, siehe `ResolveNetrcPath`); bestehende Aufrufer unverändert (nur Tests instanziieren das Plugin direkt; Plugin-Lader nutzen DI/Reflection mit optionaler Parameter-Unterstützung).

### `IGitPlugin`, `ICliRunner`, Datenmodell

Keine Änderungen — Signaturen und Schnittstellenverhalten bleiben unverändert. `GitPluginBase.CheckoutPullRequestSourceAsync`/`…CoreAsync` bleiben unverändert (Bitbucket nutzt die bestehenden Extension Points).

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `repositoryUrl` in `GitHubPlugin.CloneRepositoryAsync` | Nur `https://` direkt oder konvertierbare SSH-Formen (`git@host:path`, `ssh://[git@]host[:22]/path`) zulässig | `ToHttpsRepositoryUrl` → `null` ⇒ `InvalidOperationException` mit verständlicher Meldung, kein CLI-Aufruf |
| `repositoryUrl` in `BitbucketPlugin.CloneRepositoryAsync` | Identische Akzeptanzregeln (gemeinsamer Basisklassen-Konverter); zusätzlich SelfHosted-Browser-/API-Formen via `ResolveGitCloneUrl` | `ToHttpsRepositoryUrl` → `null` (inkl. `ssh://` mit Fremd-Port wie `:7999`) ⇒ `InvalidOperationException`, kein CLI-Aufruf |
| `remote.origin.url` in `GitHubPlugin.NormalizeRemoteUrlAsync` | Nach Token-Stripping SSH-Form → HTTPS umschreiben; nicht konvertierbar → URL unverändert lassen | Umwandlung `null` ⇒ kein `set-url`; git-Operation läuft in ihren normalen Fehlerpfad |
| `remote.origin.url` in `BitbucketPlugin.NormalizeRemoteUrlAsync` | SSH-Form → HTTPS (mit Credentials-Einbettung bei vorhandenen Credentials); HTTPS → unverändert; nicht konvertierbar → unverändert | Umwandlung `null` oder identisch ⇒ kein `set-url`; git-Operation läuft in ihren normalen Fehlerpfad |
| `core.sshCommand` in `GitHubPlugin.RemoveInsecureSshCommandAsync` | Unset nur, wenn Wert `StrictHostKeyChecking=no` enthält | Fremd-Wert oder fehlender Schlüssel ⇒ keine Änderung |
| `repositoryUrl` in `GetRemoteBranchesAsync`/`GetDefaultBranchAsync`/`CheckoutPullRequestSourceAsync` (beide Plugins) | Konvertieren wenn möglich, sonst Original weiterreichen | Weiterreichen ⇒ bestehender Soft-Fail-/Exception-Pfad des git-Aufrufs |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `SOFTWARESCHMIEDE_NETRC_PATH` | Umgebungsvariable (Testseam) | nicht gesetzt | Wird zusätzlich zum `GitHubPlugin` (bestehend seit #376) nun auch vom `BitbucketPlugin.ResolveNetrcPath` ausgewertet — damit credential-basierte Tests nicht die echte Benutzer-`_netrc` überschreiben |

HTTPS-only ist fest verdrahtet; kein Plugin-Setting, kein `appsettings`-Eintrag, kein Opt-out (Scope-Ausschluss 3).

## Seiteneffekte und Risiken

- **Klon ohne Credentials über SSH-URL (beide Plugins):** GitHub — bisher möglich (SSH-Keys des Nutzers + deaktivierte Host-Key-Prüfung), künftig Fail-Fast `InvalidOperationException` — gewollt. Bitbucket — Credential-Fail-Fast existierte bereits für alle URLs; SCP-`git@`-URLs scheiterten bisher ohnehin mit `UriFormatException` in `BuildAuthenticatedCloneUrl` (nie ein funktionierender Pfad); `ssh://`-Klone funktionierten über den Bypass und laufen künftig über HTTPS mit eingebetteten Credentials.
- **GHE-Verknüpfungen mit SSH-URL:** Bisher funktionierten sie ggf. über Nutzer-SSH-Keys; nach Umwandlung scheitert die HTTPS-Auth (extraheader-Key nur github.com-gebunden) nicht-interaktiv — dokumentierter Scope-Ausschluss 2 mit Folge-Issue (Orchestrator).
- **Bitbucket-Server-SSH mit Nicht-Standard-Port (`ssh://git@host:7999/…`):** Wird nicht konvertiert — beim Klon klare `InvalidOperationException`; als `remote.origin.url` in Bestands-Kopien bleibt die URL stehen und der git-Fehlerpfad greift (kein `GIT_SSH_COMMAND` mehr ⇒ sauberer, nicht-interaktiver Fehlschlag statt unsicherem Erfolg). Scope-Ausschluss 5.
- **`ScmE2EGitRunner`/E2E-Fixture:** `ConfigureGitCredentialsAsync` schreibt in der E2E-Fixture-Kopie kein `core.sshCommand` mehr — Verhaltensänderung im Fixture, aber ohne Test-Impact (Env wird ohnehin verworfen, nichts assertiert das Schreiben).
- **Bestehende Mock-basierte Tests (beide Plugins):** Die neuen `git config`-Aufrufe (`--get core.sshCommand` bei GitHub; `remote.origin.url` bei Bitbucket-Push/-Pull/-Checkout) treffen in bestehenden Tests auf nicht aufgesetzte `ICliRunner`-Setups — die Null-tolerante Implementierung (`result?.IsSuccess != true` → Return) verhindert NREs; die Tests erhalten trotzdem explizite Setups für Nachvollziehbarkeit (siehe „Betroffene bestehende Tests").
- **Bitbucket-Tests mit Credentials:** Ohne die neue `netrcPath`-Seam würde `GetGitEnvironment` die echte `%USERPROFILE%\_netrc` mit Test-Credentials überschreiben — die Seam muss vor bzw. mit den neuen Tests eingeführt werden (Umsetzungsreihenfolge) **und in beiden Bitbucket-Testfixtures verdrahtet werden**: `BitbucketPluginTests` (neue credential-basierte Klon-/Push-/Pull-/Checkout-Tests) und `BitbucketPluginTests_GetRepositoryStructureAsync` (bestehender Seiteneffekt — `GetCredential(It.IsAny) → "test"` macht Credentials nicht-leer, `SetupDefaultBranch`-Tests schreiben heute schon die echte `_netrc`) erhalten jeweils eine eindeutige Temp-Datei pro Testinstanz.
- **`BitbucketPlugin.CheckoutPullRequestSourceAsync`-Override:** Die Basisklassen-Variante lief bisher mit `environmentVariables: null`; der Override übergibt `GetGitEnvironment()` — neuer positiver Effekt (`GIT_TERMINAL_PROMPT=0`, netrc-Pflege), kein Risiko. `GitPluginBaseTests` (basisklassenbezogen, `env == null`-Erwartung) bleiben unberührt, da die virtuelle Basismethode unverändert bleibt.
- **`NormalizeRemoteUrlAsync` (GitHub) bei `ssh://`-Remotes mit eingebettetem Userinfo-Token:** Edge Case `ssh://oauth2:token@host/…` — Token-Stripping erfolgt vor der Konvertierung; verbleibender Userinfo-Anteil führt zu `ToHttpsRepositoryUrl`-`null` (Userinfo ≠ `git`) → URL bleibt, git-Fehlerpfad greift. Bewusst akzeptiert (kein realer GitHub-Fall).
- **`BitbucketPlugin.GetRepositoryStructureLoadResultAsync`:** SCP-`RepositoryUrl`s werden erstmals aufgelöst statt mit „Repository-ID konnte nicht ermittelt werden" zu scheitern — Verhaltensverbesserung, kein Breaking.
- **Dokumentation:** `docs/help/plugins/ablauf-technisch.md` (Z. 88, 90, 92) und `docs/help/plugins/business-rules.md` (Z. 160) beschreiben `GIT_SSH_COMMAND`/`core.sshCommand` als Ist-Zustand — Aktualisierung inkl. Bitbucket-Env-Beschreibung im Lifecycle-Schritt „Dokumentation" eingeplant (Tasks-Datei).
- **Agenten-git-Operationen:** `GitHubPlugin.GetAgentEnvironmentAsync` war bereits frei von `GIT_SSH_COMMAND`; nach der Bestandsbereinigung erben Agenten-Aufrufe in betreuten Kopien auch kein persistentes `core.sshCommand` mehr — Zielzustand. `BitbucketPlugin` implementiert `IScmAgentAccess` nicht — nichts zu tun.

## Umsetzungsreihenfolge

1. **`ToHttpsRepositoryUrl`-Helper in `GitPluginBase<TPlugin>` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `protected static`-Methode in `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`: `https` → Eingabe; SCP-Form und `ssh://` (User `git`/leer, Port default/22) → `https://host/path`; sonst `null`.
2. **`GitHubPlugin.GetGitEnvironment` bereinigen**
   - Voraussetzungen: Keine.
   - Beschreibung: `GIT_SSH_COMMAND`-Belegung und Kommentarzeile entfernen.
3. **`GitHubPlugin.CloneRepositoryAsync` auf HTTPS festlegen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Eingangs-Konvertierung bzw. Ablehnung; konvertierte URL durchgängig verwenden (Klon-Arg + `ConfigureGitCredentialsAsync`-Parameter); Token-Fail-Fast greift dadurch für alle URLs.
4. **`GitHubPlugin.ConfigureGitCredentialsAsync` bereinigen**
   - Voraussetzungen: Keine.
   - Beschreibung: `core.sshCommand`-Config-Block entfernen; `user.name`/`user.email`/`set-url` unverändert.
5. **`GitHubPlugin.NormalizeRemoteUrlAsync` um SSH→HTTPS erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Token-Stripping → `ToHttpsRepositoryUrl` → `set-url` bei Differenz.
6. **`RemoveInsecureSshCommandAsync` anlegen und verdrahten (GitHub)**
   - Voraussetzungen: Keine.
   - Beschreibung: Methode implementieren; Aufruf in `PushBranchAsync`/`PullAsync` nach `NormalizeRemoteUrlAsync` sowie in `CheckoutPullRequestSourceAsync` (Schritt 8).
7. **`GitHubPlugin.GetRemoteBranchesAsync`/`GetDefaultBranchAsync` konvertieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `ToHttpsRepositoryUrl(repositoryUrl) ?? repositoryUrl` vor `ls-remote`.
8. **`GitHubPlugin.CheckoutPullRequestSourceAsync` anpassen**
   - Voraussetzungen: Schritte 1, 5, 6.
   - Beschreibung: `NormalizeRemoteUrlAsync` + `RemoveInsecureSshCommandAsync` vorweg; `SourceRepositoryUrl` via `with` normalisieren; Delegation an `CheckoutPullRequestSourceCoreAsync` unverändert.
9. **`BitbucketPlugin`: netrc-Testseam einführen**
   - Voraussetzungen: Keine.
   - Beschreibung: Optionaler Konstruktorparameter `netrcPath`; private `ResolveNetrcPath()` (Parameter → `SOFTWARESCHMIEDE_NETRC_PATH` → bisheriger Profilpfad); `GetGitEnvironment` nutzt `ResolveNetrcPath()` statt Inline-Ermittlung. **Muss vor allen credential-basierten Bitbucket-Tests liegen** (Schritt 15).
10. **`BitbucketPlugin.GetGitEnvironment` bereinigen**
    - Voraussetzungen: Schritt 9 (für die Pfad-Ermittlung; die `GIT_SSH_COMMAND`-Entfernung selbst ist voraussetzungslos).
    - Beschreibung: `GIT_SSH_COMMAND`-Belegung (Z. 172) entfernen.
11. **`BitbucketPlugin.CloneRepositoryAsync` auf HTTPS festlegen**
    - Voraussetzungen: Schritt 1.
    - Beschreibung: `ToHttpsRepositoryUrl`-Konvertierung bzw. Ablehnung nach dem Credential-Fail-Fast; konvertierte URL in `ResolveGitCloneUrl` (SelfHosted) und `BuildAuthenticatedCloneUrl` verwenden.
12. **`BitbucketPlugin.NormalizeRemoteUrlAsync` anlegen und in `PullAsync`/`PushBranchAsync` verdrahten**
    - Voraussetzungen: Schritt 1.
    - Beschreibung: `remote.origin.url` lesen → SSH-Form → HTTPS-Ziel (mit `BuildAuthenticatedCloneUrl`-Einbettung bei Credentials) → `set-url` bei Differenz; Fehler `LogWarning`; Aufruf vor dem `pull`/`push`.
13. **`BitbucketPlugin.GetRemoteBranchesAsync`/`GetDefaultBranchAsync`/`GetRepositoryStructureLoadResultAsync` konvertieren**
    - Voraussetzungen: Schritt 1.
    - Beschreibung: `ToHttpsRepositoryUrl(repositoryUrl) ?? repositoryUrl` vor `ls-remote` bzw. am Methodenanfang der Structure-Methode.
14. **`BitbucketPlugin.CheckoutPullRequestSourceAsync`-Override anlegen**
    - Voraussetzungen: Schritte 1, 12.
    - Beschreibung: `NormalizeRemoteUrlAsync` vorweg; `SourceRepositoryUrl` via `with` normalisieren; `CheckoutPullRequestSourceCoreAsync` mit `GetGitEnvironment()`.
15. **Bestehende Tests anpassen (inkl. Bitbucket-Fixture-Verdrahtung)**
    - Voraussetzungen: Schritte 1-14 (Fixture-Umstellung setzt Schritt 9 voraus).
    - Beschreibung: `CloneRepositoryAsync_OhneToken_NurTerminalPromptUndSshInEnv` neu schreiben; Env-Matcher um `!env.ContainsKey("GIT_SSH_COMMAND")` schärfen; Push-/Pull-/Checkout-Tests (GitHub) um `config --get core.sshCommand`-Setup **und Aufruf-Asserts** ergänzen (`Times.Once` auf `config remote.origin.url`/`--get core.sshCommand` im Checkout-Pfad — ohne Assert bleibt die Hygiene-Verdrahtung unbeweisbar); beide Bitbucket-Testfixtures (`BitbucketPluginTests`, `BitbucketPluginTests_GetRepositoryStructureAsync`) instanziieren `_sut` mit eindeutigem Temp-`netrcPath` pro Testinstanz.
16. **Neue Tests schreiben**
    - Voraussetzungen: Schritte 1-14 (Bitbucket-Tests zwingend nach Schritt 9).
    - Beschreibung: Konvertierungs-, Ablehnungs-, Normalisierungs- und Legacy-Unset-Tests für beide Plugins gemäß Testabschnitt.
17. **Build, Formatierung und reguläre Testlane**
    - Voraussetzungen: Schritte 1-16.
    - Beschreibung: `dotnet build Softwareschmiede.slnx -c Debug`, `dotnet format Softwareschmiede.slnx --verify-no-changes`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` und entsprechend `src/Softwareschmiede.IntegrationTests` — synchron, nicht im Hintergrund.
18. **Dokumentation aktualisieren** (separater Lifecycle-Schritt)
    - Voraussetzungen: Schritte 1-17.
    - Beschreibung: `docs/help/plugins/ablauf-technisch.md` (Z. 88, 90, 92) und `docs/help/plugins/business-rules.md` (Z. 160) auf den HTTPS-only-Zustand bringen — inkl. der Bitbucket-`GetGitEnvironment`-Beschreibung (kein `GIT_SSH_COMMAND`, `_netrc`-Pflege bleibt).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CloneRepositoryAsync_SshUrl_WirdZuHttpsGeklont` | `GitHubPluginTests` | `git@github.com:owner/repo.git` + Token → `git clone https://github.com/owner/repo.git`; Env ohne `GIT_SSH_COMMAND`, mit `GH_TOKEN` + extraheader |
| `CloneRepositoryAsync_SshSchemeUrl_WirdZuHttpsGeklont` | `GitHubPluginTests` | `ssh://git@github.com/owner/repo.git` + Token → Klon-Arg `https://github.com/owner/repo.git` |
| `CloneRepositoryAsync_SshUrlOhneToken_WirftInvalidOperationException` | `GitHubPluginTests` | `git@…` ohne Token → `InvalidOperationException` (Token-Hinweis), `RunAsync` `Times.Never` — ersetzt den bisherigen OhneToken-Test |
| `CloneRepositoryAsync_NichtHttpsSchema_WirdAbgelehnt` | `GitHubPluginTests` | Als `[Theory]` mit `[InlineData]`: `http://github.com/owner/repo`, `git://github.com/owner/repo` und `ssh://git@github.com:2222/owner/repo.git` (Fremd-Port — schließt die GitHub-seitige Fremd-Port-Ablehnung explizit ab) → `InvalidOperationException`, `RunAsync` `Times.Never` |
| `CloneRepositoryAsync_KonfiguriertKeinSshCommand` | `GitHubPluginTests` | Nach erfolgreichem Klon: kein `RunAsync`-Aufruf mit `core.sshCommand` in den Args (`Times.Never`); `user.name`/`user.email`/`set-url` weiterhin vorhanden |
| `PushBranchAsync_SshRemoteUrl_WirdZuHttpsNormalisiert` | `GitHubPluginTests` | `config remote.origin.url` liefert `git@github.com:owner/repo.git` → `remote set-url origin https://github.com/owner/repo.git` vor dem Push |
| `PullAsync_SshRemoteUrl_WirdZuHttpsNormalisiert` | `GitHubPluginTests` | analog für Pull |
| `PushBranchAsync_LegacySshCommand_WirdEntfernt` | `GitHubPluginTests` | `config --get core.sshCommand` liefert `ssh -o StrictHostKeyChecking=no …` → `config --unset core.sshCommand` `Times.Once` |
| `PushBranchAsync_FremdesSshCommand_BleibtErhalten` | `GitHubPluginTests` | `core.sshCommand` = `ssh -i /key` (ohne Flag) → `--unset` `Times.Never`, Push läuft normal |
| `PullAsync_OhneSshCommand_KeinUnset` | `GitHubPluginTests` | `config --get` schlägt fehl (Schlüssel nicht gesetzt) → `--unset` `Times.Never`, Pull erfolgt trotzdem |
| `PullAsync_SshCommandReadFehler_NurWarnung` | `GitHubPluginTests` | `config --get` schlägt fehl/Nicht-git-Pfad → kein Wurf, Pull läuft weiter (Fehlertoleranz) |
| `CheckoutPullRequestSourceAsync_SshForkUrl_WirdZuHttpsGefetcht` | `GitHubPluginTests` | Spec mit `SourceRepositoryUrl = "git@github.com:fork/repo.git"` + abweichender `SourceRepositoryId` → `git fetch --no-tags https://github.com/fork/repo.git …` |
| `GetRemoteBranchesAsync_SshUrl_NutztHttpsRemote` | `GitHubPluginTests` | `ls-remote --heads https://github.com/owner/repo.git` |
| `GetDefaultBranchAsync_SshUrl_NutztHttpsRemote` | `GitHubPluginTests` | `ls-remote --symref https://github.com/owner/repo.git HEAD` |
| `CheckoutPullRequestSourceAsync_SshOriginUrl_WirdVorFetchNormalisiert` | `GitHubPluginTests` | Same-Repo-Spec (`SourceRepositoryId == TargetRepositoryId` ⇒ `fetch` geht gegen `origin`); `config remote.origin.url` liefert `git@github.com:owner/repo.git` → `remote set-url origin https://github.com/owner/repo.git` `Times.Once` **vor** `fetch --no-tags origin …` — Reihenfolge via Aufrufprotokoll-Hilfsliste |
| `CheckoutPullRequestSourceAsync_LegacySshCommand_WirdEntfernt` | `GitHubPluginTests` | `config --get core.sshCommand` liefert `ssh -o StrictHostKeyChecking=no …` → `config --unset core.sshCommand` `Times.Once` vor dem `fetch` — belegt die Legacy-Bereinigung im Checkout-Pfad (Helpers sind fehlschlagtolerant, ohne Assert unbeweisbar) |
| `PushBranchAsync_NichtKonvertierbareRemoteUrl_KeinSetUrl` | `GitHubPluginTests` | `config remote.origin.url` liefert nicht konvertierbare URL (z. B. `ssh://git@github.com:2222/owner/repo.git` Fremd-Port oder `ssh://user:pass@github.com/owner/repo.git` Fremd-Userinfo) → `remote set-url` `Times.Never`; Push läuft in den normalen git-Pfad |
| Aufrufprotokoll-Hilfsliste | `GitHubPluginTests`, `BitbucketPluginTests` | Geteilte `List<string>` in den `RunAsync`-`Callback`s befüllen (normalisierte Args), um die Reihenfolge `set-url`/`--unset` < `fetch`/`push` per Index-Vergleich zu beweisen — Moq-`Verify` allein assertiert keine Aufrufreihenfolge |
| `CloneRepositoryAsync_SshUrl_WirdZuHttpsGeklont` | `BitbucketPluginTests` | `git@bitbucket.org:ws/repo.git` + Credentials → Klon-Arg `https://<user>:<appPassword>@bitbucket.org/ws/repo.git` (URL-escaped via `BuildAuthenticatedCloneUrl`-Semantik); Env ohne `GIT_SSH_COMMAND` |
| `CloneRepositoryAsync_SshSchemeUrl_WirdZuHttpsGeklont` | `BitbucketPluginTests` | `ssh://git@bitbucket.org/ws/repo.git` + Credentials → Klon-Arg mit `https://`-Host/Pfad und eingebetteten Credentials |
| `CloneRepositoryAsync_SelfHostedSshUrl_WirdZuScmHttpsUrlGeklont` | `BitbucketPluginTests` | HostingMode `SelfHosted` + `git@bitbucket.example.com:projects/KEY/repos/slug` → Klon-Arg `https://<user>:<pw>@bitbucket.example.com/scm/KEY/slug.git` (Zusammenspiel `ToHttpsRepositoryUrl` + `ResolveGitCloneUrl`) |
| `CloneRepositoryAsync_NichtHttpsSchema_WirdAbgelehnt` | `BitbucketPluginTests` | `http://bitbucket.org/ws/repo` → `InvalidOperationException`, `RunAsync` `Times.Never` |
| `CloneRepositoryAsync_SshUrlMitFremdPort_WirdAbgelehnt` | `BitbucketPluginTests` | `ssh://git@bitbucket.example.com:7999/scm/KEY/slug.git` (SelfHosted) → `InvalidOperationException`, `RunAsync` `Times.Never` (Scope-Ausschluss 5) |
| `CloneRepositoryAsync_OhneCredentials_WirftInvalidOperationException` | `BitbucketPluginTests` | Fehlender `user`/`appPassword` → bestehender Fail-Fast vor jedem CLI-Aufruf |
| `PushBranchAsync_SshRemoteUrl_WirdZuHttpsNormalisiert` | `BitbucketPluginTests` | `config remote.origin.url` liefert `git@bitbucket.org:ws/repo.git` → `remote set-url origin https://<user>:<pw>@bitbucket.org/ws/repo.git` (Credentials eingebettet) vor dem Push |
| `PullAsync_SshRemoteUrl_WirdZuHttpsNormalisiert` | `BitbucketPluginTests` | analog für Pull |
| `PushBranchAsync_HttpsRemoteUrl_KeinSetUrl` | `BitbucketPluginTests` | Remote bereits HTTPS (inkl. eingebetteter Credentials) → `set-url` `Times.Never`, Push unverändert |
| `PullAsync_RemoteUrlReadFehler_LaeuftWeiter` | `BitbucketPluginTests` | `config remote.origin.url` schlägt fehl/nicht aufgesetzt → kein Wurf, Pull läuft normal (Fehlertoleranz) |
| `GetRemoteBranchesAsync_SshUrl_NutztHttpsRemote` | `BitbucketPluginTests` | `ls-remote --heads https://bitbucket.org/ws/repo.git` |
| `GetDefaultBranchAsync_SshUrl_NutztHttpsRemote` | `BitbucketPluginTests` | `ls-remote --symref https://bitbucket.org/ws/repo.git HEAD` |
| `CheckoutPullRequestSourceAsync_SshForkUrl_WirdZuHttpsGefetcht` | `BitbucketPluginTests` | Spec mit `SourceRepositoryUrl = "git@bitbucket.org:fork/repo.git"` + abweichender `SourceRepositoryId` → `git fetch --no-tags https://bitbucket.org/fork/repo.git …` |
| `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` | `BitbucketPluginTests` | `fetch`/`checkout` erhalten Env mit `GIT_TERMINAL_PROMPT=0` und **ohne** `GIT_SSH_COMMAND` (bisher `null`-Env); zusätzlich `Times.Once`-Assert auf den `config remote.origin.url`-Vorab-Aufruf (`NormalizeRemoteUrlAsync`-Verdrahtung im Checkout-Pfad) |
| `CheckoutPullRequestSourceAsync_SshOriginUrl_WirdVorFetchNormalisiert` | `BitbucketPluginTests` | Same-Repo-Spec (`SourceRepositoryId == TargetRepositoryId` ⇒ `fetch` gegen `origin`); `config remote.origin.url` liefert `git@bitbucket.org:ws/repo.git` + Credentials → `remote set-url origin https://<user>:<pw>@bitbucket.org/ws/repo.git` `Times.Once` **vor** `fetch --no-tags origin …` — Reihenfolge via Aufrufprotokoll-Hilfsliste |
| `PushBranchAsync_SshRemoteUrlOhneCredentials_WirdZuNacktemHttpsNormalisiert` | `BitbucketPluginTests` | `config remote.origin.url` liefert `git@bitbucket.org:ws/repo.git`, **keine** `Username`/`AppPassword` gesetzt → `remote set-url origin https://bitbucket.org/ws/repo.git` (nackte HTTPS-URL ohne Credential-Einbettung; `_netrc`-Fallback-Auth greift ggf. zur Laufzeit) |
| `PushBranchAsync_NichtKonvertierbareRemoteUrl_KeinSetUrl` | `BitbucketPluginTests` | `config remote.origin.url` liefert nicht konvertierbare URL (z. B. `ssh://git@bitbucket.example.com:7999/scm/KEY/slug.git` Fremd-Port oder Fremd-Userinfo wie `ssh://user:pass@host/…`) → `remote set-url` `Times.Never`; Push läuft in den normalen git-Pfad |
| `GetRepositoryStructureLoadResultAsync_SshUrl_LoestRepositoryIdAuf` | `BitbucketPluginTests_GetRepositoryStructureAsync` | `git@bitbucket.org:ws/repo` → `TryExtractRepositoryId` liefert `ws/repo`, API-Aufruf erfolgt (statt „konnte nicht ermittelt werden") |
| `NetrcPfad_AusKonstruktorParameter_WirdGenutzt` | `BitbucketPluginTests` | Fixture stellt den Temp-`netrcPath` als Feld bereit; Credentials gesetzt → nach einem `GetGitEnvironment`-Pfad (z. B. `PullAsync`) enthält die Temp-Datei die `machine bitbucket.org`-Stanza; der reale Profilpfad (`%USERPROFILE%\_netrc`) bleibt nachweislich unangetastet |
| Env-Negativ-Asserts `!env.ContainsKey("GIT_SSH_COMMAND")` | `BitbucketPluginTests` | In allen neuen Env-erfassenden Tests (Clone/Push/Pull/ls-remote/Checkout) als Matcher-Bestandteil |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `CloneRepositoryAsync_OhneToken_NurTerminalPromptUndSshInEnv` (`GitHubPluginTests.cs:1761-1785`) | Assertiert `env.ContainsKey("GIT_SSH_COMMAND")` und tokenlosen SSH-Klon — beides entfällt; wird zu `…_SshUrlOhneToken_WirftInvalidOperationException` umgeschrieben |
| `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled` | Env-Matcher um `!env.ContainsKey("GIT_SSH_COMMAND")` verschärfen |
| `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush`, `…_ShouldNotEmbedToken_InRemoteUrl`, `…_ShouldNotSetRemoteUrl_WhenTokenIsMissing`, `…_ShouldThrow_WhenPushFails` | Neuer `config --get core.sshCommand`-Aufruf: explizites Setup ergänzen (Fehlschlag = nicht gesetzt) + Env-Matcher um `!env.ContainsKey("GIT_SSH_COMMAND")` schärfen |
| `PullAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm`, `…_ShouldRunGitPull_WithConfiguredCredentials`, `…_ShouldNotRequireEmbeddedToken`, `…_ShouldThrow_WhenGitPullFails` | analog zu Push: Setup für `config --get core.sshCommand` + Env-Schärfung |
| `GetDefaultBranchAsync_Env_EnthaeltGitAuth` | Env-Matcher um `!env.ContainsKey("GIT_SSH_COMMAND")` schärfen |
| `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` (`GitHubPluginTests`) | Neue Vorab-Aufrufe (`config remote.origin.url`, `config --get core.sshCommand`): Setups ergänzen **und Aufruf-Asserts ergänzen** (`Times.Once` auf beide Vorab-Aufrufe) — ohne Assert wäre die Verdrahtung der Hygiene im Checkout-Pfad unbewiesen, da die Helpers fehlschlagtolerant sind; Env-Matcher um `!env.ContainsKey("GIT_SSH_COMMAND")` schärfen |
| `GetAgentEnvironmentAsync_WithToken_ProvidesGhAndGitAuth`, `…_WithoutToken_OnlyDisablesGitPrompt`, `…_SeparatesKonto_UsesAgentToken` | `!env.ContainsKey("GIT_SSH_COMMAND")`-Assert ergänzen (regressionssicher) |
| `GetRepositoryStructureAsync_ShouldResolveRepositoryId_FromSshUrl` (`GitHubPluginTests_GetRepositoryStructureAsync`) | **Unverändert** — SSH-Toleranz der ID-Extraktion bleibt gewollt (Designentscheidung) |
| `GitOrchestrationServiceTests.PullRequestErstellenAsync_ShouldResolveRepositoryId_FromSshRepositoryUrl` | **Unverändert** — persistierte SSH-`RepositoryUrl` bleibt gültige Eingabe |
| `BitbucketPluginTests`-Fixture (Konstruktor) | `_sut` mit Temp-`netrcPath` instanziieren (eindeutige Temp-Datei pro Testinstanz, z. B. `Path.Combine(Path.GetTempPath(), …)`): Alle neuen credential-basierten Tests dieser Klasse (Klon/Push/Pull/Checkout) würden sonst via `GetGitEnvironment` → `UpdateNetrcEntry` die echte `%USERPROFILE%\_netrc` mit Test-Credentials überschreiben. Fachlich unverändert bleiben: `GetDefaultBranchAsync_ShouldTrimCarriageReturn_WhenOutputHasCRLF` (keine Credentials gesetzt → kein netrc-Zugriff) und `ResolveGitCloneUrl_ShouldReturnCorrectCloneUrl` (arbeitet auf bereits-HTTPS-Eingaben; Konvertierung erfolgt vorher in `CloneRepositoryAsync`) |
| `BitbucketPluginTests_GetRepositoryStructureAsync`-Fixture (Konstruktor) + `SetupDefaultBranch` | `_sut` mit Temp-`netrcPath` instanziieren: Der Fixture-Ctor setzt `GetCredential(It.IsAny<string>()) → "test"` — damit sind `Username`/`AppPassword` nicht-leer und **jeder** `SetupDefaultBranch`-Test schreibt heute bereits über den internen `GetDefaultBranchAsync`-Aufruf (`BitBucketPlugin.cs:1097` → `GetGitEnvironment` → `UpdateNetrcEntry`, `BitBucketPlugin.cs:1487-1521`) eine `machine bitbucket.org`-Stanza mit Test-Credentials in die echte Benutzer-`_netrc`. Die Seam beendet diesen bestehenden Seiteneffekt; fachlich bleibt der generische `args.Contains("ls-remote")`-Mock unverändert (gilt weiterhin für die konvertierte URL) |
| `GitPluginBaseTests` | **Unverändert** — Basisklassen-Methoden inkl. `environmentVariables: null`-Default des virtuellen `CheckoutPullRequestSourceAsync` bleiben bestehen; `ToHttpsRepositoryUrl` ist `protected` und wird über die Plugin-Public-APIs geprüft |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich — Begründung: Die Anforderung betrifft ausschließlich den internen git-Transport des `GitHubPlugin` und `BitbucketPlugin` (Prozessumgebung, Remote-URL-Form und lokale `.git/config`), keinen über UI oder Nutzeraktion erreichbaren Ablauf; die einzige E2E-Nähe (`ScmEventsE2ETests`/`ScmE2EGitRunner`) verwirft `environmentVariables` vollständig und ersetzt Remotes durch lokale Pfade — der geänderte Effekt ist auf E2E-Ebene nicht beobachtbar. Die beobachtbare Seam ist die `ICliRunner`-Grenze; dort decken die Unit-Tests Env-Inhalte, CLI-Argumente und Config-Aufrufe beider Plugins exakt ab. Zusätzlich ist eine reale manuelle Abnahme sinnvoll (Akzeptanzszenarien 1/3 und 8/9: echter Klon+Push gegen GitHub- und Bitbucket-Test-Repositories mit PAT bzw. App-Password; `.git/config` der Klons auf fehlendes `core.sshCommand` bzw. HTTPS-Remote prüfen) — als `Bestanden (manuell)` oder begründetes `Verzichtet` zu dokumentieren.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Klon mit SSH-`RepositoryUrl` (GitHub) | Aufgabenstart auf Projekt mit verknüpftem Repo (`EntwicklungsprozessService.PrepareCloneDirectoryAsync` → `CloneRepositoryAsync`) | `GitRepository.RepositoryUrl` = `git@github.com:owner/repo.git`; GitHub-PAT im Credential Store; lokale Arbeitskopie existiert noch nicht | Plugin konvertiert URL → `https://github.com/owner/repo.git` → `git clone` mit Token-Env → `ConfigureGitCredentialsAsync` | Klon liegt lokal; `remote.origin.url` = HTTPS; `.git/config` enthält **kein** `core.sshCommand`; keine `GIT_SSH_COMMAND`-Belegung im Prozess | `CloneRepositoryAsync_SshUrl_WirdZuHttpsGeklont` + `…_KonfiguriertKeinSshCommand`; reale Abnahme optional |
| 2 | Happy Path: Bestands-Arbeitskopie wird hygienisch bereinigt (GitHub) | Nächster Push/Pull oder PR-Checkout auf einer von einer Altversion geklonten Kopie | `.git/config` trägt `core.sshCommand` mit `StrictHostKeyChecking=no`; `remote.origin.url` = `git@github.com:owner/repo.git`; PAT vorhanden | `PushBranchAsync` → `NormalizeRemoteUrlAsync` (set-url auf HTTPS) → `RemoveInsecureSshCommandAsync` (`--unset`) → `git push` | Push erfolgreich; `.git/config` frei von `core.sshCommand`; `remote.origin.url` = HTTPS | `PushBranchAsync_SshRemoteUrl_WirdZuHttpsNormalisiert` + `…_LegacySshCommand_WirdEntfernt` |
| 3 | Fehlerfall: Klon ohne Token (GitHub, SSH-URL) | Aufgabenstart ohne konfigurierten PAT | SSH-`RepositoryUrl`; Credential Store leer | `CloneRepositoryAsync` → Konvertierung → `IsHttpsRepositoryUrl` → Fail-Fast | `InvalidOperationException` mit Token-Hinweis; **kein** git-Prozess gestartet; Aufgabe bricht mit verständlicher Meldung ab | `CloneRepositoryAsync_SshUrlOhneToken_WirftInvalidOperationException` |
| 4 | Fehlerfall: nicht konvertierbare URL (beide Plugins) | Klon mit `http://`-URL oder ungültiger URL | `RepositoryUrl` im ungültigen Schema | `ToHttpsRepositoryUrl` → `null` → Ablehnung | `InvalidOperationException` mit verständlicher Meldung; kein git-Aufruf | `CloneRepositoryAsync_NichtHttpsSchema_WirdAbgelehnt` (je Plugin) |
| 5 | Edge Case: Fork-PR-Checkout mit `ssh_url` (GitHub) | Review-Checkout eines PRs aus einem Fork (`CheckoutReviewSourceAsync`) | `PullRequestReferenz.SourceRepositoryUrl` = `git@github.com:fork/repo.git` (aus `clone_url ?? ssh_url`); betreute Kopie evtl. mit Legacy-Config | Spec-Normalisierung → `git fetch --no-tags https://github.com/fork/repo.git <ref>` → `checkout -B` | PR-Quell-Branch ausgecheckt; kein SSH-Aufruf | `CheckoutPullRequestSourceAsync_SshForkUrl_WirdZuHttpsGefetcht` |
| 5a | Happy Path: Same-Repo-PR-Checkout auf Alt-Kopie (GitHub) | Review-Checkout eines PRs aus demselben Repo (`SourceRepositoryId == TargetRepositoryId` ⇒ `fetch` geht gegen `origin`) | `.git/config` trägt `core.sshCommand` mit `StrictHostKeyChecking=no` und `remote.origin.url` = `git@github.com:owner/repo.git` (von Altversion geklont) | `CheckoutPullRequestSourceAsync` → `NormalizeRemoteUrlAsync` (`set-url` auf HTTPS) + `RemoveInsecureSshCommandAsync` (`--unset`) **vor** `git fetch --no-tags origin <ref>` → `checkout -B` | Checkout erfolgreich; `remote.origin.url` = HTTPS, `core.sshCommand` entfernt — ohne Hygiene würde der `origin`-Fetch nicht-interaktiv scheitern | `CheckoutPullRequestSourceAsync_SshOriginUrl_WirdVorFetchNormalisiert` + `…_LegacySshCommand_WirdEntfernt` |
| 6 | Edge Case: Nutzer-eigenes `core.sshCommand` (GitHub) | Push/Pull auf Kopie mit fremd gesetztem `core.sshCommand` (ohne `StrictHostKeyChecking=no`) | `.git/config` trägt `core.sshCommand = ssh -i /key` | `RemoveInsecureSshCommandAsync` prüft Wert → kein Unset | Fremder Eintrag bleibt erhalten; Operation erfolgreich | `PushBranchAsync_FremdesSshCommand_BleibtErhalten` |
| 7 | Negativ/Regression: Agenten-Umgebung (GitHub) | Agenten-CLI-Start in betreuter Kopie (`GetAgentEnvironmentAsync`) | Beliebig | Env wird gebaut | Env enthält `GIT_TERMINAL_PROMPT`, `GH_TOKEN`, `GIT_CONFIG_*` — nachweislich **kein** `GIT_SSH_COMMAND` | `GetAgentEnvironmentAsync_*` mit neuem Negativ-Assert |
| 8 | Happy Path: Klon mit SSH-`RepositoryUrl` (Bitbucket Cloud) | Aufgabenstart auf Projekt mit Bitbucket-verknüpftem Repo | `GitRepository.RepositoryUrl` = `git@bitbucket.org:ws/repo.git`; `Username`/`AppPassword` im Credential Store; Kopie existiert noch nicht | `CloneRepositoryAsync` → Fail-Fast-Check → `ToHttpsRepositoryUrl` → `BuildAuthenticatedCloneUrl` (Credentials eingebettet) → `git clone` | Klon liegt lokal; `remote.origin.url` = `https://user:pass@bitbucket.org/ws/repo.git`; kein `GIT_SSH_COMMAND` im Prozess; `_netrc` mit `machine bitbucket.org`-Stanza gepflegt | `BitbucketPluginTests.CloneRepositoryAsync_SshUrl_WirdZuHttpsGeklont`; reale Abnahme optional |
| 9 | Happy Path: Bestands-Arbeitskopie wird normalisiert (Bitbucket) | Nächster Pull/Push auf einer Kopie mit SSH-`origin` | `remote.origin.url` = `git@bitbucket.org:ws/repo.git`; Credentials vorhanden | `PullAsync` → `NormalizeRemoteUrlAsync` (set-url auf HTTPS mit eingebetteten Credentials) → `git pull` | Pull erfolgreich; `remote.origin.url` = HTTPS | `PullAsync_SshRemoteUrl_WirdZuHttpsNormalisiert` |
| 10 | Edge Case: PR-Checkout mit SSH-Clone-Link bzw. SSH-`origin` (Bitbucket) | Review-Checkout eines PRs, dessen Quell-Repo keinen `https`-Clone-Link liefert (`ReadBitbucketCloneLink`-Fallback), oder Same-Repo-PR auf Alt-Kopie | `PullRequestReferenz.SourceRepositoryUrl` = `git@bitbucket.org:fork/repo.git` (bzw. `ssh://…`); bei Same-Repo-PR `remote.origin.url` = `git@bitbucket.org:ws/repo.git` | `CheckoutPullRequestSourceAsync` → `NormalizeRemoteUrlAsync` (`set-url` auf HTTPS mit eingebetteten Credentials vor dem `origin`-Fetch) + Spec-Normalisierung → `git fetch --no-tags … <ref>` mit Env (`GIT_TERMINAL_PROMPT=0`, netrc-Auth) | PR-Quell-Branch ausgecheckt; kein SSH-Aufruf; keine Prompt-Blockade | `CheckoutPullRequestSourceAsync_SshForkUrl_WirdZuHttpsGefetcht` + `…_UebergibtGitEnvironment` + `…_SshOriginUrl_WirdVorFetchNormalisiert` |
| 10a | Edge Case: Bestands-Remote nicht konvertierbar / ohne Credentials (Bitbucket) | Push/Pull auf Kopie mit `ssh://`-Fremd-Port-Remote bzw. SSH-Remote ohne gesetzte Credentials | `remote.origin.url` = `ssh://git@host:7999/…` (nicht konvertierbar) oder `git@bitbucket.org:ws/repo.git` ohne Credentials | `NormalizeRemoteUrlAsync`: nicht konvertierbar → kein `set-url` (git-Fehlerpfad greift); ohne Credentials → `set-url` auf nackte HTTPS-URL | Kein ungewolltes `set-url`; dokumentiertes Fallback-Verhalten | `PushBranchAsync_NichtKonvertierbareRemoteUrl_KeinSetUrl` + `…_SshRemoteUrlOhneCredentials_WirdZuNacktemHttpsNormalisiert` |
| 11 | Edge Case: SelfHosted-SSH-URL mit Fremd-Port (Bitbucket Server) | Klon auf Projekt mit `RepositoryUrl` = `ssh://git@bitbucket.example.com:7999/scm/KEY/slug.git` | HostingMode `SelfHosted`; `SelfHostedUrl` konfiguriert; Credentials vorhanden | `CloneRepositoryAsync` → `ToHttpsRepositoryUrl` → `null` (Port 7999 ≠ 22/default) → Ablehnung | `InvalidOperationException` mit HTTPS-Hinweis; kein git-Aufruf; Anwender stellt die Verknüpfung auf die HTTPS-URL um | `CloneRepositoryAsync_SshUrlMitFremdPort_WirdAbgelehnt` |
| 12 | Happy Path: SelfHosted-SSH-URL in SCP-Form (Bitbucket Server) | Klon auf Projekt mit `RepositoryUrl` = `git@bitbucket.example.com:projects/KEY/repos/slug` | HostingMode `SelfHosted`; Credentials vorhanden | `ToHttpsRepositoryUrl` → `https://bitbucket.example.com/projects/KEY/repos/slug` → `ResolveGitCloneUrl` → `https://bitbucket.example.com/scm/KEY/slug.git` → Credentials-Einbettung → `git clone` | Klon erfolgreich über die SCM-HTTPS-URL | `CloneRepositoryAsync_SelfHostedSshUrl_WirdZuScmHttpsUrlGeklont` |

## Offene Punkte

Keine.
