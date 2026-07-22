# Code-Review: CLI-Output-Protokollierung

Status: Befunde vorhanden

## Befunde

### 1. CLI-Output-Queue bleibt unbegrenzt und kann bei hoher Ausgabe weiter unkontrolliert wachsen

- Schweregrad: Mittel
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:17`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:118`
- Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs:140`

Der vorherige Befund zur unbegrenzten Queue ist nur teilweise behoben. Die Implementierung zaehlt jetzt ausstehende Zeilen und loggt ab 1000 sowie danach alle weiteren 1000 Zeilen eine Warnung. Das macht Rueckstau sichtbar, begrenzt ihn aber nicht. Da weiterhin `Channel.CreateUnbounded<string>()` verwendet wird und jede Zeile einzeln ueber `ProtokollService.AddCliOutputAsync` inklusive DB-Speicherung persistiert wird, kann ein ausgabestarker CLI-Prozess schneller produzieren als die Datenbank schreibt. Dann waechst die Queue ohne harte Obergrenze im Prozessspeicher.

Konkretes Risiko: Lange Builds, Paketmanager, Testlaeufe oder fehlerhafte Prozesse mit sehr viel Output koennen die Anwendung durch Speicherwachstum destabilisieren. Die Warnung schuetzt nicht vor dem Ressourcenverbrauch; sie dokumentiert ihn nur nachtraeglich.

Empfehlung: Eine klare Hochlast-Strategie ergaenzen. Naheliegend waere batching-faehige Persistenz bei erhaltener Reihenfolge. Falls bewusst keine Ausgabe verworfen werden darf, sollte zumindest ein begrenzter Speicherpuffer mit Backpressure auf den Accumulator-/Sink-Pfad oder eine persistente Zwischenablage definiert werden. Dazu gehoert ein Stresstest, der deutlich mehr Zeilen produziert als die Persistenz sofort wegschreiben kann.

## Bewertung der vorherigen Befunde

- Tail-Output beim Prozessende: behoben im normalen Prozessende-Pfad. `CancelAndDisposeConPtyResourcesAsync` wartet vor `PseudoConsoleSession.Dispose()` begrenzt auf `DrainOutputAsync`, und die Session ruft `Complete()` erst im `ReadLoopAsync`-`finally` auf.
- `Complete()` garantiert keine Persistenz: behoben fuer den kontrollierten Abschluss. `ITerminalOutputSink.CompleteAsync(...)` wartet auf den Worker, und `KiAusfuehrungsService` nutzt diesen Pfad beim ConPTY-Cleanup.
- Unbegrenzte Queue: teilweise behoben durch Warn-Logging, aber als Ressourcenrisiko weiterhin offen.

## Testluecken

- Es fehlt ein Hochlast-/Backlog-Test fuer den Writer, bei dem die Persistenz kuenstlich langsam ist und viele Zeilen schneller eingereiht werden als sie gespeichert werden koennen. Ohne diesen Test bleibt das Ressourcenrisiko der unbegrenzten Queue unquantifiziert.
- Die Drain-Race-Tests decken den normalen Tail-Drain ab. Der Timeout-Fallback bei dauerhaft blockierter Leseschleife wird zwar geloggt, aber nicht fuer moeglichen Outputverlust oder Restqueue-Verhalten verifiziert.

## Ausgefuehrte Pruefungen

- `dotnet test --filter "CliOutputLineAccumulatorTests|CliOutputProtokollWriterTests|PseudoConsoleSessionTests|KiAusfuehrungsServiceTests"`
- Ergebnis: bestanden. `Softwareschmiede.Tests`: 48 Tests erfolgreich, 0 fehlgeschlagen. In `Softwareschmiede.IntegrationTests` passte kein Test zum Filter.

## Zusammenfassung

Die kritischen Lifecycle-Races aus der vorherigen Review sind im Normalpfad adressiert: Tail-Output wird vor dem Dispose gedraint und der Writer wartet beim Abschluss auf bereits angenommene Zeilen. Offen bleibt ein mittleres Ressourcenrisiko durch die weiterhin unbegrenzte Queue bei sehr hoher CLI-Ausgabe.
