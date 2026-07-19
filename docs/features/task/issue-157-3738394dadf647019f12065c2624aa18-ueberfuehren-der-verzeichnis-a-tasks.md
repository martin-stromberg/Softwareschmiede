# Tasks: Überführen der Verzeichnis-Aktionsbuttons in das Ribbon-Menü

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ProzessStartAnfrage` Value Object anlegen (`DateiName`, `Argumente`, `ShellAusfuehren`) | Offen | — |
| 2 | Logik | `IProzessStarter` Interface anlegen (`Starten(ProzessStartAnfrage)`) | Offen | — |
| 3 | Logik | `SystemProzessStarter` implementieren (reale `Process.Start`-Implementierung mit Logging) | Offen | — |
| 4 | Logik | `AufzeichnenderProzessStarter` implementieren (Testmodus, Anfragen in Logdatei schreiben) | Offen | — |
| 5 | Logik | `ArbeitsverzeichnisOeffnenService.Oeffne(arbeitsverzeichnis)` implementieren (Plattformbefehl-Auflösung) | Offen | — |
| 6 | Logik | `IdeOeffnenService.FindeSolutions(arbeitsverzeichnis)` implementieren (alle `*.sln` oberste Ebene, alphabetisch sortiert) | Offen | — |
| 7 | Logik | `IdeOeffnenService.OeffneSolution(solutionPfad)` implementieren (Shell-Execute) | Offen | — |
| 8 | UI | `SolutionSelectionDialogViewModel` anlegen (`Solutions`, `SelectedSolution`) | Offen | — |
| 9 | UI | `SolutionSelectionDialog` (WPF-Window) anlegen, analog `IssueSelectionDialog`, mit `AutomationName`s | Offen | — |
| 10 | UI | `IDialogService.ShowSolutionSelectionDialogAsync(...)` deklarieren | Offen | — |
| 11 | UI | `WpfDialogService.ShowSolutionSelectionDialogAsync(...)` implementieren (modaler Dialog, Rückgabe Pfad/`null`) | Offen | — |
| 12 | Konfiguration | DI-Registrierung `IProzessStarter` mit Testmodus-Switch in `App.xaml.cs` | Offen | — |
| 13 | Konfiguration | DI-Registrierung `ArbeitsverzeichnisOeffnenService` und `IdeOeffnenService` in `App.xaml.cs` | Offen | — |
| 14 | Logik | `TaskDetailViewModel`: neue Konstruktorparameter + Felder für beide Dienste | Offen | — |
| 15 | Logik | `TaskDetailViewModel`: `SolutionFileExists`-Property + `_solutionPfade`-Caching im `Aufgabe`-Setter | Offen | — |
| 16 | Logik | `TaskDetailViewModel`: `OeffneArbeitsverzeichnisCommand` + Methode `OeffneArbeitsverzeichnis()` | Offen | — |
| 17 | Logik | `TaskDetailViewModel`: `OeffneIdeCommand` + Methode `OeffneIdeAsync()` inkl. Einzel-/Mehrfach-Auswahl-Dialog-Logik | Offen | — |
| 18 | UI | Ribbon-Gruppe „Dateien" in `TaskDetailView.xaml` mit vier überführten Buttons (Sichtbarkeit an `ShowFileExplorerPanel`) | Offen | — |
| 19 | UI | Ribbon-Gruppe „Werkzeuge" in `TaskDetailView.xaml` mit „Arbeitsverzeichnis öffnen" + „IDE öffnen" (immer sichtbar) | Offen | — |
| 20 | UI | CLI-Ribbon-Gruppe: `Visibility`-Bindung an `ShowCliPanel` ergänzen | Offen | — |
| 21 | UI | `FileExplorerView.xaml`: überführte Aktionsbuttons entfernen (Diff-Navigation belassen) | Offen | — |
| 22 | Tests | `TaskDetailViewModelTestFactory` an neue Konstruktorsignatur + `IDialogService`-Setup anpassen | Offen | — |
| 23 | Tests | `ArbeitsverzeichnisOeffnenServiceTests`: Plattformbefehl-Aufruf prüfen | Offen | — |
| 24 | Tests | `IdeOeffnenServiceTests`: `FindeSolutions` (alle sortiert / leere Liste) prüfen | Offen | — |
| 25 | Tests | `IdeOeffnenServiceTests`: `OeffneSolution` (Shell-Execute) prüfen | Offen | — |
| 26 | Tests | `AufzeichnenderProzessStarterTests`: Serialisierung der Anfrage in Logdatei prüfen | Offen | — |
| 27 | Tests | `TaskDetailViewModelTests`: `CanExecute` von `OeffneArbeitsverzeichnisCommand` (`ShowFileExplorerPanel`) | Offen | — |
| 28 | Tests | `TaskDetailViewModelTests`: Delegation von `OeffneArbeitsverzeichnisCommand` an den Dienst | Offen | — |
| 29 | Tests | `TaskDetailViewModelTests`: `SolutionFileExists` wird beim Laden gesetzt | Offen | — |
| 30 | Tests | `TaskDetailViewModelTests`: `OeffneIdeCommand` mit einer Solution öffnet ohne Dialog | Offen | — |
| 31 | Tests | `TaskDetailViewModelTests`: `OeffneIdeCommand` mit mehreren Solutions zeigt Auswahl-Dialog und öffnet Auswahl | Offen | — |
| 32 | Tests | `TaskDetailViewModelTests`: `OeffneIdeCommand` mit mehreren Solutions öffnet bei Dialog-Abbruch keine | Offen | — |
| 33 | E2E-Tests | `WpfTestBase.WaitForProzessStartEintragAsync(substring)` Hilfsmethode ergänzen | Offen | — |
| 34 | E2E-Tests | E2E: „Arbeitsverzeichnis öffnen" zeichnet OS-Dateiexplorer-Start mit `LokalerKlonPfad` auf | Offen | — |
| 35 | E2E-Tests | E2E: „IDE öffnen" bei genau einer `*.sln` zeichnet Shell-Execute-Start der `.sln` auf (kein Dialog) | Offen | — |
| 36 | E2E-Tests | E2E: „IDE öffnen" bei mehreren `*.sln` zeigt Auswahl-Dialog, gewählte `.sln` wird gestartet | Offen | — |
| 37 | E2E-Tests | E2E: „IDE öffnen" ohne `*.sln` ist deaktiviert | Offen | — |
| 38 | E2E-Tests | `E2E_FileExplorer` an ins Ribbon überführte Buttons (neue `AutomationName`s) anpassen | Offen | — |
