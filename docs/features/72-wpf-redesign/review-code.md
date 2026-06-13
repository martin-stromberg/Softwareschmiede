# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Toter Code / Fehlende Kapselung** — `AktualisierenCallbackAusfuehrenAsync()` (Zeile 277–280) ist eine Ein-Zeiler-Methode, die ausschließlich `ProjektListeAktualisierenCallback?.Invoke()` kapselt. Der Methodenname verschleiert, dass sie nichts weiter tut als einen Null-Check vor dem Callback-Aufruf — das ist kein eigenes Konzept, sondern eine unnötige Indirektion.

  Empfehlung: Inline-Aufruf `await ProjektListeAktualisierenCallback?.Invoke()!` an den drei Aufrufstellen (oder Pattern `if (ProjektListeAktualisierenCallback is {} cb) await cb()`) und `AktualisierenCallbackAusfuehrenAsync` entfernen.

- **Inappropriate Intimacy / Kopplung** — `LoeschenBestaetigenFunc` und `RepositoryDialogOeffnenFunc` sind öffentliche `Func`-Properties auf dem ViewModel (Zeilen 161–176). Damit kennt und steuert der Aufrufer direkt UI-Interaktionen aus dem ViewModel heraus. Das verletzt MVVM: Dialog-Logik gehört nicht als inject­ierbarer Func in das ViewModel, sondern in einen dedizierten `IDialogService` (Interface).

  Empfehlung: `IDialogService`-Interface mit Methoden `bool BestaetigenDialog(string nachricht)` und `bool RepositoryZuweisenDialog(RepositoryAssignViewModel vm)` einführen. Dieses Interface per Konstruktor injizieren; im Produktivcode durch WPF-Implementierung, in Tests durch Mock ersetzen. Die öffentlichen `Func`-Properties entfallen.

- **Temporäres Feld** — `_disposed` (Zeile 39) wird als Instanzfeld verwendet, um in `RepositoryZuweisenAsync` (Zeilen 347, 359) Folgeoperationen nach einem abgeschlossenen Dialog zu verhindern. Der Check `if (_disposed) return;` mitten im Async-Ablauf deutet darauf hin, dass der CancellationToken (`ct`) für diesen Zweck nicht genutzt wird, obwohl er vorhanden ist.

  Empfehlung: Statt des `_disposed`-Checks mitten in `RepositoryZuweisenAsync` den bereits vorhandenen `_ladenCts`-Token konsistent nutzen. `ct.IsCancellationRequested` nach dem Dialog-Aufruf prüfen, oder alternativ den Dialog erst gar nicht weiterlaufen lassen, wenn bereits disposed wurde — dann genügt der Standard-`ObjectDisposedException`-Schutz in `Dispose()`.

- **Fehlende Kapselung (doppelter Code)** — `FehlerMeldung = $"Fehler: {ex.Message}"` kommt in `ProjectListViewModel` (Zeilen 129, 155, 191) dreimal vor, während `ProjectDetailViewModel` denselben String per `SetFehler(ex)` (Zeile 398) setzt, also eine Hilfsmethode hat — aber nur in der Detail-Klasse. Die List-Klasse hat keine entsprechende Methode.

  Empfehlung: `SetFehler(Exception ex)` in `ViewModelBase` hochziehen oder eine gemeinsame Basisklasse für Projekt-ViewModels einführen, damit beide Klassen dieselbe Methode nutzen.

### ProjectListViewModel.cs (ProjectListViewModel)

- **Doppelter Code** — `FehlerMeldung = $"Fehler: {ex.Message}"` wird an drei Stellen (Zeilen 129, 155, 191) direkt inline gesetzt, statt über eine Hilfsmethode — im Gegensatz zu `ProjectDetailViewModel`, das eine `SetFehler`-Methode besitzt. (Siehe auch Befund oben.)

  Empfehlung: Hilfsmethode `SetFehler(Exception ex)` in `ViewModelBase` oder einer gemeinsamen Basis einführen.

- **Middle Man** — `LadenProjekteInternAsync` (Zeilen 104–110) hat keinen eigenen Mehrwert gegenüber einem direkten Aufruf von `_projektService.GetAllAsync`. Sie existiert nur, um die `ObservableCollection` zu befüllen — das ist jedoch genau das, was `LadenAsync` ebenfalls tun würde. Der Aufruf in `NeuesProjektHinzufuegen` (Zeile 186) umgeht die `IsLoading`/`FehlerMeldung`-Behandlung aus `LadenAsync`, was zu inkonsistenter UI-Zustandsverwaltung führt.

  Empfehlung: `LadenProjekteInternAsync` entfernen. In `NeuesProjektHinzufuegen` direkt `LadenAsync` aufrufen (ggf. mit einem neuen `CancellationToken.None`), damit `IsLoading` und `FehlerMeldung` einheitlich gesetzt werden.

