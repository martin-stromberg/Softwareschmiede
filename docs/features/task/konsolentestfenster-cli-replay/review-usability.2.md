# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

Zweite Review-Runde. Alle vier Befunde aus Runde 1 wurden aus Anwendersicht korrekt gelöst:

- **Neustart der Wiedergabe** — behoben: Nach „Wiedergabe beendet." ist die Schaltfläche „Abspielen" wieder aktiv und spielt dieselbe geladene Aufzeichnung von Position 0 ab (frische Session, Terminal wird zurückgesetzt). Siehe aber Befund 1: ein Neustart ist nur im beendeten Zustand möglich.
- **Export ohne Vorab-Prüfung** — behoben: Vor dem Speicherdialog wird geprüft, ob überhaupt ein Mitschnitt vorliegt; andernfalls erscheint die verständliche Meldung „Für diese Aufgabe liegt noch keine Aufzeichnung vor — sie wird während einer CLI-Ausführung automatisch mitgeschnitten."
- **Verwechslungsgefahr der beiden Exporte** — behoben: Zwei klar unterscheidbare Ribbon-Schaltflächen („Rohausgabe exportieren" / „Aufzeichnung exportieren") mit Tooltips, die Dateityp und Zweck benennen (.raw-Textdatei vs. .clireplay für das Konsolentestfenster); der Öffnen-Dialog filtert passend auf „CLI-Replay-Dateien (*.clireplay)".
- **Modales Fenster** — behoben: Das Konsolentestfenster öffnet nicht-modal (parallel zum Hauptfenster nutzbar, Vergleich Replay ↔ Live-Ausgabe möglich); Datei-Dialoge aus dem Fenster heraus werden korrekt zugeordnet und fokussiert.

Verbleibend zwei kleinere Befunde (beide niedrige Schwere, Umwege vorhanden):

## Befunde

### KonsolenTestDialog.xaml / KonsolenTestViewModel.cs (Konsolentestfenster)

- **Erreichbarkeit** — Eine laufende oder pausierte Wiedergabe kann nicht neu gestartet werden. „Abspielen" ist während der Wiedergabe deaktiviert (`CanExecute = !IstWiedergabeAktiv`); „Pausieren/Fortsetzen" hält nur an. Wer beim Abspielen die interessante Stelle verpasst hat, muss die Aufzeichnung entweder zu Ende laufen lassen (bzw. Zeitraffer-Schwelle auf 0 setzen und abwarten) oder die Datei über „Aufzeichnung öffnen…" erneut laden — beides umständlich für das geforderte Diagnose-Szenario, eine bestimmte Stelle wiederholt anzusehen.

  Empfehlung: Zusätzliche Schaltfläche „Neu starten" (bzw. „Abspielen" während aktiver Wiedergabe als „Neu starten" weiterhin aktiviert lassen), die dieselbe bereits geladene Aufzeichnung sofort wieder von Position 0 abspielt — analog dem vorhandenen Neustart-Pfad nach „Wiedergabe beendet" (`_wiedergabeBeendet`-Zweig in `WiedergabeStarten`).

### KonsolenTestViewModel.cs (Hinweisbanner bei unvollständiger Aufzeichnung)

- **Erreichbarkeit** — Der Hinweis „Aufzeichnung unvollständig — Budget überschritten." verwendet den technischen Begriff „Budget" und erklärt nicht, was das für den Anwender bedeutet. Ein Laie (bzw. auch die technisch affine Zielperson ohne Code-Kenntnis) kann nicht erkennen, dass ein Speicher-Limit den Mitschnitt vorzeitig abgebrochen hat und die Wiedergabe daher vor dem tatsächlichen Session-Ende stoppt.

  Empfehlung: Verständlichere Formulierung mit Konsequenz, z. B. „Aufzeichnung unvollständig — das Speicher-Limit wurde erreicht; die Wiedergabe endet vor dem tatsächlichen Ende der Session."

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Mitschnitt der CLI-Ausgabe (läuft automatisch im Hintergrund, kein Eingriff nötig; bei fehlendem Mitschnitt erklärt die Export-Fehlermeldung warum) → unauffällig
- Aufzeichnung als `.clireplay` aus der Aufgaben-Detailansicht exportieren (Ribbon-Gruppe „CLI", Speicherdialog, Vorab-Prüfung, vorgeschlagener Dateiname) → unauffällig
- Konsolentestfenster öffnen (Einstellungen → Tab „Allgemein" → Abschnitt „Diagnose", erklärender Text + Schaltfläche) → unauffällig (Anmerkung: Export und Wiedergabefenster liegen an zwei Orten; nach dem Export muss der Anwender wissen, dass er über die Einstellungen zum Fenster kommt — durch die Beschriftung der Export-Schaltfläche und des Diagnose-Abschnitts auffindbar, daher kein Befund)
- Aufzeichnung im Fenster laden (Öffnen-Dialog mit `.clireplay`-Filter, Ladefehler als verständliches Fehlerbanner) → unauffällig
- Wiedergabe zeitreal abspielen und nach Ende erneut abspielen → unauffällig
- Wiedergabe mitten im Lauf neu starten → **Befund vorhanden** (siehe oben)
- Pausieren/Fortsetzen (Toggle-Schaltfläche, Statusanzeige „Pausiert."/„Wiedergabe läuft.") → unauffällig
- Zeitraffer-Schwelle einstellen (Sekunden-Eingabefeld, Validierung mit verständlicher Fehlermeldung, 0 = maximale Geschwindigkeit) → unauffällig (Anmerkung: Die rote Fehlerleiste erscheint bereits während der Eingabe bei Zwischenständen, die nicht parsebar sind — bei dieser Werkzeug-Zielgruppe akzeptabel)
- Quell-Ansicht der Chunks mit sichtbaren Steuersequenzen synchron zur Wiedergabeposition (Liste mit #/Offset/Bytes/Quelltext, ESC als ␛, automatisches Mitscrollen auf den aktuellen Chunk) → unauffällig (Anmerkung: Manueller Sprung zu einem Chunk per Klick ist nicht möglich — Anforderung verlangt nur synchrone Anzeige, kein Seek)
- Positions-/Statusanzeige („Chunk x/y", Ladestatus mit Plugin-Name und Chunk-Anzahl) → unauffällig
- Hinweis bei unvollständiger Aufzeichnung (Banner oberhalb der Ansicht) → **Befund vorhanden** (Formulierung, siehe oben)
- Nicht-modale Nutzung parallel zum Hauptfenster; Schließen über „Schließen"-Schaltfläche oder Fenster-X → unauffällig (Anmerkung: Mehrfaches Öffnen erzeugt mehrere Fenster — für den Diagnose-Zweck eher nützlich als störend)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`
- `src/Softwareschmiede.App/Services/CliChunkQuelltextFormatter.cs`
- `src/Softwareschmiede.App/Services/CliReplayExportService.cs`
- `src/Softwareschmiede.App/Services/IDialogService.cs` (Diff)
- `src/Softwareschmiede.App/Services/WpfDialogService.cs` (Diff)
- `src/Softwareschmiede.App/Views/SettingsView.xaml` (Diff)
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` (Diff)
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` (Diff)
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (Diff)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs` (Wiedergabe-Semantik, zur Beurteilung der Bedienlogik)
