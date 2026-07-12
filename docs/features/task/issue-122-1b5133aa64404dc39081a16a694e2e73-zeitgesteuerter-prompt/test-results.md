# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests.

## Zusammenfassung

- Gesamt: 870
- Bestanden: 855
- Fehlgeschlagen: 0
- Übersprungen: 15

## Testabdeckung

**Abdeckung:** 72.1 %

| Datei | Abdeckung |
|-------|-----------|
| src/Softwareschmiede.App/App.xaml.cs | 14.4 % |
| src/Softwareschmiede/Application/Services/ProjektService.cs | 20.0 % |
| src/Softwareschmiede/Application/Services/BenachrichtigungsService.cs | 20.3 % |
| src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs | 35.0 % |
| src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs | 37.0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs | 39.6 % |
| src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs | 40.0 % |
| src/Softwareschmiede/Application/Services/GitOrchestrationService.cs | 41.2 % |
| src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs | 41.2 % |
| src/Softwareschmiede/Application/Services/DiffCachingService.cs | 42.4 % |
| src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs | 47.0 % |
| src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs | 48.0 % |
| src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs | 48.1 % |
| src/Softwareschmiede/Domain/ValueObjects/BranchCommit.cs | 50.0 % |
| src/Softwareschmiede.App/Services/DarkModeService.cs | 51.7 % |
| src/Softwareschmiede.App/ViewModels/ViewModelBase.cs | 54.5 % |
| src/Softwareschmiede.App/Services/DesktopIntegration/DesktopIntegrationService.cs | 55.6 % |
| src/Softwareschmiede/Application/Services/TerminalInspectionService.cs | 55.8 % |
| src/Softwareschmiede/Application/Services/AufgabeService.cs | 56.5 % |
| src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs | 57.1 % |
| src/Softwareschmiede/Application/Services/RepositoryConfigurationService.cs | 58.8 % |
| src/Softwareschmiede/Application/Services/RepositoryService.cs | 61.5 % |
| src/Softwareschmiede/Domain/Entities/Aufgabe.cs | 62.5 % |
| src/Softwareschmiede/Domain/Entities/Projekt.cs | 65.7 % |

## Fehlende Tests

Quelle: Coverage-Daten

Dateien mit 0 % Abdeckung (überwiegend generierte Migrations-Dateien):

- src/Softwareschmiede/Migrations/*.cs (189 Dateien) — 0 % Abdeckung (korrekt: generierte Dateien)

Kritische Dateien, die Test-Abdeckung benötigen (< 50 % Abdeckung):

- src/Softwareschmiede.App/App.xaml.cs — 14.4 % Abdeckung
- src/Softwareschmiede/Application/Services/ProjektService.cs — 20.0 % Abdeckung
- src/Softwareschmiede/Application/Services/BenachrichtigungsService.cs — 20.3 % Abdeckung
- src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs — 35.0 % Abdeckung
- src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs — 37.0 % Abdeckung
- src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs — 39.6 % Abdeckung
- src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs — 40.0 % Abdeckung
- src/Softwareschmiede/Application/Services/GitOrchestrationService.cs — 41.2 % Abdeckung

## Hinweise

- 855 Tests bestanden erfolgreich
- 15 ConPTY-Tests wurden übersprungen (Sandbox-Umgebung gemäß SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1)
- Gesamttestlaufzeit: 3 Minuten 7 Sekunden
- Coverage wird über XPlat Code Coverage (Cobertura Format) gemessen
- Generierte Migrations-Dateien (189 Klassen) haben 0 % Abdeckung (erwartetes Verhalten)
- Neue Service-Klasse PromptZeitVersandService hat 48.1 % Abdeckung (aus Issue-122)
