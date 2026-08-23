# Tests

## Testklassen

### `RepositoryStartskriptServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/RepositoryStartskriptServiceTests.cs`

**Testmethoden:**

| Testmethode | Zweck |
|-------------|-------|
| `RunAsync_ShouldSkipExecution_WhenConfigurationIsInactive` | Verifiziiert, dass `RunAsync()` nicht aufgerufen wird, wenn Konfiguration `Aktiv = false` |
| `RunAsync_ShouldThrow_WhenScriptPathEscapesRepositoryRoot` | Testet Path-Traversal-Sicherheit: muss Exception werfen wenn Pfad außerhalb Repository liegt |
| `RunAsync_ShouldPassOnlyScriptArgumentsWithoutPortContract_WhenScriptExecutionSucceeds` | Verifiziert korrekte Argumente-Übergabe an `ICliRunner` (PowerShell-Parameter, Skriptpfad) |
| `RunAsync_ShouldThrow_WhenCliExecutionFails` | Testet Exception-Throwing bei fehlgeschlagener Ausführung |

**Hilfsmethoden:**

| Hilfsmethode | Zweck |
|-------------|-------|
| `CreateSut()` | Erstellt Service-Instance mit Mock `ICliRunner` |
| `CreateConfig()` | Erstellt `RepositoryStartKonfiguration` mit Standard-Werten |
| `CreateScript(relativePath)` | Erstellt temporäre Test-Skriptdatei im Repository-Verzeichnis |

**Setup/Teardown:**
- Constructor: Erstellt temporäres Repository-Verzeichnis in `Path.GetTempPath()`
- `Dispose()`: Löscht temporäres Repository-Verzeichnis

**Mocks:**
- `Mock<ICliRunner>` — wird für Assertions auf Aufrufe geprüft

**Bemerkungen:**
- Bildet das Vorbild-Testmuster für `RepositoryInitialisierungServiceTests`
- Testet kritische Aspekte: Inaktivierung, Sicherheit, Fehlerbehandlung
- Verwendet `FluentAssertions` für Assertions
- Nutzt `Path.Combine()` und `Path.GetRelativePath()` für plattformunabhängige Pfade

---

### `EntwicklungsprozessServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

**Relevanz zur Anforderung:**
- Testet integriertes Startskript-Ausführungs-Verhalten im Klon-Lifecycle
- Wird als Vorlage für Tests zur Initialisierungsskript-Integration verwendet

**Bemerkungen:**
- Testet Klon + Branch + Startskript-Ausführung in realem Szenario
- Fehlertoleranz-Tests für Startskript-Fehler vorhanden

---

## Hilfsmethoden und Utilities

### `WorkingDirectoryResolver`

Datei: `src/Softwareschmiede/Application/Services/WorkingDirectoryResolver.cs` (implizit über EntwicklungsprozessService)

- Wird zur Auflösung effektiven Arbeitsverzeichnisses verwendet
- Berücksichtigt `RepositoryStartKonfiguration.WorkingDirectoryRelativePath`

---

## Fehlende Tests

Die folgenden Tests sind noch nicht implementiert und müssen hinzugefügt werden:

| Test | Klasse | Zweck |
|------|--------|-------|
| `RunAsync_ShouldSucceed_WhenInitializationScriptExecutes` | `RepositoryInitialisierungServiceTests` | Erfolgreiche Ausführung eines Initialisierungsskripts |
| `RunAsync_ShouldLogWarning_WhenInitializationScriptFails` | `RepositoryInitialisierungServiceTests` | Fehler werden geloggt, nicht geworfen |
| `RunAsync_ShouldThrow_WhenPathTraversalAttempted` | `RepositoryInitialisierungServiceTests` | Sicherheit gegen Path-Traversal |
| `RunAsync_ShouldSkipExecution_WhenConfigurationIsInactive` | `RepositoryInitialisierungServiceTests` | Inaktive Konfiguration wird übersprungen |
| `RunAsync_ShouldThrow_WhenScriptFileNotFound` | `RepositoryInitialisierungServiceTests` | Fehler wenn Skriptdatei nicht existiert |
| `ProzessStartenAsync_ShouldExecuteInitializationScript_AfterClone` | `EntwicklungsprozessServiceTests` | Integration von Initialisierungsskript nach Klon |
| `ProzessStartenAsync_ShouldNotBlockTask_WhenInitializationScriptFails` | `EntwicklungsprozessServiceTests` | Fehlertoleranz: Aufgabe wird trotz Fehler nicht blockiert |
| `InitialisierungsskriptSuggestionen_ShouldBeFetchedFromRemote` | `ProjectDetailViewModelTests` | ViewModel lädt Vorschläge korrekt |
| `SaveInitialisierungsskriptAsync_ShouldPersist_SelectedScript` | `ProjectDetailViewModelTests` | Manuelle Überschreibung wird persistiert |

