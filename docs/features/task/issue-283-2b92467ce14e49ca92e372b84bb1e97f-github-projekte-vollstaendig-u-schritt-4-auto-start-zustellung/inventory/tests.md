# Bestandsaufnahme-Detail: Tests und Testinfrastruktur

Zurück: [../inventory.md](../inventory.md)

## Testklassen (bestehend)

### `ScmEventMonitoringServiceTests_TaskAssignment` (`src/Softwareschmiede.IntegrationTests/Services/`)

- `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry` — Anlage + `AufgabeStatus.Gestartet` + Inbox-Zuordnung (ohne CLI-Konfiguration → Ereignis wartet implizit).
- `Monitor_QualifiedPullRequestEvent_CreatesReviewSourceTask`, `Monitor_RepeatedPollsAndBatches_CreateSingleTask`, `Monitor_UnqualifiedEvent_KeepsUnassignedWithoutTask`, `Monitor_VanishedReference_StaysUnassigned`, `Monitor_ResolutionFailure_RecordsCheckpointFailure`, `Monitor_ExistingTask_IsReusedWithoutSecondCreation`.

### `ScmEventDispatchTests` / `ScmEventDispatchTests_HostRetry` (`src/Softwareschmiede.IntegrationTests/Services/`)

- `Monitor_RepositoryAliasesForDifferentTasks_ReportAmbiguityWithoutDispatch`, `Monitor_RepositoryAliasesWithDifferentReferences_AssignBothTasks`, `Monitor_UnresolvedAlias_BlocksPartialAssignmentUntilResolutionRecovers`, `Monitor_ProcessesPastOneHundredWaitingEvents` (Dispatch → `Zugestellt` mit `configured: true`), `ConcurrentDispatchAndManualStart_PreserveWorkspaceAndStartOneProcess`.
- HostRetry: `DueHostResponse_DoesNotBypassDispatchDelay`, `CompletedEvent_RetriesHostResponseWithoutNewDispatch`.

### `ScmEventOrchestratorTests` (`src/Softwareschmiede.Tests/Application/Services/`)

