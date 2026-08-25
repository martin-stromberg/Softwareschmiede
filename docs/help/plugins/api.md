# Plugin-System — API

## `IPlugin`

Gemeinsame Basis aller Plugins.

| Member | Typ | Beschreibung |
|--------|-----|--------------|
| `PluginName` | `string` | Anzeigename (z.B. `"GitHub"`) |
| `PluginPrefix` | `string` | Eindeutiger Präfix für Credential-Store-Schlüssel |
| `PluginType` | `PluginType` | `SourceCodeManagement` oder `DevelopmentAutomation` |
| `GetSettingGroups()` | `IReadOnlyList<PluginSettingGroup>` | Einstellungsfelder für die UI |

---

## `IGitPlugin` : `IPlugin`

SCM-Operationen auf einem lokalen Repository-Klon.

| Methode | Parameter | Rückgabe | Beschreibung |
|---------|-----------|---------|--------------|
| `CloneRepositoryAsync` | `repositoryUrl`, `localPath`, `ct` | `Task` | Repository klonen |
| `CreateBranchAsync` | `localPath`, `branchName`, `ct` | `Task` | Branch anlegen und auschecken |
| `CheckoutRemoteBranchAsync` | `localPath`, `branchName`, `ct` | `Task` | Vorhandenen Remote-Branch auschecken |
| `GetDefaultBranchAsync` | `repositoryUrl`, `ct` | `Task<string>` | Hauptbranch ermitteln |
| `GetRemoteBranchesAsync` | `repositoryUrl`, `ct` | `Task<IEnumerable<string>>` | Alle Remote-Branches |
| `CommitAsync` | `localPath`, `message`, `ct` | `Task` | Staged Changes committen |
| `PushBranchAsync` | `localPath`, `branchName`, `ct` | `Task` | Branch pushen |
| `PullAsync` | `localPath`, `ct` | `Task` | Änderungen vom Remote holen |
| `CreatePullRequestAsync` | `repositoryId`, `branchName`, `title`, `body`, `ct` | `Task<PullRequest>` | Pull Request erstellen |
| `GetPullRequestStatusAsync` | `repositoryId`, `pullRequestNumber`, `ct` | `Task<PullRequestStatusInfo>` | Status, Merge-Status und relevante SHAs eines Pull Requests abrufen. Default: `NotSupported`. |
| `GetPullRequestWorkflowRunsAsync` | `repositoryId`, `pullRequestNumber`, `headSha?`, `mergeCommitSha?`, `ct` | `Task<IReadOnlyList<PullRequestWorkflowRunInfo>>` | Zugeordnete Workflow-/Action-Runs eines Pull Requests abrufen. |
| `CompletePullRequestAsync` | `repositoryId`, `pullRequestNumber`, `options`, `ct` | `Task<PullRequestCompletionResult>` | Pull Request nach konfigurierter Strategie abschliessen, Auto-Merge aktivieren oder genehmigen. Default: `NotSupported`. |
| `ResetAsync` | `localPath`, `resetType`, `targetRef?`, `ct` | `Task` | Git-Reset ausführen |
| `GetRepositoryStructureAsync` | `repositoryUrl`, `maxDepth`, `ct`, `branchName?` | `Task<IEnumerable<RepositoryDirectoryEntry>>` | Kompatibilitätsmethode für direkte Aufrufer: liefert Verzeichniseinträge aus dem angegebenen Branch (oder Remote-Standard-Branch, wenn `branchName` nicht gesetzt) oder wirft `NotSupportedException`, wenn das Plugin keinen Strukturabruf unterstützt. |
| `GetRepositoryStructureLoadResultAsync` | `repositoryUrl`, `maxDepth`, `ct`, `branchName?` | `Task<RepositoryStructureLoadResult>` | Bevorzugte Methode für UI und Services: liefert Verzeichnisstruktur aus dem angegebenen Branch (oder Remote-Standard-Branch, wenn `branchName` nicht gesetzt) mit expliziter Erfolg-/Fehlersemantik für die Arbeitsverzeichnis-Auswahl und zur Auswahl von Skriptdateien. Die Default-Implementierung ruft `GetRepositoryStructureAsync` auf und wandelt Erfolg, `NotSupportedException` und sonstige Fehler in ein Result um. |

---

## `IScmAlertProvider`

Optionale Erweiterung für SCM-Plugins, die Sicherheits- oder Qualitätsalerts als eigene Anforderungsart liefern können.

| Methode | Parameter | Rückgabe | Beschreibung |
|---------|-----------|---------|--------------|
| `GetAlertsAsync` | `repositoryId`, `ct` | `Task<IEnumerable<ScmAlert>>` | Liefert offene Alerts für ein Repository. Plugins ohne Alert-Unterstützung geben eine leere Liste zurück. |

