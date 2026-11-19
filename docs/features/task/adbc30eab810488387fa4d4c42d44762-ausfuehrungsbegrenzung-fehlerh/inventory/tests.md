# Tests — Bestandsaufnahme

## Testklassen

### `ScmEventExecutionLimitServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventExecutionLimitServiceTests.cs`

Unit-Tests der Zulassungskontrolle mit InMemory-DB (`TestDbContextFactory.Create`) und gemocktem `ICliExecutionLimitSource`. `ReservierungenZuruecksetzen()` wird in Ctor und `Dispose` aufgerufen. **Keine Tests mit `PausiertBisUtc` vorhanden** — Pausenfälle der Belegungszählung sind unabgedeckt.

- `GetStartBlockReasonAsync_OhneGrenzen_GibtNull` — ohne Limits frei
- `GetStartBlockReasonAsync_UngueltigeGrenze_GiltAlsUnbegrenzt` (Theory) — 0/negativ/Text = unbegrenzt
- `GetStartBlockReasonAsync_GlobalesLimitErreicht_Sperrt` — globales Limit sperrt jeden Prefix
- `GetStartBlockReasonAsync_GlobalesLimitNichtErreicht_GibtNull` — frei unter dem Limit
- `GetStartBlockReasonAsync_PluginLimitErreicht_SperrtNurDiesesPlugin` — Plugin-Limit wirkt selektiv
- `GetStartBlockReasonAsync_BeideLimitsErreicht_MeldetGlobalesLimit` — Vorrang des globalen Grundes
- `GetStartBlockReasonAsync_GemischtePlugins_ZaehltNurEigenenPrefix` — getrennte Plugin-Zählung
- `GetStartBlockReasonAsync_LebendeSession_ZaehltTatsaechlichesPlugin` — lebende Sitzung attribuiert aufs tatsächliche Plugin
- `GetStartBlockReasonAsync_SessionTot_FaelltAufAufgabenPrefixZurueck` — Fallback auf `KiPluginPrefix`
- `GetStartBlockReasonAsync_OhnePrefix_ZaehltNurGlobal` — ohne Plugin-Attribution nur global
- `GetStartBlockReason_PrueftGlobalesVorPluginLimit` — reine Auswertungsreihenfolge
- `FuerPlugin_IgnoriertGrossKleinschreibung` — `OrdinalIgnoreCase` im ProPlugin-Zähler
- `PruefeUndReserviereStartPlatzAsync_OhneGrenzen_FreiOhneReservierung` — unbegrenzt ohne Reservierung
- `PruefeUndReserviereStartPlatzAsync_Reservierung_BelegtGlobalenPlatz` — Reservierung zählt sofort, Dispose gibt frei
- `PruefeUndReserviereStartPlatzAsync_Reservierung_BelegtNurEigenesPluginLimit` — Plugin-selektive Reservierung
- `PruefeUndReserviereStartPlatzAsync_ParalleleVersuche_NurEineReservierung` — Atomarität über getrennte Service-Instanzen/DbContexts
- `PruefeUndReserviereStartPlatzAsync_Reservierung_VerfaelltNachLebensdauer` — Verfall via `FakeTimeProvider`

### `ScmEventDispatchTests_Limits`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Limits.cs`

Integrations-Tests der Begrenzung auf `ScmDispatchFixture` (echte SQLite, echte Services, gemockte Terminal-Grenze mit echtem Kindprozess). **Kein Pausen-Fall vorhanden** — das hier beschriebene Muster (`Dispatch_SlotFreed_WaitingEventStarts`) ist die Referenz für den geforderten `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts`-Fall.

- `Dispatch_GlobalLimitReached_EventWaitsWithReason` — ehrlicher Wartegrund, kein harter NextAttempt-Lock
- `Dispatch_SlotFreed_WaitingEventStarts` — nach Prozessende (`StopCliAsync`) startet das wartende Ereignis
- `Dispatch_PluginLimitReached_OnlyThatPluginWaits` — Plugin-Selektivität mit zweitem KI-Profil
- `Dispatch_BothLimits_GlobalDecidesWhenReached` — beide Limits wirken gleichzeitig
- `Dispatch_ReconcileAttempt_IgnoresLimit` — Reconcile läuft unbegrenzt
- `Dispatch_EndedOrArchivedTask_StaysBlocked` (Theory) — `BlockReason`-Sperren bleiben
- `Monitor_PrioritizedDispatch_ContinuationThenDelayThenSequence` — priorisierte Batch-Reihenfolge
- `Monitor_GlobalLimitOne_ContinuationWinsSlotOverSequence` — Fortsetzung gewinnt den einen Platz
- `OpenFromQueue_LimitReached_WaitsWithReason` — UI-Pfad durchläuft dieselbe Zulassung
- `Dispatch_ConcurrentStartDuringHealthCheck_GlobalLimitOne_ExactlyOneStarts` — Reservierung deckt das Startfenster
- `Dispatch_StartFails_ReservationReleasedForNextDispatch` — Freigabe bei Fehlschlag
- `Dispatch_HostActions_UnlimitedWhileSlotOccupied` — Hostaktionen laufen unbegrenzt

