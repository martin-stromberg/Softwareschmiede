# Aufgaben: Programmupdate-Fehler beheben (Issue 135)

| # | Aufgabe | Status | Testnachweis |
|---|---------|--------|--------------|
| 1 | Setter der 7 Properties (`PhaseText`, `Message`, `Percent`, `IsIndeterminate`, `HasError`, `CanClose`, `CanCancel`) in `UpdateProgressViewModel` von `private set` auf öffentliches `set` umstellen | Erledigt | `UpdateProgressDialogTests.Show_ShouldNotThrowBindingException` (deckt den Binding-Pfad direkt ab); `UpdateProgressViewModelTests` (belegt unveränderte interne Zustandsführung) |
| 2 | `CanCancel`-Setter behält `RelayCommand.Refresh`-Callback im `SetProperty`-Aufruf | Erledigt | `UpdateProgressViewModelTests.CancelCommand_ShouldInvokeCancellationAndDisableCancel` |
| 3 | Konstruktor, `CancelCommand`, `Apply`, `SetError`, `MarkUpdaterStarting`, `RequestCancel` und Felder unverändert lassen | Erledigt | `UpdateProgressViewModelTests` (alle 4 bestehenden Tests) |
| 4 | Neue Testklasse `UpdateProgressDialogTests` mit STA-Regressionstest `Show_ShouldNotThrowBindingException` anlegen | Erledigt | `UpdateProgressDialogTests.Show_ShouldNotThrowBindingException` |
| 5 | Bestehende `UpdateProgressViewModelTests` bleiben grün (keine Anpassung nötig) | Erledigt | `UpdateProgressViewModelTests` (4 Tests, unverändert) |
