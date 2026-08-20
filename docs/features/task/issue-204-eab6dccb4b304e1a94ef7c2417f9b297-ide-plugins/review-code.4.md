# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### IIdePlugin.cs, VisualStudioIdePlugin.cs, VisualStudioCodeIdePlugin.cs (`IIdePlugin`, `VisualStudioIdePlugin`, `VisualStudioCodeIdePlugin`)

- **Toter Code** — `OpenRepositoryAsync` ist weiterhin Teil des `IIdePlugin`-Vertrags (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`) und in beiden eingebauten Plugins implementiert (`src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs`, `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs`), wird aber von keiner Produktionslogik mehr aufgerufen. `TaskDetailViewModel` löst IDE-Öffnen-Vorgänge ausschließlich über `FindEntryPointsAsync`/`OpenEntryPointAsync` auf (siehe `ErmittleIdeEntryPointsAsync`/`OeffneIdeInternAsync`). Die Methode wird nur noch aus den zugehörigen Unit-Tests aufgerufen (`VisualStudioIdePluginTests.OpenRepositoryAsync_ShouldOpenFirstSolution_WhenMultipleExist`, `VisualStudioCodeIdePluginTests.OpenRepositoryAsync_*`).

  Empfehlung: `OpenRepositoryAsync` aus `IIdePlugin` sowie beiden Implementierungen und den zugehörigen Tests entfernen, sofern kein externer Plugin-Konsument dokumentiert auf diese Methode angewiesen ist. Falls die Methode bewusst als Kompatibilitätsschicht für Drittanbieter-Plugins erhalten bleiben soll, dies als Kommentar am Interface-Member dokumentieren.

### TaskDetailViewModel.cs (`TaskDetailViewModel`)

- **Doppelter Code** — Die Berechnung `KannIdeAuswaehlen = entryPoints.Count >= 2;` ist identisch in `AktualisiereKannIdeAuswaehlenAsync` (Zeile ~1798) und in `OeffneIdeInternAsync` (Zeile ~1882) enthalten.

  Empfehlung: Gemeinsame private statische Hilfsmethode extrahieren, z. B. `private static bool BerechneKannIdeAuswaehlen(IReadOnlyList<IdeEntryPoint> entryPoints) => entryPoints.Count >= 2;`, und an beiden Stellen verwenden.

- **Fehlerbehandlung** — In `WaehleEntryPointAsync` (Zeile ~1920–1929) wird, falls der vom Dialog zurückgegebene Anzeigewert keinem der übergebenen `IdeEntryPoint`-Objekte zugeordnet werden kann, per Fallback `new IdeEntryPoint(gewaehlterWert)` ein neuer Einstiegspunkt konstruiert, dessen `Path` in diesem Fall tatsächlich der Anzeigetext (`DisplayName`) ist, nicht ein gültiger Dateisystempfad. Da `ShowSolutionSelectionDialogAsync` nur Werte aus `anzeigeWerte` zurückgeben sollte, ist dieser Pfad im Normalfall unerreichbar — als Fallback für einen eigentlich nie erwarteten Zustand erzeugt er aber einen fachlich falschen Einstiegspunkt (Versuch, den Anzeigetext als Pfad zu öffnen) statt den Fehlerfall explizit zu behandeln.

  Empfehlung: Bei fehlendem Treffer `null` zurückgeben (wie beim Abbruch) statt einen synthetischen `IdeEntryPoint` mit ungültigem `Path` zu erzeugen, oder eine aussagekräftige Exception werfen.

### SettingsView.xaml.cs (`SettingsView`)

- **Doppelter Code** — `OnIdePluginSelectionChanged` (Zeile 54–61) dupliziert die Guard-Klausel (`DataContext is not SettingsViewModel vm || e.AddedItems.Count == 0`) sowie das `PluginActivationEntry`-Pattern-Matching, das im bestehenden gemeinsamen Handler `OnPluginSelectionChanged` (Zeile 32–49) für den `PluginActivationEntry`-Fall bereits vorhanden ist. Da beide Handler dasselbe Element (`PluginActivationEntry`) behandeln, aber unterschiedliche Kommandos aufrufen müssen (SCM/KI vs. IDE), lässt sich der Typ-Switch allein nicht zur Unterscheidung nutzen.

  Empfehlung: Gemeinsame private Hilfsmethode für die Guard-Prüfung extrahieren (z. B. `TryGetViewModelAndFirstAddedItem`), die von beiden Handlern genutzt wird, um die Duplikation der Prüfung zu vermeiden.

### SettingsViewModel.cs (`SettingsViewModel`)

- **Fehlerbehandlung / Konsistenz** — `MoveIdePlugin` (Zeile ~469) startet die Persistierung der Reihenfolge mit einem bloßen `_ = PersistiereIdePluginOrderAsync();` statt der im übrigen Code etablierten `SafeFireAndForget`-Erweiterungsmethode (siehe `TaskDetailViewModel.LadenAsync(...).SafeFireAndForget(_logger, "...")`). `PersistiereIdePluginOrderAsync` fängt zwar intern alle Exceptions ab, sodass kein unbeobachteter Task entsteht, die Abweichung vom projektweiten Fire-and-Forget-Muster ist aber inkonsistent zum sonstigen Umgang mit "Feuer und vergiss"-Aufrufen im Code.

  Empfehlung: `_ = PersistiereIdePluginOrderAsync().SafeFireAndForget(_logger, "SettingsViewModel.PersistiereIdePluginOrderAsync");` verwenden, um dem etablierten Muster zu folgen (auch wenn intern bereits abgefangen wird, erhöht dies die Einheitlichkeit und schützt vor künftig hinzugefügten, nicht abgefangenen Exceptions).

### PluginSelectionServiceTests_IdePlugin.cs (`PluginSelectionServiceTests_IdePlugin`)

- **Testqualität** — `CreateSut(IPluginManager pluginManager, AppEinstellungService appEinstellungService)` (Zeile 114–119) erzeugt für `PluginDefaultSettingsService` über `CreateDb()` eine eigene, neue In-Memory-Datenbank, die von der über den Parameter `appEinstellungService` übergebenen (in den Testmethoden separat erzeugten) Datenbank völlig unabhängig ist. In der Produktion teilen sich `PluginDefaultSettingsService`, `PluginActivationService` und `AppEinstellungService` innerhalb eines `PluginSelectionService` stets denselben `DbContext` (Scoped Lifetime). Aktuell wirkt sich das nicht aus, da `ResolveIdePluginAsync` `_defaultSettingsService` nicht verwendet — bei künftiger Testerweiterung (z. B. Tests, die sowohl IDE- als auch SCM/KI-Plugin-Standardwerte in derselben Instanz prüfen) führt die DB-Trennung aber zu stillschweigend falschen Testergebnissen, da gespeicherte Defaults dann in der falschen Datenbank landen bzw. gesucht werden.

  Empfehlung: `CreateSut` so anpassen, dass `PluginDefaultSettingsService` denselben `DbContext` verwendet wie der übergebene `appEinstellungService` (z. B. indem `CreateSut` selbst den `DbContext` erzeugt und daraus alle drei Abhängigkeiten konstruiert, statt eine unabhängige zweite Datenbank anzulegen).

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
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
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/IdePluginCompatibility.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IdeEntryPoint.cs`
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

Nicht im Detail geprüft (reine Dokumentations-/Planungsartefakte ohne Code-Charakter, außerhalb des Geltungsbereichs der oben genannten Kriterien): `README.md`, `changes.log`, sowie sämtliche Dateien unter `docs/features/task/issue-204-eab6dccb4b304e1a94ef7c2417f9b297-ide-plugins/` und `docs/help/`.
