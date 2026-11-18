# Umsetzungsplan: Ereignisgesteuerte Startprüfung bei Beendet/Pausiert (Schritt 3, #410 Teil 2)

Umgesetzt wird der zweite, im Issue belegte Auslöser der Schritt-2-Startprüfung: Bei Beendigung oder Pausierung einer SCM-ausgeführten Aufgabe läuft sofort — nicht erst beim nächsten 60-s-Poll — dieselbe priorisierte Batch-Startprüfung an. Es wird kein neuer Poll eingeführt; die Erkennung dockt an `IRunningAutomationStatusSource.RunningCountChanged` (Beendet) und den drei Pausen-Setzpfaden (Pausiert) an.

## Umsetzungsidee

### 1. Batch-Startprüfung in den `ScmEventOrchestrator` heben

Die Schleife in `ScmEventMonitoringService.RunOnceAsync` (Z. 229-258: `upperSequence`-Fenster,
fällige Ereignisse + fällige Outbox-Aktionen, `ScmDispatchPrioritaet.OrderForDispatch`,
Dispatch je Ereignis in eigenem Scope) wird **unverändert** als neue öffentliche Methode in den
Orchestrator verschoben:

```csharp
/// <summary>Führt die Startprüfung aus: alle fälligen, nicht abgeschlossenen Ereignisse in
/// priorisierter Reihenfolge (Fortsetzung → fällige Verzögerung → Eingang) einzeln dispatchen.
/// Wiederholbar aufrufbar (Poll wie Trigger); Idempotenz/Sperren liegen in DispatchAsync.</summary>
public async Task DispatchDueEventsAsync(CancellationToken ct = default)
```

- Der Orchestrator bringt `db`, `scopes` (`IServiceScopeFactory`) und `time` bereits mit —
  kein neuer Konstrukturparameter.
- `ScmEventMonitoringService.RunOnceAsync` ersetzt den Inline-Block durch
  `await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchDueEventsAsync(ct);`
  (gleicher Scope → gleicher `DbContext` → identisches Verhalten).

### 2. Auslöser-Signal `ScmStartPruefungsAusloeser` (Singleton-Notifier)

Neue Datei `src/Softwareschmiede/Application/Services/ScmStartPruefungsService.cs`, darin ein
kleiner Notifier im `ScmEventChangeNotifier`-Muster:

```csharp
/// <summary>Fordert eine SCM-Startprüfung außerhalb des Poll-Zyklus an (Pausen-Setzpfade).</summary>
public sealed class ScmStartPruefungsAusloeser
{
    /// <summary>Eine Startprüfung wurde angefordert.</summary>
    public event Action? Angefordert;
    /// <summary>Fordert eine Startprüfung an; Häufungen werden vom Dienst zusammengefasst.</summary>
    public void StartpruefungAnfordern() => Angefordert?.Invoke();
}
```

### 3. `ScmStartPruefungsService` (Singleton, `BackgroundService`)

Dieselbe Datei. Konstruktor: `IServiceScopeFactory scopes`, `ScmStartPruefungsAusloeser ausloeser`,
`IRunningAutomationStatusSource laufzaehlung`, `ScmEventStartupGate startup`, `TimeProvider time`,
`ILogger<ScmStartPruefungsService> logger`.

- **Signal:** `SemaphoreSlim _signal(0, int.MaxValue)`. Handler sind bewusst non-blocking
  (Invoke-Kontext von `RunningCountChanged` liegt teils unter `_startLock`): nur `Release()`.
  - `ausloeser.Angefordert += …` → `Release()`.
  - `laufzaehlung.RunningCountChanged += …` → `Release()` nur bei `current < previous`
    (Slot freigeworden; Start-Ereignisse sind kein Auslöser).
  - Abonniert im Konstruktor — Auslöser vor Dienststart sammeln sich an und werden nach dem
    Startup-Gate einmal zusammengefasst abgearbeitet; `Dispose()`-Override meldet ab.
- **Schleife** (`ExecuteAsync`): `startup.WaitAsync` → Loop:
  `_signal.WaitAsync` → kurze Sammelphase `Task.Delay(Sammelzeit, time, ct)` →
  verbleibende Signale per `_signal.Wait(0)` zusammenfassen → `RunOnceAsync`.
  Fehler je Durchlauf protokollieren (`LogError`), OCE beim Stoptoken sauber beenden.
- **Sammelphase** `Sammelzeit = 250 ms`: bündelt Burst-Auslöser und gibt dem Exited-Handler Zeit,
  `AusfuehrungsStatus = Beendet` zu persistieren (`RunningCountChanged` feuert vor
  `PersistAusfuehrungBeendetAsync`, inventory/logic.md §1) — sonst liefe die Prioritätsklasse 1
  für Fortsetzungsereignisse der soeben beendeten Aufgabe transient in Klasse 3.
  `internal` setzbar (`Sammelzeit`-Property) für Tests mit Fake-`TimeProvider`.
