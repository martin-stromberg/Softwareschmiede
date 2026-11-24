# Bestandsaufnahme: Datenmodell

## `ScmIncomingEvent`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | PK (ValueGeneratedOnAdd); Eingangsreihenfolge. |
| `Id` | `Guid` | Externe Identität (unique index). |
| `EventKey` | `string` | Deduplikationsschlüssel (unique index). |
| `WorkItemId` | `Guid` | Aufgabenbezug — **kein FK, kein Navigation**: Waisen nach Aufgabenlöschung möglich. Index `{WorkItemId, Sequence}`. |
| `ObservationJson` | `string` | Persistierte `ScmEventObservation`. |
| `Status` | `string` | Statuswerte aus `ScmIncomingEventStatus` (Default `Aufgenommen`). Index `{Status, NextAttemptUtc}`. |
| `Reason` | `string?` | Benutzerlesbarer Warte-/Klärungsgrund. |
| `ReceivedUtc` | `long` | Eingangszeitpunkt (Unix ms). |
| `Attempts` | `int` | Zustellversuche. |
| `NextAttemptUtc` | `long?` | Frühester nächster Versuch (Unix ms); `long.MaxValue` = bewusster manueller Neuversand nötig (Auth-/Konfig-/Startfehler); `null` = sofort fällig. |
| `ErrorCode` | `string?` | `ScmProviderFailureKind` bei Anbieterfehlern. |
| `LeaseOwner`/`LeaseUntilUtc` | `string?`/`long?` | Versuchs-Lease. |
| `DeliveryAttemptId` | `Guid?` | Korrelation des laufenden Zustellversuchs; `null` = noch nie zugestellt. |
| `CliRunId` | `string?` | Run-Id des geprüften Laufs. |
| `ExchangePath` / `ExchangeRestrictedWriteSidsJson` | `string?` | Antwortkanal-Verzeichnis + SID-Nachweis. |
| `DeliveredUtc` / `AcceptedUtc` | `long?` | Zustell-/Annahme-Zeitpunkt. |
| `ReadyConfirmedRunId` | `string?` | Bestätigte Run-Id für Ein-Eingabe-Bestätigung. |
| `Purpose` | `string?` | Zweck-Kennzeichnung (`"IssueAufbereitung"` u. a.); `null` = regulär. |

## `WorkItem` (nur relevante Felder)
Datei: `src/Softwareschmiede/Domain/Entities/WorkItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Status` | `WorkItemStatus` | `New`, `Started`, `Waiting`, `Completed`, `Archived` — `Completed`/`Archived` lösen `BlockReason` aus. |
| `PausedUntilUtc` | `DateTimeOffset?` | Pausenende; `IsCurrentlyPaused(now)` für aktive Pause. |
| `GitRepositoryId` | `Guid?` | Repository-Zuordnung — `null` möglich; Dispatch dereferenziert `!.Value`. |
| `GitRepository` | `GitRepository?` | Navigation (per `Include` geladen); `IssuePreparationEnabled` für Aufbereitungspfad. |
| `AiPluginPrefix` | `string?` | Gespeichertes CLI-Plugin der Aufgabe (Attribution-Fallback). |
| `LocalClonePath` / `BaseBranchName` / `BranchName` | `string?` | Klonpfad/Branches; beim Abschluss via `GitWorkspace = null` geleert. |
| `Kind` | `WorkItemKind` | `Instruction` nach erster Aufbereitungs-Zustellung. |
