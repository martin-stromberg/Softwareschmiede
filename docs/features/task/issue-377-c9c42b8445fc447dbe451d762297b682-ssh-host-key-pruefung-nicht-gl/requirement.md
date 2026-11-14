# Übersetzte Anforderung: SSH-Host-Key-Prüfung nicht global aushebeln (Issue #377, aus #374)

## Fachliche Zusammenfassung

Das `GitHubPlugin` soll git-Operationen ausschließlich über HTTPS mit der bestehenden Token-Authentifizierung (`GH_TOKEN`/`http.extraheader`) ausführen. Die heute doppelt vorhandene Deaktivierung der SSH-Host-Key-Prüfung wird komplett entfernt: zum einen die Prozessumgebungsvariable `GIT_SSH_COMMAND=ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null` in `GetGitEnvironment`, zum anderen das persistente `git config core.sshCommand …`, das `ConfigureGitCredentialsAsync` beim Klonen in jede betreute Arbeitskopie schreibt und das dort auch für spätere, nicht vom Plugin gestartete git-Aufrufe (z. B. Agenten- oder Nutzer-Operationen) die Host-Key-Prüfung aushebelt. SSH-Remote-URLs werden für vom Plugin ausgeführte git-Operationen nicht mehr verwendet; wo sie in verknüpften Repositories oder Bestands-Arbeitskopien auftreten, werden sie auf HTTPS umgestellt oder mit verständlicher Fehlermeldung abgelehnt.

## Fachlicher Scope

- **Verarbeitungseinheit:** Jede vom `GitHubPlugin` ausgeführte git-Operation gegen ein verknüpftes GitHub-Repository bzw. eine betreute lokale Arbeitskopie (Klon, Pull, Push, `ls-remote`, PR-Quell-Checkout) sowie die vom Plugin in die Arbeitskopie geschriebene lokale git-Konfiguration.
- **Auslösende Ereignisse:** Bestehende Plugin-Aufrufe ohne neuen Nutzertrigger: `CloneRepositoryAsync`, `PullAsync`, `PushBranchAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`, `CheckoutPullRequestSourceAsync` sowie `ConfigureGitCredentialsAsync` (intern nach dem Klon). Kein UI-Eingriff erforderlich.
- **Erwartete Wirkung:**
  - Keine vom Plugin gebaute git-Prozessumgebung enthält mehr `GIT_SSH_COMMAND` mit ausgehebelter Host-Key-Prüfung; neue Klone erhalten kein `core.sshCommand` in `.git/config`.
  - Alle plugin-geführten git-Operationen laufen über HTTPS-Remote-URLs und authentifizieren sich weiterhin nicht-interaktiv über `GH_TOKEN`/`GIT_CONFIG_*`-`http.extraheader`; ohne Token schlägt die Authentifizierung weiterhin sauber fehl (`GIT_TERMINAL_PROMPT=0`), nicht blockierend.
  - Sichtbarer Effekt für den Anwender: Klonen/Pullen/Pushen/PR-Checkout funktionieren unverändert mit konfiguriertem Personal Access Token; die MITM-Angriffsfläche durch die deaktivierte Host-Key-Prüfung entfällt.
- **Umgang mit fehlenden Objekten:**
  - Verknüpfte Repositories, deren `RepositoryUrl` im SSH-Format hinterlegt ist (`git@host:owner/repo`, `ssh://git@host/…`): auf die entsprechende HTTPS-Remote-URL umsetzen (Host/Pfad sind aus der URL ableitbar) oder ablehnen — Annahme: Umwandlung, siehe Offene Fragen.
  - Bestehende lokale Arbeitskopien, deren `remote.origin.url` noch ein SSH-Format trägt: bei der nächsten plugin-geführten Operation auf die HTTPS-URL normalisieren (Erweiterung des bestehenden `NormalizeRemoteUrlAsync`, das bereits eingebettete Legacy-Token aus URLs entfernt) — Annahme, siehe Offene Fragen.
  - Bestehende Arbeitskopien mit bereits persistent gesetztem `core.sshCommand`: Eintrag aktiv entfernen (`git config --unset core.sshCommand`), analog der einmaligen `_netrc`-Legacy-Bereinigung aus Issue #376 — Annahme, siehe Offene Fragen.
  - Fehlender GitHub-Token bei HTTPS-Bedarf: bestehender Fail-Fast in `CloneRepositoryAsync` (`InvalidOperationException` mit Hinweis auf die Token-Einstellung) bleibt bestehen und gilt nach der Umstellung für alle Remote-Operationen.
