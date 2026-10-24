# Logikklassen und Mechanismen

Methodenbestand der von der Anforderung ([`requirement.md`](../requirement.md)) betroffenen Klassen. Quellpfade relativ zum Repo-Root.

## `ScmPromptSessionAdapter` — `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`

`public sealed class ScmPromptSessionAdapter : IScmPromptSessionAdapter, IDisposable` (Zeilen 54–342)

### Zustandsfelder (relevante)

| Feld | Zeile | Bedeutung heute |
|---|---|---|
| `_recognizedCli` | 81 | `bool` — einziger Erkennungszustand; unterscheidet **nicht** zwischen „unbekannt", „bekannte CLI/andere Version" und „unterstützt"; die erkannte Versionszeichenfolge wird nirgends gemerkt |
| `_ready` | 80 | Letzter `ScmPromptReadyStatus` |
| `_manuallyConfirmed` | 82 | Nach `ConfirmReady` bis zur nächsten Eingabe (`MarkInputStarted`) |
| `_startupOutput` | 74 | `StringBuilder`, Rohausgabe bis max. 32 KiB (Zeile 267) — Erkennungsfenster |
| `_access` | 69 | Startvertrag-Ergebnis aus `ScmCliAccessEvaluator.Evaluate` (Zeile 99) |
| `_accessProbe`, `_accessProbeSentUtc`, `_accessProbeSends` | 76-78 | Kanalproben-Zustand; Limit `AccessProbeMaxSends = 3` (59), Resend `AccessProbeResendDelay = 60 s` (57) |
| `TrustDialogMarkers` | 61-67 | Vier gepinnte Trust-Dialog-Texte (EN, lowercase) |
| `_inputRevision`/`_outputRevision` | 83-84 | Erkennen neuer Ausgaben; `_inputRevision == _outputRevision` verhindert Re-Bewertung |

### Methoden

