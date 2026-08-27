# Tasks: Autonome Aufgaben mit Feature-Flag in Einstellungen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | Konstante `AutonomAufgabenEnabledKey = "autonomeaufgaben.enabled"` in `AppEinstellungService` definieren | Offen | — |
| 2 | Services (Logik) | Guard-Klausel in `AutonomAufgabenInitialisierungsService.InitialisiereAsync()`: Prüfe `_options.Value.Enabled`, werfe `InvalidOperationException` wenn false | Offen | Unit-Test: `WhenEnabledFlagIsFalse_InitialisiereAsync_ShouldThrow()` |
| 3 | Services (Logik) | Dependency Injection von `IOptions<AutonomAufgabenOptions>` in `ProjektleiterAgentService` Constructor hinzufügen | Offen | — |
| 4 | Services (Logik) | Guard-Klausel in `ProjektleiterAgentService.StarteAgentAsync()`: Prüfe `_autonomAufgabenOptions.Value.Enabled`, werfe `InvalidOperationException` wenn false | Offen | Unit-Test: `WhenEnabledFlagIsFalse_StarteAgentAsync_ShouldThrow()` |
| 5 | Services (Logik) | Dependency Injection von `IOptions<AutonomAufgabenOptions>` in `AutonomAufgabeStartService` Constructor hinzufügen | Offen | — |
| 6 | Services (Logik) | Guard-Klausel in `AutonomAufgabeStartService.StarteAsync()`: Prüfe `_autonomAufgabenOptions.Value.Enabled`, gebe Fehlerresultat zurück wenn false | Offen | Unit-Test: `WhenEnabledFlagIsFalse_StarteAsync_ShouldReturnError()` |
| 7 | ViewModels | Dependency Injection von `IOptions<AutonomAufgabenOptions>?` in `TaskDetailViewModel` Constructor hinzufügen | Offen | — |
| 8 | ViewModels | Neue computed Property `IsAutonomAufgabenEnabled` in `TaskDetailViewModel`: Gibt `_autonomAufgabenOptions?.Value.Enabled ?? false` zurück | Offen | Unit-Test: `IsAutonomAufgabenEnabled_WhenOptionsIsNull_ShouldReturnFalse()` |
| 9 | ViewModels | Geänderte Property `ShowAutomatisierungPanel` in `TaskDetailViewModel`: Bedingung von `IsAutonomAufgabe` zu `IsAutonomAufgabe && IsAutonomAufgabenEnabled` ändern | Offen | Unit-Test: `ShowAutomatisierungPanel_ShouldConsiderBothConditions()` |
| 10 | ViewModels | Neue Property `IsAutonomAufgabenEnabled` in `SettingsViewModel` mit privatem Backing-Field, Getter/Setter (WPF MVVM SetProperty-Pattern) | Offen | — |
| 11 | ViewModels | Handler für `SettingsViewModel.LadenCommand` anpassen: Lade `IsAutonomAufgabenEnabled` aus `AppEinstellungService.GetBoolSettingAsync(AutonomAufgabenEnabledKey)`, Fallback true | Offen | Unit-Test: `LoadCommand_ShouldLoadAutonomAufgabenEnabledFlag()` |
| 12 | ViewModels | Handler für `SettingsViewModel.SpeichernCommand` anpassen: Speichere `IsAutonomAufgabenEnabled` via `AppEinstellungService.SetBoolSettingAsync(AutonomAufgabenEnabledKey, value)` | Offen | Unit-Test: `SaveCommand_ShouldPersistAutonomAufgabenEnabledFlag()` |
| 13 | UI/Views | CheckBox in `SettingsView.xaml` hinzufügen: Binding `IsChecked="{Binding IsAutonomAufgabenEnabled, Mode=TwoWay}"`, Label "Autonome Aufgaben aktivieren" | Offen | E2E-Test: Settings UI-Schalter |
| 14 | Unit-Tests | Schreibe Test `AutonomAufgabenInitialisierungsServiceTests.WhenEnabledFlagIsFalse_InitialisiereAsync_ShouldThrow()`: Prüfe dass `InvalidOperationException` geworfen wird | Offen | Test-Datei: `AutonomAufgabenInitialisierungsServiceTests.cs` |
| 15 | Unit-Tests | Schreibe Test `AutonomAufgabenInitialisierungsServiceTests.WhenEnabledFlagIsTrue_InitialisiereAsync_ShouldSucceed()`: Baseline-Test, normale Ausführung | Offen | Test-Datei: `AutonomAufgabenInitialisierungsServiceTests.cs` |
| 16 | Unit-Tests | Schreibe Test `ProjektleiterAgentServiceTests.WhenEnabledFlagIsFalse_StarteAgentAsync_ShouldThrow()`: Prüfe dass `InvalidOperationException` geworfen wird | Offen | Test-Datei: `ProjektleiterAgentServiceTests.cs` (neu oder existierend) |
| 17 | Unit-Tests | Schreibe Test `ProjektleiterAgentServiceTests.WhenEnabledFlagIsTrue_StarteAgentAsync_ShouldSucceed()`: Baseline-Test, normale Ausführung | Offen | Test-Datei: `ProjektleiterAgentServiceTests.cs` |
| 18 | Unit-Tests | Schreibe Test `AutonomAufgabeStartServiceTests.WhenEnabledFlagIsFalse_StarteAsync_ShouldReturnError()`: Prüfe Fehlerresultat | Offen | Test-Datei: `AutonomAufgabeStartServiceTests.cs` (neu oder existierend) |
| 19 | Unit-Tests | Schreibe Test `AutonomAufgabeStartServiceTests.WhenEnabledFlagIsTrue_StarteAsync_ShouldShowDialog()`: Baseline-Test, Dialog wird geöffnet | Offen | Test-Datei: `AutonomAufgabeStartServiceTests.cs` |
| 20 | Unit-Tests | Schreibe Test `TaskDetailViewModelTests.IsAutonomAufgabenEnabled_WhenOptionsIsNull_ShouldReturnFalse()`: Property gibt false zurück wenn null | Offen | Test-Datei: `TaskDetailViewModelTests.cs` (neu oder existierend) |
| 21 | Unit-Tests | Schreibe Test `TaskDetailViewModelTests.IsAutonomAufgabenEnabled_WhenOptionsFalse_ShouldReturnFalse()`: Property gibt false zurück wenn Flag false | Offen | Test-Datei: `TaskDetailViewModelTests.cs` |
| 22 | Unit-Tests | Schreibe Test `TaskDetailViewModelTests.ShowAutomatisierungPanel_ShouldConsiderBothConditions()`: Panel wird nur gezeigt wenn beide Bedingungen true | Offen | Test-Datei: `TaskDetailViewModelTests.cs` |
| 23 | Unit-Tests | Schreibe Test `SettingsViewModelTests.LoadCommand_ShouldLoadAutonomAufgabenEnabledFlag()`: LadenCommand lädt Flag aus Service | Offen | Test-Datei: `SettingsViewModelTests.cs` (neu oder existierend) |
| 24 | Unit-Tests | Schreibe Test `SettingsViewModelTests.SaveCommand_ShouldPersistAutonomAufgabenEnabledFlag()`: SpeichernCommand speichert Flag in Service | Offen | Test-Datei: `SettingsViewModelTests.cs` |
| 25 | Integration-Tests | Schreibe Test `EntwicklungsprozessServiceTests.WhenFeatureFlagDisabled_ShouldUseFallbackPath()`: Prüfe dass einfacher Weg weiterhin funktioniert | Offen | Test-Datei: `EntwicklungsprozessServiceTests.cs` (Integration-Ordner) |
| 26 | E2E-Tests | Schreibe Test `E2E_AutonomAufgabenInitialisierung.WhenAutonomAufgabenEnabled_FullInitializationFlow()`: Happy Path mit aktiviertem Feature | Offen | E2E-Testlauf erfolgreich |
| 27 | E2E-Tests | Schreibe Test `E2E_AutonomAufgabenInitialisierung.WhenAutonomAufgabenDisabled_UIElementsShouldNotBeDisplayed()`: Buttons/Dialog nicht sichtbar wenn deaktiviert | Offen | E2E-Testlauf erfolgreich |
| 28 | E2E-Tests | Schreibe Test `E2E_AutonomAufgabenInitialisierung.WhenAutonomAufgabenDisabled_SimpleStartButtonShouldBeAvailable()`: Fallback-Button ist verfügbar | Offen | E2E-Testlauf erfolgreich |
| 29 | E2E-Tests | Schreibe Test `E2E_AutonomAufgabenAgentExecution.WhenAutonomAufgabenDisabled_AgentShouldNotStart()`: Agent startet nicht wenn deaktiviert | Offen | E2E-Testlauf erfolgreich |
| 30 | E2E-Tests | Schreibe Test für Settings UI-Schalter: Nutzer toggle CheckBox "Autonome Aufgaben aktivieren", Änderung wird persistiert | Offen | E2E-Testlauf erfolgreich |
| 31 | Test-Anpassungen | Überprüfe und passe bestehende Tests in `AutonomAufgabenInitialisierungsServiceTests.*` an: Feature-Flag muss in Test-Setup true sein | Offen | Alle bestehenden Tests laufen durch |
| 32 | Test-Anpassungen | Überprüfe und passe bestehende Tests in `E2E_AutonomAufgabenInitialisierung.cs` an: Feature-Flag muss vor Tests aktiviert sein | Offen | Alle bestehenden E2E-Tests laufen durch |
| 33 | Test-Anpassungen | Überprüfe und passe bestehende Tests in `E2E_AutonomAufgabenAgentExecution.cs` an: Feature-Flag muss vor Tests aktiviert sein | Offen | Alle bestehenden E2E-Tests laufen durch |
