# Tasks: Zuverlässige Anwendung des Arbeitsverzeichnisses

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | ViewModel | `TaskDetailViewModel.OeffneArbeitsverzeichnis()` zu `async void` umwandeln und `WorkingDirectoryResolver` nutzen | Offen | — |
| 2 | ViewModel | `TaskDetailViewModel.OeffneVisualStudioCodeFallback()` zu `async void` umwandeln und `WorkingDirectoryResolver` nutzen | Offen | — |
| 3 | ViewModel | `TaskDetailViewModel.OeffneIdeAsync()` anpassen — `WorkingDirectoryResolver` nutzen und Solution-Suche im aufgelösten Verzeichnis durchführen | Offen | — |
| 4 | Unit-Tests | Testklasse `TaskDetailViewModelTests_Arbeitsverzeichnis` anlegen | Offen | — |
| 5 | Unit-Tests | Test `OeffneArbeitsverzeichnis_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf` schreiben | Offen | — |
| 6 | Unit-Tests | Test `OeffneArbeitsverzeichnis_OhneKonfiguration_RuftServiceMitRepositoryRootAuf` schreiben | Offen | — |
| 7 | Unit-Tests | Test `OeffneArbeitsverzeichnis_MitUngueltigemArbeitsverzeichnis_ZeigtFehlermeldung` schreiben | Offen | — |
| 8 | Unit-Tests | Testklasse `TaskDetailViewModelTests_VisualStudioCode` anlegen oder in vorhandene erweitern | Offen | — |
| 9 | Unit-Tests | Test `OeffneVisualStudioCodeFallback_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf` schreiben | Offen | — |
| 10 | Unit-Tests | Test `OeffneVisualStudioCodeFallback_OhneKonfiguration_RuftServiceMitRepositoryRootAuf` schreiben | Offen | — |
| 11 | Unit-Tests | Test `OeffneVisualStudioCodeFallback_OhneVsCode_ZeigtFehlermeldung` schreiben | Offen | — |
| 12 | Unit-Tests | Tests für `TaskDetailViewModel.OeffneIdeAsync()` erweitern — `FindeSolutions()` mit aufgelöstem Verzeichnis prüfen | Offen | — |
| 13 | Unit-Tests | Test `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck` schreiben | Offen | — |
| 14 | Unit-Tests | Bestehende `TaskDetailViewModelTests` anpassen — async void-Methoden, Mocks aktualisieren | Offen | — |
| 15 | E2E-Tests | E2E-Testklasse `E2E_RibbonActions_WorkingDirectory` oder Testmethoden in `E2E_WorkingDirectory` anlegen | Offen | — |
| 16 | E2E-Tests | E2E-Test `RibbonAction_OeffneArbeitsverzeichnis_MitKonfiguriertemArbeitsverzeichnis_OeffnetKorrektesVerzeichnis` schreiben | Offen | — |
| 17 | E2E-Tests | E2E-Test `RibbonAction_OeffneVisualStudioCode_MitKonfiguriertemArbeitsverzeichnis_OeffnetMitKorrektemWorkingDir` schreiben (falls VSCode verfügbar) | Offen | — |
| 18 | E2E-Tests | E2E-Test `RibbonAction_OeffneIde_FindetSolutionImAufgeloestenArbeitsverzeichnis` schreiben | Offen | — |
| 19 | Verifikation | Vollständiger Build durchführen (`dotnet build`) | Offen | — |
| 20 | Verifikation | Alle Unit-Tests ausführen (`dotnet test --filter "Category!=OsInterface"`) | Offen | — |
| 21 | Verifikation | Alle E2E-Tests ausführen (Arbeitsverzeichnis-Suite) | Offen | — |
