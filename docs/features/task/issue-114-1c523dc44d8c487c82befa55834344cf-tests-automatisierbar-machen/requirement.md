# Kundenanforderung – Tests automatisierbar machen

## Fachliche Zusammenfassung

Die automatisierten E2E-Tests (WPF-UI-Tests) der Anwendung scheitern sporadisch mit zwei wiederkehrenden Fehlermustern:

1. Fehlende `.NET Desktop Runtime` bei der Testausführung, wenn das Kompilat (`*.exe`, `*.dll`) während der Tests verändert wird. Eine manuelle Ausführung der drei Befehle `dotnet clean`, `dotnet build`, `dotnet test` in sequenzielle Reihenfolge führt nicht zu diesem Fehler.

2. UI-Startfehler bei E2E-Tests: Das `MainWindow` wird nicht gefunden und/oder die Anwendung startet nicht, obwohl die Tests in anderen Läufen erfolgreich verlaufen.

Beide Fehler sind nicht konsistent reproduzierbar und deuten auf ein Timing- oder Abhängigkeitsproblem in der automatisierten Test-Orchestrierung hin.

## Betroffene Klassen und Komponenten

### Infrastruktur und Konfiguration
- **Hook-System** (`.claude/settings.json`): `PreToolUse`-Hook für `dotnet test`, der vor jedem Test-Befehl automatisch einen Build auslöst (`.claude/hooks/build_before_test.py`)
- **Build-Orchestrierung** (`.claude/hooks/build_before_test.py`): Python-Hook, der `dotnet build` vor `dotnet test` ausführt
- **Test-Infrastruktur** (WPF E2E Tests)
  - `WpfTestBase`: Basis-Klasse für WPF-UI-Tests mit `WaitForElement`- und `WaitWhileMainHandleIsMissing`-Methoden
  - Testprozess und Prozessmanagement

### Artefakte und Daten
- Anwendungs-Log-Dateien: `src/Softwareschmiede.App/bin/<Config>/<TargetFramework>/logs/softwareschmiede-*.log`
- Build-Ausgabeverzeichnisse und Artefakte (`bin/`, `obj/`)
- WPF-Ressourcen und XAML-Dependencies

## Implementierungsansatz

### Diagnose Problem 1: .NET Desktop Runtime & Build-Lock

**Ursache (Hypothese):** Der `PreToolUse`-Hook triggert einen `dotnet build` vor jedem `dotnet test`, während die vorherige Test-Instanz noch aktiv ist. Dies kann zu:
- **Datei-Locks** führen (MSB3027/MSB3026 Fehler beim Kopieren von DLLs durch eine laufende `Softwareschmiede.App.exe` während Tests)
- **Inkonsistenten Assemblies** im `bin/`-Verzeichnis (nur teilweise aktualisierte Binaries, während die Runtime noch die alten Versionen nutzt)

**Empfohlene Maßnahmen:**
- Prüfe die `.claude/hooks/build_before_test.py`: Wird der Build mit `dotnet clean` vorab ausgeführt, oder nur `dotnet build`?
- Verifiziere, dass `dotnet build` wirklich notwendig ist vor jedem `dotnet test` oder ob es nur beim initialen Test-Durchlauf nötig wäre
- Implementiere ggf. eine "Shared Build"-Strategie: Ein einzelner `dotnet clean && dotnet build` vor *allen* Tests, nicht vor jedem einzelnen Test-Befehl
- Dokumentiere Workaround: Benutzer kann laufende `Softwareschmiede.App.exe`-Instanzen selbst schließen, bevor Tests laufen

### Diagnose Problem 2: UI-Startup-Fehler im MainWindow

**Ursache (Hypothese):** Laut `CLAUDE.md` kann ein `XamlParseException` oder anderer Crash während `App.xaml.cs`-Startup sich identisch anfühlen wie ein fehlender/langsamer `MainWindow`, aber hat eine völlig andere Ursache.

