# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### GitHubPlugin.cs (GitHubPlugin)

- **Doppelter Code** — `SanitizeSensitiveOutput()` (Zeilen 171–192) dupliziert das Token-Muster `oauth2:[^@\s]+@` als String-Literal in einem eigenen `Regex.Replace(sanitizedMessage, "oauth2:[^@\\s]+@", "oauth2:***@", RegexOptions.IgnoreCase)`-Aufruf (Zeilen 185–189), obwohl exakt dasselbe Muster bereits als kompilierte, wiederverwendbare `EmbeddedTokenPattern`-Regex (Zeile 20, `RegexOptions.Compiled`) existiert und in `EnsureRemoteCredentialsAsync()` genutzt wird. Zwei unabhängige Kopien desselben sicherheitsrelevanten Musters erhöhen das Risiko, dass sie bei einer künftigen Änderung auseinanderlaufen, und verzichten zusätzlich auf den Compiled-Vorteil der statischen Instanz.

  Empfehlung: In `SanitizeSensitiveOutput()` `EmbeddedTokenPattern.Replace(sanitizedMessage, "oauth2:***@")` verwenden statt eines eigenen `Regex.Replace`-Aufrufs mit dupliziertem Pattern-String.

- **Toter Code** — `BuildAuthenticatedCloneUrl()` (Zeilen 146–156) wird nach der Umstellung auf `GH_TOKEN`-Authentifizierung nirgends mehr aufgerufen (weder produktiv noch in Tests; per Repo-weiter Suche bestätigt). Als `private static` Methode kann sie auch nicht von außerhalb der Klasse verwendet werden, sodass „Rückwärtskompatibilität" kein tragfähiges Argument für ihren Verbleib ist.

  Empfehlung: Methode entfernen.

- **Methodennamen, die nicht beschreiben was die Methode tut** — `EnsureRemoteCredentialsAsync()` (Zeile 203) hieß und tat vor der Änderung genau das: fehlende Credentials in der Remote-URL ergänzen. Nach der Änderung tut die Methode das Gegenteil ihrer Namensbedeutung: Sie stellt keine Credentials mehr her, sondern entfernt ausschließlich einen ggf. vorhandenen eingebetteten Legacy-Token aus der Remote-URL (Normalisierung), unabhängig davon, ob überhaupt ein aktueller Token verfügbar ist. Der Name suggeriert weiterhin fälschlich, dass hier Authentifizierung "sichergestellt" wird.

  Empfehlung: Umbenennen, z. B. in `NormalizeRemoteUrlAsync()` oder `RemoveEmbeddedTokenFromRemoteUrlAsync()`, um die tatsächliche Verantwortung widerzuspiegeln.

- **Doppelter Code / unnötige Mehrfachzugriffe** — In `PushBranchAsync()` (Zeilen 768–787) und `PullAsync()` (Zeilen 790–809) wird im Fehlerfall der Token erneut über `_credentialStore.GetCredential(GitHubTokenCredentialKey)` abgerufen (Zeilen 784 bzw. 806), obwohl unmittelbar zuvor `GetGitEnvironment()` (ohne Parameter) denselben Token bereits intern aus demselben Credential-Store geholt hat. Das ist ein redundanter zweiter Zugriff auf den Credential-Store (potenziell OS-Credential-Manager/-Keychain) pro Fehlerfall. In `CloneRepositoryAsync()` wurde der Token dagegen korrekt einmal in eine lokale Variable geladen und für beide Zwecke wiederverwendet (Zeilen 734, 748, 753) — das neue Muster in Push/Pull weicht davon inkonsistent ab.

  Empfehlung: Token in `PushBranchAsync()`/`PullAsync()` einmal lokal auflösen (`var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);`) und sowohl an `GetGitEnvironment(token)` als auch an `SanitizeSensitiveOutput(result.StdErr, token)` übergeben, analog zu `CloneRepositoryAsync()`.

### GitHubPluginTests.cs (GitHubPluginTests)

- **Testqualität — Doppelter Code** — `CloneRepositoryAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication()` (Zeilen 538–560) prüft exakt dasselbe Verhalten, das bereits vollständig von `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()` (Zeilen 484–506) abgedeckt wird: dass beim `git clone`-Aufruf ein `IDictionary` mit `GH_TOKEN == <konfigurierter Token>` übergeben wird. Beide Tests unterscheiden sich nur durch den literalen Token-Wert ("token" vs. "secret-token"), decken aber keinen zusätzlichen fachlichen Fall ab.

  Empfehlung: Einen der beiden Tests entfernen, oder `CloneRepositoryAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication()` in einen tatsächlich neuen Aspekt umwandeln bzw. streichen, da die Aussage bereits durch den bestehenden Test abgedeckt ist.

- **Testqualität — Doppelter Code** — `PushBranchAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication()` (Zeilen 1078–1105) dupliziert die bereits in `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()` (Zeilen 999–1030, Assert-Block ab Zeile 1024) enthaltene Prüfung, dass beim `git push`-Aufruf `GH_TOKEN` im Environment gesetzt ist (dort sogar mit demselben Token-Wert `"token123"`). Es wird kein zusätzlicher fachlicher Fall geprüft.

  Empfehlung: `PushBranchAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication()` entfernen, da die Aussage bereits durch `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()` abgedeckt ist.

- **Testqualität — teilweise redundante Prüfung** — `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()` (Zeilen 1267–1300) enthält neben der eigentlichen neuen Aussage (kein `set-url`-Aufruf nötig, wenn die Remote-URL bereits unauthentifiziert ist) eine zweite `Verify`-Prüfung, dass `GH_TOKEN` im Environment des `pull`-Aufrufs vorhanden ist — dieselbe Aussage prüft bereits `PullAsync_ShouldRunGitPull_WithConfiguredCredentials()` (Zeilen 1237–1263). Die zusätzliche Prüfung ist hier weniger schwerwiegend als bei Clone/Push, da der Test primär einen anderen Aspekt (kein `set-url`) abdeckt, macht den Test aber unnötig breiter als nötig.

  Empfehlung: Die redundante `GH_TOKEN`-Verify-Prüfung aus `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()` entfernen und den Test auf seine eigentliche Aussage (kein `set-url`-Aufruf) fokussieren.

## Geprüfte Dateien

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
