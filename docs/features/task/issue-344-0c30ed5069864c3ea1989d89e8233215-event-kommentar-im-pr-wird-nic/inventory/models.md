# Datenmodell

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID; Teil des generierten Branchnamens (`task/...-{AufgabeId:N}-...`). |
| `ProjektId` | `Guid` | Zugehöriges Projekt. |
| `GitRepositoryId` | `Guid?` | Verknüpftes lokales `GitRepository`; Scope der Auflösung (`repositoryIds.Contains(a.GitRepositoryId)`). |
| `Status` | `AufgabeStatus` | Lebenszyklus (`Neu`/`Gestartet`/`Wartend`/`Beendet`/`Archiviert`); Grundlage der „aktiv"-Definition. |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter CLI-Lebenszyklus (`NichtGestartet`/`Aktiv`/`Beendet`). |
| `BranchName` | `string?` | Name des Aufgaben-Branches; direktes Match-Signal zum PR-Head-Branch (`PullRequest.BranchName`). |
| `BasisBranchName` | `string?` | Ursprungs-Branch des Feature-Branches (Vergleichbar mit `PullRequest.TargetBranch`). |
| `LokalerKlonPfad` | `string?` | Lokaler Klonpfad; entscheidet im Dispatch zwischen Erststart und Neustart. |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausier-Ende; blockiert Starts/Zustellung (`BlockReason`), nicht das Zuordnen. |
| `AktiveRunId` | `string?` | Lauf-ID einer aktiven KI-Ausführung. |
| `LaufStatus` | `AufgabeLaufStatus?` | Feiner Laufzeit-Substatus (`Laeuft`/`WartetAufEingabe`) während `AktiveRunId` gesetzt ist. |
| `ScmAbschlussErkanntUtc` | `DateTimeOffset?` | Startpunkt der Abschluss-Wartezeit der `ScmTaskCompletionMonitoringService`. |
| `ScmAbschlussReferenz` | `string?` | Anzeigename der abschlussrelevanten Referenz. |
| `GitRepository` | `GitRepository?` | Navigation zum lokalen Repository. |
| `IssueReferenz` | `IssueReferenz?` | Verknüpftes Issue; trägt `IssueNummer` und `SourceKey`. |
| `PullRequests` | `List<PullRequestReferenz>` | Alle verknüpften Pull-Request-Referenzen (u. a. `CreatedByTask`, `ReviewSource`). |
| `Protokolleintraege`, `Todos`, `DiffResults` | Listen | Nebenbestand, für die Auflösung nicht relevant. |
| `GitArbeitsbereich` | `GitArbeitsbereich?` (`[NotMapped]`) | Convenience-Wrapper über `BranchName`/`LokalerKlonPfad`/`BasisBranchName`. |

