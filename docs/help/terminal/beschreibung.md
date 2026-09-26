← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Beschreibung

## Zweck

Das Terminal-System ermöglicht die direkte, interaktive Bedienung von KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI) innerhalb der Softwareschmiede. Die Ausgabe des Prozesses wird in Echtzeit in einem WPF-Control gerendert; Tastatureingaben werden direkt an den Prozess weitergeleitet — der Anwender arbeitet mit dem echten CLI im nativen Kontext, ohne Fenster zu wechseln.

## Funktionsweise

Das System nutzt Windows Pseudo Console (ConPTY) API zum Starten des CLI-Prozesses — oder, wenn kein Pseudo-Terminal verfügbar ist bzw. das Plugin keine PTY-Unterstützung deklariert, ein diagnostiziertes Pipe-Fallback-Backend. Die Anbieter-CLI wird dabei direkt aus der vom Plugin gelieferten Startbeschreibung (`TerminalSessionStartSpec`) gestartet — es gibt keine `cmd.exe`-Zwischenschale mehr, in die der Befehl als simulierte Tastatureingabe getippt wird. Der Prozess-Output wird als Byte-Stream gelesen und durch den `AnsiSequenceParser` in strukturierte Terminal-Ereignisse (Text, Cursor-Bewegung, Farben, Erase-Befehle, Alternate Screen, Scroll-Regionen u. a.) zerlegt. Ein `TerminalBuffer` verwaltet einen 2D-Grid aus `TerminalCell`-Objekten mit Zeichen, Farben und Text-Attributen. Das `TerminalControl` ist ein reiner Renderer, der den `TerminalBuffer` per `DrawingContext` mit monospace-Schriftart darstellt; Tastatureingaben werden durch `KeyToVt100Encoder` in VT100-Escape-Sequenzen konvertiert und in die Prozess-Input-Pipe geschrieben.

Die Leseschleife läuft unabhängig vom `TerminalControl`-Lebenszyklus in `PseudoConsoleSession` selbst — von der Session-Konstruktion bis zur Dispose — damit mehrere CLI-Prozesse parallel weiterlaufen können, auch wenn ihre Aufgabenseite nicht angezeigt wird (Issue-86). Dieselbe Leseschleife legt jeden gelesenen Roh-Chunk im begrenzten `TerminalReplayBuffer` ab (Basis für die Neuanbindung eines Controls an eine laufende Session) und meldet ihn zusätzlich an eine optionale `ITerminalOutputSink`; für Aufgabenläufe ist dort ein `CliOutputProtokollWriter` angebunden, der Ausgabezeilen im Aufgabenprotokoll speichert.

Die Aufgabendetailansicht zeigt den Laufzeitstatus der CLI in der Fusszeile. `PseudoConsoleSession` verfolgt dafür die letzte Ausgabe- und Eingabeaktivität: Frische I/O-Aktivität wird als laufende Ausführung angezeigt (`Laeuft`); bleibt Ausgabe bei weiterhin laufendem Prozess aus, wird nach kurzer Zeit "Wartet auf Eingabe" angezeigt (`WartetAufEingabe`).

Die CLI-Ausgabe ist vertikal scrollbar. `TerminalBuffer` hält dafür bis zu 1000 Scrollback-Zeilen vor; `TerminalControl` stellt den Verlauf zeilenbasiert über den WPF-`ScrollViewer` bereit. Anwender können ältere Ausgaben per Scrollbar, Mausrad oder Page-Scroll lesen. Solange die Ansicht am Ende steht, folgt sie neuer Ausgabe automatisch; nach manuellem Hochscrollen bleibt die Leseposition stabil. Der Eingabefokus bleibt dabei auf dem Terminalbereich, sodass Tastatureingaben und Zwischenablage-Einfügungen weiterhin an die aktive CLI gehen.

### Prozess-Lifecycle

