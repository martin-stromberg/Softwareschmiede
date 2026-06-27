# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Softwareschmiede.Tests.E2E

- **ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E** — Could not find process with id (Application startup failed - hostpolicy.dll not found)
- **ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E** — Could not find process with id (Application startup failed - hostpolicy.dll not found)
- **ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E** — Could not find process with id (Application startup failed - hostpolicy.dll not found)
- **ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E** — Could not find process with id (Application startup failed - hostpolicy.dll not found)
- **AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E** — Could not find process with id (Application startup failed - hostpolicy.dll not found)
- **AufgabeStarten_KlontRepositoryUndStartetCli_E2E** — Could not find process with id (Application startup failed - hostpolicy.dll not found)
- **E2E_AutoStartCli (1 additional failure in Clipboard-related test)** — Could not find process with id (Application startup failed)

## Zusammenfassung

- Gesamt: 609
- Bestanden: 595
- Fehlgeschlagen: 14
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** 29,80 % (Gesamtabdeckung)

| Datei | Abdeckung |
|-------|-----------|
| src\Softwareschmiede.Plugin.KiSimulator | 100 % |
| src\Softwareschmiede.Plugin.GitHub | 86,36 % |
| src\Softwareschmiede.Plugin.GitHubCopilot | 65 % |
| src\Softwareschmiede.Plugin.ClaudeCli | 64,89 % |
| src\Softwareschmiede.Plugin.Contracts | 68,12 % |
| src\Softwareschmiede.Plugin.Codex | 59,03 % |
| src\Softwareschmiede.Plugin.BitBucket | 50 % |
| src\Softwareschmiede.App | 42,02 % |
| src\Softwareschmiede | 23,58 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

### 0% Abdeckung (Migration und generierte Dateien)

- `src\Softwareschmiede\Migrations\*.cs` (alle Migrations-Dateien) — 0 % Abdeckung (generierte DB-Migrations)
- `src\Softwareschmiede.App\App.xaml.cs` — 0 % Abdeckung (generierte WPF-Initialization)
- `src\Softwareschmiede.App\Controls\TerminalControl.cs` — 0 % Abdeckung (neu implementiertes WPF-Control)

### Unter 80% Abdeckung (kritische Services)

- `src\Softwareschmiede.Plugin.Contracts\Domain\Abstractions\CliKiPluginBase.cs` — 11,11 % (Plugin-Basis)
- `src\Softwareschmiede\Application\Services\ProjektService.cs` — 20 % (Geschäftslogik)
- `src\Softwareschmiede\Application\Services\BenachrichtigungsService.cs` — 20,28 % (Benachrichtigungen)
- `src\Softwareschmiede\Application\Services\EntwicklungsprozessService.cs` — 40-64 % (Kern-Service)
- `src\Softwareschmiede\Application\Services\GitOrchestrationService.cs` — 41,17 % (Git-Integration)
- `src\Softwareschmiede.App\ViewModels\*` — 41-77 % (diverse ViewModels)
- `src\Softwareschmiede\Domain\ValueObjects\BranchCommit.cs` — 50 % (Domain-Modell)

## Anmerkungen

Die E2E-Testfehler sind auf ein Umgebungsproblem zurückzuführen: Die .NET 10.0 Runtime-Datei `hostpolicy.dll` fehlt in `C:\Program Files\dotnet`. Dies ist ein **Infrastruktur-Problem**, kein Code-Fehler.

Die Unit-Tests bestehen zu 97,7% (595/609). Die Testabdeckung liegt bei 29,80%, was typisch für ein großes WPF/Desktop-Anwendungsprojekt mit vielen UI-Komponenten ist. Die niedrige Abdeckung der `Softwareschmiede.App`-Komponente (42,02%) ist bedingt durch nicht testbare WPF-Designs und generierte UI-Codes.

**Kritische Lücken** in Business Logic Services sollten durch zusätzliche Unit-Tests adressiert werden:
- ProjektService (20%)
- BenachrichtigungsService (20%)
- EntwicklungsprozessService (40-64%)
