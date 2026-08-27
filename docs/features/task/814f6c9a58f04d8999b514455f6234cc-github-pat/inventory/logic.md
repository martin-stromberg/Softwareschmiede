# Logik-Komponenten: GitHub-PAT-Authentifizierung

## `GitHubPlugin`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct)` | public | Klont ein GitHub-Repository. **Aktuell bettes Token in Clone-URL ein via `BuildAuthenticatedCloneUrl()`** |
| `ConfigureGitCredentialsAsync(string localPath, string repositoryUrl, CancellationToken ct)` | private | Konfiguriert Git-Credentials für ein lokales Repository. **Aktuell setzt Token direkt in Remote-URL via `git remote set-url` ein** |
| `EnsureRemoteCredentialsAsync(string localPath, CancellationToken ct)` | private | Prüft und aktualisiert Remote-URL mit Credentials. **Aktuell bettes Token ein wenn nicht vorhanden** |
| `PushBranchAsync(string localPath, string branchName, CancellationToken ct)` | public | Pusht einen Branch. Ruft `EnsureRemoteCredentialsAsync()` auf |
| `PullAsync(string localPath, CancellationToken ct)` | public | Führt `git pull` durch. Ruft `EnsureRemoteCredentialsAsync()` auf |
| `GetIssuesAsync(string repositoryId, CancellationToken ct)` | public | Ruft Issues via `gh issue list` ab, nutzt `GetGhEnvironment()` |
| `GetAlertsAsync(string repositoryId, CancellationToken ct)` | public | Ruft Code-Scanning-Alerts via `gh api` ab, nutzt `GetGhEnvironment()` |
| `CreateIssueAsync(string repositoryId, IssueCreateRequest request, CancellationToken ct)` | public | Erstellt Issue via `gh issue create`, nutzt `GetGhEnvironment()` |
| `CreatePullRequestAsync(...)` | public | Erstellt PR via `gh pr create`, nutzt `GetGhEnvironment()` |
| `CompletePullRequestAsync(...)` | public | Mergent oder genehmigt PR, nutzt `GetGhEnvironment()` |
| `GetPullRequestStatusAsync(string repositoryId, int pullRequestNumber, CancellationToken ct)` | public | Ruft PR-Status via `gh pr view` ab, nutzt `GetGhEnvironment()` |
| `GetPullRequestWorkflowRunsAsync(...)` | public | Ruft Workflow-Runs ab, nutzt `GetGhEnvironment()` |
| `CheckHealthAsync(CancellationToken ct)` | public | Prüft Authentifizierung via `gh auth status`, nutzt `GetGhEnvironment()` |
| `GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct)` | public | Ruft Remote-Branches via `git ls-remote --heads` ab, nutzt `GetGitEnvironment()` |
| `GetAvailableRepositoriesAsync(CancellationToken ct)` | public | Ruft verfügbare Repositories via `gh repo list` ab, nutzt `GetGhEnvironment()` |
| `GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct)` | public | Ermittelt Standard-Branch via `git ls-remote --symref`, nutzt `GetGhEnvironment()` |
| `GetRepositoryStructureAsync(...)` | public | Ruft Verzeichnisstruktur via `gh api` ab |
| `GetRepositoryStructureLoadResultAsync(...)` | public | Wie oben, mit DetailResult |

### Private Hilfsmethoden (Authentifizierung & Token-Handling)

| Methode | Sichtbarkeit | Zweck |
|---------|-------------|-------|
| `GetGhEnvironment()` | private | Liest Token aus `ICredentialStore`, gibt Dictionary mit `GH_TOKEN` Umgebungsvariable zurück. **Wird für `gh`-CLI-Aufrufe genutzt** |
| `GetGitEnvironment(string? token)` | private | Konfiguriert Umgebungsvariablen für `git`-Befehle: `GIT_TERMINAL_PROMPT=0`, `GIT_SSH_COMMAND` mit `StrictHostKeyChecking=no`, **`NETRC` Pfad** (Windows `_netrc`, Unix `.netrc`). **Aktuell keine Token-Übergabe als Umgebungsvariable** |
| `BuildAuthenticatedCloneUrl(string repositoryUrl, string token)` | private static | **Problematisch**: Baut HTTPS-URL mit eingebettetem Token `https://oauth2:{token}@github.com/owner/repo` |
| `SanitizeSensitiveOutput(string? message, string? token)` | private static | **Sicherheitsmaßnahme**: Ersetzt Token und Pattern `oauth2:***@` in Fehlermeldungen |
| `IsAuthenticationFailure(string error)` | private static | Detektiert Auth-Fehlern in CLI-Ausgabe |
| `IsNoCommitsBetweenFailure(string error)` | private static | Detektiert "no commits between" Fehler |
| `IsNotFound(string error)` | private static | Detektiert 404-Fehler |
| `IsBranchProtectionFailure(string error)` | private static | Detektiert Branch-Protection-Fehler |

### Token-Speicherung & Zugriff

- **Constant:** `GitHubTokenCredentialKey = "Softwareschmiede.GitHub.Token"`
- **Speicher:** `ICredentialStore` (injiziert via Constructor)
- **Abruf:** `_credentialStore.GetCredential(GitHubTokenCredentialKey)`

### Abhängigkeiten & Injektionen

- `ICliRunner _cliRunner` — Führt CLI-Befehle aus
- `ICredentialStore _credentialStore` — Speichert/holt Token
- `ILogger<GitHubPlugin> _logger` — Logging

---

## `PluginSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetValue(IPlugin plugin, PluginSettingField field)` | public | Gibt Wert aus `ICredentialStore` zurück (Schlüssel: `<PluginPrefix>.<FieldKey>`) |
| `SetValue(IPlugin plugin, PluginSettingField field, string value)` | public | Speichert Wert via `ICredentialStore` |
| `DeleteValue(IPlugin plugin, PluginSettingField field)` | public | Löscht Wert aus `ICredentialStore` |
| `HasValue(IPlugin plugin, PluginSettingField field)` | public | Prüft, ob Wert vorhanden ist |
| `GetAllPlugins(...)` | public | Kombiniert Git- und KI-Plugins |

**Abhängigkeiten:** `ICredentialStore`, `ILogger<PluginSettingsService>`

---

## Konfigurationsfeld: GitHub Personal Access Token

Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, Zeilen 36-44

```csharp
new PluginSettingGroup("Authentifizierung",
[
    new PluginSettingField(
        Key: "Token",
        Label: "Personal Access Token",
        FieldType: PluginSettingFieldType.Secret,
        Placeholder: "ghp_...",
        Description: "GitHub Personal Access Token mit den Berechtigungen repo, read:org und Zugriff auf Code-Scanning-Alerts.",
        IsRequired: true)
])
```

- **Key:** `Token`
- **Speicherung:** via `PluginSettingsService` unter `Softwareschmiede.GitHub.Token`
- **UI-Typ:** Secret (maskiert in Eingabefeldern)
- **Erforderlich:** Ja