### `ScmEventDispatchTests_Startpruefung`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Startpruefung.cs`

Integrations-Tests der ereignisgesteuerten Startprüfung.

- `StartPruefung_CliBeendet_WartendesEreignisStartetOhnePoll` — Prozessende → Trigger → sofortiger Folgestart
- `StartPruefung_PauseAufgehoben_EreignisStartetOhnePoll` — `SetPauseAsync(null)` löst aus; zuvor „Aufgabe pausiert; Start gesperrt."
- `StartPruefung_VerzoegerteAufnahme_LoestSignalAus` — `#verzögert`-Direktive setzt Pause und meldet den Auslöser
- `StartPruefung_RateLimitPause_LoestSignalAus` — `KiPluginLimitService.VerarbeiteRateLimitAsync` setzt Pause und meldet den Auslöser
- `StartPruefung_UeberlappendeAusloeser_KeinDoppelstart` — koaleszierte Auslöser + Poll serialisieren korrekt
- `StartPruefung_MonitoringDeaktiviert_KeinDispatch` — Laufzeit-Schalter greift auch ereignisgesteuert
- `StartPruefung_NichtScmLauf_ZaehlungKorrektUndLoestAus` — Nicht-SCM-Lauf belegt/gibt frei korrekt

### `ScmEventDispatchTests_Delay`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Delay.cs`

- `DeferredEvent_ReceivesWaitingCommentAndDeliversAfterTarget` — verzögerte Zustellung; Pause wird per `ExecuteUpdateAsync` aufgehoben
- `Cancel_DeferredEvent_LiftsTaskPause` — Abbruch hebt die Direktiven-Pause auf

### `ScmStartPruefungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmStartPruefungsServiceTests.cs`

Unit-Tests der Signalfassade mit lokaler `Fixture` (Moq `IRunningAutomationStatusSource`).

- `Ausloeser_SignalWirdAngenommen` — Koaleszierung mehrfacher Auslöser
- `Laufzahl_NurRueckgangLoestAus` — nur sinkende Zählung löst aus
- `Laufzahl_RueckgangAufNullLoestAus` — Rückgang auf null löst aus
- `Dispose_TrenntAbonnements` — Abmeldung sauber

### `ScmDispatchPrioritaetTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmDispatchPrioritaetTests.cs`

- `PrioritaetsKlasse_Pausiert_GibtKlasse1` / `PrioritaetsKlasse_PausiertInZukunft_GibtKlasse1` — gesetztes `PausiertBisUtc` → Fortsetzungsklasse
- `PrioritaetsKlasse_LaufBeendetAufgabeAktiv_GibtKlasse1` (Theory) — beendeter Lauf + aktiver Status → Klasse 1
- `PrioritaetsKlasse_KeinFortsetzungsfall_GibtNichtKlasse1` (Theory)
- `PrioritaetsKlasse_PausiertAberAufgabeBeendet_NichtKlasse1` (Theory) — `Beendet`/`Archiviert` schlagen die Pause
- `PrioritaetsKlasse_Verzoegert_GibtKlasse2`, `PrioritaetsKlasse_FortsetzungSchlaegtVerzoegerung`
- `OrderForDispatch_SortiertNachKlassenUndZielzeit`, `OrderForDispatch_VerzoegertOhneZielzeit_AmKlassenende`, `OrderForDispatch_OhneAufgabenzustand_Eingangsreihenfolge`

### `AufgabeServiceTests_Pause`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests_Pause.cs`

- `SetPauseAsync_ShouldPersistPausiertBisUtc_AndWriteSystemLog` — Persistenz + SystemMeldung
- `SetPauseAsync_ShouldClearPause_WhenNull` — Aufheben mit `null`
- `SetPauseAsync_ShouldAllowActiveStatuses` (Theory) — `Neu`/`Gestartet`/`Wartend` erlaubt
- `SetPauseAsync_ShouldThrow_WhenStatusNotPausable` (Theory) — `Beendet`/`Archiviert` abgelehnt
- `SetPauseAsync_ShouldThrow_WhenTimestampInPast` — Zeitpunkt muss zukünftig sein
- `SetPauseAsync_ShouldThrow_WhenAufgabeNotFound`

### `KiPluginLimitServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiPluginLimitServiceTests.cs`

