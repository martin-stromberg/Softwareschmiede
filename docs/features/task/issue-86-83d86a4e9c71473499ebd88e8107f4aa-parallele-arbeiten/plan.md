# Umsetzungsplan: Parallele CLI-Ausführungen — ReadLoop in Service-Layer

## Übersicht

Die ReadLoop wird aus dem TerminalControl-Lifecycle in die Service-Layer (`PseudoConsoleSession`) verlagert. Die ReadLoop läuft ab Prozessstart bis `PseudoConsoleSession.Dispose()`, völlig unabhängig davon, ob ein `TerminalControl` gebunden ist oder zwischen Aufgaben navigiert wird. `TerminalControl` wird zu einem reinen Renderer: Es reagiert auf ein `BufferChanged`-Event der Session und invalidiert seine visuelle Ausgabe; die ReadLoop-Verwaltung (`_readCts`, Unloaded-Handler) wird entfernt. Dies ermöglicht parallele CLI-Prozesse, die weiterlaufen, auch wenn ihre Aufgabenseite nicht angezeigt wird.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| ReadLoop-Lifecycle | ReadLoop läuft in `PseudoConsoleSession` (Service-Layer), nicht in `TerminalControl` (UI-Layer) | Option A (Entkopplung): Minimal invasiv, nutzt bestehende Service-Struktur (`KiAusfuehrungsService` verwaltet Sessions), Speicherverwaltung bleibt an Prozess-Lifecycle gebunden, nicht UI-Control-Lifecycle. Alternativoption B (zentrale ReadLoop-Verwaltung) würde mehr Architektur-Umgestaltung erfordern. |
| ReadLoop-Start | ReadLoop startet in `PseudoConsoleSession`-Konstruktor oder unmittelbar in `KiAusfuehrungsService.StartCliAsync()` nach Session-Erstellung | Sicherstellung, dass ReadLoop läuft, sobald Prozess läuft, auch wenn noch kein TerminalControl gebunden ist. |
| UI-Update-Mechanismus | `TerminalControl` abonniert `PseudoConsoleSession.BufferChanged`-Event und ruft `Dispatcher.InvokeAsync(InvalidateVisual)` auf | Entkopplung UI-Rendering von ReadLoop-Logik; jeder Chunk-Verarbeitung triggert Event, UI wird lazy aktualisiert. |
| Unloaded-Verhalten | Unloaded-Handler in `TerminalControl` wird entfernt; ReadLoop wird NICHT gecancelt bei Control-Unload | ReadLoop läuft unabhängig weiter in Session, kann von anderem TerminalControl wiederaufgenommen werden. |
| Session-Lebenszyklusende | `PseudoConsoleSession.Dispose()` beendet die ReadLoop (cancelled CancellationTokenSource) | Saubere Beendigung nach Prozess-Ende, keine verwaisten ReadLoops. |

## Programmabläufe

### CLI-Start mit paralleler ReadLoop

1. Benutzer startet CLI auf Aufgabenseite (klickt Button)
2. `TaskDetailViewModel.StartCliAndUpdateStateAsync()` wird aufgerufen
3. `KiAusfuehrungsService.StartCliAsync()` wird aufgerufen (mit AufgabeId, Plugin, RepoPath, optionale Parameter)
4. Service ruft `plugin.StartCliAsync()` oder `plugin.StartWithPseudoConsoleAsync()` auf
5. Plugin erstellt `PseudoConsoleSession` via `PseudoConsoleSession`-Konstruktor (mit Prozess, Input-/Output-Streams)
6. **`PseudoConsoleSession`-Konstruktor startet die ReadLoop** (startet `ReadLoopAsync()` als Background-Task, mit eigenem `CancellationTokenSource`)
7. Service speichert `CliProcessHandle` in `_handles` (ConcurrentDictionary)
8. Service feuert `CliProcessStatusChanged(aufgabeId, CliProcessStatus.Gestartet)`-Event
9. `TaskDetailViewModel.OnCliProcessStatusChanged()` setzt `IsCliRunning = true`
10. `TaskDetailViewModel` feuert `PseudoConsoleSessionGestartet(session)`-Event
11. `TaskDetailView.DataContextChanged` oder Event-Handler ruft `TerminalControl.Session = session` auf
12. **`TerminalControl.OnSessionChanged()`** wird aufgerufen:
    - Alte ReadLoop-Abonnements werden deregistriert (falls vorhanden)
    - **Neue EventHandler: `TerminalControl` abonniert `session.BufferChanged` Event** (nicht ReadLoopAsync!)
    - `TerminalControl` setzt seine `_buffer`-Referenz auf `session.Buffer`
    - `InvalidateVisual()` wird aufgerufen für initiales Rendering
