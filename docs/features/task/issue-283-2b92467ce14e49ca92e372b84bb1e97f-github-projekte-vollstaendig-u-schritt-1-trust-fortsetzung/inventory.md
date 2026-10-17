# Bestandsaufnahme: Verlässliche Fortsetzung der CLI-Zustellung nach der Vertrauenswürdigkeitsabfrage

Analysiert wurde der SCM-Zustellungspfad (Dispatch → Eingabebereitschaft → Kanalnachweis → Prompt) sowie die CLI-Startkonfiguration, bezogen auf die übersetzte Anforderung in `requirement.md`. Die Voranalyse unter `docs/projects/task/issue-283-…/inventory/` (insb. `trust-deadlock.md`, `cli-delivery.md`) wurde übernommen und anhand des Codes verifiziert.

## Zusammenfassung

- **Belegter Deadlock:** `_accessProbe` in `ScmPromptSessionAdapter` wird genau einmal erzeugt/gesendet (`CheckExchangeAccessCoreAsync`, `ScmPromptSessionAdapter.cs:159-168`) und **nie** zurückgesetzt oder erneut gesendet. Geht der Probe-Prompt im Vertrauensdialog verloren, liefert jede weitere Prüfung denselben `Unknown`-Grund „Nachweis des tatsächlichen Dateitool-Zugriffs steht aus …" (Z. 169) — ohne Timeout, ohne UI-Auflösungspfad.
- **`ConfirmReady` ist während des Dialogs erlaubt:** Einzige Bedingung `_recognizedCli && _ready != Busy` (Z. 181-192); es gibt keine Prüfung, ob tatsächlich ein Eingabeprompt sichtbar ist. Damit kann die Erzwingung den verlustbehafteten Probe-Versand auslösen.
- **Kein eigener Trust-Wartezustand:** `ScmPromptReadyStatus` kennt nur `Unknown/Ready/Busy/Exited` (Z. 9-19); der Dialog fällt unter „Eingabebereitschaft unbekannt" (`ScmEventOrchestrator.cs:164-169`) und bietet `CanConfirmReady` an (`ScmEventQueueViewModel.cs:295` — Reason-Substring).
- **`long.MaxValue`-Sperre:** `InvalidOperationException`/`IOException`/`UnauthorizedAccessException` setzen `NextAttemptUtc = long.MaxValue` (`ScmEventOrchestrator.cs:209-214`) → kein Auto-Retry. `RetryAsync` (Z. 235-244) und `OpenFromQueueAsync` (Z. 58) heben die Sperre auf; **`ConfirmReadyAsync` hebt sie nicht auf** (Z. 247-259) — die Bestätigung läuft in den `NextAttemptUtc`-Return in Z. 114 und ist damit für gesperrte Ereignisse wirkungslos.
- **Auto-Dispatch existiert:** `Wartend`-Einträge mit `NextAttemptUtc == null` werden vom 60-s-Poll automatisch erneut dispatchet (`ScmEventMonitoringService.cs:121-124`) — der Fortsetzungsmechanismus ist vorhanden, scheitert nur am hängenden Probe-Zustand.
- **Prüfauftrag verifiziert (externe Quellen):** Claude Code kennt **keinen** Trust-CLI-Parameter; dokumentierter Mechanismus ist `projects["<dir>"].hasTrustDialogAccepted = true` in `~/.claude.json` (von Claude-Code-Fehlermeldung selbst empfohlen). Codex (v0.113+/v0.114) zeigt ebenfalls einen blockierenden Trust-Dialog; dokumentierter Mechanismus ist `[projects."<dir>"] trust_level = "trusted"` in `~/.codex/config.toml`. `--dangerously-skip-permissions`/`--yolo` scheiden aus (unterlaufen den Sicherheitsvertrag; Codex-yolo zeigt den Dialog seit v0.113 ohnehin wieder).
- **Evaluator-Lücke für Codex-Trust-Konfiguration:** `ScmCliAccessEvaluator` akzeptiert in `config.toml` nur die Abschnitte `[sandbox_workspace_write]` und `[features]` (`ScmCliAccessEvaluator.cs:113-114`); ein `[projects.*]`-Abschnitt (Trust-Vorbelegung oder bereits vom Nutzer vorhanden) führt zu `Unknown` → Kanal tot.
- **Probe erfordert Git-Worktree + private ACLs:** `PrepareAccessProbeAsync` → `PrepareRunDirectoryAsync` → `EnsureGitExcludedAsync` ruft `git rev-parse --show-toplevel` u. a. auf (`ScmEventExchangeProtocol.cs:167-193, 396-414`); `CreatePrivateDirectory`/`VerifyPrivateDirectory` erzwingen Owner-ACLs. Tests benötigen ein `git init`-isiertes Arbeitsverzeichnis.
- **Einfügepunkt Trust-Vorbelegung:** `KiAusfuehrungsService.StartTerminalSessionCoreAsync` — `spec` steht ab Z. 238, `ScmCliAccessEvaluator.Capture` folgt in Z. 240; `_timeProvider` ist bereits injiziert (Z. 34, 50, 57).

## Details

- [Adapter: Eingabebereitschaft und Kanalprobe](inventory/adapter.md)
- [Orchestrator: Dispatch-Pipeline und Wartegründe](inventory/orchestrator.md)
- [CLI-Startparameter und Trust-Mechanismen (Prüfauftrag)](inventory/trust-start.md)
- [Tests und Infrastruktur](inventory/tests.md)
