# Tasks: Issue-Aufbereitung im Überwachungsmodus „Separates Konto“

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `ScmIssueAufbereitungsService`: statische Methode `AuswertungsCheckpointKey(ScmEventReference)` für den SourceKey `issueaufbereitung-issue/{provider}/{host}/{repoId}/{number}` hinzufügen | Offen | — |
| 2 | Logik | `ErkennenUndEinreihenAsync`: `ScmContentSnapshot`-Erstbeobachtungsprüfung durch Prüfung „`issueaufbereitung-issue/…`-Checkpoint ohne offenen Fehler vorhanden" ersetzen | Offen | — |
| 3 | Logik | `ErkennenUndEinreihenAsync`: nach abgeschlossener Auswertung (Einreihung, Altbestand-Skip, bereits-eingereiht-Skip, `ReadIssueAsync == null`) den `issueaufbereitung-issue/…`-Checkpoint mit Endzustand (`NextAttemptUtc = long.MaxValue`, `ErrorCode = null`) schreiben | Offen | — |
| 4 | Logik | `ScmIssueAufbereitungMonitoringService` als `PeriodicBackgroundService` in `src/Softwareschmiede/Application/Services/` anlegen (Konstruktor-Dependencies analog `ScmBackmergeMonitoringService`) | Offen | — |
| 5 | Logik | `RunOnceAsync`: `ScmEventStartupGate.WaitAsync` + Laufzeit-Schalter `GetScmIssueAufbereitungEnabledAsync` (Early-Return ohne Nebenwirkungen) + `GetScmKontoModusAsync` lesen | Offen | — |
| 6 | Logik | `RunOnceAsync`: `GitRepository` mit `Aktiv && IssueAufbereitungAktiv` laden, Plugins filtern (`IScmEventSource`), Alias-Gruppen über `RepositoryUrl` + `ScmPluginMatcher.MatchesPlugin` bilden | Offen | — |
| 7 | Logik | `RunOnceAsync`: Alias-Auflösung mit Checkpoint `issueaufbereitung-repository/{prefix}/{alias}` unter `ScmKontoModus.Benutzerkonto`, `localRepositories`-Mapping, Fehlerisolation via `RecordFailureAsync(retryPermanent: true)` | Offen | — |
| 8 | Logik | `RunOnceAsync`: `ScmCheckpointSupport.EnumerateRepositoryAsync` mit `checkpointKeyPrefix = "issueaufbereitung-enumeration"` und laufweitem `enumerated`-HashSet aufrufen; im `verarbeite`-Callback nur `ScmReferenceKind.Issue` betrachten | Offen | — |
| 9 | Logik | `verarbeite`-Callback: Baseline-Erstlauf — fehlender `issueaufbereitung/…`-Baseline-Checkpoint ⇒ Baseline anlegen + alle enumerierten Issue-Referenzen als ausgewertet markieren (kein Beschreibungs-Read) | Offen | — |
| 10 | Logik | `verarbeite`-Callback: je Issue-Referenz Auswertungs-Checkpoint prüfen (ausgewertet/Backoff überspringen), Referenz auf konfigurierten `kontoModus` umtaggen, `ReadEventsAsync` für `ScmContentKind.Description` lesen, `ErkennenUndEinreihenAsync` aufrufen | Offen | — |
| 11 | Logik | `verarbeite`-Callback: eigenes try/catch je Issue — Auswertungs-Checkpoint mit Endzustand bei Erfolg bzw. `RecordFailureAsync` (Standard-Backoff) bei Exception; Enumerations-Watermark darf nicht zurückfallen | Offen | — |
| 12 | Logik | `RunOnceAsync`: am Zyklusende `ScmEventOrchestrator.DispatchDueEventsAsync` aufrufen (in eigenem Scope), damit fällige Aufbereitungsereignisse auch bei deaktiviertem `scm.monitoring.enabled` zugestellt werden | Offen | — |
| 13 | Konfiguration | `App.xaml.cs`: `AddSingleton<ScmIssueAufbereitungMonitoringService>` + `AddHostedService` nach dem `ScmBackmergeMonitoringService`-Muster registrieren | Offen | — |
| 14 | UI | `ScmEventQueueViewModel`: neue Dependency `ScmIssueAufbereitungMonitoringService` im Konstruktor; `RetrySourcesCommand` ruft zusätzlich dessen `RunOnceAsync` auf (Backmerge-Präzedenz) | Offen | — |
| 15 | Tests | `ScmDispatchFixture`: `AddSingleton<ScmIssueAufbereitungMonitoringService>` ergänzen | Offen | — |
| 16 | Tests | Neue Testdatei `ScmIssueAufbereitungMonitoringServiceTests` mit privaten Hilfsmethoden (`AufbereitungMonitor`, `EnableAsync`, `AddRepositoryAsync`, `ConfigureEnumeration`, `ConfigureDescriptions`, `AddSnapshotAsync`) anlegen | Offen | — |
| 17 | Tests | Test `AufbereitungMonitor_SeparatesKonto_NewIssue_CreatesPreparationTaskAndInternalEvent` (Happy Path, Akzeptanzkriterium a) | Offen | — |
| 18 | Tests | Test `AufbereitungMonitor_FirstRun_SetsBaselineAndMarksAllReferencesEvaluated` (Erstlauf ohne Retro-Scan, `ReadEventsAsync` nie) | Offen | — |
| 19 | Tests | Test `AufbereitungMonitor_RepeatedCycle_NoDescriptionReread` (Checkpoint-basierte Überspringung, Rate-Limit-Schonung) | Offen | — |
| 20 | Tests | Test `AufbereitungMonitor_Disabled_NoProviderWork` + Test `AufbereitungMonitor_RepositoryFlagDisabled_NoProviderWork` | Offen | — |
| 21 | Tests | Test `AufbereitungMonitor_EventMonitoringDisabled_AufbereitungStillRuns` (Unabhängigkeit von `scm.monitoring.enabled`) | Offen | — |
| 22 | Tests | Test `AufbereitungMonitor_RepositoryResolution_UsesBenutzerkontoContext` + Test `AufbereitungMonitor_SeparatesKonto_EventCarriesMonitoringKonto` (Konto-Auflösung vs. Konto-Tagging) | Offen | — |
| 23 | Tests | Test `AufbereitungMonitor_MentionPrereadIssue_StillPrepared` (Erwähnungs-Verschnitt-Lücke geschlossen — Snapshot existiert, Aufbereitung läuft trotzdem) | Offen | — |
| 24 | Tests | Test `AufbereitungMonitor_ReadFailure_IsolatesSingleIssue` + Test `AufbereitungMonitor_AliasResolutionFailure_IsolatesRepository` (Fehlerisolation auf beiden Checkpoint-Ebenen) | Offen | — |
| 25 | Tests | Test `AufbereitungMonitor_NonIssueReferences_Ignored` + Test `AufbereitungMonitor_EnqueuedEvent_IsDispatchedWithinCycle` | Offen | — |
| 26 | Tests | Test `AufbereitungMonitor_ModeSwitch_NoDuplicateTasks` (Moduswechsel `Benutzerkonto` ↔ `SeparatesKonto`, Akzeptanzkriterium b) | Offen | — |
| 27 | Tests | `ScmEventQueueViewModelTests` an geänderte Konstruktor-Signatur anpassen + neuen Test `RetrySourcesCommand_StillRunsAufbereitungCycle` | Offen | — |
| 28 | Tests | `ScmEventMonitoringServiceTests_IssueAufbereitung` gegen das geänderte Auswertungs-Kriterium verifizieren und ggf. anpassen (insb. `Monitor_RepeatedPolls_CreateNoSecondPreparation`, `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop`) | Offen | — |
| 29 | Tests | Voller Build + stabile Testlane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` sowie Integrationstests durchlaufen lassen | Offen | — |
