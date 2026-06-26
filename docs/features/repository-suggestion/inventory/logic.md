# Logikklassen und Services

## `ProjektService`
Datei: `src/Softwareschmiede/Application/Services/ProjektService.cs`

### Implementierte Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllAsync(CancellationToken)` | public | Gibt alle Projekte alphabetisch sortiert zurück |
| `GetByIdAsync(Guid, CancellationToken)` | public | Gibt ein Projekt anhand seiner ID zurück (minimal) |
| `GetDetailAsync(Guid, CancellationToken)` | public | Gibt ein Projekt mit Repositories und Aufgaben zurück (geladen mit Includes) |
| `CreateAsync(string, string?, CancellationToken)` | public | Erstellt ein neues Projekt mit Status `Aktiv` |
| `UpdateAsync(Guid, string, string?, CancellationToken)` | public | Aktualisiert Name und Beschreibung eines Projekts |
| `ArchivierenAsync(Guid, CancellationToken)` | public | Setzt Projekt-Status auf `Archiviert` |
| `DeleteAsync(Guid, CancellationToken)` | public | Löscht ein Projekt inkl. aller verknüpften Daten |
| `AddRepositoryAsync(Guid, string, string, string, CancellationToken)` | public | Überladung: Fügt Repository mit pluginTyp, URL und Name hinzu |
| `AddRepositoryAsync(Guid, string, IReadOnlyDictionary<string, string>, CancellationToken)` | public | Überladung: Fügt Repository über pluginabhängige Eingabefelder hinzu |
| `GetAllRepositoriesAsync(CancellationToken)` | public | Gibt alle bekannten Git-Repositories aus der DB zurück |
| `RemoveRepositoryAsync(Guid, CancellationToken)` | public | Entfernt ein Git-Repository aus dem Projekt |
| `SaveRepositoryStartKonfigurationAsync(Guid, string, bool, CancellationToken)` | public | Speichert oder aktualisiert Startkonfiguration für ein Repository |
| `GetRepositoryStartKonfigurationAsync(Guid, CancellationToken)` | public | Liefert Startkonfiguration eines Repositories |

### Abonnierte Events
Keine Event-Abos implementiert.

### Publizierte Events
Keine Event-Publikationen implementiert.

### Abhängigkeiten
- `SoftwareschmiededDbContext` — Datenbankzugriff
- `ILogger<ProjektService>` — Logging

### Notizen
- Service validiert lokale Pfade für `LocalDirectoryPlugin`
- Service validiert Felder für GitHub-Plugins (RepositoryUrl, RepositoryName)
- Service leitet Repository-Namen aus URLs ab (als Fallback)
- **Keine Methode `GetUnassignedRepositoriesAsync()` vorhanden** — dies muss noch implementiert werden

---

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

### Interface-Methoden

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins()` | — | `IReadOnlyList<IGitPlugin>` | Gibt alle geladenen SCM-Plugins zurück |
| `GetDevelopmentAutomationPlugins()` | — | `IReadOnlyList<IKiPlugin>` | Gibt alle geladenen Development-Automation-Plugins zurück |
| `GetDefaultSourceCodeManagementPlugin()` | — | `IGitPlugin` | Gibt das erste verfügbare SCM-Plugin zurück |
| `GetDefaultDevelopmentAutomationPlugin()` | — | `IKiPlugin` | Gibt das priorisierte Development-Automation-Plugin zurück |

### Zweck
- Zentrale Verwaltung für Discovery und Zugriff auf geladene Plugins
- Wird verwendet, um alle SCM-Plugins zu iterieren (für Repository-Aggregation)

---

## `IGitPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

### Interface-Methoden (Auszug für Repository-Vorschlag)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAvailableRepositoriesAsync(CancellationToken)` | `ct` | `Task<IEnumerable<AvailableRepository>>` | **Entscheidend**: Liefert verfügbare Repositories aus der externen SCM-Quelle |
| `GetIssuesAsync(string, CancellationToken)` | `repositoryId`, `ct` | `Task<IEnumerable<Issue>>` | Ruft Issues aus dem Repository ab |
| `CheckHealthAsync(CancellationToken)` | `ct` | `Task<bool>` | Prüft ob Plugin verfügbar ist (CLI installiert, Token gültig) |

### Weitere Methoden (nicht direkt relevant)
- Repository-Verwaltung: CloneRepositoryAsync, CreateBranchAsync, PushBranchAsync, PullAsync, CommitAsync, ResetAsync
- Branch-Verwaltung: GetRemoteBranchesAsync, GetDefaultBranchAsync, CheckoutRemoteBranchAsync
- Pull-Request: CreatePullRequestAsync
- Andere: GetRepositoryLinkFields(), GetGitActionCapabilitiesAsync(), MergeToSourceAsync()

### Zweck
- Plugin-Interface für Git-Provider (GitHub, GitLab, Bitbucket, LocalDirectory, etc.)
- `GetAvailableRepositoriesAsync()` ist die Kernmethode für die Repository-Suggestion
- Alle verfügbaren Repositories werden als `AvailableRepository`-Objekte zurückgegeben
