# Enums

## `WorkItemStatus`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `New` | Erstellt, wartet auf Bearbeitung |
| `Started` | Gestartet (Branch erstellt, CLI läuft/sollte laufen) |
| `Waiting` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme (Lebenszyklus, nicht Eingabewarten) |
| `Completed` | Beendet (erfolgreich oder mit Fehler) |
| `Archived` | Archiviert, nicht mehr aktiv |

Erweiterungen in `WorkItemStatusExtensions`/`WorkItemExecutionStatusExtensions` (u. a. `ActiveOrWaitingStatuses`, `IsActiveOrWaiting()`, `IsCurrentlyPaused`-Hilfe für `PausedUntilUtc`).

## `WorkItemExecutionStatus`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemExecutionStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NotStarted` | KI-Ausführung noch nicht gestartet |
| `Active` | Ausführung aktiv bzw. nach Neustart wiederherzustellen |
| `Completed` | Ausführung beendet; Neustart nur explizit |

## `WorkItemRunStatus`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemRunStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Running` | CLI läuft und hat kürzlich Ausgabe/Eingabe verarbeitet |
| `WaitingForInput` | CLI läuft, erzeugt seit längerem keine Ausgabe — vermutlich wartet sie auf Benutzereingabe (Basis für Sektion „Wartet auf Anwender") |

## `WorkItemKind`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemKind.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unspecified = 0` | Nicht bestimmt / Bestandsdaten |
| `Manual = 1` | Manuell angelegte Aufgabe |
| `Instruction = 2` | Aus SCM-Trigger-Ereignis angelegte Anweisungsaufgabe (Zieltyp der Issue-Aufbereitung) |
| `PullRequest = 3` | Aus Pull-Request-Ereignis angelegt |
| `Alert = 4` | Aus SCM-Alert angelegt |
| `IssuePreparation = 5` | Automatische Issue-Aufbereitung |

## `AiExecutionState`
Datei: `src/Softwareschmiede/Domain/Enums/AiExecutionState.cs`

| Wert | Bedeutung |
|------|-----------|
| `Paused` | Aufgabe pausiert (`PausedUntilUtc` in der Zukunft) |
| `PromptQueued` | Zeitgesteuerter Prompt steht in der Warteschlange |
| `Running` | CLI-Ausführung läuft (aktiver Run ohne `WaitingForInput`-Substatus) |
| `Waiting` | Ausführung wartet (`WaitingForInput`-Substatus oder Aufgabenstatus `Waiting`) |
| `Ready` | Keine aktive Ausführung erkennbar |

Wird aus `AiExecutionStatusData` via `AiExecutionStatusClassification` abgeleitet — gemeinsame „läuft"-Klassifikation auch für `RunningAutomationsCount`.

## `ScmAccountMode`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmAccountMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `UserAccount = 0` | Überwachung mit dem Benutzerkonto (Trigger-Keyword) |
| `SeparateAccount = 1` | Überwachung mit separatem Konto (Erwähnung/Inbox) — beeinflusst, welche Kommentar-Identität `ScmEventValidationService` akzeptiert |

## `PullRequestProvider`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestProvider.cs`

| Wert | Bedeutung |
|------|-----------|
| `GitHub = 0` | GitHub |
| `BitbucketCloud = 1` | Bitbucket Cloud |
| `BitbucketServerDataCenter = 2` | Bitbucket Server/Data Center |

## `PullRequestStatus` / `PullRequestMergeStatus` / `PullRequestReferenceRole` / `PullRequestChecksState`
Dateien: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/{PullRequestStatus,PullRequestMergeStatus,PullRequestReferenceRole,PullRequestChecksState}.cs`

| Enum | Werte |
|------|-------|
| `PullRequestStatus` | `Unknown`, `Open`, `Closed`, `Merged` |
| `PullRequestMergeStatus` | `Unknown`, `Mergeable`, `Blocked`, `Conflicting`, `Merged` |
| `PullRequestReferenceRole` | `CreatedByTask = 0`, `ReviewSource = 1` |
| `PullRequestChecksState` | `Unknown`, `None`, `Pending`, `Success`, `Failure` |

## `PullRequestMonitoringPhase`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestMonitoringPhase.cs`

| Wert | Bedeutung |
|------|-----------|
| `Created = 0` … `NotMonitored = 12` | `Created`, `PreMergeRunning`, `PreMergeSucceeded`, `Completing`, `Completed`, `Approved`, `PostMergeUncertain`, `PostMergeRunning`, `PostMergeSucceeded`, `PostMergeFailed`, `Blocked`, `Failed`, `NotMonitored` |

## `WorkflowRunStatus` / `WorkflowRunConclusion`
Dateien: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/{WorkflowRunStatus,WorkflowRunConclusion}.cs`

| Enum | Werte |
|------|-------|
| `WorkflowRunStatus` | `Unknown`, `Queued`, `InProgress`, `Completed` |
| `WorkflowRunConclusion` | `Unknown`, `Success`, `Failure`, `Cancelled`, `Skipped`, `TimedOut`, `ActionRequired` |

## `LogEntryType`
Datei: `src/Softwareschmiede/Domain/Enums/LogEntryType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Prompt`, `AiResponse`, `StatusTransition`, `TestResult`, `GitAction`, `CliOutput`, `RateLimit`, `SystemMessage` | Typen aufgabengebundener Protokolleinträge; `SystemMessage`/`GitAction` tragen u. a. Wartegründe und PR-Verknüpfungs-Hinweise |

## SCM-Ereignis-Value-Enums (Plugin-Contracts, `Domain/ValueObjects`/`ScmEvents.cs`)

Für die Inbox relevante Werte in `ScmEvents.cs` (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/`): `ScmReferenceKind` (Issue/PullRequest), `ScmContentKind` (`Description`, `IssueComment`, `ReviewComment`, `PullRequestReview`), `ScmResponseKind` (u. a. `Comment`, `Reaction` für `receipt`-/`waiting`-Hostaktionen). `ScmNotification.AssignReason` kennzeichnet Zuweisungs-Auslöser.
