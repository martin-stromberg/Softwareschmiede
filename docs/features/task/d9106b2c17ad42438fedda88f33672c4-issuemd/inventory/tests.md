# Tests - Bestandsaufnahme

## Testklassen

### `EntwicklungsprozessServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

Testklasse mit `IDisposable`-Implementierung für Ressourcen-Management.

**Setup:**
- Erstellt `SoftwareschmiededDbContext` via `TestDbContextFactory.Create()`
- Initialisiert Mock-Objekte für: `IGitPlugin`, `IKiPlugin`, `IArbeitsverzeichnisResolver`
- Erstellt `EntwicklungsprozessService` mit Test-Abhängigkeiten

**Test-Methoden (relevant für `ProzessStartenAsync` Integration):**

| Test-Methode | Beschreibung |
|-------------|-------------|
| `ProzessStartenAsync_ShouldCloneAndCreateBranch_WhenAufgabeExists` | Verifiziert Klon und Branch-Erstellung; Status wird auf `Gestartet` gesetzt |
| `ProzessStartenUndCliStartenAsync_Success` | Kombinierter Start mit KI-Plugin-Ausführung |
| `ProzessStartenUndCliStartenAsync_RepositoryCloneFails_RollbackStatus` | Fehlerbehandlung bei Clone-Fehler |
| `ProzessStartenUndCliStartenAsync_CliStartFails_RollbackStatus` | Fehlerbehandlung bei CLI-Start-Fehler |
| `ProzessStartenAsync_ShouldContinue_WhenRepositoryStartScriptFails` | Graceful Degradation bei Startskript-Fehler |
| `ProzessStartenAsync_ShouldCreateIssueBranch_WhenAufgabeHasIssueReference` | Branch-Naming mit Issue-Referenz |
| `ProzessStartenAsync_ShouldThrowInvalidOperationException_WhenAufgabeDoesNotExist` | Exception bei fehlender Aufgabe |
| `AbschliessenAsync_ShouldSetStatusAbgeschlossenAndAddProtokoll_WhenAufgabeExists` | Status-Übergang und Protokoll-Eintrag |
| `CommitDurchfuehrenAsync_ShouldThrowInvalidOperationException_WhenNoKlonPfad` | Exception bei fehlendem Klonpfad |
| `PushDurchfuehrenAsync_ShouldThrowInvalidOperationException_WhenNoKlonPfad` | Exception bei fehlendem Klonpfad |
| `ProzessStartenAsync_ShouldUseConfiguredWorkdirBase_ForClonePath` | Konfiguriertes Arbeitsverzeichnis wird verwendet |
| `ProzessStartenAsync_ShouldUseFallbackPath_WhenResolverReturnsFallback` | Fallback-Pfad wird verwendet |
| `ProzessStartenAsync_ShouldCheckoutExistingBranch_WhenBasisBranchIsNotDefault` | Checkout eines bestehenden Branches |
| `ProzessStartenAsync_ShouldCreateTaskBranch_WhenBasisBranchEqualsDefaultBranch_CaseInsensitive` | Case-insensitive Vergleich |
| `ProzessStartenAsync_ShouldDeleteExistingCloneDirectory_BeforeClone` | Löschen existierender Klone (auch Read-Only-Dateien) |
| `ProzessStartenAsync_ShouldThrow_WhenRepositoryContextIsAmbiguous` | Exception bei mehrdeutigem Repository-Kontext |
| `GetRemoteBranchesAsync_ShouldResolvePluginBySelectedPrefix_AndReturnPluginBranches` | Plugin-Auswahl nach Prefix |

### `EntwicklungsprozessServiceTests` (Integration Tests)
Datei: `src/Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs`

Zusätzliche Integration Tests (nicht vollständig analysiert in dieser Bestandsaufnahme).

## Hilfsmethoden und Test-Utilities

### In `EntwicklungsprozessServiceTests`

| Hilfsmethode | Zweck |
|-------------|------|
| `CreatePluginSelectionService(params IKiPlugin[] kiPlugins)` | Erstellt einen `PluginSelectionService` mit Mock-Plugin-Manager für Tests |
| `Dispose()` | Räumt auf: `_kiAusfuehrungsService.Dispose()`, `_db.Dispose()` |

### Verwendete Test-Helfer

| Klasse | Zweck |
|--------|--------|
| `TestDbContextFactory` | Erstellt In-Memory-Datenbank für Tests |
| `Mock<T>` (Moq) | Mocking-Framework für Abhängigkeiten |
| `FluentAssertions` | Assertions für lesbare Test-Statements |

## Test-Patterns

### Arrange-Act-Assert (AAA)

Alle Test-Methoden folgen dem AAA-Pattern:
1. **Arrange**: Aufgabe erstellen, Mocks konfigurieren
2. **Act**: Methode aufrufen
3. **Assert**: Verhalten verifizieren (Status, Mock-Aufrufe, Exceptions)

### Mock-Setup-Beispiele

```csharp
_gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
```

### Direktes Verzeichnis-Management

Tests erstellen teilweise echte Verzeichnisse für Validierung (z.B. `ProzessStartenAsync_ShouldDeleteExistingCloneDirectory_BeforeClone`):
- Verzeichnisse werden erstellt mit `Directory.CreateDirectory()`
- Read-Only-Dateien werden mit `File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly)` erstellt
- Validierung, dass `DeleteDirectoryForce` diese korrekt löscht

## Abhängigkeiten für neue Tests

Für Tests der neuen `CreateIssueFileAsync` und `UpdateGitignoreAsync` Methoden werden benötigt:
- Möglichkeit, echte Dateisystem-Operationen zu testen (möglicherweise eigenes Temp-Verzeichnis)
- File I/O Mocks oder echte Datei-Validierung
- Mock-Aufrufe zu `File.WriteAllLinesAsync` / `File.ReadAllLinesAsync` (falls indirekt getestet) oder echte Dateien
