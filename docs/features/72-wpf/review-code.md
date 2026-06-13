# Code-Review: Branch 72-wpf

Basis: `git diff main...HEAD` (455 geänderte Dateien, ~18 000 Zeilen diff)
Methode: 7 Finder-Winkel (Zeile-für-Zeile, entfernte Invarianten, Cross-File, Wiederverwendung, Vereinfachung, Effizienz, Altitude) → 1-Vote-Verifikation (recall-biased)

---

## Findings (JSON)

```json
[
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 68,
    "summary": "AufgabeDetail.razor.cs referenziert mehr als 10 entfernte Enum-Werte (Offen, InBearbeitung, KiAktiv, TestsLaufen, Abgeschlossen, Fehlgeschlagen) sowie nicht mehr existierende Service-Methoden (KiAbgeschlossenAsync, UpdateAsync mit 6 Parametern) — das Blazor-Projekt kompiliert nicht.",
    "failure_scenario": "Das AufgabeStatus-Enum enthält heute nur Neu/ArbeitsverzeichnisEingerichtet/Gestartet/InArbeit/Wartend/Beendet/Archiviert. Alle alten Werte in AufgabeDetail.razor.cs (Zeilen 68, 85–92, 221, 533, 1586–1597) erzeugen CS0117-Fehler; dazu kommen Aufrufe von KiAbgeschlossenAsync (Zeile 1597) und UpdateAsync mit falscher Signatur (Zeile 731). Der Blazor-Host startet nicht."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 1703,
    "summary": "AufgabeDetail.razor.cs ruft EntwicklungsprozessService.AbbrechenAsync auf, das aus dem Service vollständig entfernt wurde.",
    "failure_scenario": "Zeile 1703 ruft _entwicklungsprozessService.AbbrechenAsync(Id, _cts.Token) — die Methode existiert in EntwicklungsprozessService.cs nicht mehr. CS1061-Kompilierfehler; der Abbrechen-Button ist komplett defekt."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 73,
    "summary": "_handles[aufgabeId] = handle wird gesetzt, bevor process.Start() aufgerufen wird — schlägt Start() fehl, verbleibt ein Handle für einen nicht gestarteten Prozess im Dictionary.",
    "failure_scenario": "Ausführbardatei nicht gefunden oder OS verweigert Ausführung → process.Start() wirft Exception → der Handle ist bereits in _handles eingetragen → nächster Heartbeat-Tick ruft IsRunning(aufgabeId) auf → handle.Process.HasExited wirft InvalidOperationException für einen nicht gestarteten Prozess → unbehandelte Exception im ThreadPool-Callback."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 63,
    "summary": "Der Exited-Handler emittiert immer CliProcessStatus.Gestoppt, unabhängig vom Exit-Code — Abstürze sind von sauberem Stopp nicht unterscheidbar.",
    "failure_scenario": "Ein CLI-Prozess stürzt ab (Segfault, OOM) → Exit-Code ist ungleich 0 → der Handler sendet trotzdem Gestoppt statt Fehler → Recovery-Logik, Retry oder Alerts, die auf Fehler reagieren sollen, werden niemals ausgelöst. CliProcessStatus.Fehler ist im Enum definiert, wird aber nirgendwo emittiert."
  },
  {
    "file": "src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs",
    "line": 62,
    "summary": "ReadOutputLoop verwendet !_process.HasExited als Schleifenbedingung statt EOF auf dem Pipe — bei schnellem Prozessende gehen gepufferte Ausgabe-Zeilen verloren.",
    "failure_scenario": "CLI-Prozess schreibt letzte Ausgabe und beendet sich in schneller Folge → HasExited wird true, bevor ReadLineAsync erneut aufgerufen wird → Schleife endet → gepufferte Zeilen im Pipe-Buffer werden nie an _onOutput übergeben → letzte Statusmeldungen des KI-Tools (z.B. Fehlermeldungen) erscheinen nicht in der UI."
  },
  {
    "file": "src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs",
    "line": 62,
    "summary": "Wenn der Prozess stdout schließt ohne sich zu beenden, gibt ReadLineAsync wiederholt null zurück — die Schleife dreht sich mit vollem CPU-Verbrauch.",
    "failure_scenario": "Kind-Prozess schließt stdout-Pipe aber bleibt am Leben → HasExited bleibt false → ReadLineAsync() gibt sofort null zurück → null-Rückgabe wird lautlos verworfen → Schleife ruft ReadLineAsync() sofort erneut auf → 100 % CPU-Auslastung auf einem Kern bis zum App-Exit."
  },
  {
    "file": "src/Softwareschmiede/terminal-backend/server.js",
    "line": 23,
    "summary": "pty.spawn wird auf allen Plattformen mit dem Argument [\"-NoExit\"] aufgerufen — auf Linux/macOS ist das kein Shell-Flag, sondern ein Dateiname-Argument.",
    "failure_scenario": "Server läuft auf Linux/macOS → os.platform() gibt 'linux' zurück → shell = 'bash' → pty.spawn('bash', ['-NoExit'], ...) → bash interpretiert '-NoExit' als auszuführende Datei, findet sie nicht, beendet sich → PTY liefert keine Ausgabe → alle START_CLI- und Input-Nachrichten werden lautlos verworfen."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/AufgabeService.cs",
    "line": 220,
    "summary": "DeleteAsync enthält keine Status-Prüfung — aktive Aufgaben (InArbeit, Wartend, Gestartet) können gelöscht werden, ohne den laufenden Prozess zu stoppen.",
    "failure_scenario": "Aufgabe hat Status InArbeit mit aktivem Klon-Verzeichnis und laufendem CLI-Prozess → DeleteAsync löscht nur den DB-Eintrag → Klon-Verzeichnis bleibt auf Disk, CLI-Prozess läuft weiter ohne zugehörige Aufgabe → Ressourcen-Leak und verwaiste Prozesse."
  },
  {
    "file": "src/Softwareschmiede.App/Controls/ProcessWindowHost.cs",
    "line": 106,
    "summary": "GetWindowLong gibt 0 sowohl bei Erfolg als auch bei Win32-Fehler zurück; SetWindowLong wird danach bedingungslos mit dem möglicherweise invaliden Wert (0 | WS_CHILD) aufgerufen.",
    "failure_scenario": "GetWindowLong schlägt fehl (z.B. UIPI, elevated vs. non-elevated Process-Grenze) → style = 0 → SetWindowLong setzt das eingebettete Fenster auf WS_CHILD & ~WS_CAPTION mit allen anderen Bits gelöscht → alle echten Style-Bits (WS_POPUP, WS_SYSMENU, WS_MINIMIZEBOX) werden entfernt → Fenster rendert falsch oder reagiert nicht mehr auf Eingaben."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 169,
    "summary": "Event-Handler wird im Konstruktor auf KiAusfuehrungsService.CliProcessStatusChanged abonniert, aber nur in Dispose wieder abgemeldet — wird Dispose nicht gerufen, hält der Singleton-Service den ViewModel dauerhaft im Speicher.",
    "failure_scenario": "ViewModel wird als Transient aufgelöst; View ruft Dispose beim Unloaded-Event nicht auf (kein WPF-Standard) → Service (Singleton) hält Delegate → ViewModel wird nicht vom GC gesammelt → bei wiederholter Navigation akkumulieren sich ViewModel-Instanzen im Heap (Memory Leak)."
  }
]
```

