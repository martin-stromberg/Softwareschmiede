# Datenmodell — Bestandsaufnahme Issue #442

## `ScmEventRepository`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:16`

Unveränderliches Record-DTO der kanonischen Provider-Identität; Ergebnis von `IScmEventSource.GetEventRepositoryAsync`. Wird von `GitHubPlugin.GetEventRepositoryAsync` (`GitHubPlugin.Events.cs:43–46`) erzeugt und in `ScmEventReference` eingebettet persistiert (Inbox, Outbox). Als Record wertbasiert vergleichbar und damit cachebar.

| Eigenschaft (Positionsparameter) | Typ | Beschreibung / Zweck |
|----------------------------------|-----|----------------------|
| `ProviderPrefix` | `string` | Plugin-Prefix des Providers (z. B. `Softwareschmiede.GitHub`); Fließt in Checkpoint-/Content-Keys ein |
| `Host` | `string` | Repository-Host (z. B. `github.com`, GHES-Host) |
| `RepositoryId` | `string` | Kanonische Repository-ID des Providers (GitHub: Node-ID `node_id`) — Identitätsvergleich in `ScmEventOrchestrator`/`ExecuteResponseAsync` |
| `Owner` | `string` | Kanonischer Owner-Login (aus Remote-Metadaten, nicht aus dem Alias) |
| `Name` | `string` | Kanonischer Repository-Name |
| `OwnerId` | `string?` | Node-ID des Owners (`owner.node_id`) |
| `ViewerId` | `string` | Node-ID des authentifizierten Kontos (`GET /user` → `node_id`) |
| `ViewerLogin` | `string` | Login des authentifizierten Kontos |
| `CanWrite` | `bool` | Schreibberechtigung (`permissions.push‖admin‖maintain`) — Gate für Dispatch/Antworten |
| `Account` | `ScmAccountMode` (JSON-Property `konto`) | Kontomodus der Auflösung; wählt über `ClientFor` den API-Client/Token |
| `NumericRepositoryId` | `string?` | Numerische Repository-ID (`id` aus `GET /repos/…`); für Attachment-Upload (`uploads.github.com`) benötigt |

## `ScmRepositoryContext`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRepositoryContext.cs:6`

Eingabe-Record der Auflösung; alle sechs Call-Sites bauen ihn mit dem **rohen Alias** als `RepositoryId`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GitRepositoryId` | `Guid` | ID des lokal persistierten Projekt-Repositories |
| `PluginPrefix` | `string` | Erwarteter Provider-Prefix (wird in `GetEventRepositoryAsync` geprüft) |
| `RepositoryId` | `string` | „Kanonische API-Repository-ID" — faktisch der lokale Alias (HTTPS-URL, SSH-URL oder `owner/name`) |
| `Account` | `ScmAccountMode` | Kontomodus für die Provider-Aufrufe |

## `ScmEventReference`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:22`

`record ScmEventReference(ScmEventRepository Repository, ScmReferenceKind Kind, int Number)` — validierter Ursprung; die Repository-Auflösung steckt vollständig in `Repository`.

## `ScmEventReadRequest` / `ScmEventBatch` / `ScmEventObservation`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:25/29/32`

Leserahmen einer Quelle (`Reference`, `ContentKind`, `SinceUtc`, `WatermarkUtc`), Ergebnis (`Request`, `Observations`) und einzelne beobachtete Textversion inkl. `ContentKey`/`EventKey`-Dedup-Schlüsseln (Key enthält `ProviderPrefix`/`Host`/`RepositoryId` in lowercase — kanonisch, nicht aliasbasiert).

## `ScmNotification`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:85`

Roher Inbox-Eintrag des separaten Kontos (`ThreadId`, `RepositoryNodeId`, `Host`, `Owner`, `Name`, `Kind`, `Number`, `Reason`, `UpdatedAt`); `RepositoryNodeId` wird gegen `ScmEventRepository.RepositoryId` gematcht (`ScmEventMonitoringService.cs:280`) — setzt erfolgreiche Auflösung voraus. `MentionReason = "mention"` (`:90`).

## `ScmObservationCheckpoint`
Datei: `src/Softwareschmiede/Domain/Entities/ScmObservationCheckpoint.cs:4`

Persistenter Backoff-/Watermark-Datensatz je `SourceKey` — u. a. die Auflösungs-Checkpoints `repository/{prefix}/{alias}`, `issueaufbereitung-repository/{prefix}/{alias}`, `backmerge-repository/{prefix}/{alias}` (Schlüssel aus dem **rohen, lowercase-escapten Alias**).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceKey` | `string` | Primärschlüssel der Quelle |
| `WatermarkUtc` | `long?` | Letzter erfolgreicher Lesestand (Unix-ms) |
| `Failures` | `int` | Fehlerzähler für exponentielles Backoff (`ScmEventStore.RetryAt`) |
| `NextAttemptUtc` | `long?` | Nächster erlaubter Versuch; `long.MaxValue` = dauerhaft gesperrt |
| `ErrorCode` | `string?` | Name des `ScmProviderFailureKind` (z. B. `RateLimit`) — Grundlage von `IsInaccessible` |
| `Diagnostic` | `string?` | Anzeigetext inkl. Wiederherstellungshinweis |

## `ScmProviderException`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:97`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FailureKind` | `ScmProviderFailureKind` | Fehlerkategorie; `RateLimit` ist bereits vorhanden |
| `RetryAfterUtc` | `DateTimeOffset?` | Frühester Wiederholungszeitpunkt; wird von `EvaluateResponse` aus `Retry-After`/`X-RateLimit-Reset` befüllt und von `ScmCheckpointSupport.ApplyFailure` ins Checkpoint-Backoff übernommen |
| `OutcomeUncertain` | `bool` | Remoteänderung möglicherweise erfolgt (nur Mutationspfade/Transport) |

## Transport-Records (Plugin-intern)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs`

| Record | Member | Zweck |
|--------|--------|-------|
| `GitHubEventApiRequest` (`:16`) | `Host`, `Method`, `Endpoint`, `JsonBody?` | Einzelner API-Aufruf; `Endpoint == "graphql"` kennzeichnet GraphQL — bereits unterscheidbar für ein Budget-Typ-Gate |
| `GitHubAssetUploadRequest` (`:20`) | `Host`, `RepositoryId` (numerisch), `Name`, `ContentType`, `Data` | Attachment-Upload |
| `GitHubEventApiResponse` (`:23`) | `StatusCode`, `Body`, `Headers?` | Antwort inkl. Header — trägt `X-RateLimit-*`/`Retry-After`/`Link` |
