# CLI-Zustellung und Statusmodell

Zurück: [../inventory.md](../inventory.md)

## 1. Dispatch-Einstiegspunkte

`ScmEventOrchestrator` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`, scoped):

| Methode | Aufrufer | Zweck |
|---------|----------|-------|
| `DispatchAsync(eventId, explicitPluginPrefix?)` (Z. 21-26) | Monitoring-Poll, Exchange-Service, „Status erneut prüfen" | Zustellung versuchen oder laufenden Versuch rekonstruieren |
| `OpenFromQueueAsync(eventId, select)` (Z. 29-70) | UI „Öffnen / CLI auswählen" | Lease + Queue-Auswahl (laufende Sitzung bestätigen oder Plugin wählen) + Dispatch |
| `ActivateForOpenAsync` (Z. 226-232) | UI | Status Neu→Gestartet für unpausierte Aufgabe |
| `RetryAsync` (Z. 235-244) | UI „Erneut prüfen" | `NextAttemptUtc`/`ErrorCode` löschen (auch Outbox-Actions + alle Checkpoints!) + Dispatch |
| `ConfirmReadyAsync` (Z. 247-259) | UI „Eingabebereitschaft bestätigen" | rungebundene einmalige Ready-Bestätigung am lebenden Adapter |
| `ResendAsync` (Z. 262-276) | UI „Neu senden…" | nur bei `ZustellungUngewiss`/`VerarbeitungUngeklärt` mit passender AttemptId |

Alles läuft unter `ScmTaskCoordinator.Shared.RunAsync(taskId,…)` + DB-Lease `ScmTaskLeases` (`ClaimTaskAsync`, 60 s, 15 s Renewal, `ScmEventStore.cs:84-97`; Renewal in `ScmEventOrchestrator.cs:401-414`). `MutateAndDispatchAsync` (Z. 278-305) ist der gemeinsame Rahmen für Retry/Confirm/Resend.

## 2. `DispatchCoreAsync` — die Pipeline (Z. 72-223)

Reihenfolge der Prüfungen (jede „Wartend"-Setzung via `WaitAsync`/`WaitWithCommentAsync`, letztere postet zusätzlich einen Wartehinweis-Kommentar über die Outbox):

1. Plugin muss `IScmEventSource` **und** `IScmEventResponder` sein (Z. 84-89).
2. Nicht-Description-Events: „receipt"-Outbox (`eyes`-Reaktion) + fällige „waiting"-Actions nachholen (Z. 91-99) — Hostaktionen unabhängig vom Zustellstatus.
3. `Status == Verarbeitet` → Ende (Z. 100).
4. `DeliveryAttemptId != null` → `ReconcileAttemptAsync` (s. u.) und **Return** — kein zweiter Prompt (Z. 101-111).
5. `NextAttemptUtc` in Zukunft → Return (Z. 114).
6. Älteres unverarbeitetes Event derselben Aufgabe → Wartend (FIFO, Z. 115-119).
7. Aufgabe gelöscht → Wartend; `BlockReason`: beendet/archiviert oder `PausiertBisUtc` in Zukunft → Wartend (Z. 120-123, 381-383).
8. **Owner-/Rechte-Prüfung pro Dispatch** (Z. 124-131): `GetEventRepositoryAsync` erneut; `RepositoryId`-Drift oder `!CanWrite` → Wartend mit Token-Anleitung; `GetRejection` mit aktueller Repo-Identität → Wartend „Angemeldeter Benutzer ist nicht mehr der bestätigte Repository-Owner."
9. Status `Neu` → `SetStatusAsync(Gestartet)` (Z. 132).
10. Lebender Adapter mit anderem Prefix als explizit gewählt → Wartend (Z. 134-136). `adapter == null` aber andere `ITerminalSession` aktiv (Alt-Session ohne Startvertrag) → Wartend (Z. 137-138).
11. `adapter == null || !IsAlive` → CLI-Auflösung & -Start (Z. 139-153):
    - `prefix = explicitPluginPrefix ?? ResolveDevelopmentAutomationPluginWithProjectScopeAsync(task.KiPluginPrefix, task.ProjektId)`; `null` → **„Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen."** (Z. 141-143 — das ist die FR-6-Aufforderung)
    - `ResolveDevelopmentAutomationPluginStrictAsync` + `CheckHealthAsync` (Fehler → `InvalidOperationException` → `NextAttemptUtc = long.MaxValue`)
    - Simulator bekommt `--scm-exchange` (Z. 146)
    - ohne `LokalerKlonPfad` → `ProzessStartenUndCliStartenAsync`, sonst `CliNeustartenAsync` (Z. 147-151)
    - danach erneut `GetScmPromptSession`; immer noch null → Wartend „CLI-Sitzung ist nicht verfügbar." (Z. 154-155)
12. Task nach Reload erneut auf BlockReason/Status prüfen (Z. 156-160).
13. `item.CliRunId = adapter.CliRunId` persistiert (Z. 163) — Basis für `CanConfirmReady` in der UI.
14. `WaitForInputReadyAsync` ≠ `Ready` → Wartend mit Reason je nach `IsBusy`: „CLI beschäftigt; wartet auf Eingabebereitschaft." oder **„Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen."** (Z. 164-169).
15. `CheckExchangeAccessAsync(.softwareschmiede-scm-exchange)` ≠ `Supported` → Wartend „Antwortkanal für diese CLI nicht verfügbar: <Reason>" (Z. 171-173).
16. `exchange.PrepareAsync` legt Attempt-Verzeichnis + `request.json` an (Z. 174-176); Adapterwechsel-Recheck (Z. 177-178).
17. Persistieren: `Status="ZustellungVorbereitet"`, `DeliveryAttemptId`, `CliRunId`, `ExchangePath`, `ExchangeRestrictedWriteSidsJson`, `DeliveredUtc`, `Attempts++` (Z. 179-188).
18. `adapter.WritePromptAsync(attempt.Prompt)` → Erfolg `Status="Zugestellt"`, Exception `Status="ZustellungUngewiss"` (Z. 189-199).

Fehlerbehandlung: `ScmProviderException` → ErrorCode + Backoff bzw. `long.MaxValue` bei Auth/Permission (Z. 202-208); `InvalidOperationException/IOException/UnauthorizedAccessException` → **`NextAttemptUtc = long.MaxValue`** (kein Auto-Retry, nur manuelle Prüfung) + Wartehinweis-Kommentar (Z. 209-214).

`ReconcileAttemptAsync` (Z. 307-379): liest `accepted.json`, `actions/*.json`, `complete.json` aus dem Exchange-Verzeichnis; mapped Status `InBearbeitung`/`Verarbeitet`/`VerarbeitungUngeklärt`; verarbeitet KI-Aktionen über Outbox→`IScmEventResponder` und schreibt `results/{actionId}.json`; Timeouts aus `ScmEventOptions` (`AcceptanceTimeout` 2 min, `CompletionTimeout` 30 min) oder tote/geänderte Session → „VerarbeitungUngeklärt" **ohne** automatischen Neuversand.

## 3. Statusmodell `ScmIncomingEvent.Status`

| Status | Bedeutung | Erreicht über |
|--------|-----------|---------------|
| `Aufgenommen` | Default nach Inbox-Insert | `ScmIncomingEvent.cs:22` |
| `Wartend` | blockiert mit `Reason` | `WaitAsync` (`ScmEventOrchestrator.cs:385-390`) |
| `ZustellungVorbereitet` | Attempt persistiert, Schreiben begonnen | Z. 179 |
| `Zugestellt` | Prompt erfolgreich geschrieben | Z. 192 |
| `InBearbeitung` | `accepted.json` korreliert gelesen | `ReconcileAttemptAsync` Z. 325 |
| `Verarbeitet` | `complete.json` + alle Aktionen `Succeeded` (oder begründetes `noRemoteAction`) | Z. 349-358 |
| `ZustellungUngewiss` | Schreibversuch nicht sicher bestätigt / unterbrochen | Z. 196-197, 362-365 |
| `VerarbeitungUngeklärt` | Kanal/Timeout/Identitätsproblem | Z. 106-107, 319-320, 335-346, 374-375 |

Wartend-Einträge mit `NextAttemptUtc == null` werden vom 60-s-Poll automatisch erneut dispatchet; `long.MaxValue` = manuelle Prüfung erforderlich (`ScmEventMonitoringService.cs:121-124`).

## 4. `ScmPromptSessionAdapter` — Eingabebereitschaft und Kanalnachweis

`src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`; erzeugt in `KiAusfuehrungsService.StartTerminalSessionCoreAsync` (`KiAusfuehrungsService.cs:300-302`) an jedes `CliProcessHandle` gebunden (in-memory, `_handles`).

- **CLI-Erkennung** (`RecognizeStartup`, Z. 227-234): `_recognizedCli` nur bei Bildschirmtext „OpenAI Codex (v0.114.0)" / „Claude Code v2.1.0" / `SCM_SIMULATOR_PROTOCOL_V1` (Simulator + Supported/Denied).
- **Ready-Erkennung** (`RefreshScreen`, Z. 236-262): liest `Session.Buffer` (letzter Bildschirm); Busy bei „esc to interrupt"/„Esc to cancel"; Ready bei Cursorzeile `›`/`› Implement {feature}`/`› Explain this codebase` (Codex) bzw. `❯` (Claude) bzw. `SCM_READY_V1` (Simulator); sonst `Unknown`. Guards: `!_recognizedCli` → nie Ready; `_manuallyConfirmed` → bleibt Ready; `_inputRevision == _outputRevision` → keine neue Bewertung ohne neue Ausgabe.
- **`WaitForInputReadyAsync`** (Z. 111-121): einmaliger Refresh + max. 2 s Warten auf Statuswechsel — kein langes Warten.
- **`ConfirmReady(runId)`** (Z. 181-192): manuelle Einmal-Bestätigung, schlägt fehl wenn `!_recognizedCli || _ready == Busy` → Orchestrator wirft `InvalidOperationException` („CLI ist beschäftigt oder kann noch nicht bestätigt werden…").
- **`MarkInputStarted`** (Z. 195-203): bei jeder Eingabe (auch Benutzereingaben im Terminal — `ScmCoordinatedTerminalSession` ruft `InputStarted` bei `WriteInputAsync`/`WritePromptAsync`, `ScmCoordinatedTerminalSession.cs:21-33`) → `_manuallyConfirmed=false`, `Busy`.
- **`CheckExchangeAccessAsync`** (Z. 124-178): statischer `ScmCliAccessEvaluator`-Befund + Revalidierung (`IsUnchanged(_files)`), Pfadsicherheit, `_recognizedCli`, dann für echte CLIs die **Dateitool-Probe**: `_accessProbe` wird nur erzeugt wenn `ReadyStatus==Ready` (Z. 159); Probe-Prompt (`BuildAccessProbePrompt`, `ScmEventExchangeProtocol.cs:121-128`) instruiert die CLI, `challenge.json` zu lesen → `proof.tmp` zu schreiben → in `proof.json` umzubenennen → `challenge.json` in `challenge-read.json` umzubenennen. Ergebnis bis dahin: `Unknown` „Nachweis des tatsächlichen Dateitool-Zugriffs steht aus; separate lokale Kanalprobe muss erfolgreich abgeschlossen werden." (Z. 169). **Kein Timeout/Reset für `_accessProbe`.**
- **`WritePromptAsync`** (Z. 206-216): unter Task-Sperre; verbietet Steuerzeichen; recheckt Kanal + Ready unmittelbar vor dem Schreiben.
- Zugriffsevaluierung `ScmCliAccessEvaluator` (`ScmCliAccessEvaluator.cs`): `Capture` (Z. 38-64) liest vor dem Start Codex-`~/.codex/*.toml` + Managed-Pfade + `.codex/config.toml`-Kette bzw. Claude `~/.claude/settings.json`, `<repo>/.claude/settings(.local).json`, Managed-Settings; `Evaluate` (Z. 74-202) erlaubt nur streng analysierbare Konfigurationen: Codex `sandbox_mode=workspace-write`, Claude `permissions.defaultMode="acceptEdits"`, keine deny/ask-Regeln, keine managed/requirements-Dateien, keine freien Parameter außer `--model`, `--sandbox/-s`, `--ask-for-approval/-a` (Codex) bzw. `--permission-mode` (Claude). Alles andere → `Unknown`.
- `ScmRestrictedProcessAccess` (`ScmRestrictedProcessAccess.cs`): Windows-only; liest dedizierte Capability-SIDs des Arbeitsverzeichnis-DACLs bzw. Prozess-Tokens für ACLs des Austauschkanals.

## 5. Dateitransport `.softwareschmiede-scm-exchange/`

`ScmEventExchangeProtocol` (`ScmEventExchangeProtocol.cs`): `DirectoryName` (Z. 71); Layout `<workdir>/.softwareschmiede-scm-exchange/<runId>/<attemptId>/{request.json, accepted.json, actions/, results/, complete.json}`; Ownership-Marker `.channel-v1`; private ACLs (`CreatePrivateDirectory` mit dedizierten Write-SIDs); `.git/info/exclude`-Eintrag (`EnsureGitExcludedAsync`); `BuildPrompt` (Z. 260-268) serialisiert Instruction + Request + Schemata als einzeiliges JSON; alle Meldungen mit `protocolVersion=1` + EventId/AttemptId/CliRunId-Korrelation (`Correlate` Z. 293-297); atomare Publikation via tmp+rename; Dateigrößenlimit 1 MB.

**Prompt-Zustellung**: `PseudoConsoleSession.WritePromptAsync` (`PseudoConsoleSession.cs:497-501`) schreibt UTF-8 + abschließendes `\r` (CR wie physischer Enter) in den ConPTY-Input-Stream — kein SendKeys/Zwischenablage. `ScmCoordinatedTerminalSession` serialisiert alle Eingaben (manuell + SCM) über die Task-Sperre und feuert `InputStarted`.

## 6. UI: SCM-Ereignisübersicht

- `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml` + `ScmEventQueueViewModel.cs`; Navigation über Button `ScmEventQueueNavigation` im Dashboard (`MainWindow.xaml:94-102`, `MainWindowViewModel.cs:244-259`); Label zeigt Anzahl nicht-verarbeiteter Events (`MainWindowViewModel.cs:268-277`).
- Filter: „Wartend" (= `Status != Verarbeitet`), „Verarbeitet", „Alle" (`ScmEventQueueViewModel.cs:75,147-149`).
- Aktionen (AutomationIds): `ScmEventOpen` „Öffnen / CLI auswählen" (nur `!IsProcessed && !IsBlocked && AttemptId == null`), `ScmEventRetry` „Erneut prüfen", `ScmEventConfirmReady` „Eingabebereitschaft bestätigen" (nur `CanConfirmReady`: AttemptId null + CliRunId gesetzt + Reason enthält „Eingabebereitschaft unbekannt", `ScmEventQueueViewModel.cs:295`), `ScmEventCheckStatus` „Status erneut prüfen" + `ScmEventResend` „Neu senden…" (nur `CanResend`: AttemptId + Status ungewiss/ungeklärt), `ScmEventRetrySources` „Quellen erneut prüfen" (Checkpoint-Sperren aufheben + Monitorlauf, Z. 47-53), `ScmEventTask`, `ScmEventSettings`, `ScmEventBack`, `ScmEventRefresh`.
- `OpenAsync` (Z. 219-246): bei lebender Session `BestaetigenDialog` „An laufende CLI übergeben…", sonst `ShowPluginSelectionDialogAsync` mit Option „als Projekt-Default speichern".
- Refresh über `ScmEventChangeNotifier.Changed` → `RefreshInBackground` (Z. 272-273); Diagnostikzeile aus Checkpoints/Actions (Z. 154-178); `MonitoringNotice` bei unterbrochenen Quellen.
- `ScmEventQueueItem` (Z. 286-311): projiziert Status in deutsche Texte; `IsBlocked` für beendete/archivierte/pausierte Aufgaben.
