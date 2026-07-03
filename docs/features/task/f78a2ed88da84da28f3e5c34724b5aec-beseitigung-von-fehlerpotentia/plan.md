# Umsetzungsplan: Beseitigung von Fehlerpotentialen

## Übersicht

Die WPF-Desktopanwendung Softwareschmiede wird gegen unkontrollierte Abstürze während der CLI-Ausführung gehärtet. Der Plan adressiert 19 konkrete Fehlerquellen durch Installation von drei globalen Exception-Handlern, Absicherung von Fire-and-Forget-Task-Aufrufen mit zentralisiertem Logging, Schutz von Event-Handlern vor Exceptions, Concurrency-Schutz für Heartbeat-Updates und korrekte Ressourcen-Freigabe bei nativen Handles. Die Anwendung wird so konfiguriert, dass sie Fehler loggt und wo möglich weiterlauft statt unkontrolliert abzustürzen.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Fire-and-Forget Exception-Handling | Extension-Methode `SafeFireAndForget` auf `Task` mit Logger-Injection via Konstruktor | Zentrale Fehlerbehandlung über eine wiederverwendbare Extension-Methode, die Logger via DI erhält; ermöglicht konsistentes Logging aller Fire-and-Forget-Aufrufe ohne Codeduplication. Alle Klassen, die `SafeFireAndForget` verwenden, erhalten ihren `ILogger` über Dependency Injection. |
| Globale Exception-Handler | Registrierung in `App.xaml.cs` → `OnStartup()` vor `StartupAsync()`-Aufruf | Sichert alle drei Exception-Kanäle (UI-Thread, AppDomain, TaskScheduler) auf globaler Ebene ab; Registrierung vor Startup ermöglicht Fehlererfassung auch bei Service-Initialisierung. |
| DispatcherUnhandledException-Handling | `e.Handled = true` für alle Exceptions, mit Exception-Logging; kein Resume-Versuch | Blockiert Shutdown bei unbehandelten UI-Thread-Exceptions und loggt diese; ermöglicht Fortbetrieb der Anwendung. Keine selektive Behandlung nach Exception-Typ — alle Exceptions werden gleich behandelt. |
| Startup-Fehlerbehandlung | Individuelle try-catch um `GetRequiredService<CliProcessManager>()` und `mainWindow.Show()`, nicht pauschaler Shutdown | Ermöglicht selektive Fehlerbehandlung: CLI-Fehler führen nicht mehr zum Shutdown, MainWindow-Fehler können gezielt adressiert werden. |
| Heartbeat-Concurrency-Schutz | `SemaphoreSlim(1, 1)` mit explizit seriellem Zugriff auf `AktualisierungAsync()` | Verhindert überlappende Timer-Ticks durch Serialisierung. Einfacher als optimistisches Concurrency-Handling und passt zu SQLite-Concurrency-Limitationen. |
| Process.Exited-Handler-Schutz | Umschließendes try-catch um gesamten Handler-Body mit Exception-Logging | Handler wird nicht abgebrochen, sondern loggt den Fehler und läuft bis zum Ende. Dies verhindert, dass eine Exception in einem Abonnenten die Event-Multicast-Kette unterbricht. |
| Terminal-Lesevorgang-Überwachung | Gespeicherte Task-Referenz (`_readLoopTask`) mit `ContinueWith` zum Loggen von Exceptions nach Task-Abschluss | Ermöglicht Post-mortem-Fehleranalyse: Exceptions, die im `ReadLoopAsync`-Task auftreten, werden geloggt, auch wenn sie nicht oberflächlich sichtbar sind. |
| ConfigureAwait-Strategie | Keine `ConfigureAwait(false)` in ViewModels/Event-Handlern, aber in reinen Service-Klassen (`CliProcessManager`, `KiAusfuehrungsService`) falls performanz-kritisch | ViewModels müssen zum UI-Thread zurückkehren; Services können off-UI-Thread weiterlaufen. Niedrige Priorität (F19), optional. |

---

## Programmabläufe

### Startup mit globalen Exception-Handlern

1. `App.OnStartup()` wird aufgerufen.
2. Serilog-Logger wird initialisiert.
3. **Globale Handler registrieren:**
   - `Application.DispatcherUnhandledException` → loggt Exception, setzt `e.Handled = true`
   - `AppDomain.CurrentDomain.UnhandledException` → loggt Exception
   - `TaskScheduler.UnobservedTaskException` → loggt Exception, ruft `e.SetObserved()` auf
4. `StartupAsync()` wird aufgerufen.
5. Host wird erstellt und gestartet.
6. **`CliProcessManager` wird mit try-catch instanziiert:** Fehler beim DI-Abruf werden geloggt, die Anwendung läuft weiter ohne CLI-Funktionalität.
7. Datenbankmigrationen werden durchgeführt (bereits try-catch vorhanden).
8. **`MainWindow.Show()` wird mit try-catch aufgerufen:** Fehler bei der MainWindow-Instanziierung oder Anzeige werden geloggt und führen nicht zum Shutdown.

