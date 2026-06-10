# Tests

## Unit-Tests

### `AufgabeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`

Testet CRUD-Operationen und Lebenszyklus-Methoden von `AufgabeService`.

- Testen von `CreateAsync()`, `UpdateAsync()`, `DeleteAsync()`
- Testen von Status-Übergängen (`StartenAsync()`, `KiAktiviertAsync()`, `AbschliessenAsync()`, etc.)
- Testen von Prompt-Vorschlag-Verwaltung (`SavePromptVorschlagAsync()`, `ClearPromptVorschlagAsync()`)

### `ProtokollServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProtokollServiceTests.cs`

Testet Protokoll-Verwaltung.

- Testen von `AddEintragAsync()`
- Testen von `AddTestErgebnisseAsync()`
- Testen von `AddStatusUebergangAsync()`
- Testen von Suchen-Funktionalität

### `ProjektServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProjektServiceTests.cs`

Testet Projekt-CRUD und Archivierung.

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

Testet Session-Management und Streaming-Funktionalität.

- Testen von `StartKiLauf()`
- Testen von Buffering und Subscription
- Testen von `RunningCountChanged` Event

### `AufgabeRecoveryServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeRecoveryServiceTests.cs`

Testet Recovery-Logik für festhängende Aufgaben.

- Testen von `RecoverManuellAsync()`
- Validierung von Wiederherstellungs-Bedingungen

### `GitOrchestrationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`

Testet Git-Operationen und Orchestrierung.

- Testen von `CommitAsync()`
- Testen von `ResetAsync()`
- Mocking von `IGitPlugin`

### `EntwicklungsprozessServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

Testet den KI-gestützten Entwicklungsprozess.

- Prozessstart und -orchestrierung
- Kontext-Management

### `PluginManagerTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`

Testet Plugin-Loading und -Discovery.

- Testen von `GetSourceCodeManagementPlugins()`
- Testen von `GetDevelopmentAutomationPlugins()`
- Testen von Plugin-Auswahl

### `GitPluginBaseTests`
Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`

Testet die Basis-Klasse für Git-Plugins.

### `GitHubPluginTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`

Testet GitHub-Plugin-Implementierung.

### `LocalDirectoryPluginTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests.cs`

Testet Local Directory Plugin für lokale Verzeichnisse.

### `KiSimulatorPluginTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/KiSimulatorPluginTests.cs`

Testet Simulator-KI-Plugin (für Test-Zwecke).

### `BenachrichtigungsEinstellungenServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/BenachrichtigungsEinstellungenServiceTests.cs`

Testet Benachrichtigungs-Einstellungen.

### `PluginDefaultSettingsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PluginDefaultSettingsServiceTests.cs`

Testet Standard-Einstellungen für Plugins.

### `PluginSelectionServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests.cs`

Testet Plugin-Auswahl-Logik.

### `ArbeitsverzeichnisSettingsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ArbeitsverzeichnisSettingsServiceTests.cs`

Testet Arbeitsverzeichnis-Einstellungen.

### `ArbeitsverzeichnisResolverTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Services/ArbeitsverzeichnisResolverTests.cs`

Testet Arbeitsverzeichnis-Auflösung.

### `DiffServiceTests`, `DiffAlgorithmServiceTests`, `DiffCachingServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/Diff*.cs`

Testen Diff-Generierung, -Algorithmus und -Caching.

## Integrations-Tests

### `AufgabeServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`

Integrations-Tests mit echter SQLite In-Memory Datenbank.

### `ProtokollServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/ProtokollServiceTests.cs`

Integrations-Tests für Protokoll-Service mit echter DB.

### `ProjektServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/ProjektServiceTests.cs`

Integrations-Tests für Projekt-Service mit echter DB.

### `AufgabeRecoveryServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/AufgabeRecoveryServiceTests.cs`

Integrations-Tests für Recovery-Service.

### `EntwicklungsprozessServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs`

Integrations-Tests für Entwicklungsprozess-Service.

### `AgentPackageFileServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/AgentPackageFileServiceTests.cs`

Integrations-Tests für Agentenpaket-Dateiverwaltung.

### `LocalDirectoryPluginIntegrationTests`
Datei: `src/Softwareschmiede.IntegrationTests/Infrastructure/Plugins/LocalDirectoryPluginIntegrationTests.cs`

Integrations-Tests für Local Directory Plugin (echte Dateisystem-Operationen).

## Test-Hilfsmethoden

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`

Hilfsmethode zum Erstellen von In-Memory SQLite Datenbanken für Tests.

- Erstellt eine neue `SoftwareschmiededDbContext` Instanz mit In-Memory SQLite
- Führt Migrations durch
- Wird verwendet von Unit-Tests

## Fehlende Test-Coverage (Anforderung)

Entsprechend der Anforderung sollten folgende Bereiche getestet sein (siehe `tests` Section in `requirement.md`):

- **Statusübergänge:** `AufgabeStatus`-Transitionslogik
- **Plugin-Ladeprozess:** Plugin-Discovery und -Instanziierung
- **Prozessverwaltung:** KI-CLI Start, Stop, Streaming
- **Git-Operationen:** Mit Mocking (clone, push, pull, etc.)
- **Einstellungen & Validierungen:** Plugin-Einstellungen, App-Einstellungen
- **Datenbankzugriffe:** SQLite In-Memory, Migrations

Diese sind teilweise abgedeckt, aber noch nicht vollständig (z.B. fehlende UI-Tests für XAML-Views, fehlende End-to-End Tests für komplette Workflows mit WPF-UI).
