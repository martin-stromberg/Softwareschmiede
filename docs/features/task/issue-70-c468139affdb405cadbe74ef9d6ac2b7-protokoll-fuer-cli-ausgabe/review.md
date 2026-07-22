# Plan-Review: CLI-Ausgaben im Aufgabenprotokoll speichern

Status: Vollstaendig umgesetzt

## Zusammenfassung

Die aktuelle Umsetzung erfuellt den Plan. Terminal-Output wird in `PseudoConsoleSession` UI-unabhaengig aus der Leseschleife an eine optionale `ITerminalOutputSink` gemeldet, in `CliOutputProtokollWriter` zeilenweise aggregiert und ueber `ProtokollService.AddCliOutputAsync` aufgabenbezogen als `CliOutput` persistiert.

Die vormals offenen Punkte aus `review.1.md` sind erledigt: Die fehlenden Tests fuer parallele Aufgaben, Writer-Fehlerpfade, Rate-Limit-Erkennung ueber den ConPTY-Pfad und Restzeilen auf Service-/Session-Ebene sind vorhanden.

Auch die kritischen Befunde aus `review-code.1.md` sind fuer den Plan-Review ausreichend geschlossen: Der Prozessende-Pfad wartet vor dem Dispose auf den ReadLoop-Drain, der Writer bietet `CompleteAsync(...)` mit Worker-Drain, und die Queue-Groesse wird ueber Zaehler und Warn-Logging beobachtbar gemacht. Die Queue bleibt bewusst unbounded; das entspricht dem Plan, der eine wachsende Queue als Risiko akzeptiert und Beobachtbarkeit statt Batching als Umsetzungsvorgabe nennt.

## Gepruefte Planpunkte

| Planpunkt | Bewertung | Nachweis |
|-----------|-----------|----------|
| `ITerminalOutputSink` einfuehren | Erfuellt | `src/Softwareschmiede/Infrastructure/Terminal/ITerminalOutputSink.cs` definiert `OnOutputChunk(...)`, `Complete()` und den neuen Drain-Pfad `CompleteAsync(...)`. |
| `PseudoConsoleSession` an Output-Senke anbinden | Erfuellt | `PseudoConsoleSession` akzeptiert optional eine Senke, ruft `OnOutputChunk(...)` nach erfolgreichem Read auf und schliesst die Senke am Ende der Leseschleife ab. |
| Terminal-Buffer-Verarbeitung unveraendert erhalten | Erfuellt | Nach dem Sink-Aufruf laufen `AnsiSequenceParser.Parse(...)`, `Buffer.Apply(...)` und `BufferChanged` weiter; `ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin` prueft beides. |
| UTF-8-Zeilenaggregation implementieren | Erfuellt | `CliOutputLineAccumulator` nutzt einen zustandsbehafteten UTF-8-Decoder, trennt `\n`, `\r\n` und einzelnes `\r` und flusht Restzeilen. |
| Nicht blockierenden Protokoll-Writer implementieren | Erfuellt | `CliOutputProtokollWriter` verarbeitet fertige Zeilen ueber einen sequenziellen Hintergrund-Worker und einen Channel. |
| Scoped DI fuer `ProtokollService` verwenden | Erfuellt | `PersistLineAsync` erzeugt pro Zeile einen Async-Scope und holt `ProtokollService` aus dem Scope. |
| Launcher- und Service-Anbindung ergaenzen | Erfuellt | `IPseudoConsoleProcessLauncher.Start(...)`, echte/simulierte Launcher und Test-Launcher reichen die optionale Senke bis zur `PseudoConsoleSession` durch; `KiAusfuehrungsService.StartWithPseudoConsoleAsync` erzeugt den Writer pro `aufgabeId`. |
| Aufraeumpfad robust machen | Erfuellt | `CliProcessHandle` haelt `OutputSink`; `CancelAndDisposeConPtyResourcesAsync` ruft zuerst `PseudoConsoleSession.DrainOutputAsync(...)`, danach `Dispose()` und anschliessend `OutputSink.CompleteAsync(...)` auf. |
| Keine DB-Migration / kein neues Modell | Erfuellt | Es werden weiterhin `ProtokollService.AddCliOutputAsync` und `ProtokollTyp.CliOutput` genutzt. |
| UI-unabhaengige Persistenz | Erfuellt | `StartWithPseudoConsoleAsync_PersistiertSessionOutputAlsCliOutput` prueft den Service-/Session-Pfad ohne `TerminalControl`. |
| Queue-Risiko beobachten | Erfuellt | `CliOutputProtokollWriter` fuehrt `_queuedLineCount` und loggt Warnungen ab Schwellwerten, wenn Persistenz hinter Terminal-Ausgabe zurueckfaellt. |
| Geplante Testmatrix | Erfuellt | Die geplanten Aggregator-, Session-, Writer- und Service-Tests sind vorhanden; zusaetzlich wurden Tests fuer die vorherigen Review-Befunde ergaenzt. |

