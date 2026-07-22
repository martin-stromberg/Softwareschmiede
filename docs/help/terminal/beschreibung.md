← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Beschreibung

## Zweck

Das Terminal-System ermöglicht die direkte, interaktive Bedienung von KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI) innerhalb der Softwareschmiede. Die Ausgabe des Prozesses wird in Echtzeit in einem WPF-Control gerendert; Tastatureingaben werden direkt an den Prozess weitergeleitet — der Anwender arbeitet mit dem echten CLI im nativen Kontext, ohne Fenster zu wechseln.

## Funktionsweise

Das System nutzt Windows Pseudo Console (ConPTY) API zum Starten des CLI-Prozesses. Der Prozess-Output wird als Byte-Stream aus einer anonymen Pipe gelesen und durch den `AnsiSequenceParser` in strukturierte Terminal-Ereignisse (Text, Cursor-Bewegung, Farben, Erase-Befehle) zerlegt. Ein `TerminalBuffer` verwaltet einen 2D-Grid aus `TerminalCell`-Objekten mit Zeichen, Farben und Text-Attributen. Das `TerminalControl` ist ein reiner Renderer, der den `TerminalBuffer` per `DrawingContext` mit monospace-Schriftart darstellt; Tastatureingaben werden durch `KeyToVt100Encoder` in VT100-Escape-Sequenzen konvertiert und in die Prozess-Input-Pipe geschrieben.

Die Leseschleife läuft unabhängig vom `TerminalControl`-Lebenszyklus in `PseudoConsoleSession` selbst — von der Session-Konstruktion bis zur Dispose — damit mehrere CLI-Prozesse parallel weiterlaufen können, auch wenn ihre Aufgabenseite nicht angezeigt wird (Issue-86). Dieselbe Leseschleife meldet jeden gelesenen Output-Chunk zusätzlich an eine optionale `ITerminalOutputSink`; für Aufgabenläufe ist dort ein `CliOutputProtokollWriter` angebunden, der Ausgabezeilen im Aufgabenprotokoll speichert.

Die Aufgabendetailansicht zeigt den Laufzeitstatus der CLI in der Fusszeile. `PseudoConsoleSession` verfolgt dafür die letzte Ausgabe- und Eingabeaktivität: Frische I/O-Aktivität wird als laufende Ausführung angezeigt (`Laeuft`); bleibt Ausgabe bei weiterhin laufendem Prozess aus, wird nach kurzer Zeit "Wartet auf Eingabe" angezeigt (`WartetAufEingabe`).

Die CLI-Ausgabe ist vertikal scrollbar. `TerminalBuffer` hält dafür bis zu 1000 Scrollback-Zeilen vor; `TerminalControl` stellt den Verlauf zeilenbasiert über den WPF-`ScrollViewer` bereit. Anwender können ältere Ausgaben per Scrollbar, Mausrad oder Page-Scroll lesen. Solange die Ansicht am Ende steht, folgt sie neuer Ausgabe automatisch; nach manuellem Hochscrollen bleibt die Leseposition stabil. Der Eingabefokus bleibt dabei auf dem Terminalbereich, sodass Tastatureingaben und Zwischenablage-Einfügungen weiterhin an die aktive CLI gehen.

### Prozess-Lifecycle

1. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` erstellt eine Pseudo Console mit `CreatePseudoConsole` API.
2. Der CLI-Prozess wird via `PseudoConsoleProcessStarter.Start` mit Prozess-Attribut-Liste gestartet.
3. `PseudoConsoleSession` wird erstellt und koordiniert Prozess, Input-Pipe und Output-Pipe. Im Konstruktor wird sofort die `ReadLoopAsync`-Leseschleife als Background-Task gestartet, unabhängig davon, ob ein `TerminalControl` gebunden ist.
4. Die Leseschleife läuft kontinuierlich: Gelesene Bytes werden zuerst an die Output-Senke gemeldet; danach zerlegt `AnsiSequenceParser.Parse` die Bytes, `TerminalBuffer.Apply` wendet Events an und `BufferChanged` wird nach jeder erfolgreichen Chunk-Verarbeitung gefeuert.
5. `TaskDetailViewModel.PseudoConsoleSessionGestartet`-Event propagiert die Session an `TaskDetailView`.
6. `TerminalControl.Session`-Property wird gesetzt; Control abonniert das `BufferChanged`-Event der Session.
7. `TerminalControl.OnBufferChanged` wird aufgerufen, wenn neue Ausgabe verarbeitet wurde, und triggert `InvalidateVisual()`.
8. `TerminalControl.OnRender` rendert den anhand des Scroll-Offsets sichtbaren Ausschnitt aus Scrollback und aktuellem `TerminalBuffer`-Grid per `DrawingContext`.
9. `TaskDetailViewModel.CliStatusText` aktualisiert die Fusszeile bei Laufzeitstatus-Änderungen der Session (Event `RuntimeStatusChanged`).

### Aufgabenprotokollierung

Bei über `KiAusfuehrungsService.StartWithPseudoConsoleAsync` gestarteten ConPTY-Sitzungen wird pro Aufgabe ein `CliOutputProtokollWriter` erzeugt. Er erhält rohe UTF-8-Bytes aus der `PseudoConsoleSession`, rekonstruiert daraus Ausgabezeilen über Chunk-Grenzen hinweg und speichert sie als `ProtokollTyp.CliOutput`. Die Terminalanzeige bleibt davon getrennt: Das `TerminalControl` rendert weiterhin den `TerminalBuffer`, während das Aufgabenprotokoll auch dann fortgeschrieben wird, wenn gerade keine Aufgabenseite gebunden ist.

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

3. **Clipboard-Paste-Support:** Benutzer können nun mit **Ctrl+V** Text aus der Zwischenablage direkt in die CLI einfügen. Die Text-Eingabe wird zeilenweise normalisiert (alle Newline-Varianten → `\r`) und als UTF-8 kodiert, um mit Windows-Standard-Clipboard-Verhalten kompatibel zu sein.

4. **Zeilenvorschub-Normalisierung:** Das Terminal behandelt Unix-Style Line Feeds (`\n`) jetzt identisch wie Windows-Style CRLF (`\r\n`) — beide erzeugen einen Zeilenvorschub **und** setzen die Cursor-Spalte auf 0. Dies verhindert den „Treppeneffekt", der entsteht, wenn Programme nur `\n` senden. Carriage Return (`\r`) allein wird weiterhin korrekt als Spalte-0-Rückkehr in der gleichen Zeile behandelt.

5. **Screen-Clear mit vollständiger Bereinigung:** Der ESC-Befehl `ESC[2J` (Clear Entire Screen) leert nun nicht nur das sichtbare Terminal-Grid, sondern auch den internen Scrollback-Puffer — genau wie in echten Windows-Konsolen. Dies stellt sicher, dass der Bildschirmzustand nach dem Clear vollständig konsistent ist.

6. **Robustes Terminal-Resize:** Bei Verkleinerung des Terminals werden nun die **aktuellen (unteren) Zeilen** beibehalten und alte obere Zeilen in den Scrollback verschoben — der aktuelle Prompt/Cursor bleibt sichtbar am unteren Rand. Dies behebt das Problem, dass nach Verkleinerung veraltete alte Zeilen in die Anzeige rutschten.

7. **Automatische CLI-Ausgabe-Protokollierung:** Terminal-Output wird nicht nur gerendert, sondern zeilenweise im aufgabenbezogenen Protokoll gespeichert. Dadurch bleibt die Ausgabe nach Abschluss, Unterbrechung oder erneutem Öffnen der Aufgabe nachvollziehbar.

## Einschränkungen

- `CreatePseudoConsole` ist erst ab Windows 10 Build 17763 verfügbar. Das Projekt zielt auf `net10.0-windows10.0.17763.0`, daher ist das kein praktisches Risiko.
- Scrollback-Puffer ist auf 1000 Zeilen begrenzt; ältere Zeilen gehen verloren.
- Mouse-Tracking-Sequenzen werden nicht unterstützt (nicht erforderlich für Standard-CLI-Verwendung).
- OSC-Sequenzen (z. B. Fenstertitel-Setzung) werden verworfen.
- Clipboard-Paste ist auf System.Windows.Clipboard beschränkt (WPF-Standard) — andere Quellen von Zwischenablage-Daten werden nicht unterstützt.
- Das Aufgabenprotokoll speichert dekodierte Ausgabezeilen nahe am Rohstream. ANSI- und Control-Sequenzen können daher im Protokollinhalt enthalten sein.
- Bei gleichzeitigem Abschluss der Output-Senke und starker Backpressure gibt es eine bekannte Nacharbeit: Ein bereits dekodierter, aber noch nicht vollständig in die bounded Queue geschriebener Chunk kann im Race-Fall teilweise verloren gehen. Der drainbare Abschluss schützt bereits angenommene Queue-Einträge.
