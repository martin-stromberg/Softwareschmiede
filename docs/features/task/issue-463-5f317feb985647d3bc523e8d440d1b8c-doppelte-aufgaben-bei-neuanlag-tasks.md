# Tasks: Doppelte Aufgaben bei Neuanlage (Issue #463)

|| # | Bereich | Aufgabe | Status | Testnachweis |
||---|---------|---------|--------|--------------|
|| 1 | Stabilisierungsfrist | `IScmPromptSessionAdapter` um `DateTimeOffset? ReadySinceUtc` erweitern | Offen | — |
|| 2 | Stabilisierungsfrist | `ScmPromptSessionAdapter`: `_readySinceUtc`-Feld, Stempelung/Leerung in `SetReady` über `_time` | Offen | — |
|| 3 | Stabilisierungsfrist | `ScmEventOrchestrator`: Konstante `DeliveryStabilityPeriod` (10 s, `internal`) anlegen | Offen | — |
|| 4 | Stabilisierungsfrist | `DispatchCoreAsync`: Fristprüfung nach `Ready`-/`IsBusy`-Check, `NextAttemptUtc`-Aufschub + `WaitWithCommentAsync` | Offen | — |
|| 5 | Stabilisierungsfrist | `DispatchPreparationAsync`: analoge Fristprüfung + `WaitAsync` | Offen | — |
|| 6 | Reihenfolge | `ScmDispatchPriority.EventSelection` um `Purpose` erweitern | Offen | — |
|| 7 | Reihenfolge | `ScmDispatchPriority.OrderForDispatch`: Purpose-Tiebreak innerhalb gleicher Klasse/Klasse-2-Zeit vor `Sequence` | Offen | — |
|| 8 | Reihenfolge | `ScmEventOrchestrator.DispatchDueEventsAsync`: Projektion um `x.Purpose` erweitern | Offen | — |
|| 9 | Aufgabenauflösung | `ScmEventTaskResolver`: `IssueSourceKey` auf `internal` heben, `internal static Guid IssueGateKey(ScmEventReference)` (SHA-256-Gate-Schlüssel) hinzufügen | Offen | — |
|| 10 | Aufgabenauflösung | `ScmEventTaskResolver.ResolveIssueAsync`: Kandidatenmenge um `Purpose == ScmIssuePreparationService.Purpose` erweitern, zweistufige Auswertung (regulär vor zweck-markiert, `Ambiguous` bei Mehrfachtreffern je Stufe), Abschnitt unter `ScmTaskCoordinator.Shared.RunAsync(IssueGateKey(reference), …)` | Offen | — |
|| 11 | Aufgabenauflösung | `ScmIssuePreparationService.DetectAndEnqueueAsync`: je Beobachtung EventKey-Prüfung + Bestandssuche + Anlage/Bindung unter dem Issue-Gate; Kandidaten `GitRepositoryId ∈ unlocked`, `Status != Completed/Archived`, Issue-Nummer, `Purpose == null \|\| Purpose`; Auswahl regulär zuerst, dann `CreatedAt`; internes Ereignis an Treffer binden; Log-/Protokolltexte für Zuordnungsfall | Offen | — |
|| 12 | Aufgabenauflösung | `ScmEventMonitoringService.RunOnceAsync` (~Zeile 50–51): Verknüpfungsfilter auf `Purpose == null \|\| Purpose == ScmIssuePreparationService.Purpose` erweitern + Kommentar anpassen | Offen | — |
|| 13 | Aufgabenauflösung | `ScmEventMonitoringService.RunOnceAsync` (~Zeile 61–62): PR-Guard — `task.PullRequests`-Schleife nur für `task.Purpose is null` | Offen | — |
|| 14 | Aufgabenauflösung | `ScmEventMonitoringService`: `linked`/`resolved`-Tupel um `string? Purpose` erweitern (inkl. `BuildAliasGroups`- und `EnumerateNotificationsAsync`-Signaturen) | Offen | — |
|| 15 | Aufgabenauflösung | `ScmEventMonitoringService` Gruppen-Auswertung (~Zeile 192–201): zweistufige Auswahl statt `taskIds.Length == 1` (genau eine reguläre → zuordnen; >1 regulär → mehrdeutig; 0 regulär + genau 1 zweck-markiert → zuordnen; >1 zweck-markiert → mehrdeutig; sonst Resolver-Pfad) | Offen | — |
|| 16 | Testinfrastruktur | `ScmDispatchFixture`: `fixture.Time`-Zeitquelle (Standard `TestTimeProvider`), `DispatchAsync` mit Advance + Wiederholversuch bei `Wartend`, Monitor-Helfer `RunMonitorAsync` | Offen | — |
|| 17 | Tests | `Monitor_TriggerOnPreparedIssue_ResolvesToCanonicalTask` → `..._AssignsToPreparationTask`: eine Aufgabe, beide Ereignisse derselben `WorkItemId` | Offen | — |
|| 18 | Tests | `Monitor_TriggerOnIssueWithPreparationTask_CreatesOneInstructionTaskAlongside` → `..._AssignsToPreparationTask`: Zuordnung zur Purpose-Aufgabe ohne Zweitanlage | Offen | — |
|| 19 | Tests | Neu: `Monitor_CommentTaskPrecedesIssueDetection_InternalEventBindsToExistingTask` (entgegengesetzte Reihenfolge) | Offen | — |
|| 20 | Tests | Neu: `Monitor_FollowUpCommentOnPreparationTask_AssignsDirectly` (Verknüpfungsmenge) | Offen | — |
|| 21 | Tests | Neu: `Monitor_LegacyTaskPair_CommentAssignsToRegularTask` (Altbestand-Paar → reguläre Aufgabe, keine Ambiguous-Diagnose) | Offen | — |
|| 22 | Tests | Neu: `Monitor_PullRequestOnPreparationTask_StaysUnassigned` (PR-Guard) | Offen | — |
|| 23 | Tests | Neu: `Preparation_ExistingCompletedTask_CreatesNewPreparationTask` (beendete/archivierte Aufgabe → neue Purpose-Aufgabe) | Offen | — |
|| 24 | Tests | Neu: `Preparation_ExistingTaskOnDisabledRepository_CreatesNewPreparationTask` (nicht freigeschaltetes Repository → neue Purpose-Aufgabe) | Offen | — |
|| 25 | Tests | Neu: `Preparation_ConcurrentCommentResolution_CreatesSingleTask` (Issue-Gate-Invariante unter Parallelaufruf) | Offen | — |
|| 26 | Tests | Neu: `Dispatch_ReadySinceYoungerThanDeadline_StaysWaiting` (Frist-Aufschub + Zustellung nach Advance) | Offen | — |
|| 27 | Tests | Neu: `Dispatch_ReadyFlipsWithinDeadline_ResetsDeadline` (Statuswechsel setzt Frist zurück) | Offen | — |
|| 28 | Tests | Neu: `DispatchPreparation_ReadySinceYoungerThanDeadline_StaysWaiting` (Frist im Aufbereitungspfad) | Offen | — |
|| 29 | Tests | Neu: `ScmDispatchPriorityTests` Purpose-Tiebreak + kein Klassensprung | Offen | — |
|| 30 | Tests | Bestehende Dispatch-/Monitoring-Tests auf neue Frist-Semantik verifizieren/anpassen (Dispatch-Partial-Dateien, AutoStartDelivery, RepoWide, Settings, Notifications, AmbiguityRecovery, PendingProbe, ReadySignal, TaskCompletionMonitoring, DelayIntake) | Offen | — |
|| 31 | E2E-Tests | Neu: konsolidierte Testmethode `ScmEvents_NewIssueWithTriggerComment_CreatesSingleTask_E2E` in `ScmEventsE2ETests` (`E2E_ScmEvents.cs`) inkl. Pflicht-Frist-Teilschritt | Offen | — |
|| 32 | E2E-Tests | `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline`: Wartehelfer/Budgets gegen reale Frist-Wirkung verifizieren | Offen | — |
|| 33 | Abschluss | Voll-Build (`dotnet build Softwareschmiede.slnx -c Debug`), reguläre + OsInterface-Testlanes mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, `dotnet format --verify-no-changes` | Offen | — |