- **`RunOnceAsync`** (öffentlich, testbar, wie `PeriodicBackgroundService`-Konvention):
  neuer Scope → `GetScmMonitoringEnabledAsync` prüfen (gleicher Laufzeit-Schalter wie der
  Poll — deaktivierte Überwachung stellt auch getriggert nichts zu) →
  `ScmEventOrchestrator.DispatchDueEventsAsync(ct)`.
- **Serialisierung:** Single-Consumer-Schleife serialisiert Trigger-Durchläufe untereinander;
  Überschneidungen mit einem Poll-Durchlauf oder UI-Dispatch sind durch
  `ScmTaskCoordinator`-Gates, `ScmTaskLeases`-Claim und die atomare Slot-Reservierung
  (`PruefeUndReserviereStartPlatzAsync`) idempotent — kein Doppelstart, keine
  Limit-Überschreitung. Nicht-SCM-Ausführungen lösen über dasselbe `RunningCountChanged` aus;
  die Zählung bleibt unverändert der Schritt-2-Logik überlassen.

### 4. Pausenpfade melden den Auslöser (optionaler Ctor-Parameter)

Neue optionale Abhängigkeit `ScmStartPruefungsAusloeser? startPruefungsAusloeser = null`
(jeweils letzter Parameter, damit alle manuellen `new`-Aufrufe in Tests unverändert bleiben;
`ActivatorUtilities` injiziert den Singleton, sobald registriert):

- `AufgabeService` → Feld `_startPruefungsAusloeser`; am Ende von `SetPauseAsync` nach
  `SaveChangesAsync`: `_startPruefungsAusloeser?.StartpruefungAnfordern();` — bei Setzen **und**
  Aufheben der Pause (beides ändert die Startbarkeit: Aufheben entblockt, Setzen bewertet
  Priorisierung neu; Annahme A1).
- `ScmEventStore` → Feld; `PauseForDelayAsync` liefert `bool` (Pause tatsächlich gesetzt?);
  `AcceptBatchAsync` merkt sich das und ruft nach `transaction.CommitAsync` + `notifier.Notify()`
  `startPruefungsAusloeser?.StartpruefungAnfordern()` auf — bewusst **nach** dem Commit,
  damit der Trigger den persistierten Zustand sieht.
- `KiPluginLimitService` → Feld; am Ende von `VerarbeiteRateLimitAsync` nach den
  `NotifyLaufdatenChanged`-Aufrufen: `_startPruefungsAusloeser?.StartpruefungAnfordern();`
  (nur im Pfad mit tatsächlich pausierten Aufgaben).

### 5. DI-Verdrahtung

`App.xaml.cs` (SCM-Block, nach `ScmTaskCompletionMonitoringService`):

```csharp
services.AddSingleton<ScmStartPruefungsAusloeser>();
services.AddSingleton<ScmStartPruefungsService>();
services.AddHostedService(sp => sp.GetRequiredService<ScmStartPruefungsService>());
```

`IRunningAutomationStatusSource` ist bereits registriert (`App.xaml.cs` Z. 314-315).

### 6. `ScmDispatchFixture` nachrüsten

- `services.AddSingleton<IRunningAutomationStatusSource>(sp => sp.GetRequiredService<KiAusfuehrungsService>());`
- `services.AddSingleton<ScmStartPruefungsAusloeser>();`
- `services.AddSingleton<ScmStartPruefungsService>();`
- `services.AddScoped<KiPluginLimitService>();` + `services.AddSingleton<AufgabeLaufdatenChangedNotifier>();`
  (für den Rate-Limit-Pausenpfad-Test).
- Komfort-Accessoren: `Ausloeser` (`ScmStartPruefungsAusloeser`), `StartPruefung`
  (`ScmStartPruefungsService`).

## Annahmen (dokumentiert, engste anforderungskonforme Wahl)

- **A1 — Pausen-Aufhebung löst ebenfalls aus:** `SetPauseAsync` meldet Setzen und Aufheben
  (`null`); ein aufgehobener Pausenzustand entblockt wartende Ereignisse, die dann ohne
  Poll-Wartezeit starten. Deckt die Anforderung „Pausierung → Startprüfung" ab und ist
  durch die Idempotenz gefahrlos.
- **A2 — 250-ms-Sammelphase:** „Unmittelbar" im Sinne des Issues (statt bis zu 60 s Poll);
  dient Burst-Bündelung und dem Persistenz-Nachlauf des Exited-Handlers (§1 inventory/logic.md).
- **A3 — `DispatchDueEventsAsync` im Orchestrator:** der Orchestrator besitzt bereits
  `DispatchAsync`/Leases/Zulassung; die Batch-Auswahl gehört fachlich zum Dispatch, nicht
  zur Beobachtung. Poll und Trigger teilen exakt denselben Code (Single Source).
- **A4 — Keine Trigger bei `ScmTaskCompletionMonitoringService`-Fachabschluss nötig:**
  `AbschliessenAsync` beendet den CLI-Prozess → `RunningCountChanged` deckt den Fall ab;
  Ereignisse beendeter Aufgaben bleiben ohnehin via `BlockReason` gesperrt.
