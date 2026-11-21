# Logik — Bestandsaufnahme Issue #442

## `GitHubPlugin` (Ereignisse, partial)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetEventRepositoryAsync` (`:29`) | `public` (`IScmEventSource`) | Löst Alias → `ScmEventRepository` auf. **Pro Aufruf 2 REST-Calls**: `GET user` (`:35`, Viewer-Identität) und `GET repos/{owner}/{name}` (`:36`, Metadaten + `permissions`). Kein Cache, kein GraphQL — die Alias-Normalisierung läuft über `ParseEventRepository`, das Ergebnis wird aber nicht zur Gruppierung/Dedup genutzt |
| `ReadEventsAsync` (`:50`) | `public` | Liest eine Quelle (Description/Reviews via GraphQL; Kommentare REST-Liste + GraphQL-Node je Kommentar) |
| `EnumerateReferencesAsync` (`:114`) | `public` | Repository-weite Enumeration (offene immer, geschlossene im `since`-Fenster) via GraphQL |
| `ReadIssueAsync` (`:135`) / `ReadPullRequestAsync` (`:157`) | `public` | REST-Einzelabrufe der Anlagedaten |
| `EnumerateNotificationsAsync` (`:170`) | `public` (`IScmNotificationSource`) | `GET notifications` des separaten Kontos (`ClientFor(SeparateAccount)`) |
| `ClientFor` (`:334`) | `private` | Konto→Client-Routing: `SeparateAccount` → `_agentEventApiClient` (wirft `Authentication`, wenn Agent-Token fehlt), sonst `_eventApiClient` |
| `SendEventApiAsync` (`:348`) | `private` | Zentraler Sende-Engpass: `client.SendAsync` + Exception-Mapping + `EvaluateResponse`. **An dieser Stelle gäbe ein Fail-fast-Gate alle Pfade abgedeckt** |
| `EvaluateResponse` (`:362`) | `private static` | Status→`ScmProviderFailureKind`-Matrix: `rateLimited` bei 429 oder 403 mit `X-RateLimit-Remaining: 0` bzw. `Retry-After` (`:365–366`); `RetryAfterUtc` aus `Retry-After` (Sekunden/HTTP-Date) oder `X-RateLimit-Reset` (Unix-Sekunden, `:374–379`) |
| `EventApiAsync` (`:301`) | `private` | Senden + JSON-Parse; Mutationen mit nicht auswertbarer Antwort → `Temporary` + `OutcomeUncertain` |
| `GraphAsync` (`:309`) | `private` | POST `graphql`; `errors[]` → typisierte Exception (`RATE_LIMITED` → `RateLimit` — jedoch **ohne** `RetryAfterUtc`, `:315–318`) |
| `RestPagesAsync` (`:266`/`:280`) | `private` | Link-Header-Pagination mit Zyklus-/Host-Prüfung (`ValidateNextPage` `:394`) |
| `ParseEventRepository` (`:405`) | `private static` | Alias-Normalisierung zu `(Host, Owner, Name)`: SCP-SSH (`git@host:owner/repo.git`), `ssh://`, `https://`, `.git`-Suffix, `owner/name`. **Nur plugin-intern — hostseitig nicht erreichbar** |
| `ValidateReference` (`:434`) | `private` | Prüft gespeicherte Referenz (Prefix, Host, `RepositoryId`/`ViewerId` vorhanden) |
| `ValidateSegment` (`:444`) / `RepositoryEndpoint` (`:450`) / `IsViewer` (`:451`) | `private static` | Segment-Validierung, `repos/{o}/{n}`-Pfad, Viewer-Vergleich (Node-ID, sonst Login) |
| `SinceParameter` (`:25`) / `SinceSafetyMargin` (`:20`) | `private`/`internal` | `since`-Filter mit 2-h-Sicherheitsabstand (DST-Bug GitHub) |

Felder: `_eventApiClient`, `_agentEventApiClient` (`:11–12`); Instanzierung `GitHubPlugin.cs:156–157` als `GitHubHttpApiClient` mit Lazy-Credentials `Softwareschmiede.GitHub.Token` / `Softwareschmiede.GitHub.AgentToken` (`GitHubPlugin.cs:21–22`) — die Instanz-Trennung bildet die Konto-Dimension bereits ab.