- **Scope-Ausschlüsse:**
  - Keine abgesicherte SSH-Alternative: die zweite Issue-Empfehlung (`StrictHostKeyChecking=accept-new` mit echter `known_hosts`) wird bewusst nicht umgesetzt — der Repository-Owner hat die Variante „nur HTTPS" gewählt.
  - `BitBucketPlugin.GetGitEnvironment` (BitBucketPlugin.cs Z. 172) setzt denselben `GIT_SSH_COMMAND`-Wert; das Issue nennt explizit nur `GitHubPlugin.cs` — das BitBucket-Plugin bleibt in dieser Anforderung unverändert (Abklärung als Offene Frage).
  - `GetAgentEnvironmentAsync` setzt heute bereits kein `GIT_SSH_COMMAND` (nur `GIT_TERMINAL_PROMPT`, `GH_TOKEN`, `GIT_CONFIG_*`-extraheader); dort ist keine Codeänderung erforderlich.
  - Kein neues Plugin-Setting und kein Opt-out, um SSH weiterhin zu erlauben — Annahme, siehe Offene Fragen.
  - `ParseEventRepository`/`TryExtractRepositoryId` dürfen SSH-URL-Formate weiterhin zur Extraktion der Repository-ID (`owner/repo`, Host) akzeptieren, solange keine git-Transportoperation mehr über SSH läuft — Annahme, andernfalls brechen Bestandsverknüpfungen mit SSH-URLs.

## Betroffene Klassen und Komponenten

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` (`GitHubPlugin`):
  - `GetGitEnvironment` (ca. Z. 206–230): `GIT_SSH_COMMAND`-Belegung (Z. 215) entfernen.
  - `ConfigureGitCredentialsAsync` (ca. Z. 492–561): `git config core.sshCommand …`-Aufruf (Z. 549–560) entfernen; ggf. Bereinigung des Eintrags in Bestands-Arbeitskopien.
  - `CloneRepositoryAsync` (ca. Z. 1037–1072): Behandlung von Nicht-HTTPS-URLs — heute ist ein SSH-Klon ohne Token zulässig; künftig Umwandlung zu HTTPS oder Ablehnung.
  - `NormalizeRemoteUrlAsync` (ca. Z. 448–490): ggf. um SSH→HTTPS-Normalisierung von `remote.origin.url` erweitern.
  - `IsHttpsRepositoryUrl` (ca. Z. 399–401): wiederverwendbare Prüfung.
  - `TryExtractRepositoryId` (ca. Z. 1782–1829): SSH-Formate weiterhin zur ID-Erkennung tolerieren (Annahme).
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs` — `ParseEventRepository` (ca. Z. 397–424): akzeptiert `git@`- und `ssh://`-URLs zur ID-/Host-Extraktion; keine Transportänderung, aber relevant für die HTTPS-Umsetzung (liefert Host und Pfad).
- Tests (`src/Softwareschmiede.Tests/Infrastructure/Plugins/`):
  - `GitHubPluginTests.CloneRepositoryAsync_OhneToken_NurTerminalPromptUndSshInEnv` (ca. Z. 1761): assertiert `env.ContainsKey("GIT_SSH_COMMAND")` und klont per `git@…`-URL — muss auf das neue Verhalten umgestellt werden.
  - Bestehende Env-Assertions um „kein `GIT_SSH_COMMAND`" zu verschärfen: `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled`, `PushBranchAsync_…`, `PullAsync_…`, `GetDefaultBranchAsync_Env_EnthaeltGitAuth`, `CheckoutPullRequestSourceAsync_UebergibtGitEnvironment`.
  - Neue Tests für SSH-URL-Behandlung (Umwandlung/Ablehnung), Entfernen von `core.sshCommand` in Bestands-Repos und Fehlen des Config-Aufrufs beim Klon.
