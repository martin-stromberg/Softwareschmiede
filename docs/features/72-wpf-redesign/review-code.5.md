# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Temporäres Feld / fehlende Kapselung** — `_disposed` (Zeile 388) ist ein privates Instanzfeld, das nur in `Dispose()` und `RepositoryZuweisenAsync()` geprüft wird. Das Feld liegt am Ende der Klasse, weit von der `Dispose()`-Implementierung entfernt, und bricht die übliche Konvention, `_disposed` direkt vor `Dispose()` zu deklarieren.

  Empfehlung: `_disposed` direkt oberhalb der `Dispose()`-Methode deklarieren (nach den anderen privaten Feldern, z. B. nach `_ladenCts`).

- **Doppelter Code** — `ProjektSpeichernAsync` ruft `ProjektListeAktualisierenCallback` identisch in zwei Branches auf (Zeilen 273–274 und 280–281):
  ```csharp
  if (ProjektListeAktualisierenCallback != null)
      await ProjektListeAktualisierenCallback();
  ```
  Dasselbe Muster wiederholt sich auch in `ProjektLoeschenAsync` (Zeile 307–308).

  Empfehlung: Hilfsmethode `AktualisierenCallbackAusfuehrenAsync()` extrahieren und alle drei Aufruforte ersetzen.

- **Fehlende Kapselung / Feature Envy** — `RepositoryZuweisenAsync` (Zeile 322) instanziiert und öffnet direkt einen WPF-Dialog (`new RepositoryAssignDialog(vm)`), greift dabei auf `System.Windows.Application.Current.MainWindow` zu und enthält damit UI-Code im ViewModel. Das verstößt gegen MVVM und erschwert Unit-Tests.

  Empfehlung: Dialog-Logik hinter einen `Func<RepositoryAssignViewModel, bool>` oder einen `IDialogService` auslagern, ähnlich wie `LoeschenBestaetigenFunc` bereits abstrahiert ist.

- **Hardcodierter Wert** — In `LadenAsync` (Zeile 217) wird mit `Projekt.Repositories.FirstOrDefault()` immer nur das erste Repository verwendet. Die Semantik „ein Projekt hat genau ein aktives Repository" ist implizit; es gibt keine Konstante oder Kommentar, der diesen Entscheid erklärt. Wenn mehrere Repositories in Zukunft unterstützt werden sollen, ist diese Stelle nicht auffindbar.

  Empfehlung: Entweder eine benannte Konstante/Property (`PrimaerRepositoryIndex = 0`) einführen oder die Auswahl durch eine dedizierte Service-Methode delegieren und das Verhalten kommentieren.

---

### ProjectListViewModel.cs (ProjectListViewModel)

- **Doppelter Code** — `ZeigeDetail` (Zeile 154) und `ZeigeDetailErstellungsFormular` (Zeile 163) sind nahezu identisch: Beide holen ein `ProjectDetailViewModel` aus dem DI-Container, setzen `ZurueckAction` und `ProjektListeAktualisierenCallback`, und unterscheiden sich nur im `ProjektId`-Wert. Der Initialisierungsblock (drei Zeilen) ist doppelt vorhanden.

  Empfehlung: Private Hilfsmethode `InitDetailViewModel(ProjectDetailViewModel vm)` extrahieren, die die gemeinsamen Zuweisungen kapselt.

- **Doppelter Code** — `NeuesProjektHinzufuegen` (Zeile 174) lädt die gesamte Projektliste neu (`GetAllAsync()` + `Clear()` + `foreach Add`) — dieselbe Logik wie `LadenAsync` (Zeile 104). Bei jeder Projektänderung existieren zwei separate Implementierungen desselben Ladevorgangs.

  Empfehlung: Den Kern-Ladeblock in eine private Methode `LadenProjekteInternAsync(CancellationToken)` auslagern, die von `LadenAsync` und `NeuesProjektHinzufuegen` aufgerufen wird.

---

### ViewModelBase.cs (AsyncRelayCommand)

- **Fehlende Fehlerbehandlung / unerwartetes Verhalten** — Bei einem unbehandelten Fehler in `Execute` (Zeile 126) wird die Exception über `dispatcher.BeginInvoke` als `InvalidOperationException` auf dem UI-Thread neu geworfen (Zeilen 144–151). Das führt zu einem unbehandelten Exception-Crash des gesamten Prozesses, ohne dass der Nutzer eine verständliche Fehlermeldung sieht. Alle ViewModel-Methoden, die eigene `catch`-Blöcke mit `FehlerMeldung =` haben, werden dadurch doppelt behandelt — erst im ViewModel, dann noch einmal im Command.

  Empfehlung: Den `catch (Exception)`-Block in `AsyncRelayCommand.Execute` entfernen oder durch ein konfigurierbares `Action<Exception>? OnError`-Callback ersetzen, das vom Aufrufer gesetzt werden kann. Alternativ sollte die Dokumentation klarstellen, dass alle `AsyncRelayCommand`-Delegates intern alle Exceptions abfangen müssen.

- **Fehlende CancellationTokenSource-Entsorgung bei Neustart** — Wenn `Execute` aufgerufen wird, wird eine neue `_cts` erstellt (Zeile 131). Wenn `Cancel()` aufgerufen wird und danach sofort erneut `Execute`, wird die vorherige `_cts` nicht disposed bevor sie ersetzt wird. In `finally` wird `_cts?.Dispose()` aufgerufen, aber nur für den erfolgreichen Ausführungsweg.

  Empfehlung: In `Execute`, vor der Zuweisung `_cts = new CancellationTokenSource()`, die vorherige Instanz disposen: `_cts?.Dispose()`.

---

### AppConverters.cs (BoolToWidthConverter)

