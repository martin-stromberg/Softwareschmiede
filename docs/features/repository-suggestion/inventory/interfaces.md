# Interfaces

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins()` | — | `IReadOnlyList<IGitPlugin>` | Gibt alle geladenen SCM-Plugins zurück — **zentral für Repository-Aggregation** |
| `GetDevelopmentAutomationPlugins()` | — | `IReadOnlyList<IKiPlugin>` | Gibt alle Development-Automation-Plugins zurück |
| `GetDefaultSourceCodeManagementPlugin()` | — | `IGitPlugin` | Gibt das erste verfügbare SCM-Plugin zurück |
| `GetDefaultDevelopmentAutomationPlugin()` | — | `IKiPlugin` | Gibt das priorisierte Development-Automation-Plugin zurück |

---

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

### Kernmethoden für Repository-Suggestion

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAvailableRepositoriesAsync(CancellationToken)` | `ct` | `Task<IEnumerable<AvailableRepository>>` | **Zentral**: Liefert verfügbare Repositories aus der externen SCM-Quelle (z.B. GitHub API) |
| `GetIssuesAsync(string, CancellationToken)` | `repositoryId`, `ct` | `Task<IEnumerable<Issue>>` | Ruft Issues für ein Repository ab |

### Repository-Verwaltungsmethoden

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CloneRepositoryAsync(string, string, CancellationToken)` | `repositoryUrl`, `targetPath`, `ct` | `Task` | Klont ein Repository |
| `CreateBranchAsync(string, string, CancellationToken)` | `localPath`, `branchName`, `ct` | `Task` | Legt neuen Branch an |
| `PushBranchAsync(string, string, CancellationToken)` | `localPath`, `branchName`, `ct` | `Task` | Pusht Branch auf Remote |
| `PullAsync(string, CancellationToken)` | `localPath`, `ct` | `Task` | Holt Änderungen vom Remote |
| `CommitAsync(string, string, CancellationToken)` | `localPath`, `message`, `ct` | `Task` | Führt Commit durch |
| `ResetAsync(string, string, string?, CancellationToken)` | `localPath`, `resetType`, `targetRef`, `ct` | `Task` | Setzt Commits zurück |

### Branch-Verwaltungsmethoden

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRemoteBranchesAsync(string, CancellationToken)` | `repositoryUrl`, `ct` | `Task<IEnumerable<string>>` | Listet Remote-Branches auf (ohne Klon) |
| `GetDefaultBranchAsync(string, CancellationToken)` | `repositoryUrl`, `ct` | `Task<string>` | Ermittelt Standard-Branch |
| `CheckoutRemoteBranchAsync(string, string, CancellationToken)` | `localPath`, `branchName`, `ct` | `Task` | Wechselt zu Remote-Branch |

### Pull-Request-Verwaltung

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreatePullRequestAsync(string, string, string, string, CancellationToken)` | `repositoryId`, `branchName`, `title`, `body`, `ct` | `Task<PullRequest>` | Erstellt einen Pull Request |

### Weitere Methoden

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRepositoryLinkFields()` | — | `IReadOnlyList<PluginSettingField>` | Liefert Felder für Repository-Verknüpfung |
| `CheckHealthAsync(CancellationToken)` | `ct` | `Task<bool>` | Prüft ob Plugin verfügbar ist |
| `GetGitActionCapabilitiesAsync(string?, CancellationToken)` | `localPath`, `ct` | `Task<GitActionCapabilities>` | Liefert verfügbare Git-Aktionen für UI |
| `MergeToSourceAsync(string, CancellationToken)` | `localPath`, `ct` | `Task` | Übernimmt lokale Änderungen ins Quellverzeichnis |

### Erbt von
- `IPlugin` — Basis-Plugin-Interface mit `PluginPrefix`, `PluginName`, `PluginType`, etc.

---

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Basis-Interface mit allgemeinen Plugin-Eigenschaften:
- `PluginPrefix` — eindeutige Kennung des Plugins
- `PluginName` — Anzeigename
- `PluginType` — Kategorie (SourceCodeManagement, DevelopmentAutomation, etc.)
- `GetSettingGroups()` — Konfigurierbare Plugin-Einstellungen
