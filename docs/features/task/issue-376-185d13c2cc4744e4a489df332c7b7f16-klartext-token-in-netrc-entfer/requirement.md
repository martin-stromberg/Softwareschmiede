# Übersetzte Anforderung: Klartext-Token in `_netrc` entfernen

**Quelle:** Issue #376 (aus Architektur-/Sicherheitsreview #374, Prio 1) / Aufgaben-ID `185d13c2-cc47-44e4-a489-df332c7b7f16`
**Branch:** `task/issue-376-185d13c2cc4744e4a489df332c7b7f16-klartext-token-in-netrc-entfer`

## Fachliche Zusammenfassung

`GitHubPlugin.ConfigureGitCredentialsAsync` schreibt nach jedem `git clone` den GitHub-Personal-Access-Token als `password {token}` im Klartext nach `%USERPROFILE%\_netrc` (Windows) bzw. `~/.netrc` (`GitHubPlugin.cs`, Zeilen 334–355). Diese Token-Persistenz untergräbt die Credential-Manager-Strategie (`ICredentialStore`): bekannter Looting-Pfad, keine ACL-Härtung, keine Bereinigung bei Token-Wechsel. Die Anforderung entfernt das `_netrc`-Schreiben, bereinigt die von früheren Versionen angelegten GitHub-Einträge in einer bestehenden Datei und stellt die Authentifizierung der Host-seitigen git-Operationen (`clone`, `push`, `pull`, `ls-remote`) auf einen nicht-persistierenden Mechanismus um (z. B. `http.extraheader` via `GIT_CONFIG_*`-Prozessumgebung — dasselbe Muster, das `GetAgentEnvironmentAsync` bereits für Agent-Prozesse nutzt — oder `credential.helper`).

## Fachlicher Scope

- **Verarbeitungseinheit:** Die *Credential-Konfiguration für Host-git-Operationen gegen github.com* — d. h. das Bündel aus git-Prozessumgebung (`GetGitEnvironment`) und der einmaligen Repository-Nachkonfiguration (`ConfigureGitCredentialsAsync`), die bisher den Token in `_netrc` persistierte.
- **Auslösende Ereignisse:**
  - Erfolgreicher `git clone` → `ConfigureGitCredentialsAsync` (`GitHubPlugin.cs`, Zeile 900, aufgerufen aus `CloneRepositoryAsync`, Zeile 866).
  - Jeder Host-git-Aufruf über `GetGitEnvironment(token)`: `clone` (Zeile 884), `push` (`PushBranchAsync`, Zeile 917), `pull` (`PullAsync`, Zeile 941), `ls-remote` (`GetRemoteBranchesAsync`, Zeile 1420).
  - Bereinigung einer bestehenden `_netrc`: fachlich einmalig nötig (Migration für Bestandsinstallationen); technischer Trigger zu klären (siehe Offene Fragen).
- **Erwartete Wirkung:**
  - Es wird kein GitHub-Token mehr nach `_netrc`/`.netrc` geschrieben; der Pfad enthält nach der Umstellung keine von diesem Plugin geschriebenen Klartext-Tokens.
  - In einer bereits existierenden `_netrc` werden die Einträge `machine github.com` und `machine api.github.com` entfernt.
  - `git clone`/`push`/`pull`/`ls-remote` gegen private GitHub-Repositories funktionieren weiterhin nicht-interaktiv (`GIT_TERMINAL_PROMPT=0` bleibt); Authentifizierungsfehler werden weiterhin über `IsAuthenticationFailure`/`SanitizeSensitiveOutput` behandelt und der Token erscheint weder in Remote-URLs, Logs noch Fehlermeldungen.
- **Umgang mit fehlenden Objekten:**
  - *Kein GitHub-Token im `ICredentialStore`:* bisheriges Verhalten beibehalten — Warnung und keinerlei Dateizugriff (`ConfigureGitCredentialsAsync`, Zeilen 300–305; `CloneRepositoryAsync` wirft bei HTTPS ohne Token bereits vorher, Zeilen 872–876).
  - *`_netrc` existiert nicht:* nichts zu bereinigen; es darf insbesondere **keine** neue Datei angelegt werden.
  - *`_netrc` existiert mit Fremd-Einträgen:* nur die GitHub-Stanzas (`machine github.com`, `machine api.github.com`) entfernen, alle übrigen Einträge unverändert lassen — zwingend, weil das `BitBucketPlugin` dieselbe Datei für `machine bitbucket.org` bzw. Self-Hosted-Hosts beschreibt (`BitBucketPlugin.UpdateNetrcEntry`, Zeilen 1487–1521; dokumentiert in `docs/help/plugins/bitbucket-plugin/troubleshooting.md`).
  - *`_netrc` nach Bereinigung leer:* Datei löschen oder leer belassen — Annahme: löschen, da eine leere Credential-Datei keinen Nutzen hat (siehe Offene Fragen).
