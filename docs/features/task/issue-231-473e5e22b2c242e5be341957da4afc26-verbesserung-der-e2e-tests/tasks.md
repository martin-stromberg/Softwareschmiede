# Tasks: Verbesserung der E2E-Tests — View-Pattern

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Test Infrastructure | `BaseWindowView` abstrakte Basisklasse anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 2 | Test Infrastructure | `BaseWindowView`: Property `Window` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 3 | Test Infrastructure | `BaseWindowView`: Abstrakte Property `IsVisible` definieren | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 4 | Test Infrastructure | `BaseWindowView`: Abstrakte Methode `ForceShow()` definieren | Erledigt | `E2E_ViewPattern.ForceShowNavigatesCorrectly_E2E` |
| 5 | Test Infrastructure | `BaseWindowView`: Abstrakte Methode `ForceClose(bool recurseToDashboard)` definieren | Erledigt | `E2E_ViewPattern.ForceCloseWithRecursion_E2E` |
| 6 | Test Infrastructure | `BaseWindowView`: Property `Menu` vom Typ `MenuView` definieren | Erledigt | `E2E_ViewPattern.MenuNavigationWorks_E2E` |
| 7 | Test Infrastructure | `BaseWindowView`: Geschützte Hilfsmethoden für Element-Suche implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 8 | Menu and Navigation | `MenuView` Klasse anlegen (erbt von `BaseWindowView`) | Erledigt | `E2E_ViewPattern.MenuNavigationWorks_E2E` |
| 9 | Menu and Navigation | `MenuView`: `IsVisible` implementieren (prüfe auf Navigationsbuttons) | Erledigt | `E2E_ViewPattern.MenuNavigationWorks_E2E` |
| 10 | Menu and Navigation | `MenuView`: `ForceShow()` als No-Op implementieren | Erledigt | `E2E_ViewPattern.MenuNavigationWorks_E2E` |
| 11 | Menu and Navigation | `MenuView`: `NavigateToDashboard()` Methode implementieren | Erledigt | `E2E_ViewPattern.MenuNavigationWorks_E2E` |
| 12 | Menu and Navigation | `MenuView`: `NavigateToProjects()` Methode implementieren | Erledigt | `E2E_ViewPattern.MenuNavigationWorks_E2E` |
| 13 | Menu and Navigation | `MenuView`: `NavigateToSettings()` Methode implementieren | Erledigt | `E2E_ViewPattern.MenuNavigationWorks_E2E` |
| 14 | Test Infrastructure | `DialogView` abstrakte Basisklasse anlegen (erbt von `BaseWindowView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 15 | Test Infrastructure | `DialogView`: Fenster-Handle-Ermittlung für modale Dialoge implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 16 | Test Infrastructure | `DialogView`: `IsVisible` abstrakt definieren (mit Dialog-Fenster-Prüfung) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 17 | Main App Views | `DashboardView` Klasse anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 18 | Main App Views | `DashboardView`: `IsVisible` implementieren (prüfe auf Navigationsbuttons) | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 19 | Main App Views | `DashboardView`: `ForceShow()` implementieren (Button-Klick) | Erledigt | `E2E_ViewPattern.ForceShowNavigatesCorrectly_E2E` |
| 20 | Main App Views | `DashboardView`: `ForceClose()` implementieren (No-Op, da oberste Ebene) | Erledigt | `E2E_ViewPattern.ForceCloseWithRecursion_E2E` |
| 21 | Main App Views | `ProjectListView` Klasse anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 22 | Main App Views | `ProjectListView`: `IsVisible` implementieren (prüfe auf "Neu"-Button und Projekt-Elemente) | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 23 | Main App Views | `ProjectListView`: `ForceShow()` implementieren (klick " Projekte"-Button) | Erledigt | `E2E_ViewPattern.ForceShowNavigatesCorrectly_E2E` |
| 24 | Main App Views | `ProjectListView`: `GetProjectElements()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 25 | Main App Views | `ProjectListView`: `CreateProject(string name)` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 26 | Main App Views | `ProjectListView`: `OpenProject(string name)` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 27 | Main App Views | `ProjectDetailView` Klasse anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 28 | Main App Views | `ProjectDetailView`: `IsVisible` implementieren (prüfe auf "ProjektName" und "AufgabeNeu"-Button) | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 29 | Main App Views | `ProjectDetailView`: `ForceShow()` implementieren (öffne Projekt aus Liste) | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 30 | Main App Views | `ProjectDetailView`: `ForceClose()` implementieren (gehe zu ProjectListView) | Erledigt | `E2E_ViewPattern.ForceCloseWithoutRecursion_E2E` |
| 31 | Main App Views | `ProjectDetailView`: `GetProjectName()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 32 | Main App Views | `ProjectDetailView`: `CreateTask()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 33 | Main App Views | `ProjectDetailView`: `DeleteProject()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 34 | Main App Views | `ProjectDetailView`: `GetTaskElements()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 35 | Main App Views | `TaskDetailView` Klasse anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 36 | Main App Views | `TaskDetailView`: `IsVisible` implementieren (prüfe auf "EditTitel" und Speichern-Button) | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 37 | Main App Views | `TaskDetailView`: `ForceShow()` implementieren (öffne Task aus Projektdetail) | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 38 | Main App Views | `TaskDetailView`: `ForceClose()` implementieren (gehe zu ProjectDetailView) | Erledigt | `E2E_ViewPattern.ForceCloseWithoutRecursion_E2E` |
| 39 | Main App Views | `TaskDetailView`: `GetTaskTitle()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 40 | Main App Views | `TaskDetailView`: `SetTaskTitle(string title)` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 41 | Main App Views | `TaskDetailView`: `SaveTask()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 42 | Main App Views | `TaskDetailView`: `DeleteTask()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 43 | Main App Views | `SettingsView` Klasse anlegen | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 44 | Main App Views | `SettingsView`: `IsVisible` implementieren (prüfe auf Einstellungs-Tabs) | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 45 | Main App Views | `SettingsView`: `ForceShow()` implementieren (klick " Einstellungen"-Button) | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 46 | Main App Views | `SettingsView`: `ForceClose()` implementieren (gehe zu DashboardView) | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 47 | Main App Views | `SettingsView`: `GetActiveTab()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 48 | Main App Views | `SettingsView`: `SwitchTab(string tabName)` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 49 | Main App Views | `SettingsView`: `SaveSettings()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 50 | Main App Views | `FileExplorerView` Klasse anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 51 | Main App Views | `FileExplorerView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 52 | Main App Views | `FileExplorerView`: `ForceShow()` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 53 | Main App Views | `FileExplorerView`: `ForceClose()` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 54 | Main App Views | `AutonomAufgabeDetailView` Klasse anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 55 | Main App Views | `AutonomAufgabeDetailView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 56 | Main App Views | `AutonomAufgabeDetailView`: `ForceShow()` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 57 | Main App Views | `AutonomAufgabeDetailView`: `ForceClose()` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 58 | Main App Views | `TodoListView` Klasse anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 59 | Main App Views | `TodoListView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 60 | Main App Views | `TodoListView`: `ForceShow()` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 61 | Main App Views | `TodoListView`: `ForceClose()` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 62 | Main App Views | `ErrorView` Klasse anlegen (erkennt Fehler-Banner) | Erledigt | `E2E_ViewPattern.RecognizeErrorViewCorrectly_E2E` |
| 63 | Main App Views | `ErrorView`: `IsVisible` implementieren (prüfe auf "FehlerMeldung"-TextBlock) | Erledigt | `E2E_ViewPattern.RecognizeErrorViewCorrectly_E2E` |
| 64 | Main App Views | `ErrorView`: `GetErrorMessage()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeErrorViewCorrectly_E2E` |
| 65 | Main App Views | `ErrorView`: `DismissError()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeErrorViewCorrectly_E2E` |
| 66 | Dialog Views | `RepositoryAssignDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 67 | Dialog Views | `RepositoryAssignDialogView`: `IsVisible` implementieren (prüfe auf Dialog-Titel "Repository zuweisen") | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 68 | Dialog Views | `RepositoryAssignDialogView`: `SelectRepository(string name)` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 69 | Dialog Views | `RepositoryAssignDialogView`: `Confirm()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 70 | Dialog Views | `PluginSelectionDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 71 | Dialog Views | `PluginSelectionDialogView`: `IsVisible` implementieren (prüfe auf Dialog-Titel "KI-Plugin auswählen") | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 72 | Dialog Views | `PluginSelectionDialogView`: `SelectPlugin(string name)` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 73 | Dialog Views | `PluginSelectionDialogView`: `Confirm()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 74 | Dialog Views | `PluginSelectionDialogView`: `ConfirmForProject()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 75 | Dialog Views | `IssueSelectionDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 76 | Dialog Views | `IssueSelectionDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 77 | Dialog Views | `IssueCreateDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 78 | Dialog Views | `IssueCreateDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 79 | Dialog Views | `AutonomAufgabeInitialisierungsDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 80 | Dialog Views | `AutonomAufgabeInitialisierungsDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 81 | Dialog Views | `AutonomAufgabeDetailDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 82 | Dialog Views | `AutonomAufgabeDetailDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 83 | Dialog Views | `ArbeitsverzeichnisBearbeitenDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 84 | Dialog Views | `ArbeitsverzeichnisBearbeitenDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 85 | Dialog Views | `OpenTodosDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 86 | Dialog Views | `OpenTodosDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 87 | Dialog Views | `HelpTextDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 88 | Dialog Views | `HelpTextDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 89 | Dialog Views | `SolutionSelectionDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 90 | Dialog Views | `SolutionSelectionDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 91 | Dialog Views | `UpdateProgressDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 92 | Dialog Views | `UpdateProgressDialogView`: `IsVisible` implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 93 | Dialog Views | `DeleteConfirmationDialogView` Klasse anlegen (erbt von `DialogView`) | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 94 | Dialog Views | `DeleteConfirmationDialogView`: `IsVisible` implementieren (prüfe auf "Löschen bestätigen") | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 95 | Dialog Views | `DeleteConfirmationDialogView`: `Confirm()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 96 | Dialog Views | `DeleteConfirmationDialogView`: `Cancel()` Hilfsmethode implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 97 | Extension Methods | `WindowExtensions.cs` Datei anlegen | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 98 | Extension Methods | `Window.CurrentView()` Erweiterungsmethode implementieren | Erledigt | `E2E_ViewPattern.RunViewPatternHappyPath_E2E` |
| 99 | Extension Methods | `CurrentView()`: Dialog-Fenster-Prüfung (RepositoryAssignDialog, PluginSelectionDialog, etc.) implementieren | Erledigt | `E2E_ViewPattern.RecognizeDialogsCorrectly_E2E` |
| 100 | Extension Methods | `CurrentView()`: View-Erkennung für Haupt-Views implementieren (TaskDetail → ProjectDetail → ProjectList → DashboardView) | Erledigt | `E2E_ViewPattern.RecognizeViewsCorrectly_E2E` |
| 101 | Extension Methods | `CurrentView()`: Fallback-Prüfung für ErrorView implementieren | Erledigt | `E2E_ViewPattern.RecognizeErrorViewCorrectly_E2E` |
| 102 | Extension Methods | `CurrentView()`: Fehlerbericht bei unbekannter Ansicht (InvalidOperationException mit Marker-Diagnose) implementieren | Erledigt | `E2E_ViewPattern.UnrecognizedViewThrowsDetailedException_E2E` |
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
| 152 | E2E Tests | `E2E_ViewPattern.cs` Szenario-Datei anlegen | Erledigt | `End2EndTest.RunViewPatternHappyPath_E2E` |
| 153 | E2E Tests | E2E Test: Happy Path (App-Start bis Aufgabenerstellung und Schließen) | Erledigt | `End2EndTest.RunViewPatternHappyPath_E2E` |
| 154 | E2E Tests | E2E Test: View-Erkennung für alle Haupt-Views (Happy Path mit `CurrentView()`-Prüfungen) | Erledigt | `End2EndTest.RecognizeViewsCorrectly_E2E` |
| 155 | E2E Tests | E2E Test: MenuView-Navigation zwischen Ansichten | Erledigt | `End2EndTest.MenuNavigationWorks_E2E` |
| 156 | E2E Tests | E2E Test: `ForceShow()` Navigation für alle Haupt-Views | Erledigt | `End2EndTest.ForceShowNavigatesCorrectly_E2E` |
| 157 | E2E Tests | E2E Test: `ForceClose()` ohne Rekursion (TaskDetail → ProjectDetail) | Erledigt | `End2EndTest.ForceCloseWithoutRecursion_E2E` |
| 158 | E2E Tests | E2E Test: `ForceClose(recurseToDashboard: true)` (TaskDetail → ProjectDetail → ProjectList → Dashboard) | Erledigt | `End2EndTest.ForceCloseWithRecursion_E2E` |
| 159 | E2E Tests | E2E Test: Dialog-Erkennung (RepositoryAssignDialog via `CurrentView()`) | Erledigt | `End2EndTest.RecognizeDialogsCorrectly_E2E` |
| 160 | E2E Tests | E2E Test: Dialog-Erkennung (PluginSelectionDialog via `CurrentView()`) | Erledigt | `End2EndTest.RecognizeDialogsCorrectly_E2E` |
| 161 | E2E Tests | E2E Test: ErrorView-Erkennung (Fehler erzwingen, Exception prüfen) | Erledigt | `End2EndTest.RecognizeErrorViewCorrectly_E2E` |
| 162 | E2E Tests | E2E Test: `CurrentView()` Fehlerfall (unbekannte Ansicht) wirft detaillierte Exception | Erledigt | `End2EndTest.UnrecognizedViewThrowsDetailedException_E2E` |
| 163 | Documentation | E2E-Test-Dokumentation aktualisieren (Beispiele: Vorher/Nachher View-Pattern-Nutzung) | Offen | — |
| 164 | Documentation | Views-Namespace Dokumentation erstellen (Naming-Konventionen, Best Practices) | Offen | — |
