# Datenmodell: Assign-Auslöser und SCM-Ereignisüberwachung

Vertrags- und Persistenzklassen des Verarbeitungspfads assign-Notification → Description-Batch → `ScmIncomingEvent`.

## `ScmEventObservation`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 37–60)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reference` | `ScmEventReference` | Validierter Ereignisursprung (Repository + Referenzart + Nummer). |
| `ContentKind` | `ScmContentKind` | Textquelle; `Description` ist die vom Assign-Auslöser genutzte Art. |
| `RemoteNodeId` | `string` | Remote-Identität; wird im Assign-Pfad vom Monitor um `"/assign-{eventNodeId}"` erweitert (Z. 231 in `ScmEventMonitoringService.cs`) und geht so in den `ContentKey` ein. |
| `RemoteCommentId` | `string?` | REST-ID des Kommentars (für Beschreibungen `null`). |
| `CreatedAt` / `UpdatedAt` / `LastEditedAt` | `DateTimeOffset` / `DateTimeOffset?` | Zeitstempel der Textversion. |
| `AuthorId` / `AuthorLogin` | `string?` | Autor der beobachteten Textversion. |
| `EditorId` / `EditorLogin` | `string?` | Letzter Bearbeiter (bei `LastEditedAt != null`). |
| `Body` | `string` | Vollständiger Text — bei aufbereiteten Issues die marker-tragende Beschreibung. |
| `Url` | `string` | Kanonische Remote-URL der Textversion. |
| `ObservedAt` | `DateTimeOffset` | Lokaler Beobachtungszeitpunkt. |
| `ViewerReactions` | `IReadOnlyList<string>?` | Reaktionen des lesenden Kontos; `ViewerHasReacted` davon abgeleitet (Z. 47). |
| `Trigger` | `ScmEventTrigger?` | Auslöseherkunft; `null` = regulärer Texttrigger. Im Assign-Pfad mit `Reason = "assign"` dekoriert; wird über `ObservationJson` persistiert (Z. 114 in `ScmEventStore.cs`). |
| `BodyHash` | `string` (abgeleitet, Z. 44) | SHA-256 über den UTF-8-Text; Deduplizierung. |
| `ViewerHasReacted` | `bool` (abgeleitet, Z. 47) | Ereignis-Konto hat remote reagiert. |
| `Revision` | `DateTimeOffset` (abgeleitet, Z. 50) | `LastEditedAt ?? CreatedAt`; geht in `EventKey` ein. |
| `ContentKey` | `string` (abgeleitet, Z. 53–56) | `{provider}/{host}/{repoId}/{kind}/{number}/{contentKind}/{remoteNodeId}` — enthält im Assign-Fall das `/assign-…`-Suffix. |
| `EventKey` | `string` (abgeleitet, Z. 59) | `{ContentKey}/{Revision}/{BodyHash}`; Deduplizierungs- und Unique-Schlüssel. |

## `ScmEventTrigger`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 33–34)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reason` | `string` | Auslösegrund; im Assign-Pfad `ScmNotification.AssignReason` (`"assign"`). |
| `ActorId` / `ActorLogin` | `string?` | Zuweisender (aus `ScmAssignment.AssignerId`/`AssignerLogin`); `null` wenn nicht bestimmbar. |
| `EventNodeId` | `string?` | Identität des Zuweisungsereignisses (`ScmAssignment.EventNodeId`). |
| `OccurredAt` | `DateTimeOffset?` | Zeitpunkt der wirksamen Zuweisung. |

## `ScmEventReadRequest` / `ScmEventBatch`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 25–29)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reference` | `ScmEventReference` | Zu lesende Referenz. |
| `ContentKind` | `ScmContentKind` | Zu lesende Textquelle (hier: `Description`). |
| `SinceUtc` | `DateTimeOffset?` | Zeitfilter (enthält bereits die Überlappung des Aufrufers); **kein Auslöse-/Trigger-Kontext** — das Plugin sieht beim Lesen nicht, ob ein Assign dahintersteht. |
| `WatermarkUtc` | `DateTimeOffset` | Obergrenze der im Batch zugelassenen Textversionen. |
| `Observations` (Batch) | `IReadOnlyList<ScmEventObservation>` | Vollständig gelesene Quelle; ein leerer Batch löst `EmptyDescriptionBatchDiagnostic` aus. |

