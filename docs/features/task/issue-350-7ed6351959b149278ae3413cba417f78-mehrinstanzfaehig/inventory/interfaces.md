# Bestandsaufnahme: Interfaces (SCM-Ereignisverarbeitung)

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext repository`, `CancellationToken` | `Task<ScmEventRepository>` | Authentifizierte Identität + kanonischer Repository-Kontext (Viewer-Identität = Erkennungskonto) |
| `ReadEventsAsync` | `ScmEventReadRequest request`, `CancellationToken` | `Task<ScmEventBatch>` | Vollständiges Lesen einer Quelle; kein Teilresultat bei Seitenfehlern — Reaktionsdaten reiten hier auf `ScmEventObservation`, keine Signaturänderung nötig |
| `EnumerateReferencesAsync` | `ScmEventRepository repository`, `DateTimeOffset? sinceUtc`, `CancellationToken` | `Task<IReadOnlyList<ScmEventReference>>` | Zu überwachende Referenzen (Benutzerkonto-Modus) |
| `ReadIssueAsync` | `ScmEventReference reference`, `CancellationToken` | `Task<Issue?>` | Anlagedaten für `ScmEventTaskResolver` |
| `ReadPullRequestAsync` | `ScmEventReference reference`, `CancellationToken` | `Task<PullRequest?>` | Anlagedaten inkl. Quell-/Ziel-Branch |

Implementiert von: `GitHubPlugin` (`GitHubPlugin.Events.cs`). In Tests gemockt über `IGitPlugin.As<IScmEventSource>()` (`ScmDispatchFixture.cs` Z. 63–71, `Moq`).

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReconcileResponseAsync` | `ScmResponseRequest request`, `CancellationToken` | `Task<ScmResponseResult?>` | Wiederfinden einer bereits gesetzten Aktion; `null` = sicher nicht gefunden |
| `ExecuteResponseAsync` | `ScmResponseRequest request`, `CancellationToken` | `Task<ScmResponseResult>` | Abgleich vor dem Schreiben; `OutcomeUncertain` bei unbekanntem Ergebnis |

Implementiert von: `GitHubPlugin` (`GitHubPlugin.EventResponses.cs`). Der Schreibpfad der Empfangsreaktion (`receipt`/`eyes`) — der remote sichtbare Marker für die Deduplikation.

## `IScmNotificationSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `string host`, `DateTimeOffset? sinceUtc`, `CancellationToken` | `Task<IReadOnlyList<ScmNotification>>` | Rohliste der Notifications-Inbox (SeparatesKonto-Modus) |

Implementiert von: `GitHubPlugin` (`GitHubPlugin.Events.cs` Z. 170–199).

## `IGitHubEventApiClient`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `SendAsync` | `GitHubEventApiRequest request`, `CancellationToken` | `Task<GitHubEventApiResponse>` | Exakt eine API-Seite/Mutation; austauschbarer, credentialfreier Transport |

Implementierungen: `GitHubHttpApiClient` (Produktion), `ScmE2EApiClient` (E2E-Fixture, zwei Instanzen für Viewer-/Bot-Identität, `ScmE2EPluginManager.cs` Z. 24–25), `FakeGitHubEventApi` (Unit-Tests). `GitHubEventApiRequest(Host, Method, Endpoint, JsonBody?)`, `GitHubEventApiResponse(StatusCode, Body, Headers?)`.
