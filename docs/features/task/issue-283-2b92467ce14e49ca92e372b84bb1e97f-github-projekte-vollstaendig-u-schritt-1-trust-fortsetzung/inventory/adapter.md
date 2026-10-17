# Adapter: Eingabebereitschaft und Kanalprobe

Zurück: [../inventory.md](../inventory.md)

## `ScmPromptReadyStatus`

Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Z. 9-19)

| Wert | Bedeutung |
|------|-----------|
| `Unknown` | Kein verifizierter Eingabeprompt (u. a. jeder Blockierdialog — es gibt keinen eigenen Trust-Zustand). |
| `Ready` | Bestätigter KI-Eingabeprompt für den aktuellen Lauf. |
| `Busy` | Ein Turn oder eine Benutzereingabe läuft. |
| `Exited` | Der gebundene Lauf ist beendet oder fehlgeschlagen. |

## `ScmPromptSessionAdapter`

Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`

| Methode/Feld | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ScmPromptSessionAdapter(taskId, runId, pluginPrefix, spec, session, files, isCurrent)` | public ctor | Bindet StartSpec + erfasste Dateirechte; wertet `ScmCliAccessEvaluator.Evaluate` aus; abonniert `OutputChunk`/`BufferChanged`/`Exited`/`Failed`; kein `TimeProvider`. |
| `ReadyStatus` | public | `Exited` wenn `!IsAlive`, sonst `_ready` unter `_sync`. |
| `IsBusy` | public | `ReadyStatus == Busy`. |
| `WaitForInputReadyAsync` | public | Einmal `RefreshScreen` + max. 2 s auf Statuswechsel warten. |
| `CheckExchangeAccessAsync(exchangePath, ct)` | public | Unter `ScmTaskCoordinator`-Sperre → `CheckExchangeAccessCoreAsync`. |
| `CheckExchangeAccessCoreAsync` | private | Konfig-/Pfad-/Erkennungs-Guards; für echte CLIs (nicht `KiSimulator`) die Dateitool-Probe (s. u.). |
| `ConfirmReady(runId)` | public | Lehnt ab bei `!_recognizedCli \|\| _ready == Busy`; sonst `_manuallyConfirmed=true` + `Ready`. **Kein** Bildschirm-Recheck, kein Dialog-Guard. |
| `MarkInputStarted` | public | Bei jeder Session-Eingabe: `_manuallyConfirmed=false`, `_inputRevision=_outputRevision`, `Busy`. |
| `WritePromptAsync` | public | Steuerzeichen-Verbot; Kanal + Ready erneut prüfen; `MarkInputStarted`; `Session.WritePromptAsync`. |
| `RefreshScreen` | private | Liest `Session.Buffer`; `RecognizeStartup`; Guards `!_recognizedCli`, `_manuallyConfirmed`, `_inputRevision == _outputRevision`; Busy via „esc to interrupt"/„Esc to cancel"; Ready via Cursorzeile (`›`/`› Implement {feature}`/`› Explain this codebase`, `❯`, `SCM_READY_V1`); sonst `Unknown`. |
| `RecognizeStartup` | private | `_recognizedCli` nur bei exakt „OpenAI Codex (v0.114.0)", „Claude Code v2.1.0" oder `SCM_SIMULATOR_PROTOCOL_V1` (+ Supported/Denied). |
| `_accessProbe`, `_probeProtocol` | private Felder | Einmal-Probe, kein Zeitstempel/Zähler/Reset. |

Abonnierte Events: `Session.OutputChunk`, `Session.BufferChanged`, `Session.Exited`, `Session.Failed`.

### Probe-Ablauf (Z. 150-171)

1. `ScmRestrictedProcessAccess.CaptureWorkspaceCapabilities(WorkingDirectory)` — dedizierte SIDs der Workdir-DACL (Windows; sonst `[]`).
2. `_accessProbe != null` + SID-Drift → `Unknown` „regulärer Neustart erforderlich".
3. `_accessProbe != null` + `ReadAccessProbeAsync` wahr → `Supported` (mit `RestrictedWriteSids`).
4. `_accessProbe == null` + `ReadyStatus == Ready` → `PrepareAccessProbeAsync` → Alive/Ready-Recheck → `MarkInputStarted` → `Session.WritePromptAsync(BuildAccessProbePrompt(_accessProbe))`.
5. Ergebnis sonst `Unknown` „Nachweis des tatsächlichen Dateitool-Zugriffs steht aus; separate lokale Kanalprobe muss erfolgreich abgeschlossen werden."

**Lücke:** In Fall 3 (`ReadAccessProbeAsync` falsch) wird die Probe nie erneut gesendet — unabhängig von Alter, Bereitschaft oder Trust-Dialog. `ReadAccessProbeAsync` wirft `IOException` bei Inhalt-Mismatch → äußerer Catch → `Denied`.

### Eingangs-Koordination

`ScmCoordinatedTerminalSession` (`src/Softwareschmiede/Application/Services/ScmCoordinatedTerminalSession.cs`) feuert `InputStarted` bei `WriteInputAsync`/`WritePromptAsync` → `MarkInputStarted` (verdrahtet in `KiAusfuehrungsService.cs:302`). Der Probe-Prompt wird über `Session.WritePromptAsync` gesendet (UTF-8 + CR in ConPTY, `PseudoConsoleSession.cs:497-501`).
