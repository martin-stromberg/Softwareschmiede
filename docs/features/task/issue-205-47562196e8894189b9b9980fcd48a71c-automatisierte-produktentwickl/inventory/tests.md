# Tests

## Testklassen

### `TaskDetailViewModelTests`

Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

Haupttest-Klasse für TaskDetailViewModel (Unit-Tests).

**Abhängige Testklassen:**
- `TaskDetailViewModelTestsBase.cs` — Basis-Setup
- `TaskDetailViewModelTests_Arbeitsverzeichnis.cs` — Arbeitsverzeichnis-Operationen
- `TaskDetailViewModelTests_IdeAuswahl.cs` — IDE-Auswahl-Logik
- `TaskDetailViewModelTests_Todos.cs` — To-Do-Listen-Integration
- `TaskDetailViewModelTests_VisualStudioCode.cs` — Visual Studio Code-Integration
- `TaskDetailViewModelTests_PluginAktivierung.cs` — Plugin-Aktivierung
- `TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs` — Zeitgesteuerte Prompts

**Setup-Details (Zeilen 40–132):**
- Testdatenbank via `TestDbContextFactory.Create()`
- Services: AufgabeService, ProtokollService, TodoService, KiAusfuehrungsService, etc.
- Mock-Plugins: TestKiPlugin, TestGitPlugin (2 KI-Plugins für Single-Plugin-Tests)
- Mock IDialogService (`_dialogServiceMock`)
- Temp-Verzeichnis-Fixture für Arbeitsverzeichnis-Tests

### `AutonomAufgabeDetailViewModelTests`

Datei: `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeDetailViewModelTests.cs`

Unit-Tests für AutonomAufgabeDetailViewModel.

**Abhängige Testklassen:**
- `AutonomAufgabeDetailViewModelTests_BranchUndVorlagen.cs` — Branch- und Vorlage-spezifische Tests

### `AutonomAufgabenInitialisierungsServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`

Unit-Tests für AutonomAufgabenInitialisierungsService.

### `E2E_AutonomAufgabenAgentExecution`

Datei: `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenAgentExecution.cs`

End-to-End-Tests für die Ausführung des Projektleiter-Agenten.

### `E2E_AutonomAufgabenInitialisierung`

Datei: `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`

End-to-End-Tests für die Initialisierung von Autonomen Aufgaben.

### `E2E Tests Base`

Datei: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`

Basis-Klasse für WPF-E2E-Tests mit FlaUI-Automatisierung.

**Hinweis:** Diese Tests starten real die Anwendung via `Softwareschmiede.App.exe` und fahren die UI mit FlaUI.

---

## Hilfsmethoden und Factories

### `AutonomAufgabenInitialisierungsServiceTestFactory`

Datei: `src/Softwareschmiede.Tests/Helpers/AutonomAufgabenInitialisierungsServiceTestFactory.cs`

**Zweck:** Erzeugt vorkonfigurierte Test-Instanzen von Services und ViewModels für Autonome-Aufgaben-Tests.

**Zu erweitern:** Eventuell neue Factory-Methoden für TaskDetailViewModel mit eingebettetem AutonomAufgabeDetailViewModel.

---

## Bestehende Test-Abdeckung

### Für TaskDetailViewModel

- ✓ Ansicht-Umschaltung (Info, CLI, Diff, Dateiexplorer, PR, Todos)
- ✓ CLI-Start/Stop/Neustart
- ✓ Plugin-Auswahl und -Wechsel
- ✓ IDE-Auswahl und -Öffnung (IDE-Einstiegspunkte)
- ✓ Arbeitsverzeichnis-Operationen
- ✓ To-Do-Listen-Integration
- ✓ Pull-Request-Verwaltung
- ✓ Issue-Verwaltung
- ✓ Zeitgesteuerte Prompt-Versendung

### Für AutonomAufgabeDetailViewModel

- ✓ Start/Stop/Resume-Commands
- ✓ Plan/Progress/Governance-Datei-Laden
- ✓ Plan-Speicherung
- ✓ Fehlerbehandlung

### Für AutonomAufgabeStartService

- ✓ Initialisierungsdialog-Anzeige
- ✓ Detail-Ansicht-Anzeige (via `_dialogService.ShowAutonomAufgabeDetailAsync()`)
- ✓ Fehlerbehandlung

---

## Zu erweiternde Tests (nach der Anforderungs-Integration)

### TaskDetailViewModelTests — neue Tests

- [ ] Neue Enum-Wert `DetailAnsicht.Automatisierung` wird korrekt gespeichert
- [ ] Command `AutomatisierungViewCommand` existiert und wechselt zur Automatisierung-Ansicht
- [ ] Property `IsAutomatisierungViewSelected` spiegelt den Zustand
- [ ] Property `ShowAutomatisierungPanel` ist true, wenn Autonome Aufgabe vorhanden
- [ ] Property `AutonomAufgabeDetailViewModel` wird korrekt gespeichert und kann sein
- [ ] Ribbon-Commands (Start/Stop/Resume) sind an `AutonomAufgabeDetailViewModel` gebunden
- [ ] Start/Stop/Resume-Buttons sind nur sichtbar wenn `ShowAutomatisierungPanel` true

### TaskDetailViewModelTests — Ansicht-Wechsel-Tests

- [ ] Wechsel von Info zu Automatisierung funktioniert
- [ ] Wechsel von Automatisierung zu anderen Ansichten funktioniert
- [ ] `WaehleStandardAnsicht()` behält Automatisierung nicht aus (falls Aufgabe nicht mehr autonom)

### E2E-Tests

- [ ] UI-Test: Autonome Aufgabe starten, neue Registerkarte "Automatisierung" erscheint
- [ ] UI-Test: Start/Stop/Resume-Buttons im Ribbon sind sichtbar und funktionierend
- [ ] UI-Test: Wechsel zwischen Registerkarten funktioniert

### AutonomAufgabenInitialisierungsServiceTests — neue Tests

- [ ] Nach erfolgreicher Initialisierung wird `TaskDetailViewModel` über neue Automatisierung-Ansicht benachrichtigt (statt Dialog zu öffnen)

---

## Mock-Setup-Patterns (aus bestehenden Tests)

### IDialogService Mock

```csharp
Mock<IDialogService> _dialogServiceMock = new Mock<IDialogService>();
// Setup für Dialog-Rückgabewerte
_dialogServiceMock.Setup(d => d.ShowAutonomAufgabeDetailAsync(...))
    .Returns(Task.CompletedTask);
```

**Zu erweitern:** Mock muss konfigurierbar sein für neue Anforderung der TaskDetailViewModel-Benachrichtigung statt Dialog-Anzeige.

### ServiceProvider-Setup

```csharp
var serviceProvider = new ServiceCollection()
    .AddScoped<AutonomAufgabeDetailViewModel>()
    // ...
    .BuildServiceProvider();
```

**Zu erweitern:** Tests müssen überprüfen, dass AutonomAufgabeDetailViewModel korrekt von TaskDetailViewModel verwendet wird.
