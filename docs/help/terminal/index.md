# Terminal-Integration

Das Terminal-System rendert die Ausgabe von KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI) nativ in der WPF-Aufgabendetailansicht. Die Implementierung nutzt Windows Pseudo Console (ConPTY) API zum Starten der Prozesse und einen VT100/ANSI-Parser zum Rendering von Ausgabeströmen in einem benutzerdefinierten WPF-Control.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [API](api.md)
- [Architektur](architektur.md)
