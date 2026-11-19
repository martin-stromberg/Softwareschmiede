# Interfaces — Bestandsaufnahme

## `ICliExecutionLimitSource`
Datei: `src/Softwareschmiede/Application/Services/ICliExecutionLimitSource.cs`

Liefert die für die Ausführungsbegrenzung benötigte Belegung laufender CLI-Ausführungen. **Einzige Implementierung: `KiAusfuehrungsService`** (registriert in `ScmDispatchFixture` Z. 161 und der App-DI). Semantik des Vertrags ist „laufender Prozess" — laut Anforderung bewusst unverändert zu lassen.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetLaufendeAufgabenIds()` | — | `IReadOnlyCollection<Guid>` | Aufgaben-IDs mit laufendem CLI-Prozess-Handle (`!Process.HasExited`); Basis des `Global`-Zählers und der Plugin-Attribution in `GetBelegungAsync`; kein Pausenbewusstsein |
| `GetScmPromptSession(aufgabeId)` | `Guid` | `IScmPromptSessionAdapter?` | Registrierte SCM-Promptsitzung einer Aufgabe; `IsAlive`/`PluginPrefix` steuern die Plugin-Attribution in `GetBelegungAsync` und den Fortsetzungspfad im `ScmEventOrchestrator` |

## `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`

Laufzeitinformationen laufender Automatisierungen. Implementierung: `KiAusfuehrungsService`. Abonniert von `ScmStartPruefungsService` (Trigger bei sinkender Zählung) und `AutoShutdownOrchestrator`; `GetLiveLaufStatus` fließt in `LaufendeAutomatisierungenZaehlung` und die Statusanzeige ein; `IsRunning` wird von `AufgabeRecoveryService` und `CliProcessManager` genutzt.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunningCountChanged` (Event) | — | `Action<int, int>?` | Änderung der laufenden Prozesszahl (vorheriger/aktueller Wert); **feuert bei Prozessstart und Prozessende — nicht beim Pausen-Setzen** |
| `GetRunningCount()` | — | `int` | Anzahl laufender Handles (`!HasExited`) |
| `IsRunning(aufgabeId)` | `Guid` | `bool` | Läuft ein gehandelter Prozess für die Aufgabe? |
| `GetLiveLaufStatus(aufgabeId)` | `Guid` | `AufgabeLaufStatus?` | Live-Substatus (`WartetAufEingabe`/`Laeuft`/`null`) aus der Terminal-Session |

## `IScmPromptSessionAdapter`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Z. 24–55)

Rungebundene sichere SCM-Texteingabe und Prüfung des Antwortkanals. Implementierung: `ScmPromptSessionAdapter`; für die Limit-Attribution relevant sind `IsAlive` und `PluginPrefix`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CliRunId` (Property) | — | `Guid` | Identität des tatsächlichen Prozessstarts |
| `WorkingDirectory` (Property) | — | `string` | Arbeitsverzeichnis des Laufs (Exchange-Kanal) |
| `PluginPrefix` (Property) | — | `string` | Beim Start gewähltes Plugin — Attribution im `ProPlugin`-Zähler |
| `Session` (Property) | — | `ITerminalSession` | Gebundene Terminalsession |
| `IsAlive` (Property) | — | `bool` | Prozess und Session leben noch; steuert Attribution (lebende Sitzung vor `Aufgabe.KiPluginPrefix`) und den Fortsetzungspfad ohne Zulassungsprüfung |
| `IsBusy` (Property) | — | `bool` | Beschäftigter Turn — Gate vor Eingabe |
| `ReadyStatus` (Property) | — | `ScmPromptReadyStatus` | Nachweis einer KI-Eingabe |
| `CliRecognition` (Property) | — | `ScmCliRecognition` | Erkannter CLI-Typ/Versionszustand |
| `WaitForInputReadyAsync(ct)` | `CancellationToken` | `Task<ScmPromptReadyStatus>` | Wartet auf echtes Promptsignal |
| `CheckExchangeAccessAsync(exchangePath, ct, sessionName)` | `string`, `CancellationToken`, `string?` | `Task<ScmExchangeAccess>` | Prüft den Antwortkanal in der CLI-Schreibwurzel |
| `ConfirmReady(runId)` | `Guid` | `bool` | Einmalige Bestätigung des sichtbaren Laufs |
| `WritePromptAsync(prompt, ct)` | `string`, `CancellationToken` | `Task` | Eine geprüfte Texteingabe (Zustellung/Fortsetzung) |
