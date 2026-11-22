# Interfaces — Bestandsaufnahme

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `GetEventRepositoryAsync` | `ScmRepositoryContext`, `CancellationToken` | `Task<ScmEventRepository>` | Löst den Repository-Kontext auf (u. a. `CanWrite`, `Account`); wird in `DispatchCoreAsync`/`DispatchPreparationAsync` für Schreibrechte und in `DetermineClosureAsync` unter `ScmAccountMode.UserAccount` genutzt. |
| `ReadEventsAsync` | `ScmEventReadRequest`, `CancellationToken` | `Task<ScmEventBatch>` | Poll der Ereignisquelle (`ScmEventMonitoringService`). |
| `EnumerateReferencesAsync` | `ScmEventRepository`, `DateTimeOffset?`, `CancellationToken` | `Task<IReadOnlyList<ScmEventReference>>` | Referenzenumeration für die Aufgabenzuordnung. |
| `ReadIssueAsync` | `ScmEventReference`, `CancellationToken` | `Task<Issue?>` | Issue lesen; `IsClosed` treibt die Issue-Fallback-Prüfung der Abschlussüberwachung und die „bereits geschlossen"-Regel der Aufbereitung. |
| `ReadPullRequestAsync` | `ScmEventReference`, `CancellationToken` | `Task<PullRequest?>` | PR lesen. |

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `ReconcileResponseAsync` | `ScmResponseRequest`, `CancellationToken` | `Task<ScmResponseResult?>` | Remote-Abgleich einer Aktion (Marker-Check). |
| `ExecuteResponseAsync` | `ScmResponseRequest`, `CancellationToken` | `Task<ScmResponseResult>` | Führt Kommentar/Reaktion/PR/IssueBodyUpdate remote aus (Outbox-`ExecuteAsync`). |

## `IScmPromptSessionAdapter`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Interface Z. 24–54, Implementierung `ScmPromptSessionAdapter` ab Z. 62)

| Member | Parameter | Typ/Rückgabewert | Zweck |
|---|---|---|---|
| `CliRunId` | — | `Guid` | Run-Identität der Sitzung; korreliert Versuch und Timeout-Prüfung (`session.CliRunId != runId` → `VerarbeitungUngeklärt`). |
| `WorkingDirectory` | — | `string` | Klon-/Arbeitsverzeichnis; Basis des Exchange-Pfads. |
| `PluginPrefix` | — | `string` | CLI-Präfix der laufenden Sitzung. |
| `IsAlive`/`IsBusy` | — | `bool` | Lebens-/Belegtzustand der Sitzung. |
| `ReadyStatus`/`CliRecognition` | — | `ScmPromptReadyStatus`/`ScmCliRecognition` | Erkennungszustand (Implementierungsseite; Treiber der Wartegründe). |
| `WaitForInputReadyAsync` | `CancellationToken` | `Task<ScmPromptReadyStatus>` | Wartet auf Eingabebereitschaft (`Ready`/`TrustPrompt`/`Busy`/`Unknown`/`Exited`). |
| `CheckExchangeAccessAsync` | `exchangePath`, `CancellationToken`, `sessionName?` | `Task<ScmExchangeAccess>` | Prüft/etabliert den Antwortkanal-Vertrag der Sitzung (Dateitool-Probe). |
| `ConfirmReady` | `runId` | `bool` | Einmalige, run-gebundene Bereit-Bestätigung. |
| `WritePromptAsync` | `prompt`, `CancellationToken` | `Task` | Schreibt den Ereignis-Prompt in die Sitzung (Erfolg → `Zugestellt`, Fehler → `ZustellungUngewiss`). |

## `IGitPlugin` (relevante Ausschnitte)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `PluginPrefix`/`PluginName` | — | `string` | Plugin-Identität; `ResolvePlugin` gleicht `PullRequestProviderDescriptor.GetPluginPrefix` ab. |
| `GetPullRequestStatusAsync` | `repositoryId`, `pullRequestNumber`, `CancellationToken` | `Task<PullRequestStatusInfo>` | Remote-PR-Status der Abschlussüberwachung (`NotSupportedException` → nicht beobachtbar). |
| `CloneRepositoryAsync`/`CreatePullRequestAsync`/… | diverse | diverse | Übrige SCM-Operationen (Klon, PR-Erstellung); nicht direkt Teil der Anforderung. |
| `CheckHealthAsync` | `CancellationToken` | `Task<bool>` | Startbereitschaft (KI-Seite über `IAiPlugin`). |

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `GetSourceCodeManagementPlugins` | — | `IReadOnlyList<IGitPlugin>` | SCM-Plugins für `DetermineClosureAsync`/`DispatchCoreAsync`. |
| `GetDevelopmentAutomationPlugins` | — | `IReadOnlyList<IAiPlugin>` | KI-Plugins für Dispatch/Plugin-Auflösung. |
| `GetDefaultSourceCodeManagementPlugin`/`GetDefaultDevelopmentAutomationPlugin`/`GetIdePlugins`/`GetDefaultIdePlugin` | — | diverse | Standard-/IDE-Plugins. |

## Weitere Contracts am Rand
- `IScmAgentAccess` (`Plugin.Contracts`): `GetAgentEnvironmentAsync` — Agenten-Umgebung (z. B. `GH_TOKEN`) beim CLI-Start.
- `IScmNotificationSource` (`Plugin.Contracts`): `EnumerateNotificationsAsync` — Inbox-Benachrichtigungen.
- `IScmEventStore`/`ScmEventOutbox`/`ScmEventStore` sind konkrete Services (keine Interfaces), siehe [logic.md](logic.md).
