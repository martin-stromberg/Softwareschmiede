# Umsetzungsplan: Klartext-Token in `_netrc` entfernen (Issue #376)

## Übersicht

`GitHubPlugin` persistiert den GitHub-PAT heute nach jedem `git clone` im Klartext nach `%USERPROFILE%\_netrc`/`~/.netrc` (`ConfigureGitCredentialsAsync`, `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, Z. 334–355) und verweist zusätzlich per `NETRC`-Umgebungsvariable darauf (`GetGitEnvironment`, Z. 196–199). Die Umsetzung entfernt den Schreibpfad komplett, ersetzt die Host-git-Authentifizierung durch nicht-persistierende `GIT_CONFIG_*`/`http.extraheader`-Prozessumgebung (bereits bewährtes Muster aus `GetAgentEnvironmentAsync`, Z. 172–175) und bereinigt einmalig pro Plugin-Instanziierung die von Altversionen geschriebenen `machine github.com`/`machine api.github.com`-Stanzas in einer existierenden `_netrc`. Betroffen ist ausschließlich `GitHubPlugin.cs` (keine neuen Interfaces, Entities, Migrationen oder UI).

## Scope-Klärung

|| Feld | Festlegung |
||------|------------|
|| Verarbeitungseinheit | Credential-Konfiguration für Host-git-Operationen gegen github.com: `GetGitEnvironment` (Prozessumgebung) + `ConfigureGitCredentialsAsync` (Repository-Nachkonfiguration) + einmalige Legacy-Bereinigung der `_netrc` |
|| Auslösende Ereignisse | (a) Plugin-Instanziierung durch `PluginManager.DiscoverPlugins`/`ActivatorUtilities` → einmalige `_netrc`-Bereinigung; (b) jeder Host-git-Aufruf über `GetGitEnvironment`: `git clone` (`CloneRepositoryAsync`, Z. 884), `git push` (`PushBranchAsync`, Z. 917), `git pull` (`PullAsync`, Z. 941), `git ls-remote` (`GetRemoteBranchesAsync`, Z. 1420; `GetDefaultBranchAsync`, Z. 1476) sowie `git fetch`/`checkout` im PR-Checkout (`CheckoutPullRequestSourceAsync`) |
|| Erwartete Wirkung | Kein Schreiben von GitHub-Tokens nach `_netrc`; existierende GitHub-Stanzas werden entfernt (Fremd-Einträge wie `machine bitbucket.org` bleiben unverändert); clone/push/pull/ls-remote/fetch funktionieren weiterhin nicht-interaktiv via `http.extraheader`; Token erscheint weder in URLs, Logs noch Fehlermeldungen |
|| Umgang mit fehlenden Objekten | Kein Token im `ICredentialStore`: Verhalten wie bisher (Warnung, kein Dateizugriff; HTTPS-Clone wirft bereits vorher). `_netrc` nicht vorhanden: nichts zu tun, **keine** neue Datei anlegen. `_netrc` mit Fremd-Einträgen: nur GitHub-Stanzas entfernen, Rest unverändert zurückschreiben (nur bei tatsächlicher Änderung). `_netrc` bestand nur aus GitHub-Stanzas: Datei löschen. Von Anwendern manuell gepflegte GitHub-Stanzas werden ebenfalls entfernt — plugin-geschrieben und manuell sind nicht unterscheidbar, der Pfad ist für das Plugin obsolet (Kern der geforderten Bereinigung). |

### Scope-Ausschlüsse

Keine. Alle gegenüber einer naheliegenden Nutzererwartung erkennbaren Einschränkungen folgen direkt aus dem Issue-Text bzw. sind unten als Designentscheidungen bzw. Validierungsregeln dokumentiert:

- `BitBucketPlugin` bleibt unverändert (expliziter Scope der Anforderung); die Erhaltung seiner `_netrc`-Einträge ist als Validierungsregel festgeschrieben.
- Die Bereinigung entfernt auch von Anwendern manuell gepflegte `machine github.com`/`api.github.com`-Stanzas — das ist Kern der geforderten Bereinigung, kein Ausschluss (plugin-geschrieben und manuell sind nicht unterscheidbar; der Pfad ist für das Plugin obsolet).
- `GetAgentEnvironmentAsync`, `NormalizeRemoteUrlAsync` und `SanitizeSensitiveOutput` bleiben verhaltensgleich — direkte Umsetzung des Issue-Texts (siehe Designentscheidungen).
- Die ACL-restriktive Schreibvariante ist im Issue nur Fallback und wird zugunsten des Streichens verworfen (Begründung in Designentscheidungen).

## Designentscheidungen

|| Komponente / Bereich | Gewählter Ansatz | Begründung |
||----------------------|-----------------|------------|
|| Ersatz-Auth für Host-git | `http.extraheader` via `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`, `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64("x-access-token:"+token)>` in `GetGitEnvironment` | Exakt das in `GetAgentEnvironmentAsync` (Z. 172–175) bewährte Muster: rein prozessbasiert, keine Persistenz, keine Laufzeitabhängigkeit zur `gh`-CLI (die ein `credential.helper` wie `!gh auth git-credential` hätte). Die BitBucket-Alternative `-c credential.helper= -c http.extraheader=…` als CLI-Args (`GetGitHttpAuthArgs`, Z. 201–213) würde das Token in die Prozess-Kommandozeile heben — die Env-Variante vermeidet das. Ein zusätzliches Leeren von `credential.helper` ist nicht nötig: `extraheader` wird beim initialen Request mitgesendet und der Agent-Pfad funktioniert nachweislich ohne. `GH_TOKEN` bleibt zusätzlich gesetzt (für `gh`-CLI-Nebenpfade und ohne Nebenwirkung). Die im Issue genannte Fallback-Variante „ACL-restriktiv schreiben" wird verworfen: Streichen des Schreibens ist das primäre Ziel, und `extraheader` deckt exakt dieselben Hosts ab, die `_netrc` bisher bediente (github.com, api.github.com) — kein Szenario erfordert Persistenz. |
|| Trigger der Legacy-Bereinigung | Einmalig im `GitHubPlugin`-Konstruktor (`RemoveLegacyNetrcEntries`, komplett in try/catch → `LogWarning`, wirft nie) | Es gibt keinen Plugin-Lifecycle-Hook (`IPlugin` hat keine Initialisierungsmethode); der Konstruktor ist der einzige zuverlässige „Plugin-Start"-Punkt. `PluginManager.DiscoverPlugins` instanziiert Plugins über `ActivatorUtilities.CreateInstance` — damit läuft die Bereinigung auch bei Installationen, die nie wieder klonen (der Fall „nur bei `ConfigureGitCredentialsAsync`" würde Altdateien sonst ewig stehen lassen). Idempotent und ohne Token-Abhängigkeit: `File.Exists`-Kurzschluss macht den Normalfall (keine/keine passende Datei) nahezu frei. |
|| Testbarkeit des `_netrc`-Pfads | Neuer optionaler Konstruktorparameter `string? netrcPath = null` am vollsten Konstruktor; Auflösung über `ResolveNetrcPath()` (Fallback: `SpecialFolder.UserProfile` + `_netrc`/`.netrc`) | Ohne diesen Seam würde die Konstruktor-Bereinigung in Unit-Tests die echte `%USERPROFILE%\_netrc` des Ausführers verändern. `ActivatorUtilities` unterstützt optionale Parameter mit Default — kein neuer Service, kein `InternalsVisibleTo` nötig; die Bereinigung ist über die öffentliche Oberfläche (Konstruktion mit Temp-Pfad) testbar. |
|| `GetDefaultBranchAsync` | Umstellung von `GetGhEnvironment()` auf `GetGitEnvironment()` (Z. 1476) | `git ls-remote --symref` ist eine git-Operation gegen github.com und braucht dieselbe Auth. Bisher griff implizit die Default-`_netrc`-Suche von libcurl; nach dem Entfernen der Datei würden private Repositories ohne Auth scheitern und still auf `"main"` zurückfallen. `GH_TOKEN` allein authentifiziert `git` nicht. |
|| PR-Checkout (`CheckoutPullRequestSourceAsync`) | `GitHubPlugin` überschreibt die Methode und ruft `CheckoutPullRequestSourceCoreAsync(localPath, spec, GetGitEnvironment(), ct)` | Notwendige Regressionsvermeidung, kein optionaler Zusatz: `git fetch` im PR-Checkout läuft bisher ganz ohne Umgebung und hing faktisch an derselben `_netrc`, die jetzt entfernt wird — bei privaten Repositories würde der Checkout sonst scheitern. Die Basis-Signatur akzeptiert `environmentVariables` bereits (Z. 185–222) — minimale, vom Bestand vorgesehene Erweiterung. |
|| Duplikat der extraheader-Belegung | Privater statischer Helfer (z. B. `AddGitHubHttpAuthHeader(IDictionary<string,string?> env, string token)`), genutzt von `GetGitEnvironment` und `GetAgentEnvironmentAsync` | Identische Basis64-/Key-Belegung an zwei Stellen wäre Drift-Risiko; rein interne Extraktion ohne Verhaltensänderung am Agent-Pfad. |

## Programmabläufe

### Einmalige `_netrc`-Bereinigung bei Plugin-Instanziierung

1. `PluginManager.DiscoverPlugins` instanziiert `GitHubPlugin` via `ActivatorUtilities.CreateInstance` (oder Tests/DI erzeugen die Instanz direkt).
2. Der Konstruktor ruft nach Feldinitialisierung `RemoveLegacyNetrcEntries()` auf.
3. `RemoveLegacyNetrcEntries` ermittelt den Pfad via `ResolveNetrcPath()` (`_netrcPath`-Parameter oder `SpecialFolder.UserProfile` + `_netrc`/`.netrc`); existiert die Datei nicht → Return ohne Aktion.
4. Datei einlesen; `RemoveNetrcStanzas(content, hosts: ["github.com","api.github.com"])` parst zeilenweise analog `BitBucketPlugin.UpdateNetrcEntry` (Z. 1498–1515): trifft eine Zeile (getrimmt) auf `machine <host>` (case-insensitiv), werden diese Zeile plus alle Folgezeilen bis zur nächsten `machine `-Zeile verworfen; alle anderen Zeilen bleiben erhalten.
5. Kein Treffer → keine Änderung, kein Zurückschreiben, kein Löschen.
6. Rest-Inhalt leer/whitespace-only → `File.Delete(netrcPath)` + `LogInformation`. Sonst → `File.WriteAllText(netrcPath, rest)` + `LogInformation`.
7. Jegliche Exception (gesperrte/unlesbare Datei, Rechte) → `LogWarning`, Ablauf bricht nicht ab.

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `PluginManager` (indirekt, Instanziierung), `ICredentialStore` (kein Zugriff nötig), `ILogger<GitHubPlugin>`

### Host-git-Operation mit nicht-persistenter Auth (clone/push/pull/ls-remote/fetch)

1. Aufrufer (`CloneRepositoryAsync`, `PushBranchAsync`, `PullAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`, `CheckoutPullRequestSourceAsync`) ruft `GetGitEnvironment(token)` — ohne Token-Argument wird intern `ICredentialStore.GetCredential(GitHubTokenCredentialKey)` gelesen.
2. `GetGitEnvironment` setzt `GIT_TERMINAL_PROMPT=0` und `GIT_SSH_COMMAND` (unverändert). Bei vorhandenem Token: `GH_TOKEN` (unverändert) plus via `AddGitHubHttpAuthHeader` die drei `GIT_CONFIG_*`-Einträge. **`NETRC` entfällt komplett.**
3. Der git-Kindprozess sendet bei HTTPS-Operationen gegen `https://github.com/` den `AUTHORIZATION: basic …`-Header; kein Token in URL, Args oder Dateien.
4. Fehlerbehandlung unverändert: `IsAuthenticationFailure`/`SanitizeSensitiveOutput` mappen Auth-Fehler auf verständliche `InvalidOperationException`s; `git pull`/`push`/`fetch` ohne verwertbare Credentials schlagen nicht-interaktiv fehl (kein Prompt).

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `ICliRunner`, `ICredentialStore`, `GitPluginBase<GitHubPlugin>` (`CheckoutPullRequestSourceCoreAsync`)

### Repository-Nachkonfiguration nach `git clone`

1. `CloneRepositoryAsync` klont mit `GetGitEnvironment(token)`; bei HTTPS ohne Token bereits früh `InvalidOperationException` (Z. 872–876, unverändert).
2. Nach Erfolg `ConfigureGitCredentialsAsync(targetPath, repositoryUrl, ct)`:
   - Token lesen; fehlend → `LogWarning` + Return (unverändert).
   - `git config user.name`/`user.email` mit `GetGitEnvironment(token)` (unverändert).
   - **Der komplette netrc-Schreibblock (Z. 334–355 inkl. `netrcPath`-Ermittlung, `netrcContent`, `File.WriteAllText`, try/catch) entfällt.** Die Bereinigung übernimmt der Konstruktor-Pfad; hier ist nichts mehr zu tun.
   - `git remote set-url origin <repositoryUrl>` bei HTTPS (unverändert), `git config core.sshCommand` (unverändert).
   - Kommentare an Z. 357–358 und Z. 878–879 werden an den `extraheader`-Mechanismus angepasst (aktuell verweisen sie auf `.netrc` bzw. nur `GH_TOKEN`).

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `ICliRunner`

## Neue Klassen

Keine. Alle Erweiterungen sind private Member des bestehenden `GitHubPlugin`.

## Änderungen an bestehenden Klassen

### `GitHubPlugin` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, `public sealed partial class`)

- **Neue Felder:** `_netrcPath` (`string?`) — injizierter Pfad-Override für Tests; `null` → Standard-Pfadauflösung.
- **Geänderter Konstruktor:** Der volle Konstruktor (`ICliRunner, ICredentialStore, ILogger<GitHubPlugin>, IGitHubEventApiClient?, IGitHubEventApiClient?`) erhält einen optionalen Parameter `string? netrcPath = null`; setzt `_netrcPath` und ruft am Ende `RemoveLegacyNetrcEntries()` auf. Die beiden verketteten Überladungen (Z. 113–123) bleiben unverändert.
- **Geänderte Methode `GetGitEnvironment`** (Z. 180–203): `NETRC`-Belegung (Z. 196–199 inkl. Kommentar) entfällt; bei vorhandenem Token zusätzlich `GIT_CONFIG_*`-Einträge via `AddGitHubHttpAuthHeader`. Signatur und `GH_TOKEN`/`GIT_TERMINAL_PROMPT`/`GIT_SSH_COMMAND` bleiben.
- **Geänderte Methode `GetAgentEnvironmentAsync`** (Z. 158–178): extraheader-Belegung (Z. 172–175) durch Aufruf des gemeinsamen Helfers ersetzt — keine Verhaltensänderung.
- **Geänderte Methode `ConfigureGitCredentialsAsync`** (Z. 298–390): netrc-Schreibblock (Z. 334–355) komplett entfernt; Kommentar an Z. 357–358 aktualisiert (Auth nun über `http.extraheader`, nicht mehr `.netrc`). Rest unverändert.
- **Geänderte Methode `GetDefaultBranchAsync`** (Z. 1468–1495): `GetGhEnvironment()` → `GetGitEnvironment()` (Z. 1476), damit `ls-remote --symref` gegen private Repositories authentifiziert bleibt.
- **Neue Methode `CheckoutPullRequestSourceAsync`** (`public override`): delegiert an `CheckoutPullRequestSourceCoreAsync(localPath, checkoutSpec, GetGitEnvironment(), ct)`.
- **Neue private Methoden:**
  - `ResolveNetrcPath()` → `string`: `_netrcPath` oder `Path.Combine(SpecialFolder.UserProfile, _netrc/.netrc)`.
  - `RemoveLegacyNetrcEntries()` → `void`: orchestriert Lesen → Stanzas entfernen → bedingtes Schreiben/Löschen; komplett try/catch → `LogWarning`.
  - `RemoveNetrcStanzas(string content, IReadOnlyCollection<string> hosts)` → `string?` (`private static`): reine Parsefunktion; liefert `null` bei keinem Treffer, sonst den bereinigten Inhalt. Referenz: `BitBucketPlugin.UpdateNetrcEntry` (Z. 1487–1521).
  - `AddGitHubHttpAuthHeader(IDictionary<string,string?> env, string token)` (`private static`): setzt `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`, `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64("x-access-token:"+token)>`; gemeinsam von `GetGitEnvironment` und `GetAgentEnvironmentAsync` genutzt.
- **Kommentar-Updates:** Z. 878–879 (Clone-Kommentar: Auth über `extraheader`, nicht nur `GH_TOKEN`).

## Datenbankmigrationen

Keine.

## Validierungsregeln

|| Feld / Objekt | Regel | Fehlerfall |
||---------------|-------|------------|
|| `_netrc`-Inhalt | Nur exakte `machine github.com`/`machine api.github.com`-Stanzas (case-insensitiv, getrimmt) entfernen; Folgezeilen bis zur nächsten `machine `-Zeile gehören zur Stanza | Fremd-Einträge (z. B. `machine bitbucket.org`, beliebige andere Hosts) dürfen weder verändert noch gelöscht werden |
|| `_netrc`-Datei | Datei nur bei tatsächlicher Änderung zurückschreiben bzw. löschen; niemals neu anlegen | Eine nicht existierende Datei darf nach der Bereinigung nicht existieren |
|| `_netrc`-Restinhalt | Bestand die Datei ausschließlich aus den entfernten GitHub-Stanzas (Rest leer/whitespace-only) → Datei löschen | Fremd-Reste müssen erhalten bleiben — kein Löschen |
|| Bereinigungs-Laufzeitfehler | Jede Exception beim Lesen/Schreiben/Löschen → `LogWarning`, kein Throw | Plugin-Instanziierung und Klon-Ablauf dürfen nicht durch die Bereinigung fehlschlagen |

## Konfigurationsänderungen

Keine. Das Verhalten ist sicherheitsrelevant und nicht per Einstellung abschaltbar.

## Seiteneffekte und Risiken

- **`CheckoutPullRequestSourceCoreAsync` / PR-Checkout:** Erhält erstmals eine Umgebung — Verhaltensänderung nur insofern, als `git fetch` nun explizit authentifiziert ist; positiv gemeint (Regressionsvermeidung nach `_netrc`-Entzug).
- **`GetDefaultBranchAsync`:** Wechsel zu `GetGitEnvironment()` fügt dem `ls-remote`-Aufruf `GIT_SSH_COMMAND` und `GIT_CONFIG_*` hinzu — für SSH-URLs unverändertes Verhalten (StrictHostKeyChecking war bereits implizit gewünscht); für HTTPS-URLs nun authentifiziert.
- **Unit-Tests berühren echte Dateien:** Mit Konstruktor-Trigger würden alle `new GitHubPlugin(...)`-Testaufrufe ohne `netrcPath` die echte `%USERPROFILE%\_netrc` bereinigen — alle Test-Konstruktionsstellen müssen einen Temp-Pfad übergeben (siehe Tests). Betrifft auch die implizit bisher schon bestehende Unreinheit, dass der alte Schreibblock in Tests die echte Datei beschrieb.
- **`ScmE2EPluginManager`** (`src/Softwareschmiede.App/Services/Testing/ScmE2EPluginManager.cs`, Z. 24): instanziiert `GitHubPlugin` ohne `netrcPath` → läuft in E2E-Kontext mit realem Profilpfad. Das ist produktionsnahes Verhalten und unverändert korrekt (kompiliert dank optionalem Parameter ohne Anpassung).
- **git-Submodule/LFS gegen Nicht-GitHub-Hosts:** `extraheader` ist auf `https://github.com/` beschränkt — identisch zum bisherigen Abdeckungsumfang der `_netrc` (nur github.com/api.github.com), also keine Verschlechterung.
- **Credential-Helper-Konflikt (theoretisch):** Installierte Credential-Manager (z. B. GCM) könnten zusätzliche `Authorization`-Header liefern; `extraheader` wird bereits beim initialen Request gesendet und greift — derselbe Mechanismus läuft im Agent-Pfad produktiv. Kein Handlungsbedarf.

## Umsetzungsreihenfolge

1. **`netrcPath`-Seam + Bereinigungslogik in `GitHubPlugin`**
   - Voraussetzungen: keine (nur `System.IO`, `Microsoft.Extensions.Logging` — vorhanden).
   - Beschreibung: `_netrcPath`-Feld und optionalen Konstruktorparameter am vollen Konstruktor ergänzen; `ResolveNetrcPath`, `RemoveNetrcStanzas` (statisch, Parsing analog `BitBucketPlugin.UpdateNetrcEntry`), `RemoveLegacyNetrcEntries` implementieren; Aufruf aus dem Konstruktor.

2. **Nicht-persistente Auth in `GetGitEnvironment`**
   - Voraussetzungen: keine.
   - Beschreibung: `AddGitHubHttpAuthHeader` extrahieren (aus `GetAgentEnvironmentAsync`-Logik), in `GetGitEnvironment` bei Token aufrufen; `NETRC`-Block (Z. 196–199) entfernen; `GetAgentEnvironmentAsync` auf den Helfer umstellen.

3. **Aufrufer-Anpassungen**
   - Voraussetzungen: Schritt 2 (`GetGitEnvironment` liefert die Auth).
   - Beschreibung: `GetDefaultBranchAsync` auf `GetGitEnvironment()` umstellen; `CheckoutPullRequestSourceAsync`-Override mit `GetGitEnvironment()` ergänzen.

4. **Schreibpfad entfernen**
   - Voraussetzungen: Schritt 1 und 2 (Bereinigung läuft über Konstruktor; Auth hängt nicht mehr an `_netrc`).
   - Beschreibung: netrc-Block in `ConfigureGitCredentialsAsync` (Z. 334–355) streichen; veraltete Kommentare (Z. 357–358, Z. 878–879) aktualisieren.

5. **Tests anpassen und ergänzen**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: Test-Konstruktionsstellen auf Temp-`netrcPath` umstellen; neue Env-/Bereinigungs-Tests (siehe Tests).

6. **Formatierung und Verifikation**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `dotnet format Softwareschmiede.slnx`, vollständiger Build, reguläre Testlane (`--filter "Category!=OsInterface"`) mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`.

## Tests

### Neue Tests

|| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
||--------------------|------------|-------------------------------------|
|| `GetGitEnvironment_MitToken_SetztExtraHeaderUndKeinNetrc` (o. ä. über `CloneRepositoryAsync`/`PushBranchAsync`-Env-Matcher) | `GitHubPluginTests` (`src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`) | Env enthält `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`, `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64("x-access-token:"+token)>` sowie `GH_TOKEN`; enthält **kein** `NETRC` |
|| `GetGitEnvironment_OhneToken_NurTerminalPromptUndSsh` | `GitHubPluginTests` | Ohne Credential: kein `GH_TOKEN`, keine `GIT_CONFIG_*`, kein `NETRC` |
|| `Ctor_BereinigtGithubStanzas_UndBehalteFremdeintraege` | neue Datei `GitHubPluginNetrcTests.cs` (Testklasse z. B. `GitHubPluginNetrcCleanupTests`) | Temp-`_netrc` mit `machine github.com`, `machine api.github.com` und `machine bitbucket.org` (+Self-Hosted-Host): nach `new GitHubPlugin(..., netrcPath)` sind GitHub-Stanzas entfernt, Bitbucket-/Fremd-Inhalt unverändert |
|| `Ctor_LoeschtNetrc_WennNurGithubStanzas` | `GitHubPluginNetrcCleanupTests` | Datei nur mit den zwei GitHub-Stanzas → Datei existiert danach nicht mehr |
|| `Ctor_LaesstNetrcUnveraendert_OhneGithubStanzas` | `GitHubPluginNetrcCleanupTests` | Datei ohne GitHub-Einträge → Inhalt byteidentisch (kein Rewrite) |
|| `Ctor_LegtKeineNetrcAn_WennFehlend` | `GitHubPluginNetrcCleanupTests` | `netrcPath` zeigt auf nicht-existente Datei → Datei existiert weiterhin nicht |
|| `Ctor_Bereinigungsfehler_WirftNicht` | `GitHubPluginNetrcCleanupTests` | `netrcPath` zeigt z. B. auf ein Verzeichnis → `new GitHubPlugin(...)` wirft nicht; `LogWarning` wird geloggt (Mock-Verify) |
|| `GetDefaultBranchAsync_Env_EnthaeltGitAuth` | `GitHubPluginTests` | `ls-remote --symref` erhält Env mit `GIT_CONFIG_*` (statt bisher nur `GH_TOKEN`) |
|| `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment` | `GitHubPluginTests` | `git fetch`/`checkout`-Aufrufe des PR-Checkouts erhalten Env mit `GIT_CONFIG_*`/`GH_TOKEN` |
|| Hilfsmethode `CreateTempNetrcPath()` / Env-Matcher `HasGitHubExtraHeader(env, token)` | `GitHubPluginNetrcCleanupTests` / `GitHubPluginTests` | Stellt nicht-existenten Temp-Pfad bzw. Temp-Verzeichnis (`IDisposable`-Aufräumen) und die wiederkehrende Env-Prüfung bereit |

### Betroffene bestehende Tests

|| Test / Testklasse | Grund der Anpassung |
||-------------------|---------------------|
|| `GitHubPluginTests` Konstruktor-Aufruf (Z. 26) | Temp-`netrcPath` übergeben, damit die Konstruktor-Bereinigung nicht die echte `%USERPROFILE%\_netrc` anfasst |
|| `GitHubPluginTests_GetRepositoryStructureAsync` Konstruktor-Aufruf (Z. 30) | dito |
|| `GitHubSinceWindowTests` Konstruktor-Aufrufe (Z. 31, Z. 45) | dito |
|| `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled`, `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush`, `PullAsync_ShouldRunGitPull_WithConfiguredCredentials` | Env-Matcher um `GIT_CONFIG_*`-Präsenz und `NETRC`-Abwesenheit erweitern (bisher nur `GH_TOKEN`/`GIT_TERMINAL_PROMPT`) |
|| `GetDefaultBranchAsync_ShouldReturnParsedBranch_WhenSymRefCanBeParsed` / `…_ShouldFallbackToMain_WhenSymRefFails` | Env-Matcher ggf. von `It.IsAny` auf `GIT_CONFIG_*`-Assert schärfen (durch neue Tests abgedeckt; mindestens Compile-/Verhaltenssicherung) |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich. Begründung konkret für diese Anforderung:

- Die Änderung berührt **keinen über die UI auslösbaren Benutzerfluss**: kein neuer oder geänderter Dialog, kein ViewModel, keine sichtbare Interaktion. Betroffen sind ausschließlich die Prozessumgebung von `git`-Kindprozessen und eine Datei-Migration im Benutzerprofil — beides nicht durch UI-Aktionen direkt beobachtbar.
- Der eigentliche Funktionsnachweis (kein Schreiben/Löschen von `_netrc`-Inhalten, korrekte Env-Belegung) ist über die öffentliche Oberfläche (`new GitHubPlugin(..., netrcPath)`, `CloneRepositoryAsync` u. a. mit gemocktem `ICliRunner`) **vollständig und präziser** durch Unit-Tests prüfbar als über FlaUI — ein E2E-Test könnte nur indirekt (Datei-Seiteneffekt) asserten, was der Unit-Test direkt prüft.
- Ein echter E2E-Nachweis der Auth erforderte ein privates GitHub-Repository mit echtem PAT — nicht verfügbar und für die Sandbox-/CI-Lane nicht vorgesehen.
- Bestehende E2E-Suiten (`ScmE2EPluginManager`, LocalDirectory-basierte E2E-Tests) sind unberührt: Der E2E-Pfad nutzt das `LocalDirectory`-Plugin bzw. eigene Runner, nicht die geänderte `_netrc`-/Auth-Logik. Kein bestehender E2E-Test muss angepasst werden.

## Akzeptanzszenarien

|| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
||---|----------|----------|------------------|----------|-------------------|----------|
|| 1 | Happy Path: Bestandsinstallation wird bereinigt und Klonen funktioniert | App-Start → Plugin-Discovery instanziiert `GitHubPlugin`; anschließend Klon eines privaten GitHub-Repos über die bestehende Repository-Anlage | `_netrc` existiert mit plugin-geschriebenen GitHub-Stanzas (Altversion) und ggf. Bitbucket-Eintrag; gültiger PAT im `ICredentialStore` | Konstruktor-Bereinigung entfernt GitHub-Stanzas; `CloneRepositoryAsync` → `git clone` mit `GIT_CONFIG_*`-extraheader → `ConfigureGitCredentialsAsync` ohne Schreibzugriff | `_netrc` enthält keine GitHub-Tokens mehr (Fremd-Einträge erhalten); Klon erfolgreich ohne interaktive Prompts; Token nirgends in URL/Log/Datei | Unit-Tests: Cleanup-Tests (Temp-Datei), Clone-Env-Test; reale Abnahme optional durch Klon eines privaten Repos |
|| 2 | Keine `_netrc` vorhanden | App-Start / Plugin-Instanziierung | `_netrc` existiert nicht | Bereinigung kurzschließt | Es wird **keine** Datei angelegt; Git-Operationen funktionieren via extraheader | Unit-Test `Ctor_LegtKeineNetrcAn_WennFehlend` |
|| 3 | `_netrc` nur mit GitHub-Stanzas | Plugin-Instanziierung | Datei besteht ausschließlich aus `machine github.com`/`api.github.com`-Blöcken | Bereinigung → Rest leer → `File.Delete` | Datei ist gelöscht | Unit-Test `Ctor_LoeschtNetrc_WennNurGithubStanzas` |
|| 4 | Fremd-gepflegte `_netrc` (Bitbucket & Co.) | Plugin-Instanziierung | Datei mit `machine bitbucket.org` und GitHub-Stanzas | Bereinigung | Nur GitHub-Blöcke entfernt, Bitbucket-Eintrag byteidentisch erhalten | Unit-Test `Ctor_BereinigtGithubStanzas_UndBehalteFremdeintraege` |
|| 5 | Bereinigungsfehler blockiert nichts | Plugin-Instanziierung | `_netrc` gesperrt/unlesbar | Exception in Bereinigung → `LogWarning` | Plugin lädt normal; Git-Operationen laufen anschließend mit extraheader | Unit-Test `Ctor_Bereinigungsfehler_WirftNicht` |
|| 6 | Push/Pull/ls-remote auf bereits geklontem Repo | Nutzeraktion Push/Pull in Task-/Projektfluss bzw. Branch-Abfrage | Geklontes Repo, PAT vorhanden, `_netrc` bereits bereinigt | `GetGitEnvironment` liefert extraheader; git authentifiziert nicht-interaktiv | Operationen erfolgreich; keine Prompts (`GIT_TERMINAL_PROMPT=0` greift bei Auth-Fehlern weiter) | Unit-Env-Tests Push/Pull/ls-remote; `IsAuthenticationFailure`-Mapping unverändert |
|| 7 | Fehlendes Token | HTTPS-Klon ohne konfigurierten PAT | Kein Credential im Store | `CloneRepositoryAsync` wirft früh `InvalidOperationException`; Bereinigung läuft trotzdem token-unabhängig | Verständliche Fehlermeldung wie bisher; kein Dateizugriff auf `_netrc` über den Configure-Pfad hinaus | Bestehender Test `CloneRepositoryAsync_ShouldFailEarly_WhenHttpsTokenIsMissing` (unverändert) |

## Offene Punkte

Keine.
