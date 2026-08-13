# Interfaces – Bestandsaufnahme IDE-Plugin-System

## Basis-Plugin-Interface

### `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Gemeinsame Basis aller Plugins (Git, KI, zukünftig: IDE). Definiert die minimalen Eigenschaften und Methoden.

| Mitglied | Typ | Beschreibung |
|----------|-----|-------------|
| `PluginName` | Property (string) | Eindeutiger Anzeigename des Plugins (z.B. "GitHub", "GitHub Copilot") |
| `PluginPrefix` | Property (string) | Präfix für Credential-Store-Schlüssel (z.B. "Softwareschmiede.GitHub"). Format: `<PluginPrefix>.<FieldKey>` |
| `PluginType` | Property (PluginType) | Plugin-Typ zur automatischen Zuordnung im PluginManager (`SourceCodeManagement`, `DevelopmentAutomation`) |
| `GetSettingGroups()` | Method | Gibt konfigurierbare Einstellungsgruppen mit ihren Feldern zurück. Bestimmt die UI-Anzeigereihenfolge |

---

## Git-Plugin-Interface

### `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

Erbt von `IPlugin`. Definiert die Schnittstelle für Source-Code-Management-Plugins (z.B. GitHub, GitLab).

| Methode | Rückgabe | Zweck |
|---------|----------|-------|
| `GetRepositoryLinkFields()` | `IReadOnlyList<PluginSettingField>` | Felder für projektbezogene Repository-Verknüpfung |
| `GetIssuesAsync(repositoryId, ct)` | `Task<IEnumerable<Issue>>` | Ruft Issues aus dem Repository ab |
| `CloneRepositoryAsync(repositoryUrl, targetPath, ct)` | `Task` | Klont ein Repository |
| `CreateBranchAsync(localPath, branchName, sourceBranchName, ct)` | `Task` | Legt einen neuen Branch an |
| `PushBranchAsync(localPath, branchName, ct)` | `Task` | Pusht den Branch auf den Remote |
| `PullAsync(localPath, ct)` | `Task` | Holt Änderungen vom Remote |
| `CreatePullRequestAsync(...)` | `Task<PullRequest>` | Erstellt einen Pull Request |
| `GetPullRequestStatusAsync(...)` | `Task<PullRequestStatusInfo>` | Ruft den Status eines Pull Requests ab (ggf. NotSupported) |
| `GetPullRequestWorkflowRunsAsync(...)` | `Task<IReadOnlyList<PullRequestWorkflowRunInfo>>` | Ruft Workflow-Runs ab (ggf. NotSupported) |
| `CompletePullRequestAsync(...)` | `Task<PullRequestCompletionResult>` | Schliesst einen Pull Request ab (ggf. NotSupported) |
| `CommitAsync(localPath, message, ct)` | `Task` | Führt einen Commit durch |
| `ResetAsync(localPath, resetType, targetRef, ct)` | `Task` | Setzt Commits zurück |
| `CheckHealthAsync(ct)` | `Task<bool>` | Prüft ob das Plugin verfügbar ist (CLI installiert, Token gültig) |
| `GetRemoteBranchesAsync(repositoryUrl, ct)` | `Task<IEnumerable<string>>` | Listet Remote-Branches auf (ohne Klon) |
| `GetDefaultBranchAsync(repositoryUrl, ct)` | `Task<string>` | Ermittelt den Standard-Branch |
| `CheckoutRemoteBranchAsync(localPath, branchName, ct)` | `Task` | Wechselt zu einem Remote-Branch |
| `GetGitActionCapabilitiesAsync(localPath, ct)` | `Task<GitActionCapabilities>` | Liefert verfügbare Git-Aktionen für die UI |
| `MergeToSourceAsync(localPath, ct)` | `Task` | Übernimmt lokale Änderungen ins Quellverzeichnis (ggf. NotSupported) |
| `GetAvailableRepositoriesAsync(ct)` | `Task<IEnumerable<AvailableRepository>>` | Liefert verfügbare Repositories aus der externen Quelle |
| `GetRepositoryStructureAsync(repositoryUrl, maxDepth, ct)` | `Task<IEnumerable<RepositoryDirectoryEntry>>` | Ruft Verzeichnisstruktur ab (ggf. NotSupported) |
| `GetRepositoryStructureLoadResultAsync(repositoryUrl, maxDepth, ct)` | `Task<RepositoryStructureLoadResult>` | Ruft Verzeichnisstruktur mit expliziter Fehlerbehandlung ab |
| `ResolveEffectiveRepositoryPathAsync(localPath, ct)` | `Task<string>` | Löst den tatsächlichen Repository-Pfad auf (für indirekte Workspace-Mappings) |

