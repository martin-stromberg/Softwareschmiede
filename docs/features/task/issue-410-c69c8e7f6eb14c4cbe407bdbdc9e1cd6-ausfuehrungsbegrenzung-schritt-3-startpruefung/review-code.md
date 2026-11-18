# Code-Review

## Ergebnis

**Status:** Keine Befunde

Gegenstand: Vollständiger Branch-Diff gegen die Basis
`task/issue-410-…-ausfuehrungsbegrenzung` (Schritt-3-Implementierung).

## Geprüfte Aspekte

### Korrektheit / Concurrency

- **`ScmStartPruefungsService`** — Signal ist `SemaphoreSlim(0, 1)`: Häufungen
  koaleszieren zu höchstens einem eingereihten Durchlauf
  (`SemaphoreFullException` wird als Koaleszierung behandelt). `ExecuteAsync`
  wartet das `ScmEventStartupGate` ab, blockiert dann auf das Signal und führt
  nach einer 250-ms-Sammelphase (über `TimeProvider`, in Tests skriptbar) genau
  eine `RunOnceAsync`-Prüfung aus; weitere im Sammelfenster ankommende Signale
  werden vor dem Durchlauf abgebaut (`while (_signal.Wait(0))`). Fehler im
  Durchlauf werden geloggt und beenden die Schleife nicht; Abbruch über
  `stoppingToken` ist sauber (`OperationCanceledException`-Filter).
- **Serialisierung/Idempotenz** — der Dienst startet nichts selbst: Jeder
  Durchlauf ruft `ScmEventOrchestrator.DispatchDueEventsAsync` — dieselbe
  priorisierte Auswahl wie der Poll. Aufgabensperre (`ScmTaskCoordinator`),
  Leases und die atomare `PruefeUndReserviereStartPlatzAsync`-Zulassung bleiben
  die einzige Zulassungsinstanz; zwei gleichzeitige `RunOnceAsync`-Aufrufe
  (Test `StartPruefung_UeberlappendeAusloeser_KeinDoppelstart`) erzeugen weder
  Doppelstarts noch Limit-Überschreitungen.
- **Auslösefilter** — `OnRunningCountChanged` löst nur bei `currentCount <
  previousCount` (frei werdender Platz); Starts und unveränderte Zählungen
  lösen nicht aus. Der Invoke-Kontext von `RunningCountChanged` liegt teils
  unter der Start-Sperre des `KiAusfuehrungsService` — der Handler ist bewusst
  non-blocking (`_signal.Release()` nur), es gibt keine Möglichkeit eines
  Deadlocks.
- **Sichtbarkeit persistierter Zustände** — alle drei Pausenpfade lösen erst
  nach dem Persistieren aus: `SetPauseAsync` nach `SaveChangesAsync`,
  `AcceptBatchAsync` nach `transaction.CommitAsync` (und nur, wenn
  `PauseForDelayAsync` tatsächlich `true` zurückgab — ein bereits weiter
  reichender `PausiertBisUtc`-Wert oder ein nicht pausierbarer Status löst
  nicht aus), `VerarbeiteRateLimitAsync` nach `SaveChangesAsync` und nur bei
  tatsächlich betroffenen Aufgaben.
- **`DispatchDueEventsAsync`-Extraktion** — wörtlich unveränderter Code aus
  `ScmEventMonitoringService.RunOnceAsync` (Abbruch-Filter, Fälligkeitsprädikat,
  Batch-Fenster `Sequence`/`Take(100)`, gebündelte Aufgabenzustände,
  `OrderForDispatch`). Der Monitor delegiert nur noch; kein Verhaltensdelta im
  Poll-Pfad — abgesichert durch die unverändert grüne Gesamtsuite der
  Integrationstests (331/331).

### Lebenszyklen / Dispose

- `Dispose` trennt beide Abonnements vor dem Entsorgen des Signals;
  `OnAngefordert` fängt `ObjectDisposedException` für den Shutdown-Race —
  ein nach dem Dispose noch laufender Event-Invoke schlägt nicht fehl
  (Test `Dispose_TrenntAbonnements`).
- `ScmStartPruefungsAusloeser` ist ein zustandsloser Event-Verteiler; ohne
  Abonnenten (Reihenfolge beim App-Start oder in isolierten Unit-Fixtures)
  verfällt eine Anforderung wirkungslos — der Poll bleibt Fallback.

### API-Kompatibilität / Erweiterbarkeit

- Die neuen Konstruktor-Parameter (`ScmStartPruefungsAusloeser?`) sind an
  `AufgabeService`, `ScmEventStore` und `KiPluginLimitService` optional —
  alle bestehenden manuellen Konstruktionen in Tests bleiben kompilier- und
  lauffähig (volle Unit-Lane grün); die DI löst den Singleton automatisch auf.
- `PauseForDelayAsync` ist `private`; die `bool`-Rückgabe verändert keinen
  öffentlichen Vertrag.

### Dokumentation / Konventionen

- Alle neuen/veränderten öffentlichen Member tragen XML-Dokumentation
  (CS1591-konform, Build ohne Warnungen); die erweiterten Konstruktoren wurden
  mit vollständigen `<param>`-Einträgen versehen.
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: sauber.
- Namensgebung/Kommentarstil folgen den Bestandsmustern (deutsche Fachsprache,
  Begründungskommentare an den Trigger-Stellen).

### Testabdeckung

- Trigger-Pfad, Slot-Freigabe, Idempotenz, alle drei Pausen-Setzpfade,
  Monitoring-Schalter, Nicht-SCM-Signal und Signalfassade sind abgedeckt
  (11 neue Tests). Die Fixture-Session simuliert das produktive
  `PseudoConsoleSession.Exited`-Signal explizit (`session.Raise(Exited)`),
  weil `Mock<ITerminalSession>` kein automatisches Exited feuert — im Test
  als Kommentar dokumentiert.

## Feststellungen

Keine.
