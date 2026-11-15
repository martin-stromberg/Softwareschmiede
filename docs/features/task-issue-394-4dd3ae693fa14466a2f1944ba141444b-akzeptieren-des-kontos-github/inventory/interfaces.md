# Interfaces — Bestandsaufnahme

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

Optionale Provider-Fähigkeit, vollständige beobachtete Textstände einer verknüpften Referenz zu lesen. **Einzige Implementierung: `GitHubPlugin`** (`BitbucketPlugin`, `LocalDirectoryPlugin` implementieren sie nicht).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext repository`, `CancellationToken ct` | `Task<ScmEventRepository>` | Authentifizierte Identität und kanonischer Repository-Kontext |
| `ReadEventsAsync` | `ScmEventReadRequest request`, `CancellationToken ct` | `Task<ScmEventBatch>` | Vollständiges Lesen einer Quelle; kein Teilresultat bei Fehlern |
| `EnumerateReferencesAsync` | `ScmEventRepository repository`, `DateTimeOffset? sinceUtc`, `CancellationToken ct` | `Task<IReadOnlyList<ScmEventReference>>` | Zu überwachende Referenzen (offene; plus geschlossene mit Aktivität seit `sinceUtc`) |
| `ReadIssueAsync` | `ScmEventReference reference`, `CancellationToken ct` | `Task<Issue?>` | Anlagedaten einer Issue-Referenz für `ScmEventTaskResolver` |
| `ReadPullRequestAsync` | `ScmEventReference reference`, `CancellationToken ct` | `Task<PullRequest?>` | Anlagedaten einer PR-Referenz für `ScmEventTaskResolver` |

Aufrufer: `ScmEventMonitoringService` (Poll-Zyklus), `ScmEventOrchestrator` (Revalidierung via `GetEventRepositoryAsync`), `ScmCheckpointSupport`, `ScmBackmergeMonitoringService`, `ScmTaskCompletionMonitoringService`.

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs` (Implementierung: `GitHubPlugin`, `GitHubPlugin.EventResponses.cs`)

Antwortkanal für Reaktionen/Kommentare (`ScmResponseKind`); für die Akteursprüfung nicht direkt relevant, gehört aber zum Dispatch-Pfad (`ScmEventOrchestrator` Z. 89).

## `IScmNotificationSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs` (Implementierung: `GitHubPlugin`, `GitHubPlugin.Events.cs` Z. 170–199)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `string host`, `DateTimeOffset? sinceUtc`, `CancellationToken ct` | `Task<IReadOnlyList<ScmNotification>>` | Notifications-Inbox des separaten Kontos im Modus `SeparatesKonto`; nur `reason = "mention"` wird ausgewertet |

**Feststellung:** Für die Anforderung ist keine Interface-Erweiterung nötig — `AuthorId`/`AuthorLogin`/`EditorId`/`EditorLogin` werden bereits durch den bestehenden `IScmEventSource`-Vertrag geliefert.
