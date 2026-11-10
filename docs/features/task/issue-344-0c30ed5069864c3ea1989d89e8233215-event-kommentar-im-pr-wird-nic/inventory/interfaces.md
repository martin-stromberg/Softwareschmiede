# Interfaces

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `ScmRepositoryContext`, `ct` | `Task<ScmEventRepository>` | Kanonische Repository-Auflösung (Alias → `ScmEventRepository`). |
| `ReadEventsAsync` | `ScmEventReadRequest`, `ct` | `Task<ScmEventBatch>` | Vollständiges Lesen einer Quelle. |
| `EnumerateReferencesAsync` | `ScmEventRepository`, `DateTimeOffset? sinceUtc`, `ct` | `Task<IReadOnlyList<ScmEventReference>>` | Enumeration offener (und seit `sinceUtc` aktiver geschlossener) Referenzen — der Pfad, auf dem ein selbst angelegter PR entdeckt wird. |
| `ReadIssueAsync` | `ScmEventReference`, `ct` | `Task<Issue?>` | Anlagedaten einer Issue-Referenz (`null` = nicht vorhanden). |
| `ReadPullRequestAsync` | `ScmEventReference`, `ct` | `Task<PullRequest?>` | Anlagedaten einer PR-Referenz **inkl. `BranchName`, `SourceRepositoryId`, `SourceRef`, `Body`** — liefert bereits alle Kontextsignale der neuen Prüfung; wird in `ResolvePullRequestAsync` vor dem Lookup aufgerufen (Zeile 79). |

## `IScmPromptSessionAdapter`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Zeilen 24–55)

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|------------------------|-----------|--------------|-------|
| `CliRunId`, `WorkingDirectory`, `PluginPrefix`, `Session` | — | `Guid`/`string`/`ITerminalSession` | Identität des gebundenen CLI-Laufs. |
| `IsAlive` | — | `bool` | Prozess und Session leben. |
| `IsBusy` | — | `bool` | Turn/Eingabe läuft — Zustellung wartet. |
| `ReadyStatus`, `CliRecognition` | — | `ScmPromptReadyStatus`/`ScmCliRecognition` | Nachgewiesene Eingabebereitschaft, CLI-Erkennung. |
| `WaitForInputReadyAsync` | `ct` | `Task<ScmPromptReadyStatus>` | Wartet auf echtes Promptsignal (nie nur I/O-Ruhe). |
| `CheckExchangeAccessAsync` | `exchangePath`, `ct`, `sessionName` | `Task<ScmExchangeAccess>` | Prüft/fordert den Dateitool-Antwortkanal. |
| `ConfirmReady` | `runId` | `bool` | Einmalige Benutzer-Bestätigung des sichtbaren Laufs. |
| `WritePromptAsync` | `prompt`, `ct` | `Task` | Identitätsgeprüfte Texteingabe. |

Bereitstellung: `KiAusfuehrungsService.GetScmPromptSession(aufgabeId)` — `null`, solange keine lebende, vertragssichere Sitzung existiert.

## `IScmNotificationSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `host`, `DateTimeOffset? sinceUtc`, `ct` | `Task<IReadOnlyList<ScmNotification>>` | Inbox des separaten Kontos; ersetzt im `SeparatesKonto`-Modus die Repository-Enumeration. |

## `IAktiveAufgabenService`
Datei: `src/Softwareschmiede/Application/Services/IAktiveAufgabenService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAktiveAufgabenAsync` | `ct` | `Task<List<Aufgabe>>` | Seitenleisten-„aktive" Aufgaben (`Gestartet`/`Wartend`); implementiert von `AufgabeService`. |
