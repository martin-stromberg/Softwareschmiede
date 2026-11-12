# Bestandsaufnahme: Logik (SCM-Ereignisverarbeitung)

## `GitHubPlugin` (partial, Ereignispfade)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetEventRepositoryAsync` | `public` | Lädt Viewer-Identität (`GET user`) + Repository-Metadaten; liefert `ScmEventRepository` mit `ViewerId`/`ViewerLogin`/`Konto` (Z. 29–47) |
| `ReadEventsAsync` | `public` | Liest eine Quelle je nach `ContentKind`: `Description` (GraphQL `TextFields`), `PullRequestReview` (paginierte `reviews`-Connection + `TextFields`, Pending ausgeschlossen), `IssueComment`/`ReviewComment` (REST-Liste + je Kommentar GraphQL `node(id:)` mit `TextFields`, Z. 50–111) |
| `EnumerateReferencesAsync` | `public` | Repository-weite Referenzliste (offen + zuletzt aktive geschlossene), Benutzerkonto-Modus (Z. 114–132) |
| `ReadIssueAsync` / `ReadPullRequestAsync` | `public` | Anlagedaten für `ScmEventTaskResolver` (Z. 135–167) |
| `EnumerateNotificationsAsync` | `public` | Notifications-Inbox des separaten Kontos; läuft immer über `_agentEventApiClient` (Z. 170–199) |
| `AddObservation` | `private static` | Einzige Produktivstelle, die `ScmEventObservation` konstruiert: Watermark-Filter (Z. 239), Eigenantwort-Filter `IsViewer` + `<!-- softwareschmiede:`-Marker (Z. 246), URL-Fragmente je `ContentKind`, `ObservedAt = UtcNow` (Z. 232–258) |
| `ClientFor` | `private` | Kontoabhängige Transportwahl: `SeparatesKonto` → `_agentEventApiClient` (mit Token-Pflicht), sonst `_eventApiClient` — über `repository.Konto` automatisch passend zur Viewer-Identität (Z. 328–335) |
| `IsViewer` | `private static` | Identitätsabgleich Author/Editor/Reaction-User gegen `ViewerId`/`ViewerLogin` (Z. 437–439) |
| `TextFields` | `private const` | Gemeinsames GraphQL-Feldfragment `id body createdAt updatedAt lastEditedAt url author{…} editor{…}` (Z. 13) — einzige Stelle, an der Reaktionsfelder für alle drei Kommentar-/Review-Abfragen mitgezogen würden |
| `RestPagesAsync` / `GraphAsync` / `EventApiAsync` / `SendEventApiAsync` | `private` | Pagination, GraphQL-Fehlertypisierung, HTTP-Fehler → `ScmProviderException` mit Backoff-Metadaten (Z. 260–368) |

## `GitHubPlugin` (partial, Antwortpfad)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ReconcileResponseAsync` | `public` | Findet eine bereits gesetzte Aktion wieder (Marker im Body bzw. gleiche Reaktion des Viewers); Reactions über REST-`{endpoint}/reactions?per_page=100` für Issue-/Review-Kommentare, GraphQL für `PullRequestReview` (Z. 10–36) |
| `ExecuteResponseAsync` | `public` | Revalidiert Repository/Viewer (`GetEventRepositoryAsync`), gleicht vor, postet erst bei Fehlschlag; Reaktion auf `PullRequestReview` via GraphQL `addReaction` (Z. 39–77) |
| `ReconcileReviewReactionAsync` | `private` | Paginierter GraphQL-Abgleich von `reactions{nodes{id content user{id login}}}` auf `PullRequestReview` (Z. 79–107) — demonstriert Reaktionslektüre per GraphQL für den nicht-REST-abdeckbaren Fall |
| `ReactionEndpoint` | `private static` | REST-Pfad `repos/{o}/{r}/issues|pulls/comments/{id}/reactions` (Description: `issues/{n}/reactions`) (Z. 142–153) |
| `GraphReaction` | `private static` | REST-Content → GraphQL-`ReactionContent` (`eyes`→`EYES`, `+1`→`THUMBS_UP` u. a.) (Z. 155–166) |