13. **ReadLoop in `PseudoConsoleSession` läuft weiter asynchron:**
    - Liest Bytes aus `OutputStream` (blockierend bis Bytes verfügbar oder CancellationToken cancelled)
    - Parsed Bytes mit `AnsiSequenceParser.Parse()`
    - Wendet Events auf `Buffer` an via `_buffer.Apply(evt)`
    - **Feuert `BufferChanged`-Event nach jeder erfolgreichen Chunk-Verarbeitung**
    - Fehler werden geloggt (Debug-Level), ReadLoop läuft weiter oder beendet sauber bei OperationCanceledException
14. **`TerminalControl.OnBufferChanged()` wird aufgerufen** (Event-Handler der Session):
    - Ruft `Dispatcher.InvokeAsync(InvalidateVisual)` auf
    - Nächster `OnRender()` zeichnet aktuellen Buffer-Zustand
15. Prozess läuft im OS unabhängig weiter, auch wenn Benutzer zu anderer Aufgabe navigiert

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `PseudoConsoleSession`, `TerminalControl`, `TaskDetailViewModel`, `TaskDetailView`

### View-Wechsel (Aufgabenseite verborgen / zu anderer Aufgabe gewechselt)

1. Benutzer navigiert zu anderer Aufgabe (via Navigation oder TaskList-Klick)
2. `TaskDetailView` wird mit neuem `TaskDetailViewModel` gebunden oder erkennt `DataContextChanged`
3. **`TerminalControl.OnSessionChanged()`** wird aufgerufen mit neuer (oder `null`) Session:
   - Alte `BufferChanged`-Event-Abonnement wird deregistriert (alte Handler entfernt)
   - Falls neue Session nicht `null`: Neue `BufferChanged`-Event-Abonnement wird registriert
   - `_buffer`-Referenz wird aktualisiert
   - `InvalidateVisual()` wird aufgerufen
4. **Kritisch: Alte `PseudoConsoleSession` läuft WEITER in `KiAusfuehrungsService._handles`:**
   - ReadLoop der alten Session läuft weiter, liest OutputStream, updates Buffer
   - `BufferChanged`-Event wird weiterhin gefeuert, aber **kein TerminalControl abboniert es mehr** (keine UI-Updates für unsichtbare Session)
   - Daten werden im Buffer gepuffert, gehen nicht verloren
5. **Neue `PseudoConsoleSession` beginnt zu rendern:**
   - ReadLoop läuft in neuer Session, `BufferChanged`-Event wird gefeuert
   - `TerminalControl` abboniert das Event und rendert
6. Beide Prozesse laufen parallel weiter, nur einer wird angezeigt

Beteiligte Klassen/Komponenten: `TaskDetailView`, `TerminalControl`, `PseudoConsoleSession`, `KiAusfuehrungsService`

### View-Rückkehr (Aufgabenseite wieder angezeigt)

1. Benutzer navigiert zurück zur ursprünglichen Aufgabe
2. `TaskDetailView` wird mit ursprünglichem `TaskDetailViewModel` gebunden
3. **`TerminalControl.OnSessionChanged()`** wird aufgerufen mit ursprünglicher Session:
   - Neue `BufferChanged`-Event-Abonnement wird registriert
   - `_buffer`-Referenz wird gesetzt
   - `InvalidateVisual()` wird aufgerufen
4. **Puffer ist vollständig erhalten:**
   - `session.Buffer` enthält alle seit View-Verstecken gelesenene Ausgabe
   - ReadLoop hat weitergepuffert, obwohl TerminalControl nicht gebunden war
5. TerminalControl rendert den aktuellen Puffer-Zustand
6. Benutzer sieht komplette Ausgabe-Historie seit Start, keine Lücke

Beteiligte Klassen/Komponenten: `TaskDetailView`, `TerminalControl`, `PseudoConsoleSession`, `KiAusfuehrungsService`

### Session-Dispose (Prozess beendet / CLI gestoppt)

1. CLI-Prozess endet (normal, Fehler, oder Benutzer stoppt via `TaskDetailViewModel.CliStoppenAsync()`)
2. `Process.Exited`-Event wird ausgelöst (wenn normal) oder `StopCliAsync()` wird aufgerufen (wenn manuell)
3. `KiAusfuehrungsService.HandleProcessExited(aufgabeId, process, handle, reason)` wird aufgerufen:
   - Session wird aus `_handles` entfernt
   - `CliProcessStatusChanged(aufgabeId, CliProcessStatus.Gestoppt)`-Event wird gefeuert
4. `TaskDetailViewModel.OnCliProcessStatusChanged()` setzt `IsCliRunning = false`
5. `TaskDetailViewModel` feuert `CliGestoppt`-Event
6. `TaskDetailView` reagiert, setzt `TerminalControl.Session = null`
7. **`TerminalControl.OnSessionChanged(null)`** wird aufgerufen:
   - `BufferChanged`-Event-Abonnement wird deregistriert
   - `InvalidateVisual()` wird aufgerufen
   - Control stoppt Rendering (zeigt zuletzt gepufferte Ausgabe, ändert sich nicht mehr)
