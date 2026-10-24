# Interfaces und Contracts

## `IScmPromptSessionAdapter` — `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs:24-48`

Einzige Implementierung: `ScmPromptSessionAdapter` (Zeile 54). Konsumenten: `ScmEventOrchestrator` (Dispatch/ConfirmReady/Reconcile), `ScmEventQueueViewModel` (Öffnen-Dialog), `KiAusfuehrungsService` (Erzeugung).

| Member | Typ | Kommentar heute |
|---|---|---|
| `CliRunId` | `Guid` | „Identität des tatsächlichen Prozessstarts." |
| `WorkingDirectory` | `string` | „Das beim Start erfasste tatsächliche Arbeitsverzeichnis." |
| `PluginPrefix` | `string` | „Beim Start gewähltes Plugin." |
| `Session` | `ITerminalSession` | „Gebundene Terminalsession." |
| `IsAlive` | `bool` | „Prozess und Session leben noch." |
| `IsBusy` | `bool` | „Eine Eingabe darf einen beschäftigten Turn nicht unterbrechen." (`ReadyStatus == Busy`) |
| `ReadyStatus` | `ScmPromptReadyStatus` | „Aktueller Nachweis einer KI-Eingabe." — trägt heute die ganze Erkennungslast; `Unknown` bleibt mehrdeutig |
| `WaitForInputReadyAsync(ct)` | `Task<ScmPromptReadyStatus>` | „Wartet auf ein echtes Promptsignal, nie allein auf I/O-Ruhe." |
| `CheckExchangeAccessAsync(exchangePath, ct)` | `Task<ScmExchangeAccess>` | „Prüft den Startvertrag und fordert bei Bedarf eine separate tatsächliche Dateitool-Probe an." |
| `ConfirmReady(runId)` | `bool` | „Explizite einmalige Bestätigung des sichtbaren aktuellen Laufs." |
| `WritePromptAsync(prompt, ct)` | `Task` | „Prüft Sessionidentität und Ready erneut unmittelbar vor genau einer Texteingabe." |

**Nicht vorhanden:** Eine Eigenschaft für erkannte CLI-Version bzw. Erkennungszustand (in der Anforderung als Annahme genannt — entweder hier oder über einen neuen `ScmPromptReadyStatus`-Wert).

## `ITerminalSession` — `src/Softwareschmiede/Infrastructure/Terminal/` (Contract der Session)

Vom Adapter abonnierte Events (Konstruktor `ScmPromptSessionAdapter.cs:100-102`, Abmeldung `Dispose` 337-340):

| Member | Verwendung im Adapter |
|---|---|
| `event OutputChunk` (`TerminalOutputChunkEventArgs`, `Data` = `ReadOnlyMemory<byte>`) | Rohbytes → `_startupOutput` (≤ 32 KiB) → `RecognizeStartup` |
| `event BufferChanged` | `_outputRevision++` → `RefreshScreen` (Bildschirm-Pfad der Erkennung) |
| `event Exited` / `event Failed` | `SetReady(Exited)` |
| `TerminalBuffer? Buffer` | Zeilenraster + `CursorRow` für `RefreshScreen` |
| `Process` | `IsAlive` (`HasExited`), `ScmRestrictedProcessAccess.Capture` (indirekt in Tests) |
| `Failure` / `ExitCode` | `IsAlive` |
| `WritePromptAsync(string, CancellationToken)` | Kanalprobe (`SendAccessProbeAsync`) und Ereignis-Prompt (`WritePromptAsync`) |

`ITerminalSessionFactory.StartAsync` (`KiAusfuehrungsService.cs:282`) liefert `TerminalSessionStartResult(Process, Session, …)`; das Fixture ersetzt beides per Moq.

## SCM-Plugin-Verträge (Kontext, unverändert)

- `IScmEventSource` / `IScmEventResponder` (`src/Softwareschmiede/Domain/Interfaces`): Orchestrator nutzt `GetEventRepositoryAsync` (Berechtigungsprüfung, `ScmEventOrchestrator.cs:130-133`) und `ExecuteResponseAsync` (receipt/waiting/ai-Aktionen via `ScmEventOutbox`). Für die Versionserkennung irrelevant.
- `IScmAgentAccess` — optionale Agenten-Anbindung (`GH_TOKEN` etc.), unberührt.
- `IKiPlugin.GetTerminalStartSpecAsync` / `CheckHealthAsync` — liefern den Startvertrag, unberührt.

## Nebeneinander der Contracts (Ist)

`IScmPromptSessionAdapter` transportiert heute keinen Erkennungs-/Versionszustand; die einzige Informationsquelle für „warum wartet es" ist `ReadyStatus` (mehrdeutiges `Unknown`) plus der `Reason` von `CheckExchangeAccessAsync` — der aber im Orchestrator erst abgefragt wird, wenn `ready == Ready` war, und daher im beanstandeten Fehlerfall praktisch nie in den Queue-Grund gelangt (siehe [logic.md](logic.md), Mechanismen-Kette).
