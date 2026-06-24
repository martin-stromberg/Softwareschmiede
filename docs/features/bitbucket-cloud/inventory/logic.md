# Logikklassen

## `BitbucketPlugin`

Datei: `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

Hauptimplementierung für Git-Operationen gegen Bitbucket Cloud und Self-Hosted.

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| PluginName | `string` | "Bitbucket" |
| PluginPrefix | `string` | "Softwareschmiede.Bitbucket" |
| PluginType | `PluginType` | SourceCodeManagement |

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSettingGroups()` | public override | Liefert Konfigurationsgruppen (Authentifizierung, Jira, BitBucket-Hosting) |
| `GetRepositoryLinkFields()` | public override | Liefert Felder für Repository-Verknüpfung (URL, Name) |
| `CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct)` | public override | Klont Repository mit eingebetteten Credentials |
| `PullAsync(string localPath, CancellationToken ct)` | public override | Führt `git pull` mit HTTP-Auth-Argumenten aus |
| `PushBranchAsync(string localPath, string branchName, CancellationToken ct)` | public override | Pusht Branch mit HTTP-Auth-Argumenten |
| `CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct)` | public override | Erstellt PR via Bitbucket API (Cloud oder Self-Hosted) |
| `GetIssuesAsync(string repositoryId, CancellationToken ct)` | public override | Ruft Jira-Issues via curl ab und parst sie |
| `CheckHealthAsync(CancellationToken ct)` | public override | Prüft Bitbucket- und Jira-Verbindung |
| `GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct)` | public override | Listet Remote-Branches via `git ls-remote` |
| `GetAvailableRepositoriesAsync(CancellationToken ct)` | public override | Ruft verfügbare Repositories aus Bitbucket API ab |
| `GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct)` | public override | Ermittelt Standard-Branch (main/master) |

### Private/Interne Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetGitEnvironment()` | private | Erstellt Umgebungsvariablen für git (GIT_TERMINAL_PROMPT, .netrc-Entry) |
| `GetGitHttpAuthArgs()` | private | Liefert `-c credential.helper= -c http.extraheader=...` für Self-Hosted |
| `GetJiraCurlAuthArgs()` | private | Liefert Basic-Auth für Jira API |
| `GetCurlAuthArgs()` | private | Liefert curl-Auth-Argumente (Bearer für Self-Hosted, Basic für Cloud) |
| `BuildAuthenticatedCloneUrl(string repositoryUrl, string user, string appPassword)` | private static | Baut Clone-URL mit eingebetteten Credentials |
| `ResolveGitCloneUrl(string repositoryUrl)` | internal static | Konvertiert Browser-/API-URLs zu SCM-Git-URLs (Self-Hosted) |
| `UpdateNetrcEntry(string netrcPath, string host, string user, string appPassword)` | private static | Aktualisiert/erstellt .netrc-Eintrag für Git-Auth |
| `BuildRepositoryCloneUrl(string repositoryId, string hostingMode)` | private | Baut Clone-URL aus Repository-ID für API-Aufrufe |
| `GetBitbucketApiBaseUrl()` | internal | Gibt API-Basis-URL zurück (Cloud oder Self-Hosted) |
| `GetBitbucketRepositoriesPath(string workspace)` | internal | Gibt API-Pfad für Repositories zurück |
| `ParseJiraIssues(JsonElement root)` | private static | Parst Jira-API-Response zu Issue-Liste |
| `RenderAdf(JsonElement node)` | private static | Konvertiert Atlassian Document Format zu reinem Text |
| `RenderAdfNode(JsonElement node, StringBuilder sb)` | private static | Rekursives Rendering einzelner ADF-Knoten |
| `RenderAdfChildren(JsonElement node, StringBuilder sb)` | private static | Rendert Child-Elemente eines ADF-Knotens |
| `HasBitbucketErrors(JsonElement root, out string message)` | private static | Prüft Bitbucket API-Response auf Fehler |
| `HasBitbucketApiError(string json)` | private static | Wrapper für JSON-Error-Prüfung |

### Abhängigkeiten (Injiziert)

| Feld | Typ | Zweck |
|------|-----|-------|
| `_cliRunner` | `ICliRunner` | Ausführung von Git/curl-Befehlen |
| `_credentialStore` | `ICredentialStore` | Abruf von Benutzerdaten, Tokens, Hosting-Modus |
| `_logger` | `ILogger<BitbucketPlugin>` | Logging für Debugging und Fehlerbehandlung |