## `ScmEventRepository`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 16–19)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProviderPrefix` / `Host` / `RepositoryId` | `string` | Kanonische Identität des Remote-Repositorys. |
| `Owner` / `Name` | `string` | Kanonischer Owner-Login und Repository-Name. |
| `OwnerId` | `string?` | Node-ID des Owners. |
| `ViewerId` / `ViewerLogin` | `string` | Identität des lesenden Ereignis-Kontos (im `SeparateAccount` das Bot-Konto); Basis des Viewer-Filters `IsViewer` im Plugin. |
| `CanWrite` | `bool` | Schreibberechtigung des Ereignis-Kontos (`viewerPermission` ≥ WRITE). |
| `Account` | `ScmAccountMode` | Kontomodus (JSON-Eigenschaft `konto`). |
| `NumericRepositoryId` | `string?` | Numerische ID (Attachment-Upload). |

## `ScmNotification`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 91–102)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ThreadId` | `string` | Inbox-Thread-ID. |
| `RepositoryNodeId` | `string` | Node-ID des betroffenen Repositorys (Zuordnung zur Allowlist). |
| `Host` / `Owner` / `Name` | `string` | Remote-Adresse des Repositorys. |
| `Kind` / `Number` | `ScmReferenceKind` / `int` | Referenzart und Nummer. |
| `Reason` | `string` | Roh-Grund; ausgewertet werden `MentionReason` (`"mention"`) und `AssignReason` (`"assign"`, Z. 96/101). |
| `UpdatedAt` | `DateTimeOffset` | Inbox-Aktualisierungszeitpunkt. |

## `ScmAssignment`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 106–107)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `EventNodeId` | `string` | Node-ID des wirksamen `assigned`-Ereignisses; geht in `ContentKey`-Suffix `assign-{eventNodeId}` ein. |
| `OccurredAt` | `DateTimeOffset` | Zeitpunkt des Ereignisses. |
| `AssignerId` / `AssignerLogin` | `string?` | Zuweisender (`actor`, Fallback `assigner`). |
| `AssigneeId` / `AssigneeLogin` | `string?` | Zugewiesenes Konto (muss der Viewer sein). |

## `ScmResponseRequest` (Marker)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 78–85)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Kind` | `ScmResponseKind` | Bei `IssueBodyUpdate` trägt `Body` die komplette neue Beschreibung; der Remote-Text wird mit `Marker` versehen. |
| `Marker` | `string` (abgeleitet, Z. 84) | `<!-- softwareschmiede:action:{ActionId} -->` — der Marker, den plugin- und hostseitige Filter an `body.Contains("<!-- softwareschmiede:")` erkennen. |

## `ScmIncomingEvent` (Entität)
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` / `Id` | `long` / `Guid` | Eingangsreihenfolge und Identität. |
| `EventKey` | `string` | Deduplizierungsschlüssel der Beobachtung. |
| `WorkItemId` | `Guid` | Zugeordnete Aufgabe. |
| `ObservationJson` | `string` | Serialisierte `ScmEventObservation` **inklusive `Trigger`** — Grundlage der Zustellungs-Gegenprüfung. |
| `Status` | `string` | Werte aus `ScmIncomingEventStatus`. |
| `Purpose` | `string?` | Zweck-Markierung; `null` = externes Ereignis, `"IssueAufbereitung"` = interner Aufbereitungsauftrag. |
| `NextAttemptUtc` / `Reason` / `DeliveryAttemptId` / `ExchangePath` / `CliRunId` / `DeliveredUtc` / `AcceptedUtc` / `Attempts` / `ErrorCode` | div. | Zustell-/Wartezustand des Dispatch-Pfads. |

## `ScmContentSnapshot` (Entität)
Datei: `src/Softwareschmiede/Domain/Entities/ScmContentSnapshot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ContentKey` | `string` | Schlüssel; trägt im Assign-Fall die Zuweisungs-Ereignisidentität (`…/assign-{eventNodeId}`). |
| `BodyHash` / `Revision` | `string` | Versionsdeduplizierung. |
| `IdentityResolved` | `bool` | Ablehnungsdiagnose abgeschlossen bestimmt. |
| `Diagnostic` | `string?` | Ablehnungsgrund (z. B. `"Eigene Antwort"`, `AssignerNotAllowedRejection`) oder `null` bei gültiger Beobachtung. |

## `ScmObservationCheckpoint` (Entität)
Datei: `src/Softwareschmiede/Domain/Entities/ScmObservationCheckpoint.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceKey` | `string` | Quellen-/Inbox-Schlüssel (z. B. `…/Issue/7/Description`, `notifications/{prefix}/{host}`). |
| `WatermarkUtc` | `long?` | Hochwassermarke der Quelle. |
| `Failures` / `NextAttemptUtc` / `ErrorCode` | `int` / `long?` / `string?` | Backoff-Zustand. |
| `Diagnostic` | `string?` | Sichtbare Diagnose; u. a. `Inbox: {rejection}`-Begründungen und `Inbox: Keine verwertbare Referenzbeschreibung`. |
