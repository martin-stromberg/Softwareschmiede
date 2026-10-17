# SCM-Ereignisüberwachung und GitHub-Quelle

Zurück: [../inventory.md](../inventory.md)

## 1. Topologie: zwei Hintergrunddienste

Beide werden in `src/Softwareschmiede.App/App.xaml.cs` registriert:

| Dienst | Datei | Intervall | Registrierung |
|--------|-------|-----------|---------------|
| `ScmEventMonitoringService` | `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` | `ScmEventOptions.PollInterval` = 60 s (`ScmEventOptions.cs:7`) | `App.xaml.cs:262-263` (Singleton + HostedService) |
| `ScmEventExchangeService` | `src/Softwareschmiede/Application/Services/ScmEventExchangeService.cs` | 2 s (`ScmEventExchangeService.cs:11`) | `App.xaml.cs:264-265` |

Beide erben `PeriodicBackgroundService` (`src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs`) und warten auf `ScmEventStartupGate` (`ScmEventOptions.cs:24-31`), das nach der DB-Migration in `App.xaml.cs:104` freigegeben wird. Konfiguration: `services.Configure<ScmEventOptions>(context.Configuration.GetSection("ScmEvents"))` (`App.xaml.cs:254`).

- `ScmEventMonitoringService` macht pro Lauf: Remote-Referenzen pollen → Batches in die Inbox schreiben → alle fälligen Inbox-Einträge dispatchen.
- `ScmEventExchangeService` dispatchet nur Einträge mit `DeliveryAttemptId != null && Status != "Verarbeitet"` erneut (lokale Antwortdateien abgleichen, `ScmEventExchangeService.cs:18-23`).

## 2. Woher die Referenzliste kommt — Kernbefund für FR-1/FR-2

`ScmEventMonitoringService.RunOnceAsync` (`ScmEventMonitoringService.cs:20-36`):

```csharp
var tasks = await db.Aufgaben.AsNoTracking().Include(x => x.GitRepository).Include(x => x.IssueReferenz)
    .Include(x => x.PullRequests).Where(x => x.GitRepository != null && x.GitRepository.Aktiv).ToListAsync(ct);
```

- Quelle der Beobachtungsliste = **Aufgaben** (`Aufgaben`-Tabelle), deren `GitRepository.Aktiv == true`.
- Pro Aufgabe werden gesammelt (`Zeilen 32-35`):
  - `task.IssueReferenz.IssueNummer` → `ScmReferenceKind.Issue`, RepositoryId = `repository.RepositoryUrl`
  - jeder `task.PullRequests`-Eintrag → `ScmReferenceKind.PullRequest`, `reference.PullRequestNumber`, RepositoryId = `reference.RepositoryId`
- Klassenkommentar Zeile 12: „Beobachtet ausschließlich lokal verknüpfte SCM-Referenzen und persistiert vollständige Quellen."
- **Konsequenz:** Ein aktiviertes Projekt-Repository ohne Aufgaben-Referenz wird nicht abgefragt; neue Issues/PRs und Kommentare an fremden Referenzen sind unsichtbar.

## 3. Plugin-Verträge

`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/`:

- `IScmEventSource` (`IScmEventSource.cs`): `GetEventRepositoryAsync(ScmRepositoryContext)` liefert `ScmEventRepository` (ProviderPrefix, Host, RepositoryId=node_id, Owner, Name, OwnerId, ViewerId, ViewerLogin, CanWrite); `ReadEventsAsync(ScmEventReadRequest)` liefert `ScmEventBatch`. **Referenzbasiert** — es gibt keinen „liste alle Issues/PRs des Repos"-Vertrag.
- `IScmEventResponder` (`IScmEventResponder.cs`): `ReconcileResponseAsync` (idempotentes Wiederfinden) + `ExecuteResponseAsync` (Kommentar, Reaktion, PullRequest).
- Value Objects in `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`:
  - `ScmReferenceKind { Issue, PullRequest }` (Z. 8)
  - `ScmContentKind { Description, IssueComment, ReviewComment, PullRequestReview }` (Z. 11)
  - `ScmEventObservation` mit `ContentKey` (Provider/Host/RepoId/Kind/Number/ContentKind/NodeId, Z. 40-43) und `EventKey` = ContentKey + Revision + BodyHash (Z. 46) — Grundlage der Deduplizierung.
  - `ScmProviderException`/`ScmProviderFailureKind { Authentication, Permission, RateLimit, Temporary, Permanent }` (Z. 66-86).
- `ScmResponseKind { Comment, Reaction, PullRequest }` (Z. 50); Marker `<!-- softwareschmiede:action:{ActionId} -->` (Z. 59).

## 4. GitHub-Implementierung

