# Logik — Bestandsaufnahme

Logikklassen des SCM-Ereigniskanals im Neuanlage-Szenario (Issue-Neuanlage + Trigger-Kommentar) und des Zustellpfads in eine laufende CLI. Zeilenangaben beziehen sich auf den dokumentierten Stand (Commit `c084897`).

## `ScmEventTaskResolver`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`

Löst eine SCM-Referenz zu genau einer lokalen Aufgabe auf (DI: scoped). Läuft **vor** `ScmEventStore.AcceptBatchAsync` im Poll-Zyklus des `ScmEventMonitoringService` (~Zeile 197).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync` | `public` | Dispatcht auf `ResolveIssueAsync` bzw. `ResolvePullRequestAsync` je `reference.Kind`. |
| `ResolveIssueAsync` | `private` | **Kern der Duplikat-Ursache** (Zeilen 52–75): Liest das Issue via `source.ReadIssueAsync`; sucht Aufgaben mit `repositoryIds.Contains(a.GitRepositoryId) && a.Purpose == null && a.IssueReference.IssueNumber == reference.Number` (Zeilen 63–67). Der `Purpose == null`-Filter schließt die zweck-markierte Aufbereitungs-Aufgabe aus ⇒ 0 Treffer ⇒ Neuanlage via `workItems.CreateFromIssueEventAsync` mit kanonischem `IssueSourceKey` (`…/Issue/{n}`). Ergebnisse: `Ambiguous` (>1), `Assigned` (1 oder neu angelegt), `Unassigned` (Issue nicht lesbar). |
| `ResolvePullRequestAsync` | `private` | Analoge PR-Auflösung über `PullRequestReferences`, danach Kontextabgleich (`FindActiveContextTaskIdsAsync`); Anlagerennen wird über den Unique-Index gefangen (erneute Abfrage statt Duplikat). |
| `PullRequestTaskIdsAsync` | `private` | Aufgaben-IDs über `db.PullRequestReferences`. |
| `FindActiveContextTaskIdsAsync` | `private` | Kontextkandidaten für PRs (Branch- oder Closing-Issue-Match) — ebenfalls mit `a.Purpose == null`-Filter (Zeile 135) und Aktiv-Status-Filter. |
| `MatchesBranch` / `MatchesClosingIssue` | `private static` | Vergleichshelfer (OrdinalIgnoreCase im Speicher, da SQLite kein `OrdinalIgnoreCase` übersetzt). |
| `ChooseRepository` | `private static` | Deterministische Repository-Wahl (URL, dann Id). |
| `ComposeBody` | `private static` | Auftragstext aus Referenzbeschreibung + auslösender Beobachtung + Herkunftsverweis. |
| `IssueSourceKey` | `private static` | Kanonischer `…/Issue/{number}`-Dedup-Schlüssel (gleicher Namensraum wie `ScmEventObservation.ContentKey`, ohne `ContentKind`/`RemoteNodeId`). |

Ergebnistypen in derselben Datei: `ScmTaskResolutionKind` (`Assigned`, `Ambiguous`, `Unassigned`) und `ScmTaskResolution` (Record mit `Kind`, `WorkItemId`, statischen `Ambiguous`/`Unassigned`, Fabrik `Assigned`).

## `ScmIssuePreparationService`
Datei: `src/Softwareschmiede/Application/Services/ScmIssuePreparationService.cs`

„Issue erstellt"-Erkennung und Einreihung des Aufbereitungsauftrags (DI: scoped).

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `Purpose` | `public const string` | Einziger Zweck-Token des Systems: `"IssueAufbereitung"` (Zeile 30). |
| `BaselineKey` | `public static` | Checkpoint-Schlüssel `issueaufbereitung/{provider}/{host}/{repoId}` des Beobachtungsbeginns. |
| `PreparationSourceKey` | `public static` | SourceKey der Aufbereitungs-Aufgabe `…/IssueAufbereitung/{n}` — eigener Namensraum, kollidiert nie mit dem kanonischen `…/Issue/{n}` (Zeilen 43–47). |
| `PreparationEventKey` | `public static` | Dedup-EventKey `{ContentKey}/IssueAufbereitung` des internen Ereignisses (Zeilen 52–53). |
| `EvaluationCheckpointKey` | `public static` | Je-Issue-Checkpoint `issueaufbereitung-issue/…/{n}`; Endzustand = `NextAttemptUtc = long.MaxValue` ohne Fehler. |
| `SkipEvaluationAsync` | `internal` | „Bereits ausgewertet"-Prüfung (Checkpoint vorhanden ohne offenen Fehler oder Backoff noch nicht erreicht). |
| `CreateBaselineAsync` | `internal` | Legt Baseline-Watermark an und markiert Bestandsreferenzen als ausgewertet (kein Retro-Scan). |
| `CompleteEvaluationAsync` | `internal` | Schreibt den Auswertungs-Checkpoint in den Endzustand. |
| `DetectAndEnqueueAsync` | `public` | Kernpfad (Zeilen 145–217): Baseline-First-Run, Altbestand-Filter (`CreatedAt <= baselineUtc`), EventKey-Dedup, `ReadIssueAsync`, dann `workItems.CreateFromIssueEventAsync(…, PreparationSourceKey(reference), ct, Purpose, pluginPrefix)` + internes `ScmIncomingEvent` mit `Purpose = "IssueAufbereitung"`, `NextAttemptUtc = null` (sofort fällig), Systemprotokoll-Eintrag. `DbUpdateException` am EventKey-Unique-Index = Anlagerennen geduldet. Abschluss via `CompleteEvaluationAsync`. |

Aufgerufen von: `ScmEventMonitoringService.RunOnceAsync` (~Zeile 210, Description-Batch) und `ScmIssuePreparationMonitoringService.EvaluateIssueAsync` (Zeile 213).

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` (1115 Zeilen)