## Vormals offene Punkte

| Quelle | Punkt | Bewertung | Nachweis |
|--------|-------|-----------|----------|
| `review.1.md` | Parallele Aufgaben trennen | Erledigt | `StartWithPseudoConsoleAsync_ParalleleAufgaben_TrenntProtokolleNachAufgabeId` prueft zwei `aufgabeId`s mit getrennten Ausgaben. |
| `review.1.md` | Writer-Persistenzfehler beeinflussen Session nicht | Erledigt | `CliOutputProtokollWriter_Persistenzfehler_BeeintraechtigtSessionNicht` prueft geloggte Exception ohne Rueckwurf in den Aufrufer. |
| `review.1.md` | Rate-Limit-Marker ueber neuen Pfad | Erledigt | `AddCliOutputAsync_RateLimitMarkerAusConPtyOutput_ErzeugtRateLimitEintrag` prueft `CliOutput` plus `RateLimit` aus Session-Output. |
| `review.1.md` | Restzeile auf Service-/Session-Ebene | Erledigt | `StartWithPseudoConsoleAsync_RestzeileOhneZeilentrenner_PersistiertCliOutput` prueft Restzeilenpersistenz ueber den Gesamtpfad. |
| `review-code.1.md` | Tail-Output kann beim Prozessende verloren gehen | Erledigt | `CancelAndDisposeConPtyResourcesAsync` wartet mit `DrainOutputAsync(...)` vor dem Session-Dispose; `StartWithPseudoConsoleAsync_ProzessEndeVorReadLoopDrain_VerliertTailOutputNicht` deckt die Race Condition ab. |
| `review-code.1.md` | `Complete()` garantiert keine Persistenz angenommener Zeilen | Erledigt | `ITerminalOutputSink.CompleteAsync(...)` und `CliOutputProtokollWriter.CompleteAsync(...)` warten begrenzt auf `_workerTask`; `CompleteAsync_DraintAngenommeneZeilen_BevorProviderDisposedWird` prueft den Drain. |
| `review-code.1.md` | Unbegrenzte Queue ohne Beobachtbarkeit | Erledigt fuer diesen Plan | Die Queue bleibt unbounded, aber `_queuedLineCount` und Warn-Logging machen Rueckstand sichtbar. Das entspricht dem Planrisiko; Batching oder bounded Queue waren nicht Teil des Plans. |

## Testabdeckung

| Bereich | Vorhandene Tests |
|---------|------------------|
| Zeilenaggregation | `Chunks_MitMehrerenLfZeilen_LiefertZeilenInReihenfolge`, `Chunks_MitGeteilterZeile_UeberChunkGrenze_LiefertEineZeile`, `Chunks_MitCrLf_ZaehltNichtDoppelt`, `Chunks_MitEinzelnemCr_FlushtProgressZeile`, `Chunks_MitUtf8MultibyteGrenze_DekodiertKorrekt`, `Flush_MitRestzeile_SpeichertUnvollstaendigeLetzteZeile` |
| Session-Anbindung | `ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin` |
| Persistenzpfad | `StartWithPseudoConsoleAsync_PersistiertSessionOutputAlsCliOutput`, `StartWithPseudoConsoleAsync_RestzeileOhneZeilentrenner_PersistiertCliOutput` |
| Parallele Aufgaben | `StartWithPseudoConsoleAsync_ParalleleAufgaben_TrenntProtokolleNachAufgabeId` |
| Rate-Limit-Integration | `AddCliOutputAsync_RateLimitMarkerAusConPtyOutput_ErzeugtRateLimitEintrag` |
| Fehler- und Drain-Pfade | `CliOutputProtokollWriter_Persistenzfehler_BeeintraechtigtSessionNicht`, `CompleteAsync_DraintAngenommeneZeilen_BevorProviderDisposedWird`, `StartWithPseudoConsoleAsync_ProzessEndeVorReadLoopDrain_VerliertTailOutputNicht` |

## Ausgefuehrte Pruefungen

- `dotnet test --filter "CliOutputLineAccumulatorTests|CliOutputProtokollWriterTests|PseudoConsoleSessionTests|KiAusfuehrungsServiceTests"`
- Ergebnis: bestanden. `Softwareschmiede.Tests`: 48 erfolgreich, 0 fehlgeschlagen, 0 uebersprungen. In `Softwareschmiede.IntegrationTests` passte kein Test zum Filter.

## Restrisiken

- Die Writer-Queue ist weiterhin unbounded. Das ist keine Planabweichung, weil der Plan diese Entscheidung zulaesst und nur Beobachtung/Logging fuer wachsende Queues fordert. Bei real sehr ausgabestarken CLI-Laeufen kann spaeter dennoch Batching oder eine bounded Queue sinnvoll werden.
