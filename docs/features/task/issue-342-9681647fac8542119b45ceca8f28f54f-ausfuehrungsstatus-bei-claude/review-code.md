# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Vorab: Bewertung der Iteration-3-Fixes

Alle vier Befunde aus Runde 2 wurden korrekt und vollständig behoben:

1. **`UpdateHeartbeatAsync` entfernt** — verifiziert: Die Methode existiert nicht mehr in `AufgabeService.cs`; einzige verbliebene Nennung ist der erklärende Kommentar im umgestellten Test `GetHeartbeatAgeMinutesAsync_BerechnetAlter_BeiGesetztemHeartbeat` (`AufgabeServiceTests.cs` Z. 610–614), der den Heartbeat jetzt direkt an der Entity setzt. Damit gibt es produktiv keinen ungatebten `LastHeartbeatUtc`-Schreiber mehr; Querprüfung (`\.AktiveRunId\s*=|\.LastHeartbeatUtc\s*=|\.LaufStatus\s*=` über alle Produktivdateien) bestätigt: Sämtliche Laufdaten-Mutationen laufen ausschließlich über die `LaufGates`-serialisierten Pfade (`StartenAsync` Z. ~779, `AbschliessenAsync` ~899, `StartZuruecksetzenAsync` ~960, `AktivenLaufSetzenAsync` ~1008, `AktivenLaufBeendenAsync` ~1045, `AktivenLaufSicherstellenAsync` ~1075). `AufgabeRecoveryService` liest die Felder nur — die Feldkommentar-Invariante „serialisiert alle Laufdaten-Mutationen" stimmt.

2. **`laufStatusProvider` unter der Sperre ausgewertet** — verifiziert: `AktivenLaufSicherstellenAsync` (`AufgabeService.cs` Z. ~1075) ruft `laufStatusProvider?.Invoke()` erst **nach** `gate.WaitAsync` und `laeuftGuard` innerhalb des kritischen Abschnitts auf (Z. ~1092), vor `FindAsync`. Beide produktiven Aufrufer übergeben echte Provider-Delegates statt vorab gelesener Werte: `CliProcessManager.LaufdatenSicherstellenAsync` (Z. 384: `() => _kiService.GetTerminalSession(aufgabeId)?.RuntimeStatus.ToAufgabeLaufStatus()`) und `TaskDetailViewModel.StartCliAndUpdateStateAsync` (Z. 1732, identisches Muster, `using Softwareschmiede.Infrastructure.Terminal` vorhanden). Der neue Test `AktivenLaufSicherstellenAsync_WertetLaufStatusProvider_InDerSperreAus` (`AufgabeServiceTests_AktiverLauf.cs` Z. 189–229) belegt über gehaltenes Gate, dass der Provider nicht vor dem Sperreintritt ausgewertet wird — er prüft genau die behobene Eigenschaft.

3. **`_disposed`-Flag deckt alle Anlagepfade ab** — verifiziert: `Dispose()` setzt `_disposed = true` als erste Aktion unter `_lifecycleLock` (`CliProcessManager.cs` Z. 467), Guards sitzen in `StartHeartbeat` (Z. 89), `LaufdatenSicherstellenAsync` (Z. 366, vor `StartHeartbeatCore`) und `SubscribeRuntimeStatus` (Z. 263, vor der Event-Registrierung). Vollständigkeitsprüfung: `new Timer` existiert nur im Konstruktor (`_abgleichTimer`, selbst disposed) und in `StartHeartbeatCore`; `RuntimeStatusChanged +=` nur in `SubscribeRuntimeStatus`; `_updateSemaphores`-Anlage nur in `StartHeartbeatCore`. `StartHeartbeatCore` wird ausschließlich unter `_lifecycleLock` aus den beiden ge-guardeten Pfaden aufgerufen — kein Anlagepfad ohne Guard. Auch die Richtung stimmt: Timer, der vor der Sperrfreigabe angelegt wurde, landet im Dictionary und wird von `Dispose()` mit disposed; danach angelegte werden durch das Flag verhindert. Der neue Test `Dispose_VerhindertNeuanlageVonTimerUndSubscription` (`CliProcessManagerTests.cs` Z. ~143) deckt alle drei Pfade inkl. `VerifyAdd(Times.Never)` direkt ab.