8. **`PseudoConsoleSession.Dispose()` wird aufgerufen** (durch Handle-Cleanup in Service oder GC):
   - `_readCts.Cancel()` cancelled die ReadLoop
   - `ReadLoopAsync()` beendet sauber (OperationCanceledException wird geloggt oder erwartet)
   - Streams werden geschlossen (`InputStream.Dispose()`, `OutputStream.Dispose()`)
   - Timer wird beendet
   - Prozess wird disposed (falls nicht bereits beendet)
9. ReadLoop-Fehler beim Dispose werden geloggt (Debug oder Info-Level), propagieren nicht

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `PseudoConsoleSession`, `TerminalControl`, `TaskDetailViewModel`, `TaskDetailView`

### App-Shutdown mit laufenden Prozessen

1. Benutzer schließt App (Window.Close oder App.Shutdown)
2. App-Shutdown-Sequence: Views werden unloaded, ViewModels werden disposed, Services werden disposed
3. `KiAusfuehrungsService.Dispose()` wird aufgerufen (muss durch DI-Container oder explizit in App-Shutdown-Handler geschehen):
   - Iteriert über alle Einträge in `_handles`
   - Für jeden laufenden Prozess: `process.Kill()` oder `process.WaitForExit(timeout)` aufgerufen
   - Für jede `PseudoConsoleSession`: `session.Dispose()` aufgerufen (cancelled ReadLoop, schliesst Streams)
4. Alle ReadLoops werden cancelled, alle Streams geschlossen
5. Alle Prozesse werden beendet
6. Keine Deadlock, keine verwaisten Pipes, sauberer Shutdown

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `PseudoConsoleSession`, `App`, DI-Container

## Neue Klassen

Keine neuen Klassen erforderlich (Option A: Minimal invasiv).

## Änderungen an bestehenden Klassen

### `TerminalControl` (WPF-Control)

- **Entfernte Event-Handler:**
  - `Unloaded`-Event-Handler (Zeilen 51–56 der aktuellen Implementierung)
    - Aktuell: Bricht `_readCts.Cancel()` ab bei Control-Unload
    - Neu: Wird vollständig entfernt; ReadLoop wird nicht mehr von hier kontrolliert
    - Grund: ReadLoop läuft unabhängig in Session weiter

- **Entfernte Methoden / Logik:**
  - `ReadLoopAsync(PseudoConsoleSession, TerminalBuffer, CancellationToken)`: **Privat-Methode wird entfernt oder deprecated**
    - ReadLoop-Logik zieht in `PseudoConsoleSession.ReadLoopAsync()`
    - `TerminalControl` ruft diese nicht mehr auf
  - `OnSessionChanged()` wird angepasst:
    - **Alt:** `_readCts.Cancel()`, dann `ReadLoopAsync()` starten, speichert Task in `_readLoopTask`
    - **Neu:** Deregistriert altes `session.BufferChanged`-Event-Handler, registriert neuen auf neuer Session
    - Grund: TerminalControl ist nur noch Consumer, nicht Lifecycle-Owner

- **Entfernte Felder:**
  - `_readCts`: CancellationTokenSource wird nicht mehr benötigt (war für ReadLoop-Cancellation via Unloaded)
  - `_readLoopTask`: Task-Referenz wird nicht mehr benötigt

- **Neue Event-Handler:**
  - `OnBufferChanged()`: Wird aufgerufen, wenn `session.BufferChanged` Event gefeuert wird
    - Logik: `Dispatcher.InvokeAsync(InvalidateVisual)` aufrufen (oder `InvalidateVisual()` direkt, je nach Dispatcher-Kontext)
    - Registrierung: In `OnSessionChanged()`, wenn neue Session gesetzt wird
    - Deregistrierung: In `OnSessionChanged()`, wenn Session zu `null` wird oder sich ändert

- **Geänderte Methoden:**
  - `OnSessionChanged(PseudoConsoleSession?)`: 
    - Alt: Stoppt alte ReadLoop via `_readCts.Cancel()`, startet neue via `ReadLoopAsync()`
    - **Neu:** 
      - Falls alte Session nicht `null`: `session.BufferChanged -= OnBufferChanged` (deabonnieren)
      - Falls neue Session nicht `null`: `session.BufferChanged += OnBufferChanged` (abonnieren)
      - `_buffer = session?.Buffer` setzen (oder null)
      - `InvalidateVisual()` aufrufen für Neuzeichnung
    - Grund: UI-Binding für Events statt ReadLoop-Verwaltung

### `PseudoConsoleSession` (Service-Klasse)

