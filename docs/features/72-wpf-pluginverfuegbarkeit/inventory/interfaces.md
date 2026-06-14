# Bestandsaufnahme: Interfaces

## `IDialogService`
Datei: `src/Softwareschmiede.App/Services/IDialogService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `BestaetigenDialog` | `string nachricht`, `string titel` | `bool` | Zeigt eine Bestätigungsabfrage (Ja/Nein) und gibt Benutzerentscheidung zurück |
| `RepositoryZuweisenDialog` | `RepositoryAssignViewModel viewModel` | `bool` | Öffnet den Repository-Zuweisungs-Dialog und gibt zurück, ob Benutzer bestätigt hat |

**Implementierung:** `WpfDialogService` (Datei: `src/Softwareschmiede.App/Services/WpfDialogService.cs`)
- `BestaetigenDialog` zeigt MessageBox mit Yes/No-Optionen
- `RepositoryZuweisenDialog` erstellt `RepositoryAssignDialog`, setzt Owner auf Main Window und zeigt ihn als Dialog an (Thread-sicher via Dispatcher)

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

**Erbt von: `IPlugin`**

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRepositoryLinkFields` | keine | `IReadOnlyList<PluginSettingField>` | Liefert Felder für projektbezogene Repository-Verknüpfung (Labels, Placeholder, Validation) |
| `GetIssuesAsync` | `string repositoryId`, `CancellationToken ct` | `Task<IEnumerable<Issue>>` | Ruft Issues aus dem Repository ab |
| `CloneRepositoryAsync` | `string repositoryUrl`, `string targetPath`, `CancellationToken ct` | `Task` | Klont Repository in Zielverzeichnis |
| `CreateBranchAsync` | `string localPath`, `string branchName`, `CancellationToken ct` | `Task` | Legt neuen Branch im lokalen Klon an |
| `PushBranchAsync` | `string localPath`, `string branchName`, `CancellationToken ct` | `Task` | Pusht Branch auf Remote |
| `PullAsync` | `string localPath`, `CancellationToken ct` | `Task` | Holt Änderungen vom Remote |
| `CreatePullRequestAsync` | `string repositoryId`, `string branchName`, `string title`, `string body`, `CancellationToken ct` | `Task<PullRequest>` | Erstellt Pull Request |
| `CommitAsync` | `string localPath`, `string message`, `CancellationToken ct` | `Task` | Führt Commit durch |
| `ResetAsync` | `string localPath`, `string resetType`, `string? targetRef`, `CancellationToken ct` | `Task` | Setzt Commits zurück |
| `CheckHealthAsync` | `CancellationToken ct` | `Task<bool>` | Prüft ob Plugin verfügbar ist (CLI, Token, etc.) |
| `GetRemoteBranchesAsync` | `string repositoryUrl`, `CancellationToken ct` | `Task<IEnumerable<string>>` | Listet Remote-Branches ohne Klon auf |
| `GetDefaultBranchAsync` | `string repositoryUrl`, `CancellationToken ct` | `Task<string>` | Ermittelt Standard-Branch (z.B. "main", "master") |
| `CheckoutRemoteBranchAsync` | `string localPath`, `string branchName`, `CancellationToken ct` | `Task` | Wechselt zu Remote-Branch |
| `GetGitActionCapabilitiesAsync` | `string? localPath`, `CancellationToken ct` | `Task<GitActionCapabilities>` | Liefert verfügbare Git-Aktionen für die UI |
| `MergeToSourceAsync` | `string localPath`, `CancellationToken ct` | `Task` | Übernimmt lokale Änderungen ins Quellverzeichnis (throws NotSupportedException) |

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `PluginName` | `string` | Eindeutiger Anzeigename des Plugins (z.B. "GitHub", "GitHub Copilot") |
| `PluginPrefix` | `string` | Präfix für Credential-Store-Schlüssel (z.B. "Softwareschmiede.GitHub") |
| `PluginType` | `PluginType` | Plugin-Typ für automatische Zuordnung im PluginManager |

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSettingGroups` | keine | `IReadOnlyList<PluginSettingGroup>` | Gibt Einstellungsgruppen mit Feldern zurück (bestimmt Anzeigereihenfolge) |

## Hinweise

- `IGitPlugin` ist die zentrale Schnittstelle für Source-Code-Management-Plugins
- `IPlugin` ist die Basis-Schnittstelle für alle Plugin-Typen
- `IDialogService` abstrahiert Dialoge für MVVM-Pattern
- `IGitPlugin` enthält bereits `GetRepositoryLinkFields()` für Plugin-spezifische UI-Felder
