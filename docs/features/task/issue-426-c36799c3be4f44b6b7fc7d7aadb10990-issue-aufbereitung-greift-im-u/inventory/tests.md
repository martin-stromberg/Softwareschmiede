# Tests

## Testklassen

### `ScmBackmergeMonitoringServiceTests`

Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmBackmergeMonitoringServiceTests.cs`

Integrationstests (xUnit + Moq, `ScmDispatchFixture`) für den Vorbild-Dienst — direktes
Muster für die Testdatei des neuen Dienstes.

- `Monitor_BackmergePullRequest_IsMergedRegularly` — Happy Path: enumerierter PR wird gemergt;
  `backmerge/`-Checkpoint erhält Endzustand.
- `Monitor_BackmergeDisabled_NoProviderWork` — Deaktivierter Modus: keinerlei Provider-Arbeit
  (Verify `GetEventRepositoryAsync`/`EnumerateReferencesAsync` `Times.Never`).
- `Monitor_EventMonitoringDisabled_BackmergeStillRuns` — `scm.monitoring.enabled = false`
  hindert den Backmerge-Zyklus nicht; beide Laufzeit-Schalter sind unabhängig.
- `EventMonitoringCycle_DoesNotTriggerBackmerge` — Der Monitor-Lauf ruft keinen Backmerge auf;
  kein `backmerge/`-Checkpoint entsteht.
- `Monitor_RepositoryResolution_UsesBenutzerkontoContext` — Bei konfiguriertem
  `SeparatesKonto` läuft die Repository-Auflösung dennoch mit
  `ScmRepositoryContext.Konto == ScmKontoModus.Benutzerkonto` (zentraler Präzedenztest für die
  Anforderung).
- `Monitor_ProjectScopedModeWithFlag_Merges` — Projektbezogener Modus mit Projekt-Freigabe.
- `Monitor_NonBackmergePullRequest_IsNotMerged` — Nicht-Treffer endet mit endgültigem
  Checkpoint (`NextAttemptUtc = long.MaxValue`).
- `Monitor_UnmergeableBackmerge_RetriesNextPoll` — Nicht mergebarer Treffer ⇒ Backoff-Wiederholung.
- `Monitor_AdminMergeEnabled_BlockedRegularMergeFallsBack` — Admin-Merge-Fallback.
- `Monitor_DraftPullRequest_WithActivation_IsReadiedAndMerged` — Draft-Aktivierung.

Private Testhelfer: `Monitor(fixture)` (Dienst aus dem Provider), `StatusInfo`,
`VerifyMerge`, `AddRepositoryWithBasisbranchAsync`, `SetModusAsync`, `SetProjektFlagAsync`,
`ConfigureBackmerge`, `ConfigureStatus`, `ConfigureMerge`.

### `ScmEventMonitoringServiceTests_IssueAufbereitung`

Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_IssueAufbereitung.cs`

Bestehende „Issue erstellt“-Erkennungstests am Enumerationskanal (10 Testmethoden).

- `Monitor_FirstObservation_CreatesBaselineOnly` — Erstbeobachtung legt nur den
  `issueaufbereitung/test-scm/github.com/R_1`-Baseline-Checkpoint an; keine Aufgaben/Ereignisse.
- `Monitor_NewIssueAfterBaseline_CreatesPreparationTaskAndInternalEvent` — Happy Path:
  `Zweck`-Aufgabe mit `…/IssueAufbereitung/9`-SourceKey, `KiPluginPrefix` aus
  `scm.issueaufbereitung.plugin`, internes sofort fälliges `ScmIncomingEvent` ohne
  Trigger-Validierung, SystemMeldung im Protokoll.
- `Monitor_RepeatedPolls_CreateNoSecondPreparation` — Snapshot-/EventKey-Dedup über Folgezyklen.
- `Monitor_GlobalSwitchDisabled_CreatesNothing` — Ohne `scm.issueaufbereitung.enabled`
  entsteht nichts, auch kein Baseline-Checkpoint.
- `Monitor_RepositoryFlagDisabled_CreatesNothing` — Ohne `IssueAufbereitungAktiv` wird nichts
  verbraucht.
- `Monitor_RepositoryWithoutEventCapablePlugin_CreatesNothing` — Plugin ohne `IScmEventSource`
  wird nie enumeriert.