4. **XML-Doc-Vertrag für Event-Invoke unter `_startLock`** — verifiziert: `<remarks>`-Blöcke an `CliProcessStatusChanged` (`KiAusfuehrungsService.cs` Z. 62–67) und `CliLaufAktiv` (Z. 76–80) dokumentieren „Invoke unter der Start-Sperre, Abonnenten müssen non-blocking sein, keine reentranten Startaufrufe". Der Vertrag ist realistisch: `IsRunning`/`GetTerminalSession`/`GetLaufendeAufgabenIds`/`GetLiveLaufStatus` lesen nur `_handles` (ConcurrentDictionary) ohne `_startLock`, sodass der synchrone Vorspann des einzigen Abonnenten (`OnCliLaufAktiv` → `LaufdatenSicherstellenAsync` bis zum ersten unvollständigen `await` auf `gate`/Scope) nachweislich nicht blockiert.

Kein neues Concurrency-Problem eingeführt: `_disposed` ist ein reines bool unter der bestehenden Blattsperre (kein await im kritischen Abschnitt); die Provider-Auswertung liest nur `RuntimeStatus` (einfacher lock-guarded Property-Read, `PseudoConsoleSession.cs` Z. 58–67); die `LaufGates`-Sperrordnung ist unverändert einschichtig. Build verifiziert: `Softwareschmiede.csproj`, `Softwareschmiede.App.csproj`, `Softwareschmiede.Tests.csproj` und `Softwareschmiede.IntegrationTests.csproj` kompilieren je mit 0 Warnungen/0 Fehlern.

## Einordnende Anmerkungen (keine Befunde, kein Handlungsbedarf für diesen Loop)

- `CliLaufAktiv`-Remarks nennen als Sperre `_startLock`; der `NotifyLaufAktiv`-Invoke aus `EntwicklungsprozessService.CliNeustartenCoordinatedAsync` läuft stattdessen unter der pro-Aufgabe-`ScmTaskCoordinator`-Sperre. Die operative Vertragsaussage (non-blocking, keine reentranten Starts — Reentrancy würde auf beiden Sperren deadlocks) ist für beide Pfade korrekt und ausreichend; nur die konkret benannte Sperre ist für den `NotifyLaufAktiv`-Pfad ungenau. Reine Doc-Präzision.
- Ein `LaufdatenSicherstellenAsync`-Aufruf, der die `_disposed`-Prüfung passiert hat, kann nach `Dispose()` noch den abschließenden `ExecuteWithAufgabeServiceAsync`-DB-Write ausführen. Unkritisch: Der Prozess läuft dann nachweislich weiter (frische Guard-Auswertung in der Gate-Sperre), der geschriebene Zustand ist konsistent, und bei bereits disposedem Provider fängt der `ObjectDisposedException`-Catch ab. Timer-/Subscription-Anlage — die eigentliche Leak-Quelle — ist dagegen vollständig unterbunden.
- `AktualisierungAsync`: `semaphore.WaitAsync()` liegt außerhalb des try-Blocks (`CliProcessManager.cs` Z. 147); eine zwischen `TryGetValue` und `WaitAsync` disposede Semaphore wirft `ObjectDisposedException`, die bis `SafeFireAndForget` propagiert und nur geloggt wird. Bestehendes Muster aus Iteration 2, Fenster minimal, Auswirkung ein Log-Eintrag.
- `StatusDaten`-Record (`AppConverters.cs` Z. ~208) hat mit `LiveLaufStatus` nun 8 Positionsparameter; als internes, einzig der Klassifikation dienendes Parameter-Objekt ist das akzeptabel. Der `Aufgabe`-Zweig übergibt bewusst `null` (Entity ohne Live-Quelle) — konsistent zum Design, dass der Live-Zweig nur auf dem Panel-Item greift.
- Tests: `CliStoppenAsync_BehaeltLaufdaten_BeiWeiterlaufendemProzess` nutzt `Process.GetCurrentProcess()` als „nicht beendbaren" Prozess — `Kill(entireProcessTree: true)` auf den eigenen Prozess wirft `InvalidOperationException` (in `StopCliAsync` gefangen/geloggt), der Handle bleibt, `IsRunning` bleibt true. Deterministisch und im Kommentar begründet; die ~5-s-`WaitForExitAsync`-Wartezeit pro Stopp-Test ist vertretbar. Reflection-Zugriffe auf `LaufGates`/`_handles`/private Methoden folgen dem im selben Testbestand etablierten Muster.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CliRuntimeStatusExtensions.cs` (neu)
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/CliProcessManager.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs` (Schreibpfade quergeprüft, unverändert)
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests_AktiverLauf.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_AktiverLauf.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_LaufStatus.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeRecoveryServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AutoShutdownOrchestratorTests.cs`
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusSymbolConverterTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ArbeitsstatusAktualisierung.cs`
- `src/Softwareschmiede.Tests/ServiceIntegration/RecoveryServiceIntegrationTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/AufgabeRecoveryServiceTests.cs`
- `docs/help/aufgaben/ablauf-technisch.md`
- `docs/help/stabilitaet/ablauf-technisch.md`
