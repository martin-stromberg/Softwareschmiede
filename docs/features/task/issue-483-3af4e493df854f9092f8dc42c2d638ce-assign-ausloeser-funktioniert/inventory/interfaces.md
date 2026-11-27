# Interfaces: SCM-Ereignis-Verträge

Optionale Plugin-Fähigkeiten (`Softwareschmiede.Plugin.Contracts`), die der Host (`ScmEventMonitoringService`/`ScmEventOrchestrator`) per Pattern-Matching (`is`-Prüfung) nutzt. Implementiert durch `GitHubPlugin` (Implementierung in `plugins/Softwareschmiede.Plugin.GitHub/`).

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext repository`, `CancellationToken` | `Task<ScmEventRepository>` | Authentifizierte Identität + kanonischer Repository-Kontext (u. a. `ViewerId`/`ViewerLogin` des Ereignis-Kontos). |
| `ReadEventsAsync` | `ScmEventReadRequest request`, `CancellationToken` | `Task<ScmEventBatch>` | Vollständige Textquelle je `ContentKind`; der Request trägt **keinen** Auslösekontext (kein Assign-Hinweis). |
| `EnumerateReferencesAsync` | `ScmEventRepository`, `DateTimeOffset? sinceUtc`, `CancellationToken` | `Task<IReadOnlyList<ScmEventReference>>` | Repository-weite Referenzliste (nur `UserAccount`-Kanal). |
| `ReadIssueAsync` | `ScmEventReference`, `CancellationToken` | `Task<Issue?>` | Anlagedaten einer Issue-Referenz (`null` bei PR/entfernt). |
| `ReadPullRequestAsync` | `ScmEventReference`, `CancellationToken` | `Task<PullRequest?>` | Anlagedaten einer PR-Referenz. |

## `IScmNotificationSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `string host`, `DateTimeOffset? sinceUtc`, `CancellationToken` | `Task<IReadOnlyList<ScmNotification>>` | Rohe Inbox-Einträge des separaten Kontos je Host (`reason` u. a. `mention`/`assign`); Referenz-Ableitung und Repository-Zuordnung bleiben im Host. |

## `IScmAssignmentSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAssignmentSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetLatestViewerAssignmentAsync` | `ScmEventReference`, `CancellationToken` | `Task<ScmAssignment?>` | Jüngste wirksame Zuweisung auf das Viewer-Konto samt Zuweisendem; `null` bei keiner wirksamen Zuweisung. Plugins ohne die Fähigkeit melden `AssignerUnknownRejection`. |

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReconcileResponseAsync` | `ScmResponseRequest`, `CancellationToken` | `Task<ScmResponseResult?>` | Marker-/Reaktions-basierter Idempotenzabgleich aller relevanten Remote-Seiten. |
| `ExecuteResponseAsync` | `ScmResponseRequest`, `CancellationToken` | `Task<ScmResponseResult>` | Remote-Aktion schreiben; bei `IssueBodyUpdate` komplette Beschreibung samt `Marker` per PATCH ersetzen. |

## `IScmAgentAccess`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAgentEnvironmentAsync` | `ScmAccountMode`, `string?`, `CancellationToken` | `Task<Dictionary<string,string?>>` | Agenten-Umgebung (u. a. `GH_TOKEN` des separaten Kontos) für CLI-Starts; nur Kontext, nicht direkt im Assign-Pfad. |

## `IScmPromptSessionAdapter` (Host-seitig, Zustellung)
Wird in `DispatchCoreAsync`/`DispatchPreparationAsync` für Bereitschaft (`WaitForInputReadyAsync`, `ReadySinceUtc`, `IsBusy`, `CliRecognition`), Kanalprobe (`CheckExchangeAccessAsync`) und `WritePromptAsync` genutzt; keine Änderung im Scope dieser Anforderung.
