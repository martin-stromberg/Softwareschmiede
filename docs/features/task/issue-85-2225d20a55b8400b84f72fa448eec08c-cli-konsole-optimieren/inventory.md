# Bestandsaufnahme: CLI Konsole optimieren

Analyse des bestehenden Projektcodes bezogen auf die Anforderung zur Behebung von Stabilitätsproblemen bei der Konsolenausgabe und Implementierung von Clipboard-Paste-Funktionalität.

## Zusammenfassung

**Vorhanden:**
- Grundarchitektur mit `TerminalBuffer` (Domain), `PseudoConsoleSession` + `AnsiSequenceParser` (Infrastructure) und `TerminalControl` (Presentation) ist implementiert
- `TerminalBuffer` nutzt interne Lock-Synchronisierung für `Apply()`-Operationen
- `TerminalEvent`-Hierarchie ist vorhanden; `ClipboardPasteEvent` existiert noch nicht
- `PseudoConsoleSession` führt Leseschleife unabhängig aus und publiziert `BufferChanged`-Event
- `KeyToVt100Encoder` konvertiert WPF-Tastatureingaben zu VT100-Sequenzen; Methode `EncodeClipboardText()` fehlt noch
- `TerminalControl` registriert Handler auf `BufferChanged`; Behandlung von `Ctrl+V` fehlt
- Umfassende Test-Suite für alle Komponenten existiert

**Nicht vorhanden / Implementierungsbedarf:**
- Verbesserungen der Buffer-Synchronisierung (Atomarität von `BufferChanged`-Event, Snapshot/Copy-on-Read)
- `Ctrl+V`-Tastatur-Handler in `TerminalControl`
- `Clipboard.GetText()` Integration
- `EncodeClipboardText()`-Methode in `KeyToVt100Encoder` für Newline-Behandlung
- Fehlerbehandlung für Clipboard-Lesefehler
- Tests für Clipboard-Paste-Funktionalität
- Optionale Konfiguration (Paste aktivieren/deaktivieren, Rate-Limiting, Zeilenseparator)

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Events / TerminalEvent-Hierarchie](inventory/events.md)
- [Interfaces und abstrakte Typen](inventory/interfaces.md)
- [Tests](inventory/tests.md)
