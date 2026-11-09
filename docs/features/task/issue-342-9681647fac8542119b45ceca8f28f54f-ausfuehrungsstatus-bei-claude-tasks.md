# Tasks: Ausführungsstatus bei Claude — Synchronisation Aufgaben-Fußzeile ↔ Aufgabenliste (Issue #342)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `IRunningAutomationStatusSource` um `AufgabeLaufStatus? GetLiveLaufStatus(Guid aufgabeId)` erweitern | Offen | — |
| 2 | Logik | `KiAusfuehrungsService`: `GetLiveLaufStatus` implementieren (`null` ohne laufendes Handle, `WartetAufEingabe` bei `CliRuntimeStatus.WartetAufEingabe`, sonst `Laeuft`) | Offen | — |
| 3 | Logik | `KiAusfuehrungsService`: Event `CliLaufAktiv` (`Action<Guid>`) ergänzen | Offen | — |
| 4 | Logik | `KiAusfuehrungsService`: `NotifyLaufAktiv(Guid)` implementieren (feuert `CliLaufAktiv` nur bei `IsRunning`) | Offen | — |
| 5 | Logik | `KiAusfuehrungsService`: `GetLaufendeAufgabenIds()` implementieren (`IReadOnlyCollection<Guid>` laufender Handles) | Offen | — |
| 6 | Logik | `StartCliCoreAsync`: `CliLaufAktiv` im Early-Return „zweiter Start abgewiesen" (~Z. 138) vor `return existing` feuern | Offen | — |
| 7 | Logik | `StartTerminalSessionCoreAsync`: `CliLaufAktiv` im Early-Return (~Z. 233) vor `return existing` feuern | Offen | — |
| 8 | Logik | `AufgabeService.AktivenLaufSicherstellenAsync(Guid, AufgabeLaufStatus?, CancellationToken)` implementieren (idempotent: volle Laufdaten bei fehlender `AktiveRunId`, sonst Heartbeat-Auffrischung; terminale Stati → Skip ohne Wurf; `LetzterCliStartUtc ??= now`) | Offen | — |
| 9 | Logik | `CliProcessManager`: `CliLaufAktiv` im Konstruktor abonnieren (`OnCliLaufAktiv`) und in `Dispose` abmelden | Offen | — |
| 10 | Logik | `CliProcessManager.LaufdatenSicherstellenAsync(Guid)` implementieren (`IsRunning`-Guard, konditionelles `StartHeartbeat`, idempotentes `SubscribeRuntimeStatus`, `AktivenLaufSicherstellenAsync` mit Live-Substatus via `ExecuteWithAufgabeServiceAsync`) | Offen | — |
| 11 | Logik | `CliProcessManager`: `_abgleichTimer` + `AbgleichAsync` (30 s, iteriert `GetLaufendeAufgabenIds` → `LaufdatenSicherstellenAsync` je Aufgabe, fehlerisoliert); Start im Konstruktor, Räumung in `Dispose` | Offen | — |
| 12 | Logik | `CliProcessManager.AktualisierungAsync`: `AktivenLaufSicherstellenAsync` statt `UpdateHeartbeatAsync` persistieren | Offen | — |
| 13 | Logik | `CliProcessManager.OnRuntimeStatusChanged`: `AktivenLaufSicherstellenAsync(aufgabeId, laufStatus)` statt `AktualisiereLaufStatusAsync`; private `AktualisiereLaufStatusAsync` entfernen | Offen | — |
| 14 | Logik | `CliProcessManager.SubscribeRuntimeStatus` idempotent machen (keine Re-Registrierung bei bereits abonnierter Session) | Offen | — |
| 15 | Logik | `EntwicklungsprozessService.CliNeustartenCoordinatedAsync`: `NotifyLaufAktiv(aufgabeId)` im Session-Alive-Early-Return (~Z. 263–268) aufrufen | Offen | — |
| 16 | UI | `AktiveAufgabePanelItem.LiveLaufStatus` (`AufgabeLaufStatus?`, `init`) ergänzen | Offen | — |
| 17 | UI | `MainWindowViewModel.MapAktiveAufgabePanelItem`: `LiveLaufStatus` aus `_runningStatusSource.GetLiveLaufStatus(aufgabe.Id)` befüllen | Offen | — |
| 18 | UI | `KiAusfuehrungsStatusKlassifikation.Klassifiziere`: `StatusDaten.LiveLaufStatus`-Feld + neuer Zweig nach `HasScheduledPrompt` (vor `AusfuehrungsStatus`-Prüfung): gesetzt → `Wartet`/`Laeuft` | Offen | — |
| 19 | UI | `TaskDetailViewModel.StartCliAndUpdateStateAsync`: `AktivenLaufSicherstellenAsync` + `NotifyLaufdatenChanged` statt `AusfuehrungAktivSetzenAsync` (F2) | Offen | — |
| 20 | UI | `TaskDetailViewModel.CliStoppenAsync`: `IsRunning`-Guard — `AktivenLaufBeendenAsync` + Beendet-UI nur bei tatsächlich beendetem Prozess; sonst `FehlerMeldung` + Lauf-Zustand behalten (F1) | Offen | — |
| 21 | Tests | `IRunningAutomationStatusSource`-Fakes erweitern: `E2EFakeRunningStatusSource`, `FakeRunningAutomationStatusSource` (AutoShutdown + Recovery), `ThrowingRunningAutomationStatusSource`, `Always{Not,}RunningAutomationStatusSource`, `MutatingNotRunningAutomationStatusSource`, `BarrieredNotRunningStatusSource` | Offen | — |
| 22 | Tests | `AufgabeServiceTests_AktiverLauf`: `AktivenLaufSicherstellenAsync`-Matrix (fehlende `AktiveRunId` → volle Laufdaten; bestehende → nur Heartbeat (+ optional `LaufStatus`); terminaler `Status` → Skip ohne Wurf; unbekannte Aufgabe → Exception) | Offen | — |
| 23 | Tests | `KiAusfuehrungsServiceTests`: `CliLaufAktiv` bei zweitem Start (statt `Gestartet`), `NotifyLaufAktiv` nur bei laufendem Prozess, `GetLiveLaufStatus`-Mapping, `GetLaufendeAufgabenIds` | Offen | — |
| 24 | Tests | `CliProcessManagerTests`: `CliLaufAktiv` → Laufdaten persistiert; `AktualisierungAsync` heilt gelöschte Laufdaten bei laufendem Prozess; `OnRuntimeStatusChanged` heilt fehlende `AktiveRunId`; `AbgleichAsync` stellt Laufdaten für laufende Aufgaben sicher | Offen | — |
| 25 | Tests | `KiAusfuehrungsStatusConverterTests` (+ Symbol-Pendant): `LiveLaufStatus`-Matrix (`Laeuft`→„▶ Läuft" auch bei `AusfuehrungsStatus=Beendet`/`AktiveRunId=null`; `WartetAufEingabe`→„⏸ Wartet"; `null`→unverändert; kein Vorrang vor `Pausiert`/`PromptInWartestellung`) | Offen | — |
| 26 | Tests | `MainWindowViewModelTests`: `MapAktiveAufgabePanelItem` befüllt `LiveLaufStatus` aus der Quelle | Offen | — |
| 27 | Tests | `TaskDetailViewModelTests`/`EntwicklungsprozessServiceTests`: `CliStoppenAsync` behält Laufdaten bei weiterlaufendem Prozess (F1); `StartCliAndUpdateStateAsync` schreibt volle Laufdaten (F2); `CliNeustartenCoordinatedAsync` meldet `CliLaufAktiv` bei lebender Session | Offen | — |
| 28 | E2E-Tests | `SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E` (`E2E_ArbeitsstatusAktualisierung.cs`, im `ConPty_WeitereLebenszyklusSzenarien_E2E`) konsolidiert erweitern: Wiederanbindung an lebende Session (Verlassen + erneutes Öffnen der Detailansicht) → Fußzeile „Ausführung läuft" ↔ Kachel „▶ Läuft" synchron; optional Divergenz-Nachspiel (per DB-Zugriff entfernte Laufdaten bei laufendem Prozess → Kachel bleibt „▶ Läuft"); danach Stopp → „✓ Bereit". In Sandbox als „Skipped" dokumentieren (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`), interaktiv laufen lassen | Offen | — |
