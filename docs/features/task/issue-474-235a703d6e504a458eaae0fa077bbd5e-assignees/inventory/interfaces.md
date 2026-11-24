# Interfaces — Bestandsaufnahme Assignee-Auslöser (Issue #474)

## `IScmNotificationSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`

Optionale Plugin-Fähigkeit: liest die Notifications-Inbox des separaten Kontos. Aktiviert den Inbox-Modus in `ScmEventMonitoringService` (`inboxMode`, Z. 89).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `string host`, `DateTimeOffset? sinceUtc`, `CancellationToken ct` | `Task<IReadOnlyList<ScmNotification>>` | Listet rohe Inbox-Einträge des Hosts; `sinceUtc` begrenzt auf Aktivität ab diesem Zeitpunkt. Referenz-Ableitung und Repository-Zuordnung bleiben im Host. |

**Keine Methode zur Zuweisender-Ermittlung vorhanden** — die Anforderung benötigt den Akteur der jüngsten Zuweisung auf den Viewer; den liefert weder dieser Vertrag noch `IScmEventSource`.

Implementiert von: `GitHubPlugin` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`, Z. 271-300).

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

Optionale Plugin-Fähigkeit: liest vollständige beobachtete Textstände einer verknüpften Referenz.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext repository`, `CancellationToken ct` | `Task<ScmEventRepository>` | Authentifizierte Identität + kanonischer Repository-Kontext (inkl. `ViewerLogin`, `OwnerId`, `Account`) |
| `ReadEventsAsync` | `ScmEventReadRequest request`, `CancellationToken ct` | `Task<ScmEventBatch>` | Liest eine Quelle (je `ContentKind`) vollständig; kein Teilresultat bei Seitenfehlern |
| `EnumerateReferencesAsync` | `ScmEventRepository repository`, `DateTimeOffset? sinceUtc`, `CancellationToken ct` | `Task<IReadOnlyList<ScmEventReference>>` | Repository-weite Referenzliste (offene + seit `sinceUtc` aktive geschlossene); im `SeparateAccount` ungenutzt |
| `ReadIssueAsync` | `ScmEventReference reference`, `CancellationToken ct` | `Task<Issue?>` | Anlagedaten einer Issue-Referenz (Titel, Body, Metadaten, URL); `null` bei Nicht-Issue/fehlend |
| `ReadPullRequestAsync` | `ScmEventReference reference`, `CancellationToken ct` | `Task<PullRequest?>` | Anlagedaten einer PR-Referenz inkl. Branches; `null` bei Nicht-PR/fehlend |

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`

Optionale Plugin-Fähigkeit für abgeglichene Remote-Antworten (unverändert relevant: `Description`-Beobachtungen erhalten **keine** `eyes`-Empfangsreaktion — siehe `ScmEventOrchestrator.DispatchCoreAsync`, Z. 155).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReconcileResponseAsync` | `ScmResponseRequest request`, `CancellationToken ct` | `Task<ScmResponseResult?>` | Prüft alle relevanten Seiten; `null` = sicher nicht gefunden |
| `ExecuteResponseAsync` | `ScmResponseRequest request`, `CancellationToken ct` | `Task<ScmResponseResult>` | Führt die Antwortaktion aus; unbekannter Ausgang → `OutcomeUncertain` |

## `IGitHubEventApiClient`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs`

Credentialfreier GitHub-Ereignistransport (Plugin-intern; `ClientFor` wählt Benutzer- vs. Agent-Client). Zugehörige Records: `GitHubEventApiRequest(Host, Method, Endpoint, JsonBody?)`, `GitHubAssetUploadRequest`, `GitHubEventApiResponse(StatusCode, Body, Headers?)`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `SendAsync` | `GitHubEventApiRequest request`, `CancellationToken ct` | `Task<GitHubEventApiResponse>` | Exakt eine API-Seite oder Mutation |
| `UploadAsync` | `GitHubAssetUploadRequest request`, `CancellationToken ct` | `Task<GitHubEventApiResponse>` | Attachment-Upload in den Nachweis-Speicher |

In Tests gemockt über `FakeGitHubEventApi` (`src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs`) bzw. im E2E-Pfad ersetzt durch `ScmE2EApiClient` (`src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs`).
