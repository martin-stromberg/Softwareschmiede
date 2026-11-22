# Logik — Bestandsaufnahme

## `ScmTaskCompletionMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`
Basisklasse: `PeriodicBackgroundService` (Intervall `ScmEventOptions.PollInterval`, Default 60 s).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `RunOnceAsync` | `public override` | Poll-Zyklus: `startup.WaitAsync`, Laufzeit-Schalter `GetScmTaskCompletionEnabledAsync` (Abbruch wenn aus), liest `wait` (`GetScmTaskCompletionWaitMinutesAsync`) und `checkMode` (`GetScmTaskCompletionCheckModeAsync`); Kandidaten = nicht beendete/archivierte Aufgaben mit SCM-Referenz (`IssueReference != null \|\| PullRequests.Any()`), im Modus `EventDriven` zusätzlich nur Aufgaben mit ≥1 `ScmIncomingEvents`-Eintrag; ruft je Kandidat `CheckTaskAsync`. |
| `CheckTaskAsync` | `private` | **Gate 1 (Z. 84–86):** `AnyAsync(x.WorkItemId == taskId && x.Status != "Verarbeitet" && x.Status != "Abgebrochen")` → `return`. Lädt Aufgabe inkl. `GitRepository`/`IssueReference`/`PullRequests`; `DetermineClosureAsync` → geschlossene Referenz oder `null`. Referenz wieder offen → `ScmCompletionDetectedUtc`/`ScmCompletionReference` zurücksetzen + `SystemMessage`-Log. Erst-Erkennung → `ScmCompletionDetectedUtc = now` + Log. Wartezeit nicht abgelaufen → `return`. Abschluss unter `ScmTaskCoordinator.Shared.RunAsync`: Reload, erneute Status-/Zeit-Prüfung, **Gate 2 (Z. 133–135):** identische `AnyAsync`-Bedingung → `return`; dann `DevelopmentProcessService.CompleteAsync` + Log. |
| `DetermineClosureAsync` | `private` | PR-Vorrang: alle `task.PullRequests` müssen `Closed`/`Merged` sein (`Merged` wird nicht erneut geprüft); Plugin-Auflösung via `ResolvePlugin`, `PullRequestMonitoringPolicy.CanMonitor`; aktualisiert `Status`/`MergeStatus`/`LastCheckedUtc`/`Url`. Ohne PRs: geschlossenes Issue via `IScmEventSource.ReadIssueAsync` unter `ScmAccountMode.UserAccount`. Rückgabe: Anzeigelabel oder `null`. |
| `ResolvePlugin` | `private static` | Plugin per `PullRequestProviderDescriptor.GetPluginPrefix(provider)`, Fallback `ScmPluginMatcher.MatchesPlugin`. |

