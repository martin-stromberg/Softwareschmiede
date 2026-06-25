← [Zurück zur Übersicht](index.md)

# BitBucket-Plugin — Technischer Ablauf

## Übersicht

Das BitBucket-Plugin verwaltet Git-Operationen gegen BitBucket Cloud und Self-Hosted durch Koordination von Credential-Management, URL-Konvertierung und Git-Befehlsausführung. Die Authentifizierung erfolgt dual: eingebettete Credentials in Clone-URLs sowie `.netrc`-basierte HTTP-Basic-Auth für Pull/Push.

## Clone-Workflow

### 1. Initialisierung und Credential-Validierung

1. `CloneRepositoryAsync(repositoryUrl, targetPath)` wird aufgerufen
2. Benutzername und App Password werden aus `ICredentialStore` abgerufen
3. Falls Credentials fehlen: Exception `InvalidOperationException` mit Meldung "Bitbucket-Authentifizierung fehlt"

Beteiligte Komponenten:
- `BitbucketPlugin.CloneRepositoryAsync()` — Einstiegspunkt
- `ICredentialStore.GetCredential()` — Credential-Verwaltung

### 2. Hosting-Mode-Bestimmung und URL-Konvertierung

1. Hosting-Modus aus `ICredentialStore` abrufen (Standard: "Cloud")
2. Falls **Cloud**: Repository-URL wird unverändert verwendet (erwartet bereits Cloud-URL wie `https://bitbucket.org/workspace/repo`)
3. Falls **Self-Hosted**: `ResolveGitCloneUrl()` konvertiert Browser-URL oder API-URL zu SCM-URL:
   - Browser: `https://bitbucket.example.com/projects/KEY/repos/SLUG/browse` → `https://bitbucket.example.com/scm/KEY/SLUG.git`
   - API: `https://bitbucket.example.com/rest/api/1.0/projects/KEY/repos/SLUG` → `https://bitbucket.example.com/scm/KEY/SLUG.git`
   - SCM: `https://bitbucket.example.com/scm/KEY/SLUG.git` → unverändert

Beteiligte Komponenten:
- `BitbucketPlugin.CloneRepositoryAsync()` — Hosting-Mode-Abfrage
- `BitbucketPlugin.ResolveGitCloneUrl()` — URL-Konvertierung (statische Methode)

### 3. Credential-Embedding in Clone-URL

1. `BuildAuthenticatedCloneUrl(resolvedUrl, user, appPassword)` wird aufgerufen
2. URL wird geparst (`new Uri(url)`)
3. Benutzername und Passwort werden mit `Uri.EscapeDataString()` URL-kodiert (z.B. `martin@example.com` → `martin%40example.com`)
4. Neue URL wird zusammengesetzt: `{scheme}://{encodedUser}:{encodedPassword}@{host}{port}{path}`

**Beispiel Cloud:**
```
Input:  https://bitbucket.org/workspace/repo.git
User:   martin@example.com
Pass:   xxxx-xxxx
Output: https://martin%40example.com:xxxx-xxxx@bitbucket.org/workspace/repo.git
```

**Beispiel Self-Hosted mit Port:**
```
Input:  https://bitbucket.example.com:7990/scm/KEY/slug.git
User:   developer@company.com
Pass:   token123
Output: https://developer%40company.com:token123@bitbucket.example.com:7990/scm/KEY/slug.git
```

Beteiligte Komponenten:
- `BitbucketPlugin.BuildAuthenticatedCloneUrl()` — Statische Methode für URL-Embedding

### 4. Git-Umgebungsvariablen und .netrc-Eintrag

1. `GetGitEnvironment()` wird aufgerufen (ruft `netrcPath` ab oder nutzt Standard: `~/.netrc` / `C:\Users\{user}\_netrc`)
2. Hosting-Mode wird abgerufen
3. Für **Cloud**: Host wird auf `bitbucket.org` gesetzt
4. Für **Self-Hosted**: Host wird aus konfigurierter URL extrahiert (z.B. `bitbucket.example.com` aus `https://bitbucket.example.com:7990`)
5. `.netrc`-Datei wird aktualisiert/erstellt mit Eintrag:
   ```
   machine {host}
   login {user}
   password {appPassword}
   ```
6. `GIT_TERMINAL_PROMPT=0` wird gesetzt (deaktiviert interaktive Prompts)

