# Bestandsaufnahme: Tests

## Testklassen

### `ProjektServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProjektServiceTests.cs`

Umfangreiche Test-Suite für `ProjektService` mit 24 Test-Methoden:

#### Repository-bezogene Tests:
- `AddRepositoryAsync_ShouldAddRepository_WhenProjektExists` — Validiert Hinzufügen von Repository
- `AddRepositoryAsync_ShouldUseSourceDirectoryForLocalDirectoryPlugin_WhenFieldValuesAreValid` — LocalDirectory-Plugin-Unterstützung
- `AddRepositoryAsync_ShouldThrow_WhenSourceDirectoryIsMissingForLocalDirectoryPlugin` — Validierung Pflichtfeld
- `AddRepositoryAsync_ShouldMapRepositoryUrlToSourceDirectory_WhenUsingStringOverloadForLocalDirectoryPlugin` — String-Overload Mapping
- `AddRepositoryAsync_ShouldHandleTrimAndTrailingSeparator_ForLocalDirectorySourceDirectory` — Normalisierung
- `AddRepositoryAsync_ShouldThrow_WhenRepositoryUrlIsMissingForGitHubPlugin` — GitHub-Plugin Validierung
- `AddRepositoryAsync_ShouldThrow_WhenRepositoryNameIsMissingForGitHubPlugin` — GitHub-Plugin Validierung
- `AddRepositoryAsync_ShouldDeriveRepositoryName_FromRepositoryUrl_WhenNameMissing_ForNonGitHubPlugin` — Name-Ableitung
- `AddRepositoryAsync_ShouldDeriveRepositoryName_FromLocalPath_WhenNameMissing_ForNonGitHubPlugin` — Lokale Pfad-Ableitung
- `AddRepositoryAsync_ShouldThrow_WhenDerivedRepositoryNameIsEmpty_ForNonGitHubPlugin` — Fehlerfall
- `AddRepositoryAsync_ShouldPreferExplicitRepositoryName_OverDerivedName_ForNonGitHubPlugin` — Präferenz-Logik
- `RemoveRepositoryAsync_ShouldRemoveRepository_WhenRepositoryExists` — Repository-Entfernung

#### Projekt-basierte Tests (relevant für Repository-Kontext):
- `CreateAsync` Tests — Projekt-Erstellung
- `GetAllAsync`, `GetByIdAsync`, `GetDetailAsync` — Projekt-Abruf (mit `GetDetailAsync` inkl. Repositories)
- `UpdateAsync`, `ArchivierenAsync`, `DeleteAsync` — Projekt-Verwaltung

#### Startkonfiguration Tests:
- `SaveRepositoryStartKonfigurationAsync_ShouldCreateConfiguration_WhenRepositoryExists`
- `SaveRepositoryStartKonfigurationAsync_ShouldUpdateExistingConfiguration_WhenAlreadyPresent`
- `GetRepositoryStartKonfigurationAsync_ShouldReturnNull_WhenNoConfigurationExists`
- `SaveRepositoryStartKonfigurationAsync_ShouldThrow_WhenScriptPathIsAbsolute`

### `PluginManagerTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`

Tests für Plugin-Discovery und -Verwaltung (genaue Details nicht ausgelesen, aber Datei existiert).

## Hilfsmethoden und Test-Infrastruktur

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs` (impliziert)

- Wird in `ProjektServiceTests.ctor` verwendet
- Methode: `TestDbContextFactory.Create()` — erstellt In-Memory-Datenbankkontext für Tests

### Test-Setup in ProjektServiceTests:
```csharp
public ProjektServiceTests()
{
    _db = TestDbContextFactory.Create();
    _loggerMock = new Mock<ILogger<ProjektService>>();
    _sut = new ProjektService(_db, _loggerMock.Object);
}
```

- Nutzt `Moq` für Logger-Mock
- Implementiert `IDisposable` zum Cleanup

## Fehlende Tests

**Keine Tests für `RepositoryAssignViewModel`** existieren derzeit:
- Laden von Repositories via `LadenAsync()`
- Plugin-Auswahl und deren Auswirkungen (noch nicht implementiert)
- IsLoading-Flag-Behavior
- BestaetigenCommand und AbbrechenCommand Behavior
- CloseRequested Event-Auslösung

## Test-Framework und Dependencies

- **XUnit** — Test-Framework
- **FluentAssertions** — Assertion-Library (Method Chaining)
- **Moq** — Mocking-Framework
- **Microsoft.EntityFrameworkCore** — EF Core mit InMemory für Tests

## Hinweise

- `ProjektService` hat umfangreiche Test-Abdeckung für Repository-Operationen
- `PluginManager` Tests existieren, aber Details nicht in dieser Bestandsaufnahme dokumentiert
- Die Test-Infrastruktur ist gut strukturiert und einsatzbereit für neue Tests
- `RepositoryAssignViewModel` benötigt Test-Abdeckung als nächsten Schritt
