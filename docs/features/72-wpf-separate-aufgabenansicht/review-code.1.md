# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### AufgabenFilterTyp.cs (Softwareschmiede.Domain.Enums.AufgabenFilterTyp)

- **Doppelter Code / Toter Code** — `src/Softwareschmiede.Domain/Enums/AufgabenFilterTyp.cs` und `src/Softwareschmiede/Domain/Enums/AufgabenFilterTyp.cs` sind byte-identisch (gleicher Namespace `Softwareschmiede.Domain.Enums`, gleicher Inhalt). Es existiert kein eigenständiges `Softwareschmiede.Domain`-Projekt (kein `.csproj` in diesem Ordner); `Softwareschmiede.App.csproj` referenziert nur `Softwareschmiede.csproj`. Die Datei unter `src/Softwareschmiede.Domain/Enums/` liegt damit in keinem kompilierten Projekt und ist eine verirrte, redundante Kopie.

  Empfehlung: Datei `src/Softwareschmiede.Domain/Enums/AufgabenFilterTyp.cs` entfernen; nur die Version unter `src/Softwareschmiede/Domain/Enums/` behalten.

### ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Fehlende Kapselung / Toter Code** — Die Property `AufgabenFilter` (ca. Zeile 112-116) wird über RadioButtons in `ProjectDetailView.xaml` (Zeile 108-119) gebunden, aber `LadenAsync` befüllt `Aufgaben` ungefiltert; es existiert keine gefilterte Projektion analog zu `TaskListViewModel.AktualisierteGefilterteAufgaben()` (siehe `TaskListViewModel.cs`, Zeile 129-137). Der Filter hat dadurch keinen funktionalen Effekt.

  Empfehlung: Gefilterte Collection einführen (analog `TaskListViewModel`) und die Aufgabenliste in `ProjectDetailView.xaml` daran binden, oder das UI-Element entfernen, falls das Feature nicht mehr benötigt wird.

- **Kopplung und Erweiterbarkeit / Lifecycle-Bug** — `ProjectDetailViewModel` implementiert `IDisposable` (siehe Dispose-Logik gegen Ende der Klasse). In `ProjectListViewModel` (siehe dort) wird beim Wechsel auf die Aufgabendetailansicht dasselbe `DetailViewModel`-Property verwendet wie für die Projektdetailansicht, wodurch das `ProjectDetailViewModel` automatisch disposed wird, sobald eine Aufgabe geöffnet wird.

  Empfehlung: Siehe Befund zu `ProjectListViewModel.cs` unten — Trennung der Slots für Projekt- und Aufgaben-Detail-ViewModel.

### ProjectListViewModel.cs (ProjectListViewModel)

- **Kopplung und Erweiterbarkeit / Temporäres Feld** — Der Setter von `DetailViewModel` (ca. Zeile 42-51) disposed automatisch das vorherige ViewModel (`if (old is IDisposable d) d.Dispose();`). `ZeigeTaskDetailView` (ca. Zeile 205-208) setzt `DetailViewModel = vm` (TaskDetailViewModel) und disposed dabei implizit das aktuell angezeigte `ProjectDetailViewModel`. `_currentProjectDetailViewModel` hält danach eine Referenz auf ein bereits disposed Objekt. `KehreZuProjectZurueck` (ca. Zeile 210-213) setzt `DetailViewModel` anschließend wieder auf dieses disposed `ProjectDetailViewModel` zurück, statt es neu zu laden — es wird ein bereits disposed ViewModel weiterverwendet, dessen interne Operationen (z. B. `RepositoryZuweisenAsync`) durch den internen `disposed`-Flag-Check vorzeitig abbrechen.

  Empfehlung: Separates Feld/Property für das Task-Detail-ViewModel anstatt desselben `DetailViewModel`-Slots verwenden, sodass das Wechseln zur Aufgabenansicht das `ProjectDetailViewModel` nicht disposed; alternativ beim Zurücknavigieren das `ProjectDetailViewModel` neu vom Service Provider laden statt die alte (disposed) Instanz wiederzuverwenden.

### RibbonLargeButton.xaml.cs / RibbonSmallButton.xaml.cs (RibbonLargeButton, RibbonSmallButton)

- **Doppelter Code** — Beide Klassen registrieren identische vier DependencyProperties (`ButtonCommand`, `ButtonIcon`, `ButtonText`, `AutomationName`) mit identischen Wrapper-Properties; einziger Unterschied ist das visuelle Layout in der zugehörigen XAML-Datei.

  Empfehlung: Gemeinsame Basisklasse (z. B. `RibbonButtonBase`) einführen, die die vier DependencyProperties einmal definiert; `RibbonLargeButton` und `RibbonSmallButton` davon ableiten lassen.

### ProjectDetailView.xaml.cs / TaskListView.xaml.cs (ProjectDetailView, TaskListView)

