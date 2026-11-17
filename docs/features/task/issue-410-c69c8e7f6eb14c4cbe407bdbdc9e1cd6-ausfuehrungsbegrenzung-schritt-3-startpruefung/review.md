# Umsetzungs-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Gegenstand: Review der Umsetzung gegen [requirement.md](requirement.md) und
[plan.md](plan.md). Prüfbasis: Quellcode-Inspektion des Branch-Diffs sowie die
dokumentierten Läufe in [test-results.md](test-results.md).

## Umgesetzte Planelemente

- [x] **Ereignisgesteuerter Auslöser bei Prozessende** —
  `ScmStartPruefungsService` (gehosteter Singleton in `App.xaml.cs`) abonniert
  `IRunningAutomationStatusSource.RunningCountChanged` und löst ausschließlich
  bei sinkender Laufzahl (`currentCount < previousCount`, frei werdender Platz)
  die Startprüfung aus. `KiAusfuehrungsService` ruft `RaiseRunningCountChanged`
  im Exited-Pfad nach dem Entfernen des Prozess-Handles — Nicht-SCM-Läufe lösen
  ebenfalls aus, ohne die SCM-Zählung zu verfälschen
  (`ScmStartPruefungsService.cs:159-164`).
- [x] **Auslöser bei Pausierung** — `ScmStartPruefungsAusloeser`
  (`StartpruefungAnfordern`) wird auf allen drei Pausen-Setzpfaden nach der
  Persistenz benachrichtigt: `AufgabeService.SetPauseAsync` (nach
  `SaveChangesAsync`, bei Setzen wie Aufheben — beides verändert die
  Startbarkeit), `ScmEventStore.AcceptBatchAsync` (nur wenn
  `PauseForDelayAsync` tatsächlich eine Pause setzte, erst nach
  `transaction.CommitAsync`, damit die Prüfung den committeten Zustand sieht)
  und `KiPluginLimitService.VerarbeiteRateLimitAsync` (nach dem Speichern der
  Session-Limit-Pausen).
- [x] **Dieselbe Startprüfung wie der Poll** — die priorisierte
  Fälligkeitsauswahl wurde unverändert aus `ScmEventMonitoringService.RunOnceAsync`
  nach `ScmEventOrchestrator.DispatchDueEventsAsync` extrahiert; Poll und
  Trigger rufen dieselbe Methode. Serialisierung/Idempotenz kommen weiterhin
  aus `ScmTaskCoordinator`-Leases und der atomaren
  `PruefeUndReserviereStartPlatzAsync`-Zulassung — kein eigenes Starten, keine
  Umgehung.
- [x] **Kein zusätzlicher Poll** — der Dienst hängt ausschließlich an
  Ereignissen (`SemaphoreSlim(0,1)`-Signal, Koaleszierung von Häufungen,
  250-ms-Sammelphase über `TimeProvider`); der 60-s-Poll bleibt als
  Sicherheitsnetz bestehen.
- [x] **Laufzeit-Schalter beachtet** — `RunOnceAsync` liest
  `scm.monitoring.enabled` wie der Poll; deaktivierte Überwachung liefert auch
  ereignisgesteuert nichts zu (Test `StartPruefung_MonitoringDeaktiviert_KeinDispatch`).
- [x] **Startup-Gate** — `ScmEventStartupGate` wird wie beim Monitor
  abgewartet; Signale vor der DB-Migration sammeln sich im Signal
  (Abonnement bereits im Konstruktor).
- [x] **DI-Verdrahtung** — `App.xaml.cs`: `AddSingleton` Auslöser + Dienst +
  `AddHostedService`; `ScmDispatchFixture` spiegelt die Registrierung
  (`Ausloeser`, `StartPruefung`-Zugriff für Tests).
- [x] **Tests** — 7 Integrationsfälle (`ScmEventDispatchTests_Startpruefung`)
  decken Trigger-Pfad über `session.Exited`/`RunningCountChanged`, Pause
  aufheben, Verzögerungsdirektive, Session-Limit-Pause, überlappende Auslöser
  (kein Doppelstart, kein Überschreiten), deaktivierte Überwachung und
  Nicht-SCM-Lauf (korrekte Zählung + Auslösung). 4 Unit-Tests
  (`ScmStartPruefungsServiceTests`) prüfen die Signalfassade (Koaleszierung,
  nur Rückgang löst aus, Rückgang auf 0, Dispose trennt Abonnements).

## Abnahmekriterien

- [x] Nach Beendigung einer SCM-Ausführung startet ein wartendes Ereignis
  sofort (ohne Poll) — `StartPruefung_CliBeendet_WartendesEreignisStartetOhnePoll`.
- [x] Nach Pausierung wird sofort neu bewertet —
  `StartPruefung_PauseAufgehoben_EreignisStartetOhnePoll`,
  `StartPruefung_VerzoegerteAufnahme_LoestSignalAus`,
  `StartPruefung_RateLimitPause_LoestSignalAus`.
- [x] Überlappende Auslöser: kein Doppelstart, kein Überschreiten des Limits —
  `StartPruefung_UeberlappendeAusloeser_KeinDoppelstart`.
- [x] Nicht-SCM-Ausführungen lösen aus, ohne die Zählung zu verfälschen —
  `StartPruefung_NichtScmLauf_ZaehlungKorrektUndLoestAus`.
- [x] Skriptbare Zeit/Sessions: `ScmDispatchFixture` mit injizierbarem
  `TimeProvider` und Fake-Terminal-Sessions; `Sammelzeit` ist `TimeProvider`-
  gesteuert.

## Feststellungen

Keine. Die Umsetzung deckt Plan und Anforderung vollständig ab.
