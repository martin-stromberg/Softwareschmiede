# Logik

## Hintergrunddienst-Muster

## `PeriodicBackgroundService`

Datei: `src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PeriodicBackgroundService(TimeSpan, TimeProvider, ILogger, string)` | protected ctor | Basisklasse: Polling-Intervall, `TimeProvider` (testbar), Fehler-Logmeldung |
| `ExecuteAsync(CancellationToken)` | protected override | Schleife: `RunOnceAsync` → Fehler fangen/logging → `Task.Delay(_pollingInterval, _timeProvider)` |
| `RunOnceAsync(CancellationToken)` | public abstract | Ein einzelner Durchlauf; direkt aus Tests aufrufbar |

Bestehende Ableitungen (alle als `AddSingleton` + `AddHostedService` registriert, `App.xaml.cs` Zeilen 243–250, 319–326):

| Dienst | Intervall | Aufgabe |
|--------|-----------|---------|
| `ScmEventMonitoringService` | `ScmEventOptions.PollInterval` (Default 60 s) | SCM-Quellen beobachten, Ereignisse in Inbox aufnehmen |
| `ScmEventExchangeService` | 2 s (fest) | Lokale Annahmen/Aktionen/Abschlüsse unabhängig vom Remote-Poll abgleichen |
| `ScmBackmergeMonitoringService` | `ScmEventOptions.PollInterval` | Backmerge-Prüfung |
| `ScmIssuePreparationMonitoringService` | `ScmEventOptions.PollInterval` | Issue-Aufbereitung |
| `ScmTaskCompletionMonitoringService` | `ScmEventOptions.PollInterval` | SCM-Abschlussüberwachung |
| `PullRequestMonitoringService` | 2 min (fest) | PR-Status/Checks pollen, optionale Auto-Completion |

Nicht-periodisch, aber relevanter Hosted Service: `ScmStartCheckService` (ereignisgesteuert über `ScmStartCheckTrigger`, `App.xaml.cs` Zeilen 324–326).

## `ScmEventOptions` / `ScmEventChangeNotifier` / `ScmEventStartupGate`

Datei: `src/Softwareschmiede/Application/Services/ScmEventOptions.cs`

| Typ/Methode | Sichtbarkeit | Kurzbeschreibung |
|-------------|-------------|------------------|
| `ScmEventOptions.PollInterval` / `AcceptanceTimeout` / `CompletionTimeout` | public | 60 s / 2 min / 30 min Defaults; Konfiguration über `ScmEvents`-Sektion |
| `ScmEventChangeNotifier.Changed` / `Notify()` | public | Singleton-Event „Einträge oder Integrationsdiagnosen haben sich geändert" — abonnierbares Change-Signal (u. a. vom `ScmEventQueueViewModel` genutzt) |
| `ScmEventStartupGate.Ready()` / `WaitAsync(ct)` | public | Hält Dienste bis nach `MigrateAsync` an; `Ready()` wird in `App.xaml.cs` Zeile 115 aufgerufen |

## SCM-Eingangskanal (für Kommentar-Aktionen der Status-Seite)

## `ScmEventMonitoringService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync(ct)` | public override | Wartet auf `ScmEventStartupGate`, prüft `GetScmMonitoringEnabledAsync`, lädt `WorkItems` (aktive Repos, `AsNoTracking`, inkl. `GitRepository`/`IssueReference`/`PullRequests`) und aktive `GitRepositories`; pro SCM-Plugin mit `IScmEventSource`: Alias-Gruppen bilden, Repository-Auflösung mit Checkpoint-Backoff, repository-weite Enumeration bzw. Inbox-Modus (`SeparateAccount` + `IScmNotificationSource`), je Referenz×`ScmContentKind` Quellen lesen und an `ScmEventStore.AcceptBatchAsync` übergeben |
| `BuildAliasGroups` | private | Kanonische Alias-Gruppierung (HTTPS/SSH/owner-name-Formen → eine Gruppe/Checkpoint) |
| `EnumerateNotificationsAsync` | private | Inbox-Enumeration im Modus `SeparateAccount` |

Read-only-Projektionsmuster für einen Snapshot-Dienst: `IServiceScopeFactory` + `CreateScope()` + `AsNoTracking()`-Abfragen (`RunOnceAsync` Zeilen ~44–67).

