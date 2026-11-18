# Bestandsaufnahme: Ereignisgesteuerte Startprüfung bei Beendet/Pausiert (Schritt 3, #410 Teil 2)

**Branch:** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-3-startpruefung`
**Basis:** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung` (enthält gemergte Schritte 1+2)
**Stand:** nach Commit `99c89bcc` (Schritt 2 gemergt, Schritt 3 in Arbeit)

## Detaildokumente

- `inventory/logic.md` — Auslösesignale, Dispatch-/Startprüfungs-Pfad, Sperr-/Serialisierungsbestand, relevante Codestellen mit Zeilen.
- `inventory/tests.md` — Test-Infrastruktur (`ScmDispatchFixture`, Testprojekte, Kategorien), betroffene Konstruktionsstellen.
- `inventory/test-results/` — Protokolle der Baseline- und Nach-Implementierung-Läufe.

## Kurzfassung

Die Ausführungsbegrenzung aus Schritt 2 wird heute ausschließlich über den 60-s-Poll des
`ScmEventMonitoringService` ausgelöst (plus UI-/Retry-Pfade pro Einzelereignis). Es existiert
**kein** ereignisgesteuerter Trigger, der bei freiwerdendem Slot oder gesetzter Pause die
Startprüfung sofort anstößt — die dafür nötigen Signale existieren aber bereits:

- `IRunningAutomationStatusSource.RunningCountChanged` (implementiert von `KiAusfuehrungsService`)
  feuert bei jeder Handle-Registrierung und jedem Prozessende (Beendet-Erkennung inkl.
  `ScmTaskCompletionMonitoringService`- und manueller Beendigungen).
- Die drei Pausen-Setzpfade `AufgabeService.SetPauseAsync`,
  `ScmEventStore.PauseForDelayAsync` und `KiPluginLimitService.VerarbeiteRateLimitAsync`
  schreiben `PausiertBisUtc` persistiert, ohne ein weitergehendes Signal zu senden.
- Die auszuführende Startprüfung ist die Batch-Schleife am Ende von
  `ScmEventMonitoringService.RunOnceAsync` (Z. 229-258) — derzeit privatem Kontext
  des Poll-Laufs, ohne eigene wiederverwendbare Methode.
- Serialisierung ist vollständig vorhanden: `ScmTaskCoordinator.Shared`-Gates je Aufgabe,
  `ScmTaskLeases`-Claim/Release im Orchestrator, atomare Slot-Reservierung
  `PruefeUndReserviereStartPlatzAsync` (prozessweiter Reservierungsbestand, 5-min-Verfall).
- Notifier-Muster existiert: `ScmEventChangeNotifier` (Singleton mit `Action`-Event),
  `AufgabeLaufdatenChangedNotifier`, `ScmEventStartupGate` (Start-Gate nach DB-Migration).

## Lücken (von Schritt 3 zu schließen)

- **L1:** Kein Trigger-Dienst: nichts abonniert `RunningCountChanged` für den SCM-Dispatch;
  ein freiwerdender Slot bleibt bis zum nächsten Poll (≤ 60 s) ungenutzt.
- **L2:** Kein Pausen-Signal: die drei Pausenpfade benachrichtigen niemanden; eine
  Neubewertung (z. B. nach Aufheben einer Pause) wartet ebenfalls auf den Poll.
- **L3:** Die Batch-Startprüfung ist nicht wiederverwendbar: sie liegt als Inline-Block
  am Ende von `RunOnceAsync` und ist an den Monitoring-Scope gebunden.
- **L4:** DI: `IRunningAutomationStatusSource` ist in der App registriert
  (`App.xaml.cs` Z. 314-315), fehlt aber im `ScmDispatchFixture` — für Trigger-Tests
  nachzurüsten.

## Risiken/Nebenbedingungen

- `RunningCountChanged` feuert (Z. 710) **vor** `PersistAusfuehrungBeendetAsync` (Z. 727):
  ein sofort lesender Trigger kann `AusfuehrungsStatus` noch als `Aktiv` sehen → kurze
  Sammelphase im Trigger-Dienst abfedern (Annahme, siehe `plan.md`).
- `RunningCountChanged`-Abonnenten müssen non-blocking sein (Invoke-Kontexte: `_startLock`-
  Umfeld der Startpfade bzw. Exited-Handler) → nur Signal setzen, nie synchron dispatchen.
- `AufgabeService`, `ScmEventStore`, `KiPluginLimitService` werden in Tests dutzendfach
  manuell konstruiert (~30 `new AufgabeService(…)`, ~11 `new ScmEventStore(…)`, ~3
  `new KiPluginLimitService(…)`) → neue Abhängigkeit nur als optionaler Ctor-Parameter.