- **Doppelter Code** — Die Doppelklick-Handler (`ProjectDetailView.xaml.cs` ca. Zeile 18-25, `TaskListView.xaml.cs` ca. Zeile 22-29) prüfen strukturell identisch `sender is <ListItemType> { DataContext: Aufgabe aufgabe }` und `DataContext is <ViewModelType> vm` und rufen jeweils `vm.AufgabeOeffnenCommand.Execute(aufgabe.Id)` auf — nur die Typnamen unterscheiden sich.

  Empfehlung: Gemeinsames Attached Behavior oder Hilfsmethode für "Doppelklick auf Listenelement führt Command mit Item-Id aus" extrahieren und in beiden Views verwenden.

### PluginSettingsView.xaml.cs / SettingsView.xaml.cs (PluginSettingsView, SettingsView)

- **Doppelter Code** — Die Methoden `OnPasswordBoxLoaded`, `OnPasswordChanged` und `OnDateiAuswaehlenClick` (`PluginSettingsView.xaml.cs` ca. Zeile 23-51, `SettingsView.xaml.cs` ca. Zeile 41-69) sind wortwörtlich identisch, inklusive `OpenFileDialog`-Konfiguration und Filterlogik.

  Empfehlung: Gemeinsame statische Helper-Klasse oder Attached Behavior extrahieren (z. B. für PasswordBox-Zwei-Wege-Bindung und Dateiauswahl-Dialog), das von beiden Views referenziert wird.

### SettingsViewModel.cs / PluginSettingsViewModel.cs (SettingsViewModel, PluginSettingsViewModel)

- **Doppelter Code** — `SettingsViewModel.LadePluginEinstellungen` (ca. Zeile 269-279) und `SettingsViewModel.SpeicherePluginEinstellungen` (ca. Zeile 281-293) bauen `PluginSettingGroupEntry`-Listen mit derselben verschachtelten Select/Foreach-Struktur auf und persistieren sie über `_pluginSettingsService.SetValue(...)` wie `PluginSettingsViewModel.LadenAsync` (ca. Zeile 163-177) und `SpeichernAsync` (ca. Zeile 211-220).

  Empfehlung: Gemeinsame Mapping-/Persistenzlogik in `PluginSettingsService` oder eine neue Hilfsklasse (z. B. `PluginSettingsMapper`) auslagern, die von beiden ViewModels genutzt wird.

### StatusIndicatorControl.xaml(.cs) / RecoveryBannerControl.xaml(.cs) / DashboardView.xaml (StatusIndicatorControl, RecoveryBannerControl, DashboardView)

- **Toter Code / Doppelter Code** — `StatusIndicatorControl` und `RecoveryBannerControl` werden in keiner View per `<controls:...>`-Tag eingebunden (keine Treffer in den XAML-Dateien des Branches). `DashboardView.xaml` dupliziert stattdessen die Recovery-Banner-Anzeige inline (ca. Zeile 58-69, gleicher Text "Aufgabe(n) benötigen Wiederherstellung."), anstatt das vorhandene `RecoveryBannerControl` zu verwenden.

  Empfehlung: `RecoveryBannerControl` in `DashboardView.xaml` tatsächlich einsetzen und die inline duplizierte Logik entfernen. `StatusIndicatorControl` entweder in `TaskDetailView.xaml`/`ProjectDetailView.xaml` statt reinem `TextBlock` verwenden oder das ungenutzte Control entfernen.

### TaskListViewModel.cs / TaskListView.xaml(.cs) (TaskListViewModel, TaskListView)

- **Toter Code** — `TaskListViewModel` ist in `App.xaml.cs` als `AddTransient<TaskListViewModel>()` registriert, wird aber von keinem anderen ViewModel via `GetRequiredService` angefordert. `MainWindow.xaml` enthält kein `DataTemplate` für `TaskListViewModel`, sodass die View nie angezeigt werden kann (verifiziert: keine Treffer für `TaskListViewModel` in `MainWindow.xaml`). Die Aufgabenliste wird stattdessen direkt in `ProjectDetailView`/`ProjectDetailViewModel` gerendert.

  Empfehlung: `TaskListViewModel`, `TaskListView.xaml(.cs)` und die zugehörige DI-Registrierung entfernen, falls die direkte Integration in `ProjectDetailView` final ist — oder die Navigation dorthin tatsächlich verdrahten, falls eine separate Listenansicht weiterhin geplant ist.

### PluginSettingsViewModel.cs / PluginSettingsView.xaml(.cs) (PluginSettingsViewModel, PluginSettingsView)

