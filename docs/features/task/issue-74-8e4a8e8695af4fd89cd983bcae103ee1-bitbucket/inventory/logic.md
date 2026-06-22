# Logik und Services

## `BitbucketPlugin`
Datei: `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

### Öffentliche Methoden (Abstrakt überschrieben / Implementiert)

| Methode | Sichtbarkeit | Rückgabe | Kurzbeschreibung |
|---------|-------------|----------|------------------|
| `GetSettingGroups()` | Public, Override | `IReadOnlyList<PluginSettingGroup>` | Gibt 2 Einstellungsgruppen zurück: "Authentifizierung" und "Jira" |
| `GetRepositoryLinkFields()` | Public, Override | `IReadOnlyList<PluginSettingField>` | Gibt 2 Felder für Repository-Verknüpfung zurück (URL, Name) |
| `CloneRepositoryAsync(...)` | Public, Async | `Task` | Klont Bitbucket-Repository mit Git – nutzt `.netrc` für Authentifizierung |
| `PullAsync(...)` | Public, Async | `Task` | Führt `git pull` aus |
| `PushBranchAsync(...)` | Public, Async | `Task` | Pusht einen Branch mit `git push --set-upstream origin <branch>` |
| `GetIssuesAsync(...)` | Public, Async | `Task<IEnumerable<Issue>>` | Ruft Issues von Jira (nicht Bitbucket) ab via REST-API |
| `CreatePullRequestAsync(...)` | Public, Async | `Task<PullRequest>` | Erstellt PR über Bitbucket API 2.0 (`/2.0/repositories/{id}/pullrequests`) |
| `CheckHealthAsync()` | Public, Async | `Task<bool>` | Prüft Bitbucket-Verbindung (`/2.0/user`) und Jira-Verbindung; gibt true zurück wenn beide erfolgreich |
| `GetRemoteBranchesAsync(...)` | Public, Async | `Task<IEnumerable<string>>` | Listet Remote-Branches via `git ls-remote --heads` |
| `GetAvailableRepositoriesAsync()` | Public, Async | `Task<IEnumerable<AvailableRepository>>` | Ruft Repositories des Workspace von Bitbucket API ab (`/2.0/repositories/{workspace}?pagelen=100`) |
| `GetDefaultBranchAsync(...)` | Public, Async | `Task<string>` | Ermittelt Standard-Branch via `git ls-remote --symref` |

### Private Hilfsmethoden

| Methode | Rückgabe | Kurzbeschreibung |
|---------|----------|------------------|
| `GetGitEnvironment()` | `IDictionary<string, string>` | Rüstet Git-Umgebungsvariablen aus: `GIT_TERMINAL_PROMPT=0`, SSH-Keys, `.netrc`-Datei mit Credentials |
| `BuildAuthenticatedCloneUrl(...)` | `string` | Konstruiert URL mit eingebetteten Credentials (Username + App-Password) |
| `ParseJiraIssues(json)` | `IEnumerable<Issue>` | Parst Jira-API-Antwort (JSON) und liefert Issue-Objekte zurück |

### Abonnierte Events
Keine direkten Event-Abonnierungen in dieser Klasse vorhanden.

### Publizierte Events
Keine direkten Event-Publikationen in dieser Klasse vorhanden.

### Abhängigkeiten (Verwendet externe Services)
- **`ICliRunner`** – Ausführung von CLI-Befehlen (git, curl)
- **`ICredentialStore`** – Lese-Zugriff auf Credentials für Bitbucket (User, AppPassword, Workspace) und Jira (URL, Email, Token)
- **`ILogger<BitbucketPlugin>`** – Protokollierung

## `SettingsViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`

Dieser ViewModel lädt und speichert Plugin-Einstellungen allgemein (nicht spezifisch für Bitbucket). Er ist verantwortlich für die Datenbindung zwischen UI und Plugins.

### Relevante öffentliche Methoden
- `LoadScmPluginSettings(IGitPlugin plugin)` – Lädt Einstellungsgruppen eines SCM-Plugins
- `LadePluginEinstellungen(IPlugin plugin)` → `IReadOnlyList<PluginSettingGroupEntry>` – Konvertiert `PluginSettingGroup` in bearbeitbare `PluginSettingGroupEntry`-Objekte
- `SpeicherePluginEinstellungen(IPlugin, IReadOnlyList<PluginSettingGroupEntry>)` – Speichert alle Werte einer Plugin-Einstellungsgruppe via `PluginSettingsService`

### Abhängigkeiten
- **`PluginSettingsService`** – Speichern/Laden von einzelnen Plugin-Einstellungswerten

## `PluginManager`
Datei: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`

Lädt Plugins dynamisch aus dem `plugins/`-Verzeichnis (Reflection + Assembly Loading).

### Relevante öffentliche Methoden
- `GetSourceCodeManagementPlugins()` → `IReadOnlyList<IGitPlugin>` – Gibt alle geladenen SCM-Plugins zurück (inkl. BitbucketPlugin)
- `GetDefaultSourceCodeManagementPlugin()` → `IGitPlugin` – Gibt das erste verfügbare SCM-Plugin zurück

### Ablauf Plugin-Discovery
1. Durchsucht `plugins/`-Verzeichnis nach `.dll`-Dateien
2. Lädt Assembly via `AssemblyLoadContext.Default.LoadFromAssemblyPath()`
3. Sucht nach Typen die `IGitPlugin` oder `IKiPlugin` implementieren
4. Instanziiert via `ActivatorUtilities.CreateInstance()` mit DI-Container
5. Registriert in `_gitPlugins` oder `_kiPlugins`

Wenn BitbucketPlugin vorhanden und gebaut wird, wird es automatisch bei Plugin-Discovery geladen.
