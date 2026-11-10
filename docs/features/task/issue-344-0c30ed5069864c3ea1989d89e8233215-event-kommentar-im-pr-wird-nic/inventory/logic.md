# Logik

## `ScmEventTaskResolver`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`

Kern der Aufgabenauflösung. Wird aufgerufen von `ScmEventMonitoringService.RunOnceAsync` (Zeilen 184–185), ausschließlich wenn die Ereignisquelle unzugeordnet ist (`taskIds.Length == 0`) und eine qualifizierte Beobachtung (`ScmEventValidationService.GetRejection(...) is null`) existiert. DI: `AddScoped<ScmEventTaskResolver>` (`App.xaml.cs` Zeile 226; `ScmDispatchFixture` Zeile 151).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync(reference, source, trigger, repositories, ct)` | public | Dispatcher nach `reference.Kind` → Issue- oder PR-Pfad. |
| `ResolveIssueAsync(...)` | private | Liest Issue via `ReadIssueAsync`; sucht Aufgaben über `IssueReferenz.IssueNummer == reference.Number` im `repositories`-Scope (Zeilen 63–66, **statusagnostisch** — kein `Status`-Filter). `>1` → `Ambiguous`; `==1` → `Assigned`; `0` → `CreateFromIssueEventAsync` mit `IssueSourceKey`. |
| `ResolvePullRequestAsync(...)` | private | Liest PR via `ReadPullRequestAsync`; normalisiert `repositoryId` aus `owner/name` der Referenz; sucht nur über `PullRequestTaskIdsAsync` (persistierte Referenzen). `>1` → `Ambiguous`; `==1` → `Assigned`; `0` → `CreateFromPullRequestAsync`. `InvalidOperationException` beim Anlegen → Re-Lookup als Anlagerennen-Auflösung (Zeilen 99–105). |
| `PullRequestTaskIdsAsync(provider, repositoryId, number, ct)` | private | Einziger PR-Lookup: `PullRequestReferenzen` nach `Provider` + `RepositoryId` + `PullRequestNumber`, `Distinct` Aufgaben-IDs (Zeilen 108–112). |
| `ChooseRepository(repositories)` | private | Deterministische lokale Repository-Wahl (`RepositoryUrl`, dann `Id`); wirft bei leerer Liste. |
| `ComposeBody(referenceBody, trigger)` | private | Auftragstext aus Referenzbeschreibung + auslösender Beobachtung + URL. |
| `IssueSourceKey(reference)` | private | Kanonischer Dedup-Schlüssel für `IssueReferenz.SourceKey`. |

Publizierte/abonnierte Events: keine. Schreibt nur über `AufgabeService` bzw. liest `db.Aufgaben`/`db.PullRequestReferenzen` (`AsNoTracking`).

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

`PeriodicBackgroundService`; beobachtet verknüpfte Referenzen und enumeriert repository-weit; stößt Auflösung und Dispatch an.

| Methode / Abschnitt | Sichtbarkeit | Kurzbeschreibung |
|---------------------|-------------|------------------|
| `RunOnceAsync(ct)` | public override | Startup-Gate → Laufzeit-Schalter `GetScmMonitoringEnabledAsync` → lädt `tasks` (Aufgaben mit `GitRepository`, `IssueReferenz`, `PullRequests`; nur `GitRepository.Aktiv`) und `repositories`. |
| `linked`-Aufbau (Zeilen 51–59) | — | Zuordnungsliste `(TaskId, Repository, Kind, Number, RepositoryId)`: je Aufgabe einmal `Issue` aus `IssueReferenz.IssueNummer` und je `PullRequestReferenz` einmal `PullRequest`. **Kein Branch-/Kontext-Bezug.** |
| Alias-Auflösung (Zeilen 75–124) | — | `aliasGroups` nach `RepositoryId`; `GetEventRepositoryAsync` je Alias; Auflösungsfehler → Checkpoint-Backoff (`RecordFailureAsync`); unvollständige Auflösung blockiert die Provider-Runde. |
| Inbox-Modus `SeparatesKonto` (Zeilen 127–139) | — | `EnumerateNotificationsAsync` ersetzt die Referenz-Enumeration; verknüpfte Referenzen werden nicht als Quellen übernommen. |
| `EnumerateRepositoryAsync` | private | `EnumerateReferencesAsync` pro Repository einmal pro Lauf; fügt `(null, reference)` in `resolved`; Backmerge-Bewertung; eigener Checkpoint `enumeration/...`. |
| `EnumerateNotificationsAsync` | private | Liest Host-Inbox; nur `MentionReason`-Notifications auf registrierte `RepositoryNodeId`s werden zu `resolved`-Referenzen; Watermark-Commit verzögert (`pendingInboxCommits`). |
| Quellen-Batches (Zeilen 153–198) | — | Je Referenz × `ScmContentKind`: `ReadEventsAsync`; `taskIds` aus `resolved` (`==1` → Aufgabe, `0` → Resolver, `>1` → Ambiguous-Diagnose); `ScmEventStore.AcceptBatchAsync`. |
| Dispatch-Schleife (Zeilen 222–241) | — | Alle fälligen `ScmIncomingEvents` in `Sequence`-Ordnung → `ScmEventOrchestrator.DispatchAsync`. |
| `MatchesPlugin`, `IsInaccessible`, `RecordFailureAsync` | private | Plugin-Match, 404/403-Isolierung, Checkpoint-Fehlerpersistenz. |

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

Relevante Methoden für Anlage, Lookup und „aktiv"-Definition:

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateFromIssueEventAsync(projektId, issue, gitRepositoryId, sourceKey, ct)` | public | Vorab-Lookup `IssueReferenzen.SourceKey` (Zeilen 276–285) → liefert Bestandsaufgabe; sonst Anlage `Status = Neu` mit `IssueReferenz` inkl. `SourceKey`; `DbUpdateException` → Re-Lookup (Unique-Index-Rennen). |
| `CreateFromPullRequestAsync(projektId, pullRequest, repositoryContext, ct)` | public | Validierungen (Nummer, `BranchName`, Repository-Kontext, auscheckbare Quelle); Vorab-Check `IsPullRequestLinked`-äquivalent (Zeilen 364–371) → `InvalidOperationException`; Anlage `Status = Neu` + `PullRequestReferenz` mit `Rolle = ReviewSource`, `SourceBranch = pullRequest.BranchName`, `SourceRepositoryId` (Zeilen 387–410); `DbUpdateException` → `InvalidOperationException` bei parallel verknüpfter Referenz (Zeilen 418–431). |
| `IsPullRequestLinkedAsync(provider, repositoryId, number, ct)` | public | Globaler Existenzcheck einer `PullRequestReferenz` (normalisierte `RepositoryId`). |
| `CreateFromIssueAsync`, `CreateFromAlertAsync` | public | Andere Anlagepfade (manuell/Alert), unverändert relevant als Referenz. |
| `GetAktiveAufgabenAsync(ct)` | public | „Aktive" Aufgaben für die Seitenleiste: `IstAktivOderWartendPredicate` = `Status ∈ {Gestartet, Wartend}` (ohne `Neu`), max. 20. |
| `GetAktiveUndWartendeCountAsync(ct)` | public | Zählt `Gestartet`/`Wartend` über dasselbe Prädikat. |
| `IstAktivOderWartendPredicate` (Feld, Zeilen 21–22) | private static | EF-fähige Variante von `AufgabeStatusExtensions.AktivOderWartendStatus`. |
| `SetStatusAsync`/`StatusSetzenAsync`, `StartenAsync`, `SetPauseAsync`, `AbschliessenAsync`, `ArchivierenAsync` | public | Lebenszyklus-Pfade; `ValidateStatusTransition` erlaubt `Neu→Gestartet`, `Gestartet→{Wartend,Beendet}`, `Wartend→{Gestartet,Beendet}`; `Archiviert` immer erreichbar. |

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