**Windows-Spezifisch:**
- Datei heißt `_netrc` (statt `.netrc`)
- Pfad: `C:\Users\{username}\_netrc`

Beteiligte Komponenten:
- `BitbucketPlugin.GetGitEnvironment()` — Umgebungsvariablen und `.netrc`-Verwaltung
- `BitbucketPlugin.UpdateNetrcEntry()` — `.netrc`-Datei-Aktualisierung

### 5. Git-Clone-Ausführung

1. `ICliRunner.RunAsync()` wird mit Kommando `git clone {authenticatedUrl} {targetPath}` aufgerufen
2. Umgebungsvariablen (mit `.netrc`-Eintrag und `GIT_TERMINAL_PROMPT=0`) werden übergeben
3. Git authentifiziert sich via:
   - **Primary:** URL-eingebettete Credentials (`user:password@host`)
   - **Fallback:** `.netrc`-Eintrag (falls Git URL-Credentials ignoriert)

### 6. Fehlerbehandlung bei Clone

1. Falls `git clone` fehlschlägt (Exit-Code != 0):
   - `StdErr` wird auf Authentifizierungsfehler geprüft:
     - "Invalid username or token"
     - "Authentication failed"
   - Fehlerausgabe wird mit `SanitizeSensitiveOutput()` bereinigt (App Password wird durch `***` ersetzt)
   - Spezifische Fehlerlog-Meldung wird geschrieben: "Bitbucket-Authentifizierung fehlgeschlagen (Modus: {HostingMode}). Bitte Benutzernamen und App Password prüfen."
2. `InvalidOperationException` mit sanitizierter Fehlermeldung wird geworfen

Beteiligte Komponenten:
- `BitbucketPlugin.SanitizeSensitiveOutput()` — Regex-basierte Bereinigung von Credentials
- `ILogger<BitbucketPlugin>` — Logging von Errors und Debugs

## Pull/Push-Workflow

### 1. Credential-Validierung

1. Ähnlich wie Clone: Benutzername und App Password aus `ICredentialStore` abrufen
2. Falls Credentials fehlen: Exception

Beteiligte Komponenten:
- `BitbucketPlugin.PullAsync()` / `BitbucketPlugin.PushBranchAsync()`

### 2. Authentifizierungs-Argument-Zusammenstellung

1. `GetGitHttpAuthArgs()` wird aufgerufen:
   - Für **Cloud**: Leeres Array `[]` zurückgeben (`.netrc`-Fallback)
   - Für **Self-Hosted**: HTTP-Header-Argumente zusammenstellen:
     ```csharp
     ["-c", "credential.helper=", "-c", "http.extraheader=Authorization: Basic {base64-encoded}"]
     ```
     Wobei `base64-encoded = Base64(user:appPassword)`

2. `.netrc`-Eintrag wird via `GetGitEnvironment()` aktualisiert (wie in Clone-Workflow)

Beteiligte Komponenten:
- `BitbucketPlugin.GetGitHttpAuthArgs()` — Git-Argumente für HTTP-Auth
- `BitbucketPlugin.GetGitEnvironment()` — `.netrc`-Management

### 3. Git-Kommando-Ausführung

1. `ICliRunner.RunAsync()` wird mit `git {httpAuthArgs} pull` oder `git {httpAuthArgs} push --set-upstream origin {branchName}` aufgerufen
2. Arbeitsverzeichnis wird übergeben (`localPath`)
3. Umgebungsvariablen werden übergeben (mit `.netrc`)

**Cloud-Beispiel:**
```
git -u (from .netrc) pull
```

**Self-Hosted-Beispiel:**
```
git -c credential.helper= -c http.extraheader=Authorization: Basic dXNlcjpwYXNz push --set-upstream origin feature-branch
```

### 4. Fehlerbehandlung

Identisch mit Clone-Workflow: Fehler werden erkannt, Credentials werden bereinigt, spezifische Log-Meldungen werden geschrieben.

Beteiligte Komponenten:
- `BitbucketPlugin.SanitizeSensitiveOutput()` — Fehlerbereinigung

## Health-Check-Workflow

### 1. BitBucket-API-Erreichbarkeit prüfen

