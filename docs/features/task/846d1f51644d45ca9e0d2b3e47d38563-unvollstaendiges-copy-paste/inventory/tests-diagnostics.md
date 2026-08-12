# Tests und Diagnose

## Relevante Dateien

- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs`
- `src/Softwareschmiede.Tests/App/Controls/KeyToVt100EncoderTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests_WritePromptAsync.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`
- `src/Softwareschmiede.Tests/Helpers/TestPseudoConsoleSessionFactory.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs`

## Vorhandene Abdeckung

`KeyToVt100EncoderTests` prueft:

- einzeilige Clipboard-Kodierung;
- LF- und CRLF-Normalisierung zu `\r`;
- Unicode-Kodierung;
- leere und `null`-Eingaben;
- Tabs/Sonderzeichen;
- alleinstehendes CR.

`TerminalControlTests.ClipboardPaste` prueft:

- `Ctrl+V` setzt `Handled`;
- ein einfacher Clipboard-Text wird kodiert und in den Input-Stream geschrieben;
- `ReadClipboardAndInsertAsync` schreibt newline-normalisierte Bytes;
- leere Zwischenablage schreibt nichts;
- Schreib-/Clipboard-Fehler werden geloggt;
- `MarkInputActivity` wird nach erfolgreichem Schreiben aufgerufen.

`PseudoConsoleSessionTests_WritePromptAsync` prueft:

- Prompt-Submit nutzt einzelnes `\r`, kein CRLF;
- eingebettete Prompt-Zeilenumbrueche werden zu `\r` normalisiert.

`TestPseudoConsoleSessionFactory` erlaubt Unit-Tests ohne echte ConPTY-Handles. Dadurch kann ein kontrollierter Input-Stream fuer Paste-/Prompt-Tests injiziert werden.

## Testluecken bezogen auf die Anforderung

- Kein Test nutzt einen langen mehrzeiligen Stacktrace-Text oder einen vergleichbar grossen Clipboard-Inhalt.
- Kein Test prueft, dass sehr lange Clipboard-Bytes vollstaendig ankommen, wenn der Stream nur partielle Writes oder langsame Verarbeitung simuliert.
- Kein Test prueft Chunking-Reihenfolge, weil es aktuell kein Chunking gibt.
- Kein Test prueft konkurrierende Writes auf denselben `InputStream` (z. B. Paste waehrend Promptversand oder mehrfaches `Ctrl+V`).
- Kein Test prueft, dass Clipboard-Paste nach dem Schreiben flusht.
- Kein Test prueft Zielsession-Stabilitaet, wenn `TerminalControl.Session` waehrend eines laufenden Paste-Vorgangs wechselt.

## Diagnosemoeglichkeiten

Fuer die Umsetzung ist temporaere oder gezielte Diagnose im Paste-/Input-Pfad sinnvoll:

- Laenge des Clipboard-Texts in Zeichen und Bytes;
- Ziel-Aufgabe bzw. Session-Identitaet;
- Anzahl geplanter Chunks und Chunk-Groessen;
- geschriebene Byteanzahl je Chunk, falls ein kontrollierter Writer eingefuehrt wird;
- Fehler beim Write/Flush mit Chunk-Index;
- Abbruch wegen Session-Ende oder Cancellation.

Dauerhaftes Logging sollte auf Debug-Level oder nur bei Fehlern erfolgen, damit produktive CLI-Nutzung nicht mit grossen Paste-Inhalten protokolliert wird. Insbesondere duerfen vollstaendige Clipboard-Inhalte nicht geloggt werden.

## Empfohlene Teststrategie

Die niedrigste stabile Testebene ist ein Unit-Test gegen eine neue Session-nahe Schreibmethode, z. B. `WriteInputAsync` oder `PasteTextAsync`. Dieser Test kann einen Stacktrace-aehnlichen mehrzeiligen String kodieren lassen und anschliessend die exakt erwarteten Bytes im Memory-/Recording-Stream vergleichen.

Falls Chunking eingefuehrt wird, sollte ein Recording-Stream oder Writer-Adapter erfassen, in welcher Reihenfolge Chunks geschrieben wurden. Der Test muss sicherstellen, dass die Konkatenation aller Chunks exakt dem erwarteten UTF-8-Bytearray entspricht.

Ein zusaetzlicher `TerminalControl`-Test sollte belegen, dass `Ctrl+V` die neue zentrale Methode nutzt bzw. die Session stabil snapshotet. Ein E2E-Test gegen echte Claude-CLI ist wegen Nichtdeterminismus und externer Abhaengigkeiten weniger geeignet als Primaertest, kann aber spaeter als manuelle Reproduktionshilfe dienen.
