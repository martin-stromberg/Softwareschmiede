# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

- `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` — schlägt reproduzierbar (2x) innerhalb des Teilszenarios `RunViewPatternHappyPath_E2E` fehl: `WindowExtensions.CurrentView()` erkennt nach `Menu.NavigateToProjects()` fälschlich weiterhin `TaskDetailView` statt der tatsächlich sichtbaren `ProjectListView`. Root-Cause-Hypothese (Iteration-3-Agent, plausibel, nicht abschließend verifiziert): Nach dem Verlassen einer zuvor geöffneten, fensterumfassenden `TaskDetailView` verbleiben deren Marker-Elemente ("EditTitel"/"Zurück") im Automation-Baum, ohne dass `IsOffscreen` dies erkennt (reine Z-Order-Überdeckung statt Clipping). Dieser Fehler ist ein echter Funktionsfehler im Kern des Features (View-Erkennung), keine reine Codequalitäts-Frage, und wurde bislang nicht behoben (außerhalb des Scopes der zugewiesenen Iteration-3-Befunde entdeckt). **Muss vor Merge behoben werden.**

## Zusammenfassung

- Gesamt: 1399
- Bestanden: 1398
- Fehlgeschlagen: 0
- Übersprungen: 1

## Nachtrag: Verifikation nach Iteration 2 (Code-Review-Befunde behoben)

Nach Behebung der 11 Code-Review-Befunde (siehe `review-code.1.md`) wurde erneut verifiziert:

- **Voller Build** (`dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`): sauber, 0 Warnungen/Fehler.
- **Reguläre Testlane** (`--filter "Category!=OsInterface"`): 1398 bestanden, 1 übersprungen, 0 fehlgeschlagen — erneut bestätigt.
- **OsInterface-Lane** (`End2EndTest.RunGeneralTests`, enthält die 9 neuen View-Pattern-Szenarien): Zwei Testläufe scheiterten jeweils an einer *anderen*, bereits vor den View-Pattern-Szenarien liegenden, unveränderten Bestandsmethode (`RepositoryZuweisen_MitErfolgreichemStrukturabruf_...` bzw. `AutonomAufgabeInitialisierung_...`) — beide Dateien sind laut `git diff` durch diesen Branch nicht verändert. Ursache verifiziert: `%APPDATA%\AutonomAufgaben` enthält 340 verwaiste Verzeichnisse aus früheren Sandbox-Läufen (Altlast, nicht durch diese Änderung verursacht — bereits vom Implementierungs-Agenten in Iteration 1 mit 325 Verzeichnissen dokumentiert). Die View-Pattern-Szenarien selbst wurden dadurch in diesem Sandbox-Lauf nicht erneut erreicht; sie wurden aber bereits in Iteration 1 isoliert vollständig grün verifiziert, und alle Quelldateien wurden nach den Iteration-2-Fixes zusätzlich manuell durchgesehen (kovariante Rückgabetypen, Fail-Fast-Verhalten, totes-Code-Entfernen — alle in den bestehenden Szenarien verankert, keine neuen Testmethoden).

## Testabdeckung

**Abdeckung:** 29.1%

| Datei | Abdeckung |
|-------|----------|
| `src/Softwareschmiede.App/App.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/AutonomAufgabeDetailDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/AutonomAufgabeDetailView.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/DashboardView.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/HelpTextDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/IssueCreateDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/IssueSelectionDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/MainWindow.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/PluginSettingFieldTemplateSelector.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/ProjectListView.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/SettingsView.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/SolutionSelectionDialog.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/Views/TodoListView.xaml.cs` | 0.0% |
| `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs` | 0.0% |
| `src/Softwareschmiede.App/ViewModels/PluginSelectionDialogViewModel.cs` | 0.0% |
| `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs` | 0.0% |
| `src/Softwareschmiede.App/ViewModels/SolutionSelectionDialogViewModel.cs` | 0.0% |
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | 0.0% |
| `src/Softwareschmiede.App/Services/DarkModeService.cs` | 0.0% |
| `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs` | 0.0% |
| `src/Softwareschmiede.App/Services/WpfApplicationShutdownService.cs` | 0.0% |
| `src/Softwareschmiede.App/Services/WpfAudioService.cs` | 0.0% |
| ... und 163 weitere Dateien | < 80% |

## Fehlende Tests

Quelle: `Coverage-Daten`

- `src/Softwareschmiede.App/App.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailView.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/DashboardView.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/HelpTextDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/IssueCreateDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/IssueSelectionDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/PluginSettingFieldTemplateSelector.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/ProjectListView.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs` — 0% Abdeckung
- `src/Softwareschmiede.App/Views/SolutionSelectionDialog.xaml.cs` — 0% Abdeckung

*(und 173 weitere Dateien mit niedriger Abdeckung)*