GitHub implementiert diese Erweiterung initial für Code-Scanning-Alerts. Bitbucket/Jira und andere Provider sind dadurch nicht verpflichtet, Alerts bereitzustellen.

---

## `IKiPlugin` : `IPlugin`

KI-Entwicklungsautomatisierung.

| Methode | Parameter | Rückgabe | Beschreibung |
|---------|-----------|---------|--------------|
| `GetAvailableAgentsAsync` | `agentPackagePath`, `ct` | `Task<IEnumerable<AgentInfo>>` | Verfügbare Agenten aus Paket lesen |
| `IsAgentPackageCompatibleAsync` | `agentPackagePath`, `ct` | `Task<bool>` | Kompatibilität des Pakets prüfen |
| `DeployAgentPackageAsync` | `agentPackagePath`, `localRepoPath`, `ct` | `Task` | Paket ins Repository deployen |
| `StartDevelopmentAsync` | `prompt`, `agent`, `localRepoPath`, `model?`, `ct` | `IAsyncEnumerable<string>` | KI starten, Ausgabe streamen |
| `RunTestsAsync` | `localRepoPath`, `ct` | `Task<TestResult>` | Tests ausführen |
| `CheckHealthAsync` | `ct` | `Task<bool>` | Plugin-Verfügbarkeit prüfen |

---

## `CliKiPluginBase`

Abstrakte Basisklasse für CLI-basierte KI-Plugins (`ClaudeCliPlugin`, `GitHubCopilotPlugin`, `CodexPlugin`).

| Member | Beschreibung |
|--------|--------------|
| `ProviderDateiPraefix` | Provider-Kürzel für Dateinamen (`claude`, `copilot`) |
| `BuildContextFilePath(localRepoPath)` | Pfad zur nächsten freien Kontextdatei |
| `GetLatestContextFilePath(localRepoPath)` | Pfad zur zuletzt erzeugten Kontextdatei |
| `ClearContextFiles(localRepoPath)` | Alle Kontextdateien löschen |
| `MarkPromptToIncludeContextFile(prompt)` | Prompt mit `[[INCLUDE_CONTEXT_FILE_REFERENCE]]`-Marker versehen |
| `UnwrapPromptContextMarker(prompt)` | Marker auslösen, `IncludeContext`-Flag zurückgeben |
| `EnsureGitignoreEntries(path)` | `.gitignore` um task- und context-Dateimuster ergänzen |

CLI-basierte Plugins können zusätzliche Startargumente über das Feld `CommandLineParameters` beziehen. Für `Softwareschmiede.Codex.CommandLineParameters` gilt: Nur ein gespeicherter Anwenderwert wird verwendet; die Settings-UI übernimmt keinen `DefaultValue` als automatischen Codex-Parameter.

---

## Value Objects

### `AgentInfo`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Name` | `string` | Anzeigename des Agenten |
| `Beschreibung` | `string?` | Erste Zeile der Agent-Definitionsdatei |
| `Pfad` | `string` | Absoluter Pfad zur Agent-Definitionsdatei |

### `PullRequest`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Nummer` | `int` | PR-Nummer |
| `Titel` | `string` | PR-Titel |
| `Url` | `string` | Direkt-URL zum PR |
| `BranchName` | `string` | Name des Quell-Branches |
| `Provider` | `PullRequestProvider` | Provider des Pull Requests, initial `GitHub` |
| `RepositoryId` | `string?` | Repository-Identifier beim Provider |
| `ProviderPullRequestId` | `string?` | Optionale eindeutige Provider-ID |
| `TargetBranch` | `string?` | Name des Ziel-Branches |
| `HeadSha` | `string?` | Head-SHA des Pull Requests |

### `PullRequestStatusInfo`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Status` | `PullRequestStatus` | Aktueller PR-Status |
| `MergeStatus` | `PullRequestMergeStatus` | Mergebarkeit beziehungsweise Merge-Status |
| `HeadSha` | `string?` | Aktuelle Head-SHA |
| `MergeCommitSha` | `string?` | Merge-Commit-SHA, sofern bekannt |
| `LastUpdatedUtc` | `DateTimeOffset?` | Providerzeitpunkt der letzten Aktualisierung, sofern bekannt |

### `PullRequestWorkflowRunInfo`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `ProviderRunId` | `string` | Run-ID beim Provider |
| `Name` | `string` | Workflow- oder Check-Name |
| `Url` | `string?` | Direkt-URL zum Run |
| `HeadSha` | `string?` | Head-SHA des Runs |
| `BranchName` | `string?` | Branch des Runs |
| `Status` | `WorkflowRunStatus` | Laufstatus |
| `Conclusion` | `WorkflowRunConclusion` | Abschlussbewertung |
| `StartedAtUtc` | `DateTimeOffset?` | Startzeitpunkt, sofern bekannt |
| `CompletedAtUtc` | `DateTimeOffset?` | Abschlusszeitpunkt, sofern bekannt |

