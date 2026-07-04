# Tasks: Parallele CLI-Ausführungen — ReadLoop in Service-Layer

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests | Reproduktions-Test schreiben: Pipe-Blockade bei View-Wechsel simulieren | Offen | — |
| 2 | Logik | `PseudoConsoleSession`: Feld `_readCts` (CancellationTokenSource) hinzufügen | Offen | — |
| 3 | Logik | `PseudoConsoleSession`: Feld `_readLoopTask` hinzufügen | Offen | — |
| 4 | Logik | `PseudoConsoleSession`: Event `BufferChanged` hinzufügen | Offen | — |
| 5 | Logik | `PseudoConsoleSession`: `private async Task ReadLoopAsync()` implementieren (ReadLoop-Logik aus TerminalControl kopieren und anpassen) | Offen | — |
| 6 | Logik | `PseudoConsoleSession`: Konstruktor anpassen — ReadLoop starten nach Initialisierung | Offen | — |
| 7 | Logik | `PseudoConsoleSession.Dispose()`: `_readCts.Cancel()`, auf Task warten, Streams schließen | Offen | — |
| 8 | UI | `TerminalControl`: Unloaded-Event-Handler entfernen (Zeile 51–56) | Offen | — |
| 9 | UI | `TerminalControl`: Felder `_readCts`, `_readLoopTask` entfernen | Offen | — |
| 10 | UI | `TerminalControl`: Feld `_currentSession` hinzufügen zum Tracking alte Session | Offen | — |
| 11 | UI | `TerminalControl.OnSessionChanged()`: Alte Event-Handler deregistrieren, neue registrieren | Offen | — |
| 12 | UI | `TerminalControl`: Neue Methode `private void OnBufferChanged()` implementieren | Offen | — |
| 13 | UI | `TerminalControl`: `ReadLoopAsync()`-Methode entfernen oder deprecated markieren | Offen | — |
| 14 | Tests | Unit-Test `OnSessionChanged_RegistersBufferChangedHandler()` schreiben | Offen | — |
| 15 | Tests | Unit-Test `OnSessionChanged_ToNewSession_DeregistersOldHandler()` schreiben | Offen | — |
| 16 | Tests | Unit-Test `OnSessionChanged_ToNull_DeregistersAllHandlers()` schreiben | Offen | — |
| 17 | Tests | Bestehenden Test `ReadLoopAsync_WhenControlUnloaded_StopsReading()` anpassen oder entfernen | Offen | — |
| 18 | Tests | Unit-Test `ReadLoopAsync_WithException_LogsAndContinues()` in PseudoConsoleSessionTests schreiben | Offen | — |
| 19 | Tests | Unit-Test `ReadLoopAsync_CancellationToken_GracefulShutdown()` in PseudoConsoleSessionTests schreiben | Offen | — |
| 20 | Tests | Unit-Test `SessionDispose_CancelsReadLoop()` in PseudoConsoleSessionTests schreiben | Offen | — |
| 21 | Logik | `KiAusfuehrungsService.StartWithPseudoConsoleAsync()`: ReadLoop-Start sicherstellen (Kommentar/Dokumentation) | Offen | — |
| 22 | Logik | `KiAusfuehrungsService.HandleProcessExited()`: `session.Dispose()` vor Entfernung aufrufen | Offen | — |
| 23 | Logik | `KiAusfuehrungsService.Dispose()`: Für alle Sessions `session.Dispose()` aufrufen | Offen | — |
| 24 | Logik | `KiAusfuehrungsService`: Debug-Level Logging beim Session-Start/Stop/Cleanup erweitern | Offen | — |
| 25 | Tests | Unit-Test `KiAusfuehrungsService_HandleProcessExited_DisposesSession()` schreiben | Offen | — |
| 26 | Tests | Unit-Test `KiAusfuehrungsService_Dispose_CancelsAllSessions()` schreiben | Offen | — |
| 27 | Logik | App-Shutdown: Überprüfung dass `KiAusfuehrungsService.Dispose()` aufgerufen wird | Offen | — |
| 28 | Logik | App-Shutdown: Shutdown-Handler implementieren (falls nicht automatisch aufgerufen) | Offen | — |
| 29 | Tests | Unit-Test `ParallelSessions_NoBufferInterference()` schreiben | Offen | — |
| 30 | Tests | Unit-Test `ViewWechsel_BufferErhält()` schreiben | Offen | — |
| 31 | E2E | E2E-Test `E2E_ParallelCliExecution()` schreiben (zwei Aufgaben parallel, Navigation) | Offen | — |
| 32 | E2E | E2E-Test `E2E_AppShutdownWithRunningProcesses()` schreiben | Offen | — |
| 33 | Dokumentation | `KiAusfuehrungsService`: Inline-Dokumentation erweitern (Kommentare zu Session-Lifecycle) | Offen | — |
| 34 | Dokumentation | `PseudoConsoleSession`: Inline-Dokumentation erweitern (ReadLoop, BufferChanged, Dispose) | Offen | — |
| 35 | Dokumentation | `TerminalControl`: Inline-Dokumentation aktualisieren (Event-Binding statt ReadLoop-Management) | Offen | — |
| 36 | Integrationstests | Integrations-Test Session-Cleanup und Memory-Management schreiben (10+ Sessions) | Offen | — |