- **A5 — Keine Trigger für Outbox-/Reconcile-only-Fälligkeit:** Hostaktionen sind unbegrenzt;
  der Trigger zielt auf Startplätze. Die getriggerte Prüfung holt fällige Hostaktionen
  dennoch mit ab (Teil der geteilten Schleife) — unschädlich.
- **A6 — Optionale Ctor-Parameter statt Pflicht-DI:** ~44 manuelle `new`-Aufrufe in Tests
  bleiben kompilierfähig; Produktiv-DI injiziert den Singleton automatisch.

## Tests

**Integration (`src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Startpruefung.cs`, `ScmDispatchFixture`):**

1. `StartPruefung_CliBeendet_WartendesEreignisStartetOhnePoll` — globales Limit 1, zwei
   Ereignisse; erstes läuft, zweites `Wartend`; Trigger-Dienst gestartet;
   `fixture.Cli.StopCliAsync(erste Aufgabe)` → innerhalb kurzer Wartezeit: zweite Session
   gestartet, zweites Ereignis `Zugestellt`, genau 2 Sessions (kein Poll-Aufruf dazwischen).
2. `StartPruefung_PauseAufgehoben_EreignisStartetOhnePoll` — Pause via `SetPauseAsync` →
   Dispatch `Wartend` („Aufgabe pausiert; Start gesperrt."); Dienst gestartet;
   `SetPauseAsync(…, null)` → Ereignis startet ohne Poll.
3. `StartPruefung_VerzoegerteAufnahme_LoestSignalAus` — `Angefordert`-Zähler am Auslöser;
   `AcceptBatchAsync` mit `#verzögert`-Body → Signal ausgelöst (PauseForDelayAsync-Pfad).
4. `StartPruefung_RateLimitPause_LoestSignalAus` — `VerarbeiteRateLimitAsync` mit
   zukünftigem Reset auf Aufgabe mit Prefix → Signal ausgelöst.
5. `StartPruefung_UeberlappendeAusloeser_KeinDoppelstart` — Limit 1; ein Dispatch läuft;
   mehrere `StartpruefungAnfordern()` + direkter `RunOnceAsync`-Aufruf + `Monitor.RunOnceAsync()`
   überlappend → weiterhin genau 1 Session, kein Start über das Limit.
6. `StartPruefung_NichtScmLaufBeendet_ZaehlungBleibtKorrekt` — ein nicht-SCM-CLI-Beenden
   löst den Lauf aus (nachweisbar über `Angefordert`/Durchlauf), verändert die
   Belegungszählung nicht (zweites Ereignis wartet weiter, solange Limit belegt;
   nach tatsächlichem Freiwerden startet es).
7. `StartPruefung_MonitoringDeaktiviert_KeinDispatch` — `scm.monitoring.enabled` aus,
   Trigger aktiv → kein Session-Start (Laufzeit-Schalter respektiert).

**Unit (`src/Softwareschmiede.Tests/Application/Services/ScmStartPruefungsServiceTests.cs`):**

- `RunningCountChanged` mit `current < previous` → Signal gesetzt (`AusstehendePruefungen`);
  mit `current > previous`/`==` → kein Signal; `StartpruefungAnfordern` → Signal.
- `ScmEventStore`-/Notifier-Unit-Tests entfallen zugunsten der Integrationsnachweise (3, 4).

**Fake-`TimeProvider`:** Test 6/7-Varianten sowie ein dedizierter Fall
(`StartPruefung_VerzoegertesEreignisNachZeitfortschritt_StartetOhnePoll`) nutzen
`new ScmDispatchFixture(time: fakeTime)` und `service.Sammelzeit = TimeSpan.Zero`, um
`NextAttemptUtc`-Fälligkeit skriptbar vorzutreiben.

**E2E:** Kein UI-Benutzerfluss betroffen (reine Backend-Verdrahtung) → keine neuen
FlaUI-Tests; reale End-to-End-Abnahme in dieser Umgebung nicht durchführbar
(laufende App/kein SCM-Remote) → dokumentiert in `test-results.md`.

## Offene Punkte

Keine.

## Akzeptanzszenarien

1. **Happy Path Beendet:** Globales Limit 1; Aufgabe A läuft via SCM-Ereignis, Ereignis B wartet
   mit Limit-Grund. A's CLI endet (manuell gestoppt oder regulär) → `RunningCountChanged` →
   Trigger → B wird vorrangig geprüft und gestartet, ohne dass der 60-s-Poll abgewartet wird.
2. **Happy Path Pausiert:** Aufgabe pausiert (Direktive/manuell/Rate-Limit) → Startprüfung läuft;
   nach Aufheben der Pause startet ein wartendes Ereignis sofort statt beim nächsten Poll.
3. **Robustheit:** Burst von Auslösern (mehrere Exits + Pause) → genau eine (bzw. wenige)
   serialisierte Prüfung(en); kein Doppelstart, kein Start über Limit; deaktivierte
   Überwachung → nichts startet; Poll bleibt als Fallback wirksam.
