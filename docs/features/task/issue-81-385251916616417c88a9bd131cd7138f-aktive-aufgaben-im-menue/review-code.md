# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### AufgabeService.cs / AufgabeRecoveryService.cs (AufgabeService, AufgabeRecoveryService)

- **Doppelter Code** — Die fachliche Regel „Aufgabe ist aktiv oder wartend" (Status `Gestartet` oder `Wartend`) ist jetzt an drei Stellen unabhängig voneinander implementiert:
  1. `AufgabeService.IstAktivOderWartendPredicate` (neu, `src/Softwareschmiede/Application/Services/AufgabeService.cs:14-15`)
  2. Inline-Check in `AufgabeService.DeleteAsync` (`src/Softwareschmiede/Application/Services/AufgabeService.cs:282`: `if (aufgabe.Status is AufgabeStatus.Gestartet or AufgabeStatus.Wartend)`)
  3. `AufgabeRecoveryService.IstRecoveryStatus` (`src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs:202-203`: `status is AufgabeStatus.Gestartet or AufgabeStatus.Wartend`)

  Die neue Änderung hat zwar die beiden LINQ-Nutzungen innerhalb von `AufgabeService` konsolidiert (`GetAktiveUndWartendeCountAsync` und `GetAktiveAufgabenAsync` teilen sich jetzt `IstAktivOderWartendPredicate`), aber die Gelegenheit wurde nicht genutzt, die bereits vorhandene `AufgabeRecoveryService.IstRecoveryStatus`-Methode und den Inline-Check in `DeleteAsync` mit einzubeziehen. Bei einer künftigen Änderung des Statusmodells müssten drei Stellen synchron angepasst werden.

  Empfehlung: Eine einzige Quelle der Wahrheit einführen, z. B. eine statische Methode `AufgabeStatus.IstAktivOderWartend(AufgabeStatus status)` (oder eine Extension-Method auf `AufgabeStatus`), die von `DeleteAsync` und `AufgabeRecoveryService.IstRecoveryStatus` direkt aufgerufen wird. Für die EF-Query (`IstAktivOderWartendPredicate`) auf dieselbe Methode referenzieren, sofern EF Core den Aufruf übersetzen kann, oder zumindest im Kommentar auf die gemeinsame Definition verweisen, damit alle drei Stellen erkennbar zusammengehören.

### MainWindowViewModel.cs (MainWindowViewModel)

- **Fehlerbehandlung** — `AktiveAufgabenAktualisierenAsync` (Zeilen 137-148) fängt `catch (Exception ex)` pauschal ab und loggt nur eine Warnung, ohne `OperationCanceledException` auszunehmen. Das ist inkonsistent zum Schwester-Pattern in `DashboardViewModel.LadenAsync` (`src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs:124-132`), das `OperationCanceledException` explizit weiterwirft, bevor der allgemeine `catch (Exception)`-Block greift. Da `AktiveAufgabenAktualisierenAsync` ein `CancellationToken` entgegennimmt, wird eine reguläre Abbruchanforderung derzeit stillschweigend als „Fehler beim Aktualisieren" geloggt statt korrekt als Abbruch propagiert.

  Empfehlung: Vor dem allgemeinen `catch (Exception ex)`-Block einen `catch (OperationCanceledException) { throw; }`-Block ergänzen, analog zu `DashboardViewModel.LadenAsync`.

- **Kopplung und Erweiterbarkeit** — `NavigateToDashboard` (Zeilen 96-106) konfiguriert das frisch erzeugte `DashboardViewModel` durch direktes Setzen von zwei öffentlichen, extern beschreibbaren Zustands-Properties (`_dashboardViewModel.AktiveAufgabenListe = AktiveAufgabenListe;` und `_dashboardViewModel.NavigateZuAufgabeAction = NavigateZuAufgabe;`). Das macht `DashboardViewModel.AktiveAufgabenListe` (Setter in `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs:69-73`) und `NavigateZuAufgabeAction` (Zeile 76) von außen frei überschreibbar und abhängig von der Aufrufreihenfolge in `MainWindowViewModel` — jeder andere Konsument könnte versehentlich eine andere Collection oder Action zuweisen und den Shared-State brechen. Die Kopplung zwischen `MainWindowViewModel` und der konkreten Klasse `DashboardViewModel` ist damit enger als nötig.

  Empfehlung: Die Verdrahtung über eine dedizierte Initialisierungsmethode kapseln (z. B. `_dashboardViewModel.Initialize(AktiveAufgabenListe, NavigateZuAufgabe)`), die beide Werte atomar setzt und nicht mehr einzeln von außen überschreibbar macht, oder die gemeinsame Datenquelle über den Konstruktor von `DashboardViewModel` injizieren statt per Property-Zuweisung nach der Erzeugung.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Extensions/ObservableCollectionExtensions.cs`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/DashboardViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `.gitignore`
- `README.md`