- **Scope-Ausschlüsse:**
  - `BitBucketPlugin` bleibt unverändert; dessen `_netrc`-Nutzung ist nicht Gegenstand dieses Issues.
  - Keine Änderung an `GetAgentEnvironmentAsync` — die Agent-Umgebung nutzt bereits `http.extraheader` via `GIT_CONFIG_*` und schreibt nichts persistent (Zeilen 158–178).
  - Keine Änderung an `NormalizeRemoteUrlAsync` (Bereinigung eingebetteter Legacy-Tokens in Remote-URLs bleibt bestehen) und an `SanitizeSensitiveOutput`.
  - Die im Issue genannte Alternative „zumindest ACL-restriktiv schreiben" ist nur Fallback, nicht das primäre Ziel — die Empfehlung ist das Streichen des Schreibens (siehe Offene Fragen).

## Betroffene Klassen und Komponenten

### Bestehende, voraussichtlich zu ändernde Artefakte

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
  - `ConfigureGitCredentialsAsync` (Zeilen 298–390): Schreibblock für `netrcContent`/`File.WriteAllText` (Zeilen 334–355) entfernen und durch eine Bereinigung bestehender GitHub-Einträge ersetzen. `user.name`/`user.email`/`core.sshCommand`-Konfiguration und `git remote set-url` bleiben unverändert.
  - `GetGitEnvironment` (Zeilen 180–203): `NETRC`-Umgebungsvariable (Zeilen 196–199) entfällt; dafür muss ein Ersatz-Authentifizierungsmechanismus in die Prozessumgebung, denn **`GH_TOKEN` allein authentifiziert `git` nicht** (nur die `gh`-CLI) — die bisherige Host-git-Auth lief faktisch über `_netrc`.
  - Aufrufer, die die neue Umgebung erhalten: `CloneRepositoryAsync` (Zeile 884), `PushBranchAsync` (Zeile 917), `PullAsync` (Zeile 941), `GetRemoteBranchesAsync` (Zeile 1420). Hinweis: `GetDefaultBranchAsync` (Zeile 1476) nutzt `GetGhEnvironment` — ebenfalls ohne `GIT_CONFIG_*`; zu prüfen, ob es auf denselben Mechanismus umzustellen ist.

### Neu zu erstellende Artefakte (voraussichtlich, Namen sind Vorschläge)

- Private Hilfsmethode im `GitHubPlugin` zum Entfernen von netrc-Stanzas (z. B. `RemoveNetrcEntries(netrcPath, params string[] hosts)`) — Referenz für das Stanza-Parsing ist `BitBucketPlugin.UpdateNetrcEntry` (Zeilen 1487–1521), die dasselbe `machine`-Block-Format verarbeitet.
- Keine neuen Interfaces, Entities, Enums oder UI-Komponenten ableitbar.

### Tests

- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` (und ggf. weitere `GitHubPluginTests_*.cs`): Bestehende Tests prüfen netrc nicht explizit (Treffer-Suche: keine `netrc`-Referenzen in den Tests). Neu zu prüfende Aspekte:
  - `CloneRepositoryAsync`/Configure-Pfad schreibt **keine** `_netrc` mehr bzw. ruft keinen Schreibzugriff auf den User-Profile-Pfad auf.
  - Bereinigung: bestehende `_netrc` mit GitHub- und Fremd-Einträgen — nur `machine github.com`/`machine api.github.com` werden entfernt, Fremd-Einträge bleiben; nicht vorhandene Datei wird nicht angelegt.
  - `GetGitEnvironment` liefert den Ersatz-Auth-Mechanismus (z. B. `GIT_CONFIG_KEY_0`/`GIT_CONFIG_VALUE_0` mit `http.https://github.com/.extraheader`) und kein `NETRC` mehr.
  - Fehlerpfade: Bereinigungsfehler (z. B. gesperrte Datei) werden wie bisher nur geloggt (`LogWarning`) und brechen den Ablauf nicht.

## Implementierungsansatz