- **Neue Felder:**
  - `_readCts`: CancellationTokenSource für die ReadLoop
    - Initialisiert im Konstruktor
    - Cancelled in `Dispose()`
  - `_readLoopTask`: Task-Referenz für die ReadLoop
    - Gespeichert nach Start
    - Gewartet in `Dispose()`

- **Neue Methoden:**
  - `private async Task ReadLoopAsync()`: Die ReadLoop-Logik (analog aktueller `TerminalControl.ReadLoopAsync()`)
    - Wird aufgerufen im Konstruktor oder direkt nach Session-Erstellung
    - Liest Bytes aus `OutputStream`, parsed, wendet auf `Buffer` an, feuert `BufferChanged`-Event
    - Läuft bis CancellationToken cancelled wird
    - Fehler werden geloggt (Debug-Level), ReadLoop endet sauber bei OperationCanceledException

- **Neue Events:**
  - `BufferChanged`: EventHandler (oder Action), wird nach jeder Buffer-Update gefeuert
    - Signature: `event EventHandler? BufferChanged` oder ähnlich
    - Gefeuert von: `ReadLoopAsync()` nach `_buffer.Apply(evt)` und nach vorverarbeiteter Chunk-Verarbeitung

- **Geänderte Methoden:**
  - `Dispose()`: 
    - Alt: Disposed `InputStream`, `OutputStream`, Timer, Prozess
    - **Neu:** 
      - `_readCts.Cancel()` aufrufen
      - `if (_readLoopTask != null) await _readLoopTask` warten (mit Timeout, z. B. 5 Sekunden)
      - Danach `_readCts.Dispose()`, `InputStream.Dispose()`, `OutputStream.Dispose()`, etc.
    - Grund: ReadLoop sauber beenden, bevor Streams geschlossen werden

### `KiAusfuehrungsService` (Singleton-Service)

- **Geänderte Methoden:**
  - `StartWithPseudoConsoleAsync()`: 
    - Alt: Erstellt `PseudoConsoleSession`, speichert in Handle
    - **Neu:** Nach Session-Erstellung **ReadLoop wird durch `PseudoConsoleSession`-Konstruktor gestartet** (keine zusätzliche Aktion notwendig)
      - Falls ReadLoop-Start später erfolgt: Optionale Methode `session.StartReadLoopAsync()` aufrufen
    - Grund: ReadLoop ist ab Konstruktion aktiv
  - `StartCliAsync()`: 
    - Alt: Delegiert an `StartWithPseudoConsoleAsync()`
    - **Neu:** Kein Verhalten-Änderung, ReadLoop läuft nach Session-Erstellung

  - `HandleProcessExited()`:
    - Alt: Removed Session aus `_handles`, feuert Event
    - **Neu:** 
      - `session.Dispose()` aufrufen (falls noch nicht disposed)
      - Danach Session aus `_handles` entfernen
      - `CliProcessStatusChanged`-Event feuern
    - Grund: Sauberes Cleanup der ReadLoop vor Session-Entfernung
  - `Dispose()`:
    - Alt: Killed alle Prozesse, disposed Ressourcen
    - **Neu:** 
      - Für alle Sessions in `_handles`: `session.Dispose()` aufrufen (cancelled ReadLoop, schliesst Streams)
      - Danach Prozesse awaiten oder killen
      - `_handles` clearen
    - Grund: Readloops sauber beenden vor App-Exit

- **Neue Dokumentation:**
  - Dokumentation erweitern, dass `HandleProcessExited()` zuverlässig aufgerufen werden muss (z. B. via `Process.Exited`-Event)
  - Dokumentation, dass `Dispose()` beim App-Shutdown aufgerufen werden muss (z. B. über DI-Container oder explizit)

### `TaskDetailView` (WPF-View)

- **Geänderte Methoden:**
  - `DataContextChanged`: Verhalten bleibt — bindet Session an `TerminalControl.Session`
    - Neu: Dokumentation, dass bei View-Wechsel `TerminalControl.OnSessionChanged()` ausgelöst wird (kein Verhalten-Änderung)
    - Grund: Klarstellung für Wartung

- **Keine anderen Änderungen**

### `TaskDetailViewModel` (ViewModel)

- **Keine Code-Änderungen erforderlich**
- **Neue Dokumentation:**
  - Dokumentation erweitern, dass `GetPseudoConsoleSession()` unabhängig vom View-Lifecycle laufen kann
  - Dokumentation, dass Events `PseudoConsoleSessionGestartet` und `CliGestoppt` weiterhin die UI-Binding auslösen

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine (Feature ist transparent, keine Benutzerkonfigurierbarkeit erforderlich).

Optional: Logging-Level für Debug (z. B. `LogLevel.Debug` für ReadLoop-Starts/Stops in `KiAusfuehrungsService` und `PseudoConsoleSession`) um Debugging zu unterstützen.

## Seiteneffekte und Risiken

