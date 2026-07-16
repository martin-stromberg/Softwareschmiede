# Test-Infrastruktur

## Testprojekt

### `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`

**Target:** `.net10.0-windows10.0.17763.0` (WPF-kompatibel, Windows-only)

**Status:** Zentrales Testprojekt für alle Testtypen

## Test-Framework und Tools

| Tool | Version | Zweck |
|------|---------|-------|
| **xunit** | 2.9.3 | Test-Framework (Basis) |
| **xunit.runner.visualstudio** | 3.1.5 | Visual Studio Test-Explorer-Integration |
| **Xunit.SkippableFact** | 1.5.23 | Bedingte Test-Übersprungung |
| **Microsoft.NET.Test.Sdk** | 18.7.0 | .NET Test-SDK (Test-Discovery, -Execution) |
| **bunit** | 2.7.2 | Blazor-Komponenten-Tests (Server-Side) |
| **FlaUI.Core** | 5.0.0 | UI-Automatisierung (Basis) |
| **FlaUI.UIA3** | 5.0.0 | UI-Automatisierung (Windows UIA3-Provider) |
| **FluentAssertions** | 8.10.0 | Fluente Assertion-Syntax |
| **Moq** | 4.* | Mocking-Framework |
| **coverlet.collector** | 10.0.1 | Code-Coverage-Berichterstellung |
| **Microsoft.EntityFrameworkCore.InMemory** | 10.0.9 | In-Memory-Datenbank für Tests |
| **Microsoft.Extensions.TimeProvider.Testing** | 10.7.0 | Fakeable `TimeProvider` für deterministische Zeit-Tests |
| **Microsoft.Extensions.Logging.Abstractions** | 10.0.9 | Logging für Tests |

## Test-Kategorien

### ✅ Unit-Tests
- **Klassifizierung:** Keine Kategorie-Einschränkung (Standard)
- **Beispiele:** Domain-Layer (Geschäftslogik), Service-Layer (Business-Regeln)
- **Framework:** xunit + Moq
- **Bedingung:** Läuft in CI/CD

### ✅ Integration-Tests
- **Klassifizierung:** Keine Kategorie-Einschränkung (Standard)
- **Beispiele:** DbContext-Tests mit In-Memory-DB, Service-Integration
- **Framework:** xunit + Microsoft.EntityFrameworkCore.InMemory
- **Bedingung:** Läuft in CI/CD

### ✅ BUnit-Tests
- **Klassifizierung:** Keine Kategorie-Einschränkung (Standard)
- **Beispiele:** Blazor-Serverkomponenten-Tests
- **Framework:** bunit
- **Bedingung:** Läuft in CI/CD

### 🚫 E2E-Tests (WPF/FlaUI)
- **Klassifizierung:** `Category="E2E"`
- **Beispiele:** WPF-Desktopanwendung, Fenster-Interaktion, UI-Workflows
- **Framework:** FlaUI (UIA3)
- **Bedingung:** **Ausgeschlossen aus CI/CD** (läuft nur lokal)
- **Grund:** GitHub-hosted Runner bieten keine verlässliche interaktive Desktop-Session

### 🚫 ConPTY-Tests
- **Klassifizierung:** `Category="ConPTY"`
- **Beispiele:** Pseudo-Konsolen-Tests (Windows ConAPI)
- **Framework:** xunit mit ConPTY-Child-Prozess-Integration
- **Bedingung:** **Ausgeschlossen aus CI/CD** (läuft nur lokal)
- **Grund:** ConPTY-Child-Prozess wird im GitHub-Runner nicht isoliert zur Pseudo-Konsole
- **Workaround:** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` setzt Tests auf "Skipped" statt Fehler

## CI/CD Test-Ausführung

### `.github/workflows/test.yml`

**Trigger:** `push` auf `main`, `pull_request` zu `main` (verhindert Doppellauf)

**Test-Befehl:**
```bash
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj \
  --no-build \
  -c Debug \
  --filter "Category!=E2E&Category!=ConPTY" \
  --logger "trx;LogFileName=test-results.trx" \
  --logger "console;verbosity=normal"
