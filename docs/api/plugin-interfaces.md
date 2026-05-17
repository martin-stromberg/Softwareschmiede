# Plugin-Entwickler-Dokumentation

> **Zielgruppe:** C#-Entwickler, die neue Git- oder KI-Plugins für die Softwareschmiede implementieren möchten.

---

## Inhaltsverzeichnis

1. [Einleitung](#1-einleitung)
   - [1.1 Standardplugin, Pluginart, KI-Plugin-Auswahl und Fallback](#11-standardplugin-pluginart-ki-plugin-auswahl-und-fallback)
2. [IGitPlugin – Schnittstellenreferenz](#2-igitplugin--schnittstellenreferenz)
3. [IKiPlugin – Schnittstellenreferenz](#3-ikiplugin--schnittstellenreferenz)
4. [Neues Git-Plugin implementieren](#4-neues-git-plugin-implementieren)
5. [Neues KI-Plugin implementieren](#5-neues-ki-plugin-implementieren)
6. [Agentenpaket-Struktur](#6-agentenpaket-struktur)

---

## 1. Einleitung

Die Softwareschmiede ist eine Blazor Server-Anwendung für KI-gestützte Softwareentwicklung. Ihr **Plugin-System** entkoppelt die Kernlogik von konkreten Git-Diensten und KI-Systemen, sodass neue Anbieter ohne Änderungen an der bestehenden Anwendung ergänzt werden können.

Das System definiert zwei Plugin-Schnittstellen:

| Schnittstelle | Zweck | Referenzimplementierung |
|---|---|---|
| `IGitPlugin` | Git-Operationen (Issues, Clone, Branch, Push, PR, …) | `GitHubPlugin` |
| `IKiPlugin` | KI-Integration (Agenten, Entwicklung, Tests) | `GitHubCopilotPlugin`, `ClaudeCliPlugin` |

**Dateipfade der Verträge (Contracts):**
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

**Dateipfade der Referenzimplementierungen (Plugin-Projekte):**
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
- `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`

**Plugin-Discovery & DI:**
- `IPluginManager` wird als **Singleton** registriert und lädt Plugin-DLLs dynamisch aus `<AppBase>/plugins`.
- `IGitPlugin` und `IKiPlugin` werden als **Scoped** über den `PluginManager` auf die Default-Plugins aufgelöst.
- Die Zuordnung erfolgt über `IPlugin.PluginType` (`SourceCodeManagement`, `DevelopmentAutomation`).

### Unterstützte KI-Plugin-Implementierungen (Stand: claude-cli-integration)

| Implementierung | PluginName | CLI-Binary | Env-Variable für Token | CLI-Pfad-Konfiguration | Quellcode |
|---|---|---|---|---|---|
| `GitHubCopilotPlugin` | `GitHub Copilot` | `copilot` | `GH_TOKEN` | optional: `Softwareschmiede.GitHubCopilot.ExecutablePath` | `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` |
| `ClaudeCliPlugin` | `Claude CLI` | `claude` | `ANTHROPIC_API_KEY` | nicht vorgesehen | `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs` |

**Hinweis:** Beide Implementierungen erfüllen denselben `IKiPlugin`-Contract. Provider-spezifische Unterschiede (CLI-Parameter, Token-Variable, Health-Check) sind ausschließlich Implementierungsdetails.

### 1.1 Standardplugin, Pluginart, KI-Plugin-Auswahl und Fallback

Die Auswahl des effektiven Plugins erfolgt nicht im Plugin-Contract selbst, sondern in der Application-Schicht:

- **Pluginart:** `PluginType.SourceCodeManagement` oder `PluginType.DevelopmentAutomation`
- **Standardplugin:** Persistierter `PluginPrefix` je Pluginart
- **KI-Plugin-Auswahl:** Optionale explizite Auswahl (`selectedKiPluginPrefix`) beim Prompt-Start
- **Fallback:** Wenn Auswahl/Standard nicht gültig sind, wird ein verfügbares Plugin aufgelöst

Verbindliche Reihenfolge:
1. explizite Auswahl
2. gespeichertes Standardplugin
3. Fallback

Diese Logik ist im Detail dokumentiert unter:
- [plugin-default-selection.md](./plugin-default-selection.md)
- [plugin-default-selection-flow.md](../flows/plugin-default-selection-flow.md)
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](../business/features/F014-standardplugin-ki-plugin-auswahl.md)

---

## 2. IGitPlugin – Schnittstellenreferenz

```csharp
// src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs
public interface IGitPlugin : IPlugin
{
    IReadOnlyList<PluginSettingField> GetRepositoryLinkFields() => [];
    Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default);
    Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default);
    Task CreateBranchAsync(string localPath, string branchName, CancellationToken ct = default);
    Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default);
    Task PullAsync(string localPath, CancellationToken ct = default);
    Task<PullRequest> CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct = default);
    Task CommitAsync(string localPath, string message, CancellationToken ct = default);
    Task ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct = default);
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default);
    Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default);
    Task CheckoutRemoteBranchAsync(string localPath, string branchName, CancellationToken ct = default);
    Task<GitActionCapabilities> GetGitActionCapabilitiesAsync(string? localPath = null, CancellationToken ct = default);
    Task MergeToSourceAsync(string localPath, CancellationToken ct = default);
}
```

---

### `PluginName`

```csharp
string PluginName { get; }
```

**Beschreibung:** Eindeutiger Anzeigename des Plugins. Wird in der Benutzeroberfläche und in Log-Ausgaben verwendet.

| Element | Details |
|---|---|
| Typ | `string` |
| Beispielwert | `"GitHub"`, `"GitLab"` |

> **Hinweis:** Der Name sollte stabil und unveränderlich sein, da er ggf. zur Identifikation in Konfigurationen genutzt wird.

---

### Implementierungshinweis: `LocalDirectoryPlugin`

Für die konkrete `IGitPlugin`-Implementierung `LocalDirectoryPlugin` gelten provider-spezifische Abweichungen:

- **Unterstützt:** `CloneRepositoryAsync`, `CreateBranchAsync`, `CommitAsync`, `ResetAsync`, `CheckHealthAsync`, `PushBranchAsync` *(nur `SeparateWorkingDirectory`, als Push-Sync)*, `PullAsync` *(nur `SeparateWorkingDirectory`, als Pull-Sync ohne Merge)*
- **Nicht unterstützt (`NotSupportedException`):** `GetIssuesAsync`, `CreatePullRequestAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`, `CheckoutRemoteBranchAsync`
- **Capabilities für UI-Aktionsmatrix:** `GetGitActionCapabilitiesAsync` liefert `RepositoryKind.LocalDirectory` + Modus-Flags; bei `SeparateWorkingDirectory` wird `CanMergeToSource=true` für die sichtbare **Merge**-Aktion gesetzt.
- **Merge statt Push/Pull/PR in Arbeitskopie:** `MergeToSourceAsync` übernimmt Änderungen `WorkingDirectory -> SourceDirectory`; die Aufgabenansicht blendet Push/Pull/PR für lokale Arbeitskopien aus.
- **Konfigurations-/Guardrail-Details:** `WorkspaceMode`, `SourceDirectory`, `ConfirmGitInitInSourceDirectory`, `CopyTimeoutSeconds`, `CopyMaxFiles`, `CopyMaxMegabytes`
- **Git-Bootstrap:** Im separaten Workspace wird die Quelle kopiert, im Working Directory `git init` ausgeführt und ein initialer Snapshot-Commit angelegt.

Details: [local-directory-plugin.md](./local-directory-plugin.md)

---

### `GetRepositoryLinkFields`

```csharp
IReadOnlyList<PluginSettingField> GetRepositoryLinkFields() => [];
```

**Beschreibung:** Liefert das Schema für die projektbezogene Repository-Verknüpfung in der UI (`Projekte -> Repository verknüpfen`).

**Verhalten:**

- Die UI rendert die Eingabefelder dynamisch aus den zurückgegebenen `PluginSettingField`-Definitionen.
- Bei Pluginwechsel wird das Feldschema neu geladen; nicht mehr gültige Feldwerte werden verworfen.
- Die initiale Pluginauswahl nutzt die Auflösungskette  
  **explizite Auswahl -> gespeichertes SCM-Standardplugin -> Fallback**.

**Beispiele aus Referenzimplementierungen:**

- `GitHubPlugin`: `RepositoryUrl` (URL, Pflicht), `RepositoryName` (Text, Pflicht)
- `LocalDirectoryPlugin`: `SourceDirectory` (Text, Pflicht)

Die konkreten Feldwerte werden als `Dictionary<string, string>` an `ProjektService.AddRepositoryAsync(...)` übergeben.

---

### `GetIssuesAsync`

```csharp
Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default);
```

**Beschreibung:** Ruft alle offenen Issues eines Repositories ab.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `repositoryId` | `string` | *(required)* | Repository-Bezeichner im Format `owner/repo` (z. B. `"octocat/hello-world"`) |
| `ct` | `CancellationToken` | optional | Abbruch-Token für kooperativen Abbruch |

**Rückgabewert:** `Task<IEnumerable<Issue>>` – Auflistung der offenen Issues. Leere Liste, wenn keine Issues vorhanden sind.

**Datenmodell [`Issue`](#issue):**

```csharp
public sealed record Issue(
    int Nummer,
    string Titel,
    string? Body,
    List<string> Labels,
    string? Milestone,
    string? Url
);
```

**Fehlerverhalten:**
- `InvalidOperationException` – Repository nicht gefunden oder kein Zugriff.
- `OperationCanceledException` – Abbruch über `ct`.
- Netzwerkfehler sollten als aussagekräftige Exception weitergegeben werden.

---

### `CloneRepositoryAsync`

```csharp
Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default);
```

**Beschreibung:** Klont ein Remote-Repository in ein lokales Verzeichnis.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `repositoryUrl` | `string` | *(required)* | HTTPS- oder SSH-URL des Repositories (z. B. `"https://github.com/owner/repo.git"`) |
| `targetPath` | `string` | *(required)* | Absoluter lokaler Zielpfad, in den geklont werden soll |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task` – abgeschlossen, wenn das Klonen erfolgreich beendet wurde.

**Fehlerverhalten:**
- `InvalidOperationException` – Zielverzeichnis existiert bereits und ist nicht leer.
- `UnauthorizedAccessException` – Fehlende Schreibrechte auf `targetPath`.
- `OperationCanceledException` – Abbruch über `ct`.

---

### `CreateBranchAsync`

```csharp
Task CreateBranchAsync(string localPath, string branchName, CancellationToken ct = default);
```

**Beschreibung:** Erstellt einen neuen lokalen Branch im geklonten Repository und checkt ihn aus.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `localPath` | `string` | *(required)* | Absoluter Pfad zum lokalen Repository-Verzeichnis |
| `branchName` | `string` | *(required)* | Name des zu erstellenden Branches (z. B. `"feature/issue-42"`) |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task` – abgeschlossen, wenn Branch erstellt und ausgecheckt wurde.

**Hinweis zur Branch-Namensbildung:**  
Die eigentliche Branch-Namenskonvention wird in der Application-Schicht (`EntwicklungsprozessService`) erzeugt.  
Bei Aufgaben mit `IssueReferenz` wird das Muster `task/issue-<IssueNummer>-<AufgabeIdN>-<TitelSlug>` verwendet.

**Fehlerverhalten:**
- `InvalidOperationException` – Branch existiert bereits oder `localPath` ist kein gültiges Git-Repository.
- `OperationCanceledException` – Abbruch über `ct`.

---

### `PushBranchAsync`

```csharp
Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default);
```

**Beschreibung:** Pusht einen lokalen Branch in das Remote-Repository (`origin`).

> **Implementierungshinweis `LocalDirectoryPlugin`:**  
> Im `SeparateWorkingDirectory`-Modus ist dies **kein klassischer Remote-Push**, sondern ein **Push-Sync** (`WorkingDirectory -> SourceDirectory`) inkl. **Delete-Sync** über `git status --porcelain`.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `localPath` | `string` | *(required)* | Absoluter Pfad zum lokalen Repository-Verzeichnis |
| `branchName` | `string` | *(required)* | Name des zu pushenden Branches |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task` – abgeschlossen, wenn der Push erfolgreich war.

**Fehlerverhalten:**
- `InvalidOperationException` – Branch existiert lokal nicht oder Push wurde vom Remote abgelehnt (z. B. Force-Push ohne Berechtigung).
- `UnauthorizedAccessException` – Fehlende Push-Rechte (Token ungültig oder abgelaufen).
- `OperationCanceledException` – Abbruch über `ct`.

---

### `PullAsync`

```csharp
Task PullAsync(string localPath, CancellationToken ct = default);
```

**Beschreibung:** Aktualisiert das lokale Repository durch einen `git pull` vom Remote (`origin`).

> **Implementierungshinweis `LocalDirectoryPlugin`:**  
> Im `SeparateWorkingDirectory`-Modus ist dies ein **Pull-Sync** (`SourceDirectory -> WorkingDirectory`) **ohne Merge** (ff-only/rebase-äquivalentes Verhalten).  
> Bei Konflikt- oder manuellem Eingriffsbedarf (z. B. dirty Workspace) bricht der Ablauf ab; die Bereinigung muss manuell erfolgen.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `localPath` | `string` | *(required)* | Absoluter Pfad zum lokalen Repository-Verzeichnis |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task` – abgeschlossen, wenn das Pull erfolgreich war.

**Fehlerverhalten:**
- `InvalidOperationException` – Merge-Konflikt oder dirty working tree.
- `OperationCanceledException` – Abbruch über `ct`.

---

### `CreatePullRequestAsync`

```csharp
Task<PullRequest> CreatePullRequestAsync(
    string repositoryId,
    string branchName,
    string title,
    string body,
    CancellationToken ct = default);
```

**Beschreibung:** Erstellt einen Pull Request vom angegebenen Branch gegen den Standard-Branch des Repositories (in der Regel `main`).

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `repositoryId` | `string` | *(required)* | Repository-Bezeichner im Format `owner/repo` |
| `branchName` | `string` | *(required)* | Quell-Branch des Pull Requests |
| `title` | `string` | *(required)* | Titel des Pull Requests |
| `body` | `string` | *(required)* | Beschreibungstext des Pull Requests (Markdown unterstützt) |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task<PullRequest>` – Daten des erstellten Pull Requests.

**Hinweis zur Closing-Direktive:**  
Die Ergänzung von `Closes #<IssueNummer>` erfolgt in der Orchestrierung (`GitOrchestrationService.BuildPullRequestBody`), nicht im `IGitPlugin`-Contract selbst.

**Datenmodell [`PullRequest`](#pullrequest):**

```csharp
public sealed record PullRequest(
    int Nummer,
    string Titel,
    string Url,
    string BranchName
);
```

**Fehlerverhalten:**
- `InvalidOperationException` – Branch existiert nicht im Remote oder PR für diesen Branch ist bereits offen.
- `UnauthorizedAccessException` – Fehlende Schreibrechte.
- `OperationCanceledException` – Abbruch über `ct`.

---

### `CommitAsync`

```csharp
Task CommitAsync(string localPath, string message, CancellationToken ct = default);
```

**Beschreibung:** Stagt alle geänderten Dateien (`git add -A`) und erstellt einen Commit mit der angegebenen Nachricht.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `localPath` | `string` | *(required)* | Absoluter Pfad zum lokalen Repository-Verzeichnis |
| `message` | `string` | *(required)* | Commit-Nachricht |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task` – abgeschlossen, wenn der Commit erstellt wurde.

**Fehlerverhalten:**
- `InvalidOperationException` – Keine Änderungen vorhanden (leerer Commit) oder `localPath` ist kein Git-Repository.
- `OperationCanceledException` – Abbruch über `ct`.

---

### `ResetAsync`

```csharp
Task ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct = default);
```

**Beschreibung:** Führt einen `git reset` auf dem lokalen Repository aus. Ermöglicht das Zurücksetzen auf einen bestimmten Commit oder Branch.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `localPath` | `string` | *(required)* | Absoluter Pfad zum lokalen Repository-Verzeichnis |
| `resetType` | `string` | *(required)* | Reset-Modus: `"soft"`, `"mixed"` oder `"hard"` |
| `targetRef` | `string?` | optional | Ziel-Referenz (Commit-SHA, Branch-Name, `"HEAD~1"`, …). Ohne Angabe wird `HEAD` verwendet. |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task` – abgeschlossen, wenn der Reset durchgeführt wurde.

**Fehlerverhalten:**
- `ArgumentException` – Ungültiger `resetType`.
- `InvalidOperationException` – `targetRef` existiert nicht oder ist nicht erreichbar.
- `OperationCanceledException` – Abbruch über `ct`.

> **Achtung:** `"hard"` verwirft alle nicht committeten Änderungen unwiderruflich.

---

### `CheckHealthAsync`

```csharp
Task<bool> CheckHealthAsync(CancellationToken ct = default);
```

**Beschreibung:** Prüft, ob das Plugin betriebsbereit ist (z. B. CLI verfügbar, Token gültig, Netzwerk erreichbar). Wird beim Anwendungsstart und bei der Statusanzeige aufgerufen.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task<bool>` – `true`, wenn das Plugin einsatzbereit ist; `false` bei Konfigurationsproblemen.

**Referenzimplementierung (GitHubPlugin):** Führt `gh auth status` aus und wertet den Exit-Code aus.

**Fehlerverhalten:** Soll keine Exception werfen – Fehler werden als `false` zurückgegeben und intern geloggt.

---

### `GetGitActionCapabilitiesAsync`

```csharp
Task<GitActionCapabilities> GetGitActionCapabilitiesAsync(string? localPath = null, CancellationToken ct = default);
```

**Beschreibung:** Liefert capability-basierte Flags für die Aufgaben-Aktionsleiste (`Push`, `Pull`, `Pull Request`, `Merge`).

**Rückgabewert (`GitActionCapabilities`):**

```csharp
public sealed record GitActionCapabilities(
    RepositoryKind RepositoryKind,
    bool IsWorkingDirectoryCopy,
    bool CanPush,
    bool CanPull,
    bool CanCreatePullRequest,
    bool CanMergeToSource);
```

**Wichtiger LocalDirectory-Fall (`SeparateWorkingDirectory`):**
- `RepositoryKind = LocalDirectory`
- `IsWorkingDirectoryCopy = true`
- `CanMergeToSource = true`
- Push/Pull/PR werden in der UI ausgeblendet; stattdessen wird **Merge** angeboten.

---

### `MergeToSourceAsync`

```csharp
Task MergeToSourceAsync(string localPath, CancellationToken ct = default);
```

**Beschreibung:** Übernimmt Änderungen aus dem lokalen Arbeitsverzeichnis in das Quellverzeichnis.

**LocalDirectory-Semantik:**
- Nur im `SeparateWorkingDirectory`-Modus unterstützt.
- Führt Dateisynchronisation `WorkingDirectory -> SourceDirectory` aus.
- Spiegelung von Löschungen/Umbenennungen via Git-Status (`git status --porcelain`).

---

## 3. IKiPlugin – Schnittstellenreferenz

```csharp
// src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs
public interface IKiPlugin : IPlugin
{
    Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default);
    Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default);
    Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default);
    IAsyncEnumerable<string> StartDevelopmentAsync(
        string prompt,
        AgentInfo agent,
        string localRepoPath,
        string? model = null,
        CancellationToken ct = default);
    Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default);
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
```

---

### `PluginName`

```csharp
string PluginName { get; }
```

**Beschreibung:** Eindeutiger Anzeigename des KI-Plugins.

| Element | Details |
|---|---|
| Typ | `string` |
| Beispielwert | `"GitHub Copilot"`, `"Claude CLI"`, `"OpenAI"` |

---

### `GetAvailableAgentsAsync`

```csharp
Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(
    string agentPackagePath,
    CancellationToken ct = default);
```

**Beschreibung:** Liest alle verfügbaren Agenten aus einem Agentenpaket-Verzeichnis. Ein Agent wird durch eine `*.agent.md`-Datei repräsentiert.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `agentPackagePath` | `string` | *(required)* | Absoluter Pfad zum Agentenpaket-Verzeichnis (z. B. `"…/agent-packages/mein-paket"`) |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task<IEnumerable<AgentInfo>>` – Auflistung aller erkannten Agenten im Paket.

**Datenmodell [`AgentInfo`](#agentinfo):**

```csharp
public sealed record AgentInfo(
    string Name,          // Dateiname ohne ".agent.md"
    string? Beschreibung, // Aus YAML-Frontmatter: description:
    string DateiPfad      // Absoluter Pfad zur .agent.md-Datei
);
```

**Erkennungslogik:**
- Es werden alle Dateien mit dem Muster `*.agent.md` im angegebenen Verzeichnis gesucht.
- Der Agent-Name ergibt sich aus dem Dateinamen **ohne** das Suffix `.agent.md`.
- Die Beschreibung wird aus dem YAML-Frontmatter-Feld `description:` gelesen (optional).

**Fehlerverhalten:**
- `DirectoryNotFoundException` – `agentPackagePath` existiert nicht.
- `OperationCanceledException` – Abbruch über `ct`.

---

### `DeployAgentPackageAsync`

```csharp
Task DeployAgentPackageAsync(
    string agentPackagePath,
    string localRepoPath,
    CancellationToken ct = default);
```

**Beschreibung:** Kopiert alle Agenten-Dateien eines Pakets in das `.github/agents/`-Verzeichnis des geklonten Repositories. Erstellt das Zielverzeichnis, falls es nicht existiert.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `agentPackagePath` | `string` | *(required)* | Absoluter Pfad zum Quell-Agentenpaket-Verzeichnis |
| `localRepoPath` | `string` | *(required)* | Absoluter Pfad zum lokalen Repository-Verzeichnis |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task` – abgeschlossen, wenn alle Dateien kopiert wurden.

**Deployment-Ziel:** `{localRepoPath}/.github/agents/`

**Fehlerverhalten:**
- `DirectoryNotFoundException` – Quell- oder Zielpfad nicht erreichbar.
- `UnauthorizedAccessException` – Fehlende Schreibrechte.
- `OperationCanceledException` – Abbruch über `ct`.

---

### `StartDevelopmentAsync`

```csharp
IAsyncEnumerable<string> StartDevelopmentAsync(
    string prompt,
    AgentInfo agent,
    string localRepoPath,
    string? model = null,
    CancellationToken ct = default);
```

**Beschreibung:** Startet den KI-gestützten Entwicklungsprozess für einen gegebenen Prompt und liefert die Ausgabe des KI-Systems als **Stream von Textfragmenten** zurück. Die Methode gibt `IAsyncEnumerable<string>` zurück, sodass die UI die Ausgabe schrittweise anzeigen kann.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `prompt` | `string` | *(required)* | Aufgabenbeschreibung / Entwicklungsauftrag für den Agenten |
| `agent` | `AgentInfo` | *(required)* | Der zu verwendende Agent (aus [`GetAvailableAgentsAsync`](#getavailableagentsasync)) |
| `localRepoPath` | `string` | *(required)* | Absoluter Pfad zum lokalen Repository, in dem entwickelt werden soll |
| `model` | `string?` | optional | Optionales Modell-Override. Bei `null` verwenden die CLI-Implementierungen den Wert `auto`. |
| `ct` | `CancellationToken` | optional | Abbruch-Token – bricht den Stream ab |

**Rückgabewert:** `IAsyncEnumerable<string>` – Sequenz von Textfragmenten (Streaming-Ausgabe des KI-Systems).

**Feature-Hinweis „Kontextsteuerung bei Folgeanweisungen“ (kein Contract-Change):**
- Die Signatur von `StartDevelopmentAsync` bleibt unverändert (**kein API-Impact auf Plugin-Contract-Ebene**).
- Die drei Kontextmodi (`KontextMitgeben`, `KontextIgnorieren`, `KontextNeuBeginnen`) werden in der Application-Schicht vor dem Plugin-Aufruf in den finalen Prompt überführt.
- Der `agent`-Parameter bleibt weiterhin die einzige Agentensteuerung am Plugin-Contract; Initialprompt und Folgeanweisung nutzen denselben Startpfad.
- HTTP-Status zum Feature: [http-endpoints.md#feature-impact-kontextsteuerung-bei-folgeanweisungen](./http-endpoints.md#feature-impact-kontextsteuerung-bei-folgeanweisungen)
- Architektur- und Testreferenzen: [Kontextsteuerung-Blueprint](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md), [Testplan](../tests/testplan-kontextsteuerung-folgeanweisungen.md)

**Feature-Hinweis „claude-cli-integration“:**
- `ClaudeCliPlugin` ist als zusätzliche produktive `IKiPlugin`-Implementierung verfügbar, ohne Änderung des Contracts.
- API-Status auf HTTP-Ebene: [http-endpoints.md#feature-impact-claude-cli-integration](./http-endpoints.md#feature-impact-claude-cli-integration)
- Verknüpfte Nachweise: [Anforderungen](../requirements/plugin-klassenbibliotheken-github-und-copilot.md), [Architektur](../architecture/plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md), [Review](../improvements/plugin-klassenbibliotheken-github-und-copilot-architecture-review.md), [Testplan](../tests/testplan-claude-cli-integration.md), [Testlücken](../tests/testluecken-claude-cli-integration.md)

**Streaming-Verwendung:**

```csharp
await foreach (var fragment in kiPlugin.StartDevelopmentAsync(prompt, agent, repoPath, model: null, ct: ct))
{
    Console.Write(fragment); // Schrittweise Ausgabe in der UI
}
```

**Fehlerverhalten:**
- `InvalidOperationException` – KI-System nicht erreichbar oder Fehler beim Start.
- `OperationCanceledException` – Abbruch über `ct` bricht den Stream sauber ab.

> **Hinweis:** Da `IAsyncEnumerable<string>` keine `async` Signatur hat, muss die Implementierung intern `yield return` mit `await` kombinieren (z. B. via `IAsyncEnumerable` mit `await foreach` auf einem Prozess-Stream).

---

### `RunTestsAsync`

```csharp
Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default);
```

**Beschreibung:** Führt die automatisierten Tests des Projekts aus und gibt das Ergebnis zurück. Die Referenzimplementierung nutzt `dotnet test`.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `localRepoPath` | `string` | *(required)* | Absoluter Pfad zum lokalen Repository-Verzeichnis |
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task<TestResult>` – Gesamtergebnis und Einzelergebnisse der Testausführung.

**Datenmodell [`TestResult`](#testresult):**

```csharp
public sealed record TestResult(
    bool Bestanden,
    List<TestErgebnisInfo> Ergebnisse
);
```

**Fehlerverhalten:**
- `InvalidOperationException` – Kein Testprojekt gefunden oder Build fehlgeschlagen.
- `OperationCanceledException` – Abbruch über `ct`.
- Fehlgeschlagene Tests führen **nicht** zu einer Exception, sondern werden als `Bestanden = false` zurückgegeben.

---

### `CheckHealthAsync` (IKiPlugin)

```csharp
Task<bool> CheckHealthAsync(CancellationToken ct = default);
```

**Beschreibung:** Prüft, ob das KI-Plugin betriebsbereit ist (CLI verfügbar, Authentifizierung gültig, ggf. API-Quota vorhanden).

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `ct` | `CancellationToken` | optional | Abbruch-Token |

**Rückgabewert:** `Task<bool>` – `true`, wenn das Plugin einsatzbereit ist; `false` bei Konfigurationsproblemen.

**Fehlerverhalten:** Soll keine Exception werfen – Fehler werden als `false` zurückgegeben und intern geloggt.

---

### Implementierungshinweis: GitHubCopilotPlugin

Das `GitHubCopilotPlugin` erweitert die Standard-`IKiPlugin`-Funktionalität um eine optionale Ausführungskonfiguration:

- **Authentifizierung:** `Softwareschmiede.GitHubCopilot.Token` wird als `GH_TOKEN` an den CLI-Prozess übergeben.
- **Ausführung:** `Softwareschmiede.GitHubCopilot.ExecutablePath` kann einen absoluten Pfad zur `copilot`-Executable enthalten.
- Ist der Pfad nicht konfiguriert, wird weiterhin der Kommando-Name `copilot` verwendet.
- Kann der Prozess nicht gestartet werden, wird der Fehler als `InvalidOperationException` mit einem Hinweis auf den optionalen absoluten Pfad gemappt.
- Die Einstellung ist besonders für IIS-/Service-Umgebungen relevant, wenn `copilot` nicht über die PATH-Variable auflösbar ist.

---

## 4. Neues Git-Plugin implementieren

Dieser Abschnitt zeigt Schritt für Schritt, wie ein neues Git-Plugin erstellt wird – am Beispiel eines hypothetischen **GitLabPlugin**.

### Schritt 1: Neue Klasse erstellen

Erstelle ein neues Plugin-Projekt unter `plugins/Softwareschmiede.Plugin.GitLab/` und darin z. B. die Datei `GitLabPlugin.cs`.

```csharp
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Infrastructure.Plugins;

public sealed class GitLabPlugin : IGitPlugin
{
    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<GitLabPlugin> _logger;

    // Credential-Schlüssel für den Windows Credential Store
    private const string CredentialKey = "Softwareschmiede.GitLab.Token";

    public GitLabPlugin(
        ICliRunner cliRunner,
        ICredentialStore credentialStore,
        ILogger<GitLabPlugin> logger)
    {
        _cliRunner = cliRunner;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    public string PluginName => "GitLab";
    public string PluginPrefix => "Softwareschmiede.GitLab";
    public PluginType PluginType => PluginType.SourceCodeManagement;

    // ... (weitere Methoden, siehe unten)
}
```

### Schritt 2: `PluginName`, `PluginPrefix` und `PluginType` implementieren

```csharp
public string PluginName => "GitLab";
public string PluginPrefix => "Softwareschmiede.GitLab";
public PluginType PluginType => PluginType.SourceCodeManagement;
```

Der Name erscheint in UI und Logs. `PluginPrefix` steuert die Credential-Store-Schlüssel, und `PluginType` muss zur Interface-Art passen (`IGitPlugin` → `SourceCodeManagement`), damit der `PluginManager` das Plugin korrekt registriert.

### Schritt 3: Alle Methoden implementieren

Vollständiges Skelett mit allen Interface-Methoden:

```csharp
public async Task<IEnumerable<Issue>> GetIssuesAsync(
    string repositoryId,
    CancellationToken ct = default)
{
    // repositoryId-Format: "owner/repo" (identisch zu GitHub)
    // GitLab API: GET /projects/:id/issues?state=opened
    var token = _credentialStore.GetCredential(CredentialKey)
        ?? throw new InvalidOperationException("GitLab-Token nicht konfiguriert.");

    _logger.LogInformation("Lade Issues für {RepositoryId}", repositoryId);

    // Implementierung: GitLab REST-API aufrufen
    // Umgebungsvariable GITLAB_TOKEN setzen (nie als CLI-Argument!)
    throw new NotImplementedException();
}

public async Task CloneRepositoryAsync(
    string repositoryUrl,
    string targetPath,
    CancellationToken ct = default)
{
    _logger.LogInformation("Klone {Url} nach {Path}", repositoryUrl, targetPath);
    await _cliRunner.RunAsync("git", $"clone {repositoryUrl} {targetPath}", ct: ct);
}

public async Task CreateBranchAsync(
    string localPath,
    string branchName,
    CancellationToken ct = default)
{
    await _cliRunner.RunAsync("git", $"-C {localPath} checkout -b {branchName}", ct: ct);
}

public async Task PushBranchAsync(
    string localPath,
    string branchName,
    CancellationToken ct = default)
{
    await _cliRunner.RunAsync(
        "git",
        $"-C {localPath} push --set-upstream origin {branchName}",
        ct: ct);
}

public async Task PullAsync(
    string localPath,
    CancellationToken ct = default)
{
    await _cliRunner.RunAsync("git", $"-C {localPath} pull", ct: ct);
}

public async Task<PullRequest> CreatePullRequestAsync(
    string repositoryId,
    string branchName,
    string title,
    string body,
    CancellationToken ct = default)
{
    // GitLab: Merge Request via REST-API erstellen
    throw new NotImplementedException();
}

public async Task CommitAsync(
    string localPath,
    string message,
    CancellationToken ct = default)
{
    await _cliRunner.RunAsync("git", $"-C {localPath} add -A", ct: ct);
    await _cliRunner.RunAsync("git", $"-C {localPath} commit -m \"{message}\"", ct: ct);
}

public async Task ResetAsync(
    string localPath,
    string resetType,
    string? targetRef,
    CancellationToken ct = default)
{
    var target = targetRef ?? "HEAD";
    await _cliRunner.RunAsync("git", $"-C {localPath} reset --{resetType} {target}", ct: ct);
}

public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
{
    try
    {
        // Prüfe ob GitLab-Token gesetzt und API erreichbar ist
        var token = _credentialStore.GetCredential(CredentialKey);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("GitLab-Token nicht konfiguriert (CredentialKey: {Key})", CredentialKey);
            return false;
        }

        // Optional: API-Ping durchführen
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "GitLab Health-Check fehlgeschlagen");
        return false;
    }
}
```

### Schritt 4: Token-Credential-Schlüssel definieren

Das Token wird **nie** als CLI-Argument übergeben, sondern als **Umgebungsvariable** gesetzt:

```csharp
private const string CredentialKey = "Softwareschmiede.GitLab.Token";

// Verwendung in Methoden:
var token = _credentialStore.GetCredential(CredentialKey);
Environment.SetEnvironmentVariable("GITLAB_TOKEN", token);
// → Token als Umgebungsvariable für den CLI-Prozess verfügbar machen
```

### Schritt 5: Plugin-Projekt einbinden (statt direkter DI-Bindung)

Die Host-Anwendung lädt Plugins dynamisch über den `PluginManager`. Es gibt **keine direkte** Registrierung wie `AddScoped<IGitPlugin, ...>()`.

Für ein neues Plugin-Projekt sind in `src/Softwareschmiede/Softwareschmiede.csproj` typischerweise zwei Anpassungen nötig:

```xml
<ProjectReference Include="..\..\plugins\Softwareschmiede.Plugin.GitLab\Softwareschmiede.Plugin.GitLab.csproj" ReferenceOutputAssembly="false" />
```

und in den Copy-Targets ein zusätzlicher Artefakt-Eintrag, damit die DLL nach `<OutDir>/plugins` bzw. `<PublishDir>/plugins` kopiert wird.

> **Hinweis:** Die Auflösung auf `IGitPlugin` bleibt im Host weiterhin `AddScoped(...GetDefaultSourceCodeManagementPlugin())` über den `IPluginManager`.

---

## 5. Neues KI-Plugin implementieren

Dieser Abschnitt zeigt die Implementierung eines KI-Plugins – am Beispiel des **ClaudeCliPlugin** (CLI-basiert, produktiv unterstützt).

### Schritt 1: Neue Klasse erstellen

Erstelle ein neues Plugin-Projekt unter `plugins/Softwareschmiede.Plugin.ClaudeCli/` und darin die Plugin-Klasse.

```csharp
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Infrastructure.Plugins;

public sealed class ClaudeCliPlugin : IKiPlugin
{
    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<ClaudeCliPlugin> _logger;

    private const string CredentialKey = "Softwareschmiede.ClaudeCli.Token";

    public ClaudeCliPlugin(
        ICliRunner cliRunner,
        ICredentialStore credentialStore,
        ILogger<ClaudeCliPlugin> logger)
    {
        _cliRunner = cliRunner;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    public string PluginName => "Claude CLI";
    public string PluginPrefix => "Softwareschmiede.ClaudeCli";
    public PluginType PluginType => PluginType.DevelopmentAutomation;

    // ... (weitere Methoden, siehe unten)
}
```

### Schritt 2: `GetAvailableAgentsAsync` implementieren

Liest `.agent.md`-Dateien aus dem Verzeichnis und parst das YAML-Frontmatter:

```csharp
public async Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(
    string agentPackagePath,
    CancellationToken ct = default)
{
    if (!Directory.Exists(agentPackagePath))
        throw new DirectoryNotFoundException($"Agentenpaket nicht gefunden: {agentPackagePath}");

    var agentFiles = Directory.GetFiles(agentPackagePath, "*.agent.md");
    var agents = new List<AgentInfo>();

    foreach (var filePath in agentFiles)
    {
        ct.ThrowIfCancellationRequested();

        // Agent-Name: Dateiname ohne ".agent.md"
        var name = Path.GetFileName(filePath).Replace(".agent.md", string.Empty);

        // Beschreibung aus YAML-Frontmatter lesen
        var description = await ReadDescriptionFromFrontmatterAsync(filePath, ct);

        agents.Add(new AgentInfo(name, description, filePath));
    }

    return agents;
}

private static async Task<string?> ReadDescriptionFromFrontmatterAsync(
    string filePath,
    CancellationToken ct)
{
    var lines = await File.ReadAllLinesAsync(filePath, ct);

    // YAML-Frontmatter erwartet: erste Zeile "---", endet bei nächstem "---"
    if (lines.Length < 2 || lines[0].Trim() != "---")
        return null;

    foreach (var line in lines.Skip(1).TakeWhile(l => l.Trim() != "---"))
    {
        if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            return line["description:".Length..].Trim();
    }

    return null;
}
```

### Schritt 3: `DeployAgentPackageAsync` implementieren

Kopiert alle Dateien des Pakets ins `.github/agents/`-Verzeichnis des Repositories:

```csharp
public async Task DeployAgentPackageAsync(
    string agentPackagePath,
    string localRepoPath,
    CancellationToken ct = default)
{
    var targetDir = Path.Combine(localRepoPath, ".github", "agents");
    Directory.CreateDirectory(targetDir);

    var files = Directory.GetFiles(agentPackagePath);

    foreach (var file in files)
    {
        ct.ThrowIfCancellationRequested();
        var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
        File.Copy(file, targetFile, overwrite: true);
        _logger.LogDebug("Deployed {File} → {Target}", file, targetFile);
    }

    _logger.LogInformation(
        "{Count} Dateien nach {Target} deployed",
        files.Length, targetDir);

    await Task.CompletedTask; // Für zukünftige async-Operationen
}
```

### Schritt 4: `StartDevelopmentAsync` mit Streaming implementieren

Die Methode gibt `IAsyncEnumerable<string>` zurück und streamt die KI-Ausgabe zeilenweise:

```csharp
public async IAsyncEnumerable<string> StartDevelopmentAsync(
    string prompt,
    AgentInfo agent,
    string localRepoPath,
    string? model = null,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var token = _credentialStore.GetCredential(CredentialKey)
        ?? throw new InvalidOperationException("Claude-Token nicht konfiguriert.");

    // Token als Umgebungsvariable setzen (nie als CLI-Argument!)
    Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", token);

    _logger.LogInformation(
        "Starte Entwicklung mit Agent '{Agent}' in {RepoPath}",
        agent.Name, localRepoPath);

    // Beispiel: Claude CLI-Stream aufrufen
    // Der Stream liefert die Ausgabe Fragment für Fragment
    await foreach (var fragment in _cliRunner.StreamAsync(
        "claude",
        $"--agent {agent.DateiPfad} \"{prompt}\"",
        workingDirectory: localRepoPath,
        ct: ct))
    {
        yield return fragment;
    }
}
```

> **Wichtig:** Der `[EnumeratorCancellation]`-Attribute muss am `CancellationToken`-Parameter gesetzt werden, damit `ct` bei `await foreach` korrekt weitergereicht wird.

### Schritt 5: `RunTestsAsync` implementieren

Führt `dotnet test` aus und parst die Ergebnisse:

```csharp
public async Task<TestResult> RunTestsAsync(
    string localRepoPath,
    CancellationToken ct = default)
{
    _logger.LogInformation("Führe Tests aus in {RepoPath}", localRepoPath);

    var output = await _cliRunner.RunAsync(
        "dotnet",
        "test --logger trx --no-build",
        workingDirectory: localRepoPath,
        ct: ct);

    // Exit-Code 0 = alle Tests bestanden
    var bestanden = output.ExitCode == 0;

    // Ergebnisse aus der Ausgabe oder TRX-Datei parsen
    var ergebnisse = ParseTestOutput(output.StandardOutput);

    _logger.LogInformation(
        "Tests abgeschlossen: {Status} ({Count} Ergebnisse)",
        bestanden ? "BESTANDEN" : "FEHLGESCHLAGEN",
        ergebnisse.Count);

    return new TestResult(bestanden, ergebnisse);
}

private static List<TestErgebnisInfo> ParseTestOutput(string output)
{
    // Ausgabe von "dotnet test" parsen
    // Implementierungsdetails je nach gewünschtem Detailgrad
    return new List<TestErgebnisInfo>();
}
```

### Schritt 6: Plugin-Projekt einbinden (statt direkter DI-Bindung)

Analog zum Git-Plugin wird das KI-Plugin als eigenes Projekt unter `plugins/` angelegt, per `ProjectReference` im Host referenziert und per Copy-Target in den Runtime-Ordner `plugins` kopiert.

> **Hinweis:** Die Auflösung auf `IKiPlugin` erfolgt weiterhin über `IPluginManager.GetDefaultDevelopmentAutomationPlugin()`.

---

## 6. Agentenpaket-Struktur

### Verzeichnisstruktur

Agentenpakete werden unter `<AppVerzeichnis>/agent-packages/` abgelegt:

```
agent-packages/
└── mein-paket/
    ├── planner.agent.md       ← Agent "planner"
    ├── implementer.agent.md   ← Agent "implementer"
    ├── reviewer.agent.md      ← Agent "reviewer"
    └── README.md              ← Optionale Paketbeschreibung
```

### Datei-Erkennung

Das Plugin sucht nach dem Glob-Muster `*.agent.md` im angegebenen Verzeichnis. Nur Dateien mit dieser Endung werden als Agenten erkannt. Die `README.md` und andere Dateien werden beim Lesen der Agenten ignoriert, aber beim **Deployment** ebenfalls kopiert.

### Namenskonvention

| Dateiname | Agent-Name |
|---|---|
| `planner.agent.md` | `planner` |
| `implementer.agent.md` | `implementer` |
| `code-reviewer.agent.md` | `code-reviewer` |

> **Regel:** Agent-Name = Dateiname ohne das Suffix `.agent.md`

### YAML-Frontmatter-Format

Jede `.agent.md`-Datei **muss** ein YAML-Frontmatter-Abschnitt am Dateianfang enthalten:

```yaml
---
name: planner
description: Analysiert Anforderungen und erstellt einen strukturierten Entwicklungsplan.
---
```

| Feld | Pflicht | Beschreibung |
|---|---|---|
| `name` | empfohlen | Interner Name des Agenten |
| `description` | empfohlen | Kurzbeschreibung, wird in der UI angezeigt |

Das Frontmatter wird durch zwei `---`-Trennlinien begrenzt. Alles unterhalb des schließenden `---` ist der eigentliche Agenteninhalt (Anweisungen, Prompts, etc.).

### Vollständiges Beispiel einer `.agent.md`-Datei

```markdown
---
name: planner
description: Analysiert Anforderungen und erstellt einen strukturierten Entwicklungsplan.
---

# Planner Agent

Du bist ein erfahrener Software-Architekt. Deine Aufgabe ist es, die gegebenen
Anforderungen zu analysieren und einen detaillierten Entwicklungsplan zu erstellen.

## Vorgehen

1. Analysiere die Anforderungen sorgfältig.
2. Identifiziere betroffene Dateien und Komponenten.
3. Erstelle einen schrittweisen Implementierungsplan.
4. Schätze den Aufwand für jeden Schritt.

## Ausgabeformat

Erstelle einen strukturierten Plan im Markdown-Format mit:
- Kurzzusammenfassung der Änderungen
- Liste der zu erstellenden/ändernden Dateien
- Implementierungsschritte in Reihenfolge
```

### Speicherort der Pakete

```
<AppVerzeichnis>/
└── agent-packages/
    ├── basis-agenten/
    │   ├── planner.agent.md
    │   └── implementer.agent.md
    └── spezialisiert/
        ├── api-designer.agent.md
        └── test-writer.agent.md
```

> **Hinweis:** `<AppVerzeichnis>` entspricht dem Verzeichnis, aus dem die Softwareschmiede-Anwendung gestartet wird (z. B. das Ausgabeverzeichnis von `dotnet publish`).

### Deployment-Ziel

Nach dem Aufruf von [`DeployAgentPackageAsync`](#deployagentpackageasync) werden die Agenten-Dateien in das geklonte Repository kopiert:

```
{localRepoPath}/
└── .github/
    └── agents/
        ├── planner.agent.md
        ├── implementer.agent.md
        └── README.md
```

Das Verzeichnis `.github/agents/` wird automatisch erstellt, falls es nicht existiert. Vorhandene Dateien werden **überschrieben**.

---

## Anhang: Datenmodelle

### `Issue`

```csharp
public sealed record Issue(
    int Nummer,        // Issue-Nummer im Repository
    string Titel,      // Titel des Issues
    string? Body,      // Beschreibungstext (Markdown, optional)
    List<string> Labels,   // Liste der Labels
    string? Milestone,     // Milestone-Name (optional)
    string? Url        // Direkte URL zum Issue (optional)
);
```

### `PullRequest`

```csharp
public sealed record PullRequest(
    int Nummer,        // PR-Nummer im Repository
    string Titel,      // Titel des Pull Requests
    string Url,        // Direkte URL zum Pull Request
    string BranchName  // Quell-Branch des Pull Requests
);
```

### `AgentInfo`

```csharp
public sealed record AgentInfo(
    string Name,           // Agent-Name (Dateiname ohne ".agent.md")
    string? Beschreibung,  // Beschreibung aus YAML-Frontmatter (optional)
    string DateiPfad       // Absoluter Pfad zur .agent.md-Datei
);
```

### `AgentPackageInfo`

```csharp
public sealed record AgentPackageInfo(
    string Name,                        // Paketname (Verzeichnisname)
    string Pfad,                        // Absoluter Pfad zum Paketverzeichnis
    IReadOnlyList<AgentInfo> Agenten,   // Enthaltene Agenten
    IReadOnlyList<string> Dateien       // Alle Dateien im Paket
);
```

### `TestResult`

```csharp
public sealed record TestResult(
    bool Bestanden,                         // true = alle Tests bestanden
    List<TestErgebnisInfo> Ergebnisse       // Einzelergebnisse je Test
);
```