1. `KiAusfuehrungsService.StartTerminalSessionAsync` holt die Startbeschreibung über `IKiPlugin.GetTerminalStartSpecAsync` und delegiert die Session-Erzeugung an die zentrale `ITerminalSessionFactory` (`TerminalSessionService`).
2. Die Factory löst die Executable auf (`TerminalExecutableResolver` — `.exe` direkt, `.cmd`/`.bat` werden zu `cmd.exe /d /s /c` normalisiert), führt die Preflight-Diagnose aus (`TerminalSessionDiagnostics`: PTY-Verfügbarkeit, Executable, CLI-Health, Encoding, Terminalgröße, Pluginparameter) und wählt das Backend.
3. Der gewählte Launcher startet die CLI direkt: PTY-Backend über `PseudoConsole.Create` + `PseudoConsoleProcessStarter.Start` (ConPTY), Pipe-Backend über `Process.Start` mit umgeleiteten Streams.
4. `PseudoConsoleSession` wird erstellt und koordiniert Prozess, Input-Pipe und Output-Pipe. Im Konstruktor wird sofort die `ReadLoopAsync`-Leseschleife als Background-Task gestartet, unabhängig davon, ob ein `TerminalControl` gebunden ist.
5. Die Leseschleife läuft kontinuierlich: Gelesene Bytes werden im `TerminalReplayBuffer` abgelegt und an die Output-Senke gemeldet; danach feuert das `OutputChunk`-Event, `AnsiSequenceParser.Parse` zerlegt die Bytes, `TerminalBuffer.Apply` wendet Events an und `BufferChanged` wird nach jeder erfolgreichen Chunk-Verarbeitung gefeuert.
6. `TaskDetailViewModel.TerminalSessionGestartet`-Event propagiert die Session an `TaskDetailView`.
7. `TerminalControl.Session`-Property wird gesetzt; das Control ruft `RebuildBufferFromReplay()` auf (deterministischer Neuaufbau aus den Roh-Chunks) und abonniert das `BufferChanged`-Event der Session.
8. `TerminalControl.OnBufferChanged` wird aufgerufen, wenn neue Ausgabe verarbeitet wurde, und triggert `InvalidateVisual()`.
9. `TerminalControl.OnRender` rendert den anhand des Scroll-Offsets sichtbaren Ausschnitt aus Scrollback und aktuellem `TerminalBuffer`-Grid per `DrawingContext` (bei aktivem Alternate Screen nur das Alt-Grid ohne Scrollback).
10. `TaskDetailViewModel.CliStatusText` aktualisiert die Fusszeile bei Laufzeitstatus-Änderungen der Session (Event `RuntimeStatusChanged`); beim Pipe-Fallback trägt der Status den Hinweis „ (eingeschränkter Modus – kein Pseudo-Terminal)".

### Aufgabenprotokollierung

Bei über `KiAusfuehrungsService.StartTerminalSessionAsync` gestarteten Terminal-Sitzungen (ConPTY und Pipe-Fallback) wird pro Aufgabe ein `CliOutputProtokollWriter` erzeugt. Er erhält rohe UTF-8-Bytes aus der `PseudoConsoleSession`, rekonstruiert daraus Ausgabezeilen über Chunk-Grenzen hinweg und speichert sie als `ProtokollTyp.CliOutput`. Die Terminalanzeige bleibt davon getrennt: Das `TerminalControl` rendert weiterhin den `TerminalBuffer`, während das Aufgabenprotokoll auch dann fortgeschrieben wird, wenn gerade keine Aufgabenseite gebunden ist.

Die Persistenz läuft in einem Hintergrund-Worker mit bounded Queue. Bei sehr hoher Ausgabe wartet der Terminal-Output-Reader über Backpressure auf freie Queue-Kapazität; Persistenzfehler werden geloggt und beenden die CLI-Sitzung nicht.

### Größenanpassung

Das `TerminalControl` passt Spalten- und Zeilenanzahl automatisch an verfügbare Pixel an (monospace-Grid). Bei Größenänderungen wird `ResizePseudoConsole` aufgerufen, um die echte Terminal-Größe zu aktualisieren.

## Beispiele

- Claude CLI direkt in der Aufgabenansicht bedienen, ohne das Fenster zu wechseln.
- Codex CLI mit voller Farbunterstützung (SGR 3-bit, 8-bit, 24-bit) interaktiv nutzen.
- Tastatureingaben (Pfeiltasten, F1–F12, Ctrl+C) funktionieren nativ ohne Verzögerung.

### Neuerungen: Buffer-Stabilität, Scrollback-Anzeige, Clipboard-Paste und Zeilenvorschub-Normalisierung

Das Terminal-System wurde mit mehreren Verbesserungen erweitert:

1. **Buffer-Snapshot für stabiles Rendering:** Die Rendering-Engine nutzt jetzt `TerminalBuffer.GetSnapshot()`, eine Methode, die einen konsistenten Snapshot des aktuellen Buffer-Zustands unter einem einzigen Lock erstellt. Dies verhindert Race Conditions zwischen paralleler CLI-Ausgabe und gleichzeitigen Render-Operationen — die Ausgabe bleibt stabil und vermischt sich nicht mehr bei schnellen, aufeinanderfolgenden CLI-Ausgaben.