Events: keine publizierten; liest `ScmIncomingEvents`, schreibt `WorkItems`, `PullRequestReferences`, `LogEntries` (via `LogService`).

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` (1051 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `DispatchDueEventsAsync` | `public` | Startprüfung/Poll: wählt fällige, nicht-`Abgebrochen`-Ereignisse (nicht-`Verarbeitet` mit fälligem `NextAttemptUtc` **oder** offene `ScmOutgoingActions`), priorisiert via `ScmDispatchPriority.OrderForDispatch`, ruft `DispatchAsync` je Ereignis. Aufgerufen aus `ScmEventMonitoringService.RunOnceAsync` (Z. 247) und `ScmStartCheckService`. |
| `DispatchAsync` | `public` | Einstiegspunkt; serialisiert über `ScmTaskCoordinator.Shared.RunAsync` → `DispatchCoreAsync`. |
| `OpenFromQueueAsync` | `public` | Queue-Aktivierung unter Lease (`ClaimTaskAsync`/`RenewLeaseAsync`/`ReleaseTaskAsync`); CLI-Auswahl per Callback, dann `DispatchCoreAsync`. |
| `DispatchCoreAsync` | `private` | Kern-Dispatch: `Abgebrochen`-Abbruch; `Purpose == ScmIssuePreparationService.Purpose` → `DispatchPreparationAsync`; Hostaktionen (receipt + nicht-`ai:`-Outbox) nachholen; `DeliveryAttemptId != null` → `ReconcileAttemptAsync` (Fehler → `VerarbeitungUngeklärt`); `NextAttemptUtc`-Frist; **Reihenfolge-Regel (Z. 182):** `AnyAsync(Sequence < item.Sequence && Status != "Verarbeitet" && != "Abgebrochen")` → `WaitWithCommentAsync` „Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden."; `BlockReason`-Prüfung, Schreibrechte, Trigger-Revalidierung, CLI-Start (`executionLimits.CheckAndReserveStartSlotAsync`, `StartProcessAndCliAsync`/`RestartCliAsync`), `WaitForInputReadyAsync`, `CheckExchangeAccessAsync`, `exchange.PrepareAsync`, `adapter.WritePromptAsync` → Status `Zugestellt`/`ZustellungUngewiss`. |
| `ActivateForOpenAsync` | `public` | Aktiviert neue, unpausierte Aufgabe beim Öffnen. |
| `RetryAsync` | `public` | Setzt `NextAttemptUtc`/`ErrorCode` und offene Outbox-/Checkpoint-Fristen zurück, dann `MutateAndDispatchAsync`. |
| `ConfirmReadyAsync` | `public` | Run-gebundene Bereit-Bestätigung; hebt `NextAttemptUtc`-Lock auf. |
| `ResendAsync` | `public` | Neuversand nur für `ZustellungUngewiss`/`VerarbeitungUngeklärt` mit passender `DeliveryAttemptId`; setzt Status zurück auf `"Aufgenommen"` und leert Versuchsfelder. |
| `CancelAsync` | `public` | Atomarer Übergang → `"Abgebrochen"` (`ExecuteUpdate`), stuft Direktiven-Pause zurück, `notifier.Notify()` + `startCheckTrigger?.RequestStartCheck()`. |
| `MutateAndDispatchAsync` | `private` | Mutation + `DispatchCoreAsync` unter Aufgabenlease. |
| `ReconcileAttemptAsync` | `private` (Z. 485–600) | Abgleich des Versuchskanals: baut `ScmExchangeRequest`/`ScmExchangeAttempt` aus persistierten Feldern; `exchange.ReadAsync` → `ScmExchangeMessages`. `Diagnostic` → `VerarbeitungUngeklärt`. **`if (messages.Accepted is not null)`-Block (Z. 502–580):** `AcceptedUtc`, Status `InBearbeitung`; `messages.Actions` → Outbox (`EnsureAsync`/`ExecuteAsync`/`WriteResultAsync`), Kollision/Fehler → `VerarbeitungUngeklärt`; `messages.Complete` + Status `InBearbeitung` → Mengenprüfung der Aktions-IDs → `Verarbeitet` + Attachments-Einsammlung (`CollectAttachmentsAsync`, Host-Aktion `attachments`); **`LinkReportedPullRequestAsync` (Z. 578–579)** — Aufruf liegt innerhalb des `Accepted`-Blocks, ausgewertet wird `messages.Complete is { PullRequestNumber: > 0 }`. Nach dem Block (Z. 581–599): `ZustellungVorbereitet` → `ZustellungUngewiss`; `Zugestellt`/`InBearbeitung` + Timeout (`AcceptanceTimeout`/`CompletionTimeout`) oder Sitzung weg → `VerarbeitungUngeklärt` („Annahme fehlt oder CLI-Sitzung beendet." / „Korrelierter Abschluss fehlt oder CLI-Sitzung beendet."). |
| `DispatchPreparationAsync` | `private` (Z. 607–804) | Spiegelbildlicher Pfad für `Purpose == "IssueAufbereitung"`: eigener Analyse-Prompt (`ScmIssuePreparationPrompt.Build`), eigene Schalter (`GetScmIssuePreparationEnabledAsync`, `GitRepository.IssuePreparationEnabled`, `GetScmIssuePreparationPluginPrefixAsync`), kein Wartehinweis-/Fehlerkommentar. |
| `ReconcilePreparationAttemptAsync` | `private` (Z. 824–971) | Abgleich des Aufbereitungsversuchs: `Accepted` → `InBearbeitung`; Aktionen nur `issueBodyUpdate`/`comment` (Kommentar-Schalter `GetScmIssuePreparationCommentEnabledAsync`); `Complete` → `Verarbeitet` bei `NoRemoteAction` bzw. bestätigtem `IssueBodyUpdate` (+ Kommentar wenn erlaubt), sonst `VerarbeitungUngeklärt`. **`ScmExchangeComplete.PullRequestNumber` wird hier nicht ausgewertet.** |
| `LinkReportedPullRequestAsync` | `private` (Z. 976–991) | Ruft `pullRequestReferences.SaveReportedAsync` mit Repository-/Provider-Daten aus der gespeicherten Observation; schreibt `LogEntryType.GitAction`-Eintrag (verknüpft vs. fremd zugeordnet). |
| `IsCancelledAsync` | `private` | Persistenz-Abbruchprüfung (`Status == "Abgebrochen"`). |
| `BlockReason` | `private` | `Completed`/`Archived`/pausiert → Wartegrund. |
| `WaitAsync`/`WaitWithCommentAsync` | `private` | Status `"Wartend"` + `Reason`; Variante mit Host-`waiting`-Kommentar. |
| `EnsureHostActionAsync` | `private` | `outbox.EnsureAsync` für Host-Aktionen; `ScmActionIdCollisionException` → `VerarbeitungUngeklärt`. |
| `RenewLeaseAsync` | `private` | 15-s-`ScmTaskLease`-Erneuerung während Versuch. |

## `ScmEventExchangeProtocol`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs` (743 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `PrepareAccessProbeAsync`/`ReadAccessProbeAsync`/`BuildAccessProbePrompt` | `public`/`public`/`public static` | Run-gebundene Dateitool-Probe (challenge/proof) vor dem ersten Versuch. |
| `PrepareAsync` | `public` | Legt Versuchsverzeichnis `{workdir}/.softwareschmiede-scm-exchange/{runId}/{attemptId}` mit `actions/`, `results/`, `screenshots/` an; schreibt `request.json` atomar. |
| `ReadAsync` | `public` (Z. 335–380) | Liest `accepted.json`, `actions/*.json` und `complete.json` **unabhängig voneinander** (`ReadStableAsync`), jeweils mit Korrelation (`Correlate`: Version/EventId/AttemptId/CliRunId) und Validierung; `complete.json` braucht `actionIds`+Duplikat-/Leer-Checks bzw. `report`/`noRemoteAction`+`reason`; `pullRequestNumber <= 0` macht den Abschluss ungültig. Fehler → `ScmExchangeMessages(null, [], null, Diagnostic)`. |
| `CollectAttachmentsAsync` | `public` | Sammelt deklarierte + abgelegte Nachweis-Bilder fehlertolerant (Gründe in `Skipped`). |
| `WriteResultAsync` | `public` | Atomares `results/{actionId}.json`; erfolgreiche Ergebnisse unveränderlich. |
| `BuildPrompt` | `public static` | Prompt-Envelope inkl. `completeSchema` mit `pullRequestNumber`. |

Records derselben Datei: `ScmExchangeRequest`, `ScmExchangeAttempt` (`RequestPath`, `AttachmentsPath`, `Prompt`), `ScmExchangeAccepted`, `ScmExchangeAction`, `ScmExchangeAttachment`, `ScmExchangeComplete` (u. a. `ActionIds`, `NoRemoteAction`, `Reason`, `Report`, **`PullRequestNumber`**, `Attachments`), `ScmExchangeActionResult`, **`ScmExchangeMessages(Accepted, Actions, Complete, Diagnostic)`**, `ScmCollectedAttachment`, `ScmAttachmentCollection`, `ScmExchangeAccessProbe`, `ScmExchangeAttachmentsConverter` (toleranter `attachments`-Parser).

## `PullRequestReferenceService`
Datei: `src/Softwareschmiede/Application/Services/PullRequestReferenceService.cs` (scoped)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `SaveCreatedAsync` | `public` | Persistiert/aktualisiert vom Host erstellten PR; fremd zugeordnete Referenz → `InvalidOperationException`. |
| `SaveReportedAsync` | `public` (Z. 93–149) | **Idempotente Registrierung** der `pullRequestNumber` aus `complete.json`: Provider aus `pluginPrefix` (`PullRequestProviderDescriptor`, Fallback `GitHub`), `RepositoryId` normalisiert; existiert Referenz derselben Aufgabe → ergänzen; fremd zugeordnet → `null` + Warnlog; `Status == Unknown` → `Open`. Wirft `ArgumentOutOfRangeException` bei `pullRequestNumber <= 0`. |
| `GetByWorkItemAsync`/`GetRefreshableByWorkItemAsync`/`GetDueForMonitoringAsync` | `public` | Lesepfade für UI/Monitoring. |
| `UpdateFromProviderAsync`/`SetProviderUncertaintyAsync`/`SetRetryableErrorAsync`/`SetProblemAsync`/`SetPhaseAsync` | `public` | Monitoring-Statusupdates (PR-Monitoring, nicht Teil des Ereignisflusses). |

## Weitere beteiligte Klassen

- **`ScmEventExchangeService`** (`Application/Services/ScmEventExchangeService.cs`): 2-s-Poller; wählt `DeliveryAttemptId != null && Status != "Verarbeitet" && != "Abgebrochen"` → `DispatchAsync` (Zeile 27). Treibt damit auch `VerarbeitungUngeklärt`-Ereignisse erneut in `ReconcileAttemptAsync`.
- **`ScmEventMonitoringService`** (`Application/Services/ScmEventMonitoringService.cs`): SCM-Poll; ruft `DispatchDueEventsAsync` (Z. 247).
- **`ScmEventStore`** (`Application/Services/ScmEventStore.cs`): `AcceptBatchAsync` legt Ereignisse an (Default-Status `Aufgenommen`); Verzögerungsdirektive → Status `"Verzögert"` + `NextAttemptUtc` (Z. 101) + optional `PauseForDelayAsync`. Hilfsfunktionen: `Observation`, `ActionId`, `RetryAt`, `JsonOptions`, Diagnose-Konstanten (`UnassignedDiagnostic`, `AmbiguousAssignmentDiagnostic`, `InboxDiscardDiagnosticPrefix`).
- **`ScmTaskCoordinator`** (`Application/Services/ScmTaskCoordinator.cs`): `Shared`-Singleton; `RunAsync` serialisiert je `taskId` über `SemaphoreSlim`-Gates mit `AsyncLocal`-Reentrancy (Dispatch, manuelle Starts, Abschluss teilen dieselbe Sperre).
- **`AppSettingService`** (`Application/Services/AppSettingService.cs`): Konstanten `ScmTaskCompletionEnabledKey` (Z. 76), `ScmTaskCompletionWaitMinutesKey` (Z. 79), `ScmTaskCompletionCheckModeKey` (Z. 103), `ScmTaskCompletionDefaultWaitMinutes = 60` (Z. 82); `GetScmTaskCompletionEnabledAsync` (Default `true`), `GetScmTaskCompletionWaitMinutesAsync` (Clamp 0–43200), `GetScmTaskCompletionCheckModeAsync`/`SetScmTaskCompletionCheckModeAsync`; weitere `scm.*`-Getter (`GetScmMonitoringEnabledAsync`, `GetScmTriggerKeywordAsync`, `GetScmIssuePreparation*`, `GetScmPromptAppendicesAsync`).
- **`ScmDispatchPriority`** (`Application/Services/ScmDispatchPriority.cs`): statisch; `PriorityClass`/`OrderForDispatch` — Status `"Verzögert"` ist Prioritätsklasse 2.
- **`RunningAutomationsCount`**: zählt `Status == "Verzögert"` (Z. 60).
- **`MainWindowViewModel`**: `RefreshScmCountAsync` zählt `Status != "Verarbeitet" && != "Abgebrochen"` (Z. 393).
- **`DevelopmentProcessService.CompleteAsync`**: kanonischer Aufgaben-Abschluss (CLI-Stopp, Prompt-Rückzug, Klon-Löschung) — vom Abschlusspfad aufgerufen.
- **`PeriodicBackgroundService`**: Basisklasse der Poll-Dienste.

## `ScmEventQueueViewModel` (+ `ScmEventQueueItem`)
Datei: `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`

| Member | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `RefreshAsync` | `public` | Lädt Ereignisse gefiltert: `Wartend` = `Status != "Verarbeitet" && != "Abgebrochen"` (Z. 175); `Verarbeitet`/`Abgebrochen` exakt (Z. 176–177). Baut Diagnosezeilen aus `ScmObservationCheckpoints` + offenen `ScmOutgoingActions`. |
| `RefreshCommand`/`RetrySourcesCommand` | `public` | Aktualisieren / Quellen, Backmerge- und Aufbereitungszyklus anstoßen. |
| `OpenEventCommand` | `public` | Aktiv nur für `{ IsFinal: false, IsBlocked: false, AttemptId: null }` → `OpenFromQueueAsync` + CLI-Auswahl. |
| `RetryCommand` | `public` | `{ IsFinal: false }` oder `{ CanRetryProcessed: true }` → `RetryAsync`. |
| `CheckStatusCommand` | `public` | `{ IsFinal: false, AttemptId: not null }` → `DispatchAsync` (Abgleich ohne Neuversand — deckt `VerarbeitungUngeklärt` ab). |
| `CancelCommand` | `public` | `{ IsFinal: false }` → `CancelAsync` nach Bestätigungsdialog. |
| `ConfirmReadyCommand` | `public` | `{ CanConfirmReady: true }` → `ConfirmReadyAsync`. |
| `ResendCommand` | `public` | `{ CanResend: true, IsBlocked: false }` → `ResendAsync` nach Doppelbearbeitungs-Warnung. |
| `ScmEventQueueItem.IsProcessed`/`IsFinal`/`CanRetryProcessed`/`CanCancel`/`CanResend`/`CanConfirmReady` | `public` (record, Z. 379–411) | `IsFinal = Status is "Verarbeitet" or "Abgebrochen"`; `CanResend = AttemptId != null && Status is "ZustellungUngewiss" or "VerarbeitungUngeklärt"`; `From` mappt Status→Anzeigetext und Aufgaben-Blockzustand. |

## Dokumentationsstellen
- `docs/help/plugins/github-ereignisse/business-rules.md` Abschnitt „Abschlussüberwachung von Aufgaben mit SCM-Referenz" (Z. 112–114): dokumentiert pauschal „die Ereignisverarbeitung einer Aufgabe [muss] abgeschlossen sein"; Z. 104 beschreibt `pullRequestNumber`-Verknüpfung im Abschlusspfad.
- Weitere Kandidaten laut Anforderung: `ablauf-anwender.md`, `ablauf-technisch.md`, `datenmodell.md`, `troubleshooting.md` unter `docs/help/plugins/github-ereignisse/` sowie `docs/help/aufgaben/datenmodell.md`.
