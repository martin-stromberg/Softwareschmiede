# Release Notes

## Important Notes Before Update

- AI CLIs that require a pseudo terminal (Claude CLI, Codex CLI, GitHub Copilot CLI, Devin CLI) now fail with an explicit error message if no ConPTY is available, instead of silently running in degraded pipe mode.
- New `Terminal` section in `appsettings.json` (`ReplayBufferByteBudget`, `DefaultCols`, `DefaultRows`, `AufzeichnungByteBudget`) controls the replay buffer size, the default terminal size and the byte budget of the automatic CLI recording (8 MB by default; values <= 0 disable the recording).

## What's New

- Terminal output can now be selected with the mouse or keyboard (Shift+arrow keys, Home/End) and copied with the context menu or Ctrl+Shift+C. Ctrl+C continues to go to the active CLI. Ordinary output outside the selection keeps it selected; selection behavior across scrollback movement and full live/replay E2E verification remain open follow-up work.
- The console test window now plays `.clireplay` recordings back in the terminal geometry stored in the recording header (Cols×Rows) instead of the window size — fixed: line-wrap and offset artifacts in recordings wider than the window, where absolute cursor positioning landed in the wrong places.
- Recordings wider than the window are reachable via a horizontal scrollbar (clip/scroll instead of wrap); live sessions in the task view keep resizing to the window as before.
- Keyboard navigation in the replay: arrow keys, Page Up/Down and Home/End scroll the replay view instead of being encoded as input bytes that went nowhere.
- The console test window's toolbar shows the geometry of the loaded recording (e.g. "Recording: 220×50").
- Single-step playback in the console test window: the new "Step back"/"Step forward" buttons apply a loaded recording chunk by chunk, independent of the recorded timestamps; "Step back" deterministically restores the rendered state before the last applied chunk.
- Stepping and playback interact seamlessly: resuming or starting playback continues at the position changed by stepping, and "Restart" is now also available from pure step mode as a direct way back to the beginning.
- The console test window's source list is consistently synchronized with the playback position: 1-based chunk numbering matching the "Chunk n/y" display, with the last applied chunk highlighted.
- Fixed: in the console test window the toolbar could cut off the position display and the "Close" button at the default window width — the toolbar now wraps.
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

- Terminalausgaben lassen sich jetzt per Maus oder Tastatur (Umschalt+Pfeiltasten, Pos1/Ende) markieren und über das Kontextmenü oder Ctrl+Shift+C kopieren. Ctrl+C wird weiterhin an die aktive CLI weitergegeben. Normale Ausgabe außerhalb der Auswahl hebt diese nicht auf; das Verhalten bei Scrollback-Verschiebungen und die vollständige Live-/Replay-E2E-Abnahme sind noch offene Nacharbeiten.
- Das Konsolentestfenster spielt `.clireplay`-Aufzeichnungen jetzt in der im Aufzeichnungs-Header gespeicherten Terminal-Geometrie (Cols×Rows) ab statt in der Fenstergröße — behoben: Zeilenumbruch- und Versatz-Artefakte bei Aufzeichnungen, die breiter als das Fenster sind, bei denen absolute Cursorpositionierungen an falschen Stellen landeten.
- Aufzeichnungen, die breiter als das Fenster sind, sind über eine horizontale Scrollbar erreichbar (abschneiden/scrollen statt umbrechen); Live-Sitzungen in der Aufgabenansicht passen sich weiterhin an die Fenstergröße an.
- Tastatur-Navigation in der Wiedergabe: Pfeiltasten, Bild auf/ab und Pos1/Ende scrollen die Wiedergabe-Ansicht, statt als Eingabe-Bytes kodiert ins Leere zu laufen.
- Die Werkzeugleiste des Konsolentestfensters zeigt die Geometrie der geladenen Aufzeichnung (z. B. „Aufzeichnung: 220×50").
- Einzelschritt-Wiedergabe im Konsolentestfenster: Die neuen Schaltflächen „Schritt zurück"/„Schritt vor" spielen eine geladene Aufzeichnung Chunk für Chunk ab, unabhängig von den aufgezeichneten Zeitstempeln; „Schritt zurück" stellt den gerenderten Zustand vor dem zuletzt angewendeten Chunk deterministisch wieder her.
- Schrittmodus und Wiedergabe greifen nahtlos ineinander: Fortsetzen oder Starten der Wiedergabe läuft an der durch Schritte veränderten Position weiter, und „Neu starten" ist auch aus dem reinen Schrittmodus als direkter Rückweg zum Anfang verfügbar.
- Die Quell-Liste des Konsolentestfensters ist jetzt konsistent zur Wiedergabeposition synchronisiert: 1-basierte Chunk-Nummerierung passend zur Anzeige „Chunk n/y", der zuletzt angewendete Chunk ist markiert.
- Behoben: Im Konsolentestfenster konnte die Werkzeugleiste bei der Standard-Fensterbreite die Positionsanzeige und die Schaltfläche „Schließen" abschneiden — die Leiste bricht jetzt um.
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