## `GitHubPlugin` (Antworten, partial)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteResponseAsync` (`:43`) | `public` (`IScmEventResponder`) | **Ruft `GetEventRepositoryAsync` pro Outbox-Aktion erneut auf** (`:49–50`, d. h. 2 REST-Calls) — Identitäts-Revalidierung `RepositoryId`/`ViewerId` (`:51`) und `CanWrite` (`:53`), danach Reconcile + POST |
| `ReconcileResponseAsync` (`:12`) | `public` | Seitenweiser Marker-/Viewer-Abgleich vor jedem Schreiben |
| `ComposeBodyAsync` (`:185`) | `private` | Attachment-Uploads; nutzt `current.NumericRepositoryId`, lädt `GET repos/{o}/{n}` nach, wenn sie fehlt (`:196–202`) |
| `SendUploadApiAsync` (`:281`) | `private` | Upload-Variante ohne `OutcomeUncertain`, gleiche `EvaluateResponse`-Matrix |

## `GitHubHttpApiClient`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubHttpApiClient.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SendAsync` (`:37`) | `public` (`IGitHubEventApiClient`) | Ein Request über geteilten `HttpClient` (`Shared`, `:13`, 100 s Timeout); Token pro Request aus `_tokenProvider`; liefert Status/Body/Header unbearbeitet — **Fehlerklassifikation liegt ausschließlich im Plugin (`EvaluateResponse`), nicht hier**. Mögliche Ebene für ein providerweites Rate-Limit-Gate (Instanz = Konto) |
| `UploadAsync` (`:67`) | `public` | `uploads.github.com`-Attachment-Upload (nur github.com), `MaxUploadBytes` = 10 MiB (`:34`) |
| `BuildRequestUri` (`:109`) | `private static` | github.com → `api.github.com` (`graphql` → `/graphql`), GHES → `host/api/v3` bzw. `host/api/graphql` |
| `ValidateHost` (`:123`) | `internal static` | DNS-Host-Validierung (Token-Grenze) |

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