Dispatch-Pipeline; ändert sich für die Anforderung nicht, liefert aber die „aktiv"-Semantik und den Meldepfad für PR-Verknüpfungen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchAsync(eventId, explicitPluginPrefix, ct)` | public | Liest `AufgabeId` des Events; `ScmTaskCoordinator.Shared.RunAsync(taskId)` → `DispatchCoreAsync`. |
| `OpenFromQueueAsync(eventId, select, ct)` | public | Queue-Aktivierung unter Aufgabenlease: `ClaimTaskAsync`, `BlockReason`-Prüfung, `Neu→Gestartet`, CLI-Auswahl. |
| `DispatchCoreAsync` | private | Receipt-/Waiting-Hostaktionen → Reconcile laufender Versuche → Sequenzreihenfolge → `BlockReason` → Schreibrechte → Trigger-Revalidierung → Session-Management (`cli.GetScmPromptSession`, Start/Neustart via `EntwicklungsprozessService`) → `WaitForInputReadyAsync` („CLI beschäftigt; wartet auf Eingabebereitschaft.") → `ScmEventExchangeProtocol`-Übergabe → `WritePromptAsync`. |
| `BlockReason(task)` | private static | `Beendet`/`Archiviert` → „Aufgabe beendet/archiviert; Start gesperrt."; `PausiertBisUtc > now` → „Aufgabe pausiert; Start gesperrt."; sonst `null` (Zeilen 469–471). **Die de-facto-Aktivdefinition des Dispatch.** |
| `LinkReportedPullRequestAsync(item, observation, number, ct)` | private | `pullRequestNumber` aus `complete.json` → `PullRequestReferenzService.SaveReportedAsync` + Protokolleintrag (Zeilen 447–462). |
| `ReconcileAttemptAsync` | private | Liest Antwortkanal-Meldungen; ruft `LinkReportedPullRequestAsync` bei `Complete.PullRequestNumber > 0` (Zeile 421). |
| `ActivateForOpenAsync`, `RetryAsync`, `ConfirmReadyAsync`, `ResendAsync`, `CancelAsync`, `MutateAndDispatchAsync` | public/private | Manuelle Eingriffe; alle unter `ScmTaskCoordinator`. |
| `WaitAsync`, `WaitWithCommentAsync`, `RenewLeaseAsync` | private | Wartestatus + Host-Kommentar + Lease-Erneuerung. |

## `ScmEventStore`
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode / Konstante | Sichtbarkeit | Kurzbeschreibung |
|---------------------|-------------|------------------|
| `AmbiguousAssignmentDiagnostic` | public const | `"Mehrdeutige Zuordnung zu Aufgaben"` — Snapshots/Checkpoint-Diagnose bei `taskIds > 1` (bzw. `aufgabeId == null && !unassigned`). |
| `UnassignedDiagnostic` | public const | `"Keine Aufgabe zugeordnet"` — bei `unassigned = true`. |
| `InboxDiscardDiagnosticPrefix` | public const | `"Inbox:"` — Verwerfungsdiagnose fremder Notifications. |
| `AcceptBatchAsync(aufgabeId, sourceKey, batch, unassigned, triggerKeyword, ct)` | public | Transaktionale Aufnahme: Snapshots upserten, `ScmIncomingEvent` nur bei `rejection == null` anlegen (mit `aufgabeId!`), Checkpoint-Watermark setzen, `Notify`. |
| `ClaimTaskAsync`/`RenewTaskAsync`/`ReleaseTaskAsync` | public | Instanzübergreifende Aufgabenlease (`ScmTaskLeases`, 60 s). |
| `Observation`, `ActionId`, `RetryAt` | public static | Deserialisierung, deterministische Aktions-IDs, Backoff mit Jitter. |

## `PullRequestReferenzService`
Datei: `src/Softwareschmiede/Application/Services/PullRequestReferenzService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SaveCreatedAsync(aufgabeId, repositoryId, pullRequest, ct)` | public | Upsert beim hostseitigen Anlegen (`GitOrchestrationService`); `Rolle = CreatedByTask`; Fremdzuordnung → `InvalidOperationException`. Setzt `SourceBranch`, `SourceRepositoryId`, `SourceRef`, `TargetBranch`, `HeadSha`. |
| `SaveReportedAsync(aufgabeId, pluginPrefix, repositoryId, pullRequestNumber, url, ct)` | public | Upsert bei Agenten-Meldung (`complete.json`); `Rolle = CreatedByTask`; Fremdzuordnung → `null` + Warnlog. |
| `GetByAufgabeAsync`, `GetDueForMonitoringAsync`, `GetRefreshableByAufgabeAsync` | public | Lese-/Monitoring-Zugriffe. |
| `UpdateFromProviderAsync`, `SetProviderUncertaintyAsync`, `SetRetryableErrorAsync`, `SetProblemAsync`, `SetPhaseAsync` | public | Monitoring-Zustandsmutationen. |

## `ScmTaskCompletionMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync(ct)` | public override | Kandidaten: `Status != Beendet && != Archiviert` **und** mindestens ein `ScmIncomingEvent` (Zeilen 46–50) — eigene Aktivdefinition. |
| `CheckTaskAsync` | private | Offene Ereignisse haben Vorrang; `DetermineClosureAsync` → Countdown (`ScmAbschlussErkanntUtc` + Wartezeit) → `EntwicklungsprozessService.AbschliessenAsync` unter `ScmTaskCoordinator`. |
| `DetermineClosureAsync` | private | `task.PullRequests.Count > 0` hat Vorrang: alle Referenzen müssen `Closed`/`Merged` sein (`CanMonitor` auf `Rolle`+`Provider`); sonst `IssueReferenz.IssueNummer` + `IstGeschlossen` (Zeilen 149–200). Eine früh implizit verknüpfte Referenz würde diese Auswertung beeinflussen. |

## `ScmTaskCoordinator`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCoordinator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Shared` | public static | Prozessweite Instanz (`SemaphoreSlim` je `taskId`, `AsyncLocal`-Besitzerverfolgung). |
| `RunAsync(taskId, operation, ct)` | public | Serialisiert Dispatch, manuelle Starts, Abschluss und Terminaleingaben pro Aufgabe; reentrancy-fähig. |

## `KiAusfuehrungsService` (relevante Ausschnitte)
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetScmPromptSession(aufgabeId)` | public | Laufzeit-Adapter `IScmPromptSessionAdapter` der lebenden CLI-Sitzung (nur bei sicherem Startvertrag); `null` sonst (Zeilen 475–477). |
| `GetTerminalSession(aufgabeId)` | public | Rohe `ITerminalSession` (Dispatch-Unterscheidung „Sitzung ohne Startvertrag"). |

## `EntwicklungsprozessService` (relevante Ausschnitte)
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ErstelleTaskBranchName(aufgabe)` | private static | Branch-Namensmuster: `task/issue-{IssueNummer}-{AufgabeId:N}-{TitelSlug}` mit `IssueReferenz`, sonst `task/{AufgabeId:N}-{TitelSlug}` (Zeilen 833–841) — der Branchname enthält Issue-Nummer **und** Aufgaben-GUID. |
| `ProzessStartenUndCliStartenAsync`, `CliNeustartenAsync` | public | Startpfade aus dem Dispatch. |
| `AbschliessenAsync` | public | Abschlusspfad der Completion-Überwachung. |

## `PullRequestBodyBuilder`
Datei: `src/Softwareschmiede/Application/Services/PullRequestBodyBuilder.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Build(aufgabe, body)` | internal static | Ergänzt `Closes #{IssueNummer}` aus `aufgabe.IssueReferenz` ohne Duplikat (Zeilen 11–30). |
| `BuildFromCommits(aufgabe, commits)` | internal static | Commit-Liste + Closing-Direktive. |
| `ContainsClosingDirectiveForIssue(body, issueNummer)` | private static | Regex `\b(close[sd]?\|fix(e[sd])?\|resolve[sd]?)\s+(owner/repo)?#N\b` — die Erkennungslogik für Issue-Bezüge im PR-Body (Zeile 51). |

## `GitOrchestrationService` (relevanter Ausschnitt)
Datei: `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PullRequestErstellenAsync(aufgabeId, title, body, ct)` | public | Hostseitige PR-Anlage: Push → `CreatePullRequestAsync` → `SaveCreatedAsync` (Zeilen 183–234); PR-Body via `BuildPullRequestBodyAsync` → `PullRequestBodyBuilder`. |

## `ScmEventValidationService` (relevanter Ausschnitt)
Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `DefaultTriggerKeyword` | public const | `"$Softwareschmiede"`. |
| `GetRejection(observation, triggerKeyword)` | public static | Ablehnungsgrund oder `null`; bestimmt den „qualifizierten Trigger", der den Resolver aufruft (Monitoring Zeile 182). |