- **Speicherverwaltung:** Sessions bleiben solange in `KiAusfuehrungsService._handles`, bis Prozess beendet ist — auch wenn View verborgen ist. Dies ist **gewünscht** (parallele Ausführung), aber Speicherleck-Potenzial, falls `HandleProcessExited()` nicht zuverlässig aufgerufen wird.
  - *Mitigation:* Unit-Tests für Session-Cleanup beim Prozess-Ende; Logging beim Session-Cleanup; ggf. Timeout-Mechanismus für "verwaiste" Sessions in Zukunfts-Version.

- **ReadLoop-Cancellation bei Session-Wechsel:** Wenn Control zu neuer Session wechselt, wird keine alte ReadLoop mehr abgebrochen (da sie nicht von TerminalControl verwaltet wird). Dies ist **gewünscht** (ReadLoop läuft in Session weiter), aber bedeutet: Alte Session's ReadLoop läuft asynchron weiter im Hintergrund.
  - *Mitigation:* Dies ist per Designentscheidung akzeptiert; ReadLoop wird nur bei `PseudoConsoleSession.Dispose()` oder `Process.Exit` beendet.

- **Event-Memory-Leak:** Wenn `TerminalControl` mit Sessionen bindet/unbindet, müssen Event-Handler korrekt deregistriert werden, sonst entsteht Memory-Leak (Session hält TerminalControl in Memory).
  - *Mitigation:* In `OnSessionChanged()` explizit alte Handler deregistrieren vor neuer Registrierung; Unit-Tests für Event-Binding schreiben.

- **Rendering-Race-Condition:** Wenn ReadLoop in einer Session Bytes verarbeitet, während TerminalControl von anderer Session rendern, können Race-Conditions entstehen (TerminalControl liest Buffer, während ReadLoop schreibt).
  - *Mitigation:* `TerminalBuffer.Apply()` ist bereits Thread-safe mit Lock; `OnRender()` liest Buffer intern Thread-safe; keine zusätzliche Synchronisierung nötig (bestehender Design ist bereits sicher).

- **Multiple TerminalControl-Instanzen an gleicher Session:** Wenn zwei Controls sich an gleiche Session binden (technisch möglich aber unwahrscheinlich), können beide `InvalidateVisual()` aufrufen.
  - *Mitigation:* Dies ist akzeptabel (beide rendern aktuellen Buffer-Zustand); `BufferChanged`-Event ist Broadcast, mehrere Subscriber möglich.

- **ReadLoop-Fehler bei View verborgen:** Wenn ReadLoop Fehler wirft, während View nicht angezeigt wird, sieht Benutzer keinen Error (kein UI-Update möglich). Error wird geloggt, aber nicht UI-sichtbar.
  - *Mitigation:* Dies ist akzeptiert (nur Logging, keine UI-Änderung in dieser Version, wie empfohlen); in Zukunft: Status-Banner oder Fehler-Indikator in Liste.

- **Bestehende Tests brechen:** `TerminalControlTests` hat Tests, die Unloaded-Verhalten oder `ReadLoopAsync`-Direktaufrufe prüfen. Diese müssen angepasst werden.
  - *Mitigation:* Tests für neues Verhalten schreiben; alte Tests anpassen oder entfernen (s. Tests-Sektion).

## Umsetzungsreihenfolge

1. **Test schreiben: Reproduktion der Pipe-Blockade bei View-Wechsel**
   - Voraussetzungen: `TerminalControlTests`-Infrastruktur vorhanden, Stream-Test-Doppelgänger vorhanden
   - Beschreibung: 
     - Test-Szenario: Zwei `ReadLoopAsync`-Aufgaben (simuliert zwei Sessions), erste liest, dann wird sie "unloaded" (`_readCts.Cancel()` aufgerufen), zweite versucht zu lesen
     - Erwartung (aktuell fehlerhaft): Erste ReadLoop stoppt, Pipe füllt sich, Prozess blockiert
     - Nach Fix: Erste ReadLoop läuft weiter unabhängig (oder ist in Session), Pipe wird geleert
   - Ziel: Regressions-Nachweis, dass Issue-86 existiert und Fix funktioniert

2. **PseudoConsoleSession: ReadLoop-Logik und BufferChanged-Event hinzufügen**
   - Voraussetzungen: `PseudoConsoleSession`-Code vorhanden, Test aus Schritt 1
   - Beschreibung:
     - Neue Felder `_readCts`, `_readLoopTask` hinzufügen
     - Neue Event `BufferChanged` hinzufügen
     - `private async Task ReadLoopAsync()` aus `TerminalControl.ReadLoopAsync()` kopieren und anpassen
     - Im Konstruktor oder direkt nach Konstruktion: `_ = ReadLoopAsync(_readCts.Token)` starten (Fire-and-forget mit Fehler-Logging)
     - `Dispose()` anpassen: `_readCts.Cancel()`, auf `_readLoopTask` warten (mit Timeout), dann Streams schließen
   - Test-Validierung: Reproduktions-Test soll jetzt grün werden

