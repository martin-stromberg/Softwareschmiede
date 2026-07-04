# Tests und Hilfsmethoden

## Testklassen

### `TerminalControlTests`
Datei: `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`

Unit-Tests für `TerminalControl`: Exception-Behandlung im Terminal-Lesevorgang (F12–F14).

| Testmethode | Was wird getestet? |
|-------------|-------------------|
| `ReadLoopAsync_WithException_LogsAndDoesNotThrow()` | ReadLoopAsync muss Exceptions außerhalb von ReadAsync intern fangen und nicht propagieren; Fehler wird geloggt |
| `OnSessionChanged_StoresReadLoopTask()` | OnSessionChanged (ausgelöst über Session-Setter) speichert die Task-Referenz des Lesevorgangs in `_readLoopTask` |
| `OnSessionChanged_ReadLoopThrows_LogsErrorViaInjectedLogger()` | Wenn der Output-Stream beim Lesen eine Exception wirft, muss der Fehler über den injizierten Logger protokolliert werden (Verhaltenstest über public Session-Setter) |
| `OnTextInput_WriteThrows_LogsWarning()` | Wenn der Anwender Text schreibt, während die Pipe zum CLI-Prozess geschlossen ist, darf OnTextInput die Exception nicht stillschweigend verwerfen, sondern muss sie loggen |

**Abdeckung:**
- Exception-Behandlung im ReadLoop-Vorgang
- Logging von Read- und Write-Fehlern
- Task-Speicherung bei Session-Änderung

**Nicht abgedeckt (relevant für Issue-86):**
- Verhalten bei parallelen Sessions
- Verhalten bei View-Wechsel (Unloaded-Event) und aktiv laufenden Prozessen
- Verhalten bei mehreren TerminalControl-Instanzen

---

## Hilfsmethoden / Test-Doppelgänger

### `RunOnSta(Action action)`
Hilfsmethode (Zeilen 173–187): Führt eine Aktion auf einem STA (Single-Threaded Apartment) Thread aus. Notwendig, weil WPF-Controls (wie TerminalControl/DispatcherSynchronizationContext) auf STA-Threads laufen müssen.

### Stream-Test-Doppelgänger

| Klasse | Verhalten |
|--------|-----------|
| `SingleByteStream` | Liefert beim ersten Read ein Byte ('A'), beim zweiten wirft OperationCanceledException (simuliert Stream-Ende nach 1 Byte) |
| `ImmediateEofStream` | Liefert beim Lesen sofort 0 Bytes (simuliertes Stream-Ende), ohne Dispatcher-Pumpe |
| `ThrowingStream` | Wirft IOException beim Lesen (simuliert Pipe-Fehler) |
| `WriteThrowingStream` | Wirft IOException beim Schreiben (simuliert geschlossene Pipe zum CLI-Prozess) |

**Verwendung:** Diese Streams werden in `CreateSession()` injiziert, um verschiedene Fehlerszenarien zu testen, ohne echte Prozesse/Pipes zu verwenden.

### `CreateSession(Stream outputStream)` und `CreateSession(Stream inputStream, Stream outputStream)`
Hilfsmethoden (Zeilen 164–171): Erstellen eine Test-`PseudoConsoleSession` mit injizierbaren Input/Output-Streams. Verwendet einen Mock-Prozess (GetCurrentProcess()).

---

## Beobachtete Testlücken bezogen auf Issue-86

- **Keine Tests für parallele Sessions:** Es gibt keine Tests, die mehrere TerminalControl-Instanzen mit verschiedenen Sessions gleichzeitig rendern/lesen.
- **Keine Tests für Unloaded-Verhalten:** Es gibt keinen Test, der prüft, ob die ReadLoop bei Unloaded abgebrochen wird und ob die Pipe blockiert.
- **Keine Tests für View-Wechsel-Szenarien:** TaskDetailView registriert sich via DataContextChanged; es gibt keine Tests für das Verhalten bei schnellem Wechsel zwischen Aufgaben.
- **Keine Integrationstests:** Es gibt keine End-to-End-Tests, die echte CLI-Prozesse parallel laufen lassen und View-Wechsel simulieren.
