# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Vorab: Bewertung der Iteration-2-Fixes

Alle fünf Befunde aus Runde 1 wurden fachlich korrekt behoben:

1. **TOCTOU/Phantom-Lauf** — behoben. `laeuftGuard` wird in `AktivenLaufSetzenAsync`/`AktivenLaufSicherstellenAsync` **innerhalb** der prozessweiten `LaufGates`-Sperre unmittelbar vor `FindAsync`/`SaveChanges` ausgewertet (`AufgabeService.cs` ~Z. 1015–1020, 1097–1102). Da `AktivenLaufBeendenAsync` unter derselben Sperre serialisiert, steht die Schreibreihenfolge deterministisch fest; die Rest-Race (Prozessende zwischen Guard und Save) ist dokumentiert und auf Millisekunden begrenzt.
2. **Reentrancy** — behoben. `_abgleichLaeuft` (Interlocked) verhindert überlappende `AbgleichAsync`-Durchläufe; `_lifecycleLock` macht die Check-then-act-Sequenzen auf `_heartbeatTimers`/`_updateSemaphores`/`_runtimeStatusSubscriptions` atomar und ist eine echte Blattsperre (kein `await`/Blocking in kritischen Abschnitten — verifiziert); `SubscribeRuntimeStatus` ist idempotent. `LaufGates` deckt die früher ungeschützten Nicht-Tick-Schreiber ab.
3. **Dupliziertes Mapping** — behoben. `CliRuntimeStatusExtensions.ToAufgabeLaufStatus()` ist die einzige Abbildung; die abweichende `Inaktiv → Laeuft`-Semantik von `GetLiveLaufStatus` ist als `?? Laeuft`-Fallback am Aufrufer explizit dokumentiert.
4. **Toter Code** — `AusfuehrungAktivSetzenAsync`/`AktualisiereLaufStatusAsync` entfernt, Tests auf Direkt-Mutation der Entity bzw. `AktivenLaufSicherstellenAsync` umgestellt (mit erklärenden Kommentaren).
5. **Veraltete Doc-Kommentare** — `SubscribeRuntimeStatus`, `AppConverters.cs` Z. 194 und die Help-Doku (`docs/help/*/ablauf-technisch.md`) aktualisiert.

Kein Deadlock-/AsyncLocal-Problem eingeführt: `_lifecycleLock` wird nie gehalten, während auf ein `LaufGates`-Semaphore gewartet wird (Sperre nur in `CliProcessManager`, Gate nur in `AufgabeService`); `laeuftGuard` (`IsRunning` → `ConcurrentDictionary`-Lookup + `Process.HasExited`) ist nicht-blockierend. `LaufGates`-Einträge werden bewusst nie entfernt/disposed — begründet im Feldkommentar, durch Aufgabenanzahl begrenzt, `SemaphoreSlim` ohne `WaitHandle`-Nutzung ist disposal-unkritisch. Build verifiziert: `Softwareschmiede.csproj` und `Softwareschmiede.App.csproj` kompilieren mit 0 Warnungen/Fehlern.

## Befunde

### AufgabeService.cs (AufgabeService)

- **Toter Code / ungatebter Laufdaten-Writer** — `UpdateHeartbeatAsync` (Z. 984–991) hat nach diesem Change keinen produktiven Aufrufer mehr (ehemaliger Aufrufer `CliProcessManager.AktualisierungAsync` nutzt jetzt `LaufdatenSicherstellenAsync`); einziger verbleibender Aufrufer ist der Test in `AufgabeServiceTests.cs` Z. 620. Das ist exakt die Konstellation, die Runde 1 bei `AusfuehrungAktivSetzenAsync`/`AktualisiereLaufStatusAsync` als Befund gewertet hat und die dort zur Entfernung führte. Verschärft dadurch, dass die Methode `LastHeartbeatUtc` **außerhalb** von `LaufGates` schreibt und damit die im Feldkommentar (Z. 24–31) behauptete Invariante „serialisiert alle Laufdaten-Mutationen" als einziger produktiver Schreibpfad verletzt — ein Wiederbeleben des Aufrufs würde die neue Serialisierung unterlaufen.

  Empfehlung: Methode inklusive des ausschließlich sie deckenden Tests entfernen (konsistent zur Runde-1-Entscheidung). Falls sie als API bewusst bestehen bleiben soll: unter `LaufGates` stellen und die Abweichung im Doc-Kommentar begründen.

