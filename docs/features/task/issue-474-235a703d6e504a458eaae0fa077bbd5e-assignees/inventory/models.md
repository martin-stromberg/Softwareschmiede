# Datenmodell — Bestandsaufnahme Assignee-Auslöser (Issue #474)

## `ScmNotification`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 85-91)

Roh-Eintrag der Notifications-Inbox des separaten Kontos. Wird von `IScmNotificationSource.EnumerateNotificationsAsync` geliefert und in `ScmEventMonitoringService.EnumerateNotificationsAsync` ausgewertet.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ThreadId` | `string` | GitHub-Notification-Thread-ID (`thread.id`); bisher ungenutzt für die Ereignisidentität |
| `RepositoryNodeId` | `string` | Node-ID des Repositories; Match-Schlüssel gegen `ScmEventRepository.RepositoryId` (Allowlist) |
| `Host` | `string` | SCM-Host; Match-Bedingung gegen die aufgelösten Repositories desselben Hosts |
| `Owner` | `string` | Repository-Owner-Login aus der Notification |
| `Name` | `string` | Repository-Name |
| `Kind` | `ScmReferenceKind` | `Issue` oder `PullRequest` (aus `subject.type`) |
| `Number` | `int` | Referenznummer (letztes Segment von `subject.url`) |
| `Reason` | `string` | Notification-Grund (`thread.reason`); einzig definierte Konstante: `MentionReason = "mention"` (Z. 90) |
| `UpdatedAt` | `DateTimeOffset` | `thread.updated_at` |

Hinweis: **Kein** Zuweisender-/Akteursfeld, kein `AssignReason`. Die GitHub-Notifications-API liefert den Zuweisenden nicht.

## `ScmEventObservation`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 32-54)

Text und Identität einer beobachteten Remoteversion; persistiert als `ObservationJson` im `ScmIncomingEvent`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reference` | `ScmEventReference` | Ursprungsreferenz |
| `ContentKind` | `ScmContentKind` | `Description`, `IssueComment`, `ReviewComment`, `PullRequestReview` |
| `RemoteNodeId` | `string` | GraphQL-Node-ID des Textobjekts |
| `RemoteCommentId` | `string?` | Numerische REST-ID (Kommentare/Reviews) |
| `CreatedAt` / `UpdatedAt` / `LastEditedAt` | `DateTimeOffset` / `DateTimeOffset?` | Zeitstempel der Remoteversion |
| `AuthorId` / `AuthorLogin` | `string?` | Autor-Identität (Akteursprüfung in `GetRejection`) |
| `EditorId` / `EditorLogin` | `string?` | Bearbeiter-Identität (greift bei `LastEditedAt != null`) |
| `Body` | `string` | Textinhalt; wird später zu `ScmExchangeRequest.Body` (Prompt-Inhalt) |
| `Url` | `string` | Ursprungsadresse |
| `ObservedAt` | `DateTimeOffset` | Lesezeitpunkt |
| `ViewerReactions` | `IReadOnlyList<string>?` | Reaktionen des Ereignis-Kontos (Duplikatmerkmal) |
| `BodyHash` | `string` (abgeleitet) | SHA-256 über `Body` |
| `ViewerHasReacted` | `bool` (abgeleitet) | `ViewerReactions.Count > 0` |
| `Revision` | `DateTimeOffset` (abgeleitet) | `LastEditedAt ?? CreatedAt` |
| `ContentKey` | `string` (abgeleitet) | Stabiler Schlüssel: `provider/host/repoId/kind/number/contentKind/remoteNodeId` |
| `EventKey` | `string` (abgeleitet) | `ContentKey/Revision/BodyHash` — Deduplizierungsschlüssel einer Version |

Relevant für die Anforderung: `EventKey` enthält **keine** Zuweisungs-/Auslöseridentität — eine erneute Zuweisung bei unverändertem Text erzeugt denselben `EventKey` (Dedup in `ScmEventStore.AcceptBatchAsync`).

