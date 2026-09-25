# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- **CLI für eine Aufgabe starten** (Schaltfläche „Start", bei Bedarf Plugin-Auswahl über den vorhandenen Dialog mit Klartext-Namen) → unauffällig; keine interne Kennung nötig, Arbeitsverzeichnis/Umgebung werden automatisch aus Aufgabe und Plugin-Spec übernommen.
- **Interaktive Eingabe im eingebetteten Terminal** (Tastatur, Enter, Backspace, Ctrl-C, Paste) → unauffällig; Bedienung in `TerminalControl` unverändert, Eingaben laufen jetzt über `ITerminalSession.WriteInputAsync` — für die Anwenderin ohne spürbaren Unterschied.
- **Terminal-Ausgabe lesen** (Fortschrittsanzeigen mit `\r`, ANSI-Farben, Cursorbewegungen, mehrzeilige Ausgabe, Alternate Screen) → unauffällig; im Vollbild-/Alternate-Screen-Modus wird der Scrollback korrekt deaktiviert (kein verwirrendes Scrollen in einen leeren Bereich).
- **Fenster-/Terminalgröße ändern** → unauffällig; `OnRenderSizeChanged` → `session.Resize` bleibt verdrahtet, reine Beobachtungsaufgabe.
- **Zu einer laufenden Aufgabe zurücknavigieren** (Session-Neuanbindung) → unauffällig; `RebuildBufferFromReplay` stellt den Bildschirminhalt wieder her, ohne dass Ausgaben doppelt erscheinen — Verhalten für die Anwenderin identisch zum bisherigen „Inhalt bleibt erhalten".
- **Erkennen, dass die CLI im eingeschränkten Modus (Pipe-Fallback) läuft** — Kernbefund aus Runde 1 → unauffällig: Die Statusleiste der Aufgabe (`CliStatusText`, `TaskDetailView.xaml:661-667`) zeigt dauerhaft den Suffix „ (eingeschränkter Modus – kein Pseudo-Terminal)" — im Zustand „Gestartet" (`TaskDetailViewModel.cs:1714`) ebenso wie in allen Laufzeitstatus-Texten „Ausführung läuft" / „Wartet auf Eingabe" / „Inaktiv" (`UpdateCliStatusText`, `:1985`), auch nach Weg-und-Wieder-Navigation (`AttachCliStatusSession` bei `LadenAsync`). Zusätzlich landet eine `[Terminal-Diagnose]`-Zeile mit den Preflight-Ergebnissen im sichtbaren Aufgabenprotokoll (`TerminalSessionService.WriteDiagnosis`). Der Fallback gilt damit an keiner Stelle mehr stillschweigend als gleichwertiger interaktiver Modus.
- **Verstehen, warum eine CLI nicht startet** (fehlende/inkompatible PTY-Abhängigkeit, nicht startbare Executable, Plugin mit `RequiresPty` ohne verfügbares PTY) → unauffällig; die geworfene `InvalidOperationException` trägt einen verständlichen deutschen Text (z. B. „Die CLI '…' erfordert ein Pseudo-Terminal (PTY), das auf diesem System nicht verfügbar ist.") und erscheint im roten Fehlerbanner der Aufgabenseite („Aufgabe konnte nicht gestartet werden: …", `TaskDetailViewModel.cs:1820` / `TaskDetailView.xaml:286-289`).

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `src/Softwareschmiede.App/Controls/TerminalControl.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` (unverändert; nur Kontext: `CliStatusText`-Bindung in der Statusleiste, Protokoll-Liste, Fehlerbanner)

Kontext-Dateien (nicht UI, zur Verifikation der Sichtbarkeits-Kette herangezogen):

- `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs` (`IsPseudoTerminal`-Contract)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionService.cs` (Backend-Wahl, `WriteDiagnosis`)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionDiagnostics.cs` (Preflight-Checks)
- `src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs` (Pipe-Fallback meldet `IsPseudoTerminal = false`)
- `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs` (PTY-Pfad meldet `IsPseudoTerminal = true`)
- `src/Softwareschmiede.Tests/E2E/E2E_TerminalFallbackDiagnose.cs` (Kontext: beabsichtigtes anwendersichtbares Verhalten)
