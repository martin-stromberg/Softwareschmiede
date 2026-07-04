← [Zurück zur Übersicht](index.md)

# Stabilität & Fehlerbehandlung — Beschreibung

## Zweck

Als WPF-Desktopanwendung läuft die Softwareschmiede dauerhaft im Vordergrund des Anwenders. Eine unbehandelte Exception auf einem beliebigen Thread — dem UI-Thread, einem Hintergrund-Task, einem Timer-Callback oder einem Prozess-Event — konnte die gesamte Anwendung unkontrolliert beenden und laufende Aufgaben (CLI-Ausführungen) abbrechen. Diese Fehlerbehandlung sorgt dafür, dass Fehler an allen bekannten Ausfallstellen abgefangen, vollständig geloggt und wo möglich ohne Abbruch der Anwendung behandelt werden.

## Funktionsweise

Drei globale Exception-Handler werden beim Start der Anwendung registriert (`App.OnStartup`) und decken zusammen alle Ausführungskontexte ab: den WPF-UI-Thread, alle sonstigen Threads (inklusive ThreadPool) und unbeobachtete Fehler in Fire-and-Forget-Tasks.

Alle bewusst nicht abgewarteten (Fire-and-Forget) asynchronen Aufrufe verwenden die Erweiterungsmethode `AsyncTaskExtensions.SafeFireAndForget`, die Exceptions des Tasks über einen `ILogger` protokolliert, statt sie unbeobachtet zu lassen oder zum Aufrufer zu propagieren.

Der `Process.Exited`-Handler von `KiAusfuehrungsService` — sowohl für den klassischen als auch den ConPTY-basierten CLI-Start — läuft vollständig in einer zentralen, try-catch-geschützten Methode (`HandleProcessExited`), damit ein Fehler in einem Teilschritt (z. B. beim Dispose einer bereits geschlossenen `PseudoConsoleSession`) nicht die Ausführung der übrigen Schritte oder die Benachrichtigung anderer Event-Abonnenten verhindert.

Der Heartbeat-Mechanismus, der den Bearbeitungsfortschritt periodisch in der Datenbank aktualisiert, verwendet pro Aufgabe eine eigene Sperre (`SemaphoreSlim`), damit sich überlappende Timer-Ticks nur innerhalb derselben Aufgabe serialisieren — Heartbeats unabhängiger Aufgaben blockieren sich nicht gegenseitig.

Beim Aufbau der ConPTY-Ein-/Ausgabe-Streams (`CreatePseudoConsoleSession`) werden bei einem Fehler bereits erzeugte `FileStream`-Instanzen sauber freigegeben, statt native Handles offen zu lassen.

Im `TerminalControl` wird der Lesevorgang aus dem ConPTY-Output-Stream (`ReadLoopAsync`) zusätzlich von einem generischen `catch (Exception)` abgesichert; der zugehörige Hintergrund-Task wird in `_readLoopTask` gespeichert und über `SafeFireAndForget` überwacht, sodass auch dort unbeobachtete Fehler protokolliert werden.

## Beispiele

- Eine Exception, die tief in einem Timer-Callback für Heartbeat-Updates auftritt (z. B. eine gesperrte SQLite-Datenbank), wird über `SafeFireAndForget` als `LogError` protokolliert. Die Anwendung läuft weiter, der nächste Timer-Tick versucht es erneut.
- Wirft `PseudoConsoleSession.Dispose()` beim Beenden eines ConPTY-Prozesses eine `ObjectDisposedException` (z. B. durch gleichzeitigen Zugriff), fängt `HandleProcessExited` diese ab, loggt sie und meldet den Prozess trotzdem korrekt als beendet.
- Schlägt die Initialisierung von `CliProcessManager` beim Anwendungsstart fehl, wird dies geloggt; die Anwendung startet dennoch, allerdings ohne CLI-Funktionalität.
- Wirft der ANSI-Parser oder das Rendering im Terminal-Lesevorgang eine unerwartete Exception, wird sie geloggt und die Leseschleife endet geordnet, statt die Anwendung abstürzen zu lassen.

## Einschränkungen

- `DispatcherUnhandledException` setzt für **jede** Exception `e.Handled = true` — es gibt keine Unterscheidung nach Exception-Typ (z. B. `OutOfMemoryException`). Dies kann dazu führen, dass die Anwendung nach einem schwerwiegenden, eigentlich fatalen Fehler in einem inkonsistenten Zustand weiterläuft. Das vollständige Exception-Logging ist die einzige Absicherung, um solche Fälle nachträglich zu erkennen.
- `AppDomain.CurrentDomain.UnhandledException` kann den Prozessabbruch in der Regel nicht verhindern (`IsTerminating` ist bei .NET auf Nicht-UI-Threads meist `true`) — der Handler dient ausschließlich der Diagnose (Logging vor dem Absturz), nicht der Fehlerbehebung.
- `SafeFireAndForget` loggt Fehler, propagiert sie aber nie zum Aufrufer zurück. Aufrufende Codestellen erfahren nicht direkt, ob eine Fire-and-Forget-Operation fehlgeschlagen ist — Diagnose erfolgt ausschließlich über die Logs.
- Es werden keine Crash-Dumps oder Mini-Dumps erzeugt; die einzige Diagnosequelle ist die Serilog-Protokolldatei.