3. **TerminalControl: Unloaded-Handler entfernen, OnSessionChanged anpassen, BufferChanged-Handler hinzufügen**
   - Voraussetzungen: PseudoConsoleSession-Änderungen vorhanden
   - Beschreibung:
     - `Unloaded`-Event-Handler (Zeile 51–56) vollständig entfernen
     - Felder `_readCts`, `_readLoopTask` entfernen
     - `ReadLoopAsync()`-Methode entfernen (oder als deprecated markieren)
     - `OnSessionChanged()` umschreiben:
       - Falls `_currentSession != null`: `_currentSession.BufferChanged -= OnBufferChanged`
       - Falls neue `session != null`: `session.BufferChanged += OnBufferChanged`
       - `_buffer = session?.Buffer`
       - `InvalidateVisual()` aufrufen
     - Neue Methode `private void OnBufferChanged(object? sender, EventArgs e)`:
       - Ruft `Dispatcher.InvokeAsync(InvalidateVisual)` auf (oder `InvalidateVisual()` direkt)
     - `_currentSession` Feld hinzufügen zum Tracking alte Session für Deregistrierung
   - Test-Validierung: TerminalControl-Tests sollten weiterhin erfolgreich sein (angepasste Tests); Reproduktions-Test grün

4. **TerminalControlTests: Unloaded-Tests anpassen/entfernen, neue Tests hinzufügen**
   - Voraussetzungen: TerminalControl-Änderungen vorhanden
   - Beschreibung:
     - Test `ReadLoopAsync_WhenControlUnloaded_StopsReading()` (falls vorhanden): **Entfernen oder umschreiben**
       - Aktuelles Verhalten ist nicht mehr zutreffend; ReadLoop läuft weiter
       - Alternativ: Test für neues Verhalten schreiben (ReadLoop läuft weiter bei Unloaded)
     - Neue Test `OnSessionChanged_RegistersBufferChangedHandler()`:
       - Session wird auf Control gesetzt, prüfe dass `BufferChanged` Handler registriert ist
     - Neue Test `OnSessionChanged_ToNewSession_DeregistersOldHandler()`:
       - Session A wechselt zu Session B, prüfe dass Handler für A deregistriert, für B registriert
     - Neue Test `OnSessionChanged_ToNull_DeregistersAllHandlers()`:
       - Session wird auf null gesetzt, prüfe dass alle Handler deregistriert

5. **ReadLoop-Fehlerbehandlung und Logging testen**
   - Voraussetzungen: PseudoConsoleSession ReadLoop vorhanden, TerminalControl angepasst
   - Beschreibung:
     - Unit-Test `ReadLoopAsync_WithException_LogsAndContinues()`: ReadLoop in Session wirft Exception, muss geloggt werden, ReadLoop beendet sauber
     - Unit-Test `ReadLoopAsync_CancellationToken_GracefulShutdown()`: CancellationToken.Cancel() wird aufgerufen, ReadLoop beendet sauber, keine ungehandhabte Exception
   - Ziel: Robustheit der ReadLoop sicherstellen

6. **KiAusfuehrungsService: ReadLoop-Start und Session-Cleanup anpassen**
   - Voraussetzungen: PseudoConsoleSession ReadLoop vorhanden
   - Beschreibung:
     - `StartWithPseudoConsoleAsync()` anpassen: Falls ReadLoop nicht im Konstruktor gestartet wird, `session.StartReadLoopAsync()` aufrufen (oder kommentar hinzufügen dass Konstruktor es tut)
     - `HandleProcessExited()` anpassen: `session.Dispose()` aufrufen bevor Session aus `_handles` entfernt wird
     - `Dispose()` anpassen: Für alle Sessions `session.Dispose()` aufrufen
     - Logging erweitern: Debug-Level beim Session-Start, Stop, Cleanup
   - Test-Validierung: KiAusfuehrungsService-Tests sollten Cleanup und Event-Firing prüfen

7. **KiAusfuehrungsService.Dispose() aufrufen bei App-Shutdown sicherstellen**
   - Voraussetzungen: KiAusfuehrungsService-Änderungen vorhanden, App-Startup-Infrastruktur
   - Beschreibung:
     - Prüfung: Wird `KiAusfuehrungsService.Dispose()` beim App-Shutdown aufgerufen?
       - Wenn ja (via DI-Container `DisposeAsync()` oder ähnlich): Dokumentation erweitern, fertig
       - Wenn nein: **Shutdown-Handler in App-Klasse registrieren oder DI-Container konfigurieren**
         - Option 1: `App.xaml.cs` in `MainWindow.Closed` oder `App.Exit` Event `_kiService.Dispose()` aufrufen
         - Option 2: DI-Container konfigurieren, dass Service bei App-Exit disposed wird (depends on Container)
     - Ziel: Graceful-Shutdown, keine verwaisten Prozesse
   - Test-Validierung: Manueller Test oder E2E-Test (s. unten)

