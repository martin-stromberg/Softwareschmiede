# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModelTests_IdeAuswahl.cs (TaskDetailViewModelTests_IdeAuswahl)

- **Doppelter Code** — `WaehleEntryPointAsync_UsesDisplayNameInDialog` (Zeile 242–252) und `KannIdeAuswaehlen_WhenOpenEntryPointFailsWithMultipleEntryPoints_BleibtTrue` (Zeile 287–297) bauen jeweils denselben `Mock<IIdePlugin>` mit acht identischen Setup-Zeilen auf (`PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups()`, `CheckCompatibilityAsync(...)`, `FindEntryPointsAsync(...)`) und unterscheiden sich nur im letzten Setup für `OpenEntryPointAsync` (einmal `Returns(Task.CompletedTask)`, einmal `ThrowsAsync(...)`).

  Empfehlung: Eine private Hilfsmethode wie `CreateTestIdePluginMock(IReadOnlyList<IdeEntryPoint> entryPoints)` in der Testklasse (oder in `TaskDetailViewModelTestsBase`, falls künftig weitere Tests denselben Aufbau brauchen) ergänzen, die den gemeinsamen Teil kapselt und ein `Mock<IIdePlugin>` zurückgibt; das `OpenEntryPointAsync`-Setup bleibt Sache des jeweiligen Testfalls.

## Geprüfte Dateien

Vollständig gelesen (im aktuellen Branch gegenüber `main` geänderte bzw. neu erstellte Quelldateien):

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyles.xaml`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs`
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml` (neu, noch ungetrackt)
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml.cs` (neu, noch ungetrackt)
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/IdePluginCompatibility.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IdeEntryPoint.cs`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede/Application/Services/IdePluginOrderResolver.cs`
- `src/Softwareschmiede/Application/Services/PluginActivationService.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Domain/Enums/PluginKategorie.cs`
- `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`
- `src/Softwareschmiede/Domain/Interfaces/IVisualStudioCodeLocator.cs`
- `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs`
- `src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs`
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- `src/Softwareschmiede/Infrastructure/Services/VisualStudioCodeLocator.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests_IdePlugin.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_Arbeitsverzeichnis.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/Application/Services/PluginActivationServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests_IdePlugin.cs`
- `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioCodeIdePluginTests.cs`
- `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioIdePluginTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_IdePluginSelection.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_IdePluginSettings.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailView_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede.Tests/Helpers/TestVisualStudioCodeLocator.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`

Mit besonderem Fokus (Kernstück dieser Iteration) auf Diff gegen den letzten Commit (`63f3d9e`) geprüft:

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` — Bugfix aus dem vorherigen Review verifiziert: `KannIdeAuswaehlen = false;` wurde aus dem `catch`-Block von `OeffneIdeInternAsync` entfernt (Zeile ~1899–1907); der zuvor im try-Block über `BerechneKannIdeAuswaehlen(entryPoints)` gesetzte Wert bleibt nach einem fehlgeschlagenen `OpenEntryPointAsync` erhalten. Zusätzlich wurde die gemeinsame Ermittlungslogik in `ErmittleIdeEntryPointsAsync` extrahiert und wird jetzt auch aus einer neuen `AktualisiereKannIdeAuswaehlenAsync` heraus in `LadenAsync` aufgerufen, wodurch `KannIdeAuswaehlen` bereits beim Laden der Aufgabe (nicht erst nach einem Öffnen-Versuch) korrekt berechnet wird.
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml` / `.xaml.cs` — neues UserControl, das die vormals doppelte StackPanel-Struktur aus `SettingsView.xaml` ablöst. DependencyProperty-Implementierung (`HeaderText`, `IsEnabledChecked`, `SettingsSource`, `SettingsItemTemplate`, `CheckboxAutomationName`) ist sauber, konsistent benannt und deckt sich mit dem Muster der übrigen Controls (`RibbonLargeButton`, `RibbonSplitButton`). Keine eigenen Unit-Tests vorhanden — konsistent mit dem bestehenden Muster des Projekts, WPF-Controls nicht isoliert zu unit-testen, sondern über E2E-Automatisierung abzudecken; funktionale Abdeckung der beiden Checkbox-Instanzen (`PluginAktiviert`, `IdePluginAktiviert`) ist über die bereits bestehenden `E2E_PluginAktivierung.cs` und `E2E_IdePluginSettings.cs` gegeben (beide Automation-Namen unverändert erhalten, keine Anpassung nötig).
- `src/Softwareschmiede.App/Views/SettingsView.xaml` / `.xaml.cs` — Duplikat aus dem vorherigen Review verifiziert behoben: beide StackPanel-Blöcke im Plugins-Register sind durch zwei `PluginDetailPanel`-Instanzen ersetzt. Zusätzlich wurde der Selektions-Handler-Guard (`DataContext is SettingsViewModel && AddedItems.Count > 0`) in eine gemeinsame `TryGetViewModelAndFirstAddedItem`-Methode extrahiert, die von `OnPluginSelectionChanged` und `OnIdePluginSelectionChanged` genutzt wird — sinnvolle zusätzliche Entdopplung.
- `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs`, `VisualStudioIdePlugin.cs`, `Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs` und zugehörige Tests — verwaiste `OpenRepositoryAsync`-Methode (durch `FindEntryPointsAsync`/`OpenEntryPointAsync` abgelöst) vollständig aus Interface, beiden Implementierungen und den zugehörigen Testklassen entfernt; keine verbliebenen Referenzen im Quellbaum gefunden.
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` — `_ = PersistiereIdePluginOrderAsync();` durch `PersistiereIdePluginOrderAsync().SafeFireAndForget(_logger, ...)` ersetzt, wodurch unbehandelte Exceptions im Fire-and-forget-Aufruf jetzt geloggt statt still verschluckt werden.
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs` — Befund aus dem vorherigen Review verifiziert behoben: `CreateVerzeichnisAktionenServices` in `CreateArbeitsverzeichnisOeffnenService` umbenannt, konsistent an allen fünf Aufrufstellen aktualisiert.
