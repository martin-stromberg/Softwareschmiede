# Vorhandene Tests und Testluecken

## Protokollierung

`src/Softwareschmiede.Tests/ServiceIntegration/ProtocolLoggingServiceIntegrationTests.cs`

- `AddCliOutputAsync_StreichtAusgabeInProtokoll` ruft `AddCliOutputAsync` zweimal auf und erwartet zwei `CliOutput`-Eintraege (`:40`, `:44`, `:47`, `:49`).
- `AddCliOutputAsync_MehrereZeilen_SindInReihenfolgeGespeichert` prueft Reihenfolge mehrerer gespeicherter Zeilen (`:55`, `:60`, `:63`, `:66`).
- `AddStatusUebergangAsync_ErstelltEintragImProtokoll` prueft Statusprotokollierung (`:72`, `:76`, `:80`).

`src/Softwareschmiede.Tests/ServiceIntegration/RateLimitDetectionServiceIntegrationTests.cs`

- `MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt` prueft, dass `AddCliOutputAsync` bei Marker einen zusaetzlichen `RateLimit`-Eintrag erzeugt (`:32`, `:37`, `:38`, `:42`).
- `ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt` prueft den Parser isoliert (`:48`, `:50`).

## PseudoConsoleSession / Terminalausgabe

`src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`

- Lesefehler werden geloggt und beenden die Leseschleife sauber (`:19`).
- Cancellation/Dispose beendet bzw. unterbricht Leseschleifen robust (`:44`, `:60`, `:79`, `:159`).
- Concurrent Dispose wird genau einmal bereinigt (`:106`).
- `ReadLoopAsync_BufferChangedFiredAfterBufferUpdated` prueft, dass `BufferChanged` erst nach Buffer-Update feuert (`:191`, `:197`, `:205`).

`src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests_WritePromptAsync.cs`

- prueft Eingabe-/Prompt-Zeilenenden fuer `WritePromptAsync` (`:19`, `:37`).

## TerminalControl und UI-Bindung

`src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`

- Schreibfehler bei Eingabe werden geloggt (`:25`).
- Sessionwechsel registriert/deregistriert `BufferChanged` korrekt (`:65`, `:87`, `:115`).
- parallele Sessions beeinflussen ihre Buffer nicht gegenseitig (`:137`).
- Rueckwechsel zu alter Session erhaelt Bufferinhalt (`:166`).

`src/Softwareschmiede.Tests/App/Views/TaskDetailViewTests.cs`

- enthaelt aktuell nur einen statischen XAML-Test fuer Pull-Request-Button (`:10`). Keine direkte Abdeckung fuer Terminal-Protokollanzeige.

## KiAusfuehrungsService / ConPTY

`src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

- Basisverhalten ohne Prozess (`:29`, `:36`, `:43`, `:50`, `:58`).
- klassischer Prozessstart ueber OS-Interface (`:66`, `:92`).
- `GetPseudoConsoleSession` ohne Session (`:113`).
- Exited-Handler-Fehlerfaelle und Subscriber-Exceptions, inklusive ConPTY mit `SimulatedPseudoConsoleProcessLauncher` (`:126`, `:170`, `:191`, `:207`).
- ConPTY-Session-Dispose bei Prozessende und Service-Dispose (`:267`, `:306`).
- Race zwischen verzögertem Senden und Prozessende (`:353`).
- simulierter ConPTY-Launcher erreicht `Gestartet` ohne echtes ConPTY (`:421`).

## Testluecken fuer diese Anforderung

1. Kein Test prueft, dass Output aus `PseudoConsoleSession.ReadLoopAsync` automatisch `ProtokollService.AddCliOutputAsync` erreicht.
2. Kein Test prueft Chunk-zu-Zeile-Aggregation, insbesondere `\r`, `\n`, `\r\n`, ANSI-Sequenzen und unvollstaendige Zeilen am Prozessende.
3. Kein Test prueft, dass Ausgabe weiter protokolliert wird, wenn kein `TerminalControl` gebunden ist oder beim Taskwechsel eine andere Session angezeigt wird.
4. Kein Test prueft parallele Aufgaben mit getrennten `AufgabeId`-Protokollen.
5. Kein Test prueft DB-/Scope-Fehler waehrend Output-Protokollierung, ohne die Terminalausgabe oder den Prozess-Lifecycle zu stoeren.
6. Kein E2E-/Integrationstest verifiziert, dass nach Abschluss/Unterbrechung die CLI-Ausgabe in der Info-Protokollliste oder via `GetByAufgabeAsync` nachvollziehbar ist.

