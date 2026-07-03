# Anforderung: Beseitigung von Fehlerpotentialen

**Aufgaben-ID:** f78a2ed8-8da8-4da2-8f3e-5c34724b5aec  
**Feature-Branch:** task/f78a2ed88da84da28f3e5c34724b5aec-beseitigung-von-fehlerpotentia  
**Erstellt:** 2026-07-02

---

## Fachliche Zusammenfassung

Die WPF-Desktopanwendung Softwareschmiede wird gegen wiederholte Abstürze während der CLI-Ausführung stabilisiert. Eine umfassende Fehleranalyse hat 19 konkrete Fehlerquellen identifiziert, die zu unkontrollierten Prozessabbrüchen führen. Die Anwendung wird durch folgende Maßnahmen gehärtet: Installation von drei globalen Exception-Handlern (für UI-Thread, AppDomain und unbeobachtete Tasks), Absicherung aller Fire-and-Forget-Async-Aufrufe mit zentralisiertem Logging, Schutz von Event-Handlern vor Exceptions in nebenläufigen Szenarien, und Gewährleistung korrekter Ressourcen-Freigabe bei nativen Handles. Ziel ist, dass die Anwendung Fehler loggt und wo möglich weiterlauft statt unkontrolliert abzustürzen.

---

## Betroffene Klassen und Komponenten

### Kategorie 1: Globale Exception-Handler (F1–F3)

#### App.xaml.cs (neue globale Handler)

**Zu registrierende Handler:**
- `Application.DispatcherUnhandledException` (F1)
  - Abfängt unbehandelte Exceptions im UI-Thread
  - Setzt `e.Handled = true` (mit dokumentiertem Vorbehalt)
  - Loggt Exception via Serilog
  
- `AppDomain.CurrentDomain.UnhandledException` (F2)
  - Last-Resort-Handler für alle anderen Threads
  - Nur Logging möglich (IsTerminating ist meist true)
  - Aktiviert Diagnose von nicht-UI-Thread-Abstürzen

- `TaskScheduler.UnobservedTaskException` (F3)
  - Abfängt unbeobachtete Task-Exceptions
  - Ruft `e.SetObserved()` auf
  - Loggt Exception vor Beendigung

**Registrierungsort:** `App.xaml.cs` → `OnStartup()` oder Konstruktor (vor `StartupAsync`)

### Kategorie 2: Geschützte Startup-Pfade (F4–F5)

#### App.xaml.cs → StartupAsync() (Zeile 57–76)

- Zeile 66: `_host.Services.GetRequiredService<CliProcessManager>();` (F4)
  - try-catch mit spezifischer Fehlermeldung
  - Ermöglicht Fortbetrieb ohne CLI-Funktionalität bei DI-Fehler

- Zeile 74–75: `mainWindow.Show();` (F5)
  - try-catch um MainWindow-Instanziierung und -Anzeige
  - Gezielt statt pauschaler Shutdown

### Kategorie 3: Fire-and-Forget Task-Verwaltung (F6–F8, F15–F17)

#### Neue Hilfsklasse: `SafeFireAndForgetTaskHelper` oder Extension-Methode

**Ort:** Neue Datei `src/Softwareschmiede/Application/Services/SafeFireAndForgetTaskHelper.cs` (oder `src/Softwareschmiede.App/Services/`)

**Zweck:** Zentralisierte Fehlerbehandlung für alle `_ = ...`-Aufrufe

**Schnittstelle (Beispiel):**
```csharp
public static class AsyncTaskExtensions
{
    public static void SafeFireAndForget(
        this Task task,
        ILogger logger,
        string operationName = "Fire-and-Forget Task")
    {
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                logger.LogError(t.Exception, 
                    "Unerwarteter Fehler in {OperationName}", operationName);
            }
            else if (t.IsCanceled)
            {
                logger.LogInformation("Operation {OperationName} wurde abgebrochen", operationName);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
```

**Zu refaktorisierende Fire-and-Forget-Aufrufe:**

1. **`CliProcessManager.cs`** (F6)
   - Zeile 38–42: Timer-Callback `_ = AktualisierungAsync(aufgabeId)`
   - → `AktualisierungAsync(aufgabeId).SafeFireAndForget(_logger, "CliProcessManager.AktualisierungAsync")`

