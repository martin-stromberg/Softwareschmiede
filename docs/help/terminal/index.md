# Terminal-Integration

Das Terminal-System rendert die Ausgabe von KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI) nativ in der WPF-Aufgabendetailansicht. Die Implementierung nutzt Windows Pseudo Console (ConPTY) API zum Starten der Prozesse und einen VT100/ANSI-Parser zum Rendering von Ausgabeströmen in einem benutzerdefinierten WPF-Control.

Das System unterstützt volle Farb-Rendering (3-bit, 8-bit, 24-bit ANSI-Farben), interaktive Tastaturendigaben (einschließlich Pfeiltasten, Funktionstasten und Ctrl-Kombinationen), automatische Terminal-Größenanpassung bei Fensterresize und parallele Ausführung mehrerer CLI-Prozesse ohne Blockade. Zusätzlich wurde das Rendering mit einem Buffer-Snapshot-Mechanismus stabilisiert, um Race Conditions bei schnellen Ausgaben zu verhindern, und Clipboard-Paste-Unterstützung (Ctrl+V) hinzugefügt.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [API](api.md)
- [Architektur](architektur.md)
