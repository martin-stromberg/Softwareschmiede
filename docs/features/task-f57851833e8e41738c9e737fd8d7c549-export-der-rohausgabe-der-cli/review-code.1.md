# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs (TaskDetailViewModel)

- **Kopplung und Erweiterbarkeit** — `ExportCliRawAsync` (ca. ab Zeile 2241) enthält Dateisystemzugriff (`File.WriteAllTextAsync`) direkt im ViewModel. Dadurch ist die Export-Logik eng an UI-Logik gekoppelt und nur schwer isoliert testbar (z. B. I/O-Fehler, Pfadvalidierung, Encoding-Verhalten).

  Empfehlung: Export in einen dedizierten Service (z. B. `ICliRawExportService`) auslagern und im ViewModel nur Orchestrierung/Fehleranzeige belassen.

- **Testqualität** — Für den neu eingeführten Exportpfad ist nur E2E-Happy-Path + Abbruch abgedeckt (`MainTest` ruft `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` und `CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E` auf). Die fachlich relevante Validierung `zielPfad.EndsWith(".raw")` sowie der Fehlerpfad bei Schreibfehlern sind nicht gezielt automatisiert abgesichert.

  Empfehlung: Ergänzende Unit-Tests auf ViewModel-/Service-Ebene für (1) ungültige Dateiendung, (2) I/O-Exception beim Schreiben, (3) erwartete FehlerMeldung/Logging-Verhalten hinzufügen.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `src/Softwareschmiede.App/Services/IDialogService.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.Tests/App/Views/TaskDetailViewTests.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
