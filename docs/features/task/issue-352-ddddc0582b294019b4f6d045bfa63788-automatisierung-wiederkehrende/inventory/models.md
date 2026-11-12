# Datenmodell — SCM-Ereignis-Pipeline

Nur die für die Anforderung tragenden Records: Sie bestimmen, welche Daten eine `@softwarewerkstatt`-Erwähnung in einem Workflow-erzeugten Issue an die Überwachung liefert und woran die Owner-Prüfung hängt. Quelle aller Records: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (außer `Issue`, `ScmRepositoryContext`).

## `ScmEventRepository`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeilen 15–17)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProviderPrefix` | `string` | Plugin-Prefix (`github`) |
| `Host` | `string` | API-Host (`github.com` bzw. `api.github.com`-Ableitung) |
| `RepositoryId` | `string` | Kanonische API-Repository-ID (`node_id` des Repos — Inbox-Zuordnungsschlüssel, siehe `ScmEventMonitoringService`) |
| `Owner` | `string` | Owner-Login aus `repos/{owner}/{name}` → `owner.login` (hier: `martin-stromberg`) |
| `Name` | `string` | Kanonischer Repository-Name |
| `OwnerId` | `string?` | `owner.node_id` — primärer Identitätsvergleich der Owner-Prüfung |
| `ViewerId` | `string` | `node_id` des abfragenden Kontos (im `SeparatesKonto`: `softwarewerkstatt`) |
| `ViewerLogin` | `string` | Login des abfragenden Kontos — Ziel der frei stehenden `@ViewerLogin`-Erwähnung |
| `CanWrite` | `bool` | `push \|\| admin \|\| maintain` aus Repo-Permissions |
| `Konto` | `ScmKontoModus` | Kontomodus, Default `Benutzerkonto` |

Befüllt von `GitHubPlugin.GetEventRepositoryAsync` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs:29-47`).

## `ScmEventReference`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeile 20)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Repository` | `ScmEventRepository` | Aufgelöster Repository-Kontext |
| `Kind` | `ScmReferenceKind` | `Issue` oder `PullRequest` |
| `Number` | `int` | Issue-/PR-Nummer |

Aus einer Mention-Notification abgeleitete Referenzen tragen `Kind = Issue` (Subject-Type `Issue` des Notification-Threads) — der Workflow-Erzeugungsfall landet also genau hier.

## `ScmEventObservation`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeilen 30–52)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reference` | `ScmEventReference` | Verknüpfte Referenz |
| `ContentKind` | `ScmContentKind` | `Description` (Issue-Body!), `IssueComment`, `ReviewComment`, `PullRequestReview` |
| `RemoteNodeId` | `string` | GraphQL-`id` des Textknotens |
| `RemoteCommentId` | `string?` | REST-Kommentar-ID (`null` bei `Description`) |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | Remote-Zeitstempel |
| `LastEditedAt` | `DateTimeOffset?` | GraphQL `lastEditedAt` — steuert, ob Autor oder **Bearbeiter** validiert wird |
| `AuthorId` / `AuthorLogin` | `string?` | GraphQL `author { login ... on Node { id } }` — für `GITHUB_TOKEN`-Erzeugnisse `github-actions[bot]` |
| `EditorId` / `EditorLogin` | `string?` | GraphQL `editor { login ... on Node { id } }` — zählt statt Autor, sobald `LastEditedAt` gesetzt |
| `Body` | `string` | Textinhalt (hier: Issue-Body mit `@softwarewerkstatt`-Zeile) |
| `Url` | `string` | Anzeige-URL |
| `ObservedAt` | `DateTimeOffset` | Lokaler Beobachtungszeitpunkt |
| `ViewerReactions` | `IReadOnlyList<string>?` | `reactionGroups { content viewerHasReacted }` → Reaktionen des Ereignis-Kontos |
| `BodyHash` | `string` (berechnet) | SHA-256 über UTF-8-Body |
| `ViewerHasReacted` | `bool` (berechnet) | `ViewerReactions is { Count: > 0 }` |
| `Revision` | `DateTimeOffset` (berechnet) | `LastEditedAt ?? CreatedAt` |
| `ContentKey` / `EventKey` | `string` (berechnet) | Dedup-/Snapshotschlüssel |

Befüllt von `GitHubPlugin.AddObservation` (`GitHubPlugin.Events.cs:232-264`). Wichtig: `author.id`/`login` und `editor.id`/`login` stammen direkt aus der GraphQL-Antwort — ein `GITHUB_TOKEN`-Issue hat `author.login = "github-actions[bot]"` und erfüllt die Owner-Prüfung nicht.

## `ScmNotification`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeilen 71–77)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ThreadId` | `string` | Notification-Thread-ID |
| `RepositoryNodeId` | `string` | `repository.node_id` — Zuordnungsschlüssel zur registrierten Repository-Allowlist |
| `Host` | `string` | Host |
| `Owner` / `Name` | `string` | Repository owner/name aus der Notification |
| `Kind` | `ScmReferenceKind` | `Issue`/`PullRequest` (andere Subject-Typen werden in `GitHubPlugin.EnumerateNotificationsAsync` verworfen) |
| `Number` | `int` | Aus `subject.url` geparste Referenznummer |
| `Reason` | `string` | GitHub-`reason`; nur `mention` zählt |
| `UpdatedAt` | `DateTimeOffset` | `updated_at` des Threads |
| `MentionReason` | `const string = "mention"` | Konstante für den Auslöser-Grund |

## `Issue`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs` (Zeilen 10–23)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Nummer` | `int` | Issue-Nummer |
| `Titel` | `string` | Wird zum Aufgabentitel bei `CreateFromIssueEventAsync` |
| `Body` | `string?` | Wird mit Trigger-Beobachtung zu Aufgabentext komponiert (`ScmEventTaskResolver.ComposeBody`) |
| `Labels` | `IReadOnlyList<string>` | Label-Namen (für Duplikatprüfung nicht ausgewertet — reine Anzeige-/Metadaten) |
| `Milestone` | `string?` | Milestone-Titel |
| `IssueUrl` | `string?` | URL |
| `IstGeschlossen` | `bool` | Remote-Status (nur `ReadIssueAsync`-Pfad) |

## `ScmRepositoryContext`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRepositoryContext.cs` (Zeilen 6–34)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GitRepositoryId` | `Guid` | Lokale Repository-Entitäts-ID |
| `PluginPrefix` | `string` | Provider-Prefix |
| `RepositoryId` | `string` | Kanonische API-Repository-ID |
| `Konto` | `ScmKontoModus` | Wählt Credential (`ClientFor`) für den Ereignispfad |
