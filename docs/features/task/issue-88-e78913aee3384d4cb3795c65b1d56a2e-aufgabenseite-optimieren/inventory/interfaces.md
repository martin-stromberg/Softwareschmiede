# Interfaces

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

| Methode | Parameter | Rueckgabewert | Zweck |
|---------|-----------|---------------|-------|
| `PluginName` | - | `string` | Anzeigename des Plugins, z. B. fuer UI-Kontext. |
| `PluginPrefix` | - | `string` | Technischer Prefix fuer Plugin-Aufloesung und Einstellungen. |
| `GetSettingGroups` | - | `IReadOnlyList<PluginSettingGroup>` | Liefert konfigurierbare Plugin-Einstellungen. |
| `PluginType` | - | `PluginType` | Ordnet Plugin einer Kategorie zu. |

## `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

| Methode | Parameter | Rueckgabewert | Zweck |
|---------|-----------|---------------|-------|
| `StartCliAsync` | `string localRepoPath`, `string? parameters`, `CancellationToken ct` | `Task<ProcessStartInfo>` | Liefert Startinformationen fuer den CLI-Prozess. |
| `GetProcessWindowTitle` | `Guid aufgabeId` | `string` | Liefert erwarteten Fenstertitel des CLI-Prozesses. |
| `SupportsSessionContinuation` | - | `bool` | Gibt an, ob Session-Fortsetzung unterstuetzt wird. |
| `CheckHealthAsync` | `CancellationToken ct` | `Task<bool>` | Prueft Verfuegbarkeit des KI-Plugins. |

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

| Methode | Parameter | Rueckgabewert | Zweck |
|---------|-----------|---------------|-------|
| `GetIssuesAsync` | `string repositoryId`, `CancellationToken ct` | `Task<IEnumerable<Issue>>` | Ruft Issues aus einem Repository ab. |
| `CloneRepositoryAsync` | `string repositoryUrl`, `string targetPath`, `CancellationToken ct` | `Task` | Klont ein Repository fuer die Aufgabe. |
| `CreateBranchAsync` | `string localPath`, `string branchName`, `CancellationToken ct` | `Task` | Legt Task-Branch an. |
| `CheckoutRemoteBranchAsync` | `string localPath`, `string branchName`, `CancellationToken ct` | `Task` | Wechselt auf vorhandenen Remote-Branch. |
| `GetDefaultBranchAsync` | `string repositoryUrl`, `CancellationToken ct` | `Task<string>` | Ermittelt Default-Branch. |
| `GetGitActionCapabilitiesAsync` | `string? localPath`, `CancellationToken ct` | `Task<GitActionCapabilities>` | Liefert verfuegbare Git-Aktionen fuer die UI. |
| `ResolveEffectiveRepositoryPathAsync` | `string localPath`, `CancellationToken ct` | `Task<string>` | Loest tatsaechlichen Repository-Pfad fuer lokale Plugin-Varianten auf. |
