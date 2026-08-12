# Terminal-Integration

Das Terminal-System rendert die Ausgabe von KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI) nativ in der WPF-Aufgabendetailansicht. Die Implementierung nutzt Windows Pseudo Console (ConPTY) API zum Starten der Prozesse und einen VT100/ANSI-Parser zum Rendering von Ausgabeströmen in einem benutzerdefinierten WPF-Control.

Das System unterstützt volle Farb-Rendering (3-bit, 8-bit, 24-bit ANSI-Farben), interaktive Tastatureingaben (einschließlich Pfeiltasten, Funktionstasten und Ctrl-Kombinationen), robuste Clipboard-Paste-Unterstützung für lange mehrzeilige Texte (Ctrl+V), automatische Terminal-Größenanpassung bei Fensterresize, eine vertikal scrollbare CLI-Ausgabe mit 1000 Zeilen Scrollback und parallele Ausführung mehrerer CLI-Prozesse ohne Blockade. Zusätzlich wurde das Rendering mit einem Buffer-Snapshot-Mechanismus stabilisiert, um Race Conditions bei schnellen Ausgaben zu verhindern, und der gelesene Terminal-Output wird über eine Output-Senke automatisch im Aufgabenprotokoll gespeichert.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [Eingabeverarbeitung](eingabeverarbeitung.md) — Alt Gr-Sonderzeichen, Ctrl+Pfeiltaste-Navigation und robustes Clipboard-Paste
- [API](api.md)
- [Installation & Konfiguration](installation.md)
- [Architektur](architektur.md)
