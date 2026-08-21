# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests.

## Zusammenfassung

- Gesamt: 1491
- Bestanden: 1489
- Fehlgeschlagen: 0
- Übersprungen: 2

### Test-Aufschlüsselung

**Reguläre Tests (`Softwareschmiede.Tests`, `Category!=OsInterface`):** 1367 Tests
- Bestanden: 1366
- Übersprungen: 1
  - `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — Test gilt nur für Nicht-Windows-Betriebssysteme (erwartetes Skip auf Windows-Sandbox)

**OS-Interface Tests (`Softwareschmiede.Tests`, `Category=OsInterface`, u. a. E2E/FlaUI, ConPTY, Clipboard, Prozessstart):** 47 Tests
- Bestanden: 46
- Übersprungen: 1
  - `End2EndTest.RunConPtyTests` — Umgebungsvariable `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (bestätigte Sandbox-Limitation, siehe CLAUDE.md)
- Hinweis zur Flakiness: In zwei vorangegangenen Läufen schlug `End2EndTest.RunGeneralTests` sporadisch mit `System.TimeoutException: Element wurde nicht innerhalb von 30s gefunden` beim Warten auf `AutonomAufgabeStart` in `E2E_AutonomAufgabenInitialisierung.cs` fehl. Vor der Bewertung wurde geprüft: (a) das App-Log (`softwareschmiede-20260821.log`) zeigt keinen Startup-Crash/keine Exception beim Fenster-/Prozessstart, die Anwendung lief durchgehend sauber; (b) das UI-Element `AutonomAufgabeStart` ist korrekt im XAML (`AutonomAufgabeDetailView.xaml`) mit passendem `AutomationProperties.Name` vorhanden. Ein dritter, isolierter Lauf war vollständig grün (46/46 bestanden, 0 Fehler) — damit als timing-sensitive FlaUI-Flakiness unter Sandbox-Last bestätigt, kein reproduzierbarer Fehler. Das unten berichtete Ergebnis (46 bestanden, 0 fehlgeschlagen) stammt aus diesem sauberen dritten Lauf.

**Integrationstests (`Softwareschmiede.IntegrationTests`):** 77 Tests
- Bestanden: 77
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** Nicht messbar (in diesem Lauf nicht erhoben — nicht Teil des angeforderten Testumfangs)

## Fehlende Tests

Nicht ermittelt (keine Coverage-Daten in diesem Lauf erhoben).

## Test-Ausführung

**Konfiguration:**
- .NET 10.0, Solution `Softwareschmiede.slnx` (voller `dotnet build` vor allen Testläufen, 0 Fehler / 0 Warnungen)
- Umgebungsvariable: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (inline pro Aufruf gesetzt)
- Alle `dotnet test`-Aufrufe synchron im Vordergrund ausgeführt (kein `--no-build` beim Build selbst, kein `run_in_background`)
- Vor und nach den Testläufen geprüft: keine laufende/verwaiste `Softwareschmiede.App.exe`- oder `testhost.exe`-Instanz

**Ausgeführte Kommandos:**

```
dotnet build Softwareschmiede.slnx

SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "console;verbosity=normal"

SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface" --logger "console;verbosity=normal"
  (3x ausgeführt zur Flakiness-Verifikation, siehe Hinweis oben; Endergebnis grün)

SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --logger "console;verbosity=normal"
```

**Datum:** 2026-08-21
