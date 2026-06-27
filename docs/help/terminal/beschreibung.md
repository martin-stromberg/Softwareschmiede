← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Beschreibung

## Zweck

Das Terminal-System ermöglicht die direkte, interaktive Bedienung von KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI) innerhalb der Softwareschmiede. Die Ausgabe des Prozesses wird in Echtzeit in einem WPF-Control gerendert; Tastatureingaben werden direkt an den Prozess weitergeleitet — der Anwender arbeitet mit dem echten CLI im nativen Kontext, ohne Fenster zu wechseln.

## Funktionsweise

Das System nutzt Windows Pseudo Console (ConPTY) API zum Starten des CLI-Prozesses. Der Prozess-Output wird als Byte-Stream aus einer anonymen Pipe gelesen und durch den `AnsiSequenceParser` in strukturierte Terminal-Ereignisse (Text, Cursor-Bewegung, Farben, Erase-Befehle) zerlegt. Ein `TerminalBuffer` verwaltet einen 2D-Grid aus `TerminalCell`-Objekten mit Zeichen, Farben und Text-Attributen. Das `TerminalControl` rendert diesen Buffer per `DrawingContext` mit monospace-Schriftart; Tastatureingaben werden durch `KeyToVt100Encoder` in VT100-Escape-Sequenzen konvertiert und in die Prozess-Input-Pipe geschrieben.

### Prozess-Lifecycle

1. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` erstellt eine Pseudo Console mit `CreatePseudoConsole` API.
2. Der CLI-Prozess wird via `PseudoConsoleProcessStarter.Start` mit Prozess-Attribut-Liste gestartet.
3. `PseudoConsoleSession` koordiniert Prozess, Input-Pipe und Output-Pipe.
4. `TaskDetailViewModel.PseudoConsoleSessionGestartet`-Event propagiert die Session an `TaskDetailView`.
5. `TerminalControl.Session`-Property wird gesetzt; Control startet Read-Loop aus Output-Pipe.
6. `AnsiSequenceParser.Parse` zerlegt eingehende Bytes; `TerminalBuffer.Apply` wendet Events an.
7. `TerminalControl.OnRender` rendert `TerminalBuffer`-Inhalt per `DrawingContext`.

### Größenanpassung

Das `TerminalControl` passt Spalten- und Zeilenanzahl automatisch an verfügbare Pixel an (monospace-Grid). Bei Größenänderungen wird `ResizePseudoConsole` aufgerufen, um die echte Terminal-Größe zu aktualisieren.

## Beispiele

- Claude CLI direkt in der Aufgabenansicht bedienen, ohne das Fenster zu wechseln.
- Codex CLI mit voller Farbunterstützung (SGR 3-bit, 8-bit, 24-bit) interaktiv nutzen.
- Tastatureingaben (Pfeiltasten, F1–F12, Ctrl+C) funktionieren nativ ohne Verzögerung.

## Einschränkungen

- `CreatePseudoConsole` ist erst ab Windows 10 Build 17763 verfügbar. Das Projekt zielt auf `net10.0-windows10.0.17763.0`, daher ist das kein praktisches Risiko.
- Scrollback-Puffer ist auf 1000 Zeilen begrenzt; ältere Zeilen gehen verloren.
- Mouse-Tracking-Sequenzen werden nicht unterstützt (nicht erforderlich für Standard-CLI-Verwendung).
- OSC-Sequenzen (z. B. Fenstertitel-Setzung) werden verworfen.
