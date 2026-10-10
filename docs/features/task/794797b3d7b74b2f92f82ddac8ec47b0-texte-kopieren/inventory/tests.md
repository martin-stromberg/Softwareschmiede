# Vorhandene Tests und Abdeckung

## Relevante Tests

- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs` — Renderer-/Scrollverhalten und Sessionbindung
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.KeyInput.cs` — Weiterleitung von Tastatureingaben sowie Navigation bei Replay-Sessions
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs` — vorhandener Clipboard-Zugriff, derzeit für Einfügen mit `Ctrl+V`
- `src/Softwareschmiede.Tests/App/Controls/KeyToVt100EncoderTests.KeyEncoding.cs` — Tastenkodierung für Terminaleingaben
- `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs` — E2E-Abdeckung des Replay-Fensters; Kommentare halten fest, dass der gerenderte Inhalt des custom Controls nicht über ein UIA-TextPattern lesbar ist

## Abdeckungslücke

Im Bestand sind keine Tests zur Mausauswahl, zum sichtbaren Auswahlzustand, zum Extrahieren des ausgewählten Textes oder zum Kopieren mit Zeilenumbrüchen erkennbar. Auch das Zusammenspiel aus Kopierkürzel und laufender Live-Session ist nicht abgedeckt. Es wurde für diese Bestandsaufnahme kein Testlauf ausgeführt; dies ist eine Code-Inventur, kein aktueller Testergebnisbericht.
