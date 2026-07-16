# Interfaces

## `IAgentPackageService`

Datei: `src/Softwareschmiede/Domain/Interfaces/IAgentPackageService.cs`

Service für Agentenpaket-Abfragen (read-only).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPackagesAsync` | `CancellationToken ct = default` | `Task<IEnumerable<AgentPackageInfo>>` | Gibt alle verfügbaren Agentenpakete zurück |
| `GetPackageAsync` | `string name`, `CancellationToken ct = default` | `Task<AgentPackageInfo?>` | Gibt ein spezifisches Agentenpaket nach Namen zurück oder null |

---

## `IAgentPackageFileService`

Datei: `src/Softwareschmiede/Domain/Interfaces/IAgentPackageFileService.cs`

Service zur Verwaltung von Agentenpaketen, Verzeichnissen und Dateien im Dateisystem.

### Paket-Verwaltung

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreatePackageAsync` | `string name`, `CancellationToken ct = default` | `Task<AgentPackageInfo>` | Erstellt ein neues Agentenpaket (Verzeichnis) |
| `RenamePackageAsync` | `string oldName`, `string newName`, `CancellationToken ct = default` | `Task` | Benennt ein Agentenpaket um |
| `DeletePackageAsync` | `string name`, `CancellationToken ct = default` | `Task` | Löscht ein Agentenpaket inkl. aller Inhalte |
| `BuildPackageTreeAsync` | `string packageName`, `CancellationToken ct = default` | `Task<FileTreeNode>` | Erstellt den kompletten Dateibaum eines Pakets |

### Verzeichnis-Verwaltung

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateDirectoryAsync` | `string packageName`, `string relativePath`, `CancellationToken ct = default` | `Task` | Erstellt ein neues Verzeichnis innerhalb eines Pakets |
| `RenameDirectoryAsync` | `string packageName`, `string relativeOldPath`, `string newName`, `CancellationToken ct = default` | `Task` | Benennt ein Verzeichnis um |
| `DeleteDirectoryAsync` | `string packageName`, `string relativePath`, `CancellationToken ct = default` | `Task` | Löscht ein Verzeichnis inkl. aller Inhalte |

### Datei-Verwaltung

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReadFileAsync` | `string packageName`, `string relativeFilePath`, `CancellationToken ct = default` | `Task<string>` | Liest den Textinhalt einer Datei |
| `WriteFileAsync` | `string packageName`, `string relativeFilePath`, `string content`, `CancellationToken ct = default` | `Task` | Schreibt/überschreibt den Textinhalt einer Datei |
| `CreateEmptyFileAsync` | `string packageName`, `string relativeFilePath`, `CancellationToken ct = default` | `Task` | Erstellt eine leere Datei |
| `UploadFileAsync` | `string packageName`, `string relativeDirectory`, `string fileName`, `Stream content`, `CancellationToken ct = default` | `Task` | Lädt eine Datei aus einem Stream hoch |
| `RenameFileAsync` | `string packageName`, `string relativeOldPath`, `string newName`, `CancellationToken ct = default` | `Task` | Benennt eine Datei um |
| `DeleteFileAsync` | `string packageName`, `string relativeFilePath`, `CancellationToken ct = default` | `Task` | Löscht eine Datei |
