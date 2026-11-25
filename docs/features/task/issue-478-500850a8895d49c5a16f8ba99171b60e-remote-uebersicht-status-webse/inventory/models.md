# Datenmodell

Alle Entitäten werden über `SoftwareschmiedeDbContext` (SQLite, EF Core, `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiedeDbContext.cs`) persistiert; Migrationen liegen unter `src/Softwareschmiede/Migrations/` (letzte: `20261122125707_AddWorkItemKind`).

## `WorkItem`

Datei: `src/Softwareschmiede/Domain/Entities/WorkItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjectId` | `Guid` | Zugehöriges Projekt |
| `GitRepositoryId` | `Guid?` | Verknüpftes Git-Repository |
| `Title` | `string` | Titel der Aufgabe |
| `RequirementDescription` | `string?` | Anforderungsbeschreibung für den KI-Agenten |
| `Status` | `WorkItemStatus` | Lebenszyklus-Status (New/Started/Waiting/Completed/Archived) |
| `ExecutionStatus` | `WorkItemExecutionStatus` | Persistierter KI-Ausführungsstatus (NotStarted/Active/Completed) |
| `BranchName` / `LocalClonePath` / `BaseBranchName` | `string?` | Branch-/Klon-Daten; gebündelt als `[NotMapped]` `GitWorkspace` |
| `AgentPackageName` / `AgentName` / `AiPluginPrefix` | `string?` | Verwendeter Agent bzw. KI-Plugin-Prefix |
| `CreatedAt` / `CompletedAt` | `DateTimeOffset` / `DateTimeOffset?` | Erstellung / Abschluss |
| `ActiveRunId` | `string?` | Aktive Lauf-ID einer KI-Ausführung (gesetzt ⇒ CLI-Lauf aktiv) |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat einer Ausführung (Aktivitätsregel: `WorkItemRunActivity`, Schwelle `WorkItemRecoveryService.HeartbeatTimeoutMinutes = 5` min) |
| `LastCliStartUtc` | `DateTimeOffset?` | Letzter echter CLI-Prozessstart |
| `RunStatus` | `WorkItemRunStatus?` | Laufzeit-Substatus `Running`/`WaitingForInput` — fachliche Basis für „wartet auf Benutzereingabe"; wird von `CliProcessManager` aus `PseudoConsoleSession.RuntimeStatusChanged` aktualisiert |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-relevante Statusänderungen |
| `SuggestedPrompt` / `SuggestedExecuteAfterUtc` | `string?` / `DateTimeOffset?` | Persistierter Prompt-Vorschlag + geplanter Ausführungszeitpunkt |
| `PausedUntilUtc` | `DateTimeOffset?` | Bis zu diesem UTC-Zeitpunkt pausiert (blockiert Starts/Recovery/zeitgesteuerte Prompts) |
| `ScmCompletionDetectedUtc` | `DateTimeOffset?` | Erster beobachteter Schluss/Merge der SCM-Referenz (Abschluss-Wartezeit) |
| `ScmCompletionReference` | `string?` | Anzeigetext der beobachteten SCM-Referenz (z. B. „Pull Request #7") |
| `Purpose` | `string?` | Zweck-Markierung; `null` = reguläre Arbeitsaufgabe, `"IssueAufbereitung"` = Aufbereitungs-Aufgabe (von kanonischer Ereignisauflösung ausgeschlossen) |
| `Kind` | `WorkItemKind` | Fachlicher Ursprung (Manual/Instruction/PullRequest/Alert/IssuePreparation/Unspecified) |
| `Project` / `GitRepository` / `IssueReference` / `AlertReference` | Navigationen | Übergeordnetes Projekt, Repository, Issue-/Alert-Referenz |
| `PullRequests` | `List<PullRequestReference>` | Verknüpfte Pull Requests |
| `LogEntries` / `DiffResults` / `Todos` | Listen | Protokolleinträge, Diff-Ergebnisse, To-Dos |

## `ScmIncomingEvent`

Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

Eingehende SCM-Ereignis-Warteschlange; der Status ist ein `string` mit verbindlichen Konstanten aus `ScmIncomingEventStatus` (siehe unten).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Fortlaufende Sequenz (Eingangsreihenfolge/Dispatch-Scan) |
| `Id` | `Guid` | Ereignis-ID (Default `Guid.NewGuid()`) |
| `EventKey` | `string` | Provider-/Quellenschlüssel des Ereignisses |
| `WorkItemId` | `Guid` | Zugeordnete Aufgabe |
| `ObservationJson` | `string` | Serialisierte Beobachtung (`ScmEventObservation`, gelesen via `ScmEventStore.Observation`) |
| `Status` | `string` | Verarbeitungsstatus (`ScmIncomingEventStatus`-Konstanten, Default `Aufgenommen`) |
| `Reason` | `string?` | Warte-/Diagnosegrund (Texte u. a. aus `ScmCliWaitReason`, „CLI beschäftigt", `BlockReason`, Zulassungslimits — `ScmEventOrchestrator` Zeilen ~196–332) |
| `ReceivedUtc` | `long` | Empfangszeitpunkt (Unix-ms) |
| `Attempts` / `NextAttemptUtc` | `int` / `long?` | Versuchszähler / nächster Versuch (Unix-ms; `long.MaxValue` = blockierend bis Benutzereingriff) |
| `ErrorCode` | `string?` | Fehlercode |
| `LeaseOwner` / `LeaseUntilUtc` | `string?` / `long?` | Flüchtiger Lease-Besitzer des Dispatch (pro Versuch neue GUID — **kein** Instanzkonzept) |
| `DeliveryAttemptId` / `CliRunId` | `Guid?` / `string?` | Laufender Zustellversuch / korrelierte CLI-Lauf-ID |
| `ExchangePath` / `ExchangeRestrictedWriteSidsJson` | `string?` | Antwortkanal-Pfad und dessen eingeschränkte Schreib-SIDs |
| `DeliveredUtc` / `AcceptedUtc` | `long?` | Zustell- / Annahme-Zeitpunkt (Unix-ms) |
| `ReadyConfirmedRunId` | `string?` | Bestätigte Bereitschafts-Lauf-ID |
| `Purpose` | `string?` | Zweck-Kennzeichnung; `null` = regulär, `"IssueAufbereitung"` = interner Pfad (umgeht Trigger-Validierung) |

## `ScmIncomingEventStatus` (Konstanten-Klasse)

Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs`

Verbindliche String-Statuswerte für `ScmIncomingEvent.Status` (kein `enum`; `ScmIncomingEventStatusTests` hält die Literalwerte fest):

| Konstante | Wert | Bedeutung |
|-----------|------|-----------|
| `Aufgenommen` | `"Aufgenommen"` | Neu registriert, noch nicht zugestellt (Default) |
| `Verzögert` | `"Verzögert"` | Zustellung ruht persistiert bis `NextAttemptUtc` (Verzögerungsdirektive) |
| `Wartend` | `"Wartend"` | Wartet auf Bedingung/Korrektur; `Reason` erklärt den Grund |
| `ZustellungVorbereitet` | `"ZustellungVorbereitet"` | Versuchskanal angelegt, Prompt noch nicht geschrieben |
| `Zugestellt` | `"Zugestellt"` | Prompt in CLI geschrieben, Annahme steht aus |
| `ZustellungUngewiss` | `"ZustellungUngewiss"` | Schreibversuch begonnen, Erfolg nicht bestätigt; kein Neuversand |
| `InBearbeitung` | `"InBearbeitung"` | Korrelierte Annahme eingetroffen, Abschluss steht aus |
| `VerarbeitungUngeklärt` | `"VerarbeitungUngeklärt"` | Terminaler Diagnose-Status, nur manuell klärbar |
| `Verarbeitet` | `"Verarbeitet"` | Final: Annahme + bestätigter Abschluss |
| `Abgebrochen` | `"Abgebrochen"` | Final: vom Anwender abgebrochen |
| `NonBlocking` | `IReadOnlyCollection<string>` | Freigabeliste: `Verarbeitet`, `Abgebrochen`, `VerarbeitungUngeklärt`, `ZustellungUngewiss` — alle anderen Werte (inkl. unbekannter) blockieren Abschlussprüfung und Folgezustellung konservativ |

## `ScmObservationCheckpoint`

Datei: `src/Softwareschmiede/Domain/Entities/ScmObservationCheckpoint.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceKey` | `string` | Quellenschlüssel (Beobachtungsquelle bzw. Auflösungs-Checkpoint `repository/{prefix}/{alias}`) |
| `WatermarkUtc` | `long?` | Beobachtungs-Watermark (Unix-ms) |
| `Failures` | `int` | Fehlerzähler (Backoff) |
| `NextAttemptUtc` | `long?` | Nächster Versuch (Unix-ms) |
| `ErrorCode` / `Diagnostic` | `string?` | Fehlercode / Diagnosetext (sichtbare Provider-Diagnose) |

## `ScmOutgoingAction`

Datei: `src/Softwareschmiede/Domain/Entities/ScmOutgoingAction.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Aktions-ID (deterministisch via `ScmEventStore.ActionId`) |
| `EventId` / `AttemptId` | `Guid` / `Guid?` | Zugehöriges Ereignis / Versuch |
| `Purpose` | `string` | Zweck (z. B. `"receipt"`, `"waiting"`, `"ai:…"`) |
| `RequestJson` | `string` | Serialisierte `ScmResponseRequest` |
| `Status` | `string` | Ausführungsstatus (`"Pending"`, `"Succeeded"`, …) |
| `RemoteId` | `string?` | Remote-ID der ausgeführten Aktion |
| `Attempts` / `NextAttemptUtc` | `int` / `long?` | Versuchszähler / nächster Versuch |
| `ErrorCode` / `Error` | `string?` | Fehlercode / Fehlertext |

## `ScmTaskLease`

Datei: `src/Softwareschmiede/Domain/Entities/ScmTaskLease.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `WorkItemId` | `Guid` | Gesperrte Aufgabe |
| `Owner` | `string` | Flüchtiger Lease-Owner (pro Dispatch neu vergebene GUID, `ScmEventOrchestrator` ~Z. 94/133) |
| `ExpiresUtc` | `long` | Lease-Ablauf (Unix-ms); Verlängerung alle 15 s via `RenewTaskAsync` |

## `ScmContentSnapshot`

Datei: `src/Softwareschmiede/Domain/Entities/ScmContentSnapshot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ContentKey` | `string` | Schlüssel der beobachteten Textversion |
| `BodyHash` / `Revision` | `string` | Deduplizierungs-/Revisionsdaten |
| `IdentityResolved` | `bool` | Identitätsauflösung erfolgt |
| `Diagnostic` | `string?` | Diagnose (u. a. `AmbiguousAssignmentDiagnostic`, `UnassignedDiagnostic`, `Inbox:`-Präfix aus `ScmEventStore`) |

## `PullRequestReference`

Datei: `src/Softwareschmiede/Domain/Entities/PullRequestReference.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` / `WorkItemId` | `Guid` | IDs der Referenz / Aufgabe |
| `Provider` | `PullRequestProvider` | GitHub / BitbucketCloud / BitbucketServerDataCenter (`ProviderText` = Anzeigename) |
| `Role` | `PullRequestReferenceRole` | `CreatedByTask` / `ReviewSource` (`RoleText` = Anzeigename) |
| `RepositoryId` | `string` | Provider-Repository-ID (z. B. `owner/repo`) |
| `PullRequestNumber` | `int` | PR-Nummer beim Provider |
| `ProviderPullRequestId` | `string?` | Eindeutige Provider-ID |
| `Url` / `Title` | `string` | PR-URL / -Titel |
| `SourceBranch` / `SourceRepositoryId` / `SourceRepositoryUrl` / `SourceRef` | `string`/`string?` | Quellbranch/-Repository/-Referenz |
| `TargetBranch` | `string` | Zielbranch |
| `HeadSha` / `MergeCommitSha` | `string?` | Head-/Merge-Commit-SHA |
| `Status` | `PullRequestStatus` | Unknown/Open/Closed/Merged |
| `MergeStatus` | `PullRequestMergeStatus` | Unknown/Mergeable/Blocked/Conflicting/Merged |
| `MonitoringPhase` | `PullRequestMonitoringPhase` | Monitoring-Phase (`MonitoringPhaseText` = Anzeigename) |
| `LastCheckedUtc` / `NextCheckUtc` | `DateTimeOffset?` | Letzte/nächste Statusprüfung (Polling durch `PullRequestMonitoringService`) |
| `LastError` | `string?` | Letzter sichtbarer Fehler/Blockierungsgrund |
| `CreatedUtc` | `DateTimeOffset` | Anlagezeitpunkt des lokalen Eintrags |
| `WorkflowRuns` | `List<PullRequestWorkflowRun>` | Zugeordnete Workflow-Runs |

## `PullRequestWorkflowRun`

Datei: `src/Softwareschmiede/Domain/Entities/PullRequestWorkflowRun.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` / `PullRequestReferenceId` | `Guid` | IDs |
| `ProviderRunId` | `string` | Run-ID beim Provider |
| `Name` / `Url` | `string` / `string?` | Workflow-/Checkname, Run-URL |
| `HeadSha` / `BranchName` | `string?` | Head-SHA / Branch |
| `Status` / `Conclusion` | `WorkflowRunStatus` / `WorkflowRunConclusion` | Laufstatus / Abschlussbewertung |
| `StartedAtUtc` / `CompletedAtUtc` | `DateTimeOffset?` | Start-/Abschlusszeitpunkt |
| `IsPostMerge` | `bool` | Nach dem Merge zugeordnet |
| `UpdatedUtc` | `DateTimeOffset` | Letzter lokaler Aktualisierungszeitpunkt |

## `IssueReference`

Datei: `src/Softwareschmiede/Domain/Entities/IssueReference.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` / `WorkItemId` | `Guid` | IDs |
| `IssueNumber` | `int?` | Issue-Nummer beim Provider |
| `Title` / `Body` | `string` / `string?` | Titel / Beschreibung |
| `LabelsJson` / `Milestone` | `string` / `string?` | Labels (JSON) / Milestone |
| `IssueUrl` | `string?` | Issue-URL |
| `SourceKey` | `string?` | Kanonischer Deduplizierungsschlüssel (`{provider}/{host}/{repositoryId}/Issue/{number}`) |

## `AppSetting`

Datei: `src/Softwareschmiede/Domain/Entities/AppSetting.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID |
| `Key` | `string` | Maschinenlesbarer Schlüssel (Konvention `bereich.name`, z. B. `scm.monitoring.enabled`) |
| `Value` | `string?` | Wert; `null`/leer = Default |
| `UpdatedAt` | `DateTimeOffset` | Letzte Aktualisierung |

→ Träger der Einstellungspersistenz für `status.remote.*`-Schlüssel inkl. persistierbarer Instanz-ID.

## `GitRepository`

Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` / `ProjectId` | `Guid` | IDs |
| `PluginType` | `string` | Plugin-Typ (z. B. „GitHub") |
| `RepositoryUrl` / `RepositoryName` | `string` | URL / Name |
| `Active` | `bool` | Aktiv-Markierung — steuert die repository-weite SCM-Enumeration (Kandidat für die serverseitig zu aggregierende Repo-Liste) |
| `IssuePreparationEnabled` | `bool` | Repository-Schalter der Issue-Aufbereitung |
| `DefaultSourceBranchName` | `string?` | Konfigurierter Basis-Branch |
| `StartConfiguration` / `InitializationConfiguration` | Navigationen | Start-/Initialisierungsskript-Konfiguration |

## `LogEntry`

Datei: `src/Softwareschmiede/Domain/Entities/LogEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` / `WorkItemId` | `Guid` | IDs (Eintrag ist aufgabengebunden) |
| `Type` | `LogEntryType` | Prompt/AiResponse/StatusTransition/TestResult/GitAction/CliOutput/RateLimit/SystemMessage |
| `Content` | `string` | Eintragstext |
| `AgentName` | `string?` | Beteiligter Agent |
| `Timestamp` | `DateTimeOffset` | Zeitstempel |

→ `LogService.AddEntryAsync` schreibt aufgabengebundene Einträge; die App-weite Datei-Diagnose läuft separat über Serilog (`logs/softwareschmiede-*.log`, `App.xaml.cs` Zeilen 42–52).
