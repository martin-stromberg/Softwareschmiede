# Tasks: Programmupdate-Fehler beheben

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | ViewModel | `UpdateProgressViewModel.Percent` Setter von `private set` auf öffentliches `set` umstellen | Offen | — |
| 2 | ViewModel | `UpdateProgressViewModel.PhaseText` Setter öffentlich machen | Offen | — |
| 3 | ViewModel | `UpdateProgressViewModel.Message` Setter öffentlich machen | Offen | — |
| 4 | ViewModel | `UpdateProgressViewModel.IsIndeterminate` Setter öffentlich machen | Offen | — |
| 5 | ViewModel | `UpdateProgressViewModel.HasError` Setter öffentlich machen | Offen | — |
| 6 | ViewModel | `UpdateProgressViewModel.CanClose` Setter öffentlich machen | Offen | — |
| 7 | ViewModel | `UpdateProgressViewModel.CanCancel` Setter öffentlich machen (`RelayCommand.Refresh`-Callback beibehalten) | Offen | — |
| 8 | Tests | Testklasse `UpdateProgressDialogTests` anlegen | Offen | — |
| 9 | E2E-Tests | STA-Regressionstest `Show_ShouldNotThrowBindingException`: Dialog mit gebundenem ViewModel instanziieren/anzeigen, keine `InvalidOperationException` | Offen | — |
| 10 | Tests | Bestehende `UpdateProgressViewModelTests` gegenprüfen (weiterhin grün) | Offen | — |
