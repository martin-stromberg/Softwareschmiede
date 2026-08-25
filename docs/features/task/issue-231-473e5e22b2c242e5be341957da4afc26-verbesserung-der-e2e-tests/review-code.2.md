# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### src/Softwareschmiede.Tests/E2E/Views/BaseWindowView.cs (BaseWindowView)

- **Doppelter Code** — `WaitForElement` (Zeilen 78-108), `WaitUntilGone` (Zeilen 114-130), `GetHelpTextOrName` (Zeilen 149-161) und die Timeout-Konstanten `ShortTimeout`/`MediumTimeout` (Zeilen 16-19) sind nahezu identische Neuimplementierungen von `WpfTestBase.WaitForElement` (`src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`, Zeilen 301-331), `WpfTestBase.WaitUntilGone`, `WpfTestBase.GetHelpTextOrName` (Zeilen 337-352) und `WpfTestBase.Short`/`WpfTestBase.Medium` (Zeilen 34-37) — inklusive der identischen Fail-Fast-Logik für das "FehlerMeldung"-Banner. Die Duplikation ist im Klassendoc bewusst begründet ("Erbt bewusst nicht von WpfTestBase..."), führt aber bereits zu Drift: `BaseWindowView.WaitForElement`/`ElementExists` filtern zusätzlich per `IsOnScreen`/`IsOffscreen` unsichtbare Treffer heraus (Zeilen 87, 97, 134-144) — eine Verbesserung, die in `WpfTestBase.WaitForElement` (Zeilen 309-320) fehlt. Jede künftige Fehlerbehebung an diesem Polling-Mechanismus muss doppelt gepflegt werden.

  Empfehlung: Die gemeinsame Polling-/Timeout-Logik (`WaitForElement`, `WaitUntilGone`, `GetHelpTextOrName`, Timeout-Konstanten) in eine gemeinsam genutzte statische Hilfsklasse extrahieren, die sowohl `WpfTestBase` als auch `BaseWindowView` referenzieren, statt sie unabhängig zu duplizieren.

- **Inkonsistente Benennung** — Die Timeout-Konstanten `ShortTimeout` (20s, Zeile 16) und `MediumTimeout` (15s, Zeile 19) bezeichnen exakt dasselbe Konzept wie `WpfTestBase.Short`/`WpfTestBase.Medium`, tragen im selben Testprojekt aber einen anderen Namen.

  Empfehlung: Bei einer Zusammenführung (siehe obiger Befund) einheitlich benennen; andernfalls zumindest denselben Namen (`Short`/`Medium`) verwenden.

### src/Softwareschmiede.Tests/E2E/Views/ProjectListView.cs (ProjectListView)

- **Doppelter Code** — `CreateProject(string name)` (Zeilen 51-64) und `OpenProject(string name)` (Zeilen 69-75) sind eine nahezu wortgleiche Neuimplementierung der bereits vorhandenen `WpfTestBase.CreateProject(AutomationElement mainWindow, string name)` und `WpfTestBase.OpenProject(AutomationElement mainWindow, string name)` (`src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`, Zeilen 438-464) — identische Klick-/Warte-Sequenz, nur mit `this.Window` statt `mainWindow`-Parameter.

  Empfehlung: `ProjectListView.CreateProject`/`OpenProject` intern auf einen gemeinsamen Helper umstellen (z. B. den gleichen extrahierten Baustein wie beim `WaitForElement`-Befund nutzen), statt die Klick-Sequenz ein zweites Mal zu pflegen.

### src/Softwareschmiede.Tests/E2E/E2E_ViewPattern.cs (End2EndTest)

- **Doppelter Code** — `SeedMissingWorkingDirectoryAsync()` (Zeilen 241-254) dupliziert die bereits vorhandene `SeedRepositoryWorkingDirectoryAsync(string workingDirectoryRelativePath)` aus `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs` (Zeilen 194-207) — beide Methoden gehören zur selben `partial class End2EndTest`, beide öffnen den Test-DbContext, lesen `db.GitRepositories.Single()` und legen eine `RepositoryStartKonfiguration` mit identischen Feldern an. Der einzige Unterschied ist der hartkodierte Wert `"does-not-exist"` statt eines Parameters.

  Empfehlung: `SeedMissingWorkingDirectoryAsync()` entfernen und stattdessen direkt `await SeedRepositoryWorkingDirectoryAsync("does-not-exist")` aufrufen (private Methoden aus anderen Dateien derselben `partial class` sind ohne Weiteres aufrufbar).

### src/Softwareschmiede.Tests/E2E/Views/WindowExtensions.cs und src/Softwareschmiede.Tests/E2E/Views/Dialogs/*.cs (WindowExtensions, DialogView-Hierarchie)