- `GitPluginBase` (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`), `ICliRunner`, Interfaces/Enums/Datenmodell: keine Änderung erwartet — das betroffene Verhalten liegt vollständig im `GitHubPlugin`.
- Dokumentation (späterer Lifecycle-Schritt): `docs/help/plugins/ablauf-technisch.md` (Z. 88, 90) und `docs/help/plugins/business-rules.md` (Z. 160) beschreiben `GIT_SSH_COMMAND`/`core.sshCommand` als Ist-Zustand.

## Implementierungsansatz

- Die `GIT_SSH_COMMAND`-Zeile aus `GetGitEnvironment` entfernen. Da sämtliche git-Aufrufe des Plugins über diese Umgebung laufen (`_cliRunner.RunAsync("git", …, GetGitEnvironment(…))`), greift anschließend nur noch HTTPS — unter der Voraussetzung, dass alle verwendeten Remote-URLs HTTPS sind.
- Den `git config core.sshCommand`-Aufruf aus `ConfigureGitCredentialsAsync` entfernen; für Bestands-Arbeitskopien optional `git config --unset core.sshCommand` ausführen, Fehler nur loggen (analog dem bisherigen Fehler-Handling in der Methode).
- HTTPS-Erzwingung an einer zentralen Stelle: SSH-/SCP-Repository-URLs deterministisch in HTTPS-URLs desselben Hosts umwandeln (`git@host:owner/repo(.git)` → `https://host/owner/repo(.git)`; `ParseEventRepository` liefert bereits Host und Pfad) oder mit klarer `InvalidOperationException` ablehnen. Zu prüfende Nebenbedingung: `AddGitHubHttpAuthHeader` verdrahtet den extraheader-Key fest auf `http.https://github.com/.extraheader` — für GitHub-Enterprise-Hosts (von `ParseEventRepository` unterstützt, `apiHost = host` mit `/api/v3/`-Präfix) müsste der Key hostabhängig gebildet werden, sonst schlägt die HTTPS-Auth dort fehl.
- Keine Interface-, Datenmodell- oder UI-Änderung; kein neuer Konfigurationsschalter. Agenten-Umgebung (`GetAgentEnvironmentAsync`) bleibt unverändert; Agenten-git-Operationen in betreuten Kopien erben danach automatisch HTTPS über die Remote-URL bzw. den prozessbasierten extraheader.

## Konfiguration

Nicht konfigurierbar — das Verhalten ist fest verdrahtet (HTTPS-only für das GitHub-Plugin). Repository-Verknüpfungen sollten idealerweise weiterhin SSH-URLs akzeptieren, diese aber transparent in HTTPS-Remote-URLs umwandeln (Annahme).

## Offene Fragen

1. SSH-URLs in Repository-Verknüpfungen und Remote-URLs: deterministisch in HTTPS-URLs umwandeln (empfohlen, da die Token-Auth ohnehin über HTTPS läuft) oder mit Fehlermeldung ablehnen und den Anwender zur HTTPS-URL auffordern?
2. Bestandsbereinigung: Soll `core.sshCommand` in bereits betreuten Arbeitskopien aktiv entfernt werden (`git config --unset`), und soll `remote.origin.url` mit SSH-Format auf HTTPS umgeschrieben werden — jeweils analog der `_netrc`-Legacy-Bereinigung aus Issue #376?
3. Gilt die HTTPS-only-Entscheidung auch für das `BitBucketPlugin` (identischer `GIT_SSH_COMMAND`-Eintrag in `GetGitEnvironment`, Z. 172)? Das Issue nennt nur `GitHubPlugin.cs`.
4. GitHub Enterprise (Host ≠ `github.com`, von `ParseEventRepository` unterstützt): Die HTTPS-Auth hängt am fest verdrahteten `http.https://github.com/.extraheader`. Werden GHE-Hosts für git-Operationen tatsächlich genutzt? Falls ja, muss der extraheader-Key hostabhängig gebildet werden.
5. Soll es eine Opt-out-/Konfigurationsmöglichkeit geben, SSH weiterhin zuzulassen (z. B. für Anwender mit SSH-Keys aber ohne PAT)? Annahme dieser Übersetzung: nein.