1. **Cloud:** `curl` gegen `https://api.bitbucket.org/2.0/user` aufrufen
2. **Self-Hosted:** `curl` gegen `https://{configuredUrl}/rest/api/1.0/user` aufrufen
3. Authentifizierung via `GetCurlAuthArgs()`:
   - **Cloud:** `-u user:appPassword` (HTTP Basic Auth)
   - **Self-Hosted:** `-H "Authorization: Bearer {appPassword}"` (Bearer Token)

Beteiligte Komponenten:
- `BitbucketPlugin.CheckHealthAsync()` — Health-Check-Orchestrierung
- `BitbucketPlugin.GetCurlAuthArgs()` — Curl-Authentifizierungs-Argumente
- `BitbucketPlugin.GetBitbucketApiBaseUrl()` — API-Basis-URL-Bestimmung

### 2. Jira-API-Erreichbarkeit prüfen (optional)

1. Falls `JiraUrl` konfiguriert: `curl` gegen `{jiraUrl}/rest/api/3/myself` aufrufen
2. Authentifizierung via `GetJiraCurlAuthArgs()`: `-H "Authorization: Basic {base64(email:token)}"`
3. Falls Jira-Verbindung fehlschlägt, wird Health-Check als fehlgeschlagen markiert

### 3. Fehler-Analyse

1. JSON-Response wird geparst
2. `HasBitbucketApiError()` prüft auf Error-Felder in der Response
3. Falls Fehler: `false` zurückgeben; sonst `true`

Beteiligte Komponenten:
- `BitbucketPlugin.HasBitbucketApiError()` — Error-Detektion in API-Responses

## Repository-Discovery-Workflow

### 1. Workspace/Project-Abruf

1. Workspace/Project Key wird aus `ICredentialStore` abgerufen (via `BitbucketWorkspaceKey`)
2. Falls nicht konfiguriert: leere List `[]` zurückgeben

### 2. API-Endpunkt-Bestimmung

1. `GetBitbucketRepositoriesPath(workspace)` wird aufgerufen:
   - **Cloud:** `/2.0/repositories/{workspace}?pagelen=100`
   - **Self-Hosted:** `/rest/api/1.0/projects/{workspace}/repos`
2. Basis-URL wird via `GetBitbucketApiBaseUrl()` ermittelt
3. Vollständige API-URL wird zusammengesetzt: `{baseUrl}{path}`

### 3. Repository-List-Abruf

1. `curl` wird mit `GetCurlAuthArgs()` gegen die API-URL aufgerufen
2. JSON-Response wird geparst

### 4. JSON-Parsing (unterschiedliche Formate für Cloud und Self-Hosted)

**Cloud-Format:**
```json
{
  "values": [
    {
      "name": "my-repo",
      "full_name": "workspace/my-repo",
      "updated_on": "2025-01-01T...",
      "links": { "html": { "href": "https://bitbucket.org/..." } }
    }
  ]
}
```

**Self-Hosted-Format:**
```json
{
  "values": [
    {
      "name": "My Repo",
      "slug": "my-repo",
      "project": { "key": "MYPROJ" },
      "updated_on": "2025-01-01T...",
      "links": { "self": [{ "href": "https://bitbucket.company.com/..." }] }
    }
  ]
}
```

Parsing:
- **Cloud:** `full_name` direkt nutzen für `NameWithOwner`; `links.html.href` für URL
- **Self-Hosted:** `project.key + "/" + slug` zusammensetzen für `NameWithOwner`; `links.self[0].href` für URL

Beteiligte Komponenten:
- `BitbucketPlugin.GetAvailableRepositoriesAsync()` — Repository-Discovery
- JSON-Parsing via `JsonDocument`
- `AvailableRepository` DTO

## Pull-Request-Erstellung-Workflow

### 1. Standard-Branch ermitteln

1. `GetDefaultBranchAsync(repositoryUrl)` wird aufgerufen
2. `git ls-remote --symref {repositoryUrl} HEAD` wird mit `.netrc`-Authentifizierung ausgeführt
3. Output wird geparst: `ref: refs/heads/{branchName}` → `{branchName}`
4. Falls Parsing fehlschlägt: Standard `"main"` zurückgeben

Beteiligte Komponenten:
- `BitbucketPlugin.GetDefaultBranchAsync()` — Git-basierte Branch-Bestimmung

### 2. Repository-Clone-URL konstruieren

1. `BuildRepositoryCloneUrl(repositoryId, hostingMode)` wird aufgerufen:
   - **Cloud:** `https://bitbucket.org/{repositoryId}.git`
   - **Self-Hosted:** `{baseUrl}/scm/{projectKey}/{repoSlug}.git` (aus `repositoryId`)