8. **Unit-Tests: Parallele Sessions und View-Wechsel**
   - Voraussetzungen: TerminalControl, PseudoConsoleSession, KiAusfuehrungsService angepasst
   - Beschreibung:
     - Test `ParallelSessions_NoBufferInterference()`: Zwei Sessions mit verschiedenen Buffern, beide rendern parallel, keine Vermengung
     - Test `ViewWechsel_BufferErhält()`: Control wechselt von Session A zu B, wechselt zurück; Buffer-Inhalt von A ist unverändert
     - Test `SessionDispose_CancelsReadLoop()`: Session.Dispose() wird aufgerufen, ReadLoop beendet sauber, Streams geschlossen
   - Ziel: Core-Logik validieren

9. **E2E-Test: Parallele CLI-Prozesse**
   - Voraussetzungen: App läuft, alle Änderungen implementiert
   - Beschreibung:
     - Test-Szenario: Zwei Aufgaben (A, B) mit CLI starten; beide parallel laufen
       - Task A: Start CLI (z. B. `echo hello` in Loop alle 500ms)
       - Task B: Start CLI (z. B. `echo world` in Loop alle 500ms)
       - Benutzer navigiert A → B → A → B
       - Erwartung: Beide Prozesse produzieren kontinuierlich Ausgabe, Ausgabe-Buffer erhalten, keine Blockade
     - Validierung: Ausgabe-Logs oder manuelle Überprüfung, keine Process-Blockade
   - Ziel: Feature-Akzeptanz-Test

10. **E2E-Test: App-Shutdown mit laufenden Prozessen**
    - Voraussetzungen: Graceful-Shutdown konfiguriert (Schritt 7)
    - Beschreibung:
      - Test-Szenario: Mehrere CLI-Prozesse starten, dann App schließen (während Prozesse laufen)
      - Erwartung: App shutdown sauber, keine Deadlock, keine Fehler in Logs, Prozesse werden beendet
      - Validierung: Log-Inspektion auf Fehler, Process-Status-Überprüfung
    - Ziel: Graceful-Shutdown validieren

11. **Dokumentation: KiAusfuehrungsService und PseudoConsoleSession**
    - Voraussetzungen: Alle Code-Änderungen vorhanden
    - Beschreibung:
      - KiAusfuehrungsService: Dokumentation erweitern:
        - Sessions werden in `_handles` gespeichert, solange Prozess läuft
        - `HandleProcessExited()` muss zuverlässig aufgerufen werden
        - `Dispose()` muss beim App-Shutdown aufgerufen werden
        - Debug-Logging für Sessions
      - PseudoConsoleSession: Dokumentation erweitern:
        - ReadLoop läuft ab Konstruktion bis Dispose
        - `BufferChanged`-Event wird nach jeder Buffer-Update gefeuert
        - `Dispose()` beendet ReadLoop sauber
    - Ziel: Klarheit für Wartung und zukünftige Entwickler

