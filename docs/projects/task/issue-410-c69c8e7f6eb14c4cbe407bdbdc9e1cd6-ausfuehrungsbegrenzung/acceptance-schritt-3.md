# Abnahmeprüfung – Entwicklungsschritt 3

Geprüft: „Ereignisgesteuerte Startprüfung bei Beendigung/Pausierung" (#410, Teil 2),
Commits `0be443ec` + `5812081e`, Diff gegen
`task/issue-410-…-ausfuehrungsbegrenzung` (Schritte 1+2).

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Beleg der Akzeptanzkriterien (eigene Code-Lektüre + Testlauf)

- **AK 1 (Beendigung → sofortige Prüfung):** `ScmStartPruefungsService` abonniert
  `IRunningAutomationStatusSource.RunningCountChanged` und löst nur bei
  `currentCount < previousCount` aus (`ScmStartPruefungsService.cs:159-166`).
  Alle produktiven Beendigungspfade laufen über dasselbe Signal:
  `KiAusfuehrungsService.HandleExitedCoreAsync` entfernt das Handle (Z. 687) und
  ruft `RaiseRunningCountChanged()` (Z. 710) — gilt für reguläres Exited,
  `TerminalSession.Failed` (Z. 678) und den HasExited-Früherkennungspfad (Z. 404).
  Manuelles Abschließen (`TaskDetailViewModel` →
  `EntwicklungsprozessService.AbschliessenAsync` → `StopCliAsync`, Z. 446) und der
  `ScmTaskCompletionMonitoringService`-Abschluss (Z. 138 → dieselbe Route) enden
  jeweils im Prozess-Exit und damit im Signal. Einen Pfad „Aufgabe beendet ohne
  Prozessende" gibt es produktiv nicht: `AufgabeService.AbschliessenAsync` wird
  ausschließlich über `EntwicklungsprozessService.AbschliessenAsync` (mit
  vorherigem `StopCliAsync`) erreicht — Direktaufrufe existieren nur in Tests.
  Entscheidend: Slot-Belegung (`ICliExecutionLimitSource.GetLaufendeAufgabenIds`)
  und Signalquelle (`GetRunningCount`) lesen dieselbe Handle-Registry — ein
  freiwerdender Platz korrespondiert 1:1 mit einem Zählungsrückgang.
- **AK 2 (Pausierung → Prüfung):** Alle drei benannten Setzpfade sind verdrahtet,
  jeweils erst nach Persistenz: `AufgabeService.SetPauseAsync` nach
  `SaveChangesAsync` (Z. 900-905, bei Setzen **und** Aufheben — das Aufheben ist
  der fachlich entscheidende Fall, der ein wartendes Ereignis startbar macht),
  `ScmEventStore.AcceptBatchAsync` nach `CommitAsync` und nur bei tatsächlich
  gesetzter Pause (`PauseForDelayAsync` → `bool`, Z. 128/148-165),
  `KiPluginLimitService.VerarbeiteRateLimitAsync` nach `SaveChangesAsync` und nur
  bei betroffenen Aufgaben (Z. 128-143; Early-Returns bei abgelaufenem Limit bzw.
  ohne Kandidaten lösen nichts aus).
- **AK 3 (Mehrfach-/Überlappungsauslöser):** `SemaphoreSlim(0,1)`-Koaleszierung
  ist korrekt: Ein Auslöser während laufender Prüfung setzt das Signal → genau
  eine Folgeprüfung; ein Auslöser im Sammelfenster wird vom Drain
  (`while (_signal.Wait(0))`, Z. 121) zusammengefasst. Kein Auslöser geht
  verloren, weil jede Prüfung eine vollständige Neubewertung aller fälligen
  Ereignisse ist — Koaleszierung ist damit fachlich verlustfrei. Überlappende
  `RunOnceAsync`-Aufrufe (Trigger + Poll + direkt) erzeugen weder Doppelstarts
  noch Limit-Überschreitungen: `ScmTaskCoordinator`-Aufgabensperre +
  `ScmTaskLeases` + atomare `PruefeUndReserviereStartPlatzAsync`
  (`ScmEventOrchestrator.cs:234-242`, `ZulassungsGate` in
  `ScmEventExecutionLimitService.cs:64/90-109`). Nicht-SCM-Läufe lösen über
  dasselbe Signal aus und werden korrekt mitgezählt (Belegung zählt alle
  laufenden Handles) — verifiziert durch
  `StartPruefung_NichtScmLauf_ZaehlungKorrektUndLoestAus`.
- **AK 4 (Tests):** 4 Unit-Tests (`ScmStartPruefungsServiceTests`) + 7
  Integrationsfälle (`ScmEventDispatchTests_Startpruefung` über
  `ScmDispatchFixture`; `TimeProvider` injizierbar, `Sammelzeit` intern setzbar).
  **Eigenständig nachgelaufen:** 7/7 Integrationstests grün (15 s), 4/4
  Unit-Tests grün; Builds beider Testprojekte 0 Warnungen/0 Fehler — die
  dokumentierten Ergebnisse in `test-results.md` sind bestätigt.

### Weitere Prüfpunkte

