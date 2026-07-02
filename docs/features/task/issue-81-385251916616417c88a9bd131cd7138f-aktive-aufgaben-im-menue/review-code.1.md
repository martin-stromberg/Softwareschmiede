# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### DashboardView.xaml / MainWindow.xaml (Aktive-Aufgaben-Karte)

- **Doppelter Code** — Das `DataTemplate` für die Anzeige einer aktiven Aufgabe (Border mit CornerRadius, Grid mit zwei Spalten, StackPanel mit Titel-TextBlock + `KiAusfuehrungsStatusConverter`-TextBlock, Pfeil-Button mit `NavigateZuAufgabeCommand`/`CommandParameter={Binding Id}`) ist in beiden Views nahezu identisch dupliziert — einziger Unterschied sind Font-/Padding-/Margin-Werte.

  Empfehlung: Das `DataTemplate` als benanntes Resource (z. B. `AktiveAufgabeCardTemplate`) in eine gemeinsame ResourceDictionary (z. B. `App.xaml` oder eine eigene `Templates.xaml`) auslagern und in beiden Views per `{StaticResource}` referenzieren. Größenunterschiede (Dashboard vs. Seitenleiste) können über einen zusätzlichen Style-Parameter oder zwei kleine Wrapper-Styles abgebildet werden, ohne das Template selbst zu duplizieren.

### DashboardViewModel.cs / MainWindowViewModel.cs (Befüllung der Aufgaben-Collections)

- **Doppelter Code** — `DashboardViewModel.LadenAsync` (Zeilen 111–115) und `MainWindowViewModel.AktiveAufgabenAktualisierenAsync` (Zeilen 128–131) implementieren exakt dasselbe Muster: `Collection.Clear()`, dann `await _aufgabeService.GetAktiveAufgabenAsync(ct)`, dann per `foreach` in die `ObservableCollection<Aufgabe>` einfügen.

  Empfehlung: Eine gemeinsame Hilfsmethode extrahieren, z. B. eine Extension-Methode `ObservableCollection<T>.ReplaceAll(IEnumerable<T> items)` (etwa in `Softwareschmiede.App/Extensions`) und in beiden ViewModels verwenden statt die Clear/Foreach-Logik zweimal auszuschreiben.

- **Namenskonvention** — Für dieselbe fachliche Menge (aktive Aufgaben mit Status Gestartet/Wartend) werden zwei unterschiedliche Property-Namen verwendet: `DashboardViewModel.AktiveAufgabenListe` vs. `MainWindowViewModel.AktiveAufgaben`.

  Empfehlung: Einheitlichen Namen wählen (z. B. beide `AktiveAufgaben` oder beide `AktiveAufgabenListe`), damit dasselbe Konzept im gesamten Feature konsistent benannt ist.

### AppConverters.cs (KiAusfuehrungsStatusConverter)

- **Hardcodierter Wert / dupliziertes Domänenwissen** — `KiAusfuehrungsStatusConverter.Convert` (Zeile 99) verwendet `TimeSpan.FromMinutes(5)` als Schwellwert dafür, ob ein Heartbeat noch "frisch" ist. Genau dieselbe fachliche Schwelle existiert bereits als benannte Konstante `AufgabeRecoveryService.HeartbeatTimeoutMinutes = 5` (`src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`, Zeile 18) und wird dort für die Recovery-Kandidaten-Erkennung verwendet. Durch die Duplikation können beide Schwellwerte künftig unbemerkt auseinanderlaufen (z. B. wenn nur einer der beiden Werte angepasst wird), obwohl sie fachlich dasselbe "Heartbeat gilt als abgelaufen"-Konzept abbilden.

  Empfehlung: Die Schwelle in eine gemeinsame, öffentlich zugängliche Konstante auslagern (z. B. auf der `Aufgabe`-Entity, einer `AufgabeKonstanten`-Klasse oder direkt auf `AufgabeRecoveryService` als `public const`) und sowohl in `AufgabeRecoveryService` als auch in `KiAusfuehrungsStatusConverter` referenzieren.

### MainWindowViewModel.cs (NavigateZuAufgabeAsync)

- **Toter Code / ungenutzter Parameter** — `private Task NavigateZuAufgabeAsync(Guid aufgabeId, CancellationToken ct)` (Zeile 143) nimmt einen `CancellationToken ct` entgegen, verwendet ihn aber nirgends im Methodenkörper. Die Methode ist vollständig synchron und gibt `Task.CompletedTask` zurück.

  Empfehlung: Den ungenutzten `ct`-Parameter entfernen, sofern die Methode dauerhaft synchron bleibt. Falls künftig asynchrone Arbeit (z. B. Laden der Aufgabe aus der DB) ergänzt werden soll, den Parameter beibehalten und dann tatsächlich durchreichen.

### MainWindowViewModel.cs (AktiveAufgabenAktualisierenAsync)

- **Fehlerbehandlung ohne Kontext** — Der `catch (Exception)`-Block in `AktiveAufgabenAktualisierenAsync` (Zeilen 137–140) schluckt jede Ausnahme kommentarlos weiter (nur ein Kommentar, keine Log-Ausgabe). Im Gegensatz dazu loggt die strukturell identische Fehlerbehandlung in `DashboardViewModel.LadenAsync` (Zeile 123) den Fehler über `_logger.LogError(ex, ...)`. `MainWindowViewModel` besitzt aktuell keinen Logger, wodurch Fehler beim Nachladen der Seitenleisten-Aufgaben komplett unsichtbar bleiben und sich nicht diagnostizieren lassen.

  Empfehlung: `ILogger<MainWindowViewModel>` in den Konstruktor injizieren und die Exception im catch-Block mindestens als Warnung loggen (analog zu `DashboardViewModel.LadenAsync`), bevor der Fehler weiterhin still im UI verborgen bleibt.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs` (neu, noch nicht committet)
- `src/Softwareschmiede.Tests/App/ViewModels/DashboardViewModelTests.cs` (neu, noch nicht committet)
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs` (neu, noch nicht committet)
