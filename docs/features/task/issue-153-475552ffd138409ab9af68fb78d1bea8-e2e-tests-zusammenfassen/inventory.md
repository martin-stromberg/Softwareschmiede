# Bestandsaufnahme: E2E-Test-Struktur zur Reduktion von Testlaufzeiten

Diese Bestandsaufnahme dokumentiert den aktuellen Zustand der E2E-Testinfrastruktur und der vorhandenen Tests bezogen auf die Anforderung, logisch zusammenhängende E2E-Szenarien durch Hilfsmethoden und Test-Konsolidierung zu optimieren.

## Zusammenfassung

### Was bereits existiert

- **`WpfTestBase`-Klasse** ist die zentrale Infrastruktur-Komponente mit bereits umfangreichen, wiederverwendbaren Hilfsmethoden:
  - Navigation (NavigateToProjecten, NavigateToSettings)
  - Projekt-CRUD-Operationen (CreateProject, OpenProject, CreateAndOpenProject)
  - Element-Wartenlogik (WaitForElement, WaitUntilGone, WaitForWindow)
  - UI-Interaktionen (SelectComboBoxItemByClick, WaitForSelectedComboBoxItem)
  - Setup-Methoden (StartAndNavigateToProjects, SetupProjectMitNeuerAufgabe)
  - Repository- und Plugin-Konfiguration (CreateLocalSourceDirectory, ConfigureLocalDirectoryPlugin, AssignLocalDirectoryRepository)
  - Plugin-Ausführung (StartenUndPluginWaehlen)
  - Fehlerdiagnose und Logging (CheckAppStartupException, GetLatestAppLogContent)
  
- **22 E2E-Test-Klassen** in `src/Softwareschmiede.Tests/E2E/E2E_*.cs`, alle erben von `WpfTestBase` und verwenden deren Hilfsmethoden
  
- **Testmuster sind etabliert** und folgen konsistenten Ablauf-Mustern:
  - Setup via Hilfsmethoden
  - Element-Wartenlogik mit FlaUI
  - Assertions zur Verifikation

### Was fehlt noch

- **Konsolidierte Tests**: Tests sind derzeit einzeln organisiert; mehrere zusammenhängende Szenarien könnten in einer Testmethode kombiniert werden (z.B. Anlage + Bearbeitung + Löschung statt drei Tests)

- **Feiner-granulare Hilfsmethoden** (optional): Für spezifische Aufgaben-CRUD-Operationen (z.B. `EditTaskTitle()`, `DeleteTask()`) könnten zusätzliche Hilfsmethoden geschrieben werden, um Test-Konsolidierung zu unterstützen

- **Spezialisierte Helper-Klassen** (optional): Je nach Komplexität könnten Helfer-Klassen wie `TaskPageHelper` entstehen, um sehr komplexe UI-Navigationsmuster zu kapseln (ist aber nicht zwingend erforderlich)

### Test-Laufzeit-Optimierungs-Potenzial

Analyse zeigt, dass mindestens 6 Tests konsolidiert werden könnten:
1. E2E_CreateNewTaskNavigation: 2 Tests → 1 konsolidierter Test
2. E2E_TaskDetailNavigation: 3 Tests → 1 konsolidierter Test
3. E2E_PluginSelectionDialog: 2 Tests → 1 konsolidierter Test

Dies würde bei ~66 E2E-Tests (aktuell insgesamt 22 Test-Klassen mit durchschnittlich ~3 Tests pro Klasse) eine Ersparnis von mindestens 3 App-Starts (≈30-60s) pro Testlauf bedeuten.

---

## Details

### [Logik-Klassen und Hilfsmethoden](inventory/logic.md)

Dokumentiert alle Hilfsmethoden in `WpfTestBase` mit Sichtbarkeit, Kurzbeschreibung, abonnierten/publizierten Events und Properties. Zeigt, dass die Infrastruktur bereits sehr gut strukturiert ist und die Grundlage für Test-Konsolidierung bietet.

### [Tests und Testinfrastruktur](inventory/tests.md)

Dokumentiert alle E2E-Test-Klassen, ihre Testmethoden, Testinfrastruktur-Klassen und das verwendete Execution-Pattern. Identifiziert konkrete Konsolidierungs-Kandidaten mit geschätztem Laufzeit-Einsparpotenzial.

---

## Schlussfolgerung

Die E2E-Testinfrastruktur ist bereits in gutem Zustand mit umfangreichen Hilfsmethoden. Die Anforderung zur Konsolidierung und Laufzeit-Optimierung kann auf dieser soliden Basis umgesetzt werden:

1. **Hilfsmethoden sind vorhanden**: Keine großen zusätzlichen Infrastruktur-Investitionen nötig
2. **Tests folgen etablierten Mustern**: Konsolidierung ist mechanisch machbar
3. **Konsolidierungs-Potenzial ist real**: Messbarer Laufzeit-Gewinn durch Prozess-Sharing bei verwandten Szenarien
4. **Granularität kann erhalten bleiben**: Mit mehreren Assertions pro konsolidiertem Test bleibt die Fehlerermittlung präzise

Die nächste Phase sollte sich auf die konkrete Konsolidierung fokussieren: Identifizieren der Tests mit höchstem Laufzeit-Potenzial, Refaktorieren hin zu Workflow-Methoden (z.B. `TaskCrudOperations_AnlageBearbeitungLoeschung_E2E()`) und optionale Erweiterung von `WpfTestBase` mit feiner-granulaeren Aufgaben-Interaktions-Methoden (falls nötig).
