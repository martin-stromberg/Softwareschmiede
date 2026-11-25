# Interfaces

## `IActiveWorkItemsService`
Datei: `src/Softwareschmiede/Application/Services/IActiveWorkItemsService.cs` (implementiert von `WorkItemService`, scoped)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetActiveWorkItemsAsync` | `CancellationToken` | `Task<List<WorkItem>>` | Alle aktiven Aufgaben (Seitenleisten-/Kandidatenmenge, Basis der Snapshot-Projektion „Laufende Aufgaben") |

## `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs` (implementiert von `AiExecutionService`, Singleton)

| Methode/Event | Parameter | Rückgabewert | Zweck |
|---------------|-----------|--------------|-------|
| `RunningCountChanged` | — | `event Action<int,int>` | Änderung der Anzahl laufender Automatisierungen (Change-Signal) |
| `GetRunningCount` | — | `int` | Anzahl laufender Automatisierungen |
| `IsRunning` | `Guid workItemId` | `bool` | Läuft für die Aufgabe aktuell eine Automatisierung |
| `GetLiveRunStatus` | `Guid workItemId` | `WorkItemRunStatus?` | Live-Substatus (`Running`/`WaitingForInput`/`null`) — aktueller als der persistierte `WorkItem.RunStatus` |

## `ICliExecutionLimitSource`
Datei: `src/Softwareschmiede/Application/Services/ICliExecutionLimitSource.cs` (implementiert von `AiExecutionService`)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRunningWorkItemIds` | — | `IReadOnlyCollection<Guid>` | Aufgaben-IDs mit laufendem CLI-Prozess-Handle |
| `GetScmPromptSession` | `Guid workItemId` | `IScmPromptSessionAdapter?` | SCM-Promptsitzung der Aufgabe |

## `IRunningAutomationsCount`
Datei: `src/Softwareschmiede/Application/Services/IRunningAutomationsCount.cs` (implementiert von `RunningAutomationsCount`, Singleton)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CountAsync` | `CancellationToken` | `Task<int>` | Anzahl fachlich laufender Automatisierungen (inkl. verzögerter SCM-Ereignisse) — fertige Queue-/Lauf-Zählung für den Snapshot |

## `ICredentialStore`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs` (implementiert von `WindowsCredentialStore`; in E2E durch `ScmE2ECredentialStore` ersetzt)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetCredential` | `string target` | `string?` | Gespeichertes Geheimnis lesen (Relay-Token-Ablage) |
| `SetCredential` | `string target`, `string value` | `void` | Geheimnis speichern |
| `DeleteCredential` | `string target` | `void` | Geheimnis löschen |

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs` (optionale Plugin-Fähigkeit; GitHub-Plugin implementiert)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext`, `CancellationToken` | `Task<ScmEventRepository>` | Authentifizierte Identität + kanonischer Repository-Kontext (inkl. `CanWrite`, Owner/Viewer-Identität) |
| `ReadEventsAsync` | `ScmEventReadRequest`, `CancellationToken` | `Task<ScmEventBatch>` | Vollständige beobachtete Textstände einer Referenz (Kommentare, Beschreibungen, Reviews) |
| `EnumerateReferencesAsync` | `ScmEventRepository`, `DateTimeOffset? sinceUtc`, `CancellationToken` | `Task<IReadOnlyList<ScmEventReference>>` | Zu überwachende Referenzen eines Repositories |
| `ReadIssueAsync` | `ScmEventReference`, `CancellationToken` | `Task<Issue?>` | Anlagedaten einer Issue-Referenz |
| `ReadPullRequestAsync` | `ScmEventReference`, `CancellationToken` | `Task<PullRequest?>` | Anlagedaten einer PR-Referenz |

→ Der Eingangskanal, über den vom Status-Server geschriebene GitHub-Kommentare in die App zurückfließen.

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs` (optionale Plugin-Fähigkeit)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReconcileResponseAsync` | `ScmResponseRequest`, `CancellationToken` | `Task<ScmResponseResult?>` | Abgleich, ob eine angeforderte Remote-Aktion bereits ausgeführt ist |
| `ExecuteResponseAsync` | `ScmResponseRequest`, `CancellationToken` | `Task<ScmResponseResult>` | Remote-Aktion ausführen (Kommentar/Reaktion u. a.) — Träger der `waiting`-/`receipt`-Hostaktionen |

## `IScmNotificationSource` / `IScmAssignmentSource`
Dateien: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/{IScmNotificationSource,IScmAssignmentSource}.cs`

- `IScmNotificationSource`: Inbox-/Notification-Enumeration im Kontomodus `SeparateAccount` (Viewer = Bot-Identität).
- `IScmAssignmentSource`: `GetLatestViewerAssignmentAsync` — liefert Zuweisungsereignis inkl. Zuweisender für den Assign-Auslöser.

## `IApplicationVersionProvider`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (implementiert von `ApplicationVersionProvider`, Singleton)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetInstalledVersionAsync` | `CancellationToken` | `Task<InstalledVersionInfo?>` | Installierte Version aus `version.json` (App-Version für den Snapshot) |

## `IUpdateReleaseClient`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (implementiert von `GitHubReleaseClient`, Singleton)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetLatestReleaseAsync` | `UpdateCheckOptions`, `CancellationToken` | `Task<UpdateReleaseLookupResult>` | Neueste zulässige GitHub-Release-Information (Releases/Tags-Sektion der Status-Seite nutzt serverseitig dieselbe API) |

## `IUpdateService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (implementiert von `UpdateService`, Singleton)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckForUpdateAsync` | `UpdateCheckOptions`, `CancellationToken` | `Task<UpdateCheckResult>` | Update-Prüfung (Update-Verfügbarkeits-Info als mögliche Snapshot-Angabe) |
| `PrepareUpdateAsync` | `UpdateInfo`, `IProgress<UpdatePreparationProgress>?`, `CancellationToken` | `Task<UpdatePreparationResult>` | Update-Vorbereitung |
| `StartPreparedUpdateAsync` | `UpdatePreparationResult`, `CancellationToken` | `Task` | Externen Updater starten und App beenden |

## `IScmPromptSessionAdapter`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`

Laufzeit-Adapter einer CLI-Sitzung für den SCM-Antwortkanal: `IsAlive`, `IsBusy`, `CliRunId`, `PluginPrefix`, Bereitschafts-/Trust-Erkennung — Quelle des CLI-Session-Laufzeitstatus im Snapshot.