```

**Effekt:**
- Lädt nur Tests ohne E2E/ConPTY-Kategorien
- Generiert TRX-Test-Report für Archivierung
- Console-Output für Live-Feedback

**Artifacts:**
- Test-Results: `src/Softwareschmiede.Tests/TestResults/*.trx`
- Retention: 14 Tage

## Lokale Test-Ausführung

### Für Entwickler

**Alle Tests (mit E2E/ConPTY):**
```bash
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug
```

**Ohne E2E/ConPTY (wie CI/CD):**
```bash
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj \
  --filter "Category!=E2E&Category!=ConPTY" \
  -c Debug
```

**Nur E2E-Tests:**
```bash
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj \
  --filter "Category=E2E" \
  -c Debug
```

**Mit Coverage:**
```bash
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj \
  -c Debug \
  /p:CollectCoverage=true \
  /p:CoverageFormat=lcov
```

## Build vor Test

### Erforderlich

**CLAUDE.md-Vorgabe:** Immer `dotnet build` VOR `dotnet test`

**Implementierung:** `.claude/hooks/build_before_test.py` (PreToolUse-Hook)

**Details:**
- Warnt vor laufender `Softwareschmiede.App.exe` (kann DLL-Lock verursachen)
- Koordiniert mit Stop-Hook via Cross-Process-Lock (`dotnet_lock.py`)
- Build blockiert Test-Start bis Freigabe durch PostToolUse-Hook (`release_build_lock.py`)

## Testflakineness und Bekannte Probleme

### ConPTY-Test-Skip im CI/CD
- **Problem:** Windows ConPTY-Child-Prozess wird im GitHub-hosted Runner nicht isoliert
- **Lösung:** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` beim Test-Run gesetzt
- **Effekt:** Tests mit `Category="ConPTY"` berichten als "Skipped" statt Fehler

### E2E-Test-Timeouts
- **Problem:** Gelegentliche Timeouts bei Fenster-Erscheinungs-Warteläufen
- **Grund:** GitHub-Runner ohne verlässliche interaktive Desktop-Session
- **Lösung:** Tests nur lokal ausführen, CI/CD schließt sie aus

### UI-Element-Nicht-Gefunden-Fehler
- **Debugging:** App-Logdatei unter `src/Softwareschmiede.App/bin/<Config>/<TargetFramework>/logs/` überprüfen
- **Häufige Root Causes:** 
  - App-Startup-Exception (XamlParseException, Fehler beim Ressourcen-Laden)
  - Race Condition bei schnellem Fenster-Navigation

## Testdaten und Fixtures

### In-Memory Datenbank
- `Microsoft.EntityFrameworkCore.InMemory` für Unit-/Integration-Tests
- Keine realen Datenbankverbindungen erforderlich

### Fake TimeProvider
- `Microsoft.Extensions.TimeProvider.Testing` für zeitgesteuerte Tests
- Deterministische Timer-Tests ohne `Thread.Sleep`

### Mocking
- Moq für Service-Dependencies
- Ermöglicht isolierte Unit-Tests

## Test-Konfiguration per Test-Projekt

### Fest definiert in `.csproj`
```xml
<ItemGroup>
  <Using Include="Xunit" />
</ItemGroup>
```

- Automatische `using Xunit;` in allen `.cs`-Dateien (Global Using)
- Reduziert Boilerplate

## Offene Punkte für öffentliche Veröffentlichung

1. **Test-Dokumentation:**
   - Wie können externe Contributors Tests schreiben?
   - Test-Konventionen (Naming, Struktur, Kategorien) dokumentieren

2. **Test-Kategorien erweitern:**
   - Sollten neue Kategorien definiert werden (z. B. `Slow`, `Integration`)?
   - Oder reicht aktuelles E2E/ConPTY-Schema?

3. **Code-Coverage-Ziele:**
   - Sind Coverage-Schwellenwerte definiert?
   - Sollten Coverage-Reports automatisch generiert werden?

4. **Test-Performance:**
   - Gesamtlaufzeit der Test-Suite dokumentieren
   - Slow-Test-Identifikation

5. **ConPTY-Test-Alternative:**
   - Sollte eine echte ConPTY-Umgebung im Docker/GitHub-hosted Runner emuliert werden?
   - Oder ist Skip-Status akzeptabel für öffentliche Releases?

## Checkliste vor öffentliche Veröffentlichung

- ✅ Alle CI/CD-Tests müssen grün sein
- ✅ E2E-Tests lokal validiert
- ✅ Code-Coverage-Report erstellt (optional)
- ✅ Bekannte flaky Tests dokumentiert
- ⚠️ Testdaten auf PII überprüfen (noch ausstehend)