Gemeinsamer Dispatch für Poll und Queue (DI: scoped; je Ereignis eigener Scope in `DispatchDueEventsAsync`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchDueEventsAsync` | `public` | Batch-Auswahl fälliger Ereignisse (Sequence-Fenster, `Status != Abgebrochen`, `!= Verarbeitet` mit `NextAttemptUtc <= now` oder offene Outbox-Aktionen), Sortierung über `ScmDispatchPriority.OrderForDispatch`, danach je Ereignis `DispatchAsync` in neuem Scope (Zeilen 34–66). Aufgerufen von `ScmEventMonitoringService`, `ScmIssuePreparationMonitoringService` und `ScmStartCheckService`. |
| `DispatchAsync` | `public` | Aufgaben-ID ermitteln und `DispatchCoreAsync` unter `ScmTaskCoordinator.Shared`-Sperre ausführen. |
| `OpenFromQueueAsync` | `public` | Queue-Öffnen: Claim + Lease-Renewal, Blocked-/Statusprüfung, explizite Plugin-Auswahl, dann `DispatchCoreAsync`. |
| `DispatchCoreAsync` | `private` | Generischer Zustellpfad (Zeilen 120–359): Abbruch-Check → **`item.Purpose == ScmIssuePreparationService.Purpose` ⇒ Umleitung nach `DispatchPreparationAsync`** (Zeilen 136–140) → Plugin-Auflösung → Hostaktionen (`receipt`, nicht-`ai:`-Outbox) → `DeliveryAttemptId` ⇒ `ReconcileAttemptAsync` → `NextAttemptUtc`-Härte → **Vorgängerprüfung** (Zeile 183: früheres Ereignis derselben Aufgabe mit nicht-`NonBlocking`-Status ⇒ `WaitWithCommentAsync`) → `BlockReason` → Schreibberechtigung + Trigger-Revalidierung → CLI-Sitzung (`cli.GetScmPromptSession`) → ggf. **Slot-Admission `executionLimits.CheckAndReserveStartSlotAsync`** (Zeile 246) + Plugin-Health-Check + Start (`StartProcessAndCliAsync`/`RestartCliAsync`) → `WaitForInputReadyAsync` (Zeile 277) mit Wartegrund-Verzweigung (`TrustPrompt`/`IsBusy`/`CliRecognition`) → `IsBusy`-Doppelcheck (Zeile 292) → `CheckExchangeAccessAsync` → `exchange.PrepareAsync` + Persistenz (`ZustellungVorbereitet`, `DeliveryAttemptId`, `DeliveredUtc`, `Attempts++`) → **`adapter.WritePromptAsync`** (Zeile 320) → `Zugestellt`; bei Schreibfehler `ZustellungUngewiss`. Fehlerpfade: `ScmProviderException` ⇒ `RetryAt`-Backoff bzw. `long.MaxValue`; `InvalidOperationException`/`IO` ⇒ `long.MaxValue` + Kommentar. |
| `ActivateForOpenAsync` | `public` | Öffnen aktiviert eine neue, unpausierte Aufgabe (`New` ⇒ `Started`). |
| `RetryAsync` / `ConfirmReadyAsync` / `ResendAsync` / `CancelAsync` | `public` | Benutzeraktionen über `MutateAndDispatchAsync` (Claim + Lease + Mutation + `DispatchCoreAsync`). `CancelAsync` setzt `Abgebrochen` atomar per `ExecuteUpdateAsync`, stuft Verzögerungspausen zurück und triggert `startCheckTrigger`. |
| `ReconcileAttemptAsync` | `private` | Abgleich laufender Versuche (accepted/actions/complete, Attachments-Kommentar, PR-Verknüpfung, Timeouts `AcceptanceTimeout`/`CompletionTimeout`). |
| `DispatchPreparationAsync` | `private` | Dedizierter Aufbereitungspfad (Zeilen 615–833), spiegelt `DispatchCoreAsync`: Hostaktionen → Reconcile (`ReconcilePreparationAttemptAsync`) → `NextAttemptUtc`-Härte → Funktions-/Repository-Schalter (`GetScmIssuePreparationEnabledAsync`, `GitRepository.IssuePreparationEnabled`) → Schreibrecht + Issue-Stand (`IsClosed` ⇒ `Verarbeitet` ohne Änderung) → **Slot-Admission** (Zeile 713) + Start → `WaitForInputReadyAsync` (Zeile 741, gleiche Wartegrund-Verzweigung) → `IsBusy`-Check (Zeile 754) → `CheckExchangeAccessAsync` → letzte Lebendprüfung des Issues → `exchange.PrepareAsync` mit `ScmIssuePreparationPrompt.Build` → **`adapter.WritePromptAsync`** (Zeile 796) → `Zugestellt` + **`task.Kind = WorkItemKind.Instruction`** (Zeile 804). **Keine Vorgängerprüfung** (Zeile 183 existiert hier nicht) und keine `waiting`-Hostkommentare (nur `WaitAsync`). |
| `ReconcilePreparationAttemptAsync` | `private` | Aufbereitungs-Abgleich (Zeilen 853–1021): `issueBodyUpdate`/`comment`-Aktionen, Kommentar-Schalter hostseitig erzwungen, Abschlusskriterien (≥1 erfolgreiches `IssueBodyUpdate`; bei aktivem Schalter zusätzlich ≥1 `Comment`), Timeout-Logik wie generisch. |
| `LogPreparationErrorAsync` | `private` | Ehrlicher Protokolleintrag (dedupliziert nach Grund), kein Issue-Kommentar. |
| `LinkReportedPullRequestAsync` | `private` | PR-Verknüpfung aus Abschlussmeldung. |
| `IsCancelledAsync` | `private` | Persistenz-Statusprüfung gegen getrackten Zustand. |
| `BlockReason` | `private` | `Completed`/`Archived` oder aktive `PausedUntilUtc` ⇒ Sperrgrund. |
| `WaitAsync` / `WaitWithCommentAsync` | `private` | Setzt `ScmIncomingEventStatus.Wartend` + `Reason` (Letztere zusätzlich deterministischen `waiting`-Hostkommentar über die Outbox). |
| `EnsureHostActionAsync` | `private` | Outbox-Hostaktion mit deterministischer ID; `ScmActionIdCollisionException` ⇒ `VerarbeitungUngeklärt`. |
| `RenewLeaseAsync` | `private` | Lease-Verlängerung alle 15 s; Verlust ⇒ Abbruch des Versuchs. |

Publizierte Signale: `startCheckTrigger.RequestStartCheck()` bei Endstatus (`Verarbeitet`/`Abgebrochen`) und in `CancelAsync`; `notifier.Notify()` im `finally`.

## `ScmDispatchPriority`
Datei: `src/Softwareschmiede/Application/Services/ScmDispatchPriority.cs`

Statische Priorisierung der Dispatch-Reihenfolge.

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `EventSelection` | `public sealed record` | Auswahlzeile `(Id, WorkItemId, Sequence, Status, NextAttemptUtc)` — **enthält kein `Purpose`-Feld**; Projektion in `DispatchDueEventsAsync` Zeile 48. |
| `WorkItemState` | `public sealed record` | `(Status, ExecutionStatus, PausedUntilUtc)` der zugehörigen Aufgabe. |
| `PriorityClass` | `public static` | Klasse 1 = Fortsetzung (pausiert oder `ExecutionStatus.Completed` bei `New`/`Started`/`Waiting`), Klasse 2 = `Verzögert`, Klasse 3 = übrige. **Kein Purpose-Vorrang** vorhanden. |
| `OrderForDispatch` | `public static` | `OrderBy(Class).ThenBy(Klasse 2: NextAttemptUtc).ThenBy(Sequence)` — stabil nach Eingangsreihenfolge. |

## `ScmEventExecutionLimitService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`

Atomare Zulassungskontrolle neuer CLI-Starts (DI: scoped, Reservierungen im Singleton-Pool).

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `GlobalLimitKey` / `PluginLimitKeyPrefix` / `PluginLimitKey` | `public const`/`static` | Einstellungsschlüssel `scm.execution.maxparallel` und `plugins.maxparallel.{prefix}`. |
| `ReservationLifetime` | `internal static` | 5 min Verfall einer Platzreservierung. |
| `CheckAndReserveStartSlotAsync` | `public` | Atomare Limit-Prüfung + Platzbelegung unter `reservationPool.AdmissionGate`; Ergebnis `CliStartSlotAdmission(WaitReason, Reservation)`. Aufrufstellen: `DispatchCoreAsync` Zeile 246, `DispatchPreparationAsync` Zeile 713. |
| `GetStartBlockReasonAsync` | `public` | Gleiche Prüfung ohne Belegung. |
| `GetStartBlockReason` | `internal static` | Reine Auswertung: globales Limit vor Plugin-Limit. |
| `EffectiveOccupancyAsync` | `private` | Handle-Belegung + offene Reservierungen (verfallene entfernt). |
| `GetOccupancyAsync` | `private` | Zählt laufende Handles; Freigaberegeln: pausierte Aufgaben und lebende Sitzungen mit `ReadyStatus == Ready` **ohne** offenes (nicht `Verarbeitet`/`Abgebrochen`) `ScmIncomingEvent` belegen keinen Platz. Plugin-Attribution über `session.PluginPrefix`, Fallback `WorkItem.AiPluginPrefix`. |

Ergebnistypen in derselben Datei: `CliExecutionOccupancy` (mit `ForPlugin`), `CliStartSlotAdmission`.

## `ScmStartSlotReservationPool`
Datei: `src/Softwareschmiede/Application/Services/ScmStartSlotReservationPool.cs`

DI-Singleton mit dem Reservierungsbestand.

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `AdmissionGate` | `internal` | `SemaphoreSlim(1,1)`; serialisiert Check-und-Claim über die Belegungsabfrage. |
| `Reserve` | `internal` | Trägt `StartSlotReservation(pluginPrefix, validUntilUtcMs)` ein. |
| `ActiveReservations` | `internal` | Gültige Reservierungen; verfallene werden entfernt. |
| `StartSlotReservation` | `internal sealed` | `IDisposable`; `Dispose` entfernt den Platz non-blocking. |

## `ScmEventStore`
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

Atomare Inbox-Aufnahme, Retry-Planung, Aufgabenleases (DI: scoped).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AcceptBatchAsync` | `public` | Transaktionale Aufnahme eines `ScmEventBatch` (Zeilen 50–129): Checkpoint/Snapshot-Verwaltung, `ScmEventValidationService.GetRejection` je Beobachtung, EventKey-Dedup, `ScmIncomingEvent`-Anlage mit `WorkItemId` (oder Diagnose `AmbiguousAssignmentDiagnostic`/`UnassignedDiagnostic`), Verzögerungsdirektive (`ScmDelayDirective` ⇒ `Verzögert` + `NextAttemptUtc` + `waiting`-Hostkommentar + `PauseForDelayAsync` + `RequestStartCheck`). **Nimmt auch Ereignisse auf, deren Aufgabe `Purpose != null` trägt** — der WorkItemId-Parameter wird ungeprüft übernommen. |
| `Observation` / `ActionId` / `RetryAt` | `public static` | JSON-Deserialisierung, deterministische Aktions-ID (SHA-256-Kürzel), Backoff 30 s/2 min/10 min/30 min + Jitter. |
| `ClaimTaskAsync` / `RenewTaskAsync` / `ReleaseTaskAsync` | `public` | Aufgaben-Lease über `ScmTaskLease` (60 s, atomares Upsert). |
| `EnqueueDelayComment` / `PauseForDelayAsync` | `private` | `waiting`-Outbox-Kommentar; Pause nur für `New`/`Started`/`Waiting`, niemals verkürzend. |

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

Poll-Dienst (`PeriodicBackgroundService`, `options.Value.PollInterval`, DI: Singleton + HostedService).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` | `public override` | Zyklus (Zeilen 30–252): Laufzeit-Schalter → verknüpfte Referenzen **nur regulärer Aufgaben** (`x.Purpose == null`, Zeile 51 — Aufbereitungs-Aufgaben ziehen keine Ereignisse auf sich) → Alias-Gruppen → Repository-Auflösung (eigene Checkpoints, `retryPermanent`) → Enumerations- bzw. Inbox-Modus → je Referenzgruppe und Quell-Kind **`Description` vor `IssueComment`** (Zeilen 170–174) → Read + ggf. `ScmEventTaskResolver.ResolveAsync` → **`ScmIssuePreparationService.DetectAndEnqueueAsync` vor `ScmEventStore.AcceptBatchAsync`** (Zeilen 206–214) → am Ende `ScmEventOrchestrator.DispatchDueEventsAsync` (Zeile 251). Im Inbox-Modus (SeparatesKonto) läuft die Aufbereitungs-Erkennung hier **nicht** (`!inboxMode`-Bedingung Zeile 206). |
| `BuildAliasGroups` | `private` | Kanonische Alias-Gruppierung verknüpfter Referenzen + aktiver Repositories. |
| `EnumerateNotificationsAsync` | `private` | Inbox-Modus: `ScmNotification.MentionReason`-Filter, Node-ID→Repository-Auflösung, Watermark-Commit erst nach fehlerfreiem Zyklus. |
| `IsInaccessible` | `private static` | `Permanent`/`Permission` ⇒ Repository für das Konto unsichtbar. |

## `ScmIssuePreparationMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmIssuePreparationMonitoringService.cs`

Eigenständiger Entdeckungszyklus der Aufbereitung (eigener `PeriodicBackgroundService`, gleicher `PollInterval`); läuft auch im SeparatesKonto-Modus und bei deaktivierter Ereignisüberwachung — immer unter `ScmAccountMode.UserAccount` für die Entdeckung, Referenz ggf. aufs Ausführungskonto umgetaggt (`EvaluateIssueAsync`, Zeilen 199–210). Stößt am Ende selbst `DispatchDueEventsAsync` an (Zeile 138).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` | `public override` | Funktionsschalter → freigeschaltete Repositories → Alias-Gruppen → Auflösung (`issueaufbereitung-repository/…`-Checkpoints) → Enumeration (`issueaufbereitung-enumeration/…`) → `EvaluateDiscoveredIssuesAsync` → Dispatch. |
| `EvaluateDiscoveredIssuesAsync` | `private` | Erstlauf: Baseline + alle Referenzen ausgewertet; danach je Issue `EvaluateIssueAsync` (Fehler isoliert per Checkpoint-Backoff). |
| `EvaluateIssueAsync` | `private` | `SkipEvaluationAsync` → Konto-Umtag → `ReadEventsAsync(Description)` → `DetectAndEnqueueAsync` → `CompleteEvaluationAsync`. |

## `ScmStartCheckTrigger` / `ScmStartCheckService`
Datei: `src/Softwareschmiede/Application/Services/ScmStartCheckService.cs`

Ereignisgesteuerte Startprüfung bei freiwerdendem Ausführungs-Slot.

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `ScmStartCheckTrigger.Requested` / `RequestStartCheck` | `public event`/`public` | Non-blocking-Signal; ohne Abonnenten wirkungslos (Poll-Fallback). Auslöser: Pausen-Setzpfade, `AiPluginLimitService.ProcessRateLimitAsync`, `ScmEventOrchestrator.CancelAsync`/Endstatus, **`IScmPromptSessionAdapter.ReadyStatusChanged` → `Ready`** (verdrahtet in `AiExecutionService` Zeilen 388–396). |
| `ScmStartCheckService` | `public sealed` | `BackgroundService`; `RunOnceAsync` = Laufzeit-Schalter + `DispatchDueEventsAsync`. Sammelphase `CollectionPeriod` (250 ms, testbar via `TimeProvider`), `SemaphoreSlim(0,1)`-Koaleszierung, Auslöser `Requested` + `IRunningAutomationStatusSource.RunningCountChanged` (nur sinkende Zählung). |

## `ScmPromptSessionAdapter` / `IScmPromptSessionAdapter`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`

Rungebundener Adapter für sichere SCM-Texteingabe (Implementierung in `AiExecutionService` pro Handle erzeugt). Interface-Dokumentation siehe [interfaces.md](interfaces.md).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsAlive` / `IsBusy` / `ReadyStatus` / `CliRecognition` | `public` | Lebens-/Bereitschaftsnachweise; `IsBusy ⇔ ReadyStatus == Busy`; `ReadyStatus` liefert `Exited` bei toter Sitzung. |
| `WaitForInputReadyAsync` | `public` | Wartet auf ein echtes Promptsignal: `RefreshScreen`, Sofortrückgabe bei `Ready`/`Exited`, sonst `Task.WhenAny(_changed, Task.Delay(2s))` — **eine einzelne 2-Sekunden-Wartephase, kein stabiler Dauerzustand, keine 10-s-Frist**. |
| `CheckExchangeAccessAsync` | `public` | Kanalzugriffsprüfung unter `ScmTaskCoordinator`-Sperre; inkl. Dateitool-Kanalprobe (`AccessProbeResendDelay` 60 s, max. 3 Sends) — die eigentliche Sicherheitsgrenze. |
| `ConfirmReady` | `public` | Einmalige manuelle Bestätigung (nicht bei `Busy`/`TrustPrompt`/unbekannter CLI). |
| `MarkInputStarted` | `public` | Verwirft Bereitschaft bei jeder Eingabe (`Busy`). |
| `WritePromptAsync` | `public` | Eine einzige Texteingabe unter `ScmTaskCoordinator`-Sperre: Steuerzeichenverbot → `CheckExchangeAccessAsync` → `IsAlive && ReadyStatus == Ready`-Re-Check → `MarkInputStarted` → `Session.WritePromptAsync`. **Keine Stabilisierungsfrist** — die Re-Checks sind punktuell. |
| `RefreshScreen` / `RecognizeStartup` / `SetReady` / `OnOutput` / `OnBufferChanged` / `OnExited` / `OnFailed` | `private` | Bildschirmauswertung (Cursorzeile + Busy-/Trust-Marker), monotone CLI-Erkennung, `ReadyStatusChanged`-Invoke unter `_sync` (Subscriber-Fehler isoliert). |

## `AiExecutionService` (relevante Ausschnitte)
Datei: `src/Softwareschmiede/Application/Services/AiExecutionService.cs`

| Stelle | Kurzbeschreibung |
|--------|------------------|
| Zeilen 383–396 | Erzeugt `ScmPromptSessionAdapter` je Handle; `session.InputStarted = adapter.MarkInputStarted`; `ReadyStatusChanged → Ready` löst `startCheckTrigger.RequestStartCheck()` aus (Slot-Freigabe ereignisgesteuert); anfänglicher `Ready`-Zustand wird einmalig nachgemeldet. |
| `GetScmPromptSession` (Zeile 497) | Liefert den an den tatsächlichen Start gebundenen Adapter (`ICliExecutionLimitSource`-Implementierung neben `GetRunningWorkItemIds`/`GetTerminalSession`). |

## `ScmEventExchangeService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExchangeService.cs`

`PeriodicBackgroundService` (2 s): holt laufende `DeliveryAttemptId`-Ereignisse unabhängig vom Remote-Pollintervall über `DispatchAsync` nach (Annahme/Aktionen/Abschluss des Antwortkanals).

## `ScmEventOutbox`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOutbox.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureAsync` | `public` | Idempotente Outbox-Aktion je `(EventId, Purpose)`; Kollision fremder Inhalte ⇒ `ScmActionIdCollisionException`. |
| `ExecuteAsync` | `public` | Führt die Remoteaktion über `IScmEventResponder` mit Retry/Status aus. |

## `ScmTaskCoordinator`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCoordinator.cs`

`Shared`-Singleton: `RunAsync(taskId, …)` serialisiert alle dispatch-seitigen Operationen je Aufgabe (Dispatch, Queue-Aktionen, Kanalprobe, Prompt-Schreiben).

## `ScmEventValidationService` (Kontext)
Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

`NormalizeTriggerKeyword`, `GetRejection(observation, triggerKeyword)` — Eingangsvalidierung (`"Kein Trigger"`, `NoMentionRejection`, `AlreadyReactedRejection`, `"Bearbeiter nicht bestimmbar"` u. a.). Wird bei Aufnahme (`AcceptBatchAsync`) und als Revalidierung im Dispatch (`DispatchCoreAsync` Zeile 201) aufgerufen; das interne Aufbereitungsereignis umgeht sie, weil es nicht über `AcceptBatchAsync` entsteht.

## `ScmIssuePreparationPrompt`
Datei: `src/Softwareschmiede/Application/Services/ScmIssuePreparationPrompt.cs`

`Build(title, description)` — fester, deterministischer Analyse-Prompt (Qualitätskriterien + `issueBodyUpdate`- und `comment`-Aktionsvertrag); Basis von `request.json`, beim Wiederanlauf bitgleich rekonstruierbar.

## `AppSettingService` (relevante Getter)
Datei: `src/Softwareschmiede/Application/Services/AppSettingService.cs`

| Methode | Schlüssel | Bedeutung |
|---------|-----------|-----------|
| `GetScmMonitoringEnabledAsync` | — | Laufzeit-Schalter „Aufgaben ausführen". |
| `GetScmIssuePreparationEnabledAsync` | `scm.issueaufbereitung.enabled` | Funktionsschalter Aufbereitung (Default `false`). |
| `GetScmIssuePreparationPluginPrefixAsync` | `scm.issueaufbereitung.plugin` | Funktions-Plugin der Aufbereitung (Default `null`). |
| `GetScmIssuePreparationCommentEnabledAsync` | `scm.issueaufbereitung.comment.enabled` | Kommentar-Schalter (Default `true`). |
| `GetScmAccountModeAsync` / `GetScmTriggerKeywordAsync` / `GetScmDelayKeywordAsync` / `GetScmPromptAppendicesAsync` | — | Kontomodus, Trigger-/Verzögerungs-Keyword, Prompt-Anhänge. |
| `GetSettingsAsync` (verwendet von `ScmEventExecutionLimitService`) | `scm.execution.maxparallel`, `plugins.maxparallel.{prefix}` | Ausführungsgrenzen. |

## `WorkItemService.CreateFromIssueEventAsync`
Datei: `src/Softwareschmiede/Application/Services/WorkItemService.cs` (Zeilen 284–355)

Anlagepfad beider Aufgabentypen: `SourceKey`-Dedup vorab und per Unique-Index im `DbUpdateException`-Fallback; `Purpose = purpose`, `AiPluginPrefix = aiPluginPrefix`, `Kind = IssuePreparation` bei `purpose == ScmIssuePreparationService.Purpose`, sonst `Instruction`. Aufgerufen von `ScmEventTaskResolver.ResolveIssueAsync` (kanonischer `…/Issue/{n}`-Key, ohne Purpose) und `ScmIssuePreparationService.DetectAndEnqueueAsync` (`PreparationSourceKey`, mit Purpose + Plugin-Prefix).

## `PromptScheduleDispatchService` (Scope-Abgrenzung)
Datei: `src/Softwareschmiede/Application/Services/PromptScheduleDispatchService.cs`

Zeitgesteuerter Prompt-Versand; schreibt über `ITerminalSession.WritePromptAsync` (Zeile 434) — **nicht** über `IScmPromptSessionAdapter.WritePromptAsync`, also ein anderer Zustellpfad ohne SCM-Kanalprüfung und ohne `Wartend`-Semantik.

## `AiExecutionStatusClassification` / `WorkItemStatusExtensions` (Kontext)
- `AiExecutionStatusClassification.Classify` (`src/Softwareschmiede/Application/Services/AiExecutionStatusClassification.cs`): fachliche Zustandsklassifikation (`AiExecutionState`), u. a. `Waiting` bei `WartetAufEingabe`-Substatus oder Aufgabenstatus `Wartend`.
- `IsCurrentlyPaused` (`src/Softwareschmiede/Domain/Enums/WorkItemStatusExtensions.cs` Zeile 21): Pausenprüfung `PausedUntilUtc > now`, genutzt in `BlockReason` und `GetOccupancyAsync`.