Beteiligte Klassen/Komponenten: `App.xaml.cs`, `Serilog.Log`, `CliProcessManager`

### Fire-and-Forget-Task-Ausführung mit zentralisiertem Logging

1. Klasse `X` hat ein Feld `private ILogger _logger;` via Konstruktor-Injection.
2. Klasse `X` ruft `await someTask().SafeFireAndForget(_logger, "OperationName")` auf.
3. `SafeFireAndForget`-Extension-Methode:
   - Registriert einen `ContinueWith`-Callback auf dem Task.
   - Wenn Task fehlgeschlagen: loggt `LogError` mit Exception und Operationsnamen.
   - Wenn Task abgebrochen: loggt `LogInformation` mit Abbruch-Hinweis.
   - Wenn Task erfolgreich: keine Aktion (optional: `LogDebug`).
4. Code kehrt sofort zurück, Task läuft asynchron; keine Exception wird zum Aufrufer propagiert.

Beteiligte Klassen/Komponenten: `AsyncTaskExtensions.SafeFireAndForget()`, `ILogger`

### Heartbeat-Update mit Concurrency-Schutz

1. `CliProcessManager.StartHeartbeat(aufgabeId)` wird aufgerufen.
2. Timer wird erstellt mit 30-Sekunden-Intervall.
3. Jede Timer-Tick ruft `AktualisierungAsync(aufgabeId).SafeFireAndForget(...)` auf.
4. In `AktualisierungAsync()`:
   - `_updateSemaphore.WaitAsync()` wird aufgerufen (blockiert, falls bereits ein Update läuft).
   - try-block: Datenbankupdate für LastHeartbeat, BearbeitungsStatus auslesen.
   - finally: `_updateSemaphore.Release()` wird immer aufgerufen.
5. Falls Fehler: Exception wird in `SafeFireAndForget` geloggt, nächster Timer-Tick kann erneut versucht werden.

Beteiligte Klassen/Komponenten: `CliProcessManager`, `SemaphoreSlim`, `SafeFireAndForget`

### Process.Exited-Handler-Execution mit Exception-Schutz (klassischer CLI)

1. CLI-Prozess beendet sich.
2. `process.Exited` wird ausgelöst.
3. **Handler-Body wird in try-catch eingehüllt:**
   - `TryGetExitCode(handle)` wird aufgerufen.
   - Handle wird aus `_handles` entfernt.
   - `RaiseRunningCountChanged()` wird aufgerufen.
   - `CliProcessStatusChanged?.Invoke(aufgabeId, status)` wird aufgerufen.
   - `PersistFehlgeschlagenAsync(aufgabeId, exitCode).SafeFireAndForget(...)` wird aufgerufen.
4. **Falls Exception auftritt:** Exception wird geloggt, Handler beendet sich ohne Neustart des Prozesses oder Exception-Propagierung.

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `SafeFireAndForget`, `ILogger`

### Process.Exited-Handler mit PseudoConsole-Cleanup

1. CLI-Prozess mit ConPTY beendet sich.
2. `process.Exited` wird ausgelöst.
3. **Handler-Body wird in try-catch eingehüllt:**
   - Handle wird aus `_handles` mit `TryRemove` entfernt.
   - `removedHandle.PseudoConsoleSession?.Dispose()` wird aufgerufen (kann `ObjectDisposedException` werfen).
   - `RaiseRunningCountChanged()` wird aufgerufen.
   - `CliProcessStatusChanged?.Invoke(...)` wird aufgerufen.
   - `PersistFehlgeschlagenAsync(...).SafeFireAndForget(...)` wird aufgerufen.
4. **Falls Exception auftritt (z. B. bei parallelem Dispose):** Exception wird geloggt, Handler beendet normal.

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `PseudoConsoleSession`, `SafeFireAndForget`

### Native Handle-Ressourcen-Freigabe bei ConPTY-Setup

1. `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` wird aufgerufen.
2. `FileStream? inputStream = null; FileStream? outputStream = null;`
3. **try-block:**
   - `inputStream` wird erstellt (`new FileStream(...)`).
   - `outputStream` wird erstellt (`new FileStream(...)`).
   - `var session = new PseudoConsoleSession(...)` wird erstellt.
   - Session wird in Dictionary gespeichert.
4. **catch-block (falls Exception vor Session-Zuweisung auftritt):**
   - `inputStream?.Dispose()` wird aufgerufen.
   - `outputStream?.Dispose()` wird aufgerufen.
   - Exception wird geloggt und re-thrown.
5. **finally:** Kein explizites Cleanup nötig (Streams sind entweder in Session erfolgreich übergeben oder im catch disposed).

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `FileStream`, `PseudoConsoleSession`

