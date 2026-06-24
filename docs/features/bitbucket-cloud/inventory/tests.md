# Tests und Hilfsmethoden

## Testklassen

### `BitbucketPluginTests`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/BitbucketPluginTests.cs`

Umfassende Unit-Tests für das `BitbucketPlugin`.

#### Test-Setup

- **System Under Test (SUT)**: `BitbucketPlugin`
- **Mocks**: `ICliRunner`, `ICredentialStore`, `ILogger<BitbucketPlugin>`
- **Test-Framework**: xUnit mit FluentAssertions

#### Testmethoden nach Kategorie

##### Konfiguration / Setting Groups

- `GetSettingGroups_ShouldReturnThreeGroups()` — Prüft, dass genau 3 Gruppen zurückgegeben werden (Authentifizierung, Jira, BitBucket-Hosting)
- `GetSettingGroups_ShouldHaveCorrectGroupNames()` — Prüft Gruppennamen
- `GetSettingGroups_BitBucketHostingGroup_ShouldHaveTwoFields()` — Prüft BitBucket-Hosting-Gruppe hat 2 Felder
- `GetSettingGroups_HostingModeField_ShouldHaveEnumOptions()` — Prüft HostingMode hat Enum-Optionen ["Cloud", "SelfHosted"]
- `GetSettingGroups_SelfHostedUrlField_ShouldBeOptional()` — Prüft SelfHostedUrl ist optional

##### GetBitbucketApiBaseUrl

- `GetBitbucketApiBaseUrl_ShouldReturnCloudUrl_WhenHostingModeIsCloud()` — Cloud → https://api.bitbucket.org
- `GetBitbucketApiBaseUrl_ShouldReturnCloudUrl_WhenHostingModeIsNotSet()` — Default → https://api.bitbucket.org
- `GetBitbucketApiBaseUrl_ShouldReturnSelfHostedUrl_WhenHostingModeIsSelfHosted()` — Self-Hosted → konfigurierte URL
- `GetBitbucketApiBaseUrl_ShouldTrimTrailingSlash_WhenSelfHostedUrlHasTrailingSlash()` — Trailing Slash wird entfernt
- `GetBitbucketApiBaseUrl_ShouldSupportPort_WhenSelfHostedUrlIncludesPort()` — Port wird beachtet
- `GetBitbucketApiBaseUrl_ShouldThrow_WhenSelfHostedUrlIsEmpty()` — Exception wenn Self-Hosted URL fehlt

##### GetBitbucketRepositoriesPath

- `GetBitbucketRepositoriesPath_ShouldReturnCloudPath_WhenHostingModeIsCloud()` — Cloud → /2.0/repositories/{workspace}?pagelen=100
- `GetBitbucketRepositoriesPath_ShouldReturnSelfHostedPath_WhenHostingModeIsSelfHosted()` — Self-Hosted → /rest/api/1.0/projects/{workspace}/repos

##### CheckHealthAsync

- `CheckHealthAsync_ShouldThrow_WhenSelfHostedAndUrlIsEmpty()` — Exception bei fehlender Self-Hosted-URL
- `CheckHealthAsync_ShouldUseCloudApiUrl_WhenHostingModeIsCloud()` — Cloud nutzt api.bitbucket.org/2.0/user
- `CheckHealthAsync_ShouldUseSelfHostedApiUrl_WhenHostingModeIsSelfHosted()` — Self-Hosted nutzt konfigurierte URL/rest/api/1.0/user
- `CheckHealthAsync_ShouldSkipJira_WhenJiraUrlIsNull()` — Jira-Check übersprungen wenn JiraUrl nicht konfiguriert

##### GetAvailableRepositoriesAsync

- `GetAvailableRepositoriesAsync_ShouldParseCloudJson()` — Parst Cloud-JSON korrekt (full_name, html.href)
- `GetAvailableRepositoriesAsync_ShouldParseSelfHostedJson()` — Parst Self-Hosted-JSON korrekt (slug, project.key, self[0].href)

