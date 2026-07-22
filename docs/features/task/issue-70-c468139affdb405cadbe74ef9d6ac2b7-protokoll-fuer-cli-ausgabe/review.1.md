# Plan-Review: CLI-Ausgaben im Aufgabenprotokoll speichern

Status: Offene Aufgaben vorhanden

## Zusammenfassung

Die aktuelle Umsetzung deckt die geplante Kernfunktion ab: Terminal-Output wird ueber eine optionale `ITerminalOutputSink` aus `PseudoConsoleSession` gemeldet, in `KiAusfuehrungsService.StartWithPseudoConsoleAsync` pro `aufgabeId` an einen `CliOutputProtokollWriter` gebunden und ueber `ProtokollService.AddCliOutputAsync` in das bestehende Aufgabenprotokoll geschrieben.

Nicht vollstaendig umgesetzt ist die im Plan vorgesehene Testabdeckung. Mehrere explizit geplante Tests fuer Parallelitaet, Fehlerpfade und Rate-Limit-Integration ueber den neuen ConPTY-Pfad fehlen.

## Gepruefte Planpunkte

| Planpunkt | Bewertung | Nachweis |
|-----------|-----------|----------|
| `ITerminalOutputSink` einfuehren | Erfuellt | `src/Softwareschmiede/Infrastructure/Terminal/ITerminalOutputSink.cs` definiert `OnOutputChunk(ReadOnlySpan<byte>)` und `Complete()`. |
| `PseudoConsoleSession` an Output-Senke anbinden | Erfuellt | `PseudoConsoleSession` akzeptiert eine optionale Senke, ruft sie in `ReadLoopAsync` mit den gelesenen Bytes auf und schliesst sie im `finally` sowie im `Dispose` idempotent ab. |
| Terminal-Buffer-Verarbeitung unveraendert erhalten | Erfuellt | Nach `OnOutputChunk(...)` laufen `AnsiSequenceParser.Parse(...)`, `Buffer.Apply(...)` und `BufferChanged` weiter; der Test `ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin` prueft beides. |
| UTF-8-Zeilenaggregation implementieren | Erfuellt | `CliOutputLineAccumulator` nutzt einen zustandsbehafteten UTF-8-Decoder, trennt `\n`, `\r\n` und einzelnes `\r` und flusht Restzeilen. |
| Nicht blockierenden Protokoll-Writer implementieren | Erfuellt | `CliOutputProtokollWriter` kopiert/aggregiert Chunks synchron kurz und schreibt fertige Zeilen ueber einen `Channel<string>` in einem Hintergrund-Worker. |
| Scoped DI fuer `ProtokollService` verwenden | Erfuellt | `CliOutputProtokollWriter.PersistLineAsync` erzeugt pro Zeile einen Async-Scope und holt `ProtokollService` aus dem Scope. |
| Launcher- und Service-Anbindung ergaenzen | Erfuellt | `IPseudoConsoleProcessLauncher.Start(...)`, `Win32PseudoConsoleProcessLauncher` und `SimulatedPseudoConsoleProcessLauncher` reichen die optionale Senke bis zur `PseudoConsoleSession` durch; `KiAusfuehrungsService.StartWithPseudoConsoleAsync` erzeugt den Writer pro Aufgabe. |
| Aufraeumpfad robust machen | Erfuellt | `CliProcessHandle` haelt `OutputSink`; `CancelAndDisposeConPtyResources` ruft `Complete()` vor dem Session-Dispose auf. |
| Keine DB-Migration / kein neues Modell | Erfuellt | Es werden das bestehende `ProtokollService.AddCliOutputAsync` und `ProtokollTyp.CliOutput` verwendet. |
| UI-unabhaengige Persistenz | Erfuellt | Der Test `StartWithPseudoConsoleAsync_PersistiertSessionOutputAlsCliOutput` startet ueber einen Test-Launcher und prueft persistierte `CliOutput`-Eintraege ohne `TerminalControl`. |
| Geplante Testmatrix vollstaendig | Nicht erfuellt | Mehrere explizit geplante Tests sind im Workspace nicht vorhanden. |

## Offene Aufgaben

- [ ] Test fuer parallele Aufgaben ergaenzen: `StartWithPseudoConsoleAsync_ParalleleAufgaben_TrenntProtokolleNachAufgabeId` oder gleichwertig. Der Plan fordert den Nachweis, dass zwei Sessions Ausgaben nicht zwischen `aufgabeId`s vermischen; aktuell existiert nur ein Einzelaufgaben-Persistenztest.
- [ ] Test fuer Writer-Fehlerpfad ergaenzen: `CliOutputProtokollWriter_Persistenzfehler_BeeintraechtigtSessionNicht` oder gleichwertig. Der Plan fordert, dass Exceptions aus Scope-Erzeugung oder `ProtokollService` geloggt werden und nicht in den Lesepfad zurueckwirken.
- [ ] Test fuer Rate-Limit-Erkennung ueber den neuen ConPTY-/Writer-Pfad ergaenzen: `AddCliOutputAsync_RateLimitMarkerAusConPtyOutput_ErzeugtRateLimitEintrag` oder gleichwertig. Bestehende Tests pruefen `AddCliOutputAsync` direkt, aber nicht, dass ein Marker aus Session-Output ueber den neuen Pfad einen `RateLimit`-Eintrag erzeugt.
- [ ] Optional den Persistenztest um Restzeilen ohne abschliessenden Zeilentrenner auf Service-/Session-Ebene erweitern. Der Aggregator-Test deckt den Flush isoliert ab; der Plan nennt das Risiko Prozessende vs. Restzeilen explizit fuer den Session-Ende-Pfad.

## Keine offenen Planabweichungen

Fuer die Implementierung selbst wurden keine offenen Planabweichungen gefunden. Die fachlichen Akzeptanzkriterien sind durch die vorhandene Architektur plausibel abgedeckt: Erfassung in der Session-Leseschleife, Persistenz im bestehenden Aufgabenprotokoll, aufgabenbezogene Writer-Instanzen und Wiederverwendung von `AddCliOutputAsync`.

## Nicht ausgefuehrt

Es wurden keine Tests ausgefuehrt. Der Review basiert auf statischer Pruefung der Planartefakte, Inventardokumente und aktuellen Workspace-Umsetzung.
