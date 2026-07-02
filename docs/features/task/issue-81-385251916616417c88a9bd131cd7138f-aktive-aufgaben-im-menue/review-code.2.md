# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MainWindowViewModel.cs (MainWindowViewModel)

- **Toter Code / Redundante Ausführung** — Im Konstruktor wird `NavigateToDashboard()` aufgerufen (Zeile 87), das selbst bereits `_ = AktiveAufgabenAktualisierenAsync();` auslöst (Zeile 98). Direkt danach ruft der Konstruktor `_ = AktiveAufgabenAktualisierenAsync();` (Zeile 88) ein zweites Mal auf. Beim Start der App wird die aktive-Aufgaben-Liste dadurch unnötig zweimal aus der Datenbank geladen.

  Empfehlung: Den expliziten Aufruf `_ = AktiveAufgabenAktualisierenAsync();` in Zeile 88 entfernen, da `NavigateToDashboard()` den Refresh bereits auslöst.

- **Doppelter Code** — Der Aufruf `_ = AktiveAufgabenAktualisierenAsync();` ist identisch in `NavigateToDashboard()` (Zeile 98), `NavigateToProjectList()` (Zeile 117) und `NavigateToSettings()` (Zeile 127) enthalten – jeweils als letzte Zeile nach dem Setzen von `CurrentView`/`Title`.

  Empfehlung: Den Refresh-Aufruf zentral im `CurrentView`-Setter auslösen, z. B. über den bereits vorhandenen `onChanged`-Callback-Parameter von `SetProperty` (wird dort schon für `OnPropertyChanged(nameof(IsDashboardVisible))` genutzt, Zeile 35): `SetProperty(ref _currentView, value, () => { OnPropertyChanged(nameof(IsDashboardVisible)); _ = AktiveAufgabenAktualisierenAsync(); });`. Damit entfällt die Wiederholung in allen drei Navigate-Methoden und der doppelte Aufruf im Konstruktor (siehe vorheriger Befund) verschwindet automatisch mit.

- **Fehlerbehandlung** — `AktiveAufgabenAktualisierenAsync` (Zeile 132-147) wird an allen vier Aufrufstellen "fire-and-forget" ausgeführt (`_ = AktiveAufgabenAktualisierenAsync();`, Zeilen 88, 98, 117, 127) mit dem Default-`CancellationToken`. Innerhalb der Methode wird `OperationCanceledException` explizit abgefangen und erneut geworfen (Zeile 139-142). Da kein Aufrufer den zurückgegebenen `Task` beobachtet, ist dieser Re-Throw in der Praxis toter Code (der Default-Token wird nie abgebrochen) bzw. würde bei künftiger Verwendung mit einem echten Token zu einer unbeobachteten Task-Exception führen.

  Empfehlung: Entweder den `CancellationToken`-Parameter an den vier Aufrufstellen entfernen/nicht anbieten, solange die Methode wirklich nur fire-and-forget verwendet wird, oder den zurückgegebenen Task an den Aufrufstellen mit einer Fehlerbehandlung verbinden (z. B. `.ContinueWith(...)` mit Logging), damit ein zukünftiger Abbruch nicht zu einer unbeobachteten Exception führt.

### App.xaml (AktiveAufgabeCardTemplate)

- **Kopplung und Erweiterbarkeit / Einheitlichkeit** — Das neue, global in `App.xaml` definierte `DataTemplate x:Key="AktiveAufgabeCardTemplate"` bindet den Button-Command über `Command="{Binding DataContext.NavigateZuAufgabeCommand, RelativeSource={RelativeSource AncestorType=Window}}"`. Das Template wird sowohl im Sidebar-`ItemsControl` von `MainWindow.xaml` als auch im `ItemsControl` von `DashboardView.xaml` verwendet; in beiden Fällen muss es über den unmittelbaren `DataContext` (in `DashboardView.xaml` z. B. `DashboardViewModel`, das den Command gar nicht besitzt) hinweg bis zum Fenster (`MainWindowViewModel`) durchgreifen. Das weicht vom im restlichen Code etablierten Muster ab, Command-Bindungen in Templates über `RelativeSource={RelativeSource AncestorType=UserControl}` an das jeweils umschließende UserControl zu binden (siehe `RecoveryBannerControl.xaml`, `StatusIndicatorControl.xaml`, `ProjectListView.xaml`). Dadurch entsteht eine versteckte Abhängigkeit: Das wiederverwendbare Template funktioniert nur, weil es zufällig innerhalb eines `Window` liegt, dessen `DataContext` `MainWindowViewModel` ist – eine stillschweigende Voraussetzung, die bei einer WPF-Bindung nicht zur Compile-Zeit geprüft wird und bei falscher Einbettung ohne Fehler, nur mit einer Bindungswarnung, fehlschlägt.

  Empfehlung: Navigation nach dem im Projekt bereits etablierten Delegate-Muster (`DetailTitelAenderungAction` in `MainWindowViewModel.NavigateToProjectList`, `ZurueckAction` in `TaskDetailViewModel`) entkoppeln: z. B. `DashboardViewModel` ebenfalls einen `NavigateZuAufgabeCommand`/eine `Action<Guid>`-Eigenschaft geben lassen, die von `MainWindowViewModel` beim Erzeugen gesetzt wird, und das Template stattdessen an den unmittelbaren `AncestorType=UserControl` binden statt an `AncestorType=Window`.

### MainWindowViewModel.cs / DashboardViewModel.cs (Doppelte Datenabfrage)

- **Doppelter Code / Kopplung** — Sowohl `MainWindowViewModel.AktiveAufgabenAktualisierenAsync` (Zeile 132-147) als auch `DashboardViewModel.LadenAsync` (Zeile 90-129, insbesondere Zeile 113-114) rufen unabhängig voneinander `_aufgabeService.GetAktiveAufgabenAsync(ct)` auf und befüllen je eine eigene `AktiveAufgabenListe`-Collection. Beim Navigieren zum Dashboard wird `NavigateToDashboard()` aufgerufen, das `AktiveAufgabenAktualisierenAsync()` auslöst (für die – dann per `InverseBoolToVisibilityConverter` ausgeblendete – Sidebar-Liste), während gleichzeitig `DashboardView.Loaded` `LadenCommand` ausführt, das dieselbe Abfrage erneut lädt. Die aktiven Aufgaben werden also bei jeder Dashboard-Navigation zweimal aus der Datenbank geladen, obwohl das Ergebnis nur einmal sichtbar ist.

  Empfehlung: Eine einzige Quelle für die aktiven Aufgaben vorsehen, z. B. `DashboardView`/`DashboardViewModel` direkt an die vom `MainWindowViewModel` gepflegte `AktiveAufgabenListe` binden lassen (etwa über eine Referenz oder ein gemeinsames Shared-State-Objekt), anstatt in zwei ViewModels unabhängig dieselbe Abfrage auszuführen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Extensions/ObservableCollectionExtensions.cs`
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/DashboardViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