- **Hardcodierte Werte** — `BoolToWidthConverter` (Zeile 36) kodiert die Breiten `240.0` und `48.0` direkt in der Converter-Implementierung. Diese Werte sind in der XAML nicht konfigurierbar und müssen bei einer Designänderung im C#-Code geändert werden, nicht im XAML.

  Empfehlung: Dependency Properties `ExpandedWidth` (Default: 240) und `CollapsedWidth` (Default: 48) als konfigurierbare Properties des Converters einführen, damit XAML-Aufrufer die Werte übersteuern können.

---

### ProjectDetailView.xaml (ProjectDetailView)

- **Kopplung zwischen View und Code-Behind** — Der `ListBox.ItemContainerStyle` registriert einen `EventSetter` auf `MouseDoubleClick` (Zeile 221) mit dem Handler `AufgabeDoubleClick`. Dieser Event-Handler im Code-Behind koppelt die View direkt an ein konkretes Navigationsverhalten, das besser per Command-Binding im ViewModel gelöst werden sollte.

  Empfehlung: Den `MouseDoubleClick`-Handler durch ein `InputBinding` mit `AufgabeOeffnenCommand` ersetzen (z. B. `<ListBoxItem.InputBindings><MouseBinding MouseAction="LeftDoubleClick" Command="{Binding DataContext.AufgabeOeffnenCommand, RelativeSource=...}" CommandParameter="{Binding Id}" /></ListBoxItem.InputBindings>`).

---

### ProjectDetailViewModelTests.cs (ProjectDetailViewModelTests)

- **Tests mit `Task.Delay` statt deterministischer Synchronisation** — Alle Async-Tests verwenden `await Task.Delay(100)` bis `await Task.Delay(300)` als Wartestrategie für das Abschließen von Hintergrundoperationen (Zeilen 52, 55, 71, 78 usw.). Das macht Tests zeitabhängig, flaky auf langsamen Systemen und schwer zu debuggen.

  Empfehlung: `AsyncRelayCommand` um eine `Task ExecuteAsync()`-Methode erweitern oder in Tests direkt die privaten `async Task`-Methoden über einen öffentlichen Einstiegspunkt testen, sodass `await` deterministisch auf das tatsächliche Ende der Operation wartet.

- **Test prüft keine echte ViewModel-Logik** — `RepositoryZuweisenAsync_Success_RuftAddRepositoryAsyncAuf` (Zeile 171) umgeht das ViewModel vollständig: Es setzt `addRepositoryAufgerufen = true` selbst ohne das ViewModel zu involvieren und assertiert immer `true` (Zeile 201). Dieser Test prüft nur `_projektService.AddRepositoryAsync` und `GetDetailAsync` direkt — keine ViewModel-Logik.

  Empfehlung: Entweder den Test löschen (da er nur den Service testet, nicht das ViewModel) oder auf eine Form umschreiben, die tatsächlich `RepositoryZuweisenCommand` im ViewModel ausführt (mit einem mockbaren Dialog-Callback).

---

### WpfTestBase.cs (WpfTestBase)

- **`Thread.Sleep` in Dispose** — `Dispose()` enthält ein `Thread.Sleep(1000)` (Zeile 89) als feste Wartezeit. In einer Test-Suite mit vielen E2E-Tests summiert sich das zu erheblichem Mehraufwand. Außerdem wird `WaitWhileMainHandleIsMissing` mit `TimeSpan.FromMilliseconds(1)` aufgerufen (Zeile 82), was keine sinnvolle Wartezeit ist und mit einer stets ausgelösten `TimeoutException` endet (diese wird still ignoriert).

  Empfehlung: Die 1-ms-Wartezeit und den ignorierten catch-Block entfernen; stattdessen `_application?.Kill()` oder `_application?.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(5))` mit einer realistischen Timeout-Dauer verwenden.

- **Leere catch-Blöcke ohne Logging** — In `Dispose()` gibt es drei leere `catch`-Blöcke (Zeilen 72, 82, 95). Fehler werden stillschweigend ignoriert, ohne dass bei Testdiagnose erkennbar ist, ob die App ordnungsgemäß beendet wurde oder ob ein Fehler aufgetreten ist.

  Empfehlung: Zumindest einen `Debug.WriteLine` oder `Console.WriteLine`-Aufruf in die catch-Blöcke einfügen, um bei Fehlersuche sichtbar zu machen, wenn Cleanup fehlschlägt.

---

### ProjectDetailE2ETests.cs (ProjectDetailE2ETests)

- **`Thread.Sleep` in Tests** — Alle E2E-Test-Methoden enthalten mehrere `Thread.Sleep`-Aufrufe (zwischen 300 ms und 1000 ms). Da `WaitForElement` bereits eine Polling-Wartelogik mit konfigurierbarem Timeout implementiert, sind die festen Sleeps redundant und erhöhen die Testlaufzeit unnötig.

  Empfehlung: Feste `Thread.Sleep`-Aufrufe nach dem Klicken auf Buttons durch `WaitForElement` mit einer angemessenen Timeout-Dauer ersetzen, das auf das Erscheinen des nächsten erwarteten Elements wartet.

- **Doppelter Setup-Code** — Jede Testmethode ruft dieselbe Initialisierungssequenz auf: `LaunchApp()`, `GetMainWindow()`, `NavigateToProjecten()`. Das ist siebenmal duplizierter Code über die Klasse hinweg.

  Empfehlung: Die Initialisierungssequenz (App starten + zur Projektliste navigieren) in die `WpfTestBase`-Basisklasse oder eine `[Collection]`-Fixture auslagern, die einmal pro Test-Session ausgeführt wird.

---

## Geprüfte Dateien

- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/WpfE2EPlaceholderTests.cs`
