# Enums und Konfigurationen

## `PluginType`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

Bestimmt den Plugin-Typ für Registry und Discovery.

| Wert | Bedeutung |
|------|-----------|
| `SourceCodeManagement` | BitbucketPlugin verwendet diesen Typ |
| `DevelopmentAutomation` | Für KI-Automation-Plugins |

## `PluginSettingFieldType`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`

Bestimmt den Datentyp eines Konfigurationsfeldes.

| Wert | Bedeutung |
|------|-----------|
| `Text` | Einzeiliger Text (Username, Workspace) |
| `Secret` | Maskiertes Feld (AppPassword, JiraApiToken) |
| `Url` | URL-Eingabe (JiraUrl, SelfHostedUrl, RepositoryUrl) |
| `Integer` | Ganzzahl (nicht verwendet in BitbucketPlugin) |
| `Boolean` | Checkbox (nicht verwendet in BitbucketPlugin) |
| `Enum` | Auswahl-Liste (HostingMode: "Cloud", "SelfHosted") |
| `FilePath` | Datei-Dialog (nicht verwendet in BitbucketPlugin) |

## `RepositoryKind`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/RepositoryKind.cs`

Bestimmt die Art des Repositories für Capabilities.

| Wert | Bedeutung |
|------|-----------|
| `RemoteGit` | BitbucketPlugin gibt diese Konstante zurück |

## Hosting-Modus (String-Konstanten)

Datei: `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

Das Plugin unterscheidet Hosting-Modi über String-Vergleiche:

| Wert | Bedeutung |
|------|-----------|
| `"Cloud"` | Bitbucket Cloud (api.bitbucket.org) - Standard |
| `"SelfHosted"` | Self-Hosted Bitbucket Server/Data Center |

Der Modus wird abgerufen über:
```csharp
var hostingMode = _credentialStore.GetCredential("Softwareschmiede.Bitbucket.HostingMode") ?? "Cloud";
```

Der Default ist `"Cloud"` wenn nicht konfiguriert.

### Verwendung in Kontrollflüssen

- **GetGitEnvironment()** (Zeile 167): Unterschiedliche .netrc-Hosts je nach Modus
- **GetGitHttpAuthArgs()** (Zeile 189): HTTP-Header nur bei SelfHosted
- **GetCurlAuthArgs()** (Zeile 213): Bearer (SelfHosted) vs. Basic Auth (Cloud)
- **CloneRepositoryAsync()** (Zeile 280): URL-Konvertierung nur bei SelfHosted
- **CreatePullRequestAsync()** (Zeile 473): Unterschiedliche API-Endpoints
- **CheckHealthAsync()** (Zeile 536): Unterschiedliche Health-Check-URLs
- **GetAvailableRepositoriesAsync()** (Zeile 579): Unterschiedliche API-Pfade
- **GetBitbucketApiBaseUrl()** (Zeile 744): Cloud vs. Self-Hosted API-URL
- **GetBitbucketRepositoriesPath()** (Zeile 760): Cloud vs. Self-Hosted Repository-Pfad
- **BuildRepositoryCloneUrl()** (Zeile 727): Unterschiedliche Clone-URL-Konstruktion

## EnumOptions in PluginSettingField

In `GetSettingGroups()` definiert das Plugin folgende Enum-Optionen:

### HostingMode-Feld (Zeile 116)

```csharp
EnumOptions: ["Cloud", "SelfHosted"]
```

Erlaubte Werte für die Konfiguration des Hosting-Modus in der UI.
