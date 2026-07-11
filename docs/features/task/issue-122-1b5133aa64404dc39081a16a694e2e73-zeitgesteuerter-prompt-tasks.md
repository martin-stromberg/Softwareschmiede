# Tasks: Zeitgesteuerter Prompt

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ScheduledPromptInfo`-Record (AufgabeId, PromptText, TargetTime) anlegen | Offen | — |
| 2 | Logik | `PseudoConsoleSession.WritePromptAsync(prompt, ct)` extrahieren | Offen | — |
| 3 | Logik | `PromptZeitVersandService` mit Dictionary, `ITimer`, `lock` und `TimeProvider` anlegen | Offen | — |
| 4 | Logik | `SchedulePromptAsync(aufgabeId, promptText, targetTime)` implementieren (inkl. Vergangenheit→Sofortversand) | Offen | — |
| 5 | Logik | `CancelScheduledPrompt(aufgabeId)` implementieren | Offen | — |
| 6 | Logik | `GetScheduledPromptStatus(aufgabeId)` implementieren | Offen | — |
| 7 | Logik | Event `PromptVersendet` (mit `aufgabeId`) im Service (kein Fehler-Event) | Offen | — |
| 8 | Logik | Automatischer Versand im Timer-Callback (Session holen, `WritePromptAsync`, Eintrag entfernen); fehlende Session → stilles Verwerfen + Log-Warnung | Offen | — |
| 9 | Konfiguration | DI-Registrierung `AddSingleton<PromptZeitVersandService>()` (+ ggf. `TimeProvider.System`) in `App.xaml.cs` | Offen | — |
| 10 | UI-ViewModel | Properties `ScheduledPromptTargetHours` / `ScheduledPromptTargetMinutes` in `TaskDetailViewModel` | Offen | — |
| 11 | UI-ViewModel | Property `ScheduledPromptStatus` in `TaskDetailViewModel` | Offen | — |
| 12 | UI-ViewModel | Berechnete Property `ScheduledPromptTimeDisplay` (HH:mm) | Offen | — |
| 13 | UI-ViewModel | Property `CanSchedulePrompt` in `TaskDetailViewModel` | Offen | — |
| 14 | UI-ViewModel | `SchedulePromptCommand` (`AsyncRelayCommand`) + private `SchedulePromptAsync` | Offen | — |
| 15 | UI-ViewModel | Konstruktor um `PromptZeitVersandService` erweitern und `PromptVersendet` abonnieren (Status räumen + CLI-Ansicht) | Offen | — |
| 16 | UI-ViewModel | `PromptVorlageAuswaehlenAsync` auf leere-Felder-Sofortversand + `WritePromptAsync` umstellen | Offen | — |
| 17 | UI-ViewModel | `Dispose` und `AufgabeAbschliessenAsync` um `CancelScheduledPrompt` + Event-Abmeldung erweitern | Offen | — |
| 17b | UI-ViewModel | `OnCliProcessStatusChanged` räumt `ScheduledPromptStatus`, wenn CLI nicht mehr läuft | Offen | — |
| 18 | UI-XAML | Zwei TextBoxen (Stunde/Minute) mit AutomationName in CLI-Ribbon-Gruppe von `TaskDetailView.xaml` | Offen | — |
| 19 | UI-XAML | Button „Zeitgesteuert senden" gebunden an `SchedulePromptCommand` | Offen | — |
| 20 | UI-XAML | Statusanzeige `ScheduledPromptStatus` (+ `ScheduledPromptTimeDisplay`) | Offen | — |
| 21 | Validierung | Range-Validierung Stunde 0–23 / Minute 0–59 mit `FehlerMeldung` | Offen | — |
| 22 | Tests | Test-NuGet `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) in `Softwareschmiede.Tests.csproj` ergänzen | Offen | — |
| 23 | Tests | `TaskDetailViewModelTestFactory` um `PromptZeitVersandService` erweitern | Offen | — |
| 24 | Tests | `TaskDetailViewModelTests`-Setup an neuen Konstruktor anpassen | Offen | — |
| 25 | Tests | `PromptZeitVersandServiceTests`: Vergangenheit → Sofortversand | Offen | — |
| 26 | Tests | `PromptZeitVersandServiceTests`: Zukunft → Prompt gepuffert | Offen | — |
| 27 | Tests | `PromptZeitVersandServiceTests`: Timer feuert (`FakeTimeProvider.Advance`) → automatischer Versand | Offen | — |
| 28 | Tests | `PromptZeitVersandServiceTests`: Stornierung | Offen | — |
| 29 | Tests | `PromptZeitVersandServiceTests`: zweiter Prompt ersetzt ersten | Offen | — |
| 30 | Tests | `PromptZeitVersandServiceTests`: fehlende Session → stilles Verwerfen (kein Event, keine Exception) | Offen | — |
| 31 | Tests | `TaskDetailViewModelTests`: Stunde/Minute-Binding | Offen | — |
| 32 | Tests | `TaskDetailViewModelTests`: leere Felder → kein Scheduling | Offen | — |
| 33 | Tests | `TaskDetailViewModelTests`: gültige Zeit → Service-Aufruf + Status | Offen | — |
| 34 | Tests | `TaskDetailViewModelTests`: ungültige Stunde → `FehlerMeldung` | Offen | — |
| 35 | Tests | `TaskDetailViewModelTests`: `Dispose` storniert geplante Prompts | Offen | — |
| 36 | E2E-Tests | `E2E_ZeitgesteuerterPrompt`: Planen → Status „Prompt in Wartestellung", kein Fehlerbanner | Offen | — |
