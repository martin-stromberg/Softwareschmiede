# UI und Task-Detailansicht

## Aktueller Zustand

Die Aufgaben-Detailansicht wird hauptsaechlich durch `TaskDetailViewModel` und `TaskDetailView.xaml` getragen.

Relevante Stellen:

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
  - `DetailAnsicht` enthaelt aktuell `Info`, `Cli`, `Diff`, `Dateibrowser`.
  - `IsInfoViewSelected`, `IsCliViewSelected`, `IsDiffViewSelected`, `IsFileExplorerViewSelected` steuern die Hauptinhalte.
  - `InfoViewCommand`, `CliViewCommand`, `DiffViewCommand`, `DateiViewCommand` wechseln die Ansicht.
  - `KannPullRequestErstellen` haengt von Aufgabe, Branch, Repository-URL und Plugin-Capability ab.
  - `PullRequestErstellenCommand` ruft `PullRequestErstellenAsync` auf.
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
  - Die Ansicht-Schaltflaeche listet `Info`, `CLI`, `Diff`, `Dateien`.
  - Die Ribbon-Gruppe `Pull Request` enthaelt nur `PR erstellen`.
  - Im Hauptinhalt gibt es Panels fuer Info, CLI, Diff und Dateiexplorer.

## Bestehender PR-Ablauf in der UI

`TaskDetailViewModel.PullRequestErstellenAsync`:

1. setzt `IsLoading`,
2. loest `GitOrchestrationService` ueber `IServiceProvider`,
3. ruft `PullRequestErstellenAsync` auf,
4. laedt die Aufgabe neu,
5. oeffnet die PR-URL im Browser.

Der erzeugte PR wird nicht in eine UI-Collection uebernommen. Die Detailansicht kann daher nach Reload keinen PR anzeigen, ausser ueber das allgemeine Protokoll.

## Relevante Erweiterungspunkte

- `DetailAnsicht` um `PullRequests` oder `Pr` erweitern.
- Neues Property wie `IsPullRequestViewSelected` und `ShowPullRequestPanel`.
- Neuer Command wie `PullRequestViewCommand`.
- Collection fuer PR-Anzeigen, z. B. `ObservableCollection<AufgabePullRequestViewModel> PullRequests`.
- Ladepfad in `LadenAsync` erweitern, damit PRs und Action-Status beim Oeffnen der Aufgabe aus der Persistenz geladen werden.
- XAML-Ansicht-Schaltflaeche `PR` neben `Dateien` einfuegen.
- Neues PR-Panel mit Leerzustand, Ladezustand, Fehlerzustand und Liste.

## UI-Zustaende aus der Anforderung

Der neue Inhaltsbereich sollte mindestens folgende Zustaende abbilden:

- keine Pull Requests vorhanden,
- Status wird geladen,
- Fehler beim Abrufen/Aktualisieren,
- Pull Request vorhanden mit Status/Merge-Status,
- keine Actions gefunden,
- Actions laufen, erfolgreich, fehlgeschlagen oder abgebrochen,
- Auto-Abschluss blockiert wegen fehlender Berechtigung oder Bypass-Anforderung.

## Tests

Vorhandene passende Testbereiche:

- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/Views/TaskDetailViewTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`

Neue Tests sollten die Sichtbarkeit des PR-Tabs, das Laden von PR-Daten und das Verhalten nach PR-Erstellung pruefen.