### Credential-Keys

Die Klasse nutzt folgende Key-Konstanten zum Abruf aus `ICredentialStore`:

| Konstante | Wert | Beschreibung |
|-----------|------|-------------|
| `BitbucketUserKey` | "Softwareschmiede.Bitbucket.Username" | Bitbucket-Benutzername oder E-Mail |
| `BitbucketAppPasswordKey` | "Softwareschmiede.Bitbucket.AppPassword" | Bitbucket App Password / Token |
| `BitbucketWorkspaceKey` | "Softwareschmiede.Bitbucket.Workspace" | Workspace-Name (Cloud) oder Projekt-Prefix |
| `BitbucketHostingModeKey` | "Softwareschmiede.Bitbucket.HostingMode" | "Cloud" oder "SelfHosted" |
| `BitbucketSelfHostedUrlKey` | "Softwareschmiede.Bitbucket.SelfHostedUrl" | URL der Self-Hosted-Instanz |
| `RepositoryUrlKey` | "RepositoryUrl" | Repository-URL für Link-Felder |
| `RepositoryNameKey` | "RepositoryName" | Repository-ID für API-Aufrufe |

### Abonnierte Events

Keine. Das Plugin abonniert keine Events von anderen Komponenten.

### Publizierte Events

Keine. Das Plugin publiziert keine Events.

### Wichtige Kontrollflüsse

#### Clone-Workflow (Cloud vs. Self-Hosted)

1. `CloneRepositoryAsync()` prüft Hosting-Mode
2. Bei Self-Hosted: `ResolveGitCloneUrl()` konvertiert Browser-URL zu SCM-URL
3. `BuildAuthenticatedCloneUrl()` embeddet Credentials in URL
4. `GetGitEnvironment()` setzt `.netrc` und Umgebungsvariablen
5. `git clone` wird mit authentifizierter URL und Umgebung ausgeführt

#### Pull/Push-Workflow

1. `PullAsync()` / `PushBranchAsync()` rufen `GetGitHttpAuthArgs()` auf
2. Bei Cloud: leer (Credentials in .netrc)
3. Bei Self-Hosted: `-c http.extraheader=Authorization: Basic ...`
4. `GetGitEnvironment()` wird für .netrc-Setup verwendet
5. Git-Befehl wird mit Auth-Argumenten und Umgebung ausgeführt

#### Issue-Abruf

1. `GetIssuesAsync()` ruft `curl` mit Jira-Auth auf
2. Response wird geparst: Jira-Keys und Titel extrahiert
3. `RenderAdf()` konvertiert Beschreibungen von ADF zu Text
4. `Issue`-Objekte werden konstruiert und zurückgegeben

#### Repository-Listing

1. `GetAvailableRepositoriesAsync()` baut API-URL mit Cloud/Self-Hosted-Pfad
2. `curl` ruft API mit Auth auf
3. Response wird geparst: Cloud nutzt `full_name`, Self-Hosted nutzt `project.key/slug`
4. `AvailableRepository`-Objekte werden konstruiert

#### Health-Check

1. `CheckHealthAsync()` ruft Cloud oder Self-Hosted API auf
2. Prüft Bitbucket-User-Endpoint
3. Falls Jira-URL konfiguriert: prüft auch Jira-Endpoint
4. Gibt `true` zurück wenn beide erfolgreich

### Fehlerbehandlung

- **InvalidOperationException**: Bei fehlenden Credentials, fehlerhaften Git/curl-Aufrufen, fehlender Self-Hosted-URL
- **Logging**: Informationen bei Clone, Warnungen bei API-Fehlern
- **API-Error-Parsing**: `HasBitbucketErrors()` extrahiert Fehlermeldungen aus JSON-Responses

### Besonderheiten für Anforderung BitBucket Cloud Support

Die Klasse enthält bereits die Differenzierung zwischen Cloud und Self-Hosted:
- `GetGitEnvironment()`: Setzt unterschiedliche `.netrc`-Hosts je nach Modus
- `GetGitHttpAuthArgs()`: Liefert nur bei Self-Hosted Auth-Header
- `GetCurlAuthArgs()`: Bearer für Self-Hosted, Basic für Cloud
- `GetBitbucketApiBaseUrl()`: Cloud = api.bitbucket.org, Self-Hosted = konfigurierte URL
- `CreatePullRequestAsync()`: Unterschiedliche API-Endpoints
- `BuildRepositoryCloneUrl()`: Unterschiedliche URL-Formate
