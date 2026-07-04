# Anforderungsübersetzung: Parallele Arbeiten — Task 83d86a4e-9c71-4734-99eb-d88e8107f4aa

## Fachliche Zusammenfassung

Das Terminal-System startet CLI-Prozesse via Windows Pseudo Console (ConPTY) und nutzt die `TerminalControl`-View, um Prozessausgabe zu rendern. Derzeit wird die `ReadLoopAsync`-Aufgabe (die kontinuierlich Bytes aus der Output-Pipe liest) durch einen `CancellationTokenSource` abgebrochen, wenn das `TerminalControl` aus der visuellen Baumstruktur entfernt wird (Unloaded-Event). Dies führt dazu, dass:

1. Der Leseprozess stoppt, wenn die Aufgabenseite nicht mehr angezeigt wird
2. Die Output-Pipe wird nicht mehr geleert
3. Der Systemipuffer für die Pipe kann sich füllen und blockiert den Prozess
4. Mehrere parallele CLI-Ausführungen sind dadurch problematisch, da beim Wechsel zur Aufgabenseite die ReadLoop für andere Aufgaben unterbrochen bleibt

**Ziel:** Der CLI-Prozess soll unabhängig davon weiterlaufen und Ausgabe produzieren, ob die Aufgabenseite gerade angezeigt wird oder nicht. Parallele Ausführungen mehrerer CLI-Prozesse sollen möglich sein, auch wenn nur eine Aufgabe angezeigt wird.

## Betroffene Klassen und Komponenten

### Logikklassen / Services
- **`TerminalControl`** (`src/Softwareschmiede.App/Controls/TerminalControl.cs`): 
  - Aktuell: Bricht `ReadLoopAsync` ab bei `Unloaded`-Event
  - Muss geändert werden: ReadLoop läuft unabhängig von Control-Lebenszyklen

- **`PseudoConsoleSession`** (`src/Softwareschmiede.Infrastructure/Terminal/PseudoConsoleSession.cs`):
  - Verwaltet Prozess, InputStream, OutputStream und Runtime-Status
  - Kandidat für Trennung von Lifecycle-Management

### UI-Komponenten / Views
- **`TaskDetailView.xaml.cs`**: Bindet `TerminalControl.Session`-Property und empfängt Sitzungen aus `TaskDetailViewModel`
- **`TaskDetailViewModel`**: Empfängt `PseudoConsoleSessionGestartet`-Event und übergibt Session an View

### Tests
- **`TerminalControlTests`**: Tests für ReadLoop-Verhalten bei verschiedenen View-Lifecycles
- Neue Tests für parallele Sitzungsverwaltung

## Implementierungsansatz

### Problembeschreibung
Der aktuelle `Unloaded`-Handler (Zeile 51–56 in `TerminalControl.cs`) bricht die ReadLoop ab:
```csharp
Unloaded += (_, _) =>
{
    _readCts?.Cancel();    // <-- Problem
    _readCts?.Dispose();
    _readCts = null;
};
```

Das führt zu:
1. **Output-Pipe-Blockade**: Output wird nicht mehr gelesen, die Pipe füllt sich, der Prozess blockiert
2. **Prozessblockade**: Der CLI-Prozess wird de facto angehalten, da er nicht mehr Ausgabe schreiben kann
3. **Einzelaufgaben-Limitation**: Parallel laufende Prozesse anderer Aufgaben können blockiert werden

### Lösungsansatz
Es sind mehrere technische Optionen zu prüfen:

**Option A: Entkopplung der ReadLoop vom Control-Lifecycle**
- Die ReadLoop läuft in `PseudoConsoleSession` weiter, nicht gebunden an `TerminalControl`-Lifecycle
- `TerminalControl` entfernt den `Unloaded`-Handler oder nimmt ihn nicht mehr ernst
- `TerminalControl.Session`-Wechsel triggert weiterhin einen neuen `ReadLoopAsync` falls keine aktive ReadLoop in der Session läuft
- **Vorteil**: Prozess läuft wirklich parallel, auch wenn View nicht angezeigt
- **Nachteil**: Speicherverwaltung muss explizit geregelt werden

**Option B: Zentrale ReadLoop pro Sitzung (nicht pro Control)**
- `PseudoConsoleSession` verwaltet selbst die ReadLoop via einen `CliProcessManager` oder ähnlich
- `TerminalControl` registriert sich als "Observer" der Session, wird aber nicht als Lifecycle-Owner betrachtet
- ReadLoop läuft bis Prozess beendet wird oder Sitzung displosed
- **Vorteil**: Klare Verantwortung, Sitzung "besitzt" ihre ReadLoop
- **Nachteil**: Architektur-Umgestaltung erforderlich

**Option C: Asynchrone Pipe-Verwaltung via Task-Pooling**
- Zentrale Service-Instanz (z. B. `CliOutputReaderService`) verwaltet eine Queue aktiver Sitzungen
- Dedizierte Background-Task(s) lesen kontinuierlich aus allen Sitzungs-Pipes
- `TerminalControl` konsumiert nur Ausgabe-Events, triggert aber keine ReadLoop
- **Vorteil**: Skalierbar, mehrere Prozesse laufen wirklich parallel
- **Nachteil**: Komplexere Koordination, neue Komponente erforderlich

### Empfohlenes Vorgehen
1. **Prüfung durchführen**: Test schreiben, der reproduziert, dass Prozess pausiert/blockiert bei View-Wechsel
2. **Option auswählen**: Basierend auf Prüfung und Architektur-Anforderungen
3. **Implementierung**: Änderungen an `TerminalControl` und ggf. `PseudoConsoleSession` oder neuen Services
4. **Testing**: Unit-Tests für parallele ReadLoops, Integrationstests für Multi-Task-Szenarios

## Konfiguration

Keine neue Konfigurationsebene erforderlich. Das Feature ist transparent und nicht benutzerkonfigurierbar.

Optional: Logging-Level für Prozess-Aktivitäten (z. B. `LogLevel.Debug` für ReadLoop-Starts/Stops) um Debugging zu unterstützen.

## Offene Fragen

1. **Ist das Problem reproduzierbar?** Kann durch ein Test-Szenario verifiziert werden, dass der Prozess tatsächlich pausiert/blockiert, wenn die View nicht angezeigt wird?

2. **Speicherverwaltung**: Wie lange sollen alte `PseudoConsoleSession`-Instanzen im Speicher behalten werden, wenn die Aufgabe navigiert wird? Soll der Buffer (`TerminalBuffer`) wiederverwendet werden (aktuell der Fall) oder sollte er bei View-Unload freigegeben werden?

3. **Mehrfaches Ein- und Ausblenden**: Wenn ein Anwender mehrfach zwischen Aufgaben wechselt, soll die Session-Referenz erhalten bleiben? Oder wird jedes Mal eine neue Session erstellt?

4. **Fehlerbehandlung**: Wenn die ReadLoop in einer zentralen Service läuft und ein Fehler auftritt, wie wird dies dem Benutzer mitgeteilt? (Seitenleisten-Status, Fehler-Banner, Protokoll?)

5. **Prozess-Beendigung**: Wenn der Anwender die Anwendung schließt während mehrere Prozesse laufen: Wie wird sichergestellt, dass alle Output-Pipes ordnungsgemäß geschlossen und Prozesse beendet werden?
