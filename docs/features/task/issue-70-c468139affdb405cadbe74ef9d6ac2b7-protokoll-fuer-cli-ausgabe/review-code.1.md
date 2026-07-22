# Code-Review: CLI-Output-Protokollierung

Status: Befunde vorhanden

## Befunde

### 1. Tail-Output kann beim Prozessende verloren gehen

- Schweregrad: Hoch
- Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:246`
- Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:253`
- Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:589`
- Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs:153`
- Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs:207`

Der Aufraeumpfad schliesst den `OutputSink` sofort, bevor garantiert ist, dass die `PseudoConsoleSession` den Output-Stream fertig gelesen und alle gelesenen Bytes an `OnOutputChunk` gemeldet hat. Das passiert sowohl ueber den `Exited`-Handler als auch im Early-Exit-Pfad. `PseudoConsoleSession.Dispose()` wartet bewusst nicht auf `_readLoopTask`; gleichzeitig setzt `CliOutputProtokollWriter.Complete()` `_completed`, sodass spaetere `OnOutputChunk`-Aufrufe verworfen werden.

Konkretes Risiko: Ein kurzlebiger CLI-Prozess kann direkt vor oder waehrend des Exited-Handlings noch Ausgaben im Pipe-/Stream-Puffer haben. Diese Bytes koennen im Terminal ggf. noch im Lesepfad liegen, werden aber nicht mehr protokolliert. Damit ist das Akzeptanzkriterium "gesamter Verlauf" gerade am Prozessende nicht verlaesslich erfuellt.

Empfehlung: `OutputSink.Complete()` sollte nur vom Ende der Leseschleife aus erfolgen oder es braucht einen expliziten Drain-/Completion-Pfad, der den Read-Loop-Abschluss mit kurzem Timeout abwartet. Der Prozess-Cleanup darf die Senke nicht vorzeitig fuer weitere Chunks sperren.

### 2. `Complete()` garantiert keine Persistenz der bereits angenommenen Zeilen

- Schweregrad: Hoch
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:57`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:71`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:74`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:91`

`Complete()` flusht den Accumulator und schliesst den Channel, wartet aber nicht darauf, dass `_workerTask` die Channel-Inhalte persistiert hat. Danach kann der DI-Container oder der zugehoerige `DbContext` bereits disposed werden; `PersistLineAsync` faengt `ObjectDisposedException` nur ab und verwirft die Zeile. Damit kann auch Output verloren gehen, der bereits erfolgreich in den Writer eingereiht wurde.

Konkretes Risiko: Beim normalen Service-Dispose, App-Shutdown oder schnellen Test-/Prozessende kehrt der Cleanup zurueck, obwohl noch Protokollzeilen in der Queue stehen. Die Ausgabe bleibt dann nicht "nach Abschluss oder Unterbrechung" nachvollziehbar.

Empfehlung: Den Writer als abschliessbare Ressource modellieren, z. B. `CompleteAsync(TimeSpan timeout)` oder `IAsyncDisposable`, und im kontrollierten Prozessende auf `_workerTask` warten. Fuer harte Shutdown-Pfade kann weiterhin ein kurzer Timeout mit Logging genutzt werden, aber der Normalfall sollte drainen.

### 3. Unbegrenzte Queue kann bei viel CLI-Output unkontrolliert wachsen

- Schweregrad: Mittel
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:14`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:93`

Der Writer verwendet `Channel.CreateUnbounded<string>()`, persistiert aber jede Zeile einzeln mit `AddCliOutputAsync` und damit jeweils mit `SaveChangesAsync`. Bei sehr ausgabestarken CLI-Laeufen kann die Leseschleife deutlich schneller produzieren als die Datenbank schreibt. Dann waechst die Queue ohne Limit und ohne Telemetrie, was Speicherverbrauch und Prozessstabilitaet gefaehrdet.

Empfehlung: Entweder eine begrenzte Queue mit klarer Backpressure-/Drop-Strategie definieren oder Persistenz batching-faehig machen. Wenn Vollstaendigkeit wichtiger ist als Backpressure, sollte zumindest Queue-Laenge/Verzoegerung beobachtbar sein und ein Test den Hochlastfall absichern.

## Testluecken

- Es fehlt ein Test fuer die Race Condition "Dispose/Exited vor ReadLoop-Drain": Ein Output-Stream sollte nach Prozessende noch verzögert Bytes liefern bzw. die Leseschleife blockieren, waehrend `CancelAndDisposeConPtyResources` ausgefuehrt wird. Erwartung: keine bereits im Stream vorhandene Schlussausgabe geht verloren.
- Es fehlt ein Test, der nach `Complete()` explizit auf den Writer-Drain wartet und beweist, dass alle eingereihten Zeilen vor Provider-/DbContext-Dispose gespeichert sind.
- Die im Plan vorgesehene Trennung paralleler Aufgaben ist nicht umgesetzt: Es gibt keinen Test fuer zwei parallele Sessions mit unterschiedlichen `aufgabeId`.
- Persistenzfehler im `CliOutputProtokollWriter` sind nicht getestet. Der Review kann daher nicht belegen, dass Exceptions zwar geloggt werden, aber die Terminalsession nicht beeinflussen.
- Die Rate-Limit-Erkennung ueber den neuen ConPTY-Pfad wird nicht getestet; bestehende `AddCliOutputAsync`-Tests decken nur den direkten Serviceaufruf ab.

## Ausgefuehrte Pruefungen

- `dotnet test --filter "CliOutputLineAccumulatorTests|PseudoConsoleSessionTests|KiAusfuehrungsServiceTests"`
- Ergebnis: bestanden, 42 Tests erfolgreich, 0 fehlgeschlagen. In `Softwareschmiede.IntegrationTests` passte kein Test zum Filter.

## Zusammenfassung

Die Umsetzung ist in der Richtung passend, aber der Ressourcen-Lifecycle ist fuer die neue Protokollierung noch nicht robust genug. Die kritischsten Risiken liegen am Session-Ende: Die Senke wird zu frueh geschlossen und ihr Hintergrund-Worker wird nicht gedraint. Dadurch kann genau die Ausgabe verloren gehen, die nach Abschluss oder Unterbrechung der Bearbeitung nachvollziehbar sein soll.
