# Interfaces — Bestandsaufnahme Issue #442

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs:6`

Einzige Repository-Auflösungsschnittstelle; einzige Implementierung ist `GitHubPlugin` (das BitBucket-Plugin implementiert sie nicht — Scope-Ausschluss der Anforderung bestätigt).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext repository`, `CancellationToken ct` | `Task<ScmEventRepository>` | „Lädt die authentifizierte Identität und den kanonischen Repository-Kontext" — einziger Auflösungspfad, alle sechs Call-Sites laufen hierüber |
| `ReadEventsAsync` | `ScmEventReadRequest request`, `CancellationToken ct` | `Task<ScmEventBatch>` | Quelle vollständig lesen; kein Teilresultat bei Seitenfehlern |
| `EnumerateReferencesAsync` | `ScmEventRepository repository`, `DateTimeOffset? sinceUtc`, `CancellationToken ct` | `Task<IReadOnlyList<ScmEventReference>>` | Repository-weite Referenzliste (offene immer, geschlossene im Fenster) |
| `ReadIssueAsync` | `ScmEventReference reference`, `CancellationToken ct` | `Task<Issue?>` | Anlagedaten einer Issue-Referenz; `null` bei PR/Nichtexistenz |
| `ReadPullRequestAsync` | `ScmEventReference reference`, `CancellationToken ct` | `Task<PullRequest?>` | Anlagedaten einer PR-Referenz |

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs:10`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReconcileResponseAsync` | `ScmResponseRequest request`, `CancellationToken ct` | `Task<ScmResponseResult?>` | Abgleich aller Seiten; `null` = sicher nicht gefunden |
| `ExecuteResponseAsync` | `ScmResponseRequest request`, `CancellationToken ct` | `Task<ScmResponseResult>` | Schreiben nach Reconcile; `OutcomeUncertain` bei unbekanntem Ergebnis — enthält die Outbox-seitige `GetEventRepositoryAsync`-Revalidierung |

## `IScmNotificationSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs:7`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `string host`, `DateTimeOffset? sinceUtc`, `CancellationToken ct` | `Task<IReadOnlyList<ScmNotification>>` | Rohe Notifications-Inbox des separaten Kontos pro Host |

## `IGitHubEventApiClient`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs:4`

Credentialfreier, austauschbarer GitHub-Ereignistransport; Produktivimplementierung `GitHubHttpApiClient`, Testimplementierung `FakeGitHubEventApi`. Zwei Instanzen pro Plugin (`_eventApiClient`/`_agentEventApiClient`) = Konto-Dimension.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `SendAsync` | `GitHubEventApiRequest request`, `CancellationToken ct` | `Task<GitHubEventApiResponse>` | Genau eine API-Seite oder Mutation — **zentraler Transport-Engpass**, mögliche Ebene des Rate-Limit-Fail-fast-Gates |
| `UploadAsync` | `GitHubAssetUploadRequest request`, `CancellationToken ct` | `Task<GitHubEventApiResponse>` | Attachment-Upload; nur typisierte Providerfehler ohne `OutcomeUncertain` |
