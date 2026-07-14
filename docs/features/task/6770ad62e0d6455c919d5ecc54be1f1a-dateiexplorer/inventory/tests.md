# Tests

## Bestehende Testklassen

### Diff-Service-Tests
Datei: `src/Softwareschmiede.Tests/Application/Services/DiffServiceTests.cs`
- Tests für DiffService-Funktionalität
- Abdeckung: Generierung, Caching, Persistierung

Datei: `src/Softwareschmiede.Tests/Application/Services/DiffAlgorithmServiceTests.cs`
- Tests für Diff-Algorithmen
- Abdeckung: Myers Diff-Berechnung

Datei: `src/Softwareschmiede.Tests/Application/Services/DiffCachingServiceTests.cs`
- Tests für Diff-Caching-Logik
- Abdeckung: Cache-Speicherung, -Abruf, -Invalidierung

### Directory-Structure-Tests
Datei: `src/Softwareschmiede.Tests/Application/Services/DirectoryStructureBrowserServiceTests.cs`
- Tests für Verzeichnisstruktur-Laden
- Abdeckung: Repository-Strukturen, Caching, Error-Handling

### TaskDetailViewModel-Tests
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- Tests für TaskDetailViewModel-Funktionalität
- Abdeckung: Laden, Ansichtswechsel, Command-Execution

Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- Tests für zeitgesteuerte Prompt-Versendung
- Abdeckung: Scheduling, Status-Updates

### Hilfsklassen für TaskDetailViewModel-Tests
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- Factory-Methoden zur Erstellung von Test-Instanzen
- Methoden: Instanziierung mit Mock-Dependencies

## Noch nicht vorhanden (für Dateiexplorer):

### `DateibrowserServiceTests`
Benötigt für:
- `LoadDirectoryStructureAsync` — Laden lokaler Verzeichnisstrukturen
- `LoadFileContentAsync` — Dateiinhalt laden
- `LoadChangedFilesAsync` — Geänderte Dateien ermitteln
- `InvalidateCacheAsync` — Cache-Invalidierung
- Edge-Cases: leere Repos, Binärdateien, große Dateien, Fehlerhafte Pfade

### `GitDiffParserServiceTests`
Benötigt für:
- `ParseGitDiffOutputAsync` — Parsing von `git diff`-Output
- `ExtractFileChangesAsync` — Parsing von `git diff --name-status`
- Korrekte Gruppierung nach Commits
- Edge-Cases: Mehrere Commits, Umbenennungen, gelöschte Dateien

### `TaskDetailViewModelTests` (Erweiterung)
Benötigt für:
- `DateibrowserModus` Enum-Wert in `DetailAnsicht`
- `AktuellerDateibrowserModus` Property
- `AusgewaehlteDatui` Property
- `DateiInhalt` Property
- `StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand`
- Modus-Umschaltung
- Dateiauswahl und Inhalts-Laden

### UI/Integration Tests (optional)
Benötigt für:
- `FileExplorerView` UserControl — Baum-Rendering, Dateiauswahl, Modus-Umschaltung
- `DiffViewer` UserControl — Diff-Rendering, Zeilennummern, Farbgebung
- E2E-Tests für Navigations-Szenarien (z. B. Dateibaum → Auswahl → Diff-Anzeige)

## Bestehende Test-Hilfsmethoden und Patterns

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`
- Erstellt In-Memory-Test-Datenbanken
- Wird von Service-Tests verwendet

### Test-Patterns
- **Async Tests:** Alle Async-Methoden verwenden `async Task` und `CancellationToken`
- **Mock-Dependencies:** Moq/NSubstitute für Service-Mocking
- **xUnit:** Test-Framework mit `[Fact]` und `[Theory]` Attributes
- **Assertions:** FluentAssertions für lesbare Assertions

### Bekannte Einschränkungen
- **SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS:** ConPTY-Tests müssen mit dieser Env-Var übersprungen werden in der Sandbox (siehe CLAUDE.md)
- **WPF E2E Tests:** Können in der Sandbox zu "Element not found" Timeouts führen; check App-Log unter `src/Softwareschmiede.App/bin/<Config>/<TargetFramework>/logs/softwareschmiede-*.log`
