# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### KonsolenTestDialog.xaml (Konsolentestfenster)

- **Erreichbarkeit** — Der Pfad der geladenen Aufzeichnung wird als ungekürzter, unbegrenzt breiter Text mitten in der einzeiligen Werkzeugleiste angezeigt (`TextBlock Text="{Binding DateiPfad}"`, Zeile 29–34: kein `MaxWidth`, kein `TextTrimming`). Der Standard-Exportname `cli-replay-<32-stellige Guid>.clireplay` ist allein ~47 Zeichen lang; mit einem typischen Verzeichnis (z. B. Downloads) kommen schnell 70–90 Zeichen zusammen. Damit schiebt der Pfad die rechts folgenden Bedienelemente — Zeitraffer-Eingabe, Status-/Positionsanzeige und den „Schließen"-Button — bei der Standardfensterbreite von 1200 px ganz oder teilweise aus dem sichtbaren Bereich. Die Leiste ist ein starres horizontales `StackPanel` ohne Umbruch oder Scrollen; betroffene Elemente sind dann nur durch manuelles Verbreitern des Fensters wieder erreichbar. Das trifft genau den Hauptablauf („Aufzeichnung öffnen" → Pfad erscheint → restliche Steuerung verschwindet).

  Empfehlung: Dem Pfad-`TextBlock` ein `MaxWidth` (z. B. 300–400) und `TextTrimming="CharacterEllipsis"` geben, vollständigen Pfad als `ToolTip` zeigen — alternativ den Pfad aus der Werkzeugleiste herauslösen (z. B. eigene Zeile unterhalb der Leiste oder in der Titelleiste).

### TaskDetailViewModel.cs (Fehlermeldung beim Export)

- **Erreichbarkeit** — Die Fehlermeldung „Für diese Aufgabe liegt noch keine Aufzeichnung vor — sie wird während einer CLI-Ausführung automatisch mitgeschnitten." (`ExportCliReplayAsync`) ist irreführend, wenn der Mitschnitt tatsächlich existiert hat, aber bereits verworfen wurde: Die Aufzeichnung liegt nur im Arbeitsspeicher des laufenden Programms, wird nur für die zuletzt gestarteten Aufgaben vorgehalten (`KiAusfuehrungsService`, `MaxAufzeichnungenAnzahl = 8`) und geht bei einem Neustart der Anwendung verloren. Eine Anwenderin, die eine früher gelaufene Aufgabe exportieren will, erhält so den Eindruck, die Aufgabe hätte nie etwas aufgezeichnet — und keine Erklärung, warum nicht bzw. was sie tun kann.

  Empfehlung: Meldung ergänzen, dass der Mitschnitt flüchtig ist — z. B. „…Der Mitschnitt wird nur im Arbeitsspeicher der laufenden Programm-Sitzung für die jüngsten Aufgaben vorgehalten und geht bei einem Neustart verloren. Ggf. die Aufgabe erneut ausführen."

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- CLI-Aufzeichnung aus der Aufgabenansicht exportieren (Ribbon „Aufzeichnung exportieren", .clireplay-Speicherdialog mit sinnvollem Dateinamen) → Befund vorhanden (Fehlermeldung bei fehlendem/verworfenem Mitschnitt irreführend, siehe oben)
- Konsolentestfenster öffnen (Einstellungen → Allgemein → Abschnitt „Diagnose" → „Konsolentestfenster öffnen", nicht-modal parallel nutzbar) → unauffällig
- Aufzeichnung laden („Aufzeichnung öffnen…", Öffnen-Dialog auf *.clireplay gefiltert, Lade-/Formatfehler als verständliche deutsche Meldung im Fehlerbanner) → unauffällig
- Wiedergabe starten („Abspielen", aktiviert erst nach dem Laden; nach Wiedergabe-Ende startet derselbe Button erneut ab Position 0) → unauffällig
- Wiedergabe pausieren/fortsetzen („Pausieren/Fortsetzen"-Toggle; Zustand über Statustext „Pausiert." erkennbar) → unauffällig
- Wiedergabe mitten im Lauf oder aus der Pause heraus neu starten („Neu starten" mit Tooltip „Wiedergabe abbrechen und sofort wieder ab Position 0 abspielen") → unauffällig (Lösung des Runde-2-Befunds wirkt aus Anwendersicht)
- Zeitraffer-Schwelle einstellen (Freitext in Sekunden, wirkt sofort auf die laufende Wiedergabe, ungültige Eingaben mit klarer Fehlermeldung, Komma und Punkt akzeptiert) → unauffällig
- Quell-Ansicht der Chunks synchron zur Wiedergabeposition (aktuelle Zeile wird markiert und automatisch in den sichtbaren Bereich gescrollt; Steuersequenzen sichtbar als ␛, \r, \n, \xNN) → unauffällig
- Positions- und Statusanzeige („Chunk x/y", Statustext) → Befund vorhanden (kann bei langem Dateipfad aus dem sichtbaren Bereich der Werkzeugleiste geschoben werden — Teil des ersten Befunds)
- Hinweis bei unvollständiger Aufzeichnung („…das Speicher-Limit wurde erreicht; die Wiedergabe endet vor dem tatsächlichen Ende der Session") → unauffällig (umformulierter Text aus Runde 2 ist verständlich und nennt die Konsequenz klar)
- Fenster schließen („Schließen"-Button bzw. Fenster-X; nicht-modales Fenster) → unauffällig

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/Services/IDialogService.cs`
- `src/Softwareschmiede.App/Services/CliReplayExportService.cs`
- `src/Softwareschmiede.App/Services/CliChunkQuelltextFormatter.cs`
