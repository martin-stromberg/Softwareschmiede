# Logik — Bestandsaufnahme

## `CliProcessManager` — zentrale Brücke Session → Persistenz

Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs` (312 Zeilen)

Singleton (DI: `App.xaml.cs` Z. 299; wird beim App-Start eifrig instanziiert, `App.xaml.cs` Z. 86 — Fehler nur geloggt, App läuft dann komplett ohne Laufdaten-Persistenz). Abonniert im Konstruktor `KiAusfuehrungsService.CliProcessStatusChanged` (Z. 47).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartHeartbeat(Guid)` | public | Startet 30-s-`Timer` → `AktualisierungAsync` (Z. 52–66) |
| `StopHeartbeat(Guid)` | public | Entfernt Timer + Semaphore (Z. 70–82) |
| `AktualisierungAsync(Guid)` | private | Pro Aufgabe serialisiert (`_updateSemaphores`): `IsRunning` false → `StopHeartbeat` + Rückkehr; sonst `_kiService.UpdateHeartbeat` + `AufgabeService.UpdateHeartbeatAsync`. Alle Exceptions → LogWarning (Z. 84–123) |
| `OnCliProcessStatusChanged(Guid, CliProcessStatus)` | private | `Gestartet` → `StartHeartbeat` + `AktivenLaufSetzenAsync` (fire-and-forget) + `SubscribeRuntimeStatus`; `Gestoppt`/`Fehler` → `StopHeartbeat` + `UnsubscribeRuntimeStatus` + `AktivenLaufBeendenAsync` (fire-and-forget). Äußerer try/catch schützt die Multicast-Kette (Z. 125–154) |
| `AktivenLaufSetzenAsync(Guid)` | private | `AufgabeService.AktivenLaufSetzenAsync(id, Guid.NewGuid().ToString("N"))` via `ExecuteWithAufgabeServiceAsync` (Z. 161–167) |
| `AktivenLaufBeendenAsync(Guid)` | private | `AufgabeService.AktivenLaufBeendenAsync` via `ExecuteWithAufgabeServiceAsync` (Z. 174–180) |
| `SubscribeRuntimeStatus(Guid)` | private | Holt Session via `_kiService.GetTerminalSession`; `null` → Rückkehr ohne Subscription (klassischer Pipe-Start); sonst `RuntimeStatusChanged += handler` in `_runtimeStatusSubscriptions` (Z. 191–212) |
| `UnsubscribeRuntimeStatus(Guid)` | private | Abmeldung aus `_runtimeStatusSubscriptions` (Z. 216–222) |
| `OnRuntimeStatusChanged(Guid, CliRuntimeStatus)` | private | `Inaktiv` → ignoriert; sonst `WartetAufEingabe`→`AufgabeLaufStatus.WartetAufEingabe`, `Laeuft`→`AufgabeLaufStatus.Laeuft` → `AktualisiereLaufStatusAsync` fire-and-forget (Z. 233–243) |
| `ExecuteWithAufgabeServiceAsync(...)` | private | DI-Scope + `AufgabeService`-Aufruf; **`ObjectDisposedException` → Debug, alle anderen Exceptions → nur LogWarning**; `NotifyLaufdatenChanged` nur im Erfolgsfall (Z. 256–284) |
| `Dispose()` | public | Meldet `CliProcessStatusChanged` ab, räumt Timer/Semaphoren/Subscriptions (Z. 287–311) |

Abonnierte Events: `KiAusfuehrungsService.CliProcessStatusChanged` (Konstruktor), `ITerminalSession.RuntimeStatusChanged` (pro gestarteter Aufgabe).
Publizierte Events: `AufgabeLaufdatenChangedNotifier.LaufdatenChanged` (indirekt, nur nach erfolgreicher Persistierung).

## `KiAusfuehrungsService` — Prozess-/Session-Registry + Event-Quelle

Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (835 Zeilen)