- `VerarbeiteRateLimitAsync_ShouldPersistSessionLimitKey` — Persistenz des Reset-Zeitpunkts
- `VerarbeiteRateLimitAsync_ShouldPauseSamePrefixTasks_AndNotify` — pausiert Prefix-Gruppe + Notifier
- `VerarbeiteRateLimitAsync_ShouldNotPause_WhenResetExpired` — abgelaufener Reset pausiert nicht
- `GetAktiveSessionLimitsAsync_ShouldReturnOnlyFutureLimits` — nur zukünftige Limits
- `VerarbeiteRateLimitAsync_ShouldDoNothing_WhenAusloeserOhnePrefix` — ohne `KiPluginPrefix` keine Aktion
- `VerarbeiteRateLimitAsync_ShouldNotPause_WhenAufgabeBeendet` — beendete Aufgaben unberührt
- `VerarbeiteRateLimitAsync_ShouldKeepLongerPause_AndExtendShorterPause` — Max-Semantik

### `ScmEventOrchestratorTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs`

- `BlockedTask_IsNeverActivated` (Theory) — `Beendet`/`Archiviert`/pausierte Aufgabe (`Neu` + `PausiertBisUtc` +1h): kein Start, Status „Wartend"

### Weitere berührte Testklassen (Pausen-Bezug vorhanden, nicht limit-spezifisch)

- `KiAusfuehrungsServiceTests` (`src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`): `GetLaufendeAufgabenIds_ListetNurLaufendeHandles`, `IsRunning_ShouldReturnFalse_WhenNoProcessStarted`, `GetRunningCount_ShouldReturnZero_WhenNoProcessStarted`
- `LaufendeAutomatisierungenZaehlungTests` (`src/Softwareschmiede.Tests/Application/Services/LaufendeAutomatisierungenZaehlungTests.cs`): `ZaehleAsync_ZaehltNicht_BeiPausierterAufgabe` — pausierte Aufgaben zählen in der Auto-Shutdown-Klassifikation bereits nicht
- `AufgabeRecoveryServiceTests` (`src/Softwareschmiede.Tests/Application/Services/AufgabeRecoveryServiceTests.cs`): `ScanForRecoveryCandidates_ShouldExcludePausedTasks`, `RecoverManuellAsync_ShouldThrow_WhenTaskIsPaused`

## Hilfsmethoden

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`

Integrations-Fixture mit echter SQLite-DB und echten Services; Terminal-Grenze gemockt (`ITerminalSessionFactory` startet echten `cmd.exe`-Ping-Kindprozess → echte `_handles`-Einträge im `KiAusfuehrungsService`). Registriert `ICliExecutionLimitSource`/`IRunningAutomationStatusSource` auf denselben `KiAusfuehrungsService`-Singleton sowie `ScmStartPruefungsAusloeser` und `ScmStartPruefungsService`.

- `AddAsync(status, configured, cloned, body, kiPluginPrefix, ...)` — legt Projekt/Repository/Aufgabe an und speist ein Ereignis über `ScmEventStore.AcceptBatchAsync` ein (löst bei `#verzögert`-Body den echten Pausenpfad aus)
- `DispatchAsync(id)` — `ScmEventOrchestrator.DispatchAsync` im Scope
- `EventAsync(id)` — lädt `ScmIncomingEvent` (AsNoTracking)
- `SetSettingAsync(schluessel, wert)` — generische Einstellung (z. B. `GlobalLimitKey`)
- `Monitor` / `StartPruefung` / `Ausloeser` — Zugriff auf Poll-Dienst, Startprüfungs-Dienst und Auslöser
- `Cli` — `KiAusfuehrungsService`-Singleton (`StopCliAsync`, `IsRunning`, `GetScmPromptSession`)
- `Sessions` / `StartSpecs` / `Prompts` / `Responses` — Beobachtungslisten der gemockten Session-Grenze

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`

- `Create()` — InMemory-`SoftwareschmiededDbContext` für Unit-Tests
- `CreateSqlite(connectionString, interceptors)` — SQLite-Variante

### Lokale Helfer in `ScmEventExecutionLimitServiceTests`

- `LaufendeAufgabeAsync(titel, kiPluginPrefix, session)` — legt `Aufgabe` (`Gestartet`, `AusfuehrungsStatus.Aktiv`) an und verknüpft optionale Session-Mock
- `SetzeLaufende(params Guid[])` — `GetLaufendeAufgabenIds` des Mocks setzen
- `LebendeSession(pluginPrefix)` — `IScmPromptSessionAdapter`-Mock mit `IsAlive = true`

**Hinweis:** `LaufendeAufgabeAsync` setzt aktuell kein `PausiertBisUtc` — eine Erweiterung um einen Pausen-Parameter oder einen eigenen Helfer wäre der natürliche Ansatzpunkt für die geforderten Pausenfälle (dokumentiert als Bestand, nicht als Plan).