- **(c) Gleiche Zulassung wie Poll:** `DispatchDueEventsAsync` ist die wörtliche,
  unveränderte Extraktion der bisherigen Poll-Batch-Schleife (Diff-Vergleich:
  Fälligkeitsprädikat, `Sequence`-Fenster `Take(100)`, gebündelte
  Aufgabenzustände, `ScmDispatchPrioritaet.OrderForDispatch` identisch). Poll
  und Trigger führen exakt denselben Codepfad; der Poll bleibt als periodisches
  Sicherheitsnetz erhalten (`ScmEventMonitoringService.cs:229-233`).
- **(d) Retrigger-Risiko:** kein Loop — vom Trigger gestartete Läufe erhöhen die
  Zählung (gefiltert); jedes Prozessende erzeugt maximal eine Folgeprüfung, und
  jede Prüfung ist idempotent. Der `scm.monitoring.enabled`-Schalter greift auch
  ereignisgesteuert (`RunOnceAsync`, Z. 100).
- **(e) Lebensdauer/DI:** `AddSingleton` + `AddHostedService` auf dieselbe
  Instanz (`App.xaml.cs:311-314`); `IRunningAutomationStatusSource` →
  `KiAusfuehrungsService`-Singleton (Z. 318-319). Abonnements stehen bereits im
  Konstruktor — Signale vor dem `ScmEventStartupGate` sammeln sich koalesziert im
  Signal. `stoppingToken`-Abbruch sauber (`OperationCanceledException`-Filter);
  Fehler im Durchlauf werden geloggt und beenden die Schleife nicht; `Dispose`
  trennt die Abos vor dem Entsorgen des Signals, `ObjectDisposedException`/
  `SemaphoreFullException` werden im Handler abgefangen. Invoke-Kontext von
  `RunningCountChanged` (teils unter `_startLock`) wird nicht blockiert — der
  Handler ist nur `Release()`.
- **(f) CLIPBRD_E_CANT_OPEN:** plausibel umgebungsbedingt, keine Regression.
  Beide Tests (`TerminalControlTests.KeyInput.cs`, `[Fact]` — gehören zur
  stabilen Lane `Category!=OsInterface`) nutzen die **echte** WPF-Zwischenablage;
  der Test-Helfer `SetClipboardTextWithRetry` dokumentiert `CLIPBRD_E_CANT_OPEN`
  selbst als transienten Konfliktfall (`TerminalControlTests.ClipboardPaste.cs:22-56`).
  Letzte Änderung der Datei (`646d7e65`) ist Vorfahre der Diff-Basis — der
  Branch-Diff berührt weder `TerminalControl` noch diese Tests.
  Randnotiz: Die Tests sind faktisch OsInterface-nah (echte Zwischenablage),
  aber nicht als `OsInterface` kategorisiert — in dieser Sandbox potenziell
  wiederkehrend flaky, unabhängig vom Branch.
- **Kleinigkeit (Beobachtung, keine Abweichung):**
  `ScmEventOrchestrator.CancelAsync` (Z. 424-433) stuft eine durch die
  Verzögerungsdirektive gesetzte Pause beim Abbruch per `ExecuteUpdateAsync`
  zurück/auf, ohne den Auslöser zu benachrichtigen. Das ist kein benannter
  Pausen-*Setz*-Pfad (die Pause wird gelockert, nicht gesetzt) und das
  betroffene Ereignis ist ohnehin `Abgebrochen`; die Neubewertung anderer
  Ereignisse derselben Aufgabe übernimmt der Poll ≤ 60 s. Optionaler
  Verbesserungshinweis, kein Kriterien-Verstoß.
- **Sammelphase:** `RunningCountChanged` feuert vor
  `PersistAusfuehrungBeendetAsync` — die 250-ms-Sammelzeit (über `TimeProvider`)
  gibt dem Exited-Handler Zeit, `AusfuehrungsStatus = Beendet` zu persistieren,
  bevor `DispatchDueEventsAsync` die Aufgabenzustände liest. Restrisiko einer
  veralteten Sicht besteht theoretisch weiterhin, wird aber durch den
  Poll-Fallback und die per-Ereignis-Neuprüfung unter der Aufgabensperre
  abgefedert; fachlich nicht relevant.

### Selbst ausgeführte Verifikation

- `dotnet build src/Softwareschmiede.IntegrationTests/…` und
  `dotnet build src/Softwareschmiede.Tests/…` — je 0 Warnungen / 0 Fehler
  (inkl. transitiver `Softwareschmiede.App`-Build).
- `dotnet test …IntegrationTests --filter "FullyQualifiedName~ScmEventDispatchTests_Startpruefung"`
  — **7/7 bestanden** (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, synchron).
- `dotnet test …Tests --filter "FullyQualifiedName~ScmStartPruefungsServiceTests"`
  — **4/4 bestanden**.

Kein Voll-Lane-Lauf durch den Prüfagenten; die dokumentierten Gesamtläufe
(`test-results.md`: reguläre Lane 2646 grün / 2 Umgebungsfehler, Integration
331/331, Format-Check sauber) sind in den belastbaren Ausschnitten bestätigt.
