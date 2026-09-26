# Release Notes

## Important Notes Before Update

- AI CLIs that require a pseudo terminal (Claude CLI, Codex CLI, GitHub Copilot CLI, Devin CLI) now fail with an explicit error message if no ConPTY is available, instead of silently running in degraded pipe mode.
- New `Terminal` section in `appsettings.json` (`ReplayBufferByteBudget`, `DefaultCols`, `DefaultRows`, `AufzeichnungByteBudget`) controls the replay buffer size, the default terminal size and the byte budget of the automatic CLI recording (8 MB by default; values <= 0 disable the recording).

## What's New

- New diagnostic tool "console test window" (Konsolentestfenster): plays an exported CLI recording (.clireplay) time-controlled through the real terminal render path — opened non-modally via Settings → General → Diagnostics.
- CLI sessions are now recorded automatically: raw output bytes with a timestamp per chunk (budget-limited, the last 8 sessions are kept) and can be exported as a .clireplay file via the new "Export recording" button in the task view.
- The console test window shows the source chunks with visible control sequences next to the rendered output, synchronized to the playback position; playback supports pause/resume, restart and a time-lapse threshold that shortens long idle gaps.
- Fixed: duplicate terminal output when the buffer was rebuilt while live output was being processed (rebuild race).
- Fixed: the ANSI parser swallowed or corrupted bytes when ESC occurred inside an incomplete CSI/OSC sequence; DCS/SOS/PM/APC string sequences are now skipped instead of being printed as text.
- Fixed: CLI sessions that exited immediately with a non-zero code were shown as "stopped" — they are now reported as errors including the exit code in the task protocol.
- Terminal integration reworked: AI CLIs are started directly through a shared terminal session service — no cmd.exe wrapper and no delayed keystroke injection anymore; bare command names are resolved via PATHEXT and `.cmd`/`.bat` shims (e.g. npm installations) are wrapped automatically.
- Preflight diagnostics before each CLI start (pseudo-terminal availability, executable resolution, CLI health, encoding, terminal size, plugin parameters) with results in the task protocol.
- CLIs running on the pipe fallback are now visibly flagged: the status line shows "(eingeschränkter Modus – kein Pseudo-Terminal)" and the task protocol contains a `[Terminal-Diagnose]` entry with the check results.
- Improved terminal rendering: alternate screen for full-screen programs, scroll regions, insert/delete-line operations and UTF-8 characters spanning output chunks.
- Reopening a task page restores the terminal content from a bounded replay buffer (512 KiB by default).

## Wichtige Hinweise vor dem Update

- KI-CLIs, die zwingend ein Pseudo-Terminal benötigen (Claude CLI, Codex CLI, GitHub Copilot CLI, Devin CLI), schlagen jetzt mit einer verständlichen Fehlermeldung fehl, wenn kein ConPTY verfügbar ist, statt still im eingeschränkten Pipe-Modus zu laufen.
- Neue `Terminal`-Sektion in der `appsettings.json` (`ReplayBufferByteBudget`, `DefaultCols`, `DefaultRows`, `AufzeichnungByteBudget`) steuert die Größe des Replay-Puffers, die Standard-Terminalgröße und das Byte-Budget der automatischen CLI-Aufzeichnung (standardmäßig 8 MB; Werte <= 0 deaktivieren den Mitschnitt).

## Neuerungen

- Neues Diagnose-Werkzeug „Konsolentestfenster": spielt eine exportierte CLI-Aufzeichnung (.clireplay) zeitgesteuert durch den echten Terminal-Renderpfad ab — nicht-modal über Einstellungen → Allgemein → Diagnose erreichbar.
- CLI-Sitzungen werden jetzt automatisch aufgezeichnet: rohe Ausgabe-Bytes mit Zeitstempel pro Chunk (budgetbegrenzt, die letzten 8 Sitzungen werden vorgehalten) und lassen sich über die neue Schaltfläche „Aufzeichnung exportieren" in der Aufgabenansicht als .clireplay-Datei exportieren.
- Das Konsolentestfenster zeigt die Quell-Chunks mit sichtbar gemachten Steuersequenzen neben der gerenderten Ausgabe, synchron zur Wiedergabeposition; die Wiedergabe unterstützt Pause/Fortsetzen, Neu starten und eine Zeitraffer-Schwelle, die lange Leerzeiten verkürzt.
- Behoben: doppelte Terminalausgabe, wenn der Puffer neu aufgebaut wurde, während Live-Ausgabe verarbeitet wurde (Race-Bedingung beim Neuaufbau).
- Behoben: Der ANSI-Parser verschluckte oder verstümmelte Bytes, wenn ein ESC mitten in einer unvollständigen CSI-/OSC-Sequenz auftrat; DCS-/SOS-/PM-/APC-String-Sequenzen werden jetzt übersprungen statt als Text ausgegeben.
- Behoben: CLI-Sitzungen, die sofort mit einem Exit-Code ungleich 0 endeten, wurden als „gestoppt" angezeigt — sie werden jetzt als Fehler inklusive Exit-Code im Aufgabenprotokoll gemeldet.
- Terminalintegration überarbeitet: KI-CLIs werden direkt über einen gemeinsamen Terminal-Session-Dienst gestartet — ohne cmd.exe-Hülle und ohne verzögerte Tastatur-Injektion; nackte Befehlsnamen werden per PATHEXT aufgelöst und `.cmd`/`.bat`-Shims (z. B. npm-Installationen) automatisch verpackt.
- Preflight-Diagnose vor jedem CLI-Start (Pseudo-Terminal-Verfügbarkeit, Executable-Auflösung, CLI-Health, Encoding, Terminalgröße, Pluginparameter) mit Ergebnissen im Aufgabenprotokoll.
- CLIs auf dem Pipe-Fallback werden jetzt sichtbar gekennzeichnet: Die Statuszeile zeigt „(eingeschränkter Modus – kein Pseudo-Terminal)" und das Aufgabenprotokoll enthält einen `[Terminal-Diagnose]`-Eintrag mit den Prüfergebnissen.
- Verbessertes Terminal-Rendering: Alternate Screen für Vollbild-Programme, Scroll-Regionen, Insert-/Delete-Line-Operationen und UTF-8-Zeichen über Chunk-Grenzen hinweg.
- Beim erneuten Öffnen einer Aufgabenseite wird der Terminalinhalt aus einem begrenzten Replay-Puffer wiederhergestellt (standardmäßig 512 KiB).
