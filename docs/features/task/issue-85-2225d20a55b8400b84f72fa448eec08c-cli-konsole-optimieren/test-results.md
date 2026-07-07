# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

**Hinweis zum Umfang:** Auf Anwenderentscheidung wurde nur die Unit-Test-Suite gewertet (`--filter "FullyQualifiedName!~E2E"`). Die FlaUI/WPF-E2E-Tests scheitern in dieser Automatisierungsumgebung unabhängig von den Code-Änderungen dieser Anforderung an einem Umgebungsproblem (fehlende/verschwindende `Softwareschmiede.App.runtimeconfig.json` und `Microsoft.Data.Sqlite`-Ladefehler beim Start der frisch gebauten App durch parallele Testhost-Prozesse) und werden hier bewusst nicht gewertet.

## Fehlgeschlagene Tests

### TerminalControlTests_ClipboardPaste.cs

- **OnPreviewKeyDown_CtrlV_SetsHandledTrue** — `Keyboard.Modifiers` erkennt die per FlaUI (SendInput) simulierte Strg-Taste in dieser Sandbox-Umgebung nicht (globale Tastatur-Modifikator-Simulation funktioniert hier nicht); Code-Logik wurde durch Review als korrekt bestätigt.
- **OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync** — dieselbe Umgebungsursache wie oben.

### KiAusfuehrungsServiceTests.cs

- **ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash** — `System.InvalidOperationException: Cannot process request because the process (...) has exited.` Timing-Race in einem bestehenden, von dieser Anforderung nicht berührten Test (echter Kindprozess beendet sich, bevor `EnableRaisingEvents` gesetzt wird). Datei wurde in diesem Branch nicht verändert — vermutlich vorbestehende Flakiness, kein Regressions-Befund dieser Anforderung.

## Zusammenfassung

- Gesamt: 669
- Bestanden: 666
- Fehlgeschlagen: 3
- Übersprungen: 0

(E2E-Tests separat ausgeschlossen, siehe Hinweis zum Umfang.)

## Testabdeckung

**Abdeckung:** Nicht messbar (kein Coverage-Collector im Testlauf verwendet)

## Fehlende Tests

Keine zusätzlichen Lücken identifiziert; die im Plan vorgesehenen E2E-Testszenarien (Clipboard-Paste während Ausgabe, Multi-line-Paste, Stabilitätsverifikation) sind laut Plan-Review (`review.md`) nicht umgesetzt worden (siehe dort).