Beteiligte Komponenten:
- `BitbucketPlugin.BuildRepositoryCloneUrl()` — URL-Konstruktion

### 3. PR-Payload zusammenstellen

1. JSON-Payload wird erstellt:
   ```json
   {
     "title": "{title}",
     "description": "{body}",
     "source": { "branch": { "name": "{branchName}" } },
     "destination": { "branch": { "name": "{defaultBranch}" } }
   }
   ```

### 4. API-Endpunkt-Bestimmung

1. **Cloud:** `{apiBaseUrl}/2.0/repositories/{repositoryId}/pullrequests`
2. **Self-Hosted:** `{apiBaseUrl}/rest/api/1.0/projects/{projectKey}/repos/{repoSlug}/pull-requests`

Beteiligte Komponenten:
- `BitbucketPlugin.CreatePullRequestAsync()` — Orchestrierung

### 5. PR-Erstellung via API

1. `curl` wird mit `GetCurlAuthArgs()` und Payload ausgeführt
2. Response wird geparst: `id` und `links.html.href` / `links.self[0].href` werden extrahiert
3. `PullRequest` DTO wird erzeugt und zurückgegeben

Beteiligte Komponenten:
- `PullRequest` DTO — Rückgabewert

## Jira-Issues-Abruf-Workflow

### 1. Jira-Konfiguration prüfen

1. Jira URL wird abgerufen
2. Falls nicht konfiguriert: leere List `[]` zurückgeben

### 2. JQL-Abfrage zusammenstellen

1. `jql = "project={jiraProjectKey} ORDER BY created DESC"`
2. Query-Parameter wird URL-kodiert

### 3. Jira-API-Aufruf

1. `curl` gegen `{jiraUrl}/rest/api/3/search?jql={jql}&fields=...` aufrufen
2. Authentifizierung via `GetJiraCurlAuthArgs()`: Base64-kodierte E-Mail und Token

### 4. JSON-Parsing und ADF-Rendering

1. Response wird geparst: `issues` Array extrahieren
2. Für jedes Issue:
   - `key` und `summary` auslesen → `Titel = "KEY: summary"`
   - `description` (Atlassian Document Format) wird via `RenderAdf()` zu Plaintext konvertiert
   - `labels` als Array auslesen
   - `self` URL als `IssueUrl` nutzen
3. `Issue` DTOs werden erzeugt und zurückgegeben

Beteiligte Komponenten:
- `BitbucketPlugin.GetIssuesAsync()` — Jira-Issues-Abruf
- `BitbucketPlugin.RenderAdf()` / `RenderAdfNode()` — ADF-zu-Plaintext-Konvertierung
- `Issue` DTO

## Diagramm

```mermaid
flowchart TD
    A[git clone aufgerufen] --> B{Hosting-Mode?}
    B -->|Cloud| C[URL unverändert]
    B -->|Self-Hosted| D[URL konvertieren]
    C --> E[Credentials einbetten]
    D --> E
    E --> F[.netrc aktualisieren]
    F --> G[git clone ausführen]
    G --> H{Erfolg?}
    H -->|Ja| I[Abschluss]
    H -->|Nein| J[Fehler bereinigen]
    J --> K[Log + Exception]
    
    L[git pull/push aufgerufen] --> M{Hosting-Mode?}
    M -->|Cloud| N[HTTP-Auth-Args: leeres Array]
    M -->|Self-Hosted| O[HTTP-Auth-Args: Basic-Header]
    N --> P[.netrc aktualisieren]
    O --> P
    P --> Q[git pull/push ausführen]
    Q --> R{Erfolg?}
    R -->|Ja| S[Abschluss]
    R -->|Nein| T[Fehler bereinigen]
    T --> U[Log + Exception]
    
    V[Health-Check aufgerufen] --> W[BitBucket-API testen]
    W --> X{Jira konfiguriert?}
    X -->|Ja| Y[Jira-API testen]
    X -->|Nein| Z[nur BitBucket prüfen]
    Y --> AA{Beide OK?}
    Z --> AB{BitBucket OK?}
    AA -->|Ja| AC[Health OK]
    AA -->|Nein| AD[Health NOK]
    AB -->|Ja| AC
    AB -->|Nein| AD
```
