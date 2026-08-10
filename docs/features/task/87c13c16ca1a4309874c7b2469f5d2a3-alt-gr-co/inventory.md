# Bestandsaufnahme: Alt Gr-Unterstützung und Ctrl+Pfeiltaste-Navigation

Diese Analyse untersucht die vorhandene Tastatur-Eingabeverarbeitung in der `TerminalControl`-Komponente und der VT100-Kodierung mit Fokus auf die geplante Unterstützung von Alt Gr-Sonderzeichen und wortweiser Navigation mit Ctrl+Pfeiltasten.

## Zusammenfassung

| Bereich | Status | Beschreibung |
|---------|--------|-------------|
| VT100-Tastaturkodierung | Teilweise | `KeyToVt100Encoder.Encode()` behandelt Ctrl+A-Z und viele Funktionstasten, aber fehlen Alt Gr-Handling und Ctrl+Links/Rechts |
| Terminal-Input-Verarbeitung | Vollständig | `TerminalControl.OnPreviewKeyDown()` und `OnTextInput()` sind vorhanden und rufen `KeyToVt100Encoder` auf |
| Pseudo-Console-Infrastruktur | Vollständig | `PseudoConsoleSession.InputStream` nimmt bereits VT100-kodierte Bytes an |
| Tests | Unvollständig | Tests existieren nur für `EncodeClipboardText`, nicht für die `Encode()`-Methode; keine Tests für Alt Gr oder Ctrl+Pfeiltasten |

## Details

- [Logik: KeyToVt100Encoder und TerminalControl](inventory/logic.md)
- [Enums und Events](inventory/enums-events.md)
- [Tests](inventory/tests.md)
