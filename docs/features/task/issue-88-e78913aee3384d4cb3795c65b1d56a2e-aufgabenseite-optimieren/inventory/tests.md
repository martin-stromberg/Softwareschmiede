# Tests

## Testklassen

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

- Enthaelt Tests fuer Laden der Aufgabe, Start-/Stopp-/Neustart-Kommandos, Info-/CLI-Umschaltung, Issue-Zuweisung, Promptvorlagen, Fehlerbehandlung und Terminal-Session-Anbindung.
- Deckt bestehende ViewModel-Logik der Aufgabendetailansicht ab; ein expliziter Test fuer Fenstertitel-Aktualisierung liegt hier nicht, da der Titel im `MainWindowViewModel` verwaltet wird.

### `MainWindowViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

- Enthaelt Tests fuer Navigation, Dashboard-/Projekt-/Settings-Titel, aktive Aufgaben in der Seitenleiste und Reaktion auf laufende Automationen.
- Relevanter Bestand fuer die Anforderung "Programmtitel", weil `MainWindowViewModel.Title` die Quelle fuer `MainWindow.Title` ist.

### `EntwicklungsprozessServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

- Enthaelt Tests fuer Repository-Setup, Branch-Erstellung, Startskript, Rollback, Repository-/Plugin-Aufloesung und Prozessstart.
- Relevanter Bestand fuer Aufgaben ohne Issue-Bezug, weil `ErstelleTaskBranchName`, `ResolveRepositoryAsync` und `CreateIssueFileAsync` ohne zwingende Issue-Referenz arbeiten.

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

- Enthaelt Tests fuer CLI-Prozessstart, laufende Handles, Fehler-/Stoppstatus und Statusereignisse.
- Relevanter Bestand fuer Fusszeilen- und Laufzeitstatus, weil `TaskDetailViewModel` seine CLI-Zustandsanzeige aus diesen Ereignissen aktualisiert.

### `E2E_TaskDetailNavigation`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`

- Enthaelt E2E-Szenarien zur Navigation in die Aufgabendetailansicht.
- Relevanter Bestand fuer Sichtbarkeit und Erreichbarkeit der Aufgabendetailansicht im laufenden UI-Kontext.

## Hilfsmethoden

### `TaskDetailViewModelTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

- Stellt Testdaten, In-Memory-Datenbank und Mock-Services fuer `TaskDetailViewModel` bereit.
- Kann bestehende Aufgaben mit verschiedenen Status und Plugin-/Repository-Kontexten fuer Detailseiten-Tests erzeugen.

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`

- Erstellt Test-Datenbankkontexte fuer Service- und ViewModel-Tests.
