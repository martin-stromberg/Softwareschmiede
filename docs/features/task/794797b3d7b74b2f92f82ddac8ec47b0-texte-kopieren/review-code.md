# Code-Review: Texte kopieren

## Status: Befunde vorhanden

Geprüft wurden die aktuellen Änderungen an `TerminalControl`, `TerminalBuffer` sowie die ergänzten Unit- und E2E-Tests. Sämtliche Vorkommen von `RaiseUiActionRequested` unter `src/` wurden ebenfalls geprüft: Es gibt keine Vorkommen. Damit gibt es in diesem Änderungsumfang keine ausgelöste UI-Aktion ohne zugehörigen Blazor-Handler.

## Befunde

1. **Hoch – E-01 ist als Pflicht-E2E registriert, testet den geforderten Auswahl- und Kopierfluss aber nicht.**  
   `TerminalText_LiveMarkCopyAndKeepSelection` in `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs:179` erzeugt nur einen Marker und prüft danach, dass Ausgabe sichtbar ist und der Prozess läuft. Es wird weder eine Textauswahl im echten Live-Terminal per Maus oder Tastatur angelegt, noch die sichtbare Markierung per Pixelvergleich geprüft, weitere Ausgabe zur Prüfung des Auswahl-Erhalts bzw. der Scrollback-Nachführung erzeugt, `Ctrl+Shift+C` gesendet oder der Clipboardtext kontrolliert. Der XML-Kommentar verlagert diese Nachweise ausdrücklich auf Replay; das widerspricht dem verbindlichen, eigenständigen E-01-Szenario.  
   **Korrektur:** Den Live-Terminal-Helper um tatsächliche Zellkoordinaten, Mausdrag, Tastaturauswahl, Screenshot-/Pixelnachweis, Clipboard-Retry und eine kontrollierte Ausgabe außerhalb der Auswahl ergänzen. Nach einem deterministischen Scrollback-Fall muss dieselbe Auswahl nach Rückscrollen sichtbar sein und exakt denselben Text kopieren.

2. **Hoch – E-02 prüft die sichtbare Markierung nicht und deckt den festgelegten UI-Nachweis damit nicht ab.**  
   `ReplayText_MarkAndCopy` in `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs:22` führt Mausdrag und Clipboardvergleich aus, nimmt jedoch keinen Screenshot vor und prüft kein Pixel im erwarteten Auswahlrechteck. Damit kann der Test auch bestehen, wenn die Auswahl nicht sichtbar gezeichnet wird. Die festgelegte E-02-Abnahme verlangt ausdrücklich einen Pixelnachweis der Hervorhebung. Zusätzlich liest `GetClipboardText` in `:76` die Zwischenablage ohne den im Plan verlangten begrenzten Retry bei temporärer Belegung; dadurch ist der gemeinsame Windows-Clipboard-Test unnötig flakey.  
   **Korrektur:** Vor und nach der Auswahl denselben Terminalausschnitt erfassen und die hervorgehobenen Zellen robust vergleichen. Das Lesen der Zwischenablage in einem STA-Thread mit begrenzten Wiederholungen bei Clipboardbelegung kapseln. Der Tastaturdurchlauf sollte ebenfalls mit der dokumentierten Fokus-/Caret-Position und seinem erwarteten Clipboardtext nachgewiesen werden.

## Positiv geprüft

- Die zuvor beanstandete Behandlung des Cursors hinter der letzten Spalte ist in `TerminalControl.ExtendSelection` durch Klemmen der Spalte behoben und durch einen Test abgedeckt.
- Der Snapshot normiert nach Resize auch die Scrollback-Versionsarrays auf die aktuelle Spaltenzahl (`TerminalBuffer.GetSnapshot`); dafür gibt es einen gezielten Test.
- Bei normalem Vollbild-Scrollen werden Zeilen-ID und Zellversionen nun zusammen verschoben (`TerminalBuffer.ScrollRangeUp`, `:332`), sodass `TerminalControl.TryNormalizeSelection` die Auswahl weiterhin derselben logischen Zeile zuordnen kann. Der zugehörige Control-Test prüft Auswahl und Kopiertext nach dem Scrollen.
- `Ctrl+Shift+C` wird gegen vorhandene Auswahl behandelt, ohne CLI-Bytes zu schreiben; `Ctrl+C` bleibt im bestehenden Eingabepfad unverändert.

## Prüfung

- `rg -n "RaiseUiActionRequested" src`: keine Vorkommen.
- `git diff --check`: keine Whitespace-Befunde; Git meldet ausschließlich die bekannte CRLF-Normalisierung.
- Tests wurden in diesem Review nicht erneut ausgeführt; der separate Testschritt ist für das Ausführungsergebnis maßgeblich.

## Ergebnis

Die Auswahlbindung für die normale Scrollback-Verschiebung ist im Code nun konsistent umgesetzt. Die zwei verbindlichen E2E-Abnahmen sind jedoch noch nicht vollständig: E-01 prüft den eigentlichen Featurefluss überhaupt nicht, E-02 lässt den sichtbaren Markierungsnachweis aus. Vor der Abnahme müssen beide Befunde behoben und die E2E-Szenarien erfolgreich ausgeführt werden.
