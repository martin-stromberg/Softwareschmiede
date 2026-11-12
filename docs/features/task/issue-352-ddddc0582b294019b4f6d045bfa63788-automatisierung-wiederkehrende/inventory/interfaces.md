# Interfaces — SCM-Ereignis-Pipeline

## `IScmNotificationSource`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs` (Zeilen 7–13)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `host`, `sinceUtc`, `ct` | `Task<IReadOnlyList<ScmNotification>>` | Liest die Notifications-Inbox des separaten Kontos eines Hosts; `sinceUtc` begrenzt auf Aktivität ab Zeitpunkt |

Implementiert nur von `GitHubPlugin` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs:170-199`) — im `SeparatesKonto` die einzige Referenzquelle. Aufgerufen von `ScmEventMonitoringService.EnumerateNotificationsAsync`.

## `IScmEventSource`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs` (Zeilen 6–26)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `repository` (`ScmRepositoryContext`), `ct` | `Task<ScmEventRepository>` | Lädt Viewer-Identität und kanonischen Repo-Kontext (inkl. `OwnerId`/`Owner`) |
| `ReadEventsAsync` | `request` (`ScmEventReadRequest`), `ct` | `Task<ScmEventBatch>` | Liest eine Quelle (z. B. Issue-`Description`) vollständig als `ScmEventObservation`s |
| `EnumerateReferencesAsync` | `repository`, `sinceUtc`, `ct` | `Task<IReadOnlyList<ScmEventReference>>` | Repository-weite Enumeration — **im `SeparatesKonto` ungenutzt** |
| `ReadIssueAsync` | `reference`, `ct` | `Task<Issue?>` | Anlagedaten für die Task-Erzeugung (`ScmEventTaskResolver`) |
| `ReadPullRequestAsync` | `reference`, `ct` | `Task<PullRequest?>` | PR-Anlagedaten |

Implementiert von `GitHubPlugin`; Aufgerufen von `ScmEventMonitoringService` und `ScmEventTaskResolver`.