---

## KI-Plugin-Interface

### `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

Erbt von `IPlugin`. Definiert die Schnittstelle für Development-Automation-Plugins (z.B. GitHub Copilot, Claude CLI).

| Methode | Rückgabe | Zweck |
|---------|----------|-------|
| `StartCliAsync(localRepoPath, parameters, ct)` | `Task<ProcessStartInfo>` | Startet den CLI-Prozess mit optionalen Parametern |
| `GetProcessWindowTitle(aufgabeId)` | `string` | Gibt einen Hinweis auf den erwarteten Fenstertitel |
| `SupportsSessionContinuation()` | `bool` | Gibt an, ob das Plugin Session-Fortsetzung unterstützt |
| `CheckHealthAsync(ct)` | `Task<bool>` | Prüft ob das Plugin verfügbar ist |

---

## Manager-Interface

### `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

Verwaltet Discovery und Zugriff auf geladene Plugins.

| Methode | Rückgabe | Zweck |
|---------|----------|-------|
| `GetSourceCodeManagementPlugins()` | `IReadOnlyList<IGitPlugin>` | Gibt alle geladenen SCM-Plugins zurück |
| `GetDevelopmentAutomationPlugins()` | `IReadOnlyList<IKiPlugin>` | Gibt alle geladenen Development-Automation-Plugins zurück |
| `GetDefaultSourceCodeManagementPlugin()` | `IGitPlugin` | Gibt das erste verfügbare SCM-Plugin zurück |
| `GetDefaultDevelopmentAutomationPlugin()` | `IKiPlugin` | Gibt das priorisierte Development-Automation-Plugin zurück (Copilot bevorzugt) |

**Zu erweitern laut Anforderung:**
- `GetIdePlugins()` → `IReadOnlyList<IIdePlugin>`
- `GetDefaultIdePlugin()` → `IIdePlugin`

---

## Weitere bestehende Plugin-Interfaces

### `IVisualStudioCodeLocator`
Datei: `src/Softwareschmiede/Application/Services/IVisualStudioCodeLocator.cs`

Ermittelt, ob Visual Studio Code auf dem System startbar ist.

| Methode | Rückgabe | Zweck |
|---------|----------|-------|
| `Locate()` | `VisualStudioCodeAvailability` | Liefert den startbaren VS-Code-Befehl oder -Pfad, falls verfügbar |

**VisualStudioCodeAvailability-Record:**
- `IsAvailable` (bool) – Gibt an, ob VS Code startbar ist
- `ExecutablePath` (string?) – Der startbare Befehl oder Pfad, falls verfügbar

---

## Zu implementierende neue Interfaces

### `IIdePlugin` (NEU)
Laut Anforderung zu erstellen in: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`

Erbt von `IPlugin`. Definiert die Schnittstelle für IDE-Integrationen.

| Methode | Rückgabe | Zweck |
|---------|----------|-------|
| `CheckCompatibilityAsync(repositoryPath, ct)` | `Task<IdePluginCompatibility>` | Prüft Kompatibilität mit dem Repository (`Explicit`, `Fallback`, `Incompatible`) |
| `OpenRepositoryAsync(repositoryPath, ct)` | `Task` | Öffnet das Repository in der IDE |