### `PullRequestCompletionOptions`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Strategy` | `PullRequestCompletionStrategy` | `Merge`, `AutoMerge` oder `ApprovalOnly` |
| `MergeMethod` | `PullRequestMergeMethod` | `Merge`, `Squash` oder `Rebase` |
| `AllowProtectedBranchBypass` | `bool` | Erlaubt administrativen Bypass, wenn Provider und Token dies unterstuetzen |

### `PullRequestCompletionResult`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Success` | `bool` | Abschlussaufruf wurde technisch erfolgreich ausgefuehrt |
| `PullRequestMerged` | `bool` | Pull Request ist tatsaechlich gemergt |
| `Blocked` | `bool` | Abschluss wurde durch Rechte, Regeln oder Voraussetzungen blockiert |
| `Message` | `string?` | Providerausgabe oder sichtbarer Fehler |
| `MergeCommitSha` | `string?` | Merge-Commit-SHA, sofern nach Abschluss bekannt |

### `TestResult`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Bestanden` | `bool` | `true` wenn alle Tests bestanden |
| `Ergebnisse` | `IReadOnlyList<TestErgebnisInfo>` | Einzelne Testergebnisse |

### `RepositoryDirectoryEntry`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Path` | `string` | Relativer Pfad des Eintrags innerhalb des Repositories, `/`-getrennt |
| `IsDirectory` | `bool` | `true` für Verzeichnisse, `false` für Dateien. Datei-Einträge werden verwendet für die Auswahl von Initialisierungsskripten; Verzeichnisse für die Arbeitsverzeichnis-Auswahl. |

### `RepositoryStructureLoadResult`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Status` | `RepositoryStructureLoadStatus` | Ergebnisstatus des Strukturabrufs |
| `Entries` | `IReadOnlyList<RepositoryDirectoryEntry>` | Geladene Verzeichniseinträge; bei Fehlern leer |
| `Message` | `string?` | Optionale Fehler- oder Hinweismeldung |

Factory-Methoden:
- `Success(entries)` — Abruf war erfolgreich, auch wenn `entries` leer ist.
- `Failed(message)` — technischer Fehler beim Abruf.
- `NotSupported(message)` — Plugin oder Funktion unterstützt den Strukturabruf nicht.

### `RepositoryStructureLoadStatus`

| Wert | Bedeutung |
|------|-----------|
| `Success` | Verzeichnisstruktur wurde erfolgreich geladen. Eine leere Liste bedeutet ein gültiges leeres Repository oder keine Unterverzeichnisse. |
| `Failed` | Abruf ist technisch fehlgeschlagen, z. B. wegen Berechtigungen, Netzwerk oder API-Fehlern. |
| `NotSupported` | Das Plugin oder die aktuelle Konfiguration unterstützt den Abruf nicht. |

### `ScmRequirement`

Gemeinsamer UI- und Workflow-Typ für offene Anforderungen aus einem SCM-Plugin.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Kind` | `ScmRequirementKind` | `Issue` oder `Alert` |
| `Issue` | `Issue?` | Gefüllt bei normalen SCM-Issues |
| `Alert` | `ScmAlert?` | Gefüllt bei SCM-Alerts |

### `ScmAlert`

Providerunabhängiges Value Object für Sicherheits- und Qualitätsalerts.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `SourceKey` | `string` | Stabile, providerweit eindeutige Kennung, z. B. für GitHub-Code-Scanning-Alerts |
| `AlertType` | `ScmAlertType` | Alert-Art, initial `CodeScanning` |
| `Title` | `string` | Anzeigename des Alerts |
| `Description` | `string?` | Beschreibung oder Nachricht aus dem Provider |
| `AlertUrl` | `string?` | Direkt-URL zum Alert |
| `Severity` | `string?` | Severity des Alerts |
| `State` | `string?` | Providerstatus |
| `ToolName` | `string?` | Meldendes Tool |
| `RuleId` | `string?` | Regelkennung |
| `RuleName` | `string?` | Regelname |
| `FilePath` | `string?` | Betroffene Datei |
| `StartLine` | `int?` | Betroffene Startzeile |

### `ScmRequirementKind`

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Normales SCM-Issue |
| `Alert` | SCM-Alert, z. B. GitHub Code Scanning |

### `ScmAlertType`

| Wert | Bedeutung |
|------|-----------|
| `CodeScanning` | GitHub-Code-Scanning-Alert |
