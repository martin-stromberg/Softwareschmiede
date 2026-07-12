# Tasks: Zeitgesteuerter Prompt

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ScheduledPromptInfo`-Record (AufgabeId, PromptText, TargetTime) anlegen | Erledigt | `PromptZeitVersandServiceTests.SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt` |
| 2 | Logik | `PseudoConsoleSession.WritePromptAsync(prompt, ct)` extrahieren | Erledigt | `PromptZeitVersandServiceTests.Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch` |
| 3 | Logik | `PromptZeitVersandService` mit Dictionary, `ITimer`, `lock` und `TimeProvider` anlegen | Erledigt | `PromptZeitVersandServiceTests.SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt` |
| 4 | Logik | `SchedulePromptAsync(aufgabeId, promptText, targetTime)` implementieren (inkl. Vergangenheit→Sofortversand) | Erledigt | `PromptZeitVersandServiceTests.SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort` |
| 5 | Logik | `CancelScheduledPrompt(aufgabeId)` implementieren | Erledigt | `PromptZeitVersandServiceTests.CancelScheduledPrompt_EntferntGeplantenPrompt` |
| 6 | Logik | `GetScheduledPromptStatus(aufgabeId)` implementieren | Erledigt | `PromptZeitVersandServiceTests.SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt` |
| 7 | Logik | Event `PromptVersendet` (mit `aufgabeId`) im Service (kein Fehler-Event) | Erledigt | `PromptZeitVersandServiceTests.Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch` |
| 8 | Logik | Automatischer Versand im Timer-Callback (Session holen, `WritePromptAsync`, Eintrag entfernen); fehlende Session → stilles Verwerfen + Log-Warnung | Erledigt | `PromptZeitVersandServiceTests.Timer_OhneSession_VerwirftPromptStill` |
| 9 | Konfiguration | DI-Registrierung `AddSingleton<PromptZeitVersandService>()` (+ ggf. `TimeProvider.System`) in `App.xaml.cs` | Erledigt | Kein direkter Test (Registrierung in `App.xaml.cs` Z. 192–193) |
| 10 | UI-ViewModel | Properties `ScheduledPromptTargetHours` / `ScheduledPromptTargetMinutes` in `TaskDetailViewModel` | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.ScheduledPromptTargetHours_Binding_SetztProperty` |
| 11 | UI-ViewModel | Property `ScheduledPromptStatus` in `TaskDetailViewModel` | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.SchedulePrompt_GueltigeZeit_RuftServiceAuf` |
| 12 | UI-ViewModel | Berechnete Property `ScheduledPromptTimeDisplay` (HH:mm) | Erledigt | Kein direkter Test (gesetzt in `SchedulePromptAsync`, Z. 1142) |
| 13 | UI-ViewModel | Property `CanSchedulePrompt` in `TaskDetailViewModel` | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.SchedulePrompt_LeereFelder_KeinScheduling` |
| 14 | UI-ViewModel | `SchedulePromptCommand` (`AsyncRelayCommand`) + private `SchedulePromptAsync` | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.SchedulePrompt_GueltigeZeit_RuftServiceAuf` |
| 15 | UI-ViewModel | Konstruktor um `PromptZeitVersandService` erweitern und `PromptVersendet` abonnieren (Status räumen + CLI-Ansicht) | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt` (Konstruktoraufruf) |
| 16 | UI-ViewModel | `PromptVorlageAuswaehlenAsync` auf leere-Felder-Sofortversand + `WritePromptAsync` umstellen | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.SchedulePrompt_LeereFelder_KeinScheduling` |
| 17 | UI-ViewModel | `Dispose` und `AufgabeAbschliessenAsync` um `CancelScheduledPrompt` + Event-Abmeldung erweitern | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.Dispose_StorniertGeplantePrompts` |
| 17b | UI-ViewModel | `OnCliProcessStatusChanged` räumt `ScheduledPromptStatus`, wenn CLI nicht mehr läuft | Erledigt | Kein direkter Test (Code `OnCliProcessStatusChanged`, Z. 860–861) |
| 18 | UI-XAML | Zwei TextBoxen (Stunde/Minute) mit AutomationName in CLI-Ribbon-Gruppe von `TaskDetailView.xaml` | Erledigt | `E2E_ZeitgesteuerterPrompt.ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` |
| 19 | UI-XAML | Button „Zeitgesteuert senden" gebunden an `SchedulePromptCommand` | Erledigt | `E2E_ZeitgesteuerterPrompt.ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` |
| 20 | UI-XAML | Statusanzeige `ScheduledPromptStatus` (+ `ScheduledPromptTimeDisplay`) | Erledigt | `E2E_ZeitgesteuerterPrompt.ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` |
| 21 | Validierung | Range-Validierung Stunde 0–23 / Minute 0–59 mit `FehlerMeldung` | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.SchedulePrompt_UngueltigeStunde_SetztFehlerMeldung` |
| 22 | Tests | Test-NuGet `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) in `Softwareschmiede.Tests.csproj` ergänzen | Erledigt | `PromptZeitVersandServiceTests` nutzt `FakeTimeProvider` |
| 23 | Tests | `TaskDetailViewModelTestFactory` um `PromptZeitVersandService` erweitern | Erledigt | `TaskDetailViewModelTestFactory.Create` (Z. 32, 54) |
| 24 | Tests | `TaskDetailViewModelTests`-Setup an neuen Konstruktor anpassen | Erledigt | `TaskDetailViewModelTests` (Z. 28, 75, 136) |
| 25 | Tests | `PromptZeitVersandServiceTests`: Vergangenheit → Sofortversand | Erledigt | `PromptZeitVersandServiceTests.SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort` |
| 26 | Tests | `PromptZeitVersandServiceTests`: Zukunft → Prompt gepuffert | Erledigt | `PromptZeitVersandServiceTests.SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt` |
| 27 | Tests | `PromptZeitVersandServiceTests`: Timer feuert (`FakeTimeProvider.Advance`) → automatischer Versand | Erledigt | `PromptZeitVersandServiceTests.Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch` |
| 28 | Tests | `PromptZeitVersandServiceTests`: Stornierung | Erledigt | `PromptZeitVersandServiceTests.CancelScheduledPrompt_EntferntGeplantenPrompt` |
| 29 | Tests | `PromptZeitVersandServiceTests`: zweiter Prompt ersetzt ersten | Erledigt | `PromptZeitVersandServiceTests.SchedulePromptAsync_ZweiterPromptFuerSelbeAufgabe_ErsetztErsten` |
| 30 | Tests | `PromptZeitVersandServiceTests`: fehlende Session → stilles Verwerfen (kein Event, keine Exception) | Erledigt | `PromptZeitVersandServiceTests.Timer_OhneSession_VerwirftPromptStill` |
| 31 | Tests | `TaskDetailViewModelTests`: Stunde/Minute-Binding | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.ScheduledPromptTargetHours_Binding_SetztProperty` |
| 32 | Tests | `TaskDetailViewModelTests`: leere Felder → kein Scheduling | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.SchedulePrompt_LeereFelder_KeinScheduling` |
| 33 | Tests | `TaskDetailViewModelTests`: gültige Zeit → Service-Aufruf + Status | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.SchedulePrompt_GueltigeZeit_RuftServiceAuf` |
| 34 | Tests | `TaskDetailViewModelTests`: ungültige Stunde → `FehlerMeldung` | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.SchedulePrompt_UngueltigeStunde_SetztFehlerMeldung` |
| 35 | Tests | `TaskDetailViewModelTests`: `Dispose` storniert geplante Prompts | Erledigt | `TaskDetailViewModelTests_ZeitgesteuerterPrompt.Dispose_StorniertGeplantePrompts` |
| 36 | E2E-Tests | `E2E_ZeitgesteuerterPrompt`: Planen → Status „Prompt in Wartestellung", kein Fehlerbanner | Erledigt | `E2E_ZeitgesteuerterPrompt.ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` |

> Hinweis: Die Tests wurden statisch (Existenz + Zuordnung) verifiziert. Ein vollständiger Build-/Testlauf zur Bestätigung der Grün-Ausführung steht im Rahmen dieses Reviews aus (nicht Teil des Plan-Reviews).
