# Datenmodell: GitHub-Notifications als Eingangskanal

Betroffene Datenmodellklassen (Value Objects in `Softwareschmiede.Plugin.Contracts`, Entities in `Softwareschmiede.Domain.Entities`, Plugin-Setting-Modelle).

## `ScmEventRepository`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 14–15)

Vom Provider geladene Repository- und Authentifizierungsdaten, ohne Credentials. Wird von `GitHubPlugin.GetEventRepositoryAsync` erzeugt und in `ScmEventMonitoringService` zur kanonischen Repository-Auflösung sowie in `ScmEventValidationService.GetRejection` für die Owner-Regel verwendet.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProviderPrefix` | `string` | Plugin-Prefix des Providers (z. B. `Softwareschmiede.GitHub`) |
| `Host` | `string` | API-/SCM-Host (z. B. `github.com` oder GHE-Host) |
| `RepositoryId` | `string` | Kanonische Remote-ID (GitHub: `node_id` des Repos) |
| `Owner` | `string` | Kanonischer Owner-Login |
| `Name` | `string` | Kanonischer Repository-Name |
| `OwnerId` | `string?` | Owner-`node_id` (Grundlage der Owner-Regel in `ScmEventValidationService`) |
| `ViewerId` | `string` | `node_id` des authentifizierten Tokens-Inhabers |
| `ViewerLogin` | `string` | Login des authentifizierten Tokens-Inhabers |
| `CanWrite` | `bool` | Schreibrecht des Viewers (`push`/`admin`/`maintain`-Permission) |

## `ScmEventReference`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 18)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Repository` | `ScmEventRepository` | Validierter Ursprung |
| `Kind` | `ScmReferenceKind` | `Issue` oder `PullRequest` |
| `Number` | `int` | Issue-/PR-Nummer |

## `ScmEventReadRequest`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 21–22)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reference` | `ScmEventReference` | Zu lesende Referenz |
| `ContentKind` | `ScmContentKind` | Textquelle (eine Quelle = ein Checkpoint) |
| `SinceUtc` | `DateTimeOffset?` | Inkrementelles Lesefenster (enthält bereits Caller-Überlappung) |
| `WatermarkUtc` | `DateTimeOffset` | Obergrenze: spätere Versionen werden nicht committet |

## `ScmEventBatch`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 25)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Request` | `ScmEventReadRequest` | Anfrage-Metadaten inkl. Watermark |
| `Observations` | `IReadOnlyList<ScmEventObservation>` | Vollständig gelesene Textversionen |

## `ScmEventObservation`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 28–47)

Text und Identität einer gemeinsamen Remoteversion. Eingang für `ScmEventValidationService.GetRejection` und `ScmEventStore.AcceptBatchAsync`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reference` | `ScmEventReference` | Ursprung |
| `ContentKind` | `ScmContentKind` | Textquelle |
| `RemoteNodeId` | `string` | GraphQL-Node-ID des Texts |
| `RemoteCommentId` | `string?` | REST-Kommentar-ID (für Reaktions-Endpunkte) |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | Remote-Zeitstempel |
| `LastEditedAt` | `DateTimeOffset?` | Bearbeitungszeitpunkt (grundlage der „edited"-Regel) |
| `AuthorId` / `AuthorLogin` | `string?` | Autor der Textversion |
| `EditorId` / `EditorLogin` | `string?` | Letzter Bearbeiter |
| `Body` | `string` | Textinhalt (enthält heute das Trigger-Keyword bzw. künftig die `@mention`) |
| `Url` | `string` | Deep-Link zur Textversion |
| `ObservedAt` | `DateTimeOffset` | Beobachtungszeitpunkt |
| `BodyHash` | `string` (berechnet) | SHA-256 über UTF-8-Text (Z. 34) |
| `Revision` | `DateTimeOffset` (berechnet) | `LastEditedAt ?? CreatedAt` (Z. 37) |
| `ContentKey` | `string` (berechnet) | Stabiler Schlüssel `{provider}/{host}/{repoId}/{kind}/{number}/{contentKind}/{nodeId}` (Z. 40–43) |
| `EventKey` | `string` (berechnet) | Deduplizierungsschlüssel `{ContentKey}/{Revision:O}/{BodyHash}` (Z. 46) |

## `ScmResponseRequest` / `ScmResponseResult`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 53–63)

`ScmResponseRequest`: Hostaktion mit deterministischer `ActionId` und `Marker` (`<!-- softwareschmiede:action:{ActionId} -->`, Z. 59) — Felder: `Reference`, `ActionId`, `Kind` (`ScmResponseKind`), `Body`, `Reaction`, `ContentKind`, `RemoteCommentId`, `RemoteNodeId`, `Title`, `HeadBranch`, `BaseBranch`.
`ScmResponseResult`: `RemoteId`, `Url?`, `AlreadyExisted`.

## `ScmRepositoryContext`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRepositoryContext.cs` (Z. 4–27)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GitRepositoryId` | `Guid` | ID des lokal persistierten `GitRepository` |
| `PluginPrefix` | `string` | Plugin-Prefix des Providers |
| `RepositoryId` | `string` | Kanonische API-Repository-ID (z. B. Repository-URL/Alias) |

## `ScmEventOptions` / `ScmEventChangeNotifier` / `ScmEventStartupGate`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOptions.cs`