### Terminal-Lesevorgang mit Exception-Logging

1. `TerminalControl.OnSessionChanged(session)` wird aufgerufen.
2. Alter `_readCts` wird abgebrochen.
3. Neuer `CancellationTokenSource` wird erstellt.
4. `_readLoopTask = Task.Run(() => ReadLoopAsync(session, _buffer, cts.Token));`
5. Auf `_readLoopTask` wird ein `ContinueWith`-Callback registriert:
   - Falls Task fehlgeschlagen: Exception wird geloggt.
   - Falls Task abgebrochen: Abbruch wird geloggt (optional).
6. In `ReadLoopAsync()`:
   - try-catch fängt `OperationCanceledException` ab (rethrow).
   - **Generisches `catch (Exception ex)`:** Loggt Exception, bricht Schleife ab (kein rethrow).
   - Bytes werden aus `session.OutputStream` gelesen, geparst, auf Buffer angewendet.
   - `InvalidateVisual()` wird aufgerufen.
7. Beim `Unloaded`-Event wird `_readCts` abgebrochen; `_readLoopTask` wird nicht explizit awaited (gibt dem Task Zeit zu sterben, Logging erfolgt über `ContinueWith`).

Beteiligte Klassen/Komponenten: `TerminalControl`, `PseudoConsoleSession`, `TerminalBuffer`, `AnsiSequenceParser`, `CancellationTokenSource`

### UI-Event-Handler-Schutz (IssueDoubleClick)

1. Benutzer double-clickt auf Issue in UI.
2. `ProjectDetailView.IssueDoubleClick()` wird aufgerufen.
3. `try-catch` umhüllt den gesamten Handler-Body.
4. `_ = vm.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue).SafeFireAndForget(_logger, "ProjectDetailView.AufgabeAusIssueErstellenCommand")` wird aufgerufen.
5. Falls Exception auftritt: Fehler wird geloggt, Handler beendet normal.

