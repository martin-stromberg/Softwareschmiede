# Tests: GitHub-PAT-Authentifizierung

## Testklassen

### `GitHubPluginTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`

#### Clone- und Credentials-Tests

| Test | Was wird getestet |
|------|-------------------|
| `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()` | **Token-Embedding in Clone-URL:** Verifizieru.a., dass `git clone` mit URL `https://oauth2:token@github.com/test/repo` aufgerufen wird (Zeile 509) |
| `CloneRepositoryAsync_ShouldFailEarly_WhenHttpsTokenIsMissing()` | Wirft Exception wenn HTTPS-Clone ohne Token versucht wird |
| `CloneRepositoryAsync_ShouldThrowInvalidOperationException_WhenCliFails()` | Wirft Exception bei `git clone` Fehler |
| `CloneRepositoryAsync_ShouldMapAuthenticationErrors_ToHelpfulMessage()` | Mappt Auth-Fehler auf verständliche Meldung |
| `CloneRepositoryAsync_ShouldSanitizeToken_InThrownExceptionMessage()` | **Wichtig für diese Anforderung:** Validiert, dass Token in Exception-Messages nicht im Klartext auftaucht, sondern als `oauth2:***@` maskiert wird (Zeile 593) |

#### Push/Pull-Tests (URL-Embedding-abhängig)

| Test | Was wird getestet |
|------|-------------------|
| `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()` | **Token-Embedding:** Verifiziertm dass `EnsureRemoteCredentialsAsync()` aufgerufen wird und Remote-URL mit `oauth2:token123@` aktualisiert wird (Zeile 987) |
| `PushBranchAsync_ShouldNotSetRemoteUrl_WhenTokenIsMissing()` | Setzt keine URL wenn Token fehlt |
| `PushBranchAsync_ShouldThrow_WhenPushFails()` | Wirft Exception bei Push-Fehler |
| `PullAsync_ShouldRunGitPull_WithConfiguredCredentials()` | Nutzt `EnsureRemoteCredentialsAsync()`, Remote-URL kann Token enthalten |
| `PullAsync_ShouldThrow_WhenGitPullFails()` | Wirft Exception bei Pull-Fehler |

#### GitHub API Tests (nutzen bereits `GetGhEnvironment()`)

| Test | Was wird getestet |
|------|-------------------|
| `GetIssuesAsync_ShouldReturnParsedIssues_WhenCliSucceeds()` | `gh issue list` nutzt `GetGhEnvironment()` mit `GH_TOKEN` |
| `GetAlertsAsync_ShouldReturnMappedCodeScanningAlerts_WhenCliSucceeds()` | `gh api code-scanning/alerts` nutzt `GetGhEnvironment()` |
| `GetAlertsAsync_ShouldReturnAlertsFromAllPaginatedPages()` | Paginierte Alerts mit `GetGhEnvironment()` |
| `GetAlertsAsync_ShouldReturnEmpty_WhenCodeScanningIsNotAvailable()` | Fehlerbehandlung |
| `GetAlertsAsync_ShouldReturnEmpty_WhenPermissionsAreMissing()` | **Token-Maskierung:** Token wird in `SanitizeSensitiveOutput()` maskiert (Zeile 211 zeigt Token `secret-token` in Fehler, wird aber niemals in Logs ausgegeben) |
| `GetAlertsAsync_ShouldPropagateCancellation_WhenCliIsCancelled()` | Cancellation-Handling |
| `CreateIssueAsync_ShouldReturnIssue_WhenCliSucceeds()` | `gh issue create` nutzt `GetGhEnvironment()` |
| `CreateIssueAsync_ShouldReturnFailed_WhenRepositoryIdIsMissing()` | Validierung ohne CLI-Aufruf |
| `CreateIssueAsync_ShouldReturnFailed_WhenTitleIsMissing()` | Validierung ohne CLI-Aufruf |
| `CreateIssueAsync_ShouldReturnFailed_WhenCliFails()` | Fehlerbehandlung mit Sanitization |
| `GetIssueTemplatesAsync_ShouldReturnTemplates_WhenRepositoryContainsTemplates()` | `gh api` Aufruf mit `GetGhEnvironment()` |
| `GetIssueTemplatesAsync_ShouldNormalizeRepositoryUrl()` | URL-Normalisierung vor API-Call |

#### Workflow & PR Tests

| Test | Was wird getestet |
|------|-------------------|
| `CreatePullRequestAsync_ShouldReturnParsedPullRequest_WhenCliSucceeds()` | `gh pr create` nutzt `GetGhEnvironment()` |
| `CreatePullRequestAsync_ShouldExplainNoCommitsBetweenFailure()` | Fehlerbehandlung |
| `GetPullRequestWorkflowRunsAsync_ShouldScopeRunListByCommitSha()` | `gh run list --commit` mit `GetGhEnvironment()` |
| `GetPullRequestWorkflowRunsAsync_ShouldCombineWorkflowNameAndDisplayTitle()` | Workflow-Parsing |
| `CompletePullRequestAsync_ShouldReturnNonMergedResult_WhenApprovalOnlySucceeds()` | `gh pr review --approve` mit `GetGhEnvironment()` |
| `CompletePullRequestAsync_ShouldReturnNonMergedResult_WhenAutoMergeDoesNotMergeImmediately()` | `gh pr merge --auto` mit `GetGhEnvironment()` |

