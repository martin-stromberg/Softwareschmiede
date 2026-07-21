# UI und Command-Fluss

## Ribbon-Aktion

`TaskDetailView.xaml` enthaelt den Ribbon-Button in der Gruppe `Werkzeuge`:

- `ButtonText="IDE oeffnen"`
- `AutomationName="IdeOeffnen"`
- `ButtonCommand="{Binding OeffneIdeCommand}"`

Direkt daneben liegt `Arbeitsverzeichnis oeffnen` mit `AutomationName="ArbeitsverzeichnisOeffnen"`. Beide Aktionen sind also in derselben UI-Gruppe verortet.

Fundstellen:

- `src/Softwareschmiede.App/Views/TaskDetailView.xaml:162`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml:170`

## ViewModel-Zustand

`TaskDetailViewModel` haelt:

- `_showFileExplorerPanel` - true, wenn `LokalerKlonPfad` gesetzt ist und das Verzeichnis existiert.
- `_solutionPfade` - gecachte Liste der gefundenen `*.sln`-Dateien.
- `ShowFileExplorerPanel` - oeffentliche Property fuer vorhandenes Arbeitsverzeichnis.
- `SolutionsVorhanden` - true, wenn `_solutionPfade.Count > 0`.

Beim Setzen von `Aufgabe` werden diese Werte synchron aktualisiert. Danach wird `OnPropertyChanged(nameof(SolutionsVorhanden))` ausgeloest.

Fundstellen:

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:70`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:104`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:345`

## Command-Registrierung

`OeffneIdeCommand` wird im Konstruktor als `AsyncRelayCommand` registriert:

```csharp
OeffneIdeCommand = new AsyncRelayCommand(OeffneIdeAsync, () => SolutionsVorhanden);
```

Damit ist die Aktion aktuell deaktiviert, sobald keine `*.sln` gefunden wurde. Das kollidiert direkt mit dem geforderten Fallback, weil der Benutzer den Command im "keine Solution"-Fall nicht ausloesen kann.

Fundstellen:

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:440`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:525`

## Aktuelle Oeffnen-Logik

`OeffneIdeAsync()`:

1. bricht bei `_solutionPfade.Count == 0` sofort ab,
2. oeffnet bei einer Solution direkt,
3. zeigt bei mehreren Solutions den Auswahl-Dialog,
4. ruft danach `IdeOeffnenService.OeffneSolution(solutionPfad)` auf,
5. schreibt Prozessstartfehler in `FehlerMeldung`.

Fuer den Fallback muss Schritt 1 ersetzt werden: bei `0` Solutions sollte der Code die neue Einstellung und VS-Code-Verfuegbarkeit pruefen.

Fundstellen:

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:1394`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:1408`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:1415`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:1419`

## Konsequenz fuer die Umsetzung

Eine naheliegende Anpassung ist eine neue CanExecute-Property, z. B. `KannIdeOeffnen`, die `SolutionsVorhanden || (ShowFileExplorerPanel && VsCodeFallbackAktiv)` abbildet. Die genaue Einstellung kann async geladen werden; deshalb muss beim Laden der Aufgabe auch der aktuelle Settings-Wert in das ViewModel gelangen oder die Fallback-Entscheidung im Command selbst getroffen werden. Wichtig ist: Ist der Fallback deaktiviert, bleibt der Button ohne Solution wie bisher deaktiviert.
