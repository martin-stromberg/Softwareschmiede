← [Zurück zur Übersicht](index.md)

# Stabilität & Fehlerbehandlung — Business Rules

## Unbedingtes Abfangen von UI-Thread-Exceptions

**Beschreibung:** Jede unbehandelte Exception im WPF-UI-Thread wird abgefangen und die Anwendung läuft weiter — unabhängig vom Exception-Typ.

**Bedingungen:**
- Exception wird im UI-Thread nicht durch anwendungseigenen Code abgefangen.

**Verhalten:**
- Exception wird via `LogError` protokolliert.
- `e.Handled = true` wird gesetzt — es gibt **keine** Sonderbehandlung nach Exception-Typ (z. B. keine gezielte Weiterleitung von `OutOfMemoryException` zum Shutdown).

**Umsetzung:** `App.OnDispatcherUnhandledException` (`src/Softwareschmiede.App/App.xaml.cs`).

---

## Fire-and-Forget-Exceptions werden nie propagiert

**Beschreibung:** Alle bewusst nicht abgewarteten asynchronen Aufrufe (`_ = task` bzw. `task.SafeFireAndForget(...)`) dürfen die aufrufende Methode nie mit einer Exception verlassen.

**Bedingungen:**
- Der Task wird über `SafeFireAndForget(logger, operationName)` gestartet.

**Verhalten:**
- Task erfolgreich: keine Aktion.
- Task fehlgeschlagen (`IsFaulted`): `LogError(t.Exception, ...)` mit dem übergebenen `operationName`.
- Task abgebrochen (`IsCanceled`): `LogInformation(...)` mit dem übergebenen `operationName`.
- In keinem Fall wird die Exception an den ursprünglichen Aufrufer zurückgegeben.

**Umsetzung:** `AsyncTaskExtensions.SafeFireAndForget` (`src/Softwareschmiede/Application/Services/AsyncTaskExtensions.cs`), verwendet u. a. in `CliProcessManager.StartHeartbeat`, `KiAusfuehrungsService.HandleProcessExited`/`SendCommandDelayedAsync`, den Property-Settern von `MainWindowViewModel.CurrentView`, `ProjectDetailViewModel.ProjektId`, `TaskDetailViewModel.AufgabeId`, `TerminalControl.OnSessionChanged` und `ProjectDetailView.IssueDoubleClick`.

---

## Process.Exited-Handler läuft immer vollständig durch

**Beschreibung:** Der `Process.Exited`-Handler eines CLI-Prozesses muss alle seine Teilschritte (Handle entfernen, Ressourcen aufräumen, Status ermitteln, Event auslösen) ausführen, auch wenn ein einzelner Teilschritt eine Exception wirft.

**Bedingungen:**
- `Process.Exited` wird ausgelöst (klassischer Start oder ConPTY-Start).

**Verhalten:**
- Der gesamte Handler-Body läuft in einem try-catch.
- Tritt eine Exception auf (z. B. `ObjectDisposedException` bei `PseudoConsoleSession.Dispose()` durch parallelen Zugriff), wird sie geloggt; der Handler wird **nicht** erneut ausgeführt und die Exception wird **nicht** weitergeworfen.
- `_handles.TryRemove` stellt sicher, dass jede Aufräum-Aktion pro Aufgabe genau einmal ausgeführt wird, auch wenn der Handler theoretisch mehrfach aufgerufen werden könnte.

**Umsetzung:** `KiAusfuehrungsService.HandleProcessExited` (gemeinsame Implementierung für `StartCliAsync` und `StartWithPseudoConsoleAsync`).

---

## Event-Abonnenten von `CliProcessStatusChanged` sind einzeln isoliert

**Beschreibung:** `CliProcessStatusChanged` ist ein Multicast-Delegate mit mehreren Abonnenten (`CliProcessManager`, `TaskDetailViewModel`). Ein Fehler bei einem Abonnenten darf die Benachrichtigung der übrigen Abonnenten nicht verhindern.

**Bedingungen:**
- `CliProcessStatusChanged?.Invoke(aufgabeId, status)` wird ausgelöst.

**Verhalten:**
- Jeder Abonnent kapselt seine eigene Verarbeitungslogik in try-catch statt sich auf den Aufrufer zu verlassen.
- Ein Fehler bei einem Abonnenten wird dort geloggt; die .NET-Multicast-Invoke-Kette wird dadurch nicht unterbrochen, weil der fehlerhafte Abonnent die Exception selbst abfängt statt sie aus seinem Handler herauszulassen.

**Umsetzung:** `CliProcessManager.OnCliProcessStatusChanged`, `TaskDetailViewModel.OnCliProcessStatusChanged`.

---

## Heartbeat-Serialisierung ist auf Aufgaben-Ebene beschränkt

**Beschreibung:** Überlappende Heartbeat-Timer-Ticks derselben Aufgabe müssen serialisiert werden; Heartbeats unterschiedlicher Aufgaben dürfen sich dabei nicht gegenseitig blockieren.

**Bedingungen:**
- Für eine Aufgabe läuft ein Heartbeat-Timer (`CliProcessManager.StartHeartbeat`).

**Verhalten:**
- Jede Aufgabe erhält beim Start des Heartbeats ein eigenes `SemaphoreSlim(1, 1)`.
- `AktualisierungAsync` wartet auf das Semaphore der jeweiligen Aufgabe, nicht auf ein globales Semaphore.
- Beim Stoppen des Heartbeats (`StopHeartbeat`) wird das zugehörige Semaphore aus dem Dictionary entfernt und disposed; ein `Release()`-Aufruf danach wird über `catch (ObjectDisposedException)` abgefangen.

**Umsetzung:** `CliProcessManager._updateSemaphores` (`ConcurrentDictionary<Guid, SemaphoreSlim>`), `CliProcessManager.AktualisierungAsync`.

---

## Native ConPTY-Ressourcen werden bei Fehlern vollständig freigegeben

**Beschreibung:** Schlägt der Aufbau der ConPTY-Ein-/Ausgabe-Streams oder des zugehörigen Prozesses fehl, dürfen keine nativen Handles offen bleiben.

**Bedingungen:**
- Eine Exception tritt zwischen der Erstellung eines nativen Handles (Pipe, PseudoConsole, Prozess-Handle) und dem erfolgreichen Zusammenbau der `PseudoConsoleSession` auf.

**Verhalten:**
- Bereits erstellte `FileStream`-Instanzen werden im catch-Block disposed, bevor die Exception erneut geworfen wird.
- Schlägt der Prozessstart selbst fehl, wird die `PseudoConsole` disposed; schlägt die Ermittlung des `Process`-Objekts fehl, wird zusätzlich das native Win32-Prozess-Handle geschlossen.

**Umsetzung:** `KiAusfuehrungsService.CreatePseudoConsoleSession`, `KiAusfuehrungsService.StartPseudoConsoleProcess`.
