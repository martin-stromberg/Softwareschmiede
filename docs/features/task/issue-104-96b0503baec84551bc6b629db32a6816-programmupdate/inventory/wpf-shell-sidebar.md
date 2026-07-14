# WPF-Shell, Sidebar und MainWindowViewModel

## MainWindow.xaml

Die WPF-Shell ist `src/Softwareschmiede.App/Views/MainWindow.xaml`. Das Fenster bindet `Title` an das ViewModel und setzt das Anwendungssymbol. Der Hauptaufbau ist ein zweispaltiges Grid: links Sidebar, rechts `ContentControl` fuer die aktuelle View.

Die Sidebar ist ein `Border` mit einem inneren `Grid`, aktuell mit zwei Zeilen:

- Zeile 0: `StackPanel` mit Navigations-Toggle, Buttons fuer Dashboard, Projekte und Einstellungen sowie dem Label "Aktive Aufgaben".
- Zeile 1: `ScrollViewer` mit `ActiveTasksListControl` fuer `AktiveAufgabenListe`.

Relevante Stellen:

- `MainWindow.xaml:37-125`: komplette Seitenleiste.
- `MainWindow.xaml:45-48`: nur zwei RowDefinitions, noch kein Footer.
- `MainWindow.xaml:62-102`: Navigationsbuttons.
- `MainWindow.xaml:117-123`: aktive Aufgabenliste.

Implikation: Fuer einen Update-Button "am unteren Rand" sollte die Sidebar ein drittes RowDefinition-Footer-Element erhalten. Der Scrollbereich fuer aktive Aufgaben kann in der mittleren Stern-Zeile bleiben; der Update-Button sitzt in der Footer-Zeile. Sichtbarkeit sollte per Binding auf eine neue ViewModel-Property erfolgen.

## MainWindowViewModel

`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` steuert Navigation und aktive Aufgaben:

- Konstruktor-Injection: `DarkModeService`, `IServiceProvider`, `AufgabeService`, `PromptZeitVersandService`, `ILogger<MainWindowViewModel>`, `IRunningAutomationStatusSource`.
- Commands: `NavigateToDashboardCommand`, `NavigateToProjectListCommand`, `NavigateToSettingsCommand`, `ToggleNavigationCommand`, `NavigateZuAufgabeCommand`.
- `AktiveAufgabenListe` ist eine `ObservableCollection<AktiveAufgabePanelItem>`.
- Ein `DispatcherTimer` aktualisiert alle 5 Sekunden; zusaetzlich wird `RunningCountChanged` verarbeitet.

Relevante Stellen:

- `MainWindowViewModel.cs:62-84`: Properties und Commands fuer Sidebar.
- `MainWindowViewModel.cs:87-121`: Konstruktor, Event-/Timer-Setup.
- `MainWindowViewModel.cs:166-190`: `AktiveAufgabenAktualisierenAsync()`.
- `MainWindowViewModel.cs:210-226`: Mapping inkl. `LaufStatus`.
- `MainWindowViewModel.cs:251-263`: Refresh bei Laufstatus-Aenderungen.

Implikation: Update-State passt fachlich in dieses ViewModel, sofern der Button global sichtbar ist. Ein neuer `UpdateVerfuegbar`/`UpdateInfo`-State und `UpdateStartenCommand` koennen hier angebunden werden. Der eigentliche Check/Download sollte in separaten Services bleiben, damit das ViewModel testbar bleibt.

## Bestehende UI-Patterns

Die Sidebar verwendet einfache WPF-Buttons mit Symbol-Text-Kombination und Visibility-Convertern fuer eingeklappte Navigation. Es gibt keine bestehende "Footer Action" in der Sidebar. Dialoge laufen ueber `IDialogService`, nicht direkt aus ViewModels per `MessageBox`.

Empfehlung fuer Umsetzung: Update-Button als normales Sidebar-Element im Footer, mit Icon und Text, sichtbar nur bei `UpdateVerfuegbar`. Bei eingeklappter Navigation nur Icon anzeigen, analog zu den bestehenden Buttons.
