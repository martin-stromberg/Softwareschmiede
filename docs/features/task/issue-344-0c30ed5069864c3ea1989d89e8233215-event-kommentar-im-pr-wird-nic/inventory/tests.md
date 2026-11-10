# Tests

## Testklassen

### `ScmEventMonitoringServiceTests_TaskAssignment`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_TaskAssignment.cs`

Zentrale Testklasse für Aufgabenanlage/-wiederverwendung aus qualifizierten Ereignissen unzugeordneter Referenzen (SQLite + `ScmDispatchFixture`, gemockte `IScmEventSource`).

- `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry` — Qualifizierter Issue-Kommentar legt genau eine Aufgabe mit `IssueReferenz` an; Inbox-Eintrag zeigt auf sie.
- `Monitor_QualifiedPullRequestEvent_CreatesReviewSourceTask` — Qualifiziertes PR-Ereignis legt Aufgabe mit `PullRequestReferenz` (`Rolle = ReviewSource`) an.
- `Monitor_RepeatedPollsAndBatches_CreateSingleTask` — Wiederholte Läufe/Batches derselben Referenz ohne Zweitanlage (Dedup).
- `Monitor_UnqualifiedEvent_KeepsUnassignedWithoutTask` — Ohne Trigger weder Aufgabe noch Inbox; `ReadIssueAsync` wird nicht aufgerufen.
- `Monitor_VanishedReference_StaysUnassigned` — `ReadIssueAsync` → `null` ⇒ `Unassigned`-Snapshots, keine Aufgabe.
- `Monitor_ResolutionFailure_RecordsCheckpointFailure` — `ScmProviderException` beim Detailabruf → Checkpoint-Backoff.
- `Monitor_ExistingTask_IsReusedWithoutSecondCreation` — Nachträglich verknüpfte Aufgabe wird wiederverwendet (Issue-Pfad, Zeile 167) — **fachliches Gegenstück zum geforderten neuen Fall**.

Lokale Hilfsmethoden derselben Datei: `Monitor`, `TasksAsync`, `RepositoriesAsync`, `AddRepositoryAsync` (legt `Projekt` + `GitRepository` an), `ConfigureEnumeration`, `ConfigureObservations` (qualifizierte Issue-Kommentare, optional Description), `Observation`, `ConfigureIssue`, `ConfigurePullRequest` (`BranchName = "feature/branch"`, `SourceRepositoryId = "owner/repo"`, `SourceRef = "refs/heads/feature/branch"`).

### `ScmEventMonitoringServiceTests_RepoWide`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_RepoWide.cs`

- `Monitor_ActiveRepositoryWithoutTask_EnumeratesAndStoresSnapshotsWithoutInbox` — Enumeration ohne Aufgabe → Snapshots ohne Inbox.
- `Monitor_EnumeratedReferenceWithTask_EntersInboxUnchanged` — Enumerierte, verknüpfte Referenz → Inbox der Aufgabe.
- `Monitor_RepeatedPolls_ProduceNoDuplicates` — Dedup über Checkpoints.
- `Monitor_InactiveOrNonGitHubRepositories_AreNotEnumerated` — `Aktiv`-Filter.
- `Monitor_EnumerationFailure_UsesBackoffWithoutBlockingLinkedReferences` — Enumerationsfehler blockieren verknüpfte Quellen nicht.

### `ScmEventMonitoringServiceTests_AutoStartDelivery`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_AutoStartDelivery.cs`

- `Monitor_CreatedTaskWithDefaultCli_StartsAndDelivers` / `..._WithSettingsDefaultCli_...` — Neu angelegte Aufgabe wird aktiviert und das Ereignis zugestellt.
- `Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers` / `Monitor_CreatedTaskWithoutCli_QueueSelectionDelivers` — Warten auf CLI-Auswahl.
- `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` (`[Theory]`, Zeile 204) — Wiederverwendete `Beendet`/`Archiviert`/pausierte Aufgabe: Ereignis bleibt `Wartend`, kein Start — direkter Beleg des `BlockReason`-Verhaltens für die „aktiv"-Frage.

### `ScmEventMonitoringServiceTests_Notifications`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Notifications.cs`

- `Monitor_SeparatesKonto_MentionNotification_EntersInboxViaResolver` — Notification-Pfad über den Resolver in die Inbox.
- `Monitor_SeparatesKonto_ForeignNotification_DiscardedWithDiagnostic`, `..._NonMentionReasons_AreIgnored`, `..._InboxFailure_KeepsWatermarkForRetry`, `..._InboxCommit_WithPreexistingCheckpoint_UpdatesWatermark`, `..._NotificationFromForeignHost_DiscardedDespiteMatchingNodeId`, `Monitor_ModeSwitch_BackToEnumeration` — Inbox-Randfälle.

### `ScmEventMonitoringServiceTests_AmbiguityRecovery`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_AmbiguityRecovery.cs`

- `Monitor_AliasRemovedAfterRestartAndPollWindow_AcceptsUnchangedCommentExactlyOnce` — Alias-Verlust/Wiederkehr mit Poll-Fenster (eigener `PollClock`).

### `ScmEventMonitoringServiceTests_RepositoryResolution`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_RepositoryResolution.cs`

- `Monitor_SeparatesKonto_InaccessibleRepository_DoesNotBlockMentionsOfOtherRepositories`, `Monitor_RepositoryResolutionNotFound_IsRetriedAfterBackoffInsteadOfBlockedForever` — Auflösungs-Backoff.

### `ScmEventMonitoringServiceTests_Settings`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Settings.cs`

