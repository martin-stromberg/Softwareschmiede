# Interfaces

## `IScmPromptSessionAdapter` — Freigabesignal-Quelle

Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Z. 24–55);
Implementierung: `ScmPromptSessionAdapter`; erzeugt in `AiExecutionService` Z. 377–379.

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|-----------------------|-----------|--------------|-------|
| `CliRunId` | — | `Guid` | Identität des tatsächlichen Prozessstarts. |
| `WorkingDirectory` | — | `string` | Arbeitsverzeichnis des Starts. |
| `PluginPrefix` | — | `string` | Beim Start gewähltes Plugin (Plugin-Attribution der Belegung). |
| `Session` | — | `ITerminalSession` | Gebundene Terminalsession. |
| `IsAlive` | — | `bool` | Prozess und Session leben noch — Teil 1 des Freigabekriteriums. |
| `IsBusy` | — | `bool` | `ReadyStatus == Busy`. |
| `ReadyStatus` | — | `ScmPromptReadyStatus` | Nachweis der Eingabebereitschaft — Teil 2 (`Ready`) des Freigabekriteriums. |
| `CliRecognition` | — | `ScmCliRecognition` | Erkannter CLI-Typ/Versionszustand. |
| `WaitForInputReadyAsync` | `ct` | `Task<ScmPromptReadyStatus>` | Wartet auf echtes Promptsignal (interner `_changed`-TCS, max. 2 s je Runde). |
| `CheckExchangeAccessAsync` | `exchangePath`, `ct`, `sessionName?` | `Task<ScmExchangeAccess>` | Startvertrags-/Kanalprüfung inkl. Dateitool-Probe. |
| `ConfirmReady` | `runId` | `bool` | Manuelle einmalige Bestätigung. |
| `WritePromptAsync` | `prompt`, `ct` | `Task` | Serialisierte, ready-geprüfte Texteingabe. |
| `MarkInputStarted` (nur Implementierung, nicht im Interface) | — | `void` | Setzt `Busy`; verdrahtet über `ITerminalSession.InputStarted`. |

Hinweis: Kein öffentliches `ReadyStatusChanged`-Ereignis — Übergänge sind nur intern über den
`_changed`-TCS sichtbar (`SetReady`, Z. 375–382). Ein dediziertes Auslösesignal (Variante b der
Anforderung) müsste neu ergänzt werden.

## `ICliExecutionLimitSource` — Belegungsquelle des Limit-Service

Datei: `src/Softwareschmiede/Application/Services/ICliExecutionLimitSource.cs`;
Implementierung: `AiExecutionService` (Singleton, `App.xaml.cs` Z. 332–333).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRunningWorkItemIds` | — | `IReadOnlyCollection<Guid>` | Aufgaben-IDs mit lebendem Prozess-Handle. |
| `GetScmPromptSession` | `workItemId` | `IScmPromptSessionAdapter?` | Registrierte SCM-Promptsitzung der Aufgabe oder `null` (klassischer Start ohne Startvertrag). |

## `IRunningAutomationStatusSource` — Laufzählung/Triggerquelle

Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`;
Implementierung: `AiExecutionService` (`App.xaml.cs` Z. 330–331); abonniert von
`ScmStartCheckService`.

| Methode / Event | Parameter | Rückgabewert | Zweck |
|-----------------|-----------|--------------|-------|
| `RunningCountChanged` | — | `event Action<int,int>?` | (vorher, aktuell); `ScmStartCheckService` löst nur bei sinkender Zahl aus. |
| `GetRunningCount` | — | `int` | Anzahl laufender Automatisierungen. |
| `IsRunning` | `workItemId` | `bool` | Prozess läuft für die Aufgabe. |
| `GetLiveRunStatus` | `workItemId` | `WorkItemRunStatus?` | Live-Substatus (`null` ohne Handle, sonst `WaitingForInput`/`Running`). |

## `ITerminalSession` — relevante Member

Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs`;
Hülle `ScmCoordinatedTerminalSession` ergänzt `InputStarted`.

| Member | Typ | Zweck |
|--------|-----|-------|
| `Process` | `Process` | Laufender Prozess (`HasExited`-Prüfungen). |
| `Buffer` | `TerminalBuffer` | Bildschirmpuffer für die Prompt-Auswertung des Adapters. |
| `RuntimeStatus` | `CliRuntimeStatus` | I/O-Ruhe-Heuristik. |
| `RuntimeStatusChanged` | `event EventHandler<CliRuntimeStatusChangedEventArgs>?` | Abonniert von `CliProcessManager` (`SubscribeRuntimeStatus`). |
| `BufferChanged` | `event EventHandler?` | Treibt `ScmPromptSessionAdapter.RefreshScreen` (Ready-Auswertung). |
| `Exited` / `Failed` | Events | → `ReadyStatus.Exited`. |
| `OutputChunk` | `event EventHandler<TerminalOutputChunkEventArgs>?` | Startup-Erkennung des Adapters. |
| `WriteInputAsync` / `WritePromptAsync` | Methoden | In `ScmCoordinatedTerminalSession` mit `InputStarted`-Invoke → `MarkInputStarted`. |
| `InputStarted` (nur `ScmCoordinatedTerminalSession`) | `Action?` | Vor jeder Eingabe; verdrahtet auf `adapter.MarkInputStarted` (`AiExecutionService` Z. 379). |
