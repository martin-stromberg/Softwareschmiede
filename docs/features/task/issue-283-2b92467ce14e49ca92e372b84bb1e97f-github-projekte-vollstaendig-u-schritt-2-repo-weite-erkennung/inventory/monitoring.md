# Überwachung, Checkpoints und Inbox-Aufnahme

Zurück: [../inventory.md](../inventory.md)

## `ScmEventMonitoringService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` (155 Zeilen)

| Methode/Block | Zeilen | Kurzbeschreibung |
|---------------|--------|------------------|
| `RunOnceAsync` | 20-134 | Ein Polling-Lauf: Referenzliste → Auflösung → ReadEvents → Dispatch. Erbt `PeriodicBackgroundService` mit `ScmEventOptions.PollInterval` (60 s); wartet auf `ScmEventStartupGate`. |
| Aufgaben-Referenzliste | 25-36 | `db.Aufgaben` mit `GitRepository != null && Aktiv`; sammelt `linked`-Tupel `(TaskId, GitRepository, Kind, Number, RepositoryId)` — `RepositoryId` ist hier die **lokale Alias-URL** (`repository.RepositoryUrl` für Issues, `reference.RepositoryId` für PRs). |
| Alias-Auflösung pro Plugin | 37-80 | Gruppiert `linked` nach `RepositoryId` (Alias); Checkpoint `repository/{prefix}/{alias}` (Z. 53); bei gesetztem `NextAttemptUtc` → `complete=false`, Provider-Runde übersprungen. `GetEventRepositoryAsync` liefert `ScmEventRepository` (node_id, Owner, Name, OwnerId, ViewerId, ViewerLogin, CanWrite); erfolgreiche Auflösung löscht den Checkpoint (Z. 64-69); Fehler → `RecordFailureAsync` mit Suffix „…keine neuen Anfragen dieses Providers eingelesen" (Z. 75-76). Bei `!complete` wird **kein** Event des Providers gelesen (Z. 79-80, Ambiguitätsschutz). |
| Referenz-Lesepfad | 81-114 | Gruppierung `resolved` nach (Provider, Host, RepositoryId, Kind, Number); ContentKinds: Issue → Description+IssueComment, PR → +ReviewComment+PullRequestReview (Z. 85-87). `sourceKey` = Provider/Host/RepoId/Kind/Number/ContentKind (Z. 93-94). `SinceUtc` = Watermark − 2×PollInterval (Z. 100-101); auf `null` zurückgesetzt (Vollscan), wenn Snapshots der Quelle unaufgelöst/ambig sind (Z. 102-104). `ReadEventsAsync` → `AcceptBatchAsync(taskIds.Length == 1 ? taskIds[0] : null, sourceKey, batch)` (Z. 105-106). Fehler → `RecordFailureAsync`. |
| Dispatch-Selektion | 116-133 | Inbox-Einträge `Status != "Verarbeitet"` fällig oder mit ausstehenden `ScmOutgoingActions` → `ScmEventOrchestrator.DispatchAsync`, Chargen à 100 in Sequence-Reihenfolge. |
| `RecordFailureAsync` | 136-154 | Checkpoint-Failures++; Authentication/Permission/Permanent → `NextAttemptUtc = long.MaxValue` (Sperre bis „Quellen erneut prüfen"); sonst Backoff via `ScmEventStore.RetryAt` (30 s → 2 min → 10 min → 30 min + Jitter). Benachrichtigt `ScmEventChangeNotifier`. |

Klassenkommentar Z. 12: „Beobachtet ausschließlich lokal verknüpfte SCM-Referenzen und persistiert vollständige Quellen." — ändert sich mit dieser Anforderung.

## `ScmEventStore`

Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `AcceptBatchAsync(Guid? aufgabeId, string sourceKey, ScmEventBatch batch, ct)` | 27-81 | Atomare Transaktion: Checkpoint + Snapshots + Inbox-Insert. `aufgabeId == null` → rejection „Mehrdeutige Zuordnung zu Aufgaben", `IdentityResolved=false`, kein Inbox-Eintrag, aber Snapshot persistiert (Z. 46-60); Checkpoint-Diagnostic wird gesetzt (Z. 76-77). Dedup: unveränderter Snapshot (`BodyHash` gleich, `IdentityResolved`, keine Ambiguitätsdiagnose) → `continue` (Z. 48-50); `EventKey`-Unique-Index über `AnyAsync`-Prüfung (Z. 61). Abgelehnte Observationen (Validierung) → Snapshot mit Diagnostic, kein Inbox-Eintrag. |
| `RetryAt` | 23-24 | Backoff-Leiter mit Jitter; `retryAfter` des Providers hat Vorrang. |
| `ClaimTaskAsync`/`RenewTaskAsync`/`ReleaseTaskAsync` | 84-97 | Aufgabenleases (für Dispatch, nicht für Monitoring relevant). |

## `ScmEventValidationService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs` — `GetRejection(observation)`: (1) `$Softwareschmiede`-Trigger OrdinalIgnoreCase, (2) `Viewer==Owner`, (3) Bearbeiter bestimmbar, (4) Bearbeiter==Viewer, (5) `<!-- softwareschmiede:`-Eigenmarker. Arbeitet nur auf `ScmEventObservation`-Feldern — Metadatenvollständigkeit des neuen Enumerationspfads ist dafür ausreichend.

## Entitäten

- `ScmObservationCheckpoint` (`src/Softwareschmiede/Domain/Entities/`): Key `SourceKey`; `WatermarkUtc`, `Failures`, `NextAttemptUtc`, `ErrorCode`, `Diagnostic`.
- `ScmContentSnapshot`: Key `ContentKey`; `BodyHash`, `Revision`, `IdentityResolved`, `Diagnostic`.
- `ScmIncomingEvent`: Unique `EventKey`, `AufgabeId` (non-nullable `Guid`), Status-String, `ObservationJson`.
- DbContext-Indizes: `SoftwareschmiededDbContext.cs:100-132` (Unique `EventKey` Z. 118-121).