---

## Zusammenfassung nach Schweregrad

| # | Datei | Zeile | Typ | Schwere |
|---|-------|-------|-----|---------|
| 1 | AufgabeDetail.razor.cs | 68 | Kompilierfehler (Enum-Mismatch) | Kritisch |
| 2 | AufgabeDetail.razor.cs | 1703 | Kompilierfehler (fehlende Methode) | Kritisch |
| 3 | KiAusfuehrungsService.cs | 73 | Runtime-Crash (Handle vor Start) | Hoch |
| 4 | KiAusfuehrungsService.cs | 63 | Logikfehler (Crash ≡ Stop) | Hoch |
| 5 | CliSessionService.cs | 62 | Datenverlust (Output-Drain) | Hoch |
| 6 | CliSessionService.cs | 62 | CPU-Spin (null-Loop) | Hoch |
| 7 | server.js | 23 | Platform-Bug (Linux/macOS) | Mittel |
| 8 | AufgabeService.cs | 220 | Fehlende Guard (aktive Tasks löschbar) | Mittel |
| 9 | ProcessWindowHost.cs | 106 | Win32-Fehlerbehandlung fehlt | Mittel |
| 10 | TaskDetailViewModel.cs | 169 | Memory Leak (Event-Subscription) | Niedrig |
