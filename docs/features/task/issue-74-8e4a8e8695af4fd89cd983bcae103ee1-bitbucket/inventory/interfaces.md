# Interfaces und Contracts

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Gemeinsame Basis für alle Plugins (Git-Verwaltung und KI-Automation).

| Mitglied | Typ | Zweck |
|----------|-----|-------|
| `PluginName` | Property | Eindeutiger Anzeigename (z.B. "Bitbucket") |
| `PluginPrefix` | Property | Basis-Präfix für Credential-Store-Schlüssel (z.B. "Softwareschmiede.Bitbucket") |
| `PluginType` | Property | Plugin-Klassifizierung: `SourceCodeManagement` oder `DevelopmentAutomation` |
| `GetSettingGroups()` | Methode | Gibt `IReadOnlyList<PluginSettingGroup>` zurück – definiert alle UI-Felder |

**BitbucketPlugin implementiert:** `IPlugin` (transitiv via `IGitPlugin`)

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

Spezialisiert `IPlugin` auf Git-Provider-Funktionalität.

| Mitglied | Parameter | Rückgabewert | Zweck |
|----------|-----------|--------------|-------|
| `GetRepositoryLinkFields()` | - | `IReadOnlyList<PluginSettingField>` | UI-Felder für Repository-Verknüpfung |
| `GetIssuesAsync(repositoryId, ct)` | Repository-ID, CancellationToken | `Task<IEnumerable<Issue>>` | Ruft Issues ab |
| `CloneRepositoryAsync(url, path, ct)` | Repository-URL, Zielverzeichnis | `Task` | Klont Repository |
| `CreateBranchAsync(path, name, ct)` | Lokaler Pfad, Branch-Name | `Task` | Erstellt Branch lokal |
| `PushBranchAsync(path, name, ct)` | Lokaler Pfad, Branch-Name | `Task` | Pusht Branch zum Remote |
| `PullAsync(path, ct)` | Lokaler Pfad | `Task` | Zieht Änderungen |
| `CreatePullRequestAsync(repoId, branch, title, body, ct)` | Repo-ID, Branch, Titel, Beschreibung | `Task<PullRequest>` | Erstellt PR |
| `CommitAsync(path, message, ct)` | Lokaler Pfad, Commit-Nachricht | `Task` | Erstellt Commit |
| `ResetAsync(path, type, ref, ct)` | Lokaler Pfad, Reset-Typ, Ref | `Task` | Setzt Commits zurück |
| `CheckHealthAsync(ct)` | CancellationToken | `Task<bool>` | Prüft Verbindung |
| `GetRemoteBranchesAsync(url, ct)` | Repository-URL | `Task<IEnumerable<string>>` | Listet Remote-Branches |
| `GetDefaultBranchAsync(url, ct)` | Repository-URL | `Task<string>` | Ermittelt Standard-Branch |
| `CheckoutRemoteBranchAsync(path, branch, ct)` | Lokaler Pfad, Branch-Name | `Task` | Checkt Remote-Branch |
| `GetGitActionCapabilitiesAsync(path, ct)` | Optionaler lokaler Pfad | `Task<GitActionCapabilities>` | Gibt verfügbare Git-Aktionen zurück |
| `MergeToSourceAsync(path, ct)` | Lokaler Pfad | `Task` | Merged zu Quellbranch (optional) |
| `GetAvailableRepositoriesAsync(ct)` | CancellationToken | `Task<IEnumerable<AvailableRepository>>` | Listet verfügbare Repositories |

**BitbucketPlugin implementiert:** `IGitPlugin` (via Erbung von `GitPluginBase<BitbucketPlugin>`)

## `ICredentialStore`
Datei: Schnittstelle definiert in Domain/Interfaces

Speichert und ruft sensible Konfigurationsdaten sicher ab.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetCredential(key)` | Schlüssel als String | `string` oder `null` | Ruft gespeicherten Credential-Wert ab |
| `SetCredential(key, value)` | Schlüssel, Wert | `void` | Speichert Credential-Wert |

**Verwendet von BitbucketPlugin für Schlüssel:**
- `Softwareschmiede.Bitbucket.Username`
- `Softwareschmiede.Bitbucket.AppPassword`
- `Softwareschmiede.Bitbucket.Workspace`
- `Softwareschmiede.Bitbucket.JiraUrl`
- `Softwareschmiede.Bitbucket.JiraProjectKey`
- `Softwareschmiede.Bitbucket.JiraEmail`
- `Softwareschmiede.Bitbucket.JiraApiToken`

**Implementierung:** `WindowsCredentialStore` (in `src/Softwareschmiede.Infrastructure/Services/`)

## `ICliRunner`
Datei: Domain/Interfaces

Führt CLI-Befehle (git, curl, ssh) aus.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync(command, args, workDir, env, ct)` | Befehl, Argumente, Arbeitsverzeichnis, Umgebungsvariablen | `Task<CliResult>` | Führt Befehl aus und gibt Ergebnis zurück |

**Verwendet von BitbucketPlugin für:**
- `git clone` – Mit Authentifizierung
- `git pull`
- `git push --set-upstream`
- `curl` – Bitbucket API 2.0 Requests
- `git ls-remote` – Remote-Branches und Default-Branch
- `curl` – Jira API 3 Requests

**Implementierung:** `CliRunner` (in `src/Softwareschmiede.Infrastructure/Services/`)

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

Zentrale Registry für alle verfügbaren Plugins.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins()` | - | `IReadOnlyList<IGitPlugin>` | Gibt alle geladenen SCM-Plugins zurück |
| `GetDevelopmentAutomationPlugins()` | - | `IReadOnlyList<IKiPlugin>` | Gibt alle geladenen KI-Plugins zurück |
| `GetDefaultSourceCodeManagementPlugin()` | - | `IGitPlugin` | Gibt das Standard-SCM-Plugin zurück |
| `GetDefaultDevelopmentAutomationPlugin()` | - | `IKiPlugin` | Gibt das Standard-KI-Plugin zurück |

**Implementierung:** `PluginManager` – lädt BitbucketPlugin automatisch beim Startup via Assembly-Discovery
