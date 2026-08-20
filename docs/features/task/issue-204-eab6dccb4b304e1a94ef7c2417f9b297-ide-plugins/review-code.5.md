# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Fehlerbehandlung** — In `OeffneIdeInternAsync` (Methode um Zeile 1870) wird `KannIdeAuswaehlen` direkt nach `ErmittleIdeEntryPointsAsync` korrekt über `BerechneKannIdeAuswaehlen(entryPoints)` gesetzt. Schlägt danach aber `plugin.OpenEntryPointAsync(...)` fehl (z. B. IDE-Prozess kann nicht gestartet werden) oder wirft der `waehleEntryPointAsync`-Callback, wird im `catch (Exception ex)`-Block unconditional `KannIdeAuswaehlen = false;` gesetzt — obwohl die Anzahl der gefundenen Einstiegspunkte sich durch diesen Fehler nicht geändert hat. Bei zwei oder mehr Einstiegspunkten verschwindet dadurch nach einem fehlgeschlagenen Öffnen-Versuch fälschlich der Dropdown-Button des `RibbonSplitButton` (`CanShowDropdown` ist an `KannIdeAuswaehlen` gebunden), obwohl der Anwender genau jetzt über den Dropdown einen anderen Einstiegspunkt probieren könnte.

  Empfehlung: Die Zeile `KannIdeAuswaehlen = false;` aus dem generischen `catch (Exception ex)`-Block entfernen. Der zuvor im try-Block gesetzte Wert aus `BerechneKannIdeAuswaehlen(entryPoints)` spiegelt bereits korrekt wider, ob mehrere Einstiegspunkte existieren, unabhängig davon, ob das anschließende Öffnen erfolgreich war. Falls schon `ErmittleIdeEntryPointsAsync` selbst fehlschlägt (bevor `KannIdeAuswaehlen` gesetzt wurde), bleibt der zuvor gültige Wert erhalten, was für diesen Fall unkritisch ist.

### SettingsView.xaml

- **Doppelter Code** — Im Plugins-Register (rechte Spalte, Zeilen ca. 400–430) existieren zwei nahezu identische `StackPanel`-Blöcke: einer für die SCM/KI-Plugin-Details (`IsScmKiPluginContentVisible`, gebunden an `SelectedPlugin`/`SelectedPluginSettings`, Checkbox-Automation-Name `PluginAktiviert`) und einer für die IDE-Plugin-Details (`IsIdePluginContentVisible`, gebunden an `SelectedIdePlugin`/`SelectedIdePluginSettings`, Checkbox-Automation-Name `IdePluginAktiviert`). Beide bestehen aus derselben Struktur (Titel-`TextBlock`, „Plugin aktiviert"-`CheckBox`, `ItemsControl` mit `PluginSettingGroupsItemTemplate`) und unterscheiden sich nur in den gebundenen Property-Namen.

  Empfehlung: Die beiden Blöcke zu einem wiederverwendbaren `UserControl` (z. B. `PluginDetailPanel`) mit `DependencyProperty`s für `HeaderText`, `IsEnabledChecked` (TwoWay) und `SettingsSource` sowie einem Parameter für den Automation-Namen der Checkbox extrahieren, und dieses UserControl zweimal mit den jeweils passenden Bindings instanziieren, statt die komplette Struktur zu duplizieren.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IdeEntryPoint.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/IdePluginCompatibility.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`
- `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs`
- `src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede/Application/Services/IdePluginOrderResolver.cs`
- `src/Softwareschmiede/Application/Services/PluginActivationService.cs`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Application/Services/AsyncTaskExtensions.cs`
- `src/Softwareschmiede/Domain/Enums/PluginKategorie.cs`
- `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`
- `src/Softwareschmiede/Domain/Interfaces/IVisualStudioCodeLocator.cs`
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- `src/Softwareschmiede/Infrastructure/Services/VisualStudioCodeLocator.cs`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyles.xaml`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
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
