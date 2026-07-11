# Bestandsaufnahme: Tests automatisierbar machen

## Überblick

Diese Bestandsaufnahme dokumentiert die **vorhandene Test-Infrastruktur** für die Softwareschmiede WPF-Anwendung, insbesondere die Komponenten, die für die automatisierte Testausführung relevant sind. Fokus liegt auf:

- **WPF E2E-Test-Basis** (FlaUI-Automation, WpfTestBase)
- **Hook-System** für automatisierte Build-Orchestrierung vor Tests
- **Datei-Lock-Mechanismus** zur Verhinderung von parallelen Build-Konflikten
- **App-Startup-Pipeline** mit strukturiertem Logging und Exception-Handling
- **Test-Ablauf und Fehlerbehandlung**

Die Anforderung behandelt zwei Fehler:
1. Sporadische ".NET Desktop Runtime nicht gefunden"-Fehler bei Testausführung (Hypothese: Build-Lock-Konflikte).
2. "MainWindow nicht gefunden"-Fehler in E2E-Tests (Hypothese: XamlParseException oder Startup-Fehler, die sich wie UI-Timeout anfühlen).

---

## Zusammenfassung der Befunde

### ✓ Vorhanden

- **WpfTestBase** (`src\Softwareschmiede.Tests\E2E\WpfTestBase.cs`)
  - Basis-Klasse für E2E-Tests mit FlaUI-Automation
  - Unterstützungs-Methoden für App-Launch, UI-Element-Suche, Test-DB-Management
  - Fail-Fast-Diagnose bei Fehlerbannern (prüft auf "FehlerMeldung"-Element)
  - **Aber:** Keine Log-Datei-Auslese bei Timeouts; keine Exception-Stack-Details bei "MainWindow nicht gefunden"

- **E2E-Test-Klassen** (`ProjectDetailE2ETests.cs`, `WpfE2EPlaceholderTests.cs`)
  - Multiple Test-Szenarien (Project Create/Open, Task Create, Settings, Dark Mode)
  - Collection-Trait `[Collection("E2E")]` zur Vermeidung von Parallelisierung

- **Hook-System für Build-Orchestrierung** (`.claude/settings.json`)
  - `PreToolUse`-Hook triggert `build_before_test.py` vor jedem `dotnet test`
  - Hook blockiert Tests nicht bei Build-Fehler

- **Build-Hook mit Datei-Lock** (`.claude/hooks/build_before_test.py` + `dotnet_lock.py`)
  - Führt `dotnet build` vor Tests aus
  - Nutzt Datei-Lock (`.claude/.locks/dotnet-build.lock/`) zum Schutz vor parallelen Zugriffen
  - Lock-Timeout: 120s; Stale-Threshold: 300s
  - Graceful Fallback: Falls Lock-Erwerb fehlschlägt, fährt Hook ohne Lock fort

- **PowerShell Stop-Hook mit Lock-Synchronisierung** (`.claude/hooks/test-csharp-startup.ps1`)
  - Führt Smoke Test für ausführbare Projekte durch
  - Nutzt **denselben Datei-Lock** wie Python-Hook
  - Includes Port-Check (falls App in VS läuft, wird Start übersprungen)

- **App-Startup-Pipeline mit Logging** (`src\Softwareschmiede.App\App.xaml.cs`)
  - Serilog-Logger mit Console- und File-Ausgabe
  - Log-Verzeichnis: `<AppBaseDirectory>/logs/`
  - Rolling daily, 14 Tage Retention
  - Exception-Handler für UI-Thread, Background-Thread, Tasks
  - Fehler beim MainWindow-Start werden geloggt: `"MainWindow konnte nicht angezeigt werden."`

- **Test-Datenbank-Isolation** (WpfTestBase)
  - Jeder Test nutzt temporäre Testdatenbank via `SOFTWARESCHMIEDE_TEST_DB_PATH`-Umgebungsvariable
  - Tests laufen sequenziell (`[Collection("E2E")]`), nicht parallel

### ✗ Fehlend oder Unvollständig

- **App-Log-Auslese in WpfTestBase**
  - `WaitForElement()` / `WaitWhileMainHandleIsMissing()` haben **keine Logik** zum Auslesen der App-Log-Datei bei Timeout
  - Test-Fehler zeigen nur "Element nicht gefunden" oder "Fenster nicht sichtbar", nicht die tatsächliche Exception aus der App
  - **Szenario:** App crasht mit `XamlParseException` → Test zeigt "TimeoutException" statt tatsächlicher Exception

- **Diagnose-Methoden in WpfTestBase**
  - Keine `GetLatestAppLogContent()` oder `CheckAppStartupException()`-Hilfsmethoden
  - Tests können nicht Anwendungs-Log-Dateien analysieren