## `ScmEventStore`

Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AcceptBatchAsync(workItemId, sourceKey, batch, unassigned, triggerKeyword, delayKeyword, rejectionOverride, ct)` | public | Nimmt Beobachtungen dedupliziert (ContentSnapshots) als `ScmIncomingEvent` auf; wendet `ScmEventValidationService.GetRejection` an; ruft `ScmEventChangeNotifier.Notify()` |
| `Observation(ScmIncomingEvent)` | public static | Deserialisiert `ObservationJson` zur `ScmEventObservation` |
| `ActionId(eventId, purpose)` | public static | Deterministische Outbox-Aktions-ID |
| `RetryAt(failures, now, retryAfter)` | public static | Backoff-Berechnung |
| `ClaimTaskAsync` / `RenewTaskAsync` / `ReleaseTaskAsync` | public | Aufgaben-Lease (`ScmTaskLease`) beanspruchen/verlängern/freigeben |
| Konstanten `AmbiguousAssignmentDiagnostic`, `UnassignedDiagnostic`, `EmptyDescriptionBatchDiagnostic`, `InboxDiscardDiagnosticPrefix` | public | Persistierte Diagnosewerte der Inbox/`ScmContentSnapshot.Diagnostic` |

## `ScmEventOrchestrator`

Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` (1176 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchDueEventsAsync(ct)` | public | Scannt fällige `ScmIncomingEvents` (Sequenz-Batches à 100) inkl. offener Outbox-Aktionen; priorisiert via `ScmDispatchPriority.OrderForDispatch`; dispatched je Ereignis unter `ScmTaskCoordinator.Shared`-Aufgabensperre |
| `DispatchAsync(eventId, explicitPluginPrefix, ct)` | public | Einzel-Dispatch unter Aufgabenlease |
| `OpenFromQueueAsync(eventId, select, ct)` | public | Manuelle Queue-Auswahl aus der Ereignisübersicht (UI) mit eigenem Lease-Ablauf |
| `DispatchCoreAsync` | private | Kern-Dispatch: Lease anlegen (`owner = Guid.NewGuid().ToString("N")`, Z. ~133 — **flüchtig, kein Instanzkonzept**), Hostaktionen (`receipt`-Reaktion, `waiting`-Kommentar mit `<!-- softwareschmiede:event:{id}:waiting -->`-Marker), Zustellung über `IScmPromptSessionAdapter`/CLI-Start, Revalidierung gegen aktuellen Repository-Stand |
| `ReconcileAttemptAsync` | private | Abgleich begonnener Versuche (Annahme/Abschluss über Antwortkanal) |
| `DispatchPreparationAsync` | private | Dedizierter Pfad für `Purpose == "IssueAufbereitung"` |
| `BlockReason(WorkItem)` | private | `"Aufgabe beendet/archiviert; Start gesperrt."` bzw. `"Aufgabe pausiert; Start gesperrt."` |
| `WaitAsync` / `WaitWithCommentAsync` | private | Status → `Wartend` + `Reason`; Letztere legt zusätzlich eine `waiting`-Kommentar-Outbox-Aktion an |
| `EnsureHostActionAsync` | private | Idempotente Host-Outbox-Aktionen mit Kollisionserkennung |
| `RenewLeaseAsync` | private | Lease-Verlängerung alle 15 s via `PeriodicTimer` |

Persistierte Wartegründe in `ScmIncomingEvent.Reason` (Auswahl, Zeilen ~196–332): „Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden.", „Zugehörige Aufgabe wurde entfernt.", „Aufgabe pausiert; Start gesperrt.", „Schreibberechtigung für den Ereignisursprung fehlt. …", „CLI beschäftigt; wartet auf Eingabebereitschaft.", `ScmCliWaitReason.Describe(recognition)` (Trust-/Versions-/Kanalgründe), `admission.WaitReason` (Ausführungslimits), „SCM-Anbieter ist nicht verfügbar." u. a.

## `ScmEventValidationService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRejection(observation, triggerKeyword)` | public static | Zentrale Akteurs-/Trigger-Prüfung: Trigger-Keyword (Default `$Softwareschmiede`) bzw. freistehende Erwähnung des separaten Kontos; `ViewerHasReacted`-Duplikatcheck; **Akteur (Autor/Editor) muss Repository-Owner oder `TrustedAutomationLogins` (`github-actions[bot]`, `app/github-actions`) sein** (Zeilen 71–80); eigene Antworten (`<!-- softwareschmiede:`) werden verworfen |
| `GetAssignRejection` | private | Assignee-Auslöser: Auftragsinhalt + Zuweisender im erlaubten Kreis (Owner, überwachtes Konto, Trusted-Automation) |
| `NormalizeTriggerKeyword` | public static | Fallback auf `DefaultTriggerKeyword` |
| Rejection-Konstanten | public | `NoMentionRejection`, `AlreadyReactedRejection`, `AssignEmptyDescriptionRejection`, `AssignerUnknownRejection`, `AssignerNotAllowedRejection` |

→ Kritische Abhängigkeit der Anforderung: Ein vom Status-Server unter Bot-/GitHub-App-Identität geschriebener Kommentar besteht diese Validierung nur, wenn die Identität dem Owner-Kreis bzw. `TrustedAutomationLogins` entspricht oder der Vertrag erweitert wird.

## `ScmEventOutbox` / `ScmEventExchangeService`

Dateien: `src/Softwareschmiede/Application/Services/ScmEventOutbox.cs`, `src/Softwareschmiede/Application/Services/ScmEventExchangeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScmEventOutbox.EnsureAsync(item, purpose, request, ct)` | public | Idempotent angelegte `ScmOutgoingAction` (Kollision → `ScmActionIdCollisionException`) |
| `ScmEventOutbox.ExecuteAsync(action, responder, ct)` | public | Führt Aktion über `IScmEventResponder.ExecuteResponseAsync` aus, persistiert Ergebnis |
| `ScmEventExchangeService.RunOnceAsync(ct)` | public override | Alle 2 s: offene, begonnene Ereignisse erneut dispatchen (Antwortkanal-Abgleich) |

## `ScmCliWaitReason` / `ScmCliWorkspaceTrust` / `ScmCliAccessEvaluator`

Dateien: `src/Softwareschmiede/Application/Services/ScmCliWaitReason.cs`, `ScmCliWorkspaceTrust.cs`, `ScmCliAccessEvaluator.cs`

- `ScmCliWaitReason` (static): einzige Quelle der CLI-Warte-/Kanalzugriffs-Grundtexte; `Describe(ScmCliRecognition)` liefert ursachenehrliche Texte (unsupported/unknown CLI, fehlendes Banner, Kanalgrund); `IsConfirmable(reason)` markiert manuell bestätigbare Gründe (Suffix `ConfirmableMarker` = „Für die angezeigte CLI-Sitzung bestätigen."). Diese Texte stehen persistiert in `ScmIncomingEvent.Reason`.
- `ScmCliWorkspaceTrust` (static): `EnsureTrusted(pluginPrefix, spec)` — vorbelegte Workspace-Trust-Entscheidung je CLI (`~/.claude.json`, Codex `config.toml`).
- `ScmCliAccessEvaluator`: prüft eingeschränkte Schreibzugriffe des Antwortkanals (`ExchangeRestrictedWriteSidsJson`).

## Einstellungen & Geheimnisse

## `AppSettingService`

Datei: `src/Softwareschmiede/Application/Services/AppSettingService.cs` (scoped, EF `AppSetting`-Tabelle)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSettingAsync(key)` / `GetIntSettingAsync` / `GetBoolSettingAsync` | public | Generische Leser; `null` bei fehlendem Wert |
| `SetSettingAsync` / `SetIntSettingAsync` / `SetBoolSettingAsync` | public | Upsert (legt `AppSetting` bei Bedarf an) |
| `GetSettingsAsync(keys)` | public | Multi-Key-Lesen |
| `GetScmMonitoringEnabledAsync` | public | Laufzeit-Schalter `scm.monitoring.enabled` (Default `true`) — Muster für einen `status.remote.enabled`-Schalter |
| `GetScmAccountModeAsync` / `SetScmAccountModeAsync` | public | Enum-persistierter Wert mit Validierung — Muster für Modus-Einstellungen |
| `GetScmTriggerKeywordAsync` / `GetScmDelayKeywordAsync` / `GetScmPromptAppendicesAsync` | public | SCM-Texteinstellungen |
| `GetScmTaskCompletionEnabledAsync` / `GetScmTaskCompletionWaitMinutesAsync` / `GetScmTaskCompletionCheckModeAsync` | public | Abschlussüberwachung (Defaults, geklemmte Werte) |
| `GetUpdateSettingsAsync` / `SetUpdateSettingsAsync` | public | Zusammengesetztes Settings-Objekt (UpdateMode/UpdatePath) — Muster für gruppierte Einstellungen |
| `GetWindowGeometryAsync` / `SetWindowGeometryAsync` | public | Fenster-Geometrie |
| `GetAutoShutdownForceAsync` / `GetAutoShutdownToleranceMinutesAsync` u. a. | public | Auto-Shutdown-Nebenwerte |
| Schlüssel-Konstanten (`*Key`, `BackmergeProjectKeyPrefix`, `UpdateSettingsReadTag`) | public const | Namenskonvention `bereich.unterbereich.name`; dynamische Keys via `BackmergeProjectKey(projectId)` |

→ Neue `status.remote.*`-Schlüssel folgen dem Muster `public const string XKey` + typisierter `Get/SetXxxAsync`. Es existiert **kein** Instanz-ID-Schlüssel/-Konzept.

## `WindowsCredentialStore` (`ICredentialStore`)

Datei: `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetCredential(target)` | public | Windows Credential Manager (`CredReadW`), `null` wenn nicht vorhanden |
| `SetCredential(target, value)` | public | `CredWriteW` (`CRED_TYPE_GENERIC`, persist local machine); wirft bei Fehler |
| `DeleteCredential(target)` | public | `CredDeleteW` |

→ Singleton in DI (`App.xaml.cs` Z. 378–379); in E2E-Umgebung durch `ScmE2ECredentialStore` ersetzt (Z. 407–419). Relay-Token-Ablage hier vorgesehen.

## Aufgaben- & Laufzeitstatus

## `WorkItemService` (`IActiveWorkItemsService`)

Datei: `src/Softwareschmiede/Application/Services/WorkItemService.cs` (1254 Zeilen, scoped)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetActiveWorkItemsAsync(ct)` | public | Aktive Aufgaben für Seitenleiste/Kandidatenmenge |
| `GetActiveAndWaitingCountAsync(ct)` | public | `(Active, Waiting)`-Zählung |
| `GetByProjectAsync` / `GetArchivedByProjectAsync` / `GetByIdAsync` / `GetDetailAsync` | public | Lesezugriffe |
| `CreateAsync` / `CreateFromIssueAsync` / `CreateFromIssueEventAsync` / `CreateFromPullRequestAsync` / `CreateFromAlertAsync` | public | Anlagepfade inkl. SCM-Ereignis-Anlage |
| `SetStatusAsync` / `SetStatusUncheckedAsync` | public | Statuswechsel mit/ohne Transitions-Validierung |
| `SetActiveRunAsync` / `EndActiveRunAsync` / `EnsureActiveRunAsync` | public | `ActiveRunId`/`RunStatus`/Heartbeat-Verwaltung |
| `SetPauseAsync` / `SavePromptSuggestionAsync` / `ClearPromptSuggestionAsync` | public | `PausedUntilUtc` / Prompt-Vorschlag |
| `GetHeartbeatAgeMinutesAsync` / `CompleteAsync` / `CanCompleteTaskAsync` / `ArchiveAsync` / `DiscardAsync` / `DeleteAsync` | public | Weitere Lebenszyklus-/Prüfmethoden |

## `AiExecutionService` (Singleton; `IRunningAutomationStatusSource`, `ICliExecutionLimitSource`)

Datei: `src/Softwareschmiede/Application/Services/AiExecutionService.cs`

| Methode/Event | Sichtbarkeit | Kurzbeschreibung |
|---------------|-------------|------------------|
| `IsRunning(workItemId)` / `GetRunningCount()` / `GetRunningWorkItemIds()` | public | Laufende CLI-Prozesse |
| `GetLiveRunStatus(workItemId)` | public | Live-Substatus (`Running`/`WaitingForInput`/`null`) — reichere Laufzeitquelle als der persistierte `WorkItem.RunStatus` |
| `GetScmPromptSession(workItemId)` / `GetTerminalSession(workItemId)` | public | SCM-Promptsitzungs-Adapter / Terminalsitzung |
| `StartCliAsync` / `StartTerminalSessionAsync` / `StopCliAsync` | public | CLI-Lebenszyklus |
| `GetLastExitCode(workItemId)` / `GetRunningProcess` / `GetCliRecording` | public | Exit-Code, Prozess, Ausgabe-Recording |
| `UpdateHeartbeat(workItemId)` / `NotifyRunActive(workItemId)` | public | Heartbeat-/Aktivitätsmarken |
| `CliProcessStatusChanged` / `CliRunActive` / `RunningCountChanged` | public events | Laufzeit-Events (vom `CliProcessManager`/`AutoShutdownOrchestrator` abonniert) |

## `CliProcessManager` (Singleton)

Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartHeartbeat(workItemId)` / `StopHeartbeat(workItemId)` | public | Pro Aufgabe ein 30-s-`Timer` (`HeartbeatInterval`), der `LastHeartbeatUtc`/`RunStatus` persistiert und `WorkItemRunDataChangedNotifier.NotifyRunDataChanged` aufruft |
| `OnCliProcessStatusChanged` / `OnCliRunActive` / `ReconcileTickAsync` | private | Reagiert auf `AiExecutionService`-Events; 30-s-Reconcile-Timer gleicht persistierte Laufdaten mit Handles ab |

Abonnierte Events: `AiExecutionService.CliProcessStatusChanged`, `AiExecutionService.CliRunActive`.
Publizierte Signale: `WorkItemRunDataChangedNotifier.RunDataChanged`.

## `WorkItemRecoveryService` / `WorkItemRunActivity`

Dateien: `src/Softwareschmiede/Application/Services/WorkItemRecoveryService.cs`, `WorkItemRunActivity.cs`

| Element | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `HeartbeatTimeoutMinutes` | public const = 5 | Schwelle „Heartbeat abgelaufen" — Referenzwert für die serverseitige Veraltungslogik |
| `ScanForRecoveryCandidatesAsync` | public | Recovery-Kandidaten: aktiver Status + `ExecutionStatus.Active` + Heartbeat älter 5 min + nicht pausiert + kein laufender Prozess |
| `RecoverManuallyAsync` | public | Manuelle Recovery mit Concurrency-Token (`RecoveryVersion`) und `LogEntry`-Audit |
| `WorkItemRunActivity.IsActive(activeRunId, lastHeartbeatUtc, nowUtc)` | public static | Einheitliche „läuft aktiv"-Regel: `ActiveRunId` gesetzt und Heartbeat jünger als 5 min |

## `RunningAutomationsCount` (`IRunningAutomationsCount`, Singleton)

Datei: `src/Softwareschmiede/Application/Services/RunningAutomationsCount.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CountAsync(ct)` | public | Zählt als `AiExecutionState.Running` klassifizierte aktive Aufgaben (via `AiExecutionStatusClassification` auf `IActiveWorkItemsService`-Kandidaten, Live-Status + zeitgesteuerte Prompts einbezogen) plus `ScmIncomingEvents` im Status `Verzögert` |

## `WorkItemRunDataChangedNotifier` / `AiWorkItemsNotificationHub` / `ScmTaskCoordinator`

Dateien: `WorkItemRunDataChangedNotifier.cs`, `AiWorkItemsNotificationHub.cs`, `ScmTaskCoordinator.cs`

- `WorkItemRunDataChangedNotifier` (Singleton): `event Action<Guid> RunDataChanged` + `NotifyRunDataChanged(workItemId)` — Change-Signal für persistierte Aufgaben-Laufdaten (potenzieller Snapshot-Trigger).
- `AiWorkItemsNotificationHub` (Singleton): in-memory `Subscribe`/`PublishAsync` für `AiWorkItemCompletionEvent` — reines In-Process-Pub/Sub, **kein** Netzwerkkanal.
- `ScmTaskCoordinator` (Singleton, `Shared`): pro-Aufgabe serielle Ausführungssperre (`RunAsync`).

## `ScmDispatchPriority`

Datei: `src/Softwareschmiede/Application/Services/ScmDispatchPriority.cs`

Statische Dispatch-Reihenfolge: Fortsetzungsereignisse → fällige Verzögerungen (zeitaufsteigend) → Eingangsreihenfolge; `EventSelection`/`WorkItemState`-Records.

## Update / Version / GitHub-Release

## `ApplicationVersionProvider` (`IApplicationVersionProvider`, Singleton)

Datei: `src/Softwareschmiede/Application/Services/Updates/ApplicationVersionProvider.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetInstalledVersionAsync(ct)` | public | Liest `version.json` aus `AppContext.BaseDirectory` → `InstalledVersionInfo` (normalisierte Version, TagName, Commit, CreatedAtUtc); `null` bei fehlender/ungültiger Datei |

## `UpdateService` (`IUpdateService`, Singleton)

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckForUpdateAsync(options, ct)` | public | Installierte Version vs. `IUpdateReleaseClient.GetLatestReleaseAsync`; Kanalprüfung (Prerelease/RC), `UpdateVersionComparer.IsNewer`; serialisiert durch `_checkGate`-Semaphore |
| `PrepareUpdateAsync` / `StartPreparedUpdateAsync` | public | Paket-Vorbereitung und Start des externen Updaters + Shutdown |

## `GitHubReleaseClient` (`IUpdateReleaseClient`, Singleton)

Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetLatestReleaseAsync(options, ct)` | public | GitHub REST `/releases?per_page=100` mit Pagination, `UpdateCheckOptions.CheckTimeout`; kein Token (öffentliches Repo); Referenz für serverseitige GitHub-API-Nutzung (User-Agent/`Accept`-Header, Fehler → `UpdateReleaseLookupResult.Failed` statt Exception) |

Weitere Update-Bausteine: `UpdateVersionComparer`, `SemanticUpdateVersion`, `UpdatePathChannelResolver` (Kanal aus Tag-Name), `UpdateModels` (`UpdateInfo`, `UpdateCheckOptions`, `UpdateChannel`, Ergebnis-Records), `CliUpdateSafetyService`, `UpdatePackageService`, `UpdateScriptService`, `UpdateProcessLauncher`.

## Logging / Diagnose

## `LogService`

Datei: `src/Softwareschmiede/Application/Services/LogService.cs` (scoped)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddEntryAsync(workItemId, type, content, …)` | public | Aufgabengebundener `LogEntry` (u. a. `SystemMessage`, `GitAction`, `StatusTransition`) — vom Orchestrator für Wartegrund-Protokoll genutzt |

App-weite Diagnose: Serilog, Tages-Rolling-Datei `logs/softwareschmiede-*.log` neben der Exe (`App.xaml.cs` Z. 42–52).

## UI-Schicht (relevant für Einstellungs-/Status-Oberfläche)

## `SettingsViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` (1340 Zeilen, transient)

- Abschnitte u. a.: Update-Modus/-Pfad, SCM-Monitoring (`IsScmMonitoringEnabled` → `scm.monitoring.enabled`), Kontomodus, Trigger-/Verzögerungs-Keywords, Abschlussüberwachung, Backmerge, Issue-Aufbereitung, Plugin-Aktivierung/-Einstellungen, IDE-Plugins, Design-Modus.
- Bindemuster: Eigenschaften mit `SetProperty`, Status-Texte („aktiviert"/„deaktiviert"), `SaveAsync`-Persistenz via `AppSettingService`; Automation-IDs/Views getestet durch `SettingsViewModelTests*` und `E2E_SettingsFeatureFlags`.

## `ScmEventQueueViewModel` / `ScmEventQueueView`

Dateien: `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`, `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml`

- Abonniert `ScmEventChangeNotifier.Changed`; lädt `ScmIncomingEvents` + `ScmOutgoingActions` (`AsNoTracking`) als Inbox-/Diagnose-Ansicht mit Aktionen „Erneut prüfen"/„Öffnen"/„Abbrechen" — die appseitige Sicht auf genau die Warteschlange, die der Snapshot-Server zusätzlich extern zeigen soll.

## DI-Registrierung

Datei: `src/Softwareschmiede.App/App.xaml.cs` (`ConfigureServices`, Z. 178–455)

- `SoftwareschmiedeDbContext` scoped (SQLite, `DatabasePathResolver`), Migration beim Start (`StartupAsync` Z. 94–101), danach `ScmEventStartupGate.Ready()` (Z. 115).
- Hosted-Service-Muster (Z. 243–250, 319–326): `services.AddSingleton<T>(); services.AddHostedService(sp => sp.GetRequiredService<T>());` — Vorlage für einen `RemoteStatusRelayService`.
- `TimeProvider.System` als Singleton (Z. 317) — Voraussetzung der `PeriodicBackgroundService`-Testbarkeit.
- `HttpClient`-Singleton (Z. 354–356) für `GitHubReleaseClient` u. a.
- Singletons für Laufzeitstatus: `AiExecutionService`, `CliProcessManager`, `TerminalSessionService`, `RunningAutomationsCount`, `WorkItemRunDataChangedNotifier`, `ScmEventChangeNotifier`, `ScmTaskCoordinator.Shared`, `PromptScheduleDispatchService`.
- E2E-Ersetzungen: `ScmE2ETestConfiguration` ersetzt `ICredentialStore`, `IPluginManager`, `ITerminalSessionFactory` (Z. 407–419); `UpdateE2ETestConfiguration` ersetzt Update-Grenzen (Z. 427–454).
- Kein SignalR-/ASP.NET-Core-/WebSocket-Paket in keinem Projekt (siehe `*.csproj`); ein Relay-Client brächte eine neue NuGet-Abhängigkeit.
