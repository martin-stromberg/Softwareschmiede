# Tasks: Ereignisgesteuerte Startprüfung bei Beendigung oder Pausierung (Schritt 3, #410)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `ScmEventOrchestrator.DispatchDueEventsAsync`: priorisierte Fälligkeitsauswahl unverändert aus dem Poll extrahiert; Poll und Trigger teilen denselben Codepfad | Erledigt | `ScmEventDispatchTests_Startpruefung.*`, unverändert grüne Monitor-/Dispatch-Tests |
| 2 | Logik | `ScmStartPruefungsService` (gehostet): abonniert `RunningCountChanged` (nur Rückgang) + `ScmStartPruefungsAusloeser`; `SemaphoreSlim(0,1)`-Koaleszierung, 250-ms-Sammelphase über `TimeProvider`, `ScmEventStartupGate`, `scm.monitoring.enabled`-Gate | Erledigt | `ScmStartPruefungsServiceTests` (4 Tests) |
| 3 | Logik | Pausen-Setzpfade melden den Auslöser nach Persistenz: `AufgabeService.SetPauseAsync`, `ScmEventStore` (Verzögerungsdirektive, nach Commit, nur bei tatsächlicher Pause), `KiPluginLimitService.VerarbeiteRateLimitAsync` | Erledigt | `StartPruefung_PauseAufgehoben_*`, `StartPruefung_VerzoegerteAufnahme_*`, `StartPruefung_RateLimitPause_*` |
| 4 | Logik | DI-Verdrahtung `App.xaml.cs` (Singleton + HostedService) und `ScmDispatchFixture` | Erledigt | `General_BasisUndEinstellungen_E2E` (echter App-Start) |
| 5 | Tests | Integration: Slot-Freigabe ohne Poll, Idempotenz/kein Doppelstart, Monitoring-Schalter, Nicht-SCM-Lauf mit korrekter Zählung | Erledigt | `ScmEventDispatchTests_Startpruefung` (7 Fälle, 331/331 grün) |
| 6 | Doku | `docs/help/plugins/github-ereignisse/` (ablauf-technisch, business-rules), `README.md`, `docs/RELEASE_NOTES.md`, `changes.log` | Erledigt | – |
