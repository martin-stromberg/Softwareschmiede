# Datenmodell

## `BitbucketPlugin`
Datei: `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

**Klasse:** `sealed class BitbucketPlugin : GitPluginBase<BitbucketPlugin>`

### Konstanten (Private)
| Konstante | Wert | Zweck |
|-----------|------|-------|
| `BitbucketUserKey` | `"Softwareschmiede.Bitbucket.Username"` | Credential-Store-Schlüssel für Bitbucket-Nutzername |
| `BitbucketAppPasswordKey` | `"Softwareschmiede.Bitbucket.AppPassword"` | Credential-Store-Schlüssel für App-Password |
| `BitbucketWorkspaceKey` | `"Softwareschmiede.Bitbucket.Workspace"` | Credential-Store-Schlüssel für Workspace-Name |
| `RepositoryUrlKey` | `"RepositoryUrl"` | Schlüssel für Repository-URL in Link-Feldern |
| `RepositoryNameKey` | `"RepositoryName"` | Schlüssel für Repository-Name in Link-Feldern |

### Eigenschaften (Public, Abstrakt überschrieben)
| Eigenschaft | Typ | Rückgabewert | Zweck |
|-------------|-----|--------------|-------|
| `PluginName` | Property | `"Bitbucket"` | Anzeigename des Plugins |
| `PluginPrefix` | Property | `"Softwareschmiede.Bitbucket"` | Basis-Präfix für Credential-Store-Schlüssel |
| `PluginType` | Property | `PluginType.SourceCodeManagement` | Plugin-Klassifizierung |

### Abhängigkeiten (Konstruktor)
- `ICliRunner _cliRunner` – CLI-Ausführung für git und curl
- `ICredentialStore _credentialStore` – Zugriff auf gespeicherte Credentials
- `ILogger<BitbucketPlugin> _logger` – Protokollierung

### Value Objects (Verwendete externe Typen)
- `PluginSettingGroup` – Gruppierung von Einstellungsfeldern
- `PluginSettingField` – Einzelne konfigurierbare Einstellung
- `Issue` – Fachliches Datenmodell für Issues (von Jira)
- `PullRequest` – Fachliches Datenmodell für Pull Requests
- `AvailableRepository` – Repräsentation verfügbarer Repositories aus der API
