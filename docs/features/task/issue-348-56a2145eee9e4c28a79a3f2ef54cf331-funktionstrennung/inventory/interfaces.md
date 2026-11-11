# Interfaces — Bestandsaufnahme

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

Optionale Plugin-Fähigkeit; `ScmEventMonitoringService` überspringt Plugins ohne diese Fähigkeit (`plugin is not IScmEventSource → continue`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext repository`, `CancellationToken` | `Task<ScmEventRepository>` | Kanonische Repository-Auflösung + Identität des Aufrufers; der `Konto`-Wert im Context wählt das Credential. Wird aufgerufen von `ScmEventMonitoringService` (Alias-Auflösung, konfigurierter Modus) und `ScmTaskCompletionMonitoringService` (Issue-Prüfung, konfigurierter Modus). |
| `ReadEventsAsync` | `ScmEventReadRequest`, `CancellationToken` | `Task<ScmEventBatch>` | Liest eine Quelle vollständig (Beschreibung + Kommentare einer Referenz). |
| `EnumerateReferencesAsync` | `ScmEventRepository`, `DateTimeOffset? sinceUtc`, `CancellationToken` | `Task<IReadOnlyList<ScmEventReference>>` | Repository-weite Enumeration; speist die Backmerge-Bewertung (`discovered`). Läuft heute nur im `Benutzerkonto`-Pfad. |
| `ReadIssueAsync` | `ScmEventReference`, `CancellationToken` | `Task<Issue?>` | Issue-Anlagedaten inkl. `IstGeschlossen` — Abschlusskriterium im Issue-Pfad. |
| `ReadPullRequestAsync` | `ScmEventReference`, `CancellationToken` | `Task<PullRequest?>` | PR-Anlagedaten (nicht Teil dieser Anforderung). |

## `IScmNotificationSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `string host`, `DateTimeOffset? sinceUtc`, `CancellationToken` | `Task<IReadOnlyList<ScmNotification>>` | Liest die Notifications-Inbox des separaten Kontos je Host; ersetzt im `SeparatesKonto`-Modus die repository-weite Enumeration vollständig. |

## `IGitPlugin` (relevante Methoden)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/` (Basis-Interface der SCM-Plugins)

Von den drei Automatisierungsdiensten genutzte Members:

| Methode | Zweck / Aufrufer |
|---------|------------------|
| `GetPullRequestStatusAsync(repositoryId, nummer, ct)` | PR-Status + Mergebarkeit: `ScmTaskCompletionMonitoringService.DetermineClosureAsync`, `ScmBackmergeService.BewertePullRequestAsync`. |
| `GetDefaultBranchAsync(repositoryUrl, ct)` | Hauptbranch-Ermittlung für das Backmerge-Muster (`ScmBackmergeService`). |
| `CompletePullRequestAsync(repositoryId, nummer, PullRequestCompletionOptions, ct)` | Admin-Merge des Backmerge-PRs (`Strategy = Merge`, `MergeMethod = Merge`, `AllowProtectedBranchBypass`). |
| `PluginPrefix` / `PluginName` / `PluginType` / `GetSettingGroups()` | Plugin-Matching (`MatchesPlugin`) und Pflichtfeld-Validierung `AgentToken` in `SettingsViewModel.ValidiereScmKontoModus`. |

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/`

| Methode | Zweck / Aufrufer |
|---------|------------------|
| `GetSourceCodeManagementPlugins()` | Liefert alle SCM-Plugins: `ScmEventMonitoringService` (Enumeration), `ScmTaskCompletionMonitoringService.DetermineClosureAsync` (Plugin-Auflösung), `SettingsViewModel` (Pluginliste + `ValidiereScmKontoModus`). |

## Kein Interface für den Backmerge-Aufrufer
Es existiert **kein** Interface und keine eigene gehostete Klasse, die die Backmerge-Bewertung unabhängig von `ScmEventMonitoringService` zyklisch aufruft; `ScmBackmergeService` ist `AddScoped` registriert und wird ausschließlich innerhalb der Enumerationsrunde aufgelöst (siehe [logic.md](logic.md)).