- **Toter Code** — Analog zu `TaskListViewModel`: in DI registriert (`App.xaml.cs`), aber kein `DataTemplate` in `MainWindow.xaml` und kein navigierender Aufrufer (verifiziert: keine Treffer für `PluginSettingsViewModel` in `MainWindow.xaml`).

  Empfehlung: Entfernen oder Navigation dazu ergänzen (z. B. via `MainWindowViewModel`/`SettingsViewModel`).

### NavigationViewModel.cs (NavigationViewModel)

- **Toter Code / Doppelter Code** — In DI registriert, aber von keiner View/ViewModel referenziert (außer der Registrierung selbst). Funktional überschneidet sich die Klasse mit `MainWindowViewModel.IsNavigationExpanded`/`ToggleNavigationCommand`, die tatsächlich verwendet werden (gleicher Zweck: Navigation einklappen/ausklappen), nur mit anderen Property-Namen (`IsExpanded`/`NavigationWidth` vs. `IsNavigationExpanded`).

  Empfehlung: `NavigationViewModel` entfernen, falls `MainWindowViewModel` die Funktion vollständig übernimmt; andernfalls die doppelte Logik konsolidieren.

### AppConverters.cs (AufgabeStatusToVisibilityConverter)

- **Toter Code** — Der Converter (ca. Zeile 71-93) ist als `StaticResource` in `App.xaml` (Zeile 18) registriert, wird aber in keiner untersuchten XAML-Datei tatsächlich als `{StaticResource AufgabeStatusToVisibilityConverter}` referenziert; die Content-Switching-Logik in `TaskDetailView.xaml` nutzt stattdessen die ViewModel-Properties `ShowEditPanel`/`ShowCliPanel`/`ShowDiffPanel`.

  Empfehlung: Converter entfernen, falls dauerhaft ungenutzt, oder konsequent für das statusabhängige Content-Switching einsetzen statt der parallelen VM-Properties (Konsistenzentscheidung erforderlich).

### WpfTestBase.cs (WpfTestBase)

- **Fehlerbehandlung** — `Dispose()` (ca. Zeile 78-87) enthält drei leere `catch {}`-Blöcke ohne Logging oder Kommentar (`try { _application?.Close(); } catch { }`, `try { _application?.WaitWhileMainHandleIsMissing(...); } catch { }`, `try { DeleteTestDatabase(); } catch { }`). Fehler beim Testaufräumen werden komplett verschluckt, was das Debuggen flakiger E2E-Tests erschwert.

  Empfehlung: Mindestens ein Logging-Statement (z. B. `Debug.WriteLine`/Test-Output) im catch-Block ergänzen oder spezifischere Exception-Typen abfangen, damit unerwartete Fehler nicht unbemerkt bleiben.

### TaskDetailView.xaml.cs (TaskDetailView)

- **Kopplung und Erweiterbarkeit / Hardcodierte Werte** — `WaitForWindowHandleAsync` (ca. Zeile 45-78) verwendet `AddSeconds(15)` als Deadline und `Task.Delay(200, ct)` als Poll-Intervall als unbenannte Literale im Methodenkörper. Vergleichbare Werte tauchen auch in `WpfTestBase.WaitForElement` (`Thread.Sleep(200)`) auf.

  Empfehlung: Benannte `private const`-Felder einführen (z. B. `WindowHandlePollTimeout`, `WindowHandlePollInterval`), um die Werte zentral wartbar zu machen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Controls/ProcessWindowHost.cs`
- `src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml`
- `src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonGroup.xaml`
- `src/Softwareschmiede.App/Controls/RibbonGroup.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml.cs`
- `src/Softwareschmiede.App/Controls/StatusIndicatorControl.xaml`
- `src/Softwareschmiede.App/Controls/StatusIndicatorControl.xaml.cs`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Services/DarkModeService.cs`
- `src/Softwareschmiede.App/Services/IDialogService.cs`
- `src/Softwareschmiede.App/Services/WpfAudioService.cs`
- `src/Softwareschmiede.App/Services/WpfBannerService.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/Softwareschmiede.App.csproj`
- `src/Softwareschmiede.App/Themes/DarkTheme.xaml`
- `src/Softwareschmiede.App/Themes/LightTheme.xaml`
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/NavigationViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/Views/DashboardView.xaml.cs`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs`
- `src/Softwareschmiede.App/Views/PluginSettingsView.xaml`
- `src/Softwareschmiede.App/Views/PluginSettingsView.xaml.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/ProjectListView.xaml`
- `src/Softwareschmiede.App/Views/ProjectListView.xaml.cs`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskListView.xaml`
- `src/Softwareschmiede.App/Views/TaskListView.xaml.cs`
- `src/Softwareschmiede.App/app.manifest`
- `src/Softwareschmiede.Domain/Enums/AufgabenFilterTyp.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabenFilterTyp.cs`
- `src/Softwareschmiede/Domain/Enums/InvalidStatusTransitionException.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/RepositoryAssignViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfE2EPlaceholderTests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