Beteiligte Klassen/Komponenten: `ProjectDetailView.xaml.cs`, `ProjectDetailViewModel`, `ILogger`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AsyncTaskExtensions` | Statische Utility-Klasse | Bietet Extension-Methode `SafeFireAndForget(Task, ILogger, string)` für zentrale Fehlerbehandlung aller Fire-and-Forget-Aufrufe. Registriert `ContinueWith`-Callback, um Exceptions zu loggen, ohne sie zu propagieren. |

---

## Änderungen an bestehenden Klassen

### `App.xaml.cs` (Code-behind)

- **Neue Event-Handler (in `OnStartup()`):**
  - `Application.DispatcherUnhandledException += (s, e) => { ... }` — Loggt Exception, setzt `e.Handled = true`
  - `AppDomain.CurrentDomain.UnhandledException += (s, e) => { ... }` — Loggt Exception
  - `TaskScheduler.UnobservedTaskException += (s, e) => { ... }` — Loggt Exception, ruft `e.SetObserved()` auf
- **Anpassung `StartupAsync()` Zeile 66:** Umhüllung von `_host.Services.GetRequiredService<CliProcessManager>();` in try-catch mit spezifischer Fehlermeldung; Fortbetrieb ohne CLI-Funktionalität bei DI-Fehler
- **Anpassung `StartupAsync()` Zeile 74–75:** Umhüllung von `mainWindow.Show();` in try-catch mit spezifischer Fehlermeldung

### `CliProcessManager` (Klasse)

- **Neue Felder:**
  - `private SemaphoreSlim _updateSemaphore = new(1, 1);` — Concurrency-Schutz für `AktualisierungAsync()`
- **Änderung `StartHeartbeat()`:** Timer-Callback-Body wird angepasst: `_ = AktualisierungAsync(aufgabeId).SafeFireAndForget(_logger, "CliProcessManager.AktualisierungAsync")` (statt pauschales `_ = ...`)
- **Änderung `AktualisierungAsync()`:** 
  - Methoden-Body wird in Semaphore-Schutz eingehüllt:
    ```
    await _updateSemaphore.WaitAsync();
    try { /* bestehende Logik */ }
    finally { _updateSemaphore.Release(); }
    ```

### `KiAusfuehrungsService` (Klasse)

- **Änderung `StartCliAsync()` Exited-Handler (Zeile 115–143):**
  - Gesamter Handler-Body wird in try-catch eingehüllt
  - Exception wird geloggt, Handler beendet normal
  - Fire-and-Forget-Aufruf: `_ = PersistFehlgeschlagenAsync(...).SafeFireAndForget(_logger, "KiAusfuehrungsService.PersistFehlgeschlagenAsync")` (statt pauschales `_ = ...`)
- **Änderung `StartWithPseudoConsoleAsync()` Exited-Handler (Zeile 272–305):**
  - Gesamter Handler-Body wird in try-catch eingehüllt
  - Exception wird geloggt, Handler beendet normal
  - `removedHandle.PseudoConsoleSession?.Dispose()` kann `ObjectDisposedException` werfen — wird vom try-catch abgefangen
  - Fire-and-Forget-Aufruf: `_ = PersistFehlgeschlagenAsync(...).SafeFireAndForget(...)` (statt pauschales `_ = ...`)
- **Änderung `StartWithPseudoConsoleAsync()` FileStream-Erstellung (Zeile 254–266):**
  - ```csharp
    FileStream? inputStream = null;
    FileStream? outputStream = null;
    try
    {
        inputStream = new FileStream(...);
        outputStream = new FileStream(...);
        var session = new PseudoConsoleSession(...);
        _handles.TryAdd(...);
    }
    catch
    {
        inputStream?.Dispose();
        outputStream?.Dispose();
        throw; // oder geloggt
    }
    ```
- **Änderung `SendCommandDelayedAsync()` Fire-and-Forget:** `_ = SendCommandDelayedAsync(...).SafeFireAndForget(_logger, "KiAusfuehrungsService.SendCommandDelayedAsync")` (statt pauschales `_ = ...`)

### `TerminalControl` (Klasse)

- **Neue Felder:**
  - `private Task? _readLoopTask;` — Speichert Task-Referenz für Exception-Logging
- **Änderung `OnSessionChanged(PseudoConsoleSession?)` (Zeile 90–96):**
  - `_readLoopTask = Task.Run(() => ReadLoopAsync(session, _buffer, cts.Token));`
  - Auf `_readLoopTask` wird `ContinueWith`-Callback registriert:
    ```csharp
    _readLoopTask.ContinueWith(t =>
    {
        if (t.IsFaulted)
            _logger.LogError(t.Exception, "Terminal-Lesevorgang fehlgeschlagen");
    }, TaskScheduler.FromCurrentSynchronizationContext());
    ```
- **Änderung `ReadLoopAsync()` (Zeile 98–134):**
  - Erweiterung des äußeren try-catch um generisches `catch (Exception ex)`:
    ```csharp
    catch (OperationCanceledException)
    {
        // bestehende Logik
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unerwarteter Fehler in Terminal-Lesevorgang");
    }
    ```

### `MainWindowViewModel` (Klasse)

- **Änderung `CurrentView` Setter (Zeile 38):**
  - `_ = AktiveAufgabenAktualisierenAsync().SafeFireAndForget(_logger, "MainWindowViewModel.AktiveAufgabenAktualisierenAsync")`

### `ProjectDetailViewModel` (Klasse)

- **Änderung `ProjektId` Setter (Zeile 65):**
  - `_ = LadenAsync(_ladenCts.Token).SafeFireAndForget(_logger, "ProjectDetailViewModel.LadenAsync")`

### `TaskDetailViewModel` (Klasse)

- **Änderung `AufgabeId` Setter (Zeile 65):**
  - `_ = LadenAsync(_ladenCts.Token).SafeFireAndForget(_logger, "TaskDetailViewModel.LadenAsync")`
- **Änderung `OnCliProcessStatusChanged()` (Zeile ca. 300+):**
  - Dispatcher-Callback-Body wird in try-catch eingehüllt (optional, aber empfohlen für Event-Handler)

### `ProjectDetailView.xaml.cs` (Code-behind)

- **Neue Felder:**
  - `private ILogger? _logger;` — Wird via Service Locator oder Constructor Injection bereitgestellt (WPF Code-behind Einschränkung)
- **Änderung `IssueDoubleClick()` (Zeile 28–35):**
  - ```csharp
    private void IssueDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var issue = ((FrameworkElement)sender).DataContext as Issue;
            if (issue != null && DataContext is ProjectDetailViewModel vm)
            {
                _ = vm.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue)
                    .SafeFireAndForget(_logger ?? LoggerFactory.CreateLogger<ProjectDetailView>(), 
                        "ProjectDetailView.AufgabeAusIssueErstellenCommand");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler in IssueDoubleClick");
        }
    }
    ```

---

## Datenbankmigrationen

Keine Datenbankmigrationen erforderlich. Alle Änderungen sind in-memory (Semaphore, Logger, Exception-Handler, Task-Referenzen).

---

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Die Fehlerbehandlung ist strukturell, nicht datenvalidierend.

---

## Konfigurationsänderungen

Keine Konfigurationsänderungen erforderlich. Alle Handler und Services verwenden bestehende Serilog-Logger-Infrastruktur.

Optional (niedrige Priorität, nicht in Anforderung):
- **`appsettings.json` Erweiterung:** `"FireAndForgetLoggingLevel": "Information"` (für zukünftige Logging-Level-Kontrolle, nicht in dieser Iteration implementieren).

---

## Seiteneffekte und Risiken

- **Serilog-Performance:** Massives Logging in Exception-Handlern kann Performance-Overhead verursachen; Logger hat bereits täglich-Rollover und 14-Tage-Aufbewahrung konfiguriert.
- **DispatcherUnhandledException mit `e.Handled = true`:** Könnte zu "stillen" Fehlern führen, wenn Anwendung nach Exception einen inconsistenten Zustand hat; mitigation durch vollständiges Exception-Logging.
- **Semaphore in `CliProcessManager.AktualisierungAsync()`:** Serialisiert Heartbeat-Updates; bei sehr hoher Prozessanzahl könnte Contention auftreten, aber mit 30-Sekunden-Intervall unwahrscheinlich.
- **TaskScheduler.UnobservedTaskException-Handler:** Wirkt sich auf alle Fire-and-Forget-Tasks in der Anwendung aus; dient als Last-Resort-Fallback.
- **PseudoConsoleSession?.Dispose():** `ObjectDisposedException` wird jetzt abgefangen statt propagiert; dies könnte maskieren echte Doppel-Dispose-Fehler, aber besser als Crash.
- **Terminal-Lesevorgang `ContinueWith` auf UI-Scheduler:** Könnte zu Deadlock oder Race-Conditions mit UI-Updates führen, aber `Dispatcher.InvokeAsync` in `ReadLoopAsync` ist bereits vorhanden.

---

## Umsetzungsreihenfolge

1. **`AsyncTaskExtensions` Klasse anlegen**
   - Voraussetzungen: Microsoft.Extensions.Logging (bereits im Projekt vorhanden)
   - Beschreibung: Neue Datei `src/Softwareschmiede/Application/Services/AsyncTaskExtensions.cs` mit statischer Extension-Methode `SafeFireAndForget(Task, ILogger, string)`. Diese Methode registriert einen `ContinueWith`-Callback, der Exceptions auf Fehler-Ebene loggt, ohne sie zu propagieren. Wird von allen nachfolgenden Schritten benötigt.

2. **Globale Exception-Handler in `App.xaml.cs` registrieren (F1–F3)**
   - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1, Serilog-Logger (bereits konfiguriert)
   - Beschreibung: In `App.OnStartup()` vor `StartupAsync()`-Aufruf die drei Handler registrieren: `Application.DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`. Alle Handler loggen via `Log.Logger`. `DispatcherUnhandledException` setzt `e.Handled = true`.

3. **Startup-Pfade in `App.xaml.cs` absichern (F4–F5)**
   - Voraussetzungen: Globale Handler aus Schritt 2 (optional, aber empfohlen)
   - Beschreibung: In `App.StartupAsync()` Zeile 66 (`GetRequiredService<CliProcessManager>()`) und Zeile 74–75 (`mainWindow.Show()`) mit individuellem try-catch absichern. CLI-Fehler sollten nicht zum Shutdown führen, MainWindow-Fehler sollten gezielt behandelt werden.

4. **Concurrency-Schutz für Heartbeat in `CliProcessManager` (F7)**
   - Voraussetzungen: Keine
   - Beschreibung: Neues Feld `private SemaphoreSlim _updateSemaphore = new(1, 1);` in `CliProcessManager` anlegen. Methode `AktualisierungAsync()` in `await _updateSemaphore.WaitAsync()` und `finally _updateSemaphore.Release()` einpacken.

5. **Fire-and-Forget in `CliProcessManager.StartHeartbeat()` schützen (F6)**
   - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1
   - Beschreibung: Timer-Callback in `StartHeartbeat()` (Zeile 38–42) von `_ = AktualisierungAsync(aufgabeId)` zu `_ = AktualisierungAsync(aufgabeId).SafeFireAndForget(_logger, "CliProcessManager.AktualisierungAsync")` ändern.

6. **Process.Exited-Handler in `KiAusfuehrungsService.StartCliAsync()` schützen (F9)**
   - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1
   - Beschreibung: In `StartCliAsync()` Zeile 115–143 (Exited-Handler) gesamten Handler-Body in try-catch einpacken; Exception loggen, Handler normal beenden. Fire-and-Forget-Aufruf `PersistFehlgeschlagenAsync()` mit `SafeFireAndForget()` schützen.

7. **Process.Exited-Handler in `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` schützen (F10)**
   - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1
   - Beschreibung: In `StartWithPseudoConsoleAsync()` Zeile 272–305 (Exited-Handler) gesamten Handler-Body in try-catch einpacken; Exception loggen, Handler normal beenden. `removedHandle.PseudoConsoleSession?.Dispose()` kann `ObjectDisposedException` werfen — wird vom try-catch abgefangen.

8. **Native Handle-Ressourcen-Freigabe in `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` (F11)**
   - Voraussetzungen: Keine
   - Beschreibung: FileStream-Erstellung (Zeile 254–266) in try-catch-finally einpacken: Bei Exception vor Session-Zuweisung werden `inputStream` und `outputStream` disposed.

9. **Fire-and-Forget in `KiAusfuehrungsService.SendCommandDelayedAsync()` schützen (F8)**
   - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1
   - Beschreibung: In `StartWithPseudoConsoleAsync()` Zeile 327 (Fire-and-Forget) von `_ = SendCommandDelayedAsync(...)` zu `_ = SendCommandDelayedAsync(...).SafeFireAndForget(...)` ändern.

10. **Terminal-Lesevorgang in `TerminalControl` schützen (F12–F14)**
    - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1, `ILogger` im Konstruktor
    - Beschreibung: 
      - Neues Feld `private Task? _readLoopTask;` anlegen.
      - `OnSessionChanged()` Zeile 92 anpassen: `_readLoopTask = Task.Run(...);` und `ContinueWith`-Callback für Exception-Logging registrieren.
      - `ReadLoopAsync()` um generisches `catch (Exception ex)` erweitern (Zeile 98–134), das Exception loggt.

11. **Fire-and-Forget in `MainWindowViewModel.CurrentView` Setter schützen (F15)**
    - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1
    - Beschreibung: Zeile 38 von `_ = AktiveAufgabenAktualisierenAsync();` zu `_ = AktiveAufgabenAktualisierenAsync().SafeFireAndForget(_logger, "MainWindowViewModel.AktiveAufgabenAktualisierenAsync")` ändern.

12. **Fire-and-Forget in `ProjectDetailViewModel.ProjektId` Setter schützen (F16)**
    - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1
    - Beschreibung: Zeile 65 von `_ = LadenAsync(_ladenCts.Token);` zu `_ = LadenAsync(_ladenCts.Token).SafeFireAndForget(_logger, "ProjectDetailViewModel.LadenAsync")` ändern.

13. **Fire-and-Forget in `TaskDetailViewModel.AufgabeId` Setter schützen (F17)**
    - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1
    - Beschreibung: Zeile 65 von `_ = LadenAsync(_ladenCts.Token);` zu `_ = LadenAsync(_ladenCts.Token).SafeFireAndForget(_logger, "TaskDetailViewModel.LadenAsync")` ändern.

14. **Fire-and-Forget in `ProjectDetailView.xaml.cs` IssueDoubleClick() schützen (F18)**
    - Voraussetzungen: `AsyncTaskExtensions` aus Schritt 1, Logger-Bereitstellung in Code-behind (via Service Locator oder statischer Fallback)
    - Beschreibung: `IssueDoubleClick()` Zeile 33 von `_ = vm.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue);` zu `_ = vm.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue).SafeFireAndForget(...)` ändern; gesamten Handler-Body in try-catch einpacken.

15. **Async/Await Best Practices — ConfigureAwait(false) (F19, niedrige Priorität, optional)**
    - Voraussetzungen: Keine
    - Beschreibung: In `CliProcessManager` und `KiAusfuehrungsService` (reinen Service-Methoden ohne UI-Kontext) `ConfigureAwait(false)` hinzufügen. ViewModels und Event-Handler nicht ändern (müssen zum UI-Thread zurückkehren).

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AsyncTaskExtensions_SafeFireAndForget_LogsErrorOnTaskException` | `AsyncTaskExtensionsTests` (neue Klasse) | Wenn ein Fire-and-Forget-Task eine Exception wirft, wird diese via Logger auf Error-Ebene geloggt, ohne propagiert zu werden. |
| `AsyncTaskExtensions_SafeFireAndForget_LogsInfoOnTaskCancellation` | `AsyncTaskExtensionsTests` | Wenn ein Task abgebrochen wird, wird dies via Logger auf Info-Ebene geloggt. |
| `App_DispatcherUnhandledException_Handler_LogsAndHandlesException` | `AppTests` (neue WPF-App-Testklasse) | `DispatcherUnhandledException`-Handler ist registriert, loggt Exception, setzt `e.Handled = true`. |
| `App_UnhandledException_Handler_Logs` | `AppTests` | `AppDomain.CurrentDomain.UnhandledException`-Handler ist registriert und loggt. |
| `App_UnobservedTaskException_Handler_LogsAndSetsObserved` | `AppTests` | `TaskScheduler.UnobservedTaskException`-Handler ist registriert, loggt Exception, ruft `e.SetObserved()` auf. |
| `App_StartupAsync_HandlesCliProcessManagerInitializationError` | `AppTests` | Fehler beim `GetRequiredService<CliProcessManager>()` wird geloggt, führt nicht zum Shutdown. |
| `App_StartupAsync_HandlesMainWindowShowError` | `AppTests` | Fehler bei `mainWindow.Show()` wird geloggt, führt nicht zum Shutdown. |
| `CliProcessManager_AktualisierungAsync_WithConcurrentTimerTicks_Serializes` | `CliProcessManagerTests` (neue Testklasse) | Zwei parallele Timer-Ticks führen nicht zu Race-Conditions; Semaphore serialisiert Zugriff auf `AktualisierungAsync()`. |
| `CliProcessManager_StartHeartbeat_UsesFireAndForgetSafely` | `CliProcessManagerTests` | Timer-Callback ruft `SafeFireAndForget` auf; Exceptions werden geloggt, nicht propagiert. |
| `KiAusfuehrungsService_StartCliAsync_ExitedHandler_WithException_LogsAndDoesNotThrow` | `KiAusfuehrungsServiceTests` (erweitern, neue Tests) | Process.Exited-Handler wird mit try-catch geschützt; Exception wird geloggt, Handler beendet normal. |
| `KiAusfuehrungsService_StartWithPseudoConsoleAsync_ExitedHandler_WithException_LogsAndDoesNotThrow` | `KiAusfuehrungsServiceTests` | Process.Exited-Handler für ConPTY wird mit try-catch geschützt; Exception wird geloggt. |
| `KiAusfuehrungsService_StartWithPseudoConsoleAsync_DisposedSessionException_Handled` | `KiAusfuehrungsServiceTests` | `ObjectDisposedException` beim `PseudoConsoleSession?.Dispose()` wird abgefangen, geloggt. |
| `KiAusfuehrungsService_StartWithPseudoConsoleAsync_FileStreamCreationFailure_DisposesStreams` | `KiAusfuehrungsServiceTests` | Exception bei FileStream-Erstellung führt zu Cleanup: beide Streams werden disposed. |
| `TerminalControl_ReadLoopAsync_WithException_LogsAndDoesNotThrow` | `TerminalControlTests` (neue Testklasse) | `ReadLoopAsync()` wirft keine unbehandelten Exceptions; Exception wird geloggt via `catch (Exception)`. |
| `TerminalControl_OnSessionChanged_StoresReadLoopTaskAndLogsCompletion` | `TerminalControlTests` | `_readLoopTask` wird gespeichert, `ContinueWith`-Callback wird registriert, loggt auf Task-Abschluss. |
| `MainWindowViewModel_CurrentView_Setter_UsesFireAndForgetSafely` | `MainWindowViewModelTests` (erweitern) | `CurrentView`-Setter ruft `SafeFireAndForget` auf; Exceptions in `AktiveAufgabenAktualisierenAsync()` werden geloggt. |
| `ProjectDetailViewModel_ProjektId_Setter_UsesFireAndForgetSafely` | `ProjectDetailViewModelTests` (erweitern) | `ProjektId`-Setter ruft `SafeFireAndForget` auf; Exceptions in `LadenAsync()` werden geloggt. |
| `TaskDetailViewModel_AufgabeId_Setter_UsesFireAndForgetSafely` | `TaskDetailViewModelTests` (erweitern) | `AufgabeId`-Setter ruft `SafeFireAndForget` auf; Exceptions in `LadenAsync()` werden geloggt. |
| `ProjectDetailView_IssueDoubleClick_UsesFireAndForgetSafely` | `ProjectDetailViewTests` (neue Testklasse) | `IssueDoubleClick` ruft `SafeFireAndForget` auf; Exceptions werden geloggt. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProcessExited_ScopeFactoryDisposed_PersistiertNichtUndWirftNicht` (in `KiAusfuehrungsServiceTests`) | Dieser Test adressiert nur den Fire-and-Forget-Pfad über `PersistFehlgeschlagenAsync()`, nicht den gesamten Handler-Body (F9). Test bleibt gültig, aber sein Scope ist begrenzt — neue Tests (F9/F10-spezifische Handler-Tests) sollten hinzugefügt werden. |
| `MainWindowViewModelTests` | Tests für `CurrentView`-Setter existieren möglicherweise nicht; falls vorhanden, müssen sie Fire-and-Forget-Verhalten berücksichtigen. Generell keine Breaking-Changes, aber neuen Fire-and-Forget-Test hinzufügen. |
| `ProjectDetailViewModelTests` | Tests für `ProjektId`-Setter existieren möglicherweise; falls vorhanden, keine Breaking-Changes durch Hinzufügen von `SafeFireAndForget`. |
| `TaskDetailViewModelTests` | Tests für `AufgabeId`-Setter existieren möglicherweise; falls vorhanden, keine Breaking-Changes durch Hinzufügen von `SafeFireAndForget`. |

Falls keine existierenden Tests für die genannten Setter vorhanden sind, ist keine Anpassung erforderlich.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| **CLI-Prozess startet und beendet sich mit regulärem Exit-Code** | E2E-Test (neue oder erweiterte Testklasse) | Prozess.Exited-Handler wird aufgerufen ohne Exceptions; `CliProcessStatusChanged` wird korrekt ausgelöst; `PersistFehlgeschlagenAsync` wird ohne Fehler durchgeführt. Logs enthalten keine unerwarteten Fehler. |
| **CLI-Prozess startet mit ConPTY und beendet sich mit regulärem Exit-Code** | E2E-Test | ConPTY-basierter Process.Exited-Handler wird aufgerufen; Session wird korrekt disposed; Logs enthalten keine `ObjectDisposedException`. |
| **Exception in Process.Exited-Handler wird geloggt, stoppt nicht die Anwendung** | E2E-Test (Fehler-Simulation) | Absichtliche Exception im Handler-Callback (z. B. Mock-Failure in `RaiseRunningCountChanged`) wird geloggt; Anwendung läuft weiter. |
| **Terminal-Lesevorgang läuft ohne Exceptions** | E2E-Test (Terminal-spezifisch) | ConPTY-Session wird gestartet, `TerminalControl` liest Ausgaben, `ReadLoopAsync` funktioniert ohne unerwartete Exceptions. Logs enthalten keine Fehler im Terminal-Lesevorgang. |
| **Exception in Fire-and-Forget-Task wird geloggt, stoppt nicht die Anwendung** | E2E-Test | Beispiel: Fehler beim Laden von aktiven Aufgaben im `MainWindowViewModel.CurrentView`-Setter; Anwendung bleibt responsiv, Fehler wird geloggt. |
| **Heartbeat-Updates laufen ohne Race-Conditions** | E2E-Test (Langzeit-Szenario) | Mehrere CLI-Prozesse mit Heartbeat laufen parallel; Datenbankupdates erfolgen ohne Locks/Timeouts; Logs enthalten keine Concurrency-Fehler. |

Welche bestehenden E2E-Tests sind betroffen?

- **Existierende Tests für CLI-Prozess-Start/Stop:** Falls bereits E2E-Tests für normale CLI-Ausführung existieren, könnten sie durch geänderte Exception-Handling-Semantik betroffen sein (z. B. wenn sie auf spezifische Exception-Behavior gerechnet haben, das jetzt abgefangen wird). **Wahrscheinlich keine Anpassungen erforderlich**, da die neuen Handler nur loggen, nicht Verhalten ändern.
- **Existierende Tests für UI-Navigation (MainWindowViewModel, ProjectDetailViewModel):** Falls E2E-Tests für View-Wechsel oder Projekt-Laden existieren, werden sie nicht betroffen (Fire-and-Forget-Schutz ist additiv).
- **Bestehende Crash-Scenarios (falls vorhanden):** Falls E2E-Tests absichtlich Exceptions auslösen, um Crash-Verhalten zu verifizieren, könnten sie jetzt nicht mehr crashen, sondern nur loggen. Diese Tests müssen an erwartetes neues Verhalten angepasst werden (z. B.: „Anwendung läuft weiter, Fehler ist geloggt").

**Zu prüfen:** Existieren E2E-Tests für Crash-Szenarien oder Exception-Propagierung? Falls ja, müssen diese angepasst werden; falls nein, sind keine Breaking-Changes zu erwarten.

---

## Offene Punkte

Keine offenen Punkte — alle Anforderungen sind durch die Bestandsaufnahme und gängige Praktiken eindeutig adressiert.

| # | Offener Punkt | Status |
|---|---------------|--------|
| — | — | Alle geklärt |

---

## Zusammenfassung

Dieser Plan adressiert alle 19 Fehlerquellen (F1–F19) durch:

1. **Globale Exception-Handler (F1–F3):** Registrierung in `App.xaml.cs` vor Startup.
2. **Startup-Fehlerbehandlung (F4–F5):** Individuelle try-catch um kritische Operationen.
3. **Fire-and-Forget-Schutz (F6, F8, F15–F18):** Zentrale `SafeFireAndForget`-Extension-Methode.
4. **Heartbeat-Concurrency (F7):** `SemaphoreSlim` in `CliProcessManager`.
5. **Process.Exited-Handler-Schutz (F9–F10):** Umschließendes try-catch mit Exception-Logging.
6. **Native Handle-Freigabe (F11):** try-catch-finally um FileStream-Erstellung.
7. **Terminal-Lesevorgang (F12–F14):** Gespeicherte Task-Referenz mit `ContinueWith` und erweitertem Exception-Handling.
8. **Async/Await Best Practices (F19):** Optional `ConfigureAwait(false)` (niedrige Priorität).

Die Implementierung folgt bestehenden Patterns (DI für Logger, Serilog-Integration, try-catch-Strukturen) und führt keine neuen Abhängigkeiten ein. Alle Schritte sind sequenziell ausführbar mit klaren Voraussetzungen.