##### GetIssuesAsync

- `GetIssuesAsync_ShouldHandleNullDescription()` — Behandelt null-Beschreibung ohne Exception

##### ResolveGitCloneUrl (Theory-Test)

Testet URL-Konvertierung für Self-Hosted (Browser-URL → SCM-URL):

- `https://bitbucket.vectron.de/projects/ERP/repos/udr-aufbereitung/browse` → `https://bitbucket.vectron.de/scm/ERP/udr-aufbereitung.git`
- `https://bitbucket.vectron.de/projects/ERP/repos/udr-aufbereitung` → `https://bitbucket.vectron.de/scm/ERP/udr-aufbereitung.git`
- `https://bitbucket.example.com:7990/projects/MY/repos/myrepo/browse` → `https://bitbucket.example.com:7990/scm/MY/myrepo.git` (mit Port)
- `https://bitbucket.example.com/rest/api/1.0/projects/KEY/repos/slug` → `https://bitbucket.example.com/scm/KEY/slug.git` (API-URL)
- `https://bitbucket.example.com/scm/KEY/slug.git` → unverändert (bereits SCM-URL)
- `https://bitbucket.example.com/scm/KEY/slug` → `https://bitbucket.example.com/scm/KEY/slug.git` (fügt .git hinzu)

##### GetDefaultBranchAsync

- `GetDefaultBranchAsync_ShouldTrimCarriageReturn_WhenOutputHasCRLF()` — Entfernt \r\n aus Windows-Ausgabe

#### Test-Daten (JSON-Fixtures)

##### Cloud Repository-JSON

```json
{
  "values": [
    {
      "name": "my-repo",
      "full_name": "myworkspace/my-repo",
      "updated_on": "2025-01-01T00:00:00Z",
      "links": {
        "html": { "href": "https://bitbucket.org/myworkspace/my-repo" }
      }
    }
  ]
}
```

##### Self-Hosted Repository-JSON

```json
{
  "values": [
    {
      "name": "My Repo",
      "slug": "my-repo",
      "project": { "key": "MYPROJ" },
      "links": {
        "self": [{ "href": "https://bitbucket.example.com/projects/MYPROJ/repos/my-repo/browse" }]
      }
    }
  ]
}
```

##### Jira Issues-JSON

```json
{
  "issues": [
    {
      "key": "PROJ-1",
      "fields": {
        "summary": "Ein Issue ohne Beschreibung",
        "description": null,
        "labels": []
      }
    }
  ]
}
```

#### Test-Abdeckung nach Anforderung

Der Test-Suite deckt folgende Aspekte der Anforderung ab:

- **Hosting-Mode-Differenzierung**: Tests prüfen Cloud vs. Self-Hosted in API-URLs, Repository-Pfaden, Health-Checks
- **Cloud-Support**: Tests verifizieren Cloud-spezifische API-Endpoints und JSON-Parsing
- **Self-Hosted-Support**: Tests verifizieren Self-Hosted-API-Endpoints, URL-Konvertierung, Port-Unterstützung
- **Fehlerbehandlung**: Tests prüfen Exception-Handling bei fehlender Konfiguration
- **JSON-Parsing**: Tests prüfen korrekte Extraktion von Cloud/Self-Hosted-spezifischen Feldern

#### Offene Lücken (nicht getestet)

- `CloneRepositoryAsync()` — Keine direkten Tests (würde echte Git-Calls benötigen)
- `PullAsync()` / `PushBranchAsync()` — Keine direkten Tests
- `CreatePullRequestAsync()` — Keine direkten Tests (würde echte API-Calls benötigen)
- `.netrc`-Datei-Handling in `UpdateNetrcEntry()` — Nicht getestet
- `BuildAuthenticatedCloneUrl()` — Nicht direkt getestet (statische Hilfsmethode)
- ADF-Rendering (`RenderAdf`, `RenderAdfNode`) — Nicht explizit getestet
- Curl-Auth-Argument-Konstruktion in `GetCurlAuthArgs()` — Nicht direkt getestet

