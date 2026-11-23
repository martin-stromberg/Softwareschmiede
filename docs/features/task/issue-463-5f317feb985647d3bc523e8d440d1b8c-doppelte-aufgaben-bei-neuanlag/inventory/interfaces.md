# Interfaces — Bestandsaufnahme

## `IScmPromptSessionAdapter`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Zeilen 24–59)

Rungebundene sichere SCM-Texteingabe und Prüfung des lokalen Antwortkanals; Implementierung: `ScmPromptSessionAdapter`, bereitgestellt über `AiExecutionService.GetScmPromptSession`. Technische Basis des „wartende CLI"-Zustands der Stabilisierungsfrist.

| Methode / Member | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `CliRunId` | — | `Guid` | Identität des tatsächlichen Prozessstarts. |
| `WorkingDirectory` | — | `string` | Beim Start erfasstes Arbeitsverzeichnis. |
| `PluginPrefix` | — | `string` | Beim Start gewähltes Plugin; Slot-Attribution der Ausführungsbegrenzung. |
| `Session` | — | `ITerminalSession` | Gebundene Terminalsession. |
| `IsAlive` | — | `bool` | Prozess und Session leben noch. |
| `IsBusy` | — | `bool` | `ReadyStatus == Busy`; Eingabe darf einen Turn nicht unterbrechen. |
| `ReadyStatus` | — | `ScmPromptReadyStatus` | Nachweis der Eingabebereitschaft (`Unknown`/`Ready`/`Busy`/`TrustPrompt`/`Exited`). |
| `ReadyStatusChanged` | — | `event Action<ScmPromptReadyStatus>?` | Zustandswechsel-Signal; `Ready`-Übergang löst die Startprüfung aus (Verdrahtung `AiExecutionService` ~Zeile 388). |
| `CliRecognition` | — | `ScmCliRecognition` | Erkannter CLI-Typ/Versionszustand. |
| `WaitForInputReadyAsync` | `ct` | `Task<ScmPromptReadyStatus>` | Wartet auf ein echtes Promptsignal (max. ~2 s pro Aufruf), nie nur auf I/O-Ruhe. |
| `CheckExchangeAccessAsync` | `exchangePath`, `ct`, `sessionName?` | `Task<ScmExchangeAccess>` | Startvertrag + Dateitool-Kanalprobe des Antwortkanals. |
| `ConfirmReady` | `runId` | `bool` | Explizite einmalige Bestätigung des sichtbaren Laufs. |
| `WritePromptAsync` | `prompt`, `ct` | `Task` | Genau eine Texteingabe mit Session-/Ready-Re-Check unmittelbar davor; wirft bei `!Ready` oder nicht-`Supported`-Zugriff. |

## `ICliExecutionLimitSource`
Datei: `src/Softwareschmiede/Application/Services/ICliExecutionLimitSource.cs`

Belegungsquelle der Ausführungsbegrenzung; implementiert von `AiExecutionService`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRunningWorkItemIds` | — | `IReadOnlyCollection<Guid>` | Aufgaben-IDs mit laufendem CLI-Prozess-Handle. |
| `GetScmPromptSession` | `workItemId` | `IScmPromptSessionAdapter?` | Registrierte SCM-Promptsitzung der Aufgabe. |

## `IScmEventSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`

Optionale Plugin-Fähigkeit: vollständige beobachtete Textstände einer Referenz lesen.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEventRepositoryAsync` | `repository: ScmRepositoryContext`, `ct` | `Task<ScmEventRepository>` | Authentifizierte Identität + kanonischer Repository-Kontext (inkl. `CanWrite`, `Account`). |
| `ReadEventsAsync` | `request: ScmEventReadRequest`, `ct` | `Task<ScmEventBatch>` | Vollständiger Quell-Read je `ContentKind` (kein Teilresultat). |
| `EnumerateReferencesAsync` | `repository`, `sinceUtc`, `ct` | `Task<IReadOnlyList<ScmEventReference>>` | Repository-weite Referenzliste (offene + ggf. geschlossene mit Aktivität). |
| `ReadIssueAsync` | `reference`, `ct` | `Task<Issue?>` | Anlagedaten eines Issues; `null` bei nicht lesbar/nicht-Issue. Genutzt von Resolver und Aufbereitungspfad. |
| `ReadPullRequestAsync` | `reference`, `ct` | `Task<PullRequest?>` | Anlagedaten eines PRs inkl. Branches. |

## `IScmEventResponder`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`

Abgleich/Ausführung von Remote-Antworten am gespeicherten Ereignisursprung.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReconcileResponseAsync` | `request: ScmResponseRequest`, `ct` | `Task<ScmResponseResult?>` | Abgleich aller relevanten Seiten; `null` = sicher nicht gefunden. |
| `ExecuteResponseAsync` | `request`, `ct` | `Task<ScmResponseResult>` | Remoteaktion ausführen; `OutcomeUncertain` bei unbekanntem Ergebnis. |

## `IScmNotificationSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnumerateNotificationsAsync` | `host`, `sinceUtc`, `ct` | `Task<IReadOnlyList<ScmNotification>>` | Notifications-Inbox des separaten Kontos (Inbox-Modus; Aufbereitungs-Entdeckung läuft hier **nicht** — dafür existiert `ScmIssuePreparationMonitoringService`). |

## `IRunningAutomationStatusSource` (Kontext)
Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`

`RunningCountChanged(previous, current)` — sinkende Laufzählung löst die SCM-Startprüfung aus (`ScmStartCheckService`).

## `ScmStartCheckTrigger` (Signal-Contract, Klasse)
Datei: `src/Softwareschmiede/Application/Services/ScmStartCheckService.cs` (Zeilen 20–27)

`event Action? Requested` + `RequestStartCheck()` — non-blocking, koaleszierend; siehe [logic.md](logic.md).
