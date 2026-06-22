# Tests und Hilfsmethoden

## Status Testabdeckung

**Aktuell:** Keine spezifischen Tests für BitbucketPlugin vorhanden.

Es existieren keine Dateien wie:
- `src/Softwareschmiede.Tests/.../BitbucketPluginTests.cs`
- `src/Softwareschmiede.Tests/.../BitbucketPlugin*.cs`

## Bestehende Test-Infrastruktur für Plugins

### `GitPluginBaseTests`
Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`

Grund-Testklasse für `GitPluginBase`-Unterklassen (wie BitbucketPlugin). Diese können als Vorlage dienen.

### Test-Hilfsmethoden und Fixtures

Können existieren in:
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs` – Testet Plugin-Discovery
- `src/Softwareschmiede.Tests/ServiceIntegration/` – Service-Integrations-Tests

Diese Tests dienen nicht spezifisch für BitbucketPlugin, zeigen aber wie Plugins getestet werden (z.B. wie Mocks für `ICliRunner`, `ICredentialStore`, `ILogger` aufgebaut werden).

## UI-Test-Infrastruktur (SettingsView)

### Bestehende Einstellungs-Tests

`src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs` – Tests für SettingsViewModel

Diese können als Referenz dienen für:
- Laden von Plugin-Einstellungsgruppen
- Speichern von Plugin-Einstellungswerten
- Validierung von Pflichtfeldern

### WPF-View-Tests

`src/Softwareschmiede.Tests/Components/Pages/` – BUnit-basierte Tests (falls Blazor-Komponenten vorhanden, sonst WPF-centric)

## Erforderliche Test-Abdeckung (Laut Anforderung)

Folgende Tests sind im Requirement definiert:

1. **Test für Cloud-Modus:** API-URLs verwenden `api.bitbucket.org`
2. **Test für Self-Hosted-Modus:** API-URLs verwenden konfigurierte URL
3. **Test: `GetSettingGroups()` gibt exakt 3 Gruppen zurück** – Authentifizierung, Jira, BitBucket-Hosting
4. **Test: Beide Setting-Gruppen sind über View korrekt dargestellt**

Diese Tests existieren aktuell **nicht** – müssen neu geschrieben werden.

## Mögliche Test-Strukturen (Vorlage)

Basierend auf bestehendem `GitHubPlugin` und `GitPluginBaseTests`:

```csharp
public class BitbucketPluginTests
{
    private Mock<ICliRunner> _cliRunnerMock;
    private Mock<ICredentialStore> _credentialStoreMock;
    private Mock<ILogger<BitbucketPlugin>> _loggerMock;
    private BitbucketPlugin _plugin;

    [Setup]
    public void Setup()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _loggerMock = new Mock<ILogger<BitbucketPlugin>>();
        
        _plugin = new BitbucketPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            _loggerMock.Object);
    }

    // Test für GetSettingGroups() – sollte 3 Gruppen geben
    // Test für Cloud-Modus – API verwendet api.bitbucket.org
    // Test für Self-Hosted-Modus – API verwendet konfigurierte URL
    // ...
}
```

## Service-Integration Tests

`src/Softwareschmiede.Tests/ServiceIntegration/PluginSettingsServiceIntegrationTests.cs` – Testet speichern/laden von Plugin-Einstellungen

Diese könnten erweitert werden um BitbucketPlugin-spezifische Integration Tests.
