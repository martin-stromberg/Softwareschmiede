# Test Coverage Plan

## Goal
Schließen der wichtigsten Testlücken aus `test-coverage-gaps.md` in der empfohlenen Reihenfolge.

## P0 — Core infrastructure

### `PluginSettingsService`
- Unit-Tests mit `ICredentialStore`-Mock
- Assertions auf exakte Schlüsselbildung
- Table-driven Tests für `HasValue`

### `CliRunner`
- Fokus auf `RunAsync` und `StreamAsync`
- `ProcessStartInfo`-verhalten indirekt über Test-Executable prüfen
- Abdeckung für:
  - Argumente
  - Env-Variablen
  - stdout/stderr
  - Exit-Code
  - Cancellation/Cleanup

### `SystemShutdownService`
- OS-abhängige Kommandos über abstrahierte Plattformtests absichern
- Non-zero exit und `Process.Start`-Fehler prüfen

## P1 — Critical UI flows

### `ProjektDetail`
- Component-Tests mit Mock-Services
- Separate Tests für:
  - Laden
  - Archivieren
  - Update/Delete
  - Repository-Form
  - Plugin-Fallback
  - Feldvalidierung

### `NeueAufgabe`
- Initialzustand und Repository-Vorbelegung
- Issue-Auswahl
- Create-Flow mit und ohne Issue
- Navigation

### `ProjektListe`
- Initialisierung
- Create-Flow
- Cancel-Flow
- Navigation

### `Home`
- Kennzahlen
- aktive Aufgaben
- Navigation

## P2 — Secondary infrastructure / complex page

### `WindowsCredentialStore`
- Roundtrip-Tests nur auf Windows
- Fehlerpfade separat absichern

### `AgentenpaketeSeite`
- Tree-Model mit Fake-Dateiservice
- CRUD-Aktionen in isolierten Tests
- Markdown-Preview und Upload

### `AufgabeDetail`
- Kritische Aktionen in einzelne Tests zerlegen
- Vor allem Statuswechsel, Folgeprompt und Fehlerbehandlung

## Suggested implementation sequence
1. `PluginSettingsServiceTests`
2. `SystemShutdownServiceTests`
3. `CliRunnerTests`
4. `ProjektDetail` component tests
5. `NeueAufgabe` component tests
6. `ProjektListe` / `Home`
7. `WindowsCredentialStoreTests`
8. `AgentenpaketeSeite` and remaining `AufgabeDetail` coverage

## Definition of done
- Each gap file has at least one matching test file or an explicit rationale why it stays untested.
- Public service methods have unit tests for happy path and failure path.
- Critical pages have one component test per user-visible action.
