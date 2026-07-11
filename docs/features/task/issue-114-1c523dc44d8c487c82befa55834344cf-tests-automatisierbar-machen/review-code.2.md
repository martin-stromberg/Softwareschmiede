# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### WpfTestBase.cs (WpfTestBase)

- **Namenskonventionen und Einheitlichkeit / Hardcodierte Werte** — Die Klasse führt oben die Timeout-Konstanten `Short` (10s), `Medium` (15s) und `Long` (30s) ein und nutzt sie konsequent in `SetupProjectMitNeuerAufgabe` und `StartenUndPluginWaehlen`. Die übrigen Helper-Methoden verwenden jedoch für exakt dieselben Werte weiterhin rohe Literale: `TimeSpan.FromSeconds(10)` (z. B. `NavigateToProjecten` Z. 249, `CreateProject` Z. 277/280/284, `OpenProject` Z. 297, `ConfigureLocalDirectoryPlugin` Z. 366/369/371/374/376/378/381/384/386, `AssignLocalDirectoryRepository` Z. 396/402/420), `TimeSpan.FromSeconds(15)` (Z. 288/291/299) und `TimeSpan.FromSeconds(30)` (Z. 95, `WaitWhileMainHandleIsMissing`). Dasselbe Konzept wird also in zwei Schreibweisen ausgedrückt.

  Empfehlung: In allen Helper-Methoden die rohen `TimeSpan.FromSeconds(10|15|30)`-Literale durch die vorhandenen Konstanten `Short`/`Medium`/`Long` ersetzen, damit die Timeout-Semantik einheitlich und an einer Stelle pflegbar ist.

- **Doppelter Code / Fehlende Kapselung** — In `LaunchApp` wird der App-Pfad zweimal aufgelöst: einmal direkt über `ResolveAppExePath()` (Z. 83) und ein weiteres Mal implizit über `ResolveAppLogDirectory()` (Z. 90), das intern erneut `ResolveAppExePath()` aufruft (Z. 507). Die Datei-Kandidatenschleife läuft dadurch doppelt.

  Empfehlung: `ResolveAppExePath()` in `LaunchApp` einmal aufrufen und den ermittelten Pfad an eine Methode wie `ResolveAppLogDirectory(string appExePath)` übergeben, statt ihn dort erneut aufzulösen.

### E2E_TaskDetailNavigation.cs (E2E_TaskDetailNavigation) / E2E_CreateNewTaskNavigation.cs (E2E_CreateNewTaskNavigation)

- **Doppelter Code / Fehlende Kapselung** — Die einleitende Sequenz `var app = LaunchApp(); var mainWindow = app.GetMainWindow(Automation, Medium)!; NavigateToProjecten(mainWindow); CreateAndOpenProject(mainWindow, "...")` ist in `E2E_TaskDetailNavigation` (Z. 26-29, 60-63, 80-83) und `E2E_CreateNewTaskNavigation` (Z. 28-31, 62-65) jeweils inline wiederholt. `ProjectDetailE2ETests` löst genau denselben Bedarf bereits über die private Helper-Methode `StartAndNavigateToProjects()` (Z. 23-29).

  Empfehlung: Eine gemeinsame Helper-Methode (z. B. `StartAndNavigateToProjects()` bzw. eine Variante mit optionalem Projektnamen) nach `WpfTestBase` hochziehen und in allen drei E2E-Testklassen verwenden, um die duplizierte Start-Navigations-Sequenz zu entfernen.

## Geprüfte Dateien

- `.claude/hooks/build_before_test.py`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/AppStartupLogInspector.cs`
- `src/Softwareschmiede.Tests/E2E/AppStartupLogInspectorTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutoStartCli.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
