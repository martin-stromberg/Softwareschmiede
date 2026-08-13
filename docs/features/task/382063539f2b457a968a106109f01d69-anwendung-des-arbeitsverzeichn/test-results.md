# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests gefunden.

## Zusammenfassung

- Gesamt: 1229
- Bestanden: 1228
- Fehlgeschlagen: 0
- Übersprungen: 1

## Testabdeckung

**Abdeckung:** 64.8 %

Dateien mit niedriger Abdeckung (<80%):

| Datei | Abdeckung |
|-------|-----------|
| Win32PseudoConsoleProcessLauncher.cs | 8.5 % |
| App.xaml.cs | 9.8 % |
| ScmRequirement.cs | 16.0 % |
| SettingsView.xaml.cs | 17.9 % |
| TaskDetailView.xaml.cs | 19.1 % |
| KiAusfuehrungsService.cs | 19.5 % |
| ProjektService.cs | 20.0 % |
| BenachrichtigungsService.cs | 20.3 % |
| EntwicklungsprozessService.cs | 40.0 % |
| ProjectListViewModel.cs | 41.2 % |
| GitOrchestrationService.cs | 41.2 % |
| DiffCachingService.cs | 42.4 % |
| FileExplorerViewModel.cs | 45.0 % |
| TaskDetailViewModel.cs | 47.0 % |
| ProjectDetailViewModel.cs | 48.0 % |
| KeyToVt100Encoder.cs | 48.9 % |

## Fehlende Tests

Quelle: Coverage-Daten

### Dateien mit sehr niedriger Abdeckung (0%)

Die folgenden Dateitypen zeigen 0% Abdeckung:
- Generierte Dateien: *.g.cs (WPF/XAML-generiert)
- Migrations-Designer: *.Designer.cs (Entity Framework)
- XAML-Markup-Dateien: *.xaml (nicht unit-testbar)
- UI-Einstiegspunkte: App.xaml.cs (Anwendungsinitialisierung)
- Infrastruktur/Plattform: PseudoConsole.cs, Win32PseudoConsoleProcessLauncher.cs (P/Invoke, nur E2E testbar)

### Zusammenfassung der Abdeckungsluecken

- Total Dateien analysiert: 823
- Dateien >= 80% Abdeckung: 473 (57.5%)
- Dateien < 80% Abdeckung: 350 (42.5%)
  - Davon 0% Abdeckung: ~200 Dateien (generiert, UI-Markup, Migrationen)
  - Mit 1-79% Abdeckung: ~150 Dateien (komplexe Services, ViewModels, Plugins)
- Durchschnittliche Abdeckung: 64.8 %

### Empfohlene Verbesserungen

1. Hoehere Prioritaet: Service-Layer (EntwicklungsprozessService, KiAusfuehrungsService, BenachrichtigungsService, ProjektService) - derzeit 19-42% Abdeckung
2. Mittlere Prioritaet: ViewModel-Tests ausbauen (aktuell 45-75%)
3. Niedrigere Prioritaet: UI-Codebehind und generierte Dateien (werden via E2E-Tests geprueft)

---

Test-Laufzeit: 1 Minute 23 Sekunden (83 Sekunden)
Umgebung: Sandbox mit SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1
Test-Framework: xUnit.net 3.1.5 + FlaUI (E2E)
Coverage-Tool: XPlat Code Coverage (Cobertura)
