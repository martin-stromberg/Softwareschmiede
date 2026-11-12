# Logik — Validierung, Monitoring, Task-Auflösung, GitHub-Plugin

Der fachliche Pfad „Issue mit `@softwarewerkstatt`-Erwähnung → automatische Aufgabe": Notification (mention) → Referenzableitung → Read (`Description`/`IssueComment`) → Validierung → Task-Auflösung → Inbox/Dispatch.

## `ScmEventValidationService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs` (83 Zeilen)

| Methode / Element | Sichtbarkeit | Kurzbeschreibung |
|-------------------|-------------|------------------|
| `DefaultTriggerKeyword = "$Softwareschmiede"` | `public const` | Keyword-Default (nur `Benutzerkonto`) |
| `KeineErwaehnungRejection` | `public const` | „Keine Erwähnung des separaten Kontos" |
| `BereitsReagiertRejection` | `public const` | „Bereits mit dem Konto reagiert" |
| `NormalizeTriggerKeyword` | `public static` | Leerer Wert → Default, sonst Trim |
| `GetRejection(ScmEventObservation, string?)` | `public static` | Liefert Ablehnungsgrund oder `null`; Regeln s. u. |
| `HasFreeStandingTrigger(body, trigger)` | `private static` | Case-insensitiver `IndexOf`-Scan; Treffer zählt nur, wenn weder Zeichen davor noch danach in `QuoteChars` (`'"`´‚‘’„“”«»`) liegt — eine eigene Zeile `@softwarewerkstatt` erfüllt das |
| `SameIdentity(...)` | `private static` | Primär `id`-Gleichheit (`Ordinal`), sonst `login`-Gleichheit (`OrdinalIgnoreCase`) |

**`GetRejection`-Regelkette (Zeilen 33–61), exakt in dieser Reihenfolge:**

1. **Trigger:** `SeparatesKonto` → frei stehendes `@<repository.ViewerLogin>` erforderlich (also `@softwarewerkstatt`); `Benutzerkonto` → frei stehendes Trigger-Keyword. Sonst `KeineErwaehnungRejection` bzw. „Kein Trigger".
2. **Kontoreaktion-Dedup:** Nur für `IssueComment`/`ReviewComment`/`PullRequestReview` mit `ViewerHasReacted` → `BereitsReagiertRejection`. **`Description` ist ausgenommen** — Issue-Beschreibungen unterliegen dieser Dedup nicht.
3. **Viewer=Owner-Regel:** nur `Benutzerkonto` — im `SeparatesKonto` entfällt sie (Zeile 49).
4. **Aktor = Autor oder Bearbeiter:** `edited = LastEditedAt is not null` → bei editierter Version zählt `Editor*` statt `Author*` (Zeilen 51–53). **Konsequenz:** Wird die Beschreibung eines Bot-erzeugten Issues nachträglich vom Owner editiert, wäre der Bearbeiter der Owner und die Prüfung formal bestanden.
5. **Aktor bestimmbar:** weder ID noch Login → „Bearbeiter nicht bestimmbar".
6. **Owner-Prüfung:** `!SameIdentity(actorId, actorLogin, repository.OwnerId, repository.Owner)` → **„Textversion stammt nicht vom Repository-Owner"** (Zeile 56–57). Ein per `GITHUB_TOKEN` erzeugtes Issue (Autor `github-actions[bot]`) scheitert hier — die kritische Abhängigkeit der Anforderung.
7. **Eigenmarker:** `Body` enthält `<!-- softwareschmiede:` → „Eigene Antwort".

Aufgerufen von: `ScmEventMonitoringService.RunOnceAsync` (Trigger-Suche für `ScmEventTaskResolver`, `ScmEventMonitoringService.cs:187`) und `ScmEventStore.AcceptBatchAsync` (Ablehnungsdiagnose je Snapshot).

## `ScmEventMonitoringService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` (307 Zeilen)

| Methode / Element | Sichtbarkeit | Kurzbeschreibung |
|-------------------|-------------|------------------|
| `RunOnceAsync` (Hauptschleife, Zeilen ~55–248) | (intern) | Pro Plugin mit `IScmEventSource`: Alias→Repository-Auflösung (`GetEventRepositoryAsync` je registriertem `GitRepository` → `repositoriesForEnumeration`-Allowlist), dann modusabhängig Inbox oder Repository-Enumeration, dann Read-/Resolve-/Accept je Referenz+ContentKind |
| `EnumerateNotificationsAsync` (Zeilen 253–299) | `private` | `SeparatesKonto`-Pfad: liest Inbox pro Host via `IScmNotificationSource`, filtert `Reason == ScmNotification.MentionReason` (Zeile 277), ordnet `RepositoryNodeId` per `byRepository`-Dictionary der `RepositoryId` (`node_id`) zu; Fremd-Notifications → `discarded`-Zähler in `Inbox:`-Diagnose (Zeile 291) |
| `IsInaccessible` (Zeilen ~301–304) | `private static` | 404/403 eines Repos für das separate Konto → Repository wird ausgelassen statt Zyklus zu stoppen |

**Wesentliche Verhaltensfakten:**

- Im `SeparatesKonto` ersetzt die Inbox die repository-weite Enumeration; verknüpfte Aufgabenreferenzen werden **nicht** als Quellen übernommen (`resolved.Clear()`, Zeilen 129–133).
- Für `ScmReferenceKind.Issue` werden `ScmContentKind.Description` **und** `IssueComment` gelesen (Zeilen 162–164) — die Erwähnung kann also in Issue-Body oder -Kommentar stehen.
- `SinceUtc`-Fenster = Watermark − `PollInterval * 2`; unvollständige Snapshot-Zustände erzwingen Vollscan (Zeilen 177–182).
- Qualifizierte Beobachtung ohne lokale Aufgabe → `ScmEventTaskResolver.ResolveAsync` (Zeilen 186–193).
- Inbox-Checkpoint-Key: `notifications/{pluginPrefix}/{host}`; Watermark-Commit erst nach fehlerfreiem Zyklus (Zeilen 205–226) — bei Teilfehlern erscheint die Notification erneut (dedupliziert).
- Poll-Intervall: `ScmEventOptions.PollInterval`, Default **60 s** (`src/Softwareschmiede/Application/Services/ScmEventOptions.cs:7`).

## `ScmEventTaskResolver`

Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (177 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync` | `public` | Dispatch nach `Reference.Kind` → `ResolveIssueAsync`/`ResolvePullRequestAsync` |
| `ResolveIssueAsync` (Zeilen 52–74) | `private` | `ReadIssueAsync` → Aufgaben-Lookup per `IssueReferenz.IssueNummer` (>1 → `Ambiguous`, 1 → `Assigned`, 0 → Anlage via `AufgabeService.CreateFromIssueEventAsync` mit `IssueSourceKey`-Dedup) |
| `ResolvePullRequestAsync` (Zeilen 76–109) | `private` | PR-Pfad mit Kontextzuordnung (Branch-/Closing-Match) — für diese Anforderung nicht relevant |
| `ComposeBody` (Zeilen 160–169) | `private static` | Aufgabentext = Referenzbody (ohne Trigger-Duplikat) + Trigger-Body + Herkunfts-URL |
| `IssueSourceKey` (Zeilen 172–176) | `private static` | Dedup-Schlüssel `provider/host/repoId/Issue/N` — verhindert Doppelanlage bei wiederholter Notification |

Konsequenz für das Feature: Ein neues Issue mit qualifizierter `Description`-Erwähnung erzeugt automatisch eine **neue Aufgabe** (Titel = Issue-Titel, Text = Issue-Body + URL), sofern das Repository registriert und aktiv ist.

## `GitHubPlugin` (Ereignis-Partial)

Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs` (477 Zeilen)

| Methode / Element | Sichtbarkeit | Kurzbeschreibung |
|-------------------|-------------|------------------|
| `TextFields` (Zeile 13) | `private const` | GraphQL-Fragment `id body createdAt updatedAt lastEditedAt url author { login ... on Node { id } } editor { login ... on Node { id } } reactionGroups { content viewerHasReacted }` — liefert alle für Validierung nötigen Identitäten |
| `SinceSafetyMargin` (Zeile 20) | `internal static` | 2 h Sicherheitsabstand für `since`-Filter (DST-Bug an GitHub-Seite) |
| `GetEventRepositoryAsync` (29–47) | `public` | `GET user` (Viewer) + `GET repos/{owner}/{name}` → `ScmEventRepository` mit `OwnerId`/`Owner` aus `owner.node_id`/`owner.login` |
| `ReadEventsAsync` (50–111) | `public` | `Description` via GraphQL-Feldknoten; `IssueComment`/`ReviewComment` via REST-Liste + GraphQL-Knoten je Kommentar; `PullRequestReview` via GraphQL-Pagination (ohne `PENDING`) |
| `EnumerateNotificationsAsync` (170–199) | `public` (`IScmNotificationSource`) | `GET notifications?all=true&participating=true&per_page=100` (+`since`) unter `ClientFor(ScmKontoModus.SeparatesKonto)`; mappt Subject `Issue`/`PullRequest` → `ScmNotification` mit `reason` und `repository.node_id`; andere Subject-Typen werden verworfen |
| `AddObservation` (232–264) | `private static` | Baut `ScmEventObservation` inkl. `AuthorId`/`AuthorLogin`, `EditorId`/`EditorLogin`, `ViewerReactions`; überspringt Viewer-eigene Texte mit `<!-- softwareschmiede:`-Marker; verwirft Post-Watermark-Texte |
| `ReadIssueAsync` (135–154) | `public` | `GET issues/{n}` → `Issue` (Titel, Body, Labels, `IstGeschlossen`); liefert `null` für PRs |

`ClientFor(repository.Konto)` wählt pro Kontomodus das Credential — Inbox, Read, Antworten laufen im `SeparatesKonto` vollständig unter dem AgentToken des separaten Kontos.

## `ScmEventStore` (Diagnose-Konstanten)

Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Konstante | Wert | Zweck |
|-----------|------|-------|
| `AmbiguousAssignmentDiagnostic` (Z. 15) | „Mehrdeutige Zuordnung zu Aufgaben" | Snapshot-Diagnose bei >1 Aufgabe |
| `UnassignedDiagnostic` (Z. 17) | „Keine Aufgabe zugeordnet" | Normalzustand ohne Aufgabe |
| `InboxDiscardDiagnosticPrefix` (Z. 20) | „Inbox:" | Zähler-Diagnose verworfener Fremd-Notifications |

## `ScmEventOptions`

Datei: `src/Softwareschmiede/Application/Services/ScmEventOptions.cs` — `PollInterval` (Zeile 7) Default `TimeSpan.FromSeconds(60)`.