### CliProcessManager.cs (CliProcessManager)

- **Concurrency / Stale-Write (niedrige Schwere)** — `LaufdatenSicherstellenAsync` (Z. 352) liest `session.RuntimeStatus` **vor** dem Eintritt in die `LaufGates`-Sperre (`ExecuteWithAufgabeServiceAsync` → `AktivenLaufSicherstellenAsync`). Bei zwei parallelen Aufrufen entscheidet die Sperreintritts-Reihenfolge, nicht die Frische des gelesenen Werts: Ein früher gelesener `Laeuft`-Wert kann hinter einem später gelesenen `WartetAufEingabe` einrücken und diesen als letzter Schreiber überschreiben. Selbstheilend durch das nächste `RuntimeStatusChanged`-Ereignis bzw. den nächsten Heartbeat-Tick (≤ 30 s), aber eine Rest-Ungenauigkeit desselben Typs, den Runde 1 beanstandet hat.

  Empfehlung: Analog zum `laeuftGuard`-Muster den Substatus als `Func<AufgabeLaufStatus?>` (Provider-Delegate) übergeben, das `AufgabeService.AktivenLaufSicherstellenAsync` innerhalb der Sperre auswertet — dann gilt beim Schreiben immer der frischeste Session-Stand. Alternativ die Rest-Ungenauigkeit ausdrücklich im `<remarks>`-Block der Methode festhalten.

- **Concurrency / Dispose-Race (niedrige Schwere)** — `Dispose()` (Z. 429–448) leert `_heartbeatTimers`/`_updateSemaphores` unter `_lifecycleLock`, wartet aber nicht auf in-flight Aufrufe: `Timer.Dispose()` des neuen `_abgleichTimer` stoppt nur zukünftige Ticks, ein bereits laufender `AbgleichTickAsync`-/Heartbeat-Tick oder ein knapp vor der Event-Abmeldung eingetretener Aufruf kann in `LaufdatenSicherstellenAsync` (Z. 342–348) **nach** dem Leeren erneut `StartHeartbeatCore` ausführen. Der so angelegte Timer landet in den bereits geleerten Dictionaries, wird nie disposed und tickt — bei weiter laufendem Prozess — permanent weiter (alle 30 s `LaufdatenSicherstellenAsync` → DB-Heartbeat über einen disposed Manager). Das Fenster ist klein (Shutdown bei laufendem Prozess), aber die Selbstheilungslogik macht die Neuanlage des Timers auf genau diesem Pfad zum Normalfall.

  Empfehlung: `_disposed`-Flag einführen, das `Dispose()` unter `_lifecycleLock` setzt und das in `LaufdatenSicherstellenAsync` (vor bzw. innerhalb des `StartHeartbeatCore`-Blocks) sowie in `SubscribeRuntimeStatus` geprüft wird; bei gesetztem Flag keine Timer/Subscriptions mehr anlegen.

### KiAusfuehrungsService.cs (KiAusfuehrungsService)

- **Concurrency / Event-Invoke unter Sperre (niedrige Schwere, bestehendes Muster)** — `CliLaufAktiv?.Invoke(aufgabeId)` feuert in beiden Early-Returns (Z. 188, 284), während das klassenweite `_startLock` (`SemaphoreSlim(1,1)`) gehalten wird. Der derzeit einzige Abonnent (`CliProcessManager.OnCliLaufAktiv`) ist non-blocking (fire-and-forget mit rein synchronem, billigem Vorspann), daher aktuell kein Defekt — aber jeder künftige Abonnent, der blockiert oder reentrant `StartCliAsync`/`StartTerminalSessionAsync` aufruft, blockiert/deadlockt sämtliche Prozessstarts aller Aufgaben. Dasselbe Muster existiert bereits beim `Gestartet`-Invoke (Z. 235); der neue Event übernimmt die latente Falle unkommentiert.

  Empfehlung: Entweder die Invokes hinter `_startLock.Release()` verlagern (Early-Return-Flag merken, Event im Anschluss an den `finally`-Block feuern) oder — konsistent zur bestehenden `Gestartet`-Stelle — zumindest im XML-Doc des `CliLaufAktiv`-Events den Vertrag „Abonnenten müssen non-blocking sein, Invoke läuft unter der Start-Sperre" festhalten.

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
