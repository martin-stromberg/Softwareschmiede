# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### KonsolenTestDialog.xaml / KonsolenTestViewModel.cs (Konsolentestfenster)

- **Erreichbarkeit** — Kein erneutes Abspielen möglich, „Abspielen"-Button wird zur Falle: Nach Ende der Wiedergabe (Status „Wiedergabe beendet.") wird der „Abspielen"-Button wieder aktivierbar (`WiedergabeStartenCommand.CanExecute` = `!IstWiedergabeAktiv`). Ein Klick ruft `TerminalReplaySession.WiedergabeStarten()` auf, das aber idempotent ist und bei bereits gelaufenem Durchlauf stillschweigend nichts tut (`_wiedergabeGestartet` bleibt 1). Das ViewModel setzt trotzdem `IstWiedergabeAktiv = true` und `StatusText = "Wiedergabe läuft."` — die Nutzerin sieht also eine angeblich laufende Wiedergabe, deren Pause-Button ebenfalls aktivierbar ist, während tatsächlich nie wieder etwas passiert. Der einzige unsichtbare Ausweg ist, dieselbe Datei über „Aufzeichnung öffnen…" erneut zu laden. Für ein Diagnose-Werkzeug, dessen Kernfall das mehrfache Abspielen derselben Aufzeichnung ist (Fehlerbild genau betrachten, Zeitraffer-Varianten probieren), ist das nicht zumutbar — und der falsche Status „Wiedergabe läuft." ist aktiv irreführend.

  Empfehlung: Echte Neustart-Möglichkeit schaffen — z. B. einen „Erneut abspielen"/„Von vorn"-Button, der den Replay-Zustand zurücksetzt (Buffer-Reset, Chunk-Index auf 0, neue Wiedergabe-Schleife auf derselben geladenen Aufzeichnung), oder die Replay-Session bei jedem Start aus der bereits geladenen `CliOutputAufzeichnung` neu erzeugen, statt die Datei neu laden zu müssen. Mindestlösung: „Abspielen" nach Ende deaktiviert lassen und im Status klartextlich sagen, dass zum erneuten Abspielen die Datei neu geöffnet werden muss — die Variante mit echtem Neustart ist fachlich klar vorzuziehen.

- **Erreichbarkeit** — Fenster ist modal: `WpfDialogService.ShowKonsolenTestDialogAsync` öffnet den Dialog mit `ShowDialog()` und `Owner = MainWindow`. Solange das Konsolentestfenster offen ist, kann die Nutzerin nicht parallel zur laufenden Anwendung arbeiten — z. B. nicht nebenher die Live-Ausgabe der betroffenen Aufgabe in der Task-Detailansicht ansehen oder eine zweite Aufzeichnung exportieren, ohne das Fenster zu schließen. Die Anforderung hat modal vs. nicht-modal ausdrücklich als offene Frage markiert; für ein Vergleichs-/Diagnose-Werkzeug ist das Blockieren des Hauptfensters ein spürbarer Nachteil.

  Empfehlung: Nicht-modales Fenster (`Show()` statt `ShowDialog()`, Owner beibehalten), damit Replay-Fenster und Live-Task parallel sichtbar bleiben. Technisch prüfen: Lebenszyklus des `KonsolenTestViewModel`/`TerminalReplaySession` muss dann auch bei offenem Fenster über Hauptfenster-Wechsel hinweg sauber disponiert werden (bereits über `Closed`-Handler vorhanden).

### TaskDetailView.xaml / TaskDetailViewModel.cs (Aufgaben-Detail, Ribbon „CLI")

