# Code-Review: CLI-Output-Protokollierung

Status: Befunde vorhanden

## Befunde

### 1. Cleanup kann bei Backpressure noch nicht gequeute Zeilen aus einem bereits gelesenen Chunk verwerfen

- Schweregrad: Mittel
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:59`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:64`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:78`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:124`
- Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:594`
- Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:599`

Die bounded-Queue-/Backpressure-Umsetzung begrenzt den Speicherpuffer jetzt wirksam, aber der Abschluss hat unter Rueckstau einen neuen Verlustpfad. `OnOutputChunk` dekodiert einen gelesenen Terminal-Chunk zuerst in `completedLines` und queued diese Zeilen danach ausserhalb des Locks einzeln. Ist die Queue voll, blockiert `TryQueueLine` synchron in `WaitToWriteAsync().AsTask().GetAwaiter().GetResult()`.

Wenn der Prozess in diesem Zustand beendet wird, wartet `CancelAndDisposeConPtyResourcesAsync` nur zwei Sekunden auf `DrainOutputAsync`. Laeuft der Reader wegen Backpressure noch in `OnOutputChunk`, kann dieser Drain timeouten. Danach ruft der Cleanup `OutputSink.CompleteAsync(...)` auf; `Complete()` setzt `_completed` und schliesst den Channel mit `TryComplete()`. Die parallel noch laufende `OnOutputChunk`-Schleife versucht anschliessend die restlichen Zeilen ihres bereits gelesenen `completedLines`-Arrays zu queuen, bekommt nach dem Channel-Abschluss aber `WaitToWriteAsync() == false` und verwirft diese Zeilen still.

Konkretes Risiko: Genau im Hochlastfall, den die bounded Queue adressieren soll, kann Tail-Output verloren gehen, obwohl die Bytes bereits aus dem Terminal-Stream gelesen und dekodiert wurden. Das betrifft lange Builds/Testlaeufe oder CLI-Fehlerausgaben mit starkem Burst am Prozessende.

Empfehlung: Den Abschluss mit dem aktiven Producer synchronisieren. Moegliche Varianten: `CompleteAsync` darf den Channel erst nach dem Ende einer laufenden `OnOutputChunk`-Queuephase schliessen; oder der Sink bekommt einen expliziten "no more chunks"-Pfad, der nur von der ReadLoop nach deren Ende gesetzt wird, waehrend der externe Cleanup lediglich auf Drain wartet und bei Timeout nicht selbst den Writer completed. Ergaenzend sollte ein Test den Ablauf simulieren: Queue voll, `OnOutputChunk` blockiert, Cleanup/`CompleteAsync` laeuft parallel, Persistenz wird danach freigegeben; erwartet wird, dass keine bereits dekodierten Zeilen verloren gehen oder der Verlust explizit als Timeout-Verlust dokumentiert und gezaehlt wird.

## Bewertung der vorherigen Befunde

- Tail-Output beim normalen Prozessende: weiterhin behoben. `PseudoConsoleSession.DrainOutputAsync(...)` wartet vor `Dispose()` auf die Leseschleife, und die ReadLoop completed den Sink erst im `finally`.
- `Complete()` garantiert keine Persistenz: im kontrollierten Abschluss weiterhin behoben. `CliOutputProtokollWriter.CompleteAsync(...)` wartet auf den Worker und `KiAusfuehrungsService` nutzt diesen Pfad beim ConPTY-Cleanup.
- Unbegrenzte Queue: behoben. `CliOutputProtokollWriter` verwendet jetzt `Channel.CreateBounded<string>(...)` mit `QueueCapacity = 4096` und `BoundedChannelFullMode.Wait`; bei voller Queue blockiert der Terminal-Output-Reader statt unbegrenzt Speicher zu belegen.

## Testluecken

- Es fehlt ein Race-Test fuer den Timeout-/Backpressure-Pfad: volle Queue, langsam/blockierte Persistenz, `OnOutputChunk` noch aktiv, paralleles `CompleteAsync`, danach Freigabe der Persistenz. Dieser Test wuerde den oben beschriebenen Verlustpfad sichtbar machen.
- Der vorhandene Hochlast-Test belegt Backpressure, prueft aber nicht, dass alle bereits gelesenen/dekodierten Zeilen nach parallelem Cleanup erhalten bleiben.
- Es fehlt weiterhin ein End-to-End- oder Integrationstest fuer einen echten bzw. simulierten ConPTY-Prozess mit sehr viel Output am Prozessende und langsamer Persistenz.

## Ausgefuehrte Pruefungen

- `dotnet test --filter "CliOutputLineAccumulatorTests|CliOutputProtokollWriterTests|PseudoConsoleSessionTests|KiAusfuehrungsServiceTests"`
- Ergebnis: bestanden. `Softwareschmiede.Tests`: 49 Tests erfolgreich, 0 fehlgeschlagen. In `Softwareschmiede.IntegrationTests` passte kein Test zum Filter.

## Zusammenfassung

Die konkrete unbounded-Queue aus `review-code.2.md` ist durch bounded Channel und Backpressure behoben. Offen bleibt ein mittleres Regressionsrisiko im Ressourcen-Lifecycle: Bei Backpressure plus Drain-Timeout kann der externe Cleanup den Writer abschliessen, waehrend der Terminal-Reader noch Zeilen aus einem bereits gelesenen Chunk queued, wodurch Tail-Output verloren gehen kann.