- **Retry-Logik für flaky UI-Elements**
  - `WaitForElement()` hat polling mit 200ms Interval, aber keine konfigurierbare Retry-Strategie
  - Keine Differenzierung zwischen "Element wirklich nicht da" und "Startup-Exception"

- **Build-Hook-Häufigkeit dokumentiert nicht**
  - `.claude/settings.json` triggert `dotnet build` vor **jedem** `dotnet test`-Befehl
  - Unklar ob das notwendig ist oder ob ein Single Build vor dem ersten Test reichen würde

- **Prozessmanagement bei Build-Locks**
  - Hook findet blockierende `Softwareschmiede.App.exe`-Prozesse nicht aktiv
  - Nur Lock-Timeout nach 120s (dann fährt Hook ohne Lock fort)
  - **Wichtig:** CLAUDE.md verbietet das Beenden von `Softwareschmiede.App.exe` (könnte Host-Session sein)

- **Flakiness-Toleranz nicht konfiguriert**
  - Keine Retry-Strategie für Fenster-Erkennungen definiert
  - `WaitForElement()` hat festes 200ms Polling, keine exponential backoff oder konfigurierbares Retry

---

## Details

### Test-Infrastruktur
Detaillierte Übersicht über WpfTestBase, Testklassen, Hilfsmethoden und deren Abhängigkeiten.

→ [Test-Infrastruktur](inventory/tests.md)

### Hook-System und Build-Orchestrierung
Detaillierte Dokumentation des PreToolUse-Hooks, Build-Hook-Implementierung, Datei-Lock-Mechanismus und PowerShell Stop-Hook.

→ [Hook-System](inventory/hooks.md)

### App-Startup und Exception-Handling
Detaillierte Dokumentation der Startup-Pipeline, Logging-Konfiguration, Exception-Handler und Log-Dateien.

→ [App-Startup und Logging](inventory/app_startup.md)

---

## Offene Fragen (aus der Anforderung)

1. **Build-Hook-Frequenz:** Ist es gewünscht, dass vor *jedem* `dotnet test`-Befehl ein Build auslöst? Oder reicht ein einmaliger Build vor dem ersten Test-Durchlauf?
   - **Aktueller Status:** Hook triggert vor jedem `dotnet test`.

2. **Prozessmanagement:** Sollte die Hook-Infrastruktur aktiv nach verwaisten oder blockierenden Test-Prozessen suchen und ggf. bereinigen?
   - **Aktueller Status:** Stop-Hook hat Port-Check für Web-Apps, aber nicht für WPF-Apps. Kein aktives Prozessmanagement für blockierende `Softwareschmiede.App.exe` (verboten per CLAUDE.md).

3. **Log-Zugriff in Tests:** Ist es zulässig, dass `WpfTestBase` die Anwendungs-Log-Dateien unter `bin/<Config>/logs/` ausliest?
   - **Aktueller Status:** Nicht implementiert. Log-Verzeichnis existiert, aber WpfTestBase greift nicht zu.

4. **Flakiness-Toleranz:** Welche Retry-Strategie für Fenster-Erkennungen ist akzeptabel? (z. B. bis zu 3 Versuche mit je 1 Sekunde Verzögerung)
   - **Aktueller Status:** Polling mit 200ms, aber keine Retry-Logik; nach Timeout wird Exception geworfen.

5. **Reproduzierbarkeit:** Können die Fehler lokal reproduziert werden, oder treten sie nur in CI/CD-Pipeline oder bei bestimmten Hardware-Konfigurationen auf?
   - **Aktueller Status:** Unklar — Hook/Lock sind bereits implementiert als Verbesserung.

6. **Hook-Korrektur-Status:** Die Kundenanforderung erwähnt „Es wurde bereits eine Korrektur der Hooks vorgenommen." — sollen die bisherigen Änderungen in `.claude/hooks/build_before_test.py` in die Analyse einbezogen werden, oder ist diese Hook noch nicht korrekt implementiert?
   - **Aktueller Status:** Hook mit Lock ist implementiert; Status der Effektivität unklar.

---

## Nächste Schritte (nicht Teil dieser Bestandsaufnahme)

Die Anforderung skizziert diese Implementierungsschritte:

- Erweitere `WpfTestBase.WaitForElement()` / `WaitWhileMainHandleIsMissing()` um Log-Auslese-Logik.
- Implementiere `GetLatestAppLogContent()` und `CheckAppStartupException()` Hilfsmethoden.
- Prüfe Hook-Konfiguration auf Build-Lock-Effektivität und Build-Frequenz.
- Optional: Retry-Logik mit Verzögerung für flaky Fenster-Erkennungen.

Diese Bestandsaufnahme liefert die Basis für die Planung dieser Implementierungen.
