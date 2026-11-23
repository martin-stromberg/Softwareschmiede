# Datenmodell — Bestandsaufnahme

Betroffene persistierte Entitäten und Value Objects des SCM-Ereigniskanals. Nur die für die Anforderung relevanten Eigenschaften sind vollständig aufgeführt; Navigationseigenschaften sind kompakt zusammengefasst.

## `ScmIncomingEvent`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

Persistenter SCM-Ereignisdatensatz (Inbox). Die Verarbeitungseinheit, um die es in der Anforderung geht.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Eingangsreihenfolge; Basis der Vorgängerprüfung im Orchestrierer und der Stabilisierung in `ScmDispatchPriority`. |
| `Id` | `Guid` | Ereignis-ID (Default `Guid.NewGuid()`). |
| `EventKey` | `string` | Dedup-Schlüssel; internes Aufbereitungsereignis trägt `ScmIssuePreparationService.PreparationEventKey` (`{ContentKey}/IssueAufbereitung`). |
| `WorkItemId` | `Guid` | Zugeordnete Aufgabe — vom Resolver bzw. vom Aufbereitungsdienst gesetzt. |
| `ObservationJson` | `string` | Serialisierte `ScmEventObservation` (unveränderlicher Ursprung; gelesen via `ScmEventStore.Observation`). |
| `Status` | `string` | Ereignisstatus, Werte aus `ScmIncomingEventStatus` (Default `Aufgenommen`). |
| `Reason` | `string?` | Benutzerlesbarer Warte-/Klärungsgrund. |
| `ReceivedUtc` | `long` | Empfangszeitpunkt (Unix-ms). |
| `Attempts` | `int` | Versuchszähler; steuert `ScmEventStore.RetryAt`-Backoff. |
| `NextAttemptUtc` | `long?` | Wiederanlaufzeitpunkt; `long.MaxValue` = harter Stopp bis Benutzereingriff; `null` = sofort fällig (Aufbereitungsereignis wird so eingereiht). |
| `ErrorCode` | `string?` | Typisierter Fehler (`ScmProviderFailureKind`-Name). |
| `LeaseOwner` / `LeaseUntilUtc` | `string?` / `long?` | Ereignis-Lease (separat von der Aufgaben-Lease in `ScmTaskLease`). |
| `DeliveryAttemptId` | `Guid?` | Korrelations-ID des laufenden Zustellversuchs; gesetzt ⇒ Reconcile-Pfad statt Neuzustellung. |
| `CliRunId` | `string?` | Run-ID des gebundenen CLI-Laufs. |
| `ExchangePath` / `ExchangeRestrictedWriteSidsJson` | `string?` | Antwortkanal-Verzeichnis und nachgewiesene Schreibidentitäten (JSON). |
| `DeliveredUtc` / `AcceptedUtc` | `long?` | Zeitpunkt der Prompt-Zustellung bzw. der korrelierten Annahme. |
| `ReadyConfirmedRunId` | `string?` | Zuletzt bestätigte Run-ID (manuelle Eingabebereitschafts-Bestätigung). |
| `Purpose` | `string?` | Zweck-Kennzeichnung; `null` = reguläres externes Ereignis. `"IssueAufbereitung"` = interner, trigger-freier Aufbereitungsauftrag mit eigenem Ausführungspfad (`DispatchPreparationAsync`). |

## `ScmIncomingEventStatus`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs`

Kein Enum, sondern eine statische Klasse mit `const string`-Werten plus der Freigabeliste `NonBlocking` (siehe [enums.md](enums.md)). Relevant: `Wartend` ist der Zielzustand für „Ereignis bleibt/kehrt auf Wartend" der Anforderung.

## `WorkItem`
Datei: `src/Softwareschmiede/Domain/Entities/WorkItem.cs`

