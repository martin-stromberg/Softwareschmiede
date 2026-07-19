# Tests

Übersicht bestehender Tests und Infrastruktur für die Anforderung.

## Bestehende Unit-Tests

### `TaskDetailViewModelTests`

Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

**Zweck:** Unit-Tests für TaskDetailViewModel

**Testinfrastruktur:**
- Verwendet `TestDbContextFactory.Create()` für Test-Datenbank
- Services werden instanziiert mit Test-Doubles (Mocks):
  - `IDialogService` (Moq.Mock)
  - `IKiPlugin` (Moq.Mock)
- Nutzt `NullLogger` für Logging in Tests

**Bestehende Testfälle (Auszug):**
- Tests für Dialog-Service Integration
- Tests für KI-Plugin-Auflösung
- Tests für Task-Status-Übergänge

**Relevanz für neue Anforderung:**
- ViewModel-Test-Pattern etabliert
- Dependency-Injection Testing vorhanden
- Command-Testing möglich (AsyncRelayCommand, RelayCommand)
- Property-Change-Signalisierung testbar

---

### `TaskDetailViewModelTests_ZeitgesteuerterPrompt`

Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`

**Zweck:** Spezialisierte Tests für zeitgesteuerten Prompt-Versand

**Testinfrastruktur:** Ähnlich wie oben, Test-klasse-Splitting nach Thema etabliert

---

### `FileExplorerViewModelTests`

Datei: `src/Softwareschmiede.Tests/App/ViewModels/FileExplorerViewModelTests.cs`

**Zweck:** Unit-Tests für FileExplorerViewModel

**Getestete Funktionalität (Auszug):**
- `AktuellerModus` Wechsel zwischen Standard und Vergleich
- Command-Ausführung (StandardAnsichtCommand, VergleichCommand)

**Relevanz für neue Anforderung:**
- FileExplorer-ViewModel Testmuster vorhanden
- Service-Mocking für IGitWorkspaceBrowserService, ITextDiffService
- Observable Collection Testing

---

## Fehlende Tests

### Unit-Tests für TaskDetailViewModel-Sichtbarkeitseigenschaften

**Status:** NICHT VORHANDEN (gemäß Anforderung)

**Erforderliche Tests (aus Anforderung Absatz 8):**
- Binding zwischen `ShowCliPanel` und CLI-Ribbon-Sichtbarkeit
- Binding zwischen `ShowFileExplorerPanel` und Dateien-Ribbon-Sichtbarkeit
- Tests für neue Properties: `ShowFileSystemGroup` (optional), `SolutionFileExists`
- Tests für `OeffneArbeitsverzeichnisCommand.CanExecute` (prüft ob Verzeichnis existiert)
- Tests für `OeffneIdeCommand.CanExecute` (prüft ob Solution existiert)

---

### E2E-Tests für neue Buttons

**Status:** NICHT VORHANDEN (gemäß Anforderung)

**Erforderliche E2E-Tests (aus Anforderung Absatz 8):**
- Test für "Arbeitsverzeichnis öffnen"-Button
  - Prüft ob OS-Dateiexplorer gestartet wird mit korrektem Pfad
  - Plattformabhängig (Windows: explorer.exe, Linux: xdg-open, macOS: open)
- Test für "IDE öffnen"-Button
  - Prüft ob Visual Studio (oder konfigurierte IDE) gestartet wird
  - Prüft ob Solution-Datei übergeben wird
  - Fehlerfall: Keine Solution gefunden

**Herausforderungen bei E2E-Tests:**
- Abhängigkeit von installierter IDE (Visual Studio)
- OS-Dateiexplorer Fenster-Management
- Requires `Category=OsInterface` Markierung (gemäß CLAUDE.md)
- Müssen möglicherweise mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gekennzeichnet werden

---

## Test-Hilfsmethoden / Test-Doubles

### Service Factories

**Bestehende Factories:**
- `TestDbContextFactory` - Erstellt Test-Datenbank
- `TestKiAusfuehrungsServiceFactory` - Erstellt Test-Instanz von KiAusfuehrungsService

**Neue Factories erforderlich:**
- `TestWorkspaceExplorerServiceFactory` - Mock für neue WorkspaceExplorerService
- `TestIdeServiceFactory` - Mock für neue IdeService
- `TestProcessStarterFactory` - Mock für Process-Starter

---

### Testkonventionen

**Aus bestehendem Code erkannt:**
- Naming: `{ClassUnderTest}Tests` und `{ClassUnderTest}Tests_{Spezialthema}`
- Setup im Constructor (TestDbContextFactory, Service-Instanziierung)
- Disposal via `IDisposable` Pattern
- Assertion Library: FluentAssertions (`Should()` Syntax)
- Mocking: Moq

---

## Test-Abdeckung Status

| Komponente | Unit-Tests | E2E-Tests | Status |
|---|---|---|---|
| TaskDetailViewModel (bestehend) | ✓ Vorhanden | ✗ Keine | Bestehend |
| FileExplorerViewModel (bestehend) | ✓ Vorhanden | ✗ Keine | Bestehend |
| Ribbon-Commands (bestehend) | ✓ Teilweise (via ViewModel) | ✗ Keine | Bestehend |
| WorkspaceExplorerService (NEU) | ✗ Nicht vorhanden | ✗ Nicht vorhanden | **Erforderlich** |
| IdeService (NEU) | ✗ Nicht vorhanden | ✗ Nicht vorhanden | **Erforderlich** |
| Neue TaskDetailViewModel-Commands | ✗ Nicht vorhanden | ✗ Nicht vorhanden | **Erforderlich** |

---

## Zusammenfassung für Testabdeckung

**Bestehende Infrastruktur:**
- Unit-Test-Framework etabliert (xUnit, FluentAssertions, Moq)
- E2E-Test-Framework etabliert (`Category=OsInterface` Markierung)
- Test-Klasse-Splitting nach Thema Konvention etabliert
- Dependency-Injection Testing Pattern vorhanden

**Erforderliche Tests (aus Anforderung):**
1. Unit-Tests für Sichtbarkeitsbindungen (TaskDetailViewModel Properties)
2. Unit-Tests für neue Commands (OeffneArbeitsverzeichnisCommand, OeffneIdeCommand)
3. Unit-Tests für WorkspaceExplorerService
4. Unit-Tests für IdeService
5. E2E-Tests für "Arbeitsverzeichnis öffnen" Button
6. E2E-Tests für "IDE öffnen" Button
