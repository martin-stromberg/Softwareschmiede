# Inventory: Signale, Dispatch-Pfad und Serialisierung (Schritt 3)

Alle Angaben verifiziert auf Branch `task/issue-410-…-schritt-3-startpruefung` (Stand `99c89bcc`).

## 1. Beendet-Signal: `RunningCountChanged`

`src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`:
- `event Action<int, int>? RunningCountChanged` — Argumente: vorheriger und aktueller Zähler.
- `int GetRunningCount()`, `bool IsRunning(Guid)`, `AufgabeLaufStatus? GetLiveLaufStatus(Guid)`.

Implementiert von `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`:
- Z. 85: `public event Action<int, int>? RunningCountChanged;`
- Raise-Stellen: Z. 246 (`StartCliAsync`, Pipe-Start), Z. 416 (`StartTerminalSessionAsync`),
  Z. 710 (`HandleSessionEndedAsync` nach `_handles.TryRemove`).
- `RaiseRunningCountChanged` (Z. 774-786): zählt `GetRunningCount()` (nur `!Process.HasExited`),
  vergleicht mit `_previousRunningCount` unter `_runningCountLock`, invokiert synchron.
- **Reihenfolge im Endpfad** (Z. 701-728): `vorAufraeumenAsync` → `RaiseRunningCountChanged()` (Z. 710)
  → `PersistAusfuehrungBeendetAsync` (Z. 727, ruft `AufgabeService.AktivenLaufBeendenAsync` →
  `AusfuehrungsStatus = Beendet`) → `CliProcessStatusChanged` (Z. 728).
  ⇒ `RunningCountChanged` sieht den beendeten `AusfuehrungsStatus` noch **nicht** persistiert;
  ein Trigger braucht eine kleine Sammel-/Settle-Zeit, damit die Prioritätsklasse 1
  (`AusfuehrungsStatus.Beendet` + aktiver `Status`) korrekt greift.
- Abonnenten-Kontrakt (Z. 62-68, 76-82): Invokes laufen teils unter `_startLock` →
  Abonnenten müssen non-blocking sein, kein reentrantes Starten.
- Bestehender Abonnent als Muster: `AutoShutdownOrchestrator` (Z. 65 subscribe, Z. 179
  `OnRunningCountChanged`, Z. 414 unsubscribe).
- DI: `IRunningAutomationStatusSource` → `KiAusfuehrungsService` (`App.xaml.cs` Z. 314-315);
  im `ScmDispatchFixture` derzeit **nicht** registriert.

## 2. Pausen-Setzpfade

- `AufgabeService.SetPauseAsync` (`AufgabeService.cs` Z. 853-892): setzt `PausiertBisUtc`
  (`null` = Pause aufheben), schreibt `SystemMeldung`-Protokolleintrag, `SaveChangesAsync` Z. 891.
  Ctor: `(SoftwareschmiededDbContext, ILogger<AufgabeService>, TodoService)` — ~30 manuelle
  `new`-Aufrufe in Tests → optionale Zusatzabhängigkeit nötig.
- `ScmEventStore.PauseForDelayAsync` (`ScmEventStore.cs` Z. 139-154): private Methode,
  gesetzt nur wenn Status Neu/Gestartet/Wartend und `PausiertBisUtc < untilUtc` (nie verkürzend);
  Aufruf Z. 99 innerhalb der `AcceptBatchAsync`-Transaktion (Commit Z. 117, `notifier.Notify()` Z. 118).
  Ctor: `(SoftwareschmiededDbContext, ScmEventChangeNotifier, TimeProvider)` — ~11 manuelle
  `new`-Aufrufe.
- `KiPluginLimitService.VerarbeiteRateLimitAsync` (`KiPluginLimitService.cs` Z. 50-131):
  persistiert `plugins.sessionlimit.<prefix>`, pausiert alle aktiv laufenden Aufgaben desselben
  Prefix (Max-Semantik, Z. 105-116), `SaveChangesAsync` Z. 119, danach
  `NotifyLaufdatenChanged` je Aufgabe (Z. 127-130). Ctor mit 4 Pflichtparametern — 3 manuelle
  `new`-Aufrufe.