2. **`KiAusfuehrungsService.cs`** (F8)
   - Zeile 325–328: `_ = SendCommandDelayedAsync(session, pluginCommand, aufgabeId, ct)`
   - → `.SafeFireAndForget(_logger, "KiAusfuehrungsService.SendCommandDelayedAsync")`

3. **`MainWindowViewModel.cs`** (F15)
   - Zeile 38: `_ = AktiveAufgabenAktualisierenAsync();` (im CurrentView-Setter)
   - → `.SafeFireAndForget(_logger, "MainWindowViewModel.AktiveAufgabenAktualisierenAsync")`

4. **`ProjectDetailViewModel.cs`** (F16)
   - Zeile 65: `_ = LadenAsync(_ladenCts.Token);` (im ProjektId-Setter)
   - → `.SafeFireAndForget(_logger, "ProjectDetailViewModel.LadenAsync")`

5. **`TaskDetailViewModel.cs`** (F17)
   - Zeile 65: `_ = LadenAsync(_ladenCts.Token);` (im AufgabeId-Setter)
   - → `.SafeFireAndForget(_logger, "TaskDetailViewModel.LadenAsync")`

### Kategorie 4: Heartbeat Concurrency Protection (F7)

#### CliProcessManager.cs (Zeile 38–78)

- Neue private Feld: `SemaphoreSlim _updateSemaphore = new(1, 1);`
- `AktualisierungAsync()` mit Semaphore-Schutz:
  ```csharp
  public async Task AktualisierungAsync(Guid aufgabeId)
  {
      await _updateSemaphore.WaitAsync();
      try
      {
          // bestehende Logik
      }
      finally
      {
          _updateSemaphore.Release();
      }
  }
  ```
- Oder: Timer mit `Change(Timeout.Infinite, ...)` am Anfang, Neustart am Ende

### Kategorie 5: Process.Exited Event-Handler-Schutz (F9–F10)

#### KiAusfuehrungsService.cs

**StartCliAsync() Exited-Handler** (Zeile 115–143 / F9)
- Gesamten Handler-Body in try-catch einhüllen
- Loggen aller Exceptions ohne Weiterwurf
- Kritische Aufrufe:
  - `TryGetExitCode(handle)`
  - `_handles.TryRemove(handle, out _)`
  - `RaiseRunningCountChanged()`
  - `CliProcessStatusChanged?.Invoke(...)`

**StartWithPseudoConsoleAsync() Exited-Handler** (Zeile 272–305 / F10)
- Analog zu F9
- Zusätzlich kritisch: `removedHandle.PseudoConsoleSession?.Dispose()` (Zeile 279)
- Kann `ObjectDisposedException` werfen bei parallelem Dispose

### Kategorie 6: Native Handle-Ressourcen-Schutz (F11)

#### KiAusfuehrungsService.cs → StartWithPseudoConsoleAsync()

**Zeile 254–266: FileStream- und PseudoConsoleSession-Erstellung**

```csharp
FileStream? inputStream = null;
FileStream? outputStream = null;
try
{
    inputStream = new FileStream(
        SafeFileHandle.DuplicateHandle(pseudoConsole.InputPipeHandle),
        FileAccess.Write);
    outputStream = new FileStream(
        SafeFileHandle.DuplicateHandle(pseudoConsole.OutputPipeHandle),
        FileAccess.Read);
    
    var session = new PseudoConsoleSession(pseudoConsole, process, inputStream, outputStream);
    // Zuweisung und weitere Logik
}
finally
{
    if (/* session creation failed */)
    {
        inputStream?.Dispose();
        outputStream?.Dispose();
    }
}
```

- Ziel: Vollständige Ressourcen-Freigabe bei Exceptions vor erfolgreicher Session-Zuweisung

### Kategorie 7: Terminal-Lesevorgang Überwachung (F12–F14)

#### TerminalControl.cs

**OnSessionChanged() Zeile 90–96** (F12–F13)

- Neues Feld: `private Task? _readLoopTask;`
- Speichern des Task: `_readLoopTask = Task.Run(() => ReadLoopAsync(session, _buffer, cts.Token));`
- `ContinueWith` zum Loggen von Exceptions nach Abschluss
- Optional: Dispatcher.InvokeAsync-Aufrufe in try-catch absichern