`plugins/Softwareschmiede.Plugin.GitHub/`:

- `GitHubPlugin.cs:15` — `GitPluginBase<GitHubPlugin>, IScmEventSource, IScmEventResponder`; Prefix `Softwareschmiede.GitHub` (Z. 29). Token via `ICredentialStore` Schlüssel `Softwareschmiede.GitHub.Token`, als `GH_TOKEN`-Umgebungsvariable an `gh` übergeben (Z. 114-123, niemals als Argument). Einstellungen PAT: `repo`, `read:org`, Code-Scanning (Z. 37-46).
- `GitHubPlugin.Events.cs`:
  - `GetEventRepositoryAsync` (Z. 14-30): `GET user` + `GET repos/{owner}/{name}`; `CanWrite` aus `permissions.push|admin|maintain`; `RepositoryId` = `node_id`.
  - `ReadEventsAsync` (Z. 33-93): je `ScmContentKind`
    - `Description`: GraphQL `issue(number)/pullRequest(number)` mit `TextFields` (id, body, createdAt, updatedAt, lastEditedAt, url, author, editor)
    - `IssueComment`/`ReviewComment`: REST `issues|pulls/{n}/comments?per_page=100` (+`since`), dann pro Kommentar GraphQL `node(id:)` für konsistenten Body/Editor-Snapshot (Z. 76-90)
    - `PullRequestReview`: GraphQL `reviews(first:100)` paginiert, nur `state != PENDING` und `submittedAt <= WatermarkUtc` (Z. 51-69)
  - `AddObservation` (Z. 95-121): filtert Post-Watermark-Texte und eigene Marker-Kommentare des Viewers; baut URLs (`#issuecomment-`, `#discussion_r`, `#pullrequestreview-`).
  - `ParseEventRepository` (Z. 217-244): HTTPS/SSH-URLs → (host, owner, name); `ValidateNextPage` (Z. 206-215) hält Pagination auf dem gleichen Host/Pfad.
- `GitHubPlugin.EventResponses.cs`: `ReconcileResponseAsync` sucht Marker unter den Kommentaren/Reactionen des Viewers; `ExecuteResponseAsync` verifiziert vor jedem Schreiben `RepositoryId`/`ViewerId`/`CanWrite` erneut (S. ~30-45), Reaktionen auf Reviews via GraphQL `addReaction`, Kommentare/PRs via REST.
- `IGitHubEventApiClient.cs` + `GhEventApiClient` (Z. 24-87): Transport = `gh api --include`, Request-Body über Tempdatei (`--input`), Antwort = HTTP-Status + Header + Body; HTTP-Fehler → typisierte `ScmProviderException` (401→Authentication, 403→Permission, 429/Rate-Limit-Header→RateLimit mit `Retry-After`-Auswertung, 5xx→Temporary).

## 5. Dispatch-Selektion, Checkpoints, Snapshots

`ScmEventMonitoringService.cs`:

