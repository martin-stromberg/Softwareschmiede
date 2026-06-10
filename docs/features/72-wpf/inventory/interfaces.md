# Interfaces & Contracts

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Basis-Interface für alle Plugins. Definiert Namen und konfigurierbare Einstellungsfelder.

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `PluginName` { get; } | - | `string` | Eindeutiger Anzeigename des Plugins (z.B. "GitHub") |
| `PluginPrefix` { get; } | - | `string` | Präfix für Credential-Store-Schlüssel (z.B. "Softwareschmiede.GitHub") |
| `PluginType` { get; } | - | `PluginType` | Plugin-Typ (SourceCodeManagement oder DevelopmentAutomation) |
| `GetSettingGroups()` | - | `IReadOnlyList<PluginSettingGroup>` | Gibt Einstellungsgruppen mit Feldern zurück |

**Verwendet von:** `IGitPlugin`, `IKiPlugin` erben von diesem Interface.

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

Git-Provider Plugin Interface mit Operationen für Repository-Verwaltung, Branch-Management und Pull Requests.

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `GetRepositoryLinkFields()` | - | `IReadOnlyList<PluginSettingField>` | Felder für projektbezogene Repository-Verknüpfung (default: []) |
| `GetIssuesAsync(repositoryId, ct)` | string, CancellationToken | `Task<IEnumerable<Issue>>` | Ruft Issues aus dem Repository ab |
| `CloneRepositoryAsync(repositoryUrl, targetPath, ct)` | string, string, CancellationToken | `Task` | Klont ein Repository in das Zielverzeichnis |
| `CreateBranchAsync(localPath, branchName, ct)` | string, string, CancellationToken | `Task` | Legt einen neuen Branch an |
| `PushBranchAsync(localPath, branchName, ct)` | string, string, CancellationToken | `Task` | Pusht Branch auf Remote |
| `PullAsync(localPath, ct)` | string, CancellationToken | `Task` | Holt Änderungen vom Remote |
| `CreatePullRequestAsync(repositoryId, branchName, title, body, ct)` | string, string, string, string, CancellationToken | `Task<PullRequest>` | Erstellt einen Pull Request |
| `CommitAsync(localPath, message, ct)` | string, string, CancellationToken | `Task` | Führt einen Commit durch |
| `ResetAsync(localPath, resetType, targetRef, ct)` | string, string, string?, CancellationToken | `Task` | Setzt Commits zurück |
| `CheckHealthAsync(ct)` | CancellationToken | `Task<bool>` | Prüft ob Plugin verfügbar ist |
| `GetRemoteBranchesAsync(repositoryUrl, ct)` | string, CancellationToken | `Task<IEnumerable<string>>` | Listet alle Remote-Branches auf |
| `GetDefaultBranchAsync(repositoryUrl, ct)` | string, CancellationToken | `Task<string>` | Ermittelt den Standard-Branch |
| `CheckoutRemoteBranchAsync(localPath, branchName, ct)` | string, string, CancellationToken | `Task` | Wechselt zu vorhandenem Remote-Branch |
| `GetGitActionCapabilitiesAsync(localPath, ct)` | string?, CancellationToken | `Task<GitActionCapabilities>` | Liefert verfügbare Git-Aktionen für UI (default impl.) |
| `MergeToSourceAsync(localPath, ct)` | string, CancellationToken | `Task` | Übernimmt lokale Änderungen ins Quellverzeichnis (throws NotSupportedException default) |

## `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

KI-Plugin Interface für KI-gestützte Entwicklung und Agentenpaket-Management.

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `GetAvailableAgentsAsync(agentPackagePath, ct)` | string, CancellationToken | `Task<IEnumerable<AgentInfo>>` | Gibt verfügbare Agenten im Agentenpaket zurück |
| `IsAgentPackageCompatibleAsync(agentPackagePath, ct)` | string, CancellationToken | `Task<bool>` | Prüft ob Agentenpaket mit Plugin kompatibel ist |
| `DeployAgentPackageAsync(agentPackagePath, localRepoPath, ct)` | string, string, CancellationToken | `Task` | Kopiert Agentenpaket ins Repository |
| `StartDevelopmentAsync(prompt, agent, localRepoPath, model, ct)` | string, AgentInfo, string, string?, CancellationToken | `IAsyncEnumerable<string>` | Startet KI-Entwicklungsprozess und streamt Ausgabe |
| `RunTestsAsync(localRepoPath, ct)` | string, CancellationToken | `Task<TestResult>` | Führt Tests im Repository aus |
| `CheckHealthAsync(ct)` | CancellationToken | `Task<bool>` | Prüft ob Plugin verfügbar ist |

## `ICredentialStore`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs`

Interface für sichere Speicherung und Abruf von Credentials (API-Keys, Tokens, etc.).

Wird verwendet von `PluginSettingsService` zur Speicherung von Plugin-Einstellungen.

## `ICliRunner`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`

Interface für Ausführung von CLI-Befehlen mit Streaming-Output.

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

Verwaltung geladener Plugins.

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `GetSourceCodeManagementPlugins()` | - | `IReadOnlyList<IGitPlugin>` | Gibt alle Git-Plugins zurück |
| `GetDevelopmentAutomationPlugins()` | - | `IReadOnlyList<IKiPlugin>` | Gibt alle KI-Plugins zurück |
| `GetDefaultSourceCodeManagementPlugin()` | - | `IGitPlugin` | Gibt Default Git-Plugin zurück (oder Exception) |
| `GetDefaultDevelopmentAutomationPlugin()` | - | `IKiPlugin` | Gibt Default KI-Plugin zurück (oder Exception) |

## `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`

Interface für Abfrage von laufenden KI-Ausführungen. Wird von `KiAusfuehrungsService` implementiert und von `AufgabeRecoveryService` genutzt.

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `IsRunning(aufgabeId)` | Guid | `bool` | Gibt an, ob für Aufgabe aktuell KI-Ausführung läuft |

## `IArbeitsverzeichnisResolver`
Datei: `src/Softwareschmiede/Domain/Interfaces/IArbeitsverzeichnisResolver.cs`

Auflösung von Arbeitsverzeichnis-Pfaden.

## `IAgentPackageService`
Datei: `src/Softwareschmiede/Domain/Interfaces/IAgentPackageService.cs`

Service zur Verwaltung von Agentenpaket-Informationen.

## `IAutoShutdownOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/IAutoShutdownOrchestrator.cs`

Orchestrierung von automatischem Herunterfahren.

## `ISystemShutdownService`
Datei: `src/Softwareschmiede/Domain/Interfaces/ISystemShutdownService.cs`

Service für Systemshutdown-Operationen.
