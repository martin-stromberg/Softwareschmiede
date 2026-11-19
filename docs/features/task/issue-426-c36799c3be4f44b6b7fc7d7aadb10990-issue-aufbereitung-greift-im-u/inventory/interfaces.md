# Interfaces

## `IScmEventSource`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

Optionale Plugin-Fähigkeit, beobachtete Textstände zu lesen; wird von allen
Enumerations-/Lesepfaden genutzt (`plugin is not IScmEventSource` ⇒ übersprungen).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext repository`, `CancellationToken ct` | `Task<ScmEventRepository>` | Lädt die authentifizierte Identität und den kanonischen Repository-Kontext; das `Konto` des übergebenen Kontexts wählt das Credential. |
| `ReadEventsAsync` | `ScmEventReadRequest request`, `CancellationToken ct` | `Task<ScmEventBatch>` | Liest eine Quelle vollständig (kein Teilresultat bei Fehlern); liefert den `ScmEventBatch`, den `ErkennenUndEinreihenAsync` auswertet. |
| `EnumerateReferencesAsync` | `ScmEventRepository repository`, `DateTimeOffset? sinceUtc`, `CancellationToken ct` | `Task<IReadOnlyList<ScmEventReference>>` | Listet zu überwachende Referenzen: immer die offenen, plus geschlossene mit Aktivität ab `sinceUtc` (`null` = nur offene). Liefert **keine** `CreatedAt`-Daten. |
| `ReadIssueAsync` | `ScmEventReference reference`, `CancellationToken ct` | `Task<Issue?>` | Lädt die Anlagedaten einer Issue-Referenz; `null`, wenn die Referenz nicht (mehr) oder nicht als Issue existiert. |
| `ReadPullRequestAsync` | `ScmEventReference reference`, `CancellationToken ct` | `Task<PullRequest?>` | Analog für Pull Requests. |

## `IScmNotificationSource`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`

Optionale Plugin-Fähigkeit für die Notifications-Inbox des separaten Kontos; entscheidet im
`ScmEventMonitoringService` zusammen mit `ScmKontoModus.SeparatesKonto` über den `inboxMode`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `string host`, `DateTimeOffset? sinceUtc`, `CancellationToken ct` | `Task<IReadOnlyList<ScmNotification>>` | Listet die Notification-Einträge des Hosts; `sinceUtc` begrenzt auf Aktivität ab diesem Zeitpunkt. |

## `IScmEventResponder`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`

Optionale Fähigkeit für abgeglichene Antworten am gespeicherten Ereignisursprung — der
unverändert bleibende Ausführungsweg (Outbox → `IssueBodyUpdate`/`Comment`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReconcileResponseAsync` | `ScmResponseRequest request`, `CancellationToken ct` | `Task<ScmResponseResult?>` | Prüft alle relevanten Seiten; `null` = sicher nicht gefunden; Fehler werfen typisierte `ScmProviderException`. |
| `ExecuteResponseAsync` | `ScmResponseRequest request`, `CancellationToken ct` | `Task<ScmResponseResult>` | Gleicht vor dem Schreiben ab; bei unbekanntem Schreibergebnis ist `OutcomeUncertain` gesetzt. |

## `IPluginManager`

Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

Verwaltet Discovery und Zugriff auf geladene Plugins; `GetSourceCodeManagementPlugins()`
liefert die SCM-Plugins, über die alle Monitoring-Dienste iterieren.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins` | — | `IReadOnlyList<IGitPlugin>` | Alle geladenen SCM-Plugins. |
| `GetDevelopmentAutomationPlugins` | — | `IReadOnlyList<IKiPlugin>` | Alle geladenen Development-Automation-Plugins. |
| `GetDefaultSourceCodeManagementPlugin` | — | `IGitPlugin` | Erstes verfügbares SCM-Plugin. |
| `GetDefaultDevelopmentAutomationPlugin` | — | `IKiPlugin` | Priorisiertes Development-Automation-Plugin. |
| `GetIdePlugins` | — | `IReadOnlyList<IIdePlugin>` | Alle geladenen IDE-Plugins. |
| `GetDefaultIdePlugin` | — | `IIdePlugin` | Erstes verfügbares IDE-Plugin. |
