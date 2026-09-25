# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- CLI für eine Aufgabe über den sichtbaren Start-Button starten → unauffällig (bestehender Bedienfluss unverändert; Session-Typ intern auf `ITerminalSession` umgestellt, ohne sichtbare Änderung)
- Eingabe, Enter, Backspace, Ctrl-C und Paste im eingebetteten Terminal der Aufgabenansicht → unauffällig (`TerminalControl` leitet Tastatur- und Zwischenablage-Eingaben weiterhin in die Session; der interne Schreibweg über `WriteInputAsync` ändert nichts am Bedienverhalten)
- Fenster-/Terminalgröße ändern → unauffällig (`OnRenderSizeChanged` ↔ `session.Resize` weiterhin verdrahtet)
- Zurück zu einer Aufgabe mit laufender CLI navigieren (Session-Neuanbindung) → unauffällig (Buffer wird aus dem Replay-Puffer neu aufgebaut; bestehender Bildschirminhalt bleibt sichtbar, keine doppelten Ausgaben)
- Scrollen im Terminal, inkl. Vollbild-TUI (Alternate Screen) → unauffällig (Scrollback wird bei aktivem Alternate Screen korrekt geklemmt — kein irritierendes Scrollen in nicht vorhandene Historie)
- Pipe-Fallback als solchen erkennen (Anforderung: „darf nicht stillschweigend als gleichwertiger interaktiver Modus gelten") → unauffällig (Statusleiste zeigt den Suffix „(eingeschränkter Modus – kein Pseudo-Terminal)" an allen Runtime-Status-Texten — „Gestartet", „Ausführung läuft", „Wartet auf Eingabe"; zusätzlich `[Terminal-Diagnose]`-Markerzeile mit Einzelcheck-Ergebnissen im CliOutput-Protokoll)
- Start einer CLI, die zwingend ein Pseudo-Terminal benötigt (`RequiresPty`), bei nicht verfügbarer PTY → unauffällig (harter Abbruch mit verständlicher deutscher Fehlermeldung im sichtbaren Fehlerbanner: „Die CLI '…' erfordert ein Pseudo-Terminal (PTY), das auf diesem System nicht verfügbar ist." — keine technische Kennung, kein stiller Fallback)
- KI-Plugin auswählen/wechseln → unauffällig (bestehender Auswahl-Dialog unverändert; kein neuer Identifikator erforderlich)
- Fehler beim Starten (CLI-Executable nicht auffindbar/nicht startbar) → unauffällig (verständliche Meldung im Fehlerbanner inkl. Grund aus der Executable-Auflösung)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Controls/TerminalControl.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs` (nur Kommentaränderung)
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede.App/App.xaml.cs` (nur DI-Registrierung, nicht benutzerseitig sichtbar)
- `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestConfiguration.cs` (nur Konstanten-Umbau, Test-Infrastruktur)

## Anmerkungen

- Die seit der letzten Runde erfolgte Umstellung von `AutomationProperties.Name` auf `AutomationProperties.AutomationId` am `CliStatusText` (TaskDetailView.xaml, Zeile 667) ist für sehende Anwender unsichtbar und für Screenreader-Nutzer eine Verbesserung: Die UIA-Name-Property liefert nun den tatsächlichen Statustext statt des Literals „CliStatusText" — konsistent mit dem benachbarten `AktiverCliName`-Element.
- Der Fallback-Hinweis ist ausschließlich in der Statusleiste der Aufgabenansicht sichtbar; die `[Terminal-Diagnose]`-Details landen im Aufgabenprotokoll. Beide Kanäle sind ohne technisches Vorwissen auffindbar; die Anforderung („explizit diagnostizierbar, nicht stillschweigend") ist erfüllt.
