# Bestandsaufnahme: Datenmodell (SCM-Ereignisverarbeitung)

## `ScmEventObservation`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 30–49)

Positionaler `sealed record` — eine neue Eigenschaft ist als weiterer Positionsparameter anzulegen; der Record wird an genau einer Produktivstelle konstruiert (`GitHubPlugin.AddObservation`, `GitHubPlugin.Events.cs` Z. 255) sowie in Testhelfern (`ScmDispatchFixture.AddAsync` Z. 184, `ScmEventMonitoringServiceTests_RepoWide.ConfigureObservations` Z. 208, `ScmEventValidationServiceTests.Observe`/`ObserveSeparate` Z. 144–158 u. a.).

Serialisierung: `ScmEventStore.JsonOptions` (`JsonSerializerDefaults.Web`, PascalCase-Property-Namen) → `ScmIncomingEvent.ObservationJson`. Abwärtskompatibilität zu älteren gespeicherten JSONs verlangt einen optionalen Parameter bzw. Standardwert (System.Text.Json ignoriert fehlende JSON-Eigenschaften nur bei optionalen Record-Parametern). Deserialisierung erfolgt in `ScmEventStore.Observation` (`ScmEventStore.cs` Z. 24–25).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reference` | `ScmEventReference` | Ursprungsreferenz inkl. Repository und Viewer-Identität |
| `ContentKind` | `ScmContentKind` | Namensraum der beobachteten Textquelle (`Description`, `IssueComment`, `ReviewComment`, `PullRequestReview`) |
| `RemoteNodeId` | `string` | Provider-Node-ID des Texts (GraphQL-`id`) |
| `RemoteCommentId` | `string?` | Numerische REST-Kommentar-ID (`fullDatabaseId`); bei `Description` `null` |
| `CreatedAt` | `DateTimeOffset` | Erstellzeitpunkt remote |
| `UpdatedAt` | `DateTimeOffset` | Metadaten-Aktualität remote (nicht teil der Version) |
| `LastEditedAt` | `DateTimeOffset?` | Letzter Textedit; `null` = unbearbeitet |
| `AuthorId` / `AuthorLogin` | `string?` | Provider-Identität des Autors |
| `EditorId` / `EditorLogin` | `string?` | Provider-Identität des letzten Bearbeiters |
| `Body` | `string` | Vollständiger UTF-8-Text |
| `Url` | `string` | Kanonische URL des Textes (im Plugin zusammengesetzt) |
| `ObservedAt` | `DateTimeOffset` | Lokaler Beobachtungszeitpunkt |
| `BodyHash` | `string` (berechnet) | SHA-256 über `Body` |
| `Revision` | `DateTimeOffset` (berechnet) | `LastEditedAt ?? CreatedAt` — Metadatenänderungen erzeugen keine neue Version |
| `ContentKey` | `string` (berechnet) | Stabiler Schlüssel des Texts: `provider/host/repoId/kind/number/contentKind/nodeId` (URI-escaped) |
| `EventKey` | `string` (berechnet) | `{ContentKey}/{Revision:O}/{BodyHash}` — Deduplizierungsschlüssel einer beobachteten Version |

**Reaktionsdaten: nicht vorhanden.** Weder ein `ViewerHasReacted`-Flag noch Reaktionslisten existieren; `reactionGroups`/`viewerHasReacted` kommt im gesamten Code nicht vor.

## `ScmEventRepository`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 15–17)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProviderPrefix` | `string` | Plugin-Prefix (`Softwareschmiede.GitHub`) |
| `Host` | `string` | Provider-Host |
| `RepositoryId` | `string` | Kanonische Repository-Node-ID |
| `Owner` / `Name` | `string` | Kanonisierter Owner/Name |
| `OwnerId` | `string?` | Node-ID des Owners (Identitätsabgleich in `GetRejection`) |
| `ViewerId` / `ViewerLogin` | `string` | Identität des authentifizierten Kontos — **die für `viewerHasReacted` maßgebliche Konto-Identität** |
| `CanWrite` | `bool` | Schreibberechtigung des Kontos |
| `Konto` | `ScmKontoModus` | `Benutzerkonto` (Default) oder `SeparatesKonto`; steuert `ClientFor`-Routing |

## `ScmEventReadRequest` / `ScmEventBatch`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 23–27)

- `ScmEventReadRequest(Reference, ContentKind, SinceUtc, WatermarkUtc)` — `SinceUtc` enthält bereits die Überlappung des Aufrufers; `WatermarkUtc` begrenzt den Batch.
- `ScmEventBatch(Request, IReadOnlyList<ScmEventObservation> Observations)` — vollständig gelesene Quelle; wird atomar mit dem Checkpoint gespeichert.