Singleton; hält `_handles: ConcurrentDictionary<Guid, CliProcessHandle>` (In-Memory — geht bei App-Neustart verloren) und serialisiert Aufgaben-Operationen über `ScmTaskCoordinator.Shared` + `_startLock`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning(Guid)` | public | `false` ohne Handle; `!handle.Process.HasExited`; **`catch { return false; }`** (Z. 68–77) |
| `GetRunningProcess(Guid)` | public | Prozess oder null (Z. 82–88) |
| `GetRunningCount()` | public | Anzahl Handles mit `!HasExited` (Z. 91–96) |
| `StartCliAsync(...)` | public | Klassischer Pipe-Start via `ScmTaskCoordinator` → `StartCliCoreAsync` (Z. 112–121) |
| `StartCliCoreAsync(...)` | private | **Early-Return: Handle vorhanden + `!HasExited` → `return existing` OHNE `Gestartet`-Event** (Z. 129–139); sonst Prozess-Start, `Exited`-Verdrahtung, `RaiseRunningCountChanged` + `CliProcessStatusChanged(Gestartet)` (Z. 184) |
| `StartTerminalSessionAsync(...)` | public | Interaktiver Start via `ScmTaskCoordinator` → `StartTerminalSessionCoreAsync` (Z. 207–216) |
| `StartTerminalSessionCoreAsync(...)` | private | **Early-Return: laufendes Handle → `return existing` OHNE `Gestartet`** (Z. 224–235); Session via `_sessionFactory.StartAsync`, `ScmCoordinatedTerminalSession`-Wrapper, `Exited`/`Failed`-Verdrahtung (Z. 313–314); Pre-Wiring-Checks: `session.Failure` (Z. 322–326), `process.HasExited` (Z. 334–338), Handle bereits entfernt (Z. 342–345) — alle ohne `Gestartet`; `Gestartet` nur in Z. 349 |
| `GetTerminalSession(Guid)` | public | `handle.Session` oder null (Z. 404–409) |
| `GetScmPromptSession(Guid)` | public | SCM-Adapter des Handles (Z. 412–413) |
| `StopCliAsync(Guid, ct)` | public | `AbsichtlichGestoppt = true`, `CloseMainWindow` → 5 s warten → `Kill(entireProcessTree)`; **Fehler nur LogError, kein Wurf** (Z. 446–478). Feuert kein Status-Event selbst — alles läuft über `Exited`/`Failed` |
| `UpdateHeartbeat(Guid)` | public | In-Memory `handle.LastHeartbeat` (Z. 495–501) |
| `HandleProcessExitedAsync` / `HandleSessionEndedAsync` / `HandleSessionFailedAsync` | private | Alle enden in `HandleExitedCoreAsync`; `Failed` wird wie fehlerhaftes Exit behandelt (`istFehlerhaftesEnde: true`, Z. 607–611) |
| `HandleExitedCoreAsync(...)` | private | Atomares `_handles.TryRemove` (Genau-einmal); Fremd-Handle → zurücklegen + return (Z. 627–631); `vorAufraeumenAsync` (Session-Drain/Dispose); Status-Mapping `AbsichtlichGestoppt`→`Gestoppt`, `istFehlerhaftesEnde`/`exitCode!=0`→`Fehler`+`PersistFehlgeschlagenAsync`, sonst `Gestoppt`; **`PersistAusfuehrungBeendetAsync` + `CliProcessStatusChanged(status)`** (Z. 613–666) |
| `PersistAusfuehrungBeendetAsync(Guid)` | private | `AufgabeService.AktivenLaufBeendenAsync` in eigenem Scope; Exceptions nur geloggt (Z. 668–704) — **doppelt zur `CliProcessManager`-Reaktion auf dasselbe Event (idempotent)** |
| `PersistFehlgeschlagenAsync` | private | `ProtokollTyp.SystemMeldung`-Eintrag bei Fehler-Exit (Z. 529–572) |
| `RaiseRunningCountChanged` | private | `RunningCountChanged(previous, current)` (Z. 706–718) |
| `Dispose()` | public | Killt alle Prozesse, disposed Sessions (Z. 504–527) |

Publizierte Events: `CliProcessStatusChanged` (Z. 62 — `Gestartet` nur in Z. 184/349), `RunningCountChanged` (Z. 65).

## `AufgabeService` — Laufdaten-API

Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs` (1110 Zeilen); Scoped, EF Core (`SoftwareschmiededDbContext`, SQLite).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartenAsync(id, branchName, klonPfad, basisBranch)` | public | `Status=Gestartet`, **`AusfuehrungsStatus=Aktiv` bei gleichzeitig `AktiveRunId=null`, `LastHeartbeatUtc=null`, `LaufStatus=null`** (Z. 763–785) — persistiert den „Aktiv ohne Lauf"-Zwischenzustand |
| `AbschliessenAsync(id)` | public | `Status=Beendet`, `AusfuehrungsStatus=Beendet`, leert `AktiveRunId`/`LastHeartbeatUtc`/`LetzterCliStartUtc`/`LaufStatus` (Z. 873–891) |
| `StartZuruecksetzenAsync(id)` | public | `Status=Neu`, `AusfuehrungsStatus=NichtGestartet`, leert alle Laufdaten (Z. 924–940) |
| `UpdateHeartbeatAsync(id)` | public | `LastHeartbeatUtc = UtcNow`, wirft bei unbekannter Aufgabe (Z. 943–950) |
| `AusfuehrungAktivSetzenAsync(id)` | public | Nur `AusfuehrungsStatus=Aktiv`; wirft bei `Status Beendet/Archiviert` (Z. 953–965). **Setzt keine `AktiveRunId`** |
| `AktivenLaufSetzenAsync(id, laufId)` | public | `AusfuehrungsStatus=Aktiv`, `AktiveRunId=laufId`, `LastHeartbeatUtc=LetzterCliStartUtc=now`, `LaufStatus=Laeuft`; wirft bei `Status Beendet/Archiviert` bzw. nicht gefundener Aufgabe (Z. 976–995) — **einziger Setzpfad für `AktiveRunId`** |
| `AktivenLaufBeendenAsync(id)` | public | `AktiveRunId`/`LastHeartbeatUtc`/`LetzterCliStartUtc`/`LaufStatus` = null, `AusfuehrungsStatus=Beendet` (Z. 1004–1017) |
| `AktualisiereLaufStatusAsync(id, laufStatus)` | public | **No-Op bei `AktiveRunId == null`** (Z. 1037–1038); sonst `LaufStatus` setzen (Z. 1029–1042) |
| `GetAktiveAufgabenAsync(ct)` | public | `Status ∈ {Gestartet, Wartend}` (`IstAktivOderWartendPredicate`), `Include(Projekt, GitRepository)`, Top 20, sortiert nach `LetzterCliStartUtc ?? ErstellungsDatum` (Z. 1064–1076) — **Datenquelle der Kachel** |
| `GetHeartbeatAgeMinutesAsync(id)` | public | Minuten seit `LastHeartbeatUtc` (Z. 1045–1059) |
| `SetStatusAsync` / `StatusSetzenAsync` | public | Statuswechsel (mit/ohne Transitionsvalidierung); **berühren die Laufdaten nicht** (Z. 894–921) |

## `EntwicklungsprozessService` — Start-/Neustart-Orchestrierung

Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (984 Zeilen); Scoped.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenUndCliStartenAsync(...)` | public | Via `ScmTaskCoordinator` → `ProzessUndCliCoordinatedAsync` (Z. 126–135) |
| `ProzessUndCliCoordinatedAsync(...)` | private | Pausen-/Endstatus-Guards; **`LokalerKlonPfad` bereits gesetzt → nur `CliNeustartenAsync` + return** (Z. 155–159); sonst `ProzessStartenCoreAsync` (Klon+Branch+`FinalizeStartAsync`→`AufgabeService.StartenAsync`) und `StartTerminalSessionAsync` (Z. 190); Fehler → `RollbackStartAsync` → `StartZuruecksetzenAsync` (Z. 193–204) |
| `CliNeustartenAsync(...)` | public | Via `ScmTaskCoordinator` → `CliNeustartenCoordinatedAsync` (Z. 235–242) |
| `CliNeustartenCoordinatedAsync(...)` | private | **Early-Return: `GetTerminalSession` nicht null + `ExitCode == null` + `Failure == null` + `!Process.HasExited` → `return` OHNE jegliche Persistenz oder Status-Event** (Z. 263–268); sonst Plugin-Auflösung + `StartTerminalSessionAsync` (Z. 288) |
| `RollbackStartAsync(...)` | private | Klon löschen + `StartZuruecksetzenAsync` (+ Status-Restore) (Z. 508–524) |
| `FinalizeStartAsync(...)` | private | Initialisierungs-/Startskripte, issue.md, .gitignore, `AufgabeService.StartenAsync`, Git-Protokolleintrag (Z. 688–719) |
| `WirfWennPausiert` | private static | Wirft bei `PausiertBisUtc` in Zukunft (Z. 976) |