- `MissingWriteAccess_ExplainsTokenSettingsBeforeCliStart`, `NoDefault_ActivatesTaskBeforeWaitingForSelection` (belegt: Status `Gestartet` + Wartend „Keine Standard-CLI" für **manuell** angelegte Aufgabe), `BlockedTask_IsNeverActivated` (Theory Beendet/Archiviert/Pausiert), `Confirmation_ChannelFailureThenManualInput_DoesNotAuthorizeLaterUnknownPrompt`, `TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically`, `ConfirmReady_LiftsNextAttemptLock`.

### `ScmEventsE2ETests` (`src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`)

- Eine konsolidierte Methode `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` (~450 Zeilen Ablauf): Empfang/Originaltext, Restart-Persistenz, Öffnen/CLI-Auswahl (auch Abbruch), Zustellung → `Verarbeitet`, Filter, blockierte Aufgaben (3/4 beendet-archiviert/5 pausiert), Startfehler+Retry, Beschreibungen/PR-Kommentare, 403-Wiederaufnahme, Bereitschaftsbestätigung, Kanalbelegung, 401/`CanWrite`, ungewisse Zustellung/Neuversand, Klon-Pfad (Aufgabe 13 ohne `LokalerKlonPfad`), fehlendes Plugin + Einstellungen, **E7-Anlage**: qualifizierter Kommentar an Issue 20 → genau eine Aufgabe + Inbox-Eintrag (endet ohne Zustellungsnachweis).

## Hilfsinfrastruktur

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`)

- Reale SQLite-DB + reale Services (`AufgabeService`, `PluginSelectionService`, `PluginDefaultSettingsService`, `PluginActivationService`, `EntwicklungsprozessService` mit `KiAusfuehrungsService`, `ScmEventOrchestrator`, `ScmEventTaskResolver`, `ScmEventMonitoringService`, `ScmEventStore`, `ScmEventOutbox`, `ScmEventExchangeProtocol`).
- `Git` (`Mock<IGitPlugin>` + `IScmEventSource`/`IScmEventResponder`): `GetEventRepositoryAsync` → `fixture.Repository` (`CanWrite=true`), `ReadEventsAsync`/`EnumerateReferencesAsync`/`ReadIssueAsync`/`ReadPullRequestAsync` per Setup steuerbar, `CloneRepositoryAsync` → `git init` + `owned.txt`, `ExecuteResponseAsync` sammelt in `Responses`.
- `Ki` (`Mock<IKiPlugin>`): Prefix `Softwareschmiede.KiSimulator`, `CheckHealthAsync → true`, `GetTerminalStartSpecAsync` mit `SOFTWARESCHMIEDE_SCM_SIMULATOR=1`; `ITerminalSessionFactory` → `ping`-Prozess + `Mock<ITerminalSession>` mit `SCM_SIMULATOR_PROTOCOL_V1`/`SCM_READY_V1`-Buffer → Simulator-Kanal = `Supported`, Prompts landen in `fixture.Prompts`.
- `AddAsync(status, configured, cloned)`: legt Projekt + Repository (`https://github.com/owner/repo`) + Aufgabe an; `configured` setzt `KiPluginPrefix` direkt, `cloned` vorbelegt `LokalerKlonPfad`; erzeugt qualifizierten Inbox-Eintrag via `AcceptBatchAsync`.
- `DispatchAsync(eventId)`, `EventAsync(eventId)`, `SetPrompt(taskId, prompt)`, `CloneGate`.
- Kein eigener Helper für CLI-Defaults — über `PluginDefaultSettingsService.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, ScmDispatchFixture.KiPrefix)` (global) bzw. `SaveProjectDefaultPluginPrefixAsync(projektId, …)` im Testscope setzbar.

### `ScmE2EFixture` (`src/Softwareschmiede.Tests/E2E/ScmE2EFixture.cs`)

- Isolierte Test-DB + `ScmE2ETestConfiguration` (Datei-Fixture): `AddText(number, id, body, kind)` steuert simulierte GitHub-Inhalte; `Hold("accepted"/"complete")`/`Control(...)` steuern Simulator-Gates; `Scenario.Authenticated/CanWrite/PluginAvailable/RejectAiResponse` via `SaveScenario()`; `WaitEventAsync(number, status[, remoteId])` (über `TaskIds`), `WaitForPollAsync`, `Prompts()`/`Responses()` als JSONL-Nachweise; `BeginRuntimeCommand` für parallele CLI-Läufe.
- Seed: 17 Aufgaben mit `IssueReferenz`/`PullRequestReferenz`, `BasisBranchName="main"`, vorbereitete Worktrees; Aufgabe 13 ohne `LokalerKlonPfad`; `DefaultKiPluginKey = null` (kein globaler CLI-Default).
- `ScmE2EApiClient` (App-intern, `IGitHubEventApiClient`): REST `issues/{n}`/`pulls/{n}` szenariogesteuert (`SCM Referenz {n}`-Titel-Fallback), GraphQL-Enumeration/Details, `reactions`/`comments`-Antworten.
- `ScmE2EGitRunner`: ersetzt `https://github.com/scm-owner/scm-e2e` durch lokale `origin`-Kopie → echter `git clone`/`branch` im E2E möglich.
- `ScmE2ETerminalSessionFactory`: startet den KI-Simulator (`--scm-exchange`).

## Testkonventionen / Regeln

- xUnit + FluentAssertions-frei (Assert.*), Moq; Integrationstests laufen gegen reale SQLite-DB (`ScmDispatchFixture`), E2E `[OsInterface]` + `[Collection("E2E")]`.
- Testklassen nach Thema: `{Klasse}Tests_{Thema}`; E2E in der einen konsolidierten Methode halten.
- Stabile Lane: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`; IntegrationTests eigenes Projekt `src/Softwareschmiede.IntegrationTests/`.

## Abdeckungslücken für Schritt 4

- Kein Test für „Ereignis ohne Aufgabe → Anlage → Auto-Start (Klon/Branch/Sitzung) → `Zugestellt`" mit aufgelöster Standard-CLI.
- Kein Test für „Anlage → Wartend ohne CLI → CLI-Konfiguration + `RetryAsync` → `Zugestellt`" und „→ `OpenFromQueueAsync`-Auswahl → `Zugestellt`" für angelegte Aufgaben.
- Kein Test, dass eine wiederverwendete **blockierte** Aufgabe (beendet/archiviert/pausiert) im neuen Pfad wartend bleibt und nicht gestartet wird.
- Kein expliziter Nachweis, dass die Anlage auch ohne CLI erfolgt (Entscheidungsvariante) — implizit in `Monitor_QualifiedCommentOnUnassignedIssue_*`, aber ohne Wartend-/Bedienbarkeitsbehauptung.
- E2E: E7-Anlageschritt endet bei Inbox; Wartend-Text, Öffnen/CLI-Auswahl und Zustellung der angelegten Aufgabe sind nicht abgedeckt.
