# Bestandsaufnahme: Tests

## Testklassen für TaskDetailViewModel

### `TaskDetailViewModelTests`

Datei: `src\Softwareschmiede.Tests\App\ViewModels\TaskDetailViewModelTests.cs`

Umfang: ~2534 Zeilen

Wichtigste Test-Kategorien (Stichwortsuche):
- Aufgabe laden und anzeigen
- CLI-Prozess-Verwaltung (Start, Stop, Neustart)
- Property-Invalidierung bei Status-Änderungen
- Plugin-Auswahl und Wechsel
- Fehlerbehandlung

### `TaskDetailViewModelTestsBase`

Datei: `src\Softwareschmiede.Tests\App\ViewModels\TaskDetailViewModelTestsBase.cs`

Umfang: ~191 Zeilen

Zweck: Base-Klasse mit gemeinsamen Test-Setup und Hilfsmethoden für TaskDetailViewModel-Tests.

### `TaskDetailViewModelTests_PluginAktivierung`

Datei: `src\Softwareschmiede.Tests\App\ViewModels\TaskDetailViewModelTests_PluginAktivierung.cs`

Umfang: ~142 Zeilen

Zweck: Tests für Plugin-Auswahl und Plugin-Wechsel-Szenarien.

### `TaskDetailViewModelTests_IdeAuswahl`

Datei: `src\Softwareschmiede.Tests\App\ViewModels\TaskDetailViewModelTests_IdeAuswahl.cs`

Umfang: ~522 Zeilen

Zweck: Tests für IDE-Auswahl und IDE-Öffnen-Funktionalität.

### `TaskDetailViewModelTests_Arbeitsverzeichnis`

Datei: `src\Softwareschmiede.Tests\App\ViewModels\TaskDetailViewModelTests_Arbeitsverzeichnis.cs`

Umfang: ~104 Zeilen

Zweck: Tests für Arbeitsverzeichnis-Handling.

### `TaskDetailViewModelTests_Todos`

Datei: `src\Softwareschmiede.Tests\App\ViewModels\TaskDetailViewModelTests_Todos.cs`

Umfang: ~169 Zeilen

Zweck: Tests für To-Do-Listen-Integration.

### `TaskDetailViewModelTests_ZeitgesteuerterPrompt`

Datei: `src\Softwareschmiede.Tests\App\ViewModels\TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`

Umfang: ~288 Zeilen

Zweck: Tests für zeitgesteuerte Prompt-Versendung.

### `TaskDetailViewModelTests_VisualStudioCode`

Datei: `src\Softwareschmiede.Tests\App\ViewModels\TaskDetailViewModelTests_VisualStudioCode.cs`

Umfang: ~117 Zeilen

Zweck: IDE-spezifische Tests für Visual Studio Code.

---

## Test-Hilfsfaktoren

### `TaskDetailViewModelTestFactory`

Datei: `src\Softwareschmiede.Tests\Helpers\TaskDetailViewModelTestFactory.cs`

Zweck: Factory zur Erzeugung vorkonfigurierter TaskDetailViewModel-Instanzen für Tests.

---

## Testklassen für KiAusfuehrungsService

### `KiAusfuehrungsServiceTests`

Datei: `src\Softwareschmiede.Tests\Application\Services\KiAusfuehrungsServiceTests.cs`

Zweck: Unit-Tests für CLI-Prozess-Verwaltung.

### `KiAusfuehrungsServiceTests_WorkingDirectory`

Datei: `src\Softwareschmiede.Tests\Application\Services\KiAusfuehrungsServiceTests_WorkingDirectory.cs`

Zweck: Tests für Arbeitsverzeichnis-Handling beim CLI-Start.

### `KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory`

Datei: `src\Softwareschmiede.Tests\Application\Services\KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory.cs`

Zweck: Tests für Arbeitsverzeichnis-Handling in "In-Source"-Konfiguration.

### `TestKiAusfuehrungsServiceFactory`

Datei: `src\Softwareschmiede.Tests\Helpers\TestKiAusfuehrungsServiceFactory.cs`

Zweck: Test-Factory zur Erzeugung von KiAusfuehrungsService-Instanzen.

---

## Testklassen für AufgabeService

### `AufgabeServiceTests`

Datei: `src\Softwareschmiede.Tests\Application\Services\AufgabeServiceTests.cs`

Zweck: Unit-Tests für Aufgabenverwaltung.

### `AufgabeServiceTests_AktiverLauf`

Datei: `src\Softwareschmiede.Tests\Application\Services\AufgabeServiceTests_AktiverLauf.cs`

Zweck: Tests für aktive Lauf-Verwaltung, insbesondere `AktivenLaufBeendenAsync`.

### Integration Tests

Datei: `src\Softwareschmiede.IntegrationTests\Services\AufgabeServiceTests.cs`

Zweck: Integrations-Tests für AufgabeService.

---

## Testklassen für EntwicklungsprozessService

### `EntwicklungsprozessServiceTests`

Datei: `src\Softwareschmiede.Tests\Application\Services\EntwicklungsprozessServiceTests.cs`

Zweck: Unit-Tests für Repository-Setup und Prozess-Orchestrierung.

### `EntwicklungsprozessServiceTests_BasisBranch`

Datei: `src\Softwareschmiede.Tests\Application\Services\EntwicklungsprozessServiceTests_BasisBranch.cs`

Zweck: Tests für Basis-Branch-Handling.

### `EntwicklungsprozessServiceTests_WorkingDirectoryValidation`

Datei: `src\Softwareschmiede.Tests\Application\Services\EntwicklungsprozessServiceTests_WorkingDirectoryValidation.cs`

Zweck: Tests für Arbeitsverzeichnis-Validierung nach Klon.

### Integration Tests

Datei: `src\Softwareschmiede.IntegrationTests\Services\EntwicklungsprozessServiceTests.cs`

Zweck: Integrations-Tests für EntwicklungsprozessService.

---

## E2E Tests

### `E2E_FileExplorer`

Datei: `src\Softwareschmiede.Tests\E2E\E2E_FileExplorer.cs`

Kontext: Enthält Hinweise auf `ShowCliPanel` und dessen Verhalten bei Status-Änderungen.

Relevante Hinweise:
- Verfügbarkeit des Panels hängt von `ShowCliPanel` ab
- Während ganzer Testdauer sollte `ShowCliPanel == true` bleiben (Status=Gestartet)
- Panels müssen nicht verschwinden, wenn `ShowCliPanel` weiterhin `true` ist

---

## Zusammenfassung der Test-Abdeckung

| Aspekt | Test-Abdeckung | Status |
|--------|-----------------|--------|
| `ShowCliPanel` Property | Indirekt via Status-Tests | Vorhanden, aber nicht spezifisch |
| `SollCliAnzeigen` Extension | Keine direkt identifiziert | **Lücke** |
| CLI-Start/Stop | Umfassend | Vorhanden |
| Property-Invalidierung | Vorhanden | Vorhanden |
| Plugin-Wechsel | Spezifische Tests | Vorhanden |
| Status-Übergänge | Teilweise | Teilweise |
| Beendet-Status | Implizit in Tests | Implizit |