**Empfohlene Maßnahmen:**
- Implementiere in `WpfTestBase.WaitForElement` / `WaitWhileMainHandleIsMissing` eine Logik zum **Auslesen der Anwendungs-Log-Datei** bei einem Timeout/Fehler
- Wenn die Test-Anwendung startet aber crashed, zeige dem Benutzer den tatsächlichen Exception-Stack aus der Log-Datei, nicht nur "element not found"
- Definiere einen erweiterten Diagnose-Report: Prüfe auf `[ERR]`-Einträge in der Log-Datei, insbesondere bei XAML-Parsing oder Resource-Loading
- Implementiere ggf. Retry-Logik mit Verzögerung für flaky Fenster-Erkennungen, aber differenziere zwischen "wirklich fehlendes Fenster" und "Startup-Exception"

### Technische Umsetzung

**Klassen/Methoden, die erweitert/neu geschaffen werden könnten:**
- `WpfTestBase`: Erweitere um `GetLatestAppLogContent()` und `CheckAppStartupException()` Hilfsmethoden
- Hook-System: Überprüfe `.claude/hooks/build_before_test.py` auf das Build-Lock-Problem
- Test-Runner-Logik: Implementiere besseres Fehler-Reporting beim `MainWindow`-Timeout

**Abhängigkeiten:**
- Dateisystem-Zugriff auf die Log-Dateien der Testanwendung
- Log-Parsing (z. B. Regex für `[ERR]`-Zeilen oder spezifische Exception-Muster)
- Timing/Retry-Logik in der WPF-Test-Infrastruktur

## Konfiguration

Die Hook-Konfiguration in `.claude/settings.json` ist direkt betroffen:

```json
"PreToolUse": [
  {
    "matcher": "Bash",
    "hooks": [
      {
        "type": "command",
        "if": "Bash(*dotnet test*)",
        "statusMessage": "Building solution before dotnet test...",
        "command": "pwsh -NoProfile -Command \"Set-Location (git rev-parse --show-toplevel); python .claude/hooks/build_before_test.py\""
      }
    ]
  }
]
```

Diese Hook muss überprüft und ggf. angepasst werden:
- Sollte der Build wirklich *vor jedem* `dotnet test` stattfinden, oder nur beim initialen Durchlauf?
- Sollte die Hook `dotnet clean` vorab ausführen, um Locks zu vermeiden?
- Kann die Hook erweitert werden, um laufende Test-Prozesse zu erkennen und zu warten?

## Offene Fragen

1. **Build-Hook-Frequenz:** Ist es gewünscht, dass vor *jedem* `dotnet test`-Befehl ein Build auslöst? Oder reicht ein einmaliger Build vor dem ersten Test-Durchlauf?

2. **Prozessmanagement:** Sollte die Hook-Infrastruktur aktiv nach verwaisten oder blockierenden Test-Prozessen suchen und ggf. bereinigen? (Hinweis: `Softwareschmiede.App.exe` darf **nicht** beendet werden, wenn es die Host-Session ist — siehe `CLAUDE.md`, Abschnitt „Self-Hosting-Risiko".)

3. **Log-Zugriff in Tests:** Ist es zulässig, dass `WpfTestBase` die Anwendungs-Log-Dateien unter `bin/<Config>/logs/` ausliest? Müssen diese Log-Dateien persistiert oder archiviert werden?

4. **Flakiness-Toleranz:** Welche Retry-Strategie für Fenster-Erkennungen ist akzeptabel? (z. B. bis zu 3 Versuche mit je 1 Sekunde Verzögerung)

5. **Reproduzierbarkeit:** Können die Fehler lokal reproduziert werden, oder treten sie nur in der CI/CD-Pipeline oder bei bestimmten Hardware-Konfigurationen auf?

6. **Hook-Korrektur-Status:** Die Kundenanforderung erwähnt „Es wurde bereits eine Korrektur der Hooks vorgenommen." — sollen die bisherigen Änderungen in `.claude/hooks/build_before_test.py` in die Analyse einbezogen werden, oder ist diese Hook noch nicht korrekt implementiert?
