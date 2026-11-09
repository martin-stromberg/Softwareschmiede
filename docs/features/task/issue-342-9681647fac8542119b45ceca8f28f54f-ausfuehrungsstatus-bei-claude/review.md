# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `IRunningAutomationStatusSource` (Interface) — erweitert: `AufgabeLaufStatus? GetLiveLaufStatus(Guid)` vorhanden (`src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs:28`)
- [x] Event `CliLaufAktiv` (`Action<Guid>`) in `KiAusfuehrungsService` — vorhanden (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:68`)
- [x] Methode `NotifyLaufAktiv(Guid)` in `KiAusfuehrungsService` — vorhanden, feuert nur bei `IsRunning` (Z. 112)
- [x] Methode `GetLaufendeAufgabenIds()` in `KiAusfuehrungsService` — vorhanden, `IReadOnlyCollection<Guid>`, Exception-tolerant (Z. 123)
- [x] Methode `GetLiveLaufStatus(Guid)` in `KiAusfuehrungsService` — vorhanden, Semantik `null`/`WartetAufEingabe`/`Laeuft` wie geplant (Z. 134)
- [x] `StartCliCoreAsync` — `CliLaufAktiv` im Early-Return „zweiter Start abgewiesen" vor `return existing` (Z. 186)
- [x] `StartTerminalSessionCoreAsync` — `CliLaufAktiv` im Early-Return analog (Z. 282)
- [x] Methode `AufgabeService.AktivenLaufSicherstellenAsync(Guid, AufgabeLaufStatus?, CancellationToken)` — vorhanden mit voller Idempotenz-Semantik: unbekannte Aufgabe → `InvalidOperationException`; terminale Stati (`Beendet`/`Archiviert`) → Log-Debug + Return; `AktiveRunId == null` → kompletter Lauf-Zustand inkl. `LetzterCliStartUtc ??= now`; sonst nur Heartbeat + optional `LaufStatus` (`src/Softwareschmiede/Application/Services/AufgabeService.cs:1060`)
- [x] `CliProcessManager`: `CliLaufAktiv`-Abonnement im Konstruktor + Abmeldung in `Dispose` — vorhanden (Z. 53, Z. 358)
- [x] Methode `LaufdatenSicherstellenAsync(Guid)` in `CliProcessManager` — vorhanden: `IsRunning`-Guard, konditionelles `StartHeartbeat` (nur wenn kein Timer in `_heartbeatTimers`), `SubscribeRuntimeStatus`, Live-Substatus-Mapping aus `GetTerminalSession(...)?.RuntimeStatus`, `ExecuteWithAufgabeServiceAsync` → `AktivenLaufSicherstellenAsync` (Z. 282–303)
- [x] Methode `OnCliLaufAktiv(Guid)` in `CliProcessManager` — vorhanden, fire-and-forget (Z. 268)
- [x] Feld `_abgleichTimer` + Methode `AbgleichAsync()` in `CliProcessManager` — vorhanden: `AbgleichIntervall = HeartbeatInterval` (30 s), Start im Konstruktor, iteriert `GetLaufendeAufgabenIds` fehlerisoliert pro Aufgabe, `Dispose` räumt Timer (Z. 24–25, 55–59, 308–319, 356)
- [x] `CliProcessManager.AktualisierungAsync` — persistiert via `LaufdatenSicherstellenAsync` statt `UpdateHeartbeatAsync` (Z. 114)
- [x] `CliProcessManager.OnRuntimeStatusChanged` — `Inaktiv` ignoriert; sonst `LaufdatenSicherstellenAsync` (intern → `AktivenLaufSicherstellenAsync` mit Live-Substatus) statt privatem `AktualisiereLaufStatusAsync` (Z. 256)
- [x] `CliProcessManager.SubscribeRuntimeStatus` — idempotent via `ReferenceEquals` auf bereits registrierte Session (Z. 211–222)
- [x] Private `CliProcessManager.AktualisiereLaufStatusAsync` — entfernt; `AufgabeService.AktualisiereLaufStatusAsync` (public) bleibt bestehen
- [x] `EntwicklungsprozessService.CliNeustartenCoordinatedAsync` — `NotifyLaufAktiv(aufgabeId)` im Session-Alive-Early-Return vor `return` (`src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:266–275`)
- [x] Eigenschaft `LiveLaufStatus` (`AufgabeLaufStatus?`, `init`) in `AktiveAufgabePanelItem` — vorhanden (`src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs:51`)
- [x] `MainWindowViewModel.MapAktiveAufgabePanelItem` — befüllt `LiveLaufStatus` aus `_runningStatusSource.GetLiveLaufStatus(aufgabe.Id)` (`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:403`)
- [x] `KiAusfuehrungsStatusKlassifikation` in `AppConverters.cs` — `StatusDaten.LiveLaufStatus`-Feld (`Aufgabe`-Pfad → `null`, `AktiveAufgabePanelItem`-Pfad → `item.LiveLaufStatus`); neuer Zweig nach `HasScheduledPrompt` und vor der `AusfuehrungsStatus`-Prüfung: `WartetAufEingabe` → `Wartet`, sonst gesetzt → `Laeuft` (Z. 152–153, 175–184, 215)
- [x] `TaskDetailViewModel.StartCliAndUpdateStateAsync` — `IsRunning`-Zweig persistiert via `AktivenLaufSicherstellenAsync` mit Live-Substatus + `NotifyLaufdatenChanged` statt `AusfuehrungAktivSetzenAsync` (`src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:1723–1734`)
- [x] `TaskDetailViewModel.CliStoppenAsync` — `IsRunning`-Guard nach `StopCliAsync`: läuft der Prozess weiter → `FehlerMeldung` + Return ohne `AktivenLaufBeendenAsync`/Beendet-UI (Z. 921–930)
- [x] Test-Fakes `IRunningAutomationStatusSource` — alle 8 handgeschriebenen Fakes implementieren `GetLiveLaufStatus`: `E2EFakeRunningStatusSource` (`RecoveryServiceIntegrationTests.cs:152`), `FakeRunningAutomationStatusSource` + `ThrowingRunningAutomationStatusSource` (`AufgabeRecoveryServiceTests.cs:316,329`), `FakeRunningAutomationStatusSource` (`AutoShutdownOrchestratorTests.cs:154`), `Always{Not,}RunningAutomationStatusSource`/`MutatingNotRunningAutomationStatusSource`/`BarrieredNotRunningStatusSource` (`Softwareschmiede.IntegrationTests/.../AufgabeRecoveryServiceTests.cs:146,156,177,197`)
- [x] Tests `AufgabeServiceTests_AktiverLauf` — 5 `AktivenLaufSicherstellenAsync`-Tests vorhanden (volle Laufdaten, `LaufStatus`-Übernahme, Bestandsschutz + Heartbeat, terminaler Skip, Exception bei unbekannter Aufgabe)
- [x] Tests `KiAusfuehrungsServiceTests` — `ZweiterStart_FeuertCliLaufAktiv_StattGestartet`, `NotifyLaufAktiv_FeuertNur_BeiLaufendemProzess`, `GetLiveLaufStatus_BildetSessionStatusAb`, `GetLaufendeAufgabenIds_ListetNurLaufendeHandles` vorhanden
- [x] Tests `CliProcessManagerTests_AktiverLauf` — `CliLaufAktiv_StelltLaufdatenSicher_BeiWiederverwendung`, `AktualisierungAsync_HeiltGeloeschteLaufdaten_BeiLaufendemProzess`, `AbgleichAsync_StelltLaufdatenSicher_FuerLaufendeAufgaben` vorhanden
- [x] Test `CliProcessManagerTests_LaufStatus.OnRuntimeStatusChanged_HeiltFehlendeAktiveRunId` — vorhanden; `VollerZyklus_*` und `RaisesRuntimeStatusChanged_AfterGestoppt_*` weiter vorhanden
- [x] Test `CliProcessManagerTests.LaufdatenSicherstellenAsync_WithDisposedScopeFactory_Completes` — ersetzt den alten `AktualisiereLaufStatusAsync_WithDisposedScopeFactory_Completes`; pflanzt laufendes Handle in `KiAusfuehrungsService._handles` per Reflection, damit der `IsRunning`-Guard den Persistenzpfad erreicht
- [x] Tests `KiAusfuehrungsStatusConverterTests` (4 neue) + `KiAusfuehrungsStatusSymbolConverterTests` (1 neu) — `LiveLaufStatus`-Matrix abgedeckt (Laeuft bei fehlenden Laufdaten, Wartet, Vorrang von Pausiert/Wartestellung, `null` unverändert)
- [x] Test `MainWindowViewModelTests.AktiveAufgabenAktualisierenAsync_ShouldMapLiveLaufStatus` — vorhanden
- [x] Tests `TaskDetailViewModelTests` — `CliStoppenAsync_BehaeltLaufdaten_BeiWeiterlaufendemProzess` (F1) und `StartCliAndUpdateStateAsync_SchreibtVolleLaufdaten_BeiWiederverwendeterSession` (F2) vorhanden
- [x] Test `EntwicklungsprozessServiceTests.CliNeustartenAsync_MeldetLaufAktiv_BeiLebenderSession` — vorhanden
- [x] E2E `SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E` (`E2E_ArbeitsstatusAktualisierung.cs`) — konsolidiert erweitert: Reattach (`taskDetail.Reload()` + Fußzeilen-/Kachel-Prüfung), Divergenz-Nachspiel (Laufdaten per DB gelöscht → Kachel bleibt „▶ Läuft" + Selbstheilungs-Nachweis binnen 45 s), danach Stopp → „✓ Bereit"; Hilfsmethode `WarteAufCliStatusText` ergänzt (benötigte View-Methoden `Reload`, `GetCliStatusText`, `WaitForCliRunning`, `OpenTestDbContext` existieren bereits)

## Hinweise

- Kosmetisch: Ein XML-Doc-Kommentar in `CliProcessManager.cs` (~Z. 195) referenziert noch `AufgabeService.AktualisiereLaufStatusAsync` — die Methode existiert weiter (planmäßig), der Kommentarkontext ist nur fachlich überholt; keine Planlücke.
- `OnRuntimeStatusChanged` ruft `LaufdatenSicherstellenAsync` auf, das den Live-Substatus selbst aus `GetTerminalSession(...)?.RuntimeStatus` ableitet statt den Event-Parameter zu übergeben — funktional deckungsgleich zum Plan (Status stammt aus derselben Session).
- Die Tasks-Datei (`issue-342-...-tasks.md`) war bereits vollständig auf `Erledigt` aktualisiert; die genannten Testnachweise wurden stichprobenartig gegen die tatsächlichen Testnamen verifiziert und stimmen überein — keine Aktualisierung erforderlich.
- `dotnet build src/Softwareschmiede/Softwareschmiede.csproj` wurde im Rahmen des Reviews ausgeführt: erfolgreich, 0 Warnungen/Fehler (inkrementell). E2E-Ausführung steht sandboxbedingt aus (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` → „Skipped"), wie im Plan vorgesehen.
