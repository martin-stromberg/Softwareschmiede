# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Doppelter Code / unnötige Doppelarbeit** — Im Haupt-Button-Zweig von `OeffneIdeInternAsync` (Zeilen 1941–1954) wird für denselben Klick zweimal vollständig unabhängig aufgelöst: zuerst `ErmittleIdeEntryPointsAsync(effectiveWorkdir, ct)` (ruft intern `PluginSelectionService.ResolveIdePluginAsync` auf, das wiederum `GetOrderedEnabledIdePluginsAsync` — mit zwei DB-Zugriffen (`GetEnabledIdePluginsAsync`, `GetSettingAsync`) — sowie `CheckCompatibilityAsync` je aktiviertem Plugin ausführt), danach direkt im Anschluss `ErmittleAggregierteIdeEinstiegspunkteAsync(effectiveWorkdir, ct)`, die exakt dieselbe Kette (`GetOrderedEnabledIdePluginsAsync` inkl. beider DB-Zugriffe, `CheckCompatibilityAsync` je Plugin) ein zweites Mal durchläuft und zusätzlich für jedes kompatible Plugin `FindEntryPointsAsync` aufruft — einschließlich des bereits in `ErmittleIdeEntryPointsAsync` abgefragten Plugins, dessen `FindEntryPointsAsync`-Ergebnis somit ebenfalls doppelt berechnet wird. Da `ResolveIdePluginAsync` stets das gemäß Plugin-Reihenfolge erste Explicit- bzw. (wenn keins existiert) erste Fallback-kompatible Plugin liefert und `ResolveAlleKompatiblenIdePluginsAsync` dieselbe Reihenfolge in `explicitPlugins`/`fallbackPlugins` gruppiert, entspricht `aggregierteEintraege[0]` bei jedem Aufruf exakt `(plugin, entryPoints[0])` aus dem ersten Aufruf. Bei jedem Klick auf den Haupt-Button werden dadurch mindestens 2 zusätzliche DB-Zugriffe sowie doppelte Kompatibilitäts-/Einstiegspunkt-Ermittlung pro aktiviertem IDE-Plugin unnötig ausgeführt.

  Empfehlung: Im Haupt-Button-Zweig von `OeffneIdeInternAsync` den Aufruf von `ErmittleIdeEntryPointsAsync` entfernen und stattdessen direkt `ErmittleAggregierteIdeEinstiegspunkteAsync` verwenden; das zu öffnende Plugin/EntryPoint-Paar ist `aggregierteEintraege[0]` (analog zum bereits vorhandenen `eintraege.Count == 1`-Fall im Dropdown-Zweig). Dadurch entfällt sowohl die private Methode `ErmittleIdeEntryPointsAsync` als auch die zugehörige Redundanz; `PluginSelectionService.ResolveIdePluginAsync` bliebe als API für andere Aufrufer weiterhin bestehen, wird von diesem Zweig dann aber nicht mehr indirekt doppelt durchlaufen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede/Application/Services/PluginActivationService.cs`
- `src/Softwareschmiede/Application/Services/IdePluginOrderResolver.cs`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs`
- `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs`
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- `src/Softwareschmiede/Infrastructure/Services/VisualStudioCodeLocator.cs`
- `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`
- `src/Softwareschmiede/Domain/Interfaces/IVisualStudioCodeLocator.cs`
- `src/Softwareschmiede/Domain/Enums/PluginKategorie.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/IdePluginCompatibility.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IdeEntryPoint.cs`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs`
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_Arbeitsverzeichnis.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests_IdePlugin.cs`
- `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests_IdePlugin.cs`
- `src/Softwareschmiede.Tests/Application/Services/PluginActivationServiceTests.cs`
- `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioCodeIdePluginTests.cs`
- `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioIdePluginTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_IdePluginSelection.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_IdePluginSettings.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailView_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede.Tests/Helpers/TestVisualStudioCodeLocator.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`
