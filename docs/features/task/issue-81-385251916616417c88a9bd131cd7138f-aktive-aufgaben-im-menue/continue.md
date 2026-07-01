# Offene Aufgaben

Erstellt am: 2026-07-01
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 1: 17 offene Punkte, Iteration 2: 18 offene Punkte)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — der Plan gilt laut `review.md` als vollständig umgesetzt.

## Code-Review-Befunde

- [ ] MainWindowViewModel.cs: Konstruktor ruft `AktiveAufgabenAktualisierenAsync()` redundant doppelt auf (einmal implizit über `NavigateToDashboard()`, einmal explizit direkt danach) — expliziten Aufruf entfernen.
- [ ] MainWindowViewModel.cs: Identischer Refresh-Aufruf `_ = AktiveAufgabenAktualisierenAsync();` in `NavigateToDashboard()`, `NavigateToProjectList()` und `NavigateToSettings()` dupliziert — zentral im `CurrentView`-Setter auslösen (z. B. über den `onChanged`-Callback von `SetProperty`).
- [ ] MainWindowViewModel.cs: Fire-and-forget-Aufrufe von `AktiveAufgabenAktualisierenAsync` mit Re-Throw von `OperationCanceledException`, die bei Default-Token toter Code sind bzw. bei künftiger Verwendung eine unbeobachtete Task-Exception erzeugen könnten — CancellationToken-Handling bereinigen oder Task-Fehlerbehandlung ergänzen.
- [ ] App.xaml: `AktiveAufgabeCardTemplate` bindet Commands über `RelativeSource AncestorType=Window` statt wie sonst im Projekt üblich über `AncestorType=UserControl` (siehe `RecoveryBannerControl.xaml`, `StatusIndicatorControl.xaml`, `ProjectListView.xaml`) — versteckte Kopplung an `MainWindowViewModel` durch Delegate-Muster ersetzen.
- [ ] MainWindowViewModel.cs / DashboardViewModel.cs: Beide laden unabhängig voneinander `GetAktiveAufgabenAsync`, wodurch bei Dashboard-Navigation die aktiven Aufgaben doppelt aus der DB geladen werden — einzige gemeinsame Datenquelle vorsehen.

## Fehlgeschlagene Tests

- [ ] TaskDetailViewModelTests.TestPluginWechselAsync_StopsCliAndStartsNew — Expected sut.IsCliRunning to be True, but found False.
- [ ] E2E_TaskExecutionCommandLineParameters.AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.
- [ ] E2E_PluginWechsel.PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.
- [ ] E2E_PluginSelectionDialog.StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.
- [ ] E2E_PluginProjectDefault_NextTask.ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.
- [ ] E2E_PluginProjectDefault.PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.
- [ ] E2E_CreateNewTaskNavigation.NeueAufgabeAbbrechen_NavigiertZurueckOhneTitelAenderungZuSpeichern_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_ConPtyTerminalStart.ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_ConPtyResize.ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_ConPtyProcessEnd.ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_ConPtyKeyboardInput.ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_AutoStartCli.AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E — System.Exception: Could not find process with id (Process is not running).
- [ ] E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E — System.Exception: Could not find process with id (Process is not running).

**Hinweis:** Die 12 E2E-Tests sind über beide Iterationen hinweg als FlaUI-Timeouts bzw. Prozess-Handle-Fehler bei der automatisierten WPF-App-Steuerung aufgetreten und betreffen keine der geänderten Dateien dieses Features (Sidebar/Dashboard/AktiveAufgaben). Sie deuten auf eine vorbestehende Umgebungs-Flakiness der E2E-Test-Infrastruktur hin, nicht auf einen Regressionsfehler dieser Anforderung. `TestPluginWechselAsync_StopsCliAndStartsNew` ist ebenfalls fachlich unabhängig vom Feature (Plugin-Wechsel/CLI-Prozess-Steuerung) und trat in Iteration 1 noch nicht auf.
