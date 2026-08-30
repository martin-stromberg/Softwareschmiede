# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Status der 2 Befunde aus review-code.2.md (Iteration 2 → Iteration 3)

Geprüft anhand `git diff HEAD -- plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`.

1. **Doppelter Test `NormalizeRemoteUrlAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl()` (Duplikat von `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()`)** → **Behoben.** Der Test wurde umbenannt zu `PullAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()` (Zeilen 1105–1140) und verifiziert die Normalisierung jetzt über den **Pull**-Pfad (`_sut.PullAsync("/repo")`, Token `"abc"`) statt über den Push-Pfad. Damit deckt er einen tatsächlich anderen, bisher ungetesteten Aspekt ab: dass `NormalizeRemoteUrlAsync()` auch aus `PullAsync()` heraus aufgerufen wird (vorher war nur der Push-Pfad mit eingebetteter Token-URL abgedeckt, `PullAsync_ShouldRunGitPull_WithConfiguredCredentials()` nutzte stets bereits eine bereinigte URL als Eingabe). Keine identische Arrange-Fixtur mehr zum Push-Test (unterschiedlicher Token-Wert, unterschiedliche Assertion: `a.Last() == ...` statt Mehrfach-Verify wie beim Push-Test).
2. **Redundante `GH_TOKEN`-Prüfung in `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()`** → **Behoben.** Die zusätzliche `Verify`-Prüfung auf `GH_TOKEN` im Environment wurde entfernt (Zeilen 1174–1200). Der Test prüft jetzt ausschließlich seine eigentliche Aussage: dass `set-url` nicht aufgerufen wird, wenn die Remote-URL bereits unauthentifiziert ist.

Beide aus Iteration 2 bemängelten Testduplikate sind damit bereinigt.

## Befunde

### GitHubPluginTests.cs (GitHubPluginTests)

- **Namenskonventionen und Einheitlichkeit** — Der Test `PullAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()` (Zeilen 1105–1140) folgt nun der durchgängigen Namenskonvention und ist nach der öffentlichen Methode `PullAsync` benannt, die er tatsächlich aufruft. Damit ist die Konvention `<Öffentliche-Methode>_Should<Verhalten>` eingehalten (wie auch `PushBranchAsync_Should...`, `CloneRepositoryAsync_Should...`, etc.). Der Testname macht sofort klar, dass die öffentliche API `PullAsync` getestet wird.

## Geprüfte Dateien

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
