# Tests

## Unit Tests

### `AgentPackageReaderTests`

Datei: `src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs`

Unit-Tests für `AgentPackageReader`. Verwendet Mock-Umgebung mit temporärem Dateisystem.

| Testmethode | Getestet | Beschreibung |
|-----------|----------|-------------|
| `GetPackagesAsync_ShouldReturnEmpty_WhenNoPackagesExist` | `GetPackagesAsync()` | Gibt leere Liste zurück, wenn keine Paket-Verzeichnisse vorhanden sind |
| `GetPackagesAsync_ShouldReturnPackages_WhenPackageDirectoriesExist` | `GetPackagesAsync()` | Gibt alle Paket-Verzeichnisse aufgelistet zurück |
| `GetPackageAsync_ShouldReturnNull_WhenPackageDoesNotExist` | `GetPackageAsync()` | Gibt null zurück, wenn angeforderten Paket nicht existiert |
| `GetPackageAsync_ShouldReturnPackageWithoutAgents_WhenAgentMdFilesExist` | `GetPackageAsync()` | Gibt `AgentPackageInfo` zurück; `Agenten`-Liste bleibt leer auch wenn `.agent.md`-Dateien vorhanden sind |
| `GetPackageAsync_ShouldReturnFilesList_WhenFilesExistInPackage` | `GetPackageAsync()` | Gibt die Dateien-Liste des Pakets korrekt zurück |
| `GetPackageAsync_ShouldListFiles_WhenAgentMdFilesExist` | `GetPackageAsync()` | Listet Dateien auch bei Anwesenheit von `.agent.md`-Dateien korrekt auf |

#### Setup / Teardown

- Jeder Test erstellt ein eigenes temporäres Verzeichnis
- `Dispose()` löscht das temporäre Verzeichnis nach jedem Test
- `IHostEnvironment` wird gemockt, um auf das temporäre Verzeichnis zu zeigen

#### Abhängigkeiten der Tests

- FluentAssertions (Assertions)
- Moq (Mocking von `IHostEnvironment` und `ILogger`)

---

## Integrationstests

### `AgentPackageFileServiceTests`

Datei: `src/Softwareschmiede.IntegrationTests/Services/AgentPackageFileServiceTests.cs`

Integrationstests für `AgentPackageFileService` mit echten Dateisystem-Operationen.

#### Paket-Tests

| Testmethode | Getestet | Beschreibung |
|-----------|----------|-------------|
| `CreatePackageAsync_ShouldCreateDirectory_WhenNameIsValid` | `CreatePackageAsync()` | Erstellt ein Paket-Verzeichnis mit gültigem Namen |
| `CreatePackageAsync_ShouldThrow_WhenNameContainsInvalidChars` | `CreatePackageAsync()` | Wirft `ArgumentException`, wenn Namen ungültige Zeichen enthalten |
| `RenamePackageAsync_ShouldRenameDirectory_WhenPackageExists` | `RenamePackageAsync()` | Benennt ein existierendes Paket um |
| `DeletePackageAsync_ShouldDeleteDirectory_WhenPackageExists` | `DeletePackageAsync()` | Löscht ein Paket komplett |

#### Verzeichnis-Tests

| Testmethode | Getestet | Beschreibung |
|-----------|----------|-------------|
| `CreateDirectoryAsync_ShouldCreateSubdirectory_WhenPathIsValid` | `CreateDirectoryAsync()` | Erstellt ein Unterverzeichnis im Paket |
| `CreateDirectoryAsync_ShouldThrow_WhenPathTraversalDetected` | `CreateDirectoryAsync()` | Wirft `InvalidOperationException` bei Path-Traversal-Versuchen (z.B. `../`) |
| `RenameDirectoryAsync_ShouldRename_WhenDirectoryExists` | `RenameDirectoryAsync()` | Benannt ein Verzeichnis um |
| `DeleteDirectoryAsync_ShouldDelete_WhenDirectoryExists` | `DeleteDirectoryAsync()` | Löscht ein Verzeichnis mit Inhalt |

#### Datei-Tests

| Testmethode | Getestet | Beschreibung |
|-----------|----------|-------------|
| `CreateEmptyFileAsync_ShouldCreateFile_WhenPathIsValid` | `CreateEmptyFileAsync()` | Erstellt eine leere Datei |
| `WriteFileAsync_ShouldWriteContent_WhenFileExists` | `WriteFileAsync()` | Schreibt Inhalt in eine Datei |
| `ReadFileAsync_ShouldReturnContent_WhenFileExists` | `ReadFileAsync()` | Liest den Inhalt einer Datei |
| `UploadFileAsync_ShouldSaveFile_WhenStreamProvided` | `UploadFileAsync()` | Speichert Dateiinhalt aus einem Stream |
| `RenameFileAsync_ShouldRenameFile_WhenFileExists` | `RenameFileAsync()` | Benannt eine Datei um |
| `DeleteFileAsync_ShouldDeleteFile_WhenFileExists` | `DeleteFileAsync()` | Löscht eine Datei |
| `WriteFileAsync_ShouldThrow_WhenPathTraversalDetected` | `WriteFileAsync()` | Wirft `InvalidOperationException` bei Path-Traversal-Versuchen |

#### Dateibaum-Tests

| Testmethode | Getestet | Beschreibung |
|-----------|----------|-------------|
| `GetFileTreeAsync_ShouldReturnPackages_WhenPackagesExist` | `BuildPackageTreeAsync()` | Erstellt einen `FileTreeNode` für ein einfaches Paket |
| `GetFileTreeAsync_ShouldReturnNestedStructure_WhenSubdirectoriesExist` | `BuildPackageTreeAsync()` | Erstellt eine hierarchische Struktur mit Unterverzeichnissen |

#### Setup / Teardown

- Jeder Test erstellt ein eigenes temporäres Verzeichnis
- `Dispose()` löscht das temporäre Verzeichnis nach jedem Test
- `IHostEnvironment` wird gemockt, um auf das temporäre Verzeichnis zu zeigen

#### Abhängigkeiten der Tests

- FluentAssertions (Assertions)
- Moq (Mocking von `IHostEnvironment`)
- `NullLogger<T>` (stille Logger-Instanz)

---

## Zusammenfassung

- **Insgesamt 19 Testmethoden** (5 Unit + 14 Integration)
- Alle Tests verwenden temporäre Dateisystem-Verzeichnisse (`Path.GetTempPath()`)
- Umfangreiche Tests für Path-Traversal-Sicherheit (2 dedizierte Tests)
- Tests decken Happy-Path und Error-Cases ab
