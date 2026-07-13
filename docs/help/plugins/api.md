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
| `ResetAsync` | `localPath`, `resetType`, `targetRef?`, `ct` | `Task` | Git-Reset ausführen |
| `GetRepositoryStructureAsync` | `repositoryUrl`, `maxDepth`, `ct` | `Task<IEnumerable<RepositoryDirectoryEntry>>` | Kompatibilitätsmethode für direkte Aufrufer: liefert Verzeichniseinträge oder wirft `NotSupportedException`, wenn das Plugin keinen Strukturabruf unterstützt. |
| `GetRepositoryStructureLoadResultAsync` | `repositoryUrl`, `maxDepth`, `ct` | `Task<RepositoryStructureLoadResult>` | Bevorzugte Methode für UI und Services: liefert Verzeichnisstruktur mit expliziter Erfolg-/Fehlersemantik für die Arbeitsverzeichnis-Auswahl. Die Default-Implementierung ruft `GetRepositoryStructureAsync` auf und wandelt Erfolg, `NotSupportedException` und sonstige Fehler in ein Result um. |

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

### `TestResult`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Bestanden` | `bool` | `true` wenn alle Tests bestanden |
| `Ergebnisse` | `IReadOnlyList<TestErgebnisInfo>` | Einzelne Testergebnisse |

### `RepositoryDirectoryEntry`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Path` | `string` | Relativer Pfad des Verzeichnisses innerhalb des Repositories, `/`-getrennt |
| `IsDirectory` | `bool` | Aktuell immer `true` — Datei-Einträge sind für die Arbeitsverzeichnis-Auswahl nicht relevant |

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
