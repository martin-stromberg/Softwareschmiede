# Usability-Review

## Ergebnis

**Status:** Keine Befunde

Nacharbeit-Lauf (`continue.md`): Beide Befunde aus `review-usability.3.md` wurden am geänderten
Code verifiziert als behoben:

- **Pfad-Überlauf in der Werkzeugleiste** — `KonsolenTestDialog.xaml` Z. 29–38: Der Pfad-`TextBlock`
  hat jetzt `MaxWidth="320"` + `TextTrimming="CharacterEllipsis"` sowie `ToolTip` und
  `AutomationProperties.HelpText` auf den vollständigen `DateiPfad`. Lange Exportpfade werden
  gekürzt angezeigt (Dateiname-Anfang bleibt sichtbar), der volle Pfad ist per Tooltip abrufbar;
  Zeitraffer-Eingabe, Status-/Positionsanzeige und „Schließen" bleiben bei der
  Standard-Fensterbreite von 1200 px im sichtbaren Bereich.
- **Irreführende „keine Aufzeichnung"-Meldung** — `TaskDetailViewModel.cs` Z. 2367: Die Meldung
  lautet nun „…der Mitschnitt wird während einer CLI-Ausführung automatisch erstellt, aber nur
  flüchtig im Arbeitsspeicher gehalten (maximal die letzten 8 Aufgaben) und geht bei einem Neustart
  der Anwendung oder durch neuere Ausführungen verloren." Sie deckt beide Fälle ab (nie gestartet /
  verworfen) und erklärt, warum ein zuvor existierender Mitschnitt fehlen kann.

## Geprüfte Interaktionen

- CLI-Aufzeichnung aus der Aufgabenansicht exportieren (Ribbon „Aufzeichnung exportieren", .clireplay-Speicherdialog mit sinnvollem Dateinamen) → unauffällig; die verbesserte Fehlermeldung nennt die Flüchtigkeit als möglichen Grund
- Konsolentestfenster öffnen (Einstellungen → Allgemein → „Konsolentestfenster öffnen", nicht-modal) → unauffällig
- Aufzeichnung laden („Aufzeichnung öffnen…", Öffnen-Dialog auf *.clireplay gefiltert, verständliche deutsche Fehlermeldungen) → unauffällig
- Anzeige des geladenen Dateipfads → unauffällig (gekürzt mit Ellipsis, voller Pfad als Tooltip)
- Wiedergabe starten / neu starten / pausieren-fortsetzen → unauffällig
- Zeitraffer-Schwelle einstellen (Freitext Sekunden, wirkt live, klare Fehlermeldung bei ungültiger Eingabe) → unauffällig; ergänzend bleibt die zuletzt gültige Schwelle jetzt auch über Neustart/Neuladen hinweg konsistent (kein stiller Tempowechsel mehr)
- Quell-Ansicht synchron zur Wiedergabeposition (Markierung + Auto-Scroll, sichtbare Steuersequenzen) → unauffällig
- Positions-/Statusanzeige → unauffällig (bleibt jetzt auch bei langen Pfaden sichtbar)
- Hinweis bei unvollständiger Aufzeichnung → unauffällig
- Fenster schließen → unauffällig

## Geprüfte Dateien

- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (Diff)
