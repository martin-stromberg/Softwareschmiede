← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Beschreibung

## Zweck

Das Terminal-System ermöglicht die direkte, interaktive Bedienung von KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI) innerhalb der Softwareschmiede. Die Ausgabe des Prozesses wird in Echtzeit in einem WPF-Control gerendert; Tastatureingaben werden direkt an den Prozess weitergeleitet — der Anwender arbeitet mit dem echten CLI im nativen Kontext, ohne Fenster zu wechseln.

## Funktionsweise

Das System nutzt Windows Pseudo Console (ConPTY) API zum Starten des CLI-Prozesses. Der Prozess-Output wird als Byte-Stream aus einer anonymen Pipe gelesen und durch den `AnsiSequenceParser` in strukturierte Terminal-Ereignisse (Text, Cursor-Bewegung, Farben, Erase-Befehle) zerlegt. Ein `TerminalBuffer` verwaltet einen 2D-Grid aus `TerminalCell`-Objekten mit Zeichen, Farben und Text-Attributen. Das `TerminalControl` ist ein reiner Renderer, der den `TerminalBuffer` per `DrawingContext` mit monospace-Schriftart darstellt; Tastatureingaben werden durch `KeyToVt100Encoder` in VT100-Escape-Sequenzen konvertiert und in die Prozess-Input-Pipe geschrieben.

Die Leseschleife läuft unabhängig vom `TerminalControl`-Lebenszyklus in `PseudoConsoleSession` selbst — von der Session-Konstruktion bis zur Dispose — damit mehrere CLI-Prozesse parallel weiterlaufen können, auch wenn ihre Aufgabenseite nicht angezeigt wird (Issue-86).

Die Aufgabendetailansicht zeigt den Laufzeitstatus der CLI in der Fusszeile. `PseudoConsoleSession` verfolgt dafür die letzte Ausgabe- und Eingabeaktivität: Frische I/O-Aktivität wird als laufende Ausführung angezeigt (`Laeuft`); bleibt Ausgabe bei weiterhin laufendem Prozess aus, wird nach kurzer Zeit "Wartet auf Eingabe" angezeigt (`WartetAufEingabe`).

### Prozess-Lifecycle

1. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` erstellt eine Pseudo Console mit `CreatePseudoConsole` API.
2. Der CLI-Prozess wird via `PseudoConsoleProcessStarter.Start` mit Prozess-Attribut-Liste gestartet.
3. `PseudoConsoleSession` wird erstellt und koordiniert Prozess, Input-Pipe und Output-Pipe. Im Konstruktor wird sofort die `ReadLoopAsync`-Leseschleife als Background-Task gestartet, unabhängig davon, ob ein `TerminalControl` gebunden ist.
4. Die Leseschleife läuft kontinuierlich: `AnsiSequenceParser.Parse` zerlegt eingehende Bytes; `TerminalBuffer.Apply` wendet Events an; `BufferChanged`-Event wird nach jeder erfolgreichen Chunk-Verarbeitung gefeuert.
5. `TaskDetailViewModel.PseudoConsoleSessionGestartet`-Event propagiert die Session an `TaskDetailView`.
6. `TerminalControl.Session`-Property wird gesetzt; Control abonniert das `BufferChanged`-Event der Session.
7. `TerminalControl.OnBufferChanged` wird aufgerufen, wenn neue Ausgabe verarbeitet wurde, und triggert `InvalidateVisual()`.
8. `TerminalControl.OnRender` rendert aktuellen `TerminalBuffer`-Inhalt per `DrawingContext`.
9. `TaskDetailViewModel.CliStatusText` aktualisiert die Fusszeile bei Laufzeitstatus-Änderungen der Session (Event `RuntimeStatusChanged`).

### Größenanpassung

Das `TerminalControl` passt Spalten- und Zeilenanzahl automatisch an verfügbare Pixel an (monospace-Grid). Bei Größenänderungen wird `ResizePseudoConsole` aufgerufen, um die echte Terminal-Größe zu aktualisieren.

## Beispiele

- Claude CLI direkt in der Aufgabenansicht bedienen, ohne das Fenster zu wechseln.
- Codex CLI mit voller Farbunterstützung (SGR 3-bit, 8-bit, 24-bit) interaktiv nutzen.
- Tastatureingaben (Pfeiltasten, F1–F12, Ctrl+C) funktionieren nativ ohne Verzögerung.

### Neuerungen: Buffer-Stabilitäts-Optimierung und Clipboard-Paste

Das Terminal-System wurde mit zwei Verbesserungen erweitert:

1. **Buffer-Snapshot für stabiles Rendering:** Die Rendering-Engine nutzt jetzt `TerminalBuffer.GetSnapshot()`, eine Methode, die einen konsistenten Snapshot des aktuellen Buffer-Zustands unter einem einzigen Lock erstellt. Dies verhindert Race Conditions zwischen paralleler CLI-Ausgabe und gleichzeitigen Render-Operationen — die Ausgabe bleibt stabil und vermischt sich nicht mehr bei schnellen, aufeinanderfolgenden CLI-Ausgaben.

2. **Clipboard-Paste-Support:** Benutzer können nun mit **Ctrl+V** Text aus der Zwischenablage direkt in die CLI einfügen. Die Text-Eingabe wird zeilenweise normalisiert (alle Newline-Varianten → `\r`) und als UTF-8 kodiert, um mit Windows-Standard-Clipboard-Verhalten kompatibel zu sein.

## Einschränkungen

- `CreatePseudoConsole` ist erst ab Windows 10 Build 17763 verfügbar. Das Projekt zielt auf `net10.0-windows10.0.17763.0`, daher ist das kein praktisches Risiko.
- Scrollback-Puffer ist auf 1000 Zeilen begrenzt; ältere Zeilen gehen verloren.
- Mouse-Tracking-Sequenzen werden nicht unterstützt (nicht erforderlich für Standard-CLI-Verwendung).
- OSC-Sequenzen (z. B. Fenstertitel-Setzung) werden verworfen.
- Clipboard-Paste ist auf System.Windows.Clipboard beschränkt (WPF-Standard) — andere Quellen von Zwischenablage-Daten werden nicht unterstützt.
