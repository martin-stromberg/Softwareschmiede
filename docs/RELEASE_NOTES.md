# Release Notes

## Important Notes Before Update

- AI CLIs that require a pseudo terminal (Claude CLI, Codex CLI, GitHub Copilot CLI, Devin CLI) now fail with an explicit error message if no ConPTY is available, instead of silently running in degraded pipe mode.
- New `Terminal` section in `appsettings.json` (`ReplayBufferByteBudget`, `DefaultCols`, `DefaultRows`) controls the replay buffer size and the default terminal size.

## What's New

- Terminal integration reworked: AI CLIs are started directly through a shared terminal session service — no cmd.exe wrapper and no delayed keystroke injection anymore; bare command names are resolved via PATHEXT and `.cmd`/`.bat` shims (e.g. npm installations) are wrapped automatically.
- Preflight diagnostics before each CLI start (pseudo-terminal availability, executable resolution, CLI health, encoding, terminal size, plugin parameters) with results in the task protocol.
- CLIs running on the pipe fallback are now visibly flagged: the status line shows "(eingeschränkter Modus – kein Pseudo-Terminal)" and the task protocol contains a `[Terminal-Diagnose]` entry with the check results.
- Improved terminal rendering: alternate screen for full-screen programs, scroll regions, insert/delete-line operations and UTF-8 characters spanning output chunks.
- Reopening a task page restores the terminal content from a bounded replay buffer (512 KiB by default).

## Wichtige Hinweise vor dem Update

- KI-CLIs, die zwingend ein Pseudo-Terminal benötigen (Claude CLI, Codex CLI, GitHub Copilot CLI, Devin CLI), schlagen jetzt mit einer verständlichen Fehlermeldung fehl, wenn kein ConPTY verfügbar ist, statt still im eingeschränkten Pipe-Modus zu laufen.
- Neue `Terminal`-Sektion in der `appsettings.json` (`ReplayBufferByteBudget`, `DefaultCols`, `DefaultRows`) steuert die Größe des Replay-Puffers und die Standard-Terminalgröße.

## Neuerungen

- Terminalintegration überarbeitet: KI-CLIs werden direkt über einen gemeinsamen Terminal-Session-Dienst gestartet — ohne cmd.exe-Hülle und ohne verzögerte Tastatur-Injektion; nackte Befehlsnamen werden per PATHEXT aufgelöst und `.cmd`/`.bat`-Shims (z. B. npm-Installationen) automatisch verpackt.
- Preflight-Diagnose vor jedem CLI-Start (Pseudo-Terminal-Verfügbarkeit, Executable-Auflösung, CLI-Health, Encoding, Terminalgröße, Pluginparameter) mit Ergebnissen im Aufgabenprotokoll.
- CLIs auf dem Pipe-Fallback werden jetzt sichtbar gekennzeichnet: Die Statuszeile zeigt „(eingeschränkter Modus – kein Pseudo-Terminal)" und das Aufgabenprotokoll enthält einen `[Terminal-Diagnose]`-Eintrag mit den Prüfergebnissen.
- Verbessertes Terminal-Rendering: Alternate Screen für Vollbild-Programme, Scroll-Regionen, Insert-/Delete-Line-Operationen und UTF-8-Zeichen über Chunk-Grenzen hinweg.
- Beim erneuten Öffnen einer Aufgabenseite wird der Terminalinhalt aus einem begrenzten Replay-Puffer wiederhergestellt (standardmäßig 512 KiB).
