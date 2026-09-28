# Terminal-Integration

Das Terminal-System rendert die Ausgabe von KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI, Devin CLI) nativ in der WPF-Aufgabendetailansicht. Die Implementierung nutzt eine gemeinsame `ITerminalSession`-Abstraktion, deren Sessions zentral über `TerminalSessionService`/`ITerminalSessionFactory` erzeugt werden: Die Anbieter-CLI wird direkt aus der vom Plugin gelieferten Startbeschreibung gestartet (kein `cmd.exe`-Wrapper mehr), vor jedem Start läuft eine Preflight-Diagnose (PTY-Verfügbarkeit, Executable-Auflösung, CLI-Health), und die Backend-Wahl entscheidet zwischen Windows Pseudo Console (ConPTY) und einem explizit diagnostizierten Pipe-Fallback, der für den Anwender als „eingeschränkter Modus" in der Statuszeile sichtbar ist. Ein VT100/ANSI-Parser rendert die Ausgabeströme in einem benutzerdefinierten WPF-Control.

Das System unterstützt volle Farb-Rendering (3-bit, 8-bit, 24-bit ANSI-Farben), Alternate Screen und Scroll-Regionen für Vollbild-TUIs, interaktive Tastatureingaben (einschließlich Pfeiltasten, Funktionstasten und Ctrl-Kombinationen), robuste Clipboard-Paste-Unterstützung für lange mehrzeilige Texte (Ctrl+V), automatische Terminal-Größenanpassung bei Fensterresize, eine vertikal scrollbare CLI-Ausgabe mit 1000 Zeilen Scrollback und parallele Ausführung mehrerer CLI-Prozesse ohne Blockade. Zusätzlich wurde das Rendering mit einem Buffer-Snapshot-Mechanismus stabilisiert, um Race Conditions bei schnellen Ausgaben zu verhindern; ein begrenzter Replay-Puffer baut die Anzeige beim erneuten Öffnen einer Aufgabenseite aus den Rohdaten neu auf, und der gelesene Terminal-Output wird über eine Output-Senke automatisch im Aufgabenprotokoll gespeichert.

Zur Diagnose von Rendering- und Streaming-Fehlern zeichnet das System die rohen Ausgabe-Bytes jeder Terminal-Session automatisch mit Zeitstempel pro Chunk auf (`CliOutputRecorder` über `CompositeTerminalOutputSink`, Budget `Terminal:AufzeichnungByteBudget`). Der Mitschnitt lässt sich aus der Aufgabendetailansicht als `.clireplay`-Datei exportieren und im **Konsolentestfenster** (Einstellungen → Allgemein → Diagnose) zeitgesteuert durch denselben echten Renderpfad wieder abspielen — mit Pausieren/Fortsetzen, Neu starten, Zeitraffer-Schwelle, Einzelschritt-Wiedergabe vorwärts und rückwärts („Schritt vor"/„Schritt zurück") und einer synchronen Quell-Ansicht der Chunks.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [Eingabeverarbeitung](eingabeverarbeitung.md) — Alt Gr-Sonderzeichen, Ctrl+Pfeiltaste-Navigation und robustes Clipboard-Paste
- [API](api.md)
- [Installation & Konfiguration](installation.md)
- [Architektur](architektur.md)
- [Business Rules](business-rules.md)
