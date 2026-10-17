# Bestandsaufnahme — Logik / Services

Zurück: [../inventory.md](../inventory.md)

## `ScmEventMonitoringService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` | public override | Lädt `Aufgaben` (aktive `GitRepository`, inkl. `IssueReferenz`/`PullRequests`) und alle aktiven `GitRepositories`; baut `linked`-Referenzen und Alias-Gruppen; löst jede Gruppe kanonisch über `GetEventRepositoryAsync` auf (Fail-closed: `complete=false` → keine Provider-Ereignisse); enumeriert Repositories; liest pro (Provider, Host, RepositoryId, Kind, Number)-Gruppe die ContentKinds und ruft `AcceptBatchAsync`; dispatchet abschließend fällige Inbox-Einträge. |
| `MatchesPlugin` | private static | Prefix- oder PluginName-Match `plugin` ↔ `GitRepository.PluginTyp`. |
| `EnumerateRepositoryAsync` | private | `enumeration/`-Checkpoint mit eigenem Watermark/Backoff; `EnumerateReferencesAsync`; trägt Treffer mit `TaskId = null` in `resolved` ein. |
| `RecordFailureAsync` | private | Checkpoint-Fehler + Backoff (`ScmEventStore.RetryAt`); Authentication/Permission/Permanent → `NextAttemptUtc = long.MaxValue`. |

Relevante Zeilen:
- Z. 26-38: `linked` = `(TaskId, GitRepository, Kind, Number, RepositoryId)`; Issue → `repository.RepositoryUrl` als Alias-Schlüssel, PR → `PullRequestReferenz.RepositoryId`.
- Z. 48-54: `aliasGroups` nach `RepositoryId` gruppiert; Repositories ohne Aufgaben werden als Gruppe mit leerem `Linked` ergänzt.
- Z. 96-123: Gruppierung von `resolved` nach kanonischer Identität; `taskIds = group.Select(TaskId).Distinct()`; `AcceptBatchAsync(taskIds.Length == 1 ? taskIds[0] : null, sourceKey, batch, taskIds.Length == 0, ct)`.
- Z. 117-120: Snapshots mit `!IdentityResolved` oder Diagnose erzwingen `SinceUtc = null` (Vollscan) — aufgabenlose Referenzen werden so bei jedem Poll vollständig neu gelesen.

## `ScmEventStore`

Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `AmbiguousAssignmentDiagnostic` | public const | `"Mehrdeutige Zuordnung zu Aufgaben"` |
| `UnassignedDiagnostic` | public const | `"Keine Aufgabe zugeordnet"` |
| `Observation(item)` | public static | Deserialisiert `ScmIncomingEvent.ObservationJson`. |
| `ActionId(eventId, purpose)` | public static | Deterministische GUID via SHA-256. |
| `RetryAt(failures, now, retryAfter)` | public static | Backoff 30 s → 2 min → 10 min → 30 min + Jitter. |
| `AcceptBatchAsync(aufgabeId, sourceKey, batch, unassigned, ct)` | public | Atomar: Checkpoint + Snapshots + Inbox. `aufgabeId == null` → Diagnose `Unassigned`/`Ambiguous` je `unassigned`, kein Inbox-Eintrag; sonst `ScmEventValidationService.GetRejection` pro Observation; Snapshot-Dedup über `BodyHash`/`IdentityResolved`; Inbox-Dedup über `EventKey`-Unique-Index. |
| `ClaimTaskAsync`/`RenewTaskAsync`/`ReleaseTaskAsync` | public | `ScmTaskLeases`-Sperren (prozessübergreifend). |

## `ScmEventValidationService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRejection(observation)` | public static | `null` = qualifiziert. Ablehnungsgründe: `Kein Trigger`, `Angemeldeter Benutzer ist nicht Repository-Owner`, `Bearbeiter nicht bestimmbar`, `Textversion stammt nicht vom Repository-Owner`, `Eigene Antwort`. |

## `AufgabeService`

Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs` (sealed, scoped; `SoftwareschmiededDbContext`, `ILogger`, `TodoService`)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `CreateAsync` | Generische Anlage (Status `Neu`). |
| `CreateFromIssueAsync(projektId, issue, gitRepositoryId, ct)` | Aufgabe + `IssueReferenz` in einem Save; **keine** Duplikatprüfung/Rennschutz (liegt im UI-Aufrufer). |
| `CreateFromPullRequestAsync(projektId, pullRequest, repositoryContext, ct)` | Validiert `Nummer`, `BranchName`, Quell-Referenz, Repository-Kontext (existiert, aktiv, `PluginTyp`-Match); Duplikat-Vorabprüfung + `DbUpdateException`-Rennschutz über den Unique-Index `(Provider, RepositoryId, PullRequestNumber)` — wirft `InvalidOperationException("…bereits einer Aufgabe zugeordnet.")`; legt `PullRequestReferenz` mit `Rolle = ReviewSource` an. |
| `BuildPullRequestAnforderungsBeschreibung` / `CleanPullRequestBody` | private static; Titel + bereinigter Body + URL als `AnforderungsBeschreibung`; HTML/Markdown-Kommentar-Bereinigung. |
| `CreateFromAlertAsync` | Vorbild für „bei Konflikt vorhandene Aufgabe zurückgeben" (`DbUpdateException` → Re-Query → `return existing`). |
| `IsPullRequestLinkedAsync(provider, repositoryId, number)` | Duplikatprüfung über `PullRequestRepositoryId.Normalize`. |
| `TryAssignIssueReferenzIfNoneAsync` / `TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync` | Nachträgliche Issue-Verknüpfung mit `DbUpdateException`-Muster (Unique-Index `IssueReferenzen.AufgabeId`). |
| `CreateIssueReferenz(aufgabeId, issue)` | private static Helper zum Befüllen einer `IssueReferenz`. |

## `ScmEventOrchestrator`

Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `DispatchAsync(eventId, …)` | Lease via `ScmTaskCoordinator.Shared.RunAsync(taskId)`; startet `Neu`-Aufgaben (`SetStatusAsync(Gestartet)`), klont/startet CLI via `EntwicklungsprozessService` bei fehlendem `LokalerKlonPfad`; bei fehlender Standard-CLI → `WaitWithCommentAsync` („Keine Standard-CLI konfiguriert."). |

## `ScmTaskCoordinator`

Datei: `src/Softwareschmiede/Application/Services/ScmTaskCoordinator.cs` — prozessweite `SemaphoreSlim`-Gates je `AufgabeId` (kein Referenzschlüssel).

## DI-Registrierung

`src/Softwareschmiede.App/App.xaml.cs`: `ScmEventMonitoringService` (Singleton + HostedService), `ScmEventStore`, `ScmEventOutbox`, `ScmEventOrchestrator` (scoped), `AufgabeService` (scoped). Neue scoped Services müssen hier und in `ScmDispatchFixture` ergänzt werden.