## `ScmEventRepository`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 16-19)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProviderPrefix` | `string` | Plugin-Präfix (z. B. `Softwareschmiede.GitHub`) |
| `Host` | `string` | SCM-Host |
| `RepositoryId` | `string` | Kanonische Repository-Node-ID (Match gegen `ScmNotification.RepositoryNodeId`) |
| `Owner` / `Name` | `string` | Kanonischer Owner-Login / Repository-Name |
| `OwnerId` | `string?` | Owner-Node-ID (Akteursvergleich in `GetRejection`/`SameIdentity`) |
| `ViewerId` / `ViewerLogin` | `string` | Identität des lesenden Kontos (im `SeparateAccount` das Bot-Konto) |
| `CanWrite` | `bool` | Schreibberechtigung (`viewerPermission`) |
| `Account` | `ScmAccountMode` | Kontomodus der Auflösung (JSON-Feldname `konto`) |
| `NumericRepositoryId` | `string?` | `databaseId` |

## `ScmEventReference` / `ScmEventReadRequest` / `ScmEventBatch`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 22-29)

| Record | Eigenschaften | Zweck |
|--------|---------------|-------|
| `ScmEventReference` | `Repository`, `Kind`, `Number` | Validierter Ursprung einer Beobachtung/Antwort |
| `ScmEventReadRequest` | `Reference`, `ContentKind`, `SinceUtc`, `WatermarkUtc` | Separat checkpointbare Quellenanfrage an `IScmEventSource.ReadEventsAsync` |
| `ScmEventBatch` | `Request`, `Observations` | Vollständig gelesene Quelle, atomar mit Checkpoint speicherbar |

## `ResolvedReference` (privat)
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` (Z. 37)

`private sealed record ResolvedReference(Guid? TaskId, ScmEventReference Reference, string? Purpose)` — interne Darstellung einer für den Provider-Durchlauf aufgelösten Referenz (verknüpft, enumeriert oder inbox-abgeleitet). **Trägt kein Reason-/Auslöserfeld**; die Inbox-Reason geht beim `resolved.Add` (Z. 354) verloren.

## `ScmIncomingEvent` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Eingangsreihenfolge (Dispatch-Priorisierung) |
| `Id` | `Guid` | Ereignis-ID |
| `EventKey` | `string` | Dedup-Schlüssel (= `ScmEventObservation.EventKey`) |
| `WorkItemId` | `Guid` | Zugeordnete Aufgabe |
| `ObservationJson` | `string` | Serialisierte `ScmEventObservation` |
| `Status` | `string` | `ScmIncomingEventStatus`-Wert |
| `Reason` | `string?` | Warte-/Fehlergrund |
| `Purpose` | `string?` | Zweck-Kennzeichnung; `null` = reguläres externes Ereignis; interne Zwecke (z. B. `IssueAufbereitung`) umgehen die Trigger-Validierung und folgen einem eigenen Ausführungspfad |
| `DeliveryAttemptId`, `CliRunId`, `ExchangePath`, `DeliveredUtc`, `AcceptedUtc` u. a. | — | Zustell-/Versuchskorrelation |

## `ScmExchangeRequest` / `ScmExchangeAttempt`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs` (Z. 13-41)

| Typ | Relevante Felder | Beschreibung / Zweck |
|-----|------------------|----------------------|
| `ScmExchangeRequest` | `EventId`, `AttemptId`, `CliRunId`, `WorkItemId` (`aufgabeId`), `Body`, `EventKind`, `Repository`, `OriginUrl`, `ProtocolVersion = 1` | Unveränderlicher Auftrag in `request.json`; `Body` = `observation.Body`, `EventKind` = `observation.ContentKind.ToString()`. **Kein Auslöser-/Herkunftsfeld.** |
| `ScmExchangeAttempt` | `Request`, `DirectoryPath`, `RestrictedWriteSids`, `PromptSuffix` | `PromptSuffix` = optionaler konfigurierbarer Anhang (`ScmPromptAppendix`), nur im Prompt (`promptSuffix`-Feld), bewusst **nicht** in `request.json` (Wiederanlauf-Bitgleichheit). |
