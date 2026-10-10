# Usability-Review

## Ergebnis

**Status:** Keine Befunde

Geprüft am 2026-10-06 gegen den explizit vorgegebenen Basisbranch `origin/staging` (Merge-Base `1283e2300dabbb8b877f8d7a3a2fb0485394bd08`), einschließlich der aktuellen uncommitteten Änderungen. Einzige fachliche Eingabe war `requirement.md`; Plan und andere Review-Dateien wurden nicht gelesen.

Die geforderten Auswahl- und Kopierhandlungen sind anhand der implementierten Oberfläche ohne technische Kennungen oder Kenntnis interner Strukturen erreichbar. Der vorhandene Terminalbereich erhält unmittelbar bedienbare Maus- und Tastaturauswahl sowie einen deutsch beschrifteten Kopiereintrag im Kontextmenü. Die Auswahl wird farbig hervorgehoben, und das Menü zeigt das Kopierkürzel an.

Dies ist eine statische Bedienbarkeitsprüfung des UI-Codes und seines Diffs. Es wurde in diesem Review keine Anwendung gestartet und kein eigener visueller oder E2E-Test durchgeführt; der Bericht ersetzt diese Funktionsnachweise nicht.

## Befunde

Keine.

## Geprüfte Interaktionen

- CLI-Text mit der Maus markieren → unauffällig. Klicken und Ziehen erzeugt eine zusammenhängende, sichtbare Auswahl; auch rückwärts gezogene Bereiche werden normalisiert.
- CLI-Text mit der Tastatur markieren → unauffällig. Umschalt mit Pfeiltasten beziehungsweise Pos1/Ende erweitert die Auswahl; der ausgewählte Endpunkt wird bei Bedarf in den sichtbaren Ausschnitt gescrollt.
- Markierten Text in die Zwischenablage kopieren → unauffällig. Das Kontextmenü bietet „Kopieren“ und zeigt „Strg+Umschalt+C“ als Kürzel. Ohne gültige Auswahl ist der Menüeintrag deaktiviert. Ein Rechtsklick ersetzt die zuvor angelegte Auswahl nicht.
- Mehrzeiligen Text außerhalb der Konsole weiterverwenden → unauffällig. Die Kopierfunktion verwendet die ausgewählten Zeichen und Windows-Zeilenumbrüche; Auffüllleerzeichen am Zeilenende werden entfernt.
- Während angezeigter beziehungsweise weiterlaufender CLI-Ausgabe auswählen und kopieren → unauffällig. Änderungen außerhalb der ausgewählten Zellen lassen die Auswahl bestehen; Veränderungen der ausgewählten Inhalte machen sie ungültig, sodass kein unsichtbar veränderter Text unter einer alten Markierung kopiert wird.

## Geprüfte Dateien

- `src/Softwareschmiede.App/Controls/TerminalControl.cs` — vollständig gelesen; einzige gegenüber der Basis geänderte Produkt-UI-Datei. Die übrigen geänderten Quelldateien betreffen Terminal-Datenhaltung und Tests.
