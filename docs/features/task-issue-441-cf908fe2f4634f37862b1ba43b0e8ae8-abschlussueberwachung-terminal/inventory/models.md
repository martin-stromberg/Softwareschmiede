# Datenmodell — Bestandsaufnahme

## `ScmIncomingEvent`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`
EF-Konfiguration: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiedeDbContext.cs` Zeilen 109–118 (PK `Sequence` autogeneriert, Unique-Indizes auf `Id` und `EventKey`, Index `(WorkItemId, Sequence)`, Index `(Status, NextAttemptUtc)`, `Purpose` max. 100 Zeichen).

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Sequence` | `long` | Fortlaufende Eingangsreihenfolge (DB-generiert, PK). |
| `Id` | `Guid` | Ereignis-ID (Default `Guid.NewGuid()`). |
| `EventKey` | `string` | Dedup-Schlüssel des Ereignisses (Unique). |
| `WorkItemId` | `Guid` | Zugeordnete Aufgabe. |
| `ObservationJson` | `string` | Serialisierte `ScmEventObservation` (gelesen via `ScmEventStore.Observation`). |
| `Status` | `string` | Freier Status-String, Default `"Aufgenommen"` — kein Enum, keine zentrale Konstantenstelle. Wertemenge siehe [enums.md](enums.md). |
| `Reason` | `string?` | Menschenlesbarer Warte-/Fehlergrund. |
| `ReceivedUtc` | `long` | Empfangszeitpunkt (Unix-ms). |
| `Attempts` | `int` | Zustellversuche (inkrementiert in `DispatchCoreAsync`/`DispatchPreparationAsync` und bei Provider-Retry via `ScmEventStore.RetryAt`). |
| `NextAttemptUtc` | `long?` | Frühestens nächster Versuch (Unix-ms); `long.MaxValue` = harter Stopp bis zur Benutzeraktion. |
| `ErrorCode` | `string?` | `ScmProviderFailureKind`-Name bei Providerfehlern. |
| `LeaseOwner`/`LeaseUntilUtc` | `string?`/`long?` | Felder für Lease; im aktuellen Pfad wird die Serialisierung über `ScmTaskCoordinator` + `ScmTaskLease` abgebildet. |
| `DeliveryAttemptId` | `Guid?` | ID des aktuellen Zustellversuchs; `null` = noch nie zugestellt bzw. zurückgesetzt (`ResendAsync`). |
| `CliRunId` | `string?` | Run-ID der CLI-Sitzung des Versuchs (GUID als „D"-String). |
| `ExchangePath` | `string?` | Verzeichnis des Antwortkanals des Versuchs (`…/.softwareschmiede-scm-exchange/{runId}/{attemptId}`). |
| `ExchangeRestrictedWriteSidsJson` | `string?` | Beschränkende SIDs des Kanals (JSON-Array). |
| `DeliveredUtc` | `long?` | Zeitpunkt der Zustellungsvorbereitung (Unix-ms). |
| `AcceptedUtc` | `long?` | Zeitpunkt der korrelierten Annahme (`accepted.json`). |
| `ReadyConfirmedRunId` | `string?` | Persistierte Bereit-Bestätigung (wird aktuell nur zurückgesetzt). |
| `Purpose` | `string?` | Zweck-Markierung; `null` = reguläres Ereignis, `"IssueAufbereitung"` (`ScmIssuePreparationService.Purpose`) = interner Aufbereitungsauftrag mit eigenem Pfad. |

## `WorkItem` (relevante Ausschnitte)
Datei: `src/Softwareschmiede/Domain/Entities/WorkItem.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Status` | `WorkItemStatus` | Lebenszyklus; `Completed`/`Archived` schließen die Aufgabe aus Beobachtung und Dispatch aus. |
| `ExecutionStatus` | `WorkItemExecutionStatus` | Lauf-Lebenszyklus; verwendet u. a. von `ScmDispatchPriority` (Fortsetzungsklasse). |
| `PausedUntilUtc` | `DateTimeOffset?` | Pausenziel; blockiert Starts (`BlockReason` im Orchestrator). |
| `ScmCompletionDetectedUtc` | `DateTimeOffset?` | Persistierter Startpunkt der Abschluss-Wartezeit. **DB-Spaltenname `ScmAbschlussErkanntUtc`** (`SoftwareschmiedeDbContext.cs` Zeile 611, Migration `20261020062227_AddAufgabeScmAbschluss`). |
| `ScmCompletionReference` | `string?` | Anzeigename der geschlossenen Referenz (DB-Spalte `ScmAbschlussReferenz`, Zeile 612). |
| `IssueReference` | `IssueReference?` | Navigation: verknüpftes Issue (SCM-Referenz). |
| `PullRequests` | `List<PullRequestReference>` | Navigation: verknüpfte Pull Requests (SCM-Referenz, Vorrang vor Issue in `DetermineClosureAsync`). |
| `GitRepository`/`GitRepositoryId` | `GitRepository?`/`Guid?` | Repository-Kontext für Plugin-Auflösung. |
| `AiPluginPrefix` | `string?` | Aufgaben-CLI-Präfix (Dispatch-Auflösung). |
| `LocalClonePath`/`BranchName`/`BaseBranchName` | `string?` | Klon-/Branch-Zustand; steuert `StartProcessAndCliAsync` vs. `RestartCliAsync`. |
| `Purpose` | `string?` | Zweck-Markierung der Aufgabe. |

## `PullRequestReference`
Datei: `src/Softwareschmiede/Domain/Entities/PullRequestReference.cs`
Unique-Index `(Provider, RepositoryId, PullRequestNumber)` (`SoftwareschmiedeDbContext.cs` Zeile 319).

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Referenz-ID. |
| `WorkItemId` | `Guid` | Zugeordnete Aufgabe. |
| `Provider` | `PullRequestProvider` | SCM-Provider (faktisch GitHub). |
| `Role` | `PullRequestReferenceRole` | `CreatedByTask` (Default) oder `ReviewSource`; `SaveReportedAsync` legt mit `CreatedByTask` an. |
| `RepositoryId` | `string` | Normalisiertes `owner/repo` (`PullRequestRepositoryId.Normalize`). |
| `PullRequestNumber` | `int` | PR-Nummer beim Provider — das aus `complete.json` gemeldete `pullRequestNumber`. |
| `ProviderPullRequestId`/`Url`/`Title`/`SourceBranch`/`SourceRepositoryId`/`SourceRepositoryUrl`/`SourceRef`/`TargetBranch`/`HeadSha`/`MergeCommitSha` | diverse | Bestandsdaten des PR (werden durch `SaveCreatedAsync`/Monitoring gefüllt; `SaveReportedAsync` setzt nur `Url` und ggf. `Status` = `Open`). |
| `Status` | `PullRequestStatus` | `Unknown`/`Open`/`Closed`/`Merged`; wird von `DetermineClosureAsync` remote aktualisiert. |
| `MergeStatus` | `PullRequestMergeStatus` | Merge-Zustand. |
| `MonitoringPhase` | `PullRequestMonitoringPhase` | Phase des PR-Monitorings (`PullRequestMonitoringPolicy.GetInitialPhase`). |
| `LastCheckedUtc`/`NextCheckUtc`/`LastError`/`CreatedUtc` | diverse | Monitoring-/Fehler-Zeitpunkte. |
| `WorkItem`/`WorkflowRuns` | Navigationen | Aufgabe bzw. `PullRequestWorkflowRun`-Liste. |

## `IssueReference`
Datei: `src/Softwareschmiede/Domain/Entities/IssueReference.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id`/`WorkItemId` | `Guid` | Referenz-ID / Aufgabe. |
| `IssueNumber` | `int?` | Issue-Nummer; `> 0` ist Beobachtungsvoraussetzung in `DetermineClosureAsync`. |
| `Title`/`Body`/`LabelsJson`/`Milestone`/`IssueUrl`/`SourceKey` | diverse | Bestandsdaten/Dedup-Schlüssel. |

## `ScmOutgoingAction`
Datei: `src/Softwareschmiede/Domain/Entities/ScmOutgoingAction.cs`
Index `(EventId, Status)` (`SoftwareschmiedeDbContext.cs` Zeile 125). Status-Wertemenge: `"Pending"` (Default), `"Sending"`, `"Succeeded"`, `"Uncertain"` (gesetzt in `ScmEventOutbox.cs` Zeilen 58–72).

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id`/`EventId`/`AttemptId` | `Guid`/`Guid`/`Guid?` | Aktions-ID (teils deterministisch via `ScmEventStore.ActionId`), Ereignis, Versuch. |
| `Purpose` | `string` | `"receipt"`, `"waiting"`, `"attachments"`, `"ai:{actionId}"` u. a. |
| `RequestJson` | `string` | Serialisierter `ScmResponseRequest`. |
| `Status`/`RemoteId`/`Attempts`/`NextAttemptUtc`/`ErrorCode`/`Error` | diverse | Zustellzustand und Retry-Felder. |
