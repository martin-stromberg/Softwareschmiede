# Interfaces

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRepositoryLinkFields()` | — | `IReadOnlyList<PluginSettingField>` | Liefert die Felder für die projektbezogene Repository-Verknüpfung |
| `GetIssuesAsync` | `string repositoryId`, `CancellationToken ct` | `Task<IEnumerable<Issue>>` | **Ruft Issues aus dem Repository ab** |
| `CloneRepositoryAsync` | `string repositoryUrl`, `string targetPath`, `CancellationToken ct` | `Task` | Klont ein Repository in das Zielverzeichnis |
| `CreateBranchAsync` | `string localPath`, `string branchName`, `CancellationToken ct` | `Task` | Legt einen neuen Branch im lokalen Klon an |
| `PushBranchAsync` | `string localPath`, `string branchName`, `CancellationToken ct` | `Task` | Pusht den Branch auf den Remote |
| `PullAsync` | `string localPath`, `CancellationToken ct` | `Task` | Holt Änderungen vom Remote |
| `CreatePullRequestAsync` | `string repositoryId`, `string branchName`, `string title`, `string body`, `CancellationToken ct` | `Task<PullRequest>` | Erstellt einen Pull Request |
| `CommitAsync` | `string localPath`, `string message`, `CancellationToken ct` | `Task` | Führt einen Commit durch |
| `ResetAsync` | `string localPath`, `string resetType`, `string? targetRef`, `CancellationToken ct` | `Task` | Setzt Commits zurück |
| `CheckHealthAsync` | `CancellationToken ct` | `Task<bool>` | Prüft ob Plugin verfügbar ist |
| `GetRemoteBranchesAsync` | `string repositoryUrl`, `CancellationToken ct` | `Task<IEnumerable<string>>` | Listet alle Remote-Branches auf |
| `GetDefaultBranchAsync` | `string repositoryUrl`, `CancellationToken ct` | `Task<string>` | Ermittelt Standard-Branch |
| `CheckoutRemoteBranchAsync` | `string localPath`, `string branchName`, `CancellationToken ct` | `Task` | Wechselt zu vorhandenem Remote-Branch |
| `GetGitActionCapabilitiesAsync` | `string? localPath`, `CancellationToken ct` | `Task<GitActionCapabilities>` | Liefert verfügbare Git-Aktionen für UI |
| `MergeToSourceAsync` | `string localPath`, `CancellationToken ct` | `Task` | Übernimmt lokale Änderungen ins Quellverzeichnis |
| `GetAvailableRepositoriesAsync` | `CancellationToken ct` | `Task<IEnumerable<AvailableRepository>>` | Liefert verfügbare Repositories aus externer Quelle |

**Erbt von:** `IPlugin`

**Hinweis:** `GetIssuesAsync` ist das zentrale Interface für diese Anforderung. Es ist bereits definiert und wird von den Git-Plugins implementiert.

---

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Dieses ist das Basis-Interface, von dem `IGitPlugin` erbt. Es definiert die grundlegenden Plugin-Eigenschaften und Methoden.

**Relevante Eigenschaften:**
- `PluginName` — Name des Plugins
- `PluginPrefix` — Eindeutiger Prefix des Plugins
- `PluginType` — Typ des Plugins (z.B. `SourceCodeManagement`)
- `GetSettingGroups()` — Liefert die Konfigurationsgruppen des Plugins
