# UI und Navigation

## Linke Aufgabenliste

Die linke Seitenleiste bindet `ActiveTasksListControl` an `MainWindowViewModel.AktiveAufgabenListe` und `NavigateZuAufgabeCommand`:

- `src/Softwareschmiede.App/Views/MainWindow.xaml:113`
- `src/Softwareschmiede.App/Views/MainWindow.xaml:114`

Die Liste wird nur angezeigt, wenn nicht das Dashboard aktiv ist:

- `src/Softwareschmiede.App/Views/MainWindow.xaml:108`
- `src/Softwareschmiede.App/Views/MainWindow.xaml:112`

`MainWindowViewModel` haelt die aktuell angezeigte View in `CurrentView` und eine gemeinsame `ObservableCollection<Aufgabe>` fuer aktive Aufgaben:

- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:42`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:60`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:63`

Beim Wechsel von `CurrentView` wird die aktive Aufgabenliste aktualisiert:

- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:47`

Die Navigation zu einer Aufgabe erzeugt ein neues `TaskDetailViewModel`, setzt `AufgabeId` und weist es `CurrentView` zu:

- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:183`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:188`

## Aktive Markierung

Aktuell gibt es keine explizite aktive Aufgaben-ID fuer die Seitenleiste. `ActiveTasksListControl` kennt nur:

- `ItemsSource`: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs:12`
- `NavigateCommand`: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs:20`
- `ShowNavigationButton`: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs:28`

Die Kachel-Templates setzen statisch:

- Hintergrund `SurfaceBrush`: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml:29` und `:60`
- Rahmen `BorderBrush`: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml:30` und `:61`

Es gibt keinen Trigger auf aktuelle Aufgabe, keine `IsSelected`-Semantik und keine Automation-Auszeichnung fuer "aktiv".

## Angezeigte Daten

Die Kachel zeigt derzeit:

- Aufgabe.Titel: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml:8`
- Projekt.Name: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml:13`
- KI-Ausfuehrungsstatus per Converter: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml:18`

SCI-/SCM-Plugin und KI-Plugin werden nicht angezeigt.

## Betroffene UI-Tests

Vorhandene Tests decken Navigation und gemeinsame Datenquelle ab:

- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskWechselUeberMenue.cs`

Fuer die neue Anforderung fehlen gezielte Tests fuer aktive Kachelmarkierung, Sortierung nach letztem CLI-Start und Plugin-Text in der Kachel.