**ReadLoopAsync() Zeile 98–134** (F14)

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

- Sicherstellen, dass auch Exceptions außerhalb von `ReadAsync` (z.B. in `_parser.Parse`, `buffer.Apply`, `InvalidateVisual`) abgefangen werden

### Kategorie 8: UI-Event-Handler-Schutz (F18)

#### ProjectDetailView.xaml.cs → IssueDoubleClick() (Zeile 33)

```csharp
try
{
    _ = vm.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Issue");
    // Optional: Fehler dem Benutzer anzeigen (via BannerService)
}
```

- Schutz der Fire-and-Forget-Ausführung vor UI-Thread-Exceptions

### Kategorie 9: Async/Await Best Practices (F19 — niedrige Priorität)

#### CliProcessManager.cs, KiAusfuehrungsService.cs (nicht-UI-Teile)

- Ergänzung von `ConfigureAwait(false)` in reinen Service-Methoden ohne UI-Kontext
- Nicht in ViewModels oder Views (dort ist UI-Thread-Rückkehr erforderlich)

---

## Implementierungsansatz

### Phase 1: Globale Handler + Logging-Infrastruktur (Priorität: HÖCHST)

1. **Registrierung der drei globalen Exception-Handler** (`App.xaml.cs`)
   - Vor `StartupAsync()`-Aufruf
   - Alle Handler mit vollständiger Exception-Protokollierung
   - Serilog-Logger verwenden (bereits konfiguriert in Zeile 32–39)

2. **SafeFireAndForget-Hilfsklasse/Extension-Methode**
   - Zentrale Stelle für Fire-and-Forget-Logging
   - Logger-Injection per Dependency Injection

### Phase 2: Kritische Event-Handler + Ressourcen-Schutz (Priorität: HOCH)

3. **Schutz von `process.Exited`-Handlern** (`KiAusfuehrungsService.cs` F9–F10)
   - Kompletter try-catch um Handler-Body
   - Wichtig: Event-Multicast-Invoke ist fehleranfällig bei mehreren Abonnenten

4. **Native Handle-Verwaltung** (`KiAusfuehrungsService.cs` F11)
   - try-finally um FileStream- und PseudoConsoleSession-Erstellung
   - Ressourcen-Cleanup bei Exception

5. **Terminal-Lesevorgang-Überwachung** (`TerminalControl.cs` F12–F14)
   - Task-Feld speichern mit `ContinueWith` für Exception-Logging
   - Erweitertes Exception-Handling in `ReadLoopAsync`

### Phase 3: Fire-and-Forget Refaktorisierung (Priorität: MITTEL)

6. **Umstellung aller Fire-and-Forget-Aufrufe** auf SafeFireAndForget
   - CliProcessManager (F6)
   - KiAusfuehrungsService (F8)
   - ViewModels (F15–F17)
   - ProjectDetailView (F18)

### Phase 4: Startup-Pfade + Optional Refactoring (Priorität: MITTEL)

7. **Try-catch um Startup-Operationen** (`App.xaml.cs` F4–F5)
   - Gezielt statt pauschaler Shutdown

8. **Concurrency-Schutz für Heartbeat** (`CliProcessManager.cs` F7)
   - SemaphoreSlim oder Timer.Change-Pattern

9. **ConfigureAwait(false)** in Service-Klassen (F19)
   - Niedrige Priorität, optional

### Abhängigkeiten zwischen Fixes

- **F1/F2/F3** (globale Handler) sind **Voraussetzung** für die Diagnosefähigkeit aller nachfolgenden Fixes
- **F12** (Task-Überwachung) hängt von **F14** (vollständigem Exception-Handling) ab
- **F6–F8, F15–F17** (SafeFireAndForget) hängen von **SafeFireAndForget-Klasse** ab
- **F4/F5** können unabhängig von anderen umgesetzt werden

---

## Konfiguration

### Logging-Konfiguration

- Alle Handler-Registrierungen verwenden bestehenden Serilog-Logger (`Log.Logger` aus `App.xaml.cs`)
- Fehlerniveaus: `LogError` für abgefangene Exceptions, `LogInformation` für Abbrüche

