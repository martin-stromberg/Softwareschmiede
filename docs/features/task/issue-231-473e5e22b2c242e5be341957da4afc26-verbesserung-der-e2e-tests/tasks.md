# Tasks: Verbesserung der E2E-Tests — View-Pattern

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Test Infrastructure | `BaseWindowView` abstrakte Basisklasse anlegen | Offen | — |
| 2 | Test Infrastructure | `BaseWindowView`: Property `Window` implementieren | Offen | — |
| 3 | Test Infrastructure | `BaseWindowView`: Abstrakte Property `IsVisible` definieren | Offen | — |
| 4 | Test Infrastructure | `BaseWindowView`: Abstrakte Methode `ForceShow()` definieren | Offen | — |
| 5 | Test Infrastructure | `BaseWindowView`: Abstrakte Methode `ForceClose(bool recurseToDashboard)` definieren | Offen | — |
| 6 | Test Infrastructure | `BaseWindowView`: Property `Menu` vom Typ `MenuView` definieren | Offen | — |
| 7 | Test Infrastructure | `BaseWindowView`: Geschützte Hilfsmethoden für Element-Suche implementieren | Offen | — |
| 8 | Menu and Navigation | `MenuView` Klasse anlegen (erbt von `BaseWindowView`) | Offen | — |
| 9 | Menu and Navigation | `MenuView`: `IsVisible` implementieren (prüfe auf Navigationsbuttons) | Offen | — |
| 10 | Menu and Navigation | `MenuView`: `ForceShow()` als No-Op implementieren | Offen | — |
| 11 | Menu and Navigation | `MenuView`: `NavigateToDashboard()` Methode implementieren | Offen | — |
| 12 | Menu and Navigation | `MenuView`: `NavigateToProjects()` Methode implementieren | Offen | — |
| 13 | Menu and Navigation | `MenuView`: `NavigateToSettings()` Methode implementieren | Offen | — |
| 14 | Test Infrastructure | `DialogView` abstrakte Basisklasse anlegen (erbt von `BaseWindowView`) | Offen | — |
| 15 | Test Infrastructure | `DialogView`: Fenster-Handle-Ermittlung für modale Dialoge implementieren | Offen | — |
| 16 | Test Infrastructure | `DialogView`: `IsVisible` abstrakt definieren (mit Dialog-Fenster-Prüfung) | Offen | — |
| 17 | Main App Views | `DashboardView` Klasse anlegen | Offen | — |
| 18 | Main App Views | `DashboardView`: `IsVisible` implementieren (prüfe auf Navigationsbuttons) | Offen | — |
| 19 | Main App Views | `DashboardView`: `ForceShow()` implementieren (Button-Klick) | Offen | — |
| 20 | Main App Views | `DashboardView`: `ForceClose()` implementieren (No-Op, da oberste Ebene) | Offen | — |
| 21 | Main App Views | `ProjectListView` Klasse anlegen | Offen | — |
| 22 | Main App Views | `ProjectListView`: `IsVisible` implementieren (prüfe auf "Neu"-Button und Projekt-Elemente) | Offen | — |
| 23 | Main App Views | `ProjectListView`: `ForceShow()` implementieren (klick " Projekte"-Button) | Offen | — |
| 24 | Main App Views | `ProjectListView`: `GetProjectElements()` Hilfsmethode implementieren | Offen | — |
| 25 | Main App Views | `ProjectListView`: `CreateProject(string name)` Hilfsmethode implementieren | Offen | — |
| 26 | Main App Views | `ProjectListView`: `OpenProject(string name)` Hilfsmethode implementieren | Offen | — |
| 27 | Main App Views | `ProjectDetailView` Klasse anlegen | Offen | — |
| 28 | Main App Views | `ProjectDetailView`: `IsVisible` implementieren (prüfe auf "ProjektName" und "AufgabeNeu"-Button) | Offen | — |
| 29 | Main App Views | `ProjectDetailView`: `ForceShow()` implementieren (öffne Projekt aus Liste) | Offen | — |
| 30 | Main App Views | `ProjectDetailView`: `ForceClose()` implementieren (gehe zu ProjectListView) | Offen | — |
| 31 | Main App Views | `ProjectDetailView`: `GetProjectName()` Hilfsmethode implementieren | Offen | — |
| 32 | Main App Views | `ProjectDetailView`: `CreateTask()` Hilfsmethode implementieren | Offen | — |
| 33 | Main App Views | `ProjectDetailView`: `DeleteProject()` Hilfsmethode implementieren | Offen | — |
| 34 | Main App Views | `ProjectDetailView`: `GetTaskElements()` Hilfsmethode implementieren | Offen | — |
| 35 | Main App Views | `TaskDetailView` Klasse anlegen | Offen | — |
| 36 | Main App Views | `TaskDetailView`: `IsVisible` implementieren (prüfe auf "EditTitel" und Speichern-Button) | Offen | — |
| 37 | Main App Views | `TaskDetailView`: `ForceShow()` implementieren (öffne Task aus Projektdetail) | Offen | — |
| 38 | Main App Views | `TaskDetailView`: `ForceClose()` implementieren (gehe zu ProjectDetailView) | Offen | — |
| 39 | Main App Views | `TaskDetailView`: `GetTaskTitle()` Hilfsmethode implementieren | Offen | — |
| 40 | Main App Views | `TaskDetailView`: `SetTaskTitle(string title)` Hilfsmethode implementieren | Offen | — |
| 41 | Main App Views | `TaskDetailView`: `SaveTask()` Hilfsmethode implementieren | Offen | — |
| 42 | Main App Views | `TaskDetailView`: `DeleteTask()` Hilfsmethode implementieren | Offen | — |
| 43 | Main App Views | `SettingsView` Klasse anlegen | Offen | — |
| 44 | Main App Views | `SettingsView`: `IsVisible` implementieren (prüfe auf Einstellungs-Tabs) | Offen | — |
| 45 | Main App Views | `SettingsView`: `ForceShow()` implementieren (klick " Einstellungen"-Button) | Offen | — |
| 46 | Main App Views | `SettingsView`: `ForceClose()` implementieren (gehe zu DashboardView) | Offen | — |
| 47 | Main App Views | `SettingsView`: `GetActiveTab()` Hilfsmethode implementieren | Offen | — |
| 48 | Main App Views | `SettingsView`: `SwitchTab(string tabName)` Hilfsmethode implementieren | Offen | — |
| 49 | Main App Views | `SettingsView`: `SaveSettings()` Hilfsmethode implementieren | Offen | — |
| 50 | Main App Views | `FileExplorerView` Klasse anlegen | Offen | — |
| 51 | Main App Views | `FileExplorerView`: `IsVisible` implementieren | Offen | — |
| 52 | Main App Views | `FileExplorerView`: `ForceShow()` implementieren | Offen | — |
| 53 | Main App Views | `FileExplorerView`: `ForceClose()` implementieren | Offen | — |
| 54 | Main App Views | `AutonomAufgabeDetailView` Klasse anlegen | Offen | — |
| 55 | Main App Views | `AutonomAufgabeDetailView`: `IsVisible` implementieren | Offen | — |
| 56 | Main App Views | `AutonomAufgabeDetailView`: `ForceShow()` implementieren | Offen | — |
| 57 | Main App Views | `AutonomAufgabeDetailView`: `ForceClose()` implementieren | Offen | — |
| 58 | Main App Views | `TodoListView` Klasse anlegen | Offen | — |
| 59 | Main App Views | `TodoListView`: `IsVisible` implementieren | Offen | — |
| 60 | Main App Views | `TodoListView`: `ForceShow()` implementieren | Offen | — |
| 61 | Main App Views | `TodoListView`: `ForceClose()` implementieren | Offen | — |
| 62 | Main App Views | `ErrorView` Klasse anlegen (erkennt Fehler-Banner) | Offen | — |
| 63 | Main App Views | `ErrorView`: `IsVisible` implementieren (prüfe auf "FehlerMeldung"-TextBlock) | Offen | — |
| 64 | Main App Views | `ErrorView`: `GetErrorMessage()` Hilfsmethode implementieren | Offen | — |
| 65 | Main App Views | `ErrorView`: `DismissError()` Hilfsmethode implementieren | Offen | — |
| 66 | Dialog Views | `RepositoryAssignDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 67 | Dialog Views | `RepositoryAssignDialogView`: `IsVisible` implementieren (prüfe auf Dialog-Titel "Repository zuweisen") | Offen | — |
| 68 | Dialog Views | `RepositoryAssignDialogView`: `SelectRepository(string name)` Hilfsmethode implementieren | Offen | — |
| 69 | Dialog Views | `RepositoryAssignDialogView`: `Confirm()` Hilfsmethode implementieren | Offen | — |
| 70 | Dialog Views | `PluginSelectionDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 71 | Dialog Views | `PluginSelectionDialogView`: `IsVisible` implementieren (prüfe auf Dialog-Titel "KI-Plugin auswählen") | Offen | — |
| 72 | Dialog Views | `PluginSelectionDialogView`: `SelectPlugin(string name)` Hilfsmethode implementieren | Offen | — |
| 73 | Dialog Views | `PluginSelectionDialogView`: `Confirm()` Hilfsmethode implementieren | Offen | — |
| 74 | Dialog Views | `PluginSelectionDialogView`: `ConfirmForProject()` Hilfsmethode implementieren | Offen | — |
| 75 | Dialog Views | `IssueSelectionDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 76 | Dialog Views | `IssueSelectionDialogView`: `IsVisible` implementieren | Offen | — |
| 77 | Dialog Views | `IssueCreateDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 78 | Dialog Views | `IssueCreateDialogView`: `IsVisible` implementieren | Offen | — |
| 79 | Dialog Views | `AutonomAufgabeInitialisierungsDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 80 | Dialog Views | `AutonomAufgabeInitialisierungsDialogView`: `IsVisible` implementieren | Offen | — |
| 81 | Dialog Views | `AutonomAufgabeDetailDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 82 | Dialog Views | `AutonomAufgabeDetailDialogView`: `IsVisible` implementieren | Offen | — |
| 83 | Dialog Views | `ArbeitsverzeichnisBearbeitenDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 84 | Dialog Views | `ArbeitsverzeichnisBearbeitenDialogView`: `IsVisible` implementieren | Offen | — |
| 85 | Dialog Views | `OpenTodosDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 86 | Dialog Views | `OpenTodosDialogView`: `IsVisible` implementieren | Offen | — |
| 87 | Dialog Views | `HelpTextDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 88 | Dialog Views | `HelpTextDialogView`: `IsVisible` implementieren | Offen | — |
| 89 | Dialog Views | `SolutionSelectionDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 90 | Dialog Views | `SolutionSelectionDialogView`: `IsVisible` implementieren | Offen | — |
| 91 | Dialog Views | `UpdateProgressDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 92 | Dialog Views | `UpdateProgressDialogView`: `IsVisible` implementieren | Offen | — |
| 93 | Dialog Views | `DeleteConfirmationDialogView` Klasse anlegen (erbt von `DialogView`) | Offen | — |
| 94 | Dialog Views | `DeleteConfirmationDialogView`: `IsVisible` implementieren (prüfe auf "Löschen bestätigen") | Offen | — |
| 95 | Dialog Views | `DeleteConfirmationDialogView`: `Confirm()` Hilfsmethode implementieren | Offen | — |
| 96 | Dialog Views | `DeleteConfirmationDialogView`: `Cancel()` Hilfsmethode implementieren | Offen | — |
| 97 | Extension Methods | `WindowExtensions.cs` Datei anlegen | Offen | — |
| 98 | Extension Methods | `Window.CurrentView()` Erweiterungsmethode implementieren | Offen | — |
| 99 | Extension Methods | `CurrentView()`: Dialog-Fenster-Prüfung (RepositoryAssignDialog, PluginSelectionDialog, etc.) implementieren | Offen | — |
| 100 | Extension Methods | `CurrentView()`: View-Erkennung für Haupt-Views implementieren (TaskDetail → ProjectDetail → ProjectList → DashboardView) | Offen | — |
| 101 | Extension Methods | `CurrentView()`: Fallback-Prüfung für ErrorView implementieren | Offen | — |
| 102 | Extension Methods | `CurrentView()`: Fehlerbericht bei unbekannter Ansicht (InvalidOperationException mit Marker-Diagnose) implementieren | Offen | — |
| 103 | Unit Tests | `BaseWindowViewTests` Testklasse anlegen | Offen | — |
| 104 | Unit Tests | Test: `BaseWindowView` Konstruktor und Fenster-Referenz | Offen | — |
| 105 | Unit Tests | Test: `BaseWindowView.Menu` Property verfügbar | Offen | — |
| 106 | Unit Tests | `DashboardViewTests` Testklasse anlegen | Offen | — |
| 107 | Unit Tests | Test: `DashboardView.IsVisible` erkennt Dashboard-Ansicht | Offen | — |
| 108 | Unit Tests | Test: `DashboardView.ForceShow()` navigiert zum Dashboard | Offen | — |
| 109 | Unit Tests | `ProjectListViewTests` Testklasse anlegen | Offen | — |
| 110 | Unit Tests | Test: `ProjectListView.IsVisible` erkennt Projektlisten-Ansicht | Offen | — |
| 111 | Unit Tests | Test: `ProjectListView.ForceShow()` navigiert zu Projektliste | Offen | — |
| 112 | Unit Tests | Test: `ProjectListView.GetProjectElements()` funktioniert | Offen | — |
| 113 | Unit Tests | `ProjectDetailViewTests` Testklasse anlegen | Offen | — |
| 114 | Unit Tests | Test: `ProjectDetailView.IsVisible` erkennt Projektdetail-Ansicht | Offen | — |
| 115 | Unit Tests | Test: `ProjectDetailView.ForceShow()` öffnet Projekt | Offen | — |
| 116 | Unit Tests | Test: `ProjectDetailView.CreateTask()` funktioniert | Offen | — |
| 117 | Unit Tests | Test: `ProjectDetailView.GetTaskElements()` funktioniert | Offen | — |
| 118 | Unit Tests | `TaskDetailViewTests` Testklasse anlegen | Offen | — |
| 119 | Unit Tests | Test: `TaskDetailView.IsVisible` erkennt Aufgabendetail-Ansicht | Offen | — |
| 120 | Unit Tests | Test: `TaskDetailView.ForceShow()` öffnet Aufgabe | Offen | — |
| 121 | Unit Tests | Test: `TaskDetailView.GetTaskTitle()` liest Titel | Offen | — |
| 122 | Unit Tests | Test: `TaskDetailView.SetTaskTitle()` ändert Titel | Offen | — |
| 123 | Unit Tests | `SettingsViewTests` Testklasse anlegen | Offen | — |
| 124 | Unit Tests | Test: `SettingsView.IsVisible` erkennt Einstellungen-Ansicht | Offen | — |
| 125 | Unit Tests | Test: `SettingsView.ForceShow()` navigiert zu Einstellungen | Offen | — |
| 126 | Unit Tests | Test: `SettingsView.SwitchTab()` funktioniert | Offen | — |
| 127 | Unit Tests | `MenuViewTests` Testklasse anlegen | Offen | — |
| 128 | Unit Tests | Test: `MenuView.NavigateToDashboard()` funktioniert | Offen | — |
| 129 | Unit Tests | Test: `MenuView.NavigateToProjects()` funktioniert | Offen | — |
| 130 | Unit Tests | Test: `MenuView.NavigateToSettings()` funktioniert | Offen | — |
| 131 | Unit Tests | `CurrentViewTests` Testklasse anlegen | Offen | — |
| 132 | Unit Tests | Test: `CurrentView()` erkennt DashboardView korrekt | Offen | — |
| 133 | Unit Tests | Test: `CurrentView()` erkennt ProjectListView korrekt | Offen | — |
| 134 | Unit Tests | Test: `CurrentView()` erkennt ProjectDetailView korrekt | Offen | — |
| 135 | Unit Tests | Test: `CurrentView()` erkennt TaskDetailView korrekt | Offen | — |
| 136 | Unit Tests | Test: `CurrentView()` erkennt SettingsView korrekt | Offen | — |
| 137 | Unit Tests | Test: `CurrentView()` erkennt Dialog-Views korrekt | Offen | — |
| 138 | Unit Tests | Test: `CurrentView()` wirft InvalidOperationException bei unbekannter Ansicht | Offen | — |
| 139 | Unit Tests | Test: `CurrentView()` Exception-Diagnose enthält erwartete Marker und aktuelle Elemente | Offen | — |
| 140 | Unit Tests | `DialogViewTests` Testklasse anlegen | Offen | — |
| 141 | Unit Tests | Test: `DialogView` Fenster-Handle-Ermittlung funktioniert | Offen | — |
| 142 | Unit Tests | Test: `DialogView.IsVisible` prüft Dialog-Fenster-Präsenz | Offen | — |
| 143 | Unit Tests | `RepositoryAssignDialogViewTests` Testklasse anlegen | Offen | — |
| 144 | Unit Tests | Test: `RepositoryAssignDialogView.IsVisible` erkennt Dialog | Offen | — |
| 145 | Unit Tests | Test: `RepositoryAssignDialogView.SelectRepository()` funktioniert | Offen | — |
| 146 | Unit Tests | `PluginSelectionDialogViewTests` Testklasse anlegen | Offen | — |
| 147 | Unit Tests | Test: `PluginSelectionDialogView.IsVisible` erkennt Dialog | Offen | — |
| 148 | Unit Tests | Test: `PluginSelectionDialogView.SelectPlugin()` funktioniert | Offen | — |
| 149 | Unit Tests | `ErrorViewTests` Testklasse anlegen | Offen | — |
| 150 | Unit Tests | Test: `ErrorView.IsVisible` erkennt Fehler-Banner | Offen | — |
| 151 | Unit Tests | Test: `ErrorView.GetErrorMessage()` liest Fehlermeldung | Offen | — |
| 152 | E2E Tests | `E2E_ViewPattern.cs` Szenario-Datei anlegen | Offen | — |
| 153 | E2E Tests | E2E Test: Happy Path (App-Start bis Aufgabenerstellung und Schließen) | Offen | — |
| 154 | E2E Tests | E2E Test: View-Erkennung für alle Haupt-Views (Happy Path mit `CurrentView()`-Prüfungen) | Offen | — |
| 155 | E2E Tests | E2E Test: MenuView-Navigation zwischen Ansichten | Offen | — |
| 156 | E2E Tests | E2E Test: `ForceShow()` Navigation für alle Haupt-Views | Offen | — |
| 157 | E2E Tests | E2E Test: `ForceClose()` ohne Rekursion (TaskDetail → ProjectDetail) | Offen | — |
| 158 | E2E Tests | E2E Test: `ForceClose(recurseToDashboard: true)` (TaskDetail → ProjectDetail → ProjectList → Dashboard) | Offen | — |
| 159 | E2E Tests | E2E Test: Dialog-Erkennung (RepositoryAssignDialog via `CurrentView()`) | Offen | — |
| 160 | E2E Tests | E2E Test: Dialog-Erkennung (PluginSelectionDialog via `CurrentView()`) | Offen | — |
| 161 | E2E Tests | E2E Test: ErrorView-Erkennung (Fehler erzwingen, Exception prüfen) | Offen | — |
| 162 | E2E Tests | E2E Test: `CurrentView()` Fehlerfall (unbekannte Ansicht) wirft detaillierte Exception | Offen | — |
| 163 | Documentation | E2E-Test-Dokumentation aktualisieren (Beispiele: Vorher/Nachher View-Pattern-Nutzung) | Offen | — |
| 164 | Documentation | Views-Namespace Dokumentation erstellen (Naming-Konventionen, Best Practices) | Offen | — |
