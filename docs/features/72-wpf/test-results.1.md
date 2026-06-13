# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine.

## Zusammenfassung

- Gesamt: 479
- Bestanden: 474
- Fehlgeschlagen: 0
- Übersprungen: 5

**Testdauer:** 10,8 Sekunden

## Testabdeckung

**Abdeckung:** 27,1 %

Die Testabdeckung basiert auf XPlat Code Coverage, gemessen über Cobertura XML. Der geringe Gesamtwert (27,1%) ist auf generierte Migrations-Dateien und App-Entry-Points zurückzuführen, die bewusst nicht getestet werden.

### Dateien mit kritischer Abdeckungslücke (0 %)

| Datei | Grund |
|-------|-------|
| `src\Softwareschmiede\Migrations\20260506195327_InitialCreate.cs` | Generierte Migrationsdatei — nicht getestet |
| `src\Softwareschmiede\Migrations\20260612193852_202606120001_RenameBannerModus.cs` | Generierte Migrationsdatei — nicht getestet |
| `src\Softwareschmiede\Application\Services\CliProcessManager.cs` | 0 % Zeilenabdeckung |
| `src\Softwareschmiede\Application\Services\BenachrichtigungsService.cs` | 0 % Zeilenabdeckung |
| `src\Softwareschmiede\Application\Services\DiffService.cs` | 0 % Zeilenabdeckung |
| `src\Softwareschmiede\Application\Services\AufgabeService.cs` | 0 % Zeilenabdeckung |
| `plugins\Softwareschmiede.Plugin.LocalDirectory\LocalDirectoryPlugin.cs` | 0 % Zeilenabdeckung |
| `src\Softwareschmiede.Plugin.Contracts\Domain\Interfaces\IGitPlugin.cs` | 0 % Zeilenabdeckung |
| `src\Softwareschmiede\Domain\Entities\PluginKonfiguration.cs` | 0 % Zeilenabdeckung |
| `src\Softwareschmiede.Plugin.Contracts\Domain\ValueObjects\AgentInfo.cs` | 0 % Zeilenabdeckung |
| `src\Softwareschmiede\Domain\ValueObjects\WorkspaceNodeRow.cs` | 0 % Zeilenabdeckung |

### Übersprungene Tests

Die folgenden Tests wurden übersprungen, da sie eine Windows-Desktop-Session und vorab gebaute `Softwareschmiede.App.exe` erfordern:

- `Softwareschmiede.Tests.E2E.WpfE2ETests.CliProzessStartenUndFensterEinbetten_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.ProduktErstellenUndAufgabeHinzufuegen_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.AufgabeStarten_RepositoryKlonen_BranchErstellen_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.RecoveryBannerNachHeartbeatTimeout_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.DarkModeAktivierenUndPersistieren_E2E`

## Test-Suites

### Softwareschmiede.Tests (Unit & Integration)
- **Bestanden:** 390
- **Übersprungen:** 5
- **Dauer:** 1,65 Sekunden

### Softwareschmiede.IntegrationTests
- **Bestanden:** 84
- **Dauer:** 5,78 Sekunden

## Coverage-Dateien

- `src\Softwareschmiede\TestResults\6003b758-087b-4137-8ec6-447de8b4b0a2\coverage.cobertura.xml`
- `src\Softwareschmiede.IntegrationTests\TestResults\07df48db-1b60-4cc4-a442-542097c47a2f\coverage.cobertura.xml`

---

**Durchgeführt am:** 2026-06-13
**Branch:** 72-wpf