- **Schreibpfad entfernen:** In `ConfigureGitCredentialsAsync` entfallen `netrcContent` und `File.WriteAllText(netrcPath, …)` komplett. Der `try/catch`-Rahmen mit `LogWarning` kann für die Bereinigungsroutine wiederverwendet werden (Fehler bei der Bereinigung sollen den Klon nicht fehlschlagen lassen — Annahme analog bisherigem Verhalten).
- **Bereinigung:** Beim nächsten Durchlauf von `ConfigureGitCredentialsAsync` (und ggf. zusätzlich einmalig beim Plugin-/App-Start — offene Frage) eine existierende `_netrc` einlesen, alle Blöcke `machine github.com` und `machine api.github.com` (jeweils `machine`-Zeile plus folgende `login`/`password`-Zeilen bis zur nächsten `machine`-Zeile) entfernen und die Datei nur bei tatsächlicher Änderung zurückschreiben. Parsing analog `UpdateNetrcEntry` im `BitBucketPlugin`.
- **Ersatz-Auth für Host-git:** `GetGitEnvironment` um dieselben `GIT_CONFIG_*`-Einträge erweitern, die `GetAgentEnvironmentAsync` bereits setzt (Zeilen 172–175): `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=http.https://github.com/.extraheader`, `GIT_CONFIG_VALUE_0=AUTHORIZATION: basic <base64("x-access-token:"+token)>`. Damit bleibt der Token ausschließlich Prozessumgebung und wird nirgends persistiert. Alternative laut Issue: `credential.helper` (z. B. `!gh auth git-credential`) — die `extraheader`-Variante liegt näher am bestehenden Codemuster und hat keine Laufzeitabhängigkeit zur `gh`-CLI innerhalb von git-Aufrufen (Annahme; siehe Offene Fragen).
- **Nebenbefund:** `env["NETRC"]` in `GetGitEnvironment` (Zeilen 197–199) verweist git/curl explizit auf die Datei; ohne geschriebene Datei ist der Eintrag wirkungslos bis kontraproduktiv (curl würde eine vom Anwender gepflegte `_netrc` mit anderen Credentials lesen) und sollte entfallen.
- **Abhängigkeiten:** `ICredentialStore` (`GitHubTokenCredentialKey`), `ICliRunner`, `ILogger<GitHubPlugin>` — keine neuen Abhängigkeiten nötig.

## Konfiguration

- Aus der Anforderung ist **kein** Konfigurationsbedarf ableitbar. Das Verhalten ist sicherheitsrelevant und sollte nicht per Einstellung abschaltbar sein (Annahme). Die ACL-restriktive Schreibvariante käme nur als Fallback in Betracht, falls sich in der Umsetzung zeigt, dass ein Szenario ohne persistierte Credentials nicht abdeckbar ist — dann als separate Entscheidung (offene Fragen).

## Offene Fragen

1. **Ersatz-Mechanismus:** `http.extraheader` via `GIT_CONFIG_*` (konsistent mit `GetAgentEnvironmentAsync`) oder `credential.helper` (z. B. `!gh auth git-credential`)? Das Issue nennt beide Optionen („`GH_TOKEN` plus bei Bedarf `credential.helper`"); `GH_TOKEN` allein reicht für `git` faktisch nicht aus.
2. **Trigger der Bestands-Bereinigung:** Bei jeder Ausführung von `ConfigureGitCredentialsAsync` (einfach, aber Installationen ohne erneutes Klonen behalten die Datei) oder einmalig beim Plugin-/App-Start (benötigt einen neuen Triggerpunkt)? Oder beides?
3. **Leere `_netrc` nach Bereinigung:** Datei löschen oder leer belassen? (Beachte: Anwender könnten die Datei auch manuell pflegen — Löschen nur, wenn sie ausschließlich aus den entfernten GitHub-Stanzas bestand.)
4. **`GetDefaultBranchAsync`:** nutzt aktuell `GetGhEnvironment` ohne git-Auth-Konfiguration (Zeile 1476); soll es auf denselben Mechanismus wie `GetGitEnvironment` umgestellt werden, damit `ls-remote` gegen private Repositories zuverlässig funktioniert?
5. **Fremd-gepflegte `machine github.com`-Einträge:** Wenn ein Anwender die GitHub-Einträge in `_netrc` bewusst manuell pflegt, würde die Bereinigung sie ebenfalls entfernen — ist das akzeptabel (Annahme: ja, da nicht unterscheidbar und der Pfad ohnehin obsolet wird)?
6. **ACL-restriktive Fallback-Variante:** Endgültig verwerfen, oder als dokumentierte Restoption für den Fall, dass `extraheader` in einem Szenario nicht greift (z. B. git-Submodules/LFS gegen fremde Hosts — wobei `_netrc` bisher ohnehin nur github.com-Einträge enthielt)?