## `ScmEventReference`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 20)

`ScmEventReference(ScmEventRepository Repository, ScmReferenceKind Kind, int Number)` — validierter Ursprung einer Beobachtung oder Antwort.

## `ScmResponseRequest` / `ScmResponseResult`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 55–65)

- `ScmResponseRequest(Reference, ActionId, Kind, Body?, Reaction?, ContentKind?, RemoteCommentId?, RemoteNodeId?, Title?, HeadBranch?, BaseBranch?)`; `Marker` → `<!-- softwareschmiede:action:{ActionId} -->`. Die Empfangsreaktion wird als `Kind = ScmResponseKind.Reaction`, `Reaction = "eyes"` angelegt (`ScmEventOrchestrator.cs` Z. 97–99).
- `ScmResponseResult(RemoteId, Url?, AlreadyExisted)` — bestätigte oder wiedergefundene Remoteaktion.

## `ScmNotification`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 68–74)

Roher Inbox-Eintrag der Notifications-API (SeparatesKonto-Pfad): `ThreadId`, `RepositoryNodeId`, `Host`, `Owner`, `Name`, `Kind`, `Number`, `Reason`, `UpdatedAt`; `MentionReason = "mention"` — nur Mentions führen zur Beobachtung (`ScmEventMonitoringService.cs` Z. 277).

## `ScmIncomingEvent`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

Persistenter Inbox-Datensatz. Für die Anforderung relevant: `EventKey` (Dedup, Z. 13), `AufgabeId` (Z. 16), `ObservationJson` (serialisierte `ScmEventObservation`, Z. 19), `Status` (Default `"Aufgenommen"`, Z. 22), `Reason` (Z. 25), `ReceivedUtc` (Z. 28), `Attempts`/`NextAttemptUtc`/`ErrorCode` (Retry/Backoff), `DeliveryAttemptId`/`CliRunId`/`ExchangePath`/`ExchangeRestrictedWriteSidsJson` (Zustellversuch), `DeliveredUtc`/`AcceptedUtc`/`ReadyConfirmedRunId`.

## `ScmContentSnapshot`
Datei: `src/Softwareschmiede/Domain/Entities/ScmContentSnapshot.cs`

Dedup-/Diagnose-Datensatz pro `ContentKey`: `BodyHash`, `Revision`, `IdentityResolved` (Bearbeiter auflösbar), `Diagnostic` (Ablehnungsgrund bzw. Zuordnungsdiagnose). `AcceptBatchAsync` schreibt den Ablehnungsgrund hierhin statt eines Inbox-Eintrags (`ScmEventStore.cs` Z. 73–77).

## `ScmObservationCheckpoint`
Datei: `src/Softwareschmiede/Domain/Entities/ScmObservationCheckpoint.cs`

Checkpoint pro Quellschlüssel: `SourceKey`, `WatermarkUtc`, `Failures`, `NextAttemptUtc` (Backoff-Sperre), `ErrorCode`, `Diagnostic`.

## `ScmOutgoingAction`
Datei: `src/Softwareschmiede/Domain/Entities/ScmOutgoingAction.cs`

Persistierte Remoteaktion (Empfangsreaktion `"receipt"`, Wartekommentar `"waiting"`, Agentenaktionen `"ai:{id}"`) mit `RequestJson`, `Status`, `Attempts`, `NextAttemptUtc`, `RemoteId`, `Error`/`ErrorCode`.

## `ScmE2EText` / `ScmE2EScenario` / `ScmE2ERemoteResponse` (E2E-Fixture-Datenmodell)
Datei: `src/Softwareschmiede.App/Services/Testing/ScmE2ETestConfiguration.cs` (Z. 49–83)

- `ScmE2EScenario`: `Authenticated`, `CanWrite`, `RejectAiResponse`, `PluginAvailable`, `Texts`, `Notifications` — Szenariodatei `scm-scenario.json`. **Kein Feld für Reaktionen**; ein Kommentar mit vorhandener Viewer-Reaktion ist derzeit nicht darstellbar.
- `ScmE2EText`: `Number`, `Kind` (z. B. `"IssueComment"`, `"Description"`), `PullRequest`, `Id`, `Titel`, `Body`, `Author`, `CreatedAt`, `EditedAt`, `NodeId` (berechnet `SCM_{Kind}_{Id}`).
- `ScmE2ERemoteResponse(Endpoint, Id, Body, Content?)` — vom Host gepostete Antworten (`scm-responses.jsonl`); `Content` trägt den Reaktionstyp (z. B. `"eyes"`).
- `ScmE2ENotification(Number, PullRequest, RepositoryNodeId, Reason, UpdatedAt, ThreadId)` — Inbox-Eintrag für SeparatesKonto-Tests.
