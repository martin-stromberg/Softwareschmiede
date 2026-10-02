# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TerminalSessionService.cs / TaskDetailView.xaml (Terminal-Anzeige und Statuszeile der Aufgabe)

- **Erreichbarkeit** — Die Anforderung verlangt ausdrücklich, dass fehlende/inkompatible PTY-Abhängigkeiten „zu einer verständlichen Diagnose führen" und der Pipe-Fallback „nicht stillschweigend als gleichwertiger interaktiver Modus" gelten darf. Gebaut ist das so: `TerminalSessionService.WriteDiagnosis` schreibt die Zeile `[Terminal-Diagnose] Pipe-Backend gewählt (PTY nicht verfügbar) | PtyVerfuegbar=False | Checks: ConPTY-Verfügbarkeit=Fehler [...]` ausschließlich in die Protokoll-Senke (`CliOutputProtokollWriter` — der Code-Kommentar sagt explizit „nicht in TerminalReplayBuffer/Terminal-Anzeige"). Für die Anwenderin heißt das: Das Terminal-Fenster startet, zeigt die CLI-Ausgabe und die Statuszeile meldet „Gestartet" — ununterscheidbar von einer voll funktionsfähigen Sitzung, obwohl der eingeschränkte Pipe-Modus aktiv ist (kein echtes Terminal: Vollbild-Ansichten, Farben und interaktive Eingabe können beeinträchtigt sein). Den einzigen Hinweis müsste sie im Tab „Protokoll" zwischen Tausenden von CLI-Ausgabezeilen suchen — und selbst dort ist die Meldung in Entwicklerjargon formuliert („Pipe-Backend", „PtyVerfuegbar=False", englisch-deutsch gemischte Check-Namen). Eine nicht-technische Person kann weder erkennen, dass ein Fallback aktiv ist, noch verstehen, warum die CLI sich anders verhält.

  Empfehlung: Den Fallback-Zustand dort sichtbar machen, wo die Anwenderin hinschaut — z. B. eine verständliche Zeile direkt in der Terminal-Anzeige („Hinweis: Diese Sitzung läuft im eingeschränkten Modus, weil kein Pseudo-Terminal verfügbar ist.") oder einen entsprechenden Hinweis im `CliStatusText` (z. B. „Gestartet (eingeschränkter Modus)"). Die detaillierte Check-Liste kann weiterhin im Protokoll/Log bleiben.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- CLI einer Aufgabe starten / neu starten (Terminal-Session mit Arbeitsverzeichnis und Umgebung) → unauffällig
- Eingabe ins Terminal: Tippen, Enter, Backspace, Ctrl-C, Einfügen aus der Zwischenablage → unauffällig (bestehende Handler in `TerminalControl`/`KeyToVt100Encoder` unverändert erhalten)
- Scrollen im Terminalverlauf, inkl. Vollbild-Ansicht (Alternate Screen ohne Scrollback) → unauffällig
- Fenster-/Terminalgröße ändern (Resize an den Prozess) → unauffällig (automatisch via `OnRenderSizeChanged`)
- Zu einer laufenden Aufgabe zurücknavigieren (Session-Neuanbindung ohne doppelte Ausgaben) → unauffällig (`RebuildBufferFromReplay` vor erneuter Bindung; 512-KB-Replay-Budget reicht für den ohnehin auf 1000 Zeilen begrenzten Scrollback aus)
- Fehler beim CLI-Start verstehen (Executable nicht startbar / Plugin erfordert PTY, das fehlt) → unauffällig (verständliche deutsche `InvalidOperationException`-Meldung erscheint als `FehlerMeldung` in der Aufgabenansicht)
- Erkennen, dass eine laufende Sitzung im eingeschränkten Pipe-Fallback arbeitet → Befund vorhanden (siehe oben)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Controls/TerminalControl.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede.App/App.xaml.cs` (DI-Registrierung, bestimmt Backend-Wahl)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionService.cs` (kein UI-File, bestimmt aber die Sichtbarkeit der Fallback-Diagnose)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionDiagnostics.cs` (Wortlaut der Diagnose-Checks)
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Replay-Verhalten bei UI-Neuanbindung)
- `src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs` (Fallback-Backend)
- `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs` (Alternate Screen / Scrollback)
- `src/Softwareschmiede/appsettings.json` (Terminal-Laufzeitparameter, keine Benutzereinstellung)