`PeriodicBackgroundService` im `ScmEventOptions.PollInterval`-Rhythmus (60 s).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` (`:30`) | `public override` | Zyklus: Einstellungen → verknüpfte Referenzen (`linked`) + aktive `GitRepository` → **Alias-Gruppierung über rohe `RepositoryId`-/`RepositoryUrl`-Strings** (`:79–85`) → je Alias: Checkpoint `repository/{prefix}/{escape(alias.ToLower)}` (`:90`), Backoff-Skip (`:92–97`), `GetEventRepositoryAsync(alias, accountMode)` (`:100`) — **ungecacht, je Zyklus, je Alias-Variante**. Bei `!complete` (Auflösungsfehler ohne Inbox-Isolierung) endet der Provider-Lauf (`:132`). Danach Inbox-Modus (`SeparateAccount` + `IScmNotificationSource`) oder `ScmCheckpointSupport.EnumerateRepositoryAsync` je aufgelöstem Repository (`:148–161`), Quellen-Reads mit Quell-Checkpoints (`:162–219`), Inbox-Watermark-Commits nur bei fehlerfreiem Zyklus (`:222–241`), abschließend `ScmEventOrchestrator.DispatchDueEventsAsync` (`:247`) |
| `EnumerateNotificationsAsync` (`:253`) | `private` | `notifications/{prefix}/{host}`-Checkpoint; dedupliziert Mentions, mappt `RepositoryNodeId` auf aufgelöste Repositories (`:280`), Watermark-Commit zurückgestellt |
| `IsInaccessible` (`:305`) | `private static` | **Nur `Permanent`/`Permission` gilt als „für das Konto unsichtbar"** — ein `RateLimit`-Checkpoint isoliert im Inbox-Modus nicht und setzt `complete = false` (blockiert den ganzen Provider-Lauf) |

## `ScmIssuePreparationMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmIssuePreparationMonitoringService.cs`

Gleiches Alias-Loop-Muster (`RunOnceAsync` `:36`): Gruppierung über `RepositoryUrl` (`:61–64`), Checkpoint `issueaufbereitung-repository/{prefix}/{alias}` (`:69`), `GetEventRepositoryAsync` immer unter `ScmAccountMode.UserAccount` (`:77–78`) — ungecacht je Zyklus. Fehler isolieren das Repository (`:98`, `retryPermanent: true`). Danach `issueaufbereitung-enumeration/*`-Checkpoints via `EnumerateRepositoryAsync` (`:109`), je-Issue-Auswertung `issueaufbereitung-issue/*` (`EvaluateDiscoveredIssuesAsync` `:137`, `EvaluateIssueAsync` `:175` — letztere baut `ScmEventReadRequest` direkt mit `ReadEventsAsync`, kein `GetEventRepositoryAsync`). Zyklusende: `DispatchDueEventsAsync` (`:131`).

## `ScmBackmergeMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmBackmergeMonitoringService.cs`

Identisches Muster (`RunOnceAsync` `:32`): Gruppierung über `RepositoryUrl` (`:54–57`), Checkpoint `backmerge-repository/{prefix}/{alias}` (`:62`), `GetEventRepositoryAsync` unter `ScmAccountMode.UserAccount` (`:70–71`) — ungecacht je Zyklus. Enumeration `backmerge-enumeration/*` (`:104`), Bewertung in `ScmBackmergeService.EvaluateDiscoveredReferencesAsync`.

## `ScmTaskCompletionMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` (`:32`) | `public override` | Kandidaten je `ScmTaskCompletionCheckMode` mit SCM-Referenz; `CheckTaskAsync` je Aufgabe, Fehler nur geloggt (keine Checkpoints) |
| `DetermineClosureAsync` (`:158`) | `private` | PR-Pfad: `plugin.GetPullRequestStatusAsync` (kein `GetEventRepositoryAsync`). Issue-Pfad: **`GetEventRepositoryAsync` unter `UserAccount` je Issue-verknüpfter Aufgabe je Zyklus** (`:205–207`) + `ReadIssueAsync` (`:208`) |

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchDueEventsAsync` (`:34`) | `public` | Wählt fällige Inbox-Ereignisse, ruft `DispatchAsync` je Ereignis (stündlicher Retry-Rhythmus über `NextAttemptUtc`) |
| `DispatchAsync` (`:69`) / `DispatchCoreAsync` (`:120`) | `public`/`private` | Generischer Zustellpfad; Zweig `DispatchPreparationAsync` bei Aufbereitungszweck (`:137`) |
| `DispatchCoreAsync` — Auflösung (`:193`) | — | **`GetEventRepositoryAsync` bei jeder Zustellung/jedem Retry**: baut den Alias neu als `https://{Host}/{Owner}/{Name}` aus der gespeicherten Referenz, `Account` aus `observation.Reference.Repository.Account` (`:193–195`); `RepositoryId`-Vergleich + `CanWrite`-Prüfung (`:196–197`), danach Trigger-Revalidierung (`GetRejection` `:200`) |
| `DispatchPreparationAsync` (`:607`) | `private` | Aufbereitungs-Zustellung; **gleiche Revalidierung** (`:654–658`), danach `ReadIssueAsync` (`:659`) |
| `ReconcileAttemptAsync` (`:485`) / `ReconcilePreparationAttemptAsync` (`:824`) | `private` | Attempt-Abgleich über `IScmEventResponder` (keine eigene Auflösung) |

## `ScmCheckpointSupport`
Datei: `src/Softwareschmiede/Application/Services/ScmCheckpointSupport.cs` (`internal static`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RecordFailureAsync` (`:23`) | `internal` | Checkpoint anlegen/fortschreiben + `ScmEventChangeNotifier.Notify()` |
| `ApplyFailure` (`:51`) | `internal` | `Failures++`, `ErrorCode` = `FailureKind`-Name, `NextAttemptUtc` = `long.MaxValue` bei `Authentication`/`Permission` (und `Permanent` ohne `retryPermanent`), sonst `ScmEventStore.RetryAt(Failures, now, error.RetryAfterUtc)` — **`RetryAfterUtc` fließt bereits ins Backoff** |
| `EnumerateRepositoryAsync` (`:78`) | `internal` | Enumerations-Checkpoint `{prefix}/{provider}/{host}/{repoId}` (kanonischer Schlüssel), `enumerated`-Dedup je Lauf, Watermark − 2×PollInterval, isoliertes Failure-Recording |

## `ScmEventOptions` / `ScmEventChangeNotifier` / `ScmEventStartupGate`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOptions.cs`

| Member | Beschreibung |
|--------|--------------|
| `PollInterval` (`:7`) | `TimeSpan`, Default 60 s; gebunden an Section `ScmEvents` (`App.xaml.cs:222`) — **kein Eintrag in den gefundenen `appsettings*.json`** (Default wirkt). Keine Cache-TTL-Option vorhanden |
| `AcceptanceTimeout` (`:9`) | 2 min — korrelierte Annahme |
| `CompletionTimeout` (`:11`) | 30 min — korreklierter Abschluss |
| `ScmEventChangeNotifier` (`:15`) | `Changed`-Event + `Notify()` nach dauerhafter Änderung |
| `ScmEventStartupGate` (`:24`) | Hält Dienste bis nach der DB-Migration |

## `PeriodicBackgroundService`
Datei: `src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs`

Basisklasse: Schleife `RunOnceAsync` → Fehler je Durchlauf abgefangen → Intervall-Wartezeit (`:34`); die vier Monitoring-Dienste laufen alle im selben `PollInterval`-Rhythmus — **jeder aktivierte Dienst löst seine Alias-Gruppen pro Zyklus neu auf** (3 Dienste mit je eigenem Checkpoint-Präfix + Completion-Service ohne Checkpoint).
