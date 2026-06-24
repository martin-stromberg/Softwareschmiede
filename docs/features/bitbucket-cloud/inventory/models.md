# Datenmodelle

Das BitbucketPlugin arbeitet mit folgenden Datenmodellen und ValueObjects:

## `CliResult`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/CliResult.cs`

Rückgabetyp von `ICliRunner.RunAsync()`. Enthält das Resultat der CLI-Ausführung.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| ExitCode | `int` | Exit-Code des Prozesses (0 = Erfolg) |
| StdOut | `string` | Standard-Ausgabe |
| StdErr | `string` | Standard-Fehlerausgabe |
| IsSuccess | `bool` | `true` wenn ExitCode == 0 |

## `Issue`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs`

Repräsentiert ein Jira-Issue, das aus der API geparst wurde.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Nummer | `int` | Issue-Nummer (wird auf 0 gesetzt) |
| Titel | `string` | Issue-Schlüssel + Zusammenfassung (z.B. "PROJ-1: Summary") |
| Body | `string?` | Issue-Beschreibung (Atlassian Document Format) |
| Labels | `List<string>` | Zugeordnete Labels |
| Milestone | `string?` | Meilenstein (nicht verwendet) |
| IssueUrl | `string?` | URL zum Issue |

## `PullRequest`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs`

Repräsentiert einen erstellten Pull Request.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Id | `int` | PR-ID von Bitbucket |
| Title | `string` | PR-Titel |
| Link | `string` | URL zur PR-Seite |
| BranchName | `string` | Quell-Branch-Name |

## `AvailableRepository`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AvailableRepository.cs`

Repräsentiert ein aus der API abrufbares Repository.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Name | `string` | Repository-Name (z.B. "my-repo") |
| UpdatedAt | `DateTime` | Letztes Aktualisierungsdatum |
| NameWithOwner | `string` | Vollständiger Name (z.B. "workspace/repo" oder "PROJECT/repo") |
| Url | `string` | Browser-URL des Repositories |

## `PluginSettingGroup`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingGroup.cs`

Konfigurationsgruppe für Plugin-Einstellungen (Authentifizierung, Jira, BitBucket-Hosting).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| GroupName | `string` | Name der Gruppe (z.B. "Authentifizierung") |
| Fields | `IReadOnlyList<PluginSettingField>` | Einstellungsfelder der Gruppe |

## `PluginSettingField`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs`

Ein einzelnes Konfigurationsfeld.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Key | `string` | Schlüssel (z.B. "Username", "AppPassword") |
| Label | `string` | Anzeigebezeichnung (z.B. "Bitbucket Username") |
| FieldType | `PluginSettingFieldType` | Feldtyp (Text, Secret, Url, Enum, etc.) |
| Placeholder | `string` | Platzhalter-Text |
| Description | `string` | Beschreibung des Feldes |
| IsRequired | `bool` | `true` wenn Pflichtfeld |
| EnumOptions | `IReadOnlyList<string>?` | Optionen für Enum-Felder (z.B. ["Cloud", "SelfHosted"]) |

## `GitActionCapabilities`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/GitActionCapabilities.cs`

Beschreibt die Fähigkeiten des Plugins für die UI.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| RepositoryKind | `RepositoryKind` | Repositoriumstyp (RemoteGit) |
| IsWorkingDirectoryCopy | `bool` | `false` (nicht zutreffend für Bitbucket Cloud) |
| CanPush | `bool` | `true` (Bitbucket Cloud unterstützt Push) |
| CanPull | `bool` | `true` (Bitbucket Cloud unterstützt Pull) |
| CanCreatePullRequest | `bool` | `true` (Bitbucket Cloud unterstützt PR-Erstellung) |
| CanMergeToSource | `bool` | `false` (nicht unterstützt) |

## Json-Modelle (für API-Responses)

Das Plugin parst die folgenden JSON-Strukturen von Bitbucket APIs:

### Bitbucket Cloud Jira-Response (GetIssuesAsync)

```
{
  "issues": [
    {
      "key": "PROJ-1",
      "self": "https://...",
      "fields": {
        "summary": "...",
        "description": { "type": "doc", "version": 1, "content": [...] },
        "labels": ["label1", "label2"],
        "status": { "name": "..." }
      }
    }
  ],
  "errorMessages": [...]
}
```

### Bitbucket Cloud Repositories-Response

```
{
  "values": [
    {
      "name": "repo-name",
      "full_name": "workspace/repo-name",
      "updated_on": "2025-01-01T00:00:00Z",
      "links": {
        "html": { "href": "https://bitbucket.org/workspace/repo-name" }
      }
    }
  ]
}
```

### Bitbucket Self-Hosted Repositories-Response

```
{
  "values": [
    {
      "name": "My Repo",
      "slug": "my-repo",
      "project": { "key": "MYPROJ" },
      "updated_on": "2025-01-01T00:00:00Z",
      "created_on": "2024-01-01T00:00:00Z",
      "links": {
        "self": [{ "href": "https://bitbucket.example.com/projects/MYPROJ/repos/my-repo/browse" }]
      }
    }
  ],
  "errors": [...]
}
```

### Bitbucket Cloud Pull Request Response

```
{
  "id": 123,
  "title": "PR Title",
  "description": "PR Description",
  "source": { "branch": { "name": "feature-branch" } },
  "destination": { "branch": { "name": "main" } },
  "links": {
    "html": { "href": "https://bitbucket.org/workspace/repo/pull-requests/123" }
  }
}
```