## `ScmEventValidationService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRejection` | `public static` | Liefert Ablehnungsgrund oder `null`. Reihenfolge: (1) Trigger — `Benutzerkonto`: frei stehendes Keyword (`NormalizeTriggerKeyword`), `SeparatesKonto`: frei stehende `@viewerLogin`-Mention; (2) `Benutzerkonto` verlangt Viewer = Owner; (3) Bearbeiter (Autor bzw. bei Edit Editor) bestimmbar; (4) Bearbeiter = Owner; (5) `<!-- softwareschmiede:`-Marker → `"Eigene Antwort"` (Z. 29–51) |
| `NormalizeTriggerKeyword` | `public static` | Fallback auf `DefaultTriggerKeyword` + Trim (Z. 22–23) |
| `HasFreeStandingTrigger` | `private static` | Treffer nur frei stehend (nicht von Zitatzeichen flankiert) (Z. 54–66) |
| `SameIdentity` | `private static` | ID-Abgleich vor Login-Abgleich (Z. 68–72) |

Konstanten: `DefaultTriggerKeyword = "$Softwareschmiede"`, `KeineErwaehnungRejection = "Keine Erwähnung des separaten Kontos"` (Z. 11–15). Übrige Gründe sind String-Literale. **Kein reaktionsbezogener Ablehnungsgrund vorhanden.**

Aufgerufen von: `ScmEventStore.AcceptBatchAsync` (Eingangsvalidierung), `ScmEventMonitoringService.RunOnceAsync` (Trigger-Suche für `ScmEventTaskResolver`, Z. 187), `ScmEventOrchestrator.DispatchCoreAsync` (Gegenprüfung mit aktualisiertem Repository, Z. 137–141).

## `ScmEventStore`
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AcceptBatchAsync` | `public` | Transaktionale Inbox-Aufnahme: je Beobachtung Snapshot finden/erzeugen → `GetRejection` (bzw. Zuordnungsdiagnose ohne Aufgabe) → bei Ablehnung `snapshot.Diagnostic = rejection`, kein Inbox-Eintrag; sonst `ScmIncomingEvent` mit `ObservationJson`/`EventKey`; Checkpoint-Watermark/Failures-Diagnose wird immer fortgeschrieben (Z. 41–98) |
| `Observation` | `public static` | `ObservationJson` → `ScmEventObservation` via `JsonOptions` (Z. 24–25) |
| `ActionId` / `RetryAt` | `public static` | Deterministische Aktions-IDs bzw. Backoff-Berechnung (Z. 27–31) |
| `ClaimTaskAsync` / `RenewTaskAsync` / `ReleaseTaskAsync` | `public` | Aufgabenleases über `ScmTaskLeases` (Z. 101–114) |

Konstanten: `AmbiguousAssignmentDiagnostic`, `UnassignedDiagnostic`, `InboxDiscardDiagnosticPrefix = "Inbox:"` (Z. 15–20). Diagnose-Literal „Bearbeiter nicht bestimmbar" steuert `IdentityResolved` (Z. 63).

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` | `public override` | Poll-Zyklus: Repository-Auflösung pro Alias-Gruppe mit Backoff-Checkpoint → Quellmenge (`SeparatesKonto`: Notifications-Inbox via `EnumerateNotificationsAsync`; `Benutzerkonto`: `EnumerateRepositoryAsync` je Repository) → je Referenz × `ContentKind` (Issue: `Description`,`IssueComment`; PR: + `ReviewComment`,`PullRequestReview`) `ReadEventsAsync` → bei unzugeordneter Quelle `ScmEventTaskResolver` über erste triggernde Beobachtung → `AcceptBatchAsync` → danach Dispatch-Schleife über fällige Inbox-Einträge (Z. 30–248) |
| `EnumerateNotificationsAsync` | `private` | Inbox-Lesen, Mention-Filter, Repository-Zuordnung per `RepositoryNodeId`+Host, verzögertes Watermark-Commit erst bei fehlerfreiem Zyklus (Z. 253–299) |
| `IsInaccessible` | `private static` | `Permanent`/`Permission` = für das Konto nicht sichtbares Repository (Z. 305–306) |