### WpfTestBase.cs (WpfTestBase)

- **Fehlerbehandlung ohne aussagekräftigen Kontext** — In `Dispose()` (Zeilen 76–88) werden Fehler beim Schließen der Anwendung und beim Warten auf das Prozessende mit `Console.WriteLine` ausgegeben. In einem xUnit-Testkontext wird `Console.WriteLine` oft nicht in der Testausgabe angezeigt und ist nicht mit dem konkreten Test verknüpft.

  Empfehlung: Exceptions im `Dispose` entweder mit `ITestOutputHelper` (xUnit) ausgeben oder still ignorieren (`catch { }`) — je nach gewünschtem Verhalten. Aktuell täuscht `Console.WriteLine` Transparenz vor, die im Testsystem nicht vorhanden ist.

- **Hardcodierter Pfad** — `ResolveAppExePath()` (Zeilen 133–163) enthält die hartkodierten Build-Konfigurationen `"Debug"` und `"Release"` sowie das Framework-Moniker `"net10.0-windows10.0.17763.0"` als Stringliterale.

  Empfehlung: Framework-Moniker und Build-Konfiguration als Konstanten (z. B. `private const string FrameworkMoniker = "net10.0-windows10.0.17763.0"`) auslagern, damit Änderungen zentral und einmalig vorgenommen werden müssen.

### ProjectDetailE2ETests.cs (ProjectDetailE2ETests)

- **Toter Code** — `Thread.Sleep`-Aufrufe ohne Kommentar (z. B. Zeile 64: `Thread.Sleep(500)` in `OpenProject`, Zeile 94, 99, etc.) sind Smell für busy-waiting. `WaitForElement` mit Polling ist bereits vorhanden — die Sleep-Aufrufe sind in vielen Fällen Doppelarbeit oder verbergen ein fehlendes `WaitForElement`.

  Empfehlung: Alle `Thread.Sleep`-Aufrufe ohne zwingend notwendigen Grund durch `WaitForElement` mit passendem Condition ersetzen. Ausnahmen (z. B. Warten auf Animation) mit Kommentar kennzeichnen.

- **Doppelter Code** — Die Hilfsmethoden `NavigateToProjecten`, `CreateProject`, `OpenProject` und `CreateAndOpenProject` duplizieren nahezu identische Schritte aus `WpfE2EPlaceholderTests.cs` (Klasse `WpfE2ETests`). In `WpfE2ETests` sind dieselben Navigation- und Erstellungsschritte inline in jedem Test wiederholt (z. B. Zeilen 28–38, 51–67, 89–103).

  Empfehlung: Die Hilfsmethoden aus `ProjectDetailE2ETests` in `WpfTestBase` verschieben (oder eine gemeinsame Basisklasse `ProjectE2ETestBase : WpfTestBase` einführen), damit `WpfE2ETests` dieselben Methoden nutzen kann und die Duplizierung entfällt.

### WpfE2EPlaceholderTests.cs (WpfE2ETests)

- **Doppelter Code** — Jeder der sieben Tests wiederholt den Block `LaunchApp() → GetMainWindow() → WaitForElement(projekteButton)`. Die identischen Setup-Schritte sind nicht in eine Hilfsmethode extrahiert.

  Empfehlung: Gemeinsame Hilfsmethode (idealerweise in `WpfTestBase`) für `LaunchApp` + `GetMainWindow` + optional Navigation erstellen; in jeden Test nur den spezifischen Teil belassen.

### ViewModelBase.cs (AsyncRelayCommand)

- **Fehlende Fehlerbehandlung bei `OnError = null`** — `AsyncRelayCommand` (Zeilen 98–164) schluckt Exceptions still, wenn `OnError` nicht gesetzt ist (Zeile 151: `OnError?.Invoke(ex)`). Ist `OnError` null, werden Fehler lautlos ignoriert.

  Empfehlung: Falls kein `OnError` gesetzt ist, Exception an den WPF-`Dispatcher.UnhandledException`-Handler weiterleiten oder zumindest per `Debug.WriteLine`/`Trace.TraceError` protokollieren, damit Fehler im Entwicklungs-Build sichtbar sind.

## Geprüfte Dateien

- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfE2EPlaceholderTests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