- `Monitor_DisabledMonitoring_SkipsObservationAndDispatch_UntilReEnabled`, `Monitor_CustomTriggerKeyword_AcceptsNewAndRejectsOld`, `ManualRetry_DispatchesWhileMonitoringDisabled`, `ExchangeService_DisabledMonitoring_SkipsPendingAttempts` — Laufzeit-Schalter und Trigger-Keyword.

### `ScmEventDispatchTests`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests.cs`

- `Monitor_RepositoryAliasesForDifferentTasks_ReportAmbiguityWithoutDispatch` (`[Theory]`, auch `ScmReferenceKind.PullRequest`) — Zwei Aufgaben mit Alias/Referenz zum selben Repository → Ambiguous ohne Dispatch.
- `Monitor_RepositoryAliasesWithDifferentReferences_AssignBothTasks` — Unterschiedliche Referenzen sauber zugeordnet.
- `Monitor_UnresolvedAlias_BlocksPartialAssignmentUntilResolutionRecovers` — Unvollständige Auflösung blockiert die Zuordnung.
- `Monitor_ProcessesPastOneHundredWaitingEvents` — Dispatch-Batching.
- `ConcurrentDispatchAndManualStart_PreserveWorkspaceAndStartOneProcess` — `ScmTaskCoordinator`-Serialisierung.
- Enthält Hilfs-Setup mit `PullRequestReferenz` (`Provider = GitHub`, `PullRequestNumber`, `MonitoringPhase.NotMonitored`, Zeilen ~173–183).

### `ScmTaskCompletionMonitoringServiceTests`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs`

- `Complete_ReportedPullRequestNumber_LinksReferenceToTask` — `pullRequestNumber` aus `complete.json` → `PullRequestReferenz` (`CreatedByTask`); der Meldepfad, der die Lücke erst nach Abschluss schließt.
- `Monitor_MergedPullRequest_StopsCliAndCompletesTaskAfterWait`, `..._CancelsScheduledPromptOnComplete`, `Monitor_ReopenedPullRequest_ResetsCountdown`, `Monitor_ClosedIssue_WithoutPullRequest_CompletesTaskAfterWait`, PR-Vorrang-/Fremd-Referenz-Fälle (Zeilen ~197–219) — `DetermineClosureAsync`-Semantik bei vorhandener Referenz.
- Hilfsmethode `ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber)` simuliert den Abschluss-Report.

### `ScmEventOrchestratorTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs`

- `BlockedTask_IsNeverActivated` (`[Theory]`, `AufgabeStatus`, `paused`) — `Beendet`/`Archiviert`/pausiert ⇒ keine Aktivierung.
- `MissingWriteAccess_ExplainsTokenSettingsBeforeCliStart`, `NoDefault_ActivatesTaskBeforeWaitingForSelection`, `SettingsDefaultCli_StartsTaskAndDelivers`, `Confirmation_...`, `TrustDialog_...`, `ConfirmReady_...`, `UnknownCliFormat_...` — Dispatch-/Session-Verhalten.

### `PullRequestReferenzServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PullRequestReferenzServiceTests.cs`

- Unit-Tests für `SaveCreatedAsync`/`SaveReportedAsync`/Monitoring-Mutationen (Fremdzuordnung, Idempotenz).

### `ScmEventPersistenceTests`, `ScmEventCancelTests`, `ScmEventExchangeServiceTests`, `ScmEventCliExchangeAccessTests*`
Dateien: `src/Softwareschmiede.IntegrationTests/Services/`

Inbox-Persistenz, Abbruch-Semantik, Antwortkanal — Dispatch-Nebenpfade, nicht die Auflösung selbst.

## Hilfsmethoden / Fixtures

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (intern, via `Softwareschmiede.TestSupport` für Integrationstests nutzbar)

- Konstruktor — Baut `ServiceProvider` mit echter SQLite-DB, gemockten `IGitPlugin`/`IKiPlugin`/`IPluginManager`/`ITerminalSessionFactory`; registriert u. a. `ScmEventMonitoringService`, `ScmEventOrchestrator`, `ScmEventTaskResolver`, `ScmEventStore`, `AufgabeService`, `PullRequestReferenzService` (Zeilen 123–157). `IScmEventSource`-Defaults: leere Enumeration, `ReadIssueAsync`/`ReadPullRequestAsync` → `null`, `ReadEventsAsync` → leerer Batch (Zeilen 63–71).
- `InitializeAsync` — `EnsureCreatedAsync` + `ScmEventStartupGate.Ready()`.
- `AddAsync(status, configured, cloned, referenceKind, contentKind, body, eventRepository)` — Legt Projekt + `GitRepository` + `Aufgabe` (beliebiger `AufgabeStatus`, optional `LokalerKlonPfad` mit `git init`, optional `KiPluginPrefix`) an und speichert direkt ein `ScmIncomingEvent` via `AcceptBatchAsync`; liefert `(TaskId, EventId)`. **Setzt `BranchName` nicht — ein „laufende Aufgabe mit Branch ohne PR-Referenz"-Zustand existiert noch nicht als Hilfsmethode.**
- `AddNotification` — Registriert Inbox-Notifications für den `SeparatesKonto`-Pfad.
- `DispatchAsync(id)`, `EventAsync(id)` — Orchestrator-Aufruf bzw. Persistenz-Lesezugriff.
- `CompleteProbeAsync(prompt)` — Simuliert die Dateitool-Kanalprobe eines CLI.
- `SetPrompt(task, prompt)` — Setzt/Raised den Terminal-Puffer der gemockten Session.
- `Responses`, `Prompts`, `Sessions`, `StartSpecs`, `Notifications`, `Repository` — Beobachtungszustände; `Repository` ist das kanonische `ScmEventRepository` (`"test-scm"/"github.com"/"R_1"/"owner/repo"`).
