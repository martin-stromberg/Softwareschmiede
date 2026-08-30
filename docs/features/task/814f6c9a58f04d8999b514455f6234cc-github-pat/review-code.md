# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Status der 2 Befunde aus review-code.2.md (Iteration 2 → Iteration 3)

Geprüft anhand `git diff HEAD -- plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`.

1. **Doppelter Test `NormalizeRemoteUrlAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl()` (Duplikat von `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()`)** → **Behoben.** Der Test wurde umbenannt zu `NormalizeRemoteUrlAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()` (Zeilen 1105–1140) und verifiziert die Normalisierung jetzt über den **Pull**-Pfad (`_sut.PullAsync("/repo")`, Token `"abc"`) statt über den Push-Pfad. Damit deckt er einen tatsächlich anderen, bisher ungetesteten Aspekt ab: dass `NormalizeRemoteUrlAsync()` auch aus `PullAsync()` heraus aufgerufen wird (vorher war nur der Push-Pfad mit eingebetteter Token-URL abgedeckt, `PullAsync_ShouldRunGitPull_WithConfiguredCredentials()` nutzte stets bereits eine bereinigte URL als Eingabe). Keine identische Arrange-Fixtur mehr zum Push-Test (unterschiedlicher Token-Wert, unterschiedliche Assertion: `a.Last() == ...` statt Mehrfach-Verify wie beim Push-Test).
2. **Redundante `GH_TOKEN`-Prüfung in `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()`** → **Behoben.** Die zusätzliche `Verify`-Prüfung auf `GH_TOKEN` im Environment wurde entfernt (Zeilen 1174–1200). Der Test prüft jetzt ausschließlich seine eigentliche Aussage: dass `set-url` nicht aufgerufen wird, wenn die Remote-URL bereits unauthentifiziert ist.

Beide aus Iteration 2 bemängelten Testduplikate sind damit bereinigt.

## Befunde

### GitHubPluginTests.cs (GitHubPluginTests)

- **Namenskonventionen und Einheitlichkeit** — Der Test `NormalizeRemoteUrlAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()` (Zeilen 1105–1140) ist nach der privaten Methode `NormalizeRemoteUrlAsync` benannt, obwohl er ausschließlich `_sut.PullAsync("/repo")` aufruft (Zeile 1132) — `NormalizeRemoteUrlAsync` ist privat und kann nicht direkt aufgerufen/getestet werden. Alle anderen Tests in dieser Datei folgen durchgängig dem Muster `<Öffentliche-Methode>_Should<Verhalten>` (z. B. `PushBranchAsync_Should...`, `PullAsync_Should...`, `CloneRepositoryAsync_Should...`), sodass der Testname sofort erkennen lässt, welche öffentliche API getestet wird. Dieser Test bricht mit dieser Konvention und könnte beim Lesen fälschlich vermuten lassen, es gäbe einen direkten Aufrufpfad zu `NormalizeRemoteUrlAsync`.

  Empfehlung: Test umbenennen zu `PullAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()` (oder analog `PullAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl_BeforePull()`), um der im Rest der Datei durchgängig verwendeten Namenskonvention zu folgen.

## Geprüfte Dateien

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