### `GitPluginBaseTests`

Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`

Tests für die Basisklasse `GitPluginBase<TPlugin>`. Tests für Standard-Implementierungen von:
- `CreateBranchAsync()`
- `CommitAsync()`
- `AddAllAsync()`
- `EnsureGitRepositoryAsync()`

Diese Tests sind nicht spezifisch für BitbucketPlugin, aber relevant für die Git-Operationen, die das Plugin verwendet.

## Mock-Setup

In `BitbucketPluginTests`:

```csharp
_cliRunnerMock = new Mock<ICliRunner>();
_credentialStoreMock = new Mock<ICredentialStore>();
_sut = new BitbucketPlugin(
    _cliRunnerMock.Object,
    _credentialStoreMock.Object,
    new Mock<ILogger<BitbucketPlugin>>().Object);
```

### Setup-Patterns

**Credential Mock für Cloud-Modus**:
```csharp
_credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
    .Returns("Cloud");
_credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>()))
    .Returns("test-value");
```

**Credential Mock für Self-Hosted-Modus**:
```csharp
_credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode"))
    .Returns("SelfHosted");
_credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl"))
    .Returns("https://bitbucket.example.com");
```

**CLI Runner Mock**:
```csharp
_cliRunnerMock.Setup(c => c.RunAsync(
    "curl",
    It.IsAny<IEnumerable<string>>(),
    null,
    null,
    It.IsAny<CancellationToken>()))
.ReturnsAsync(new CliResult(0, json, string.Empty));
```

## Hilfsmethoden

### Interne Test-Hilfsmethoden

Das Plugin selbst hat keine öffentlichen Test-Hilfsmethoden. Die Tests nutzen:

1. **Moq Mocks** für Abhängigkeiten-Isolation
2. **FluentAssertions** für lesbare Assertions
3. **xUnit Facts und Theories** für parametrisierte Tests

### Produktions-Hilfsmethoden (interne Nutzung)

Das Plugin enthält statische Hilfsmethoden, die von Tests indirekt getestet werden:

- `ResolveGitCloneUrl()` — Direkt getestet mit 6 Theory-Szenarien
- `ParseJiraIssues()` — Indirekt getestet via `GetIssuesAsync()`
- `RenderAdf()` / `RenderAdfNode()` / `RenderAdfChildren()` — Indirekt getestet via Issue-Parsing
- `UpdateNetrcEntry()` — Intern verwendet, nicht direkt getestet
- `BuildAuthenticatedCloneUrl()` — Intern verwendet, nicht direkt getestet
- `HasBitbucketErrors()` / `HasBitbucketApiError()` — Intern verwendet in API-Response-Handling

## Test-Struktur nach Anforderung

Die Tests strukturieren sich entlang der Anforderungs-Themen:

| Anforderungs-Aspekt | Test-Klasse | Testmethoden |
|-------------------|-----------|-------------|
| Cloud-Unterstützung | BitbucketPluginTests | GetBitbucketApiBaseUrl_Cloud, CheckHealthAsync_Cloud, GetAvailableRepositoriesAsync_Cloud |
| Self-Hosted-Unterstützung | BitbucketPluginTests | GetBitbucketApiBaseUrl_SelfHosted, GetBitbucketRepositoriesPath_SelfHosted, ResolveGitCloneUrl (alle Varianten) |
| Hosting-Mode-Konfiguration | BitbucketPluginTests | GetSettingGroups_BitBucketHostingGroup, HostingMode-Enum-Options |
| Error-Handling | BitbucketPluginTests | GetBitbucketApiBaseUrl_Throw, CheckHealthAsync_Throw |
| JSON-Parsing | BitbucketPluginTests | GetAvailableRepositoriesAsync_Cloud/SelfHosted, GetIssuesAsync |
