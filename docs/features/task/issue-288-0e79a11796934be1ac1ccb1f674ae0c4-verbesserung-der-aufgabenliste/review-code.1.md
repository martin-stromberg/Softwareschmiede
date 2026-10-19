# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MainWindowViewModelTests.cs, DashboardViewModelTests.cs, AufgabenListenModusServiceTests.cs

- **Doppelter Code** — Die Hilfsmethode `ErstelleModusService()` (DI-Container mit temporaerer SQLite-DB, `EnsureCreated`, Rueckgabe `AufgabenListenModusService` + `ServiceProvider`) ist in drei Testdateien nahezu identisch dupliziert (~18 Zeilen pro Datei): `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs` (ca. Zeile 1005), `src/Softwareschmiede.Tests/App/ViewModels/DashboardViewModelTests.cs` (ca. Zeile 89) und `src/Softwareschmiede.Tests/ServiceIntegration/AufgabenListenModusServiceTests.cs` (inline im Konstruktor, gleiche Logik).

  Empfehlung: Gemeinsame Test-Factory auslagern (z. B. `src/Softwareschmiede.Tests/Testing/AufgabenListenModusServiceTestFactory.cs` oder in einer bestehenden Test-Infrastruktur-Klasse) und in allen drei Testklassen verwenden.

### AufgabenListenAnsicht.cs / MainWindowViewModel.cs

- **Kopplung / falsche Namespace-Zuordnung** — `AufgabenListenAnsicht` liegt im Namespace `Softwareschmiede.App.Controls`, ist aber kein Control, sondern eine statische Hilfsklasse fuer Sort-/Gruppierungsdeskriptoren. `MainWindowViewModel` (Namespace `Softwareschmiede.App.ViewModels`) greift auf die Gruppen-Fallback-Konstanten zu (`Controls.AufgabenListenAnsicht.OhneRepository` / `.NichtAusgefuehrt`, Zeilen ~530–535) — das zieht eine Abhaengigkeit von der ViewModel- in die Controls-Schicht nach sich.

  Empfehlung: `AufgabenListenAnsicht` (oder zumindest die Konstanten `OhneRepository`/`NichtAusgefuehrt`) in einen passenderen Namespace verschieben, z. B. `Softwareschmiede.App.ViewModels` (neben `AufgabenListenModus`) oder `Softwareschmiede.App.Services`.

### E2E_AufgabenListeModi.cs

- **Hardcodierte Werte** — Die Einstellungsschluessel werden als String-Literale verwendet (`"aktiveaufgaben.listenmodus"`, `"aktiveaufgaben.listenmodus.dashboard"`, Zeilen 124, 128, 141), obwohl dafuer im selben Branch Konstanten existieren (`AppEinstellungService.AktiveAufgabenListenModusKey`, `AppEinstellungService.AktiveAufgabenListenModusDashboardKey`). Bei einer Schluessel-Aenderung driftet der Test still.

  Empfehlung: Die Literale durch die Konstanten aus `AppEinstellungService` ersetzen.

### PluginSymbolTests.cs

- **Testabdeckung / Wartbarkeit** — `AlleRepoPlugins()` (Zeilen 16–32) enumeriert die acht Plugins manuell. Wird kuenftig ein weiteres Plugin hinzugefuegt, wird es vom Test nicht erfasst, ohne dass jemand daran erinnert wird — der Testname suggeriert aber Vollstaendigkeit ("ForAllRepoPlugins").

  Empfehlung: Die Plugin-Liste nicht hart kodieren, sondern alle `IPlugin`-Implementierungen per Reflection ueber die Plugin-Assemblies ermitteln (oder den PluginManager/DI-Registrierungspfad nutzen), damit neue Plugins automatisch abgedeckt sind.

## Gepruefte Dateien

Liste aller geprueften Dateien:
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
- `src/Softwareschmiede.App/Controls/AufgabenListenAnsicht.cs` (neu)
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Services/AufgabenListenModusService.cs` (neu)
- `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`
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
- `src/Softwareschmiede.Tests/App/Controls/AufgabenListenAnsichtTests.cs` (neu)
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusSymbolConverterTests.cs` (neu)
- `src/Softwareschmiede.Tests/App/ViewModels/DashboardViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AufgabePausieren.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AufgabenListeModi.cs` (neu)
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/Views/DashboardView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`
- `src/Softwareschmiede.Tests/PluginSymbolTests.cs` (neu)
- `src/Softwareschmiede.Tests/ServiceIntegration/AufgabenListenModusServiceTests.cs` (neu)