- Repository-Auflösung pro Plugin/Repo-Alias-Gruppe (Z. 45-78): erst **alle** lokalen Aliase über `GetEventRepositoryAsync` auflösen; bei jeder nicht auflösbaren Gruppe `complete=false` → **gar keine** Events dieses Providers werden eingelesen (Ambiguity-Schutz, Z. 79-80).
- Pro (Provider, Host, RepoId, Kind, Number) werden ContentKinds gelesen (Z. 85-87): Issue → Description + IssueComment; PR → zusätzlich ReviewComment + PullRequestReview.
- `ScmObservationCheckpoint` je `sourceKey` (Z. 93-97): `WatermarkUtc`, `Failures`, `NextAttemptUtc`, `ErrorCode`, `Diagnostic`. `SinceUtc` = Watermark − 2×PollInterval (Z. 100-101); bei unaufgelöster/ambiger Zuordnung wird `SinceUtc` aufgehoben (Vollscan, Z. 102-104).
- Fehler → `RecordFailureAsync` (Z. 136-154): Backoff via `ScmEventStore.RetryAt` (30 s → 2 min → 10 min → 30 min + Jitter, `ScmEventStore.cs:23-24`); Authentication/Permission/Permanent → `NextAttemptUtc = long.MaxValue` (manuelle „Quellen erneut prüfen"-Aktion nötig).
- Dispatch-Selektion (Z. 116-133): `Status != "Verarbeitet" && (NextAttemptUtc null oder fällig)` **oder** ausstehende `ScmOutgoingActions` → `ScmEventOrchestrator.DispatchAsync`, Chargen à 100, in Sequence-Reihenfolge.

`ScmEventStore.AcceptBatchAsync` (`ScmEventStore.cs:27-81`):

- Atomar in einer Transaktion: Checkpoint-Update + Snapshots + Inbox-Insert.
- Dedup-Ebenen: `ScmContentSnapshot` (ContentKey + BodyHash + Revision) unterdrückt unveränderte Wiederholungen; `ScmIncomingEvents.EventKey` hat Unique-Index (`SoftwareschmiededDbContext.cs:118-121`).
- `aufgabeId == null` (mehrere Tasks auf derselben Referenz) → Diagnostic „Mehrdeutige Zuordnung zu Aufgaben", kein Inbox-Eintrag (Z. 46-49, 76-77).
- Validierung: `ScmEventValidationService.GetRejection(observation)` schon bei der Aufnahme; abgelehnte Observationen erzeugen keinen Inbox-Eintrag, werden aber als Snapshot mit Diagnostic gespeichert (Z. 46-62).

`ScmEventValidationService` (`src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`) — **Trigger- und Owner-Regeln (FR-9-Bestand)**:

1. Body muss `$Softwareschmiede` (OrdinalIgnoreCase) enthalten → sonst „Kein Trigger" (Z. 12-13).
2. `ViewerId/ViewerLogin` == `OwnerId/Owner` → sonst „Angemeldeter Benutzer ist nicht Repository-Owner" (Z. 14-15).
3. Bearbeiter bestimmbar (Autor bzw. bei `lastEditedAt` der Editor) → sonst „Bearbeiter nicht bestimmbar" (Z. 16-20).
4. Bearbeiter == Viewer → sonst „Textversion stammt nicht vom Repository-Owner" (Z. 21-22).
5. `<!-- softwareschmiede:` im Body → „Eigene Antwort" (Z. 23-24).

`ScmEventOutbox` (`ScmEventOutbox.cs`): deterministische Aktions-IDs `SHA256(eventId:purpose)` (`ScmEventStore.cs:20-21`); `EnsureAsync` persistiert vor dem Remote-Call, `ExecuteAsync` reconciliert zuerst (`ReconcileResponseAsync`), postet nur bei belegtem Nichtvorhandensein; „Sending"/„Uncertain" wird nie blind erneut gepostet.

## 6. Persistenz / Entitäten

`SoftwareschmiededDbContext.cs:100-132`: DbSets `ScmIncomingEvents` (Key `Sequence` auto-increment, Unique `Id`, Unique `EventKey`, Index `(AufgabeId, Sequence)`, `(Status, NextAttemptUtc)`), `ScmContentSnapshots` (Key `ContentKey`), `ScmObservationCheckpoints` (Key `SourceKey`), `ScmOutgoingActions` (Key `Id`, Index `(EventId, Status)`), `ScmTaskLeases` (Key `AufgabeId`). Migration `20261011045239_AddScmEventInbox`.

Entitäten (`src/Softwareschmiede/Domain/Entities/`): `ScmIncomingEvent` (Status-String, Reason, Attempts, NextAttemptUtc, DeliveryAttemptId, CliRunId, ExchangePath, ExchangeRestrictedWriteSidsJson, DeliveredUtc, AcceptedUtc, ReadyConfirmedRunId), `ScmContentSnapshot`, `ScmObservationCheckpoint`, `ScmOutgoingAction`, `ScmTaskLease`.

## 7. Was für FR-2 fehlt

- Kein Vertrag/Implementation zum **Enumerieren aller Referenzen eines Repos** (z. B. „list open issues/PRs" oder Repo-Events/Activity-API). `ReadEventsAsync` braucht eine konkrete `Number`.
- `ScmIncomingEvent.AufgabeId` ist `Guid` (nicht nullable) und `AcceptBatchAsync(Guid? aufgabeId, …)` nutzt `null` bisher nur als „mehrdeutig"-Marker — eine „keine Aufgabe"-Zuordnung hat kein Datenmodell.
- `GetEventRepositoryAsync` wird bereits pro Repo-Gruppe aufgerufen und liefert kanonische Repo-Identität (node_id, Owner, CanWrite) — wiederverwendbar für Repo-Level-Überwachung.
- Vorhandene Listen-APIs im `IGitPlugin`-Vertrag (`GetIssuesAsync`, `GetOpenPullRequestsAsync` — z. B. `GitHubPlugin.cs:334-358`) liefern flache `Issue`/`PullRequest`-Objekte ohne die `ScmEventObservation`-Metadaten (Autor-/Editor-IDs, node_id); für die Validierung reicht das nicht direkt.

Hilfedoku des Features (konsistent zum Code): `docs/help/plugins/github-ereignisse/` — insb. `architektur.md`, `ablauf-technisch.md`, `business-rules.md`, `datenmodell.md`, `troubleshooting.md`.