12. **Integrations-Test: Session-Cleanup und Memory-Management**
    - Voraussetzungen: Alle Änderungen vorhanden
    - Beschreibung:
      - Test: 10 Sessions starten und enden, prüfe dass `_handles` geleert wird
      - Test: Eine Session läuft lange, prüfe dass Memory nicht unbegrenzt wächst
      - Test: App startet 50 Prozesse, schließt App; prüfe Cleanup
    - Ziel: Speicherleck-Prävention, Skalierbarkeits-Validierung

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ReadLoop_RepeatCallsWithoutCancellation_ContinuesReading()` | TerminalControlTests | Zwei `ReadLoopAsync`-Aufgaben nacheinander für gleiche Session (oder Reproduktions-Test) simuliert parallele Reads |
| `OnSessionChanged_RegistersBufferChangedHandler()` | TerminalControlTests | Session wird gesetzt, `BufferChanged` Handler wird registriert |
| `OnSessionChanged_ToNewSession_DeregistersOldHandler()` | TerminalControlTests | Session A → B wechsel, Handler für A deregistriert, für B registriert |
| `OnSessionChanged_ToNull_DeregistersAllHandlers()` | TerminalControlTests | Session wird null, Handler deregistriert |
| `ReadLoopAsync_WithException_LogsAndContinues()` | PseudoConsoleSessionTests (new) | ReadLoop wirft Exception, muss geloggt werden, beendet sauber |
| `ReadLoopAsync_CancellationToken_GracefulShutdown()` | PseudoConsoleSessionTests | CancellationToken.Cancel(), ReadLoop beendet sauber |
| `SessionDispose_CancelsReadLoop()` | PseudoConsoleSessionTests | Dispose() cancelled ReadLoop, Streams geschlossen |
| `ParallelSessions_NoBufferInterference()` | TerminalControlTests | Zwei Sessions parallel, Buffers interferieren nicht |
| `ViewWechsel_BufferErhält()` | TerminalControlTests | Control wechselt A → B → A, Buffer von A unverändert |
| `KiAusfuehrungsService_HandleProcessExited_DisposesSession()` | KiAusfuehrungsServiceTests | Prozess beendet, Session.Dispose() wird aufgerufen |
| `KiAusfuehrungsService_Dispose_CancelsAllSessions()` | KiAusfuehrungsServiceTests | Service.Dispose() cancelled alle ReadLoops, Streams geschlossen |
| **E2E:** `E2E_ParallelCliExecution()` | E2E-Tests (TaskDetailViewTests oder neu ParallelCliTests) | Zwei Aufgaben mit CLI parallel, Navigation funktioniert, Ausgabe kontinuierlich |
| **E2E:** `E2E_AppShutdownWithRunningProcesses()` | E2E-Tests (AppLifecycleTests) | App shutdown mit laufenden Prozessen, sauberes Ende, keine Errors |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TerminalControlTests.ReadLoopAsync_WhenControlUnloaded_StopsReading()` (falls vorhanden) | Unloaded-Handler entfernt, Test verhalten sich nicht mehr zutreffend; Test umschreiben oder entfernen. Alternative: Test für neues Verhalten schreiben (ReadLoop läuft weiter). |
| `TerminalControlTests.OnSessionChanged_*` | Tests für `OnSessionChanged` müssen geprüft werden; Behavior ändert sich (kein ReadLoop-Start mehr, sondern Event-Binding) |
| `TerminalControlTests` (allgemein) | Tests, die `_readCts` oder `_readLoopTask` direkt verwenden, müssen angepasst werden (diese Felder werden entfernt) |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Zwei Aufgaben mit CLI starten, beide parallel, Navigation zwischen ihnen, Ausgabe kontinuierlich | TaskDetailViewTests oder ParallelCliTests (new) | Anforderung erfüllt: CLI-Prozesse laufen parallel, unabhängig von UI-Anzeige |
| CLI läuft, Aufgabenseite verborgen, wechselt zu anderer Aufgabe, wechselt zurück, Buffer erhalten | TaskDetailViewTests oder ViewNavigationTests (new) | Puffer bleibt erhalten, ReadLoop läuft weiter im Hintergrund, keine Datenverlust |
| App wird geschlossen während mehrere CLI-Prozesse laufen | AppLifecycleTests (new) oder Shutdown-Tests | Graceful-Shutdown: Alle Prozesse beendet sauber, keine Deadlock, keine Fehler |
| CLI-Prozess crasht oder wird gestoppt, Session wird entfernt, Speicher freigegeben | CliProcessManagementTests (new) | Session-Cleanup funktioniert, kein Speicherleck |

Welche bestehenden E2E-Tests sind betroffen?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Tests, die TaskDetailView Navigation simulieren (falls vorhanden) | Falls Tests View-Wechsel während CLI-Ausführung prüfen, müssen Tests validiert werden, dass neue Parallelisierung funktioniert (kein Breaking-Change erwartet) |
| Tests, die App-Shutdown simulieren (falls vorhanden) | Müssen validiert werden, dass `KiAusfuehrungsService.Dispose()` aufgerufen wird; ggf. anpassen wenn Shutdown-Handler hinzugefügt wird |

## Offene Punkte

Keine.

**Begründung:** Alle architektonischen Entscheidungen wurden in den ARGUMENTS clarified und sind oben in den Programmabläufen und Klassen-Änderungen vollständig eingearbeitet:

1. **Reproduzierbarkeit:** Reproduktionstest schreiben (Schritt 1), ReadLoop-Logik validieren
2. **Speicherverwaltung:** Sessions bleiben unbegrenzt bis Prozessende; akzeptiert per Designentscheidung (Option A minimal-invasiv)
3. **Buffer-Erhalt:** Session-Buffer bleibt bei View-Wechsel erhalten, ReadLoop läuft weiter im Hintergrund (Programmablauf View-Rückkehr)
4. **Fehlerbehandlung:** Nur Logging, keine UI-Änderung (ReadLoop-Fehler werden geloggt, propagieren nicht)
5. **Graceful-Shutdown:** `KiAusfuehrungsService.Dispose()` wird überprüft und sichergestellt (Schritt 7, E2E-Test Schritt 10)
6. **Monitoring/Logging:** Debug-Level Logging in Services (Schritt 6, Dokumentation Schritt 11)

Alle Punkte aus requirement.md "Offene Fragen" sind jetzt beantwortet und in den Plan integriert.
