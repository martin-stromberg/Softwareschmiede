# Bestandsaufnahme: BitBucket Cloud Support

Diese Bestandsaufnahme dokumentiert den bestehenden Softwareschmiede-Code bezogen auf die Anforderung zur Authentifizierung gegen Bitbucket Cloud. Der Fokus liegt auf Git-Operationen (`git clone`) und API-Integrationen für beide Hosting-Modi (Cloud und Self-Hosted).

## Zusammenfassung

### Vorhanden

- **BitbucketPlugin**: Vollständig implementierte Logikklasse mit Differenzierung zwischen Cloud und Self-Hosted
  - Bereits unterschiedliche API-Endpoints für Cloud vs. Self-Hosted
  - `.netrc`-basierte Authentifizierung mit Host-Differenzierung
  - HTTP-Basic-Auth für Cloud, Bearer-Token für Self-Hosted (curl)
  - URL-Konvertierung für Self-Hosted Repository-Klone

- **Konfiguration**: Hosting-Mode-Setting (Cloud/SelfHosted) bereits implementiert
  - 3 Einstellungsgruppen: Authentifizierung, Jira, BitBucket-Hosting
  - Enum-basierter Hosting-Mode mit Optionen ["Cloud", "SelfHosted"]
  - Self-Hosted-URL optional konfigurierbar

- **Tests**: Umfangreiche Unit-Tests für Cloud und Self-Hosted
  - Testet API-URL-Differenzierung
  - Testet JSON-Parsing für beide Modi
  - Testet URL-Konvertierung für Self-Hosted
  - Testet Error-Handling bei fehlender Konfiguration

- **Interfaces und Abstraktion**: Vollständig definiert
  - `IGitPlugin` Interface mit allen Git-Operationen
  - `ICliRunner` für Prozess-Ausführung
  - `ICredentialStore` für sichere Credential-Verwaltung
  - `GitPluginBase<T>` mit Standard-Implementierungen

- **Datenmodelle**: ValueObjects für API-Responses
  - `Issue`, `PullRequest`, `AvailableRepository`
  - `CliResult`, `GitActionCapabilities`
  - `PluginSettingGroup`, `PluginSettingField`

### Kritische Beobachtungen bezüglich der Anforderung

1. **Git Clone für Cloud**: 
   - `BuildAuthenticatedCloneUrl()` embeddet Credentials direkt in URL
   - `.netrc` wird aktualisiert mit Host `bitbucket.org` für Cloud
   - `GetGitEnvironment()` setzt `GIT_TERMINAL_PROMPT=0` (bereits vorhanden)
   - **Potenzielle Lücke**: `GetGitHttpAuthArgs()` ist leer für Cloud — HTTP-Header-Auth nur bei Self-Hosted

2. **Pull/Push für Cloud**:
   - `PullAsync()` und `PushBranchAsync()` nutzen `GetGitHttpAuthArgs()` 
   - Für Cloud wird ein leeres Array zurückgegeben
   - **Abhängigkeit**: Annahme, dass `.netrc`-Eintrag (`bitbucket.org`) von Git automatisch genutzt wird

3. **API-Aufrufe**:
   - Curl-Auth für Cloud: `-u user:token` (HTTP Basic Auth)
   - Curl-Auth für Self-Hosted: `-H "Authorization: Bearer token"`
   - Jira-Auth: immer `-H "Authorization: Basic ..."` (mit Email + API Token)

4. **Fehlerbehandlung**:
   - Logging mit `ILogger<BitbucketPlugin>` vorhanden
   - API-Fehler werden parst und geloggt
   - Git-Fehler (StdErr) werden in InvalidOperationException geworfen

### Fehlende oder unklar implementierte Aspekte

- **Direct URL embedding vs. .netrc Mischung**: Clone nutzt eingebettete Credentials, Pull/Push nutzen .netrc. Konsistenz nicht erkennbar.
- **GIT_CREDENTIAL_HELPER**: Nicht gesetzt. Plugin verlässt sich auf .netrc-Fallback.
- **Terminal-Prompts**: `GIT_TERMINAL_PROMPT=0` ist gesetzt, aber nicht klar, ob dies ausreicht für CI/CD-Umgebungen ohne TTY.
- **SSH-Unterstützung**: Keine SSH-Implementierung sichtbar. Nur HTTPS.
- **Token Format für Cloud**: App Passwords oder OAuth2? Anforderung nennt "oauth2:" als Benutzer für Cloud — nicht sichtbar in Code.