2. **Scrollbare CLI-Ausgabe:** Der Snapshot enthält Scrollback-Zeilen, Scrollback-Anzahl und Gesamtzeilenzahl. `TerminalControl` implementiert zeilenbasiertes Scrollen, sodass lange Ausgaben über vertikale Scrollbar, Mausrad und Page-Scroll erreichbar bleiben. Am Ende des Verlaufs folgt die Anzeige neuer Ausgabe automatisch; eine manuell hochgescrollte Position bleibt stabil.

3. **Robuster Clipboard-Paste-Support:** Benutzer können mit **Ctrl+V** Text aus der Zwischenablage direkt in die CLI einfügen. Auch lange mehrzeilige Inhalte wie Stacktraces werden vollständig übertragen. Die Zielsession wird beim Paste-Start festgehalten; große Eingaben werden im gemeinsamen `PseudoConsoleSession`-Eingabepfad serialisiert, in geordneten Chunks geschrieben und abschließend geflusht. Die Text-Eingabe wird zeilenweise normalisiert (alle Newline-Varianten → `\r`) und als UTF-8 kodiert, um mit Windows-Standard-Clipboard-Verhalten kompatibel zu sein.

4. **Zeilenvorschub-Normalisierung:** Das Terminal behandelt Unix-Style Line Feeds (`\n`) jetzt identisch wie Windows-Style CRLF (`\r\n`) — beide erzeugen einen Zeilenvorschub **und** setzen die Cursor-Spalte auf 0. Dies verhindert den „Treppeneffekt", der entsteht, wenn Programme nur `\n` senden. Carriage Return (`\r`) allein wird weiterhin korrekt als Spalte-0-Rückkehr in der gleichen Zeile behandelt.

5. **Screen-Clear mit vollständiger Bereinigung:** Der ESC-Befehl `ESC[2J` (Clear Entire Screen) leert nun nicht nur das sichtbare Terminal-Grid, sondern auch den internen Scrollback-Puffer — genau wie in echten Windows-Konsolen. Dies stellt sicher, dass der Bildschirmzustand nach dem Clear vollständig konsistent ist.

6. **Robustes Terminal-Resize:** Bei Verkleinerung des Terminals werden nun die **aktuellen (unteren) Zeilen** beibehalten und alte obere Zeilen in den Scrollback verschoben — der aktuelle Prompt/Cursor bleibt sichtbar am unteren Rand. Dies behebt das Problem, dass nach Verkleinerung veraltete alte Zeilen in die Anzeige rutschten.

7. **Automatische CLI-Ausgabe-Protokollierung:** Terminal-Output wird nicht nur gerendert, sondern zeilenweise im aufgabenbezogenen Protokoll gespeichert. Dadurch bleibt die Ausgabe nach Abschluss, Unterbrechung oder erneutem Öffnen der Aufgabe nachvollziehbar.

### Neuerungen: Gemeinsame Session-Abstraktion, Direct-Start und diagnostizierter Fallback (Issue #271)

Die Terminalintegration wurde auf eine gemeinsame Session-Abstraktion umgestellt:

1. **Gemeinsame `ITerminalSession`:** Prozessstart, Ein-/Ausgabe, Resize, Exit-/Fehler-Ereignisse und der Session-Lebenszyklus sind hinter einem einheitlichen Vertrag gebündelt; `PseudoConsoleSession` ist die einzige Implementierung für beide Backends. Das Prozessende meldet die Session über `Exited` mit dem Exit-Code (PID-wiederverwendungs-sicher über das native Prozess-Handle); fatale Laufzeitfehler über `Failed`.

2. **Direct-Start ohne `cmd.exe`-Hülle:** Die Anbieter-CLI wird direkt aus der vom Plugin gelieferten `TerminalSessionStartSpec` gestartet — der bisherige Umweg „`cmd.exe` in der Pseudo Console starten und den Plugin-Befehl nach ~300 ms als getippte Tastatureingabe injizieren" entfällt. Ein `TerminalExecutableResolver` löst dabei nackte Befehlsnamen mit PATHEXT-Semantik auf und normalisiert `.cmd`/`.bat`-Shims zu `cmd.exe /d /s /c`, damit der Direct-Start auch für npm-typische Shim-Installationen funktioniert.

