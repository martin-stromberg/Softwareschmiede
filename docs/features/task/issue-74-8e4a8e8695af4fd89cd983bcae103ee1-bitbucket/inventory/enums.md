# Enums und Konstanten

## `PluginSettingFieldType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`

Definiert die Darstellungstypen und Datentypen für Plugin-Einstellungsfelder.

| Wert | Bedeutung / UI-Rendering |
|------|--------------------------|
| `Text` | Einzeiliges Textfeld |
| `Secret` | Maskiertes Passwort- oder Token-Feld (PasswordBox in WPF) |
| `Url` | URL-Eingabefeld (TextBox mit URL-Validierung möglich) |
| `Integer` | Ganzzahl-Eingabefeld |
| `Boolean` | Checkbox |
| `Enum` | Dropdown/ComboBox mit vordefinierter Optionsliste |
| `FilePath` | Dateipfad-Eingabe mit Datei-Auswahl-Dialog |

**Verwendung in BitbucketPlugin:**
- Einstellungsgruppe "Authentifizierung":
  - Username: `PluginSettingFieldType.Text`
  - AppPassword: `PluginSettingFieldType.Secret`
  - Workspace: `PluginSettingFieldType.Text`
- Einstellungsgruppe "Jira":
  - JiraUrl: `PluginSettingFieldType.Url`
  - JiraProjectKey: `PluginSettingFieldType.Text`
  - JiraEmail: `PluginSettingFieldType.Text`
  - JiraApiToken: `PluginSettingFieldType.Secret`
- Repository-Link-Felder:
  - RepositoryUrl: `PluginSettingFieldType.Url`
  - RepositoryName: `PluginSettingFieldType.Text`

## `PluginType`
Datei: `src/Softwareschmiede.Domain/Enums/PluginType.cs` (angenommen)

Klassifizierung aller Plugins.

| Wert | Bedeutung |
|------|-----------|
| `SourceCodeManagement` | Git-Provider-Plugin (GitHub, Bitbucket, GitLab) |
| `DevelopmentAutomation` | KI/Automation-Plugin (Claude, GitHub Copilot, KI Simulator) |

**BitbucketPlugin verwendet:** `PluginType.SourceCodeManagement`

## Credential-Store-Schlüssel (Konstanten in `BitbucketPlugin`)

Diese Konstanten definieren die Identifikatoren für Credentials, die im `ICredentialStore` gespeichert werden.

| Konstante | Wert | Zweck | Feld-Typ in UI |
|-----------|------|-------|-----------------|
| `BitbucketUserKey` | `Softwareschmiede.Bitbucket.Username` | Bitbucket-Benutzername | Text |
| `BitbucketAppPasswordKey` | `Softwareschmiede.Bitbucket.AppPassword` | Bitbucket App Password | Secret |
| `BitbucketWorkspaceKey` | `Softwareschmiede.Bitbucket.Workspace` | Bitbucket Workspace | Text |
| `RepositoryUrlKey` | `RepositoryUrl` | Repository-URL für Link-Felder | Url |
| `RepositoryNameKey` | `RepositoryName` | Repository-Identifier für Link-Felder | Text |

**Jira-bezogene Konstanten** (in `GetIssuesAsync` hardcodiert):
- `Softwareschmiede.Bitbucket.JiraUrl`
- `Softwareschmiede.Bitbucket.JiraProjectKey`
- `Softwareschmiede.Bitbucket.JiraEmail`
- `Softwareschmiede.Bitbucket.JiraApiToken`

## Weitere verwendete Enums

### `RepositoryKind`
Klassifizierung von Repository-Quellen. BitbucketPlugin gibt `RepositoryKind.RemoteGit` zurück (hardcodiert in `GetGitActionCapabilitiesAsync`).

Wert: `RemoteGit` – Repository lebt auf Remote-Server (GitHub, Bitbucket, etc.), nicht lokal.
