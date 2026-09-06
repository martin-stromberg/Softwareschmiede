# Inventory: Anzeige der CLI

## Betroffene Komponenten

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
  - `AktiverCliName`
  - `ResolvePluginViaDialogAsync`
  - `StartCliAndUpdateStateAsync`
  - `AktualisiereAktivenCliNameAusAufgabeAsync`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
  - `MapAktiveAufgabePanelItem` (Seitenleiste / Programmmenü)
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
  - Fußzeilen-Binding `AktiverCliName`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`
  - Seitenleisten-Binding `KiPluginName`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginAuswahlUndWechsel.cs`
  - `PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
  - Regressionstests für `PluginAendernCommand` und `CliNeustartenCommand`

## Persistenz

- `Aufgabe.KiPluginPrefix` wird in `AufgabeService.UpdateAsync` geschrieben.
- `GetDetailAsync` liefert `AsNoTracking`-Entities, daher muss das in-memory `_aufgabe`-Objekt explizit nach einem Update synchronisiert werden.

## Test-Werte

- Projekt-Default: `Softwareschmiede.TestKi` → Anzeige "Test KI"
- Gewechseltes Plugin: `Softwareschmiede.ZweitesKi` → Anzeige "Zweites KI"
- E2E-Plugin-Namen: `KI Simulator` und `Claude CLI`
