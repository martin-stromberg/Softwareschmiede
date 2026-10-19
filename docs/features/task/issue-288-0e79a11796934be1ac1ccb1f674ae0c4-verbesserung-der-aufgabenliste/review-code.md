# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Nachverfolgung der Befunde aus der vorherigen Iteration

Alle fünf Befunde der letzten Iteration wurden verifiziert als behoben:

- **MenuView.cs / DashboardView.cs (E2E-Views)** — Die gemeinsame Logik wurde nach `BaseWindowView` gehoben: `KlickeAufgabenModusUmschalter` (`BaseWindowView.cs:148`), `GetAufgabenGruppenNamen(string)` (`BaseWindowView.cs:157`) und `WaitForAufgabenGruppe(string, string, TimeSpan)` (`BaseWindowView.cs:179`). `MenuView` (`MenuView.cs:309–334`) und `DashboardView` (`DashboardView.cs:47–72`) enthalten nur noch dünne Wrapper, die ihre ansichtsspezifischen Konstanten (`AufgabenListeAutomationId`, `AufgabenModusPraefix`) übergeben. Keine Duplikation mehr.

- **MainWindowViewModel.cs / DashboardViewModel.cs** — Die Modus-Verwaltung ist in die neue Hilfsklasse `src/Softwareschmiede.App/Services/AufgabenListenModusBindung.cs` ausgelagert; beide ViewModels delegieren Property (`AufgabenListenModus`) und Restore (`GespeichertenAufgabenListenModusLadenAsync`) dorthin (`MainWindowViewModel.cs:36, 107–110, 170–172, 216–217`; `DashboardViewModel.cs:16, 91–95, 108–109, 173–174`). Die Semantik ist identisch geblieben: Race-Guard `_geaendert` wird vor dem `await` (`AufgabenListenModusBindung.cs:125`) und nach dem `await` innerhalb des Dispatch-Blocks (`:131`) geprüft; die Wiederherstellung setzt `_wert` direkt ohne Write-Back über den Setter (`:134–137`); die Persistenz läuft im Setter per `SafeFireAndForget` (`:109–113`). Der Dispatcher ist optional — `FuerMenue` erhält `_dispatcherInvoke`, `FuerDashboard` keinen, wie zuvor.

- **AufgabenListenModusServiceTests.cs** — Die Diagnose-/Debug-Blöcke (Same-Scope-/Cross-Scope-`GetSettingAsync`, Roh-Tabellen-Dump) sind entfernt; `SpeichernUndLaden_MenueModus_RoundtripUeberGetrennteScopes` ist ein sauberer SUT-Roundtrip (`AufgabenListenModusServiceTests.cs:27–33`). Die Testinfrastruktur nutzt die gemeinsame Factory `AufgabenListenModusServiceTestFactory` (`:19`).

- **IPlugin.FallbackSymbol** — Konstante an `IPlugin` eingeführt (`IPlugin.cs:17`) und überall referenziert: Interface-Default (`IPlugin.cs:42`), `CliKiPluginBase.cs:24`, `GitPluginBase.cs:26`, `MainWindowViewModel.cs:441`, `MainWindowViewModelTests.cs:852, 855`, `PluginSymbolTests.cs:78`. Das verbleibende Literal ist ausschließlich der dokumentierte Contract-Pin `InterfaceDefault` in `PluginSymbolTests.cs:18`, der per Review-Empfehlung bewusst bestehen bleibt.

- **AufgabenListenAnsichtTests.cs** — Liegt nun unter `src/Softwareschmiede.Tests/App/ViewModels/` mit Namespace `Softwareschmiede.Tests.App.ViewModels` (`AufgabenListenAnsichtTests.cs:5`); der alte Controls-Pfad existiert nicht mehr.

Die diff-geänderten Dateien wurden zusätzlich auf neue grobe Verstöße (doppelter Code, toter Code, God-Methoden) geprüft — ohne Befund.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`
- `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
- `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`
- `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
- `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`
- `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Services/AufgabenListenModusBindung.cs` (neu)
- `src/Softwareschmiede.App/Services/AufgabenListenModusService.cs` (neu)
- `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`
- `src/Softwareschmiede.App/ViewModels/AufgabenListenAnsicht.cs` (neu)
- `src/Softwareschmiede.App/ViewModels/AufgabenListenModus.cs` (neu)
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowOptionaleDienste.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusSymbolConverterTests.cs` (neu)
- `src/Softwareschmiede.Tests/App/ViewModels/AufgabenListenAnsichtTests.cs` (neu)
- `src/Softwareschmiede.Tests/App/ViewModels/DashboardViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AufgabePausieren.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AufgabenListeModi.cs` (neu)
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/Views/BaseWindowView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/DashboardView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/WindowExtensions.cs`
- `src/Softwareschmiede.Tests/Helpers/AufgabenListenModusServiceTestFactory.cs` (neu)
- `src/Softwareschmiede.Tests/PluginSymbolTests.cs` (neu)
- `src/Softwareschmiede.Tests/ServiceIntegration/AufgabenListenModusServiceTests.cs` (neu)