| Methode | Zeilen | Verhalten heute | Anforderungsbezug |
|---|---|---|---|
| Konstruktor | 87-105 | Evaluiert Startvertrag, abonniert `OutputChunk`, `BufferChanged`, `Exited`, `Failed`, ruft `RefreshScreen` | — |
| `WaitForInputReadyAsync` | 130-140 | `RefreshScreen` + Signalwarten (2 s Poll) | — |
| `CheckExchangeAccessAsync` | 143-144 | Seriell über `ScmTaskCoordinator` an `CheckExchangeAccessCoreAsync` delegiert | — |
| `CheckExchangeAccessCoreAsync` | 146-211 | Kette: nicht lebend → „CLI-Sitzung hat sich geändert…"; Konfig geändert → „…regulärer Neustart erforderlich."; Kanal außerhalb Schreibwurzel → `Denied`; **`!_recognizedCli && Supported` → `Unknown` + „CLI-Version oder Initialisierung ist nicht durch ein unterstütztes Promptformat bestätigt." (162-163)**; sonst Kanalprobe vorbereiten/senden/nachlesen | Reason-Text soll erkannte Version/Erkennungszustand konkret benennen |
| `SendAccessProbeAsync` | 214-221 | `MarkInputStarted` + `BuildAccessProbePrompt`, Zähler/Zeitstempel | Wird bei `!_recognizedCli` nie erreicht — Symptom der Anforderung |
| `ConfirmReady` | 224-237 | `false` bei falscher RunId/tot; **`!_recognizedCli` oder `Busy`/`TrustPrompt` → harte Ablehnung (232)**; sonst `_manuallyConfirmed = true`, `Ready` | Offene Frage: Bestätigung bei erkannter, aber außerhalb des Bereichs liegender Version |
| `MarkInputStarted` | 240-248 | Verwirft manuelle Bestätigung, setzt `Busy` | — |
| `WritePromptAsync` | 251-261 | Steuerzeichen-Prüfung, `CheckExchangeAccessAsync` muss `Supported` liefern, `ReadyStatus == Ready`, dann `MarkInputStarted` + `Session.WritePromptAsync` | — |
| `OnOutput` | 263-270 | `_startupOutput` bis 32 KiB, dann `RecognizeStartup` | Erkennungsfenster (Offene Frage 5) |
| **`RecognizeStartup`** | **272-281** | Vier exakte `Contains`-Prüfungen: Codex `OpenAI Codex (v0.114.0)`, Claude `Claude Code v2.1.0`, Devin `Devin CLI` **und** `v3000.11.92`, Simulator `SCM_SIMULATOR_PROTOCOL_V1` (nur bei `_access.Status ∈ {Supported, Denied}`). Kommentar: „Supported transcript versions are deliberately explicit. New/changed formats require a fixture." | Zentraler Eingriffspunkt: exakte Strings → Versionsbereiche + Versionsextraktion |
| `RefreshScreen` | 283-319 | Rendert `Session.Buffer`, ruft `RecognizeStartup(screen)`, **`if (!_recognizedCli) return;` (291)** → `_ready` bleibt `Unknown`; Trust-Dialog → `TrustPrompt`; Simulator: `SCM_READY_V1`/`SCM_BUSY_V1` auf Cursorzeile; Busy-Marker (`esc to interrupt` u. a.) → `Busy`; Prompt-Glyphen (`›`, `❯`, `❭`/„❭ ") → `Ready`; sonst `Unknown` | Zeile 291 muss differenzierte Erkennungszustände abbilden |
| `SetReady` | 321-328 | Zustandswechsel + Signalschleife | — |
| `OnBufferChanged`/`OnExited`/`OnFailed` | 330-332 | `_outputRevision++` + `RefreshScreen`; `Exited` | — |
| `Dispose` | 335-341 | Deabonniert alle Session-Events | — |

### Erkannte Promptformate heute (gepinnt)

| PluginPrefix | Banner-/Versionsbedingung | Ready-Prompt | Busy-/Blocker-Erkennung |
|---|---|---|---|
| `Softwareschmiede.Codex` | `OpenAI Codex (v0.114.0)` | `›`, `› Implement {feature}`, `› Explain this codebase` | `esc to interrupt` u. a. + `TrustDialogMarkers` |
| `Softwareschmiede.ClaudeCli` | `Claude Code v2.1.0` | `❯` | dto. |
| `Softwareschmiede.Devin` | `Devin CLI` **und** `v3000.11.92` | `❭` bzw. `❭ <text>` | dto. |
| `Softwareschmiede.KiSimulator` | `SCM_SIMULATOR_PROTOCOL_V1` + `_access.Status ∈ {Supported, Denied}` | `SCM_READY_V1` (nur Cursorzeile) | `SCM_BUSY_V1` |

Unbekannte `PluginPrefix`-Werte und abweichende Versionen setzen `_recognizedCli` niemals — `ReadyStatus` bleibt `Unknown`, die Kanalprobe wird nie gesendet, der Dispatch landet bei „Eingabebereitschaft unbekannt…" (Orchestrator) bzw. „CLI-Version oder Initialisierung ist nicht durch ein unterstütztes Promptformat bestätigt." (Access-Prüfung).

## `ScmEventOrchestrator` — `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Zeilen | Anforderungsbezug |
|---|---|---|
| `DispatchAsync` | 23-28 | Einstieg, delegiert an `DispatchCoreAsync` unter Aufgabenlease |
| `OpenFromQueueAsync` | 31-72 | Queue-Öffnen mit Plugin-Auswahl; leitet danach ebenfalls an `DispatchCoreAsync` |
| **`DispatchCoreAsync`** | 74-244 | **Zeilen 173-181: nach `WaitForInputReadyAsync` ≠ `Ready` wird der Grund gewählt — `TrustPrompt` → Trust-Text; `adapter.IsBusy` → „CLI beschäftigt; wartet auf Eingabebereitschaft."; sonst → „Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen." (178). Kein Zweig für „CLI nicht erkannt/unbekannte Version".** Zeilen 183-185: `access.Status != Supported` → „Antwortkanal für diese CLI nicht verfügbar: " + `access.Reason` (hier käme der Adapter-Reason 162-163 nur zum Tragen, wenn `ready == Ready` war — bei unerkannte CLI ist `ready` aber nie `Ready`, sodass der Adapter-Reason für den Versionsfall im Queue-Grund praktisch nie sichtbar wird; stattdessen greift die Zeile-178-Pauschale) |
| `ActivateForOpenAsync` | 247-253 | — |
| `RetryAsync` | 256-266 | Hebt `NextAttemptUtc`-Sperren auf |
| **`ConfirmReadyAsync`** | 269-285 | Prüft lebende Session + angezeigte RunId; `session.ConfirmReady` false → `InvalidOperationException` „CLI ist beschäftigt oder kann noch nicht bestätigt werden. Bitte erneut prüfen." (279); setzt `NextAttemptUtc = null`, `ReadyConfirmedRunId = null` und fährt mit Dispatch fort | Fehlertext an neue Semantik anpassen |
| `ResendAsync` | 288-302 | — |
| `CancelAsync` | 308-318 | — |
| `ReconcileAttemptAsync` | 349-430 | Versuchsabgleich; holt `session.CliRunId`-Vergleich (421-426) |
| `WaitAsync`/`WaitWithCommentAsync` | 461-475 | `Status = "Wartend"`, `Reason = …`; `WaitWithCommentAsync` legt zusätzlich eine Remote-`waiting`-Kommentar-Aktion an — der neue Grundtext würde damit auch an die SCM-Referenz gehen |
| `BlockReason` | 457-459 | Beendet/archiviert/pausiert → Sperrgründe |

## `ScmCliAccessEvaluator` — `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs`

`public static class` (35-318). Startvertrag-Auswertung; **von der Bereichslogik explizit unberührt** (Anforderung: „bleibt unverändert").

| Methode | Zeilen | Inhalt |
|---|---|---|
| `Capture` | 38-72 | Sammelt bekannte Konfigurationspfade je Prefix (Codex: `config.toml`/`requirements.toml`/`managed_config.toml` + `.codex`-Kette; Claude: `settings.json`, `settings.local.json`, managed; Devin: `.devin/config.json` unter Profil/XDG/Workdir) |
| `IsUnchanged` | 75-79 | Bytegenauer Vergleich gegen erfasste Inhalte |
| `Evaluate` | 82-284 | Liefert `ScmExchangeAccess`; für Codex/Claude/Devin Whitelisting von Parametern/Modi (`workspace-write`, `acceptEdits`, `accept-edits`/`dangerous`/`bypass`, `DEVIN_PERMISSION_MODE`-Fallback); alle anderen Prefixe → `Unknown` „Für diese CLI ist kein Antwortkanal-Adapter verfügbar." (103) |
| `ReadConfiguration`/`Tokenize` | 286-317 | Hilfsfunktionen |

## `ScmEventExchangeProtocol` (Kanalprobe) — `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs`

| Methode | Zeilen | Inhalt |
|---|---|---|
| `DirectoryName` | 83 | `.softwareschmiede-scm-exchange` |
| `PrepareAccessProbeAsync` | 96-113 | Legt rungebundenes, ACL-gesichertes Probe-Verzeichnis + `challenge.json` an |
| `ReadAccessProbeAsync` | 116-130 | Nachweis: challenge gelesen (umbenannt), proof geschrieben, Bytes identisch |
| `BuildAccessProbePrompt` | 133-140 | JSON-Prompt **ohne** Ereignisinhalte (`instruction`, `challengePath`, `temporaryProofPath`, `proofPath`, `readChallengePath`) |
| `EnsureSafePath` | (372 ff.) | Pfadhygiene |

Laut Anforderung unverändert; die Probe ist das bisher nie erreichte Ziel bei Versionsabweichung.

## `ScmCliWorkspaceTrust` — `src/Softwareschmiede/Application/Services/ScmCliWorkspaceTrust.cs`

`public static class` (18-140): `EnsureTrusted` (21-52) belegt herstellerseitige Trust-Entscheidungen vor (Claude: `~/.claude.json` `projects.<dir>.hasTrustDialogAccepted`; Codex: `[projects."<dir>"] trust_level` in `config.toml`); Hilfen `GitTopLevel`, `HasProjectConfiguration`, `EnsureClaudeTrusted`, `EnsureCodexTrusted`, `WriteAtomically`. Laut Anforderung unberührt; Fehler beim Vorbelegen blockieren den CLI-Start nicht (`KiAusfuehrungsService.cs:241-247`), ein trotzdem erscheinender Dialog wird vom Adapter als `TrustPrompt` erkannt.

## `KiAusfuehrungsService` — `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

- Zeilen 248-254: `ScmCliAccessEvaluator.Capture` beim CLI-Start (Fehler → Dummy-Datei „unavailable-managed-configuration").
- **Zeilen 309-311: Erzeugung des `ScmPromptSessionAdapter` mit `spec`, `scmConfiguration`, `_isCurrent`-Delegate; `session.InputStarted = adapter.MarkInputStarted`.**
- Zeile 410: `GetScmPromptSession(aufgabeId)` liefert den `IScmPromptSessionAdapter?` für Orchestrator/ViewModel; `CliProcessHandle.ScmPromptSession` (Zeile 789) hält die Instanz.

## `ScmEventQueueViewModel` — `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`

| Element | Zeilen | Anforderungsbezug |
|---|---|---|
| `ConfirmReadyCommand` | 63-64 | Ruft `ScmEventOrchestrator.ConfirmReadyAsync(item.Id, item.CliRunId!…)`; CanExecute an `ScmEventQueueItem.CanConfirmReady` |
| `OpenAsync` | 239-266 | Plugin-Auswahl-/Bestätigungsdialog |
| `ScmEventQueueItem.CanConfirmReady` | 333 | Textabgleich auf „Eingabebereitschaft unbekannt" → Button `ScmEventConfirmReady` (`ScmEventQueueView.xaml:117-118`) |
| `ScmEventQueueItem.From` | 335-348 | Reason-Mapping; blockierte Aufgaben überschreiben den angezeigten Grund |

## Mechanismen-Kette (Ist-Zustand)

1. `KiAusfuehrungsService` startet CLI → Adapter abonniert `OutputChunk`/`BufferChanged`.
2. `OnOutput` (Rohbytes ≤ 32 KiB) und `RefreshScreen` (Bildschirmpuffer) rufen `RecognizeStartup` — beide Pfade prüfen dieselben gepinnten Strings.
3. `RecognizeStartup == false` → `RefreshScreen` bricht ab → `ReadyStatus` bleibt `Unknown` → `WaitForInputReadyAsync` liefert `Unknown` → Orchestrator-Zeile 178 Pauschalgrund → `CanConfirmReady` zeigt Button → `ConfirmReady` lehnt wegen `!_recognizedCli` trotzdem ab (Schleife ohne ehrliche Ursache).
