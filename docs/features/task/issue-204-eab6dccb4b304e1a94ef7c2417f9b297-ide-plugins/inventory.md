# Bestandsaufnahme: Split-Button-Muster für IDE-Öffnen

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezogen auf die Anforderung zur Implementierung eines Split-Button-Musters für die IDE-Öffnen-Funktion in der TaskDetailView. Das Muster soll ermöglichen, dass der Haupt-Button den ersten (priorisierten) IDE-Einstiegspunkt direkt öffnet, während ein zusätzlicher Dropdown-Button nur bei mehreren verfügbaren Einstiegspunkten sichtbar ist und die Auswahl erlaubt.

## Zusammenfassung

Die folgenden Komponenten sind bereits vorhanden und bilden die Grundlage für die geplante Erweiterung:

### Was existiert bereits

- **IDE-Plugin-System:** Das `IIdePlugin`-Interface definiert die Methoden `FindEntryPointsAsync` und `OpenEntryPointAsync`, um Einstiegspunkte zu ermitteln und zu öffnen.
- **Einstiegspunkt-Modell:** `IdeEntryPoint` ist ein Record mit `Path` und optionalem `DisplayName`.
- **Service-Logik:** 
  - `IdeOeffnenService` orchestriert die Plugin-Auflösung und Einstiegspunkt-Ermittlung.
  - `PluginSelectionService` löst das richtige IDE-Plugin basierend auf Konfiguration und Kompatibilität auf.
- **UI-Integration:** 
  - `TaskDetailViewModel` hat bereits das Kommando `OeffneIdeCommand` und ruft `IdeOeffnenService.OpenRepositoryInIdeAsync` mit einem Callback auf.
  - Der Callback nutzt `IDialogService.ShowSolutionSelectionDialogAsync` für die Mehrfach-Einstiegspunkt-Auswahl.
  - Der Button ist in `TaskDetailView.xaml` (Ribbon-Gruppe "Werkzeuge") platziert.
- **Komponenten-Basis:** `RibbonLargeButton` ist eine wiederverwendbare Single-Button-Komponente, die als Grundlage dient, aber noch keine Split-Button-Funktionalität hat.
- **Tests:** Basis-Infrastruktur in `TaskDetailViewModelTestsBase` und `IdeOeffnenServiceTests` vorhanden; Tests für die neue Funktionalität fehlen noch.

### Was muss erweitert werden

- **UI-Komponente:** Split-Button-Komponente (`RibbonSplitButton.xaml`) mit Haupt-Button + Dropdown-Button.
- **ViewModel-Properties:** `KannIdeAuswaehlen` (Boolean für Dropdown-Sichtbarkeit) und ggf. `VerfuegbareEinstiegspunkte` (gepufferte Liste).
- **ViewModel-Kommandos:** `OeffneIdeAuswahlCommand` für den Dropdown-Button.
- **Tests:** Tests für das neue Split-Button-Verhalten und Fallback-Szenarien.

---

## Details

- [Datenmodelle](inventory/models.md)
- [Geschäftslogik und Services](inventory/logic.md)
- [Interfaces und Contracts](inventory/interfaces.md)
- [UI-Komponenten](inventory/ui.md)
- [Tests](inventory/tests.md)