Die Aufgabe. Für die Anforderung relevante Eigenschaften:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Aufgaben-ID; `ScmIncomingEvent.WorkItemId` zeigt hierauf. |
| `ProjectId` / `GitRepositoryId` | `Guid` / `Guid?` | Projekt- und Repository-Zuordnung; der Resolver filtert auf `GitRepositoryId` ∈ lokale Kandidatenmenge. |
| `Status` | `WorkItemStatus` | Lebenszyklus (`New`, `Started`, `Waiting`, `Completed`, `Archived`); `BlockReason` sperrt `Completed`/`Archived` und aktive `PausedUntilUtc`. |
| `ExecutionStatus` | `WorkItemExecutionStatus` | Lauf-Lebenszyklus (`NotStarted`, `Active`, `Completed`); Kriterium der Klasse-1-Fortsetzung in `ScmDispatchPriority`. |
| `RunStatus` | `WorkItemRunStatus?` | Laufzeit-Substatus (`Running`/`WaitingForInput`, persistiert als „WartetAufEingabe"); nur während `ActiveRunId` relevant, wird von `CliProcessManager` aus Terminal-I/O gepflegt. |
| `AiPluginPrefix` | `string?` | Zugeordnetes KI-Plugin; Fallback-Attribution der Ausführungsbegrenzung und Aufbereitungs-Plugin-Override (wird bei Anlage über `aiPluginPrefix` gesetzt). |
| `PausedUntilUtc` | `DateTimeOffset?` | Aufgabenpause; blockiert Starts (`BlockReason`), befreit den Ausführungsplatz in der Belegungszählung. |
| `Purpose` | `string?` | Zweck-Markierung; `null` = reguläre Aufgabe. `"IssueAufbereitung"` markiert die Aufbereitungs-Aufgabe — vom kanonischen Resolver-Filter `a.Purpose == null` ausgeschlossen (Duplikat-Ursache). |
| `Kind` | `WorkItemKind` | Auftragsart/Anzeige; `IssuePreparation` bei Anlage mit Purpose, wechselt nach erfolgreicher Analyse-Zustellung zu `Instruction` (`ScmEventOrchestrator.cs` ~Zeile 804). |
| `IssueReference` | `IssueReference?` | Verknüpfte Issue-Referenz (Navigation, 0..1). |
| Weitere | — | `Title`, `RequirementDescription`, `BranchName`, `LocalClonePath`, `BaseBranchName`, `AgentPackageName`, `AgentName`, `ActiveRunId`, `LastHeartbeatUtc`, `LastCliStartUtc`, `RecoveryVersion`, `SuggestedPrompt`, `SuggestedExecuteAfterUtc`, `ScmCompletionDetectedUtc`, `ScmCompletionReference`, `CreatedAt`, `CompletedAt` sowie Navigationen (`Project`, `AlertReference`, `PullRequests`, `LogEntries`, `DiffResults`, `Todos`). |

## `IssueReference`
Datei: `src/Softwareschmiede/Domain/Entities/IssueReference.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` / `WorkItemId` | `Guid` | Primärschlüssel / Aufgaben-FK. |
| `IssueNumber` | `int?` | Issue-Nummer im Provider; Match-Kriterium des Resolvers (`a.IssueReference.IssueNumber == reference.Number`). |
| `Title` / `Body` / `LabelsJson` / `Milestone` / `IssueUrl` | `string`/`string?` | Anlagedaten-Snapshot; `Title` speist `ScmIssuePreparationPrompt.Build`. |
| `SourceKey` | `string?` | Kanonischer Dedup-Schlüssel; nur ereignisgesteuerte Anlage setzt ihn. Zwei Namensräume: `…/Issue/{n}` (regulär, `ScmEventTaskResolver.IssueSourceKey`) und `…/IssueAufbereitung/{n}` (`ScmIssuePreparationService.PreparationSourceKey`). Eindeutig per Unique-Index (Anlagerennen-Schutz in `WorkItemService.CreateFromIssueEventAsync`). |

## `GitRepository` (relevante Eigenschaft)
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `IssuePreparationEnabled` | `bool` | Repository-Schalter der Issue-Aufbereitung; nur freigeschaltete Repositories erzeugen Aufbereitungsaufträge (`DetectAndEnqueueAsync`, `ScmIssuePreparationMonitoringService`) und nur dann läuft `DispatchPreparationAsync` weiter. |

## `ScmOutgoingAction`
Datei: `src/Softwareschmiede/Domain/Entities/ScmOutgoingAction.cs`

Outbox-Datensatz für Host-/KI-Aktionen am gespeicherten Ereignisursprung.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Deterministisch aus `ScmEventStore.ActionId(eventId, purpose)` bzw. vom Agenten gewählt. |
| `EventId` / `AttemptId` | `Guid` / `Guid?` | Ereignis-/Versuchsbezug. |
| `Purpose` | `string` | Zweck der Aktion (`"receipt"`, `"waiting"`, `"attachments"`, `"ai:{guid}"`). |
| `RequestJson` / `Status` / `RemoteId` | `string` | Serialisierte `ScmResponseRequest`; Status `Pending`/`Succeeded`/Fehler. |
| `Attempts` / `NextAttemptUtc` / `ErrorCode` / `Error` | — | Retry-Felder der Outbox. |

## `ScmObservationCheckpoint` (Kontext)
Datei: `src/Softwareschmiede/Domain/Entities/ScmObservationCheckpoint.cs`

Checkpoint-Datensatz je Quelle/Verarbeitungsstrang (`SourceKey`, `WatermarkUtc`, `Failures`, `NextAttemptUtc`, `ErrorCode`, `Diagnostic`, `IdentityResolved`-bezogene Nutzung über `ScmContentSnapshot`). Die Aufbereitung nutzt drei eigene Namensräume: `issueaufbereitung/…` (Baseline), `issueaufbereitung-issue/…` (Auswertungs-Endzustand, `NextAttemptUtc = long.MaxValue`), `issueaufbereitung-repository/…` + `issueaufbereitung-enumeration/…` (eigener Monitoring-Strang).

## `ScmTaskLease` (Kontext)
Datei: `src/Softwareschmiede/Domain/Entities/ScmTaskLease.cs`

Aufgaben-Lease (`WorkItemId`, `Owner`, `ExpiresUtc`); beansprucht/verlängert/freigegeben über `ScmEventStore.ClaimTaskAsync`/`RenewTaskAsync`/`ReleaseTaskAsync` — die Aufgabensperre des Dispatch-Pfads.

## Value Objects des Ereigniskanals
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`

| Record/Enum | Bedeutung |
|-------------|-----------|
| `ScmEventRepository` | Aufgelöste Repository-Identität (`ProviderPrefix`, `Host`, `RepositoryId`, `Owner`, `Name`, `OwnerId`, `ViewerId`, `ViewerLogin`, `CanWrite`, `Account`, `NumericRepositoryId`). |
| `ScmEventReference` | `(Repository, Kind, Number)` — die Referenz, deren Issue-`Number` beide Ereignisse des Szenarios teilen. |
| `ScmEventReadRequest` | `(Reference, ContentKind, SinceUtc, WatermarkUtc)` — Quell-Read je Inhaltsart. |
| `ScmEventBatch` | `(Request, Observations)` — atomar speicherbarer Quell-Read. |
| `ScmEventObservation` | Beobachtete Textversion; liefert `BodyHash`, `Revision`, `ContentKey` (`{provider}/{host}/{repoId}/{Kind}/{n}/{ContentKind}/{nodeId}`) und `EventKey` (`{ContentKey}/{revision}/{hash}`). Das Aufbereitungs-EventKey hängt `/{Purpose}` an (`PreparationEventKey`). |
| `ScmResponseRequest` | Host-validierte Remote-Antwort mit deterministischer `ActionId` und `Marker`. |
| `ScmNotification` | Roh-Inbox-Eintrag des SeparatesKonto-Modus; `MentionReason = "mention"`. |
| `ScmResponseKind` | `Comment`, `Reaction`, `PullRequest`, `IssueBodyUpdate`. |
| `ScmProviderException` | Typisierter Providerfehler (`FailureKind`, `RetryAfterUtc`, `OutcomeUncertain`). |
