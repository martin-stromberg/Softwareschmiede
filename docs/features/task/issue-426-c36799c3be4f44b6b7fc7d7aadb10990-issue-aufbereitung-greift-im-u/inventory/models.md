# Datenmodell und Value Objects

Persistente Entitäten (`src/Softwareschmiede/Domain/Entities/`) und Provider-unabhängige
Value Objects (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/`).

## `GitRepository`

Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Repositories. |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts (für `CreateFromIssueEventAsync(repository.ProjektId, …)`). |
| `PluginTyp` | `string` | Plugin-Typ, z. B. „GitHub“; wird von `ScmPluginMatcher.MatchesPlugin` gegen `PluginPrefix`/`PluginName` geprüft. |
| `RepositoryUrl` | `string` | URL des Repositories; dient als Alias-Gruppierungsschlüssel bei der kanonischen Auflösung. |
| `RepositoryName` | `string` | Name des Repositories. |
| `Aktiv` | `bool` | Aktiv-Flag (Standard `true`); nur aktive Repositories werden enumeriert. |
| `IssueAufbereitungAktiv` | `bool` | Repository-Freischaltung der Issue-Aufbereitung; wirkt nur gemeinsam mit `scm.issueaufbereitung.enabled`. |
| `DefaultSourceBranchName` | `string?` | Basis-Branch für Feature-Branches (Backmerge-relevant). |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startskript-Konfiguration. |
| `InitialisierungKonfiguration` | `RepositoryInitialisierungKonfiguration?` | Optionale Initialisierungs-Konfiguration. |
| `Projekt` | `Projekt` | Navigation zum Projekt. |
| `DiffResults` | `List<DiffResult>` | Optionale Diff-Zuordnung. |

## `ScmObservationCheckpoint`

Datei: `src/Softwareschmiede/Domain/Entities/ScmObservationCheckpoint.cs`

Primärschlüssel ist `SourceKey` (`SoftwareschmiededDbContext.cs:120`). Bestehende
`SourceKey`-Namensräume (aus `docs/help/plugins/github-ereignisse/datenmodell.md`):
`repository/{provider}/{alias}`, `enumeration/{provider}/{host}/{repoId}`,
`notifications/{provider}/{host}`, `{provider}/{host}/{repoId}/{kind}/{number}/{contentKind}`,
`issueaufbereitung/{provider}/{host}/{repoId}` (Baseline) sowie
`backmerge-repository/{prefix}/{alias}`, `backmerge-enumeration/{provider}/{host}/{repoId}` und
`backmerge/{provider}/{host}/{repoId}/{nummer}`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceKey` | `string` | Checkpoint-Schlüssel; freier Namensraum, keine Schema-Änderung für neue Präfixe nötig. |
| `WatermarkUtc` | `long?` | Wasserstand (Unix-ms); bei der Baseline `issueaufbereitung/…` = Beobachtungsbeginn. |
| `Failures` | `int` | Fehlerzähler für den Backoff. |
| `NextAttemptUtc` | `long?` | Nächster Prüfzeitpunkt; `long.MaxValue` = dauerhaft gesperrt/Endzustand. |
| `ErrorCode` | `string?` | Name des `ScmProviderFailureKind` bzw. „Temporary“. |
| `Diagnostic` | `string?` | Diagnosetext, in der Ereignisübersicht sichtbar. |

## `ScmContentSnapshot`

Datei: `src/Softwareschmiede/Domain/Entities/ScmContentSnapshot.cs`

Primärschlüssel `ContentKey` (`SoftwareschmiededDbContext.cs:119`). Wird von
`ScmEventStore.AcceptBatchAsync` geschrieben; `ErkennenUndEinreihenAsync` prüft darüber die
„Erstbeobachtung“ (kein Snapshot zum `ContentKey`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ContentKey` | `string` | Stabiler Schlüssel des Textes (provider/host/repoId/kind/number/contentKind/remoteNodeId). |
| `BodyHash` | `string` | SHA-256 des Textes. |
| `Revision` | `string` | Revision der beobachteten Fassung. |
| `IdentityResolved` | `bool` | Auflösungsstatus der Identität. |
| `Diagnostic` | `string?` | Diagnose (u. a. `ScmEventStore.UnassignedDiagnostic`, `AmbiguousAssignmentDiagnostic`). |

## `ScmIncomingEvent`

Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

Unique-Indizes: `Id`, `EventKey` (`SoftwareschmiededDbContext.cs:113-114`); weitere Indizes auf
`(AufgabeId, Sequence)` und `(Status, NextAttemptUtc)`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Fortlaufende Sequenz. |
| `Id` | `Guid` | Ereignis-ID. |
| `EventKey` | `string` | Dedup-Schlüssel; für Aufbereitungsaufträge `{ContentKey}/IssueAufbereitung`. |
| `AufgabeId` | `Guid` | Zugeordnete Aufgabe (Aufbereitung: die `Zweck`-Aufgabe). |
| `ObservationJson` | `string` | Serialisierte `ScmEventObservation` (`ScmEventStore.JsonOptions`). |
| `Status` | `string` | Status, Standard `"Aufgenommen"`; u. a. `Wartend`, `Zugestellt`, `Verarbeitet`, `VerarbeitungUngeklärt`. |
| `Reason` | `string?` | Begründung/Wartegrund. |
| `ReceivedUtc` | `long` | Empfangszeitpunkt (Unix-ms). |
| `Attempts` | `int` | Zustellversuche. |
| `NextAttemptUtc` | `long?` | Nächster Versuch; `null` = sofort fällig. |
| `ErrorCode` | `string?` | Fehlercode. |
| `LeaseOwner` / `LeaseUntilUtc` | `string?` / `long?` | Ausführungs-Lease. |
| `DeliveryAttemptId` | `Guid?` | Aktueller Zustellversuch. |
| `CliRunId` | `string?` | Zugeordneter CLI-Lauf. |
| `ExchangePath` | `string?` | Dateitool-Austauschpfad. |
| `ExchangeRestrictedWriteSidsJson` | `string?` | Beschränkende Prozessidentitäten. |
| `DeliveredUtc` / `AcceptedUtc` | `long?` | Zustell-/Annahmezeitpunkt. |
| `ReadyConfirmedRunId` | `string?` | Bestätigte Run-ID. |
| `Zweck` | `string?` | Zweck-Kennzeichnung; `null` = reguläres externes Ereignis, `"IssueAufbereitung"` = interner Aufbereitungsauftrag (umgeht Trigger-Validierung, eigener Ausführungspfad). |

## `IssueReferenz`

Datei: `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

Unique-Indizes: `SourceKey`, `AufgabeId` (`SoftwareschmiededDbContext.cs:272-285`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID. |
| `AufgabeId` | `Guid` | Zugehörige Aufgabe. |
| `IssueNummer` | `int?` | Issue-Nummer beim Provider. |
| `Titel` | `string` | Issue-Titel. |
| `Body` | `string?` | Beschreibungstext. |
| `LabelsJson` | `string` | Labels als JSON-Array. |
| `Milestone` | `string?` | Milestone. |
| `IssueUrl` | `string?` | Issue-URL. |
| `SourceKey` | `string?` | Kanonischer Dedup-Schlüssel `{provider}/{host}/{repositoryId}/Issue/{number}`; die Aufbereitung nutzt den eigenen Namensraum `…/IssueAufbereitung/{number}`. |

## `Aufgabe` (relevante Eigenschaft)

Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Zweck` | `string?` | Zeile 117-120: Zweck-Markierung; `null` = reguläre Arbeitsaufgabe. `Zweck`-Aufgaben werden in `ScmEventMonitoringService.RunOnceAsync` (Z. 50-51) von der Quellenliste ausgenommen — sie ziehen weder Ereignisse noch liefern sie PR-Kontext. |

## `ScmEventRepository`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:15`

Record: `ScmEventRepository(string ProviderPrefix, string Host, string RepositoryId,
string Owner, string Name, string? OwnerId, string ViewerId, string ViewerLogin, bool CanWrite,
ScmKontoModus Konto = ScmKontoModus.Benutzerkonto, string? NumericRepositoryId = null)`

Kanonische, credentialfreie Repository-/Authentifizierungsidentität, geliefert von
`IScmEventSource.GetEventRepositoryAsync`. `Konto` markiert, über welches Konto die Auflösung
lief — relevant für die Credential-Wahl späterer Remote-Aktionen (Offene Frage 3 der
Anforderung). Wird als Dictionary-Schlüssel für die lokale Repository-Zuordnung verwendet
(`localRepositories` in `ScmEventMonitoringService` und `ScmBackmergeMonitoringService`).

## `ScmEventReference`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:20`

Record: `ScmEventReference(ScmEventRepository Repository, ScmReferenceKind Kind, int Number)`.

Validierter Ursprung einer Beobachtung oder Antwort; die Enumeration liefert nur
`(Repository, Kind, Number)` — **kein `CreatedAt`** (zentral für den „neu angelegt“-Ansatz der
Anforderung).

## `ScmEventReadRequest` / `ScmEventBatch` / `ScmEventObservation`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:23-52`

- `ScmEventReadRequest(ScmEventReference Reference, ScmContentKind ContentKind,
  DateTimeOffset? SinceUtc, DateTimeOffset WatermarkUtc)` — separat checkpointbare Quelle.
- `ScmEventBatch(ScmEventReadRequest Request, IReadOnlyList<ScmEventObservation> Observations)` —
  vollständig gelesene Quelle, atomar mit Checkpoint speicherbar.
- `ScmEventObservation(Reference, ContentKind, RemoteNodeId, RemoteCommentId, CreatedAt,
  UpdatedAt, LastEditedAt, AuthorId, AuthorLogin, EditorId, EditorLogin, Body, Url, ObservedAt,
  ViewerReactions)` mit abgeleiteten Membern:
  - `BodyHash` — SHA-256 über den UTF-8-Text.
  - `ViewerHasReacted` — Konto hat bereits remote reagiert.
  - `Revision` — `LastEditedAt ?? CreatedAt`.
  - `ContentKey` — stabiler Textschlüssel (konto-agnostisch).
  - `EventKey` — `{ContentKey}/{Revision:O}/{BodyHash}` (Dedup-Schlüssel, konto-agnostisch).

## `ScmRepositoryContext`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRepositoryContext.cs`

Record-Klasse mit `GitRepositoryId`, `PluginPrefix`, `RepositoryId`, `Konto`
(Standard `ScmKontoModus.Benutzerkonto`). Eingabe für `GetEventRepositoryAsync` — der
Konstruktor validiert nicht-leere IDs.

## `ScmNotification`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:83-89`

Roh-Eintrag der Notifications-Inbox (`SeparatesKonto`-Pfad): `ThreadId`, `RepositoryNodeId`,
`Host`, `Owner`, `Name`, `Kind`, `Number`, `Reason`, `UpdatedAt`; Konstante
`MentionReason = "mention"` — nur Erwähnungen führen zur Beobachtung einer Referenz.

## `Issue`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs`

Record: `Issue(int Nummer, string Titel, string? Body, IReadOnlyList<string> Labels,
string? Milestone, string? IssueUrl)` mit `IstGeschlossen` (init-only) — Anlagedaten, die
`IScmEventSource.ReadIssueAsync` liefert und `ErkennenUndEinreihenAsync` bzw. der
Aufbereitungs-Ausführungspfad nutzen.

## `ScmResponseRequest` / `ScmResponseResult`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:70-80`

`ScmResponseRequest` trägt die Host-vorgegebene Antwort (`ActionId`, `ScmResponseKind`, Body,
Referenz u. a.) inkl. `Marker`-Eigenschaft (`<!-- softwareschmiede:action:{ActionId} -->`);
`ScmResponseResult(RemoteId, Url?, AlreadyExisted)` ist das bestätigte Ergebnis. Relevant für
den unverändert bleibenden Ausführungsweg (`IssueBodyUpdate`, `Comment` via Outbox).