Fehlerpfad: `ScmCheckpointSupport.RecordFailureAsync` setzt `Failures`/`NextAttemptUtc`/`ErrorCode` auf dem Quell-Checkpoint; ein `ReadEventsAsync`-Fehler führt **nicht** zu stillschweigendem Verwerfen — die Anforderungsforderung „kein Ignorieren bei Lesefehler" ist damit bereits strukturell erfüllt.

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchAsync` / `OpenFromQueueAsync` | `public` | Einstieg Poll-/Queue-Pfad, jeweils unter Aufgabenlease (`ScmTaskCoordinator`) |
| `DispatchCoreAsync` | `private` | Zustellung: Abbruch-Prüfung → Plugin/Responder-Auflösung → `receipt`-Reaktion (`ScmResponseKind.Reaction`, `"eyes"`, nur Nicht-`Description`, Z. 95–101) → ausstehende `waiting`-Aktionen → Versuchs-Rekonstruktion (`ReconcileAttemptAsync`) → Sequenz-/Blockierungsprüfung → `GetEventRepositoryAsync`-Revalidierung + `GetRejection`-Gegenprüfung (Z. 130–141; nur `"Kein Trigger"` und `KeineErwaehnungRejection` toleriert, sonst `WaitAsync`) → CLI-Start/Adapter → Kanalprobe → Exchange-Prepare → `WritePromptAsync` → Status `Zugestellt`/`ZustellungUngewiss` |
| `ReconcileAttemptAsync` | `private` | Liest Exchange-Nachrichten (accepted/actions/complete/diagnostic), führt `ai:`-Aktionen über `ScmEventOutbox` aus, verknüpft gemeldete PRs (Z. 361–442) |
| `WaitAsync` / `WaitWithCommentAsync` | `private` | Status `Wartend` + Reason; Letzteres postet zusätzlich einen `waiting`-Kommentar remote (Z. 473–487) |
| `RetryAsync` / `ConfirmReadyAsync` / `ResendAsync` / `CancelAsync` | `public` | Benutzeraktionen; `CancelAsync` ist endgültig (Z. 263–330) |

## `ScmEventOutbox`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOutbox.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureAsync` | `public` | Persistiert Aktion vor dem ersten Remoteaufruf (idempotent per `ActionId`-Guid) (Z. 14–30) |
| `ExecuteAsync` | `public` | `ReconcileResponseAsync` vor `ExecuteResponseAsync`; Ungewissheit → `Uncertain` ohne Neuversand, typisierte Fehler → Backoff (Z. 33–74) |

## `ScmEventTaskResolver`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync` | `public` | Löst Referenz zu genau einer Aufgabe auf (Issue- bzw. PR-Pfad); Ergebnis `Assigned`/`Ambiguous`/`Unassigned` (Z. 46–50) |
| `ResolveIssueAsync` / `ResolvePullRequestAsync` | `private` | Eindeutigkeitsregeln, Aufgabenanlage aus Anlagedaten (`CreateFromIssueEventAsync`/`CreateFromPullRequestAsync`), PR-Kontextabgleich (Branch, Closing-Issues) (Z. 52–109) |

## `ScmEventExchangeProtocol` / `ScmEventExchangeService`
Dateien: `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs`, `ScmEventExchangeService.cs`

Dateibasierter Prompt-/Ergebniskanal (request.json/accepted/actions/complete); `PrepareAsync` wird in `DispatchCoreAsync` Z. 198 aufgerufen. Für die Eingangsdeduplikation nicht unmittelbar relevant.