#### Hilfsmethoden-Tests

| Test | Was wird getestet |
|------|-------------------|
| `CreateBranchAsync_ShouldCallGitCheckoutMinusB_WhenCalled()` | `git checkout -b` (kein Token-Handling nötig) |
| `CheckHealthAsync_ShouldReturnTrue_WhenGhAuthStatusSucceeds()` | `gh auth status` mit `GetGhEnvironment()` |
| `CheckHealthAsync_ShouldReturnFalse_WhenGhAuthStatusFails()` | Auth-Fehler-Handling |
| `GetRemoteBranchesAsync_ShouldParseAndSortBranches()` | `git ls-remote --heads` mit `GetGitEnvironment()` |
| `GetRemoteBranchesAsync_ShouldReturnEmpty_WhenCliFails()` | Fehlerbehandlung |
| `GetDefaultBranchAsync_ShouldReturnParsedBranch_WhenSymRefCanBeParsed()` | `git ls-remote --symref HEAD` mit `GetGhEnvironment()` |
| `GetDefaultBranchAsync_ShouldFallbackToMain_WhenSymRefFails()` | Fallback auf "main" |
| `CheckoutRemoteBranchAsync_ShouldThrow_WhenCheckoutFails()` | `git checkout` Fehlerbehandlung |
| `ResetAsync_ShouldCallGitResetWithTargetRef_WhenProvided()` | `git reset` mit Target-Ref |
| `ResetAsync_ShouldCallGitResetWithoutTargetRef_WhenNotProvided()` | `git reset` ohne Target-Ref |

#### Metadaten-Tests

| Test | Was wird getestet |
|------|-------------------|
| `PluginMetadata_ShouldExposeExpectedValues()` | Plugin-Einstellungen vorhanden: `PluginPrefix = "Softwareschmiede.GitHub"`, Token-Feld als Secret, erforderlich, Pull-Request-Settings vorhanden |

---

## Relevante Test-Setup

### Test-Fixture (Constructor)

```csharp
public GitHubPluginTests()
{
    _cliRunnerMock = new Mock<ICliRunner>();
    _credentialStoreMock = new Mock<ICredentialStore>();
    _sut = new GitHubPlugin(
        _cliRunnerMock.Object,
        _credentialStoreMock.Object,
        new Mock<ILogger<GitHubPlugin>>().Object);
}
```

- `_credentialStoreMock` – Mocked `ICredentialStore`, gibt Token via `GetCredential()` zurück
- `_cliRunnerMock` – Mocked `ICliRunner`, simuliert CLI-Befehle
- `_sut` (System Under Test) – Instanz von `GitHubPlugin`

### Häufige Test-Patterns

**Token-Setup:**
```csharp
_credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
```
oder
```csharp
_credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token123");
```

**Umgebungsvariablen-Verifikation:**
```csharp
It.Is<IDictionary<string, string>?>(env => env != null && env.ContainsKey("GIT_TERMINAL_PROMPT"))
It.Is<IDictionary<string, string>?>(env => env != null && env.ContainsKey("GH_TOKEN"))
```

**URL-Embedding-Verifikation (wird geprüft, aber ist Sicherheitsrisiko):**
```csharp
a.Any(x => x.Contains("https://oauth2:token@github.com/test/repo", StringComparison.Ordinal))
```

---

## Lücken in der Test-Abdeckung (für Anforderung relevant)

1. **Keine Tests für Token-Nicht-Embedding in lokalen Git-Operationen:** Es gibt keine Tests, die explizit validieren, dass `CloneRepositoryAsync()`, `PushBranchAsync()` und `PullAsync()` **ohne** Token-Embedding in der Remote-URL arbeiten können.

2. **`.netrc`-Funktionalität nicht getestet:** `ConfigureGitCredentialsAsync()` erstellt `.netrc`-Datei (Zeile 306), aber es gibt keine Tests dafür, ob `.netrc` tatsächlich funktioniert oder als Fallback genutzt wird.

3. **`GH_TOKEN`-Umgebungsvariable für `git`-Befehle nicht explizit getestet:** `GetGitEnvironment()` wird aufgerufen (z.B. bei `PushBranchAsync()`, Zeile 796), aber es gibt keine Tests, die explizit überprüfen, dass der Token als `GH_TOKEN` Umgebungsvariable (statt URL-Embedding) übergeben wird.

4. **Token-Sanitization für alle Error-Paths:** Nur `CloneRepositoryAsync` testet `SanitizeSensitiveOutput()` (Zeile 577). Andere Methoden wie `PushBranchAsync`, `PullAsync` sollten auch getestet werden.
