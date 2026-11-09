# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### CliProcessManager.cs (CliProcessManager)

- **Concurrency / TOCTOU** — `LaufdatenSicherstellenAsync` (Z. 278–303) prüft `_kiService.IsRunning(aufgabeId)` einmal am Anfang, persistiert dann aber erst nach Scope-Erzeugung, `FindAsync` und `SaveChanges` über `ExecuteWithAufgabeServiceAsync`. Läuft der Prozess in diesem Fenster aus und wird parallel `AktivenLaufBeendenAsync` (via `Gestoppt`/`Fehler`-Ereignis oder `TaskDetailViewModel.CliStoppenAsync`) geschrieben, kann der Sicherstellen-Save **nach** dem Beenden-Save landen und einen Phantom-Lauf wiederbeleben (`AusfuehrungsStatus=Aktiv`, neue `AktiveRunId`, frischer Heartbeat). Die Kachel zeigt dann bis zur Heartbeat-Staleness „▶ Läuft", während die Fußzeile korrekt „Bereit" meldet — exakt die Divergenz, die Issue 342 beseitigen soll; der DB-Zustand bleibt darüber hinaus dauerhaft verwaist (kein Timer, kein Abgleich räumt ihn auf, nächste Recovery-Session sieht einen „abgestürzten" Lauf). Der bisherige Pfad (`AktualisiereLaufStatusAsync`) war dagegen bewusst als No-Op bei `AktiveRunId == null` ausgelegt. Das Zeitfenster ist klein (Millisekunden), aber alle Selbstheilungspfade (`OnCliLaufAktiv`, `OnRuntimeStatusChanged`, Heartbeat-Tick, `AbgleichAsync`) laufen darüber.

  Empfehlung: `IsRunning` unmittelbar vor dem Persistenzaufruf erneut prüfen (verkleinert das Fenster), und/oder `AktivenLaufSicherstellenAsync` um einen Modus erweitern, der bei späten Events nur einen **bestehenden** Lauf auffrischt statt einen neuen anzulegen; zumindest die Rest-Race explizit im Doc-Kommentar festhalten.

- **Concurrency / fehlende Reentrancy-Sicherung** — Der `_abgleichTimer` (Z. 55–59) feuert alle 30 s und startet `AbgleichAsync` jeweils als fire-and-forget: Überschreitet ein Abgleich-Lauf das Intervall (langsamer DB-Zugriff, viele Aufgaben), überlappen sich die Ausführungen. Zusätzlich laufen die Trigger `OnCliLaufAktiv`/`OnRuntimeStatusChanged`/`AktualisierungAsync`/`AbgleichAsync` auf verschiedenen Threads parallel in `LaufdatenSicherstellenAsync` — die pro-Aufgabe-Serialisierung über `_updateSemaphores` greift nur innerhalb von `AktualisierungAsync`. Folge: konkurrierende `AktivenLaufSicherstellenAsync` in unterschiedlichen EF-Scopes auf derselben Row (Last-Write-Wins, verschwendete Saves, mögliche `DbUpdateException`-Warnungen), sowie nicht-atomare Check-then-act-Sequenzen in `StartHeartbeat` (Z. 64–78: `ContainsKey`-Vorprüfung in Z. 285 + `StopHeartbeat`/`_heartbeatTimers[id] = timer`) und `SubscribeRuntimeStatus` (Z. 200–230): zwei parallele Aufrufer können einen verwaisten, im Dictionary nicht mehr referenzierten Heartbeat-Timer (permanenter 30-s-No-Op-Tick) bzw. eine doppelte/nicht mehr abmeldbare `RuntimeStatusChanged`-Subscription hinterlassen.

  Empfehlung: `LaufdatenSicherstellenAsync` pro Aufgabe serialisieren (z. B. `_updateSemaphores` oder `SemaphoreSlim`-Map auch für die nicht-Tick-Pfade nutzen) und/oder `AbgleichAsync` mit `Interlocked`-Flag gegen Überlappung sichern; `SubscribeRuntimeStatus`/`StartHeartbeat` unter demselben Schutz ausführen.

- **Dokumentation (veralteter Kommentar, niedrige Schwere)** — Der XML-Doc-Kommentar von `SubscribeRuntimeStatus` (Z. 191–199) beschreibt noch den alten Pfad: Persistenz „über `AufgabeService.AktualisiereLaufStatusAsync`" für eine „gerade gestartete" Aufgabe (`<param>`: „deren CLI-Prozess gestartet wurde"). Tatsächlich persistiert der Handler über `LaufdatenSicherstellenAsync` → `AktivenLaufSicherstellenAsync`, und die Methode wird aus allen Selbstheilungspfaden inklusive 30-s-Abgleich aufgerufen. Der `cref` kompiliert weiter (die Methode existiert), ist also nur fachlich überholt — sollte dennoch an die neue Aufrufrealität angepasst werden. Gleiches gilt für den Kommentar in `AppConverters.cs` Z. 194 („persistiert über CliProcessManager/AufgabeService.AktualisiereLaufStatusAsync").

### TaskDetailViewModel.cs / CliProcessManager.cs / KiAusfuehrungsService.cs

- **Doppelter Code / divergierende Semantik** — Das Mapping `CliRuntimeStatus → AufgabeLaufStatus?` existiert an drei Stellen: `CliProcessManager.LaufdatenSicherstellenAsync` (Z. 292–297), `TaskDetailViewModel.StartCliAndUpdateStateAsync` (Z. 1727–1732) und — semantisch abweichend — `KiAusfuehrungsService.GetLiveLaufStatus` (Z. 134–144: `Inaktiv`/keine Session → `Laeuft` statt `null`). Die zwei identischen Switches mit je ~5 Zeilen driften bei einer Enum-Erweiterung (neuer `CliRuntimeStatus`) zwangsläufig auseinander, und die Abweichung zwischen den beiden Semantiken ist nur implizit dokumentiert.

  Empfehlung: Eine gemeinsame Mapping-Hilfsmethode extrahieren (z. B. statischer Mapper neben `CliRuntimeStatus` oder auf `KiAusfuehrungsService` mit Parameter für die `Inaktiv`-Behandlung); wo die `GetLiveLaufStatus`-Semantik passt, diese direkt verwenden statt des Duplikat-Switchs.

### AufgabeService.cs (AufgabeService)

- **Toter Code** — `AusfuehrungAktivSetzenAsync` (Z. 953) und `AktualisiereLaufStatusAsync` (Z. 1029) haben nach diesem Change keinen produktiven Aufrufer mehr (einziger bisheriger Aufrufer `TaskDetailViewModel` bzw. `CliProcessManager` nutzt jetzt `AktivenLaufSicherstellenAsync`); nur noch Tests halten die öffentlichen Methoden am Leben. Die Beibehaltung war laut Plan-Review bewusst, faktisch ist es aber tote Produktiv-API-Fläche inkl. eigener Testpflege.

  Empfehlung: Entweder entfernen (inkl. der ausschließlich sie deckenden Tests) oder mit einem kurzen Hinweis im Doc-Kommentar begründen, warum sie als API bestehen bleiben.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/CliProcessManager.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
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
