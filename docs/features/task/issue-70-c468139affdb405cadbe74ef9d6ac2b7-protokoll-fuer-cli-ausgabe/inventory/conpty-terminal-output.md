# ConPTY- und Terminal-Ausgabepfad

## Aktueller Datenfluss

1. `KiAusfuehrungsService.StartWithPseudoConsoleAsync(...)` erstellt bzw. startet den ConPTY-Pfad. Die Methode kennt `aufgabeId`, Plugin, Arbeitsverzeichnis und erzeugt ueber `_launcher.Start(...)` Prozess und Session (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:179`, `:210`).
2. Der resultierende `CliProcessHandle` erhaelt `PseudoConsoleSession`, `SendCts` und natives Prozesshandle (`KiAusfuehrungsService.cs:219`).
3. `PseudoConsoleSession` startet im Konstruktor sofort `_readLoopTask = Task.Run(() => ReadLoopAsync(...))` (`src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs:94`).
4. `ReadLoopAsync` liest Bytes aus `OutputStream.ReadAsync(...)`, markiert Output-Aktivitaet, parst ANSI-Sequenzen, wendet Events auf `TerminalBuffer` an und feuert `BufferChanged` (`PseudoConsoleSession.cs:175`, `:185`, `:200`, `:202`, `:204`, `:206`).
5. Ein gebundenes `TerminalControl` reagiert auf `BufferChanged` nur mit `InvalidateVisual()` und rendert den aktuellen Buffer-Snapshot (`src/Softwareschmiede.App/Controls/TerminalControl.cs:82`, `:88`, `:102`, `:112`).

## Persistenzluecke

Der ConPTY-Output wird derzeit ausschliesslich in den Terminal-Buffer verarbeitet. `PseudoConsoleSession` hat keine `aufgabeId` und keine `ProtokollService`-Abhaengigkeit. Der Output-Pfad ruft `ProtokollService.AddCliOutputAsync` nicht auf.

Wichtig: Die Ausgabe wird chunkweise gelesen, nicht zwingend zeilenweise. `AddCliOutputAsync` ist auf einzelne Ausgabezeilen ausgelegt. Eine Implementierung muss daher entscheiden, ob sie:

- Chunks direkt persistiert,
- eine Zeilenaggregation vor `AddCliOutputAsync` einfuehrt,
- oder eine neue Persistenzmethode fuer rohe Terminal-Chunks ergaenzt.

## TerminalBuffer als Quelle ungeeignet fuer vollstaendige Historie

`TerminalBuffer` repraesentiert den aktuellen Terminalbildschirm. ANSI-Erase, Cursorbewegungen, Ueberschreiben und Resize koennen Historie verlieren oder veraendern. Fuer ein nachvollziehbares Protokoll des gesamten Verlaufs sollte die Persistenz vor oder parallel zur ANSI-Anwendung auf den Buffer erfolgen, also nahe am gelesenen Byte-/Textstrom in `ReadLoopAsync`.

## Encoding und ANSI

`ReadLoopAsync` uebergibt gelesene Bytes direkt an `AnsiSequenceParser.Parse(...)`. Fuer Protokollierung muss ein Text-Encoding explizit gewaehlt werden. Andere Terminalpfade verwenden UTF-8 fuer Eingaben (`PseudoConsoleSession.WritePromptAsync`, `TerminalControl`/`KeyToVt100Encoder`). Eine Output-Protokollierung sollte konsistent UTF-8 dekodieren, aber unvollstaendige Multibyte-Sequenzen ueber Chunk-Grenzen hinweg beruecksichtigen.