- `BlockReason` (`ScmEventOrchestrator.cs` Z. 565-567): `PausiertBisUtc > UtcNow` →
  „Aufgabe pausiert; Start gesperrt." — eine gesetzte Pause blockiert eigene Starts,
  ändert aber Priorisierung/Belegung anderer Kandidaten nicht direkt; der Trigger bewirkt
  eine sofortige **Neubewertung** (Anforderung: „Priorisierung/Freigabe wird neu bewertet").

## 3. Startprüfungs-Schleife (zu extrahieren)

`ScmEventMonitoringService.RunOnceAsync` Z. 229-258:
- Laufzeit-Schalter vorab: `!GetScmMonitoringEnabledAsync` → kompletter Durchlauf
  übersprungen (Z. 38-42) — muss für den Trigger-Pfad ebenfalls gelten.
- Batch-Query (Z. 235-242): `Sequence > afterSequence && <= upperSequence`,
  `Status != "Abgebrochen"` und (fällig ≠ „Verarbeitet" mit `NextAttemptUtc ≤ jetzt`
  **oder** fällige offene `ScmOutgoingActions`), `Take(100)`, Projektion in
  `ScmDispatchPrioritaet.EreignisAuswahl`.
- `ScmDispatchPrioritaet.OrderForDispatch` (Z. 253) mit `AufgabenZustand`-Map
  (`Status`, `AusfuehrungsStatus`, `PausiertBisUtc`, Z. 247-252).
- Je Ereignis eigener Scope → `ScmEventOrchestrator.DispatchAsync(item.Id)` (Z. 255-256).

## 4. Serialisierung/Idempotenz (Bestand, nicht zu umgehen)

- `ScmTaskCoordinator.Shared.RunAsync(taskId, …)` (`ScmTaskCoordinator.cs`): prozessweites
  `SemaphoreSlim`-Gate je Aufgabe + `AsyncLocal`-Besitzer für Verschachtelung.
- `ScmEventOrchestrator.DispatchAsync` (Z. 23-28): lädt `AufgabeId`, dann `RunAsync` →
  `DispatchCoreAsync`; `ClaimTaskAsync`/`ReleaseTaskAsync` (`ScmEventStore` Z. 157-170,
  SQLite-`INSERT … ON CONFLICT` atomar, 60-s-Lease + Renewal).
- `PruefeUndReserviereStartPlatzAsync` (`ScmEventExecutionLimitService` Z. 85-110):
  `ZulassungsGate` serialisiert Check+Claim; `Reservierungen` prozessweit (static);
  Verfall `ReservierungsLebensdauer` = 5 min; Orchestrator hält Reservierung über das
  Startfenster (Z. 190-208) → keine Limit-Überschreitung bei Nebenläufigkeit.
- Idempotenz des Dispatchs: Status-Gates in `DispatchCoreAsync` („Abgebrochen",
  „Verarbeitet", `DeliveryAttemptId`-Reconcile, ältere offene Ereignisse derselben
  Aufgabe, `BlockReason`, Trigger-/Revalidierung) — ein zweiter Pass auf dasselbe
  Ereignis ist harmlos.

## 5. Notifier-/Dienst-Muster (Anlehnung)

- `ScmEventChangeNotifier` (`ScmEventOptions.cs` Z. 15-21): Singleton, `event Action? Changed`,
  `Notify()`. In `ScmEventStore` injiziert.
- `ScmEventStartupGate` (`ScmEventOptions.cs` Z. 24-31): Singleton, hält Hintergrunddienste
  bis nach DB-Migration (`WaitAsync`/`Ready`).
- `PeriodicBackgroundService` (`PeriodicBackgroundService.cs`): abstrakte Basisklasse
  (`RunOnceAsync` + `Task.Delay(interval, TimeProvider)`); `ScmEventMonitoringService` und
  `ScmTaskCompletionMonitoringService` leiten davon ab — **nicht** geeignet für den
  ereignisgesteuerten Trigger (feste Poll-Schleife), dieser erbt `BackgroundService` direkt.
- DI-Registrierung Hintergrunddienste (`App.xaml.cs` Z. 233-238, 307-310):
  `AddSingleton<T>(); AddHostedService(sp => sp.GetRequiredService<T>());`