## Details

- [Datenmodelle](inventory/models.md) — ValueObjects und JSON-Strukturen
- [Logikklassen](inventory/logic.md) — BitbucketPlugin mit Methoden und Kontrollflüssen
- [Enums und Konfiguration](inventory/enums.md) — Hosting-Mode und PluginSettingFieldType
- [Interfaces und Abstraktion](inventory/interfaces.md) — IGitPlugin, ICliRunner, ICredentialStore, GitPluginBase
- [Tests](inventory/tests.md) — Testklassen, Testmethoden, Test-Daten und Mock-Setup

## Quelldateien

### Haupt-Plugin
- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

### Contracts und Abstraktion
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/CliResult.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AvailableRepository.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/GitActionCapabilities.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingGroup.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs`

### Tests
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/BitbucketPluginTests.cs`
- `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`

## Anforderungs-Tracking

| Anforderungs-Punkt | Status | Details |
|-------------------|--------|---------|
| Differenzierung nach Hosting-Modus | ✓ Implementiert | `GetGitEnvironment()`, `GetCurlAuthArgs()`, `GetBitbucketApiBaseUrl()` unterscheiden Cloud/SelfHosted |
| Cloud API-Endpoints | ✓ Implementiert | `api.bitbucket.org/2.0/` für Cloud, Custom-URL für Self-Hosted |
| Cloud Authentifizierung (curl) | ✓ Implementiert | `-u user:token` für Cloud |
| Self-Hosted Authentifizierung (curl) | ✓ Implementiert | `-H "Authorization: Bearer token"` für Self-Hosted |
| Git Clone mit Credentials | ✓ Implementiert | `BuildAuthenticatedCloneUrl()` embeddet Credentials |
| .netrc-Unterstützung | ✓ Implementiert | `UpdateNetrcEntry()` aktualisiert .netrc je nach Host |
| GIT_TERMINAL_PROMPT=0 | ✓ Implementiert | In `GetGitEnvironment()` gesetzt |
| Self-Hosted URL-Konvertierung | ✓ Implementiert | `ResolveGitCloneUrl()` Browser-/API-URL → SCM-URL |
| Jira-Integration | ✓ Implementiert | `GetIssuesAsync()` mit ADF-Rendering |
| Health-Check | ✓ Implementiert | `CheckHealthAsync()` mit Cloud/Self-Hosted Differenzierung |
| Fehlerbehandlung | ✓ Implementiert | Logging, Exception-Werfen bei Fehlern |
| Tests für Cloud | ✓ Implementiert | `GetBitbucketApiBaseUrl_Cloud`, `CheckHealthAsync_Cloud`, etc. |
| Tests für Self-Hosted | ✓ Implementiert | `GetBitbucketApiBaseUrl_SelfHosted`, `ResolveGitCloneUrl`, etc. |
| Konfiguration (Hosting-Mode) | ✓ Implementiert | Enum-Feld in "BitBucket-Hosting"-Gruppe |

## Notizen zur Anforderungsanalyse

Die Anforderung beschreibt, dass Bitbucket Cloud mit Authentifizierungsfehlern ("Invalid username or token") fehlschlägt. Der Code zeigt:

1. **Clone-Operationen** verwenden `BuildAuthenticatedCloneUrl()` mit eingebetteten Credentials
2. **Pull/Push-Operationen** basieren auf `.netrc`-Einträgen
3. Unterschiedliche Authentifizierungsmechanismen für `curl` (API-Aufrufe) und `git` (Git-Operationen)

Die Implementierung deutet darauf hin, dass das Plugin bereits zum Support von Cloud entworfen wurde, aber die Frage bleibt offen, ob die `.netrc`-basierte Authentifizierung für Cloud tatsächlich funktioniert oder ob noch Anpassungen (z.B. `GIT_CREDENTIAL_HELPER`, oder direktes Token-Embedding auch für Pull/Push) notwendig sind.