### Optional: AppSettings für Fire-and-Forget-Logging

- **`FireAndForgetLoggingLevel`** (LogLevel, Default: `Information`)
  - Verbosity für SafeFireAndForget-Fehler
  - Erlaubt Abschaltung bei Performance-kritischen Umgebungen (nicht empfohlen)

### Task-Speicherung in TerminalControl

- Feld `_readLoopTask` mit Cancellation-Token-Verwaltung
- Cleanup in `OnSessionChanged(null)` oder `Unloaded`-Handler

---

## Offene Fragen

1. **Detailfragen zu F1 (DispatcherUnhandledException)**
   - Sollen **alle** UI-Thread-Exceptions mit `e.Handled = true` blockiert werden, oder nur bestimmte Typen?
   - Gibt es Exceptions, die definitiv zum Shutdown führen sollen (z.B. `OutOfMemoryException`)?
   - Wie wird "Recovery" nach einer abgefangenen Exception definiert? Wird dem Benutzer eine Aktion angeboten?

2. **Windows Event Log Integration**
   - Sollen Abstürze zusätzlich zum Serilog-Log auch im Windows Event Log (`Application` event log, source `.NET Runtime`) eingetragen werden?
   - Dies wäre hilfreich für System-Administratoren bei Diagnose von unerwarteten Prozessbeendigungen.

3. **Crash-Dump/Mini-Dump-Erfassung**
   - Sollen bei kritischen Exceptions (F1, F2) automatisch Mini-Dumps oder Crash-Dumps erzeugt werden?
   - Pfad und Aufbewahrungsdauer?

4. **Performance-Auswirkungen von F12–F13 (Task-Überwachung)**
   - Könnte `ContinueWith` auf dem Scheduler zu Überlastung führen bei schnellen aufeinanderfolgenden Terminal-Lesevorgängen?
   - Alternative: Polling mit `Task.IsCompleted` in einem Background-Worker?

5. **Fire-and-Forget Exception Handling in Multicast-Delegates (F9–F10)**
   - Wenn `CliProcessStatusChanged?.Invoke(...)` mehrere Abonnenten hat und einer wirft, sollen **nur Fehler dieses Abonnenten** geloggt werden oder die Exception propagieren?
   - Aktuelle Logik: Erste Exception stoppt die Invoke-Kette und wird propagiert.
   - Besser: Jeden Abonnenten isoliert aufrufen mit Try-Catch?

6. **Datenbank-Locks und F7 (Heartbeat Concurrency)**
   - Ist die geplante SemaphoreSlim-Lösung (serialisierter Zugriff) optimal, oder könnte optimistisches Concurrency-Handling (z.B. EF Core-Concurrency-Tokens) besser sein?
   - SQLite hat bekannte Concurrency-Limitationen — ist dies ein konfirmiertes Problem oder hypothetisch?

7. **Scope und Reihenfolge der Handler-Registrierung**
   - Müssen globale Handler **vor** allen DI-Initialisierungen registriert werden, oder ist die aktuelle Position in `OnStartup` (vor `StartupAsync`) ausreichend?

8. **Testing und Reproduzierbarkeit**
   - Liegen Crash-Logs oder Dumps aus den Fehlerzuständen vor, um die Fixes zu verifizieren?
   - Wie sollen Regressions-Tests für diese Fehlerquellen aussehen? (Unit-Tests für Handler-Aufrufe? Integrations-Tests mit Task-Abbruch-Simulation?)

9. **Definition von "weiterlaufen statt abzustürzen"**
   - Sollen bestimmte Fehler zum Shutdown führen (z.B. Datenbank-Korruption), andere aber zur Fortführung?
   - Ist ein Fehler-Dialog/Banner für den Benutzer erforderlich, oder stumm loggen?

10. **Dependency Injection für ILogger in SafeFireAndForget**
    - Sollen alle Klassen, die SafeFireAndForget verwenden, einen `ILogger` via Konstruktor erhalten?
    - Oder global über `Log.Logger` (Serilog-Statik)?
    - Dies beeinflusst die Refaktoring-Komplexität erheblich.
