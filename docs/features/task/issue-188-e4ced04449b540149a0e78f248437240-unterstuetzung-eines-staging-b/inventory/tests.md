# Tests

## Testklassen

### Unit-Tests

#### `RepositoryStartskriptServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/RepositoryStartskriptServiceTests.cs`

- `RunAsync_ShouldSkipExecution_WhenConfigurationIsInactive` — Prüft, dass Ausführung übersprungen wird, wenn Konfiguration deaktiviert ist.
- `RunAsync_ShouldThrow_WhenScriptPathEscapesRepositoryRoot` — Validiert Sicherheit: Skript muss innerhalb Repository-Root liegen.
- `RunAsync_ShouldPassOnlyScriptArgumentsWithoutPortContract_WhenScriptExecutionSucceeds` — Prüft korrekte PowerShell-Argumente beim erfolgreichen Start.
- `RunAsync_ShouldThrow_WhenCliExecutionFails` — Prüft Fehlerbehandlung bei gescheiterter Skriptausführung.

#### `EntwicklungsprozessServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

Standardtests für Repository-Setup (Klon, Branch-Erstellung). **Keine Tests für Basis-Branch-Szenarien.**

#### `EntwicklungsprozessServiceTests_WorkingDirectoryValidation`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests_WorkingDirectoryValidation.cs`

Tests für Arbeitsverzeichnis-Validierung nach Klon.

### Integrationstests

#### `EntwicklungsprozessServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs`

Integrationstests für vollständige Workflows mit echter Datenbankzugriff.

#### `PullRequestMonitoringServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PullRequestMonitoringServiceTests.cs`

Tests für PR-Monitoring und Status-Updates.

#### `PullRequestReferenzServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PullRequestReferenzServiceTests.cs`

Tests für Persistierung von PR-Referenzen.

## Fehlende Tests

Keine Tests existieren für:
- Basis-Branch-Validierung (ob Branch im Remote existiert)
- Feature-Branch-Erstellung vom konfigurierten Basis-Branch
- PR-Erstellung mit konfigurierten Basis-Branch als Ziel
- Fehlerbehandlung bei nicht-existierendem Basis-Branch
- Verhalten, wenn Basis-Branch gelöscht wird (sofern konfiguriert)

## Test-Hilfsmethoden

### `TestKiAusfuehrungsServiceFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestKiAusfuehrungsServiceFactory.cs`

Factory zur Erstellung von Test-Instanzen des `KiAusfuehrungsService`.

### Test-DbContextFactory
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`

Factory zum Erstellen von Test-DbContext-Instanzen für Tests mit Datenbankzugriff.
