# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ProjectDetailViewModelTests.cs / ProjectListViewModelTests.cs (Testklassen)

- **Doppelter Code** — Die private Hilfsmethode `CreateTaskDetailViewModel()` ist in `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs` und in `src/Softwareschmiede.Tests/App/ViewModels/ProjectListViewModelTests.cs` zeilenidentisch (ca. 25 Zeilen, inkl. Aufbau von `KiAusfuehrungsService`, `ProtokollService`, `PluginSelectionService`, `EntwicklungsprozessService` und `TaskDetailViewModel`) dupliziert. Eine dritte, sehr ähnliche Konstruktionslogik existiert sinngemäß bereits in `TaskDetailViewModelTests.cs` (eigene `CreateSut`-Methode mit denselben Abhängigkeiten).

  Empfehlung: Gemeinsame Test-Factory (z. B. `TaskDetailViewModelTestFactory` oder Erweiterung von `Softwareschmiede.Tests.Helpers`) extrahieren, die von allen drei Testklassen verwendet wird, statt die Konstruktionslogik mehrfach zu duplizieren.

### ProjectListViewModel.cs (Klasse `ProjectListViewModel`)

- **Temporäres Feld / Versteckter Zustand** — Das Feld `_isShowingTaskDetailView` (Zeile 24) wird ausschließlich als Vorab-Signal genutzt, um die Dispose-Logik im `DetailViewModel`-Setter (Zeile 51) zu steuern, bevor der eigentliche Wertwechsel passiert. Diese Reihenfolgeabhängigkeit (Flag muss vor dem Setzen von `DetailViewModel` korrekt gesetzt sein) ist an vier Stellen (`ZeigeDetail`, `ZeigeDetailErstellungsFormular`, `ZeigeTaskDetailView`, `KehreZuProjectZurueck`) verteilt und für Leser nicht offensichtlich; ein Vergessen des Flags an einer neuen Aufrufstelle disposed ungewollt ein ViewModel, das noch gebraucht wird (z. B. `_currentProjectDetailViewModel`).

  Empfehlung: Die Entscheidung, ob das alte `DetailViewModel` disposed werden soll, explizit am Aufrufer treffen (z. B. `SetDetailViewModel(ViewModelBase? value, bool disposeOld)` als private Methode) statt über ein implizites Instanzfeld, das vor jedem Aufruf manuell synchron gehalten werden muss.

### ProjectDetailViewModel.cs (Klasse `ProjectDetailViewModel`)

- **Fehlende Kapselung / Feature Envy (gering)** — `ReloadAufgabenListAsync()` (Zeile 404) verwendet `Aufgaben.ToList().FindIndex(...)`, um den Index eines Elements zu finden und es dann per Index zu ersetzen. Diese Logik (Einzel-Update einer `ObservableCollection` per ID) ist ein wiederkehrendes Muster, das bei künftigen ähnlichen Anforderungen (z. B. Projektliste) erneut inline geschrieben werden könnte.

  Empfehlung: Kleine Erweiterungsmethode wie `ObservableCollection<T>.ReplaceOrAdd(T item, Func<T,bool> match)` in einer gemeinsamen Utility-Klasse vorsehen, falls dieses Muster ein zweites Mal benötigt wird. Aktuell kein Muss, da nur eine Verwendungsstelle existiert — als Beobachtung dokumentiert, keine Pflichtänderung.

### TaskDetailView.xaml.cs (Klasse `TaskDetailView`)

- **Fehlerbehandlung mit breitem Exception-Filter ohne Kontext** — `WaitForWindowHandleAsync` fängt `Win32Exception` und `InvalidOperationException` ab und schluckt sie vollständig (nur Kommentar, kein Log). Sollte der Prozessstart in einem unerwarteten Zustand fehlschlagen, gibt es keine Möglichkeit, das Problem nachzuvollziehen, da nicht einmal ein Debug-Log erfolgt (im Gegensatz zu anderen Stellen im gleichen Feature wie `TaskDetailViewModel`, die durchgehend `_logger.LogWarning/LogError` verwenden).

  Empfehlung: Mindestens ein `Debug.WriteLine` oder Logger-Aufruf (sofern in der View verfügbar) ergänzen, analog zum Muster in `TaskDetailViewModel`, damit Fehlschläge beim Fenster-Einbetten im Diagnosefall nicht spurlos verschwinden.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectListViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
