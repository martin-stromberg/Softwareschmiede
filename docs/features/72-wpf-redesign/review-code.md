# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ProjectDetailViewModel.cs

- **Fehlerbehandlung / Doppeltes Dispose** — `Dispose()` setzt `_selectedTaskViewModel` und `_ladenCts` nach dem Entsorgen nicht auf `null`. Wird `Dispose()` zweimal aufgerufen (z. B. durch `DetailViewModel`-Setter in `ProjectListViewModel` und nachfolgend durch den DI-Container), wird dasselbe `IDisposable`-Objekt ein zweites Mal disposed, was `ObjectDisposedException` auslöst.

  Empfehlung: Nach dem Aufruf von `Dispose()` auf beiden Feldern das jeweilige Feld auf `null` setzen.

- **Fehlerbehandlung / Fire-and-forget nach Erstellen** — In `ProjektSpeichernAsync` (Zeile 269) setzt das Setzen von `ProjektId` im Setter sofort ein `LadenAsync` als Fire-and-forget auf einem frischen `CancellationTokenSource`. Kurz danach wird `ProjektListeAktualisierenCallback?.Invoke()` aufgerufen. Nach der Rückkehr verbleiben `ProjektName` und `ProjektBeschreibung` unverändert, das Overlay bleibt offen, und ein erneuter Klick auf „Speichern" trifft nun den Update-Pfad (Zeile 273) und speichert das Projekt ein zweites Mal stillschweigend.

  Empfehlung: Nach erfolgreicher Erstellung `ZurueckAction?.Invoke()` aufrufen oder `ProjektName`/`ProjektBeschreibung` zurücksetzen und dem Nutzer eine Erfolgsmeldung anzeigen.

- **Fehlerbehandlung / Toter Code in `RepositoryOeffnenAsync`** — `RepositoryOeffnenAsync` ist als `async Task` deklariert und akzeptiert einen `CancellationToken`, enthält aber ausschließlich synchronen Code (`Process.Start`). Der `catch (OperationCanceledException)`-Block (Zeile 368) ist unerreichbarer Code, da nichts im `try`-Block eine solche Exception werfen kann. Dies vermittelt fälschlicherweise den Eindruck, dass Abbruch korrekt behandelt wird.

  Empfehlung: Methode in eine synchrone Methode umwandeln (`void`) oder zumindest den toten `catch`-Block entfernen. Das `async`-Schlüsselwort und den `CancellationToken`-Parameter entfernen, da sie nicht benötigt werden.

- **Kopplung / ShowDialog über CancellationToken** — `RepositoryZuweisenAsync` übergibt den `CancellationToken` des `AsyncRelayCommand` über den synchronen `ShowDialog()`-Aufruf hinaus. Wenn das ViewModel während des offenen Dialogs disposed wird (z. B. durch Navigation), wird `_ladenCts` abgebrochen – aber das ist ein *anderes* `CancellationTokenSource` als das des `AsyncRelayCommand`. Nach Schließen des Dialogs schreibt `LadenAsync(ct)` (Zeile 337) in die Felder eines bereits disposed ViewModels.

  Empfehlung: Nach `ShowDialog()` den Zustand des ViewModels prüfen (z. B. ein `_disposed`-Flag) und bei Bedarf abbrechen, bevor asynchrone Folgeoperationen gestartet werden.

- **Vereinfachung / Redundante Lambda** — `LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct))` (Zeile 175) verwendet eine unnötige Lambda-Hülle. `LadenAsync` erfüllt die erwartete Signatur `Func<CancellationToken, Task>` direkt.

  Empfehlung: `new AsyncRelayCommand(LadenAsync)` verwenden, wie es bei `AufgabeErstellenCommand` (Zeile 177) korrekt gemacht wird.

### ProjectDetailView.xaml.cs

*Keine Befunde.*

### ProjectListViewModel.cs

- **Fehlerbehandlung / `async void` ohne CancellationToken** — `NeuesProjektHinzufuegen` (Zeile 174) ist `async void` und übergibt keinen `CancellationToken` an `GetAllAsync()`. Exceptions aus dem try/catch werden korrekt gefangen, aber die `async void`-Deklaration verhindert, dass der Aufrufer die Operation awaiten oder abbrechen kann. Zukünftige Änderungen, die Code außerhalb des try-Blocks hinzufügen, könnten unbehandelte Exceptions auf dem `SynchronizationContext` verursachen, die die Anwendung abstürzen lassen.

  Empfehlung: Die Methode mit einem `CancellationToken`-Parameter versehen und `GetAllAsync(ct)` aufrufen. Da die Methode als Callback übergeben wird, entweder den Callback-Typ auf `Func<CancellationToken, Task>` ändern oder eine eigene `CancellationTokenSource` im ViewModel verwalten.

- **Namenskonventionen / Falsches Async-Suffix** — `ZeigeDetailErstellungsFormularAsync` (Zeile 163) und `ZeigeDetailAsync` (Zeile 154) sind synchrone `void`-Methoden, tragen aber das Suffix `Async`. Im .NET-Konvention zeigt dieses Suffix eine `Task`- oder `ValueTask`-Rückgabe an.

  Empfehlung: Beide Methoden in `ZeigeDetailErstellungsFormular` und `ZeigeDetail` umbenennen.

### RepositoryAssignDialog.xaml.cs

- **Kopplung / Öffentlicher parameterloser Konstruktor** — `RepositoryAssignDialog()` ist `public` und lässt `DataContext` auf `null`. Wird dieser Konstruktor von externem Code, dem XAML-Designer oder einem Test ohne ViewModel aufgerufen, bleibt der Dialog offen, weil `CloseRequested` nie abonniert wird und `DialogResult` nie gesetzt wird.

  Empfehlung: Den parameterlosen Konstruktor auf `private` setzen, da er ausschließlich über `: this()` intern gekettet wird.

### ViewModelBase.cs (`AsyncRelayCommand`)

- **Effizienz / Fehlende Thread-Sicherheit auf `_isExecuting`** — Der Guard `if (_isExecuting)` (Zeile 128) und die Zuweisung `_isExecuting = true` (Zeile 132) sind nicht atomar. Bei Aufrufen aus mehreren Threads (theoretisch möglich bei Tests oder `Dispatcher.Invoke`) können beide gleichzeitig die Bedingung als `false` sehen und die Ausführung doppelt starten.

  Empfehlung: `_isExecuting` durch ein `volatile int`-Feld ersetzen und `Interlocked.CompareExchange` für den Guard verwenden – oder auf `CommunityToolkit.Mvvm.Input.AsyncRelayCommand` migrieren, das dieses Problem bereits löst.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/ProjectListView.xaml`
- `src/Softwareschmiede.App/Views/ProjectListView.xaml.cs`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