## `PullRequestReferenz`
Datei: `src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID. |
| `AufgabeId` | `Guid` | Zugeordnete Aufgabe — der einzige heute genutzte Zuordnungspfad im Resolver. |
| `Provider` | `PullRequestProvider` | Provider-Enum (String-Konversion). |
| `Rolle` | `PullRequestReferenzRolle` | `CreatedByTask` (Default) oder `ReviewSource`. |
| `RepositoryId` | `string` | Normalisierte Provider-API-ID (`owner/repo`), Pflicht, max. 500. |
| `PullRequestNumber` | `int` | PR-Nummer beim Provider. |
| `ProviderPullRequestId` | `string?` | Eindeutige Provider-ID. |
| `Url`, `Titel` | `string` | Anzeige-/Verknüpfungsdaten. |
| `SourceBranch` | `string` | Quell-Branch (Head) des PR — entspricht `PullRequest.BranchName`; Zweitsignal zum `Aufgabe.BranchName`. |
| `SourceRepositoryId` | `string` | Provider-ID des Quell-Repositories (Fork-Erkennung). |
| `SourceRepositoryUrl`, `SourceRef` | `string?` | Auscheckdaten des Quell-Branches. |
| `TargetBranch` | `string` | Ziel-Branch (Vergleichbar mit `Aufgabe.BasisBranchName`). |
| `HeadSha`, `MergeCommitSha` | `string?` | Commit-Stände. |
| `Status`, `MergeStatus`, `MonitoringPhase` | Enums | Monitoring-Zustand der Abschluss-/PR-Überwachung. |
| `NextCheckUtc`, `LastCheckedUtc`, `LastError` | — | Monitoring-Scheduling. |
| `WorkflowRuns` | `List<PullRequestWorkflowRun>` | Zugeordnete Workflow-Runs. |

Indizes (`SoftwareschmiededDbContext`, Zeilen 283–320): Unique-Index `(Provider, RepositoryId, PullRequestNumber)`; gefilterter Unique-Index auf `AufgabeId` mit `[Rolle] = 'ReviewSource'` (höchstens eine Review-Quelle pro Aufgabe); Index `AufgabeId`; Index `(MonitoringPhase, LastCheckedUtc)`.

## `IssueReferenz`
Datei: `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID. |
| `AufgabeId` | `Guid` | Zugeordnete Aufgabe (1:1). |
| `IssueNummer` | `int?` | Issue-Nummer; Match-Signal zu `Closes #N`-Direktiven im PR-Body und zur Issue-Auflösung. |
| `Titel`, `Body`, `LabelsJson`, `Milestone`, `IssueUrl` | — | Anlagedaten des Issues. |
| `SourceKey` | `string?` | Kanonischer Dedup-Schlüssel `{provider}/{host}/{repositoryId}/Issue/{number}`; Unique-Index; wird nur von der ereignisgesteuerten Anlage gesetzt. |

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Lokale Repository-ID (`ScmRepositoryContext.GitRepositoryId`). |
| `ProjektId` | `Guid` | Projektbezug für die Aufgabenanlage. |
| `PluginTyp` | `string` | Plugin-Zuordnung (`MatchesPlugin`: `PluginPrefix` oder `PluginName`). |
| `RepositoryUrl` | `string` | Alias-Schlüssel der Auflösungsrunde (`aliasGroups`). |
| `RepositoryName` | `string` | Anzeigename. |
| `Aktiv` | `bool` | Nur aktive Repositories werden enumeriert/beobachtet. |
| `DefaultSourceBranchName` | `string?` | Konfigurierter Basis-Branch. |

## `ScmIncomingEvent`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Fortlaufende Sequenz; ordnet die Zustellung (ältere offene Ereignisse zuerst). |
| `Id` | `Guid` | Ereignis-ID (Unique). |
| `EventKey` | `string` | Dedup-Schlüssel `ContentKey/Revision/BodyHash` (Unique). |
| `AufgabeId` | `Guid` | **Zugeordnete Aufgabe — genau das Feld, das die neue Kontextauflösung setzen muss.** |
| `ObservationJson` | `string` | Serialisierte `ScmEventObservation` (unveränderlicher Ursprung). |
| `Status` | `string` | `"Aufgenommen"`, `"Wartend"`, `"ZustellungVorbereitet"`, `"Zugestellt"`, `"InBearbeitung"`, `"Verarbeitet"`, `"Abgebrochen"`, `"ZustellungUngewiss"`, `"VerarbeitungUngeklärt"`. |
| `Reason`, `ErrorCode`, `NextAttemptUtc`, `Attempts` | — | Warte-/Retry-Zustand. |
| `DeliveryAttemptId`, `CliRunId`, `ExchangePath`, `ExchangeRestrictedWriteSidsJson`, `DeliveredUtc`, `AcceptedUtc`, `ReadyConfirmedRunId` | — | Zustellversuch-/Antwortkanal-Zustand. |
| `LeaseOwner`, `LeaseUntilUtc` | — | (Felder vorhanden; die eigentliche Lease liegt in `ScmTaskLeases`.) |

Indizes: Unique `Id`, Unique `EventKey`, `(AufgabeId, Sequence)`, `(Status, NextAttemptUtc)`.

