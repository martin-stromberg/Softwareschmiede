# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden (nachweislich nicht Dateiexplorer-bezogen)

## Zusammenfassung

- Gesamt: 944
- Bestanden: 941
- Fehlgeschlagen: 2
- Übersprungen: 1

## Fehlgeschlagene Tests

- `TerminalControlTests.ReadClipboardAndInsertAsync_ClipboardAccessThrows_LogsWarningAndContinues` — hängt von echtem Zwischenablage-Zugriff ab; bereits mehrfach als Sandbox-Einschränkung dokumentiert (keine interaktive Desktop-Session), nicht Dateiexplorer-bezogen.
- `WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E` — bereits mehrfach als last-/timingabhängige E2E-Flakiness dokumentiert, nicht Dateiexplorer-bezogen.

`E2E_FileExplorer` (beide Tests, inkl. der neuen Sichtbarkeitsprüfung des „Datei öffnen"-Buttons) wurde isoliert
erneut ausgeführt und bestand vollständig (2/2). Über mehrere volle Testläufe hinweg dieses Nacharbeitszyklus
schwankte die Gesamtfehleranzahl zwischen 0 und 2, stets mit wechselnden, unabhängigen E2E-/Clipboard-Tests —
kein einziger Fehlschlag betraf je Dateiexplorer-Code.
