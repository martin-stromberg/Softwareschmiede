# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Status der vorherigen 6 Befunde (Iteration 1 → Iteration 2)

Geprüft anhand `git diff HEAD -- plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`.

1. **Doppelter Code — `oauth2:[^@\s]+@`-Pattern dupliziert in `SanitizeSensitiveOutput()`** → **Behoben.** `SanitizeSensitiveOutput()` nutzt jetzt `EmbeddedTokenPattern.Replace(sanitizedMessage, "oauth2:***@")` (Zeile 173) statt eines eigenen `Regex.Replace`-Aufrufs mit dupliziertem Pattern-String.
2. **Toter Code — `BuildAuthenticatedCloneUrl()` unbenutzt** → **Behoben.** Methode wurde vollständig entfernt; repo-weite Suche findet keine verbleibenden Aufrufe/Definitionen mehr in Produktions- oder Testcode.
3. **Irreführender Methodenname `EnsureRemoteCredentialsAsync()`** → **Behoben.** Methode wurde zu `NormalizeRemoteUrlAsync()` umbenannt (Zeile 187) und tut jetzt genau das, was der Name sagt: eingebetteten Legacy-Token aus der Remote-URL entfernen, unabhängig von einem aktuell verfügbaren Token.
4. **Redundanter zweiter Credential-Store-Zugriff in `PushBranchAsync()`/`PullAsync()`** → **Behoben.** Beide Methoden lesen den Token jetzt einmal in eine lokale Variable (`var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);`, Zeilen 759 bzw. 783) und übergeben sie sowohl an `GetGitEnvironment(token)` als auch an `SanitizeSensitiveOutput(result.StdErr, token)` — analog zu `CloneRepositoryAsync()`.
5. **Doppelter Test `CloneRepositoryAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication`** → **Behoben.** Der Name existiert nicht mehr; der ehemalige Duplikat-Testkörper wurde in `CloneRepositoryAsync_ShouldNotEmbedToken_InCloneUrl()` überführt und prüft jetzt einen eigenständigen Aspekt (kein `oauth2:`/Token-Literal in den Clone-Argumenten) statt erneut das GH_TOKEN-Environment zu verifizieren.
6. **Doppelter Test `PushBranchAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication`** → **Teilweise behoben.** Der Test mit diesem Namen wurde entfernt. Die dadurch behobene Duplikation ist jedoch durch einen strukturell gleichwertigen neuen Duplikat-Test wieder aufgetreten — siehe Befund unten (`NormalizeRemoteUrlAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl`).
7. *(Ergänzend, dritter Testbefund aus Iteration 1)* **Redundante `GH_TOKEN`-Prüfung in `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()`** → **Nicht behoben.** Die empfohlene Entfernung der zusätzlichen `GH_TOKEN`-Verify-Prüfung wurde nicht umgesetzt — siehe Befund unten.

## Befunde

### GitHubPluginTests.cs (GitHubPluginTests)

- **Testqualität — Doppelter Code (neu, durch den Fix von Befund 6 wieder eingeführt)** — `NormalizeRemoteUrlAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl()` (aktuell ca. Zeilen 1105–1140) verwendet exakt dieselbe Arrange-Fixtur wie `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()` (ca. Zeilen 971–1018): Token `"token123"`, `config remote.origin.url` liefert `"https://oauth2:token123@github.com/owner/repo.git"`, `set-url` und `push` liefern Erfolg. Beide Tests verifizieren im Kern dieselbe Aussage — dass `set-url` mit einer vom eingebetteten Token bereinigten URL aufgerufen wird (`PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush` prüft `a.Any(x => x == "https://github.com/owner/repo.git")`, `NormalizeRemoteUrlAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl` prüft `a.All(x => !x.Contains("oauth2:token123@"))` — beides dieselbe Normalisierung anhand identischer Eingabedaten). Es wird kein zusätzlicher fachlicher Fall abgedeckt; dies ist dieselbe Art von Duplikat, die in Iteration 1 bereits für den Push-Test bemängelt und dort durch Entfernen des einen Tests behoben wurde, hier aber durch einen neuen Test wieder eingeführt wurde.

  Empfehlung: `NormalizeRemoteUrlAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl()` entfernen (die Aussage ist durch `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()` bereits abgedeckt), oder den Test auf einen tatsächlich neuen Aspekt umstellen, der nicht bereits durch einen bestehenden Test geprüft wird (z. B. ein zweites `oauth2:...@`-Vorkommen oder eine andere URL-Form, die die Regex-Grenzen testet).

- **Testqualität — teilweise redundante Prüfung (aus Iteration 1 nicht behoben)** — `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()` (aktuell ca. Zeilen 1209–1243) enthält weiterhin neben der eigentlichen Aussage (kein `set-url`-Aufruf nötig, wenn die Remote-URL bereits unauthentifiziert ist, Zeilen 1231–1236) eine zweite `Verify`-Prüfung (Zeilen 1237–1242), dass `GH_TOKEN` im Environment des `pull`-Aufrufs vorhanden ist — dieselbe Aussage prüft bereits `PullAsync_ShouldRunGitPull_WithConfiguredCredentials()` (Zeilen 1181–1207, dort sogar mit demselben Token-Wert `"token"` vs. hier `"token123"`, aber gleiches Prinzip). Das aus Iteration 1 empfohlene Entfernen dieser zusätzlichen Prüfung wurde nicht umgesetzt.

  Empfehlung: Die redundante `GH_TOKEN`-Verify-Prüfung aus `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()` (Zeilen 1237–1242) entfernen und den Test auf seine eigentliche Aussage (kein `set-url`-Aufruf) fokussieren.

## Geprüfte Dateien

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
