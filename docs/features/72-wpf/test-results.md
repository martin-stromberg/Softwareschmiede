# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests vorhanden.

## Zusammenfassung

- Gesamt: 477
- Bestanden: 477
- Fehlgeschlagen: 0
- Übersprungen: 0

### Details nach Test-Projekt

#### Unit Tests (Softwareschmiede.Tests)
- Gesamt: 391
- Bestanden: 391
- Fehler: 0
- Laufzeit: 3,97 Sekunden

#### Integration Tests (Softwareschmiede.IntegrationTests)
- Gesamt: 86
- Bestanden: 86
- Fehler: 0
- Laufzeit: 7,19 Sekunden

## Testabdeckung

**Gesamt Abdeckung:**

| Testsuite | Zeilenabdeckung | Branchabdeckung | Zeilen (bedeckt/valid) |
|-----------|-----------------|-----------------|----------------------|
| Unit Tests | 28,35 % | 63,83 % | 4359 / 15371 |
| Integration Tests | 44,55 % | 8,83 % | 8133 / 18255 |

## Fehlende Tests

**Quelle:** Coverage-Daten

### Migrations (0% Abdeckung)
Migrationen sind generierte EF Core-Dateien, die ausgeschlossen werden können:

- `src\Softwareschmiede\Migrations\20260506195327_InitialCreate.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260506195327_InitialCreate.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260509200234_202605090001_Add_AppEinstellung_Workdir.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260509200234_202605090001_Add_AppEinstellung_Workdir.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260514114235_202605141200_AddRepositoryStartKonfiguration.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260514114235_202605141200_AddRepositoryStartKonfiguration.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260516085728_202605161100_RemoveStartScriptArguments.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260516085728_202605161100_RemoveStartScriptArguments.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260516093219_202605161130_RemoveRepositoryStartPortSettings.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260516093219_202605161130_RemoveRepositoryStartPortSettings.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260522192947_202605171230_AddDiffComparison.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260522192947_202605171230_AddDiffComparison.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260523052722_202605230001_AddTaskRecoveryIndicators.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260523052722_202605230001_AddTaskRecoveryIndicators.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260523113807_AddKiTaskNotifications.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260523113807_AddKiTaskNotifications.Designer.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260524151645_202605241703_AddKiPluginPrefix.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\20260524151645_202605241703_AddKiPluginPrefix.Designer.cs` — 0% Abdeckung

### Entry Points (0% Abdeckung)
Entry Points werden von Tests nicht direkt aufgerufen (erwartet):

- `src\Softwareschmiede.Client\Program.cs` — 0% Abdeckung (WPF-Client-Einstiegspunkt)
- `src\Softwareschmiede\Program.cs` — 0% Abdeckung (Web-Einstiegspunkt)

### Infrastructure Services (0% Abdeckung in Integration Tests)
Services ohne direkte Abdeckung in den erfassten Tests:

- `src\Softwareschmiede\Infrastructure\Services\AgentPackageReader.cs` — 0% Abdeckung
- `src\Softwareschmiede\Infrastructure\Services\BenutzerkontextService.cs` — 0% Abdeckung
- `src\Softwareschmiede\Infrastructure\Services\CliRunner.cs` — 0% Abdeckung
- `src\Softwareschmiede\Infrastructure\Services\CliSessionService.cs` — 0% Abdeckung
- `src\Softwareschmiede\Infrastructure\Services\SystemShutdownService.cs` — 0% Abdeckung
- `src\Softwareschmiede\Infrastructure\Services\WindowsCredentialStore.cs` — 0% Abdeckung

### Value Objects & Models (0% Abdeckung in Integration Tests)
Domain Value Objects und Models:

- `src\Softwareschmiede\Domain\ValueObjects\BranchCommit.cs` — 0% Abdeckung
- `src\Softwareschmiede\Domain\ValueObjects\FilePreview.cs` — 0% Abdeckung
- `src\Softwareschmiede\Domain\ValueObjects\WorkspaceFileNode.cs` — 0% Abdeckung
- `src\Softwareschmiede\Domain\ValueObjects\WorkspaceFileStatus.cs` — 0% Abdeckung

### Other (0% Abdeckung in Integration Tests)

- `src\Softwareschmiede\Infrastructure\Plugins\PluginManager.cs` — 0% Abdeckung
- `src\Softwareschmiede\Migrations\SoftwareschmiededDbContextModelSnapshot.cs` — 0% Abdeckung (EF Core Snapshot)

## Hinweise

1. **Alle Tests erfolgreich** — Keine Regressionen erkannt.
2. **Migrationen** — EF Core-Migrationsdateien haben erwartungsgemäß 0% Coverage (generierte Code, nicht getestet).
3. **Entry Points** — `Program.cs`-Dateien sind Einstiegspunkte und werden von Unit/Integration Tests nicht direkt ausgeführt.
4. **Infrastructure Services** — Einige Infrastruktur-Services haben keine expliziten Tests. Dies erfordert eine separate Code-Review zur Bestätigung, ob Tests notwendig sind.
5. **Coverage Varianz** — Die Abdeckung unterscheidet sich zwischen Unit Tests (28%) und Integration Tests (44%), da Integration Tests mehr produktiven Code ausführen.