3. **Preflight-Diagnose und sichtbarer Fallback:** Vor jedem Start prüft `TerminalSessionDiagnostics` PTY-Verfügbarkeit (Windows 10 Build 17763+), Executable, CLI-Health, Encoding, Terminalgröße und Pluginparameter. Plugins deklarieren ihre Anforderungen über `TerminalCapabilities` (`SupportsPty`/`RequiresPty`): CLIs, die zwingend ein echtes Pseudo-Terminal brauchen, schlagen mit einer verständlichen Fehlermeldung fehl statt still auf Pipes zu laufen; für alle übrigen Fälle dient der Pipe-Pfad als explizit diagnostizierter Fallback — er wird im Aufgabenprotokoll (`[Terminal-Diagnose]`-Markerzeile mit den Einzelcheck-Ergebnissen) und in der Statuszeile der Aufgabenansicht („… (eingeschränkter Modus – kein Pseudo-Terminal)") sichtbar gemacht.

4. **Replay-Puffer für die Neuanbindung:** Jede Session hält ihre rohen Ausgabe-Chunks in einem begrenzten `TerminalReplayBuffer` (Standard 512 KiB). Beim erneuten Öffnen einer Aufgabenseite wird der Terminal-Buffer daraus deterministisch neu aufgebaut — die Anzeige zeigt denselben Zustand wie zum Verlassen, ohne doppelte Ausgaben.

5. **Erweiterter VT-Renderer:** Der Parser dekodiert UTF-8 jetzt chunk-übergreifend (kein Ersatzzeichen-Zerfall bei Mehrbyte-Zeichen an Chunk-Grenzen) und verarbeitet zusätzlich Alternate Screen (`?1049`/`?1047`/`?1048`), Insert/Delete Line/Char, Scroll-Regionen (DECSTBM), Scroll Up/Down, Save/Restore-Cursor, Tab-Stops, Backspace und RIS-Reset. Vollbild-TUIs (Alternate Screen) sind dabei bewusst nicht scrollbar — der Verlauf bleibt dem Hauptscreen vorbehalten.

### Neuerungen: CLI-Aufzeichnung und Konsolentestfenster

Zur Diagnose von Rendering- und Streaming-Fehlern wurde das Terminal-System um einen byte-exakten Mitschnitt mit zeitgesteuerter Wiedergabe erweitert:

1. **Automatischer Rohbyte-Mitschnitt:** Jede Terminal-Session wird zusätzlich zum zeilenbasierten Aufgabenprotokoll als Rohbyte-Aufzeichnung mit Zeitstempel pro Ausgabe-Chunk mitgeschrieben (`CliOutputRecorder`, läuft über die `CompositeTerminalOutputSink` parallel zum `CliOutputProtokollWriter` und erfasst dadurch auch die ersten Chunks einer Session). Der Mitschnitt ist auf `Terminal:AufzeichnungByteBudget` (Standard 8 MB) begrenzt; bei Überschreitung bleibt das intakte Anfangsstück erhalten und die Aufzeichnung wird als unvollständig markiert. Vorgehalten werden die Aufzeichnungen der letzten 8 gestarteten Aufgaben.

2. **Export als `.clireplay`:** In der Ribbon-Gruppe **CLI** der Aufgabendetailansicht exportiert der neue Button **„Aufzeichnung exportieren"** den Mitschnitt als `.clireplay`-Binärdatei (Vorschlagsname `cli-replay-<AufgabenId>.clireplay`). Der bisherige zeilenbasierte **„Rohausgabe exportieren"** (`.raw`) bleibt parallel bestehen — beide Exporte erfüllen unterschiedliche Zwecke.

3. **Konsolentestfenster:** Über **Einstellungen → Allgemein → Abschnitt „Diagnose" → „Konsolentestfenster öffnen"** lässt sich ein nicht-modales Diagnosefenster öffnen. Es lädt eine `.clireplay`-Datei und spielt sie über eine eigene Replay-Session (`TerminalReplaySession`) zeitgesteuert durch denselben echten Renderpfad wie die Live-Ausgabe ab (`AnsiSequenceParser` → `TerminalBuffer` → `TerminalControl`) — es gibt keinen separaten oder abweichenden Renderer.

4. **Wiedergabesteuerung und Quell-Ansicht:** Das Fenster zeigt links das gerenderte Terminal und rechts eine Liste aller aufgezeichneten Chunks mit Index, Zeit-Offset, Länge und Quelltext, in dem Steuersequenzen sichtbar gemacht sind (ESC als `␛`, CR als `\r`, LF als `\n` u. a.). Der gerade wiedergegebene Chunk wird in der Liste synchron markiert. Die Steuerung bietet **Abspielen**, **Neu starten** (bricht den laufenden Durchlauf ab und beginnt sofort wieder vorn), **Pausieren/Fortsetzen** sowie eine jederzeit änderbare **Zeitraffer-Schwelle** in Sekunden: Pausen zwischen Ausgabe-Blöcken, die länger als die Schwelle sind, werden auf sie verkürzt — `0` spielt mit maximaler Geschwindigkeit ab. Bei einer unvollständigen Aufzeichnung erscheint ein Hinweisband.

5. **Behobene Defekte:** Mit dem Werkzeug nachgestellte Fehler wurden direkt behoben — eine Race-Bedingung zwischen Live-Ausgabe und Buffer-Neuaufbau (mögliche Doppelausgabe), fehlerhafte Behandlung von Escape-Zeichen mitten in unvollständigen Steuersequenzen sowie das Fehlen der String-Sequenzen DCS/SOS/PM/APC, deren Inhalte zuvor irrtümlich als Text ausgegeben wurden.

## Einschränkungen

- `CreatePseudoConsole` ist erst ab Windows 10 Build 17763 verfügbar. Das Projekt zielt auf `net10.0-windows10.0.17763.0`, daher ist das kein praktisches Risiko; auf älteren Builds läuft der diagnostizierte Pipe-Fallback (eingeschränkter Modus), `RequiresPty`-CLIs schlagen mit Fehlermeldung fehl.
- Scrollback-Puffer ist auf 1000 Zeilen begrenzt; ältere Zeilen gehen verloren. Im Alternate Screen (Vollbild-TUIs) gibt es keinen Scrollback — dort ist das Scrollen bewusst deaktiviert.
- Der Replay-Puffer für die Neuanbindung ist auf `Terminal:ReplayBufferByteBudget` (Standard 512 KiB) begrenzt; bei darüber hinausgehender Ausgabe fehlen die ältesten Chunks im wiederhergestellten Bild.
- Executables, die weder `.exe`/endungsloses PE-Image noch `.cmd`/`.bat` sind (z. B. `.ps1`-Skripte), können nicht gestartet werden — der Start schlägt mit einer verständlichen Fehlermeldung fehl.
- Bei `.cmd`/`.bat`-Shims (z. B. npm-Installationen) löst `Ctrl+C` die cmd.exe-Rückfrage „Batchdatei abbrechen (J/N)?" aus statt eines direkten Signals — bekanntes Verhalten von Batch-Shims.
- Mouse-Tracking-Sequenzen werden nicht unterstützt (nicht erforderlich für Standard-CLI-Verwendung).
- OSC-Sequenzen (z. B. Fenstertitel-Setzung) sowie die String-Sequenzen DCS, SOS, PM und APC werden verworfen — ihre Inhalte erscheinen nicht als Text.
- Clipboard-Paste ist auf System.Windows.Clipboard beschränkt (WPF-Standard) — andere Quellen von Zwischenablage-Daten werden nicht unterstützt.
- Das Aufgabenprotokoll speichert dekodierte Ausgabezeilen nahe am Rohstream. ANSI- und Control-Sequenzen können daher im Protokollinhalt enthalten sein.
- Bei gleichzeitigem Abschluss der Output-Senke und starker Backpressure gibt es eine bekannte Nacharbeit: Ein bereits dekodierter, aber noch nicht vollständig in die bounded Queue geschriebener Chunk kann im Race-Fall teilweise verloren gehen. Der drainbare Abschluss schützt bereits angenommene Queue-Einträge.
- Die Rohbyte-Aufzeichnung ist speicherbasiert und budgetbegrenzt (`Terminal:AufzeichnungByteBudget`, Standard 8 MB): Bei Überschreitung stoppt der Mitschnitt — die exportierte `.clireplay`-Datei enthält dann nur den Anfang der Session und wird als unvollständig gekennzeichnet. Es werden nur die Aufzeichnungen der letzten 8 gestarteten Aufgaben vorgehalten; ältere Mitschnitte sowie alle Aufzeichnungen beim Anwendungsende gehen verloren.
- Die Aufzeichnung enthält ausschließlich echte CLI-Ausgabe-Bytes — interne `[Terminal-Diagnose]`-Markerzeilen sind nicht Teil des Mitschnitts. Terminal-Größenänderungen während einer Session werden nicht aufgezeichnet; die Wiedergabe nutzt die aktuelle Fenstergröße des Konsolentestfensters.