- `ScmEventOptions` (Z. 4–12): `PollInterval` (Default 60 s), `AcceptanceTimeout` (2 min), `CompletionTimeout` (30 min).
- `ScmEventChangeNotifier` (Z. 15–21): `Changed`-Event + `Notify()` — benachrichtigt Ansichten nach dauerhaften Änderungen.
- `ScmEventStartupGate` (Z. 24–31): hält Hintergrunddienste bis nach der DB-Migration an (`WaitAsync`/`Ready`).

## `ScmObservationCheckpoint` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/ScmObservationCheckpoint.cs`

Persistenter Beobachtungs-/Backoff-Zustand je `SourceKey`. DbSet `SoftwareschmiededDbContext.ScmObservationCheckpoints` (`src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs` Z. 95).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceKey` | `string` (PK) | Quellenschlüssel; heutige Namensräume: `repository/{prefix}/{alias}`, `enumeration/{provider}/{host}/{repoId}`, `{provider}/{host}/{repoId}/{kind}/{number}/{contentKind}` (`ScmEventMonitoringService` Z. 70, 125–126, 193–194) |
| `WatermarkUtc` | `long?` | Letzter erfolgreicher Beobachtungs-Watermark (Unix-ms) |
| `Failures` | `int` | Fehlerzähler für Backoff |
| `NextAttemptUtc` | `long?` | Frühester nächster Versuch (`long.MaxValue` = gesperrt) |
| `ErrorCode` | `string?` | `ScmProviderFailureKind`-Name oder `Temporary` |
| `Diagnostic` | `string?` | Anzeigediagnose (z. B. `UnassignedDiagnostic`) |

## `ScmContentSnapshot` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/ScmContentSnapshot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ContentKey` | `string` (PK) | Schlüssel der Textquelle (siehe `ScmEventObservation.ContentKey`) |
| `BodyHash` | `string` | Zuletzt gesehener Inhaltshash |
| `Revision` | `string` | Zuletzt gesehene Revision (Format „O") |
| `IdentityResolved` | `bool` | Bearbeiter-Identität erfolgreich geprüft |
| `Diagnostic` | `string?` | Ablehnungs-/Zuordnungsdiagnose |

## `ScmIncomingEvent` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

Inbox-Eintrag eines akzeptierten Ereignisses. DbSet `ScmIncomingEvents` (DbContext Z. 91).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Fortlaufende Reihenfolge (Dispatch-Ordnung) |
| `Id` | `Guid` | Ereignis-ID |
| `EventKey` | `string` | Dedup-Schlüssel der Version |
| `AufgabeId` | `Guid` | Zugeordnete Aufgabe (Invariante: immer gesetzt) |
| `ObservationJson` | `string` | Serialisierte `ScmEventObservation` |
| `Status` | `string` | z. B. `Aufgenommen`, `ZustellungVorbereitet`, `Zugestellt`, `Verarbeitet`, `Wartend`, `Abgebrochen`, `ZustellungUngewiss`, `VerarbeitungUngeklärt` |
| `Reason` | `string?` | Warte-/Fehlergrund |
| `ReceivedUtc` | `long` | Eingangszeitpunkt (Unix-ms) |
| `Attempts` / `NextAttemptUtc` / `ErrorCode` | `int` / `long?` / `string?` | Retry-/Backoff-Zustand |
| `LeaseOwner` / `LeaseUntilUtc` | `string?` / `long?` | Zustellungs-Lease |
| `DeliveryAttemptId` | `Guid?` | Korrelierter Zustellversuch |
| `CliRunId` | `string?` | CLI-Lauf der Zustellung |
| `ExchangePath` / `ExchangeRestrictedWriteSidsJson` | `string?` | Antwortkanal-Dateipfad bzw. eingeschränkte Schreib-SIDs |
| `DeliveredUtc` / `AcceptedUtc` / `ReadyConfirmedRunId` | `long?` / `long?` / `string?` | Zustell-/Annahme-Zeitpunkte und bestätigter Lauf |

## `ScmOutgoingAction` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/ScmOutgoingAction.cs`

Ausgehende Hostaktion (Empfangsreaktion, Warte-Kommentar, Antwort). DbSet `ScmOutgoingActions` (DbContext Z. 97): `Id`, `EventId`, `AttemptId`, `Purpose`, `RequestJson` (`ScmResponseRequest`), `Status` (`Pending`/`Succeeded`/…), `RemoteId`, `Attempts`, `NextAttemptUtc`, `ErrorCode`, `Error`.

## `ScmTaskLease` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/ScmTaskLease.cs`

Aufgaben-Lease für instanzübergreifende Bearbeitung (`AufgabeId`, `Owner`, `ExpiresUtc`); verwaltet über `ScmEventStore.ClaimTaskAsync`/`RenewTaskAsync`/`ReleaseTaskAsync`.

## `GitRepository` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

Projekt-Repository; die Projektbeschränkung der Requirement greift über `Aktiv` + `RepositoryUrl`/`PluginTyp`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Repository-ID |
| `ProjektId` | `Guid` | Zugehöriges Projekt |
| `PluginTyp` | `string` | Provider (`PluginPrefix` oder `PluginName`) |
| `RepositoryUrl` | `string` | Alias/URL — Basis der Alias-Auflösung in `ScmEventMonitoringService` |
| `RepositoryName` | `string` | Anzeigename |
| `Aktiv` | `bool` | Nur aktive Repositories werden überwacht |
| `DefaultSourceBranchName` | `string?` | Konfigurierter Basis-Branch |
| `StartKonfiguration` / `InitialisierungKonfiguration` | Entity? | Start-/Init-Skript-Konfiguration |
| `Projekt` / `DiffResults` | Navigationen | Projektbezug, Diff-Kontext |

## `AppEinstellung` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs`

Generischer Key-Value-Speicher (`Id`, `Schluessel`, `Wert`, `AktualisiertAm`); SCM-relevante Schlüssel sind als Konstanten in `AppEinstellungService` definiert (siehe [Logik](logic.md)).

## `PluginSettingField` / `PluginSettingGroup` / `PluginSettingFieldType`
Dateien: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs`, `PluginSettingGroup.cs`, `PluginSettingFieldType.cs`

- `PluginSettingField` (record): `Key`, `Label`, `FieldType`, `Placeholder`, `Description`, `IsRequired`, `EnumOptions`, `DefaultValue`. Schlüssel wird als `<PluginPrefix>.<Key>` im `ICredentialStore` persistiert (vgl. `PluginSettingsService.BuildKey`).
- `PluginSettingGroup` (record): `GroupName`, `Fields`.
- `PluginSettingFieldType`: `Text`, `Secret`, `Url`, `Integer`, `Boolean`, `Enum`, `FilePath`, `CommandLineParameters = 8` (Wert 7 reserviert).
