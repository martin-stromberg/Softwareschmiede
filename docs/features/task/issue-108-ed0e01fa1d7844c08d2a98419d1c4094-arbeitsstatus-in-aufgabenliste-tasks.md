# Tasks: Arbeitsstatus in Aufgabenliste

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik/ViewModel | `MainWindowViewModel`: Konstruktor um `IRunningAutomationStatusSource` und optionalen `Action<Action> dispatcherInvoke` erweitern (Dispatcher-Fallback wie `TaskDetailViewModel`) | Offen | — |
| 2 | Logik/ViewModel | `MainWindowViewModel`: Felder ergänzen (`DispatcherTimer`, `SemaphoreSlim _refreshGate`, `_dispatcherInvoke`, `_disposed`, Konstante `AktualisierungsIntervallSekunden = 5`) | Offen | — |
| 3 | Logik/ViewModel | `MainWindowViewModel`: `RunningCountChanged` abonnieren und `DispatcherTimer` im Konstruktor starten | Offen | — |
| 4 | Logik/ViewModel | `MainWindowViewModel.OnRunningCountChanged(int, int)` implementieren (UI-Thread-Marshalling + `SafeFireAndForget`) | Offen | — |
| 5 | Logik/ViewModel | `MainWindowViewModel.OnAktualisierungsTimerTick(...)` implementieren (Timer-Fallback) | Offen | — |
| 6 | Logik/ViewModel | `MainWindowViewModel.AktiveAufgabenAktualisierenAsync()` um Re-Entrancy-Schutz (`SemaphoreSlim.Wait(0)`/Skip-if-busy) erweitern | Offen | — |
| 7 | Logik/ViewModel | `MainWindowViewModel`: `IDisposable` implementieren (Timer stoppen, Event abmelden, Semaphore freigeben, idempotent) | Offen | — |
| 8 | UI | `MainWindow.OnClosed`: `(DataContext as IDisposable)?.Dispose()` aufrufen | Offen | — |
| 9 | UI/Animation | `StatusAenderungsErkennung` (POCO) anlegen: `Dictionary<Guid,string?>` + `HatSichGeaendert(Guid, string?)` (Baseline/unverändert → false, echter Wechsel → true) | Offen | — |
| 10 | UI/Animation | `StatusUebergangsAnimation` (Attached Behavior) anlegen: Attached Property `Status` (string), `PropertyChangedCallback` liest `Aufgabe.Id` aus `DataContext`, fragt `StatusAenderungsErkennung` ab | Offen | — |
| 11 | UI/Animation | `StatusUebergangsAnimation`: dezente Opacity-Fade-`DoubleAnimation` (0.3 → 1.0, ~250 ms, EaseOut) bei echtem Wechsel auf dem Status-`TextBlock` starten (Konstanten für Dauer/Start-Opacity) | Offen | — |
| 12 | UI | `ActiveTasksListControl.xaml`: Status-`TextBlock` um `AutomationProperties.Name` und `AutomationProperties.HelpText` (Converter-Ergebnis) ergänzen | Offen | — |
| 13 | UI/Animation | `ActiveTasksListControl.xaml`: XML-Namespace für Behavior deklarieren und `StatusUebergangsAnimation.Status="{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}"` am Status-`TextBlock` setzen | Offen | — |
| 14 | Konfiguration/DI | `App.xaml.cs`-Registrierung von `MainWindowViewModel` verifizieren (neuer `IRunningAutomationStatusSource`-Parameter auflösbar, keine Codeänderung erwartet) | Offen | — |
| 15 | Tests | `MainWindowViewModelTests.CreateSut()` um neue Konstruktorargumente erweitern (Mock + synchroner `dispatcherInvoke`) | Offen | — |
| 16 | Tests | Test `RunningCountChanged_ShouldReloadAktiveAufgabenListe_WhenRaised` schreiben | Offen | — |
| 17 | Tests | Test `AktiveAufgabenAktualisierenAsync_ShouldSkip_WhenAlreadyRunning` schreiben | Offen | — |
| 18 | Tests | Test `Dispose_ShouldUnsubscribeFromRunningCountChanged` schreiben | Offen | — |
| 19 | Tests | `StatusAenderungsErkennungTests`: Test `HatSichGeaendert_ShouldReturnFalse_OnErstbeobachtung` schreiben | Offen | — |
| 20 | Tests | `StatusAenderungsErkennungTests`: Test `HatSichGeaendert_ShouldReturnFalse_WhenStatusUnveraendert` schreiben | Offen | — |
| 21 | Tests | `StatusAenderungsErkennungTests`: Test `HatSichGeaendert_ShouldReturnTrue_WhenStatusWechselt` schreiben | Offen | — |
| 22 | Tests | `StatusAenderungsErkennungTests`: Test `HatSichGeaendert_ShouldTrackIdsUnabhaengig` schreiben | Offen | — |
| 23 | E2E-Tests | `E2E_ArbeitsstatusAktualisierung` anlegen: Aufgabe starten → Seitenleisten-Status „▶ Läuft" ohne manuelles Neuladen, nach CLI-Stopp „✓ Bereit" | Offen | — |