## `ScmContentSnapshot`
Datei: `src/Softwareschmiede/Domain/Entities/ScmContentSnapshot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ContentKey` | `string` | PK; stabiler Schlüssel eines beobachteten Textes. |
| `BodyHash`, `Revision` | `string` | Versionserkennung. |
| `IdentityResolved` | `bool` | Autor-/Trigger-Prüfung erfolgt. |
| `Diagnostic` | `string?` | u. a. `AmbiguousAssignmentDiagnostic` / `UnassignedDiagnostic` — ein `Unassigned`-Snapshot erzwingt einen kompletten Re-Read (`SinceUtc = null`, Monitoring Zeilen 174–177). |

## `PullRequest` (Value Object)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs`

Record mit `Nummer`, `Titel`, `Url`, `BranchName` (Quell-/Head-Branch — Match-Signal zu `Aufgabe.BranchName`), `Provider`, `RepositoryId`, `ProviderPullRequestId`, `TargetBranch`, `HeadSha`, `SourceRepositoryId` (Fork-Erkennung), `SourceRepositoryUrl`, `SourceRef`, `Body` (enthält u. a. `Closes #N`-Direktiven). Geliefert von `IScmEventSource.ReadPullRequestAsync`.

## `Issue` (Value Object)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs`

Record mit `Nummer`, `Titel`, `Body`, `Labels`, `Milestone`, `IssueUrl` sowie `IstGeschlossen` (`init`, nur von Status-lesenden Quellen gesetzt; von `ScmTaskCompletionMonitoringService.DetermineClosureAsync` genutzt).

## SCM-Ereignis-Value-Objects
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`

| Typ | Elemente | Zweck |
|-----|----------|-------|
| `ScmEventRepository` | `ProviderPrefix`, `Host`, `RepositoryId`, `Owner`, `Name`, `OwnerId`, `ViewerId`, `ViewerLogin`, `CanWrite`, `Konto` | Kanonisch aufgelöstes Remote-Repository (aus `GetEventRepositoryAsync`). |
| `ScmEventReference` | `Repository`, `Kind` (`Issue`/`PullRequest`), `Number` | Validierter Ursprung einer Beobachtung. |
| `ScmEventReadRequest` | `Reference`, `ContentKind`, `SinceUtc`, `WatermarkUtc` | Leseanfrage einer checkpointbaren Quelle. |
| `ScmEventBatch` | `Request`, `Observations` | Vollständig gelesene Quelle. |
| `ScmEventObservation` | `Reference`, `ContentKind`, `RemoteNodeId`, `RemoteCommentId`, Zeitstempel, Autor/Editor, `Body`, `Url`, `ObservedAt`; abgeleitet `BodyHash`, `Revision`, `ContentKey`, `EventKey` | Beobachteter Textstand; `ContentKey`/`EventKey` sind Dedup-/Persistenzschlüssel. |
| `ScmNotification` | `ThreadId`, `RepositoryNodeId`, `Host`, `Owner`, `Name`, `Kind`, `Number`, `Reason`, `UpdatedAt` | Inbox-Roheintrag des separaten Kontos (`MentionReason = "mention"`). |
| `ScmResponseRequest`/`ScmResponseResult`/`ScmResponseKind` | — | Host-Antwortkanal (Receipts, Waiting-Kommentare, Agentenaktionen). |

## `ScmRepositoryContext`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRepositoryContext.cs`

Record mit `GitRepositoryId` (lokal), `PluginPrefix`, `RepositoryId` (kanonische API-ID), `Konto`. Wird beim Anlegen aus `ResolvePullRequestAsync` (Zeile 92) und beim Dispatch (Zeile 130) aufgebaut; validiert nicht-leere IDs.

## `ScmTaskResolution`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Zeilen 23–35)

Record `(ScmTaskResolutionKind Kind, Guid? AufgabeId)` mit den statischen Werten `Ambiguous`, `Unassigned` und der Fabrik `Assigned(Guid)` (wirft bei `Guid.Empty`). Rückgabetyp von `ScmEventTaskResolver.ResolveAsync` — die neue Kontextprüfung muss denselben Ergebnistyp liefern.