- **Fehlende Testabdeckung / Speculative Generality** — `WindowExtensions.DialogFactories` (Zeilen 9-23) registriert 12 Dialog-View-Klassen für die Sichtbarkeitserkennung in `CurrentView()`. Von diesen werden im gesamten Branch nur `RepositoryAssignDialogView`, `PluginSelectionDialogView` und `DeleteConfirmationDialogView` tatsächlich instanziiert bzw. per `Assert.IsType<...>` geprüft (in `E2E_ViewPattern.cs`). Die übrigen neun Klassen — `ArbeitsverzeichnisBearbeitenDialogView`, `AutonomAufgabeDetailDialogView`, `AutonomAufgabeInitialisierungsDialogView`, `HelpTextDialogView`, `IssueCreateDialogView`, `IssueSelectionDialogView`, `OpenTodosDialogView`, `SolutionSelectionDialogView`, `UpdateProgressDialogView` — werden nirgends im Testcode konstruiert oder als aktive Ansicht erwartet; der entsprechende Zweig in `CurrentView()`s Erkennungsschleife für diese neun Einträge ist somit ungetestet totes Gewicht.

  Empfehlung: Für jede der neun Klassen entweder einen E2E-Test ergänzen, der sie tatsächlich als aktive View erkennt (z. B. `Assert.IsType<HelpTextDialogView>(mainWindow.CurrentView())` nach Öffnen des Hilfe-Dialogs), oder die noch nicht benötigten Klassen/Factory-Einträge erst anlegen, wenn ein konkreter Test sie benötigt.

### src/Softwareschmiede.Tests/E2E/Views/README.md (Dokumentation des View-Patterns)

- **Dokumentationskonsistenz** — Das Beispiel im Abschnitt "Beispiel" (Zeile 36) zeigt `var projectListView = (ProjectListView)new ProjectListView(mainWindow).ForceShow();` mit explizitem Cast. Tatsächlich überschreiben alle konkreten `*View`-Klassen `ForceShow()`/`ForceClose()` bereits kovariant (z. B. `public override ProjectListView ForceShow()` in `ProjectListView.cs` Zeile 21, ebenso in `DashboardView.cs`, `SettingsView.cs` usw.), sodass der Cast unnötig ist und Leser zu falschem Code verleitet.

  Empfehlung: Cast aus dem Beispiel entfernen: `var projectListView = new ProjectListView(mainWindow).ForceShow();`.

## Geprüfte Dateien

- `.githooks/install-hooks.cmd`
- `.githooks/install-hooks.sh`
- `.githooks/pre-commit`
- `.githooks/translation-check.py`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ViewPattern.cs`
- `src/Softwareschmiede.Tests/E2E/Views/BaseWindowView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/WindowExtensions.cs`
- `src/Softwareschmiede.Tests/E2E/Views/DialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/DashboardView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ProjectListView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ProjectDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ErrorView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/FileExplorerView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TodoListView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/AutonomAufgabeDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/README.md`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/ArbeitsverzeichnisBearbeitenDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/AutonomAufgabeDetailDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/AutonomAufgabeInitialisierungsDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/DeleteConfirmationDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/HelpTextDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/IssueCreateDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/IssueSelectionDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/OpenTodosDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/PluginSelectionDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/RepositoryAssignDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/SolutionSelectionDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/UpdateProgressDialogView.cs`

### Hinweis zum Umfang der Prüfung

`git diff --name-only --diff-filter=AM $(git merge-base HEAD main)` listet nur bereits getrackte, geänderte Dateien (`.githooks/*`, `src/Softwareschmiede.Tests/E2E/MainTest.cs`). Der fachliche Kern dieses Branches — die neue View-Pattern-Schicht (`src/Softwareschmiede.Tests/E2E/Views/**`, `E2E_ViewPattern.cs`) — liegt zum Zeitpunkt dieses Reviews als unstaged/untracked im Arbeitsverzeichnis vor und wurde zusätzlich vollständig geprüft, da sie den eigentlichen Gegenstand der Änderung ("Verbesserung der E2E-Tests") bildet. Die `.githooks/`-Dateien (Lokalisierungs-Check) sind bereits über Commits im Branch enthalten, stehen inhaltlich aber in keinem erkennbaren Zusammenhang mit dem Thema "E2E-Tests"; `translation-check.py` wurde geprüft und zeigt keine Befunde (die `main()`-Funktion ist bereits sauber in `check_missing_keys`, `check_package_consistency`, `validate_resx_headers` und `report` aufgeteilt).
