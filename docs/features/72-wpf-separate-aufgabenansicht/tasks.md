# Tasks: WPF separate Aufgabenansicht

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Überprüfung | Überprüfung: Keine Änderungen am Datenmodell erforderlich | Offen | — |
| 2 | Überprüfung | Überprüfung: `AufgabeService.CreateAsync()`, `UpdateAsync()` und `GetByProjektAsync()` sind vorhanden und ausreichend | Offen | — |
| 3 | ViewModels – Callbacks | Neue Property `NavigateToTaskViewCallback: Action<TaskDetailViewModel>?` in `ProjectDetailViewModel` hinzufügen | Offen | — |
| 4 | ViewModels – Callbacks | Neue Property `NavigateBackToProjectCallback: Action?` in `ProjectDetailViewModel` hinzufügen | Offen | — |
| 5 | ViewModels – Methoden | Neue private Methode `ReloadAufgabenListAsync()` in `ProjectDetailViewModel` implementieren mit Unterstützung für Einzel-Update | Offen | Unit-Test: `ReloadAufgabenList_UpdatesSingleItemAsync_WhenCallbackInvoked` |
| 6 | ViewModels – Anpassung | Methode `OeffneAufgabe(Guid id)` in `ProjectDetailViewModel` anpassen: TaskDetailViewModel konfigurieren, Callbacks setzen, `NavigateToTaskViewCallback?.Invoke(vm)` aufrufen | Offen | Unit-Test: `AufgabeOeffnen_CallsNavigateToTaskViewCallback_WhenAufgabeSelected` |
| 7 | ViewModels – Anpassung | Property `SelectedTaskViewModel` aus `ProjectDetailViewModel` entfernen | Offen | — |
| 8 | ViewModels – Methoden | Neue private Methode `ZeigeTaskDetailView(TaskDetailViewModel vm)` in `ProjectListViewModel` implementieren | Offen | Unit-Test: `ProjectListViewModel_ZeigeTaskDetailView_SetsDetailViewModelToTask` |
| 9 | ViewModels – Methoden | Neue private Methode `KehreZuProjectZurueck()` in `ProjectListViewModel` implementieren | Offen | Unit-Test: `ProjectListViewModel_KehreZuProjectZurueck_RestoresProjectDetailView` |
| 10 | ViewModels – Anpassung | Methode `InitDetailViewModel(ProjectDetailViewModel viewModel)` in `ProjectListViewModel` anpassen: Callbacks setzen | Offen | Unit-Test: `ProjectListViewModel_InitDetailViewModel_SetsCallbacksCorrectly` |
| 11 | ViewModels – Fehlerbehandlung | Methode `SpeichernAsync()` in `TaskDetailViewModel` anpassen: bei Fehler Fehlermeldung anzeigen, keine automatische Navigation | Offen | Unit-Test: `SpeichernAsync_ShowsErrorMessage_WhenSaveFails` |
| 12 | ViewModels – Fehlerbehandlung | Property `ErrorMessage` in `TaskDetailViewModel` hinzufügen zur Anzeige von Speicherfehlern | Offen | Unit-Test: `ErrorMessage_IsSet_WhenSaveFails` |
| 13 | UI – XAML | TaskDetailView: Fehlerbehandlungsbereich (z.B. ErrorTextBlock) hinzufügen, um Fehlermeldungen anzuzeigen | Offen | E2E-Test: `E2E_SaveTask_ShowsErrorMessage_WhenSaveFails` |
| 14 | UI – XAML | `ProjectDetailView.xaml` Zeilen mit inline `<views:TaskDetailView>` Element entfernen | Offen | E2E-Test: `E2E_OpenTask_FromProjectDetail_NavigatesToTaskDetailView` |
| 15 | Unit-Tests – Setup | `ProjectDetailViewModelTests` anpassen: `CreateSut()` um Mocking der neuen Callbacks erweitern | Offen | — |
| 16 | Unit-Tests – Setup | `ProjectListViewModelTests` anpassen: `CreateSut()` um Mocking der neuen Methoden erweitern | Offen | — |
| 17 | Unit-Tests – Setup | `TaskDetailViewModelTests` anpassen: `CreateSut()` um Mocking von ErrorMessage anpassen | Offen | — |
| 18 | Unit-Tests | Test: `AufgabeOeffnen_CallsNavigateToTaskViewCallback_WhenAufgabeSelected` | Offen | Unit-Test erfolgreich |
| 19 | Unit-Tests | Test: `NavigateToTaskViewCallback_SetsZurueckActionAndCallbackOnTaskVM` | Offen | Unit-Test erfolgreich |
| 20 | Unit-Tests | Test: `AufgabeErstellen_CreatesTaskWithStatusNeuAndNavigates` | Offen | Unit-Test erfolgreich |
| 21 | Unit-Tests | Test: `ReloadAufgabenList_UpdatesSingleItemAsync_WhenCallbackInvoked` | Offen | Unit-Test erfolgreich |
| 22 | Unit-Tests | Test: `ProjectListViewModel_ZeigeTaskDetailView_SetsDetailViewModelToTask` | Offen | Unit-Test erfolgreich |
| 23 | Unit-Tests | Test: `ProjectListViewModel_KehreZuProjectZurueck_RestoresProjectDetailView` | Offen | Unit-Test erfolgreich |
| 24 | Unit-Tests | Test: `SpeichernAsync_ShowsErrorMessage_WhenSaveFails` | Offen | Unit-Test erfolgreich |
| 25 | Unit-Tests | Test: `SpeichernAsync_DoesNotNavigateBack_WhenSaveFails` | Offen | Unit-Test erfolgreich |
| 26 | Unit-Tests | Anpassung bestehender Tests: ProjectDetailViewModelTests um neue Callbacks berücksichtigen | Offen | Alle Tests grün |
| 27 | Unit-Tests | Anpassung bestehender Tests: TaskDetailViewModelTests um Fehlerbehandlung berücksichtigen | Offen | Alle Tests grün |
| 28 | UI – Code-Behind | `ProjectDetailView.xaml.cs` überprüfen: `AufgabeDoubleClick` Handler funktioniert mit neuer Navigation | Offen | — |
| 29 | E2E-Tests | Test: `E2E_OpenTask_FromProjectDetail_NavigatesToTaskDetailView` — Öffnet Projekt, doppelklickt Aufgabe, prüft TaskDetailView | Offen | E2E-Test erfolgreich |
| 30 | E2E-Tests | Test: `E2E_NavigateBack_FromTaskDetail_ReturnsToProjectDetail` — Öffnet Task, klickt Zurück, prüft ProjectDetailView | Offen | E2E-Test erfolgreich |
| 31 | E2E-Tests | Test: `E2E_CreateNewTask_EditsAndSaves_ReturnsToProjectWithNewTask` — Erstellt Aufgabe, speichert, prüft dass Aufgabe in Liste | Offen | E2E-Test erfolgreich |
| 32 | E2E-Tests | Test: `E2E_CreateNewTask_Cancel_DoesNotPersist` — Erstellt Aufgabe, bricht ab, prüft dass nicht persistiert | Offen | E2E-Test erfolgreich |
| 33 | E2E-Tests | Test: `E2E_SaveTask_ShowsErrorMessage_WhenSaveFails` — Speichert Aufgabe mit ungültigen Daten, prüft Fehlermeldung | Offen | E2E-Test erfolgreich |
| 34 | Code Review | Überprüfung: Keine Regressions in bestehenden Features (Projekt-CRUD, Filter, Repository-Zuweisen) | Offen | Manuelle Tests: bestehende Features funktionieren |
| 35 | Code Review | Überprüfung: Binding-Fehler-Prüfung in XAML mit Visual Studio Diagnostics | Offen | Keine Binding-Fehler in Output Window |
| 36 | Dokumentation | CHANGELOG aktualisieren: Feature 72 mit Navigationstransformation und Fehlerbehandlung dokumentieren | Offen | — |
| 37 | Dokumentation | Inline-Dokumentation in neuen Methoden/Properties hinzufügen (XML-Kommentare) | Offen | — |
| 38 | Dokumentation | Architecture-ADR updaten falls vorhanden: Callback-basierte Navigation und Einzel-Update Optimierung dokumentieren | Offen | — |
