# Interfaces: GitHub-Notifications als Eingangskanal

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

Optionale Plugin-Fähigkeit zum Lesen vollständiger beobachteter Textstände. Einzige Implementierung: `GitHubPlugin`. Wird von `ScmEventMonitoringService`, `ScmEventTaskResolver` und `ScmEventOrchestrator` verwendet.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext repository`, `CancellationToken` | `Task<ScmEventRepository>` | Authentifizierte Identität + kanonischer Repository-Kontext (liefert `ViewerId`/`ViewerLogin`/`CanWrite` — im Bot-Modus wäre der Viewer das Bot-Konto) |
| `ReadEventsAsync` | `ScmEventReadRequest request`, `CancellationToken` | `Task<ScmEventBatch>` | Quelle vollständig lesen; kein Teilresultat bei Seitenfehlern |
| `EnumerateReferencesAsync` | `ScmEventRepository repository`, `DateTimeOffset? sinceUtc`, `CancellationToken` | `Task<IReadOnlyList<ScmEventReference>>` | Repository-weite Referenzliste (offen + seit `sinceUtc` aktive Geschlossene) — im neuen Modus ersetzt/ergänzt durch Inbox-Polling |
| `ReadIssueAsync` | `ScmEventReference reference`, `CancellationToken` | `Task<Issue?>` | Anlagedaten für `ScmEventTaskResolver` |
| `ReadPullRequestAsync` | `ScmEventReference reference`, `CancellationToken` | `Task<PullRequest?>` | Anlagedaten für `ScmEventTaskResolver` |

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReconcileResponseAsync` | `ScmResponseRequest request`, `CancellationToken` | `Task<ScmResponseResult?>` | Alle Seiten prüfen; `null` = sicher nicht gefunden |
| `ExecuteResponseAsync` | `ScmResponseRequest request`, `CancellationToken` | `Task<ScmResponseResult>` | Abgleich vor Schreiben; `OutcomeUncertain` bei unbekanntem Ergebnis |

## `IScmAgentAccess`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAgentEnvironmentAsync` | `CancellationToken` | `Task<IReadOnlyDictionary<string, string?>>` | Umgebungsvariablen für die CLI-Agenten-Sitzung (GitHub: `GH_TOKEN`, github.com-`http.extraheader`, `GIT_TERMINAL_PROMPT=0`); kann Geheimnisse enthalten — nur Prozessumgebung, nie Prompt/Logs/Persistenz; leere Liste blockiert Start nicht. Aufgerufen von `KiAusfuehrungsService.WithScmAgentEnvironmentAsync` |

## `ICredentialStore`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetCredential` | `string target` | `string?` | Gespeichertes Credential oder `null` |
| `SetCredential` | `string target`, `string value` | `void` | Credential speichern |
| `DeleteCredential` | `string target` | `void` | Credential löschen |

Bestehender Key: `Softwareschmiede.GitHub.Token` (`GitHubPlugin`, Z. 17). Ein zweiter Key (z. B. Bot-PAT) wäre über ein zusätzliches `PluginSettingField` anlegbar — `PluginSettingsService` bildet `<PluginPrefix>.<FieldKey>` auf den Store ab.

## `IGitHubEventApiClient`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs`

Credentialfreier, austauschbarer GitHub-Ereignistransport (Tests injizieren Mocks über den zweiten `GitHubPlugin`-Konstruktor).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `SendAsync` | `GitHubEventApiRequest request`, `CancellationToken` | `Task<GitHubEventApiResponse>` | Exakt eine API-Seite/Mutation; Bodies bleiben JSON-Daten |

Begleittypen: `record GitHubEventApiRequest(Host, Method, Endpoint, JsonBody?)` (Z. 18), `record GitHubEventApiResponse(StatusCode, Body, Headers?)` (Z. 21), Implementierung `GhEventApiClient` (Z. 24–87, `gh api` über `ICliRunner` + `environmentFactory` für `GH_TOKEN`).

## `IGitPlugin` / `IPlugin` (Auszug)
Dateien: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`, `IPlugin.cs`; Basisklasse `GitPluginBase<TPlugin>` (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`)

Für die Anforderung relevante Mitglieder: `PluginPrefix`, `PluginName`, `PluginType`, `GetSettingGroups()`, `GetRepositoryLinkFields()`, `CheckHealthAsync`, `CloneRepositoryAsync`, `GetEventRepository`-nahe Optionalfähigkeiten sind bewusst als separate Interfaces (`IScmEventSource`/`IScmEventResponder`/`IScmAgentAccess`) gehalten — `ScmEventMonitoringService` prüft mit `plugin is IScmEventSource`.

## Nicht vorhanden
Es existiert **kein** Interface für einen Notification-Eingang (z. B. `IScmNotificationSource`) und kein Value Object für Notification-Einträge (Thread-ID, Subject, Reason, `updated_at`).
