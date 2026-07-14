# CLI-Ausfuehrung und Laufstatus

## Prozessverwaltung

`KiAusfuehrungsService` ist der zentrale Singleton fuer laufende KI-CLI-Prozesse. Er implementiert `IRunningAutomationStatusSource`.

Wichtige Punkte:

- `_handles` haelt aktive Prozesse je Aufgabe.
- `IsRunning(Guid)` prueft, ob der konkrete Prozess noch lebt.
- `GetRunningCount()` zaehlt alle lebenden Prozesse.
- `RunningCountChanged` wird nach Start/Stop ausgeloest.
- `StartCliAsync()` startet klassisch via `Process`.
- `StartWithPseudoConsoleAsync()` startet ueber ConPTY und haelt eine `PseudoConsoleSession`.
- `StopCliAsync()` versucht `CloseMainWindow()`, wartet 5 Sekunden und killt dann den Prozessbaum.

Relevante Stellen:

- `KiAusfuehrungsService.cs:17-24`: Singleton, Interface, Handle-Speicher.
- `KiAusfuehrungsService.cs:46-74`: `IsRunning()` und `GetRunningCount()`.
- `KiAusfuehrungsService.cs:90-165`: klassischer CLI-Start.
- `KiAusfuehrungsService.cs:179-255`: ConPTY-Start.
- `KiAusfuehrungsService.cs:267-301`: Stop-Logik.
- `KiAusfuehrungsService.cs:404-444`: Exit-Handler und Statusereignis.

## Persistierter Laufstatus

`CliProcessManager` verbindet Prozessereignisse mit der Datenbank:

- Bei `CliProcessStatus.Gestartet` startet er Heartbeat, setzt `AktiveRunId`, setzt initial `LaufStatus = Laeuft` und abonniert `RuntimeStatusChanged`.
- Bei `Gestoppt`/`Fehler` stoppt er Heartbeat, entfernt Event-Subscriptions und loescht aktiven Laufstatus.
- Laufzeitwechsel der `PseudoConsoleSession` werden nach `AufgabeLaufStatus` uebersetzt und ueber `AufgabeService.AktualisiereLaufStatusAsync()` persistiert.

Relevante Stellen:

- `CliProcessManager.cs:122-141`: Reaktion auf Start/Stop/Fehler.
- `CliProcessManager.cs:153-174`: aktiven Lauf setzen/beenden.
- `CliProcessManager.cs:177-207`: RuntimeStatus-Subscription.
- `CliProcessManager.cs:219-247`: Uebersetzung in `AufgabeLaufStatus`.
- `AufgabeService.cs:458-515`: Persistenz fuer `AktiveRunId`, Heartbeat, `LetzterCliStartUtc`, `LaufStatus`.
- `Aufgabe.cs:47-63`: Domain-Felder `AktiveRunId`, `LastHeartbeatUtc`, `LetzterCliStartUtc`, `LaufStatus`.

## Wartet-auf-Eingabe-Erkennung

`PseudoConsoleSession` fuehrt einen Timer mit 1-Sekunden-Takt. Der Status wird aus Prozesszustand, Startzeit und letzter Ein-/Ausgabe berechnet. Der Default-Threshold liegt bei 4 Sekunden ohne I/O.

Relevante Stellen:

- `PseudoConsoleSession.cs:21-23`: Timer, Threshold, Startzeit.
- `PseudoConsoleSession.cs:98-118`: Output/Input-Aktivitaet markiert `Laeuft`.
- `PseudoConsoleSession.cs:218-245`: periodische Statusberechnung.
- `PseudoConsoleSession.cs:312-320`: `CliRuntimeStatus` mit `Inaktiv`, `Laeuft`, `WartetAufEingabe`.
- `PseudoConsoleSession.cs:337-368`: `CliRuntimeStatusEvaluator.Determine()`.
- `AufgabeLaufStatus.cs:21-27`: Domain-Substatus `Laeuft`/`WartetAufEingabe`.

Wichtig: Die Erkennung ist heuristisch. "Keine I/O-Aktivitaet seit 4 Sekunden" bedeutet "wartet vermutlich auf Benutzereingabe", nicht garantiert. Die Anforderung akzeptiert genau diese fachliche Unterscheidung, sollte aber in der Planung als bestehende Statusdefinition benannt werden.

## Geeignete Quelle fuer Update-Risikopruefung

`IRunningAutomationStatusSource` reicht nicht aus, um "aktiv und nicht wartend" zu erkennen, denn es liefert nur Anzahl und `IsRunning(Guid)`. Fuer die Update-Risikopruefung ist `AufgabeService.GetAktiveAufgabenAsync()` geeigneter:

- Liefert Aufgaben mit Status `Gestartet` oder `Wartend`.
- Enthält `AktiveRunId`, `LastHeartbeatUtc`, `LaufStatus`.
- Sortiert nach letztem CLI-Start.

Relevante Stelle: `AufgabeService.cs:534-549`.

Naheliegende Regel: Riskant sind Aufgaben mit `AktiveRunId != null` und `LaufStatus != AufgabeLaufStatus.WartetAufEingabe`. Bei `LaufStatus == null` sollte konservativ "riskant" angenommen werden, weil der Prozess aktiv sein kann, aber kein ConPTY-Substatus vorliegt.
