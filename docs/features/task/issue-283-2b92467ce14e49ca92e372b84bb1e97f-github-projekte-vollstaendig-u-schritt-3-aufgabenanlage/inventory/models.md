# Bestandsaufnahme — Datenmodell

Zurück: [../inventory.md](../inventory.md)

## `Aufgabe`

Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `Id`, `ProjektId`, `GitRepositoryId` | Guid / Guid? | Identität und Zuordnung (`ProjektId` Pflicht, `GitRepositoryId` optional, `OnDelete(SetNull)`) |
| `Titel`, `AnforderungsBeschreibung` | string / string? | Anzeige und Prompt-Quelle |
| `Status` | `AufgabeStatus` | `Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert` u. a. |
| `LokalerKlonPfad`, `BranchName`, `BasisBranchName` | string? | Startzustand; `null` → Dispatch klont beim ersten Ereignis |
| `IssueReferenz`, `PullRequests`, `AlertReferenz` | Navigation | Referenzträger |

## `IssueReferenz`

Datei: `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `Id`, `AufgabeId` | Guid | PK / FK (1:1, Cascade) |
| `IssueNummer` | int? | Provider-Issue-Nummer |
| `Titel`, `Body`, `LabelsJson`, `Milestone`, `IssueUrl` | string / string? | Metadaten |

Konfiguration (`SoftwareschmiededDbContext.cs:306-310`): nur `HasKey(Id)` — **kein Unique-Index** außer `AufgabeId` (implizit über die 1:1-Beziehung, `HasIndex(AufgabeId).IsUnique()` aus der `Aufgabe`-Konfiguration Z. 215-218). Kein Repository-Feld — die Zuordnung zum Repository läuft über `Aufgabe.GitRepositoryId`.

## `PullRequestReferenz`

Datei: `src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `Provider` | `PullRequestProvider` (enum, string-konvertiert) | GitHub / BitbucketCloud / BitbucketServerDataCenter |
| `Rolle` | `PullRequestReferenzRolle` | `CreatedByTask` (Default) / `ReviewSource`; Unique-Index `AufgabeId` gefiltert auf `ReviewSource` |
| `RepositoryId` | string (max 500, required) | Normalisierte API-ID `owner/name` (via `PullRequestRepositoryId.Normalize`), **nicht** die node_id |
| `PullRequestNumber` | int | PR-Nummer |
| `SourceBranch`, `SourceRepositoryId`, `SourceRepositoryUrl`, `SourceRef`, `TargetBranch`, `HeadSha` | string | Checkout-Daten; `SourceBranch`/`SourceRepositoryId`/`TargetBranch` required |
| `MonitoringPhase`, `NextCheckUtc`, `CreatedUtc` | enum / long | PR-Monitoring |

Unique-Index `(Provider, RepositoryId, PullRequestNumber)` (`SoftwareschmiededDbContext.cs:356`) — Grundlage des `DbUpdateException`-Rennschutzes.

## `ScmIncomingEvent` / `ScmContentSnapshot` / `ScmObservationCheckpoint`

`ScmIncomingEvent.AufgabeId` ist `Guid` (required) — Inbox-Einträge existieren nur mit Aufgabe. `ScmContentSnapshot` (PK `ContentKey`): `BodyHash`, `Revision`, `IdentityResolved`, `Diagnostic`. `ScmObservationCheckpoint` (PK `SourceKey`): `WatermarkUtc`, `Failures`, `NextAttemptUtc`, `ErrorCode`, `Diagnostic`.

## `Projekt` / `GitRepository`

`GitRepository`: `Id`, `ProjektId`, `PluginTyp`, `RepositoryUrl`, `RepositoryName`, `Aktiv` (default true). `ProjektId` der neuen Aufgabe kommt aus diesem Datensatz.

## Migrationen

`src/Softwareschmiede/Migrations/` (zuletzt `20261011045239_AddScmEventInbox`); `App.xaml.cs:98` ruft `db.Database.MigrateAsync()`. `dotnet ef` 10.0.10 ist als global installiertes Tool verfügbar; Design-Time-Factory: `SoftwareschmiededDbContextDesignTimeFactory.cs`.