- **Erreichbarkeit** — „Aufzeichnung exportieren" ist auch dann aktiv, wenn gar keine Aufzeichnung existiert: `KannCliReplayExportieren` prüft nur, ob eine Aufgabe geladen ist. Gibt es keinen Mitschnitt (Aufgabe hatte noch keinen Session-Start seit dem Feature, Aufzeichnung per `AufzeichnungByteBudget <= 0` deaktiviert, oder App-Neustart hat den In-Memory-Mitschnitt verloren), öffnet sich zunächst ganz normal der Speicherdialog, die Nutzerin wählt Zielordner und Dateiname — und bekommt erst danach die Fehlermeldung „CLI-Aufzeichnung konnte nicht exportiert werden: Für diese Aufgabe liegt keine Aufzeichnung vor." Die umsonst investierte Interaktion mit dem Dateidialog ist vermeidbar.

  Empfehlung: Vor dem `ShowSaveFileDialogAsync` prüfen, ob `GetCliAufzeichnung(aufgabeId)` einen Mitschnitt liefert (z. B. über eine `HatAufzeichnung(aufgabeId)`-Abfrage am `ICliReplayExportService`), und bei Fehlen direkt eine verständliche Meldung zeigen („Für diese Aufgabe liegt noch keine Aufzeichnung vor — sie wird während einer CLI-Ausführung automatisch mitgeschnitten.") — oder den Button in dem Fall deaktivieren.

- **Erreichbarkeit** — Zwei ähnlich klingende Export-Buttons direkt nebeneinander ohne Unterscheidungshilfe: „Rohausgabe exportieren" (.raw) und „Aufzeichnung exportieren" (.clireplay) stehen unmittelbar nebeneinander in derselben Ribbon-Gruppe. Aus der Beschriftung geht nicht hervor, welcher Export die Datei für das Konsolentestfenster liefert — eine Nutzerin, die in den Einstellungen nur „exportierte CLI-Aufzeichnung (.clireplay)" gelesen hat, muss raten oder probieren. Zusätzlich ist der Dateifilter im Speicherdialog englisch („CLI-Replay files (*.clireplay)"), während der Öffnen-Dialog im Konsolentestfenster deutsch filtert („CLI-Replay-Dateien (*.clireplay)").

  Empfehlung: Dem neuen Button einen Tooltip bzw. eine eindeutigere Beschriftung geben, die den Zweck benennt — z. B. „CLI-Aufzeichnung exportieren (.clireplay, für Konsolentestfenster)". Den Speicherdialog-Filter auf „CLI-Replay-Dateien (*.clireplay)|*.clireplay" vereinheitlichen.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Diagnose-Werkzeug über die Einstellungen öffnen → unauffällig (eigener Abschnitt „Diagnose" mit Erklärtext und klar beschriftetem Button „Konsolentestfenster öffnen")
- Aufgezeichnete CLI-Ausgabe aus der Task-Detailansicht in eine Datei exportieren → Befund vorhanden (kein Vorab-Check auf vorhandene Aufzeichnung; Verwechslungsgefahr mit „Rohausgabe exportieren"; englischer Filtertext)
- Aufzeichnung im Konsolentestfenster über eine Dateiauswahl laden → unauffällig (Standard-Öffnen-Dialog auf *.clireplay gefiltert, keine interne Kennung/Id-Eingabe nötig; Ladefehler als Klartext-Banner)
- Wiedergabe zeitreal starten → Befund vorhanden (kein erneutes Abspielen nach Ende; „Abspielen" zeigt danach fälschlich „Wiedergabe läuft.")
- Wiedergabe pausieren/fortsetzen → unauffällig (verständlich beschrifteter Toggle-Button, Status wechselt sichtbar zwischen „Wiedergabe läuft."/„Pausiert.")
- Zeitraffer-Schwelle für lange Leerzeiten einstellen → unauffällig (verständlich beschriftetes Sekundenfeld; ungültige Eingabe erzeugt klare Fehlermeldung, letzte gültige Schwelle bleibt wirksam)
- Quell-Repräsentation der Chunks synchron zur Wiedergabeposition betrachten → unauffällig (Liste mit #-/Offset-/Bytes-/Quelltext-Spalten, Steuersequenzen sichtbar gemacht, Auswahl folgt der Wiedergabeposition und scrollt mit)
- Wiedergabeposition/-status ablesen → unauffällig („Chunk x/y", Lade-Status mit Plugin-Name und Chunk-Anzahl, Hinweis bei unvollständiger Aufzeichnung)
- Fehler-/Unvollständigkeitszustände erkennen → unauffällig (Fehlerbanner und „Aufzeichnung unvollständig — Budget überschritten." als Klartext)
- Fenster schließen → unauffällig (Schließen-Button und Fenster-X; modal siehe Befund)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/Services/CliChunkQuelltextFormatter.cs`
- `src/Softwareschmiede.App/Services/CliReplayExportService.cs`
- `src/Softwareschmiede.App/Services/IDialogService.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs` (soweit es das Bedienverhalten der Wiedergabe-Steuerung bestimmt)
