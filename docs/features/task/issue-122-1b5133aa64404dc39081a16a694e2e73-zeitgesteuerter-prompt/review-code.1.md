# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Fehlerbehandlung / Zustands-Konsistenz** — `OnCliProcessStatusChanged` (ca. Zeile 843–870): Wenn die CLI stoppt oder in den Fehlerstatus wechselt, werden zwar die UI-Properties `ScheduledPromptStatus` und `ScheduledPromptTimeDisplay` auf `null` gesetzt (Zeile 860–861), aber der beim `PromptZeitVersandService` registrierte geplante Prompt wird **nicht** storniert. Der Timer im Service läuft weiter. Konsequenz: Die UI signalisiert dem Nutzer "kein Prompt geplant", während im Hintergrund noch ein Prompt gepuffert ist. Wird für dieselbe Aufgabe vor Erreichen der Zielzeit erneut eine CLI gestartet (neue Session unter derselben `_aufgabeId`), feuert der alte Timer und sendet den veralteten Prompt unerwartet in die neue Session. Nur wenn zur Fälligkeit keine Session existiert, wird er (still, mit Warnung) verworfen.

  Empfehlung: In `OnCliProcessStatusChanged` beim Zweig `status != CliProcessStatus.Gestartet` zusätzlich `_promptZeitVersandService.CancelScheduledPrompt(_aufgabeId)` aufrufen (analog zum bereits vorhandenen Aufruf in `AufgabeAbschliessenAsync` und `Dispose`), damit UI-Status und Service-Zustand konsistent bleiben.

- **Einheitlichkeit / fehlende Änderungsbenachrichtigung** — `CanSchedulePrompt` (Zeile 254 ff.) hängt von `_isCliRunning` und `_selectedPromptVorlage` ab. Die Setter von `ScheduledPromptTargetHours`/`ScheduledPromptTargetMinutes` feuern korrekt `OnPropertyChanged(nameof(CanSchedulePrompt))` (Zeile 224, 235), der `IsCliRunning`-Setter (Zeile 142–151) und der `SelectedPromptVorlage`-Setter (Zeile 202–212) tun dies jedoch nicht — obwohl der `IsCliRunning`-Setter für die eng verwandte Property `KannPromptVorlageSenden` explizit benachrichtigt (Zeile 147). Die Benachrichtigung ist damit nur zur Hälfte implementiert und inkonsistent. Direkt an `CanSchedulePrompt` gebundene UI-Elemente würden nicht aktualisiert; der Button funktioniert derzeit nur, weil `AsyncRelayCommand.CanExecuteChanged` über `CommandManager.RequerySuggested` läuft.

  Empfehlung: Im `IsCliRunning`-Setter und im `SelectedPromptVorlage`-Setter ebenfalls `OnPropertyChanged(nameof(CanSchedulePrompt))` auslösen, damit die Benachrichtigung vollständig und konsistent zu den übrigen abhängigen Properties ist.

### PromptZeitVersandService.cs (PromptZeitVersandService)

- **Namenskonventionen / Einheitlichkeit** — Innerhalb derselben Klasse mischen sich englische und deutsche Verb-Bezeichner für dasselbe Konzept: öffentlich `SchedulePromptAsync`, `CancelScheduledPrompt`, `GetScheduledPromptStatus` (englisch) gegenüber privat/Event `SendePromptAsync`, `AufTimerFaelligAsync`, `PromptVersendet` (deutsch). Auch der Klassenname selbst (`PromptZeitVersandService`) ist deutsch. Das erschwert die Lesbarkeit und weicht von einer einheitlichen Benennung innerhalb einer Klasse ab.

  Empfehlung: Auf eine Sprache pro Klasse festlegen. Da der Klassenname und die bestehende Domänensprache deutsch sind (`PromptVersendet`, `SendePromptAsync`, `AufTimerFaelligAsync`), die öffentlichen Member entsprechend eindeutschen (z. B. `PlanePromptAsync`, `StornierePlantenPrompt`, `HolePlantenPromptStatus`) — oder umgekehrt konsequent englisch. Auch die abhängigen Bezeichner in `TaskDetailViewModel` (`SchedulePromptCommand`, `ScheduledPromptStatus` etc.) sind dann anzugleichen.

### TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs (TaskDetailViewModelTests_ZeitgesteuerterPrompt)

- **Testqualität (zeitabhängige Flakiness)** — `SchedulePrompt_GueltigeZeit_RuftServiceAuf` (Zeile 152–174) berechnet die Zielzeit über `DateTime.Now.AddMinutes(2)` und übergibt nur `Hour`/`Minute`. Die Produktivlogik (`SchedulePromptAsync` im ViewModel) baut daraus eine Zielzeit **am heutigen Tag**. Läuft der Test im Zeitfenster kurz vor Mitternacht (aktuelle Zeit ≥ 23:58), rollt `AddMinutes(2)` auf den Folgetag (z. B. 00:01); die daraus gebaute heutige Zielzeit liegt dann in der Vergangenheit, der Service sendet sofort statt zu puffern und `GetScheduledPromptStatus` liefert `null` — die Assertion `NotBeNull` schlägt fehl. Der Test ist damit nicht deterministisch.

  Empfehlung: Den zeitabhängigen Pfad deterministisch machen. Da das ViewModel intern `DateTime.Now` fest verdrahtet (kein `TimeProvider` injiziert), sollte entweder das ViewModel einen `TimeProvider` erhalten und der Test einen `FakeTimeProvider` mit fester Uhrzeit setzen, oder der Test auf eine garantiert heute-in-der-Zukunft liegende feste Uhrzeit ausweichen bzw. das Mitternachtsfenster ausschließen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`
- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` (neu, untracked)
- `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs` (neu, untracked)
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs` (neu, untracked)
- `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs` (neu, untracked)
- `src/Softwareschmiede.Tests/E2E/E2E_ZeitgesteuerterPrompt.cs` (neu, untracked)
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