- `Monitor_IssueCreatedBeforeBaseline_CreatesNothing` — `CreatedAt` vor Baseline ⇒ Altbestand.
- `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` — Beschreibungsänderung (inkl.
  eigenem Marker) ist keine Erstbeobachtung ⇒ keine Aufbereitungs-Schleife.
- `Monitor_TriggerOnPreparedIssue_ResolvesToCanonicalTask` — Trigger-Kommentar löst parallel
  die kanonische `…/Issue/{n}`-Aufgabe auf; `Zweck`-Aufgabe ist weder Treffer noch
  Mehrdeutigkeitsbeitrag.
- `Monitor_InternalEvent_ExplicitDispatch_WaitsWithHonestReason` — Expliziter Dispatch ohne
  ausführbare CLI bleibt ehrlich wartend.

Private Testhelfer: `EnableAsync` (setzt `ScmIssueAufbereitungEnabledKey`),
`AddRepositoryAsync` (legt Projekt + `GitRepository` mit `IssueAufbereitungAktiv` an),
`ConfigureEnumeration`, `ConfigureDescriptions`, `DescriptionObservation`.

### `ScmEventDispatchTests_IssueAufbereitung`

Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_IssueAufbereitung.cs`

Dedizierter Ausführungspfad der Aufbereitung (11 Testmethoden); legt die Artefakte direkt an
(Hilfsmethode `AddAufbereitungAsync`) statt über die Erkennung.

- `Dispatch_Aufbereitung_DeliversAnalysisPromptAndAppliesResult` — Happy Path: Analyse-Prompt
  mit Qualitätskriterien, `IssueBodyUpdate` + Dokumentations-Kommentar remote.
- `Dispatch_Aufbereitung_KommentarDeaktiviert_SkipptKommentar` —
  `scm.issueaufbereitung.comment.enabled = false` verwirft den Kommentar hostseitig.
- `Dispatch_Aufbereitung_KommentarFehlt_Ungeklärt` — Fehlender Kommentar bei aktivem Schalter
  ⇒ `VerarbeitungUngeklärt`.
- `Dispatch_Aufbereitung_KeinPlugin_WartetMitGrund` — Kein ausführbares Plugin ⇒ `Wartend` mit Grund.
- `Dispatch_Aufbereitung_DeaktiviertesPlugin_WartetMitGrund` — Konfiguriertes, nicht vorhandenes Plugin.
- `Dispatch_Aufbereitung_GlobalesLimitErreicht_WartetMitGrund` — Ausführungsbegrenzung greift.
- `Dispatch_Aufbereitung_FunktionDeaktiviert_WartetMitGrund` — Deaktivierter Funktionsschalter.
- `Dispatch_Aufbereitung_RepositoryNichtFreigeschaltet_WartetMitGrund` — Abgewähltes
  `IssueAufbereitungAktiv`.
- `Dispatch_Aufbereitung_IssueGeschlossen_VerarbeitetOhneÄnderung` — Geschlossenes Issue ⇒
  sauberer Abschluss ohne Remote-Änderung.
- `Dispatch_Aufbereitung_RückschreibungFehler_UngeklärtOhneFehlerkommentar` — Remote-Fehler ⇒
  `VerarbeitungUngeklärt` + Protokolleintrag, kein Fehlerkommentar.
- `Dispatch_Aufbereitung_NoRemoteAction_VerarbeitetOhneSchreiben` — Begründeter Abschluss ohne
  Remoteaktion.

Private Testhelfer: `EnableAsync`, `AddAufbereitungAsync(fixture, kiPluginPrefix,
issueAufbereitungAktiv)` — legt `Zweck`-Aufgabe + `IssueReferenz` + internes `ScmIncomingEvent`
direkt an.

### `AufgabeServiceTests_IssueEvents`

Datei: `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests_IssueEvents.cs`

- `CreateFromIssueEventAsync_ShouldPersistSourceKey` — SourceKey wird persistiert.
- `CreateFromIssueEventAsync_ShouldReuseExisting_WhenSourceKeyExists` — Dedup über SourceKey.
- `CreateFromIssueEventAsync_ShouldCreateSeparateTask_ForDifferentSourceKey` — Getrennte
  Aufgaben je SourceKey.

### `ScmTaskCompletionMonitoringServiceTests`

Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs`

14 Testmethoden für den dritten `PeriodicBackgroundService`; für die Anforderung besonders
relevante Muster: `Monitor_Disabled_SkipsObservation` (Laufzeit-Schalter) und
`CompletionService_IssueCheck_UsesBenutzerkontoContext` (Konto-Kontext der Remote-Prüfung).