Aufrufer: `TaskDetailViewModel.StartenAsync`/`StartCliAndUpdateStateAsync`/`PluginWechselAsync`, `ScmEventOrchestrator` (Z. 158–161).

## `AufgabeLaufAktivitaet` — „Lauf lebt"-Regel

Datei: `src/Softwareschmiede/Application/Services/AufgabeLaufAktivitaet.cs` (18 Zeilen)

`IstAktiv(aktiveRunId, lastHeartbeatUtc, nowUtc)` → `aktiveRunId != null && lastHeartbeatUtc != null && nowUtc - lastHeartbeatUtc < 5 min` (`AufgabeRecoveryService.HeartbeatTimeoutMinutes`, Z. 14–17). Verwendet von `KiAusfuehrungsStatusKlassifikation.Klassifiziere`.

## `KiAusfuehrungsStatusKlassifikation` + Converter — Kachel-Projektion

Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs`

`Klassifiziere(object, out pausiertRestzeit)` (Z. 146–196) — Entscheidungskette in dieser Reihenfolge:
1. `PausiertBisUtc` in Zukunft → `Pausiert`
2. `HasScheduledPrompt` → `PromptInWartestellung`
3. `AusfuehrungsStatus != Aktiv` → `Bereit`
4. `AufgabeLaufAktivitaet.IstAktiv(...)` true → `LaufStatus == WartetAufEingabe ? Wartet : Laeuft`
5. `Status == AufgabeStatus.Wartend` → `Wartet`
6. sonst → `Bereit`

`KiAusfuehrungsStatusConverter` (Z. 210–235): `Laeuft`→„▶ Läuft", `Wartet`→„⏸ Wartet", `PromptInWartestellung`→„⏳ Prompt in Wartestellung", `Pausiert`→„⏸ Pausiert (noch …)", `Bereit`/null→„✓ Bereit".
`KiAusfuehrungsStatusSymbolConverter` (Z. 242–262): gleiche Klassifikation, nur Glyphen.
Eingaben: `Aufgabe` **oder** `AktiveAufgabePanelItem` — **keine Live-Session-Quelle**; rein persistenzbasiert.

## `MainWindowViewModel` — Kachel-Aktualisierung

Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` (533 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AktiveAufgabenListe` | public Property | `ObservableCollection<AktiveAufgabePanelItem>` — **gemeinsame Quelle für Seitenleiste UND Dashboard** (`DashboardViewModel.Initialize`, MainWindowViewModel.cs Z. 226) |
| `AktiveAufgabenAktualisierenAsync(ct)` | public | `GetAktiveAufgabenAsync` + `GetOpenTodoCountsAsync` → `ReplaceAll(MapAktiveAufgabePanelItem)`; `_refreshGate` verhindert parallele Läufe (Z. 323–346) |
| `MapAktiveAufgabePanelItem` | private | Kopiert Status-/Laufdaten-Felder der `Aufgabe` ins Item (Z. 366–410) |
| `OnRunningCountChanged` | private | `IRunningAutomationStatusSource.RunningCountChanged` → Dispatcher → Refresh (Z. 451–454) |
| `OnLaufdatenChanged` | private | `AufgabeLaufdatenChangedNotifier.LaufdatenChanged` → Dispatcher → Refresh (Z. 456–459) |
| `OnAktualisierungsTimerTick` | private | `DispatcherTimer` 5 s → Refresh-Fallback (Z. 20, 195–200, 461–464) |

## `TaskDetailViewModel` — Fußzeilen-Seite + Startpfade

Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (2281 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LadenAsync(ct)` | private | `darfCliAnzeigen = AusfuehrungsStatus.SollCliAnzeigen(Status)`; `IsCliRunning = darfCliAnzeigen && _kiService.IsRunning`; `AttachCliStatusSession(session)` (Z. 722–788) |
| `StartenAsync(ct)` | private | `AusfuehrungsStatus == Beendet` → `StartCliAndUpdateStateAsync`; sonst → `ProzessStartenUndCliStartenAsync`; danach `LadenAsync` + `AttachCliStatusSession` (Z. 1579–1640) |
| `StartCliAndUpdateStateAsync(...)` | private | `CliNeustartenAsync` → `!IsRunning` → `AktivenLaufBeendenAsync` + return; **`IsRunning` → nur `AusfuehrungAktivSetzenAsync` (kein `AktivenLaufSetzenAsync`!)** (Z. 1690–1727) |
| `PluginWechselAsync(ct)` | private | `StopCliAsync` → `StartCliAndUpdateStateAsync` (Z. 1642–1688) |
| `CliStoppenAsync(ct)` | private | **`StopCliAsync` + direkter `AktivenLaufBeendenAsync`-Aufruf** — unabhängig davon, ob der Prozess wirklich beendet wurde (Z. 910–933) |
| `OnCliProcessStatusChanged(id, status)` | private | Nur für `_aufgabeId`: `IsCliRunning = IsRunning`; In-Memory-`AusfuehrungsStatus` der geladenen Entity; `Gestartet` → CliStatusText „Gestartet"; sonst `!isRunning` → `AttachCliStatusSession(null)` + „CLI-Status: Fehler"/„CLI inaktiv" (Z. 1507–1557) |
| `AttachCliStatusSession(session)` | private | Abonniert `RuntimeStatusChanged` der Session → `UpdateCliStatusText` (Z. 1764–1786) |
| `UpdateCliStatusText(status)` | private | `Laeuft`→„CLI-Status: Ausführung läuft", `WartetAufEingabe`→„CLI-Status: Wartet auf Eingabe", `Inaktiv`→„CLI inaktiv" (Z. 1793–1806) |
| `PromptVorlageAuswaehlenAsync(vorlage, ct)` | private | `session.WritePromptAsync` → `MarkInputActivity` → `RuntimeStatus.Laeuft` (Z. 887–908) |

Abonniert: `CliProcessStatusChanged` (Z. 674), `RuntimeStatusChanged` (via `AttachCliStatusSession`), `PromptZeitVersandService.PromptSent`/`ScheduledPromptChanged`.

## `PseudoConsoleSession` + `CliRuntimeStatusEvaluator` — Live-Statusquelle

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (706 Zeilen)

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `RuntimeStatus` | public | `CliRuntimeStatus` unter `_runtimeStatusLock`; Startwert `Laeuft` (Z. 41, 58–67) |
| `RuntimeStatusChanged` | public event | Bei Statuswechsel via `SetRuntimeStatus` (Z. 70, 477–491) |
| `_runtimeStatusTimer` | private | 1-s-`Timer` → `RefreshRuntimeStatus` (Z. 150) |
| `RefreshRuntimeStatus` | private | `isRunning = !_process.HasExited` (catch → false) → `CliRuntimeStatusEvaluator.Determine` (Z. 447–475) |
| `MarkInputActivity` / `MarkOutputActivity` | public | Setzt `_lastInputUtc`/`_lastOutputUtc` + `Laeuft` (Z. 155–174) |
| `ReadLoopAsync` | private | Output-Loop; Lesefehler → `RaiseFailed` + `RaiseExited`; EOF → `RaiseExited`; jedes gelesene Byte → `MarkOutputActivity` (Z. 284–345) |
| `RaiseExited` | private | `Exited` genau einmal, nur bei tatsächlich beendetem Prozess (`STILL_ACTIVE`-Check via natives Handle bzw. `HasExited`) (Z. 370–423) |
| `RaiseFailed` | private | `_failure` setzen (erster Fehler gewinnt) + `Failed`-Event — **unabhängig vom Prozessende** (Z. 425–445) |
| `WriteInputAsync` / `WritePromptAsync` | public | Eingabe inkl. `MarkInputActivity`; Schreibfehler → `RaiseFailed` (Z. 516–602) |

`CliRuntimeStatusEvaluator.Determine` (Z. 685–705): `!isRunning` → `Inaktiv`; `now - letzteAktivität >= waitingThreshold` (Default 4 s, Z. 125) → `WartetAufEingabe`; sonst `Laeuft`.

## `ScmCoordinatedTerminalSession` — Session-Wrapper

Datei: `src/Softwareschmiede/Infrastructure/Terminal/ScmCoordinatedTerminalSession.cs` (46 Zeilen)

`internal`-Wrapper: `WriteInputAsync`/`WritePromptAsync` laufen über `ScmTaskCoordinator.Shared.RunAsync(taskId)` + `InputStarted`-Callback; alle Status-Member/Events (`RuntimeStatus`, `RuntimeStatusChanged`, `Exited`, `Failed`, `Failure`, `ExitCode`) werden 1:1 durchgereicht.

## `AufgabeLaufdatenChangedNotifier`

Datei: `src/Softwareschmiede/Application/Services/AufgabeLaufdatenChangedNotifier.cs` (15 Zeilen)

`NotifyLaufdatenChanged(aufgabeId)` → `LaufdatenChanged` event. Aufrufer: `CliProcessManager.ExecuteWithAufgabeServiceAsync` (nur Erfolgsfall), `TaskDetailViewModel` (Plugin-Änderungen, Z. 1738/1759/2261), `KiPluginLimitService` (Z. 129).

## `AufgabeRecoveryService`

Datei: `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`

`HeartbeatTimeoutMinutes = 5` (Z. 19). `ScanForRecoveryCandidatesAsync` (Z. 37–56): `Status ∈ {Gestartet,Wartend}` + `AusfuehrungsStatus == Aktiv` + `LastHeartbeatUtc` älter 5 min + nicht pausiert + **`!_runningStatusSource.IsRunning(id)`** — Kandidaten mit laufendem Prozess werden ausgefiltert. `RecoverManuellAsync` setzt Status→Gestartet (ohne Laufdaten zu ändern).

## `ScmEventOrchestrator` — automatisierter Startpfad

Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` (Auszug Z. 142–163)

Bei Ereignis-Zustellung: `SetStatusAsync(Gestartet)` bei `Neu`; lebende Session wird wiederverwendet (`adapter.IsAlive`); toter/fehlender Adapter → `ProzessStartenUndCliStartenAsync` (kein Klonpfad) bzw. **`CliNeustartenAsync`** (Klonpfad vorhanden, Z. 161) — trifft dieselben Early-Return-Pfade.

## `App.xaml.cs` — Verdrahtung beim Start

Datei: `src/Softwareschmiede.App/App.xaml.cs`

- `StartupAsync` (Z. 73–131): Host-Build → `GetRequiredService<CliProcessManager>()` (Z. 86 — aktiviert die Event-Brücke; Fehlschlag nur LogError, „Anwendung läuft ohne CLI-Funktionalität weiter") → `MigrateAsync` → MainWindow.
- `ConfigureServices` (Z. 173ff.): SQLite `UseSqlite` (Z. 191), `AddSingleton<CliProcessManager>` (Z. 299).

## Darstellungsorte (XAML)

| Ort | Datei / Zeile | Bindung |
|-----|---------------|---------|
| Seitenleisten-Kachel | `src/Softwareschmiede.App/Views/MainWindow.xaml` Z. 164–167 | `ActiveTasksListControl ItemsSource={Binding AktiveAufgabenListe}`, AutomationId `AktiveAufgabenListeMenue` |
| Dashboard-Kachel | `src/Softwareschmiede.App/Views/DashboardView.xaml` Z. 89–92 | dieselbe `AktiveAufgabenListe`, AutomationId `AktiveAufgabenListeDashboard` |
| Status-Glyph + Tooltip | `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` Z. 35–43 | `{Binding ., Converter=KiAusfuehrungsStatusSymbolConverter}` / `KiAusfuehrungsStatusConverter` |
| Fußzeile | `src/Softwareschmiede.App/Views/TaskDetailView.xaml` Z. 630–636 | `Text={Binding CliStatusText}`, AutomationId `CliStatusText` |