### `ScmEventMonitoringServiceTests_Notifications`

Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Notifications.cs`

8 Testmethoden zum `SeparatesKonto`-Inbox-Pfad; u. a.
`Monitor_SeparatesKonto_MentionNotification_EntersInboxViaResolver` (Erwähnungs-Reaktion
bleibt modusgebunden), `Monitor_SeparatesKonto_InboxFailure_KeepsWatermarkForRetry` und
`Monitor_ModeSwitch_BackToEnumeration` (Moduswechsel `SeparatesKonto` → Enumeration).

### `ScmEventQueueViewModelTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventQueueViewModelTests.cs`

14 Testmethoden; für die Anforderung relevant:
`RetrySourcesCommand_MonitoringDisabled_StillRunsBackmergeCycle` — Präzedenz, dass „Quellen
erneut prüfen“ einen zusätzlichen, vom Monitoring-Schalter unabhängigen Zyklus anstößt; außerdem
`SourceAuthenticationFailure_IsVisibleWithoutEvents_AndClearsAfterRecovery` und
`UnassignedSources_NeitherBannerNorDiagnostics_RealErrorsStillAlarm` (Diagnose-Anzeige der
`ScmObservationCheckpoint`-Fehler).

### `PeriodicBackgroundServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/PeriodicBackgroundServiceTests.cs`

- `ExecuteAsync_ShouldNotThrow_WhenTokenIsCanceledDuringDelay` — Shutdown während des
  Polling-Delays (mit `FakeTimeProvider` und lokaler Testableitung
  `TestPeriodicBackgroundService`, die `RunOnceAsync` injizierbar macht).

## Hilfsmethoden

### `ScmDispatchFixture`

Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (Namespace
`Softwareschmiede.TestSupport`)

SQLite-In-Memory-Fixture mit vollständig gemocktem SCM-/KI-Plugin und Terminal-Boundary.

- Konstruktor `ScmDispatchFixture(string? root, TimeProvider?, ScmKiProfile?, ScmKiProfile?)` —
  richtet Moq-Setups für `IGitPlugin` als `IScmEventSource` (Standard-Returns für
  `GetEventRepositoryAsync`, `ReadEventsAsync`, `EnumerateReferencesAsync`, `ReadIssueAsync`,
  `ReadPullRequestAsync`), `IScmEventResponder`, `IScmAgentAccess` und
  `IScmNotificationSource` ein; `IPluginManager`-Mock liefert `Git.Object`.
- DI-Registrierungen (Z. 150-196): u. a. `AddSingleton<ScmEventMonitoringService>`,
  `AddSingleton<ScmBackmergeMonitoringService>`,
  `AddSingleton<ScmTaskCompletionMonitoringService>`, `AddScoped<ScmIssueAufbereitungsService>`,
  `AddScoped<ScmBackmergeService>`, `Options.Create(new ScmEventOptions())` — **die
  Registrierungsstelle für den neuen Dienst**.
- `InitializeAsync` — `EnsureCreated` + `ScmEventStartupGate.Ready()`.
- `Repository` / `BotRepository` — Standard-`ScmEventRepository` für `Benutzerkonto` /
  `SeparatesKonto`-Auflösung.
- `AddAsync(…)` — legt Projekt/Repository/Aufgabe + Ereignis über `AcceptBatchAsync` an.
- `AddNotification(ScmNotification)` — registriert Inbox-Einträge.
- `ConfigureIssue(int? number)` — `ReadIssueAsync` liefert Standard-`Issue`.
- `ConfigureMentionObservations(authorId, authorLogin)` — `ReadEventsAsync` liefert
  Mention-Observation für `IssueComment`.
- `Monitor` — `ScmEventMonitoringService` aus dem Provider.
- `SetKontoModusAsync(ScmKontoModus)` / `SetSettingAsync(schluessel, wert)` — Einstellungen
  über `AppEinstellungService`.
- `DispatchAsync(Guid id)` / `EventAsync(Guid id)` — expliziter Dispatch / Ereignis lesen.
- Öffentliche Inspectoren: `Sessions`, `StartSpecs`, `Prompts`, `Responses`, `Notifications`,
  `Git`/`Ki` (Moq für Verify), `Cli`, `Ausloeser`, `StartPruefung`.
